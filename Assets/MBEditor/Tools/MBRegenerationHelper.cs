using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// MBRegenerationHelper - Manages terrain regeneration with Base/Overlay block support.
/// 
/// This helper provides methods to:
/// 1. Enable/disable non-generator layers in LayeredHeightmapGenerator
/// 2. Configure MBTerrainDecorator Base block layers (Overlay layers are NEVER touched)
/// 3. Rebuild Base layers from hash while preserving user Overlay layers
/// 4. Validate terrain requirements before generation
/// 
/// CRITICAL: Overlay layers persist across all regeneration operations.
/// Only Base block layers are rebuilt/configured by the regenerator.
/// </summary>
public static class MBRegenerationHelper
{
    #region Regeneration Modes
    
    /// <summary>
    /// Defines what gets processed during regeneration
    /// </summary>
    [Flags]
    public enum RegenerationMode
    {
        None = 0,
        
        /// <summary>Process only Generator layer (hash-based heightmap)</summary>
        GeneratorOnly = 1 << 0,
        
        /// <summary>Process Generator + PFM layers (heightmap + elevation delta)</summary>
        GeneratorAndPFM = 1 << 1,
        
        /// <summary>Process all heightmap layers including noise, height textures</summary>
        AllHeightmapLayers = 1 << 2,
        
        /// <summary>Include painted layer edits</summary>
        IncludePainted = 1 << 3,
        
        /// <summary>Use only tlt_ generator rules in decorator Base block (no PGM paint)</summary>
        DecoratorGeneratorOnly = 1 << 4,
        
        /// <summary>Use all decorator Base block rules including PGM paint layers</summary>
        DecoratorFull = 1 << 5,
        
        // Preset combinations
        
        /// <summary>Fresh generation: Generator+PFM heightmap, generator-only decorator</summary>
        FreshGeneration = GeneratorAndPFM | DecoratorGeneratorOnly,
        
        /// <summary>Full regeneration with all layers and paint</summary>
        FullRegeneration = AllHeightmapLayers | IncludePainted | DecoratorFull
    }
    
    #endregion
    
    #region Layer State Management
    
    /// <summary>
    /// Stores the previous active state of layers for restoration
    /// </summary>
    [Serializable]
    public class LayerStateSnapshot
    {
        public List<bool> heightmapLayerStates = new List<bool>();
        public List<List<bool>> decoratorRuleStates = new List<List<bool>>();
        public DateTime timestamp;
        
        public bool IsValid => heightmapLayerStates.Count > 0 || decoratorRuleStates.Count > 0;
    }
    
    private static LayerStateSnapshot _lastSnapshot;
    
    /// <summary>
    /// Gets the last saved layer state snapshot
    /// </summary>
    public static LayerStateSnapshot LastSnapshot => _lastSnapshot;
    
    #endregion
    
    #region Heightmap Generator Configuration
    
    /// <summary>
    /// Configures LayeredHeightmapGenerator layers based on regeneration mode.
    /// Returns a snapshot of previous states for restoration.
    /// </summary>
    public static LayerStateSnapshot ConfigureHeightmapLayers(
        LayeredHeightmapGenerator generator, 
        RegenerationMode mode)
    {
        if (generator == null)
        {
            Debug.LogError("[MBRegenerationHelper] Generator is null");
            return null;
        }
        
        var snapshot = new LayerStateSnapshot { timestamp = DateTime.Now };
        
        // Save current states
        foreach (var layer in generator.layers)
        {
            snapshot.heightmapLayerStates.Add(layer.active);
        }
        
        bool enableGenerator = mode.HasFlag(RegenerationMode.GeneratorOnly) || 
                               mode.HasFlag(RegenerationMode.GeneratorAndPFM) ||
                               mode.HasFlag(RegenerationMode.AllHeightmapLayers);
        
        bool enablePFM = mode.HasFlag(RegenerationMode.GeneratorAndPFM) ||
                         mode.HasFlag(RegenerationMode.AllHeightmapLayers);
        
        bool enableOthers = mode.HasFlag(RegenerationMode.AllHeightmapLayers);
        bool enablePainted = mode.HasFlag(RegenerationMode.IncludePainted);
        
        int modifiedCount = 0;
        
        foreach (var layer in generator.layers)
        {
            bool shouldBeActive;
            
            switch (layer.filterType)
            {
                case LayeredHeightmapGenerator.FilterType.Generator:
                    shouldBeActive = enableGenerator;
                    break;
                    
                case LayeredHeightmapGenerator.FilterType.PFM:
                    shouldBeActive = enablePFM;
                    break;
                    
                case LayeredHeightmapGenerator.FilterType.Painted:
                    shouldBeActive = enablePainted;
                    break;
                    
                case LayeredHeightmapGenerator.FilterType.Height:
                case LayeredHeightmapGenerator.FilterType.Noise:
                default:
                    shouldBeActive = enableOthers;
                    break;
            }
            
            if (layer.active != shouldBeActive)
            {
                layer.active = shouldBeActive;
                modifiedCount++;
            }
        }
        
        Debug.Log($"[MBRegenerationHelper] Configured {generator.layers.Count} heightmap layers, " +
                  $"modified {modifiedCount} (Mode: {mode})");
        
        _lastSnapshot = snapshot;
        return snapshot;
    }
    
