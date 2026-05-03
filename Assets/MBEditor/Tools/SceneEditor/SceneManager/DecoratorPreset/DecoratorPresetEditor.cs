using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Custom editor for DecoratorPreset ScriptableObjects.
/// Streamlined for overlay-only presets.
/// </summary>
[CustomEditor(typeof(DecoratorPreset))]
public class DecoratorPresetEditor : Editor
{
    private DecoratorPreset preset;
    
    private bool showInfo = true;
    private bool showLayers = true;
    private List<bool> layerFoldouts = new List<bool>();
    
    private GUIStyle _headerStyle;
    private GUIStyle _subHeaderStyle;
    
    private static readonly Color InfoColor = new Color(0.4f, 0.7f, 0.9f);
    private static readonly Color LayerColor = new Color(0.5f, 0.8f, 0.5f);
    private static readonly Color OverlayColor = new Color(0.7f, 0.5f, 0.9f);
    
    private void OnEnable()
    {
        preset = (DecoratorPreset)target;
        SyncFoldouts();
    }
    
    private void EnsureStyles()
    {
        if (_headerStyle != null) return;
        
        _headerStyle = new GUIStyle(EditorStyles.boldLabel)
        {
            fontSize = 12
        };
        
        _subHeaderStyle = new GUIStyle(EditorStyles.boldLabel)
        {
            fontSize = 10
        };
    }
    
    private void SyncFoldouts()
    {
        while (layerFoldouts.Count < preset.layers.Count)
            layerFoldouts.Add(false);
        while (layerFoldouts.Count > preset.layers.Count)
            layerFoldouts.RemoveAt(layerFoldouts.Count - 1);
    }
    
    public override void OnInspectorGUI()
    {
        EnsureStyles();
        serializedObject.Update();
        
        DrawQuickActions();
        EditorGUILayout.Space(10);
        
        DrawInfoSection();
        EditorGUILayout.Space(5);
        
        DrawLayersSection();
        EditorGUILayout.Space(10);
        
        DrawValidation();
        
        serializedObject.ApplyModifiedProperties();
    }
    
    private void DrawQuickActions()
    {
        var decorator = FindObjectOfType<MBTerrainDecorator>();
        bool hasDecorator = decorator != null;
        
        if (!hasDecorator)
        {
            EditorGUILayout.HelpBox("No MBTerrainDecorator found in scene. Add one to a Terrain to enable Save/Load.", MessageType.Info);
        }
        
        // Overlay count info
        if (hasDecorator)
        {
            int overlayCount = DecoratorPreset.CountOverlayLayers(decorator);
            EditorGUILayout.HelpBox($"Scene decorator has {overlayCount} overlay layer(s).", MessageType.None);
        }
        
        EditorGUILayout.BeginHorizontal();
        
        GUI.enabled = hasDecorator;
        
        GUI.backgroundColor = new Color(0.4f, 0.8f, 0.4f);
        if (GUILayout.Button("Save from Scene", GUILayout.Height(28)))
        {
            int overlayCount = DecoratorPreset.CountOverlayLayers(decorator);
            if (EditorUtility.DisplayDialog("Save Preset", 
                $"Save {overlayCount} overlay layer(s) to '{preset.presetName}'?\n\nOnly overlay layers are saved. Base layers are skipped.",
                "Save", "Cancel"))
            {
                Undo.RecordObject(preset, "Save Decorator Preset");
                preset.SaveFromDecorator(decorator);
            }
        }
        
        GUI.backgroundColor = new Color(0.4f, 0.6f, 0.9f);
        if (GUILayout.Button("Load to Scene", GUILayout.Height(28)))
        {
            if (EditorUtility.DisplayDialog("Load Preset",
                $"Load {preset.layers.Count} overlay layer(s) from '{preset.presetName}'?\n\nExisting overlay layers will be replaced. Base layers are preserved.",
                "Load", "Cancel"))
            {
                preset.LoadToDecorator(decorator, false);
            }
        }
        
        GUI.backgroundColor = new Color(0.7f, 0.5f, 0.9f);
        if (GUILayout.Button("Merge to Scene", GUILayout.Height(28)))
        {
            if (EditorUtility.DisplayDialog("Merge Preset",
                $"Add {preset.layers.Count} overlay layer(s) from '{preset.presetName}'?\n\nExisting overlay layers are kept, preset layers are appended.",
                "Merge", "Cancel"))
            {
                preset.LoadToDecorator(decorator, true);
            }
        }
        
        GUI.enabled = true;
        GUI.backgroundColor = Color.white;
        
        EditorGUILayout.EndHorizontal();
    }
    
