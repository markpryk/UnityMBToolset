using UnityEngine;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

/// <summary>
/// Extension methods and helpers for integrating ArcGIS-style curvature calculation
/// into MBTerrainDecorator.
/// 
/// Provides cached curvature map generation and per-pixel curvature queries
/// using the Zeverbergen & Thorne algorithm (ArcGIS standard).
/// 
/// Curvature Types:
/// - Standard: Total surface curvature (Laplacian). Positive = convex (ridges), Negative = concave (valleys)
/// - Profile: Curvature in direction of maximum slope. Affects flow acceleration/deceleration.
/// - Plan: Curvature perpendicular to slope. Affects flow convergence/divergence.
/// </summary>
public static class MBTerrainDecoratorCurvatureExtensions
{
    #region Cached Curvature Map
    
    /// <summary>
    /// Pre-calculated curvature maps for a terrain, cached for fast per-pixel access.
    /// </summary>
    public class CachedCurvatureMap
    {
        public float[,] Standard { get; private set; }
        public float[,] Profile { get; private set; }
        public float[,] Plan { get; private set; }
        
        // Normalized versions (0-1 range)
        public float[,] StandardNormalized { get; private set; }
        public float[,] ProfileNormalized { get; private set; }
        public float[,] PlanNormalized { get; private set; }
        
        public int Width { get; private set; }
        public int Height { get; private set; }
        
        public CurvatureCalculator.CurvatureStatistics Statistics { get; private set; }
        
        private float cellSize;
        private float heightScale;
        
        /// <summary>
        /// Create a cached curvature map from terrain data.
        /// </summary>
        /// <param name="terrainData">Unity terrain data</param>
        /// <param name="useParallel">Use parallel job for calculation</param>
        public static CachedCurvatureMap Create(TerrainData terrainData, bool useParallel = true)
        {
            var cache = new CachedCurvatureMap();
            
            int resolution = terrainData.heightmapResolution;
            cache.Width = resolution;
            cache.Height = resolution;
            
            // Use average cell size (assumes roughly square cells)
            cache.cellSize = (terrainData.size.x + terrainData.size.z) / 2f / (resolution - 1);
            cache.heightScale = terrainData.size.y;
            
            float[,] heights = terrainData.GetHeights(0, 0, resolution, resolution);
            
            if (useParallel)
            {
                CurvatureCalculator.CalculateCurvatureMapParallel(
                    heights,
                    cache.cellSize,
                    cache.heightScale,
                    out float[,] standard,
                    out float[,] profile,
                    out float[,] plan,
                    out var stats);
                cache.Standard = standard;
                cache.Profile = profile;
                cache.Plan = plan;
                cache.Statistics = stats;
            }
            else
            {
                // Sequential fallback - calculate all three maps manually
                cache.Standard = new float[resolution, resolution];
                cache.Profile = new float[resolution, resolution];
                cache.Plan = new float[resolution, resolution];
                
                for (int y = 0; y < resolution; y++)
                {
                    for (int x = 0; x < resolution; x++)
                    {
                        var result = CurvatureCalculator.CalculateAtPoint(
                            heights, x, y, cache.cellSize, cache.heightScale);
                        cache.Standard[y, x] = result.Standard;
                        cache.Profile[y, x] = result.Profile;
                        cache.Plan[y, x] = result.Plan;
                    }
                }
                
                // Calculate statistics manually
                cache.Statistics = CalculateStatistics(cache.Standard, cache.Profile, cache.Plan, resolution);
            }
            
            // Pre-calculate normalized versions
            cache.StandardNormalized = NormalizeMap(cache.Standard, cache.Statistics.MinStandard, cache.Statistics.MaxStandard);
            cache.ProfileNormalized = NormalizeMap(cache.Profile, cache.Statistics.MinProfile, cache.Statistics.MaxProfile);
            cache.PlanNormalized = NormalizeMap(cache.Plan, cache.Statistics.MinPlan, cache.Statistics.MaxPlan);
            
            return cache;
        }
        
        private static CurvatureCalculator.CurvatureStatistics CalculateStatistics(
            float[,] standard, float[,] profile, float[,] plan, int resolution)
        {
            var stats = new CurvatureCalculator.CurvatureStatistics
            {
                MinStandard = float.MaxValue, MaxStandard = float.MinValue,
                MinProfile = float.MaxValue, MaxProfile = float.MinValue,
                MinPlan = float.MaxValue, MaxPlan = float.MinValue
            };
            
            for (int y = 0; y < resolution; y++)
            {
                for (int x = 0; x < resolution; x++)
                {
                    float s = standard[y, x];
                    float p = profile[y, x];
                    float l = plan[y, x];
                    
                    if (!float.IsNaN(s) && !float.IsInfinity(s))
                    {
                        stats.MinStandard = Mathf.Min(stats.MinStandard, s);
                        stats.MaxStandard = Mathf.Max(stats.MaxStandard, s);
                    }
                    if (!float.IsNaN(p) && !float.IsInfinity(p))
                    {
                        stats.MinProfile = Mathf.Min(stats.MinProfile, p);
                        stats.MaxProfile = Mathf.Max(stats.MaxProfile, p);
                    }
                    if (!float.IsNaN(l) && !float.IsInfinity(l))
                    {
                        stats.MinPlan = Mathf.Min(stats.MinPlan, l);
                        stats.MaxPlan = Mathf.Max(stats.MaxPlan, l);
                    }
                }
            }
            
            return stats;
        }
        
