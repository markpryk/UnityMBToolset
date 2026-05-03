using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEditor;

/// <summary>
/// Static helper class for M&B Warband flora generation.
/// Provides API for loading flora data from JSON and spawning flora objects.
/// </summary>
public static class MBFloraGeneratorHelper
{
    #region Data Structures
    
    /// <summary>
    /// Flora instance data from JSON export
    /// </summary>
    [Serializable]
    public class FloraInstanceData
    {
        public int kind;
        public string kindId;
        public int variant;
        public string meshName;
        public int[] face;           // [faceX, faceY, triangle]
        public float[] position;     // [x, y, z]
        public float[] rotation;     // [x, y, z, w] quaternion
        public float[] scale;        // [x, y, z]
    }
    
    /// <summary>
    /// Flora kind definition from JSON
    /// </summary>
    [Serializable]
    public class FloraKindData
    {
        public int index;
        public string id;
        public long flags;
        public int density;
        public int meshCount;
        public string[] meshes;
    }
    
    /// <summary>
    /// Flora set (trees, rocks, or grass)
    /// </summary>
    [Serializable]
    public class FloraSetData
    {
        public int count;
        public List<FloraInstanceData> instances;
    }
    
    /// <summary>
    /// Complete flora export data
    /// </summary>
    [Serializable]
    public class FloraExportData
    {
        public int version;
        public string terrainCode;
        public int regionType;
        public float vegetation;
        public int disableGrass;
        public float[] terrainSize;
        public int[] numFaces;
        public float cellSize;
        public List<FloraKindData> floraKinds;
        public FloraSetData trees;
        public FloraSetData rocks;
        public FloraSetData grass;
    }
    
    /// <summary>
    /// Result of flora spawning operation
    /// </summary>
    public class FloraSpawnResult
    {
        public int TreesSpawned;
        public int RocksSpawned;
        public int GrassSpawned;
        public int TotalSpawned => TreesSpawned + RocksSpawned + GrassSpawned;
        public int MissingPrefabs;
        public List<string> MissingPrefabNames = new List<string>();
        public List<string> Warnings = new List<string>();
    }
    
    #endregion
    
    #region Configuration
    
    /// <summary>
    /// Configuration for flora spawning
    /// </summary>
    public class FloraSpawnConfig
    {
        public bool SpawnTrees = true;
        public bool SpawnRocks = true;
        public bool SpawnGrass = false; // Grass is often handled differently
        public string ModuleId = "Native";
        public Transform ParentTransform = null;
        public Vector3 PositionOffset = Vector3.zero;
        public bool ApplyTerrainHeight = false;
        public Terrain TargetTerrain = null;
    }
    
    #endregion
    
    #region Public API
    
    /// <summary>
    /// Load flora data from JSON file
    /// </summary>
    public static FloraExportData LoadFloraData(string jsonPath)
    {
        if (!File.Exists(jsonPath))
        {
            Debug.LogError($"Flora JSON file not found: {jsonPath}");
            return null;
        }
        
        try
        {
            string jsonContent = File.ReadAllText(jsonPath);
            var data = JsonConvert.DeserializeObject<FloraExportData>(jsonContent);
            
            Debug.Log($"Loaded flora data: {data.trees?.count ?? 0} trees, " +
                      $"{data.rocks?.count ?? 0} rocks, {data.grass?.count ?? 0} grass");
            
            return data;
        }
        catch (Exception ex)
        {
            Debug.LogError($"Error parsing flora JSON: {ex.Message}");
            return null;
        }
    }
    
