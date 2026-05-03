using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;

namespace MountAndBlade.Data.Editor
{
    /// <summary>
    /// Custom editor for MBParticleSystemData with visual flag editing.
    /// Fully integrated with ParticleSystemFlagsDecoder.
    /// </summary>
    [CustomEditor(typeof(MBParticleSystemData))]
    public class MBParticleSystemEditor : UnityEditor.Editor
    {
        private MBParticleSystemData psData;
        private ParticleSystemFlagsData decodedFlags;

        // Foldout states
        private bool showIdentification = true;
        private bool showMesh = true;
        private bool showBillboardMode = true;
        private bool showPropertyFlags = true;
        private bool showEmission = true;
        private bool showPhysics = true;
        private bool showTurbulence = true;
        private bool showColorKeys = true;
        private bool showScaleKeys = true;
        private bool showEmitShape = true;
        private bool showRotation = true;
        private bool showRawData = false;

        // Flag modification tracking
        private bool flagsModified = false;
        private string modifiedBillboardMode;
        private HashSet<string> modifiedPropertyFlags;

        // GUI Styles
        private GUIStyle summaryStyle;
        private GUIStyle headerStyle;

        private void OnEnable()
        {
            psData = (MBParticleSystemData)target;
            DecodeFlags();
        }

        private void InitializeStyles()
        {
            if (summaryStyle == null)
            {
                summaryStyle = new GUIStyle(EditorStyles.helpBox);
                summaryStyle.fontSize = 11;
                summaryStyle.padding = new RectOffset(10, 10, 10, 10);

                headerStyle = new GUIStyle(EditorStyles.boldLabel);
                headerStyle.fontSize = 12;
            }
        }

        public override void OnInspectorGUI()
        {
            InitializeStyles();

            serializedObject.Update();

            EditorGUILayout.Space(5);
            DrawSummaryBadge();

            EditorGUILayout.Space(10);
            DrawIdentificationSection();

            EditorGUILayout.Space(5);
            DrawMeshSection();

            EditorGUILayout.Space(5);
            DrawBillboardModeSection();

            EditorGUILayout.Space(5);
            DrawPropertyFlagsSection();

            EditorGUILayout.Space(5);
            DrawEmissionSection();

            EditorGUILayout.Space(5);
            DrawPhysicsSection();

            EditorGUILayout.Space(5);
            DrawTurbulenceSection();

            EditorGUILayout.Space(5);
            DrawColorKeysSection();

            EditorGUILayout.Space(5);
            DrawScaleKeysSection();

            EditorGUILayout.Space(5);
            DrawEmitShapeSection();

            EditorGUILayout.Space(5);
            DrawRotationSection();

            EditorGUILayout.Space(5);
            DrawValidationSection();

            EditorGUILayout.Space(5);
            DrawRawDataSection();

            // Apply flag modifications
            if (flagsModified && !Application.isPlaying)
            {
                ApplyFlagModifications();
            }

            serializedObject.ApplyModifiedProperties();
        }

        private void DecodeFlags()
        {
            try
            {
                if (string.IsNullOrEmpty(psData.Flags))
                {
                    Debug.LogWarning($"[DecodeFlags] Flags field is empty for {psData.ParticleSystemID}, setting to '0'");
                    psData.Flags = "0";
                }

                int flags = int.Parse(psData.Flags);
                decodedFlags = ParticleSystemFlagsDecoder.DecodeComplete(flags);

                // Initialize modification tracking
                modifiedBillboardMode = decodedFlags.BillboardMode;
                modifiedPropertyFlags = new HashSet<string>(decodedFlags.PropertyFlags);
                flagsModified = false;
            }
            catch (System.Exception e)
            {
                Debug.LogError($"Failed to decode flags for {psData.ParticleSystemID}: {e.Message}");
                decodedFlags = new ParticleSystemFlagsData();
                modifiedBillboardMode = "";
                modifiedPropertyFlags = new HashSet<string>();
            }
        }

