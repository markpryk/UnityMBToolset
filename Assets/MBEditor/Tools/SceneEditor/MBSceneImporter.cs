using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MountAndBlade.Data;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

public class MBSceneImporter
{
    private string _sourceScenePath = "none";
    private MBTerrainPalette _editorPalette;
    private MBTerrainDecorator _editorDecorator;
    private Terrain _editorTerrain;

    private List<MBSceneData> _availableScenes = new List<MBSceneData>();
    private string _searchQuery = "";
    private Vector2 _scrollPosition;
    private int _selectedSceneIndex = -1;

    private bool _showDetails = false;
    private Texture2D _previewImage;
    private string _previewPath;

    private string _sceneID = "scn_none";

    private const string GeneratorDataFolder = "GeneratorData";
    private const string ScoDataFolder = "ScoData";

    private MBModule _currentMod;
    private MBSceneData _currentScene;

    // Cached data to avoid per-frame I/O
    private HashSet<string> _importedScenesCache;
    private string _cachedModIdForImported;

    // Cached scene info (updated on selection)
    private SceneType _cachedSceneType;
    private bool _cachedValidSco;
    private bool _cachedValidHash;
    private bool _cachedPrefabFound;
    private string _cachedTerrainTypeName;

    // Scene type classification
    private enum SceneType
    {
        Unknown,
        TerrainWithSco,   // Has both terrain code and .sco - full outdoor scene
        ScoOnly,           // Has .sco but no terrain code - mesh-based with props
        TerrainOnly,       // Has terrain code but no .sco - generated terrain, no props
        MeshOnly,          // No terrain, no .sco - interior/arena with just a model mesh
    }

    public string ScenePath
    {
        get => $"{MBPathHelpers.ModScenesPathFull(_currentMod.ID)}/{_sceneID}";
    }

    public string ScenePathRelative
    {
        get => $"{MBPathHelpers.ModScenesPath(_currentMod.ID)}/{_sceneID}";
    }

    public string GetTerrainTypeName(int id)
    {
        switch (id)
        {
            case 0:  return "fallback";
            case 2:  return "steppe";
            case 3:  return "plain";
            case 4:  return "snow";
            case 5:  return "desert";
            case 10: return "steppe_forest";
            case 11: return "forest";
            case 12: return "snow_forest";
            case 13: return "desert_palms";
            default: return "unknown";
        }
    }

    private SceneType ClassifyScene(bool hasSco, bool hasHash)
    {
        if (hasSco && hasHash) return SceneType.TerrainWithSco;
        if (hasSco && !hasHash) return SceneType.ScoOnly;
        if (!hasSco && hasHash) return SceneType.TerrainOnly;
        return SceneType.MeshOnly;
    }

    private string GetSceneTypeLabel(SceneType type)
    {
        switch (type)
        {
            case SceneType.TerrainWithSco: return "Terrain + Props (Full Outdoor)";
            case SceneType.ScoOnly:        return "Mesh + Props (Interior/Arena)";
            case SceneType.TerrainOnly:    return "Terrain Only (No Props)";
            case SceneType.MeshOnly:       return "Mesh Only (Interior Shell)";
            default:                       return "Unknown";
        }
    }

    private Color GetSceneTypeColor(SceneType type)
    {
        switch (type)
        {
            case SceneType.TerrainWithSco: return new Color(0.4f, 0.8f, 0.4f); // green
            case SceneType.ScoOnly:        return new Color(0.5f, 0.7f, 1.0f); // blue
            case SceneType.TerrainOnly:    return new Color(0.9f, 0.8f, 0.3f); // yellow
            case SceneType.MeshOnly:       return new Color(0.8f, 0.6f, 0.3f); // orange
            default:                       return Color.gray;
        }
    }

