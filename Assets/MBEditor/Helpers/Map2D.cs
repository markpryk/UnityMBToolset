using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;

public class Map2D
{
    // referencing from TerrainInspector.ResizeControltexture()
    public static void ResizeControlTexture(TerrainData terrainData, int resolution)
    {
        RenderTexture oldRT = RenderTexture.active;
        RenderTexture[] oldAlphaMaps = new RenderTexture[terrainData.alphamapTextureCount];
        for (int i = 0; i < oldAlphaMaps.Length; i++)
        {
            terrainData.alphamapTextures[i].filterMode = FilterMode.Bilinear;
            oldAlphaMaps[i] = RenderTexture.GetTemporary(resolution, resolution, 0, SystemInfo.GetGraphicsFormat(DefaultFormat.HDR));
            Graphics.Blit(terrainData.alphamapTextures[i], oldAlphaMaps[i]);
        }

        Undo.RegisterCompleteObjectUndo(terrainData, "Resize alphamap");

        terrainData.alphamapResolution = resolution;
        for (int i = 0; i < oldAlphaMaps.Length; i++)
        {
            RenderTexture.active = oldAlphaMaps[i];

            CopyActiveRenderTextureToTexture(terrainData.GetAlphamapTexture(i), new RectInt(0, 0, resolution, resolution), Vector2Int.zero, false);
        }
        terrainData.SetBaseMapDirty();
        RenderTexture.active = oldRT;
        for (int i = 0; i < oldAlphaMaps.Length; i++)
        {
            RenderTexture.ReleaseTemporary(oldAlphaMaps[i]);
        }

        terrainData.SetBaseMapDirty();
    }

