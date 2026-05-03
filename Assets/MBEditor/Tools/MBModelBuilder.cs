using System.Collections.Generic;
using System.IO;
using System.Linq;
using MountAndBlade.ModdingToolkit;
using UnityEditor;
using UnityEngine;

namespace MountAndBlade.Data
{
    /// <summary>
    /// Builder class for creating MBModel instances from various data sources.
    /// Handles BRF data integration, collision setup, and LOD configuration.
    /// </summary>
    public class MBModelBuilder
    {
        #region Fields

        private string _modelId;
        private string _brfSource;
        private string _moduleName;
        
        private List<MBModelMesh> _meshes = new();
        private List<MBModelMesh> _lodMeshes = new();
        private MBModelCollision _collision;
        
        // Contexts for resolving materials
        private MBModuleImportContext _moduleContext;
        private MBModuleImportContext _nativeContext;
        
        // BRF data importers for collision lookup
        private Dictionary<string, BrfDataImporter> _brfImporters;

        #endregion

        #region Builder Pattern Methods

        public MBModelBuilder(string moduleName)
        {
            _moduleName = moduleName;
        }

        public MBModelBuilder WithContexts(MBModuleImportContext moduleCtx, MBModuleImportContext nativeCtx)
        {
            _moduleContext = moduleCtx;
            _nativeContext = nativeCtx;
            return this;
        }

        public MBModelBuilder WithBrfImporters(Dictionary<string, BrfDataImporter> importers)
        {
            _brfImporters = importers;
            return this;
        }

        public MBModelBuilder WithModelId(string modelId)
        {
            _modelId = modelId;
            return this;
        }

        public MBModelBuilder WithBrfSource(string brfName)
        {
            _brfSource = brfName;
            return this;
        }

        public MBModelBuilder AddMesh(Mesh mesh, Material material, string materialName = null, long materialFlags = 0)
        {
            _meshes.Add(new MBModelMesh
            {
                Mesh = mesh,
                Material = material,
                MeshName = mesh?.name ?? "Unknown",
                MaterialName = materialName,
                MaterialFlags = materialFlags,
                LodLevel = 0
            });
            return this;
        }

        public MBModelBuilder AddLodMesh(Mesh mesh, Material material, int lodLevel = 1)
        {
            _lodMeshes.Add(new MBModelMesh
            {
                Mesh = mesh,
                Material = material,
                MeshName = mesh?.name ?? "Unknown",
                LodLevel = lodLevel
            });
            return this;
        }

        public MBModelBuilder WithCollision(MBModelCollision collision)
        {
            _collision = collision;
            return this;
        }

        public MBModelBuilder WithCollisionFromBrf(string bodyName)
        {
            if (_brfImporters == null || string.IsNullOrEmpty(_brfSource))
                return this;

            if (_brfImporters.TryGetValue(_brfSource, out var importer))
            {
                var bodies = importer.GetBodies();
                var body = bodies.Find(b => b.name == bodyName || b.name == $"bo_{_modelId}");
                
                if (body != null)
                {
                    _collision = ConvertBrfBodyToCollision(body);
                }
            }

            return this;
        }

        #endregion

        #region Build Methods

        /// <summary>
        /// Build the MBModel component on a new GameObject
        /// </summary>
        public MBModel Build(Transform parent = null)
        {
            if (_meshes.Count == 0)
            {
                Debug.LogWarning($"[MBModelBuilder] Cannot build model '{_modelId}' with no meshes");
                return null;
            }

            var go = new GameObject(_modelId);
            if (parent != null)
                go.transform.SetParent(parent, false);

            var model = go.AddComponent<MBModel>();
            
            model.ModelID = _modelId;
            model.BRFSource = _brfSource;
            model.Meshes = _meshes.ToList();
            model.LodMeshes = _lodMeshes.ToList();
            model.Collision = _collision;

            // Compose the hierarchy
            model.Compose();

            return model;
        }

        /// <summary>
        /// Build and save as a prefab asset
        /// </summary>
        public GameObject BuildAsPrefab(string outputPath)
        {
            var model = Build();
            if (model == null) return null;

            // Ensure directory exists
            string directory = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                Directory.CreateDirectory(directory);

            // Save as prefab
            var prefab = PrefabUtility.SaveAsPrefabAsset(model.gameObject, outputPath);
            
            // Destroy temp object
            Object.DestroyImmediate(model.gameObject);

            return prefab;
        }

