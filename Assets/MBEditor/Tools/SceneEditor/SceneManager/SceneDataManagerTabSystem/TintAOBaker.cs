using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using System.IO;

/// <summary>
/// Bakes ambient occlusion from scene static objects onto terrain texture.
/// Raycasts from terrain surface points to detect occlusion from nearby geometry.
/// </summary>
public static class TintAOBaker
{
    #region Temporary Colliders
    
    private static List<MeshCollider> _tempColliders = new List<MeshCollider>();
    private static TerrainCollider _terrainCollider;
    private static bool _hadTerrainCollider;
    
    /// <summary>
    /// Add temporary mesh colliders to all static objects for raycasting.
    /// </summary>
    public static void AddTempColliders(bool includeTerrainSelf, Terrain terrain)
    {
        _tempColliders.Clear();
        
        // Find all static mesh filters
        var allMeshFilters = Object.FindObjectsOfType<MeshFilter>();
        
        foreach (var mf in allMeshFilters)
        {
            if (mf == null || mf.sharedMesh == null)
                continue;
            
            // Check if static
            bool isStatic = mf.gameObject.isStatic || 
                (GameObjectUtility.GetStaticEditorFlags(mf.gameObject) & StaticEditorFlags.ContributeGI) != 0;
            
            if (!isStatic)
                continue;
            
            // Skip if already has collider
            if (mf.GetComponent<Collider>() != null)
                continue;
            
            // Add temporary mesh collider
            var mc = mf.gameObject.AddComponent<MeshCollider>();
            mc.sharedMesh = mf.sharedMesh;
            _tempColliders.Add(mc);
        }
        
        // Handle terrain collider
        if (terrain != null)
        {
            _terrainCollider = terrain.GetComponent<TerrainCollider>();
            _hadTerrainCollider = _terrainCollider != null;
            
            if (includeTerrainSelf)
            {
                if (_terrainCollider == null)
                {
                    _terrainCollider = terrain.gameObject.AddComponent<TerrainCollider>();
                    _terrainCollider.terrainData = terrain.terrainData;
                }
            }
            else
            {
                // Temporarily disable terrain collider
                if (_terrainCollider != null)
                {
                    _terrainCollider.enabled = false;
                }
            }
        }
        
        Debug.Log($"[TintAOBaker] Added {_tempColliders.Count} temporary colliders for AO baking.");
    }
    
    /// <summary>
    /// Remove all temporary colliders.
    /// </summary>
    public static void RemoveTempColliders(Terrain terrain)
    {
        foreach (var mc in _tempColliders)
        {
            if (mc != null)
            {
                Object.DestroyImmediate(mc);
            }
        }
        _tempColliders.Clear();
        
        // Restore terrain collider state
        if (terrain != null && _terrainCollider != null)
        {
            if (!_hadTerrainCollider)
            {
                Object.DestroyImmediate(_terrainCollider);
            }
            else
            {
                _terrainCollider.enabled = true;
            }
        }
        _terrainCollider = null;
        
        Debug.Log("[TintAOBaker] Removed temporary colliders.");
    }
    
    #endregion
    
    #region AO Baking
    
    /// <summary>
    /// Bake ambient occlusion for a terrain into a texture.
    /// </summary>
    /// <param name="terrain">Target terrain</param>
    /// <param name="width">Output texture width</param>
    /// <param name="height">Output texture height</param>
    /// <param name="rayCount">Number of rays per pixel</param>
    /// <param name="range">Maximum ray distance</param>
    /// <param name="intensity">AO intensity multiplier</param>
    /// <param name="includeTerrainSelf">Include terrain in occlusion calculation</param>
    /// <param name="useBlur">Apply blur pass to soften result</param>
    /// <returns>Baked AO texture (grayscale in RGB)</returns>
    public static Texture2D BakeAO(
        Terrain terrain,
        int width,
        int height,
        int rayCount = 64,
        float range = 5f,
        float intensity = 1f,
        bool includeTerrainSelf = true,
        bool useBlur = true)
    {
        if (terrain == null || terrain.terrainData == null)
        {
            Debug.LogError("[TintAOBaker] No terrain provided.");
            return null;
        }
        
        // Setup colliders
        AddTempColliders(includeTerrainSelf, terrain);
        
        try
        {
            // Create output texture
            var aoTex = new Texture2D(width, height, TextureFormat.RGB24, false);
            Color[] pixels = new Color[width * height];
            
            // Initialize to white (no occlusion)
            for (int i = 0; i < pixels.Length; i++)
            {
                pixels[i] = Color.white;
            }
            
            TerrainData td = terrain.terrainData;
            Vector3 terrainPos = terrain.transform.position;
            Vector3 terrainSize = td.size;
            
            // Small offset to avoid self-intersection
            float surfaceOffset = 0.1f;
            
            int totalPixels = width * height;
            int processed = 0;
            
            // Bake each pixel
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    // UV coordinates
                    float u = (float)x / (width - 1);
                    float v = (float)y / (height - 1);
                    
                    // World position on terrain
                    float worldX = terrainPos.x + u * terrainSize.x;
                    float worldZ = terrainPos.z + v * terrainSize.z;
                    float worldY = terrain.SampleHeight(new Vector3(worldX, 0, worldZ)) + terrainPos.y;
                    
                    Vector3 position = new Vector3(worldX, worldY + surfaceOffset, worldZ);
                    
                    // Get terrain normal at this point
                    Vector3 normal = td.GetInterpolatedNormal(u, v);
                    
                    // Calculate occlusion
                    float occlusion = CalculateOcclusion(position, normal, rayCount, range);
                    
                    // Apply intensity
                    occlusion = Mathf.Pow(occlusion, intensity);
                    
                    // Store as grayscale (white = no occlusion, black = full occlusion)
                    pixels[y * width + x] = new Color(occlusion, occlusion, occlusion);
                    
                    processed++;
                    
                    // Progress bar (update every 100 pixels)
                    if (processed % 100 == 0)
                    {
                        float progress = (float)processed / totalPixels;
                        if (EditorUtility.DisplayCancelableProgressBar(
                            "Baking AO", 
                            $"Processing pixel {processed}/{totalPixels} ({progress * 100:F1}%)", 
                            progress))
                        {
                            EditorUtility.ClearProgressBar();
                            RemoveTempColliders(terrain);
                            Object.DestroyImmediate(aoTex);
                            return null;
                        }
                    }
                }
            }
            