    /// <summary>
    /// Spawn all flora from loaded data
    /// </summary>
    public static FloraSpawnResult SpawnFlora(FloraExportData data, FloraSpawnConfig config = null)
    {
        if (data == null)
        {
            Debug.LogError("Flora data is null");
            return new FloraSpawnResult();
        }
        
        config ??= new FloraSpawnConfig();
        var result = new FloraSpawnResult();
        
        // Create parent container
        GameObject floraRoot = config.ParentTransform != null 
            ? config.ParentTransform.gameObject 
            : new GameObject("GeneratedFlora");
        
        if (config.ParentTransform == null)
        {
            floraRoot.transform.position = config.PositionOffset;
        }
        
        // Create category containers
        GameObject treesContainer = null;
        GameObject rocksContainer = null;
        GameObject grassContainer = null;
        
        if (config.SpawnTrees && data.trees?.instances != null)
        {
            treesContainer = new GameObject("Trees");
            treesContainer.transform.SetParent(floraRoot.transform, false);
        }
        
        if (config.SpawnRocks && data.rocks?.instances != null)
        {
            rocksContainer = new GameObject("Rocks");
            rocksContainer.transform.SetParent(floraRoot.transform, false);
        }
        
        if (config.SpawnGrass && data.grass?.instances != null)
        {
            grassContainer = new GameObject("Grass");
            grassContainer.transform.SetParent(floraRoot.transform, false);
        }
        
        // Build flora kind lookup
        var floraKindLookup = new Dictionary<int, FloraKindData>();
        if (data.floraKinds != null)
        {
            foreach (var kind in data.floraKinds)
            {
                floraKindLookup[kind.index] = kind;
            }
        }
        
        // Spawn trees
        if (config.SpawnTrees && data.trees?.instances != null)
        {
            result.TreesSpawned = SpawnFloraSet(
                data.trees.instances, 
                floraKindLookup, 
                treesContainer.transform, 
                config, 
                "tree",
                result);
        }
        
        // Spawn rocks
        if (config.SpawnRocks && data.rocks?.instances != null)
        {
            result.RocksSpawned = SpawnFloraSet(
                data.rocks.instances, 
                floraKindLookup, 
                rocksContainer.transform, 
                config, 
                "rock",
                result);
        }
        
        // Spawn grass
        if (config.SpawnGrass && data.grass?.instances != null)
        {
            result.GrassSpawned = SpawnFloraSet(
                data.grass.instances, 
                floraKindLookup, 
                grassContainer.transform, 
                config, 
                "grass",
                result);
        }
        
        Debug.Log($"Flora spawning complete: {result.TotalSpawned} objects " +
                  $"({result.TreesSpawned} trees, {result.RocksSpawned} rocks, {result.GrassSpawned} grass)");
        
        if (result.MissingPrefabs > 0)
        {
            Debug.LogWarning($"Missing prefabs: {result.MissingPrefabs} - {string.Join(", ", result.MissingPrefabNames.Distinct())}");
        }
        
        return result;
    }
    
    /// <summary>
    /// Spawn flora from JSON file path
    /// </summary>
    public static FloraSpawnResult SpawnFloraFromJson(string jsonPath, FloraSpawnConfig config = null)
    {
        var data = LoadFloraData(jsonPath);
        if (data == null)
        {
            return new FloraSpawnResult();
        }
        
        return SpawnFlora(data, config);
    }
    
    /// <summary>
    /// Clear all generated flora
    /// </summary>
    public static void ClearGeneratedFlora(Transform parent = null)
    {
        if (parent != null)
        {
            // Clear children
            while (parent.childCount > 0)
            {
                GameObject.DestroyImmediate(parent.GetChild(0).gameObject);
            }
        }
        else
        {
            // Find and destroy GeneratedFlora object
            var floraRoot = GameObject.Find("GeneratedFlora");
            if (floraRoot != null)
            {
                GameObject.DestroyImmediate(floraRoot);
            }
            
            // Also clear by tag
            ClearFloraByTag("plant");
        }
    }
    
    /// <summary>
    /// Clear flora objects by tag
    /// </summary>
    public static void ClearFloraByTag(string tag)
    {
        var objects = GameObject.FindGameObjectsWithTag(tag);
        foreach (var obj in objects)
        {
            GameObject.DestroyImmediate(obj);
        }
    }
    
    #endregion
    
    #region Internal Methods
    