        /// <summary>
        /// Clear builder state for reuse
        /// </summary>
        public MBModelBuilder Reset()
        {
            _modelId = null;
            _brfSource = null;
            _meshes.Clear();
            _lodMeshes.Clear();
            _collision = null;
            return this;
        }

        #endregion

        #region Static Factory Methods

        /// <summary>
        /// Create an MBModel from MBResourcesModel data
        /// </summary>
        public static MBModel FromResourcesModel(
            MBResourcesModel resourcesModel,
            string moduleName,
            MBModuleImportContext moduleCtx = null,
            MBModuleImportContext nativeCtx = null,
            Dictionary<string, BrfDataImporter> brfImporters = null)
        {
            var builder = new MBModelBuilder(moduleName)
                .WithContexts(moduleCtx, nativeCtx)
                .WithBrfImporters(brfImporters)
                .WithModelId(resourcesModel.GroupID)
                .WithBrfSource(resourcesModel.BRFFileName);

            // Add base meshes
            for (int i = 0; i < resourcesModel.Meshes.Length; i++)
            {
                var mesh = resourcesModel.Meshes[i];
                var material = resourcesModel.ModelMaterials != null && i < resourcesModel.ModelMaterials.Length
                    ? resourcesModel.ModelMaterials[i]
                    : null;

                // Try to get material flags from BRF data
                long matFlags = 0;
                string matName = null;
                
                if (brfImporters != null && brfImporters.TryGetValue(resourcesModel.BRFFileName, out var importer))
                {
                    var meshData = importer.GetMeshData(mesh.name);
                    if (meshData != null)
                    {
                        matName = meshData.Material;
                        var matData = importer.GetMaterialData(matName);
                        if (matData != null)
                            matFlags = matData.flags;
                    }
                }

                builder.AddMesh(mesh, material, matName, matFlags);
            }

            // Add LOD meshes
            if (resourcesModel.LodMeshes != null)
            {
                for (int i = 0; i < resourcesModel.LodMeshes.Length; i++)
                {
                    var lodMesh = resourcesModel.LodMeshes[i];
                    var material = resourcesModel.ModelMaterials?.Length > 0 
                        ? resourcesModel.ModelMaterials[0] 
                        : null;
                    
                    builder.AddLodMesh(lodMesh, material, i + 1);
                }
            }

            // Try to find collision body
            builder.WithCollisionFromBrf($"bo_{resourcesModel.GroupID}");

            return builder.Build();
        }

        #endregion

        #region BRF Conversion Helpers

        private MBModelCollision ConvertBrfBodyToCollision(BrfBody body)
        {
            var collision = new MBModelCollision
            {
                BodyName = body.name,
                Flags = body.flags
            };

            if (body.primitives != null && body.primitives.Count > 0)
            {
                var primitives = new List<MBCollisionPrimitive>();

                foreach (var prim in body.primitives)
                {
                    var primitive = new MBCollisionPrimitive
                    {
                        Radius = prim.radius,
                        // Convert from BRF Z-up to Unity Y-up
                        Center = ConvertCoordinate(prim.center),
                        Point1 = ConvertCoordinate(prim.p1),
                        Point2 = ConvertCoordinate(prim.p2)
                    };

                    switch (prim.type.ToLowerInvariant())
                    {
                        case "sphere":
                            primitive.Type = MBCollisionType.Sphere;
                            break;
                        case "capsule":
                            primitive.Type = MBCollisionType.Capsule;
                            break;
                        case "box":
                            primitive.Type = MBCollisionType.Box;
                            primitive.Size = new Vector3(prim.radius * 2, prim.radius * 2, prim.radius * 2);
                            break;
                        default:
                            primitive.Type = MBCollisionType.Manifold;
                            break;
                    }

                    primitives.Add(primitive);
                }

                collision.Primitives = primitives.ToList();
            }

            return collision;
        }

        /// <summary>
        /// Convert from BRF coordinate system (Z-up, right-handed) to Unity (Y-up, left-handed)
        /// </summary>
        private static Vector3 ConvertCoordinate(Vector3 brfCoord)
        {
            return new Vector3(brfCoord.x, brfCoord.z, brfCoord.y);
        }

        #endregion
    }
}