        private void ApplyFlagModifications()
        {
            try
            {
                int newFlags = ParticleSystemFlagsDecoder.EncodeFlags(
                    modifiedBillboardMode,
                    modifiedPropertyFlags.ToList()
                );

                Undo.RecordObject(psData, "Modify Particle System Flags");
                psData.Flags = newFlags.ToString();

                DecodeFlags();

                EditorUtility.SetDirty(psData);
                flagsModified = false;
            }
            catch (System.Exception e)
            {
                Debug.LogError($"Failed to apply flag modifications: {e.Message}");
            }
        }

        // SUMMARY BADGE

        private void DrawSummaryBadge()
        {
            EditorGUILayout.BeginVertical(summaryStyle);

            // Title with icon
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label("✨", GUILayout.Width(20));
            EditorGUILayout.LabelField(psData.ParticleSystemID ?? "(No ID)", EditorStyles.boldLabel);
            EditorGUILayout.EndHorizontal();

            // Category and summary
            string category = DetermineEditorCategory();
            string summary = decodedFlags?.GetSummary() ?? "";

            EditorGUILayout.LabelField($"Category: {category}");

            if (!string.IsNullOrEmpty(summary))
            {
                EditorGUILayout.LabelField(summary, EditorStyles.wordWrappedMiniLabel);
            }

            // Quick info badges
            EditorGUILayout.BeginHorizontal();

            if (!string.IsNullOrEmpty(decodedFlags?.BillboardMode))
            {
                string bbShort = decodedFlags.BillboardMode.Replace("psf_", "");
                DrawBadge($"🖼️ {bbShort}", new Color(0.3f, 0.6f, 0.8f));
            }

            if (decodedFlags != null && decodedFlags.AlwaysEmits)
                DrawBadge("🔄 Always Emit", new Color(0.6f, 0.8f, 0.3f));

            if (decodedFlags != null && decodedFlags.GlobalEmitDir)
                DrawBadge("🌍 Global Dir", new Color(0.8f, 0.6f, 0.2f));

            if (decodedFlags != null && decodedFlags.RandomizeSize)
                DrawBadge("📐 Rnd Size", new Color(0.7f, 0.5f, 0.8f));

            if (decodedFlags != null && decodedFlags.RandomizeRotation)
                DrawBadge("🔀 Rnd Rot", new Color(0.7f, 0.5f, 0.8f));

            if (decodedFlags != null && decodedFlags.IsLodLinked)
                DrawBadge("🔗 LOD Link", new Color(0.5f, 0.5f, 0.5f));

            EditorGUILayout.EndHorizontal();

            // Quick stats line
            EditorGUILayout.LabelField(
                $"Mesh: {psData.MeshName ?? "none"} | {psData.NumParticlesPerSecond}/s | Life: {psData.ParticleLife:F2}s",
                EditorStyles.wordWrappedMiniLabel
            );

            EditorGUILayout.EndVertical();
        }

        private void DrawBadge(string text, Color color)
        {
            var originalColor = GUI.backgroundColor;
            GUI.backgroundColor = color;
            GUILayout.Label(text, EditorStyles.miniButton, GUILayout.Height(18));
            GUI.backgroundColor = originalColor;
        }

        private string DetermineEditorCategory()
        {
            if (psData.ParticleSystemID == null) return "Other";

            string id = psData.ParticleSystemID.ToLowerInvariant();
            if (id.Contains("rain"))   return "Weather";
            if (id.Contains("snow"))   return "Weather";
            if (id.Contains("fire"))   return "Fire";
            if (id.Contains("smoke"))  return "Smoke";
            if (id.Contains("blood"))  return "Combat";
            if (id.Contains("dust"))   return "Dust";
            if (id.Contains("spark"))  return "Sparks";
            if (id.Contains("torch"))  return "Fire";
            if (id.Contains("water"))  return "Water";
            if (id.Contains("fog"))    return "Atmosphere";
            if (id.Contains("trail"))  return "Trail";
            if (psData.GravityStrength < 0) return "Rising";
            return "Other";
        }

