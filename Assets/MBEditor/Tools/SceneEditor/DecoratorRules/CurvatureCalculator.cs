using UnityEngine;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

/// <summary>
/// Calculates terrain curvature using ArcGIS methodology (Zeverbergen & Thorne, 1987).
/// 
/// Curvature represents the second derivative of the surface - the "slope of the slope".
/// It describes how the slope changes across the terrain surface.
/// 
/// Grid Layout (3x3 window):
/// Z1 Z2 Z3
/// Z4 Z5 Z6
/// Z7 Z8 Z9
/// 
/// Where Z5 is the center cell being evaluated.
/// 
/// References:
/// - Zeverbergen, L. W., and C. R. Thorne. 1987. Quantitative Analysis of Land Surface Topography.
/// - Moore, I. D., R. B. Grayson, and A. R. Landson. 1991. Digital Terrain Modelling.
/// </summary>
public static class CurvatureCalculator
{
    #region Enums
    
    /// <summary>
    /// Types of curvature calculation.
    /// </summary>
    public enum CurvatureType
    {
        /// <summary>
        /// Standard/Total curvature - Laplacian of the surface.
        /// Positive = convex (ridge), Negative = concave (valley), Zero = flat or saddle.
        /// </summary>
        Standard,
        
        /// <summary>
        /// Profile curvature - curvature in the direction of maximum slope.
        /// Affects acceleration/deceleration of flow.
        /// Positive = convex slope (decelerating), Negative = concave slope (accelerating).
        /// </summary>
        Profile,
        
        /// <summary>
        /// Plan/Planform curvature - curvature perpendicular to slope direction.
        /// Affects convergence/divergence of flow.
        /// Positive = diverging flow, Negative = converging flow.
        /// </summary>
        Plan
    }
    
    #endregion

    #region Data Structures
    
    /// <summary>
    /// Contains all three curvature values for a single point.
    /// </summary>
    public struct CurvatureResult
    {
        public float Standard;
        public float Profile;
        public float Plan;
        
        /// <summary>
        /// Get curvature by type.
        /// </summary>
        public float Get(CurvatureType type)
        {
            switch (type)
            {
                case CurvatureType.Profile: return Profile;
                case CurvatureType.Plan: return Plan;
                default: return Standard;
            }
        }
    }
    
    /// <summary>
    /// Statistics for a curvature map, used for normalization.
    /// </summary>
    public struct CurvatureStatistics
    {
        public float MinStandard, MaxStandard;
        public float MinProfile, MaxProfile;
        public float MinPlan, MaxPlan;
        
        public float GetMin(CurvatureType type)
        {
            switch (type)
            {
                case CurvatureType.Profile: return MinProfile;
                case CurvatureType.Plan: return MinPlan;
                default: return MinStandard;
            }
        }
        
        public float GetMax(CurvatureType type)
        {
            switch (type)
            {
                case CurvatureType.Profile: return MaxProfile;
                case CurvatureType.Plan: return MaxPlan;
                default: return MaxStandard;
            }
        }
    }
    
    #endregion

    #region Sequential Calculation
    
    /// <summary>
    /// Calculate curvature at a specific heightmap position.
    /// </summary>
    /// <param name="heights">Heightmap array [y,x] indexed</param>
    /// <param name="x">X coordinate in heightmap</param>
    /// <param name="y">Y coordinate in heightmap</param>
    /// <param name="cellSize">Cell size in world units (L parameter)</param>
    /// <param name="heightScale">Height scale (terrain height)</param>
    /// <returns>All three curvature values</returns>
    public static CurvatureResult CalculateAtPoint(
        float[,] heights, int x, int y,
        float cellSize, float heightScale)
    {
        int width = heights.GetLength(1);
        int height = heights.GetLength(0);
        
        return CalculateAtPointInternal(heights, x, y, width, height, cellSize, heightScale);
    }
    
