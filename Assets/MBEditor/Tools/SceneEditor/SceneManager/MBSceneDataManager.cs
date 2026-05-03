using System;
using System.Collections.Generic;
using System.IO;
using MountAndBlade.Data;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Serialization;
using Object = System.Object;

#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Main scene data manager component for Mount & Blade scene export workflow.
/// Attach to a GameObject in your scene to manage all export operations.
/// 
/// Usage:
/// 1. Add this component to your scene
/// 2. Assign terrain and module references
/// 3. Configure export settings in Inspector
/// 4. Use Export buttons or call API methods from scripts
/// </summary>
[AddComponentMenu("M&B Tools/Scene Data Manager")]
public class MBSceneDataManager : MonoBehaviour
{
    #region Scene References

    [FormerlySerializedAs("terrain")]
    [Header("Scene References")]
    [Tooltip("Target terrain for export operations")]
    [SerializeField]
    private Terrain _terrain;

    [FormerlySerializedAs("module")] [Tooltip("M&B Module reference for props export")] [SerializeField]
    private MBModule _module;

    [SerializeField] private MBSceneData _sceneData;
    [SerializeField] private MBFloraGenerator _floraGeneratorRGL;
    [SerializeField] private Transform _waterPlane;
    [SerializeField] private TerrainTintData _tintData;
    [SerializeField] private FloraPopulatorConfig _floraPopulatorConfig;
    [SerializeField] private MBFloraLibrary _floraLibrary;

    // Auto-discovered components
    private MBTerrainDecorator _decorator;
    private LayeredHeightmapGenerator _heightmapGenerator;

    #endregion

    #region Export Configuration

    [Header("Scene Info")] [Tooltip("Scene identifier used in filenames")] [SerializeField]
    private string sceneName;

    #endregion

    #region Computed Paths (Read Only in Inspector)

    /// <summary>Editor SCO scene data path: {ModScenesPathFull}/{sceneName}/ScoData</summary>
    public string EditorScoSceneDataPath =>
        $"{MBPathHelpers.ModScenesPathFull(_module?.ID ?? "")}/{sceneName}/ScoData";

    public string EditorGeneratorSceneDataPath =>
        $"{MBPathHelpers.ModScenesPathFull(_module?.ID ?? "")}/{sceneName}/GeneratorData";

    public string EditorSceneTintDataPath =>
        $"{MBPathHelpers.ModScenesPath(_module?.ID ?? "")}/{sceneName}/TerrainData/TintLayers";

    /// <summary>M&B engine SCO file path: {EngineModuleScenesFolder}/{sceneName}.sco</summary>
    public string MBExportScoPath =>
        $"{MBPathHelpers.EngineModuleScenesFolder(_module?.ID ?? "")}/scn_{sceneName}.sco";

    /// <summary>Splatmaps export folder inside ScoData</summary>
    public string SplatmapsExportPath => EditorScoSceneDataPath;

    /// <summary>Elevation PFM export path</summary>
    public string ElevationExportPath =>
        Path.Combine(EditorScoSceneDataPath, "layer_ground_elevation.pfm");

    /// <summary>Tint PPM export path</summary>
    public string TintExportPath =>
        Path.Combine(EditorScoSceneDataPath, "layer_ground_tinting.ppm");

    /// <summary>Props JSON export path</summary>
    public string PropsExportPath =>
        Path.Combine(EditorScoSceneDataPath, "mission_objects.json");

    /// <summary>Generated flora json</summary>
    public string GeneratedFloraJsonPath =>
        Path.Combine(EditorGeneratorSceneDataPath, "flora.json");

    #endregion

    #region Splatmap Settings

    [Header("Splatmap Export Settings")] [SerializeField]
    private MBExportHelpers.SplatmapExportSettings splatmapSettings = new MBExportHelpers.SplatmapExportSettings();

    #endregion

    #region Elevation Settings

    [Header("Elevation Export Settings")] [SerializeField]
    private MBExportHelpers.ElevationExportSettings elevationSettings = new MBExportHelpers.ElevationExportSettings();

    #endregion

    #region Props Settings

    [Header("Props Export Settings")] [SerializeField]
    private MBExportHelpers.PropsExportSettings propsSettings = new MBExportHelpers.PropsExportSettings();

    #endregion

    #region SCO Settings

    [Header("SCO Repack Settings")] [SerializeField]
    private MBSCOHelpers.SCORepackSettings scoSettings = new MBSCOHelpers.SCORepackSettings();

    #endregion

    #region Export State

    [Header("Last Export Info (Read Only)")] [SerializeField]
    private string lastExportTime;

    [SerializeField] private List<string> lastExportedFiles = new List<string>();

    #endregion

    #region Properties

    /// <summary>Target terrain</summary>
    public Terrain Terrain
    {
        get => _terrain;
        set
        {
            _terrain = value;
            RefreshComponents();
        }
    }

    /// <summary>M&B Module reference</summary>
    public MBModule Module
    {
        get => _module;
        set => _module = value;
    }

    /// <summary>Flora generator component for RGL generation</summary>
    public MBFloraGenerator FloraGeneratorRGL
    {
        get
        {
            // if (_floraGeneratorRGL == null)
            // {
            // }
            return _floraGeneratorRGL;
        }
    }

    /// <summary>Scene name for filenames</summary>
    public string SceneName
    {
        get => sceneName;
        set => sceneName = value;
    }

    public MBSceneData SceneData
    {
        get
        {
            if (_sceneData == null)
            {
                var scn = sceneName;
                if (scn.StartsWith("scn_")) scn = scn.Substring(4);
                _sceneData = _module?.scenes.Find(scene => scene.name == scn);
            }

            return _sceneData;
        }
        set => _sceneData = value;
    }

    public TerrainTintData TintData
    {
        get => _tintData;
        set => _tintData = value;
    }

    /// <summary>Terrain decorator component (auto-discovered)</summary>
    public MBTerrainDecorator Decorator
    {
        get => _decorator;
        set => _decorator = value;
    }

    /// <summary>Heightmap generator component (auto-discovered)</summary>
    public LayeredHeightmapGenerator HeightmapGenerator => _heightmapGenerator;

    /// <summary>Splatmap export settings</summary>
    public MBExportHelpers.SplatmapExportSettings SplatmapSettings => splatmapSettings;

    /// <summary>Elevation export settings</summary>
    public MBExportHelpers.ElevationExportSettings ElevationSettings => elevationSettings;

    /// <summary>Props export settings</summary>
    public MBExportHelpers.PropsExportSettings PropsSettings => propsSettings;

    /// <summary>SCO repack settings</summary>
    public MBSCOHelpers.SCORepackSettings SCOSettings => scoSettings;

    /// <summary>Files from last export operation</summary>
    public IReadOnlyList<string> LastExportedFiles => lastExportedFiles;

