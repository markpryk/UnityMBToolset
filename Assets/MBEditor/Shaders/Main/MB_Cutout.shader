Shader "M&B/M&B_Cutout"
{
    Properties
    {
        [Header(Textures)]
        _MainTex ("Diffuse A (RGB) Alpha (A)", 2D) = "white" {}
        _BumpMap ("Normal Map", 2D) = "bump" {}
        _SpecGlossMap ("Specular Map", 2D) = "black" {}
        _DetailAlbedoMap ("Diffuse B (Detail)", 2D) = "white" {}

        [Header(Material Properties)]
        _MBSpecColor ("Specular Color", Color) = (0.5, 0.5, 0.5, 1)
        _Specular ("Specular Value (BRF)", Range(0, 100)) = 10
        _SpecIntensity ("Specular Intensity", Range(0, 2)) = 0.3
        _Cutoff ("Alpha Cutoff", Range(0, 1)) = 0.5

        [Header(Warband Rendering)]
        [Toggle] _AGnm ("DXT5nm Normal Maps", Float) = 1
         [Toggle] _RGBnm ("RGB Normal Maps", Float) = 1
        [Toggle] _UseVertexColor ("Vertex Colors", Float) = 0
        [Toggle] _TwoSided ("Two Sided", Float) = 1

        [Header(Lighting Tuning)]
        _AmbientBoost ("Ambient Boost", Range(0, 0.5)) = 0.1
        _DiffuseWrap ("Diffuse Wrap", Range(0, 0.5)) = 0.1
        _SubsurfaceAmount ("Subsurface Scatter", Range(0, 1)) = 0.0

        [Header(Internal)]
        _BrfFlags ("BRF Flags (read-only)", Float) = 0
    }

    SubShader
    {
        Tags { "RenderType" = "TransparentCutout" "Queue" = "AlphaTest" }
        LOD 200
        Cull Off

        CGPROGRAM
        #pragma surface surf WarbandBP fullforwardshadows alphatest:_Cutoff vertex:vert addshadow
        #pragma target 3.0
        #pragma multi_compile_instancing

        #pragma shader_feature_local _ _AG_NORMAL
        #pragma shader_feature_local _ _RGB_NORMAL
        #pragma shader_feature_local _ _USEVERTEXCOLOR_ON

        #include "MB_Common.cginc"

        sampler2D _MainTex;
        sampler2D _BumpMap;
        sampler2D _SpecGlossMap;
        sampler2D _DetailAlbedoMap;

        fixed4 _MBSpecColor;
        half _Specular;
        half _SpecIntensity;
        half _AmbientBoost;
        half _DiffuseWrap;
        half _SubsurfaceAmount;
        float4 _BumpMap_TexelSize;
        
        struct Input
        {
            float2 uv_MainTex;
            float2 uv_BumpMap;
            float4 vertexColor : COLOR;
        };

        half4 LightingWarbandBP(SurfaceOutput s, half3 lightDir, half3 viewDir, half atten)
        {
            half3 h = normalize(lightDir + viewDir);
            half NdotL = dot(s.Normal, lightDir);
            half diff = saturate((NdotL + _DiffuseWrap) / (1.0 + _DiffuseWrap));

            // Optional subsurface scatter for foliage
            half scatter = saturate(-NdotL) * _SubsurfaceAmount;
            diff += scatter;

            half NdotH = saturate(dot(s.Normal, h));
            half specPow = max(1.0, _Specular * 1.28);
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
            fixed4 tex = tex2D(_MainTex, IN.uv_MainTex);

            o.Albedo = tex.rgb;
            o.Alpha = tex.a;

            #if defined(_USEVERTEXCOLOR_ON)
                o.Albedo *= IN.vertexColor.rgb;
            #endif

            bool hasBump = _BumpMap_TexelSize.z > 4;
            if (hasBump)
                o.Normal = SampleWarbandNormal(_BumpMap, IN.uv_BumpMap);

            // Specular
            half smoothness = _Specular / 100.0;

            fixed3 specMapVal = tex2D(_SpecGlossMap, IN.uv_MainTex).rgb;
            half specMapLum = Luminance(specMapVal);
            // Use spec map if present, otherwise use smoothness
            o.Gloss = max(specMapLum, smoothness * step(specMapLum, 0.01));
            o.Specular = smoothness;

            o.Emission = o.Albedo * _AmbientBoost;
        }
        ENDCG
    }

    FallBack "Transparent/Cutout/Diffuse"
    CustomEditor "MBShaderGUI"
}
