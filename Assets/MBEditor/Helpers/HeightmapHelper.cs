using System;
using UnityEngine;

public static class HeightmapHelper
{
    public enum Rotation
    {
        None,
        R90,
        R180,
        R270
    }

    public enum Flip
    {
        None,
        Horizontal,
        Vertical
    }
     public static float[,] RotateHeightMap(float[,] heightMap, Rotation rotation)
    {
        switch (rotation)
        {
            case Rotation.R90:
                return RotateHeightMap90Degrees(heightMap);
            case Rotation.R180:
                return RotateHeightMap90Degrees(RotateHeightMap90Degrees(heightMap));
            case Rotation.R270:
                return RotateHeightMap90Degrees(RotateHeightMap90Degrees(RotateHeightMap90Degrees(heightMap)));
            default:
                return heightMap;
        }
    }

    public static float[,] FlipHeightMap(float[,] heightMap, Flip flip)
    {
        switch (flip)
        {
            case Flip.Horizontal:
                return FlipHeightMapHorizontally(heightMap);
            case Flip.Vertical:
                return FlipHeightMapVertically(heightMap);
            default:
                return heightMap;
        }
    }

    public static float[,] RotateHeightMap90Degrees(float[,] heightMap)
    {
        int width = heightMap.GetLength(0);
        int height = heightMap.GetLength(1);
        float[,] rotatedHeightMap = new float[height, width];

        for (int i = 0; i < width; i++)
        {
            for (int j = 0; j < height; j++)
            {
                rotatedHeightMap[j, width - 1 - i] = heightMap[i, j];
            }
        }

        return rotatedHeightMap;
    }

    public static float[,] ClampSquared(float[,] heightmap)
    {
        int originalRows = heightmap.GetLength(0);
        int originalCols = heightmap.GetLength(1);

        // Determine the smaller dimension to clamp to
        int newSize = Mathf.Min(originalRows, originalCols);

        // Calculate how much to trim from each side
        int rowStart = (originalRows - newSize) / 2;
        int colStart = (originalCols - newSize) / 2;

        // Create a new square matrix
        float[,] clampedHeightmap = new float[newSize, newSize];

        // Copy data from the original heightmap to the new square matrix
        for (int i = 0; i < newSize; i++)
        {
            for (int j = 0; j < newSize; j++)
            {
                clampedHeightmap[i, j] = heightmap[rowStart + i, colStart + j];
            }
        }

        return clampedHeightmap;
    }

