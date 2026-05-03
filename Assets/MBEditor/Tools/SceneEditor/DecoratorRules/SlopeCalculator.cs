using System;
using UnityEngine;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

/// <summary>
/// Terrain slope calculation utilities implementing industry-standard algorithms.
/// 
/// Based on ArcGIS Spatial Analyst slope methodology:
/// https://pro.arcgis.com/en/pro-app/3.4/tool-reference/spatial-analyst/how-slope-works.htm
/// 
/// Implements the planar method using third-order finite difference estimator (Horn's algorithm)
/// which uses a weighted 3x3 kernel for more accurate slope calculation than simple gradients.
/// 
/// The algorithm:
/// 1. For each cell, examines the 3x3 neighborhood
/// 2. Calculates rate of change in X direction: [dz/dx] using weighted neighbors (c,f,i vs a,d,g)
/// 3. Calculates rate of change in Y direction: [dz/dy] using weighted neighbors (g,h,i vs a,b,c)
/// 4. Combines: slope = atan(sqrt([dz/dx]² + [dz/dy]²))
/// 
/// Neighbor layout:
///   a b c
///   d e f
///   g h i
/// Where 'e' is the center cell being calculated
/// </summary>
public static class SlopeCalculator
{
    #region Enums
    
    /// <summary>
    /// Output units for slope calculation
    /// </summary>
    public enum SlopeUnit
    {
        /// <summary>Slope in degrees (0-90)</summary>
        Degrees,
        
        /// <summary>Slope as percent rise (0-infinity, typically 0-100+ for steep terrain)</summary>
        PercentRise,
        
        /// <summary>Slope as normalized value (0-1) for blending operations</summary>
        Normalized,
        
        /// <summary>Raw rise/run value before atan conversion</summary>
        RiseRun
    }
    
    /// <summary>
    /// Method for handling edge cells where full 3x3 neighborhood isn't available
    /// </summary>
    public enum EdgeHandling
    {
        /// <summary>Set edge cells to NoData (NaN)</summary>
        NoData,
        
        /// <summary>Mirror edge values to create virtual neighbors</summary>
        Mirror,
        
        /// <summary>Extend edge values (nearest neighbor)</summary>
        Extend,
        
        /// <summary>Use available neighbors with adjusted weights</summary>
        Weighted
    }
    
    #endregion
    
    #region Constants
    
    /// <summary>Radians to degrees conversion factor (180/π)</summary>
    private const float RAD_TO_DEG = 57.29577951f;
    
    /// <summary>Degrees to radians conversion factor (π/180)</summary>
    private const float DEG_TO_RAD = 0.01745329f;
    
    #endregion
    
    #region Main Calculation Methods
    
