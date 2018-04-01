// Upgrade NOTE: replaced 'mul(UNITY_MATRIX_MVP,*)' with 'UnityObjectToClipPos(*)'

Shader "FTPCustom/SRP06/Opaque Unlit Shadow"
{
	Properties
	{
		_MainTex ("_MainTex (RGBA)", 2D) = "white" {}
		_Color("Main Color", Color) = (1,1,1,1)
	}
	SubShader
	{
		CGINCLUDE

			// Global lighting data (setup from C# code once per frame).
			CBUFFER_START(GlobalLightData)
				// The variables are very similar to built-in unity_LightColor, unity_LightPosition,
				// unity_LightAtten, unity_SpotDirection as used by the VertexLit shaders, except here
				// we use world space positions instead of view space.
				half4 globalLightColor[8];
				float4 globalLightPos[8];
				float4 globalLightSpotDir[8];
				float4 globalLightAtten[8];
				int4  globalLightCount;
				// Global ambient/SH probe, similar to unity_SH* built-in variables.
				float4 globalSH[7];
			CBUFFER_END

		ENDCG

		Pass
		{
			Tags { "RenderType"="Opaque" "LightMode" = "BasicPass" }
			//Tags {"LightMode" = "ForwardBase" }

			CGPROGRAM
			#pragma vertex vert
			#pragma fragment frag
			//#pragma multi_compile_fwdbase

			#include "UnityCG.cginc"
			#include "AutoLight.cginc"

			struct appdata
			{
				float4 vertex : POSITION;
				float2 uv : TEXCOORD0;
				float3 normal : NORMAL;
			};

			struct v2f
			{
				float2 uv : TEXCOORD0;
				float4 pos : SV_POSITION;
				float3 normal : NORMAL;
				LIGHTING_COORDS(0,1)
			};

			sampler2D _MainTex;
			float4 _MainTex_ST;
			float4 _Color;
	
			v2f vert (appdata v)
			{
				v2f o;
				o.pos = UnityObjectToClipPos(v.vertex);
				o.uv = TRANSFORM_TEX(v.uv, _MainTex);
				o.normal = UnityObjectToWorldNormal( v.normal );
				o.normal = normalize(o.normal);
				TRANSFER_VERTEX_TO_FRAGMENT(o);
				return o;
			}
			
			fixed4 frag (v2f i) : SV_Target
			{
				fixed4 col = tex2D(_MainTex, i.uv) * _Color;

				float shadowattenuation = 1;

				for (int j = 0; j < globalLightCount.x; ++j)
				{
					//shadowattenuation = 1 - globalLightAtten[j];

					float3 lightDir = globalLightPos[j].xyz;
					float3 light = saturate(dot(lightDir, i.normal)) * globalLightColor[j];

					col.rgb *= shadowattenuation;
					col.rgb += light;
				}

				return col;
			}
			ENDCG
		}
		//========================================================================================
        Pass
     	{
			Name "ShadowCaster"
			Tags{ "Queue" = "Transparent" "LightMode" = "ShadowCaster"  }

			Blend SrcAlpha OneMinusSrcAlpha

         	CGPROGRAM
 			#pragma vertex vert
			#pragma fragment frag
			//#pragma multi_compile_shadowcaster
			#pragma fragmentoption ARB_precision_hint_fastest
 
			#include "UnityCG.cginc"
 
			struct appdata
			{
				float4 position : POSITION;
				float3 normal : NORMAL;
			};

			struct v2f
			{
				//V2F_SHADOW_CASTER;
				float4 pos : SV_POSITION;
			};

			float4 MyClipSpaceShadowCasterPos (float3 vertex, float3 normal, float3 light) 
			{
				float4 clipPos;
				
				// Important to match MVP transform precision exactly while rendering
				// into the depth texture, so branch on normal bias being zero.
				//if (unity_LightShadowBias.z != 0.0) 
				//{
					float3 wPos = mul(unity_ObjectToWorld, float4(vertex,1)).xyz;
					float3 wNormal = UnityObjectToWorldNormal(normal);
					float3 wLight = light; //normalize(UnityWorldSpaceLightDir(wPos));

				// apply normal offset bias (inset position along the normal)
				// bias needs to be scaled by sine between normal and light direction
				// (http://the-witness.net/news/2013/09/shadow-mapping-summary-part-1/)
				//
				// unity_LightShadowBias.z contains user-specified normal offset amount
				// scaled by world space texel size.

					float shadowCos = dot(wNormal, wLight);
					float shadowSine = sqrt(1 - shadowCos * shadowCos);
					float normalBias = max(0.01f, unity_LightShadowBias.z) * shadowSine;

					wPos -= wNormal * normalBias;

					clipPos = mul(UNITY_MATRIX_VP, float4(wPos, 1));
				//}
				//else 
				//{
				//	clipPos = UnityObjectToClipPos(vertex);
				//}
				return clipPos;
			}

			v2f vert(appdata v)
			{
				v2f o;

				for (int j = 0; j < globalLightCount.x; ++j)
				{
					float3 lightDir = normalize(globalLightPos[j].xyz);
					float4 position = MyClipSpaceShadowCasterPos(v.position.xyz, v.normal, lightDir);
					o.pos = position;// UnityApplyLinearShadowBias(position);
				}

				return o;
			}
 
			float4 frag(v2f i) : COLOR
			{
				return float4(0,0,0,0.8);
				//SHADOW_CASTER_FRAGMENT(i)
			}
 
         	ENDCG
    	}
	}
    FallBack Off
}
