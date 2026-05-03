using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using MountAndBlade.Data;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;

public static class TerrainPaletteHelpers
{
    // Layer index → M&B material name mapping
    // 0_gray_stone  = stone_a       | Diffuse: rock_b        | Normal: rock_b_normalmap
    // 1_brown_stone = patch_rock    | Diffuse: rock_c        | Normal: rock_c_normalmap
    // 2_turf        = grassy_ground | Diffuse: ground1       | Normal: dry_grass_bump
    // 3_steppe      = ground_steppe | Diffuse: dry_grass     | Normal: dry_grass_bump
    // 4_snow        = snow          | Diffuse: snow          | Normal: snow_bump
    // 5_earth       = ground_earth  | Diffuse: ground_earth  | Normal: ground_earth_bump
    // 6_desert      = ground_desert | Diffuse: desert        | Normal: desert_bump
    // 7_forest      = ground_forest | Diffuse: ground_forest | Normal: ground_earth_bump
    // 8_pebbles     = pebbles       | Diffuse: pebbles       | Normal: pebbles_normal
    // 9_village     = ground_village| Diffuse: ground_village | Normal: ground_village_bump
    // 10_path       = ground_path   | Diffuse: ground_path   | Normal: ground_path_bump

    private static readonly Dictionary<string, string> PaletteMaterials = new()
    {
        { "0_gray_stone",  "stone_a" },
        { "1_brown_stone", "patch_rock" },
        { "2_turf",        "grassy_ground" },
        { "3_steppe",      "ground_steppe" },
        { "4_snow",        "snow" },
        { "5_earth",       "ground_earth" },
        { "6_desert",      "ground_desert" },
        { "7_forest",      "ground_forest" },
        { "8_pebbles",     "pebbles" },
        { "9_village",     "ground_village" },
        { "10_path",       "ground_path" }
    };

    public static MBTerrainPalette CommonTerrainPalette()
    {
        return AssetDatabase.LoadAssetAtPath<MBTerrainPalette>(MBPathHelpers.CommonTerrainPalette());
    }

