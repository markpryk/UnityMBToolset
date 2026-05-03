using System;
using UnityEngine;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

/// <summary>
/// Water level and shoreline distance calculation utilities.
/// 
/// Provides height-relative-to-water calculations and shoreline distance fields
/// using Jump Flood Algorithm (JFA) for efficient distance computation.
/// 
/// The algorithm:
/// 1. For each heightmap cell, compute world-space height relative to water level
/// 2. Detect water/land edge transitions (4-neighbor adjacency)
/// 3. Compute distance to nearest edge via JFA passes
/// 4. Output: relative height + shoreline distance per cell
/// 
/// This follows the same architecture as SlopeCalculator and CurvatureCalculator
/// to maintain consistency across the terrain analysis pipeline.
/// </summary>
public static class WaterLevelCalculator
{
    #region Enums
    
    /// <summary>
    /// What the calculator outputs per cell.
    /// </summary>
    public enum OutputMode
    {
        /// <summary>World-space height relative to water level (can be negative).</summary>
        RelativeHeight,
        
        /// <summary>Distance to nearest water edge in world units.</summary>
        ShorelineDistance,
        
        /// <summary>Both relative height and shoreline distance (use CachedWaterMap).</summary>
        Both
    }
    
    #endregion
    
    #region Data Structures
    
    /// <summary>
    /// Per-cell result from water level analysis.
    /// </summary>
    public struct WaterResult
    {
        /// <summary>World-space height relative to water level. Negative = submerged.</summary>
        public float RelativeHeight;
        
        /// <summary>Distance to nearest water/land edge in world units.</summary>
        public float ShorelineDistance;
        
        /// <summary>Whether this cell is at or below water level.</summary>
        public bool IsUnderwater;
    }
    
    /// <summary>
    /// Statistics from a water level analysis pass.
    /// </summary>
    public struct WaterStatistics
    {
        public float MinRelativeHeight;
        public float MaxRelativeHeight;
        public float MaxShorelineDistance;
        public int UnderwaterCellCount;
        public int TotalCellCount;
        public int EdgeCellCount;
        
        public float UnderwaterPercentage => TotalCellCount > 0 
            ? (UnderwaterCellCount / (float)TotalCellCount) * 100f 
            : 0f;
    }
    
    #endregion
    
    #region Core Calculation
    
    /// <summary>
    /// Calculate relative height at a specific heightmap position.
    /// </summary>
    /// <param name="heights">Normalized heightmap (0-1)</param>
    /// <param name="x">X coordinate</param>
    /// <param name="y">Y coordinate</param>
    /// <param name="terrainY">Terrain transform Y position</param>
    /// <param name="heightScale">Terrain height scale (size.y)</param>
    /// <param name="waterLevel">World-space water level</param>
    /// <returns>Height relative to water level in world units</returns>
    public static float CalculateRelativeHeightAtPoint(
        float[,] heights, int x, int y,
        float terrainY, float heightScale, float waterLevel)
    {
        int rows = heights.GetLength(0); // Unity heightmap: [row, col] = [y, x]
        int cols = heights.GetLength(1);
        x = Mathf.Clamp(x, 0, cols - 1);
        y = Mathf.Clamp(y, 0, rows - 1);
        
        float worldY = terrainY + heights[y, x] * heightScale;
        return worldY - waterLevel;
    }
    
    /// <summary>
    /// Calculate relative height at a normalized terrain position (0-1 range).
    /// </summary>
    public static float CalculateRelativeHeightAtNormalizedPosition(
        float[,] heights, float normX, float normY,
        float terrainY, float heightScale, float waterLevel)
    {
        int rows = heights.GetLength(0); // [row, col] = [y, x]
        int cols = heights.GetLength(1);
        // Decorator convention: normX maps to row, normY maps to col
        int x = Mathf.Clamp(Mathf.FloorToInt(normY * (cols - 1)), 0, cols - 1);
        int y = Mathf.Clamp(Mathf.FloorToInt(normX * (rows - 1)), 0, rows - 1);
        
        return CalculateRelativeHeightAtPoint(heights, x, y, terrainY, heightScale, waterLevel);
    }
    
