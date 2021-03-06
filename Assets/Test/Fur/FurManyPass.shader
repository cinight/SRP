﻿Shader "Unlit/FurManyPass"
{
	Properties
	{
		_MainTex ("Main Texture", 2D) = "white" {}
		[NoScaleOffset] _NoiseTex ("Noise Texture", 2D) = "white" {}
		_FurLayer ("Fur Layer", Range(0.1,1)) = 1
		_FurFactor ("Fur Factor", Range(1,10)) = 1
	}
	SubShader
	{
		Tags { "RenderType"="Opaque" }

		Pass
		{
			CGPROGRAM
			#define FURLAYER 0.0
			#pragma vertex vert
			#pragma fragment frag
			#include "FurManyPass.cginc"
			ENDCG
		}
		Pass
		{
			CGPROGRAM
			#define FURLAYER 0.01
			#pragma vertex vert
			#pragma fragment frag
			#include "FurManyPass.cginc"
			ENDCG
		}
		Pass
		{
			CGPROGRAM
			#define FURLAYER 0.02
			#pragma vertex vert
			#pragma fragment frag
			#include "FurManyPass.cginc"
			ENDCG
		}
		Pass
		{
			CGPROGRAM
			#define FURLAYER 0.03
			#pragma vertex vert
			#pragma fragment frag
			#include "FurManyPass.cginc"
			ENDCG
		}
		Pass
		{
			CGPROGRAM
			#define FURLAYER 0.04
			#pragma vertex vert
			#pragma fragment frag
			#include "FurManyPass.cginc"
			ENDCG
		}
		Pass
		{
			CGPROGRAM
			#define FURLAYER 0.05
			#pragma vertex vert
			#pragma fragment frag
			#include "FurManyPass.cginc"
			ENDCG
		}
		Pass
		{
			CGPROGRAM
			#define FURLAYER 0.06
			#pragma vertex vert
			#pragma fragment frag
			#include "FurManyPass.cginc"
			ENDCG
		}
		Pass
		{
			CGPROGRAM
			#define FURLAYER 0.07
			#pragma vertex vert
			#pragma fragment frag
			#include "FurManyPass.cginc"
			ENDCG
		}
		Pass
		{
			CGPROGRAM
			#define FURLAYER 0.08
			#pragma vertex vert
			#pragma fragment frag
			#include "FurManyPass.cginc"
			ENDCG
		}
		Pass
		{
			CGPROGRAM
			#define FURLAYER 0.09
			#pragma vertex vert
			#pragma fragment frag
			#include "FurManyPass.cginc"
			ENDCG
		}
	}
}
