using System.Collections.Generic;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

//  RGLDecoratorBridge - Connects RGL Flora layers to the Decorator system
//
//  Fully automatic: parses the terrain hash from LayeredHeightmapGenerator,
//  resolves terrain type → region → decorator configuration via the existing
//  MBSplatmapImportHelper + MBRegenerationHelper pipeline.
//
//  The RGL mask (grass/flora intensity) is saved as a PNG texture asset and
//  assigned to MBTerrainDecorator.vegetationMaskTexture. The decorator's
//  existing vegetation mask compositing pipeline handles the overlay
//  punch-through during decoration.

/// <summary>
/// Bridges the Flora Populator's RGL layers to the Terrain Decorator.
/// 
/// Full pipeline (all automatic from hash):
///   1. Read hash from LayeredHeightmapGenerator on the terrain
///   2. Parse → terrain type → region type (via MBSplatmapImportHelper)
///   3. SetupDecoratorFromHash creates the 4 tlt_ generator layers
///   4. Generate RGL mask from Warband channel + splatmap masks
///   5. Save mask as PNG asset → assign to decorator.vegetationMaskTexture
///
/// This is a static utility - no MonoBehaviour required.
/// </summary>
public static class RGLDecoratorBridge
{
    /// <summary>
    /// Result of an Apply operation for logging/UI feedback.
    /// </summary>
    public class ApplyResult
    {
        public bool Success;
        public string Message;
        public int TerrainType;
        public string TerrainTypeName;
        public string RegionType;
        public bool DecoratorRebuilt;
        public bool MaskAssigned;
        
        public static ApplyResult Failure(string msg) => new ApplyResult { Success = false, Message = msg };
        public static ApplyResult Ok(string msg) => new ApplyResult { Success = true, Message = msg };
    }
    
    //  Main Entry Point
    
    /// <summary>
    /// Apply RGL integration to the Decorator. Fully automatic from hash.
    ///
    /// Steps:
    ///   1. If decorator has no generator layers → calls SetupDecoratorFromHash
    ///      to create the standard tlt_ layer configuration.
    ///   2. Generates the RGL mask from the Warband channel + splatmap masks.
    ///   3. Saves the mask as a PNG texture asset.
    ///   4. Assigns the texture to decorator.vegetationMaskTexture.
    /// </summary>
    /// <param name="terrain">The scene terrain (must have LayeredHeightmapGenerator + MBTerrainDecorator)</param>
    /// <param name="rglMask">
    ///   The RGL grass mask (alphamap resolution). Null = clear mask.
    ///   Values 0..1 where 1 = grass present = punch through overlay to show base.
    /// </param>
    /// <param name="maskStrength">Vegetation mask strength (0-1), assigned to decorator.vegetationMaskStrength.</param>
    /// <param name="rebuildDecorator">
    ///   If true, forces a full decorator rebuild from hash even if layers exist.
    ///   If false (default), only rebuilds if no generator layers are found.
    /// </param>
    public static ApplyResult Apply(
        Terrain terrain,
        float[,] rglMask = null,
        float maskStrength = 1f,
        bool rebuildDecorator = false)
    {
#if UNITY_EDITOR
        if (terrain == null)
            return ApplyResult.Failure("No terrain provided.");
        
        var decorator = terrain.GetComponent<MBTerrainDecorator>();
        if (decorator == null)
            return ApplyResult.Failure("No MBTerrainDecorator on terrain. Add one first.");
        
        var heightmapGen = terrain.GetComponent<LayeredHeightmapGenerator>();
        if (heightmapGen == null)
            return ApplyResult.Failure("No LayeredHeightmapGenerator on terrain.");
        
        string hash = heightmapGen.CurrentTerrainHash;
        if (string.IsNullOrEmpty(hash) || !MBTerrainRegenerator.ValidateHash(hash))
            return ApplyResult.Failure("Invalid or empty terrain hash on LayeredHeightmapGenerator.");
        
        // Parse terrain data
        var data = MBTerrainRegenerator.ParseHash(hash);
        if (data == null)
            return ApplyResult.Failure("Failed to parse terrain hash.");
        
        int terrainType = data.TerrainType;
        string terrainTypeName = MBTerrainRegenerator.GetTerrainTypeName(terrainType);
        
        var result = new ApplyResult
        {
            TerrainType = terrainType,
            TerrainTypeName = terrainTypeName
        };
        
        Undo.RecordObject(decorator, "RGL Apply to Decorator");
        
        bool hasGeneratorLayers = HasGeneratorRules(decorator);
        
        if (!hasGeneratorLayers || rebuildDecorator)
        {
            EditorUtility.DisplayProgressBar("RGL Apply", $"Setting up decorator for {terrainTypeName}...", 0.2f);
            
            string generatorPath = FindGeneratorDataPath();
            string scoPath = FindScoDataPath();
            
            MBRegenerationHelper.SetupDecoratorFromHash(
                decorator, hash, generatorPath, scoPath, 
                generatorRulesOnly: false
            );
            
            result.DecoratorRebuilt = true;
            result.RegionType = MBSplatmapImportHelper.GetRegionTypeFromCode(terrainType).ToString();
            
            Debug.Log($"[RGLDecoratorBridge] Rebuilt decorator for {terrainTypeName} ({result.RegionType})");
        }
        
        if (rglMask != null)
        {
            EditorUtility.DisplayProgressBar("RGL Apply", "Saving vegetation mask...", 0.6f);
            
            Texture2D maskTexture = SaveMaskAsAsset(rglMask, terrain);
            
            if (maskTexture != null)
            {
                decorator.vegetationMaskTexture = maskTexture;
                decorator.vegetationMaskChannel = MBTerrainDecorator.ImageChannel.r;
                decorator.vegetationMaskStrength = 1f; // Always full opacity - R channel carries the mask data
                decorator.vegetationMaskInvert = false; // Mask is pre-baked with correct orientation
                
                result.MaskAssigned = true;
                Debug.Log($"[RGLDecoratorBridge] Vegetation mask assigned: {AssetDatabase.GetAssetPath(maskTexture)}");
            }
            else
            {
                Debug.LogWarning("[RGLDecoratorBridge] Failed to save vegetation mask texture.");
            }
        }
        else
        {
            // No mask - clear vegetation mask from decorator
            if (decorator.vegetationMaskTexture != null)
            {
                decorator.vegetationMaskTexture = null;
                decorator.vegetationMaskStrength = 0f;
                Debug.Log("[RGLDecoratorBridge] Cleared vegetation mask from decorator.");
            }
        }
        
        EditorUtility.SetDirty(decorator);
        EditorUtility.ClearProgressBar();
        
        result.Success = true;
        
        string maskStatus = result.MaskAssigned ? "vegetation mask assigned" : "no mask";
        result.Message = result.DecoratorRebuilt
            ? $"Rebuilt decorator for {terrainTypeName}, {maskStatus}."
            : $"Updated decorator for {terrainTypeName}, {maskStatus}.";
        
        Debug.Log($"[RGLDecoratorBridge] {result.Message}");
        return result;
#else
        return ApplyResult.Failure("Editor-only operation.");
#endif
    }
    