    public void DrawGUI(MBModule currentMod)
    {
        _currentMod = currentMod;

        GUILayout.Label("M&B Scene Importer", EditorStyles.boldLabel);
        GUILayout.Box("", GUILayout.ExpandWidth(true), GUILayout.Height(1));

        if (GUILayout.Button("Load Available Scenes"))
        {
            // Use module data directly
            if (_currentMod.scenes != null)
            {
                _availableScenes = _currentMod.scenes;
            }
            else
            {
                _availableScenes = new List<MBSceneData>();
                Debug.LogWarning("[SceneImporter] Module has no scenes list defined.");
            }
            
            InvalidateImportedCache();

            var dir = MBPathHelpers.ModScenesPath(_currentMod.ID);
            if (!Directory.Exists(dir))
                Directory.CreateDirectory(dir);
        }

        if (_availableScenes != null && _availableScenes.Count > 0)
        {
            DrawSceneList();
            DrawSceneInfo();
        }
        else
        {
            GUILayout.Label("No scenes available in module data.");
        }

        if (_currentScene == null)
            return;

        GUILayout.Box("", GUILayout.ExpandWidth(true), GUILayout.Height(1));
        
        _showDetails = EditorGUILayout.Foldout(_showDetails, "Advanced Details");
        if (_showDetails)
        {
            DrawSceneDetails();
        }
        
        DrawLoadButton();
    }

    private void DrawSceneList()
    {
        GUILayout.Label("Search Scenes:");
        _searchQuery = EditorGUILayout.TextField(_searchQuery);

        var filteredScenes = string.IsNullOrWhiteSpace(_searchQuery)
            ? _availableScenes
            : _availableScenes.FindAll(scene => scene?.SceneID?.IndexOf(_searchQuery, StringComparison.OrdinalIgnoreCase) >= 0);

        GUILayout.Label($"Available Scenes ({filteredScenes.Count}):");

        _scrollPosition = GUILayout.BeginScrollView(_scrollPosition, GUILayout.Height(200));

        // Use cached imported scenes set - rebuild only when module changes or on load
        var importedScenes = GetImportedScenesCache();

        for (int i = 0; i < filteredScenes.Count; i++)
        {
            var sceneName = filteredScenes[i].SceneID;
            GUI.color = importedScenes.Contains(sceneName) ? Color.green : Color.white;

            if (GUILayout.Button(sceneName))
            {
                _selectedSceneIndex = _availableScenes.IndexOf(filteredScenes[i]);
                LoadSceneData();
            }

            GUI.color = Color.white;
        }

        GUILayout.EndScrollView();
    }

    private HashSet<string> GetImportedScenesCache()
    {
        // Rebuild cache if module changed or cache is empty
        if (_importedScenesCache == null || _cachedModIdForImported != _currentMod.ID)
        {
            _importedScenesCache = new HashSet<string>();
            _cachedModIdForImported = _currentMod.ID;

            var modScenesDir = MBPathHelpers.ModScenesPath(_currentMod.ID);
            if (Directory.Exists(modScenesDir))
            {
                foreach (var dir in Directory.GetDirectories(modScenesDir))
                    _importedScenesCache.Add(Path.GetFileName(dir));
            }
        }

        return _importedScenesCache;
    }

    /// <summary>
    /// Call after importing a scene to refresh the green highlights.
    /// </summary>
    private void InvalidateImportedCache()
    {
        _importedScenesCache = null;
    }

