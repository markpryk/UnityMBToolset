using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// MonoBehaviour component for M&B Warband flora generation.
/// Attach to a GameObject to manage flora spawning in the scene.
/// </summary>
public class MBFloraGenerator : MonoBehaviour
{
    #region Serialized Fields
    
    public string FloraJsonPath = "";
    public MBModule Module;
    public bool SpawnTrees = true;
    public bool SpawnRocks = true;
    public bool SpawnGrass = false;
    public Vector3 PositionOffset = Vector3.zero;
    public bool ApplyTerrainHeight = false;
    public Terrain TargetTerrain;
    public Transform TreesContainer;
    public Transform RocksContainer;
    public Transform GrassContainer;
    
    #endregion
    
    #region Runtime Data
    
    [HideInInspector]
    public MBFloraGeneratorHelper.FloraExportData LoadedData;
    
    [HideInInspector]
    public MBFloraGeneratorHelper.FloraSpawnResult LastSpawnResult;
    
    [HideInInspector]
    public bool IsDataLoaded => LoadedData != null;
    
    [HideInInspector]
    public int SpawnedTreeCount;
    
    [HideInInspector]
    public int SpawnedRockCount;
    
    [HideInInspector]
    public int SpawnedGrassCount;
    
    #endregion
    
    #region Public Methods
    
    /// <summary>
    /// Load flora data from configured source
    /// </summary>
    public bool LoadData()
    {
        string jsonContent = null;
        
         if (!string.IsNullOrEmpty(FloraJsonPath))
        {
            string fullPath = FloraJsonPath;
            
            // Handle relative paths
            if (!System.IO.Path.IsPathRooted(fullPath))
            {
                fullPath = System.IO.Path.Combine(Application.dataPath, fullPath);
            }
            
            if (System.IO.File.Exists(fullPath))
            {
                jsonContent = System.IO.File.ReadAllText(fullPath);
            }
            else
            {
                Debug.LogError($"Flora JSON file not found: {fullPath}");
                return false;
            }
        }
        else
        {
            Debug.LogError("No flora data source configured. Set either FloraJsonAsset or FloraJsonPath.");
            return false;
        }
        
        try
        {
            LoadedData = Newtonsoft.Json.JsonConvert.DeserializeObject<MBFloraGeneratorHelper.FloraExportData>(jsonContent);
            
            if (LoadedData == null)
            {
                Debug.LogError("Failed to parse flora JSON data");
                return false;
            }
            
            Debug.Log($"Loaded flora data: {LoadedData.trees?.count ?? 0} trees, " +
                      $"{LoadedData.rocks?.count ?? 0} rocks, {LoadedData.grass?.count ?? 0} grass");

            return true;
        }
        catch (Exception ex)
        {
            Debug.LogError($"Error parsing flora JSON: {ex.Message}");
            return false;
        }
    }
    
    /// <summary>
    /// Spawn all flora based on current settings
    /// </summary>
    public void SpawnFlora()
    {
        if (!IsDataLoaded)
        {
            if (!LoadData())
            {
                return;
            }
        }
        
        // Clear existing flora first
        ClearFlora();
        
        // Ensure containers exist
        EnsureContainers();
        
        // Build config from settings
        var config = new MBFloraGeneratorHelper.FloraSpawnConfig
        {
            SpawnTrees = SpawnTrees,
            SpawnRocks = SpawnRocks,
            SpawnGrass = SpawnGrass,
            ModuleId = Module?.ID,
            ParentTransform = transform,
            PositionOffset = PositionOffset,
            ApplyTerrainHeight = ApplyTerrainHeight,
            TargetTerrain = TargetTerrain,
        };
        
        // Limit grass if needed
        if (SpawnGrass && LoadedData.grass != null )
        {
            LoadedData.grass.instances = LoadedData.grass.instances.GetRange(0, LoadedData.grass.instances.Count);
            LoadedData.grass.count = LoadedData.grass.instances.Count;
        }
        
        // Spawn using helper
        LastSpawnResult = SpawnFloraInternal(LoadedData, config);
        
        // Update counts
        SpawnedTreeCount = LastSpawnResult.TreesSpawned;
        SpawnedRockCount = LastSpawnResult.RocksSpawned;
        SpawnedGrassCount = LastSpawnResult.GrassSpawned;
    }
    