    //  Vegetation Mask Persistence
    
    /// <summary>
    /// Save the RGL mask as a PNG texture asset in the module's scene data folder.
    /// Returns the loaded Texture2D asset reference, or null on failure.
    /// </summary>
    private static Texture2D SaveMaskAsAsset(float[,] mask, Terrain terrain)
    {
#if UNITY_EDITOR
        int w = mask.GetLength(0);
        int h = mask.GetLength(1);
        
        var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
                tex.SetPixel(x, y, new Color(mask[x, y], 0, 0, 1));
        tex.Apply();
        
        // Resolve save path from MBSceneDataManager
        string absPath = null;
        var sceneData = Object.FindFirstObjectByType<MBSceneDataManager>();
        if (sceneData != null)
        {
            absPath = MBPathHelpers.ModSceneDataGrassMaskPath(sceneData.Module.ID, sceneData.SceneName);
        }
        else
        {
            // Fallback: save next to the terrain asset
            string terrainPath = UnityEditor.AssetDatabase.GetAssetPath(terrain.terrainData);
            if (!string.IsNullOrEmpty(terrainPath))
            {
                string dir = System.IO.Path.GetDirectoryName(terrainPath);
                absPath = System.IO.Path.Combine(Application.dataPath, 
                    dir.Replace("Assets", "").TrimStart('/','\\'), "grass_mask.png");
            }
            else
            {
                absPath = System.IO.Path.Combine(Application.dataPath, "grass_mask.png");
            }
        }
        
        // Ensure directory
        string directory = System.IO.Path.GetDirectoryName(absPath);
        if (!System.IO.Directory.Exists(directory))
            System.IO.Directory.CreateDirectory(directory);
        
        System.IO.File.WriteAllBytes(absPath, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);
        
        // Import into AssetDatabase
        string relPath = "Assets" + absPath.Substring(Application.dataPath.Length);
        AssetDatabase.ImportAsset(relPath, ImportAssetOptions.ForceUpdate);
        
        // Configure import settings for single-channel mask
        var importer = (TextureImporter)AssetImporter.GetAtPath(relPath);
        if (importer != null)
        {
            importer.textureType = TextureImporterType.Default;
            importer.sRGBTexture = false;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.isReadable = true;
            importer.SaveAndReimport();
        }
        
        return AssetDatabase.LoadAssetAtPath<Texture2D>(relPath);
#else
        return null;
#endif
    }
    
    //  Mask Generation
    