    private void DrawSceneInfo()
    {
        if (_currentScene == null || _selectedSceneIndex < 0 || _selectedSceneIndex >= _availableScenes.Count)
            return;

        GUILayout.Box("", GUILayout.ExpandWidth(true), GUILayout.Height(1));
        GUILayout.Label($"Selected Scene: {_availableScenes[_selectedSceneIndex].SceneID}");

        // Scene Preview
        if (!string.IsNullOrEmpty(_previewPath) && _previewImage != null)
        {
             var rect = GUILayoutUtility.GetRect(200, 150);
             GUI.DrawTexture(rect, _previewImage, ScaleMode.ScaleToFit);
        }
        else
        {
            GUILayout.Label("No Preview Available", EditorStyles.centeredGreyMiniLabel);
        }
        
        GUILayout.Space(5);

        var prevColor = GUI.color;
        GUI.color = GetSceneTypeColor(_cachedSceneType);
        EditorGUILayout.LabelField("Scene Type", GetSceneTypeLabel(_cachedSceneType), EditorStyles.boldLabel);
        GUI.color = prevColor;

        // Terrain type info
        if (_cachedValidHash && !string.IsNullOrEmpty(_cachedTerrainTypeName))
        {
            EditorGUILayout.LabelField("Terrain Type", _cachedTerrainTypeName);
        }

        // Mesh info for non-terrain scenes
        if (!_cachedValidHash && !string.IsNullOrEmpty(_currentScene.MeshName))
        {
            EditorGUILayout.LabelField("Base Mesh", _currentScene.MeshName);

            if (_cachedPrefabFound)
            {
                EditorGUILayout.LabelField("Prefab Status", "✓ Found", EditorStyles.boldLabel);
            }
            else
            {
                var prev = GUI.color;
                GUI.color = new Color(1f, 0.4f, 0.4f);
                EditorGUILayout.LabelField("Prefab Status", "✗ Not found in module or Native");
                GUI.color = prev;
            }
        }
    }

    private void DrawSceneDetails()
    {
        EditorGUILayout.TextField("Scene Path", _sourceScenePath);

        if (!string.IsNullOrEmpty(_currentScene.TerrainCode))
            EditorGUILayout.TextField("Generator Hash", _currentScene.TerrainCode);

        if (!string.IsNullOrEmpty(_currentScene.MeshName))
            EditorGUILayout.TextField("Mesh Name", _currentScene.MeshName);

        EditorGUILayout.ObjectField("Decorator", _editorDecorator, typeof(MBTerrainDecorator), true);
        EditorGUILayout.ObjectField("Terrain", _editorTerrain, typeof(Terrain), true);
        EditorGUILayout.ObjectField("Palette", _editorPalette, typeof(MBTerrainPalette), true);
    }

    private void DrawLoadButton()
    {
        // For mesh-only scenes, show prefab warning using cached value
        if (_cachedSceneType == SceneType.MeshOnly || _cachedSceneType == SceneType.ScoOnly)
        {
            if (!string.IsNullOrEmpty(_currentScene.MeshName) && !_cachedPrefabFound)
            {
                EditorGUILayout.HelpBox(
                    $"Base mesh prefab '{_currentScene.MeshName}' not found in '{_currentMod.ID}' or 'Native'. " +
                    "The scene will load without a base model.",
                    MessageType.Warning);
            }
        }

        // Always allow loading if we have at least a .sco or hash
        bool canLoad = _cachedValidSco || _cachedValidHash;

        // Also allow loading mesh-only scenes if we have a mesh name
        if (!canLoad && !string.IsNullOrEmpty(_currentScene.MeshName))
            canLoad = true;

        if (!canLoad)
        {
            EditorGUILayout.HelpBox(
                "Cannot load this scene: no valid .sco file, terrain code, or mesh name found.",
                MessageType.Error);
            return;
        }

        if (GUILayout.Button($"Load Scene ({GetSceneTypeLabel(_cachedSceneType)})"))
        {
            LoadScene(_cachedSceneType, _cachedValidSco, _cachedValidHash);
            InvalidateImportedCache(); // refresh green highlights after import
        }
    }

