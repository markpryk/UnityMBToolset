using UnityEngine;
using MountAndBlade.Data;

namespace WarbandParticles
{
    /// <summary>
    /// MonoBehaviour wrapper for WarbandParticleSystem.
    /// Drop on any GameObject, assign MBParticleSystemData + Mesh + Material → particles.
    /// 
    /// Rendering uses the RGL engine approach: a combined dynamic mesh rebuilt
    /// each frame with all particles baked into vertex data (positions, colors).
    /// A MeshFilter + MeshRenderer on this GameObject displays the result,
    /// which Unity handles natively in both edit and play mode without flickering.
    /// 
    /// Works in Edit mode (via EditorApplication.update) and Play mode.
    /// </summary>
    [ExecuteInEditMode]
    [AddComponentMenu("Mount & Blade/Warband Particle Emitter")]
    public class WarbandParticleEmitter : MonoBehaviour
    {

        [Header("Data Source")]
        [Tooltip("MBParticleSystemData ScriptableObject from the importer")]
        [SerializeField] private MBParticleSystemData _particleData;

        [Header("Rendering")]
        [Tooltip("Particle mesh from BRF")]
        [SerializeField] private Mesh _particleMesh;

        [Tooltip("Particle material (must support vertex colors)")]
        [SerializeField] private Material _particleMaterial;

        [Header("Coordinate System")]
        [SerializeField] private bool _useWarbandCoordinates = true;

        [Header("Emission")]
        [SerializeField] private bool _emitOnEnable = true;
        [Tooltip("0 = continuous (uses AlwaysEmit flag from data)")]
        [SerializeField] private int _burstStrength = 0;
        [SerializeField] private float _waterLevel = 0f;

        [Header("View")]
        [SerializeField] private bool _useCustomViewPosition = false;
        [SerializeField] private Vector3 _customViewPosition = new Vector3(0, 0, -10);

        [Header("Editor Preview")]
        [SerializeField] private bool _previewInEditor = true;
        [SerializeField, Range(0.1f, 3f)] private float _previewSpeed = 1f;

        [Header("Debug")]
        [SerializeField] private bool _showEmitBox = true;
        [SerializeField] private bool _showBounds = false;


        private WarbandParticleSystem _system;
        private WarbandParticleRenderer _renderer;
        private MBParticleSystemData _lastAppliedData;
        private bool _isInitialized;
        private double _lastEditorTime;


        public WarbandParticleSystem System => _system;
        public int ActiveParticleCount => _system?.ActiveParticleCount ?? 0;
        public bool IsAlive => _system?.IsAlive ?? false;

        public MBParticleSystemData ParticleData
        {
            get => _particleData;
            set { _particleData = value; Reinitialize(); }
        }

        public void Emit(int strength)
        {
            EnsureInitialized();
            _system?.Emit(strength);
        }

        public void Stop() => _system?.ResetState();

        public void Restart() => Reinitialize();


        private void OnEnable()
        {
            Reinitialize();

#if UNITY_EDITOR
            _lastEditorTime = UnityEditor.EditorApplication.timeSinceStartup;
            UnityEditor.EditorApplication.update -= EditorUpdate;
            UnityEditor.EditorApplication.update += EditorUpdate;
#endif
        }

        private void OnDisable()
        {
            CleanupRenderer();

#if UNITY_EDITOR
            UnityEditor.EditorApplication.update -= EditorUpdate;
#endif
        }

        private void OnDestroy()
        {
            CleanupRenderer();
        }

        /// <summary>
        /// Play mode: simulation tick
        /// </summary>
        private void Update()
        {
            if (!Application.isPlaying || !_isInitialized)
                return;

            _system.FrameMove(Time.deltaTime);

            // Update custom view position
            if (_useCustomViewPosition && _renderer != null)
                _renderer.SetViewPosition(_customViewPosition);
        }

