using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MountAndBlade.Data;
using Newtonsoft.Json;
using UnityEditor;
using UnityEngine;

namespace MountAndBlade.ModdingToolkit
{
    /// <summary>
    /// Imports BRF data from the BRF Synchronizer's exported folder structure.
    /// Processes data.json and creates Unity assets (materials, mesh references, collision data).
    /// </summary>
    public class BrfDataImporter
    {
        #region State

        private readonly string _moduleName;
        private readonly string _brfName;
        private readonly string _brfFolderPath;
        private BrfData _brfData;

        // Extensions for texture file scanning in BRF local folder
        private static readonly string[] TextureSearchPatterns =
            { "*.png", "*.tga", "*.psd", "*.jpg", "*.jpeg", "*.bmp", "*.dds" };

        // Cached lookups
        private Dictionary<string, BrfMesh> _meshLookup;
        private Dictionary<string, BrfMaterial> _materialLookup;
        private Dictionary<string, BrfTexture> _textureLookup;
        private Dictionary<string, BrfBody> _bodyLookup;

        // BRF-local texture file cache (name -> path)
        private Dictionary<string, string> _localTextureFiles;

        // Import context for cross-BRF resolution
        private MBModuleImportContext _moduleContext;
        private MBModuleImportContext _nativeContext;

        #endregion

        #region Construction

        public BrfDataImporter(string moduleName, string brfFolderPath,
            MBModuleImportContext moduleCtx = null,
            MBModuleImportContext nativeCtx = null)
        {
            _moduleName = moduleName;
            _brfFolderPath = brfFolderPath;
            _brfName = Path.GetFileName(brfFolderPath);
            _moduleContext = moduleCtx;
            _nativeContext = nativeCtx;
        }

        #endregion

        #region Main Import Methods