    private void LoadScene(SceneType sceneType, bool validSco, bool validHash)
    {
        // Derive scene ID
        _sceneID = _currentScene.SceneID;

        // Create directory structure
        EnsureDirectories();

        // Open base scene
        var editScenePath = $"{ScenePath}/{_sceneID}.unity";
        File.Copy(MBPathHelpers.CommonImporterScene(), editScenePath, true);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        var scn = EditorSceneManager.OpenScene(editScenePath, OpenSceneMode.Single);

        // Extract .sco data if available
        if (validSco)
        {
            ExtractScoFile();
            AssetDatabase.Refresh();
        }

        var sceneManager = Object.FindFirstObjectByType<MBSceneDataManager>();

        // Set common scene manager fields
        sceneManager.SceneName = _sceneID;
        sceneManager.Module = _currentMod;
        sceneManager.SceneData = _currentScene;

        switch (sceneType)
        {
            case SceneType.TerrainWithSco:
            case SceneType.TerrainOnly:
                SetupTerrainScene(sceneManager);
                break;

            case SceneType.ScoOnly:
                SetupMeshScene(sceneManager, instantiateBaseMesh: true);
                break;

            case SceneType.MeshOnly:
                SetupMeshScene(sceneManager, instantiateBaseMesh: true);
                break;
        }

        // Instantiate outer terrain border mesh if defined
        InstantiateOuterTerrainBorder(sceneManager);

        // Import props from .sco if available
        if (validSco)
        {
            var missionObjectsPath = $"{ScenePathRelative}/{ScoDataFolder}/mission_objects.json";
            if (File.Exists(missionObjectsPath))
            {
                ImporterProps.RemoveAllProps();
                ImporterProps.LoadPropsFromJson(_currentMod, missionObjectsPath);
            }
        }

        EditorSceneManager.SaveScene(scn);
        GC.Collect();

        Debug.Log($"[SceneImporter] Loaded '{_sceneID}' as {GetSceneTypeLabel(sceneType)}");
    }

    private void EnsureDirectories()
    {
        if (!Directory.Exists(ScenePath))
            Directory.CreateDirectory(ScenePath);

        if (!Directory.Exists(Path.Combine(ScenePath, ScoDataFolder)))
            Directory.CreateDirectory(Path.Combine(ScenePath, ScoDataFolder));

        if (!Directory.Exists(Path.Combine(ScenePath, GeneratorDataFolder)))
            Directory.CreateDirectory(Path.Combine(ScenePath, GeneratorDataFolder));
    }

    private void SetupTerrainScene(MBSceneDataManager sceneManager)
    {
        _editorPalette = AssetDatabase.LoadAssetAtPath<MBTerrainPalette>(
            MBPathHelpers.ModTerrainPalette(_currentMod.ID));

        var terrainDataFolder = $"{ScenePathRelative}/TerrainData";
        if (!Directory.Exists(terrainDataFolder))
            Directory.CreateDirectory(terrainDataFolder);

        // Create terrain data asset
        var terrainDataPath = $"{terrainDataFolder}/{_sceneID}_terrain_data.asset";
        var terrainData = new TerrainData();
        AssetDatabase.CreateAsset(terrainData, terrainDataPath);

        _editorTerrain = Terrain.CreateTerrainGameObject(terrainData).GetComponent<Terrain>();
        _editorTerrain.name = "Terrain";
        _editorTerrain.transform.SetSiblingIndex(1);

        if (_editorPalette != null)
            _editorTerrain.terrainData.terrainLayers = _editorPalette.PaletteLayers.ToArray();
        else
            Debug.LogWarning("[SceneImporter] Terrain palette not found - layers will be empty.");

        _editorDecorator = _editorTerrain.gameObject.AddComponent<MBTerrainDecorator>();
        _editorDecorator.t = _editorTerrain;
        _editorDecorator.layers = new List<MBTerrainDecorator.Layers>();

        // Generate terrain
        ExtractHash();
        AssetDatabase.Refresh();

        var generatorData = MBTerrainGeneratorHelpers.ParseTerrainCode(_currentScene.TerrainCode);
        generatorData.FullHash = _currentScene.TerrainCode;

        if (_editorPalette != null)
            _editorTerrain.terrainData.terrainLayers = _editorPalette.PaletteLayers.ToArray();

        GenerateTerrain(_editorTerrain, generatorData);
        AssetDatabase.Refresh();

        GenerateBaseDecorator();
        AssetDatabase.Refresh();

        _editorDecorator.Decorate();

        // Assign to scene manager
        sceneManager.Terrain = _editorTerrain;
        sceneManager.Decorator = _editorDecorator;

        // Terrain material
        var newTerrainMat = new Material(ImporterProps.BuildData.TerrainBaseMaterial);
        var matName = $"Terrain_{_sceneID}";
        newTerrainMat.name = matName;
        string matPath = $"{ScenePathRelative}/TerrainData/{matName}.mat";

        sceneManager.Terrain.materialTemplate = newTerrainMat;
        AssetDatabase.CreateAsset(newTerrainMat, matPath);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        // Tint data
        sceneManager.TintData = sceneManager.Terrain.gameObject.AddComponent<TerrainTintData>();
        sceneManager.FloraPopulationConfig = sceneManager.Terrain.gameObject.AddComponent<FloraPopulatorConfig>();
        if (File.Exists(sceneManager.TintExportPath))
        {
            sceneManager.TintData.AddPPMLayer("Base Tint", sceneManager.TintExportPath);

            // Auto-bake tint so it appears immediately without manual intervention
            AutoBakeTint(sceneManager);
        }

        // Water
        sceneManager.gameObject.SetActive(true);
        sceneManager.SetupWaterPlane();
    }