        /// <summary>
        /// Play mode: rebuild combined mesh after all simulation updates.
        /// Edit mode rendering is handled by EditorUpdate + MeshFilter persistence.
        /// </summary>
        private void LateUpdate()
        {
            if (!Application.isPlaying || !_isInitialized) return;

            // Ensure renderer exists
            if (_renderer == null && _system.ParticleMesh != null && _system.Material != null)
                InitializeRenderer();

            if (_renderer != null)
                _renderer.UpdateMesh(_system.Particles);
        }

#if UNITY_EDITOR
        /// <summary>
        /// Edit mode: simulation + mesh rebuild tick.
        /// The MeshFilter/MeshRenderer persists the result between frames.
        /// </summary>
        private void EditorUpdate()
        {
            if (Application.isPlaying || !_previewInEditor || !_isInitialized)
                return;

            double now = UnityEditor.EditorApplication.timeSinceStartup;
            float dt = Mathf.Min((float)(now - _lastEditorTime), 0.05f);
            _lastEditorTime = now;

            if (dt <= 0f) return;

            // Simulate
            _system.FrameMove(dt * _previewSpeed);

            // Ensure renderer
            if (_renderer == null && _system.ParticleMesh != null && _system.Material != null)
                InitializeRenderer();

            // Update view position
            if (_renderer != null)
            {
                if (_useCustomViewPosition)
                    _renderer.SetViewPosition(_customViewPosition);

                // Rebuild the combined mesh - MeshFilter keeps it between repaints
                _renderer.UpdateMesh(_system.Particles);
            }

            // Request repaint so the scene view shows updated particles
            if (_system.IsAlive)
                UnityEditor.SceneView.RepaintAll();
        }
#endif

        private void OnValidate()
        {
            if (_particleData != _lastAppliedData)
            {
#if UNITY_EDITOR
                UnityEditor.EditorApplication.delayCall += () =>
                {
                    if (this != null) Reinitialize();
                };
#endif
            }
        }


        private void EnsureInitialized()
        {
            if (!_isInitialized) Reinitialize();
        }

        private void Reinitialize()
        {
            CleanupRenderer();

            // Create the particle system (plain class)
            _system = new WarbandParticleSystem();
            _system.useWarbandCoordinates = _useWarbandCoordinates;
            _system.SetEmitterTransform(transform);

            // Load data from ScriptableObject
            if (_particleData != null)
            {
                _system.InitializeFromData(_particleData);
                _lastAppliedData = _particleData;
            }
            else
            {
                _lastAppliedData = null;
            }

            // Override mesh/material from inspector
            if (_particleMesh != null)
                _system.ParticleMesh = _particleMesh;

            if (_particleMaterial != null)
                _system.Material = _particleMaterial;

            _system.SetWaterLevel(_waterLevel);

            // Start emission
            if (_emitOnEnable)
            {
                if (_burstStrength > 0)
                    _system.Emit(_burstStrength);
                // AlwaysEmit flag is handled inside EmitParticles() each frame
            }

            // Create renderer
            InitializeRenderer();

            _isInitialized = true;
        }

        private void InitializeRenderer()
        {
            if (_system.ParticleMesh == null || _system.Material == null)
                return;

            if (_renderer != null) return; // Already initialized

            _renderer = new WarbandParticleRenderer(_system, gameObject);

            if (_useCustomViewPosition)
                _renderer.SetViewPosition(_customViewPosition);
        }

        private void CleanupRenderer()
        {
            if (_renderer != null)
            {
                _renderer.Dispose();
                _renderer = null;
            }
            _isInitialized = false;
        }


        private void OnDrawGizmosSelected()
        {
            if (_particleData == null && _system == null) return;

            Matrix4x4 rotMatrix = Matrix4x4.TRS(transform.position, transform.rotation, Vector3.one);
            Gizmos.matrix = rotMatrix;

            if (_showEmitBox && _particleData != null)
            {
                Vector3 boxSize = _useWarbandCoordinates
                    ? new Vector3(_particleData.EmitBoxSize.x, _particleData.EmitBoxSize.z, _particleData.EmitBoxSize.y)
                    : _particleData.EmitBoxSize;

                Gizmos.color = new Color(0, 1, 0, 0.3f);
                Gizmos.DrawCube(Vector3.zero, boxSize);

                Vector3 emitDir = _useWarbandCoordinates
                    ? CoordinateConverter.WarbandToUnityDirection(_particleData.EmitVelocity.normalized)
                    : _particleData.EmitVelocity.normalized;

                Gizmos.color = Color.blue;
                Gizmos.DrawRay(Vector3.zero, emitDir * _particleData.EmitVelocity.magnitude);
            }

            Gizmos.matrix = Matrix4x4.identity;

            if (_showBounds && _system != null && _system.Radius > 0)
            {
                Gizmos.color = new Color(1f, 1f, 0f, 0.15f);
                Gizmos.DrawWireSphere(transform.position, _system.Radius);
            }

            // Debug view position
            if (_useCustomViewPosition)
            {
                Gizmos.color = Color.yellow;
                Gizmos.DrawWireSphere(_customViewPosition, 0.2f);
                Gizmos.DrawRay(transform.position, (_customViewPosition - transform.position).normalized * 2f);
            }

            // Particle dots
            if (_system != null && _system.Particles != null)
            {
                int count = Mathf.Min(_system.Particles.Count, 200);
                for (int i = 0; i < count; i++)
                {
                    var p = _system.Particles[i];
                    Gizmos.color = _system.GetColorAtTime(p.Time);
                    float s = _system.GetScaleAtTime(p.Time) * p.Size * 0.1f;
                    Gizmos.DrawSphere(p.Position, s);
                }
            }
        }
    }