            aoTex.SetPixels(pixels);
            aoTex.Apply();
            
            // Optional blur pass
            if (useBlur)
            {
                EditorUtility.DisplayProgressBar("Baking AO", "Applying blur...", 0.95f);
                ApplyBlur(aoTex);
            }
            
            EditorUtility.ClearProgressBar();
            
            Debug.Log($"[TintAOBaker] Baked AO texture: {width}x{height}, {rayCount} rays, range {range}");
            
            return aoTex;
        }
        finally
        {
            RemoveTempColliders(terrain);
            EditorUtility.ClearProgressBar();
        }
    }
    
    /// <summary>
    /// Calculate occlusion at a point by casting rays in hemisphere.
    /// </summary>
    private static float CalculateOcclusion(Vector3 position, Vector3 normal, int rayCount, float range)
    {
        int unoccludedRays = 0;
        
        for (int i = 0; i < rayCount; i++)
        {
            // Random direction in hemisphere oriented to normal
            Vector3 randomDir = Random.onUnitSphere;
            
            // Flip if pointing into surface
            if (Vector3.Dot(randomDir, normal) < 0)
            {
                randomDir = -randomDir;
            }
            
            // Raycast
            if (!Physics.Raycast(position, randomDir, range))
            {
                unoccludedRays++;
            }
        }
        
        // Return ratio of unoccluded rays (1 = no occlusion, 0 = full occlusion)
        return (float)unoccludedRays / rayCount;
    }
    
    /// <summary>
    /// Apply simple box blur to soften AO.
    /// </summary>
    private static void ApplyBlur(Texture2D tex)
    {
        int width = tex.width;
        int height = tex.height;
        
        Color[] src = tex.GetPixels();
        Color[] dst = new Color[src.Length];
        
        // 3x3 box blur
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                float sum = 0;
                int count = 0;
                
                for (int ky = -1; ky <= 1; ky++)
                {
                    for (int kx = -1; kx <= 1; kx++)
                    {
                        int sx = Mathf.Clamp(x + kx, 0, width - 1);
                        int sy = Mathf.Clamp(y + ky, 0, height - 1);
                        
                        sum += src[sy * width + sx].r;
                        count++;
                    }
                }
                
                float avg = sum / count;
                dst[y * width + x] = new Color(avg, avg, avg);
            }
        }
        
        tex.SetPixels(dst);
        tex.Apply();
    }
    
    #endregion
    
    #region Save/Load
    
    /// <summary>
    /// Save AO texture to PNG file.
    /// </summary>
    public static string SaveAOTexture(Texture2D aoTex, string folder, string layerName)
    {
        if (aoTex == null || string.IsNullOrEmpty(folder))
            return null;
        
        string safeName = layerName.Replace(" ", "_").Replace("/", "_");
        string path = $"{folder}/{safeName}_{aoTex.width}x{aoTex.height}.png";
        
        byte[] pngData = aoTex.EncodeToPNG();
        File.WriteAllBytes(path, pngData);
        
        AssetDatabase.ImportAsset(path);
        
        // Configure import settings
        var importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer != null)
        {
            importer.isReadable = true;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.mipmapEnabled = false;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.sRGBTexture = true;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.SaveAndReimport();
        }
        
        Debug.Log($"[TintAOBaker] Saved AO texture to: {path}");
        
        return path;
    }
    
    #endregion
}