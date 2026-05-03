// Warband Particle Shader - Alpha Blend (Modulate)
// Uses combined mesh approach (RGL-style):
//   - Per-vertex colors baked into vertex data by WarbandParticleRenderer
//   - Vertices are already in world space (transformed on CPU)
//   - Alpha blend: SrcAlpha OneMinusSrcAlpha
//   - Gamma correction for non-additive path
//   - Soft particle depth fade (optional, via depth texture)

Shader "M&B/Particles/AlphaBlend"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _TintColor ("Tint Color (= vMaterialColor)", Color) = (1, 1, 1, 1)

        [Header(Soft Particles)]
        [Toggle(_SOFT_PARTICLES)] _SoftParticles ("Enable Soft Particles", Float) = 0
        _SoftFactor ("Soft Factor", Range(256, 8192)) = 4096

        [Header(Fog)]
        [Toggle(_FOG_ON)] _FogEnabled ("Enable Fog", Float) = 0
        _FogDensity ("Fog Density", Range(0.001, 0.1)) = 0.02
        _FogColor ("Fog Color", Color) = (0.7, 0.75, 0.8, 1)

        [Header(Rendering)]
        [Toggle(_GAMMA_CORRECTION)] _GammaCorrection ("Gamma Correction", Float) = 0
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "IgnoreProjector" = "True"
            "RenderType" = "Transparent"
            "PreviewType" = "Plane"
        }

        // Alpha blend (modulate) - matches rgl soft_particle_modulate
        Blend SrcAlpha OneMinusSrcAlpha
        ColorMask RGB
        Cull Off
        Lighting Off
        ZWrite Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #pragma shader_feature_local _SOFT_PARTICLES
            #pragma shader_feature_local _FOG_ON
            #pragma shader_feature_local _GAMMA_CORRECTION

            #include "UnityCG.cginc"
            #include "MB_Particle_Common.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;       // Per-vertex color from combined mesh
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
                #ifdef _SOFT_PARTICLES
                    float4 projCoord : TEXCOORD1;
                    float depth : TEXCOORD2;
                #endif
                #ifdef _FOG_ON
                    float fogAmount : TEXCOORD3;
                #endif
                UNITY_VERTEX_OUTPUT_STEREO
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;
            float4 _TintColor;

            #ifdef _FOG_ON
                float4 _FogColor;
            #endif

            v2f vert(appdata v)
            {
                v2f o;

                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);

                // Standard MVP transform - vertices are in local space
                float4 worldPos = mul(unity_ObjectToWorld, v.vertex);
                o.pos = UnityObjectToClipPos(v.vertex);

                o.uv = TRANSFORM_TEX(v.uv, _MainTex);

                // Per-vertex color * material tint (RGL: vColor * vMaterialColor)
                o.color = v.color * _TintColor;

                #ifdef _SOFT_PARTICLES
                {
                    o.projCoord = ComputeScreenPos(o.pos);
                    COMPUTE_EYEDEPTH(o.depth);
                }
                #endif

                #ifdef _FOG_ON
                {
                    float3 viewPos = mul(UNITY_MATRIX_V, worldPos).xyz;
                    o.fogAmount = ComputeWarbandFog(length(viewPos), worldPos.y);
                }
                #endif

                return o;
            }

            // RGL ps_main_depthed_flare(sun_like=false, blend_adding=false)

            fixed4 frag(v2f i) : SV_Target
            {
                fixed4 col = tex2D(_MainTex, i.uv) * i.color;

                // Gamma correction (RGL: OUTPUT_GAMMA for non-additive)
                #ifdef _GAMMA_CORRECTION
                    col.rgb = pow(col.rgb, 1.0 / 2.2);
                #endif

                // Soft particle depth fade - non-additive: fade alpha
                #ifdef _SOFT_PARTICLES
                {
                    float alphaFactor = ComputeSoftFade(i.projCoord, i.depth);
                    col.a *= alphaFactor;
                }
                #endif

                // Fog (modulate path: blend toward fog color)
                #ifdef _FOG_ON
                    col.rgb = lerp(col.rgb, _FogColor.rgb, i.fogAmount);
                #endif

                return col;
            }
            ENDCG
        }
    }

    Fallback "Particles/Alpha Blended"
}
