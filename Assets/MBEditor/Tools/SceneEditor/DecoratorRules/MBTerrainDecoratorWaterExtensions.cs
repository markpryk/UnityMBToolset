using UnityEngine;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

/// <summary>
/// Extension methods and helpers for integrating water level and shoreline distance
/// calculation into MBTerrainDecorator.
/// 
/// Provides both cached water map generation and per-pixel water queries
/// using JFA-based shoreline distance fields.
/// 
/// Follows the same architecture as MBTerrainDecoratorSlopeExtensions and
/// MBTerrainDecoratorCurvatureExtensions for consistency.
/// </summary>
public static class MBTerrainDecoratorWaterExtensions
{
    #region Cached Water Map
    
    /// <summary>
    /// Pre-calculated water level data for a terrain, cached for fast per-pixel access.
    /// Contains both height-relative-to-water and shoreline distance data.
    /// </summary>
    public class CachedWaterMap
    {
        /// <summary>World-space height relative to water level per cell. Negative = submerged.</summary>
        public float[,] RelativeHeights { get; private set; }
        
        /// <summary>Distance to nearest water edge in world units per cell.</summary>
        public float[,] ShorelineDistances { get; private set; }
        
        public int Width { get; private set; }
        public int Height { get; private set; }
        
        public WaterLevelCalculator.WaterStatistics Statistics { get; private set; }
        
        private float terrainY;
        private float heightScale;
        private float waterLevel;
        private float maxDistance;
        
        /// <summary>
        /// Create a cached water map from terrain data.
        /// </summary>
        /// <param name="terrainData">Unity terrain data</param>
        /// <param name="terrainY">Terrain transform Y position</param>
        /// <param name="waterLevel">World-space water level</param>
        /// <param name="maxDistance">Maximum shoreline distance to compute</param>
        /// <param name="useParallel">Use parallel JFA calculation</param>
        public static CachedWaterMap Create(
            TerrainData terrainData, float terrainY, float waterLevel,
            float maxDistance = 50f, bool useParallel = true)
        {
            var cache = new CachedWaterMap();
            
            int resolution = terrainData.heightmapResolution;
            cache.Width = resolution;
            cache.Height = resolution;
            cache.terrainY = terrainY;
            cache.heightScale = terrainData.size.y;
            cache.waterLevel = waterLevel;
            cache.maxDistance = maxDistance;
            
            float cellSizeX = terrainData.size.x / (resolution - 1);
            float cellSizeZ = terrainData.size.z / (resolution - 1);
            
            float[,] heights = terrainData.GetHeights(0, 0, resolution, resolution);
            
            // Calculate relative heights
            cache.RelativeHeights = new float[resolution, resolution];
            for (int y = 0; y < resolution; y++)
                for (int x = 0; x < resolution; x++)
                    cache.RelativeHeights[y, x] = terrainY + heights[y, x] * cache.heightScale - waterLevel;
            
            // Calculate shoreline distance
            WaterLevelCalculator.WaterStatistics stats;
            
            if (useParallel)
            {
                cache.ShorelineDistances = WaterLevelCalculator.CalculateShorelineDistanceMapParallel(
                    heights, terrainY, cache.heightScale, waterLevel,
                    cellSizeX, cellSizeZ, maxDistance, out stats);
            }
            else
            {
                cache.ShorelineDistances = WaterLevelCalculator.CalculateShorelineDistanceMap(
                    heights, terrainY, cache.heightScale, waterLevel,
                    cellSizeX, cellSizeZ, maxDistance, out stats);
            }
            
            cache.Statistics = stats;
            
            return cache;
        }
        
        /// <summary>
        /// Get relative height at a normalized position (0-1 range).
        /// </summary>
        public float GetRelativeHeightAt(float normX, float normY)
        {
            // Decorator convention: normX maps to row, normY maps to col
            int col = Mathf.Clamp(Mathf.FloorToInt(normY * (Width - 1)), 0, Width - 1);
            int row = Mathf.Clamp(Mathf.FloorToInt(normX * (Height - 1)), 0, Height - 1);
            return RelativeHeights[row, col];
        }
        
