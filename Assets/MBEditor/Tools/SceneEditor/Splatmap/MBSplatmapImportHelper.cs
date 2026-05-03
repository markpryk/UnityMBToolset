using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

/// <summary>
/// MBSplatmapImportHelper v5 - Rewritten from actual M&B Warband engine source.
///
/// ENGINE SOURCE ANALYSIS (mbTerrainGenerator.cpp)
///
/// THE 15 INTERNAL LAYERS:
///
///   Layers 0-3:  Core generated layers (always present)
///     [0] tlt_rock     - initialized to 1.0 at every vertex (base fill)
///     [1] tlt_earth    - slope-based: 1.0 - clamp(((1 - normal.z) - barrenness) * 12, 0, 1)
///     [2] tlt_green    - earth * perlin_noise (if greenGroundSpec >= 0, else disabled)
///     [3] tlt_riverbed - 1.0 where vertex.z less than 0 (if river enabled)
///
///   Layers 4-14: Paint layers from SCO ground paint (offset by MB_NUM_CORE_TERRAIN_LAYERS)
///     Paint layer N maps to internal layer [N + 4]
///     Each paint layer maps to a ground spec via m_groundSpecNo
///     Paint layers are written DIRECTLY as independent intensities:
///       m_vertices[x][y].m_layerIntensities[layerNo + 4] = cellValue;
///
/// KEY RELATIONSHIPS BETWEEN CORE LAYERS:
///   - tlt_green is derived FROM tlt_earth (green = earth * noise)
///   - When green saturates (greater than 0.99), earth is set to 0
///   - Snow/Desert: greenGroundSpec = -1, so tlt_green is never computed
///
/// PAINTER'S ALGORITHM (computeFaceLayerIntensities, line 1390):
///   Processes TOP-DOWN (layer 14 to 0):
///     float intensity = 1.0;
///     for (j = 14; j >= 0; j--) {
///         float val = vertex.intensities[j] * intensity;
///         face.intensities[j] += val * 0.25;
///         intensity -= val;  // consume from remaining budget
///     }
///   Layer 0 (rock) gets whatever intensity remains - it's the implicit background.
///
/// GROUND PAINT APPLICATION (applyGroundPaintLayer, line 3114):
///   Paint layers are ADDITIVE independent intensities, NOT subtractive.
///   Each paint layer writes its own value independently:
///     m_vertices[x][y].m_layerIntensities[layerNo + 4] = value;
///   The painter's algorithm handles occlusion - no manual subtraction needed.
///
/// REGION-SPECIFIC LAYER MAPPING (generateLayers, line 410):
///   Region       | tlt_rock      | tlt_earth    | tlt_green    | barrenness
///   -------------|---------------|--------------|--------------|----------
///   Plain        | gray/brown    | earth        | turf         | 0.19
///   Steppe       | gray/brown    | earth        | steppe       | 0.19
///   Snow         | gray/brown    | snow         | DISABLED(-1) | 0.19
///   Desert       | gray/brown    | desert       | DISABLED(-1) | 0.26
///   Forest       | gray/brown    | forest       | turf         | 0.19
///   SteppeForest | gray/brown    | forest       | steppe       | 0.19
///   SnowForest   | gray/brown    | snow         | DISABLED(-1) | 0.19
///   DesertForest | gray/brown    | desert       | DISABLED(-1) | 0.26
/// </summary>
public static class MBSplatmapImportHelper
{
    // GROUND SPEC INDICES (hardcoded, match engine exactly)
    public const int GROUND_GRAY_STONE  = 0;
    public const int GROUND_BROWN_STONE = 1;
    public const int GROUND_TURF        = 2;
    public const int GROUND_STEPPE      = 3;
    public const int GROUND_SNOW        = 4;
    public const int GROUND_EARTH       = 5;
    public const int GROUND_DESERT      = 6;
    public const int GROUND_FOREST      = 7;
    public const int GROUND_PEBBLES     = 8;
    public const int GROUND_VILLAGE     = 9;
    public const int GROUND_PATH        = 10;
    public const int NUM_GROUND_SPECS   = 11;

