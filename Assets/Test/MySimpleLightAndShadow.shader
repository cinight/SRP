Shader "LightStudy/09_ForwardBaseDiffuseSpecular"
{
	Properties
	{
		_Color ("Main Color", Color) = (1,1,1,1)
		_MainTex ("Texture", 2D) = "white" {}
		_Smoothness("Smoothness", Range(0, 1)) = 0.5
		[HDR] _SpecularTint("Specular", Color) = (0.5, 0.5, 0.5)
		_LightColor("Light Color", Color) = (1,1,1,1)
		_ShadowColor("Shadow Color", Color) = (1,1,1,1)
	}
	SubShader
	{
		Pass
		{
			Tags { "RenderType"="Opaque" "LightMode" = "ForwardBase" }

			CGPROGRAM
			#pragma vertex vert
			#pragma fragment frag
			#include "UnityStandardBRDF.cginc"
			#include "UnityCG.cginc"

			struct appdata
			{
				float4 vertex : POSITION;
				float2 uv : TEXCOORD0;
				float3 normal : NORMAL;
			};

			struct v2f
			{
				float4 vertex : SV_POSITION;
				float2 uv : TEXCOORD0;
				float3 normal : NORMAL;
				float4 _ShadowCoord : TEXCOORD1;
				float3 viewDir : TEXCOORD2;
			};

			sampler2D _MainTex;
			float4 _MainTex_ST;
			float _Smoothness;
			float4 _SpecularTint;
			float4 _Color;
			float4 _ShadowColor;
			float4 _LightColor;
			sampler2D _ShadowMapTexture;
			
			v2f vert (appdata v)
			{
				v2f o;

				o.vertex = UnityObjectToClipPos(v.vertex);
				o.uv = TRANSFORM_TEX(v.uv, _MainTex);
				o.normal = UnityObjectToWorldNormal(v.normal);

				//Specular
				float3 worldPos = mul(unity_ObjectToWorld, v.vertex);
				o.viewDir = normalize(_WorldSpaceCameraPos - worldPos);

				//Shadow
				o._ShadowCoord = ComputeScreenPos(o.vertex);

				return o;
			}
			
			fixed4 frag (v2f i) : SV_Target
			{
				fixed4 col = tex2D(_MainTex, i.uv) * _Color;

				//Light
				i.normal = normalize(i.normal);
				float3 lightDir = _WorldSpaceLightPos0.xyz;
				float4 lightColor = _LightColor0;
				float lightFactor = DotClamped(lightDir, i.normal);
				float4 light = lerp(  0,_LightColor , lightFactor* _LightColor.a) ;

				//Specular
				float3 halfVector = normalize(lightDir + i.viewDir);
				float4 specular = DotClamped(halfVector, i.normal);
				specular = pow(specular, _Smoothness * 100);
				specular *= _SpecularTint;

				//Shadow
				float attenuation = tex2Dproj(_ShadowMapTexture, i._ShadowCoord).r;
				float4 shadow = lerp( _ShadowColor , 1, attenuation);

				col.rgb *= shadow;
				col.rgb *= 1 - specular.rgb;
				col.a = 1;

				return col + light + specular;
			}
			ENDCG
		}
		//========================================================================================
        Pass
     	{
			Name "ShadowCaster"
			Tags{ "Queue" = "Transparent" "LightMode" = "ShadowCaster" }

         	CGPROGRAM
 			#pragma vertex vert
			#pragma fragment frag
			#pragma multi_compile_shadowcaster
			#pragma fragmentoption ARB_precision_hint_fastest
 
			#include "UnityCG.cginc"
 
			struct v2f
			{
				float4 pos : SV_POSITION;
			};
 
			v2f vert(appdata_base v)
			{
				v2f o;

				float4 wPos = mul(unity_ObjectToWorld, v.vertex);
				float3 wNormal = normalize(mul(v.normal, (float3x3)unity_WorldToObject));

					float3 wLight = normalize(_WorldSpaceLightPos0.xyz);

					float shadowCos = dot(wNormal, wLight);
					float shadowSine = sqrt(1-shadowCos*shadowCos);
					//float normalBias = unity_LightShadowBias.z * shadowSine;
					float normalBias = 0.01f * shadowSine;

					wPos.xyz -= wNormal * normalBias;

				o.pos = mul(UNITY_MATRIX_VP, wPos);
				o.pos = UnityApplyLinearShadowBias(o.pos);

				return o;
			}
 
			float4 frag(v2f i) : COLOR
			{
				return 0;
			}
 
         	ENDCG
    	}
	}
}