        private static float[,] NormalizeMap(float[,] map, float min, float max)
        {
            int height = map.GetLength(0);
            int width = map.GetLength(1);
            float[,] normalized = new float[height, width];
            
            float range = max - min;
            if (range < 0.0001f) range = 1f;
            
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    normalized[y, x] = Mathf.Clamp01((map[y, x] - min) / range);
                }
            }
            
            return normalized;
        }
        
        /// <summary>
        /// Get curvature at a normalized position (0-1 range).
        /// </summary>
        /// <param name="normX">Normalized X position</param>
        /// <param name="normY">Normalized Y position</param>
        /// <param name="type">Curvature type to retrieve</param>
        /// <param name="normalized">If true, return normalized 0-1 value; if false, return raw value</param>
        public float GetCurvatureAt(float normX, float normY, 
            CurvatureCalculator.CurvatureType type = CurvatureCalculator.CurvatureType.Standard, 
            bool normalized = true)
        {
            // Decorator convention: normX maps to row, normY maps to col
            int x = Mathf.Clamp(Mathf.FloorToInt(normY * (Width - 1)), 0, Width - 1);
            int y = Mathf.Clamp(Mathf.FloorToInt(normX * (Height - 1)), 0, Height - 1);
            
            return GetValueAt(x, y, type, normalized);
        }
        
        /// <summary>
        /// Get curvature at a normalized position with bilinear interpolation.
        /// </summary>
        public float GetCurvatureAtInterpolated(float normX, float normY,
            CurvatureCalculator.CurvatureType type = CurvatureCalculator.CurvatureType.Standard,
            bool normalized = true)
        {
            // Decorator convention: normX maps to row, normY maps to col
            float fx = normY * (Width - 1);
            float fy = normX * (Height - 1);
            
            int x0 = Mathf.FloorToInt(fx);
            int y0 = Mathf.FloorToInt(fy);
            int x1 = Mathf.Min(x0 + 1, Width - 1);
            int y1 = Mathf.Min(y0 + 1, Height - 1);
            
            float tx = fx - x0;
            float ty = fy - y0;
            
            float v00 = GetValueAt(x0, y0, type, normalized);
            float v10 = GetValueAt(x1, y0, type, normalized);
            float v01 = GetValueAt(x0, y1, type, normalized);
            float v11 = GetValueAt(x1, y1, type, normalized);
            
            float v0 = Mathf.Lerp(v00, v10, tx);
            float v1 = Mathf.Lerp(v01, v11, tx);
            
            return Mathf.Lerp(v0, v1, ty);
        }
        
        private float GetValueAt(int x, int y, CurvatureCalculator.CurvatureType type, bool normalized)
        {
            switch (type)
            {
                case CurvatureCalculator.CurvatureType.Profile:
                    return normalized ? ProfileNormalized[y, x] : Profile[y, x];
                case CurvatureCalculator.CurvatureType.Plan:
                    return normalized ? PlanNormalized[y, x] : Plan[y, x];
                default:
                    return normalized ? StandardNormalized[y, x] : Standard[y, x];
            }
        }
    }
    
    #endregion
    
    #region Weight Calculation for Decorator
    
    /// <summary>
    /// Calculate curvature-based weight for terrain decoration.
    /// Matches the interface expected by MBTerrainDecorator rules.
    /// </summary>
    /// <param name="curvatureCache">Cached curvature map</param>
    /// <param name="normX">Normalized X position (0-1)</param>
    /// <param name="normY">Normalized Y position (0-1)</param>
    /// <param name="minCurvature">Minimum curvature threshold (normalized 0-1)</param>
    /// <param name="maxCurvature">Maximum curvature threshold (normalized 0-1)</param>
    /// <param name="falloff">Falloff distance for smooth transitions</param>
    /// <param name="type">Curvature type to use</param>
    /// <param name="interpolate">Use bilinear interpolation</param>
    /// <returns>Weight value 0-1</returns>
    public static float CalculateCurvatureWeight(
        CachedCurvatureMap curvatureCache,
        float normX, float normY,
        float minCurvature, float maxCurvature, float falloff,
        CurvatureCalculator.CurvatureType type = CurvatureCalculator.CurvatureType.Standard,
        bool interpolate = false)
    {
        float curvature = interpolate 
            ? curvatureCache.GetCurvatureAtInterpolated(normX, normY, type, true)
            : curvatureCache.GetCurvatureAt(normX, normY, type, true);
        
        return CalculateWeightWithFalloff(curvature, minCurvature, maxCurvature, falloff);
    }
    
    /// <summary>
    /// Calculate curvature-based weight without cache (direct calculation).
    /// Use cached version for better performance with repeated access.
    /// </summary>
    public static float CalculateCurvatureWeight(
        float[,] heights,
        float normX, float normY,
        float minCurvature, float maxCurvature, float falloff,
        float cellSize, float heightScale,
        CurvatureCalculator.CurvatureType type = CurvatureCalculator.CurvatureType.Standard,
        float minForNormalize = -50f, float maxForNormalize = 50f)
    {
        var result = CurvatureCalculator.CalculateAtNormalizedPosition(
            heights, normX, normY, cellSize, heightScale);
        
        float rawCurvature = result.Get(type);
        
        // Normalize to 0-1 range
        float normalized = Mathf.Clamp01((rawCurvature - minForNormalize) / (maxForNormalize - minForNormalize));
        
        return CalculateWeightWithFalloff(normalized, minCurvature, maxCurvature, falloff);
    }
    
    /// <summary>
    /// Standard weight calculation with falloff, matching MBTerrainDecorator.CalculateWeight
    /// </summary>
    private static float CalculateWeightWithFalloff(float value, float min, float max, float falloff)
    {
        if (falloff < 0.0001f) falloff = 0.0001f;
        
        float weight;
        
        if (value > min + falloff / 2f && value <= max - falloff / 2f)
        {
            weight = 1f;
        }
        else if (value < min + falloff / 2f)
        {
            weight = (value - (min - falloff / 2f)) / falloff;
        }
        else
        {
            weight = 1f - (value - (max - falloff / 2f)) / falloff;
        }
        
        return Mathf.Clamp01(weight);
    }
    
    #endregion
    
    #region Convexity/Concavity Helpers
    
    /// <summary>
    /// Check if a position is convex (ridge/peak) based on standard curvature.
    /// </summary>
    public static bool IsConvex(CachedCurvatureMap cache, float normX, float normY, float threshold = 0.55f)
    {
        return cache.GetCurvatureAt(normX, normY, CurvatureCalculator.CurvatureType.Standard, true) > threshold;
    }
    
    /// <summary>
    /// Check if a position is concave (valley/depression) based on standard curvature.
    /// </summary>
    public static bool IsConcave(CachedCurvatureMap cache, float normX, float normY, float threshold = 0.45f)
    {
        return cache.GetCurvatureAt(normX, normY, CurvatureCalculator.CurvatureType.Standard, true) < threshold;
    }
    
    /// <summary>
    /// Get a weight that favors convex areas (ridges, peaks).
    /// </summary>
    public static float GetConvexWeight(CachedCurvatureMap cache, float normX, float normY, float falloff = 0.1f)
    {
        float curvature = cache.GetCurvatureAt(normX, normY, CurvatureCalculator.CurvatureType.Standard, true);
        // Map 0.5-1.0 (convex) to 0-1
        return Mathf.Clamp01((curvature - 0.5f) * 2f);
    }
    
    /// <summary>
    /// Get a weight that favors concave areas (valleys, depressions).
    /// </summary>
    public static float GetConcaveWeight(CachedCurvatureMap cache, float normX, float normY, float falloff = 0.1f)
    {
        float curvature = cache.GetCurvatureAt(normX, normY, CurvatureCalculator.CurvatureType.Standard, true);
        // Map 0-0.5 (concave) to 1-0
        return Mathf.Clamp01((0.5f - curvature) * 2f);
    }
    
    /// <summary>
    /// Get flow convergence weight (for placing vegetation that needs water).
    /// Uses plan curvature - negative values indicate converging flow.
    /// </summary>
    public static float GetFlowConvergenceWeight(CachedCurvatureMap cache, float normX, float normY)
    {
        float planCurvature = cache.GetCurvatureAt(normX, normY, CurvatureCalculator.CurvatureType.Plan, true);
        // Plan curvature: low values (0-0.5) = converging flow
        return Mathf.Clamp01((0.5f - planCurvature) * 2f);
    }
    
    #endregion
    
    #region Burst-Compatible Functions for Decoration Jobs
    
    /// <summary>
    /// Burst-compatible curvature weight calculation.
    /// Can be used inside decoration jobs.
    /// </summary>
    [BurstCompile]
    public static float CalculateCurvatureWeightBurst(
        in NativeArray<float> heights,
        int width, int height,
        float normX, float normY,
        float minCurvature, float maxCurvature, float falloff,
        float cellSize, float heightScale,
        int curvatureType) // 0=Standard, 1=Profile, 2=Plan
    {
        // Decorator convention: normX maps to row, normY maps to col
        int x = math.clamp((int)(normY * (width - 1)), 0, width - 1);
        int y = math.clamp((int)(normX * (height - 1)), 0, height - 1);
        
        // Get 3x3 neighborhood
        int x0 = math.max(0, x - 1);
        int x2 = math.min(width - 1, x + 1);
        int y0 = math.max(0, y - 1);
        int y2 = math.min(height - 1, y + 1);
        
        float z1 = heights[y0 * width + x0] * heightScale;
        float z2 = heights[y0 * width + x] * heightScale;
        float z3 = heights[y0 * width + x2] * heightScale;
        float z4 = heights[y * width + x0] * heightScale;
        float z5 = heights[y * width + x] * heightScale;
        float z6 = heights[y * width + x2] * heightScale;
        float z7 = heights[y2 * width + x0] * heightScale;
        float z8 = heights[y2 * width + x] * heightScale;
        float z9 = heights[y2 * width + x2] * heightScale;
        
        float L = cellSize;
        float L2 = L * L;
        
        // Polynomial coefficients
        float D = ((z4 + z6) * 0.5f - z5) / L2;
        float E = ((z2 + z8) * 0.5f - z5) / L2;
        float F = (-z1 + z3 + z7 - z9) / (4f * L2);
        float G = (-z4 + z6) / (2f * L);
        float H = (z2 - z8) / (2f * L);
        
        float rawCurvature;
        
        if (curvatureType == 0) // Standard
        {
            rawCurvature = -2f * (D + E) * 100f;
        }
        else
        {
            float G2 = G * G;
            float H2 = H * H;
            float p = G2 + H2;
            
            if (p < 0.00001f)
            {
                rawCurvature = 0f;
            }
            else if (curvatureType == 1) // Profile
            {
                rawCurvature = -2f * (D * G2 + E * H2 + F * G * H) / p * 100f;
            }
            else // Plan
            {
                rawCurvature = -2f * (D * H2 + E * G2 - F * G * H) / p * 100f;
            }
        }
        
        // Normalize using typical range (-50 to 50)
        float normalized = math.clamp((rawCurvature + 50f) / 100f, 0f, 1f);
        
        // Calculate weight with falloff
        if (falloff < 0.0001f) falloff = 0.0001f;
        
        float weight;
        if (normalized > minCurvature + falloff / 2f && normalized <= maxCurvature - falloff / 2f)
        {
            weight = 1f;
        }
        else if (normalized < minCurvature + falloff / 2f)
        {
            weight = (normalized - (minCurvature - falloff / 2f)) / falloff;
        }
        else
        {
            weight = 1f - (normalized - (maxCurvature - falloff / 2f)) / falloff;
        }
        
        return math.clamp(weight, 0f, 1f);
    }
    
    /// <summary>
    /// Burst-compatible lookup from pre-calculated curvature map.
    /// </summary>
    [BurstCompile]
    public static float GetCurvatureFromCacheBurst(
        in NativeArray<float> curvatureMap,
        int width, int height,
        float normX, float normY)
    {
        // Decorator convention: normX maps to row, normY maps to col
        int x = math.clamp((int)(normY * (width - 1)), 0, width - 1);
        int y = math.clamp((int)(normX * (height - 1)), 0, height - 1);
        return curvatureMap[y * width + x];
    }
    
    #endregion
    
    #region Visualization Helpers
    
    /// <summary>
    /// Create a debug visualization texture from curvature data.
    /// Blue = concave (valleys), White = flat, Red = convex (ridges)
    /// </summary>
    public static Texture2D CreateVisualization(CachedCurvatureMap cache, CurvatureCalculator.CurvatureType type)
    {
        Texture2D texture = new Texture2D(cache.Width, cache.Height, TextureFormat.RGB24, false);
        Color[] pixels = new Color[cache.Width * cache.Height];
        
        for (int y = 0; y < cache.Height; y++)
        {
            for (int x = 0; x < cache.Width; x++)
            {
                float normalized = cache.GetCurvatureAt(
                    x / (float)(cache.Width - 1), 
                    y / (float)(cache.Height - 1), 
                    type, true);
                
                Color color;
                if (normalized < 0.5f)
                {
                    // Concave: blue to white
                    float t = normalized * 2f;
                    color = Color.Lerp(Color.blue, Color.white, t);
                }
                else
                {
                    // Convex: white to red
                    float t = (normalized - 0.5f) * 2f;
                    color = Color.Lerp(Color.white, Color.red, t);
                }
                
                pixels[y * cache.Width + x] = color;
            }
        }
        
        texture.SetPixels(pixels);
        texture.Apply();
        
        return texture;
    }
    
    #endregion
}