    /// <summary>
    /// Generate an RGL mask from the Warband flora generator for a given channel.
    /// Output is at alphamap resolution (matching decorator).
    /// </summary>
    public static float[,] GenerateRGLMask(
        Terrain terrain,
        string terrainCode,
        MBEditor.Tools.Flora.Logic.WarbandFloraGenerator.WarbandChannel channel)
    {
        if (terrain == null || string.IsNullOrEmpty(terrainCode)) return null;
        
        int w = terrain.terrainData.alphamapWidth;
        int h = terrain.terrainData.alphamapHeight;
        
        return MBEditor.Tools.Flora.Logic.WarbandFloraGenerator.GenerateIntensityMap(
            terrain, w, h, terrainCode, channel);
    }
    
    /// <summary>
    /// Generate an RGL mask and composite it with splatmap masks.
    /// Handles inversion, threshold, and splatmap multiplication.
    /// </summary>
    public static float[,] GenerateCompositeMask(
        Terrain terrain,
        string terrainCode,
        FloraDecoratorLayer layerConfig)
    {
        if (terrain == null || string.IsNullOrEmpty(terrainCode)) return null;
        
        int w = terrain.terrainData.alphamapWidth;
        int h = terrain.terrainData.alphamapHeight;
        
        // Start with warband mask (Green channel is the standard grass channel)
        var channel = MBEditor.Tools.Flora.Logic.WarbandFloraGenerator.WarbandChannel.Green;
        float[,] mask = MBEditor.Tools.Flora.Logic.WarbandFloraGenerator.GenerateIntensityMap(
            terrain, w, h, terrainCode, channel);
        
        if (mask == null)
        {
            Debug.LogWarning("[RGLDecoratorBridge] Failed to generate Warband intensity map.");
            return null;
        }
        
        // Apply splatmap masking if configured
        if (layerConfig != null && layerConfig.SplatmapMasks.Count > 0)
        {
            var td = terrain.terrainData;
            float[,,] alphamaps = td.GetAlphamaps(0, 0, td.alphamapWidth, td.alphamapHeight);
            
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    float splatWeight = 0f;
                    foreach (var splatMask in layerConfig.SplatmapMasks)
                    {
                        int layerIdx = splatMask.LayerID;
                        if (layerIdx < td.terrainLayers.Length)
                        {
                            float val = alphamaps[x, y, layerIdx];
                            if (val > splatMask.Threshold)
                                splatWeight += val;
                        }
                    }
                    
                    // Multiply warband mask by splatmap validity
                    mask[x, y] *= Mathf.Clamp01(splatWeight);
                }
            }
        }
        
        return mask;
    }
    
    //  Query Methods
    
    /// <summary>
    /// Check if decorator has any generator-type rules (tlt_ layers).
    /// </summary>
    public static bool HasGeneratorRules(MBTerrainDecorator decorator)
    {
        if (decorator == null) return false;
        foreach (var layer in decorator.layers)
        {
            if (!layer.active) continue;
            foreach (var rule in layer.rules)
            {
                if (rule.active && rule.filter == MBTerrainDecorator.FilterType.generator)
                    return true;
            }
        }
        return false;
    }
    
    /// <summary>
    /// Check if decorator has a vegetation mask texture assigned.
    /// </summary>
    public static bool HasVegetationMask(MBTerrainDecorator decorator)
    {
        return decorator != null 
            && decorator.vegetationMaskTexture != null 
            && decorator.vegetationMaskStrength > 0f;
    }
    
    /// <summary>
    /// Get status string for UI display.
    /// </summary>
    public static string GetStatusString(MBTerrainDecorator decorator)
    {
        if (decorator == null) return "No Decorator";
        
        bool hasGen = HasGeneratorRules(decorator);
        bool hasMask = HasVegetationMask(decorator);
        
        if (!hasGen && !hasMask) return "No RGL data";
        if (hasGen && !hasMask) return "Generator layers active (no vegetation mask)";
        if (!hasGen && hasMask) return "Vegetation mask assigned (no generator layers)";
        return $"Generator layers + vegetation mask (strength: {decorator.vegetationMaskStrength:F2})";
    }
    
    /// <summary>
    /// Clear the vegetation mask from the decorator.
    /// </summary>
    public static void ClearVegetationMask(MBTerrainDecorator decorator)
    {
#if UNITY_EDITOR
        if (decorator == null) return;
        
        Undo.RecordObject(decorator, "Clear Vegetation Mask");
        decorator.vegetationMaskTexture = null;
        decorator.vegetationMaskStrength = 0f;
        EditorUtility.SetDirty(decorator);
        
        Debug.Log("[RGLDecoratorBridge] Cleared vegetation mask from decorator.");
#endif
    }
    
    //  Path Resolution
    
    private static string FindGeneratorDataPath()
    {
        var sceneData = Object.FindFirstObjectByType<MBSceneDataManager>();
        if (sceneData != null)
            return sceneData.EditorGeneratorSceneDataPath;
        return "";
    }
    
    private static string FindScoDataPath()
    {
        var sceneData = Object.FindFirstObjectByType<MBSceneDataManager>();
        if (sceneData != null)
            return sceneData.EditorScoSceneDataPath;
        return "";
    }
}
