Shader "M&B/M&B_Standard"
{
    Properties
    {
        [Header(Textures)]
        _MainTex ("Diffuse A (RGB) Alpha (A)", 2D) = "white" {}
        _BumpMap ("Normal Map", 2D) = "bump" {}
        _SpecGlossMap ("Specular Map", 2D) = "black" {}
        _DetailAlbedoMap ("Diffuse B (Detail)", 2D) = "white" {}
        _EnviroMap ("Environment Map", 2D) = "black" {}

        [Header(Material Properties)]
        _MBSpecColor ("Specular Color", Color) = (0.5, 0.5, 0.5, 1)
        _Specular ("Specular Value (BRF)", Range(0, 100)) = 20
        _SpecIntensity ("Specular Intensity", Range(0, 2)) = 0.5

        [Header(Warband Rendering)]
        [KeywordEnum(Plain, Iron, Shine, Preshaded)]
        _WBMode ("Warband Mode", Float) = 0
        [Toggle] _AGnm ("DXT5nm Normal Maps", Float) = 1
        [Toggle] _RGBnm ("RGB Normal Maps", Float) = 1
        [Toggle] _UseVertexColor ("Vertex Colors", Float) = 0
        [Toggle] _UseEnvMap ("Environment Mapping", Float) = 0
        _EnvMapStrength ("Env Map Strength", Range(0, 1)) = 0.3

        [Header(Lighting Tuning)]
        _AmbientBoost ("Ambient Boost", Range(0, 0.5)) = 0.1
        _DiffuseWrap ("Diffuse Wrap", Range(0, 0.5)) = 0.05
        _DetailStrength ("Detail Blend", Range(0, 1)) = 0.0

        [Header(Internal)]
        _BrfFlags ("BRF Flags (read-only)", Float) = 0
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Geometry" }
        LOD 300

        CGPROGRAM
        #pragma surface surf WarbandBP fullforwardshadows vertex:vert addshadow
        #pragma target 3.0
        #pragma multi_compile_instancing

        #pragma shader_feature_local _WBMODE_PLAIN _WBMODE_IRON _WBMODE_SHINE _WBMODE_PRESHADED
        #pragma shader_feature_local _ _AG_NORMAL
        #pragma shader_feature_local _ _RGB_NORMAL
        #pragma shader_feature_local _ _USEVERTEXCOLOR_ON
        #pragma shader_feature_local _ _USEENVMAP_ON

        #include "MB_Common.cginc"

        sampler2D _MainTex;
        sampler2D _BumpMap;
        sampler2D _SpecGlossMap;
        sampler2D _DetailAlbedoMap;
        sampler2D _EnviroMap;

        fixed4 _MBSpecColor;
        half _Specular;
        half _SpecIntensity;
        half _AmbientBoost;
        half _DiffuseWrap;
        half _EnvMapStrength;
        half _DetailStrength;
        float4 _BumpMap_TexelSize;
        
        struct Input
        {
            float2 uv_MainTex;
            float2 uv_BumpMap;
            float2 uv_DetailAlbedoMap;
            float4 vertexColor : COLOR;
            float3 worldRefl;
            INTERNAL_DATA
        };

        // Warband-style Blinn-Phong lighting
        half4 LightingWarbandBP(SurfaceOutput s, half3 lightDir, half3 viewDir, half atten)
        {
            half3 h = normalize(lightDir + viewDir);

            half NdotL = dot(s.Normal, lightDir);
            half diff = saturate((NdotL + _DiffuseWrap) / (1.0 + _DiffuseWrap));

            half NdotH = saturate(dot(s.Normal, h));
            // Warband specular: power function with 0-100 range
            half specPow = max(1.0, _Specular * 1.28); // scale 0-100 -> ~0-128
            half spec = pow(NdotH, specPow) * s.Gloss;

            half4 c;
            c.rgb = s.Albedo * _LightColor0.rgb * diff
                  + _LightColor0.rgb * _MBSpecColor.rgb * spec * _SpecIntensity;
            c.rgb *= atten;
            c.a = s.Alpha;
            return c;
        }

        void vert(inout appdata_full v, out Input o)
        {
            UNITY_INITIALIZE_OUTPUT(Input, o);
        }

        void surf(Input IN, inout SurfaceOutput o)
        {
            // Diffuse A (primary texture)
            fixed4 diffA = tex2D(_MainTex, IN.uv_MainTex);

            o.Albedo = diffA.rgb;
            o.Alpha = diffA.a;

            // Detail texture (Diffuse B) blending
            #if !defined(_WBMODE_PRESHADED)
            {
                fixed4 detail = tex2D(_DetailAlbedoMap, IN.uv_DetailAlbedoMap);
                // Only blend if detail tex is assigned (non-white check)
                o.Albedo = lerp(o.Albedo, o.Albedo * detail.rgb * 2.0, _DetailStrength * detail.a);
            }
            #endif

            // Vertex colors
            #if defined(_USEVERTEXCOLOR_ON)
                o.Albedo *= IN.vertexColor.rgb;
            #endif

            // Preshaded: vertex-lit, early out
            #if defined(_WBMODE_PRESHADED)
                o.Albedo *= IN.vertexColor.rgb;
                o.Emission = o.Albedo * _AmbientBoost;
                o.Specular = 0;
                o.Gloss = 0;
                return;
            #endif

            bool hasBump = _BumpMap_TexelSize.z > 4;
            if (hasBump)
                o.Normal = SampleWarbandNormal(_BumpMap, IN.uv_BumpMap);

            // Specular setup per mode
            half smoothness = _Specular / 100.0;

            #if defined(_WBMODE_IRON)
                // Alpha channel -> shininess
                o.Specular = smoothness;
                o.Gloss = diffA.a;
            #elif defined(_WBMODE_SHINE)
                // Dedicated specular map
                fixed3 specMapVal = tex2D(_SpecGlossMap, IN.uv_MainTex).rgb;
                o.Specular = smoothness;
                o.Gloss = Luminance(specMapVal);
            #else
                // Plain
                o.Specular = smoothness;
                o.Gloss = smoothness;
            #endif

            // Environment mapping - Warband uses a 2D spherical enviro map
            #if defined(_USEENVMAP_ON)
            {
                float3 refl = normalize(WorldReflectionVector(IN, o.Normal));
                // Convert 3D reflection to 2D spherical UV (RGL matcap/sphere-map style)
                float2 envUV = refl.xz * 0.5 + 0.5;
                fixed4 envColor = tex2D(_EnviroMap, envUV);
                o.Emission += envColor.rgb * _EnvMapStrength * o.Gloss;
            }
            #endif

            // Ambient boost to match Warband's darker scenes
            o.Emission += o.Albedo * _AmbientBoost;
        }
        ENDCG
    }

    FallBack "Diffuse"
    CustomEditor "MBShaderGUI"
}
