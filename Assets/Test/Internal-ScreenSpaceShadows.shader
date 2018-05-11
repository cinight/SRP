// Unity built-in shader source. Copyright (c) 2016 Unity Technologies. MIT license (see license.txt)

// Collects cascaded shadows into screen space buffer
Shader "Hidden/MyInternal-ScreenSpaceShadows" 
{
	Properties 
	{
		_ShadowMap ("", any) = "" {}
	}

	CGINCLUDE




	ENDCG


	// ----------------------------------------------------------------------------------------
	// Subshader for hard shadows:
	// Just collect shadows into the buffer. Used on pre-SM3 GPUs and when hard shadows are picked.

	SubShader 
	{
		Tags{ "ShadowmapFilter" = "HardShadow" }
		Pass 
		{
			ZWrite Off ZTest Always Cull Off

			CGPROGRAM
			#pragma vertex vert
			#pragma fragment frag_hard
			//#pragma multi_compile_shadowcollector
			#include "UnityCG.cginc"
			#include "UnityShadowLibrary.cginc"

			//Debug
			SamplerState my_point_clamp_sampler;
			UNITY_DECLARE_DEPTH_TEXTURE(_CameraDepthTexture);
			UNITY_DECLARE_SHADOWMAP(_ShadowMap);

			struct appdata
			{
				float4 vertex : POSITION;
				float2 texcoord : TEXCOORD0;
				float3 ray : TEXCOORD1;
			};

			struct v2f
			{
				float4 pos : SV_POSITION;
				float4 uv : TEXCOORD0; // xy uv / zw screenpos
				float3 ray : TEXCOORD1; // View space ray, for perspective case
				// Orthographic view space positions (need xy as well for oblique matrices)
				float3 orthoPosNear : TEXCOORD2;
				float3 orthoPosFar  : TEXCOORD3;
			};

			v2f vert(appdata v)
			{
				v2f o;

				float4 clipPos = UnityObjectToClipPos(v.vertex);
				o.pos = clipPos;
				o.uv.xy = v.texcoord;
				o.uv.zw = ComputeNonStereoScreenPos(clipPos);
				o.ray = v.ray;

				// To compute view space position from Z buffer for orthographic case,
				// we need different code than for perspective case. We want to avoid
				// doing matrix multiply in the pixel shader: less operations, and less
				// constant registers used. Particularly with constant registers, having
				// unity_CameraInvProjection in the pixel shader would push the PS over SM2.0
				// limits.
				clipPos.y *= _ProjectionParams.x;
				float3 orthoPosNear = mul(unity_CameraInvProjection, float4(clipPos.x, clipPos.y, -1, 1)).xyz;
				float3 orthoPosFar = mul(unity_CameraInvProjection, float4(clipPos.x, clipPos.y, 1, 1)).xyz;
				orthoPosNear.z *= -1;
				orthoPosFar.z *= -1;
				o.orthoPosNear = orthoPosNear;
				o.orthoPosFar = orthoPosFar;

				return o;
			}

			fixed4 frag_hard (v2f i) : SV_Target
			{

				float zdepth = SAMPLE_DEPTH_TEXTURE(_CameraDepthTexture, i.uv.xy);

				// 0..1 linear depth, 0 at camera, 1 at far plane.
				float depth = lerp(Linear01Depth(zdepth), zdepth, unity_OrthoParams.w);
				#if defined(UNITY_REVERSED_Z)
							zdepth = 1 - zdepth;
				#endif

				// view position calculation for perspective & ortho cases
				float3 vposPersp = i.ray * depth;
				float3 vposOrtho = lerp(i.orthoPosNear, i.orthoPosFar, zdepth);
				// pick the perspective or ortho position as needed
				float3 camPos = lerp(vposPersp, vposOrtho, unity_OrthoParams.w);
				camPos = vposOrtho;

				float3 vpos = camPos.xyz;
				float4 wpos = mul (unity_CameraToWorld, float4(vpos,1));

				float4 shadowCoord = float4(mul(unity_WorldToShadow[0], wpos).xyz, 0);

				//1 tap hard shadow
				fixed shadow = UNITY_SAMPLE_SHADOW(_ShadowMap, shadowCoord);
				//shadow = lerp(_LightShadowData.r, 1.0, shadow);

				float4 d = 0.5f;
				//d = _ShadowMapTexture.Sample(my_point_clamp_sampler, i.uv.xy);
				//d = shadowCoord;
				d.rgb = shadow;
				d.a = 1;
				return d;
			}

			ENDCG
		}
	}
}
