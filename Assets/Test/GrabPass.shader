Shader "GrabPass"
{
	Properties
	{
		_Color("Main Color", Color) = (1,1,1,1)
		_MainTex ("_MainTex (RGBA)", 2D) = "white" {}
		_Noise("Noise", Range(0, 1)) = 1
	}

	SubShader
	{
		Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" }
		Blend SrcAlpha OneMinusSrcAlpha

		Cull [_CullMode] Lighting Off ZWrite Off

		Pass
		{
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
				float4 grabPos : TEXCOORD1;
			};

			sampler2D _GrabOpaqueTexture;
			sampler2D _MainTex;
			float4 _MainTex_ST;
			float4 _Color;
			float _Noise;
			
			v2f vert (appdata v)
			{
				v2f o;
				o.vertex = UnityObjectToClipPos(v.vertex);
				o.uv = TRANSFORM_TEX(v.uv, _MainTex);
				o.grabPos = ComputeGrabScreenPos(o.vertex);

				return o;
			}

			fixed4 frag (v2f i) : SV_Target
			{
				fixed4 bguv = i.grabPos;
				float2 nuv = bguv.xy / bguv.w;
				
				//#ifdef UNITY_UV_STARTS_AT_TOP
				//	nuv.y = 1-nuv.y;
				//#endif
				
				//Distoriton
				float2 st = nuv;
				float noise = sin(nuv*10.0f);
				nuv = lerp(nuv-noise*_Noise,nuv+noise*_Noise,_SinTime.w);

				float4 bg = tex2D(_GrabOpaqueTexture, nuv); //The background texture

				float4 col = tex2D(_MainTex,i.uv);
				col.rgb = 1-bg.rgb;
				col *= _Color;
				col.a = saturate(col.a);

				return col;
			}
			ENDCG
		}
	}
}
