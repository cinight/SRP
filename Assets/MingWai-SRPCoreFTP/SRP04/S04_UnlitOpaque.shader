Shader "FTPCustom/SRP04/Opaque Unlit"
{
	Properties
	{
		_MainTex ("_MainTex (RGBA)", 2D) = "white" {}
		_Color("Main Color", Color) = (1,1,1,1)
	}
	SubShader
	{
		Tags { "RenderType"="Opaque" }
		LOD 200

		//RT0 - Albedo - final binded surface color
		//RT1 - Emission
		//DEPTH - Depth

		Pass
		{
			Tags { "LightMode" = "BasicPass" }

			CGPROGRAM
			#pragma vertex vert
			#pragma fragment frag

			#include "UnityCG.cginc"

			struct appdata
			{
				float4 vertex : POSITION;
				float2 uv : TEXCOORD0;
			};

			struct v2f
			{
				float2 uv : TEXCOORD0;
				float4 vertex : SV_POSITION;
			};

			struct RTstruct
			{
				float4 Albedo : SV_Target0;
				float4 Emission : SV_Target1;
			};

			sampler2D _MainTex;
			float4 _MainTex_ST;
			float4 _Color;
			
			v2f vert (appdata v)
			{
				v2f o;
				o.vertex = UnityObjectToClipPos(v.vertex);
				o.uv = TRANSFORM_TEX(v.uv, _MainTex);
				return o;
			}
			
			void frag (v2f i, inout RTstruct o  )
			{
				fixed4 col = tex2D(_MainTex, i.uv);

				o.Albedo = col;
				o.Emission = frac(float4(_Time.x, _Time.y, _Time.z, _Time.w));
			}
			ENDCG
		}

		Pass
		{
			Tags { "LightMode" = "AddPass" }

			CGPROGRAM
			#pragma vertex vert
			#pragma fragment frag

			#include "UnityCG.cginc"
			#include "HLSLSupport.cginc"

			struct appdata
			{
				float4 vertex : POSITION;
			};

			struct v2f
			{
				float4 vertex : SV_POSITION;
				float4 fbUV : TEXCOORD0;
			};

			struct RTstruct
			{
				float4 Albedo : SV_Target0;
				float4 Emission : SV_Target1;
			};
			
			v2f vert (appdata v)
			{
				v2f o;
				o.vertex = UnityObjectToClipPos(v.vertex);
				o.fbUV.xy = ComputeScreenPos(o.vertex);
				o.fbUV.zw = float2(0,1);
				return o;
			}
			
			UNITY_DECLARE_FRAMEBUFFER_INPUT_FLOAT(0);

			void frag (v2f i, inout RTstruct o )
			{
				float4 uv = i.fbUV;
				float4 col = UNITY_READ_FRAMEBUFFER_INPUT(0, uv);
				o.Albedo += col;
				o.Emission += col;
			}
			ENDCG
		}
	}
}