    /// <summary>
    /// Restores heightmap layer states from a snapshot
    /// </summary>
    public static void RestoreHeightmapLayers(LayeredHeightmapGenerator generator, LayerStateSnapshot snapshot)
    {
        if (generator == null || snapshot == null || !snapshot.IsValid)
            return;
        
        int count = Mathf.Min(generator.layers.Count, snapshot.heightmapLayerStates.Count);
        
        for (int i = 0; i < count; i++)
        {
            generator.layers[i].active = snapshot.heightmapLayerStates[i];
        }
        
        Debug.Log($"[MBRegenerationHelper] Restored {count} heightmap layer states");
    }
    
    /// <summary>
    /// Sets only Generator and PFM layers active (for fresh generation)
    /// </summary>
    public static LayerStateSnapshot SetGeneratorOnlyMode(LayeredHeightmapGenerator generator)
    {
        return ConfigureHeightmapLayers(generator, RegenerationMode.GeneratorAndPFM);
    }
    
    /// <summary>
    /// Enables all heightmap layers
    /// </summary>
    public static void EnableAllHeightmapLayers(LayeredHeightmapGenerator generator)
    {
        if (generator == null) return;
        
        foreach (var layer in generator.layers)
        {
            layer.active = true;
        }
    }
    
    #endregion
    
    #region Decorator Configuration
    
    /// <summary>
    /// Configures MBTerrainDecorator rules based on regeneration mode.
    /// ONLY affects Base block layers - Overlay layers are NEVER touched.
    /// </summary>
    public static void ConfigureDecoratorRules(MBTerrainDecorator decoratorModular, RegenerationMode mode)
    {
        if (decoratorModular == null)
        {
            Debug.LogError("[MBRegenerationHelper] Decorator is null");
            return;
        }
        
        bool generatorOnly = mode.HasFlag(RegenerationMode.DecoratorGeneratorOnly);
        bool fullMode = mode.HasFlag(RegenerationMode.DecoratorFull);
        
        if (!generatorOnly && !fullMode)
            return; // No decorator mode specified
        
        int generatorRulesEnabled = 0;
        int generatorRulesDisabledNoAsset = 0;
        int pgmRulesDisabled = 0;
        int totalBaseRules = 0;
        int overlayLayersSkipped = 0;
        
        foreach (var layer in decoratorModular.layers)
        {
            // CRITICAL: Never touch overlay layers during regeneration
            if (layer.block == MBTerrainDecorator.LayerBlock.Overlay)
            {
                overlayLayersSkipped++;
                continue;
            }
            
            foreach (var rule in layer.rules)
            {
                totalBaseRules++;
                
                if (generatorOnly)
                {
                    // Enable only generator rules, disable PGM and other rules
                    if (rule.filter == MBTerrainDecorator.FilterType.generator)
                    {
                        // SAFETY: Only enable if asset is assigned to prevent null path errors
                        if (rule.generatorAsset != null)
                        {
                            rule.active = true;
                            generatorRulesEnabled++;
                        }
                        else
                        {
                            rule.active = false;
                            generatorRulesDisabledNoAsset++;
                        }
                    }
                    else if (rule.filter == MBTerrainDecorator.FilterType.pgm)
                    {
                        rule.active = false;
                        pgmRulesDisabled++;
                    }
                    // Height and slope rules remain as-is (they're based on terrain, not paint)
                }
                else if (fullMode)
                {
                    // Enable all rules (but still check for null assets on generator rules)
                    if (rule.filter == MBTerrainDecorator.FilterType.generator)
                    {
                        rule.active = rule.generatorAsset != null;
                        if (rule.active) generatorRulesEnabled++;
                        else generatorRulesDisabledNoAsset++;
                    }
                    else
                    {
                        rule.active = true;
                    }
                }
            }
        }
        
        if (generatorOnly)
        {
            Debug.Log($"[MBRegenerationHelper] Decorator generator-only mode (Base block): " +
                      $"{generatorRulesEnabled} generator rules enabled, " +
                      $"{generatorRulesDisabledNoAsset} disabled (no asset), " +
                      $"{pgmRulesDisabled} PGM rules disabled. " +
                      $"{overlayLayersSkipped} overlay layers preserved.");
        }
        else
        {
            Debug.Log($"[MBRegenerationHelper] Decorator full mode (Base block): {totalBaseRules} rules, " +
                      $"{generatorRulesEnabled} generator enabled, " +
                      $"{generatorRulesDisabledNoAsset} generator disabled (no asset). " +
                      $"{overlayLayersSkipped} overlay layers preserved.");
        }
    }
    