    private void DrawInfoSection()
    {
        DrawColoredFoldout(ref showInfo, "\u2139 Preset Info", InfoColor);
        if (!showInfo) return;
        
        EditorGUI.indentLevel++;
        
        EditorGUILayout.PropertyField(serializedObject.FindProperty("presetName"), new GUIContent("Name"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("description"));
        
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.PropertyField(serializedObject.FindProperty("author"), GUILayout.Width(EditorGUIUtility.currentViewWidth * 0.5f - 20));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("version"), GUILayout.Width(EditorGUIUtility.currentViewWidth * 0.5f - 20));
        EditorGUILayout.EndHorizontal();
        
        using (new EditorGUI.DisabledGroupScope(true))
        {
            EditorGUILayout.PropertyField(serializedObject.FindProperty("lastModified"), new GUIContent("Last Modified"));
        }
        
        EditorGUILayout.Space(5);
        EditorGUILayout.LabelField("Summary", preset.GetSummary(), EditorStyles.helpBox);
        
        EditorGUI.indentLevel--;
    }
    
    private void DrawLayersSection()
    {
        SyncFoldouts();
        
        EditorGUILayout.BeginHorizontal();
        DrawColoredFoldout(ref showLayers, $"\uD83C\uDFA8 Overlay Layers ({preset.layers.Count})", LayerColor);
        
        GUILayout.FlexibleSpace();
        
        if (GUILayout.Button("+ Add", GUILayout.Width(60)))
        {
            Undo.RecordObject(preset, "Add Preset Layer");
            preset.layers.Add(new DecoratorPreset.PresetLayer { name = "New Overlay Layer" });
            layerFoldouts.Add(true);
            EditorUtility.SetDirty(preset);
        }
        
        EditorGUILayout.EndHorizontal();
        
        if (!showLayers) return;
        
        EditorGUI.indentLevel++;
        
        for (int i = 0; i < preset.layers.Count; i++)
        {
            DrawLayerEntry(i);
        }
        
        if (preset.layers.Count == 0)
        {
            EditorGUILayout.HelpBox("No overlay layers. Save from scene or add manually.", MessageType.Info);
        }
        
        EditorGUI.indentLevel--;
    }
    
