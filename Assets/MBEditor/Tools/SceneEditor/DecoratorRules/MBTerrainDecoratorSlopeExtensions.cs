using UnityEngine;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

/// <summary>
/// Extension methods and helpers for integrating ArcGIS-style slope calculation
/// into MBTerrainDecorator.
/// 
/// Provides both cached slope map generation and per-pixel slope queries
/// using the proper 3x3 weighted kernel (Horn's algorithm).
/// </summary>
public static class MBTerrainDecoratorSlopeExtensions
{
    #region Cached Slope Map
    
    /// <summary>
    /// Pre-calculated slope map for a terrain, cached for fast per-pixel access.
    /// </summary>
    public class CachedSlopeMap
    {
        public float[,] SlopesDegrees { get; private set; }
        public float[,] SlopesNormalized { get; private set; }
        public int Width { get; private set; }
        public int Height { get; private set; }
        
        private float cellSizeX;
        private float cellSizeY;
        private float heightScale;
        
        /// <summary>
        /// Create a cached slope map from terrain data.
        /// </summary>
        public static CachedSlopeMap Create(TerrainData terrainData, bool useParallel = true)
        {
            var cache = new CachedSlopeMap();
            
            int resolution = terrainData.heightmapResolution;
            cache.Width = resolution;
            cache.Height = resolution;
            cache.cellSizeX = terrainData.size.x / (resolution - 1);
            cache.cellSizeY = terrainData.size.z / (resolution - 1);
            cache.heightScale = terrainData.size.y;
            
            float[,] heights = terrainData.GetHeights(0, 0, resolution, resolution);
            
            if (useParallel)
            {
                cache.SlopesDegrees = SlopeCalculator.CalculateSlopeMapParallel(
                    heights, cache.cellSizeX, cache.cellSizeY, cache.heightScale, 
                    SlopeCalculator.SlopeUnit.Degrees);
            }
            else
            {
                cache.SlopesDegrees = SlopeCalculator.CalculateSlopeMap(
                    heights, cache.cellSizeX, cache.cellSizeY, cache.heightScale,
                    SlopeCalculator.SlopeUnit.Degrees);
            }
            
            // Pre-calculate normalized version
            cache.SlopesNormalized = new float[resolution, resolution];
            for (int y = 0; y < resolution; y++)
            {
                for (int x = 0; x < resolution; x++)
                {
                    cache.SlopesNormalized[x, y] = Mathf.Clamp01(cache.SlopesDegrees[x, y] / 90f);
                }
            }
            
            return cache;
        }
        
        /// <summary>
        /// Get slope at a normalized position (0-1 range).
        /// </summary>
        /// <param name="normX">Normalized X position</param>
        /// <param name="normY">Normalized Y position</param>
        /// <param name="inDegrees">If true, return degrees; if false, return normalized 0-1</param>
        /// <returns>Slope value</returns>
        public float GetSlopeAt(float normX, float normY, bool inDegrees = true)
        {
            int x = Mathf.Clamp(Mathf.FloorToInt(normX * (Width - 1)), 0, Width - 1);
            int y = Mathf.Clamp(Mathf.FloorToInt(normY * (Height - 1)), 0, Height - 1);
            
            return inDegrees ? SlopesDegrees[x, y] : SlopesNormalized[x, y];
        }
        
        /// <summary>
        /// Get slope at a normalized position with bilinear interpolation.
        /// Smoother but slightly slower.
        /// </summary>
        public float GetSlopeAtInterpolated(float normX, float normY, bool inDegrees = true)
        {
            float fx = normX * (Width - 1);
            float fy = normY * (Height - 1);
            
            int x0 = Mathf.FloorToInt(fx);
            int y0 = Mathf.FloorToInt(fy);
            int x1 = Mathf.Min(x0 + 1, Width - 1);
            int y1 = Mathf.Min(y0 + 1, Height - 1);
            
            float tx = fx - x0;
            float ty = fy - y0;
            
            float[,] source = inDegrees ? SlopesDegrees : SlopesNormalized;
            
            float v00 = source[x0, y0];
            float v10 = source[x1, y0];
            float v01 = source[x0, y1];
            float v11 = source[x1, y1];
            
            float v0 = Mathf.Lerp(v00, v10, tx);
            float v1 = Mathf.Lerp(v01, v11, tx);
            
            return Mathf.Lerp(v0, v1, ty);
        }
    }
    