    public static readonly string[] GroundSpecNames = new string[]
    {
        "gray_stone", "brown_stone", "turf", "steppe", "snow",
        "earth", "desert", "forest", "pebbles", "village", "path"
    };

    // Internal layer type indices
    public const int TLT_ROCK     = 0;
    public const int TLT_EARTH    = 1;
    public const int TLT_GREEN    = 2;
    public const int TLT_RIVERBED = 3;
    public const int NUM_CORE_LAYERS = 4;

    /// <summary>
    /// Region types from engine (TerrainGenerator.h)
    /// </summary>
    public enum MBRegionType
    {
        Ocean          = 0,
        Mountain       = 1,
        Steppe         = 2,
        Plain          = 3,
        Snow           = 4,
        Desert         = 5,
        Bridge         = 7,
        River          = 8,
        MountainForest = 9,
        SteppeForest   = 10,
        Forest         = 11,
        SnowForest     = 12,
        DesertForest   = 13,
        DeepWater      = 15
    }

    /// <summary>
    /// Container for loaded assets.
    /// Generator text files contain the 4 core layer intensity maps.
    /// PGM files contain the 11 paint layer intensity maps from the SCO.
    /// </summary>
    public class AssetBundle
    {
        // Core generated layers (exported from C++ terrain generator)
        public TextAsset TltRock;     // Always 1.0 everywhere
        public TextAsset TltEarth;    // Slope-based intensity
        public TextAsset TltGreen;    // Earth * perlin noise
        public TextAsset TltRiverbed; // 1.0 below water

        // Paint PGMs (from SCO ground paint, one per ground spec)
        public DefaultAsset[] PaintPGMs = new DefaultAsset[NUM_GROUND_SPECS];

        public DefaultAsset GetPGM(int groundSpecIndex)
        {
            if (groundSpecIndex < 0 || groundSpecIndex >= NUM_GROUND_SPECS) return null;
            return PaintPGMs[groundSpecIndex];
        }
    }

    // ASSET LOADING

    public static AssetBundle LoadAssets(string generatorPath, string scoPath)
    {
        var bundle = new AssetBundle();
        string genPath = ToUnityAssetPath(generatorPath);
        string scoAssetPath = ToUnityAssetPath(scoPath);

        Debug.Log($"[MBSplatmapImportHelper] Loading assets:\n" +
                  $"  Generator: {genPath}\n  SCO: {scoAssetPath}");

        // Core generator layers
        bundle.TltRock     = LoadAsset<TextAsset>(genPath, "tlt_rock.txt");
        bundle.TltEarth    = LoadAsset<TextAsset>(genPath, "tlt_earth.txt");
        bundle.TltGreen    = LoadAsset<TextAsset>(genPath, "tlt_green.txt");
        bundle.TltRiverbed = LoadAsset<TextAsset>(genPath, "tlt_riverbed.txt");

        // Paint PGMs
        string[] pgmNames = {
            "layer_gray_stone.pgm", "layer_brown_stone.pgm", "layer_turf.pgm",
            "layer_steppe.pgm", "layer_snow.pgm", "layer_earth.pgm",
            "layer_desert.pgm", "layer_forest.pgm", "layer_pebbles.pgm",
            "layer_village.pgm", "layer_path.pgm"
        };

        int pgmCount = 0;
        for (int i = 0; i < NUM_GROUND_SPECS; i++)
        {
            bundle.PaintPGMs[i] = LoadAsset<DefaultAsset>(scoAssetPath, pgmNames[i]);
            if (bundle.PaintPGMs[i] != null) pgmCount++;
        }

        Debug.Log($"[MBSplatmapImportHelper] Loaded: " +
                  $"rock={bundle.TltRock != null} earth={bundle.TltEarth != null} " +
                  $"green={bundle.TltGreen != null} river={bundle.TltRiverbed != null} " +
                  $"PGMs={pgmCount}/{NUM_GROUND_SPECS}");

        return bundle;
    }