    public FloraPopulatorConfig FloraPopulationConfig
    {
        get => _floraPopulatorConfig;
        set => _floraPopulatorConfig = value;
    }

    public MBFloraLibrary FloraLibrary
    {
        get => _floraLibrary;
        set => _floraLibrary = value;
    }

    #endregion

    #region Unity Lifecycle

    private void OnValidate()
    {
        RefreshComponents();

        // Set default scene name from Unity scene if empty
        if (string.IsNullOrEmpty(sceneName))
        {
            sceneName = SceneManager.GetActiveScene().name;
        }
    }

    private void Reset()
    {
        // Auto-find terrain
        _terrain = GetComponent<Terrain>();
        if (_terrain == null)
            _terrain = FindObjectOfType<Terrain>();

        // Set scene name from Unity scene
        sceneName = SceneManager.GetActiveScene().name;

        // Auto-configure SCO settings with tool paths
        scoSettings.repackExePath = MBPathHelpers.MBScoRepackToolPath();

        RefreshComponents();
    }

    #endregion

    #region Initialization

    /// <summary>Refresh auto-discovered component references</summary>
    public void RefreshComponents()
    {
        if (_terrain != null)
        {
            _decorator = _terrain.GetComponent<MBTerrainDecorator>();
            _heightmapGenerator = _terrain.GetComponent<LayeredHeightmapGenerator>();
            _tintData = _terrain.GetComponent<TerrainTintData>();
        }
        else
        {
            _decorator = null;
            _heightmapGenerator = null;
        }
    }

    /// <summary>Get the scene export directory path (ScoData folder)</summary>
    public string GetSceneExportDirectory()
    {
        return EditorScoSceneDataPath;
    }

    /// <summary>Ensure all export directories exist</summary>
    public void EnsureExportDirectoriesExist()
    {
        Directory.CreateDirectory(EditorScoSceneDataPath);
        Directory.CreateDirectory(SplatmapsExportPath);
    }

    #endregion

    #region Export All

    /// <summary>
    /// Export all scene data (splatmaps, elevation, props) to the ScoData directory.
    /// </summary>
    /// <returns>Combined result of all export operations</returns>
    public ExportAllResult ExportAll()
    {
        var result = new ExportAllResult();
        lastExportedFiles.Clear();

        // Ensure directories exist
        EnsureExportDirectoriesExist();

        // Export splatmaps
        result.splatmapResult = ExportSplatmaps();
        if (result.splatmapResult.success)
            lastExportedFiles.AddRange(result.splatmapResult.exportedFiles);

        // Export elevation
        result.elevationResult = ExportElevation();
        if (result.elevationResult.success)
            lastExportedFiles.AddRange(result.elevationResult.exportedFiles);

        // Export props
        result.propsResult = ExportProps();
        if (result.propsResult.success)
            lastExportedFiles.AddRange(result.propsResult.exportedFiles);

        // Update export time
        lastExportTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

        result.success = result.splatmapResult.success &&
                         result.elevationResult.success &&
                         result.propsResult.success;

        Debug.Log($"[MBSceneDataManager] Export All: {(result.success ? "Success" : "Partial/Failed")} - " +
                  $"{lastExportedFiles.Count} files to {EditorScoSceneDataPath}");

        return result;
    }

    /// <summary>Result of ExportAll operation</summary>
    public class ExportAllResult
    {
        public bool success;
        public MBExportHelpers.ExportResult splatmapResult;
        public MBExportHelpers.ExportResult elevationResult;
        public MBExportHelpers.ExportResult propsResult;

        public int TotalFileCount =>
            (splatmapResult?.exportedFiles?.Count ?? 0) +
            (elevationResult?.exportedFiles?.Count ?? 0) +
            (propsResult?.exportedFiles?.Count ?? 0);
    }

    #endregion

    #region Splatmap Export

    /// <summary>
    /// Export splatmaps to the ground_paint directory.
    /// </summary>
    /// <param name="outputDirectory">Target directory (uses SplatmapsExportPath if null)</param>
    /// <returns>Export result</returns>
    public MBExportHelpers.ExportResult ExportSplatmaps(string outputDirectory = null)
    {
        if (_terrain == null)
        {
            return MBExportHelpers.ExportResult.Failure("Terrain is not assigned");
        }

        outputDirectory ??= SplatmapsExportPath;
        Directory.CreateDirectory(outputDirectory);

        var decorator = Terrain.GetComponent<MBTerrainDecorator>();
        var result = MBExportHelpers.ExportSplatmaps(
            Terrain, outputDirectory, splatmapSettings,
            HeightmapGenerator, decorator);
        
        if (result.success)
        {
            lastExportTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        }

        return result;
    }

    /// <summary>
    /// Export a single splatmap layer.
    /// </summary>
    /// <param name="layerIndex">Layer index to export</param>
    /// <param name="outputPath">Output file path</param>
    /// <returns>Export result</returns>
    public MBExportHelpers.ExportResult ExportSplatmapLayer(int layerIndex, string outputPath)
    {
        if (_terrain == null)
        {
            return MBExportHelpers.ExportResult.Failure("Terrain is not assigned");
        }

        var decorator = Terrain.GetComponent<MBTerrainDecorator>();
        return MBExportHelpers.ExportSplatmaps(
            Terrain, outputPath, splatmapSettings,
            HeightmapGenerator, decorator);
    }

    #endregion

    #region Elevation Export

    /// <summary>
    /// Export elevation data to PFM file with resolution settings support.
    /// </summary>
    /// <param name="outputPath">Output file path (uses ElevationExportPath if null)</param>
    /// <returns>Export result</returns>
    public MBExportHelpers.ExportResult ExportElevation(string outputPath = null)
    {
        if (_heightmapGenerator == null)
        {
            return MBExportHelpers.ExportResult.Failure("LayeredHeightmapGenerator not found on terrain");
        }

        if (_terrain == null)
        {
            return MBExportHelpers.ExportResult.Failure("Terrain is not assigned");
        }

        outputPath ??= ElevationExportPath;
        Directory.CreateDirectory(Path.GetDirectoryName(outputPath));

        try
        {
            // Use the new export method with settings support
            var result = MBExportHelpers.ExportElevationWithSettings(
                _terrain,
                _heightmapGenerator,
                outputPath,
                elevationSettings,
                EditorScoSceneDataPath // For MatchSplatmaps mode
            );

            if (result.success)
            {
                lastExportTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            }

            return result;
        }
        catch (Exception ex)
        {
            Debug.LogError($"[MBSceneDataManager] Elevation export failed: {ex}");
            return MBExportHelpers.ExportResult.Failure($"Elevation export failed: {ex.Message}", ex);
        }
    }

    #endregion