        /// <summary>
        /// Get shoreline distance at a normalized position (0-1 range).
        /// </summary>
        public float GetShorelineDistanceAt(float normX, float normY)
        {
            // Decorator convention: normX maps to row, normY maps to col
            int col = Mathf.Clamp(Mathf.FloorToInt(normY * (Width - 1)), 0, Width - 1);
            int row = Mathf.Clamp(Mathf.FloorToInt(normX * (Height - 1)), 0, Height - 1);
            return ShorelineDistances[row, col];
        }
        
        /// <summary>
        /// Get relative height at a normalized position with bilinear interpolation.
        /// </summary>
        public float GetRelativeHeightAtInterpolated(float normX, float normY)
        {
            return BilinearSample(RelativeHeights, normX, normY);
        }
        
        /// <summary>
        /// Get shoreline distance at a normalized position with bilinear interpolation.
        /// </summary>
        public float GetShorelineDistanceAtInterpolated(float normX, float normY)
        {
            return BilinearSample(ShorelineDistances, normX, normY);
        }
        
        /// <summary>
        /// Get the shoreline distance attenuation factor (1 at shore, 0 at maxDistance).
        /// </summary>
        public float GetShorelineAttenuationAt(float normX, float normY)
        {
            float dist = GetShorelineDistanceAt(normX, normY);
            return 1f - Mathf.Clamp01(dist / maxDistance);
        }
        
        /// <summary>
        /// Get the shoreline distance attenuation with interpolation.
        /// </summary>
        public float GetShorelineAttenuationAtInterpolated(float normX, float normY)
        {
            float dist = GetShorelineDistanceAtInterpolated(normX, normY);
            return 1f - Mathf.Clamp01(dist / maxDistance);
        }
        
        private float BilinearSample(float[,] data, float normX, float normY)
        {
            // Decorator convention: normX maps to row, normY maps to col
            float fx = normY * (Width - 1);
            float fy = normX * (Height - 1);
            
            int x0 = Mathf.FloorToInt(fx);
            int y0 = Mathf.FloorToInt(fy);
            int x1 = Mathf.Min(x0 + 1, Width - 1);
            int y1 = Mathf.Min(y0 + 1, Height - 1);
            x0 = Mathf.Clamp(x0, 0, Width - 1);
            y0 = Mathf.Clamp(y0, 0, Height - 1);
            
            float tx = fx - x0;
            float ty = fy - y0;
            
            float v00 = data[y0, x0];
            float v10 = data[y0, x1];
            float v01 = data[y1, x0];
            float v11 = data[y1, x1];
            
            return Mathf.Lerp(
                Mathf.Lerp(v00, v10, tx),
                Mathf.Lerp(v01, v11, tx),
                ty);
        }
    }
    
    #endregion
    
    #region Direct Calculation Methods
    
    /// <summary>
    /// Calculate water-relative height at a specific heightmap position.
    /// Use this for one-off calculations; for repeated access, use CachedWaterMap.
    /// </summary>
    public static float CalculateRelativeHeightAtPoint(
        float[,] heights, int x, int y,
        float terrainY, float heightScale, float waterLevel)
    {
        return WaterLevelCalculator.CalculateRelativeHeightAtPoint(
            heights, x, y, terrainY, heightScale, waterLevel);
    }
    
    /// <summary>
    /// Calculate relative height at a normalized terrain position (0-1 range).
    /// </summary>
    public static float CalculateRelativeHeightAtNormalizedPosition(
        float[,] heights, float normX, float normY,
        float terrainY, float heightScale, float waterLevel)
    {
        return WaterLevelCalculator.CalculateRelativeHeightAtNormalizedPosition(
            heights, normX, normY, terrainY, heightScale, waterLevel);
    }
    
    #endregion
    
    #region Weight Calculation for Decorator
    