    #endregion
    
    #region Direct Calculation Methods
    
    /// <summary>
    /// Calculate slope at a specific heightmap position using proper ArcGIS algorithm.
    /// Use this for one-off calculations; for repeated access, use CachedSlopeMap.
    /// </summary>
    /// <param name="heights">Heightmap array</param>
    /// <param name="x">X coordinate in heightmap</param>
    /// <param name="y">Y coordinate in heightmap</param>
    /// <param name="cellSizeX">Cell size in world units (X)</param>
    /// <param name="cellSizeY">Cell size in world units (Y)</param>
    /// <param name="heightScale">Height scale (terrain height)</param>
    /// <returns>Slope in degrees</returns>
    public static float CalculateSlopeAtPoint(
        float[,] heights, int x, int y,
        float cellSizeX, float cellSizeY, float heightScale)
    {
        int width = heights.GetLength(0);
        int height = heights.GetLength(1);
        
        // Get 3x3 neighborhood with edge clamping
        float a = GetHeightClamped(heights, x - 1, y + 1, width, height) * heightScale;
        float b = GetHeightClamped(heights, x, y + 1, width, height) * heightScale;
        float c = GetHeightClamped(heights, x + 1, y + 1, width, height) * heightScale;
        float d = GetHeightClamped(heights, x - 1, y, width, height) * heightScale;
        float f = GetHeightClamped(heights, x + 1, y, width, height) * heightScale;
        float g = GetHeightClamped(heights, x - 1, y - 1, width, height) * heightScale;
        float h = GetHeightClamped(heights, x, y - 1, width, height) * heightScale;
        float i = GetHeightClamped(heights, x + 1, y - 1, width, height) * heightScale;
        
        // Calculate dz/dx using weighted kernel
        // [dz/dx] = ((c + 2f + i) - (a + 2d + g)) / (8 * cellSizeX)
        float dzdx = ((c + 2f * f + i) - (a + 2f * d + g)) / (8f * cellSizeX);
        
        // Calculate dz/dy using weighted kernel
        // [dz/dy] = ((g + 2h + i) - (a + 2b + c)) / (8 * cellSizeY)
        float dzdy = ((g + 2f * h + i) - (a + 2f * b + c)) / (8f * cellSizeY);
        
        // Calculate slope in degrees
        float riseRun = Mathf.Sqrt(dzdx * dzdx + dzdy * dzdy);
        return Mathf.Atan(riseRun) * 57.29577951f; // Convert to degrees
    }
    
    /// <summary>
    /// Calculate slope at a normalized terrain position (0-1 range).
    /// </summary>
    public static float CalculateSlopeAtNormalizedPosition(
        float[,] heights,
        float normX, float normY,
        float cellSizeX, float cellSizeY, float heightScale)
    {
        int width = heights.GetLength(0);
        int height = heights.GetLength(1);
        
        int x = Mathf.Clamp(Mathf.FloorToInt(normX * (width - 1)), 0, width - 1);
        int y = Mathf.Clamp(Mathf.FloorToInt(normY * (height - 1)), 0, height - 1);
        
        return CalculateSlopeAtPoint(heights, x, y, cellSizeX, cellSizeY, heightScale);
    }
    
    private static float GetHeightClamped(float[,] heights, int x, int y, int width, int height)
    {
        x = Mathf.Clamp(x, 0, width - 1);
        y = Mathf.Clamp(y, 0, height - 1);
        return heights[x, y];
    }
    
    #endregion
    
    #region Weight Calculation for Decorator
    