        // IDENTIFICATION SECTION

        private void DrawIdentificationSection()
        {
            showIdentification = EditorGUILayout.BeginFoldoutHeaderGroup(showIdentification, "Identification");

            if (showIdentification)
            {
                EditorGUI.indentLevel++;

                EditorGUILayout.PropertyField(serializedObject.FindProperty("ParticleSystemID"),
                    new GUIContent("Particle System ID", "Identifier (psys_ prefix added automatically)"));

                EditorGUI.indentLevel--;
            }

            EditorGUILayout.EndFoldoutHeaderGroup();
        }

        // MESH SECTION

        private void DrawMeshSection()
        {
            showMesh = EditorGUILayout.BeginFoldoutHeaderGroup(showMesh, "Mesh");

            if (showMesh)
            {
                EditorGUI.indentLevel++;

                EditorGUILayout.PropertyField(serializedObject.FindProperty("MeshName"),
                    new GUIContent("Particle Mesh", "Name of the particle mesh resource (e.g., prtcl_rain, prt_mesh_snow_fall_1)"));

                if (string.IsNullOrEmpty(psData.MeshName) || psData.MeshName == "0")
                {
                    EditorGUILayout.HelpBox("⚠️ No mesh specified - particles won't render!", MessageType.Warning);
                }

                EditorGUI.indentLevel--;
            }

            EditorGUILayout.EndFoldoutHeaderGroup();
        }

        // BILLBOARD MODE SECTION

        private void DrawBillboardModeSection()
        {
            showBillboardMode = EditorGUILayout.BeginFoldoutHeaderGroup(showBillboardMode, "Billboard Mode");

            if (showBillboardMode)
            {
                EditorGUI.indentLevel++;

                EditorGUILayout.HelpBox(
                    "Billboard modes are mutually exclusive - only ONE can be active.\n" +
                    "Controls how particles orient relative to the camera.",
                    MessageType.Info
                );

                // Get all modes in sorted order
                var modes = ParticleSystemFlagsDecoder.GetBillboardModes();
                var modeOptions = new List<string> { "(No Billboard)" };
                var sortedModes = modes.Keys.OrderBy(x => modes[x]).ToList();
                modeOptions.AddRange(sortedModes);

                // Find current selection
                int currentIndex = 0;
                if (!string.IsNullOrEmpty(modifiedBillboardMode))
                {
                    currentIndex = modeOptions.IndexOf(modifiedBillboardMode);
                    if (currentIndex == -1)
                    {
                        currentIndex = 0;
                        modifiedBillboardMode = "";
                        flagsModified = true;
                    }
                }

                // Draw dropdown
                int newIndex = EditorGUILayout.Popup("Mode", currentIndex, modeOptions.ToArray());

                if (newIndex != currentIndex)
                {
                    modifiedBillboardMode = newIndex == 0 ? "" : modeOptions[newIndex];
                    flagsModified = true;
                }

                // Show description
                if (!string.IsNullOrEmpty(modifiedBillboardMode))
                {
                    string description = ParticleSystemFlagsDecoder.GetFlagDescription(modifiedBillboardMode);
                    if (!string.IsNullOrEmpty(description))
                    {
                        EditorGUILayout.HelpBox(description, MessageType.None);
                    }
                }

                EditorGUI.indentLevel--;
            }

            EditorGUILayout.EndFoldoutHeaderGroup();
        }

        // PROPERTY FLAGS SECTION

