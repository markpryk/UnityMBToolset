using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace MountAndBlade.Data
{
    /// <summary>
    /// Module-level database that aggregates all MBBrfData assets.
    /// Provides cross-BRF lookups for meshes, materials, textures, and model groups.
    /// Replaces the old ModModelsDataBase for the BRF-centric pipeline.
    /// </summary>
    [CreateAssetMenu(fileName = "BrfDataBase", menuName = "MBToolset/BRF DataBase")]
    public class ModBrfDataBase : ScriptableObject
    {
        #region Serialized Data

        [Header("Module")]
        [SerializeField] private string _moduleName;

        [Header("BRF Assets")]
        [SerializeField] private List<MBBrfData> _brfAssets = new();

        public string ModuleName { get => _moduleName; set => _moduleName = value; }
        public List<MBBrfData> BrfAssets => _brfAssets;
        public int BrfCount => _brfAssets.Count;

        #endregion

        #region Runtime Lookups

        [NonSerialized] private Dictionary<string, MBBrfData> _brfByName;
        [NonSerialized] private Dictionary<string, BrfMeshEntry> _meshLookup;
        [NonSerialized] private Dictionary<string, BrfMaterialEntry> _materialLookup;
        [NonSerialized] private Dictionary<string, BrfModelGroup> _modelGroupLookup;
        [NonSerialized] private Dictionary<string, BrfTextureEntry> _textureLookup;
        [NonSerialized] private bool _lookupsBuilt;

        #endregion

        #region Unity Lifecycle

        private void OnEnable()
        {
            _brfAssets ??= new List<MBBrfData>();
        }

        #endregion

        #region BRF Management

        /// <summary>
        /// Add a BRF data asset to the database. Skips duplicates by name.
        /// </summary>
        public void AddBrf(MBBrfData brfData)
        {
            if (brfData == null) return;

            // Remove existing with same name (update scenario)
            _brfAssets.RemoveAll(b =>
                b != null && b.BrfName.Equals(brfData.BrfName, StringComparison.OrdinalIgnoreCase));

            _brfAssets.Add(brfData);
            InvalidateLookups();
        }

        /// <summary>
        /// Remove a BRF data asset by name.
        /// </summary>
        public bool RemoveBrf(string brfName)
        {
            int removed = _brfAssets.RemoveAll(b =>
                b != null && b.BrfName.Equals(brfName, StringComparison.OrdinalIgnoreCase));

            if (removed > 0) InvalidateLookups();
            return removed > 0;
        }

        /// <summary>
        /// Get a specific BRF data asset by name.
        /// </summary>
        public MBBrfData GetBrf(string brfName)
        {
            EnsureLookups();
            return _brfByName.TryGetValue(brfName, out var brf) ? brf : null;
        }

        /// <summary>
        /// Clear all BRF data from the database.
        /// </summary>
        public void Clear()
        {
            _brfAssets.Clear();
            InvalidateLookups();
        }

        #endregion

        #region Cross-BRF Lookups

        /// <summary>
        /// Find a mesh entry across all BRFs.
        /// </summary>
        public BrfMeshEntry FindMesh(string meshName)
        {
            if (string.IsNullOrEmpty(meshName)) return null;
            EnsureLookups();
            return _meshLookup.TryGetValue(meshName, out var mesh) ? mesh : null;
        }

        /// <summary>
        /// Find a material entry across all BRFs.
        /// </summary>
        public BrfMaterialEntry FindMaterialEntry(string materialName)
        {
            if (string.IsNullOrEmpty(materialName)) return null;
            EnsureLookups();
            return _materialLookup.TryGetValue(materialName, out var mat) ? mat : null;
        }

        /// <summary>
        /// Find a Unity Material across all BRFs.
        /// </summary>
        public Material FindMaterial(string materialName)
        {
            return FindMaterialEntry(materialName)?.UnityMaterial;
        }

        /// <summary>
        /// Find a model group across all BRFs.
        /// </summary>
        public BrfModelGroup FindModelGroup(string groupId)
        {
            if (string.IsNullOrEmpty(groupId)) return null;
            EnsureLookups();
            return _modelGroupLookup.TryGetValue(groupId, out var group) ? group : null;
        }

        /// <summary>
        /// Find a texture entry across all BRFs.
        /// </summary>
        public BrfTextureEntry FindTextureEntry(string textureName)
        {
            if (string.IsNullOrEmpty(textureName)) return null;
            EnsureLookups();
            return _textureLookup.TryGetValue(textureName, out var tex) ? tex : null;
        }

        /// <summary>
        /// Find a Unity Texture2D across all BRFs.
        /// </summary>
        public Texture2D FindTexture(string textureName)
        {
            return FindTextureEntry(textureName)?.UnityTexture;
        }

        /// <summary>
        /// Find which BRF a mesh belongs to.
        /// </summary>
        public MBBrfData FindBrfForMesh(string meshName)
        {
            if (string.IsNullOrEmpty(meshName)) return null;

            foreach (var brf in _brfAssets)
            {
                if (brf == null) continue;
                if (brf.GetMesh(meshName) != null)
                    return brf;
            }

            return null;
        }

        /// <summary>
        /// Find which BRF a material belongs to.
        /// </summary>
        public MBBrfData FindBrfForMaterial(string materialName)
        {
            if (string.IsNullOrEmpty(materialName)) return null;

            foreach (var brf in _brfAssets)
            {
                if (brf == null) continue;
                if (brf.GetMaterialEntry(materialName) != null)
                    return brf;
            }

            return null;
        }

        #endregion

        #region Enumeration

        /// <summary>
        /// Get all model groups across all BRFs.
        /// </summary>
        public IEnumerable<BrfModelGroup> GetAllModelGroups()
        {
            foreach (var brf in _brfAssets)
            {
                if (brf?.ModelGroups == null) continue;
                foreach (var group in brf.ModelGroups)
                    yield return group;
            }
        }

        /// <summary>
        /// Get all material entries across all BRFs.
        /// </summary>
        public IEnumerable<BrfMaterialEntry> GetAllMaterials()
        {
            foreach (var brf in _brfAssets)
            {
                if (brf?.Materials == null) continue;
                foreach (var mat in brf.Materials)
                    yield return mat;
            }
        }

        /// <summary>
        /// Get all mesh entries across all BRFs.
        /// </summary>
        public IEnumerable<BrfMeshEntry> GetAllMeshes()
        {
            foreach (var brf in _brfAssets)
            {
                if (brf?.Meshes == null) continue;
                foreach (var mesh in brf.Meshes)
                    yield return mesh;
            }
        }

        /// <summary>
        /// Get all texture entries across all BRFs.
        /// </summary>
        public IEnumerable<BrfTextureEntry> GetAllTextures()
        {
            foreach (var brf in _brfAssets)
            {
                if (brf?.Textures == null) continue;
                foreach (var tex in brf.Textures)
                    yield return tex;
            }
        }

        #endregion

        #region Lookup Management

        public void BuildLookups()
        {
            _brfByName = new Dictionary<string, MBBrfData>(StringComparer.OrdinalIgnoreCase);
            _meshLookup = new Dictionary<string, BrfMeshEntry>(StringComparer.OrdinalIgnoreCase);
            _materialLookup = new Dictionary<string, BrfMaterialEntry>(StringComparer.OrdinalIgnoreCase);
            _modelGroupLookup = new Dictionary<string, BrfModelGroup>(StringComparer.OrdinalIgnoreCase);
            _textureLookup = new Dictionary<string, BrfTextureEntry>(StringComparer.OrdinalIgnoreCase);

            foreach (var brf in _brfAssets)
            {
                if (brf == null) continue;

                // BRF by name
                _brfByName[brf.BrfName] = brf;

                // Meshes (first-found wins for cross-BRF duplicates)
                foreach (var mesh in brf.Meshes)
                {
                    if (!string.IsNullOrEmpty(mesh.Name) && !_meshLookup.ContainsKey(mesh.Name))
                        _meshLookup[mesh.Name] = mesh;
                }

                // Materials
                foreach (var mat in brf.Materials)
                {
                    if (!string.IsNullOrEmpty(mat.Name) && !_materialLookup.ContainsKey(mat.Name))
                        _materialLookup[mat.Name] = mat;
                }

                // Model groups
                foreach (var group in brf.ModelGroups)
                {
                    if (!string.IsNullOrEmpty(group.GroupID) && !_modelGroupLookup.ContainsKey(group.GroupID))
                        _modelGroupLookup[group.GroupID] = group;
                }

                // Textures
                foreach (var tex in brf.Textures)
                {
                    if (!string.IsNullOrEmpty(tex.Name) && !_textureLookup.ContainsKey(tex.Name))
                        _textureLookup[tex.Name] = tex;
                }
            }

            _lookupsBuilt = true;
        }

        private void EnsureLookups()
        {
            if (!_lookupsBuilt || _brfByName == null)
                BuildLookups();
        }

        private void InvalidateLookups()
        {
            _lookupsBuilt = false;
        }

        #endregion

        #region Statistics

        public BrfDataBaseStats GetStats()
        {
            var stats = new BrfDataBaseStats
            {
                TotalBrfs = _brfAssets.Count(b => b != null),
            };

            foreach (var brf in _brfAssets)
            {
                if (brf == null) continue;

                stats.TotalMeshes += brf.Meshes.Count;
                stats.TotalMaterials += brf.Materials.Count;
                stats.TotalTextures += brf.Textures.Count;
                stats.TotalBodies += brf.Bodies.Count;
                stats.TotalModelGroups += brf.ModelGroups.Count;
                stats.MaterialsAssigned += brf.Materials.Count(m => m.UnityMaterial != null);
                stats.ModelsWithCollision += brf.ModelGroups.Count(g => g.HasCollision);
            }

            return stats;
        }

        [Serializable]
        public struct BrfDataBaseStats
        {
            public int TotalBrfs;
            public int TotalMeshes;
            public int TotalMaterials;
            public int TotalTextures;
            public int TotalBodies;
            public int TotalModelGroups;
            public int MaterialsAssigned;
            public int ModelsWithCollision;

            public float MaterialCoverage =>
                TotalMaterials > 0 ? (float)MaterialsAssigned / TotalMaterials * 100f : 0f;
        }

        #endregion

        #region Validation

        /// <summary>
        /// Validate all BRF data in the database.
        /// </summary>
        public List<string> Validate()
        {
            var issues = new List<string>();

            // Check for null BRF references
            int nullCount = _brfAssets.Count(b => b == null);
            if (nullCount > 0)
                issues.Add($"{nullCount} null BRF reference(s) in database");

            // Check for duplicate BRF names
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var brf in _brfAssets.Where(b => b != null))
            {
                if (!seen.Add(brf.BrfName))
                    issues.Add($"Duplicate BRF name: {brf.BrfName}");
            }

            // Validate each BRF
            foreach (var brf in _brfAssets.Where(b => b != null))
            {
                var brfIssues = brf.Validate();
                foreach (var issue in brfIssues)
                    issues.Add($"[{brf.BrfName}] {issue}");
            }

            return issues;
        }

        /// <summary>
        /// Remove null entries and sort BRFs alphabetically.
        /// </summary>
        public void CleanAndSort()
        {
            _brfAssets.RemoveAll(b => b == null);
            _brfAssets.Sort((a, b) =>
                string.Compare(a.BrfName, b.BrfName, StringComparison.OrdinalIgnoreCase));

            InvalidateLookups();

#if UNITY_EDITOR
            EditorUtility.SetDirty(this);
#endif
        }

        #endregion
    }
}