    /// <summary>
    /// Create a unique terrain palette for a module, sourcing textures from BRF data.
    /// </summary>
    public static void CreateUniqueTerrainPalette(MBModule module)
    {
        var commonPalette = CommonTerrainPalette();
        if (commonPalette == null)
        {
            Debug.LogError("Common terrain palette not found.");
            return;
        }

        string configPath = MBPathHelpers.ModConfigsPath(module.ID);
        if (!Directory.Exists(configPath))
        {
            Directory.CreateDirectory(configPath);
            AssetDatabase.Refresh();
        }

        string layersPath = MBPathHelpers.ModTerrainPaletteLayers(module.ID);
        if (!Directory.Exists(layersPath))
        {
            Directory.CreateDirectory(layersPath);
            AssetDatabase.Refresh();
        }

        // Clone common palette layers into per-module copies
        var modPalette = ScriptableObject.CreateInstance<MBTerrainPalette>();

        foreach (var layer in commonPalette.PaletteLayers)
        {
            string srcPath = AssetDatabase.GetAssetPath(layer);
            string dstPath = Path.Combine(layersPath, $"{layer.name}.terrainlayer");

            AssetDatabase.CopyAsset(srcPath, dstPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            var clonedLayer = AssetDatabase.LoadAssetAtPath<UnityEngine.TerrainLayer>(dstPath);
            modPalette.PaletteLayers.Add(clonedLayer);
        }

        // Apply textures from BRF database
        ApplyModTexturesToPalette(module, modPalette);

        AssetDatabase.CreateAsset(modPalette, MBPathHelpers.ModTerrainPalette(module.ID));
        EditorUtility.SetDirty(modPalette);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
    }

    /// <summary>
    /// Apply mod-specific textures to terrain palette layers using the BRF database.
    /// Resolves diffuse and normal textures from BrfMaterialEntry data.
    /// 
    /// Resolution order per palette slot:
    ///   1. Module BRF database  (mod's own materials)
    ///   2. Native BRF database  (fallback to game base)
    ///   3. Leave unchanged      (keeps common palette default)
    /// </summary>
    public static void ApplyModTexturesToPalette(MBModule module, MBTerrainPalette palette)
    {
        if (palette == null)
        {
            Debug.LogError("Terrain palette is null.");
            return;
        }

        // Load module BRF database
        string dbPath = MBPathHelpers.ModBRFDataBasePath(module.ID);
        var modDb = AssetDatabase.LoadAssetAtPath<ModBrfDataBase>(dbPath);

        // Load native BRF database as fallback
        ModBrfDataBase nativeDb = null;
        if (module.ID != "Native")
        {
            string nativeDbPath = MBPathHelpers.ModBRFDataBasePath("Native");
            nativeDb = AssetDatabase.LoadAssetAtPath<ModBrfDataBase>(nativeDbPath);
            nativeDb?.BuildLookups();
        }

        modDb?.BuildLookups();

        string normalsOutputPath = MBPathHelpers.ModTerrainPaletteNormals(module.ID);
        if (!Directory.Exists(normalsOutputPath))
        {
            Directory.CreateDirectory(normalsOutputPath);
            AssetDatabase.Refresh();
        }

        foreach (var kvp in PaletteMaterials)
        {
            int layerIndex = int.Parse(kvp.Key.Split('_')[0]);
            string mbMaterialName = kvp.Value;

            if (layerIndex >= palette.PaletteLayers.Count)
            {
                Debug.LogWarning($"Palette has only {palette.PaletteLayers.Count} layers, " +
                                 $"skipping index {layerIndex} ({mbMaterialName}).");
                continue;
            }

            var layer = palette.PaletteLayers[layerIndex];

            // Resolve material entry from BRF databases
            BrfMaterialEntry matEntry = modDb?.FindMaterialEntry(mbMaterialName);
            matEntry ??= nativeDb?.FindMaterialEntry(mbMaterialName);

            if (matEntry == null)
            {
                Debug.LogWarning($"Terrain material '{mbMaterialName}' not found in any BRF database, " +
                                 $"keeping default for layer {layerIndex}.");
                continue;
            }

            Texture2D diffuse = ResolvePaletteTexture(matEntry.DiffuseA, matEntry.DiffuseATexture,
                modDb, nativeDb);

            if (diffuse != null)
            {
                layer.diffuseTexture = diffuse;
            }
            else
            {
                // Try loading from the Unity material if it exists
                if (matEntry.UnityMaterial != null)
                {
                    var matTex = matEntry.UnityMaterial.GetTexture("_MainTex") as Texture2D;
                    if (matTex != null)
                        layer.diffuseTexture = matTex;
                }
            }

            Texture2D normal = ResolvePaletteNormal(
                layerIndex, kvp.Key, matEntry, modDb, nativeDb, normalsOutputPath);

            if (normal != null)
            {
                layer.normalMapTexture = normal;
            }

            // Apply UV Scale from Ground Specs
            if (module.groundSpecs != null && module.groundSpecs.GroundSpecs != null)
            {
                var spec = module.groundSpecs.GroundSpecs.Find(s => s.Index == layerIndex);
                if (spec != null && spec.UVScale > 0)
                {
                    float size = 40f / spec.UVScale;
                    layer.tileSize = new Vector2(size, size);
                }
            }

            EditorUtility.SetDirty(layer);
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
    }

    /// <summary>
    /// Resolve a diffuse texture for a palette slot.
    /// Uses the pre-resolved Texture2D from BrfMaterialEntry first,
    /// then falls back to cross-BRF lookup by texture name.
    /// </summary>
    private static Texture2D ResolvePaletteTexture(
        string textureName,
        Texture2D preResolved,
        ModBrfDataBase modDb,
        ModBrfDataBase nativeDb)
    {
        // Already resolved during populate step
        if (preResolved != null)
            return preResolved;

        if (string.IsNullOrEmpty(textureName) ||
            textureName.Equals("none", StringComparison.OrdinalIgnoreCase))
            return null;

        // Strip extension if present
        string cleanName = Path.GetFileNameWithoutExtension(textureName);

        // Try module database texture lookup
        Texture2D tex = modDb?.FindTexture(cleanName);
        if (tex != null) return tex;

        // Try native database fallback
        tex = nativeDb?.FindTexture(cleanName);
        if (tex != null) return tex;

        return null;
    }

    /// <summary>
    /// Resolve or convert a normal map for a terrain palette slot.
    /// DDS normal maps need conversion to PNG with proper TextureImporter settings.
    /// </summary>
    private static Texture2D ResolvePaletteNormal(
        int layerIndex,
        string layerKey,
        BrfMaterialEntry matEntry,
        ModBrfDataBase modDb,
        ModBrfDataBase nativeDb,
        string normalsOutputPath)
    {
        // Check if we already have a converted normal
        Texture2D bumpTex = matEntry.BumpTexture;

        if (bumpTex != null)
        {
            // If it's already imported as NormalMap type, use directly
            string texPath = AssetDatabase.GetAssetPath(bumpTex);
            if (!string.IsNullOrEmpty(texPath))
            {
                var importer = AssetImporter.GetAtPath(texPath) as TextureImporter;
                if (importer != null && importer.textureType == TextureImporterType.NormalMap)
                    return bumpTex;
            }
        }

        // Need to find the source DDS and convert
        string bumpName = matEntry.Bump;
        if (string.IsNullOrEmpty(bumpName) ||
            bumpName.Equals("none", StringComparison.OrdinalIgnoreCase))
            return null;

        string cleanBumpName = Path.GetFileNameWithoutExtension(bumpName);

        // Find the DDS source file from BRF texture folders
        string ddsPath = FindDdsInBrfFolders(cleanBumpName, modDb);
        ddsPath ??= FindDdsInBrfFolders(cleanBumpName, nativeDb);

        if (string.IsNullOrEmpty(ddsPath))
        {
            // Fall back to cross-BRF texture lookup (already imported)
            Texture2D fallback = modDb?.FindTexture(cleanBumpName);
            fallback ??= nativeDb?.FindTexture(cleanBumpName);
            return fallback;
        }

        // Convert DDS → PNG with normal map import settings
        string outName = $"{layerKey}_nm";
        string outputPath = Path.Combine(normalsOutputPath, outName + ".png");

        DDSToPNG(ddsPath, outputPath, true);

        return AssetDatabase.LoadAssetAtPath<Texture2D>(outputPath);
    }

    /// <summary>
    /// Search all BRF Textures/ folders in a database for a DDS file by name.
    /// Returns the Unity-relative path to the DDS if found.
    /// </summary>
    private static string FindDdsInBrfFolders(string textureName, ModBrfDataBase db)
    {
        if (db == null || string.IsNullOrEmpty(textureName))
            return null;

        foreach (var brf in db.BrfAssets)
        {
            if (brf == null || string.IsNullOrEmpty(brf.FolderPath))
                continue;

            string texturesDir = Path.Combine(brf.FolderPath, "Textures");
            if (!Directory.Exists(texturesDir))
                continue;

            // Check for the DDS file (case-insensitive)
            var files = Directory.GetFiles(texturesDir, "*.dds");
            foreach (var file in files)
            {
                string name = Path.GetFileNameWithoutExtension(file);
                if (name.Equals(textureName, StringComparison.OrdinalIgnoreCase))
                    return file;
            }
        }

        return null;
    }

    /// <summary>
    /// Convert a DDS texture to PNG using TexConv, with optional normal map import settings.
    /// </summary>
    private static void DDSToPNG(string ddsPath, string outputPath, bool isNormalMap = false)
    {
        string texConvPath = MBPathHelpers.TexConvPath();

        if (!File.Exists(texConvPath))
        {
            Debug.LogError($"TexConv.exe not found at {texConvPath}");
            return;
        }

        if (!File.Exists(ddsPath))
        {
            Debug.LogError($"DDS file not found: {ddsPath}");
            return;
        }

        string absDdsPath = Path.GetFullPath(ddsPath);
        string absOutputPath = Path.GetFullPath(outputPath);
        string outputDir = Path.GetDirectoryName(absOutputPath);

        Directory.CreateDirectory(outputDir);

        // Delete existing file
        if (File.Exists(absOutputPath))
        {
            try
            {
                File.SetAttributes(absOutputPath, FileAttributes.Normal);
                File.Delete(absOutputPath);
            }
            catch (Exception ex)
            {
                Debug.LogError($"Failed to delete existing PNG: {absOutputPath}. Error: {ex.Message}");
                return;
            }
        }

        var psi = new ProcessStartInfo
        {
            FileName = texConvPath,
            Arguments = $"\"{absDdsPath}\" -ft png -srgb -y -o \"{outputDir}\" -nologo",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = new Process { StartInfo = psi };
        process.Start();
        string output = process.StandardOutput.ReadToEnd();
        string error = process.StandardError.ReadToEnd();
        process.WaitForExit();

        if (!string.IsNullOrEmpty(error))
            Debug.LogError($"TexConv Error: {error}");

        if (process.ExitCode != 0)
        {
            Debug.LogError($"TexConv failed with exit code {process.ExitCode}. Error: {error}");
            return;
        }

        // TexConv outputs with the original filename - rename to our target name
        string generatedFile = Path.Combine(outputDir,
            Path.GetFileNameWithoutExtension(absDdsPath) + ".png");

        if (!File.Exists(generatedFile))
        {
            Debug.LogError("TexConv did not generate expected PNG file.");
            return;
        }

        if (generatedFile != absOutputPath)
            File.Move(generatedFile, absOutputPath);

        AssetDatabase.ImportAsset(outputPath);

        if (isNormalMap)
        {
            var importer = AssetImporter.GetAtPath(outputPath) as TextureImporter;
            if (importer != null)
            {
                importer.textureType = TextureImporterType.NormalMap;
                importer.SaveAndReimport();
            }
        }
    }

    /// <summary>
    /// Get the palette key string by layer index (e.g., 0 → "0_gray_stone").
    /// </summary>
    private static string GetKeyByIndex(int index)
    {
        string prefix = index + "_";
        foreach (var key in PaletteMaterials.Keys)
        {
            if (key.StartsWith(prefix))
                return key;
        }
        return null;
    }
}
