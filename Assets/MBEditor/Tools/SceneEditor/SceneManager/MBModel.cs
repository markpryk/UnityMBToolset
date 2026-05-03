using System;
using System.Collections.Generic;
using UnityEngine;

namespace MountAndBlade.Data
{
    /// <summary>
    /// Core model component that bridges BRF data and Unity GameObjects.
    /// Handles composition (BRF -> Unity) and decomposition (Unity -> BRF).
    /// </summary>
    [DisallowMultipleComponent]
    public class MBModel : MonoBehaviour
    {
        #region Serialized Data

        [Header("Model Identity")] [SerializeField]
        private string _modelId;

        [SerializeField] protected string _prefabId;
        [SerializeField] protected string _sourceModule;

        [SerializeField] private string _brfSource;

        [Header("Mesh Data")] [SerializeField] private List<MBModelMesh> _meshes = new();
        [SerializeField] private List<MBModelMesh> _lodMeshes = new();

        [Header("Collision Data")] [SerializeField]
        private MBModelCollision _collision;

        [Header("Runtime References (Auto-generated)")] [SerializeField]
        private LODGroup _lodGroup;

        [SerializeField] private List<MeshRenderer> _renderers = new();
        [SerializeField] private List<Collider> _colliders = new();

        #endregion

        #region Properties

        public string ModelID
        {
            get => _modelId;
            set => _modelId = value;
        }

        public string BRFSource
        {
            get => _brfSource;
            set => _brfSource = value;
        }

        public List<MBModelMesh> Meshes
        {
            get => _meshes;
            set => _meshes = value ?? new List<MBModelMesh>();
        }

        public List<MBModelMesh> LodMeshes
        {
            get => _lodMeshes;
            set => _lodMeshes = value ?? new List<MBModelMesh>();
        }

        public MBModelCollision Collision
        {
            get => _collision;
            set => _collision = value;
        }

        public string SourceModule
        {
            get => _sourceModule;
            set => _sourceModule = value;
        }
        
        public string PrefabID { get => _prefabId; set => _prefabId = value; }

        public LODGroup LodGroup => _lodGroup;
        public List<MeshRenderer> Renderers => _renderers;
        public List<Collider> Colliders => _colliders;

        /// <summary>
        /// Quick access to primary mesh (LOD0, first submesh)
        /// </summary>
        public Mesh PrimaryMesh => _meshes != null && _meshes.Count > 0 ? _meshes[0].Mesh : null;

        /// <summary>
        /// Quick access to primary material
        /// </summary>
        public Material PrimaryMaterial => _meshes != null && _meshes.Count > 0 ? _meshes[0].Material : null;

        /// <summary>
        /// Check if model has LOD meshes configured
        /// </summary>
        public bool HasLODs => _lodMeshes != null && _lodMeshes.Count > 0;

        /// <summary>
        /// Check if model has collision data
        /// </summary>
        public bool HasCollision => _collision != null && _collision.HasData;

        #endregion

        #region Mesh Management

        /// <summary>
        /// Add a base mesh (LOD0) with material and flags.
        /// </summary>
        public void AddMesh(Mesh mesh, Material material, string materialName = null, long materialFlags = 0)
        {
            if (mesh == null) return;

            _meshes.Add(new MBModelMesh
            {
                MeshName = mesh.name,
                Mesh = mesh,
                Material = material,
                MaterialName = materialName ?? "",
                MaterialFlags = materialFlags,
                LodLevel = 0
            });
        }

        /// <summary>
        /// Add a LOD mesh.
        /// </summary>
        public void AddLodMesh(Mesh mesh, Material material, int lodLevel = 1)
        {
            if (mesh == null) return;

            _lodMeshes.Add(new MBModelMesh
            {
                MeshName = mesh.name,
                Mesh = mesh,
                Material = material,
                LodLevel = lodLevel
            });
        }