    #endregion
    
    #region Shoreline Edge Detection
    
    /// <summary>
    /// Detect water/land edge cells using 4-neighbor adjacency.
    /// An edge cell has at least one neighbor on the opposite side of the water level.
    /// </summary>
    /// <param name="heights">Normalized heightmap (0-1)</param>
    /// <param name="terrainY">Terrain Y position</param>
    /// <param name="heightScale">Terrain height scale</param>
    /// <param name="waterLevel">World-space water level</param>
    /// <param name="edgeCellCount">Number of edge cells found</param>
    /// <returns>Boolean array [height, width] where true = edge cell</returns>
    public static bool[,] DetectEdgeCells(
        float[,] heights, float terrainY, float heightScale, float waterLevel,
        out int edgeCellCount)
    {
        int h = heights.GetLength(0);
        int w = heights.GetLength(1);
        bool[,] edges = new bool[h, w];
        edgeCellCount = 0;
        
        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                float wH = terrainY + heights[y, x] * heightScale;
                bool isWater = wH <= waterLevel;
                bool isEdge = false;
                
                if (x > 0)
                {
                    bool nWater = (terrainY + heights[y, x - 1] * heightScale) <= waterLevel;
                    if (isWater != nWater) isEdge = true;
                }
                if (!isEdge && x < w - 1)
                {
                    bool nWater = (terrainY + heights[y, x + 1] * heightScale) <= waterLevel;
                    if (isWater != nWater) isEdge = true;
                }
                if (!isEdge && y > 0)
                {
                    bool nWater = (terrainY + heights[y - 1, x] * heightScale) <= waterLevel;
                    if (isWater != nWater) isEdge = true;
                }
                if (!isEdge && y < h - 1)
                {
                    bool nWater = (terrainY + heights[y + 1, x] * heightScale) <= waterLevel;
                    if (isWater != nWater) isEdge = true;
                }
                
                edges[y, x] = isEdge;
                if (isEdge) edgeCellCount++;
            }
        }
        
        return edges;
    }
    
    #endregion
    
    #region JFA Distance Field
    
    /// <summary>
    /// Calculate shoreline distance field using Jump Flood Algorithm.
    /// </summary>
    /// <param name="heights">Normalized heightmap (0-1)</param>
    /// <param name="terrainY">Terrain Y position</param>
    /// <param name="heightScale">Terrain height scale</param>
    /// <param name="waterLevel">World-space water level</param>
    /// <param name="cellSizeX">World-space cell size X</param>
    /// <param name="cellSizeZ">World-space cell size Z</param>
    /// <param name="maxDistance">Clamp distance to this value</param>
    /// <param name="stats">Output statistics</param>
    /// <returns>Distance map [height, width] in world units</returns>
    public static float[,] CalculateShorelineDistanceMap(
        float[,] heights,
        float terrainY, float heightScale, float waterLevel,
        float cellSizeX, float cellSizeZ, float maxDistance,
        out WaterStatistics stats)
    {
        int h = heights.GetLength(0);
        int w = heights.GetLength(1);
        
        stats = new WaterStatistics
        {
            TotalCellCount = w * h,
            MaxShorelineDistance = 0f,
            MinRelativeHeight = float.MaxValue,
            MaxRelativeHeight = float.MinValue
        };
        
        // Detect edges
        bool[,] edges = DetectEdgeCells(heights, terrainY, heightScale, waterLevel, out int edgeCount);
        stats.EdgeCellCount = edgeCount;
        
        // Initialize JFA seed map
        const int FAR = -9999;
        Vector2Int[] nearest = new Vector2Int[w * h];
        
        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                int idx = y * w + x;
                
                if (edges[y, x])
                    nearest[idx] = new Vector2Int(x, y);
                else
                    nearest[idx] = new Vector2Int(FAR, FAR);
                
                // Gather stats
                float relH = terrainY + heights[y, x] * heightScale - waterLevel;
                if (relH < stats.MinRelativeHeight) stats.MinRelativeHeight = relH;
                if (relH > stats.MaxRelativeHeight) stats.MaxRelativeHeight = relH;
                if (relH <= 0) stats.UnderwaterCellCount++;
            }
        }
        
        // JFA passes - start from next power-of-two
        int maxDim = Mathf.Max(w, h);
        int step = Mathf.NextPowerOfTwo(maxDim) / 2;
        
        for (; step >= 1; step /= 2)
        {
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    int idx = y * w + x;
                    Vector2Int best = nearest[idx];
                    
                    for (int dy = -1; dy <= 1; dy++)
                    {
                        for (int dx = -1; dx <= 1; dx++)
                        {
                            int nx = x + dx * step;
                            int ny = y + dy * step;
                            if (nx < 0 || nx >= w || ny < 0 || ny >= h) continue;
                            
                            Vector2Int candidate = nearest[ny * w + nx];
                            if (candidate.x == FAR) continue;
                            
                            if (best.x == FAR)
                            {
                                best = candidate;
                            }
                            else
                            {
                                float bDistSq = Sqr((best.x - x) * cellSizeX) + Sqr((best.y - y) * cellSizeZ);
                                float cDistSq = Sqr((candidate.x - x) * cellSizeX) + Sqr((candidate.y - y) * cellSizeZ);
                                
                                if (cDistSq < bDistSq)
                                    best = candidate;
                            }
                        }
                    }
                    
                    nearest[idx] = best;
                }
            }
        }
        
        // Convert to distance map
        float[,] distanceMap = new float[h, w];
        
        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                int idx = y * w + x;
                Vector2Int n = nearest[idx];
                
                float dist;
                if (n.x == FAR)
                {
                    dist = maxDistance;
                }
                else
                {
                    float ddx = (n.x - x) * cellSizeX;
                    float ddy = (n.y - y) * cellSizeZ;
                    dist = Mathf.Min(Mathf.Sqrt(ddx * ddx + ddy * ddy), maxDistance);
                }
                
                distanceMap[y, x] = dist;
                if (dist > stats.MaxShorelineDistance)
                    stats.MaxShorelineDistance = dist;
            }
        }
        
        return distanceMap;
    }
    
    /// <summary>
    /// Calculate both relative height and shoreline distance maps.
    /// </summary>
    public static void CalculateWaterMaps(
        float[,] heights,
        float terrainY, float heightScale, float waterLevel,
        float cellSizeX, float cellSizeZ, float maxDistance,
        out float[,] relativeHeightMap,
        out float[,] shorelineDistanceMap,
        out WaterStatistics stats)
    {
        int h = heights.GetLength(0);
        int w = heights.GetLength(1);
        
        // Relative height map
        relativeHeightMap = new float[h, w];
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
                relativeHeightMap[y, x] = terrainY + heights[y, x] * heightScale - waterLevel;
        
        // Shoreline distance map
        shorelineDistanceMap = CalculateShorelineDistanceMap(
            heights, terrainY, heightScale, waterLevel,
            cellSizeX, cellSizeZ, maxDistance, out stats);
    }
    
    #endregion
    
    #region Parallel JFA Job
    
    /// <summary>
    /// Burst-compiled job for JFA pass. Each pass processes all cells for one step size.
    /// Note: JFA is inherently sequential per pass (reads neighbors from previous iteration),
    /// so we parallelize within each pass across rows.
    /// </summary>
    [BurstCompile(FloatPrecision.Standard, FloatMode.Fast)]
    public struct JFAPassJob : IJobParallelFor
    {
        [ReadOnly] public int width;
        [ReadOnly] public int height;
        [ReadOnly] public int step;
        [ReadOnly] public float cellSizeX;
        [ReadOnly] public float cellSizeZ;
        
        [ReadOnly] public NativeArray<int2> readNearest;
        [NativeDisableParallelForRestriction]
        public NativeArray<int2> writeNearest;
        
        public void Execute(int y)
        {
            int2 FAR = new int2(-9999, -9999);
            
            for (int x = 0; x < width; x++)
            {
                int idx = y * width + x;
                int2 best = readNearest[idx];
                
                for (int dy = -1; dy <= 1; dy++)
                {
                    for (int dx = -1; dx <= 1; dx++)
                    {
                        int nx = x + dx * step;
                        int ny = y + dy * step;
                        if (nx < 0 || nx >= width || ny < 0 || ny >= height) continue;
                        
                        int2 candidate = readNearest[ny * width + nx];
                        if (candidate.x == -9999) continue;
                        
                        if (best.x == -9999)
                        {
                            best = candidate;
                        }
                        else
                        {
                            float bx = (best.x - x) * cellSizeX;
                            float by = (best.y - y) * cellSizeZ;
                            float cx = (candidate.x - x) * cellSizeX;
                            float cy = (candidate.y - y) * cellSizeZ;
                            
                            if ((cx * cx + cy * cy) < (bx * bx + by * by))
                                best = candidate;
                        }
                    }
                }
                
                writeNearest[idx] = best;
            }
        }
    }
    
    /// <summary>
    /// Calculate shoreline distance field using parallel JFA.
    /// </summary>
    public static float[,] CalculateShorelineDistanceMapParallel(
        float[,] heights,
        float terrainY, float heightScale, float waterLevel,
        float cellSizeX, float cellSizeZ, float maxDistance,
        out WaterStatistics stats)
    {
        int h = heights.GetLength(0);
        int w = heights.GetLength(1);
        int totalCells = w * h;
        
        stats = new WaterStatistics
        {
            TotalCellCount = totalCells,
            MaxShorelineDistance = 0f,
            MinRelativeHeight = float.MaxValue,
            MaxRelativeHeight = float.MinValue
        };
        
        // Detect edges and init seed map
        bool[,] edges = DetectEdgeCells(heights, terrainY, heightScale, waterLevel, out int edgeCount);
        stats.EdgeCellCount = edgeCount;
        
        var nearestA = new NativeArray<int2>(totalCells, Allocator.TempJob);
        var nearestB = new NativeArray<int2>(totalCells, Allocator.TempJob);
        
        int2 FAR = new int2(-9999, -9999);
        
        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                int idx = y * w + x;
                nearestA[idx] = edges[y, x] ? new int2(x, y) : FAR;
                
                float relH = terrainY + heights[y, x] * heightScale - waterLevel;
                if (relH < stats.MinRelativeHeight) stats.MinRelativeHeight = relH;
                if (relH > stats.MaxRelativeHeight) stats.MaxRelativeHeight = relH;
                if (relH <= 0) stats.UnderwaterCellCount++;
            }
        }
        
        // JFA passes with ping-pong buffers
        int maxDim = Mathf.Max(w, h);
        int step = Mathf.NextPowerOfTwo(maxDim) / 2;
        bool readFromA = true;
        
        while (step >= 1)
        {
            var job = new JFAPassJob
            {
                width = w,
                height = h,
                step = step,
                cellSizeX = cellSizeX,
                cellSizeZ = cellSizeZ,
                readNearest = readFromA ? nearestA : nearestB,
                writeNearest = readFromA ? nearestB : nearestA
            };
            
            job.Schedule(h, 8).Complete();
            
            readFromA = !readFromA;
            step /= 2;
        }
        
        // Read from the last written buffer
        var finalNearest = readFromA ? nearestA : nearestB;
        
        float[,] distanceMap = new float[h, w];
        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                int idx = y * w + x;
                int2 n = finalNearest[idx];
                
                float dist;
                if (n.x == -9999)
                {
                    dist = maxDistance;
                }
                else
                {
                    float ddx = (n.x - x) * cellSizeX;
                    float ddy = (n.y - y) * cellSizeZ;
                    dist = Mathf.Min(Mathf.Sqrt(ddx * ddx + ddy * ddy), maxDistance);
                }
                
                distanceMap[y, x] = dist;
                if (dist > stats.MaxShorelineDistance)
                    stats.MaxShorelineDistance = dist;
            }
        }
        
        nearestA.Dispose();
        nearestB.Dispose();
        
        return distanceMap;
    }
    
    #endregion
    
    #region Utility
    
    private static float Sqr(float v) => v * v;
    
    #endregion
}