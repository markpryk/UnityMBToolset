using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Database of all model resources for a module.
/// Stores mesh, material, LOD, and collision data extracted from BRF files.
/// </summary>
[Serializable]
[CreateAssetMenu(fileName = "ModelsDataBase", menuName = "MBToolset/ModelsDataBase")]
public class ModModelsDataBase : ScriptableObject
{
    #region Serialized Data

    [SerializeField]
    private List<MBResourcesModel> _modResourcesModels = new();

    #endregion

    #region Runtime Lookups

    [NonSerialized] private Dictionary<string, MBResourcesModel> _modelsById;
    [NonSerialized] private Dictionary<string, MBResourcesModel> _modelsByMesh;
    [NonSerialized] private bool _lookupsBuilt;

    #endregion

    #region Properties

    public List<MBResourcesModel> ModResourcesModels => _modResourcesModels;

    public int Count => _modResourcesModels?.Count ?? 0;

    #endregion

    #region Unity Lifecycle

    private void OnEnable()
    {
        if (_modResourcesModels == null)
            _modResourcesModels = new List<MBResourcesModel>();

        BuildLookups();
    }

    private void OnValidate()
    {
        _lookupsBuilt = false;
    }

    #endregion

    #region Lookup Management

    /// <summary>
    /// Rebuild all lookup dictionaries.
    /// </summary>
    public void BuildLookups()
    {
        _modelsById = new Dictionary<string, MBResourcesModel>(StringComparer.OrdinalIgnoreCase);
        _modelsByMesh = new Dictionary<string, MBResourcesModel>(StringComparer.OrdinalIgnoreCase);

        if (_modResourcesModels == null)
        {
            _lookupsBuilt = true;
            return;
        }

        foreach (var model in _modResourcesModels)
        {
            if (string.IsNullOrEmpty(model.GroupID))
                continue;

            _modelsById[model.GroupID] = model;

            // Index by mesh names
            if (model.Meshes != null)
            {
                foreach (var mesh in model.Meshes)
                {
                    if (mesh != null && !_modelsByMesh.ContainsKey(mesh.name))
                        _modelsByMesh[mesh.name] = model;
                }
            }

            // Also index mesh entries by name
            if (model.MeshEntries != null)
            {
                foreach (var entry in model.MeshEntries)
                {
                    if (!string.IsNullOrEmpty(entry.MeshName) && !_modelsByMesh.ContainsKey(entry.MeshName))
                        _modelsByMesh[entry.MeshName] = model;
                }
            }
        }

        _lookupsBuilt = true;
    }

    private void EnsureLookups()
    {
        if (!_lookupsBuilt || _modelsById == null)
            BuildLookups();
    }

    #endregion

    #region Query Methods

    /// <summary>
    /// Find a model by its GroupID.
    /// </summary>
    public MBResourcesModel FindById(string groupId)
    {
        if (string.IsNullOrEmpty(groupId))
            return null;

        EnsureLookups();
        _modelsById.TryGetValue(groupId, out var model);
        return model;
    }

    /// <summary>
    /// Find a model that contains a specific mesh.
    /// </summary>
    public MBResourcesModel FindByMesh(string meshName)
    {
        if (string.IsNullOrEmpty(meshName))
            return null;

        EnsureLookups();

        // Try direct lookup first
        if (_modelsByMesh.TryGetValue(meshName, out var model))
            return model;

        // Try parsing mesh name (remove .1, .lod suffixes)
        string baseName = MBEditorUtility.ParseMeshName(meshName)[0];
        _modelsByMesh.TryGetValue(baseName, out model);
        return model;
    }

    /// <summary>
    /// Find the prefab/model ID for a mesh name.
    /// </summary>
    public string FindPrefabByMesh(string meshName)
    {
        return FindByMesh(meshName)?.GroupID;
    }