    // MAIN SETUP - builds decorator layers matching engine architecture

    /// <summary>
    /// Configure the decorator to match M&B's terrain layer system.
    ///
    /// Architecture:
    ///   BASE BLOCK  = 4 core generated layers (rock, earth, green, riverbed)
    ///                 Each with independent intensity. Painter's algorithm in
    ///                 normalization handles occlusion between them.
    ///
    ///   OVERLAY BLOCK = 11 paint layers (one per ground spec)
    ///                   Each with independent intensity from PGM files.
    ///                   Composited on top of base using PainterOcclusion.
    ///
    /// NO subtraction rules anywhere - the painter's algorithm handles everything.
    /// </summary>
    public static void SetupDecorator(
        MBTerrainDecorator decorator,
        string generatorPath,
        string scoPath,
        MBRegionType regionType,
        bool useGrayStoneForRock = true)
    {
        var assets = LoadAssets(generatorPath, scoPath);

        // Get region-specific mapping
        var mapping = GetRegionMapping(regionType);

        // Apply using the unified setup
        BuildDecoratorLayers(decorator, assets, mapping, useGrayStoneForRock);

        EditorUtility.SetDirty(decorator);
        Debug.Log($"[MBSplatmapImportHelper] Setup complete for {regionType}: {GetLayerMappingSummary(regionType)}");
    }

    // REGION MAPPING - maps core layer types to ground specs per region

    /// <summary>
    /// Describes how the 4 core layers map to ground specs for a given region.
    /// Directly from generateLayers() in the engine source.
    /// </summary>
    public struct RegionMapping
    {
        /// <summary>Ground spec for tlt_earth. Always valid.</summary>
        public int EarthGroundSpec;

        /// <summary>Ground spec for tlt_green. -1 means disabled (snow/desert).</summary>
        public int GreenGroundSpec;

        /// <summary>Barrenness factor - higher = more rock exposed on slopes.</summary>
        public float Barrenness;
    }

    /// <summary>
    /// Get the region-specific layer mapping, exactly matching generateLayers().
    /// </summary>
    public static RegionMapping GetRegionMapping(MBRegionType region)
    {
        switch (region)
        {
            case MBRegionType.Plain:
            case MBRegionType.Ocean:
            case MBRegionType.Bridge:
            case MBRegionType.Mountain:
            case MBRegionType.MountainForest:
                return new RegionMapping
                {
                    EarthGroundSpec = GROUND_EARTH,
                    GreenGroundSpec = GROUND_TURF,
                    Barrenness = 0.19f
                };

            case MBRegionType.Steppe:
                return new RegionMapping
                {
                    EarthGroundSpec = GROUND_EARTH,
                    GreenGroundSpec = GROUND_STEPPE,
                    Barrenness = 0.19f
                };

            case MBRegionType.Snow:
            case MBRegionType.SnowForest:
                return new RegionMapping
                {
                    EarthGroundSpec = GROUND_SNOW,
                    GreenGroundSpec = -1, // DISABLED
                    Barrenness = 0.19f
                };

            case MBRegionType.Desert:
            case MBRegionType.DesertForest:
                return new RegionMapping
                {
                    EarthGroundSpec = GROUND_DESERT,
                    GreenGroundSpec = -1, // DISABLED
                    Barrenness = 0.26f
                };

            case MBRegionType.Forest:
                return new RegionMapping
                {
                    EarthGroundSpec = GROUND_FOREST,
                    GreenGroundSpec = GROUND_TURF,
                    Barrenness = 0.19f
                };

            case MBRegionType.SteppeForest:
                return new RegionMapping
                {
                    EarthGroundSpec = GROUND_FOREST,
                    GreenGroundSpec = GROUND_STEPPE,
                    Barrenness = 0.19f
                };

            default:
                return new RegionMapping
                {
                    EarthGroundSpec = GROUND_EARTH,
                    GreenGroundSpec = GROUND_TURF,
                    Barrenness = 0.19f
                };
        }
    }