    /// <summary>
    /// Enables only generator (tlt_) rules in the decorator Base block, disables all PGM paint rules.
    /// Overlay layers are never touched.
    /// </summary>
    public static void SetDecoratorGeneratorOnlyMode(MBTerrainDecorator decoratorModular)
    {
        ConfigureDecoratorRules(decoratorModular, RegenerationMode.DecoratorGeneratorOnly);
    }
    
    /// <summary>
    /// Enables all decorator Base block rules including PGM paint.
    /// Overlay layers are never touched.
    /// </summary>
    public static void SetDecoratorFullMode(MBTerrainDecorator decoratorModular)
    {
        ConfigureDecoratorRules(decoratorModular, RegenerationMode.DecoratorFull);
    }
    
    /// <summary>
    /// Sets up the decorator Base and Overlay blocks from terrain hash.
    /// 
    /// MBSplatmapImportHelper.SetupDecorator() now auto-assigns blocks in AddLayer():
    /// - Layers with active generator+asset → Base
    /// - All other layers → Overlay
    /// 
    /// This method just handles preserving user overlays across regeneration.
    /// </summary>
    public static void SetupDecoratorFromHash(
        MBTerrainDecorator decoratorModular,
        string hash,
        string generatorPath,
        string scoPath,
        bool generatorRulesOnly = true)
    {
        if (decoratorModular == null)
        {
            Debug.LogError("[MBRegenerationHelper] Decorator is null");
            return;
        }
        
        if (string.IsNullOrEmpty(hash))
        {
            Debug.LogError("[MBRegenerationHelper] Hash is null or empty");
            return;
        }
        
        if (string.IsNullOrEmpty(generatorPath))
        {
            Debug.LogError("[MBRegenerationHelper] Generator path is null or empty");
            return;
        }
        
        // Parse hash to get terrain type
        var terrainData = MBTerrainRegenerator.ParseHash(hash);
        if (terrainData == null)
        {
            Debug.LogError($"[MBRegenerationHelper] Failed to parse hash: {hash}");
            return;
        }
        
        var regionType = MBSplatmapImportHelper.GetRegionTypeFromCode(terrainData.TerrainType);
        bool useGrayStone = MBSplatmapImportHelper.ShouldUseGrayStone((int)terrainData.TerrainSeed);
        
        // Save user-created overlay layers (manually added by user)
        var userOverlays = new List<MBTerrainDecorator.Layers>();
        foreach (var layer in decoratorModular.layers)
        {
            if (layer.block == MBTerrainDecorator.LayerBlock.Overlay && layer.isUserCreated)
                userOverlays.Add(layer);
        }
        
        Debug.Log($"[MBRegenerationHelper] Rebuilding decorator for {regionType}, " +
                  $"rock: {(useGrayStone ? "gray_stone" : "brown_stone")}, " +
                  $"preserving {userOverlays.Count} user overlay layers");
        
        // SetupDecorator clears all layers and rebuilds with proper block assignments
        // (AddLayer auto-assigns Base/Overlay based on active generator rule)
        MBSplatmapImportHelper.SetupDecorator(
            decoratorModular,
            generatorPath,
            scoPath ?? "",
            regionType,
            useGrayStone
        );
        
        // Restore user-created overlay layers at the end
        foreach (var layer in userOverlays)
            decoratorModular.layers.Add(layer);
        
        // If generator-only mode, disable PGM rules in Base block
        if (generatorRulesOnly)
        {
            SetDecoratorGeneratorOnlyMode(decoratorModular);
        }
        
        // Verify generator assets were loaded
        VerifyDecoratorGeneratorAssets(decoratorModular, generatorPath);
        
        int baseCount = decoratorModular.GetBaseLayers().Count;
        int overlayCount = decoratorModular.GetOverlayLayers().Count;
        
        Debug.Log($"[MBRegenerationHelper] Rebuild complete: " +
                  $"{baseCount} base + {overlayCount} overlay " +
                  $"(incl. {userOverlays.Count} user)");
        
#if UNITY_EDITOR
        EditorUtility.SetDirty(decoratorModular);
#endif
    }
    
