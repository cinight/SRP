Shader "Hidden/My/ScreenSpaceShadows"
{
    SubShader
    {
        Tags{ "RenderPipeline" = "ForwardBase" "IgnoreProjector" = "True"}

        Pass
        {
            Name "Default"
            ZTest Always
            ZWrite Off
            Cull Off

            CGPROGRAM

            #pragma vertex   Vertex
            #pragma fragment Fragment
            #pragma multi_compile __ _FLIPUV

            #include "UnityCG.cginc"
			#include "UnityShadowLibrary.cginc"


            struct VertexInput
            {
                float4 vertex   : POSITION;
                float4 texcoord : TEXCOORD0;
            };

            struct Interpolators
            {
                float4  pos      : SV_POSITION;
                float4  texcoord : TEXCOORD0;
            };

            Interpolators Vertex(VertexInput v)
            {
                Interpolators o;

                o.pos = UnityObjectToClipPos(v.vertex.xyz);
                o.texcoord = v.texcoord;

                return o;
            }

            half _ShadowStrength;
            UNITY_DECLARE_SHADOWMAP(_ShadowMapTexture);
            UNITY_DECLARE_DEPTH_TEXTURE(_CameraDepthTexture);

            half4 Fragment(Interpolators i) : SV_Target
            {
                //uv
                float2 uv = i.texcoord.xy;
                #if defined(UNITY_UV_STARTS_AT_TOP) && !defined(_FLIPUV)
                        uv.y = 1- uv.y;
                #endif

                //depth
                float zdepth = tex2D(_CameraDepthTexture, uv).r;
                zdepth = Linear01Depth(zdepth);

                //View
                float4 positionCS = float4(uv,zdepth,1.0);
                float4 positionVS = mul(unity_CameraInvProjection, positionCS);
                positionVS.z = -positionVS.z;
                float3 vpos = positionVS.xyz / positionVS.w;

                //World
                float3 wpos = mul(unity_CameraToWorld, float4(vpos, 1)).xyz;

                //Fetch shadow coordinates for cascade.
                float4 coords  = mul(unity_WorldToShadow[0], float4(wpos, 1.0));
                coords.xyz /= coords.w;

                float attenuation = UNITY_SAMPLE_SHADOW(_ShadowMapTexture, coords);
                attenuation = lerp(1.0f-_ShadowStrength,1,attenuation);

                return attenuation;
            }

            ENDCG
        }
    }
}
