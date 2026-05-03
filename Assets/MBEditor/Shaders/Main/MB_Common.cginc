// MB_Common.cginc
// Shared utilities for M&B Warband shaders.
// Include in all M&B surface shaders.

#ifndef MB_COMMON_INCLUDED
#define MB_COMMON_INCLUDED

#include "UnityCG.cginc"

// ----- Normal Map Sampling -----
// Supports both DXT5nm (Warband default) and standard RGB normal maps.

// sRGB to Linear approximation (matches GPU hardware conversion)
half3 SRGBToLinear(half3 srgb)
{
    return srgb * (srgb * (srgb * 0.305306011 + 0.682171111) + 0.012522878);
}
half SRGBToLinearExact(half c)
{
    return c <= 0.04045 ? c / 12.92 : pow((c + 0.055) / 1.055, 2.4);
}
half3 SampleWarbandNormal(sampler2D bumpMap, float2 uv, half strength = 1)
{

    
        #if defined(_AG_NORMAL)
    // Raw DXT5nm DDS: X in alpha, Y in green
    half4 packed = tex2D(bumpMap, uv);
        packed.ag = SRGBToLinear(half3(packed.a, packed.g, 0)).xy;
        half3 n;
        n.x = packed.a ;
        n.y = -packed.g ;
        n.z = sqrt(saturate(1.0 - dot(n.xy, n.xy)));
        n.xyz *= strength;
        return normalize(n);

        #elif defined(_RGB_NORMAL)
        // Raw RGB normal map with sRGB correction
        half3 raw = tex2D(bumpMap, uv).rgb;
        raw = SRGBToLinear(raw);
        half3 n = raw;
        n.y = -raw.g ;
        n.xyz *= strength;
        return normalize(n);

        #else
        return UnpackNormal(tex2D(bumpMap, uv));
        #endif
}



// ----- Coordinate Conversion -----
// Warband uses Z-up, Unity uses Y-up

float3 BrfToUnity(float3 brfCoord)
{
    return float3(brfCoord.x, brfCoord.z, brfCoord.y);
}

// ----- Warband Color Utilities -----

// Convert Warband specular power (0-100 in BRF) to a Blinn-Phong exponent
half WarbandSpecPowerToExponent(half specValue)
{
    // BRF stores 0-100, OpenBRF uses roughly 0-128 range for the power function
    return max(1.0, specValue * 1.28);
}

// Convert Warband specular value to Unity smoothness (0-1)
half WarbandSpecToSmoothness(half specValue)
{
    return saturate(specValue / 100.0);
}

// Warband-style HDR color decode (for particle systems and lights)
// Warband stores intensity as a multiplier on normalized color
half3 DecodeWarbandHDR(half3 color, half intensity)
{
    return color * intensity;
}

// ----- Vertex Color Helpers -----

// Warband vertex color channels:
// RGB: typically color tint (skin, team color, AO)
// A: sometimes used for alpha or weight

half4 ApplyVertexColor(half4 baseColor, half4 vertColor, bool multiplyAlpha)
{
    half4 result;
    result.rgb = baseColor.rgb * vertColor.rgb;
    result.a = multiplyAlpha ? baseColor.a * vertColor.a : baseColor.a;
    return result;
}

// ----- Tangent Space -----
// Warband's tangent sign convention (from OpenBRF source):
// tany = cross(norm, tanx)
// tany *= (gl_MultiTexCoord2.x == 0.0) ? -1.0 : 1.0
// Unity handles this via TANGENT semantic w-component, so no extra work needed
// in surface shaders. This note is for reference if writing vert/frag shaders.

#endif // MB_COMMON_INCLUDED