        /// <summary>
        /// Set collision data from primitives.
        /// </summary>
        public void SetCollision(string bodyName, List<MBCollisionPrimitive> primitives)
        {
            _collision = new MBModelCollision
            {
                BodyName = bodyName,
                Primitives = primitives ?? new List<MBCollisionPrimitive>()
            };
        }

        #endregion

        #region Composition (Build Unity hierarchy from data)

        /// <summary>
        /// Builds the complete Unity hierarchy from stored mesh/collision data.
        /// Call this after setting Meshes, LodMeshes, and Collision.
        /// </summary>
        public void Compose()
        {
            ClearHierarchy();

            if (_meshes == null || _meshes.Count == 0)
            {
                Debug.LogWarning($"[MBModel] No meshes to compose for {_modelId}");
                return;
            }

            // Create mesh renderers for base meshes
            for (int i = 0; i < _meshes.Count; i++)
            {
                var meshData = _meshes[i];
                if (meshData == null || meshData.Mesh == null) continue;

                string childName = !string.IsNullOrEmpty(meshData.MeshName)
                    ? meshData.MeshName
                    : $"Mesh_{i}";

                var meshGO = CreateMeshObject(meshData, childName);
                meshGO.transform.SetParent(transform, false);

                var renderer = meshGO.GetComponent<MeshRenderer>();
                if (renderer != null)
                    _renderers.Add(renderer);
            }

            // Setup LOD Group if we have LOD meshes
            if (HasLODs)
            {
                SetupLODGroup();
            }

            // Setup Collision
            if (HasCollision)
            {
                SetupCollision();
            }
        }

        private GameObject CreateMeshObject(MBModelMesh meshData, string name)
        {
            var go = new GameObject(name);

            var filter = go.AddComponent<MeshFilter>();
            filter.sharedMesh = meshData.Mesh;

            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = meshData.Material;

            // Apply material flags if available
            // if (meshData.MaterialFlags != 0)
            // {
            //     ApplyMaterialFlags(renderer, meshData.MaterialFlags);
            // }

            return go;
        }

        private void SetupLODGroup()
        {
            _lodGroup = gameObject.GetComponent<LODGroup>();
            if (_lodGroup == null)
                _lodGroup = gameObject.AddComponent<LODGroup>();

            // Collect LOD renderers: LOD0 (base) + LOD1..N from _lodMeshes
            var lodRenderers = new List<Renderer[]>();

            // LOD0 - Base meshes (highest detail)
            if (_renderers != null && _renderers.Count > 0)
            {
                lodRenderers.Add(_renderers.ToArray());
            }

            // LOD1+ - Lower detail meshes
            for (int i = 0; i < _lodMeshes.Count; i++)
            {
                var lodMesh = _lodMeshes[i];
                if (lodMesh == null || lodMesh.Mesh == null) continue;

                var lodGO = CreateMeshObject(lodMesh, $"LOD{lodMesh.LodLevel}_{lodMesh.MeshName}");
                lodGO.transform.SetParent(transform, false);

                var renderer = lodGO.GetComponent<MeshRenderer>();
                if (renderer != null)
                {
                    lodRenderers.Add(new Renderer[] { renderer });
                }
            }

            // Distribute thresholds so the last LOD has threshold 0 (never culled).
            // LOD0 gets the highest threshold, each subsequent LOD gets proportionally
            // lower, and the final LOD reaches 0 - always visible at any distance.
            int totalLods = lodRenderers.Count;
            var lods = new LOD[totalLods];

            for (int i = 0; i < totalLods; i++)
            {
                // Last LOD → 0 (never culled), others distributed proportionally
                float threshold = (i < totalLods - 1)
                    ? 1.0f - ((float)i / (totalLods - 1))
                    : 0f;

                // Scale down so LOD0 starts at a reasonable screen percentage
                // instead of 100%. Using 0.6 as max so LOD0 ≈ 60% screen size.
                threshold *= 0.6f;

                lods[i] = new LOD(threshold, lodRenderers[i]);
            }

            _lodGroup.SetLODs(lods);
            _lodGroup.RecalculateBounds();
        }

