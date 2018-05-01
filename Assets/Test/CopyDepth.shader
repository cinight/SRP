Shader "Hidden/MyTestCopyDepth"
{
    Properties
    {
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "LightweightPipeline"}

        Pass
        {
            Name "Default"
            ZTest Always ZWrite On ColorMask 0

            CGPROGRAM

            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile __ _FLIPUV

            #include "UnityCG.cginc"

            struct VertexInput
            {
                float4 vertex   : POSITION;
                float2 uv       : TEXCOORD0;
            };

            struct VertexOutput
            {
                float4 position : SV_POSITION;
                float2 uv       : TEXCOORD0;
            };

            sampler2D _CameraDepthTexture;

            VertexOutput vert(VertexInput i)
            {
                VertexOutput o;
                o.uv = i.uv;
                o.position = UnityObjectToClipPos(i.vertex.xyz);
                return o;
            }

            float frag(VertexOutput i) : SV_Depth
            {
                float2 uv = i.uv;

                #if defined(UNITY_UV_STARTS_AT_TOP) && !defined(_FLIPUV)
					uv.y = 1-uv.y;
				#endif

                return tex2D(_CameraDepthTexture, uv);
            }

            ENDCG
        }
    }
}