        /// <summary>
        /// Load and parse the data.json file.
        /// </summary>
        public bool LoadDataJson()
        {
            string jsonPath = Path.Combine(_brfFolderPath, "data.json");

            if (!File.Exists(jsonPath))
            {
                Debug.LogError($"data.json not found in: {_brfFolderPath}");
                return false;
            }

            try
            {
                string json = File.ReadAllText(jsonPath);
                _brfData = JsonConvert.DeserializeObject<BrfData>(json);

                BuildLookups();
                BuildLocalTextureCache();

                Debug.Log($"Loaded BRF data: {_brfName} - " +
                          $"{_brfData.meshes?.Count ?? 0} meshes, " +
                          $"{_brfData.materials?.Count ?? 0} materials, " +
                          $"{_brfData.textures?.Count ?? 0} textures, " +
                          $"{_brfData.bodies?.Count ?? 0} bodies");

                return true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"Failed to parse data.json: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Get all mesh data with material associations.
        /// </summary>
        public List<BrfMesh> GetMeshes() => _brfData?.meshes ?? new List<BrfMesh>();

        /// <summary>
        /// Get all material definitions.
        /// </summary>
        public List<BrfMaterial> GetMaterials() => _brfData?.materials ?? new List<BrfMaterial>();

        /// <summary>
        /// Get all texture definitions from data.json.
        /// </summary>
        public List<BrfTexture> GetTextures() => _brfData?.textures ?? new List<BrfTexture>();

        /// <summary>
        /// Get all collision body definitions.
        /// </summary>
        public List<BrfBody> GetBodies() => _brfData?.bodies ?? new List<BrfBody>();

        /// <summary>
        /// Get mesh data by name.
        /// </summary>
        public BrfMesh GetMeshData(string meshName)
        {
            if (_meshLookup == null) return null;
            _meshLookup.TryGetValue(meshName, out var mesh);
            return mesh;
        }

        /// <summary>
        /// Get material data by name.
        /// </summary>
        public BrfMaterial GetMaterialData(string materialName)
        {
            if (_materialLookup == null) return null;
            _materialLookup.TryGetValue(materialName, out var material);
            return material;
        }

        /// <summary>
        /// Get texture data by name from data.json definitions.
        /// </summary>
        public BrfTexture GetTextureData(string textureName)
        {
            if (_textureLookup == null) return null;
            var lookupName = Path.GetFileNameWithoutExtension(textureName);
            _textureLookup.TryGetValue(lookupName, out var texture);
            return texture;
        }

        /// <summary>
        /// Get body/collision data by name.
        /// </summary>
        public BrfBody GetBodyData(string bodyName)
        {
            if (_bodyLookup == null) return null;
            _bodyLookup.TryGetValue(bodyName, out var body);
            return body;
        }

        /// <summary>
        /// Get the material name for a given mesh.
        /// </summary>
        public string GetMaterialNameForMesh(string meshName)
        {
            var meshData = GetMeshData(meshName);
            return meshData?.Material;
        }

        /// <summary>
        /// Get collision data as MBModelCollision for use with MBModel component.
        /// </summary>
        public MBModelCollision GetCollisionData(string bodyName)
        {
            var body = GetBodyData(bodyName);
            if (body == null) return null;

            return ConvertBrfBodyToCollision(body);
        }

        #endregion

        #region Texture Resolution

        /// <summary>
        /// Build a cache of texture files in this BRF's Textures/ folder.
        /// </summary>
        private void BuildLocalTextureCache()
        {
            _localTextureFiles = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            string texturesFolder = Path.Combine(_brfFolderPath, "Textures");
            if (!Directory.Exists(texturesFolder)) return;

            foreach (var pattern in TextureSearchPatterns)
            {
                foreach (var file in Directory.EnumerateFiles(texturesFolder, pattern))
                {
                    var name = Path.GetFileNameWithoutExtension(file);
                    _localTextureFiles.TryAdd(name, file);
                }
            }
        }

        /// <summary>
        /// Resolve a BRF texture name to a Unity Texture2D.
        /// Search order: BRF local Textures/ → module context → native context.
        /// </summary>
        public Texture2D ResolveTexture(string textureName)
        {
            if (string.IsNullOrEmpty(textureName) || textureName == "none")
                return null;

            var lookupName = Path.GetFileNameWithoutExtension(textureName);

            // 1. BRF's own Textures/ folder (highest priority)
            if (_localTextureFiles != null && _localTextureFiles.TryGetValue(lookupName, out var localPath))
            {
                var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(localPath);
                if (tex != null) return tex;
            }

            // 2. Module context (other BRF folders in same module)
            var texture = _moduleContext?.GetTexture(textureName);
            if (texture != null) return texture;

            // 3. Native context (fallback)
            texture = _nativeContext?.GetTexture(textureName);
            if (texture != null) return texture;

            return null;
        }

        /// <summary>
        /// Check if a texture can be resolved (exists somewhere in the search chain).
        /// </summary>
        public bool CanResolveTexture(string textureName)
        {
            if (string.IsNullOrEmpty(textureName) || textureName == "none")
                return false;

            var lookupName = Path.GetFileNameWithoutExtension(textureName);

            if (_localTextureFiles != null && _localTextureFiles.ContainsKey(lookupName))
                return true;

            if (_moduleContext?.HasTexture(textureName) == true)
                return true;

            if (_nativeContext?.HasTexture(textureName) == true)
                return true;

            return false;
        }

        /// <summary>
        /// Get all texture file paths from BRF's local Textures/ folder.
        /// </summary>
        public IEnumerable<string> GetLocalTextureFiles()
        {
            return _localTextureFiles?.Values ?? Enumerable.Empty<string>();
        }

        #endregion

        #region Material Creation

        /// <summary>
        /// Create or update all Unity materials from the BRF material definitions.
        /// Existing .mat assets are updated IN-PLACE to preserve prefab references.
        /// New materials are created normally.
        /// </summary>
        public Dictionary<string, Material> CreateAllMaterials(string outputPath)
        {
            var createdMaterials = new Dictionary<string, Material>(StringComparer.OrdinalIgnoreCase);

            if (_brfData?.materials == null || _brfData.materials.Count == 0)
            {
                Debug.LogWarning($"No materials found in {_brfName}");
                return createdMaterials;
            }

            if (!Directory.Exists(outputPath))
                Directory.CreateDirectory(outputPath);

            var toCreate = new List<(Material mat, string path)>();

            try
            {
                AssetDatabase.StartAssetEditing();

                foreach (var brfMat in _brfData.materials)
                {
                    string matPath = Path.Combine(outputPath, $"{brfMat.name}.mat");
                    var existing = AssetDatabase.LoadAssetAtPath<Material>(matPath);

                    if (existing != null)
                    {
                        // UPDATE IN-PLACE: apply new shader/texture/color directly onto
                        // the existing asset so all prefab references remain valid
                        var updated = CreateMaterial(brfMat);
                        if (updated != null)
                        {
                            existing.CopyPropertiesFromMaterial(updated);
                            existing.shader = updated.shader;
                            existing.renderQueue = updated.renderQueue;
                            EditorUtility.SetDirty(existing);
                            createdMaterials[brfMat.name] = existing;
                        }
                    }
                    else
                    {
                        // CREATE NEW
                        var material = CreateMaterial(brfMat);
                        if (material != null)
                        {
                            toCreate.Add((material, matPath));
                            createdMaterials[brfMat.name] = material;
                        }
                    }
                }
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
            }

            // Batch-create any new materials
            if (toCreate.Count > 0)
            {
                try
                {
                    AssetDatabase.StartAssetEditing();
                    foreach (var (mat, path) in toCreate)
                        AssetDatabase.CreateAsset(mat, path);
                }
                finally
                {
                    AssetDatabase.StopAssetEditing();
                }
            }

            int updated2 = createdMaterials.Count - toCreate.Count;
            Debug.Log($"Materials [{_brfName}]: {toCreate.Count} created, {updated2} updated in-place");

            return createdMaterials;
        }

        /// <summary>
        /// Create a single Unity material from BRF material data.
        /// </summary>
        public Material CreateMaterial(BrfMaterial brfMat)
        {
            var brfShader = _brfData.shaders?.FirstOrDefault(s => s.name == brfMat.shader);
            var setup = MBShaderResolver.Resolve(brfMat, brfShader);
            Shader shader = Shader.Find(setup.ShaderName);
    
            if (shader == null)
            {
                Debug.LogWarning($"Shader '{setup.ShaderName}' not found for material: {brfMat.name}");
                return null;
            }

            var material = new Material(shader)
            {
                name = brfMat.name,
                color = brfMat.GetColor()
            };
    
            MBShaderResolver.ApplySetup(material, setup, brfMat, ResolveTexture);

            return material;
        }

        private Shader SelectShader(BrfMaterial brfMat)
        {
            var blendMode = BrfFlagDecoder.GetBlendMode(brfMat.flags);
            float alphaRef = BrfFlagDecoder.GetAlphaTestRef(brfMat.flags);
            bool noZWrite = BrfFlagDecoder.HasFlag(brfMat.flags, BrfMaterialFlags.NoZWrite);

            if (blendMode == BrfBlendMode.Additive)
                return Shader.Find("M&B/M&B_Additive") ?? Shader.Find("M&B/M&B_Standard");

            if (blendMode == BrfBlendMode.AlphaBlend || noZWrite)
                return Shader.Find("M&B/M&B_Transparent") ?? Shader.Find("M&B/M&B_Standard");

            if (alphaRef > 0)
                return Shader.Find("M&B/M&B_Cutout") ?? Shader.Find("M&B/M&B_Standard");

            return Shader.Find("M&B/M&B_Standard");
        }

        private void ApplyMaterialFlags(Material material, long flags)
        {
            int renderOrder = BrfFlagDecoder.GetRenderOrder(flags);
            if (renderOrder != 0)
            {
                material.renderQueue = 2000 + (renderOrder * 100);
            }

            float alphaRef = BrfFlagDecoder.GetAlphaTestRef(flags);
            if (alphaRef > 0 && material.HasProperty("_Cutoff"))
            {
                material.SetFloat("_Cutoff", alphaRef);
            }

            if (BrfFlagDecoder.HasFlag(flags, BrfMaterialFlags.NoZWrite))
            {
                if (material.HasProperty("_ZWrite"))
                    material.SetFloat("_ZWrite", 0);
            }

            if (material.HasProperty("_BrfFlags"))
                material.SetFloat("_BrfFlags", flags);
        }

        private void SetMaterialTexture(Material material, string property, string textureName, bool isNormalMap = false)
        {
            if (string.IsNullOrEmpty(textureName) || textureName == "none")
                return;

            Texture2D texture = ResolveTexture(textureName);
            if (texture != null)
            {
                material.SetTexture(property, texture);

                if (isNormalMap)
                    DDSUtility.FixSRGBNormalSettings(texture);
            }
        }

        #endregion

        #region Collision Data Conversion

        /// <summary>
        /// Convert BrfBody to MBModelCollision for use with MBModel component.
        /// Only creates colliders based on primitive data in JSON:
        /// - sphere/capsule/box -> Unity primitive colliders
        /// - manifold -> MeshCollider from vertices/faces data OR from bo_*.obj
        /// Does NOT create MeshCollider just because bo_*.obj exists.
        /// </summary>
        private MBModelCollision ConvertBrfBodyToCollision(BrfBody body)
        {
            var collision = new MBModelCollision
            {
                BodyName = body.name,
                Flags = body.flags
            };

            bool hasManifoldInData = false;

            // Only process primitives from JSON data
            if (body.primitives != null && body.primitives.Count > 0)
            {
                foreach (var prim in body.primitives)
                {
                    // Skip if marked as no collision
                    if (prim.NoCollision)
                        continue;

                    var primType = prim.type?.ToLowerInvariant();

                    switch (primType)
                    {
                        case "sphere":
                            collision.Primitives.Add(new MBCollisionPrimitive
                            {
                                Type = MBCollisionType.Sphere,
                                Radius = prim.radius,
                                Center = prim.GetCenterUnity()
                            });
                            break;

                        case "capsule":
                            collision.Primitives.Add(new MBCollisionPrimitive
                            {
                                Type = MBCollisionType.Capsule,
                                Radius = prim.radius,
                                Center = prim.GetCenterUnity(),
                                Point1 = prim.GetP1Unity(),
                                Point2 = prim.GetP2Unity()
                            });
                            break;

                        case "box":
                            collision.Primitives.Add(new MBCollisionPrimitive
                            {
                                Type = MBCollisionType.Box,
                                Center = prim.GetCenterUnity(),
                                Size = new Vector3(prim.radius * 2, prim.radius * 2, prim.radius * 2)
                            });
                            break;

                        case "manifold":
                        case "polygon":
                            // Mark that we have manifold data - will load mesh below
                            hasManifoldInData = true;
                            break;

                        default:
                            // Skip unknown types
                            continue;
                    }
                }
            }

            // Only load collision mesh if manifold/polygon primitives exist in the DATA
            if (hasManifoldInData && !string.IsNullOrEmpty(body.source))
            {
                string meshPath = Path.Combine(_brfFolderPath, body.source);
                collision.CollisionMesh = AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
                collision.IsConvex = false;
            }

            return collision;
        }

        /// <summary>
        /// Create colliders on a GameObject from BrfBody data.
        /// </summary>
        public static bool CreateCollidersFromBody(GameObject parent, BrfBody body)
        {
            if (body == null) return false;

            if (body.primitives == null || body.primitives.Count == 0)
                return false;

            foreach (var prim in body.primitives)
            {
                CreatePrimitiveCollider(parent, prim);
            }

            return true;
        }

        private static void CreatePrimitiveCollider(GameObject parent, BrfPrimitive primitive)
        {
            Vector3 ConvertCoord(Vector3 v) => new Vector3(v.x, v.z, v.y);

            switch (primitive.type?.ToLowerInvariant())
            {
                case "sphere":
                    var sphere = parent.AddComponent<SphereCollider>();
                    sphere.center = ConvertCoord(primitive.center);
                    sphere.radius = primitive.radius;
                    break;

                case "capsule":
                    var capsule = parent.AddComponent<CapsuleCollider>();
                    Vector3 p1 = ConvertCoord(primitive.p1);
                    Vector3 p2 = ConvertCoord(primitive.p2);

                    capsule.center = (p1 + p2) * 0.5f;
                    capsule.height = Vector3.Distance(p1, p2) + primitive.radius * 2f;
                    capsule.radius = primitive.radius;

                    Vector3 dir = (p2 - p1).normalized;
                    if (Mathf.Abs(dir.y) > 0.9f)
                        capsule.direction = 1;
                    else if (Mathf.Abs(dir.x) > 0.9f)
                        capsule.direction = 0;
                    else
                        capsule.direction = 2;
                    break;

                case "box":
                    var box = parent.AddComponent<BoxCollider>();
                    box.center = ConvertCoord(primitive.center);
                    box.size = Vector3.one * primitive.radius * 2f;
                    break;
            }
        }

        private static Vector3 ConvertCoordinate(Vector3 brfCoord)
        {
            return new Vector3(brfCoord.x, brfCoord.z, brfCoord.y);
        }

        #endregion

        #region JSON Export (for compatibility)

        /// <summary>
        /// Export mesh data to a meshes.json file.
        /// </summary>
        public void ExportMeshDataJson(string outputPath)
        {
            if (_brfData?.meshes == null) return;

            var meshDataList = _brfData.meshes.Select(m => new MBMeshData
            {
                name = m.Name,
                material = m.Material,
                flags = m.Flags
            }).ToArray();

            string json = JsonConvert.SerializeObject(meshDataList, Formatting.Indented);
            string jsonPath = Path.Combine(outputPath, "meshes.json");
            File.WriteAllText(jsonPath, json);
        }

        /// <summary>
        /// Export material data to a materials.json file.
        /// </summary>
        public void ExportMaterialDataJson(string outputPath)
        {
            if (_brfData?.materials == null) return;

            var materialDataList = _brfData.materials.Select(m => new MBMaterialData
            {
                name = m.name,
                flags = m.flags,
                shader = m.shader,
                diffuseA = m.diffuseA,
                diffuseB = m.diffuseB,
                bump = m.bump,
                enviro = m.enviro,
                spec = m.spec,
                color = m.color_rgb ?? new float[3],
                specular = m.specular_value
            }).ToArray();

            string json = JsonConvert.SerializeObject(materialDataList, Formatting.Indented);
            string jsonPath = Path.Combine(outputPath, "materials.json");
            File.WriteAllText(jsonPath, json);
        }

        #endregion

        #region Helper Methods

        private void BuildLookups()
        {
            _meshLookup = _brfData.meshes?
                .GroupBy(m => m.Name, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase)
                ?? new Dictionary<string, BrfMesh>();

            _materialLookup = _brfData.materials?
                .GroupBy(m => m.name, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase)
                ?? new Dictionary<string, BrfMaterial>();

            _textureLookup = _brfData.textures?
                .GroupBy(t => t.name, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase)
                ?? new Dictionary<string, BrfTexture>();

            _bodyLookup = _brfData.bodies?
                .GroupBy(b => b.name, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase)
                ?? new Dictionary<string, BrfBody>();
        }

        #endregion
    }

    #region Legacy Compatibility Classes

    [Serializable]
    public class MBMeshData
    {
        public string name;
        public string material;
        public long flags;
    }

    [Serializable]
    public class MBMaterialData
    {
        public string name;
        public long flags;
        public string shader;
        public string diffuseA;
        public string diffuseB;
        public string bump;
        public string enviro;
        public string spec;
        public float[] color;
        public float specular;
    }

    #endregion
}