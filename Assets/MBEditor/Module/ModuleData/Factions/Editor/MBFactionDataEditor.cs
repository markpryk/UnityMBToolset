using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Numerics;

namespace MountAndBlade.Data.Editor
{
    /// <summary>
    /// Custom editor for MBFactionData ScriptableObjects
    /// Enhanced with decoder integration and user-friendly features
    /// </summary>
    [CustomEditor(typeof(MBFactionData))]
    public class MBFactionDataEditor : UnityEditor.Editor
    {
        // Foldout states
        private bool showBasicInfo = true;
        private bool showFlags = true;
        private bool showRelations = true;
        private bool showColor = true;

        // Flag editing state
        private bool flagAlwaysHideLabel = false;
        private int maxPlayerRating = 100;
        private bool hasMaxRating = false;

        // Color preview
        private Color colorPreview = Color.white;

        // Validation cache
        private bool needsValidation = true;
        private List<string> validationWarnings = new List<string>();

        private void OnEnable()
        {
            // Parse flags when editor loads
            MBFactionData faction = (MBFactionData)target;
            ParseCurrentFlags(faction);
        }

        public override void OnInspectorGUI()
        {
            MBFactionData faction = (MBFactionData)target;

            serializedObject.Update();

            // Header
            EditorGUILayout.Space(5);
            EditorGUILayout.LabelField("Mount & Blade Faction Data", EditorStyles.boldLabel);
            EditorGUILayout.Space(5);

            // Validation warnings at top
            if (needsValidation)
            {
                ValidateFaction(faction);
                needsValidation = false;
            }

            // Basic Information
            DrawBasicInfo(faction);
            EditorGUILayout.Space(10);

            // Flags with decoder integration
            DrawFlagsWithDecoder(faction);
            EditorGUILayout.Space(10);

            // Color
            DrawColor(faction);
            EditorGUILayout.Space(10);

            // Relations
            DrawRelations(faction);

            serializedObject.ApplyModifiedProperties();

            if (GUI.changed)
            {
                needsValidation = true;
                EditorUtility.SetDirty(faction);
            }
        }

        // BASIC INFORMATION

        private void DrawBasicInfo(MBFactionData faction)
        {
            showBasicInfo = EditorGUILayout.Foldout(showBasicInfo, "Basic Information", true, EditorStyles.foldoutHeader);

            if (showBasicInfo)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);

                // Faction ID (read-only, important identifier)
                EditorGUI.BeginDisabledGroup(true);
                EditorGUILayout.TextField(new GUIContent("Faction ID", 
                    "Unique identifier used in module system (prefix 'fac_' is auto-added)"), 
                    faction.factionId);
                EditorGUI.EndDisabledGroup();

                // Faction Name (editable)
                faction.factionName = EditorGUILayout.TextField(
                    new GUIContent("Faction Name", "Display name shown in game"), 
                    faction.factionName);

                EditorGUILayout.Space(5);

                // Coherence with extended range and better feedback
                EditorGUILayout.LabelField("Faction Coherence", EditorStyles.boldLabel);
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);

                float oldCoherence = faction.coherence;
                faction.coherence = EditorGUILayout.Slider(
                    new GUIContent("Coherence", "Internal faction unity (-1.0 to 1.0)"),
                    faction.coherence, -1.0f, 1.0f);

                // Show coherence description with color coding
                string coherenceDesc = GetCoherenceDescription(faction.coherence);
                GUIStyle coherenceStyle = new GUIStyle(EditorStyles.miniLabel);
                
                if (faction.coherence < 0)
                    coherenceStyle.normal.textColor = new Color(1f, 0.3f, 0.3f); // Red for negative
                else if (faction.coherence >= 0.9f)
                    coherenceStyle.normal.textColor = new Color(0.3f, 1f, 0.3f); // Green for high
                
                EditorGUILayout.LabelField("  " + coherenceDesc, coherenceStyle);

                // Warning for negative coherence
                if (faction.coherence < 0)
                {
                    EditorGUILayout.HelpBox(
                        "⚠ Negative coherence causes in-fighting within the faction! Members may attack each other.",
                        MessageType.Warning);
                }

                EditorGUILayout.EndVertical();