    // DECORATOR CONSTRUCTION - unified for all regions

    /// <summary>
    /// Build all decorator layers matching the engine's 15-layer architecture.
    ///
    /// Engine pipeline (generate() in mbTerrainGenerator.cpp):
    ///   1. generateLayers()                    - assign ground specs to core layers
    ///   2. computeVertexLayerIntensities()     - fill layers 0-3 with procedural values
    ///   3. applyGroundPaintLayer() for each    - fill layers 4-14 with paint values
    ///   4. computeFaceLayerIntensities()       - painter's algorithm top-down
    ///
    /// Our decorator mirrors this:
    ///   BASE BLOCK   = core layers 0-3 (from generator text files)
    ///   OVERLAY BLOCK = paint layers 4-14 (from PGM files)
    ///   Normalization = MBNormalized (painter's algorithm + normalize)
    /// </summary>
    private static void BuildDecoratorLayers(
        MBTerrainDecorator decorator,
        AssetBundle assets,
        RegionMapping mapping,
        bool useGrayStone)
    {
        decorator.layers.Clear();
        decorator.normalizationMode = MBTerrainDecorator.NormalizationMode.MBNormalized;
        decorator.visibilityThreshold = 0.01f;

        int rockSpec = useGrayStone ? GROUND_GRAY_STONE : GROUND_BROWN_STONE;

        // BASE BLOCK - Core generated layers (internal layers 0-3)
        //
        // These have INDEPENDENT intensities. The painter's algorithm in
        // MBNormalized normalization handles the occlusion between them.
        // No subtraction rules needed.

        // [0] tlt_rock - always 1.0 everywhere (base fill background)
        // Ground spec = gray_stone or brown_stone (50/50 from seed)
        AddBaseLayer(decorator, GroundSpecNames[rockSpec], rockSpec, assets.TltRock);

        // [1] tlt_earth - slope-based intensity
        // Ground spec varies by region (earth, snow, desert, forest)
        AddBaseLayer(decorator, GroundSpecNames[mapping.EarthGroundSpec],
            mapping.EarthGroundSpec, assets.TltEarth);

        // [2] tlt_green - earth * perlin noise (disabled in snow/desert)
        // Ground spec varies by region (turf, steppe, or -1 for disabled)
        if (mapping.GreenGroundSpec >= 0)
        {
            AddBaseLayer(decorator, GroundSpecNames[mapping.GreenGroundSpec],
                mapping.GreenGroundSpec, assets.TltGreen);
        }

        // [3] tlt_riverbed - 1.0 where below water
        AddBaseLayer(decorator, "pebbles", GROUND_PEBBLES, assets.TltRiverbed);

        // OVERLAY BLOCK - Paint layers (internal layers 4-14)
        //
        // Each paint layer is an INDEPENDENT intensity from PGM data.
        // Composited onto base using PainterOcclusion blend mode.
        //
        // ORDER MATTERS: in the painter's algorithm, higher layers in the
        // list occlude lower ones. We add them in ground spec order (0-10)
        // which matches the engine's paint layer ordering.
        //
        // Engine source (line 3114):
        //   m_vertices[x][y].m_layerIntensities[layerNo + 4] = value;
        //   m_layers[layerNo + 4].setGroundSpec(layer.m_groundSpecNo);

        for (int i = 0; i < NUM_GROUND_SPECS; i++)
        {
            DefaultAsset pgm = assets.GetPGM(i);
            if (pgm == null) continue;

            AddOverlayLayer(decorator, GroundSpecNames[i], i, pgm);
        }
    }

