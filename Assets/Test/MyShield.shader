// Unity built-in shader source. Copyright (c) 2016 Unity Technologies. MIT license (see license.txt)

Shader "Shield 2" 
{
    Properties 
    {
        _MainTex ("Particle Texture", 2D) = "white" {}
        _InvFade ("Soft Particles Factor", Range(0.01,3.0)) = 1.0

        _EdgeAroundColor("Edge Color", Color) = (1,1,1,1)
        _EdgeAroundPower("Edge Color Power",Range(1,10)) = 1
    }

    SubShader 
    {
        Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" }
        Blend SrcAlpha OneMinusSrcAlpha
        Cull Back Lighting Off ZWrite Off

        Pass 
        {

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 2.0

            #include "UnityCG.cginc"


            struct appdata_t 
            {
                float4 vertex : POSITION;
                float2 texcoord : TEXCOORD0;
            };

            struct v2f 
            {
                float4 vertex : SV_POSITION;
                float2 texcoord : TEXCOORD0;
                float4 projPos : TEXCOORD2;
            };

            v2f vert (appdata_t v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.projPos = ComputeScreenPos (o.vertex);

                COMPUTE_EYEDEPTH(o.projPos.z);
                o.texcoord = v.texcoord;

                return o;
            }

            sampler2D _CameraDepthTexture;
            float _InvFade;
            sampler2D _MainTex;
       
            fixed4 _TintColor;
			fixed4 _EdgeAroundColor;
			fixed _EdgeAroundPower;

            fixed4 frag (v2f i) : SV_Target
            {
				half4 col = tex2D(_MainTex, i.texcoord);

                    float sceneZ = LinearEyeDepth (SAMPLE_DEPTH_TEXTURE_PROJ(_CameraDepthTexture, UNITY_PROJ_COORD(i.projPos)));
                    float partZ = i.projPos.z;
                    float fZ = (sceneZ-partZ);
                    float fade = saturate (_InvFade * fZ);
                    col.a *= fade;

                    float edgearound = pow( fade *_EdgeAroundColor.a, _EdgeAroundPower);
                    col.rgb = lerp( _EdgeAroundColor.rgb, col.rgb, edgearound);

                    //float depth = tex2D(_CameraDepthTexture, i.texcoord).r * 10;
                    //float4 c = float4(depth, 0 ,0,1);

                return col;
            }
            ENDCG
        }
    }
}