    /// <summary>
    /// Clear all spawned flora
    /// </summary>
    public void ClearFlora()
    {
        ClearContainer(TreesContainer);
        ClearContainer(RocksContainer);
        ClearContainer(GrassContainer);
        
        // Also clear any direct children with plant tag
        var children = new List<Transform>();
        foreach (Transform child in transform)
        {
            if (child != TreesContainer && child != RocksContainer && child != GrassContainer)
            {
                children.Add(child);
            }
        }
        
        foreach (var child in children)
        {
            DestroyImmediate(child.gameObject);
        }
        
        SpawnedTreeCount = 0;
        SpawnedRockCount = 0;
        SpawnedGrassCount = 0;
        LastSpawnResult = null;
    }
    
    /// <summary>
    /// Get statistics string for display
    /// </summary>
    public string GetStats()
    {
        if (!IsDataLoaded)
        {
            return "No data loaded";
        }
        
        return MBFloraGeneratorHelper.GetFloraStats(LoadedData);
    }
    
    /// <summary>
    /// Spawn only trees
    /// </summary>
    public void SpawnTreesOnly()
    {
        if (!IsDataLoaded && !LoadData()) return;
        
        ClearContainer(TreesContainer);
        EnsureContainers();
        
        var tempSpawnTrees = SpawnTrees;
        var tempSpawnRocks = SpawnRocks;
        var tempSpawnGrass = SpawnGrass;
        
        SpawnTrees = true;
        SpawnRocks = false;
        SpawnGrass = false;
        
        SpawnFlora();
        
        SpawnTrees = tempSpawnTrees;
        SpawnRocks = tempSpawnRocks;
        SpawnGrass = tempSpawnGrass;
    }
    
    /// <summary>
    /// Spawn only rocks
    /// </summary>
    public void SpawnRocksOnly()
    {
        if (!IsDataLoaded && !LoadData()) return;
        
        ClearContainer(RocksContainer);
        EnsureContainers();
        
        var tempSpawnTrees = SpawnTrees;
        var tempSpawnRocks = SpawnRocks;
        var tempSpawnGrass = SpawnGrass;
        
        SpawnTrees = false;
        SpawnRocks = true;
        SpawnGrass = false;
        
        SpawnFlora();
        
        SpawnTrees = tempSpawnTrees;
        SpawnRocks = tempSpawnRocks;
        SpawnGrass = tempSpawnGrass;
    }
    
    #endregion
    
    #region Private Methods
    
    private void EnsureContainers()
    {
        if (TreesContainer == null && SpawnTrees)
        {
            var treesGO = new GameObject("Trees");
            treesGO.transform.SetParent(transform, false);
            TreesContainer = treesGO.transform;
        }
        
        if (RocksContainer == null && SpawnRocks)
        {
            var rocksGO = new GameObject("Rocks");
            rocksGO.transform.SetParent(transform, false);
            RocksContainer = rocksGO.transform;
        }
        
        if (GrassContainer == null && SpawnGrass)
        {
            var grassGO = new GameObject("Grass");
            grassGO.transform.SetParent(transform, false);
            GrassContainer = grassGO.transform;
        }
    }
    
    private void ClearContainer(Transform container)
    {
        if (container == null) return;
        
        while (container.childCount > 0)
        {
            DestroyImmediate(container.GetChild(0).gameObject);
        }
    }
    