    /// <summary>
    /// Creates an overlay copy of a layer, optionally stripping generator rules.
    /// </summary>
    private static MBTerrainDecorator.Layers CreateOverlayCopy(MBTerrainDecorator.Layers source, bool stripGenerator)
    {
        var copy = new MBTerrainDecorator.Layers
        {
            name = source.name,
            active = true,
            layerIndex = source.layerIndex,
            layerType = source.layerType,
            block = MBTerrainDecorator.LayerBlock.Overlay,
            overlayBlendMode = MBTerrainDecorator.OverlayBlendMode.PainterOcclusion,
            overlayOpacity = 1f,
            isUserCreated = false,
            probability = source.probability,
            maximumTreeCount = source.maximumTreeCount,
            width = source.width,
            height = source.height,
            randomPosition = source.randomPosition,
            randomRotation = source.randomRotation,
            randomSize = source.randomSize,
            randomHealth = source.randomHealth,
            offset = source.offset,
            rules = new List<MBTerrainDecorator.Rules>()
        };
        
        foreach (var rule in source.rules)
        {
            if (stripGenerator && rule.filter == MBTerrainDecorator.FilterType.generator)
                continue;
            copy.rules.Add(rule);
        }
        
        return copy;
    }
    
    /// <summary>
    /// Verifies that generator assets are loaded in Base block layers and logs warnings for missing ones.
    /// Only checks Base block layers.
    /// </summary>
    public static void VerifyDecoratorGeneratorAssets(MBTerrainDecorator decoratorModular, string generatorPath)
    {
        if (decoratorModular == null)
            return;
        
        int totalGeneratorRules = 0;
        int loadedAssets = 0;
        int missingAssets = 0;
        
        foreach (var layer in decoratorModular.layers)
        {
            // Only verify base layers - overlay layers have user-managed assets
            if (layer.block == MBTerrainDecorator.LayerBlock.Overlay)
                continue;
            
            foreach (var rule in layer.rules)
            {
                if (rule.filter != MBTerrainDecorator.FilterType.generator)
                    continue;
                
                if (!rule.active)
                    continue;
                
                totalGeneratorRules++;
                
                if (rule.generatorAsset != null)
                {
                    loadedAssets++;
                }
                else
                {
                    missingAssets++;
                    Debug.LogWarning($"[MBRegenerationHelper] Base layer '{layer.name}' has active generator rule but no asset assigned");
                }
            }
        }
        
        if (missingAssets > 0)
        {
            Debug.LogWarning($"[MBRegenerationHelper] {missingAssets}/{totalGeneratorRules} base generator rules are missing assets. " +
                            $"Check that generator files exist in: {generatorPath}");
        }
        else if (totalGeneratorRules > 0)
        {
            Debug.Log($"[MBRegenerationHelper] All {loadedAssets} base generator assets loaded successfully");
        }
    }
    
    #endregion
    
    #region Validation
    
