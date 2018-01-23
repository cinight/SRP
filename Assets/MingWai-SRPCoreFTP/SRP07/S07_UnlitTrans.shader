Shader "FTPCustom/SRP07/Transparent Unlit"
{
	Properties
	{
		[Enum(UnityEngine.Rendering.BlendMode)] _SrcBlend("Source", Int) = 1.0
		[Enum(UnityEngine.Rendering.BlendMode)] _DstBlend("Destination", Int) = 1.0
		[Enum(UnityEngine.Rendering.BlendOp)] _BlendOp("BlendOp", Int) = 1.0
		_ColorMultiplier("Alpha Multiplier", Range(1,5)) = 1
		_Color("Main Color", Color) = (1,1,1,1)
		_TintColor("Tint Color", Color) = (0,0,0,0)
		[Enum(Off,0,Front,1,Back,2)] _CullMode ("Culling Mode", int) = 0
		_MainTex ("_MainTex (RGBA)", 2D) = "white" {}
	}
	SubShader
	{
		Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" }
		Blend[_SrcBlend][_DstBlend] //Additive
		BlendOp[_BlendOp]

		Cull [_CullMode] Lighting Off ZWrite Off

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
				fixed4 color : COLOR;
			};

			struct v2f
			{
				float2 uv : TEXCOORD0;
				float4 vertex : SV_POSITION;
				fixed4 color : COLOR;
			};

			sampler2D _MainTex;
			float4 _MainTex_ST;
			float4 _Color;
			float4 _TintColor;
			fixed _ColorMultiplier;
			
			v2f vert (appdata v)
			{
				v2f o;
				o.vertex = UnityObjectToClipPos(v.vertex);
				o.uv = TRANSFORM_TEX(v.uv, _MainTex);
				o.color = v.color;
				return o;
			}
			
			fixed4 frag (v2f i) : SV_Target
			{
				fixed4 col = tex2D(_MainTex, i.uv) * _Color + _TintColor;

				col.rgb *= i.color.rgb;
				
				col *= col.a; //premultiplied

				col.a *= _ColorMultiplier;

				return col;
			}
			ENDCG
		}
	}
}
