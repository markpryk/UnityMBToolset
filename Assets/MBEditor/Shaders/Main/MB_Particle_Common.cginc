// MB_Particle_Common.cginc
// Shared code for all M&B Warband particle shaders.
// Supports two rendering paths:
//   1. Combined mesh (RGL-style): per-vertex colors via COLOR semantic
//   2. GPU instanced (legacy): per-particle colors from StructuredBuffer
//
// Requirements:
//   - Must #include "UnityCG.cginc" before this file
//   - For GPU instanced path: #pragma require compute_buffers
//   - Shader must declare relevant features:
//       #pragma shader_feature_local _SOFT_PARTICLES
//       #pragma shader_feature_local _FOG_ON

#ifndef MB_PARTICLE_COMMON_INCLUDED
#define MB_PARTICLE_COMMON_INCLUDED

// ═══════════════════════════════════════════════════════════════
//  GPU Instance Buffer (legacy path, kept for compatibility)
//  Only used when _GPU_INSTANCED is defined
// ═══════════════════════════════════════════════════════════════

#ifdef _GPU_INSTANCED
struct ParticleData
{
    float4x4 matrix_data;   // World-space transform
    float4   color;         // RGBA from key interpolation
    float2   uv;            // Reserved / atlas offset
    float2   uv2;           // Reserved / atlas scale
};

StructuredBuffer<ParticleData> _ParticleBuffer;
#endif

// ═══════════════════════════════════════════════════════════════
//  Common Properties (declare in each shader's Properties block)
// ═══════════════════════════════════════════════════════════════
//  _MainTex, _MainTex_ST, _TintColor
//  _SoftFactor (for non-sunflare soft particles)
//  _FogDensity, _FogColor (for fog)

// ═══════════════════════════════════════════════════════════════
//  Fog - approximates rgl get_fog_amount_new
// ═══════════════════════════════════════════════════════════════

#ifdef _FOG_ON
float _FogDensity;

// RGL: exponential distance fog with height-based density falloff
float ComputeWarbandFog(float viewDist, float worldY)
{
    float heightFactor = saturate(1.0 - worldY * 0.005);
    float fog = 1.0 - exp(-_FogDensity * viewDist * (1.0 + heightFactor));
    return saturate(fog);
}
#endif

// ═══════════════════════════════════════════════════════════════
//  Soft Particles
// ═══════════════════════════════════════════════════════════════

#ifdef _SOFT_PARTICLES
sampler2D _CameraDepthTexture;
float _SoftFactor;

// Standard soft particle fade: compare particle depth to scene depth.
// Returns 0..1 alpha factor.
float ComputeSoftFade(float4 projCoord, float particleDepth)
{
    float sceneDepth = LinearEyeDepth(
        SAMPLE_DEPTH_TEXTURE_PROJ(_CameraDepthTexture, UNITY_PROJ_COORD(projCoord)));
    return saturate((sceneDepth - particleDepth) * _SoftFactor);
}

// Sun flare variant: ignores particle depth, uses raw scene depth + fog factor.
// RGL: fog_factor = 1.001 - (10 * (fFogDensity + 0.001))
//   density 0.1 → fog_factor ≈ 0 (thick fog = no flare)
//   density 0.01 → fog_factor ≈ 0.89 (clear = full flare)
float ComputeSunFlareFade(float4 projCoord, float fogDensity)
{
    float depth = SAMPLE_DEPTH_TEXTURE_PROJ(_CameraDepthTexture, UNITY_PROJ_COORD(projCoord)).r;
    #if defined(UNITY_REVERSED_Z)
        depth = 1.0 - depth;
    #endif

    float fogFactor = saturate(1.001 - (10.0 * (fogDensity + 0.001)));
    return depth * fogFactor;
}
#endif

// ═══════════════════════════════════════════════════════════════
//  Vertex Helpers
// ═══════════════════════════════════════════════════════════════

#ifdef _GPU_INSTANCED
// Transform particle vertex using instance buffer.
// Instance matrices are already world-space, so we skip M and go straight to VP.
float4 TransformParticleVertex(float4 localPos, float4x4 instanceMatrix)
{
    float4 worldPos = mul(instanceMatrix, localPos);
    return mul(UNITY_MATRIX_VP, worldPos);
}

// Get world position from instance matrix (for fog, distance calculations)
float4 GetParticleWorldPos(float4 localPos, float4x4 instanceMatrix)
{
    return mul(instanceMatrix, localPos);
}
#endif

#endif // MB_PARTICLE_COMMON_INCLUDED