    /// <summary>
    /// Calculate curvature at a normalized terrain position (0-1 range).
    /// </summary>
    public static CurvatureResult CalculateAtNormalizedPosition(
        float[,] heights,
        float normX, float normY,
        float cellSize, float heightScale)
    {
        int width = heights.GetLength(1);
        int height = heights.GetLength(0);
        
        // Decorator convention: normX maps to row, normY maps to col
        int x = Mathf.Clamp(Mathf.FloorToInt(normY * (width - 1)), 0, width - 1);
        int y = Mathf.Clamp(Mathf.FloorToInt(normX * (height - 1)), 0, height - 1);
        
        return CalculateAtPointInternal(heights, x, y, width, height, cellSize, heightScale);
    }
    
    private static CurvatureResult CalculateAtPointInternal(
        float[,] heights, int x, int y, int width, int height,
        float cellSize, float heightScale)
    {
        // Get 3x3 neighborhood with edge clamping
        // Grid layout:
        // z1 z2 z3
        // z4 z5 z6
        // z7 z8 z9
        
        int x0 = Mathf.Max(0, x - 1);
        int x1 = x;
        int x2 = Mathf.Min(width - 1, x + 1);
        int y0 = Mathf.Max(0, y - 1);
        int y1 = y;
        int y2 = Mathf.Min(height - 1, y + 1);
        
        // Heights scaled to world units
        float z1 = heights[y0, x0] * heightScale;
        float z2 = heights[y0, x1] * heightScale;
        float z3 = heights[y0, x2] * heightScale;
        float z4 = heights[y1, x0] * heightScale;
        float z5 = heights[y1, x1] * heightScale;
        float z6 = heights[y1, x2] * heightScale;
        float z7 = heights[y2, x0] * heightScale;
        float z8 = heights[y2, x1] * heightScale;
        float z9 = heights[y2, x2] * heightScale;
        
        return CalculateCurvatureFromNeighborhood(z1, z2, z3, z4, z5, z6, z7, z8, z9, cellSize);
    }
    
    /// <summary>
    /// Core curvature calculation from 3x3 neighborhood values.
    /// Implements ArcGIS polynomial coefficient method.
    /// </summary>
    private static CurvatureResult CalculateCurvatureFromNeighborhood(
        float z1, float z2, float z3,
        float z4, float z5, float z6,
        float z7, float z8, float z9,
        float L)
    {
        float L2 = L * L;
        
        // Calculate polynomial coefficients (ArcGIS formulas)
        // D = ∂²z/∂x² = [(Z4 + Z6) / 2 - Z5] / L²
        // E = ∂²z/∂y² = [(Z2 + Z8) / 2 - Z5] / L²
        // F = ∂²z/∂x∂y = (-Z1 + Z3 + Z7 - Z9) / 4L²
        // G = ∂z/∂x = (-Z4 + Z6) / 2L
        // H = ∂z/∂y = (Z2 - Z8) / 2L
        
        float D = ((z4 + z6) * 0.5f - z5) / L2;
        float E = ((z2 + z8) * 0.5f - z5) / L2;
        float F = (-z1 + z3 + z7 - z9) / (4f * L2);
        float G = (-z4 + z6) / (2f * L);
        float H = (z2 - z8) / (2f * L);
        
        var result = new CurvatureResult();
        
        // Standard curvature = -2(D + E) * 100
        // This is the negative Laplacian scaled by 100
        result.Standard = -2f * (D + E) * 100f;
        
        // Profile and Plan curvature require gradient magnitude
        float G2 = G * G;
        float H2 = H * H;
        float p = G2 + H2; // gradient magnitude squared
        
        if (p < 0.00001f)
        {
            // Flat area - no meaningful directional curvature
            result.Profile = 0f;
            result.Plan = 0f;
        }
        else
        {
            // Profile curvature: curvature in direction of steepest descent
            // Affects flow acceleration (erosion/deposition)
            result.Profile = -2f * (D * G2 + E * H2 + F * G * H) / p * 100f;
            
            // Plan curvature: curvature perpendicular to slope
            // Affects flow convergence/divergence
            result.Plan = -2f * (D * H2 + E * G2 - F * G * H) / p * 100f;
        }
        
        return result;
    }
    
    #endregion

    #region Map Generation
    