    private void DrawLayerEntry(int index)
    {
        var layer = preset.layers[index];
        
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        
        // Header
        EditorGUILayout.BeginHorizontal();
        
        layerFoldouts[index] = EditorGUILayout.Foldout(layerFoldouts[index], "", true);
        
        // Active toggle
        bool newActive = EditorGUILayout.Toggle(layer.active, GUILayout.Width(20));
        if (newActive != layer.active)
        {
            Undo.RecordObject(preset, "Toggle Layer Active");
            layer.active = newActive;
            EditorUtility.SetDirty(preset);
        }
        
        // Name with overlay badge
        string displayName = layer.active ? layer.name : $"[OFF] {layer.name}";
        if (!layer.active) GUI.color = new Color(1, 1, 1, 0.5f);
        EditorGUILayout.LabelField($"[{layer.layerIndex}] {displayName}", _subHeaderStyle);
        GUI.color = Color.white;
        
        GUILayout.FlexibleSpace();
        
        // Blend mode badge
        GUI.color = OverlayColor;
        EditorGUILayout.LabelField(layer.overlayBlendMode.ToString(), EditorStyles.miniLabel, GUILayout.Width(80));
        GUI.color = Color.white;
        
        // Rule count badge
        int activeRules = layer.rules.Count(r => r.active);
        EditorGUILayout.LabelField($"{activeRules}/{layer.rules.Count}R", EditorStyles.miniLabel, GUILayout.Width(40));
        
        // Move buttons
        GUI.enabled = index > 0;
        if (GUILayout.Button("\u2191", GUILayout.Width(22)))
        {
            Undo.RecordObject(preset, "Move Layer Up");
            SwapLayers(index, index - 1);
        }
        
        GUI.enabled = index < preset.layers.Count - 1;
        if (GUILayout.Button("\u2193", GUILayout.Width(22)))
        {
            Undo.RecordObject(preset, "Move Layer Down");
            SwapLayers(index, index + 1);
        }
        GUI.enabled = true;
        
        // Delete button
        GUI.backgroundColor = new Color(1f, 0.5f, 0.5f);
        if (GUILayout.Button("\u00D7", GUILayout.Width(22)))
        {
            if (EditorUtility.DisplayDialog("Remove Layer", $"Remove overlay layer '{layer.name}'?", "Yes", "No"))
            {
                Undo.RecordObject(preset, "Remove Preset Layer");
                preset.layers.RemoveAt(index);
                layerFoldouts.RemoveAt(index);
                EditorUtility.SetDirty(preset);
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.EndVertical();
                return;
            }
        }
        GUI.backgroundColor = Color.white;
        
        EditorGUILayout.EndHorizontal();
        
        // Content
        if (layerFoldouts[index])
        {
            EditorGUI.indentLevel++;
            
            string newName = EditorGUILayout.TextField("Name", layer.name);
            if (newName != layer.name)
            {
                Undo.RecordObject(preset, "Rename Layer");
                layer.name = newName;
                EditorUtility.SetDirty(preset);
            }
            
            // Target layer
            int newIndex = EditorGUILayout.IntField("Target Layer Index", layer.layerIndex);
            if (newIndex != layer.layerIndex)
            {
                Undo.RecordObject(preset, "Change Layer Index");
                layer.layerIndex = newIndex;
                EditorUtility.SetDirty(preset);
            }
            
            // Layer type
            var newType = (MBTerrainDecorator.LayerType)EditorGUILayout.EnumPopup("Layer Type", layer.layerType);
            if (newType != layer.layerType)
            {
                Undo.RecordObject(preset, "Change Layer Type");
                layer.layerType = newType;
                EditorUtility.SetDirty(preset);
            }
            
            // Overlay settings
            EditorGUILayout.Space(3);
            Rect overlayRect = EditorGUILayout.GetControlRect(GUILayout.Height(16));
            EditorGUI.DrawRect(new Rect(overlayRect.x, overlayRect.y, 3, overlayRect.height), OverlayColor);
            EditorGUI.LabelField(new Rect(overlayRect.x + 8, overlayRect.y, overlayRect.width, overlayRect.height), 
                "Overlay Settings", EditorStyles.boldLabel);
            
            var newBlendMode = (MBTerrainDecorator.OverlayBlendMode)EditorGUILayout.EnumPopup("Blend Mode", layer.overlayBlendMode);
            if (newBlendMode != layer.overlayBlendMode)
            {
                Undo.RecordObject(preset, "Change Overlay Blend Mode");
                layer.overlayBlendMode = newBlendMode;
                EditorUtility.SetDirty(preset);
            }
            
            float newOpacity = EditorGUILayout.Slider("Opacity", layer.overlayOpacity, 0f, 1f);
            if (!Mathf.Approximately(newOpacity, layer.overlayOpacity))
            {
                Undo.RecordObject(preset, "Change Overlay Opacity");
                layer.overlayOpacity = newOpacity;
                EditorUtility.SetDirty(preset);
            }
            
            // Tree settings if tree type
            if (layer.layerType == MBTerrainDecorator.LayerType.tree)
            {
                EditorGUILayout.Space(5);
                EditorGUILayout.LabelField("Tree Settings", EditorStyles.boldLabel);
                
                layer.probability = EditorGUILayout.Slider("Probability", layer.probability, 0f, 1f);
                layer.maximumTreeCount = EditorGUILayout.IntField("Max Tree Count", layer.maximumTreeCount);
                layer.width = EditorGUILayout.FloatField("Width", layer.width);
                layer.height = EditorGUILayout.FloatField("Height", layer.height);
                layer.randomPosition = EditorGUILayout.Slider("Random Position", layer.randomPosition, 0f, 1f);
                layer.randomRotation = EditorGUILayout.Slider("Random Rotation", layer.randomRotation, 0f, 1f);
                layer.randomSize = EditorGUILayout.Slider("Random Size", layer.randomSize, 0f, 1f);
                layer.randomHealth = EditorGUILayout.Slider("Random Health", layer.randomHealth, 0f, 1f);
                layer.offset = EditorGUILayout.FloatField("Vertical Offset", layer.offset);
            }
            
            EditorGUILayout.Space(5);
            DrawRulesSection(layer);
            
            EditorGUI.indentLevel--;
        }
        
        EditorGUILayout.EndVertical();
    }
    
