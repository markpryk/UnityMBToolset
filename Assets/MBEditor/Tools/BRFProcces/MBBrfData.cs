// MBBrfData.cs
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace MountAndBlade.Data
{
    /// <summary>
    /// ScriptableObject representing a single BRF file's complete data.
    /// Created in the Resources folder alongside the BRF's exported content.
    /// </summary>
    [CreateAssetMenu(fileName = "BrfData", menuName = "MBToolset/BRF Data")]
    public class MBBrfData : ScriptableObject
    {
        #region Identity

        [Header("BRF Identity")]
        [SerializeField] private string _brfName;
        [SerializeField] private string _moduleName;
        [SerializeField] private string _folderPath;
        [SerializeField] private string _sourceBrfPath;

        public string BrfName { get => _brfName; set => _brfName = value; }
        public string ModuleName { get => _moduleName; set => _moduleName = value; }
        public string FolderPath { get => _folderPath; set => _folderPath = value; }
        public string SourceBrfPath { get => _sourceBrfPath; set => _sourceBrfPath = value; }

        #endregion

        #region Mesh Data

        [Header("Meshes")]
        [SerializeField] private List<BrfMeshEntry> _meshes = new();

        public List<BrfMeshEntry> Meshes => _meshes;

        public BrfMeshEntry GetMesh(string meshName)
        {
            return _meshes.Find(m => m.Name.Equals(meshName, StringComparison.OrdinalIgnoreCase));
        }

        public IEnumerable<BrfMeshEntry> GetBaseMeshes() =>
            _meshes.Where(m => m.LodLevel == 0 && !m.IsCollisionMesh);

        public IEnumerable<BrfMeshEntry> GetLodMeshes(string baseName) =>
            _meshes.Where(m => m.BaseName.Equals(baseName, StringComparison.OrdinalIgnoreCase) && m.LodLevel > 0);

        #endregion

        #region Material Data

        [Header("Materials")]
        [SerializeField] private List<BrfMaterialEntry> _materials = new();

        public List<BrfMaterialEntry> Materials => _materials;

        public BrfMaterialEntry GetMaterialEntry(string materialName)
        {
            return _materials.Find(m => m.Name.Equals(materialName, StringComparison.OrdinalIgnoreCase));
        }

        public Material GetMaterial(string materialName)
        {
            return GetMaterialEntry(materialName)?.UnityMaterial;
        }

        #endregion

        #region Texture Data

        [Header("Textures")]
        [SerializeField] private List<BrfTextureEntry> _textures = new();

        public List<BrfTextureEntry> Textures => _textures;

        public BrfTextureEntry GetTextureEntry(string textureName)
        {
            return _textures.Find(t => t.Name.Equals(textureName, StringComparison.OrdinalIgnoreCase));
        }

        public Texture2D GetTexture(string textureName)
        {
            return GetTextureEntry(textureName)?.UnityTexture;
        }

        #endregion

        #region Body/Collision Data

        [Header("Collision Bodies")]
        [SerializeField] private List<BrfBodyEntry> _bodies = new();

        public List<BrfBodyEntry> Bodies => _bodies;

        public BrfBodyEntry GetBody(string bodyName)
        {
            return _bodies.Find(b => b.Name.Equals(bodyName, StringComparison.OrdinalIgnoreCase));
        }

        public BrfBodyEntry GetBodyForModel(string modelName)
        {
            return GetBody($"bo_{modelName}") ?? GetBody(modelName);
        }

        #endregion

        #region Skeleton & Animation Data

        [Header("Skeletons & Animations")]
        [SerializeField] private List<BrfSkeletonEntry> _skeletons = new();
        [SerializeField] private List<BrfAnimationEntry> _animations = new();

        public List<BrfSkeletonEntry> Skeletons => _skeletons;
        public List<BrfAnimationEntry> Animations => _animations;

        #endregion

        #region Shader Data

        [Header("Shaders")]
        [SerializeField] private List<BrfShaderEntry> _shaders = new();

        public List<BrfShaderEntry> Shaders => _shaders;

        #endregion

        #region Model Groups

        [Header("Model Groups (Auto-generated)")]
        [SerializeField] private List<BrfModelGroup> _modelGroups = new();

        public List<BrfModelGroup> ModelGroups => _modelGroups;

        public BrfModelGroup GetModelGroup(string groupId)
        {
            return _modelGroups.Find(g => g.GroupID.Equals(groupId, StringComparison.OrdinalIgnoreCase));
        }

        public void RebuildModelGroups()
        {
            _modelGroups.Clear();

            var groups = new Dictionary<string, BrfModelGroup>(StringComparer.OrdinalIgnoreCase);

            foreach (var mesh in _meshes)
            {
                if (mesh.IsCollisionMesh) continue;

                string baseName = mesh.BaseName;

                if (!groups.TryGetValue(baseName, out var group))
                {
                    group = new BrfModelGroup
                    {
                        GroupID = baseName,
                        BrfName = _brfName
                    };
                    groups[baseName] = group;
                }

                if (mesh.LodLevel == 0)
                    group.MeshEntries.Add(mesh);
                else
                    group.LodMeshEntries.Add(mesh);

                if (!string.IsNullOrEmpty(mesh.MaterialName) && !group.MaterialNames.Contains(mesh.MaterialName))
                    group.MaterialNames.Add(mesh.MaterialName);
            }

            foreach (var group in groups.Values)
            {
                group.CollisionBody = GetBodyForModel(group.GroupID);
                _modelGroups.Add(group);
            }

            _modelGroups.Sort((a, b) => string.Compare(a.GroupID, b.GroupID, StringComparison.OrdinalIgnoreCase));
        }

        #endregion

        #region Statistics

        [Header("Statistics")]
        [SerializeField] private BrfStatistics _statistics;

        public BrfStatistics Statistics => _statistics;

        public void UpdateStatistics()
        {
            _statistics = new BrfStatistics
            {
                MeshCount = _meshes.Count,
                MaterialCount = _materials.Count,
                TextureCount = _textures.Count,
                BodyCount = _bodies.Count,
                SkeletonCount = _skeletons.Count,
                AnimationCount = _animations.Count,
                ShaderCount = _shaders.Count,
                ModelGroupCount = _modelGroups.Count,
                BaseMeshCount = _meshes.Count(m => m.LodLevel == 0 && !m.IsCollisionMesh),
                LodMeshCount = _meshes.Count(m => m.LodLevel > 0),
                CollisionMeshCount = _meshes.Count(m => m.IsCollisionMesh),
                MaterialsWithTextures = _materials.Count(m => m.DiffuseATexture != null),
                ModelsWithCollision = _modelGroups.Count(g => g.CollisionBody != null),
                TexturesResolved = _textures.Count(t => t.UnityTexture != null)
            };
        }

        #endregion

        #region Validation

        public List<string> Validate()
        {
            var issues = new List<string>();

            foreach (var mesh in _meshes)
            {
                if (mesh.UnityMesh == null)
                    issues.Add($"Mesh '{mesh.Name}' has no Unity mesh assigned");
            }

            foreach (var mat in _materials)
            {
                if (mat.UnityMaterial == null)
                    issues.Add($"Material '{mat.Name}' has no Unity material assigned");
            }

            foreach (var tex in _textures)
            {
                if (tex.UnityTexture == null)
                    issues.Add($"Texture '{tex.Name}' has no Unity texture assigned");
            }

            foreach (var group in _modelGroups)
            {
                if (group.MeshEntries.Count == 0)
                    issues.Add($"Model group '{group.GroupID}' has no meshes");
            }

            return issues;
        }

        #endregion

        #region Lookups

        [NonSerialized] private Dictionary<string, BrfMeshEntry> _meshLookup;
        [NonSerialized] private Dictionary<string, BrfMaterialEntry> _materialLookup;
        [NonSerialized] private Dictionary<string, BrfTextureEntry> _textureLookup;
        [NonSerialized] private Dictionary<string, BrfModelGroup> _modelGroupLookup;
        [NonSerialized] private bool _lookupsBuilt;

        public void BuildLookups()
        {
            _meshLookup = _meshes
                .GroupBy(m => m.Name, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

            _materialLookup = _materials
                .GroupBy(m => m.Name, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

            _textureLookup = _textures
                .GroupBy(t => t.Name, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

            _modelGroupLookup = _modelGroups
                .GroupBy(g => g.GroupID, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

            _lookupsBuilt = true;
        }

        public BrfMeshEntry GetMeshFast(string meshName)
        {
            if (!_lookupsBuilt) BuildLookups();
            return _meshLookup.TryGetValue(meshName, out var mesh) ? mesh : null;
        }

        public BrfMaterialEntry GetMaterialFast(string materialName)
        {
            if (!_lookupsBuilt) BuildLookups();
            return _materialLookup.TryGetValue(materialName, out var mat) ? mat : null;
        }

        public BrfTextureEntry GetTextureFast(string textureName)
        {
            if (!_lookupsBuilt) BuildLookups();
            return _textureLookup.TryGetValue(textureName, out var tex) ? tex : null;
        }

        #endregion
    }

    #region Entry Types

    [Serializable]
    public class BrfMeshEntry
    {
        [Header("Identity")]
        public string Name;
        public string BaseName;
        public string SourcePath;

        [Header("Unity Asset")]
        public Mesh UnityMesh;

        [Header("BRF Data")]
        public string MaterialName;
        public long Flags;
        public int LodLevel;
        public int SubMeshIndex;

        [Header("Computed")]
        public bool IsCollisionMesh;

        public bool HasTangents => BrfFlagDecoder.MeshHasTangents(Flags);

        public static (string baseName, int lodLevel, int subMesh) ParseMeshName(string meshName)
        {
            string baseName = meshName;
            int lodLevel = 0;
            int subMesh = 0;

            int lodIdx = meshName.IndexOf(".lod", StringComparison.OrdinalIgnoreCase);
            if (lodIdx > 0)
            {
                baseName = meshName.Substring(0, lodIdx);
                string lodPart = meshName.Substring(lodIdx + 4);
                if (int.TryParse(lodPart, out int lod))
                    lodLevel = lod;
                else
                    lodLevel = 1;
            }
            else
            {
                int lastDot = meshName.LastIndexOf('.');
                if (lastDot > 0 && lastDot < meshName.Length - 1)
                {
                    string suffix = meshName.Substring(lastDot + 1);
                    if (int.TryParse(suffix, out int sub))
                    {
                        baseName = meshName.Substring(0, lastDot);
                        subMesh = sub;
                    }
                }
            }

            return (baseName, lodLevel, subMesh);
        }
    }

    [Serializable]
    public class BrfMaterialEntry
    {
        [Header("Identity")]
        public string Name;

        [Header("Unity Asset")]
        public Material UnityMaterial;

        [Header("BRF Data")]
        public long Flags;
        public string ShaderName;

        [Header("Texture Names (BRF Strings)")]
        public string DiffuseA;
        public string DiffuseB;
        public string Bump;
        public string Enviro;
        public string Spec;

        [Header("Texture References (Unity)")]
        public Texture2D DiffuseATexture;
        public Texture2D DiffuseBTexture;
        public Texture2D BumpTexture;
        public Texture2D EnviroTexture;
        public Texture2D SpecTexture;

        [Header("Properties")]
        public Color Color = Color.white;
        public float SpecularValue;

        public int RenderOrder => BrfFlagDecoder.GetRenderOrder(Flags);
        public BrfBlendMode BlendMode => BrfFlagDecoder.GetBlendMode(Flags);
        public float AlphaTestThreshold => BrfFlagDecoder.GetAlphaTestThreshold(Flags);
        public bool NoZWrite => BrfFlagDecoder.HasFlag(Flags, BrfMaterialFlags.NoZWrite);
        public bool IsTransparent => BlendMode != BrfBlendMode.None || NoZWrite;
    }

    [Serializable]
    public class BrfTextureEntry
    {
        [Header("Identity")]
        public string Name;
        public long Flags;

        [Header("Unity Asset")]
        public Texture2D UnityTexture;

        public int FrameCount => BrfFlagDecoder.GetTextureFrameCount(Flags);
        public bool IsAnimated => FrameCount > 0;
    }

    [Serializable]
    public class BrfBodyEntry
    {
        [Header("Identity")]
        public string Name;
        public string SourcePath;

        [Header("Unity Asset")]
        public Mesh CollisionMesh;

        [Header("BRF Data")]
        public long Flags;
        public List<BrfCollisionPrimitive> Primitives = new();

        public bool HasPrimitives => Primitives != null && Primitives.Count > 0;
        public bool HasMesh => CollisionMesh != null;
    }

    [Serializable]
    public class BrfCollisionPrimitive
    {
        public BrfPrimitiveType Type;
        public long Flags;
        public float Radius;
        public Vector3 Center;
        public Vector3 Point1;
        public Vector3 Point2;
        public Vector3 Size;

        public bool NoCollision => BrfFlagDecoder.HasHitboxFlag(Flags, BrfFlagDecoder.HitboxFlags.NoCollision);
    }

    [Serializable]
    public class BrfSkeletonEntry
    {
        public string Name;
        public string SourcePath;
        public long Flags;
    }

    [Serializable]
    public class BrfAnimationEntry
    {
        public string Name;
        public string SourcePath;
    }

    [Serializable]
    public class BrfShaderEntry
    {
        public string Name;
        public string Technique;
        public string Fallback;
        public long Flags;
        public long Requirements;
    }

    #endregion

    #region Model Group

    [Serializable]
    public class BrfModelGroup
    {
        [Header("Identity")]
        public string GroupID;
        public string BrfName;

        [Header("Meshes")]
        public List<BrfMeshEntry> MeshEntries = new();
        public List<BrfMeshEntry> LodMeshEntries = new();

        [Header("Materials")]
        public List<string> MaterialNames = new();

        [Header("Collision")]
        public BrfBodyEntry CollisionBody;

        [Header("Prefab")]
        public GameObject Prefab;

        public bool HasLods => LodMeshEntries.Count > 0;
        public bool HasCollision => CollisionBody != null;
        public int TotalMeshCount => MeshEntries.Count + LodMeshEntries.Count;
        public Mesh PrimaryMesh => MeshEntries.Count > 0 ? MeshEntries[0].UnityMesh : null;
    }

    #endregion

    #region Statistics

    [Serializable]
    public struct BrfStatistics
    {
        public int MeshCount;
        public int MaterialCount;
        public int TextureCount;
        public int BodyCount;
        public int SkeletonCount;
        public int AnimationCount;
        public int ShaderCount;
        public int ModelGroupCount;
        public int BaseMeshCount;
        public int LodMeshCount;
        public int CollisionMeshCount;
        public int MaterialsWithTextures;
        public int ModelsWithCollision;
        public int TexturesResolved;
    }

    #endregion
}