    // CUSTOM EDITOR

#if UNITY_EDITOR
    [UnityEditor.CustomEditor(typeof(WarbandParticleEmitter))]
    public class WarbandParticleEmitterEditor : UnityEditor.Editor
    {
        private WarbandParticleEmitter _emitter;
        private bool _showColorPreview = true;
        private bool _showRuntimeStats = true;

        private void OnEnable() => _emitter = (WarbandParticleEmitter)target;

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            // Data source
            UnityEditor.EditorGUILayout.Space(5);
            UnityEditor.EditorGUILayout.LabelField("Data Source", UnityEditor.EditorStyles.boldLabel);
            var dataProp = serializedObject.FindProperty("_particleData");
            UnityEditor.EditorGUI.BeginChangeCheck();
            UnityEditor.EditorGUILayout.PropertyField(dataProp, new GUIContent("Particle Data"));
            if (UnityEditor.EditorGUI.EndChangeCheck())
            {
                serializedObject.ApplyModifiedProperties();
                _emitter.Restart();
            }

            // Summary badge
            if (_emitter.ParticleData != null)
            {
                var data = _emitter.ParticleData;
                UnityEditor.EditorGUILayout.BeginVertical(UnityEditor.EditorStyles.helpBox);
                UnityEditor.EditorGUILayout.LabelField($"✨ psys_{data.ParticleSystemID}", UnityEditor.EditorStyles.boldLabel);

                if (int.TryParse(data.Flags, out int flags))
                {
                    UnityEditor.EditorGUILayout.BeginHorizontal();
                    var d = ParticleSystemFlagsDecoder.DecodeComplete(flags);
                    if (!string.IsNullOrEmpty(d.BillboardMode))
                        Badge($"🖼️ {d.BillboardMode.Replace("psf_", "")}", new Color(0.3f, 0.6f, 0.8f));
                    if (d.AlwaysEmits)       Badge("🔄 Always",  new Color(0.6f, 0.8f, 0.3f));
                    if (d.GlobalEmitDir)     Badge("🌍 Global",  new Color(0.8f, 0.6f, 0.2f));
                    if (d.RandomizeSize)     Badge("📐 RndSize", new Color(0.7f, 0.5f, 0.8f));
                    if (d.RandomizeRotation) Badge("🔀 RndRot",  new Color(0.7f, 0.5f, 0.8f));
                    UnityEditor.EditorGUILayout.EndHorizontal();
                }

                UnityEditor.EditorGUILayout.LabelField(
                    $"Mesh: {data.MeshName} | {data.NumParticlesPerSecond}/s | Life: {data.ParticleLife:F2}s | Gravity: {data.GravityStrength:F1}",
                    UnityEditor.EditorStyles.wordWrappedMiniLabel);
                UnityEditor.EditorGUILayout.EndVertical();
            }

            // Rendering
            UnityEditor.EditorGUILayout.Space(3);
            UnityEditor.EditorGUILayout.LabelField("Rendering", UnityEditor.EditorStyles.boldLabel);
            UnityEditor.EditorGUILayout.PropertyField(serializedObject.FindProperty("_particleMesh"), new GUIContent("Mesh"));
            UnityEditor.EditorGUILayout.PropertyField(serializedObject.FindProperty("_particleMaterial"), new GUIContent("Material"));

            if (serializedObject.FindProperty("_particleMaterial").objectReferenceValue == null)
                UnityEditor.EditorGUILayout.HelpBox("⚠️ No material - particles won't render!", UnityEditor.MessageType.Warning);
            if (serializedObject.FindProperty("_particleMesh").objectReferenceValue == null)
                UnityEditor.EditorGUILayout.HelpBox("⚠️ No mesh - particles won't render!", UnityEditor.MessageType.Warning);

            // Settings
            UnityEditor.EditorGUILayout.Space(3);
            UnityEditor.EditorGUILayout.PropertyField(serializedObject.FindProperty("_useWarbandCoordinates"), new GUIContent("Warband Coords"));