    public static float[,] CleanEmptyData(float[,] heightmap, float threshold = 0.01f, int gap = 0)
    {
        int width = heightmap.GetLength(0);
        int height = heightmap.GetLength(1);

        // Find bounds of non-empty area
        int minX = width, maxX = 0, minY = height, maxY = 0;

        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                if (heightmap[x, y] > threshold)
                {
                    minX = Math.Min(minX, x);
                    maxX = Math.Max(maxX, x);
                    minY = Math.Min(minY, y);
                    maxY = Math.Max(maxY, y);
                }
            }
        }

        // If no valid area found, return an empty heightmap
        if (minX > maxX || minY > maxY)
        {
            return new float[width, height];
        }

        gap = 0;
        // Apply gap (ensure it doesn't exceed boundaries)
        minX = Math.Max(0, minX - gap);
        maxX = Math.Min(width - 1, maxX + gap);
        minY = Math.Max(0, minY - gap);
        maxY = Math.Min(height - 1, maxY + gap);

        // Determine the side length of the square
        int squareSize = Math.Max(maxX - minX + 1, maxY - minY + 1);

        // Create new square heightmap
        float[,] squareHeightmap = new float[squareSize, squareSize];

        for (int x = 0; x < squareSize; x++)
        {
            for (int y = 0; y < squareSize; y++)
            {
                int srcX = minX + x;
                int srcY = minY + y;

                if (srcX < width && srcY < height)
                {
                    squareHeightmap[x, y] = heightmap[srcX, srcY];
                }
                else
                {
                    squareHeightmap[x, y] = 0f; // Fill with empty values if out of bounds
                }
            }
        }

        return squareHeightmap;
    }

    public static int NextPowerOfTwo(int value)
    {
        if (value <= 0)
            throw new ArgumentException("Value must be greater than 0.");

        value--;
        value |= value >> 1;
        value |= value >> 2;
        value |= value >> 4;
        value |= value >> 8;
        value |= value >> 16;
        return value + 1;
    }


    // Multiply two heightmaps
    public static float[,] MultiplyHeightmaps(float[,] heightmap1, float[,] heightmap2)
    {
        int width = heightmap1.GetLength(0);
        int height = heightmap1.GetLength(1);
        float[,] result = new float[width, height];

        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                result[x, y] = heightmap1[x, y] * heightmap2[x, y];
            }
        }

        return result;
    }

    // Add two heightmaps
    public static float[,] AddHeightmaps(float[,] heightmap1, float[,] heightmap2)
    {
        int width = heightmap1.GetLength(0);
        int height = heightmap1.GetLength(1);
        float[,] result = new float[width, height];

        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                result[x, y] = heightmap1[x, y] + heightmap2[x, y] > 1f ? 1f : heightmap1[x, y] + heightmap2[x, y];
            }
        }

        return result;
    }

    // Subtract two heightmaps
    public static float[,] SubtractHeightmaps(float[,] heightmap1, float[,] heightmap2)
    {
        int width = heightmap1.GetLength(0);
        int height = heightmap1.GetLength(1);
        float[,] result = new float[width, height];

        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                result[x, y] = heightmap1[x, y] - heightmap2[x, y];
            }
        }

        return result;
    }

    // Divide two heightmaps
    public static float[,] DivideHeightmaps(float[,] heightmap1, float[,] heightmap2)
    {
        int width = heightmap1.GetLength(0);
        int height = heightmap1.GetLength(1);
        float[,] result = new float[width, height];

        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                result[x, y] =
                    heightmap2[x, y] != 0f ? heightmap1[x, y] / heightmap2[x, y] : 0f; // Prevent division by zero
            }
        }

        return result;
    }

    // Apply power to heightmap
    public static float[,] PowerHeightmap(float[,] heightmap, float power)
    {
        int width = heightmap.GetLength(0);
        int height = heightmap.GetLength(1);
        float[,] result = new float[width, height];

        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                result[x, y] = Mathf.Pow(heightmap[x, y], power);
            }
        }

        return result;
    }

    // Linear Light Blend Mode
    public static float[,] LinearLightBlend(float[,] baseMap, float[,] blendMap)
    {
        int width = baseMap.GetLength(0);
        int height = baseMap.GetLength(1);
        float[,] result = new float[width, height];

        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                result[x, y] = Mathf.Clamp(baseMap[x, y] + 2 * (blendMap[x, y] - 0.5f), 0f, 1f);
            }
        }

        return result;
    }

    // Soft Light Blend Mode
    public static float[,] SoftLightBlend(float[,] baseMap, float[,] blendMap)
    {
        int width = baseMap.GetLength(0);
        int height = baseMap.GetLength(1);
        float[,] result = new float[width, height];

        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                float basePixel = baseMap[x, y];
                float blendPixel = blendMap[x, y];

                result[x, y] = basePixel < 0.5f
                    ? basePixel * (blendPixel + 0.5f)
                    : 1 - (1 - basePixel) * (1 - (blendPixel - 0.5f));
            }
        }

        return result;
    }

    public static float[,] InverseHeightmap(float[,] heightmap)
    {
        if (heightmap == null)
        {
            throw new ArgumentNullException(nameof(heightmap), "Heightmap cannot be null.");
        }

        int width = heightmap.GetLength(0);
        int height = heightmap.GetLength(1);

        // Step 1: Find the min and max values in the heightmap
        float minValue = float.MaxValue;
        float maxValue = float.MinValue;

        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                if (heightmap[x, y] < minValue) minValue = heightmap[x, y];
                if (heightmap[x, y] > maxValue) maxValue = heightmap[x, y];
            }
        }

        // Step 2: Handle edge case where maxValue equals minValue
        if (Math.Abs(maxValue - minValue) < float.Epsilon)
        {
            Debug.LogWarning("Heightmap has constant values. Returning a flat inverted heightmap.");
            float[,] flatInverted = new float[width, height];
            for (int x = 0; x < width; x++)
            {
                for (int y = 0; y < height; y++)
                {
                    flatInverted[x, y] = 0f; // Inverted normalized value for constant heightmaps
                }
            }

            return flatInverted;
        }

        // Step 3: Create the inverted heightmap
        float[,] inverted = new float[width, height];

        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                // Normalize the value to [0, 1], invert it, then map it back to the original range
                float normalized = (heightmap[x, y] - minValue) / (maxValue - minValue);
                float invertedNormalized = 1f - normalized;
                inverted[x, y] = invertedNormalized * (maxValue - minValue) + minValue;
            }
        }

        return inverted;
    }


    // Normalize Heightmap
    public static float[,] NormalizeHeightmap(float[,] heightmap, float min = 0f, float max = 1f)
    {
        int width = heightmap.GetLength(0);
        int height = heightmap.GetLength(1);
        float[,] normalized = new float[width, height];

        float currentMin = float.MaxValue;
        float currentMax = float.MinValue;

        // Find min and max values in the heightmap
        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                float value = heightmap[x, y];
                if (value < currentMin) currentMin = value;
                if (value > currentMax) currentMax = value;
            }
        }

        // Normalize the values
        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                normalized[x, y] = Mathf.Lerp(min, max, Mathf.InverseLerp(currentMin, currentMax, heightmap[x, y]));
            }
        }

        return normalized;
    }