        private void SetupCollision()
        {
            // MeshCollider doesn't need this because OBJ import handles it

            if (_collision.Primitives != null && _collision.Primitives.Count > 0)
            {
                foreach (var primitive in _collision.Primitives)
                {
                    var collider = CreatePrimitiveCollider(primitive, transform);
                    if (collider != null)
                    {
                        _colliders.Add(collider);
                    }
                }
            }

            // Create mesh collider if we have a collision mesh (manifold data)
            // No rotation needed - OBJ import already handles coordinate conversion
            if (_collision.CollisionMesh != null)
            {
                var colliderGO = new GameObject("MeshCollider");
                colliderGO.transform.SetParent(transform, false);

                var meshCollider = colliderGO.AddComponent<MeshCollider>();
                meshCollider.sharedMesh = _collision.CollisionMesh;
                meshCollider.convex = _collision.IsConvex;

                _colliders.Add(meshCollider);
            }
        }

        private Collider CreatePrimitiveCollider(MBCollisionPrimitive primitive, Transform parent)
        {
            var colliderGO = new GameObject($"Collider_{primitive.Type}");
            colliderGO.transform.SetParent(parent, false);

            Collider collider = null;

            switch (primitive.Type)
            {
                case MBCollisionType.Sphere:
                    var sphere = colliderGO.AddComponent<SphereCollider>();
                    sphere.center = MBModelMesh.ConvertFromBRF(primitive.Center);
                    sphere.radius = primitive.Radius;
                    collider = sphere;
                    break;

                case MBCollisionType.Capsule:
                    collider = CreateCapsuleCollider(colliderGO, primitive);
                    break;

                case MBCollisionType.Box:
                    var box = colliderGO.AddComponent<BoxCollider>();
                    box.center = MBModelMesh.ConvertFromBRF(primitive.Center);
                    box.size = MBModelMesh.ConvertFromBRF(primitive.Size);
                    collider = box;
                    break;
            }

            return collider;
        }

        /// <summary>
        /// Creates a capsule collider with proper rotation for arbitrary orientations.
        /// Unity CapsuleCollider only supports axis-aligned directions (X/Y/Z),
        /// so for diagonal capsules we rotate the GameObject itself.
        /// </summary>
        private Collider CreateCapsuleCollider(GameObject colliderGO, MBCollisionPrimitive primitive)
        {
            Vector3 p1 = MBModelMesh.ConvertFromBRF(primitive.Point1);
            Vector3 p2 = MBModelMesh.ConvertFromBRF(primitive.Point2);

            Vector3 center = (p1 + p2) * 0.5f;
            Vector3 axis = p2 - p1;
            float distance = axis.magnitude;

            // Degenerate capsule (points are same) - treat as sphere
            if (distance < 0.0001f)
            {
                var sphere = colliderGO.AddComponent<SphereCollider>();
                sphere.center = center;
                sphere.radius = primitive.Radius;
                return sphere;
            }

            axis.Normalize();

            colliderGO.transform.localPosition = center;

            // Rotate so local Y-axis aligns with capsule axis
            Quaternion capsuleRotation = Quaternion.FromToRotation(Vector3.up, axis);
            colliderGO.transform.localRotation = capsuleRotation;

            var capsule = colliderGO.AddComponent<CapsuleCollider>();
            capsule.center = Vector3.zero; // Center is now at GameObject's position
            capsule.height = distance + primitive.Radius * 2f;
            capsule.radius = primitive.Radius;
            capsule.direction = 1; // Y-axis (local Y after rotation)

            return capsule;
        }

        private void ApplyMaterialFlags(MeshRenderer renderer, long flags)
        {
            // Apply render queue based on render order
            int renderOrder = BrfFlagDecoder.GetRenderOrder(flags);
            if (renderOrder != 0 && renderer.sharedMaterial != null)
            {
                // Create instance to modify render queue
                var matInstance = new Material(renderer.sharedMaterial);
                matInstance.renderQueue = 2000 + (renderOrder * 100);
                renderer.sharedMaterial = matInstance;
            }
        }