    /// <summary>
    /// Direct elevation export using the same algorithm as LayeredHeightmapGenerator.ExportElevationToPFM
    /// 
    /// IMPORTANT: The Generator layer represents M&B's base terrain which is generated at ORIGIN (Y=0).
    /// The generator heightmap values ARE the actual world heights (no offset needed).
    /// 
    /// Our Unity terrain may be positioned anywhere (terrain.transform.position.y) and has a height range (TerrainHeight).
    /// We need to convert Unity's normalized heights back to world space, then subtract the generator heights.
    /// 
    /// Formula:
    ///   worldHeight = normalizedUnityHeight * TerrainHeight + terrain.transform.position.y
    ///   elevationDelta = worldHeight - generatorHeight
    /// 
    /// The elevationDelta is what M&B adds on top of its generated terrain to recreate our scene.
    /// </summary>
    private void ExportElevationDirect(string outputPath)
    {
        int resolution = _terrain.terrainData.heightmapResolution;
        float[,] currentHeights = _terrain.terrainData.GetHeights(0, 0, resolution, resolution);

        // TerrainHeight = the vertical range of our terrain (size.y)
        // We use generator's stored value as it accounts for height expansion
        float terrainHeight = _heightmapGenerator.TerrainHeight;

        // Fallback if not yet generated
        if (terrainHeight <= 0)
        {
            terrainHeight = _terrain.terrainData.size.y;
            Debug.LogWarning("[MBSceneDataManager] Using terrain.size.y as fallback - generator.TerrainHeight is 0");
        }

        // The terrain's Y position in world space
        // This is where the "bottom" of our terrain sits
        float terrainY = _terrain.transform.position.y;

        Debug.Log($"[MBSceneDataManager] Export: TerrainHeight={terrainHeight:F2}m, TerrainY={terrainY:F2}m");

        // Convert normalized Unity heights [0-1] to world heights
        // Unity terrain uses [y,x] indexing
        float[,] worldHeights = new float[resolution, resolution];
        for (int y = 0; y < resolution; y++)
        {
            for (int x = 0; x < resolution; x++)
            {
                // normalizedHeight * heightRange + baseY = worldHeight
                worldHeights[y, x] = currentHeights[y, x] * terrainHeight + terrainY;
            }
        }

        // Find the Generator layer
        LayeredHeightmapGenerator.Layer generatorLayer = null;
        foreach (var layer in _heightmapGenerator.layers)
        {
            if (layer.filterType == LayeredHeightmapGenerator.FilterType.Generator && layer.active)
            {
                generatorLayer = layer;
                break;
            }
        }

        if (generatorLayer == null)
        {
            throw new Exception("No active Generator layer found. Cannot compute elevation delta.");
        }

        // Load generator heightmap if not already loaded
        if (generatorLayer.heightmap == null && generatorLayer.generatorAsset != null)
        {
#if UNITY_EDITOR
            string genPath = AssetDatabase.GetAssetPath(generatorLayer.generatorAsset);
            generatorLayer.heightmap = NativeTerrainUtils.ReadHeightmap(genPath);
#endif
        }

        if (generatorLayer.heightmap == null)
        {
            throw new Exception("Generator heightmap data not available.");
        }

        // Resize generator heightmap to match current resolution
        // Generator heightmap values ARE world heights (generator terrain is at origin Y=0)
        float[,] genHeightmap = HeightmapHelper.ResizeMap(generatorLayer.heightmap, resolution, resolution);

        // Compute elevation delta: what we need to ADD to generator to get our terrain
        // Generator is at origin, so its values are direct world heights - no offset compensation needed
        float[,] elevationDelta = new float[resolution, resolution];
        for (int y = 0; y < resolution; y++)
        {
            for (int x = 0; x < resolution; x++)
            {
                // worldHeights uses [y,x], genHeightmap uses [x,y] internally
                elevationDelta[y, x] = worldHeights[y, x] - genHeightmap[x, y];
            }
        }

        // M&B requires horizontal flip
        elevationDelta = HeightmapHelper.FlipHeightMapHorizontally(elevationDelta);

        // Match original PFM dimensions if available
        var pfmLayer = _heightmapGenerator.layers.Find(l => l.filterType == LayeredHeightmapGenerator.FilterType.PFM);
        if (pfmLayer?.pfmAsset != null)
        {
#if UNITY_EDITOR
            string originalPath = AssetDatabase.GetAssetPath(pfmLayer.pfmAsset);
            var (origWidth, origHeight) = PfmHelper.GetDimensions(originalPath);

            if (origWidth > 0 && origHeight > 0 &&
                (origWidth != resolution || origHeight != resolution))
            {
                elevationDelta = HeightmapHelper.ResizeMap(elevationDelta, origHeight, origWidth);
                Debug.Log(
                    $"[MBSceneDataManager] Elevation resized: {resolution}x{resolution} -> {origWidth}x{origHeight}");
            }
#endif
        }

        // Write PFM file
        PfmHelper.WriteFloats(elevationDelta, outputPath);
        Debug.Log($"[MBSceneDataManager] Elevation exported to: {outputPath}");
    }

    /// <summary>
    /// Export elevation and overwrite the original PFM file.
    /// </summary>
    /// <returns>Export result</returns>
    public MBExportHelpers.ExportResult ExportElevationOverwrite()
    {
        if (_heightmapGenerator == null)
        {
            return MBExportHelpers.ExportResult.Failure("LayeredHeightmapGenerator not found on terrain");
        }

        string originalPath = GetOriginalElevationPath();
        if (string.IsNullOrEmpty(originalPath))
        {
            return MBExportHelpers.ExportResult.Failure("No original PFM path found in generator");
        }

        // Use full path for overwrite
        string fullPath = Path.GetFullPath(originalPath);
        return ExportElevation(fullPath);
    }

    /// <summary>
    /// Get the original PFM file path from the heightmap generator.
    /// </summary>
    public string GetOriginalElevationPath()
    {
        return _heightmapGenerator?.GetDefaultExportPath();
    }

    #region Props Export

    /// <summary>
    /// Export props to JSON file.
    /// </summary>
    /// <param name="outputPath">Output file path (uses PropsExportPath if null)</param>
    /// <returns>Export result</returns>
    public MBExportHelpers.ExportResult ExportProps(string outputPath = null)
    {
        outputPath ??= PropsExportPath;
        Directory.CreateDirectory(Path.GetDirectoryName(outputPath));

        var result = MBExportHelpers.ExportProps(_module, outputPath, _terrain, _floraLibrary, propsSettings);

        if (result.success)
        {
            lastExportTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        }

        return result;
    }

    #endregion

    #region SCO Operations

