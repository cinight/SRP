Shader "Hidden/My/ScreenSpaceShadows"
{
    SubShader
    {
        Tags{ "RenderPipeline" = "ForwardBase" "IgnoreProjector" = "True"}

        CGINCLUDE

        struct VertexInput
        {
            float4 vertex   : POSITION;
            float2 texcoord : TEXCOORD0;
        };

        struct Interpolators
        {
            float4  pos      : SV_POSITION;
            float4  texcoord : TEXCOORD0;
        };

        Interpolators Vertex(VertexInput i)
        {
            Interpolators o;

            o.pos = UnityObjectToClipPos(i.vertex.xyz);

            float4 projPos = o.pos * 0.5;
            projPos.xy = projPos.xy + projPos.w;

            float4 scaleOffset = 1;
            float2 suv = saturate(i.texcoord);
            o.texcoord.xy = suv.xy * scaleOffset.xy + scaleOffset.zw * 1.0;
            o.texcoord.zw = projPos.xy;

            return o;
        }

        float4x4  _WorldToShadow; //only directional light, and one for avoid issue
        half _ShadowStrength;
        sampler2D _MyShadowMap;
        sampler2D _CameraDepthTexture;

        half4 Fragment(Interpolators i) : SV_Target
        {

            float deviceDepth = tex2D(_CameraDepthTexture, i.texcoord.xy).r;

            #if UNITY_REVERSED_Z
                deviceDepth = 1 - deviceDepth;
            #endif
            deviceDepth = 2 * deviceDepth - 1; //NOTE: Currently must massage depth before computing CS position.

            //View
            float4 positionCS = float4(i.texcoord.zw * 2.0 - 1.0, deviceDepth, 1.0);
            #if UNITY_UV_STARTS_AT_TOP
                positionCS.y = -positionCS.y;
            #endif
            float4 positionVS = mul(unity_CameraInvProjection, positionCS);
            positionVS.z = -positionVS.z;
            float3 vpos = positionVS.xyz / positionVS.w;

            //World
            float3 wpos = mul(unity_CameraToWorld, float4(vpos, 1)).xyz;

            //Fetch shadow coordinates for cascade.
            float4 coords  = mul(_WorldToShadow, float4(wpos, 1.0)); //no cascade

            // Screenspace shadowmap is only used for directional lights which use orthogonal projection.
            coords.xyz /= coords.w;
            float attenuation = tex2D(_MyShadowMap, coords.xy).r; //textureName.SampleCmpLevelZero(samplerName, (coord3).xy, (coord3).z)
            float oneMinusT = 1.0 - _ShadowStrength;
            attenuation = oneMinusT + attenuation * _ShadowStrength;

            return attenuation;
            //#if UNITY_REVERSED_Z
            //    if(coords.z <= 0) return 1.0;
            //    else return attenuation;
            //#else
             //   if(coords.z >= 0) return 1.0;
            //    else return attenuation;
            //#endif
        }

        ENDCG

        Pass
        {
            Name "Default"
            ZTest Always
            ZWrite Off
            Cull Off

            CGPROGRAM

            #pragma vertex   Vertex
            #pragma fragment Fragment
            ENDCG
        }
    }
}