        #endregion

        #region Decomposition (Extract data from Unity hierarchy)

        /// <summary>
        /// Extracts mesh and collision data from the current Unity hierarchy.
        /// Use this before exporting back to BRF.
        /// </summary>
        public void Decompose()
        {
            DecomposeMeshes();
            DecomposeCollision();
        }

        private void DecomposeMeshes()
        {
            _meshes.Clear();
            _lodMeshes.Clear();

            var filters = GetComponentsInChildren<MeshFilter>(true);

            foreach (var filter in filters)
            {
                if (filter.sharedMesh == null) continue;

                var renderer = filter.GetComponent<MeshRenderer>();
                var meshData = new MBModelMesh
                {
                    Mesh = filter.sharedMesh,
                    Material = renderer?.sharedMaterial,
                    MeshName = filter.sharedMesh.name
                };

                // Determine if this is a LOD mesh based on naming
                bool isLod = filter.gameObject.name.Contains("LOD", StringComparison.OrdinalIgnoreCase) &&
                             !filter.gameObject.name.Contains("LOD0", StringComparison.OrdinalIgnoreCase);

                if (isLod)
                {
                    // Extract LOD level from name
                    for (int i = 1; i <= 5; i++)
                    {
                        if (filter.gameObject.name.Contains($"LOD{i}"))
                        {
                            meshData.LodLevel = i;
                            break;
                        }
                    }

                    _lodMeshes.Add(meshData);
                }
                else
                {
                    meshData.LodLevel = 0;
                    _meshes.Add(meshData);
                }
            }
        }

        private void DecomposeCollision()
        {
            _collision = new MBModelCollision();

            var colliders = GetComponentsInChildren<Collider>(true);

            foreach (var collider in colliders)
            {
                switch (collider)
                {
                    case SphereCollider sphere:
                        Vector3 sphereCenterWorld = sphere.transform.TransformPoint(sphere.center);
                        Vector3 sphereCenterLocal = transform.InverseTransformPoint(sphereCenterWorld);
                        _collision.Primitives.Add(new MBCollisionPrimitive
                        {
                            Type = MBCollisionType.Sphere,
                            Center = MBModelMesh.ConvertToBRF(sphereCenterLocal),
                            Radius = sphere.radius
                        });
                        break;

                    case CapsuleCollider capsule:
                        var halfHeight = (capsule.height - capsule.radius * 2f) * 0.5f;
                        var localAxis = capsule.direction switch
                        {
                            0 => Vector3.right,
                            1 => Vector3.up,
                            _ => Vector3.forward
                        };
                        
                        Vector3 capCenterWorld = capsule.transform.TransformPoint(capsule.center);
                        Vector3 p1World = capsule.transform.TransformPoint(capsule.center - localAxis * halfHeight);
                        Vector3 p2World = capsule.transform.TransformPoint(capsule.center + localAxis * halfHeight);
                        
                        Vector3 capCenterLocal = transform.InverseTransformPoint(capCenterWorld);
                        Vector3 p1Local = transform.InverseTransformPoint(p1World);
                        Vector3 p2Local = transform.InverseTransformPoint(p2World);

                        _collision.Primitives.Add(new MBCollisionPrimitive
                        {
                            Type = MBCollisionType.Capsule,
                            Center = MBModelMesh.ConvertToBRF(capCenterLocal),
                            Point1 = MBModelMesh.ConvertToBRF(p1Local),
                            Point2 = MBModelMesh.ConvertToBRF(p2Local),
                            Radius = capsule.radius
                        });
                        break;

                    case BoxCollider box:
                        Vector3 boxCenterWorld = box.transform.TransformPoint(box.center);
                        Vector3 boxCenterLocal = transform.InverseTransformPoint(boxCenterWorld);
                        _collision.Primitives.Add(new MBCollisionPrimitive
                        {
                            Type = MBCollisionType.Box,
                            Center = MBModelMesh.ConvertToBRF(boxCenterLocal),
                            Size = MBModelMesh.ConvertToBRF(box.size)
                        });
                        break;

                    case MeshCollider meshCollider:
                        _collision.CollisionMesh = meshCollider.sharedMesh;
                        _collision.IsConvex = meshCollider.convex;
                        break;
                }
            }
        }