        private void DrawPropertyFlagsSection()
        {
            showPropertyFlags = EditorGUILayout.BeginFoldoutHeaderGroup(showPropertyFlags,
                $"Property Flags ({modifiedPropertyFlags.Count} active)");

            if (showPropertyFlags)
            {
                EditorGUI.indentLevel++;

                EditorGUILayout.HelpBox(
                    "Property flags can be combined - check all that apply.",
                    MessageType.Info
                );

                // Organize flags by category
                var allFlags = ParticleSystemFlagsDecoder.GetPropertyFlags();
                var categories = new Dictionary<string, List<string>>();

                foreach (var flag in allFlags.Keys)
                {
                    string category = ParticleSystemFlagsDecoder.GetFlagCategory(flag);
                    if (!categories.ContainsKey(category))
                        categories[category] = new List<string>();
                    categories[category].Add(flag);
                }

                // Draw each category
                foreach (var category in categories.OrderBy(c => c.Key))
                {
                    EditorGUILayout.LabelField(category.Key, EditorStyles.boldLabel);

                    foreach (var flag in category.Value.OrderBy(f => f))
                    {
                        DrawFlagCheckbox(flag);
                    }

                    EditorGUILayout.Space(3);
                }

                EditorGUI.indentLevel--;
            }

            EditorGUILayout.EndFoldoutHeaderGroup();
        }

        private void DrawFlagCheckbox(string flagName)
        {
            bool currentValue = modifiedPropertyFlags.Contains(flagName);
            string description = ParticleSystemFlagsDecoder.GetFlagDescription(flagName);
            string displayName = flagName.Replace("psf_", "");

            GUIContent label = new GUIContent(displayName, description);

            bool newValue = EditorGUILayout.Toggle(label, currentValue);

            if (newValue != currentValue)
            {
                if (newValue)
                    modifiedPropertyFlags.Add(flagName);
                else
                    modifiedPropertyFlags.Remove(flagName);

                flagsModified = true;
            }
        }

        // EMISSION SECTION

        private void DrawEmissionSection()
        {
            showEmission = EditorGUILayout.BeginFoldoutHeaderGroup(showEmission, "Emission");

            if (showEmission)
            {
                EditorGUI.indentLevel++;

                EditorGUILayout.PropertyField(serializedObject.FindProperty("NumParticlesPerSecond"),
                    new GUIContent("Particles/Second", "Number of particles emitted per second"));

                EditorGUILayout.PropertyField(serializedObject.FindProperty("ParticleLife"),
                    new GUIContent("Particle Life (s)", "How long each particle lives in seconds"));

                // Validation
                if (psData.NumParticlesPerSecond <= 0)
                {
                    EditorGUILayout.HelpBox("⚠️ Particle count should be > 0", MessageType.Warning);
                }

                if (psData.ParticleLife <= 0)
                {
                    EditorGUILayout.HelpBox("⚠️ Particle life should be > 0", MessageType.Warning);
                }

                // Max particles alive info
                if (psData.NumParticlesPerSecond > 0 && psData.ParticleLife > 0)
                {
                    int maxAlive = Mathf.CeilToInt(psData.NumParticlesPerSecond * psData.ParticleLife);
                    EditorGUILayout.LabelField(
                        $"Max particles alive: ~{maxAlive}",
                        EditorStyles.miniLabel
                    );
                }

                EditorGUI.indentLevel--;
            }

            EditorGUILayout.EndFoldoutHeaderGroup();
        }

        // PHYSICS SECTION

        private void DrawPhysicsSection()
        {
            showPhysics = EditorGUILayout.BeginFoldoutHeaderGroup(showPhysics, "Physics");

            if (showPhysics)
            {
                EditorGUI.indentLevel++;

                EditorGUILayout.PropertyField(serializedObject.FindProperty("Damping"),
                    new GUIContent("Damping", "How much particle speed is lost to friction [0-1]"));

                EditorGUILayout.PropertyField(serializedObject.FindProperty("GravityStrength"),
                    new GUIContent("Gravity Strength", "Effect of gravity (negative = float upwards)"));

                // Visual hints
                if (psData.GravityStrength < 0)
                {
                    EditorGUILayout.LabelField("↑ Particles float upward", EditorStyles.miniLabel);
                }
                else if (psData.GravityStrength > 0)
                {
                    EditorGUILayout.LabelField("↓ Particles fall downward", EditorStyles.miniLabel);
                }
                else
                {
                    EditorGUILayout.LabelField("- No gravity influence", EditorStyles.miniLabel);
                }

                if (psData.Damping < 0 || psData.Damping > 1)
                {
                    EditorGUILayout.HelpBox("⚠️ Damping is typically in range [0, 1]", MessageType.Warning);
                }

                EditorGUI.indentLevel--;
            }

            EditorGUILayout.EndFoldoutHeaderGroup();
        }