    /// <summary>
    /// Execute SCO repacking with current settings.
    /// </summary>
    /// <returns>Repack result</returns>
    public MBSCOHelpers.SCORepackResult RepackSCO()
    {
        // Auto-configure paths from module if not set
        if (string.IsNullOrEmpty(scoSettings.repackExePath))
        {
            scoSettings.repackExePath = MBPathHelpers.MBScoRepackToolPath();
        }

        // Input is the ScoData folder (unpacked scene data)
        if (string.IsNullOrEmpty(scoSettings.inputFolder))
        {
            scoSettings.inputFolder = EditorScoSceneDataPath;
        }

        // Output SCO goes to the ScoData folder first (next to the unpacked data)
        // Then we copy it to the Warband folder
        string localScoOutput = Path.Combine(
            Path.GetDirectoryName(EditorScoSceneDataPath),
            $"scn_{sceneName}.sco"
        );

        scoSettings.outputSCOPath = localScoOutput;

        // Set destination to M&B scenes folder for the copy step
        if (_module != null)
        {
            scoSettings.copyToDestination = true;
            scoSettings.destinationFolder = Path.GetDirectoryName(MBExportScoPath);
        }

        var result = MBSCOHelpers.Repack(scoSettings);

        if (result.success)
        {
            lastExportTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            lastExportedFiles.Clear();

            if (!string.IsNullOrEmpty(result.outputPath) && File.Exists(result.outputPath))
                lastExportedFiles.Add(result.outputPath);
            if (!string.IsNullOrEmpty(result.copiedToPath) && File.Exists(result.copiedToPath))
                lastExportedFiles.Add(result.copiedToPath);
        }

        return result;
    }

    /// <summary>
    /// Unpack an existing SCO file to the ScoData directory.
    /// </summary>
    /// <param name="scoFilePath">Path to .sco file to unpack (uses MBExportScoPath if null)</param>
    /// <returns>Unpack result</returns>
    public MBSCOHelpers.SCORepackResult UnpackSCO(string scoFilePath = null)
    {
        scoFilePath ??= MBExportScoPath;

        // For unpack, we'd need a separate tool call
        // The unpack tool takes: mab_sco_unpack.exe <input.sco> [output_folder]
        return MBSCOHelpers.Unpack(
            MBPathHelpers.MBScoUnpackToolPath(),
            scoFilePath,
            EditorScoSceneDataPath
        );
    }

    /// <summary>
    /// Preview the SCO repack command without executing.
    /// </summary>
    /// <returns>Command string that would be executed</returns>
    public string PreviewSCOCommand()
    {
        // Make sure paths are set for preview
        if (string.IsNullOrEmpty(scoSettings.inputFolder))
            scoSettings.inputFolder = EditorScoSceneDataPath;
        if (string.IsNullOrEmpty(scoSettings.outputSCOPath))
            scoSettings.outputSCOPath = Path.Combine(Path.GetDirectoryName(EditorScoSceneDataPath), $"{sceneName}.sco");

        return MBSCOHelpers.PreviewCommand(scoSettings);
    }

    #endregion

    #region Statistics

    /// <summary>
    /// Get scene object statistics.
    /// </summary>
    public SceneStatistics GetStatistics()
    {
        return new SceneStatistics
        {
            propCount = MBExportHelpers.CountObjectsWithTag("prop"),
            entryCount = MBExportHelpers.CountObjectsWithTag("entry"),
            itemCount = MBExportHelpers.CountObjectsWithTag("item"),
            passageCount = MBExportHelpers.CountObjectsWithTag("passage"),
            plantCount = MBExportHelpers.CountObjectsWithTag("plant"),
            terrainLayerCount = _terrain != null ? _terrain.terrainData.terrainLayers.Length : 0,
            heightmapResolution = _terrain != null ? _terrain.terrainData.heightmapResolution : 0,
            alphamapResolution = _terrain != null ? _terrain.terrainData.alphamapWidth : 0
        };
    }

    /// <summary>Scene statistics data</summary>
    [Serializable]
    public struct SceneStatistics
    {
        public int propCount;
        public int entryCount;
        public int itemCount;
        public int passageCount;
        public int plantCount;
        public int terrainLayerCount;
        public int heightmapResolution;
        public int alphamapResolution;

        public int TotalObjectCount => propCount + entryCount + itemCount + passageCount + plantCount;
    }

    #endregion

    #region Context Menu Actions

    [ContextMenu("Export All")]
    private void ContextExportAll()
    {
        var result = ExportAll();
        Debug.Log($"Export All: {(result.success ? "Success" : "Failed")} - {result.TotalFileCount} files");
    }

    [ContextMenu("Export Splatmaps")]
    private void ContextExportSplatmaps()
    {
        var result = ExportSplatmaps();
        Debug.Log($"Export Splatmaps: {result.message}");
    }

    [ContextMenu("Export Elevation")]
    private void ContextExportElevation()
    {
        var result = ExportElevation();
        Debug.Log($"Export Elevation: {result.message}");
    }

    [ContextMenu("Export Props")]
    private void ContextExportProps()
    {
        var result = ExportProps();
        Debug.Log($"Export Props: {result.message}");
    }

    [ContextMenu("Refresh Components")]
    private void ContextRefreshComponents()
    {
        RefreshComponents();
        Debug.Log($"Components refreshed. Decorator: {_decorator != null}, Generator: {_heightmapGenerator != null}");
    }

    [ContextMenu("Open ScoData Folder")]
    private void ContextOpenExportFolder()
    {
        string path = EditorScoSceneDataPath;
        if (Directory.Exists(path))
        {
            MBSCOHelpers.OpenFolder(path);
        }
        else
        {
            Debug.LogWarning($"ScoData folder does not exist: {path}");
        }
    }

    [ContextMenu("Log Export Paths")]
    private void ContextLogPaths()
    {
        Debug.Log($"[MBSceneDataManager] Export Paths for '{sceneName}':\n" +
                  $"  ScoData: {EditorScoSceneDataPath}\n" +
                  $"  Splatmaps: {SplatmapsExportPath}\n" +
                  $"  Elevation: {ElevationExportPath}\n" +
                  $"  Props: {PropsExportPath}\n" +
                  $"  SCO Output: {MBExportScoPath}");
    }

    #endregion

    #region Terrain Regeneration Settings

    [Header("Terrain Regeneration")] [SerializeField]
    private MBTerrainRegenerator.RegenerationSettings regenerationSettings =
        new MBTerrainRegenerator.RegenerationSettings();

    [Tooltip("Regeneration mode controls which layers are processed")] [SerializeField]
    private MBRegenerationHelper.RegenerationMode regenerationMode =
        MBRegenerationHelper.RegenerationMode.FreshGeneration;

    /// <summary>Regeneration settings for terrain from hash</summary>
    public MBTerrainRegenerator.RegenerationSettings RegenerationSettings => regenerationSettings;

    /// <summary>Current regeneration mode</summary>
    public MBRegenerationHelper.RegenerationMode CurrentRegenerationMode
    {
        get => regenerationMode;
        set => regenerationMode = value;
    }

    #endregion