    private void SetupMeshScene(MBSceneDataManager sceneManager, bool instantiateBaseMesh)
    {
        // Scene manager stays active for mesh scenes so we can still use scene tools
        sceneManager.gameObject.SetActive(true);

        // Disable water plane for mesh-based scenes (no terrain = no water)
        sceneManager.DisableWaterPlane();

        if (!instantiateBaseMesh || string.IsNullOrEmpty(_currentScene.MeshName))
            return;

        // Try current module first, then fall back to Native
        var prefab = MBEditorUtility.GetModelPrefab(_currentMod.ID, _currentScene.MeshName);
        string resolvedFrom = _currentMod.ID;

        if (prefab == null && !_currentMod.ID.Equals("Native", StringComparison.OrdinalIgnoreCase))
        {
            prefab = MBEditorUtility.GetModelPrefab("Native", _currentScene.MeshName);
            resolvedFrom = "Native";
        }

        if (prefab != null)
        {
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            instance.name = $"{_currentScene.MeshName}";
            instance.transform.SetSiblingIndex(1);

            // Apply module-specific overrides (materials/textures)
            ApplyModuleOverrides(instance, _currentMod.ID);

            Debug.Log($"[SceneImporter] Instantiated base mesh: {_currentScene.MeshName} (from {resolvedFrom})");
        }
        else
        {
            Debug.LogWarning(
                $"[SceneImporter] Base mesh prefab '{_currentScene.MeshName}' not found in " +
                $"'{_currentMod.ID}' or 'Native'. Scene loaded without base model.");
        }
    }

    private void InstantiateOuterTerrainBorder(MBSceneDataManager sceneManager)
    {
        if (string.IsNullOrEmpty(_currentScene.OuterTerrainMesh))
            return;

        // Try current module first, then fall back to Native
        var prefab = MBEditorUtility.GetModelPrefab(_currentMod.ID, _currentScene.OuterTerrainMesh);
        string resolvedFrom = _currentMod.ID;

        if (prefab == null && !_currentMod.ID.Equals("Native", StringComparison.OrdinalIgnoreCase))
        {
            prefab = MBEditorUtility.GetModelPrefab("Native", _currentScene.OuterTerrainMesh);
            resolvedFrom = "Native";
        }

        if (prefab != null)
        {
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            instance.name = $"{_currentScene.OuterTerrainMesh} (Border)";
            instance.transform.SetSiblingIndex(1);

            // Scale and position to match terrain size (same as water plane)
            var terrainSize = sceneManager.GetActualTerrainSize();
            int sizeX = terrainSize.sizeX;
            int sizeY = terrainSize.sizeY;

            // Fall back to scene bounds if no terrain hash available
            if (sizeX == 0 || sizeY == 0)
            {
                sizeX = (int)(_currentScene.MaxPos.x - _currentScene.MinPos.x);
                sizeY = (int)(_currentScene.MaxPos.y - _currentScene.MinPos.y);
            }

            if (sizeX > 0 && sizeY > 0)
            {
                // Get the lowest terrain bound for Y positioning
                float lowestY = 0f;
                var terrain = sceneManager.Terrain;
                if (terrain != null)
                {
                    lowestY = terrain.transform.position.y + terrain.terrainData.bounds.min.y;
                }

                instance.transform.localScale = new Vector3(sizeX, Mathf.Max(sizeX,sizeY), sizeY);
                instance.transform.localPosition = new Vector3(0f, lowestY, 0f);
            }

            // Apply module-specific overrides (materials/textures)
            ApplyModuleOverrides(instance, _currentMod.ID);

            Debug.Log($"[SceneImporter] Instantiated outer terrain border: {_currentScene.OuterTerrainMesh} (from {resolvedFrom}), size: {sizeX}x{sizeY}");
        }
        else
        {
            Debug.LogWarning(
                $"[SceneImporter] Outer terrain border prefab '{_currentScene.OuterTerrainMesh}' not found in " +
                $"'{_currentMod.ID}' or 'Native'.");
        }
    }