            UnityEditor.EditorGUILayout.Space(3);
            UnityEditor.EditorGUILayout.LabelField("Emission", UnityEditor.EditorStyles.boldLabel);
            UnityEditor.EditorGUILayout.PropertyField(serializedObject.FindProperty("_emitOnEnable"));
            UnityEditor.EditorGUILayout.PropertyField(serializedObject.FindProperty("_burstStrength"));
            UnityEditor.EditorGUILayout.PropertyField(serializedObject.FindProperty("_waterLevel"));

            UnityEditor.EditorGUILayout.Space(3);
            UnityEditor.EditorGUILayout.LabelField("View", UnityEditor.EditorStyles.boldLabel);
            UnityEditor.EditorGUILayout.PropertyField(serializedObject.FindProperty("_useCustomViewPosition"));
            if (serializedObject.FindProperty("_useCustomViewPosition").boolValue)
                UnityEditor.EditorGUILayout.PropertyField(serializedObject.FindProperty("_customViewPosition"));

            UnityEditor.EditorGUILayout.Space(3);
            UnityEditor.EditorGUILayout.LabelField("Editor Preview", UnityEditor.EditorStyles.boldLabel);
            UnityEditor.EditorGUILayout.PropertyField(serializedObject.FindProperty("_previewInEditor"));
            UnityEditor.EditorGUILayout.PropertyField(serializedObject.FindProperty("_previewSpeed"));

            UnityEditor.EditorGUILayout.Space(3);
            UnityEditor.EditorGUILayout.LabelField("Debug", UnityEditor.EditorStyles.boldLabel);
            UnityEditor.EditorGUILayout.PropertyField(serializedObject.FindProperty("_showEmitBox"));
            UnityEditor.EditorGUILayout.PropertyField(serializedObject.FindProperty("_showBounds"));

            // Color preview
            if (_emitter.ParticleData != null && _emitter.System != null)
            {
                UnityEditor.EditorGUILayout.Space(5);
                _showColorPreview = UnityEditor.EditorGUILayout.BeginFoldoutHeaderGroup(_showColorPreview, "🎨 Color Over Lifetime");
                if (_showColorPreview)
                {
                    var sys = _emitter.System;
                    float[] times = { 0f, 0.15f, 0.3f, 0.5f, 0.7f, 0.85f, 1f };
                    UnityEditor.EditorGUILayout.BeginHorizontal();
                    foreach (float t in times)
                    {
                        Color c = sys.GetColorAtTime(t);
                        float s = sys.GetScaleAtTime(t);
                        var ob = GUI.backgroundColor;
                        GUI.backgroundColor = new Color(c.r, c.g, c.b, 1f);
                        GUILayout.Button(new GUIContent($"t={t:F2}\nα={c.a:F2}\ns={s:F2}"),
                            GUILayout.Height(50), GUILayout.ExpandWidth(true));
                        GUI.backgroundColor = ob;
                    }
                    UnityEditor.EditorGUILayout.EndHorizontal();
                }
                UnityEditor.EditorGUILayout.EndFoldoutHeaderGroup();
            }

            // Runtime stats
            if (_emitter.System != null)
            {
                UnityEditor.EditorGUILayout.Space(3);
                _showRuntimeStats = UnityEditor.EditorGUILayout.BeginFoldoutHeaderGroup(_showRuntimeStats, "📊 Runtime");
                if (_showRuntimeStats)
                {
                    GUI.enabled = false;
                    UnityEditor.EditorGUILayout.IntField("Active Particles", _emitter.System.ActiveParticleCount);
                    UnityEditor.EditorGUILayout.Toggle("Is Alive", _emitter.IsAlive);
                    GUI.enabled = true;
                }
                UnityEditor.EditorGUILayout.EndFoldoutHeaderGroup();
                if (_emitter.IsAlive) Repaint();
            }

            // Buttons
            UnityEditor.EditorGUILayout.Space(10);
            UnityEditor.EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("▶ Restart", GUILayout.Height(28))) _emitter.Restart();
            if (GUILayout.Button("■ Stop", GUILayout.Height(28))) _emitter.Stop();
            if (GUILayout.Button("💥 100", GUILayout.Height(28))) _emitter.Emit(100);
            if (GUILayout.Button("💥 500", GUILayout.Height(28))) _emitter.Emit(500);
            UnityEditor.EditorGUILayout.EndHorizontal();

            serializedObject.ApplyModifiedProperties();
        }

        private void Badge(string text, Color color)
        {
            var ob = GUI.backgroundColor;
            GUI.backgroundColor = color;
            GUILayout.Label(text, UnityEditor.EditorStyles.miniButton, GUILayout.Height(18));
            GUI.backgroundColor = ob;
        }
    }
#endif
}