        // TURBULENCE SECTION

        private void DrawTurbulenceSection()
        {
            bool hasTurbulence = psData.TurbulenceSize > 0 || psData.TurbulenceStrength > 0;

            showTurbulence = EditorGUILayout.BeginFoldoutHeaderGroup(showTurbulence,
                hasTurbulence ? "🌊 Turbulence" : "Turbulence (None)");

            if (showTurbulence)
            {
                EditorGUI.indentLevel++;

                EditorGUILayout.PropertyField(serializedObject.FindProperty("TurbulenceSize"),
                    new GUIContent("Size (meters)", "Size of random turbulence in meters"));

                EditorGUILayout.PropertyField(serializedObject.FindProperty("TurbulenceStrength"),
                    new GUIContent("Strength", "How much particles are affected by turbulence"));

                // Validation
                if (psData.TurbulenceStrength > 0 && psData.TurbulenceSize <= 0)
                {
                    EditorGUILayout.HelpBox("⚠️ Turbulence strength is set but size is zero", MessageType.Warning);
                }

                if (psData.TurbulenceSize > 0 && psData.TurbulenceStrength <= 0)
                {
                    EditorGUILayout.HelpBox("ℹ️ Turbulence size set but strength is zero - no visible effect", MessageType.Info);
                }

                // 2D turbulence flag hint
                if (modifiedPropertyFlags.Contains("psf_2d_turbulance"))
                {
                    EditorGUILayout.LabelField("psf_2d_turbulance flag active - turbulence is 2D", EditorStyles.miniLabel);
                }

                EditorGUI.indentLevel--;
            }

            EditorGUILayout.EndFoldoutHeaderGroup();
        }

        // COLOR KEYS SECTION

        private void DrawColorKeysSection()
        {
            showColorKeys = EditorGUILayout.BeginFoldoutHeaderGroup(showColorKeys, "🎨 Color Keys (Alpha / RGB)");

            if (showColorKeys)
            {
                EditorGUI.indentLevel++;

                EditorGUILayout.HelpBox(
                    "Each channel has 2 keys: (time, magnitude).\n" +
                    "Time: 0 = particle birth, 1 = particle death.\n" +
                    "Values interpolate between keys, constant beyond.\n" +
                    "Alpha always starts from 0 at time 0.",
                    MessageType.Info
                );

                // Alpha
                EditorGUILayout.LabelField("Alpha", EditorStyles.boldLabel);
                DrawKeyPair("AlphaKey1", "AlphaKey2", psData.AlphaKey1, psData.AlphaKey2);

                EditorGUILayout.Space(3);

                // Red
                DrawColorKeyWithPreview("Red", "RedKey1", "RedKey2", psData.RedKey1, psData.RedKey2, Color.red);

                EditorGUILayout.Space(3);

                // Green
                DrawColorKeyWithPreview("Green", "GreenKey1", "GreenKey2", psData.GreenKey1, psData.GreenKey2, Color.green);

                EditorGUILayout.Space(3);

                // Blue
                DrawColorKeyWithPreview("Blue", "BlueKey1", "BlueKey2", psData.BlueKey1, psData.BlueKey2, new Color(0.3f, 0.5f, 1f));

                // Color preview at key points
                EditorGUILayout.Space(5);
                DrawColorPreview();

                EditorGUI.indentLevel--;
            }

            EditorGUILayout.EndFoldoutHeaderGroup();
        }