    private MBFloraGeneratorHelper.FloraSpawnResult SpawnFloraInternal(
        MBFloraGeneratorHelper.FloraExportData data, 
        MBFloraGeneratorHelper.FloraSpawnConfig config)
    {
        var result = new MBFloraGeneratorHelper.FloraSpawnResult();
        
        // Build flora kind lookup
        var floraKindLookup = new Dictionary<int, MBFloraGeneratorHelper.FloraKindData>();
        if (data.floraKinds != null)
        {
            foreach (var kind in data.floraKinds)
            {
                floraKindLookup[kind.index] = kind;
            }
        }
        
        // Spawn trees
        if (config.SpawnTrees && data.trees?.instances != null && TreesContainer != null)
        {
            result.TreesSpawned = SpawnFloraSet(
                data.trees.instances, 
                floraKindLookup, 
                TreesContainer, 
                config, 
                "tree",
                result);
        }
        
        // Spawn rocks
        if (config.SpawnRocks && data.rocks?.instances != null && RocksContainer != null)
        {
            result.RocksSpawned = SpawnFloraSet(
                data.rocks.instances, 
                floraKindLookup, 
                RocksContainer, 
                config, 
                "rock",
                result);
        }
        
        // Spawn grass
        if (config.SpawnGrass && data.grass?.instances != null && GrassContainer != null)
        {
            result.GrassSpawned = SpawnFloraSet(
                data.grass.instances, 
                floraKindLookup, 
                GrassContainer, 
                config, 
                "grass",
                result);
        }
        
        return result;
    }
    
    private int SpawnFloraSet(
        List<MBFloraGeneratorHelper.FloraInstanceData> instances,
        Dictionary<int, MBFloraGeneratorHelper.FloraKindData> kindLookup,
        Transform parent,
        MBFloraGeneratorHelper.FloraSpawnConfig config,
        string floraType,
        MBFloraGeneratorHelper.FloraSpawnResult result)
    {
        int spawned = 0;
        var prefabCache = new Dictionary<string, GameObject>();
        
        foreach (var instance in instances)
        {
            // Get flora kind info
            string floraId = instance.kindId;
            if (string.IsNullOrEmpty(floraId) && kindLookup.TryGetValue(instance.kind, out var kindData))
            {
                floraId = kindData.id;
            }
            
            if (string.IsNullOrEmpty(floraId))
            {
                result.Warnings.Add($"Unknown flora kind index: {instance.kind}");
                continue;
            }
            
            // Try to get prefab from cache or load it
            if (!prefabCache.TryGetValue(floraId, out var prefab))
            {
                prefab = LoadFloraPrefab(floraId, config.ModuleId);
                prefabCache[floraId] = prefab;
            }
            
            // Calculate position - M&B to Unity coordinate conversion
            Vector3 position = new Vector3(
                instance.position[0],
                instance.position[2], // M&B Z -> Unity Y
                instance.position[1]  // M&B Y -> Unity Z
            );
            
            // Apply terrain height if configured
            if (config.ApplyTerrainHeight && config.TargetTerrain != null)
            {
                Vector3 worldPos = parent.TransformPoint(position) + config.PositionOffset;
                float terrainHeight = config.TargetTerrain.SampleHeight(worldPos);
                terrainHeight += config.TargetTerrain.transform.position.y;
                position.y = terrainHeight - parent.position.y;
            }
            
            // Calculate rotation
            Quaternion rotation = ConvertMBRotation(instance.rotation);
            
            // Calculate scale - M&B to Unity
            Vector3 scale = instance.scale != null && instance.scale.Length >= 3
                ? new Vector3(
                    instance.scale[0],
                    instance.scale[2], // M&B Z -> Unity Y
                    instance.scale[1]) // M&B Y -> Unity Z
                : Vector3.one;
            
            GameObject floraObj;
            
            if (prefab != null)
            {
#if UNITY_EDITOR
                floraObj = UnityEditor.PrefabUtility.InstantiatePrefab(prefab) as GameObject;
                if (floraObj == null)
                {
                    floraObj = Instantiate(prefab);
                }
#else
                floraObj = Instantiate(prefab);
#endif
            }
            else
            {
                floraObj = CreatePlaceholder(floraId, floraType);
                result.MissingPrefabs++;
                if (!result.MissingPrefabNames.Contains(floraId))
                {
                    result.MissingPrefabNames.Add(floraId);
                }
            }
            
            // Apply transform
            floraObj.transform.SetParent(parent, false);
            floraObj.transform.localPosition = position;
            floraObj.transform.localRotation = rotation;
            floraObj.transform.localScale = scale;
            
            // Set name and tag
            floraObj.name = $"{floraId}_{spawned}";
            try { floraObj.tag = "plant"; } catch { }
            
            // Add metadata
            var metadata = floraObj.AddComponent<MBFloraInstance>();
            metadata.FloraKindId = floraId;
            metadata.FloraKindIndex = instance.kind;
            metadata.VariantIndex = instance.variant;
            if (instance.face != null && instance.face.Length >= 3)
            {
                metadata.FaceX = instance.face[0];
                metadata.FaceY = instance.face[1];
                metadata.FaceTriangle = instance.face[2];
            }
            
            spawned++;
        }
        
        return spawned;
    }
    