    /// <summary>
    /// Calculate slope-based weight for terrain decoration.
    /// Matches the interface expected by MBTerrainDecorator rules.
    /// </summary>
    /// <param name="heights">Cached heightmap</param>
    /// <param name="normX">Normalized X position (0-1)</param>
    /// <param name="normY">Normalized Y position (0-1)</param>
    /// <param name="minSlope">Minimum slope threshold (degrees or normalized depending on mode)</param>
    /// <param name="maxSlope">Maximum slope threshold</param>
    /// <param name="falloff">Falloff distance for smooth transitions</param>
    /// <param name="cellSizeX">Cell size X</param>
    /// <param name="cellSizeY">Cell size Y</param>
    /// <param name="heightScale">Height scale</param>
    /// <param name="useDegrees">If true, min/max are in degrees; if false, normalized 0-1</param>
    /// <returns>Weight value 0-1</returns>
    public static float CalculateSlopeWeight(
        float[,] heights,
        float normX, float normY,
        float minSlope, float maxSlope, float falloff,
        float cellSizeX, float cellSizeY, float heightScale,
        bool useDegrees = false)
    {
        float slopeDegrees = CalculateSlopeAtNormalizedPosition(
            heights, normX, normY, cellSizeX, cellSizeY, heightScale);
        
        float slope;
        if (useDegrees)
        {
            slope = slopeDegrees;
        }
        else
        {
            // Convert to normalized 0-1 range (where 90° = 1.0)
            slope = slopeDegrees / 90f;
        }
        
        return CalculateWeightWithFalloff(slope, minSlope, maxSlope, falloff);
    }
    
    /// <summary>
    /// Calculate slope weight using a pre-cached slope map.
    /// Faster for repeated access.
    /// </summary>
    public static float CalculateSlopeWeight(
        CachedSlopeMap slopeCache,
        float normX, float normY,
        float minSlope, float maxSlope, float falloff,
        bool useDegrees = false)
    {
        float slope = slopeCache.GetSlopeAt(normX, normY, useDegrees);
        return CalculateWeightWithFalloff(slope, minSlope, maxSlope, falloff);
    }
    
    /// <summary>
    /// Standard weight calculation with falloff, matching MBTerrainDecorator.CalculateWeigth
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
    
    #region Burst Job for Decorator Integration
    
    /// <summary>
    /// Burst-compiled slope calculation matching MBTerrainDecorator job format.
    /// Can be used as a drop-in replacement for the simple slope calculation in DecorateJob.
    /// </summary>
    [BurstCompile(FloatPrecision.Standard, FloatMode.Fast)]
    public static float CalculateSlopeBurst(
        in NativeArray<float> heights,
        int heightMapWidth, int heightMapHeight,
        float normX, float normY,
        float cellSizeX, float cellSizeY, float heightScale)
    {
        int x = math.clamp((int)(normX * (heightMapWidth - 1)), 0, heightMapWidth - 1);
        int y = math.clamp((int)(normY * (heightMapHeight - 1)), 0, heightMapHeight - 1);
        
        // Get 3x3 neighborhood
        float a = GetHeightBurst(heights, x - 1, y + 1, heightMapWidth, heightMapHeight) * heightScale;
        float b = GetHeightBurst(heights, x, y + 1, heightMapWidth, heightMapHeight) * heightScale;
        float c = GetHeightBurst(heights, x + 1, y + 1, heightMapWidth, heightMapHeight) * heightScale;
        float d = GetHeightBurst(heights, x - 1, y, heightMapWidth, heightMapHeight) * heightScale;
        float f = GetHeightBurst(heights, x + 1, y, heightMapWidth, heightMapHeight) * heightScale;
        float g = GetHeightBurst(heights, x - 1, y - 1, heightMapWidth, heightMapHeight) * heightScale;
        float h = GetHeightBurst(heights, x, y - 1, heightMapWidth, heightMapHeight) * heightScale;
        float i = GetHeightBurst(heights, x + 1, y - 1, heightMapWidth, heightMapHeight) * heightScale;
        
        // Horn's algorithm
        float dzdx = ((c + 2f * f + i) - (a + 2f * d + g)) / (8f * cellSizeX);
        float dzdy = ((g + 2f * h + i) - (a + 2f * b + c)) / (8f * cellSizeY);
        
        float riseRun = math.sqrt(dzdx * dzdx + dzdy * dzdy);
        return math.atan(riseRun) * 57.29577951f;
    }
    
    private static float GetHeightBurst(in NativeArray<float> heights, int x, int y, int width, int height)
    {
        x = math.clamp(x, 0, width - 1);
        y = math.clamp(y, 0, height - 1);
        return heights[y * width + x];
    }
    
    #endregion
}