    /// <summary>
    /// Validates that terrain is ready for generation from hash
    /// </summary>
    public static (bool isValid, string message) ValidateForGeneration(
        Terrain terrain,
        LayeredHeightmapGenerator generator,
        string hash)
    {
        if (terrain == null)
            return (false, "No terrain assigned");
        
        if (generator == null)
            return (false, "LayeredHeightmapGenerator not found on terrain");
        
        if (string.IsNullOrEmpty(hash))
            return (false, "No terrain hash provided");
        
        if (!MBTerrainRegenerator.ValidateHash(hash))
            return (false, $"Invalid hash format: {hash}");
        
        // Check for Generator layer
        var generatorLayer = generator.layers.Find(
            l => l.filterType == LayeredHeightmapGenerator.FilterType.Generator);
        
        if (generatorLayer == null)
            return (false, "No Generator layer found in LayeredHeightmapGenerator");
        
        // Check TerrainGenerator tool
        string toolPath = MBPathHelpers.MBTerrainGeneratorToolPath();
        if (!File.Exists(toolPath))
            return (false, $"TerrainGenerator tool not found at: {toolPath}");
        
        return (true, "Ready for generation");
    }
    
    /// <summary>
    /// Checks if generator heightmap data needs to be regenerated based on hash change
    /// </summary>
    public static bool NeedsHeightmapRegeneration(LayeredHeightmapGenerator generator, string newHash)
    {
        if (generator == null || string.IsNullOrEmpty(newHash))
            return true;
        
        string currentHash = generator.CurrentTerrainHash;
        
        // If no current hash, needs regeneration
        if (string.IsNullOrEmpty(currentHash))
            return true;
        
        // Compare hashes (case-insensitive)
        return !string.Equals(currentHash, newHash, StringComparison.OrdinalIgnoreCase);
    }
    
    #endregion
    
    #region Full Regeneration Pipeline
    
    /// <summary>
    /// Result of a regeneration operation with detailed info
    /// </summary>
    public class RegenerationResult
    {
        public bool success;
        public string message;
        public RegenerationMode modeUsed;
        public float heightmapTime;
        public float decoratorTime;
        public float totalTime;
        public LayerStateSnapshot previousState;
        public int baseLayerCount;
        public int overlayLayerCount;
        
        public static RegenerationResult Success(string msg, RegenerationMode mode)
        {
            return new RegenerationResult { success = true, message = msg, modeUsed = mode };
        }
        
        public static RegenerationResult Failure(string msg)
        {
            return new RegenerationResult { success = false, message = msg };
        }
    }
    