    private GameObject LoadFloraPrefab(string floraId, string moduleId)
    {
        GameObject prefab = null;
        
        // Try MBEditorUtility if available
        try
        {
            prefab = MBEditorUtility.GetFloraPrefab(moduleId, floraId);
            
            if (prefab == null && moduleId != "Native")
            {
                prefab = MBEditorUtility.GetFloraPrefab("Native", floraId);
            }
        }
        catch
        {
            // MBEditorUtility not available, try direct resource loading
        }
        
        // Fallback: try Resources
        if (prefab == null)
        {
            prefab = Resources.Load<GameObject>($"Flora/{floraId}");
        }
        
        if (prefab == null)
        {
            prefab = Resources.Load<GameObject>($"Prefabs/Flora/{floraId}");
        }
        
        return prefab;
    }
    
    private Quaternion ConvertMBRotation(float[] mbQuat)
    {
        if (mbQuat == null || mbQuat.Length < 4)
            return Quaternion.identity;
        
        // M&B stores rotation as quaternion with Z-up convention
        // The flora rotation is typically just Y-axis rotation (around M&B's Z axis)
        
        // Extract the rotation angle from the quaternion
        // For a Z-axis rotation: quat = (0, sin(a/2), 0, cos(a/2)) in M&B
        float halfAngle = Mathf.Asin(Mathf.Clamp(mbQuat[1], -1f, 1f));
        float angle = halfAngle * 2f;
        
        // Convert to Unity Y-axis rotation
        return Quaternion.Euler(0f, angle * Mathf.Rad2Deg, 0f);
    }
    
    private GameObject CreatePlaceholder(string floraId, string floraType)
    {
        GameObject placeholder = new GameObject($"MISSING_{floraId}");
        
        PrimitiveType primType = floraType switch
        {
            "tree" => PrimitiveType.Capsule,
            "rock" => PrimitiveType.Sphere,
            _ => PrimitiveType.Cube
        };
        
        GameObject visual = GameObject.CreatePrimitive(primType);
        visual.transform.SetParent(placeholder.transform);
        visual.transform.localPosition = floraType == "tree" ? Vector3.up : Vector3.up * 0.25f;
        visual.transform.localScale = floraType == "tree" 
            ? new Vector3(0.5f, 2f, 0.5f) 
            : Vector3.one * 0.5f;
        
        var renderer = visual.GetComponent<Renderer>();
        if (renderer != null)
        {
            var mat = new Material(Shader.Find("Standard"));
            mat.color = floraType switch
            {
                "tree" => new Color(0.2f, 0.6f, 0.2f),
                "rock" => Color.gray,
                _ => new Color(0.5f, 0.7f, 0.3f)
            };
            renderer.material = mat;
        }
        
        // Remove collider
        var collider = visual.GetComponent<Collider>();
        if (collider != null)
        {
            DestroyImmediate(collider);
        }
        
        return placeholder;
    }
    
    #endregion
    
    #region Context Menu
    
    [ContextMenu("Load Flora Data")]
    private void ContextLoadData()
    {
        LoadData();
    }
    
    [ContextMenu("Spawn All Flora")]
    private void ContextSpawnFlora()
    {
        SpawnFlora();
    }
    
    [ContextMenu("Clear All Flora")]
    private void ContextClearFlora()
    {
        ClearFlora();
    }
    
    [ContextMenu("Print Stats")]
    private void ContextPrintStats()
    {
        Debug.Log(GetStats());
    }
    
    #endregion
}