Shader "M&B/M&B_Additive"
{
    Properties
    {
        [Header(Textures)]
        _MainTex ("Diffuse A (RGB)", 2D) = "white" {}

        [Header(Material Properties)]
        _Color ("Color Tint", Color) = (1, 1, 1, 1)
        _Intensity ("Intensity", Range(0, 4)) = 1.0

        [Header(Warband Rendering)]
        [Toggle] _UseVertexColor ("Vertex Colors", Float) = 1
        [Toggle] _SoftParticles ("Soft Particles", Float) = 0
        _SoftFactor ("Soft Factor", Range(0.01, 3)) = 1.0

        [Header(Internal)]
        _BrfFlags ("BRF Flags (read-only)", Float) = 0
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent+100" "IgnoreProjector" = "True" }
        LOD 100

        Blend One One
        ZWrite Off
        Cull Off
        Lighting Off
        Fog { Mode Off }

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #pragma shader_feature_local _ _USEVERTEXCOLOR_ON
            #pragma shader_feature_local _ _SOFTPARTICLES_ON

            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
                #if defined(_SOFTPARTICLES_ON)
                float4 screenPos : TEXCOORD1;
                #endif
                UNITY_VERTEX_OUTPUT_STEREO
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;
            fixed4 _Color;
            half _Intensity;
            half _SoftFactor;

            UNITY_DECLARE_DEPTH_TEXTURE(_CameraDepthTexture);

            v2f vert(appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);

                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                o.color = v.color;

                #if defined(_SOFTPARTICLES_ON)
                o.screenPos = ComputeScreenPos(o.pos);
                COMPUTE_EYEDEPTH(o.screenPos.z);
                #endif

                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                fixed4 tex = tex2D(_MainTex, i.uv);

                fixed4 col = tex * _Color * _Intensity;

                #if defined(_USEVERTEXCOLOR_ON)
                    col *= i.color;
                #endif

                #if defined(_SOFTPARTICLES_ON)
                {
                    float sceneZ = LinearEyeDepth(
                        SAMPLE_DEPTH_TEXTURE_PROJ(_CameraDepthTexture, UNITY_PROJ_COORD(i.screenPos)));
                    float partZ = i.screenPos.z;
                    float fade = saturate(_SoftFactor * (sceneZ - partZ));
                    col *= fade;
                }
                #endif

                return col;
            }
            ENDCG
        }
    }

    FallBack Off
    CustomEditor "MBShaderGUI"
}