    /// <summary>
    /// Calculate water-level-based weight for terrain decoration.
    /// Matches the interface expected by MBTerrainDecorator rules.
    /// </summary>
    /// <param name="heights">Cached heightmap (normalized 0-1)</param>
    /// <param name="normX">Normalized X position (0-1)</param>
    /// <param name="normY">Normalized Y position (0-1)</param>
    /// <param name="minRelHeight">Minimum relative height threshold</param>
    /// <param name="maxRelHeight">Maximum relative height threshold</param>
    /// <param name="falloff">Falloff distance for smooth transitions</param>
    /// <param name="terrainY">Terrain Y position</param>
    /// <param name="heightScale">Height scale (terrain size.y)</param>
    /// <param name="waterLevel">World-space water level</param>
    /// <returns>Weight value 0-1</returns>
    public static float CalculateWaterWeight(
        float[,] heights,
        float normX, float normY,
        float minRelHeight, float maxRelHeight, float falloff,
        float terrainY, float heightScale, float waterLevel)
    {
        float relHeight = CalculateRelativeHeightAtNormalizedPosition(
            heights, normX, normY, terrainY, heightScale, waterLevel);
        
        return CalculateWeightWithFalloff(relHeight, minRelHeight, maxRelHeight, falloff);
    }
    
    /// <summary>
    /// Calculate water-level weight with shoreline distance attenuation using a pre-cached map.
    /// Faster for repeated access.
    /// </summary>
    /// <param name="waterCache">Pre-calculated water map</param>
    /// <param name="normX">Normalized X position (0-1)</param>
    /// <param name="normY">Normalized Y position (0-1)</param>
    /// <param name="minRelHeight">Minimum relative height threshold</param>
    /// <param name="maxRelHeight">Maximum relative height threshold</param>
    /// <param name="falloff">Falloff distance for smooth transitions</param>
    /// <param name="useShoreline">Whether to apply shoreline distance attenuation</param>
    /// <param name="maxShorelineDistance">Max distance for attenuation (only if useShoreline)</param>
    /// <returns>Weight value 0-1</returns>
    public static float CalculateWaterWeight(
        CachedWaterMap waterCache,
        float normX, float normY,
        float minRelHeight, float maxRelHeight, float falloff,
        bool useShoreline, float maxShorelineDistance)
    {
        float relHeight = waterCache.GetRelativeHeightAt(normX, normY);
        float weight = CalculateWeightWithFalloff(relHeight, minRelHeight, maxRelHeight, falloff);
        
        if (useShoreline && maxShorelineDistance > 0.001f)
        {
            float dist = waterCache.GetShorelineDistanceAt(normX, normY);
            float attenuation = 1f - Mathf.Clamp01(dist / maxShorelineDistance);
            weight *= attenuation;
        }
        
        return weight;
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
    
    #region Burst-Compatible Functions for Decoration Jobs
    
    /// <summary>
    /// Burst-compiled water level weight calculation.
    /// Reads directly from cached height data in NativeArray format.
    /// </summary>
    [BurstCompile(FloatPrecision.Standard, FloatMode.Fast)]
    public static float CalculateWaterWeightBurst(
        in NativeArray<float> heights,
        int heightMapWidth, int heightMapHeight,
        float normX, float normY,
        float minRelHeight, float maxRelHeight, float falloff,
        float terrainY, float heightScale, float waterLevel)
    {
        // Decorator convention: normX maps to row, normY maps to col
        int x = math.clamp((int)(normY * (heightMapWidth - 1)), 0, heightMapWidth - 1);
        int y = math.clamp((int)(normX * (heightMapHeight - 1)), 0, heightMapHeight - 1);
        
        float normalizedHeight = heights[y * heightMapWidth + x];
        float worldY = terrainY + normalizedHeight * heightScale;
        float relHeight = worldY - waterLevel;
        
        return CalculateWeightBurst(relHeight, minRelHeight, maxRelHeight, falloff);
    }
    
    /// <summary>
    /// Burst-compiled water level weight with shoreline distance attenuation.
    /// Uses pre-cached shoreline distance data.
    /// </summary>
    [BurstCompile(FloatPrecision.Standard, FloatMode.Fast)]
    public static float CalculateWaterWeightWithShorelineBurst(
        in NativeArray<float> heights,
        in NativeArray<float> shorelineDistances,
        int heightMapWidth, int heightMapHeight,
        float normX, float normY,
        float minRelHeight, float maxRelHeight, float falloff,
        float terrainY, float heightScale, float waterLevel,
        float maxShorelineDistance)
    {
        // Decorator convention: normX maps to row, normY maps to col
        int x = math.clamp((int)(normY * (heightMapWidth - 1)), 0, heightMapWidth - 1);
        int y = math.clamp((int)(normX * (heightMapHeight - 1)), 0, heightMapHeight - 1);
        int idx = y * heightMapWidth + x;
        
        float normalizedHeight = heights[idx];
        float worldY = terrainY + normalizedHeight * heightScale;
        float relHeight = worldY - waterLevel;
        
        float weight = CalculateWeightBurst(relHeight, minRelHeight, maxRelHeight, falloff);
        
        // Apply shoreline distance attenuation
        if (shorelineDistances.IsCreated && shorelineDistances.Length > 0 && maxShorelineDistance > 0.001f)
        {
            float dist = shorelineDistances[idx];
            float attenuation = 1f - math.clamp(dist / maxShorelineDistance, 0f, 1f);
            weight *= attenuation;
        }
        
        return weight;
    }
    
    /// <summary>
    /// Burst-compiled lookup from pre-calculated shoreline distance map.
    /// </summary>
    [BurstCompile(FloatPrecision.Standard, FloatMode.Fast)]
    public static float GetShorelineDistanceFromCacheBurst(
        in NativeArray<float> shorelineDistances,
        int width, int height,
        float normX, float normY)
    {
        // Decorator convention: normX maps to row, normY maps to col
        int x = math.clamp((int)(normY * (width - 1)), 0, width - 1);
        int y = math.clamp((int)(normX * (height - 1)), 0, height - 1);
        return shorelineDistances[y * width + x];
    }
    
    [BurstCompile(FloatPrecision.Standard, FloatMode.Fast)]
    private static float CalculateWeightBurst(float value, float min, float max, float falloff)
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
        
        return math.clamp(weight, 0f, 1f);
    }
    