    /// <summary>
    /// Get all models from a specific BRF file.
    /// </summary>
    public IEnumerable<MBResourcesModel> GetModelsByBRF(string brfName)
    {
        return _modResourcesModels.Where(m =>
            m.BRFFileName.Equals(brfName, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Check if a model with the given ID exists.
    /// </summary>
    public bool Contains(string groupId)
    {
        EnsureLookups();
        return _modelsById.ContainsKey(groupId);
    }

    #endregion

    #region Modification Methods

    /// <summary>
    /// Add a new model to the database.
    /// </summary>
    public void AddModelData(MBResourcesModel model)
    {
        if (model == null || string.IsNullOrEmpty(model.GroupID))
            return;

        EnsureLookups();

        // Update existing or add new
        if (_modelsById.TryGetValue(model.GroupID, out var existing))
        {
            int index = _modResourcesModels.IndexOf(existing);
            if (index >= 0)
                _modResourcesModels[index] = model;
        }
        else
        {
            _modResourcesModels.Add(model);
        }

        _modelsById[model.GroupID] = model;

        // Update mesh lookups
        if (model.Meshes != null)
        {
            foreach (var mesh in model.Meshes)
            {
                if (mesh != null)
                    _modelsByMesh[mesh.name] = model;
            }
        }
    }

    /// <summary>
    /// Remove a model from the database.
    /// </summary>
    public bool RemoveModel(string groupId)
    {
        var model = FindById(groupId);
        if (model == null)
            return false;

        _modResourcesModels.Remove(model);
        _modelsById.Remove(groupId);

        // Remove mesh lookups
        if (model.Meshes != null)
        {
            foreach (var mesh in model.Meshes)
            {
                if (mesh != null && _modelsByMesh.TryGetValue(mesh.name, out var m) && m == model)
                    _modelsByMesh.Remove(mesh.name);
            }
        }

        return true;
    }

    /// <summary>
    /// Clear all model data.
    /// </summary>
    public void Clear()
    {
        _modResourcesModels.Clear();
        _modelsById?.Clear();
        _modelsByMesh?.Clear();
    }

    /// <summary>
    /// Sort models alphabetically by GroupID.
    /// </summary>
    public void SortAlphabetically()
    {
        _modResourcesModels = _modResourcesModels
            .OrderBy(m => m.GroupID, StringComparer.OrdinalIgnoreCase)
            .ToList();

#if UNITY_EDITOR
        EditorUtility.SetDirty(this);
#endif
    }

    #endregion

    #region Validation

    /// <summary>
    /// Validate all model entries and report issues.
    /// </summary>
    public List<string> Validate()
    {
        var issues = new List<string>();

        var seenIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var model in _modResourcesModels)
        {
            if (string.IsNullOrEmpty(model.GroupID))
            {
                issues.Add("Model with empty GroupID found");
                continue;
            }

            if (!seenIds.Add(model.GroupID))
            {
                issues.Add($"Duplicate GroupID: {model.GroupID}");
            }

            if (model.Meshes == null || model.Meshes.Length == 0)
            {
                issues.Add($"Model '{model.GroupID}' has no meshes");
            }
            else
            {
                for (int i = 0; i < model.Meshes.Length; i++)
                {
                    if (model.Meshes[i] == null)
                        issues.Add($"Model '{model.GroupID}' has null mesh at index {i}");
                }
            }

            if (model.ModelMaterials != null && model.Meshes != null &&
                model.ModelMaterials.Length != model.Meshes.Length)
            {
                issues.Add($"Model '{model.GroupID}' has mismatched mesh/material counts");
            }
        }

        return issues;
    }

    #endregion

    #region Statistics

    /// <summary>
    /// Get statistics about the database contents.
    /// </summary>
    public ModelDatabaseStats GetStats()
    {
        var stats = new ModelDatabaseStats
        {
            TotalModels = _modResourcesModels.Count,
            BRFFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        };

        foreach (var model in _modResourcesModels)
        {
            if (!string.IsNullOrEmpty(model.BRFFileName))
                stats.BRFFiles.Add(model.BRFFileName);

            stats.TotalMeshes += model.Meshes?.Length ?? 0;
            stats.TotalLodMeshes += model.LodMeshes?.Length ?? 0;

            if (model.ModelMaterials != null)
                stats.ModelsWithMaterials += model.ModelMaterials.Any(m => m != null) ? 1 : 0;

            if (model.HasCollision)
                stats.ModelsWithCollision++;
        }

        return stats;
    }

    public struct ModelDatabaseStats
    {
        public int TotalModels;
        public int TotalMeshes;
        public int TotalLodMeshes;
        public int ModelsWithMaterials;
        public int ModelsWithCollision;
        public HashSet<string> BRFFiles;

        public float MaterialCoverage => TotalModels > 0 ? (float)ModelsWithMaterials / TotalModels * 100f : 0f;
        public float CollisionCoverage => TotalModels > 0 ? (float)ModelsWithCollision / TotalModels * 100f : 0f;
    }

    #endregion
}

/// <summary>
/// Complete model resource data extracted from BRF files.
/// Contains all information needed to create an MBModel prefab.
/// </summary>
[Serializable]
public class MBResourcesModel
{
    #region Identity

    [Header("Identity")]
    public string GroupID;
    public string BRFFileName;

    #endregion

    #region Mesh Data (Legacy - for backward compatibility)

    [Header("Mesh Data")]
    public Mesh[] Meshes = Array.Empty<Mesh>();
    public Mesh[] LodMeshes = Array.Empty<Mesh>();
    public Material[] ModelMaterials = Array.Empty<Material>();

    #endregion

    #region Extended Mesh Data

    [Header("Extended Data")]
    [SerializeField]
    private List<MeshEntry> _meshEntries = new();

    public List<MeshEntry> MeshEntries
    {
        get => _meshEntries;
        set => _meshEntries = value ?? new List<MeshEntry>();
    }

    #endregion

    #region Collision Data

    [Header("Collision")]
    public string CollisionBodyName;
    public bool HasCollision;

    [SerializeField]
    private List<CollisionPrimitiveData> _collisionPrimitives = new();

    public List<CollisionPrimitiveData> CollisionPrimitives
    {
        get => _collisionPrimitives;
        set => _collisionPrimitives = value ?? new List<CollisionPrimitiveData>();
    }

    #endregion

    #region BRF Metadata

    [Header("BRF Flags")]
    public long Flags;

    #endregion

    #region Constructors

    public MBResourcesModel()
    {
        GroupID = string.Empty;
        BRFFileName = string.Empty;
        Meshes = Array.Empty<Mesh>();
        LodMeshes = Array.Empty<Mesh>();
        ModelMaterials = Array.Empty<Material>();
        _meshEntries = new List<MeshEntry>();
        _collisionPrimitives = new List<CollisionPrimitiveData>();
    }

    public MBResourcesModel(string groupId, string brfName) : this()
    {
        GroupID = groupId;
        BRFFileName = brfName;
    }

    #endregion

    #region Mesh Entry Management

    /// <summary>
    /// Add a mesh with its associated data.
    /// </summary>
    public void AddMesh(Mesh mesh, Material material, string materialName, long materialFlags, int lodLevel = 0)
    {
        var entry = new MeshEntry
        {
            MeshName = mesh?.name ?? string.Empty,
            Mesh = mesh,
            Material = material,
            MaterialName = materialName,
            MaterialFlags = materialFlags,
            LodLevel = lodLevel
        };

        _meshEntries.Add(entry);

        // Also update legacy arrays
        SyncLegacyArrays();
    }

    /// <summary>
    /// Sync MeshEntries to legacy Meshes/LodMeshes/ModelMaterials arrays.
    /// </summary>
    public void SyncLegacyArrays()
    {
        var baseMeshes = _meshEntries.Where(e => e.LodLevel == 0).ToList();
        var lodMeshes = _meshEntries.Where(e => e.LodLevel > 0).OrderBy(e => e.LodLevel).ToList();

        Meshes = baseMeshes.Select(e => e.Mesh).ToArray();
        ModelMaterials = baseMeshes.Select(e => e.Material).ToArray();
        LodMeshes = lodMeshes.Select(e => e.Mesh).ToArray();
    }

    /// <summary>
    /// Populate MeshEntries from legacy arrays.
    /// </summary>
    public void PopulateFromLegacyArrays()
    {
        _meshEntries.Clear();

        if (Meshes != null)
        {
            for (int i = 0; i < Meshes.Length; i++)
            {
                var mesh = Meshes[i];
                var material = ModelMaterials != null && i < ModelMaterials.Length
                    ? ModelMaterials[i]
                    : null;

                _meshEntries.Add(new MeshEntry
                {
                    MeshName = mesh?.name ?? string.Empty,
                    Mesh = mesh,
                    Material = material,
                    LodLevel = 0
                });
            }
        }

        if (LodMeshes != null)
        {
            for (int i = 0; i < LodMeshes.Length; i++)
            {
                var mesh = LodMeshes[i];
                _meshEntries.Add(new MeshEntry
                {
                    MeshName = mesh?.name ?? string.Empty,
                    Mesh = mesh,
                    Material = ModelMaterials?.FirstOrDefault(),
                    LodLevel = i + 1
                });
            }
        }
    }

    #endregion

    #region Collision Management

    /// <summary>
    /// Add a collision primitive.
    /// </summary>
    public void AddCollisionPrimitive(CollisionPrimitiveData primitive)
    {
        _collisionPrimitives.Add(primitive);
        HasCollision = true;
    }

    /// <summary>
    /// Set collision data from BRF body.
    /// </summary>
    public void SetCollisionFromBody(string bodyName, List<CollisionPrimitiveData> primitives)
    {
        CollisionBodyName = bodyName;
        _collisionPrimitives = primitives ?? new List<CollisionPrimitiveData>();
        HasCollision = _collisionPrimitives.Count > 0;
    }

    #endregion

    #region Utility Methods

    /// <summary>
    /// Get mesh name from mesh asset.
    /// </summary>
    public string GetMeshName(Mesh mesh)
    {
        if (mesh == null)
            return string.Empty;

#if UNITY_EDITOR
        string path = AssetDatabase.GetAssetPath(mesh);
        if (!string.IsNullOrEmpty(path))
            return Path.GetFileNameWithoutExtension(path);
#endif

        return mesh.name;
    }

    /// <summary>
    /// Get the material for a specific mesh.
    /// </summary>
    public Material GetMaterialForMesh(Mesh mesh)
    {
        if (mesh == null)
            return null;

        // Try MeshEntries first
        var entry = _meshEntries.Find(e => e.Mesh == mesh || e.MeshName == mesh.name);
        if (entry != null)
            return entry.Material;

        // Fallback to legacy arrays
        if (Meshes != null && ModelMaterials != null)
        {
            int index = Array.IndexOf(Meshes, mesh);
            if (index >= 0 && index < ModelMaterials.Length)
                return ModelMaterials[index];
        }

        return null;
    }

    /// <summary>
    /// Get material flags for a specific mesh.
    /// </summary>
    public long GetMaterialFlagsForMesh(Mesh mesh)
    {
        var entry = _meshEntries.Find(e => e.Mesh == mesh || e.MeshName == mesh.name);
        return entry?.MaterialFlags ?? 0;
    }

    /// <summary>
    /// Validate model data.
    /// </summary>
    public bool Validate(out string error)
    {
        error = null;

        if (string.IsNullOrEmpty(GroupID))
        {
            error = "GroupID is empty";
            return false;
        }

        if ((Meshes == null || Meshes.Length == 0) &&
            (_meshEntries == null || _meshEntries.Count == 0))
        {
            error = "No meshes defined";
            return false;
        }

        return true;
    }

    #endregion
}

/// <summary>
/// Extended mesh entry with full BRF metadata.
/// </summary>
[Serializable]
public class MeshEntry
{
    public string MeshName;
    public Mesh Mesh;
    public Material Material;
    public string MaterialName;
    public long MaterialFlags;
    public int LodLevel;

    /// <summary>
    /// Get the blend mode from material flags.
    /// </summary>
    public MountAndBlade.Data.BrfBlendMode BlendMode =>
        MountAndBlade.Data.BrfFlagDecoder.GetBlendMode(MaterialFlags);

    /// <summary>
    /// Get the alpha test reference value.
    /// </summary>
    public float AlphaTestRef =>
        MountAndBlade.Data.BrfFlagDecoder.GetAlphaTestRef(MaterialFlags);

    /// <summary>
    /// Get the render order.
    /// </summary>
    public int RenderOrder =>
        MountAndBlade.Data.BrfFlagDecoder.GetRenderOrder(MaterialFlags);
}

/// <summary>
/// Serializable collision primitive data.
/// </summary>
[Serializable]
public class CollisionPrimitiveData
{
    public enum PrimitiveType
    {
        Sphere,
        Capsule,
        Box,
        Manifold
    }

    public PrimitiveType Type;
    public Vector3 Center;
    public Vector3 Point1;  // Capsule bottom
    public Vector3 Point2;  // Capsule top
    public Vector3 Size;    // Box dimensions
    public float Radius;

    /// <summary>
    /// Create a sphere primitive.
    /// </summary>
    public static CollisionPrimitiveData Sphere(Vector3 center, float radius)
    {
        return new CollisionPrimitiveData
        {
            Type = PrimitiveType.Sphere,
            Center = center,
            Radius = radius
        };
    }

    /// <summary>
    /// Create a capsule primitive.
    /// </summary>
    public static CollisionPrimitiveData Capsule(Vector3 p1, Vector3 p2, float radius)
    {
        return new CollisionPrimitiveData
        {
            Type = PrimitiveType.Capsule,
            Point1 = p1,
            Point2 = p2,
            Center = (p1 + p2) * 0.5f,
            Radius = radius
        };
    }

    /// <summary>
    /// Create a box primitive.
    /// </summary>
    public static CollisionPrimitiveData Box(Vector3 center, Vector3 size)
    {
        return new CollisionPrimitiveData
        {
            Type = PrimitiveType.Box,
            Center = center,
            Size = size
        };
    }
}