                EditorGUILayout.EndVertical();
                EditorGUI.indentLevel--;
            }
        }

        // FLAGS WITH DECODER INTEGRATION

        private void DrawFlagsWithDecoder(MBFactionData faction)
        {
            showFlags = EditorGUILayout.Foldout(showFlags, "Faction Flags", true, EditorStyles.foldoutHeader);

            if (showFlags)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);

                // User-friendly flag controls
                EditorGUILayout.LabelField("Flag Settings", EditorStyles.boldLabel);

                // Always Hide Label checkbox
                bool newHideLabel = EditorGUILayout.Toggle(
                    new GUIContent("Always Hide Label", 
                        "Hides faction name in popup tooltips (used by Innocents/Merchants)"),
                    flagAlwaysHideLabel);

                // Max Player Rating
                EditorGUILayout.BeginHorizontal();
                bool newHasMaxRating = EditorGUILayout.Toggle(
                    new GUIContent("Limit Max Rating", 
                        "Cap maximum reputation player can achieve with this faction"),
                    hasMaxRating,
                    GUILayout.Width(150));

                EditorGUILayout.EndHorizontal();

                EditorGUILayout.Space(5);

                EditorGUI.BeginDisabledGroup(!newHasMaxRating);
                int newMaxRating = EditorGUILayout.IntSlider(maxPlayerRating, -100, 100);
                EditorGUI.EndDisabledGroup();

                // Update flags if changed
                if (newHideLabel != flagAlwaysHideLabel || 
                    newHasMaxRating != hasMaxRating || 
                    newMaxRating != maxPlayerRating)
                {
                    flagAlwaysHideLabel = newHideLabel;
                    hasMaxRating = newHasMaxRating;
                    maxPlayerRating = newMaxRating;
                    
                    // Encode back to string
                    EncodeFlagsToString(faction);
                }

                EditorGUILayout.EndVertical();

                EditorGUI.indentLevel--;
            }
        }

        // COLOR

        private void DrawColor(MBFactionData faction)
        {
            showColor = EditorGUILayout.Foldout(showColor, "Faction Color", true, EditorStyles.foldoutHeader);

            if (showColor)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);

                // Parse current color
                if (!string.IsNullOrEmpty(faction.factionColor))
                {
                    TryParseColor(faction.factionColor, out colorPreview);
                }

                // Color picker (main control)
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.PrefixLabel("Faction Color");
                Color newColor = EditorGUILayout.ColorField(colorPreview, GUILayout.Height(20));
                if (newColor != colorPreview)
                {
                    faction.factionColor = ColorToHtmlString(newColor);
                    colorPreview = newColor;
                }
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.EndVertical();
                EditorGUI.indentLevel--;
            }
        }

        // RELATIONS

        private void DrawRelations(MBFactionData faction)
        {
            showRelations = EditorGUILayout.Foldout(showRelations,
                $"Faction Relations ({faction.relations.Count})", true, EditorStyles.foldoutHeader);

            if (showRelations)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);

                if (faction.relations.Count == 0)
                {
                    EditorGUILayout.HelpBox("No relations defined. Relations range from -1.0 (war) to 1.0 (allied).", 
                        MessageType.Info);
                }
                else
                {
                    // Display each relation with better controls
                    for (int i = faction.relations.Count - 1; i >= 0; i--)
                    {
                        DrawRelationEntry(faction, i);
                    }
                }

                // Add new relation button
                EditorGUILayout.Space(5);
                if (GUILayout.Button("+ Add Relation", GUILayout.Height(25)))
                {
                    faction.relations.Add(new FactionRelation
                    {
                        factionId = "new_faction",
                        relationValue = 0.0f
                    });
                }

                EditorGUILayout.EndVertical();
                EditorGUI.indentLevel--;
            }
        }

        private void DrawRelationEntry(MBFactionData faction, int index)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            
            FactionRelation relation = faction.relations[index];

            EditorGUILayout.BeginHorizontal();

            // Faction ID with better styling
            EditorGUILayout.LabelField("Faction:", GUILayout.Width(50));
            string newFactionId = EditorGUILayout.TextField(relation.factionId, GUILayout.Width(150));
            
            if (newFactionId != relation.factionId)
            {
                relation.factionId = newFactionId;
                faction.relations[index] = relation;
            }

            GUILayout.FlexibleSpace();

            // Delete button
            GUI.backgroundColor = new Color(1f, 0.5f, 0.5f);
            if (GUILayout.Button("✕", GUILayout.Width(25), GUILayout.Height(18)))
            {
                faction.relations.RemoveAt(index);
                GUI.backgroundColor = Color.white;
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.EndVertical();
                return;
            }
            GUI.backgroundColor = Color.white;

            EditorGUILayout.EndHorizontal();

            // Relation value slider with color coding
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Value:", GUILayout.Width(50));
            
            float oldValue = relation.relationValue;
            float newValue = GUILayout.HorizontalSlider(relation.relationValue, -1.0f, 1.0f);
            
            // Numeric input
            newValue = EditorGUILayout.FloatField(newValue, GUILayout.Width(50));
            newValue = Mathf.Clamp(newValue, -1.0f, 1.0f);

            if (newValue != oldValue)
            {
                relation.relationValue = newValue;
                faction.relations[index] = relation;
            }

            EditorGUILayout.EndHorizontal();

            // Relation description with color
            string desc = GetRelationDescription(relation.relationValue);
            GUIStyle descStyle = new GUIStyle(EditorStyles.miniLabel);
            descStyle.normal.textColor = GetRelationColor(relation.relationValue);
            EditorGUILayout.LabelField("  " + desc, descStyle);

            // War warning
            if (relation.relationValue < 0)
            {
                EditorGUILayout.LabelField("  ⚔ At war - factions will attack each other", 
                    new GUIStyle(EditorStyles.miniLabel) { normal = { textColor = new Color(1f, 0.3f, 0.3f) } });
            }

            EditorGUILayout.EndVertical();
            EditorGUILayout.Space(3);
        }

        // VALIDATION

        private void ValidateFaction(MBFactionData faction)
        {
            validationWarnings.Clear();

            // Check for empty/invalid ID
            if (string.IsNullOrWhiteSpace(faction.factionId))
            {
                validationWarnings.Add("Faction ID is empty - this faction cannot be referenced");
            }

            // Check for empty name
            if (string.IsNullOrWhiteSpace(faction.factionName))
            {
                validationWarnings.Add("Faction name is empty");
            }

            // Check duplicate relations
            HashSet<string> seenFactions = new HashSet<string>();
            for (int i = 0; i < faction.relations.Count; i++)
            {
                string factionId = faction.relations[i].factionId;
                if (seenFactions.Contains(factionId))
                {
                    validationWarnings.Add($"Duplicate relation entry for faction '{factionId}'");
                }
                seenFactions.Add(factionId);
            }

            // Check color
            if (!string.IsNullOrEmpty(faction.factionColor))
            {
                if (!TryParseColor(faction.factionColor, out _))
                {
                    validationWarnings.Add($"Invalid color format '{faction.factionColor}'");
                }
            }
        }

        // FLAG PARSING/ENCODING HELPERS

        private void ParseCurrentFlags(MBFactionData faction)
        {
            if (string.IsNullOrEmpty(faction.flags) || faction.flags == "0")
            {
                flagAlwaysHideLabel = false;
                hasMaxRating = false;
                maxPlayerRating = 100;
                return;
            }

            var (flags, maxRating) = FactionFlagPropertyDecoder.DecodeAllFlags(faction.flags);
            
            flagAlwaysHideLabel = flags.Contains("ff_always_hide_label");
            hasMaxRating = maxRating.HasValue;
            maxPlayerRating = maxRating ?? 100;
        }

        private void EncodeFlagsToString(MBFactionData faction)
        {
            var activeFlags = new HashSet<string>();
            
            if (flagAlwaysHideLabel)
            {
                activeFlags.Add("ff_always_hide_label");
            }

            int? rating = hasMaxRating ? (int?)maxPlayerRating : null;
            
            BigInteger encoded = FactionFlagPropertyDecoder.EncodeAllFlags(activeFlags, rating);
            
            if (encoded == 0)
            {
                faction.flags = "0";
            }
            else
            {
                faction.flags = "0x" + encoded.ToString("X");
            }
        }

        // DESCRIPTION HELPERS

        private string GetCoherenceDescription(float coherence)
        {
            if (coherence < -0.5f)
                return "Extreme In-Fighting (members attack each other frequently)";
            else if (coherence < 0.0f)
                return "Internal Conflict (members may attack each other)";
            else if (coherence < 0.1f)
                return "No Organization (very loose association)";
            else if (coherence < 0.5f)
                return "Loosely Organized (temporary alliances)";
            else if (coherence < 0.9f)
                return "Moderately Cohesive (organized groups)";
            else
                return "Very Cohesive (kingdom-level organization)";
        }

        private string GetRelationDescription(float value)
        {
            if (value >= 0.9f)
                return "Allied (0.9 to 1.0)";
            else if (value >= 0.5f)
                return "Friendly (0.5 to 0.9)";
            else if (value >= 0.1f)
                return "Positive (0.1 to 0.5)";
            else if (value >= -0.1f)
                return "Neutral (-0.1 to 0.1)";
            else if (value >= -0.5f)
                return "Unfriendly (-0.5 to -0.1)";
            else if (value >= -0.9f)
                return "Hostile (-0.9 to -0.5)";
            else
                return "War (-1.0 to -0.9)";
        }

        private Color GetRelationColor(float value)
        {
            if (value >= 0.5f)
                return new Color(0.3f, 1f, 0.3f); // Green - friendly
            else if (value >= 0.0f)
                return new Color(0.7f, 1f, 0.7f); // Light green - positive
            else if (value >= -0.5f)
                return new Color(1f, 0.8f, 0.3f); // Orange - unfriendly
            else
                return new Color(1f, 0.3f, 0.3f); // Red - war
        }

        // COLOR HELPERS

        private bool TryParseColor(string htmlColor, out Color color)
        {
            color = Color.white;

            if (string.IsNullOrEmpty(htmlColor))
                return false;

            if (!htmlColor.StartsWith("#"))
                htmlColor = "#" + htmlColor;

            return ColorUtility.TryParseHtmlString(htmlColor, out color);
        }

        private string ColorToHtmlString(Color color)
        {
            return "#" + ColorUtility.ToHtmlStringRGB(color);
        }
    }
}