        private void DrawColorKeyWithPreview(string label, string prop1, string prop2, Vector2 key1, Vector2 key2, Color tint)
        {
            EditorGUILayout.BeginHorizontal();
            var originalColor = GUI.backgroundColor;
            GUI.backgroundColor = tint;
            EditorGUILayout.LabelField(label, EditorStyles.boldLabel, GUILayout.Width(50));
            GUI.backgroundColor = originalColor;
            EditorGUILayout.EndHorizontal();

            DrawKeyPair(prop1, prop2, key1, key2);
        }

        private void DrawKeyPair(string prop1Name, string prop2Name, Vector2 key1, Vector2 key2)
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Key 1", GUILayout.Width(40));
            EditorGUILayout.PropertyField(serializedObject.FindProperty(prop1Name), GUIContent.none);
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Key 2", GUILayout.Width(40));
            EditorGUILayout.PropertyField(serializedObject.FindProperty(prop2Name), GUIContent.none);
            EditorGUILayout.EndHorizontal();

            // Validate time range
            if (key1.x < 0 || key1.x > 1 || key2.x < 0 || key2.x > 1)
            {
                EditorGUILayout.HelpBox("⚠️ Key time values should be in [0, 1]", MessageType.Warning);
            }
        }

        private void DrawColorPreview()
        {
            EditorGUILayout.LabelField("Color Preview", EditorStyles.boldLabel);

            // Sample color at birth (t=0), midlife (t=0.5), and death (t=1)
            float[] sampleTimes = { 0f, 0.25f, 0.5f, 0.75f, 1f };

            EditorGUILayout.BeginHorizontal();

            foreach (float t in sampleTimes)
            {
                float r = SampleKey(psData.RedKey1, psData.RedKey2, t);
                float g = SampleKey(psData.GreenKey1, psData.GreenKey2, t);
                float b = SampleKey(psData.BlueKey1, psData.BlueKey2, t);
                float a = SampleAlphaKey(psData.AlphaKey1, psData.AlphaKey2, t);

                Color sampleColor = new Color(r, g, b, 1f); // Show opaque for visibility

                var originalColor = GUI.backgroundColor;
                GUI.backgroundColor = sampleColor;

                GUILayout.Button(
                    new GUIContent($"t={t:F2}\nα={a:F2}", $"R:{r:F2} G:{g:F2} B:{b:F2} A:{a:F2}"),
                    GUILayout.Height(40), GUILayout.ExpandWidth(true)
                );

                GUI.backgroundColor = originalColor;
            }

            EditorGUILayout.EndHorizontal();

            EditorGUILayout.LabelField("(Birth → Death, alpha shown as text)", EditorStyles.centeredGreyMiniLabel);
        }

        /// <summary>
        /// Samples a keyed value at time t using M&B interpolation rules.
        /// Before key1.time: constant at key1.magnitude
        /// Between keys: linear interpolation
        /// After key2.time: constant at key2.magnitude
        /// </summary>
        private float SampleKey(Vector2 key1, Vector2 key2, float t)
        {
            if (t <= key1.x)
                return key1.y;
            if (t >= key2.x)
                return key2.y;

            // Linear interpolation
            if (Mathf.Approximately(key2.x, key1.x))
                return key1.y;

            float blend = (t - key1.x) / (key2.x - key1.x);
            return Mathf.Lerp(key1.y, key2.y, blend);
        }

        /// <summary>
        /// Alpha always starts from 0 at time 0.
        /// </summary>
        private float SampleAlphaKey(Vector2 key1, Vector2 key2, float t)
        {
            if (t <= 0f)
                return 0f;

            if (t <= key1.x)
            {
                // Interpolate from 0 at t=0 to key1.magnitude at key1.time
                if (Mathf.Approximately(key1.x, 0f))
                    return key1.y;
                return Mathf.Lerp(0f, key1.y, t / key1.x);
            }

            return SampleKey(key1, key2, t);
        }