    #region Terrain Regeneration API

    /// <summary>
    /// Regenerates the terrain from the current hash using the current regeneration mode.
    /// This extracts hash data, regenerates heightmap, and re-decorates the terrain.
    /// </summary>
    /// <returns>Result of the regeneration operation</returns>
    public MBTerrainRegenerator.RegenerationResult RegenerateTerrain()
    {
        if (_terrain == null)
            return MBTerrainRegenerator.RegenerationResult.Failure("Terrain is not assigned");

        // Use the new helper for controlled regeneration
        var helperResult = MBRegenerationHelper.RegenerateFromSceneData(
            this,
            regenerationMode,
            regenerationSettings.extractHashData,
            regenerationSettings.showProgress
        );

        // Convert helper result to standard result format
        var result = new MBTerrainRegenerator.RegenerationResult
        {
            success = helperResult.success,
            message = helperResult.message,
            heightmapTime = helperResult.heightmapTime,
            decoratorTime = helperResult.decoratorTime,
            totalTime = helperResult.totalTime
        };

        if (result.success)
        {
            lastExportTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        }

        return result;
    }

    /// <summary>
    /// Regenerates terrain in fresh mode (Generator+PFM only, no paint layers).
    /// This is the cleanest way to regenerate terrain from hash.
    /// </summary>
    public MBTerrainRegenerator.RegenerationResult RegenerateFresh()
    {
        var helperResult = MBRegenerationHelper.RegenerateFreshFromSceneData(
            this,
            regenerationSettings.extractHashData,
            regenerationSettings.showProgress
        );

        return ConvertHelperResult(helperResult);
    }

    /// <summary>
    /// Regenerates terrain with all layers including paint.
    /// </summary>
    public MBTerrainRegenerator.RegenerationResult RegenerateFull()
    {
        var helperResult = MBRegenerationHelper.RegenerateFullFromSceneData(
            this,
            regenerationSettings.extractHashData,
            regenerationSettings.showProgress
        );

        return ConvertHelperResult(helperResult);
    }

    private MBTerrainRegenerator.RegenerationResult ConvertHelperResult(
        MBRegenerationHelper.RegenerationResult helperResult)
    {
        var result = new MBTerrainRegenerator.RegenerationResult
        {
            success = helperResult.success,
            message = helperResult.message,
            heightmapTime = helperResult.heightmapTime,
            decoratorTime = helperResult.decoratorTime,
            totalTime = helperResult.totalTime
        };

        if (result.success)
        {
            lastExportTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        }

        return result;
    }

    /// <summary>
    /// Regenerates terrain keeping current layer states (doesn't auto-configure).
    /// Use this after manually setting up layers via the Layer Status section.
    /// </summary>
    public MBTerrainRegenerator.RegenerationResult RegenerateKeepLayers()
    {
        if (_terrain == null)
            return MBTerrainRegenerator.RegenerationResult.Failure("Terrain is not assigned");

        // Don't call ConfigureHeightmapLayers or ConfigureDecoratorRules
        // Just run the generation with whatever is currently set

#if UNITY_EDITOR
        try
        {
            float startTime = Time.realtimeSinceStartup;

            string hash = GetCurrentTerrainHash();
            if (string.IsNullOrEmpty(hash))
                return MBTerrainRegenerator.RegenerationResult.Failure("No terrain hash found");

            // Extract hash data if needed
            if (regenerationSettings.extractHashData)
            {
                if (regenerationSettings.showProgress)
                    EditorUtility.DisplayProgressBar("Regenerating Terrain", "Extracting hash data...", 0.1f);


                if (!MBTerrainGeneratorHelpers.ExtractHashData(hash, EditorGeneratorSceneDataPath,
                        MBPathHelpers.ModFloraDataJsonPath(_module.ID)))
                {
                    EditorUtility.ClearProgressBar();
                    return MBTerrainRegenerator.RegenerationResult.Failure($"Failed to extract hash data");
                }
            }

            // Generate heightmap (respects current layer active states)
            if (regenerationSettings.regenerateHeightmap && _heightmapGenerator != null)
            {
                if (regenerationSettings.showProgress)
                    EditorUtility.DisplayProgressBar("Regenerating Terrain", "Generating heightmap...", 0.4f);

                Undo.RegisterCompleteObjectUndo(_heightmapGenerator, "Regenerate Terrain");
                Undo.RegisterCompleteObjectUndo(_terrain.terrainData, "Regenerate Terrain");
                Undo.RegisterCompleteObjectUndo(_terrain, "Regenerate Terrain");

                _heightmapGenerator.GenerateHeightmap();
            }

            // Decorate (respects current rule active states)
            if (regenerationSettings.regenerateDecorator && _decorator != null)
            {
                if (regenerationSettings.showProgress)
                    EditorUtility.DisplayProgressBar("Regenerating Terrain", "Decorating terrain...", 0.7f);

                // Load generator assets if needed
                MBRegenerationHelper.LoadDecoratorGeneratorAssets(_decorator, EditorGeneratorSceneDataPath);

                // Process only active filters
                _decorator.ProcessGeneratorFilters();

                // Check if any PGM rules are active
                bool hasPgmActive = false;
                foreach (var layer in _decorator.layers)
                {
                    foreach (var rule in layer.rules)
                    {
                        if (rule.filter == MBTerrainDecorator.FilterType.pgm && rule.active)
                        {
                            hasPgmActive = true;
                            break;
                        }
                    }

                    if (hasPgmActive) break;
                }

                if (hasPgmActive)
                {
                    _decorator.ProcessPGMFilters();
                }

                _decorator.ProcessTextureFilters();
                _decorator.StartCoroutine(_decorator.DecorateNow());
            }

            float elapsed = Time.realtimeSinceStartup - startTime;

            if (regenerationSettings.showProgress)
                EditorUtility.ClearProgressBar();

            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(_terrain.gameObject.scene);

            lastExportTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

            return new MBTerrainRegenerator.RegenerationResult
            {
                success = true,
                message = $"Regeneration complete in {elapsed:F2}s (kept layer states)",
                totalTime = elapsed
            };
        }
        catch (Exception ex)
        {
            EditorUtility.ClearProgressBar();
            return MBTerrainRegenerator.RegenerationResult.Failure($"Error: {ex.Message}");
        }
#else
        return MBTerrainRegenerator.RegenerationResult.Failure("Only available in Editor");
#endif
    }