    private void DrawRulesSection(DecoratorPreset.PresetLayer layer)
    {
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField($"Rules ({layer.rules.Count})", _subHeaderStyle);
        
        GUILayout.FlexibleSpace();
        
        if (GUILayout.Button("+", GUILayout.Width(22)))
        {
            Undo.RecordObject(preset, "Add Rule");
            layer.rules.Add(new DecoratorPreset.PresetRule());
            EditorUtility.SetDirty(preset);
        }
        
        EditorGUILayout.EndHorizontal();
        
        for (int i = 0; i < layer.rules.Count; i++)
        {
            DrawRuleEntry(layer, i);
        }
    }
    
    private void DrawRuleEntry(DecoratorPreset.PresetLayer layer, int ruleIndex)
    {
        var rule = layer.rules[ruleIndex];
        
        EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);
        
        // Active toggle
        bool newActive = EditorGUILayout.Toggle(rule.active, GUILayout.Width(20));
        if (newActive != rule.active)
        {
            Undo.RecordObject(preset, "Toggle Rule Active");
            rule.active = newActive;
            EditorUtility.SetDirty(preset);
        }
        
        // Filter type
        var newFilter = (MBTerrainDecorator.FilterType)EditorGUILayout.EnumPopup(rule.filter, GUILayout.Width(80));
        if (newFilter != rule.filter)
        {
            Undo.RecordObject(preset, "Change Rule Filter");
            rule.filter = newFilter;
            EditorUtility.SetDirty(preset);
        }
        
        // Blend type
        var newBlend = (MBTerrainDecorator.BlendType)EditorGUILayout.EnumPopup(rule.blend, GUILayout.Width(60));
        if (newBlend != rule.blend)
        {
            Undo.RecordObject(preset, "Change Rule Blend");
            rule.blend = newBlend;
            EditorUtility.SetDirty(preset);
        }
        
        // Range
        EditorGUILayout.LabelField($"{rule.min:F2}-{rule.max:F2}", EditorStyles.miniLabel, GUILayout.Width(70));
        
        GUILayout.FlexibleSpace();
        
        // Delete
        GUI.backgroundColor = new Color(1f, 0.6f, 0.6f);
        if (GUILayout.Button("\u00D7", EditorStyles.miniButton, GUILayout.Width(20)))
        {
            Undo.RecordObject(preset, "Remove Rule");
            layer.rules.RemoveAt(ruleIndex);
            EditorUtility.SetDirty(preset);
        }
        GUI.backgroundColor = Color.white;
        
        EditorGUILayout.EndHorizontal();
    }
    
    private void DrawValidation()
    {
        var issues = preset.Validate();
        
        if (issues.Count > 0)
        {
            EditorGUILayout.Space(5);
            EditorGUILayout.LabelField("\u26A0 Validation Issues", _headerStyle);
            
            foreach (var issue in issues)
            {
                EditorGUILayout.HelpBox(issue, MessageType.Warning);
            }
        }
    }
    
    private void DrawColoredFoldout(ref bool foldout, string title, Color color)
    {
        Rect rect = EditorGUILayout.GetControlRect(GUILayout.Height(20));
        EditorGUI.DrawRect(new Rect(rect.x, rect.y, 4, rect.height), color);
        
        Rect foldoutRect = new Rect(rect.x + 24, rect.y, rect.width - 8, rect.height);
        foldout = EditorGUI.Foldout(foldoutRect, foldout, title, true, _headerStyle);
    }
    
    private void SwapLayers(int a, int b)
    {
        var temp = preset.layers[a];
        preset.layers[a] = preset.layers[b];
        preset.layers[b] = temp;
        
        var tempFold = layerFoldouts[a];
        layerFoldouts[a] = layerFoldouts[b];
        layerFoldouts[b] = tempFold;
        
        EditorUtility.SetDirty(preset);
    }
}