    private static int SpawnFloraSet(
        List<FloraInstanceData> instances, 
        Dictionary<int, FloraKindData> kindLookup,
        Transform parent,
        FloraSpawnConfig config,
        string floraType,
        FloraSpawnResult result)
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
            
            // Calculate position
            Vector3 position = new Vector3(
                instance.position[0],
                instance.position[2], // M&B Z -> Unity Y
                instance.position[1]  // M&B Y -> Unity Z
            );
            
            // Apply terrain height if configured
            if (config.ApplyTerrainHeight && config.TargetTerrain != null)
            {
                float terrainHeight = config.TargetTerrain.SampleHeight(position + config.PositionOffset);
                position.y = terrainHeight;
            }
            
            // Calculate rotation (M&B quaternion to Unity)
            Quaternion rotation = ConvertMBQuaternion(instance.rotation);
            
            // Calculate scale
            Vector3 scale = new Vector3(
                instance.scale[0],
                instance.scale[2], // M&B Z -> Unity Y
                instance.scale[1]  // M&B Y -> Unity Z
            );
            
            GameObject floraObj;
            
            if (prefab != null)
            {
                // Instantiate prefab
                floraObj = PrefabUtility.InstantiatePrefab(prefab) as GameObject;
                if (floraObj == null)
                {
                    floraObj = GameObject.Instantiate(prefab);
                }
            }
            else
            {
                // Create placeholder
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
            
            // Add metadata component
            var metadata = floraObj.AddComponent<MBFloraInstance>();
            metadata.FloraKindId = floraId;
            metadata.FloraKindIndex = instance.kind;
            metadata.VariantIndex = instance.variant;
            metadata.FaceX = instance.face?[0] ?? 0;
            metadata.FaceY = instance.face?[1] ?? 0;
            metadata.FaceTriangle = instance.face?[2] ?? 0;
            
            spawned++;
        }
        