    /// <summary>
    /// Regenerates terrain from a new hash, keeping current layer states.
    /// Updates the hash but doesn't reconfigure which layers are active.
    /// </summary>
    public MBTerrainRegenerator.RegenerationResult RegenerateFromHashKeepLayers(string newHash)
    {
        if (_terrain == null)
            return MBTerrainRegenerator.RegenerationResult.Failure("Terrain is not assigned");

        if (string.IsNullOrEmpty(newHash))
            return MBTerrainRegenerator.RegenerationResult.Failure("Hash cannot be empty");

        if (!MBTerrainRegenerator.ValidateHash(newHash))
            return MBTerrainRegenerator.RegenerationResult.Failure($"Invalid hash: {newHash}");

        // Update the generator layer hash
        var generatorLayer = _heightmapGenerator?.layers.Find(
            l => l.filterType == LayeredHeightmapGenerator.FilterType.Generator);

        if (generatorLayer != null)
        {
            generatorLayer.generatorHash = newHash;
        }

        // set layered generator current hash
        SetTerrainHash(newHash);

        // Update generator data
        if (_heightmapGenerator?.generatorData != null)
        {
            var parsedData = MBTerrainRegenerator.ParseHash(newHash);
            if (parsedData != null)
            {
                _heightmapGenerator.generatorData.SizeX = parsedData.SizeX;
                _heightmapGenerator.generatorData.SizeY = parsedData.SizeY;
                _heightmapGenerator.generatorData.PolygonSize = parsedData.PolygonSize;
                _heightmapGenerator.generatorData.TerrainType = parsedData.TerrainType;
                _heightmapGenerator.generatorData.HillHeight = parsedData.HillHeight;
                _heightmapGenerator.generatorData.Valley = parsedData.Valley;
                _heightmapGenerator.generatorData.Ruggedness = parsedData.Ruggedness;
                _heightmapGenerator.generatorData.Vegetation = parsedData.Vegetation;
                _heightmapGenerator.generatorData.PlaceRiver = parsedData.PlaceRiver;
                _heightmapGenerator.generatorData.DeepWater = parsedData.DeepWater;
                _heightmapGenerator.generatorData.ShadeOcclude = parsedData.ShadeOcclude;
                _heightmapGenerator.generatorData.DisableGrass = parsedData.DisableGrass;
                _heightmapGenerator.generatorData.TerrainSeed = parsedData.TerrainSeed;
                _heightmapGenerator.generatorData.RiverSeed = parsedData.RiverSeed;
                _heightmapGenerator.generatorData.FloraSeed = parsedData.FloraSeed;
                _heightmapGenerator.generatorData.FullHash = newHash;
            }
        }

        // Regenerate without changing layer states
        var result = RegenerateKeepLayers();

        // Update water plane to match new terrain dimensions
        if (result.success)
            SetupWaterPlane();

        return result;
    }

    /// <summary>
    /// Regenerates the terrain from a new hash code.
    /// Updates the generator layer hash, then performs regeneration based on current mode.
    /// </summary>
    /// <param name="newHash">New terrain hash code (0x... format)</param>
    /// <returns>Result of the regeneration operation</returns>
    public MBTerrainRegenerator.RegenerationResult RegenerateFromHash(string newHash)
    {
        if (_terrain == null)
            return MBTerrainRegenerator.RegenerationResult.Failure("Terrain is not assigned");

        if (string.IsNullOrEmpty(newHash))
            return MBTerrainRegenerator.RegenerationResult.Failure("Hash cannot be empty");

        if (!MBTerrainRegenerator.ValidateHash(newHash))
            return MBTerrainRegenerator.RegenerationResult.Failure($"Invalid hash: {newHash}");

        // Update the generator layer hash
        var generatorLayer = _heightmapGenerator?.layers.Find(
            l => l.filterType == LayeredHeightmapGenerator.FilterType.Generator);

        if (generatorLayer != null)
        {
            generatorLayer.generatorHash = newHash;
        }

        // Update generator data
        if (_heightmapGenerator?.generatorData != null)
        {
            var parsedData = MBTerrainRegenerator.ParseHash(newHash);
            if (parsedData != null)
            {
                _heightmapGenerator.generatorData.SizeX = parsedData.SizeX;
                _heightmapGenerator.generatorData.SizeY = parsedData.SizeY;
                _heightmapGenerator.generatorData.PolygonSize = parsedData.PolygonSize;
                _heightmapGenerator.generatorData.TerrainType = parsedData.TerrainType;
                _heightmapGenerator.generatorData.HillHeight = parsedData.HillHeight;
                _heightmapGenerator.generatorData.Valley = parsedData.Valley;
                _heightmapGenerator.generatorData.Ruggedness = parsedData.Ruggedness;
                _heightmapGenerator.generatorData.Vegetation = parsedData.Vegetation;
                _heightmapGenerator.generatorData.PlaceRiver = parsedData.PlaceRiver;
                _heightmapGenerator.generatorData.DeepWater = parsedData.DeepWater;
                _heightmapGenerator.generatorData.ShadeOcclude = parsedData.ShadeOcclude;
                _heightmapGenerator.generatorData.DisableGrass = parsedData.DisableGrass;
                _heightmapGenerator.generatorData.TerrainSeed = parsedData.TerrainSeed;
                _heightmapGenerator.generatorData.RiverSeed = parsedData.RiverSeed;
                _heightmapGenerator.generatorData.FloraSeed = parsedData.FloraSeed;
                _heightmapGenerator.generatorData.FullHash = newHash;
            }
        }

        var result = RegenerateTerrain();

        // Update water plane to match new terrain dimensions
        if (result.success)
            SetupWaterPlane();

        return result;
    }

    /// <summary>
    /// Regenerates only the heightmap without decoration.
    /// Uses generator-only mode for heightmap layers.
    /// </summary>
    public MBTerrainRegenerator.RegenerationResult RegenerateHeightmapOnly()
    {
        // Configure for heightmap only
        var mode = MBRegenerationHelper.RegenerationMode.GeneratorAndPFM;

        var helperResult = MBRegenerationHelper.RegenerateTerrain(
            _terrain,
            mode,
            EditorGeneratorSceneDataPath,
            EditorScoSceneDataPath,
            regenerationSettings.showProgress
        );

        return ConvertHelperResult(helperResult);
    }

    /// <summary>
    /// Regenerates only the decoration/splatmaps without heightmap.
    /// </summary>
    public MBTerrainRegenerator.RegenerationResult RegenerateDecoratorOnly()
    {
        if (_decorator == null)
            return MBTerrainRegenerator.RegenerationResult.Failure("No decorator found");

#if UNITY_EDITOR
        try
        {
            float startTime = Time.realtimeSinceStartup;

            // Configure decorator based on current mode
            bool useGeneratorOnly =
                regenerationMode.HasFlag(MBRegenerationHelper.RegenerationMode.DecoratorGeneratorOnly);

            if (useGeneratorOnly)
                MBRegenerationHelper.SetDecoratorGeneratorOnlyMode(_decorator);
            else
                MBRegenerationHelper.SetDecoratorFullMode(_decorator);

            // Process and decorate
            _decorator.ProcessGeneratorFilters();

            if (!useGeneratorOnly)
            {
                _decorator.ProcessPGMFilters();
                _decorator.ProcessTextureFilters();
            }

            _decorator.StartCoroutine(_decorator.DecorateNow());

            float elapsed = Time.realtimeSinceStartup - startTime;

            return new MBTerrainRegenerator.RegenerationResult
            {
                success = true,
                message = $"Decorator regenerated in {elapsed:F2}s",
                decoratorTime = elapsed,
                totalTime = elapsed
            };
        }
        catch (Exception ex)
        {
            return MBTerrainRegenerator.RegenerationResult.Failure($"Decorator failed: {ex.Message}");
        }
#else
        return MBTerrainRegenerator.RegenerationResult.Failure("Only available in Editor");
#endif
    }