    /// <summary>
    /// Add a BASE block layer with a single generator rule.
    /// No subtraction, no PGM - just the raw generated intensity.
    /// </summary>
    private static void AddBaseLayer(
        MBTerrainDecorator decorator,
        string name,
        int groundSpecIndex,
        TextAsset generatorAsset)
    {
        var layer = new MBTerrainDecorator.Layers
        {
            name = name,
            active = generatorAsset != null,
            layerIndex = groundSpecIndex,
            layerType = MBTerrainDecorator.LayerType.texture,
            block = MBTerrainDecorator.LayerBlock.Base,
            rules = new List<MBTerrainDecorator.Rules>()
        };

        layer.rules.Add(new MBTerrainDecorator.Rules
        {
            active = generatorAsset != null,
            filter = MBTerrainDecorator.FilterType.generator,
            blend = MBTerrainDecorator.BlendType.add,
            generatorAsset = generatorAsset,
            intensity = 1f,
            contrast = 0f
        });

        decorator.layers.Add(layer);
    }

    /// <summary>
    /// Add an OVERLAY block layer with a single PGM rule.
    /// Independent intensity - painter's algorithm handles occlusion.
    /// </summary>
    private static void AddOverlayLayer(
        MBTerrainDecorator decorator,
        string name,
        int groundSpecIndex,
        DefaultAsset pgmAsset)
    {
        var layer = new MBTerrainDecorator.Layers
        {
            name = name,
            active = true,
            layerIndex = groundSpecIndex,
            layerType = MBTerrainDecorator.LayerType.texture,
            block = MBTerrainDecorator.LayerBlock.Overlay,
            overlayBlendMode = MBTerrainDecorator.OverlayBlendMode.PainterOcclusion,
            overlayOpacity = 1f,
            rules = new List<MBTerrainDecorator.Rules>()
        };

        layer.rules.Add(new MBTerrainDecorator.Rules
        {
            active = true,
            filter = MBTerrainDecorator.FilterType.pgm,
            blend = MBTerrainDecorator.BlendType.add,
            pgmAsset = pgmAsset,
            intensity = 1f,
            contrast = 0f
        });

        decorator.layers.Add(layer);
    }

    // UTILITY METHODS

    /// <summary>
    /// Get region type from terrain code key4 bits.
    /// </summary>
    public static MBRegionType GetRegionTypeFromCode(int terrainTypeCode)
    {
        switch (terrainTypeCode)
        {
            case 0:  return MBRegionType.Ocean;
            case 1:  return MBRegionType.Mountain;
            case 2:  return MBRegionType.Steppe;
            case 3:  return MBRegionType.Plain;
            case 4:  return MBRegionType.Snow;
            case 5:  return MBRegionType.Desert;
            case 7:  return MBRegionType.Bridge;
            case 8:  return MBRegionType.River;
            case 9:  return MBRegionType.MountainForest;
            case 10: return MBRegionType.SteppeForest;
            case 11: return MBRegionType.Forest;
            case 12: return MBRegionType.SnowForest;
            case 13: return MBRegionType.DesertForest;
            case 15: return MBRegionType.DeepWater;
            default: return MBRegionType.Plain;
        }
    }

    /// <summary>
    /// Determine rock type from seed[0], matching generateLayers() line 417.
    ///
    /// Engine: int random = rglRand(2);
    ///         if (random == 1) then brown_stone
    ///         if (random == 0) then gray_stone
    ///
    /// rglRand uses MSVC rand(): seed = 214013 * seed + 2531011, result = (seed >> 16) and 0x7FFF
    /// </summary>
    public static bool ShouldUseGrayStone(int seed0)
    {
        uint seed = (uint)seed0;
        seed = 214013 * seed + 2531011;
        int random = (int)((seed >> 16) & 0x7FFF);
        return (random % 2) == 0; // 0 = gray_stone, 1 = brown_stone
    }

    /// <summary>
    /// Summary string for a region's layer mapping.
    /// </summary>
    public static string GetLayerMappingSummary(MBRegionType region)
    {
        var m = GetRegionMapping(region);
        string earthName = m.EarthGroundSpec >= 0 ? GroundSpecNames[m.EarthGroundSpec] : "NONE";
        string greenName = m.GreenGroundSpec >= 0 ? GroundSpecNames[m.GreenGroundSpec] : "DISABLED";
        return $"rock->rock, earth->{earthName}, green->{greenName}, barrenness={m.Barrenness}";
    }

