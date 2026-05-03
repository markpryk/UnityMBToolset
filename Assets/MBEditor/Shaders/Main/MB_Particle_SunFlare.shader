// Warband Particle Shader - Sun Flare
// Uses combined mesh approach (RGL-style):
//   - Per-vertex colors baked into vertex data by WarbandParticleRenderer
//   - Vertices are already in world space (transformed on CPU)
//   - Additive blend
//   - Depth = 0 (ignores particle depth, just uses scene depth as alpha)
//   - Fog-based alpha falloff: fog_factor = 1.001 - (10 * (fFogDensity + 0.001))
//   - alpha_factor = depth * fog_factor
//   - Premultiplied into RGB (additive path)
//
// Use this for sun glare, lens flares, light shafts

Shader "M&B/Particles/SunFlare"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _TintColor ("Tint Color", Color) = (0.5, 0.5, 0.5, 0.5)

        [Header(Soft Sun)]
        [Toggle(_SOFT_PARTICLES)] _SoftParticles ("Enable Soft Sun", Float) = 1
        _SoftFactor ("Soft Factor (unused, for compat)", Float) = 4096
        _FogDensity ("Fog Density", Range(0.001, 0.1)) = 0.02
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent+100"
            "IgnoreProjector" = "True"
            "RenderType" = "Transparent"
            "PreviewType" = "Plane"
        }

        Blend SrcAlpha One
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
                #endif
                UNITY_VERTEX_OUTPUT_STEREO
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;
            float4 _TintColor;

            // SunFlare uses _FogDensity for the flare fade calculation,
            // not the _FOG_ON / ComputeWarbandFog path.
            float _FogDensity;

            v2f vert(appdata v)
            {
                v2f o;

                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);

                // Standard MVP transform - vertices are in local space
                float4 worldPos = mul(unity_ObjectToWorld, v.vertex);
                o.pos = UnityObjectToClipPos(v.vertex);

                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                o.color = v.color * _TintColor;

                #ifdef _SOFT_PARTICLES
                    o.projCoord = ComputeScreenPos(o.pos);
                #endif

                return o;
            }

            // RGL: ps_main_depthed_flare(sun_like=true, blend_adding=true)
            // - my_depth = 0  (sun flare ignores own depth)
            // - alpha_factor = depth * fog_factor
            // - Premultiplied RGB (additive)

            fixed4 frag(v2f i) : SV_Target
            {
                fixed4 col = tex2D(_MainTex, i.uv) * i.color;

                // Premultiplied alpha
                col.rgb *= col.a;

                #ifdef _SOFT_PARTICLES
                {
                    float alphaFactor = ComputeSunFlareFade(i.projCoord, _FogDensity);
                    col.rgb *= alphaFactor;
                }
                #endif

                return col;
            }
            ENDCG
        }
    }

    Fallback "Particles/Additive"
}