    /// <summary>
    /// Sets up the decorator based on the current terrain hash.
    /// Configures proper layer mapping for the terrain type using MBSplatmapImportHelper.
    /// </summary>
    /// <param name="generatorRulesOnly">If true, only enables generator (tlt_) rules after setup</param>
    public void SetupDecoratorFromCurrentHash(bool generatorRulesOnly = true, string currentHash = null)
    {
        if (_decorator == null)
        {
            Debug.LogError("[MBSceneDataManager] No decorator found");
            return;
        }

        string hash = GetCurrentTerrainHash();
        if (!string.IsNullOrEmpty(currentHash))
        {
            hash = currentHash;
        }

        if (string.IsNullOrEmpty(hash))
        {
            Debug.LogError("[MBSceneDataManager] No terrain hash found");
            return;
        }

        // Parse hash to get terrain type and seed
        var terrainData = MBTerrainRegenerator.ParseHash(hash);
        if (terrainData == null)
        {
            Debug.LogError($"[MBSceneDataManager] Failed to parse hash: {hash}");
            return;
        }

        // Get region type and rock type from hash
        var regionType = MBSplatmapImportHelper.GetRegionTypeFromCode(terrainData.TerrainType);
        bool useGrayStone = MBSplatmapImportHelper.ShouldUseGrayStone((int)terrainData.TerrainSeed);

        Debug.Log($"[MBSceneDataManager] Setting up decorator:\n" +
                  $"  Region: {regionType}\n" +
                  $"  Rock: {(useGrayStone ? "gray_stone" : "brown_stone")}\n" +
                  $"  Generator Path: {EditorGeneratorSceneDataPath}\n" +
                  $"  SCO Path: {EditorScoSceneDataPath}\n" +
                  $"  Mapping: {MBSplatmapImportHelper.GetLayerMappingSummary(regionType)}");

#if UNITY_EDITOR
        // Refresh asset database to make sure files are imported
        MBSplatmapImportHelper.RefreshAssets(EditorGeneratorSceneDataPath, EditorScoSceneDataPath);
#endif

        // Use MBSplatmapImportHelper to setup the full decorator structure
        MBSplatmapImportHelper.SetupDecorator(
            _decorator,
            EditorGeneratorSceneDataPath,
            EditorScoSceneDataPath,
            regionType,
            useGrayStone
        );

        // If generator-only mode, disable all PGM rules
        if (generatorRulesOnly)
        {
            MBRegenerationHelper.SetDecoratorGeneratorOnlyMode(_decorator);
        }

        // Debug print the setup
        MBSplatmapImportHelper.DebugPrintSetup(_decorator);

#if UNITY_EDITOR
        EditorUtility.SetDirty(_decorator);
#endif
    }

    /// <summary>
    /// Sets up decorator with custom region type (override auto-detection from hash)
    /// </summary>
    public void SetupDecoratorWithRegion(MBSplatmapImportHelper.MBRegionType regionType, bool useGrayStone,
        bool generatorRulesOnly = true)
    {
        if (_decorator == null)
        {
            Debug.LogError("[MBSceneDataManager] No decorator found");
            return;
        }

        Debug.Log($"[MBSceneDataManager] Setting up decorator with custom region:\n" +
                  $"  Region: {regionType}\n" +
                  $"  Rock: {(useGrayStone ? "gray_stone" : "brown_stone")}\n" +
                  $"  Generator Path: {EditorGeneratorSceneDataPath}\n" +
                  $"  SCO Path: {EditorScoSceneDataPath}");

        MBSplatmapImportHelper.SetupDecorator(
            _decorator,
            EditorGeneratorSceneDataPath,
            EditorScoSceneDataPath,
            regionType,
            useGrayStone
        );

        if (generatorRulesOnly)
        {
            MBRegenerationHelper.SetDecoratorGeneratorOnlyMode(_decorator);
        }

#if UNITY_EDITOR
        EditorUtility.SetDirty(_decorator);
#endif
    }

    /// <summary>
    /// Gets the current terrain region type from hash
    /// </summary>
    public MBSplatmapImportHelper.MBRegionType GetCurrentRegionType()
    {
        var data = GetTerrainDataFromHash();
        if (data == null)
            return MBSplatmapImportHelper.MBRegionType.Plain;

        return MBSplatmapImportHelper.GetRegionTypeFromCode(data.TerrainType);
    }

    /// <summary>
    /// Gets whether gray stone should be used based on terrain seed
    /// </summary>
    public bool GetUseGrayStone()
    {
        var data = GetTerrainDataFromHash();
        if (data == null)
            return true;

        return MBSplatmapImportHelper.ShouldUseGrayStone((int)data.TerrainSeed);
    }

    /// <summary>
    /// Enables only generator layers in heightmap and decorator.
    /// Use this for clean terrain generation from hash.
    /// </summary>
    public void SetGeneratorOnlyMode()
    {
        regenerationMode = MBRegenerationHelper.RegenerationMode.FreshGeneration;

        if (_heightmapGenerator != null)
        {
            MBRegenerationHelper.SetGeneratorOnlyMode(_heightmapGenerator);
            Debug.Log(
                $"[MBSceneDataManager] Heightmap: {MBRegenerationHelper.GetLayerStateSummary(_heightmapGenerator)}");
        }

        if (_decorator != null)
        {
            MBRegenerationHelper.SetDecoratorGeneratorOnlyMode(_decorator);
            Debug.Log($"[MBSceneDataManager] Decorator: {MBRegenerationHelper.GetDecoratorRuleSummary(_decorator)}");
        }
    }

    /// <summary>
    /// Enables all layers in heightmap and decorator.
    /// Use this for full terrain with paint layers.
    /// </summary>
    public void SetFullMode()
    {
        regenerationMode = MBRegenerationHelper.RegenerationMode.FullRegeneration;

        if (_heightmapGenerator != null)
        {
            MBRegenerationHelper.EnableAllHeightmapLayers(_heightmapGenerator);
            Debug.Log(
                $"[MBSceneDataManager] Heightmap: {MBRegenerationHelper.GetLayerStateSummary(_heightmapGenerator)}");
        }

        if (_decorator != null)
        {
            MBRegenerationHelper.SetDecoratorFullMode(_decorator);
            Debug.Log($"[MBSceneDataManager] Decorator: {MBRegenerationHelper.GetDecoratorRuleSummary(_decorator)}");
        }
    }