    /// <summary>
    /// Calculate curvature map for entire heightmap (sequential).
    /// </summary>
    public static void CalculateCurvatureMap(
        float[,] heights,
        float cellSize, float heightScale,
        out float[,] standard, out float[,] profile, out float[,] plan,
        out CurvatureStatistics stats)
    {
        int width = heights.GetLength(1);
        int height = heights.GetLength(0);
        
        standard = new float[height, width];
        profile = new float[height, width];
        plan = new float[height, width];
        
        stats = new CurvatureStatistics
        {
            MinStandard = float.MaxValue, MaxStandard = float.MinValue,
            MinProfile = float.MaxValue, MaxProfile = float.MinValue,
            MinPlan = float.MaxValue, MaxPlan = float.MinValue
        };
        
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                var result = CalculateAtPointInternal(heights, x, y, width, height, cellSize, heightScale);
                
                standard[y, x] = result.Standard;
                profile[y, x] = result.Profile;
                plan[y, x] = result.Plan;
                
                // Update statistics
                if (!float.IsNaN(result.Standard) && !float.IsInfinity(result.Standard))
                {
                    stats.MinStandard = Mathf.Min(stats.MinStandard, result.Standard);
                    stats.MaxStandard = Mathf.Max(stats.MaxStandard, result.Standard);
                }
                if (!float.IsNaN(result.Profile) && !float.IsInfinity(result.Profile))
                {
                    stats.MinProfile = Mathf.Min(stats.MinProfile, result.Profile);
                    stats.MaxProfile = Mathf.Max(stats.MaxProfile, result.Profile);
                }
                if (!float.IsNaN(result.Plan) && !float.IsInfinity(result.Plan))
                {
                    stats.MinPlan = Mathf.Min(stats.MinPlan, result.Plan);
                    stats.MaxPlan = Mathf.Max(stats.MaxPlan, result.Plan);
                }
            }
        }
        
        // Ensure valid ranges
        if (stats.MinStandard == float.MaxValue) { stats.MinStandard = 0; stats.MaxStandard = 0; }
        if (stats.MinProfile == float.MaxValue) { stats.MinProfile = 0; stats.MaxProfile = 0; }
        if (stats.MinPlan == float.MaxValue) { stats.MinPlan = 0; stats.MaxPlan = 0; }
    }
    
    /// <summary>
    /// Calculate curvature map using parallel jobs (faster for large terrains).
    /// </summary>
    public static void CalculateCurvatureMapParallel(
        float[,] heights,
        float cellSize, float heightScale,
        out float[,] standard, out float[,] profile, out float[,] plan,
        out CurvatureStatistics stats)
    {
        int width = heights.GetLength(1);
        int height = heights.GetLength(0);
        int totalCells = width * height;
        
        // Allocate native arrays
        var heightsFlat = new NativeArray<float>(totalCells, Allocator.TempJob);
        var standardFlat = new NativeArray<float>(totalCells, Allocator.TempJob);
        var profileFlat = new NativeArray<float>(totalCells, Allocator.TempJob);
        var planFlat = new NativeArray<float>(totalCells, Allocator.TempJob);
        
        // Copy heights to flat array
        for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
                heightsFlat[y * width + x] = heights[y, x];
        
        // Schedule job
        var job = new CalculateCurvatureMapJob
        {
            heights = heightsFlat,
            standardCurvature = standardFlat,
            profileCurvature = profileFlat,
            planCurvature = planFlat,
            width = width,
            height = height,
            cellSize = cellSize,
            heightScale = heightScale
        };
        
        job.Schedule(totalCells, 64).Complete();
        
        // Allocate output arrays
        standard = new float[height, width];
        profile = new float[height, width];
        plan = new float[height, width];
        
        stats = new CurvatureStatistics
        {
            MinStandard = float.MaxValue, MaxStandard = float.MinValue,
            MinProfile = float.MaxValue, MaxProfile = float.MinValue,
            MinPlan = float.MaxValue, MaxPlan = float.MinValue
        };
        
        // Copy results and calculate statistics
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                int idx = y * width + x;
                float s = standardFlat[idx];
                float p = profileFlat[idx];
                float l = planFlat[idx];
                
                standard[y, x] = s;
                profile[y, x] = p;
                plan[y, x] = l;
                
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
        
        // Cleanup
        heightsFlat.Dispose();
        standardFlat.Dispose();
        profileFlat.Dispose();
        planFlat.Dispose();
        
        // Ensure valid ranges
        if (stats.MinStandard == float.MaxValue) { stats.MinStandard = 0; stats.MaxStandard = 0; }
        if (stats.MinProfile == float.MaxValue) { stats.MinProfile = 0; stats.MaxProfile = 0; }
        if (stats.MinPlan == float.MaxValue) { stats.MinPlan = 0; stats.MaxPlan = 0; }
    }
    
    #endregion

    #region Burst Jobs
    
    /// <summary>
    /// Burst-compiled job for parallel curvature map calculation.
    /// </summary>
    [BurstCompile(FloatPrecision.Standard, FloatMode.Fast)]
    public struct CalculateCurvatureMapJob : IJobParallelFor
    {
        [ReadOnly] public NativeArray<float> heights;
        [WriteOnly] public NativeArray<float> standardCurvature;
        [WriteOnly] public NativeArray<float> profileCurvature;
        [WriteOnly] public NativeArray<float> planCurvature;
        [ReadOnly] public int width;
        [ReadOnly] public int height;
        [ReadOnly] public float cellSize;
        [ReadOnly] public float heightScale;
        
        public void Execute(int index)
        {
            int x = index % width;
            int y = index / width;
            
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
            
            // Standard curvature
            standardCurvature[index] = -2f * (D + E) * 100f;
            
            // Profile and Plan
            float G2 = G * G;
            float H2 = H * H;
            float p = G2 + H2;
            
            if (p < 0.00001f)
            {
                profileCurvature[index] = 0f;
                planCurvature[index] = 0f;
            }
            else
            {
                profileCurvature[index] = -2f * (D * G2 + E * H2 + F * G * H) / p * 100f;
                planCurvature[index] = -2f * (D * H2 + E * G2 - F * G * H) / p * 100f;
            }
        }
    }
    
    /// <summary>
    /// Burst-compatible curvature calculation for inline use in decoration jobs.
    /// </summary>
    [BurstCompile]
    public static void CalculateCurvatureBurst(
        in NativeArray<float> heights,
        int width, int height,
        int x, int y,
        float cellSize, float heightScale,
        out float standard, out float profile, out float plan)
    {
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
        
        float D = ((z4 + z6) * 0.5f - z5) / L2;
        float E = ((z2 + z8) * 0.5f - z5) / L2;
        float F = (-z1 + z3 + z7 - z9) / (4f * L2);
        float G = (-z4 + z6) / (2f * L);
        float H = (z2 - z8) / (2f * L);
        
        standard = -2f * (D + E) * 100f;
        
        float G2 = G * G;
        float H2 = H * H;
        float p = G2 + H2;
        
        if (p < 0.00001f)
        {
            profile = 0f;
            plan = 0f;
        }
        else
        {
            profile = -2f * (D * G2 + E * H2 + F * G * H) / p * 100f;
            plan = -2f * (D * H2 + E * G2 - F * G * H) / p * 100f;
        }
    }
    
    #endregion

    #region Utility
    
    /// <summary>
    /// Normalize a curvature value to 0-1 range.
    /// </summary>
    public static float Normalize(float value, float min, float max)
    {
        float range = max - min;
        if (range < 0.0001f)
            return 0.5f;
        return Mathf.Clamp01((value - min) / range);
    }
    
    /// <summary>
    /// Get a description of what a curvature value means.
    /// </summary>
    public static string GetCurvatureDescription(CurvatureType type, float value)
    {
        switch (type)
        {
            case CurvatureType.Standard:
                if (value > 0.1f) return "Convex (ridge/peak)";
                if (value < -0.1f) return "Concave (valley/depression)";
                return "Flat or saddle";
                
            case CurvatureType.Profile:
                if (value > 0.1f) return "Convex slope (flow decelerates)";
                if (value < -0.1f) return "Concave slope (flow accelerates)";
                return "Linear slope";
                
            case CurvatureType.Plan:
                if (value > 0.1f) return "Diverging flow (spreading)";
                if (value < -0.1f) return "Converging flow (concentrating)";
                return "Parallel flow";
                
            default:
                return "";
        }
    }
    
    #endregion
}