Shader "Hidden/Custom/Terrain/Standard-PlanarOverlay-AddPass"
{
    Properties
    {
        [HideInInspector] _Control("Control (RGBA)", 2D) = "black" {}
        [HideInInspector] _Splat3("Layer 3 (A)", 2D) = "grey" {}
        [HideInInspector] _Splat2("Layer 2 (B)", 2D) = "grey" {}
        [HideInInspector] _Splat1("Layer 1 (G)", 2D) = "grey" {}
        [HideInInspector] _Splat0("Layer 0 (R)", 2D) = "grey" {}
        [HideInInspector] _Normal3("Normal 3 (A)", 2D) = "bump" {}
        [HideInInspector] _Normal2("Normal 2 (B)", 2D) = "bump" {}
        [HideInInspector] _Normal1("Normal 1 (G)", 2D) = "bump" {}
        [HideInInspector] _Normal0("Normal 0 (R)", 2D) = "bump" {}

        _OverlayTex("Overlay Texture", 2D) = "black" {}
        _OverlayScaleX("World Scale X", Float) = 0.01
_OverlayScaleZ("World Scale Z", Float) = 0.01
        _OverlayStrength("Strength", Range(0, 1)) = 0.5
        _Smoothness("Smoothness", Range(0, 1)) = 0.0
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Geometry-99"
            "RenderType" = "Opaque"
        }

        CGPROGRAM
        #pragma surface surf Standard decal:add fullforwardshadows finalcolor:FinalColor
        #pragma multi_compile_fog
        #pragma target 3.0

        #define TERRAIN_SPLAT_ADDPASS

        sampler2D _Control;
        float4 _Control_TexelSize;
        sampler2D _Splat0, _Splat1, _Splat2, _Splat3;
        float4 _Splat0_ST, _Splat1_ST, _Splat2_ST, _Splat3_ST;
        sampler2D _Normal0, _Normal1, _Normal2, _Normal3;

        sampler2D _OverlayTex;
        float _OverlayScaleX;
        float _OverlayScaleZ;
        half _OverlayStrength;
        half _Smoothness;

        struct Input
        {
            float2 uv_Control;
            float3 worldPos;
            UNITY_FOG_COORDS(0)
        };

        void surf(Input IN, inout SurfaceOutputStandard o)
        {
            // Pixel-center adjusted UVs
            float2 splatUV = (IN.uv_Control * (_Control_TexelSize.zw - 1.0) + 0.5) * _Control_TexelSize.xy;

            half4 ctrl = tex2D(_Control, splatUV);
            half weight = dot(ctrl, half4(1,1,1,1));

            // Early out if this pass contributes nothing
            #if !defined(SHADER_API_MOBILE)
                clip(weight == 0.0 ? -1 : 1);
            #endif

            // Normalize weights before blending
            ctrl /= (weight + 1e-3);

            // Sample terrain layers
            float2 uv0 = TRANSFORM_TEX(IN.uv_Control, _Splat0);
            float2 uv1 = TRANSFORM_TEX(IN.uv_Control, _Splat1);
            float2 uv2 = TRANSFORM_TEX(IN.uv_Control, _Splat2);
            float2 uv3 = TRANSFORM_TEX(IN.uv_Control, _Splat3);

            fixed4 col0 = tex2D(_Splat0, uv0);
            fixed4 col1 = tex2D(_Splat1, uv1);
            fixed4 col2 = tex2D(_Splat2, uv2);
            fixed4 col3 = tex2D(_Splat3, uv3);

            fixed3 terrainColor = col0.rgb * ctrl.r + col1.rgb * ctrl.g + col2.rgb * ctrl.b + col3.rgb * ctrl.a;

            // Normals
            half3 n0 = UnpackNormal(tex2D(_Normal0, uv0));
            half3 n1 = UnpackNormal(tex2D(_Normal1, uv1));
            half3 n2 = UnpackNormal(tex2D(_Normal2, uv2));
            half3 n3 = UnpackNormal(tex2D(_Normal3, uv3));
            half3 terrainNormal = n0 * ctrl.r + n1 * ctrl.g + n2 * ctrl.b + n3 * ctrl.a;
            terrainNormal.z += 1e-5;

            // Planar overlay (masked by this pass's weight)
            float2 planarUV = IN.worldPos.xz * float2(_OverlayScaleX, _OverlayScaleZ);
            half4 overlay = tex2D(_OverlayTex, planarUV);
            half overlayMask = overlay.a * _OverlayStrength;

            o.Albedo = lerp(terrainColor,terrainColor* overlay.rgb, overlayMask);
            o.Normal = normalize(terrainNormal);
            o.Smoothness = _Smoothness;
            o.Metallic = 0;
            o.Alpha = weight;
        }

        void FinalColor(Input IN, SurfaceOutputStandard o, inout fixed4 color)
        {
            color *= o.Alpha;
            UNITY_APPLY_FOG_COLOR(IN.fogCoord, color, fixed4(0,0,0,0));
        }
        ENDCG
    }

    Fallback "Hidden/TerrainEngine/Splatmap/Standard-AddPass"
}