using UnityEngine;
using System.Collections.Generic;
using UnityEngine.Rendering;

namespace WarbandParticles
{
    /// <summary>
    /// Renders particles by building a combined dynamic mesh on CPU, matching
    /// the RGL engine approach (rglParticleSystem::render / renderParticle).
    ///
    /// Each particle's source mesh vertices are transformed (billboard, scale,
    /// rotation) and baked into a single mesh with per-vertex colors.
    /// A MeshFilter + MeshRenderer on the emitter GameObject displays the result.
    ///
    /// This avoids DrawMeshInstancedIndirect which flickers in the Unity editor
    /// because it is an immediate-mode draw that does not persist between frames.
    /// </summary>
    public class WarbandParticleRenderer
    {
        private WarbandParticleSystem _particleSystem;
        private Mesh _sourceMesh;             // The per-particle template mesh
        private Material _material;           // Runtime material instance
        private Material _sharedMaterial;     // Reference to the original asset

        // Combined mesh for rendering
        private Mesh _combinedMesh;
        private MeshFilter _meshFilter;
        private MeshRenderer _meshRenderer;
        private GameObject _owner;
        private Transform _ownerTransform;

        // Cached source mesh data (read once)
        private Vector3[] _srcVertices;
        private Vector3[] _srcNormals;
        private Vector2[] _srcUVs;
        private int[] _srcTriangles;

        // Reusable buffers for combined mesh
        private Vector3[] _vertices;
        private Vector3[] _normals;
        private Color32[] _colors;
        private Vector2[] _uvs;
        private int[] _triangles;
        private int _lastParticleCount;

        // Cached view data
        private Vector3 _lastViewPosition = new Vector3(0, 0, -10);
        private bool _hasCustomViewPosition = false;

        public WarbandParticleRenderer(WarbandParticleSystem particleSystem, GameObject owner)
        {
            _particleSystem = particleSystem;
            _sourceMesh = particleSystem.ParticleMesh;
            _sharedMaterial = particleSystem.Material;
            _owner = owner;
            _ownerTransform = owner.transform;

            // Create runtime material instance
            _material = new Material(_sharedMaterial);
            _material.name = _sharedMaterial.name + " (Instance)";

            // Cache source mesh data
            CacheSourceMesh();

            // Create the combined mesh and renderer components
            InitializeMeshComponents();
        }

        private void CacheSourceMesh()
        {
            if (_sourceMesh == null) return;

            _srcVertices = _sourceMesh.vertices;
            _srcNormals = _sourceMesh.normals;
            _srcUVs = _sourceMesh.uv;
            _srcTriangles = _sourceMesh.triangles;

            // Ensure we have normals
            if (_srcNormals == null || _srcNormals.Length == 0)
            {
                _srcNormals = new Vector3[_srcVertices.Length];
                for (int i = 0; i < _srcNormals.Length; i++)
                    _srcNormals[i] = Vector3.forward;
            }

            // Ensure we have UVs
            if (_srcUVs == null || _srcUVs.Length == 0)
            {
                _srcUVs = new Vector2[_srcVertices.Length];
            }
        }

        private void InitializeMeshComponents()
        {
            _combinedMesh = new Mesh();
            _combinedMesh.name = "WarbandParticles_Combined";
            _combinedMesh.MarkDynamic();

            // Get or create MeshFilter
            _meshFilter = _owner.GetComponent<MeshFilter>();
            if (_meshFilter == null)
                _meshFilter = _owner.AddComponent<MeshFilter>();
            _meshFilter.sharedMesh = _combinedMesh;

            // Get or create MeshRenderer
            _meshRenderer = _owner.GetComponent<MeshRenderer>();
            if (_meshRenderer == null)
                _meshRenderer = _owner.AddComponent<MeshRenderer>();

            _meshRenderer.sharedMaterial = _material;
            _meshRenderer.shadowCastingMode = ShadowCastingMode.Off;
            _meshRenderer.receiveShadows = false;
            _meshRenderer.lightProbeUsage = LightProbeUsage.Off;
            _meshRenderer.reflectionProbeUsage = ReflectionProbeUsage.Off;

            // Hide from hierarchy to avoid confusion
            _meshFilter.hideFlags = HideFlags.HideInInspector | HideFlags.DontSaveInEditor | HideFlags.DontSaveInBuild;
            _meshRenderer.hideFlags = HideFlags.HideInInspector | HideFlags.DontSaveInEditor | HideFlags.DontSaveInBuild;
        }

        /// <summary>
        /// Set a custom view position for billboard orientation.
        /// </summary>
        public void SetViewPosition(Vector3 position)
        {
            _lastViewPosition = position;
            _hasCustomViewPosition = true;
        }