        // SCALE KEYS SECTION

        private void DrawScaleKeysSection()
        {
            showScaleKeys = EditorGUILayout.BeginFoldoutHeaderGroup(showScaleKeys, "📐 Scale Keys");

            if (showScaleKeys)
            {
                EditorGUI.indentLevel++;

                EditorGUILayout.HelpBox(
                    "Scale controls particle size over lifetime.\n" +
                    "Key format: (time, scale_multiplier)",
                    MessageType.Info
                );

                DrawKeyPair("ScaleKey1", "ScaleKey2", psData.ScaleKey1, psData.ScaleKey2);

                // Scale preview
                EditorGUILayout.Space(3);
                float scaleBirth = SampleKey(psData.ScaleKey1, psData.ScaleKey2, 0f);
                float scaleMid = SampleKey(psData.ScaleKey1, psData.ScaleKey2, 0.5f);
                float scaleDeath = SampleKey(psData.ScaleKey1, psData.ScaleKey2, 1f);
                EditorGUILayout.LabelField(
                    $"Scale: {scaleBirth:F2} → {scaleMid:F2} → {scaleDeath:F2}  (birth → mid → death)",
                    EditorStyles.miniLabel
                );

                EditorGUI.indentLevel--;
            }

            EditorGUILayout.EndFoldoutHeaderGroup();
        }

        // EMIT SHAPE & VELOCITY SECTION

        private void DrawEmitShapeSection()
        {
            showEmitShape = EditorGUILayout.BeginFoldoutHeaderGroup(showEmitShape, "📦 Emit Shape & Velocity");

            if (showEmitShape)
            {
                EditorGUI.indentLevel++;

                EditorGUILayout.PropertyField(serializedObject.FindProperty("EmitBoxSize"),
                    new GUIContent("Emit Box Size", "Dimensions of the emission box (x, y, z in meters)"));

                EditorGUILayout.PropertyField(serializedObject.FindProperty("EmitVelocity"),
                    new GUIContent("Emit Velocity", "Initial velocity of particles (x, y, z)"));

                EditorGUILayout.PropertyField(serializedObject.FindProperty("EmitDirRandomness"),
                    new GUIContent("Direction Randomness", "How much the emit direction is randomized [0 = none, 1 = fully random]"));

                // Info
                if (psData.EmitBoxSize == Vector3.zero)
                {
                    EditorGUILayout.LabelField("Point emitter (all particles from center)", EditorStyles.miniLabel);
                }
                else
                {
                    float volume = psData.EmitBoxSize.x * psData.EmitBoxSize.y * psData.EmitBoxSize.z;
                    EditorGUILayout.LabelField(
                        $"Box volume: {volume:F2} m³",
                        EditorStyles.miniLabel
                    );
                }

                float speed = psData.EmitVelocity.magnitude;
                if (speed > 0)
                {
                    EditorGUILayout.LabelField(
                        $"Initial speed: {speed:F2} m/s",
                        EditorStyles.miniLabel
                    );
                }

                EditorGUI.indentLevel--;
            }

            EditorGUILayout.EndFoldoutHeaderGroup();
        }

        // ROTATION SECTION

        private void DrawRotationSection()
        {
            bool hasRotation = psData.RotationSpeed != 0;

            showRotation = EditorGUILayout.BeginFoldoutHeaderGroup(showRotation,
                hasRotation ? "🔄 Rotation" : "Rotation (None)");

            if (showRotation)
            {
                EditorGUI.indentLevel++;

                EditorGUILayout.PropertyField(serializedObject.FindProperty("RotationSpeed"),
                    new GUIContent("Rotation Speed (°/s)", "Angular speed in degrees per second"));

                EditorGUILayout.PropertyField(serializedObject.FindProperty("RotationDamping"),
                    new GUIContent("Rotation Damping", "How quickly particles stop rotating"));

                if (psData.RotationSpeed != 0)
                {
                    float rpm = Mathf.Abs(psData.RotationSpeed) / 360f * 60f;
                    EditorGUILayout.LabelField(
                        $"~{rpm:F1} RPM {(psData.RotationSpeed > 0 ? "(CW)" : "(CCW)")}",
                        EditorStyles.miniLabel
                    );
                }

                EditorGUI.indentLevel--;
            }

            EditorGUILayout.EndFoldoutHeaderGroup();
        }