// Lerp between two heightmaps
    public static float[,] LerpHeightmaps(float[,] heightmap1, float[,] heightmap2, float t)
    {
        int width = heightmap1.GetLength(0);
        int height = heightmap1.GetLength(1);

        if (heightmap2.GetLength(0) != width || heightmap2.GetLength(1) != height)
            throw new ArgumentException("Heightmaps must have the same dimensions");

        float[,] result = new float[width, height];

        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                result[x, y] = Mathf.Lerp(heightmap1[x, y], heightmap2[x, y], t);
            }
        }

        return result;
    }

    // Utility to Create a Heightmap Filled with a Specific Value
    public static float[,] CreateHeightmap(int width, int height, float value = 0f)
    {
        float[,] heightmap = new float[width, height];

        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                heightmap[x, y] = value;
            }
        }

        return heightmap;
    }

    // Utility to Print Heightmap (Debugging)
    public static void PrintHeightmap(float[,] heightmap)
    {
        int width = heightmap.GetLength(0);
        int height = heightmap.GetLength(1);

        for (int y = 0; y < height; y++)
        {
            string line = "";
            for (int x = 0; x < width; x++)
            {
                line += $"{heightmap[x, y]:0.00} ";
            }

            Console.WriteLine(line);
        }
    }

    public static float[,] BlendHeightmaps(float[,] heightmap, float[,] baseNoise, float sliderValueA,
        float sliderValueB)
    {
        int width = heightmap.GetLength(0);
        int height = heightmap.GetLength(1);

        if (baseNoise.GetLength(0) != width || baseNoise.GetLength(1) != height)
            throw new ArgumentException("Heightmaps must have the same dimensions");

        float[,] blended = new float[width, height];

        // Normalize the slider values to make their sum equal to 1 (optional, for weighted blending)
        float totalSliderValue = sliderValueA + sliderValueB;
        float normalizedA = totalSliderValue > 0 ? sliderValueA / totalSliderValue : 0.5f;
        float normalizedB = totalSliderValue > 0 ? sliderValueB / totalSliderValue : 0.5f;

        // Blend each pixel
        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                blended[x, y] = (heightmap[x, y] * normalizedA) + (baseNoise[x, y] * normalizedB);
            }
        }

        return blended;
    }

    private static void ValidateDimensions(float[,] map1, float[,] map2)
    {
        if (map1.GetLength(0) != map2.GetLength(0) || map1.GetLength(1) != map2.GetLength(1))
            throw new ArgumentException("Heightmaps must have the same dimensions.");
    }

    public static float[,] RemapHeightmap(float[,] heightmap, float targetMaxHeight)
    {
        int width = heightmap.GetLength(0);
        int height = heightmap.GetLength(1);

        // Find the current max height in the heightmap
        float currentMaxHeight = float.MinValue;

        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                if (heightmap[x, y] > currentMaxHeight)
                {
                    currentMaxHeight = heightmap[x, y];
                }
            }
        }

        // Avoid division by zero if the heightmap is flat
        if (Mathf.Approximately(currentMaxHeight, 0f))
        {
            Debug.LogWarning("Heightmap is flat. No remapping needed.");
            return heightmap;
        }

        // Create a new heightmap with remapped values
        float[,] remappedHeightmap = new float[width, height];

        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                // Scale the values to match the target max height
                remappedHeightmap[x, y] = (heightmap[x, y] / currentMaxHeight) * targetMaxHeight;
            }
        }

        return remappedHeightmap;
    }

    // Utility: Find the max height in a heightmap
    public static float GetMaxHeightInMeters(float[,] heightmap, float maxHeightScale = 1)
    {
        int width = heightmap.GetLength(0);
        int height = heightmap.GetLength(1);
        float maxValue = float.MinValue;

        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                if (heightmap[x, y] > maxValue)
                {
                    maxValue = heightmap[x, y];
                }
            }
        }

        return maxValue * maxHeightScale;
    }

    public static float GetMinHeightInMeters(float[,] heightmap, float heightScale = 1)
    {
        int width = heightmap.GetLength(0);
        int height = heightmap.GetLength(1);
        float minValue = float.MaxValue;

        // Iterate through the heightmap to find the minimum value
        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                if (heightmap[x, y] < minValue)
                {
                    minValue = heightmap[x, y];
                }
            }
        }

        // Convert the minimum value to meters using the height scale
        return minValue * heightScale;
    }

    public static float GetFullRangeHeightInMeters(float[,] heightmap, float heightScale = 1)
    {
        int width = heightmap.GetLength(0);
        int height = heightmap.GetLength(1);
        float minValue = float.MaxValue;
        float maxValue = float.MinValue;

        // Iterate through the heightmap to find the minimum and maximum values
        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                if (heightmap[x, y] < minValue)
                {
                    minValue = heightmap[x, y];
                }

                if (heightmap[x, y] > maxValue)
                {
                    maxValue = heightmap[x, y];
                }
            }
        }

        // Convert the minimum and maximum values to meters using the height scale
        float minMeters = minValue * heightScale;
        float maxMeters = maxValue * heightScale;

        return maxMeters - minMeters;
    }

    public static float[,] FlipHeightMapHorizontally(float[,] heightMap)
    {
        int width = heightMap.GetLength(0);
        int height = heightMap.GetLength(1);
        float[,] flippedHeightMap = new float[width, height];

        for (int i = 0; i < width; i++)
        {
            for (int j = 0; j < height; j++)
            {
                flippedHeightMap[i, j] = heightMap[i, height - 1 - j];
            }
        }

        return flippedHeightMap;
    }

    public static float[,] FlipHeightMapVertically(float[,] heightMap)
    {
        int width = heightMap.GetLength(0);
        int height = heightMap.GetLength(1);
        float[,] flippedHeightMap = new float[width, height];

        for (int i = 0; i < width; i++)
        {
            for (int j = 0; j < height; j++)
            {
                flippedHeightMap[i, j] = heightMap[width - 1 - i, j];
            }
        }

        return flippedHeightMap;
    }

    public static float[,] ResizeMap(float[,] map, int targetWidth, int targetHeight)
    {
        int sourceWidth = map.GetLength(0);
        int sourceHeight = map.GetLength(1);

        float[,] resized = new float[targetWidth, targetHeight];

        for (int x = 0; x < targetWidth; x++)
        {
            for (int y = 0; y < targetHeight; y++)
            {
                // Map target coordinates to source coordinates
                float gx = x / (float)(targetWidth - 1) * (sourceWidth - 1);
                float gy = y / (float)(targetHeight - 1) * (sourceHeight - 1);

                int x0 = Mathf.FloorToInt(gx);
                int x1 = Mathf.Min(x0 + 1, sourceWidth - 1);
                int y0 = Mathf.FloorToInt(gy);
                int y1 = Mathf.Min(y0 + 1, sourceHeight - 1);

                // Bilinear interpolation
                float dx = gx - x0;
                float dy = gy - y0;

                float top = Mathf.Lerp(map[x0, y0], map[x1, y0], dx);
                float bottom = Mathf.Lerp(map[x0, y1], map[x1, y1], dx);

                resized[x, y] = Mathf.Lerp(top, bottom, dy);
            }
        }

        return resized;
    }

    /// <summary>Resize 2D float array using bilinear interpolation</summary>
    public static float[,] ResizeBilinear(float[,] source, int newWidth, int newHeight)
    {
        int srcWidth = source.GetLength(0);
        int srcHeight = source.GetLength(1);
        float[,] result = new float[newWidth, newHeight];

        float xRatio = (float)(srcWidth - 1) / (newWidth - 1);
        float yRatio = (float)(srcHeight - 1) / (newHeight - 1);

        for (int y = 0; y < newHeight; y++)
        {
            for (int x = 0; x < newWidth; x++)
            {
                float srcX = x * xRatio;
                float srcY = y * yRatio;

                int x0 = (int)srcX;
                int y0 = (int)srcY;
                int x1 = Mathf.Min(x0 + 1, srcWidth - 1);
                int y1 = Mathf.Min(y0 + 1, srcHeight - 1);

                float xFrac = srcX - x0;
                float yFrac = srcY - y0;

                float top = Mathf.Lerp(source[x0, y0], source[x1, y0], xFrac);
                float bottom = Mathf.Lerp(source[x0, y1], source[x1, y1], xFrac);
                result[x, y] = Mathf.Lerp(top, bottom, yFrac);
            }
        }
        
        return result;
    }

public static float[,] ClampToGeometryData(float[,] heightmap, MBTerrainGeneratorHelpers.TerrainGeometryData geoData)
    {
        int maxX = geoData.NumVerticesY; // Inverted because of Unity's coordinate system
        int maxY = geoData.NumVerticesX;

        int width = heightmap.GetLength(0);
        int height = heightmap.GetLength(1);

        if (width == maxX && height == maxY)
        {
            return heightmap;
        }

        float[,] clampedHeightmap = new float[maxX, maxY];

        for (int x = 0; x < maxX; x++)
        {
            for (int y = 0; y < maxY; y++)
            {
                int srcX = Mathf.Clamp(x, 0, width - 1);
                int srcY = Mathf.Clamp(y, 0, height - 1);

                clampedHeightmap[x, y] = heightmap[srcX, srcY];
            }
        }

        return clampedHeightmap;
    }
}