    private void ApplyModuleOverrides(GameObject instance, string moduleName)
    {
        if (string.IsNullOrEmpty(moduleName) || moduleName == "Native")
            return;

        string dbPath = MBPathHelpers.ModBRFDataBasePath(moduleName);
        var brfDb = AssetDatabase.LoadAssetAtPath<ModBrfDataBase>(dbPath);
        
        if (brfDb == null) return;
        
        // Ensure lookups are built
        brfDb.BuildLookups();

        var renderers = instance.GetComponentsInChildren<Renderer>(true);
        bool anyChange = false;

        foreach (var renderer in renderers)
        {
            var sharedMats = renderer.sharedMaterials;
            bool matsChanged = false;

            for (int i = 0; i < sharedMats.Length; i++)
            {
                Material originalMat = sharedMats[i];
                if (originalMat == null) continue;

                // 1. Check for Material replacement (same name in module)
                var replacementMatEntry = brfDb.FindMaterialEntry(originalMat.name);
                if (replacementMatEntry?.UnityMaterial != null && replacementMatEntry.UnityMaterial != originalMat)
                {
                    sharedMats[i] = replacementMatEntry.UnityMaterial;
                    matsChanged = true;
                    continue;
                }

                // 2. Check for Texture replacements within the same material name
                var newMat = CheckForTextureOverrides(originalMat, brfDb);
                if (newMat != null)
                {
                    sharedMats[i] = newMat;
                    matsChanged = true;
                }
            }

            if (matsChanged)
            {
                renderer.sharedMaterials = sharedMats;
                anyChange = true;
            }
        }

        if (anyChange)
        {
            Debug.Log($"[SceneImporter] Applied module overrides to '{instance.name}' for module '{moduleName}'");
        }
    }

    private Material CheckForTextureOverrides(Material original, ModBrfDataBase brfDb)
    {
        if (original == null) return null;

        string[] textureSlots = { "_MainTex", "_SpecGlossMap", "_BumpMap" };
        bool needsOverride = false;
        
        // First pass: check if any replacements exist
        foreach (var slot in textureSlots)
        {
            if (!original.HasProperty(slot)) continue;
            
            var tex = original.GetTexture(slot);
            if (tex == null) continue;

            var replacementTex = brfDb.FindTexture(tex.name);
            if (replacementTex != null && replacementTex != tex)
            {
                needsOverride = true;
                break;
            }
        }

        if (!needsOverride) return null;

        // Second pass: create copy and apply
        Material instanceMat = new Material(original);
        instanceMat.name = $"{original.name} (Module Override)";

        foreach (var slot in textureSlots)
        {
            if (!instanceMat.HasProperty(slot)) continue;

            var tex = instanceMat.GetTexture(slot);
            if (tex == null) continue;

            var replacementTex = brfDb.FindTexture(tex.name);
            if (replacementTex != null && replacementTex != tex)
            {
                instanceMat.SetTexture(slot, replacementTex);
            }
        }

        return instanceMat;
    }