    /// <summary>
    /// Performs full terrain regeneration with specified mode.
    /// This is the main entry point for controlled regeneration.
    /// Only Base block layers are affected - Overlay layers persist.
    /// </summary>
    public static RegenerationResult RegenerateTerrain(
        Terrain terrain,
        RegenerationMode mode,
        string generatorDataPath = null,
        string scoDataPath = null,
        bool showProgress = true)
    {
        if (terrain == null)
            return RegenerationResult.Failure("Terrain is null");
        
        float startTime = Time.realtimeSinceStartup;
        
        var generator = terrain.GetComponent<LayeredHeightmapGenerator>();
        var decorator = terrain.GetComponent<MBTerrainDecorator>();
        
        if (generator == null)
            return RegenerationResult.Failure("LayeredHeightmapGenerator not found");
        
        string hash = generator.CurrentTerrainHash;
        
        var (isValid, validationMsg) = ValidateForGeneration(terrain, generator, hash);
        if (!isValid)
            return RegenerationResult.Failure(validationMsg);
        
#if UNITY_EDITOR
        try
        {
            var result = new RegenerationResult { modeUsed = mode };
            
            // Step 1: Configure heightmap layers
            if (showProgress)
                EditorUtility.DisplayProgressBar("Regenerating Terrain", "Configuring layers...", 0.1f);
            
            result.previousState = ConfigureHeightmapLayers(generator, mode);
            
            // Step 2: Generate heightmap
            if (showProgress)
                EditorUtility.DisplayProgressBar("Regenerating Terrain", "Generating heightmap...", 0.3f);
            
            float heightmapStart = Time.realtimeSinceStartup;
            
            Undo.RegisterCompleteObjectUndo(generator, "Regenerate Terrain");
            Undo.RegisterCompleteObjectUndo(terrain.terrainData, "Regenerate Terrain");
            Undo.RegisterCompleteObjectUndo(terrain, "Regenerate Terrain");
            
            generator.GenerateHeightmap();
            
            result.heightmapTime = Time.realtimeSinceStartup - heightmapStart;
            
            // Step 3: Rebuild Base block layers and run decorator (Overlay preserved)
            if (decorator != null && (mode.HasFlag(RegenerationMode.DecoratorGeneratorOnly) || 
                                       mode.HasFlag(RegenerationMode.DecoratorFull)))
            {
                if (showProgress)
                    EditorUtility.DisplayProgressBar("Regenerating Terrain", "Rebuilding Base layers from hash...", 0.5f);
                
                float decoratorStart = Time.realtimeSinceStartup;
                
                bool generatorOnly = mode.HasFlag(RegenerationMode.DecoratorGeneratorOnly);
                
                // REBUILD Base block layer list from hash (overlay preserved inside)
                SetupDecoratorFromHash(
                    decorator,
                    hash,
                    generatorDataPath ?? "",
                    scoDataPath ?? "",
                    generatorOnly
                );
                
                // Load any missing generator assets into Base block
                if (!string.IsNullOrEmpty(generatorDataPath))
                {
                    LoadDecoratorGeneratorAssets(decorator, generatorDataPath);
                }
                
                if (showProgress)
                    EditorUtility.DisplayProgressBar("Regenerating Terrain", "Decorating terrain...", 0.6f);
                
                // Process filters and decorate
                decorator.ProcessGeneratorFilters();
                
                if (mode.HasFlag(RegenerationMode.DecoratorFull) && !string.IsNullOrEmpty(scoDataPath))
                {
                    decorator.ProcessPGMFilters();
                    decorator.ProcessTextureFilters();
                }
                
                // DecorateNow runs two-pass: Base block → Overlay block compositing
                decorator.StartCoroutine(decorator.DecorateNow());
                
                result.decoratorTime = Time.realtimeSinceStartup - decoratorStart;
            }
            
            // Record block counts in result
            if (decorator != null)
            {
                result.baseLayerCount = decorator.GetBaseLayers().Count;
                result.overlayLayerCount = decorator.GetOverlayLayers().Count;
            }
            
            result.totalTime = Time.realtimeSinceStartup - startTime;
            result.success = true;
            result.message = $"Regeneration complete in {result.totalTime:F2}s " +
                           $"(Base: {result.baseLayerCount}L, Overlay: {result.overlayLayerCount}L preserved)";
            
            if (showProgress)
                EditorUtility.ClearProgressBar();
            
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(terrain.gameObject.scene);
            
            return result;
        }
        catch (Exception ex)
        {
            EditorUtility.ClearProgressBar();
            Debug.LogError($"[MBRegenerationHelper] Error: {ex}");
            return RegenerationResult.Failure($"Regeneration failed: {ex.Message}");
        }
#else
        return RegenerationResult.Failure("Regeneration only available in Editor");
#endif
    }
    
    /// <summary>
    /// Loads generator assets (tlt_*.txt files) into Base block decorator rules from the specified path.
    /// Only loads into Base block layers - Overlay layers are never touched.
    /// This ensures the decorator can process generator filters even if assets aren't assigned in Inspector.
    /// </summary>
    public static void LoadDecoratorGeneratorAssets(MBTerrainDecorator decoratorModular, string generatorDataPath)
    {
        if (decoratorModular == null || string.IsNullOrEmpty(generatorDataPath))
            return;
        
#if UNITY_EDITOR
        // Map of generator file names to their tlt_ identifiers
        var generatorFiles = new Dictionary<string, string>
        {
            { "tlt_rock", "tlt_rock.txt" },
            { "tlt_earth", "tlt_earth.txt" },
            { "tlt_green", "tlt_green.txt" },
            { "tlt_riverbed", "tlt_riverbed.txt" }
        };
        
        int loadedCount = 0;
        
        foreach (var layer in decoratorModular.layers)
        {
            // Only load into Base block layers - overlay assets are user-managed
            if (layer.block == MBTerrainDecorator.LayerBlock.Overlay)
                continue;
            
            foreach (var rule in layer.rules)
            {
                if (rule.filter != MBTerrainDecorator.FilterType.generator)
                    continue;
                
                // If asset is already assigned and valid, skip
                if (rule.generatorAsset != null)
                    continue;
                
                // Try to find matching generator file based on layer name
                string assetPath = null;
                
                // Check each generator file
                foreach (var kvp in generatorFiles)
                {
                    string filePath = $"{generatorDataPath}/{kvp.Value}";
                    
                    // Check if this rule should use this generator based on layer name
                    // tlt_rock -> rock layers (gray_stone, brown_stone)
                    // tlt_earth -> earth, snow, desert layers
                    // tlt_green -> turf, steppe layers
                    // tlt_riverbed -> pebbles layer
                    
                    bool shouldUse = false;
                    string layerName = layer.name.ToLower();
                    
                    switch (kvp.Key)
                    {
                        case "tlt_rock":
                            shouldUse = layerName.Contains("stone") || layerName.Contains("rock");
                            break;
                        case "tlt_earth":
                            shouldUse = layerName.Contains("earth") || layerName.Contains("snow") || 
                                       layerName.Contains("desert");
                            break;
                        case "tlt_green":
                            shouldUse = layerName.Contains("turf") || layerName.Contains("steppe") ||
                                       layerName.Contains("grass") || layerName.Contains("green");
                            break;
                        case "tlt_riverbed":
                            shouldUse = layerName.Contains("pebble") || layerName.Contains("river");
                            break;
                    }
                    
                    if (shouldUse && System.IO.File.Exists(filePath))
                    {
                        assetPath = filePath;
                        break;
                    }
                }
                
                // Load the asset if we found a matching file
                if (!string.IsNullOrEmpty(assetPath))
                {
                    var asset = AssetDatabase.LoadAssetAtPath<TextAsset>(MBPathHelpers.ConvertToUnityPath(assetPath));
                    if (asset != null)
                    {
                        rule.generatorAsset = asset;
                        loadedCount++;
                    }
                }
            }
        }
        
        if (loadedCount > 0)
        {
            Debug.Log($"[MBRegenerationHelper] Loaded {loadedCount} generator assets into Base block from: {generatorDataPath}");
        }
#endif
    }
    