        return spawned;
    }
    
    private static GameObject LoadFloraPrefab(string floraId, string moduleId)
    {
        // Try current module first
        var prefab = MBEditorUtility.GetFloraPrefab(moduleId, floraId);
        
        // Try Native module
        if (prefab == null && moduleId != "Native")
        {
            prefab = MBEditorUtility.GetFloraPrefab("Native", floraId);
        }
        
        // Try by mesh name variations
        if (prefab == null)
        {
            string[] variations = new[]
            {
                floraId,
                $"flora_{floraId}",
                floraId.Replace("_", ""),
                floraId.ToLowerInvariant()
            };
            
            foreach (var variation in variations)
            {
                prefab = MBEditorUtility.GetFloraPrefab(moduleId, variation);
                if (prefab != null) break;
                
                prefab = MBEditorUtility.GetFloraPrefab("Native", variation);
                if (prefab != null) break;
            }
        }
        
        return prefab;
    }
    
    private static Quaternion ConvertMBQuaternion(float[] mbQuat)
    {
        if (mbQuat == null || mbQuat.Length < 4)
            return Quaternion.identity;
        
        // M&B uses Z-up, Unity uses Y-up
        // M&B rotation is around Z axis stored as (x=0, y=sin(a/2), z=0, w=cos(a/2))
        // This needs to become Unity Y-axis rotation
        
        // The stored quaternion represents rotation around M&B's Z axis (up)
        // In Unity, up is Y, so we need to convert
        
        float mbX = mbQuat[0];
        float mbY = mbQuat[1]; // This is actually the Z-rotation component in M&B
        float mbZ = mbQuat[2];
        float mbW = mbQuat[3];
        
        // Convert M&B Z-axis rotation to Unity Y-axis rotation
        // Swap Y and Z, and adjust for coordinate system
        Quaternion converted = new Quaternion(mbX, mbZ, mbY, mbW);
        
        // Apply additional rotation to correct for coordinate system difference
        // M&B: X-right, Y-forward, Z-up
        // Unity: X-right, Y-up, Z-forward
        Quaternion coordFix = Quaternion.Euler(-90f, 0f, 0f);
        
        return coordFix * converted;
    }
    
    private static GameObject CreatePlaceholder(string floraId, string floraType)
    {
        GameObject placeholder = new GameObject($"MISSING_{floraId}");
        
        // Create visual indicator
        GameObject visual = GameObject.CreatePrimitive(
            floraType == "tree" ? PrimitiveType.Capsule :
            floraType == "rock" ? PrimitiveType.Sphere :
            PrimitiveType.Cube);
        
        visual.transform.SetParent(placeholder.transform);
        visual.transform.localPosition = Vector3.up * 0.5f;
        visual.transform.localScale = floraType == "tree" 
            ? new Vector3(0.5f, 2f, 0.5f) 
            : Vector3.one * 0.5f;
        
        // Set color based on type
        var renderer = visual.GetComponent<Renderer>();
        if (renderer != null)
        {
            var material = new Material(Shader.Find("Standard"));
            material.color = floraType == "tree" ? Color.green :
                            floraType == "rock" ? Color.gray :
                            new Color(0.5f, 0.8f, 0.3f);
            renderer.material = material;
        }
        
        // Remove collider from placeholder
        var collider = visual.GetComponent<Collider>();
        if (collider != null)
        {
            GameObject.DestroyImmediate(collider);
        }
        
        return placeholder;
    }
    
    #endregion
    
    #region Utility Methods
    
    /// <summary>
    /// Get statistics from flora data without spawning
    /// </summary>
    public static string GetFloraStats(FloraExportData data)
    {
        if (data == null) return "No data";
        
        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"Terrain Code: {data.terrainCode}");
        sb.AppendLine($"Region Type: {data.regionType}");
        sb.AppendLine($"Vegetation: {data.vegetation:F2}");
        sb.AppendLine($"Terrain Size: {data.terrainSize?[0] ?? 0} x {data.terrainSize?[1] ?? 0}");
        sb.AppendLine($"Cell Size: {data.cellSize}");
        sb.AppendLine($"Flora Kinds: {data.floraKinds?.Count ?? 0}");
        sb.AppendLine($"Trees: {data.trees?.count ?? 0}");
        sb.AppendLine($"Rocks: {data.rocks?.count ?? 0}");
        sb.AppendLine($"Grass: {data.grass?.count ?? 0}");
        
        // Kind breakdown
        if (data.floraKinds != null && data.floraKinds.Count > 0)
        {
            sb.AppendLine("\nFlora Kinds:");
            foreach (var kind in data.floraKinds.Take(10))
            {
                sb.AppendLine($"  - {kind.id} (density: {kind.density}, meshes: {kind.meshCount})");
            }
            if (data.floraKinds.Count > 10)
            {
                sb.AppendLine($"  ... and {data.floraKinds.Count - 10} more");
            }
        }
        
        return sb.ToString();
    }
    
    /// <summary>
    /// Validate flora JSON structure
    /// </summary>
    public static bool ValidateFloraJson(string jsonPath, out string errorMessage)
    {
        errorMessage = null;
        
        if (!File.Exists(jsonPath))
        {
            errorMessage = "File not found";
            return false;
        }
        
        try
        {
            var data = LoadFloraData(jsonPath);
            if (data == null)
            {
                errorMessage = "Failed to parse JSON";
                return false;
            }
            
            if (data.trees == null && data.rocks == null && data.grass == null)
            {
                errorMessage = "No flora sets found in data";
                return false;
            }
            
            return true;
        }
        catch (Exception ex)
        {
            errorMessage = ex.Message;
            return false;
        }
    }
    
    #endregion
}

/// <summary>
/// Component attached to spawned flora instances for metadata
/// </summary>
public class MBFloraInstance : MonoBehaviour
{
    public string FloraKindId;
    public int FloraKindIndex;
    public int VariantIndex;
    public int FaceX;
    public int FaceY;
    public int FaceTriangle;
}