    /// <summary>
    /// Gets the current layer state summary for display
    /// </summary>
    public string GetLayerStateSummary()
    {
        string heightmapSummary = MBRegenerationHelper.GetLayerStateSummary(_heightmapGenerator);
        string decoratorSummary = MBRegenerationHelper.GetDecoratorRuleSummary(_decorator);
        return $"Heightmap: {heightmapSummary}\nDecorator: {decoratorSummary}";
    }

    /// <summary>
    /// Validates that the terrain is ready for regeneration.
    /// </summary>
    public (bool isValid, string message) ValidateForRegeneration()
    {
        return MBTerrainRegenerator.ValidateForRegeneration(_terrain);
    }

    /// <summary>
    /// Gets the current terrain hash from the generator.
    /// </summary>
    public string GetCurrentTerrainHash()
    {
        return SceneData?.TerrainCode;
    }

    public void SetTerrainHash(string terrainHash)
    {
        _heightmapGenerator.CurrentTerrainHash = terrainHash;

        SceneData.TerrainCode = terrainHash;
        EditorUtility.SetDirty(_sceneData);
        AssetDatabase.SaveAssets();
    }

    /// <summary>
    /// Gets parsed terrain data from the current hash.
    /// </summary>
    public MBTerrainGeneratorData GetTerrainDataFromHash()
    {
        string hash = GetCurrentTerrainHash();
        if (string.IsNullOrEmpty(hash))
            return null;

        return MBTerrainRegenerator.ParseHash(hash);
    }

    /// <summary>
    /// Generates a new hash from the provided terrain data.
    /// </summary>
    public string GenerateTerrainHash(MBTerrainGeneratorData data)
    {
        return MBTerrainRegenerator.GenerateHash(data);
    }

    /// <summary>
    /// Calculates the actual in-game terrain size from hash parameters.
    /// </summary>
    public (int sizeX, int sizeY, int verticesX, int verticesY) GetActualTerrainSize()
    {
        var data = GetTerrainDataFromHash();
        if (data == null)
            return (0, 0, 0, 0);

        var (sizeX, sizeY, vertX, vertY, _, _) = MBTerrainRegenerator.CalculateActualSize(
            data.SizeX, data.SizeY, data.PolygonSize);

        return (sizeX, sizeY, vertX, vertY);
    }

    public void SetupWaterPlane()
    {
        if (_waterPlane == null)
            return;

        var size = GetActualTerrainSize();
        _waterPlane.transform.localScale = new Vector3(size.sizeX * 0.1f, 1, size.sizeY * 0.1f);
        _waterPlane.transform.localPosition = new Vector3(size.sizeX / 2, -0.3f, size.sizeY / 2);
    }

    /// <summary>
    /// Disables the water plane GameObject. Used for mesh-based scenes that have no terrain.
    /// </summary>
    public void DisableWaterPlane()
    {
        if (_waterPlane != null)
            _waterPlane.gameObject.SetActive(false);
    }

    #endregion

    #region Context Menu Actions for Regeneration

    [ContextMenu("Regenerate Terrain (Current Mode)")]
    private void ContextRegenerateTerrain()
    {
        var result = RegenerateTerrain();
        Debug.Log($"Regenerate Terrain: {result.message}");
    }

    [ContextMenu("Regenerate Fresh (Generator Only)")]
    private void ContextRegenerateFresh()
    {
        var result = RegenerateFresh();
        Debug.Log($"Regenerate Fresh: {result.message}");
    }

    [ContextMenu("Regenerate Full (All Layers)")]
    private void ContextRegenerateFull()
    {
        var result = RegenerateFull();
        Debug.Log($"Regenerate Full: {result.message}");
    }

    [ContextMenu("Regenerate Heightmap Only")]
    private void ContextRegenerateHeightmap()
    {
        var result = RegenerateHeightmapOnly();
        Debug.Log($"Regenerate Heightmap: {result.message}");
    }

    [ContextMenu("Regenerate Decorator Only")]
    private void ContextRegenerateDecorator()
    {
        var result = RegenerateDecoratorOnly();
        Debug.Log($"Regenerate Decorator: {result.message}");
    }

    [ContextMenu("Setup Decorator From Hash")]
    private void ContextSetupDecoratorFromHash()
    {
        SetupDecoratorFromCurrentHash(generatorRulesOnly: true);
    }

    [ContextMenu("Set Generator-Only Mode")]
    private void ContextSetGeneratorOnlyMode()
    {
        SetGeneratorOnlyMode();
    }

    [ContextMenu("Set Full Mode")]
    private void ContextSetFullMode()
    {
        SetFullMode();
    }

    [ContextMenu("Validate Regeneration Setup")]
    private void ContextValidateRegeneration()
    {
        var (isValid, message) = ValidateForRegeneration();
        Debug.Log($"Regeneration Validation: {(isValid ? "VALID" : "INVALID")} - {message}");
    }

    [ContextMenu("Log Layer State Summary")]
    private void ContextLogLayerSummary()
    {
        Debug.Log($"[MBSceneDataManager] Layer State Summary:\n{GetLayerStateSummary()}");
    }

    [ContextMenu("Log Current Hash Info")]
    private void ContextLogHashInfo()
    {
        string hash = GetCurrentTerrainHash();
        var data = GetTerrainDataFromHash();

        if (data == null)
        {
            Debug.Log("No valid terrain hash found");
            return;
        }

        var (sizeX, sizeY, vertX, vertY, _, _) = MBTerrainRegenerator.CalculateActualSize(
            data.SizeX, data.SizeY, data.PolygonSize);

        Debug.Log($"[MBSceneDataManager] Terrain Hash Info:\n" +
                  $"  Hash: {hash}\n" +
                  $"  Type: {MBTerrainRegenerator.GetTerrainTypeName(data.TerrainType)} ({data.TerrainType})\n" +
                  $"  Size: {data.SizeX}x{data.SizeY} → Actual: {sizeX}x{sizeY}m\n" +
                  $"  Polygon: {data.PolygonSize}m, Vertices: {vertX}x{vertY}\n" +
                  $"  Features: Veg={data.Vegetation}, Rug={data.Ruggedness}, Val={data.Valley}, Hill={data.HillHeight}\n" +
                  $"  Flags: River={data.PlaceRiver}, DeepWater={data.DeepWater}, Shade={data.ShadeOcclude}, NoGrass={data.DisableGrass}\n" +
                  $"  Seeds: Terrain={data.TerrainSeed}, River={data.RiverSeed}, Flora={data.FloraSeed}");
    }

    #endregion
}