        /// <summary>
        /// Rebuild the combined mesh from current particle state.
        /// Call this after simulation to update visuals.
        /// </summary>
        public void UpdateMesh(List<Particle> particles)
        {
            if (_sourceMesh == null || _combinedMesh == null)
                return;

            if (particles == null || particles.Count == 0)
            {
                _combinedMesh.Clear();
                _lastParticleCount = 0;
                return;
            }

            // Resolve view position
            Vector3 viewPosition = _lastViewPosition;
            if (!_hasCustomViewPosition)
            {
                Camera cam = null;
                #if UNITY_EDITOR
                if (UnityEditor.SceneView.lastActiveSceneView != null)
                    cam = UnityEditor.SceneView.lastActiveSceneView.camera;
                #endif
                if (cam == null) cam = Camera.main;
                if (cam != null) viewPosition = cam.transform.position;
            }

            int numParticles = particles.Count;
            int vertsPerParticle = _srcVertices.Length;
            int trisPerParticle = _srcTriangles.Length;
            int totalVerts = numParticles * vertsPerParticle;
            int totalTris = numParticles * trisPerParticle;

            // Reallocate buffers if particle count changed
            if (_vertices == null || _vertices.Length != totalVerts)
            {
                _vertices = new Vector3[totalVerts];
                _normals = new Vector3[totalVerts];
                _colors = new Color32[totalVerts];
                _uvs = new Vector2[totalVerts];
                _triangles = new int[totalTris];
            }

            bool useWarbandCoordinates = _particleSystem.useWarbandCoordinates;

            // Cache world-to-local matrix (same for all particles)
            Matrix4x4 worldToLocal = _ownerTransform.worldToLocalMatrix;

            // Build per-particle mesh data - mirrors rglParticleSystem::renderParticle()
            for (int p = 0; p < numParticles; p++)
            {
                Particle particle = particles[p];
                int vertStart = p * vertsPerParticle;
                int triStart = p * trisPerParticle;

                Vector3 particlePosition = particle.Position;
                Vector3 particleVelocity = particle.Velocity;
                float scale = particle.Size * _particleSystem.GetScaleAtTime(particle.Time);

                // Billboard orientation - same logic as RGL renderParticle()
                Vector3 right, up, forward;
                int billboard = (int)(_particleSystem.Flags & ParticleSystemFlags.BillboardMask);

                if (billboard == (int)ParticleSystemFlags.Billboard2D)
                {
                    // RGL: dirToCamera, u = m_rotation, computeSide, f = u x s
                    forward = (particlePosition - viewPosition).normalized;
                    up = useWarbandCoordinates
                        ? CoordinateConverter.WarbandToUnityDirection(new Vector3(0, 0, 1))
                        : Vector3.up;
                    right = Vector3.Cross(up, forward).normalized;
                    if (right.sqrMagnitude < 0.001f)
                    {
                        right = Vector3.right;
                    }
                    forward = Vector3.Cross(right, up).normalized;
                }
                else if (billboard == (int)ParticleSystemFlags.Billboard3D)
                {
                    // RGL: dirToCamera, u = Up, computeSide, u = s x f
                    forward = (particlePosition - viewPosition).normalized;
                    up = Vector3.up;
                    right = Vector3.Cross(up, forward).normalized;
                    if (right.sqrMagnitude < 0.001f)
                    {
                        right = Vector3.right;
                    }
                    up = Vector3.Cross(forward, right).normalized;
                }
                else if (billboard == (int)ParticleSystemFlags.BillboardDrop)
                {
                    // RGL: dirToCamera, u = velocity, computeSide, u = s x f
                    forward = (particlePosition - viewPosition).normalized;
                    up = particleVelocity.normalized;
                    if (up.sqrMagnitude < 0.001f)
                        up = Vector3.up;
                    right = Vector3.Cross(up, forward).normalized;
                    if (right.sqrMagnitude < 0.001f)
                    {
                        right = Vector3.right;
                    }
                    up = Vector3.Cross(forward, right).normalized;
                }
                else if (billboard == (int)ParticleSystemFlags.TurnToVelocity)
                {
                    // RGL: f = velocity, u = m_rotation, computeSide, u = s x f
                    forward = particleVelocity.normalized;
                    if (forward.sqrMagnitude < 0.001f)
                        forward = Vector3.forward;
                    up = useWarbandCoordinates
                        ? CoordinateConverter.WarbandToUnityDirection(new Vector3(0, 0, 1))
                        : Vector3.up;
                    right = Vector3.Cross(up, forward).normalized;
                    if (right.sqrMagnitude < 0.001f)
                    {
                        right = Vector3.right;
                    }
                    up = Vector3.Cross(forward, right).normalized;
                }
                else
                {
                    // No billboard - use identity or particle rotation
                    if (useWarbandCoordinates)
                    {
                        right = CoordinateConverter.WarbandToUnityDirection(new Vector3(1, 0, 0));
                        up = CoordinateConverter.WarbandToUnityDirection(new Vector3(0, 0, 1));
                        forward = CoordinateConverter.WarbandToUnityDirection(new Vector3(0, 1, 0));
                    }
                    else
                    {
                        right = Vector3.right;
                        up = Vector3.up;
                        forward = Vector3.forward;
                    }
                }

                // Billboard spin rotation - RGL: rotateY(particle.m_rotation)
                float cosRot = Mathf.Cos(particle.RotationZ);
                float sinRot = Mathf.Sin(particle.RotationZ);
                Vector3 rotRight = right * cosRot - up * sinRot;
                Vector3 rotUp = right * sinRot + up * cosRot;

                // Apply scale
                rotRight *= scale;
                rotUp *= scale;
                forward *= scale;

                // Color - matches RGL: RGB uses constant outside keys, alpha uses fade
                float red, green, blue, alpha;

                // RGL RGB interpolation: constant outside key range
                red = _particleSystem.InterpolateRGBKey(_particleSystem.RedKeys, particle.Time);
                green = _particleSystem.InterpolateRGBKey(_particleSystem.GreenKeys, particle.Time);
                blue = _particleSystem.InterpolateRGBKey(_particleSystem.BlueKeys, particle.Time);
                // RGL Alpha interpolation: fades to 0 outside key range
                alpha = _particleSystem.InterpolateKey(_particleSystem.AlphaKeys, particle.Time);

                Color32 vertexColor = new Color32(
                    (byte)Mathf.Clamp(red * 255f, 0, 255),
                    (byte)Mathf.Clamp(green * 255f, 0, 255),
                    (byte)Mathf.Clamp(blue * 255f, 0, 255),
                    (byte)Mathf.Clamp(alpha * 255f, 0, 255)
                );

                // Transform each source vertex to LOCAL space
                // MeshRenderer will apply the object transform (M) to get world space

                for (int v = 0; v < vertsPerParticle; v++)
                {
                    Vector3 srcPos = _srcVertices[v];

                    // First compute world-space position
                    Vector3 worldPos = particlePosition
                        + rotRight * srcPos.x
                        + rotUp * srcPos.y
                        + forward * srcPos.z;

                    // Then convert to local space relative to owner transform
                    _vertices[vertStart + v] = worldToLocal.MultiplyPoint3x4(worldPos);

                    // Transform normal direction to local space
                    Vector3 srcN = _srcNormals[v];
                    Vector3 worldNormal = (right * srcN.x + up * srcN.y + forward * srcN.z).normalized;
                    _normals[vertStart + v] = worldToLocal.MultiplyVector(worldNormal);

                    // Per-vertex color (RGL: faceCorner.m_color = vertexColor)
                    _colors[vertStart + v] = vertexColor;

                    // Copy UVs
                    _uvs[vertStart + v] = _srcUVs[v];
                }

                // Build triangle indices with offset
                for (int t = 0; t < trisPerParticle; t++)
                {
                    _triangles[triStart + t] = _srcTriangles[t] + vertStart;
                }
            }

            // Update the combined mesh
            _combinedMesh.Clear();
            _combinedMesh.vertices = _vertices;
            _combinedMesh.normals = _normals;
            _combinedMesh.colors32 = _colors;
            _combinedMesh.uv = _uvs;
            _combinedMesh.triangles = _triangles;

            // Recalculate bounds so Unity doesn't cull it
            _combinedMesh.RecalculateBounds();

            _lastParticleCount = numParticles;
        }