    /// <summary>
    /// Performs fresh terrain generation (Generator+PFM heightmap, generator-only decorator).
    /// This is the cleanest way to regenerate terrain from hash without paint interference.
    /// Overlay layers are preserved.
    /// </summary>
    public static RegenerationResult RegenerateFresh(
        Terrain terrain,
        string generatorDataPath = null,
        string scoDataPath = null,
        bool showProgress = true)
    {
        return RegenerateTerrain(
            terrain, 
            RegenerationMode.FreshGeneration, 
            generatorDataPath, 
            scoDataPath, 
            showProgress);
    }
    
    /// <summary>
    /// Performs full terrain regeneration with all layers and paint.
    /// Overlay layers are preserved.
    /// </summary>
    public static RegenerationResult RegenerateFull(
        Terrain terrain,
        string generatorDataPath = null,
        string scoDataPath = null,
        bool showProgress = true)
    {
        return RegenerateTerrain(
            terrain, 
            RegenerationMode.FullRegeneration, 
            generatorDataPath, 
            scoDataPath, 
            showProgress);
    }
    
    #endregion
    
    #region MBSceneDataManager Integration
    
    /// <summary>
    /// Regenerates terrain through MBSceneDataManager with specified mode.
    /// This is the recommended way to regenerate when using the scene data manager.
    /// Overlay layers are always preserved across regeneration.
    /// </summary>
    public static RegenerationResult RegenerateFromSceneData(
        MBSceneDataManager sceneData,
        RegenerationMode mode,
        bool extractHashData = true,
        bool showProgress = true)
    {
        if (sceneData == null)
            return RegenerationResult.Failure("MBSceneDataManager is null");
        
        if (sceneData.Terrain == null)
            return RegenerationResult.Failure("Terrain not assigned in MBSceneDataManager");
        
        var generator = sceneData.HeightmapGenerator;
        var decorator = sceneData.Decorator;
        
        if (generator == null)
            return RegenerationResult.Failure("LayeredHeightmapGenerator not found");
        
        string hash = generator.CurrentTerrainHash;
        
#if UNITY_EDITOR
        try
        {
            float startTime = Time.realtimeSinceStartup;
            var result = new RegenerationResult { modeUsed = mode };
            
            // Log overlay preservation
            if (decorator != null)
            {
                int overlayCount = decorator.GetOverlayLayers().Count;
                if (overlayCount > 0)
                    Debug.Log($"[MBRegenerationHelper] Preserving {overlayCount} overlay layers during regeneration");
            }
            
            // Step 1: Extract hash data if requested
            if (extractHashData)
            {
                if (showProgress)
                    EditorUtility.DisplayProgressBar("Regenerating Terrain", "Extracting hash data...", 0.1f);
                
                // Use the dedicated generator path property
                string extractDir = sceneData.EditorGeneratorSceneDataPath;
                string floraDataJsonPath = MBPathHelpers.ModFloraDataJsonPath(sceneData.Module.ID);
                
                if (!MBTerrainGeneratorHelpers.ExtractHashData(hash, extractDir, floraDataJsonPath))
                {
                    EditorUtility.ClearProgressBar();
                    return RegenerationResult.Failure($"Failed to extract hash data to: {extractDir}");
                }
            }
            
            // Step 2: Configure and generate (overlay preserved automatically)
            result = RegenerateTerrain(
                sceneData.Terrain,
                mode,
                sceneData.EditorGeneratorSceneDataPath,
                sceneData.EditorScoSceneDataPath,
                showProgress
            );
            
            return result;
        }
        catch (Exception ex)
        {
            EditorUtility.ClearProgressBar();
            return RegenerationResult.Failure($"Error: {ex.Message}");
        }
#else
        return RegenerationResult.Failure("Only available in Editor");
#endif
    }
    