        // VALIDATION SECTION

        private void DrawValidationSection()
        {
            try
            {
                int flags = int.Parse(psData.Flags ?? "0");
                var flagIssues = ParticleSystemFlagsDecoder.ValidateFlags(flags);

                // Also run data validation
                var dataIssues = new List<string>();

                if (string.IsNullOrEmpty(psData.ParticleSystemID))
                    dataIssues.Add("Empty particle system ID");

                if (string.IsNullOrEmpty(psData.MeshName) || psData.MeshName == "0")
                    dataIssues.Add("No mesh specified");

                if (psData.NumParticlesPerSecond <= 0)
                    dataIssues.Add($"Invalid particle count: {psData.NumParticlesPerSecond}");

                if (psData.ParticleLife <= 0)
                    dataIssues.Add($"Invalid particle life: {psData.ParticleLife}");

                var allIssues = flagIssues.Concat(dataIssues).ToList();

                if (allIssues.Count > 0)
                {
                    EditorGUILayout.Space(5);
                    EditorGUILayout.LabelField("⚠️ Validation Issues", EditorStyles.boldLabel);

                    foreach (var issue in allIssues)
                    {
                        MessageType msgType = issue.StartsWith("INFO:") ? MessageType.Info : MessageType.Warning;
                        EditorGUILayout.HelpBox(issue, msgType);
                    }
                }
            }
            catch (System.Exception e)
            {
                EditorGUILayout.HelpBox($"Validation error: {e.Message}", MessageType.Error);
            }
        }

        // RAW DATA SECTION

        private void DrawRawDataSection()
        {
            showRawData = EditorGUILayout.BeginFoldoutHeaderGroup(showRawData, "Raw Data (Advanced)");

            if (showRawData)
            {
                EditorGUI.indentLevel++;

                EditorGUILayout.HelpBox(
                    "Flags field is the source of truth. Billboard mode and property flags above are derived from it.",
                    MessageType.Info
                );

                // Flags (hex display)
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("Flags (Hex)", GUILayout.Width(EditorGUIUtility.labelWidth));

                try
                {
                    int flags = int.Parse(psData.Flags ?? "0");
                    EditorGUILayout.SelectableLabel($"0x{flags:X}", EditorStyles.textField, GUILayout.Height(18));
                }
                catch
                {
                    EditorGUILayout.LabelField("(Invalid)");
                }

                EditorGUILayout.EndHorizontal();

                // Flags (decimal)
                EditorGUILayout.PropertyField(serializedObject.FindProperty("Flags"),
                    new GUIContent("Flags (Decimal)", "Raw flags value"));

                // Decoded flags list
                if (decodedFlags != null)
                {
                    EditorGUILayout.Space(3);
                    EditorGUILayout.LabelField("Decoded Components:", EditorStyles.boldLabel);

                    GUI.enabled = false;

                    EditorGUILayout.TextField("Billboard Mode",
                        string.IsNullOrEmpty(decodedFlags.BillboardMode) ? "(none)" : decodedFlags.BillboardMode);

                    EditorGUILayout.TextField("Property Flags",
                        decodedFlags.PropertyFlags.Count > 0
                            ? string.Join(", ", decodedFlags.PropertyFlags)
                            : "(none)");

                    GUI.enabled = true;
                }

                EditorGUILayout.LabelField("(Decoded values auto-updated from Flags)", EditorStyles.miniLabel);

                EditorGUI.indentLevel--;
            }

            EditorGUILayout.EndFoldoutHeaderGroup();
        }
    }
}