        /// <summary>
        /// Legacy API compatibility - calls UpdateMesh internally.
        /// </summary>
        public void Render(List<Particle> particles)
        {
            UpdateMesh(particles);
        }

        /// <summary>
        /// Legacy API compatibility with camera parameter.
        /// </summary>
        public void Render(List<Particle> particles, Camera camera)
        {
            if (camera != null)
                SetViewPosition(camera.transform.position);
            UpdateMesh(particles);
        }

        public void Dispose()
        {
            // Destroy combined mesh (standalone asset, safe to destroy anytime)
            if (_combinedMesh != null)
            {
                #if UNITY_EDITOR
                if (!Application.isPlaying)
                    Object.DestroyImmediate(_combinedMesh);
                else
                #endif
                    Object.Destroy(_combinedMesh);
                _combinedMesh = null;
            }

            // Remove components from owner - must defer if GameObject is
            // being activated/deactivated (e.g. during scene load)
            DestroyComponent(ref _meshFilter);
            DestroyComponent(ref _meshRenderer);

            // Destroy runtime material instance (standalone asset, safe to destroy anytime)
            if (_material != null)
            {
                #if UNITY_EDITOR
                if (!Application.isPlaying)
                    Object.DestroyImmediate(_material);
                else
                #endif
                    Object.Destroy(_material);
                _material = null;
            }

            _vertices = null;
            _normals = null;
            _colors = null;
            _uvs = null;
            _triangles = null;
        }

        private static void DestroyComponent<T>(ref T component) where T : Component
        {
            if (component == null) return;

            #if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                var toDestroy = component;
                component = null;
                // Defer destruction if called during activate/deactivate
                UnityEditor.EditorApplication.delayCall += () =>
                {
                    if (toDestroy != null)
                        Object.DestroyImmediate(toDestroy);
                };
                return;
            }
            #endif

            Object.Destroy(component);
            component = null;
        }
    }
}