    /// <summary>
    /// Regenerates fresh terrain from MBSceneDataManager (generator-only mode).
    /// Overlay layers are preserved.
    /// </summary>
    public static RegenerationResult RegenerateFreshFromSceneData(
        MBSceneDataManager sceneData,
        bool extractHashData = true,
        bool showProgress = true)
    {
        return RegenerateFromSceneData(
            sceneData, 
            RegenerationMode.FreshGeneration, 
            extractHashData, 
            showProgress);
    }
    
    /// <summary>
    /// Regenerates terrain with full layers from MBSceneDataManager.
    /// Overlay layers are preserved.
    /// </summary>
    public static RegenerationResult RegenerateFullFromSceneData(
        MBSceneDataManager sceneData,
        bool extractHashData = true,
        bool showProgress = true)
    {
        return RegenerateFromSceneData(
            sceneData, 
            RegenerationMode.FullRegeneration, 
            extractHashData, 
            showProgress);
    }
    
    #endregion
    
    #region Utility Methods
    
    /// <summary>
    /// Gets a summary of current layer states
    /// </summary>
    public static string GetLayerStateSummary(LayeredHeightmapGenerator generator)
    {
        if (generator == null)
            return "No generator";
        
        int total = generator.layers.Count;
        int active = 0;
        int generatorActive = 0;
        int pfmActive = 0;
        int otherActive = 0;
        
        foreach (var layer in generator.layers)
        {
            if (!layer.active) continue;
            
            active++;
            
            switch (layer.filterType)
            {
                case LayeredHeightmapGenerator.FilterType.Generator:
                    generatorActive++;
                    break;
                case LayeredHeightmapGenerator.FilterType.PFM:
                    pfmActive++;
                    break;
                default:
                    otherActive++;
                    break;
            }
        }
        
        return $"Layers: {active}/{total} active (Gen:{generatorActive}, PFM:{pfmActive}, Other:{otherActive})";
    }
    
    /// <summary>
    /// Gets a summary of current decorator rule states, split by block.
    /// </summary>
    public static string GetDecoratorRuleSummary(MBTerrainDecorator decoratorModular)
    {
        if (decoratorModular == null)
            return "No decorator";
        
        int baseLayers = 0, overlayLayers = 0;
        int baseActiveRules = 0, baseGenRules = 0, basePgmRules = 0;
        int overlayActiveRules = 0;
        
        foreach (var layer in decoratorModular.layers)
        {
            if (layer.block == MBTerrainDecorator.LayerBlock.Overlay)
            {
                overlayLayers++;
                overlayActiveRules += layer.rules.Count(r => r.active);
            }
            else
            {
                baseLayers++;
                foreach (var rule in layer.rules)
                {
                    if (!rule.active) continue;
                    baseActiveRules++;
                    
                    if (rule.filter == MBTerrainDecorator.FilterType.generator)
                        baseGenRules++;
                    else if (rule.filter == MBTerrainDecorator.FilterType.pgm)
                        basePgmRules++;
                }
            }
        }
        
        return $"Base: {baseLayers}L/{baseActiveRules}R (Gen:{baseGenRules}, PGM:{basePgmRules}), " +
               $"Overlay: {overlayLayers}L/{overlayActiveRules}R";
    }
    
    #endregion
}