    // referencing from TerrainData.GPUCopy.CopyActiveRenderTextureToTexture()
    public static void CopyActiveRenderTextureToTexture(Texture2D dstTexture, RectInt sourceRect, Vector2Int dest, bool allowDelayedCPUSync)
    {
        var source = RenderTexture.active;
        if (source == null)
            throw new InvalidDataException("Active RenderTexture is null.");

        int dstWidth = dstTexture.width;
        int dstHeight = dstTexture.height;

        allowDelayedCPUSync = allowDelayedCPUSync && SupportsCopyTextureBetweenRTAndTexture;
        if (allowDelayedCPUSync)
        {
            if (dstTexture.mipmapCount > 1)
            {
                var tmp = RenderTexture.GetTemporary(new RenderTextureDescriptor(dstWidth, dstHeight, source.format));
                if (!tmp.IsCreated())
                {
                    tmp.Create();
                }
                Graphics.CopyTexture(dstTexture, 0, 0, tmp, 0, 0);
                Graphics.CopyTexture(source, 0, 0, sourceRect.x, sourceRect.y, sourceRect.width, sourceRect.height, tmp, 0, 0, dest.x, dest.y);

                tmp.GenerateMips();
                Graphics.CopyTexture(tmp, dstTexture);
                RenderTexture.ReleaseTemporary(tmp);
            }
            else
            {
                Graphics.CopyTexture(source, 0, 0, sourceRect.x, sourceRect.y, sourceRect.width, sourceRect.height, dstTexture, 0, 0, dest.x, dest.y);
            }
        }
        else
        {
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Metal || !SystemInfo.graphicsUVStartsAtTop)
                dstTexture.ReadPixels(new Rect(sourceRect.x, sourceRect.y, sourceRect.width, sourceRect.height), dest.x, dest.y);
            else
                dstTexture.ReadPixels(new Rect(sourceRect.x, source.height - sourceRect.yMax, sourceRect.width, sourceRect.height), dest.x, dest.y);
            dstTexture.Apply(true);
        }
    }

    private static bool SupportsCopyTextureBetweenRTAndTexture
    {
        get
        {
            const CopyTextureSupport kRT2TexAndTex2RT = CopyTextureSupport.RTToTexture | CopyTextureSupport.TextureToRT;
            return (SystemInfo.copyTextureSupport & kRT2TexAndTex2RT) == kRT2TexAndTex2RT;
        }
    }

    public static float kNormalizedHeightScale => 32766.0f / 65535.0f;
    public static void ResizeHeightmap(TerrainData terrainData, int resolution)
    {
        RenderTexture oldRT = RenderTexture.active;

        RenderTexture oldHeightmap = RenderTexture.GetTemporary(terrainData.heightmapTexture.descriptor);
        Graphics.Blit(terrainData.heightmapTexture, oldHeightmap);

#if UNITY_2019_3_OR_NEWER
        // terrain holes
        RenderTexture oldHoles = RenderTexture.GetTemporary(terrainData.holesTexture.width, terrainData.holesTexture.height);
        Graphics.Blit(terrainData.holesTexture, oldHoles);
#endif

        Undo.RegisterCompleteObjectUndo(terrainData, "Resize heightmap");

        float sUV = 1.0f;
        int dWidth = terrainData.heightmapResolution;
        int sWidth = resolution;

        Vector3 oldSize = terrainData.size;
        terrainData.heightmapResolution = resolution;
        terrainData.size = oldSize;

        oldHeightmap.filterMode = FilterMode.Bilinear;

        // Make sure textures are offset correctly when resampling
        // tsuv = (suv * swidth - 0.5) / (swidth - 1)
        // duv = (tsuv(dwidth - 1) + 0.5) / dwidth
        // duv = (((suv * swidth - 0.5) / (swidth - 1)) * (dwidth - 1) + 0.5) / dwidth
        // k = (dwidth - 1) / (swidth - 1) / dwidth
        // duv = suv * (swidth * k)		+ 0.5 / dwidth - 0.5 * k

        float k = (dWidth - 1.0f) / (sWidth - 1.0f) / dWidth;
        float scaleX = sUV * (sWidth * k);
        float offsetX = (float)(0.5 / dWidth - 0.5 * k);
        Vector2 scale = new Vector2(scaleX, scaleX);
        Vector2 offset = new Vector2(offsetX, offsetX);

        Graphics.Blit(oldHeightmap, terrainData.heightmapTexture, scale, offset);
        RenderTexture.ReleaseTemporary(oldHeightmap);

#if UNITY_2019_3_OR_NEWER
        oldHoles.filterMode = FilterMode.Point;
        Graphics.Blit(oldHoles, (RenderTexture)terrainData.holesTexture);
        RenderTexture.ReleaseTemporary(oldHoles);
#endif

        RenderTexture.active = oldRT;

        terrainData.DirtyHeightmapRegion(new RectInt(0, 0, terrainData.heightmapTexture.width, terrainData.heightmapTexture.height), TerrainHeightmapSyncControl.HeightAndLod);
#if UNITY_2019_3_OR_NEWER
        terrainData.DirtyTextureRegion(TerrainData.HolesTextureName, new RectInt(0, 0, terrainData.holesTexture.width, terrainData.holesTexture.height), false);
#endif
    }
    public static Material GetHeightBlitMaterial()
    {
        return new Material(Shader.Find("Hidden/TerrainTools/HeightBlit"));
    }
    public static float[,,] ConvertTextureToAlphaMap(Texture2D texture, int mapWidth, int mapHeight, int numLayers)
    {
        float[,,] alphaMap = new float[mapWidth, mapHeight, numLayers];
        for (int y = 0; y < mapHeight; y++)
        {
            for (int x = 0; x < mapWidth; x++)
            {
                Color color = texture.GetPixel(x, y);
                for (int layer = 0; layer < numLayers; layer++)
                {
                    alphaMap[x, y, layer] = color[layer];
                }
            }
        }
        return alphaMap;
    }
    public static Texture2D ResizeTexture(Texture2D originalTexture, int width, int height)
    {
        // Create a new render texture with the desired width and height,
        // and render the original texture to this new texture
        RenderTexture renderTex = RenderTexture.GetTemporary(
                    width,
                    height,
                    0,
                    RenderTextureFormat.Default,
                    RenderTextureReadWrite.Linear);

        // Render the original texture to the render texture
        Graphics.Blit(originalTexture, renderTex);

        // Create a new texture and read the render texture data into the new texture
        Texture2D resizedTexture = new Texture2D(width, height);
        RenderTexture.active = renderTex;
        resizedTexture.ReadPixels(new Rect(0, 0, width, height), 0, 0);
        resizedTexture.Apply();

        // Clean up
        RenderTexture.ReleaseTemporary(renderTex);
        RenderTexture.active = null;

        return resizedTexture;
    }

    public static float[,] ResampleMapBilinear(float[,] originalHeightMap, float scaleFactorX, float scaleFactorY)
    {
        int originalWidth = originalHeightMap.GetLength(0);
        int originalHeight = originalHeightMap.GetLength(1);
        int newWidth = Mathf.FloorToInt(originalWidth * scaleFactorX);
        int newHeight = Mathf.FloorToInt(originalHeight * scaleFactorY);

        float[,] resizedHeightMap = new float[newWidth, newHeight];

        for (int i = 0; i < newWidth; i++)
        {
            for (int j = 0; j < newHeight; j++)
            {
                float originalX = i / scaleFactorX;
                float originalY = j / scaleFactorY;

                int x1 = Mathf.FloorToInt(originalX);
                int y1 = Mathf.FloorToInt(originalY);
                int x2 = Mathf.Min(x1 + 1, originalWidth - 1);
                int y2 = Mathf.Min(y1 + 1, originalHeight - 1);

                // Bilinear interpolation
                float value = BilinearInterpolation(originalHeightMap, originalX, originalY, x1, y1, x2, y2);
                resizedHeightMap[i, j] = value;
            }
        }

        return resizedHeightMap;
    }
    public static float[,] ResampleMapLanczos3(float[,] originalHeightMap, int oldResolution, int newResolution)
    {
        float[,] newHeights = new float[newResolution, newResolution];

        for (int i = 0; i < newResolution; i++)
        {
            for (int j = 0; j < newResolution; j++)
            {
                float x = (float)i / (newResolution - 1) * (oldResolution - 1);
                float z = (float)j / (newResolution - 1) * (oldResolution - 1);

                float totalWeight = 0;
                float totalHeight = 0;
                for (int m = -3; m <= 3; m++)
                {
                    for (int n = -3; n <= 3; n++)
                    {
                        int ix = Mathf.Clamp(Mathf.RoundToInt(x) + m, 0, oldResolution - 1);
                        int iz = Mathf.Clamp(Mathf.RoundToInt(z) + n, 0, oldResolution - 1);
                        float weight = Lanczos3(Mathf.Sqrt((x - ix) * (x - ix) + (z - iz) * (z - iz)));
                        totalHeight += weight * originalHeightMap[iz, ix];
                        totalWeight += weight;
                    }
                }
                newHeights[j, i] = totalHeight / totalWeight;
            }
        }

        return newHeights;
    }

    public static float[,] ResampleMapBicubic(float[,] originalHeightMap, int oldResolution, int newResolution)
    {
        float[,] newHeights = new float[newResolution, newResolution];

        for (int i = 0; i < newResolution; i++)
        {
            for (int j = 0; j < newResolution; j++)
            {
                float x = i * (oldResolution - 1.0f) / (newResolution - 1.0f);
                float z = j * (oldResolution - 1.0f) / (newResolution - 1.0f);

                int xInt = Mathf.FloorToInt(x);
                int zInt = Mathf.FloorToInt(z);

                float[,] values = new float[4, 4];
                for (int m = -1; m <= 2; m++)
                {
                    for (int n = -1; n <= 2; n++)
                    {
                        int xIndex = Mathf.Clamp(xInt + m, 0, oldResolution - 1);
                        int zIndex = Mathf.Clamp(zInt + n, 0, oldResolution - 1);
                        values[m + 1, n + 1] = originalHeightMap[zIndex, xIndex];
                    }
                }

                float xFrac = x - xInt;
                float zFrac = z - zInt;

                newHeights[i, j] = BicubicInterpolate(values, xFrac, zFrac);
            }
        }

        return newHeights;
    }

    public static float BilinearInterpolation(float[,] heightMap, float x, float y, int x1, int y1, int x2, int y2)
    {
        float r1 = Mathf.Lerp(heightMap[x1, y1], heightMap[x2, y1], x - x1);
        float r2 = Mathf.Lerp(heightMap[x1, y2], heightMap[x2, y2], x - x1);
        return Mathf.Lerp(r1, r2, y - y1);
    }
    public static float BilinearInterpolation3D(float[,,] data, float x, float y, int layer)
    {
        int x1 = Mathf.FloorToInt(x);
        int y1 = Mathf.FloorToInt(y);
        int x2 = Mathf.Min(x1 + 1, data.GetLength(0) - 1);
        int y2 = Mathf.Min(y1 + 1, data.GetLength(1) - 1);

        float r1 = Mathf.Lerp(data[x1, y1, layer], data[x2, y1, layer], x - x1);
        float r2 = Mathf.Lerp(data[x1, y2, layer], data[x2, y2, layer], x - x1);

        return Mathf.Lerp(r1, r2, y - y1);
    }

    public static float Lanczos3(float x)
    {
        if (x == 0)
            return 1;
        if (x > 3)
            return 0;
        x *= Mathf.PI;
        return 3 * Mathf.Sin(x) * Mathf.Sin(x / 3) / (x * x);
    }

    public static float BicubicInterpolate3D(float[,,] values, float x, float y, int z)
    {
        float[] arr = new float[4];
        for (int i = -1; i < 3; i++)
        {
            for (int j = -1; j < 3; j++)
            {
                int xi = Mathf.Clamp(Mathf.FloorToInt(x) + i, 0, values.GetLength(0) - 1);
                int yj = Mathf.Clamp(Mathf.FloorToInt(y) + j, 0, values.GetLength(1) - 1);
                arr[j + 1] = values[xi, yj, z];
            }
            arr[i + 1] = CubicInterpolate(arr[0], arr[1], arr[2], arr[3], y - Mathf.Floor(y));
        }
        return CubicInterpolate(arr[0], arr[1], arr[2], arr[3], x - Mathf.Floor(x));
    }

    public static float BicubicInterpolate(float[,] values, float x, float y)
    {
        float[] arr = new float[4];
        for (int i = 0; i < 4; i++)
        {
            arr[i] = CubicInterpolate(values[i, 0], values[i, 1], values[i, 2], values[i, 3], y);
        }
        return CubicInterpolate(arr[0], arr[1], arr[2], arr[3], x);
    }
    public static float CubicInterpolate(float v0, float v1, float v2, float v3, float x)
    {
        float P = (v3 - v2) - (v0 - v1);
        float Q = (v0 - v1) - P;
        float R = v2 - v0;
        float S = v1;

        return P * Mathf.Pow(x, 3) + Q * Mathf.Pow(x, 2) + R * x + S;
    }

    public static Texture2D ConvertFloatArrayToTexture2D(float[,] floatArray)
    {
        if(floatArray == null)
        {
            return null;
        }
        
        int width = floatArray.GetLength(0);
        int height = floatArray.GetLength(1);
        Texture2D texture = new Texture2D(width, height);

        for (int i = 0; i < width; i++)
        {
            for (int j = 0; j < height; j++)
            {
                float value = floatArray[i, j];
                Color color = new Color(value, value, value);  // Grayscale color
                texture.SetPixel(i, j, color);
            }
        }

        texture.Apply();

        return texture;
    }

    public static float[,] ConvertTexture2DToFloatArray(Texture2D texture)
    {
        int width = texture.width;
        int height = texture.height;
        float[,] floatArray = new float[width, height];

        for (int i = 0; i < width; i++)
        {
            for (int j = 0; j < height; j++)
            {
                float value = texture.GetPixel(i, j).grayscale;  // Use the grayscale value
                floatArray[i, j] = value;
            }
        }

        return floatArray;
    }
    
    public static float[,] ConvertTexture2DToFloatArrayHDR(Texture2D texture)
    {
        int width = texture.width;
        int height = texture.height;
        float[,] floatArray = new float[width, height];

        for (int i = 0; i < width; i++)
        {
            for (int j = 0; j < height; j++)
            {
                float value = texture.GetPixel(i, j).grayscale;  // Use the grayscale value
                floatArray[i, j] = value*255f;
            }
        }

        return floatArray;
    }

   public static bool ValidateSplatmap(Vector2Int resolution, int expectedCount, int tilesCount)
    {

        if (!IsPowerOfTwo(resolution.x))
        {
            EditorUtility.DisplayDialog("Error", "The selected splatmap resolutions aren't a power of two.", "OK");
            return false;
        }
        else if (resolution.x != resolution.y)
        {
            EditorUtility.DisplayDialog("Error", "The selected splatmaps resolution isn't square.", "OK");
            return false;
        }
        else if (expectedCount > tilesCount)
        {
            EditorUtility.DisplayDialog("Error", "The terrains selected aren't square.", "OK");
            return false;
        }
        return true;
    }
    public static bool IsPowerOfTwo(int x)
    {
        return (x != 0) && ((x & (x - 1)) == 0);
    }

    public static bool IsInteger(double x)
    {
        return (x % 1) == 0;
    }
}