        #endregion

        #region Utility Methods

        /// <summary>
        /// Clears all child objects (mesh renderers, colliders, LOD groups)
        /// </summary>
        public void ClearHierarchy()
        {
            // Remove LOD group
            if (_lodGroup != null)
            {
                if (Application.isPlaying)
                    Destroy(_lodGroup);
                else
                    DestroyImmediate(_lodGroup);
                _lodGroup = null;
            }

            // Remove all children
            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                var child = transform.GetChild(i).gameObject;
                if (Application.isPlaying)
                    Destroy(child);
                else
                    DestroyImmediate(child);
            }

            _renderers.Clear();
            _colliders.Clear();
        }

        /// <summary>
        /// Recalculates bounds for the entire model
        /// </summary>
        public Bounds CalculateBounds()
        {
            var bounds = new Bounds(transform.position, Vector3.zero);
            bool initialized = false;

            foreach (var renderer in GetComponentsInChildren<Renderer>())
            {
                if (!initialized)
                {
                    bounds = renderer.bounds;
                    initialized = true;
                }
                else
                {
                    bounds.Encapsulate(renderer.bounds);
                }
            }

            return bounds;
        }

        /// <summary>
        /// Validates that the model has all required data
        /// </summary>
        public bool Validate(out string error)
        {
            error = null;

            if (string.IsNullOrEmpty(_modelId))
            {
                error = "Model ID is not set";
                return false;
            }

            if (_meshes == null || _meshes.Count == 0)
            {
                error = "No meshes assigned";
                return false;
            }

            if (_meshes[0].Mesh == null)
            {
                error = "Primary mesh is null";
                return false;
            }

            return true;
        }

        #endregion
    }

    #region Supporting Data Structures

    /// <summary>
    /// Represents a single mesh with its material and BRF metadata
    /// </summary>
    [Serializable]
    public class MBModelMesh
    {
        public string MeshName = "";
        public Mesh Mesh;
        public Material Material;
        public string MaterialName = "";
        public long MaterialFlags;
        public int LodLevel;

        /// <summary>
        /// Converts coordinate system from BRF (Z-up) to Unity (Y-up)
        /// </summary>
        public static Vector3 ConvertFromBRF(Vector3 brfCoord)
        {
            return new Vector3(brfCoord.x, brfCoord.z, brfCoord.y);
        }

        /// <summary>
        /// Converts coordinate system from Unity (Y-up) to BRF (Z-up)
        /// </summary>
        public static Vector3 ConvertToBRF(Vector3 unityCoord)
        {
            return new Vector3(unityCoord.x, unityCoord.z, unityCoord.y);
        }
    }

    /// <summary>
    /// Container for all collision data associated with a model
    /// </summary>
    [Serializable]
    public class MBModelCollision
    {
        public string BodyName = "";
        public List<MBCollisionPrimitive> Primitives = new();
        public Mesh CollisionMesh;
        public bool IsConvex;
        public long Flags;

        public bool HasData => (Primitives != null && Primitives.Count > 0) || CollisionMesh != null;
    }

    /// <summary>
    /// Represents a single collision primitive (sphere, capsule, box)
    /// </summary>
    [Serializable]
    public class MBCollisionPrimitive
    {
        public MBCollisionType Type;
        public Vector3 Center;
        public Vector3 Point1; // For capsule: bottom point
        public Vector3 Point2; // For capsule: top point
        public Vector3 Size; // For box
        public float Radius;
    }

    /// <summary>
    /// Collision primitive types matching BRF body definitions
    /// </summary>
    public enum MBCollisionType
    {
        Sphere,
        Capsule,
        Box,
        Manifold // Mesh-based collision
    }

    #endregion
}