    private void LoadSceneData()
    {
        var selectedSceneData = _availableScenes[_selectedSceneIndex];

        // Directly use the selected MBSceneData
        _currentScene = selectedSceneData;

        // Identify .sco file path if it exists
        _sourceScenePath = ModuleInspector.GetSceneObjFileByName(_currentMod.ID, _currentScene.SceneID);

        // Preview Image
        _previewPath = ModuleInspector.GetScenePreviewPath(_currentMod.ID, _currentScene.SceneID);
        
        if (_previewImage != null)
        {
            Object.DestroyImmediate(_previewImage);
        }
        _previewImage = null;

        if (!string.IsNullOrEmpty(_previewPath))
        {
            var bytes = File.ReadAllBytes(_previewPath);
            _previewImage = new Texture2D(2, 2);
            _previewImage.LoadImage(bytes);
        }

        // Cache all expensive lookups once
        _cachedValidSco = !string.IsNullOrEmpty(_sourceScenePath) && File.Exists(_sourceScenePath);
        _cachedValidHash = MBTerrainGeneratorHelpers.ValidateHashCode(_currentScene.TerrainCode);
        _cachedSceneType = ClassifyScene(_cachedValidSco, _cachedValidHash);

        // Cache terrain type name
        _cachedTerrainTypeName = null;
        if (_cachedValidHash)
        {
            var parsed = MBTerrainGeneratorHelpers.ParseTerrainCode(_currentScene.TerrainCode);
            if (parsed != null)
                _cachedTerrainTypeName = GetTerrainTypeName(parsed.TerrainType);
        }

        // Cache prefab lookup (module → Native fallback)
        _cachedPrefabFound = false;
        if (!string.IsNullOrEmpty(_currentScene.MeshName))
        {
            var prefab = MBEditorUtility.GetModelPrefab(_currentMod.ID, _currentScene.MeshName);

            if (prefab == null && !_currentMod.ID.Equals("Native", StringComparison.OrdinalIgnoreCase))
                prefab = MBEditorUtility.GetModelPrefab("Native", _currentScene.MeshName);

            _cachedPrefabFound = prefab != null;
        }
    }

    private void GenerateTerrain(Terrain tr, MBTerrainGeneratorData generatorData)
    {
        DefaultAsset heightmap =
            AssetDatabase.LoadAssetAtPath<DefaultAsset>(
                $"{ScenePathRelative}/{ScoDataFolder}/layer_ground_elevation.pfm");
        TextAsset baseNoise =
            AssetDatabase.LoadAssetAtPath<TextAsset>(
                $"{ScenePathRelative}/{GeneratorDataFolder}/heightmap.txt");

        var generator = tr.gameObject.AddComponent<LayeredHeightmapGenerator>();
        generator.Initialize(tr, generatorData, heightmap, baseNoise);
        generator.GenerateHeightmap();
    }

    private void GenerateBaseDecorator()
    {
        var regionType = (MBSplatmapImportHelper.MBRegionType)
            MBTerrainGeneratorHelpers.ParseTerrainCode(_currentScene.TerrainCode).TerrainType;

        MBSplatmapImportHelper.SetupDecorator(
            _editorDecorator,
            Path.Combine(ScenePathRelative, GeneratorDataFolder),
            Path.Combine(ScenePathRelative, ScoDataFolder),
            regionType);
    }

    private void ExtractHash()
    {
        var outputDirectory = Path.Combine(ScenePath, GeneratorDataFolder);
        MBTerrainGeneratorHelpers.ExtractHashData(_currentScene.TerrainCode, outputDirectory,
            MBPathHelpers.ModFloraDataJsonPath(_currentMod.ID));
        AssetDatabase.Refresh();
    }

    private void ExtractScoFile()
    {
        var outputDirectory = Path.Combine(ScenePath, ScoDataFolder);
        ScoHelpers.ExtractSco(_sourceScenePath, outputDirectory);
        AssetDatabase.Refresh();
    }