    /// <summary>
    /// Calculate slope for an entire heightmap using ArcGIS planar method.
    /// Uses the third-order finite difference estimator (Horn's algorithm).
    /// </summary>
    /// <param name="heights">2D heightmap array [x,y] with values typically 0-1</param>
    /// <param name="cellSizeX">Cell size in world units (X direction)</param>
    /// <param name="cellSizeY">Cell size in world units (Y direction)</param>
    /// <param name="heightScale">Scale factor for height values (terrain height)</param>
    /// <param name="unit">Output unit for slope values</param>
    /// <param name="edgeHandling">How to handle edge cells</param>
    /// <returns>2D array of slope values in specified units</returns>
    public static float[,] CalculateSlopeMap(
        float[,] heights,
        float cellSizeX,
        float cellSizeY,
        float heightScale = 1f,
        SlopeUnit unit = SlopeUnit.Degrees,
        EdgeHandling edgeHandling = EdgeHandling.Weighted)
    {
        int width = heights.GetLength(0);
        int height = heights.GetLength(1);
        
        float[,] slopeMap = new float[width, height];
        
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                slopeMap[x, y] = CalculateSlopeAtPoint(
                    heights, x, y, cellSizeX, cellSizeY, heightScale, unit, edgeHandling);
            }
        }
        
        return slopeMap;
    }
    
    /// <summary>
    /// Calculate slope at a specific point using the 3x3 neighborhood.
    /// Implements ArcGIS planar slope algorithm.
    /// </summary>
    /// <param name="heights">2D heightmap array</param>
    /// <param name="x">X coordinate of center cell</param>
    /// <param name="y">Y coordinate of center cell</param>
    /// <param name="cellSizeX">Cell size in X direction</param>
    /// <param name="cellSizeY">Cell size in Y direction</param>
    /// <param name="heightScale">Height scale factor</param>
    /// <param name="unit">Output unit</param>
    /// <param name="edgeHandling">Edge handling method</param>
    /// <returns>Slope value in specified units</returns>
    public static float CalculateSlopeAtPoint(
        float[,] heights,
        int x, int y,
        float cellSizeX, float cellSizeY,
        float heightScale = 1f,
        SlopeUnit unit = SlopeUnit.Degrees,
        EdgeHandling edgeHandling = EdgeHandling.Weighted)
    {
        int width = heights.GetLength(0);
        int height = heights.GetLength(1);
        
        // Get 3x3 neighborhood values
        // Layout:
        //   a(x-1,y+1)  b(x,y+1)  c(x+1,y+1)
        //   d(x-1,y)    e(x,y)    f(x+1,y)
        //   g(x-1,y-1)  h(x,y-1)  i(x+1,y-1)
        
        GetNeighborhood(heights, x, y, width, height, edgeHandling, heightScale,
            out float a, out float b, out float c,
            out float d, out float e, out float f,
            out float g, out float h, out float i,
            out float wght1, out float wght2, out float wght3, out float wght4);
        
        // Check if we have enough valid neighbors
        if (float.IsNaN(a) && float.IsNaN(d) && float.IsNaN(g) &&
            float.IsNaN(c) && float.IsNaN(f) && float.IsNaN(i))
        {
            return float.NaN;
        }
        
        // Calculate dz/dx (rate of change in X direction)
        // [dz/dx] = ((c + 2f + i)*4/wght1 - (a + 2d + g)*4/wght2) / (8 * cellSizeX)
        float sumRight = GetValidSum(c, 1) + GetValidSum(f, 2) + GetValidSum(i, 1);
        float sumLeft = GetValidSum(a, 1) + GetValidSum(d, 2) + GetValidSum(g, 1);
        
        float dzdx = 0f;
        if (wght1 > 0 && wght2 > 0)
        {
            dzdx = ((sumRight * 4f / wght1) - (sumLeft * 4f / wght2)) / (8f * cellSizeX);
        }
        
        // Calculate dz/dy (rate of change in Y direction)
        // [dz/dy] = ((g + 2h + i)*4/wght3 - (a + 2b + c)*4/wght4) / (8 * cellSizeY)
        float sumBottom = GetValidSum(g, 1) + GetValidSum(h, 2) + GetValidSum(i, 1);
        float sumTop = GetValidSum(a, 1) + GetValidSum(b, 2) + GetValidSum(c, 1);
        
        float dzdy = 0f;
        if (wght3 > 0 && wght4 > 0)
        {
            dzdy = ((sumBottom * 4f / wght3) - (sumTop * 4f / wght4)) / (8f * cellSizeY);
        }
        
        // Calculate rise/run
        float riseRun = Mathf.Sqrt(dzdx * dzdx + dzdy * dzdy);
        
        // Convert to requested unit
        return ConvertSlopeUnit(riseRun, unit);
    }
    
    /// <summary>
    /// Calculate slope using Unity terrain data directly.
    /// Convenience method that extracts heights and calculates cell sizes.
    /// </summary>
    /// <param name="terrainData">Unity TerrainData</param>
    /// <param name="unit">Output unit for slope values</param>
    /// <returns>2D array of slope values matching heightmap resolution</returns>
    public static float[,] CalculateSlopeFromTerrain(
        TerrainData terrainData,
        SlopeUnit unit = SlopeUnit.Degrees)
    {
        int resolution = terrainData.heightmapResolution;
        float[,] heights = terrainData.GetHeights(0, 0, resolution, resolution);
        
        // Calculate cell size in world units
        float cellSizeX = terrainData.size.x / (resolution - 1);
        float cellSizeY = terrainData.size.z / (resolution - 1);
        float heightScale = terrainData.size.y;
        
        return CalculateSlopeMap(heights, cellSizeX, cellSizeY, heightScale, unit);
    }
    
    /// <summary>
    /// Calculate slope at a normalized terrain position (0-1 range).
    /// Useful for per-pixel terrain decoration.
    /// </summary>
    /// <param name="heights">Cached height array</param>
    /// <param name="normX">Normalized X position (0-1)</param>
    /// <param name="normY">Normalized Y position (0-1)</param>
    /// <param name="cellSizeX">Cell size in X direction</param>
    /// <param name="cellSizeY">Cell size in Y direction</param>
    /// <param name="heightScale">Height scale factor</param>
    /// <param name="unit">Output unit</param>
    /// <returns>Slope value at the interpolated position</returns>
    public static float CalculateSlopeAtNormalizedPosition(
        float[,] heights,
        float normX, float normY,
        float cellSizeX, float cellSizeY,
        float heightScale = 1f,
        SlopeUnit unit = SlopeUnit.Degrees)
    {
        int width = heights.GetLength(0);
        int height = heights.GetLength(1);
        
        int x = Mathf.Clamp(Mathf.FloorToInt(normX * (width - 1)), 0, width - 1);
        int y = Mathf.Clamp(Mathf.FloorToInt(normY * (height - 1)), 0, height - 1);
        
        return CalculateSlopeAtPoint(heights, x, y, cellSizeX, cellSizeY, heightScale, unit);
    }
    
    #endregion
    
    #region Aspect Calculation
    
    /// <summary>
    /// Calculate aspect (slope direction) for an entire heightmap.
    /// Aspect is the compass direction that a slope faces.
    /// </summary>
    /// <param name="heights">2D heightmap array</param>
    /// <param name="cellSizeX">Cell size in X direction</param>
    /// <param name="cellSizeY">Cell size in Y direction</param>
    /// <param name="heightScale">Height scale factor</param>
    /// <returns>2D array of aspect values in degrees (0-360, with 0=North, 90=East, etc.)</returns>
    public static float[,] CalculateAspectMap(
        float[,] heights,
        float cellSizeX, float cellSizeY,
        float heightScale = 1f)
    {
        int width = heights.GetLength(0);
        int height = heights.GetLength(1);
        
        float[,] aspectMap = new float[width, height];
        
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                aspectMap[x, y] = CalculateAspectAtPoint(
                    heights, x, y, cellSizeX, cellSizeY, heightScale);
            }
        }
        
        return aspectMap;
    }
    
    /// <summary>
    /// Calculate aspect at a specific point.
    /// </summary>
    /// <param name="heights">2D heightmap array</param>
    /// <param name="x">X coordinate</param>
    /// <param name="y">Y coordinate</param>
    /// <param name="cellSizeX">Cell size in X direction</param>
    /// <param name="cellSizeY">Cell size in Y direction</param>
    /// <param name="heightScale">Height scale factor</param>
    /// <returns>Aspect in degrees (0-360), or -1 for flat areas</returns>
    public static float CalculateAspectAtPoint(
        float[,] heights,
        int x, int y,
        float cellSizeX, float cellSizeY,
        float heightScale = 1f)
    {
        int width = heights.GetLength(0);
        int height = heights.GetLength(1);
        
        GetNeighborhood(heights, x, y, width, height, EdgeHandling.Extend, heightScale,
            out float a, out float b, out float c,
            out float d, out float e, out float f,
            out float g, out float h, out float i,
            out float wght1, out float wght2, out float wght3, out float wght4);
        
        // Calculate gradients
        float sumRight = GetValidSum(c, 1) + GetValidSum(f, 2) + GetValidSum(i, 1);
        float sumLeft = GetValidSum(a, 1) + GetValidSum(d, 2) + GetValidSum(g, 1);
        float dzdx = (wght1 > 0 && wght2 > 0) 
            ? ((sumRight * 4f / wght1) - (sumLeft * 4f / wght2)) / (8f * cellSizeX)
            : 0f;
        
        float sumBottom = GetValidSum(g, 1) + GetValidSum(h, 2) + GetValidSum(i, 1);
        float sumTop = GetValidSum(a, 1) + GetValidSum(b, 2) + GetValidSum(c, 1);
        float dzdy = (wght3 > 0 && wght4 > 0)
            ? ((sumBottom * 4f / wght3) - (sumTop * 4f / wght4)) / (8f * cellSizeY)
            : 0f;
        
        // Calculate aspect
        float riseRun = Mathf.Sqrt(dzdx * dzdx + dzdy * dzdy);
        
        if (riseRun < 0.0001f)
        {
            return -1f; // Flat area
        }
        
        float aspect = Mathf.Atan2(dzdy, -dzdx) * RAD_TO_DEG;
        
        // Convert to compass direction (0 = North)
        if (aspect < 0)
            aspect = 90f - aspect;
        else if (aspect > 90f)
            aspect = 360f - aspect + 90f;
        else
            aspect = 90f - aspect;
        
        return aspect;
    }
    
    #endregion
    
    #region Burst-Compatible Job
    
    /// <summary>
    /// Burst-compiled parallel job for calculating slope across entire heightmap.
    /// Much faster than sequential calculation for large terrains.
    /// </summary>
    [BurstCompile(FloatPrecision.Standard, FloatMode.Fast)]
    public struct CalculateSlopeJob : IJobParallelFor
    {
        [ReadOnly] public int width;
        [ReadOnly] public int height;
        [ReadOnly] public float cellSizeX;
        [ReadOnly] public float cellSizeY;
        [ReadOnly] public float heightScale;
        [ReadOnly] public SlopeUnit unit;
        
        [ReadOnly] public NativeArray<float> heights;
        [WriteOnly] public NativeArray<float> slopes;
        
        public void Execute(int index)
        {
            int x = index % width;
            int y = index / width;
            
            // Get neighborhood values with edge extension
            float a = GetHeight(x - 1, y + 1);
            float b = GetHeight(x, y + 1);
            float c = GetHeight(x + 1, y + 1);
            float d = GetHeight(x - 1, y);
            float f = GetHeight(x + 1, y);
            float g = GetHeight(x - 1, y - 1);
            float h = GetHeight(x, y - 1);
            float i = GetHeight(x + 1, y - 1);
            
            // Apply height scale
            a *= heightScale; b *= heightScale; c *= heightScale;
            d *= heightScale; f *= heightScale;
            g *= heightScale; h *= heightScale; i *= heightScale;
            
            // Calculate dz/dx
            float dzdx = ((c + 2f * f + i) - (a + 2f * d + g)) / (8f * cellSizeX);
            
            // Calculate dz/dy
            float dzdy = ((g + 2f * h + i) - (a + 2f * b + c)) / (8f * cellSizeY);
            
            // Calculate rise/run
            float riseRun = math.sqrt(dzdx * dzdx + dzdy * dzdy);
            
            // Convert to output unit
            float slope;
            switch (unit)
            {
                case SlopeUnit.Degrees:
                    slope = math.atan(riseRun) * 57.29577951f;
                    break;
                case SlopeUnit.PercentRise:
                    slope = riseRun * 100f;
                    break;
                case SlopeUnit.Normalized:
                    slope = math.clamp(math.atan(riseRun) * 57.29577951f / 90f, 0f, 1f);
                    break;
                default:
                    slope = riseRun;
                    break;
            }
            
            slopes[index] = slope;
        }
        
        private float GetHeight(int x, int y)
        {
            // Clamp to valid range (edge extension)
            x = math.clamp(x, 0, width - 1);
            y = math.clamp(y, 0, height - 1);
            return heights[y * width + x];
        }
    }
    
    /// <summary>
    /// Calculate slope map using parallel Burst-compiled job.
    /// Significantly faster for large heightmaps.
    /// </summary>
    /// <param name="heights">2D heightmap array</param>
    /// <param name="cellSizeX">Cell size in X direction</param>
    /// <param name="cellSizeY">Cell size in Y direction</param>
    /// <param name="heightScale">Height scale factor</param>
    /// <param name="unit">Output unit</param>
    /// <returns>2D slope map</returns>
    public static float[,] CalculateSlopeMapParallel(
        float[,] heights,
        float cellSizeX, float cellSizeY,
        float heightScale = 1f,
        SlopeUnit unit = SlopeUnit.Degrees)
    {
        int width = heights.GetLength(0);
        int height = heights.GetLength(1);
        int totalPixels = width * height;
        
        // Flatten input
        var heightsNative = new NativeArray<float>(totalPixels, Allocator.TempJob);
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                heightsNative[y * width + x] = heights[x, y];
            }
        }
        
        var slopesNative = new NativeArray<float>(totalPixels, Allocator.TempJob);
        
        var job = new CalculateSlopeJob
        {
            width = width,
            height = height,
            cellSizeX = cellSizeX,
            cellSizeY = cellSizeY,
            heightScale = heightScale,
            unit = unit,
            heights = heightsNative,
            slopes = slopesNative
        };
        
        job.Schedule(totalPixels, 64).Complete();
        
        // Convert back to 2D
        float[,] result = new float[width, height];
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                result[x, y] = slopesNative[y * width + x];
            }
        }
        
        heightsNative.Dispose();
        slopesNative.Dispose();
        
        return result;
    }
    
    #endregion
    
    #region Helper Methods
    
    /// <summary>
    /// Get the 3x3 neighborhood around a cell with proper edge handling.
    /// </summary>
    private static void GetNeighborhood(
        float[,] heights, int x, int y, int width, int height,
        EdgeHandling edgeHandling, float heightScale,
        out float a, out float b, out float c,
        out float d, out float e, out float f,
        out float g, out float h, out float i,
        out float wght1, out float wght2, out float wght3, out float wght4)
    {
        // Initialize weights (for weighted averaging with missing neighbors)
        wght1 = 0; wght2 = 0; wght3 = 0; wght4 = 0;
        
        // Center cell
        e = heights[x, y] * heightScale;
        
        // Get neighbors based on edge handling
        switch (edgeHandling)
        {
            case EdgeHandling.NoData:
                a = GetHeightOrNaN(heights, x - 1, y + 1, width, height, heightScale);
                b = GetHeightOrNaN(heights, x, y + 1, width, height, heightScale);
                c = GetHeightOrNaN(heights, x + 1, y + 1, width, height, heightScale);
                d = GetHeightOrNaN(heights, x - 1, y, width, height, heightScale);
                f = GetHeightOrNaN(heights, x + 1, y, width, height, heightScale);
                g = GetHeightOrNaN(heights, x - 1, y - 1, width, height, heightScale);
                h = GetHeightOrNaN(heights, x, y - 1, width, height, heightScale);
                i = GetHeightOrNaN(heights, x + 1, y - 1, width, height, heightScale);
                break;
                
            case EdgeHandling.Mirror:
                a = GetHeightMirrored(heights, x - 1, y + 1, width, height, heightScale);
                b = GetHeightMirrored(heights, x, y + 1, width, height, heightScale);
                c = GetHeightMirrored(heights, x + 1, y + 1, width, height, heightScale);
                d = GetHeightMirrored(heights, x - 1, y, width, height, heightScale);
                f = GetHeightMirrored(heights, x + 1, y, width, height, heightScale);
                g = GetHeightMirrored(heights, x - 1, y - 1, width, height, heightScale);
                h = GetHeightMirrored(heights, x, y - 1, width, height, heightScale);
                i = GetHeightMirrored(heights, x + 1, y - 1, width, height, heightScale);
                break;
                
            case EdgeHandling.Extend:
            default:
                a = GetHeightClamped(heights, x - 1, y + 1, width, height, heightScale);
                b = GetHeightClamped(heights, x, y + 1, width, height, heightScale);
                c = GetHeightClamped(heights, x + 1, y + 1, width, height, heightScale);
                d = GetHeightClamped(heights, x - 1, y, width, height, heightScale);
                f = GetHeightClamped(heights, x + 1, y, width, height, heightScale);
                g = GetHeightClamped(heights, x - 1, y - 1, width, height, heightScale);
                h = GetHeightClamped(heights, x, y - 1, width, height, heightScale);
                i = GetHeightClamped(heights, x + 1, y - 1, width, height, heightScale);
                break;
        }
        
        // Calculate weights for averaging (used in ArcGIS algorithm)
        // wght1: c, f, i (right side)
        // wght2: a, d, g (left side)
        // wght3: g, h, i (bottom)
        // wght4: a, b, c (top)
        
        if (!float.IsNaN(c)) wght1 += 1;
        if (!float.IsNaN(f)) wght1 += 2;
        if (!float.IsNaN(i)) wght1 += 1;
        
        if (!float.IsNaN(a)) wght2 += 1;
        if (!float.IsNaN(d)) wght2 += 2;
        if (!float.IsNaN(g)) wght2 += 1;
        
        if (!float.IsNaN(g)) wght3 += 1;
        if (!float.IsNaN(h)) wght3 += 2;
        if (!float.IsNaN(i)) wght3 += 1;
        
        if (!float.IsNaN(a)) wght4 += 1;
        if (!float.IsNaN(b)) wght4 += 2;
        if (!float.IsNaN(c)) wght4 += 1;
    }
    
    private static float GetHeightOrNaN(float[,] heights, int x, int y, int width, int height, float scale)
    {
        if (x < 0 || x >= width || y < 0 || y >= height)
            return float.NaN;
        return heights[x, y] * scale;
    }
    
    private static float GetHeightClamped(float[,] heights, int x, int y, int width, int height, float scale)
    {
        x = Mathf.Clamp(x, 0, width - 1);
        y = Mathf.Clamp(y, 0, height - 1);
        return heights[x, y] * scale;
    }
    
    private static float GetHeightMirrored(float[,] heights, int x, int y, int width, int height, float scale)
    {
        if (x < 0) x = -x;
        if (y < 0) y = -y;
        if (x >= width) x = 2 * (width - 1) - x;
        if (y >= height) y = 2 * (height - 1) - y;
        
        x = Mathf.Clamp(x, 0, width - 1);
        y = Mathf.Clamp(y, 0, height - 1);
        
        return heights[x, y] * scale;
    }
    
    private static float GetValidSum(float value, float weight)
    {
        if (float.IsNaN(value))
            return 0f;
        return value * weight;
    }
    
    private static float ConvertSlopeUnit(float riseRun, SlopeUnit unit)
    {
        switch (unit)
        {
            case SlopeUnit.Degrees:
                return Mathf.Atan(riseRun) * RAD_TO_DEG;
                
            case SlopeUnit.PercentRise:
                return riseRun * 100f;
                
            case SlopeUnit.Normalized:
                // Normalize to 0-1 range where 90 degrees = 1
                return Mathf.Clamp01(Mathf.Atan(riseRun) * RAD_TO_DEG / 90f);
                
            case SlopeUnit.RiseRun:
            default:
                return riseRun;
        }
    }
    
    #endregion
    
    #region Utility Methods
    
    /// <summary>
    /// Convert slope from degrees to percent rise.
    /// </summary>
    public static float DegreesToPercent(float degrees)
    {
        return Mathf.Tan(degrees * DEG_TO_RAD) * 100f;
    }
    
    /// <summary>
    /// Convert slope from percent rise to degrees.
    /// </summary>
    public static float PercentToDegrees(float percent)
    {
        return Mathf.Atan(percent / 100f) * RAD_TO_DEG;
    }
    
    /// <summary>
    /// Normalize slope degrees to 0-1 range.
    /// </summary>
    public static float NormalizeDegrees(float degrees)
    {
        return Mathf.Clamp01(degrees / 90f);
    }
    
    /// <summary>
    /// Create a debug visualization texture from a slope map.
    /// </summary>
    public static Texture2D CreateSlopeVisualization(float[,] slopeMap, SlopeUnit sourceUnit)
    {
        int width = slopeMap.GetLength(0);
        int height = slopeMap.GetLength(1);
        
        Texture2D texture = new Texture2D(width, height, TextureFormat.RGB24, false);
        Color[] pixels = new Color[width * height];
        
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                float slope = slopeMap[x, y];
                
                // Normalize to 0-1
                float normalized;
                switch (sourceUnit)
                {
                    case SlopeUnit.Degrees:
                        normalized = slope / 90f;
                        break;
                    case SlopeUnit.PercentRise:
                        normalized = Mathf.Atan(slope / 100f) * RAD_TO_DEG / 90f;
                        break;
                    case SlopeUnit.Normalized:
                        normalized = slope;
                        break;
                    default:
                        normalized = Mathf.Atan(slope) * RAD_TO_DEG / 90f;
                        break;
                }
                
                normalized = Mathf.Clamp01(normalized);
                
                // Color gradient: green (flat) -> yellow -> red (steep)
                Color color;
                if (normalized < 0.5f)
                {
                    color = Color.Lerp(Color.green, Color.yellow, normalized * 2f);
                }
                else
                {
                    color = Color.Lerp(Color.yellow, Color.red, (normalized - 0.5f) * 2f);
                }
                
                pixels[y * width + x] = color;
            }
        }
        
        texture.SetPixels(pixels);
        texture.Apply();
        
        return texture;
    }
    
    /// <summary>
    /// Calculate slope statistics for a heightmap.
    /// </summary>
    public static SlopeStatistics CalculateStatistics(float[,] slopeMap)
    {
        int width = slopeMap.GetLength(0);
        int height = slopeMap.GetLength(1);
        
        float min = float.MaxValue;
        float max = float.MinValue;
        double sum = 0;
        int count = 0;
        
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                float val = slopeMap[x, y];
                if (float.IsNaN(val)) continue;
                
                min = Mathf.Min(min, val);
                max = Mathf.Max(max, val);
                sum += val;
                count++;
            }
        }
        
        float mean = count > 0 ? (float)(sum / count) : 0f;
        
        // Calculate standard deviation
        double variance = 0;
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                float val = slopeMap[x, y];
                if (float.IsNaN(val)) continue;
                
                variance += (val - mean) * (val - mean);
            }
        }
        
        float stdDev = count > 1 ? Mathf.Sqrt((float)(variance / (count - 1))) : 0f;
        
        return new SlopeStatistics
        {
            Min = min,
            Max = max,
            Mean = mean,
            StandardDeviation = stdDev,
            ValidCellCount = count
        };
    }
    
    #endregion
    
    #region Data Structures
    
    /// <summary>
    /// Statistics about a slope map
    /// </summary>
    public struct SlopeStatistics
    {
        public float Min;
        public float Max;
        public float Mean;
        public float StandardDeviation;
        public int ValidCellCount;
        
        public override string ToString()
        {
            return $"Slope Stats: Min={Min:F2}°, Max={Max:F2}°, Mean={Mean:F2}°, StdDev={StandardDeviation:F2}°, Cells={ValidCellCount}";
        }
    }
    
    #endregion
}