    /// <summary>
    /// Refresh assets at given paths.
    /// </summary>
    public static void RefreshAssets(string generatorPath, string scoPath)
    {
        string genPath = ToUnityAssetPath(generatorPath);
        string scoAssetPath = ToUnityAssetPath(scoPath);

        if (!string.IsNullOrEmpty(genPath) && genPath.StartsWith("Assets"))
            AssetDatabase.ImportAsset(genPath, ImportAssetOptions.ImportRecursive);

        if (!string.IsNullOrEmpty(scoAssetPath) && scoAssetPath.StartsWith("Assets"))
            AssetDatabase.ImportAsset(scoAssetPath, ImportAssetOptions.ImportRecursive);

        AssetDatabase.Refresh();
    }

    /// <summary>
    /// Debug: print decorator layer setup.
    /// </summary>
    public static void DebugPrintSetup(MBTerrainDecorator decorator)
    {
        Debug.Log("=== MBSplatmapImportHelper Setup ===");
        Debug.Log($"Normalization: {decorator.normalizationMode}, Threshold: {decorator.visibilityThreshold}");

        foreach (var layer in decorator.layers)
        {
            string block = layer.block == MBTerrainDecorator.LayerBlock.Base ? "BASE" : "OVERLAY";
            Debug.Log($"  [{layer.layerIndex}] {layer.name} ({block}, active:{layer.active})");

            foreach (var rule in layer.rules)
            {
                string asset = "null";
                if (rule.filter == MBTerrainDecorator.FilterType.generator && rule.generatorAsset != null)
                    asset = rule.generatorAsset.name;
                else if (rule.filter == MBTerrainDecorator.FilterType.pgm && rule.pgmAsset != null)
                    asset = rule.pgmAsset.name;

                Debug.Log($"    {(rule.active ? "+" : "-")} {rule.blend} {rule.filter}: {asset}");
            }
        }
    }

    // PRIVATE HELPERS - path conversion and asset loading

    private static string ToUnityAssetPath(string path)
    {
        if (string.IsNullOrEmpty(path)) return path;
        if (path.StartsWith("Assets/") || path.StartsWith("Assets\\"))
            return path.Replace("\\", "/");

        string dataPath = Application.dataPath;
        string projectPath = System.IO.Path.GetDirectoryName(dataPath);
        string normalizedPath = System.IO.Path.GetFullPath(path).Replace("\\", "/");
        string normalizedDataPath = dataPath.Replace("\\", "/");
        string normalizedProjectPath = projectPath.Replace("\\", "/");

        if (normalizedPath.StartsWith(normalizedDataPath))
            return "Assets" + normalizedPath.Substring(normalizedDataPath.Length);

        if (normalizedPath.StartsWith(normalizedProjectPath))
            return normalizedPath.Substring(normalizedProjectPath.Length + 1);

        Debug.LogWarning($"[MBSplatmapImportHelper] Path outside project: {path}");
        return path;
    }

    private static T LoadAsset<T>(string folderPath, string fileName) where T : UnityEngine.Object
    {
        if (string.IsNullOrEmpty(folderPath)) return null;

        string fullPath = folderPath + "/" + fileName;
        string absolutePath = fullPath;

        if (fullPath.StartsWith("Assets/"))
        {
            absolutePath = System.IO.Path.Combine(
                System.IO.Path.GetDirectoryName(Application.dataPath), fullPath);
        }

        if (!System.IO.File.Exists(absolutePath)) return null;

        T asset = AssetDatabase.LoadAssetAtPath<T>(fullPath);
        if (asset == null)
        {
            AssetDatabase.ImportAsset(fullPath, ImportAssetOptions.ForceSynchronousImport);
            asset = AssetDatabase.LoadAssetAtPath<T>(fullPath);
        }

        return asset;
    }
}