    /// <summary>
    /// Auto-bake tint layers to a PNG and apply to the terrain material.
    /// Called during import so the tint is visible immediately.
    /// </summary>
    private void AutoBakeTint(MBSceneDataManager sceneManager)
    {
        var tintData = sceneManager.TintData;
        if (tintData == null || !tintData.HasLayers)
            return;

        try
        {
            // Ensure PPM cached textures are loaded
            for (int i = 0; i < tintData.layers.Count; i++)
            {
                var layer = tintData.layers[i];
                if (layer.type == TerrainTintData.LayerType.PPM && layer.ppmCachedTexture == null)
                {
                    string ppmPath = layer.ppmSourcePath;
                    if (!string.IsNullOrEmpty(ppmPath) && File.Exists(ppmPath))
                    {
                        Color[,] ppmData = PpmHelper.ReadPpm(ppmPath);
                        layer.ppmCachedTexture = PpmHelper.MBTerrainPpmToTexture(ppmData, convertToLinear: false);
                        layer.ppmCachedTexture.hideFlags = HideFlags.HideAndDontSave;
                    }
                }
            }

            // Determine output size from the first layer with a texture
            int width = 0, height = 0;
            foreach (var layer in tintData.layers)
            {
                var tex = layer.EffectiveTexture;
                if (tex != null)
                {
                    width = tex.width;
                    height = tex.height;
                    break;
                }
            }

            if (width == 0 || height == 0)
                return;

            // CPU composite (same logic as TintTab.BakeCPU)
            var result = new Texture2D(width, height, TextureFormat.RGB24, false);
            Color[] pixels = new Color[width * height];
            for (int i = 0; i < pixels.Length; i++)
                pixels[i] = Color.white;

            foreach (var layer in tintData.layers)
            {
                var layerTex = layer.EffectiveTexture;
                if (!layer.active || layer.opacity <= 0f || layerTex == null)
                    continue;

                for (int y = 0; y < height; y++)
                {
                    float v = (float)y / (height - 1);
                    for (int x = 0; x < width; x++)
                    {
                        float u = (float)x / (width - 1);
                        int idx = y * width + x;

                        Color layerColor = layerTex.GetPixelBilinear(u, v);

                        if (layerColor.r >= 0.999f && layerColor.g >= 0.999f && layerColor.b >= 0.999f)
                            continue;

                        layerColor = Color.Lerp(Color.white, layerColor, layer.opacity);

                        // Multiply blend (default)
                        pixels[idx] = new Color(
                            pixels[idx].r * layerColor.r,
                            pixels[idx].g * layerColor.g,
                            pixels[idx].b * layerColor.b);
                    }
                }
            }

            result.SetPixels(pixels);
            result.Apply();

            // Save as PNG
            string tintFolder = sceneManager.EditorSceneTintDataPath;
            if (!Directory.Exists(tintFolder))
                Directory.CreateDirectory(tintFolder);

            string savePath = $"{tintFolder}/Tint_{_sceneID}.png";
            File.WriteAllBytes(savePath, result.EncodeToPNG());
            Object.DestroyImmediate(result);

            AssetDatabase.ImportAsset(savePath);

            var importer = AssetImporter.GetAtPath(savePath) as TextureImporter;
            if (importer != null)
            {
                importer.isReadable = true;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.mipmapEnabled = false;
                importer.npotScale = TextureImporterNPOTScale.None;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.sRGBTexture = false;
                importer.SaveAndReimport();
            }

            // Assign baked tint map
            tintData.bakedTintMap = AssetDatabase.LoadAssetAtPath<Texture2D>(savePath);

            // Apply to terrain material
            var terrain = sceneManager.Terrain;
            if (tintData.bakedTintMap != null && terrain?.materialTemplate != null)
            {
                var mat = terrain.materialTemplate;
                if (mat.HasProperty(tintData.shaderProperty))
                {
                    mat.SetTexture(tintData.shaderProperty, tintData.bakedTintMap);
                }

                // Set overlay scale
                Vector3 terrainSize = terrain.terrainData.size;
                mat.SetFloat("_OverlayScaleX", 1.0f / terrainSize.x);
                mat.SetFloat("_OverlayScaleZ", 1.0f / terrainSize.z);
            }

            Debug.Log($"[SceneImporter] Auto-baked tint: {savePath}");
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[SceneImporter] Auto-bake tint failed: {e.Message}");
        }
    }
}