    #endregion
    
    #region Visualization Helpers
    
    /// <summary>
    /// Create a debug visualization texture showing water/land and shoreline distance.
    /// Blue = underwater, Green = land, White = shoreline edge.
    /// </summary>
    public static Texture2D CreateVisualization(CachedWaterMap cache)
    {
        Texture2D texture = new Texture2D(cache.Width, cache.Height, TextureFormat.RGB24, false);
        Color[] pixels = new Color[cache.Width * cache.Height];
        
        float maxDist = cache.Statistics.MaxShorelineDistance;
        if (maxDist < 0.001f) maxDist = 1f;
        
        for (int y = 0; y < cache.Height; y++)
        {
            for (int x = 0; x < cache.Width; x++)
            {
                float relH = cache.RelativeHeights[y, x];
                float dist = cache.ShorelineDistances[y, x];
                float distNorm = Mathf.Clamp01(dist / maxDist);
                
                Color color;
                if (relH <= 0)
                {
                    // Underwater: dark blue to light blue based on distance from shore
                    color = Color.Lerp(new Color(0.2f, 0.5f, 1f), new Color(0f, 0.1f, 0.4f), distNorm);
                }
                else
                {
                    // Land: white at shoreline, green further away
                    color = Color.Lerp(Color.white, new Color(0.2f, 0.6f, 0.2f), distNorm);
                }
                
                pixels[y * cache.Width + x] = color;
            }
        }
        
        texture.SetPixels(pixels);
        texture.Apply();
        
        return texture;
    }
    
    /// <summary>
    /// Create a visualization of shoreline distance attenuation only.
    /// White = at shoreline, Black = at max distance.
    /// </summary>
    public static Texture2D CreateAttenuationVisualization(CachedWaterMap cache, float maxDistance)
    {
        Texture2D texture = new Texture2D(cache.Width, cache.Height, TextureFormat.RGB24, false);
        Color[] pixels = new Color[cache.Width * cache.Height];
        
        for (int y = 0; y < cache.Height; y++)
        {
            for (int x = 0; x < cache.Width; x++)
            {
                float dist = cache.ShorelineDistances[y, x];
                float attenuation = 1f - Mathf.Clamp01(dist / maxDistance);
                pixels[y * cache.Width + x] = new Color(attenuation, attenuation, attenuation);
            }
        }
        
        texture.SetPixels(pixels);
        texture.Apply();
        
        return texture;
    }
    
    #endregion
}