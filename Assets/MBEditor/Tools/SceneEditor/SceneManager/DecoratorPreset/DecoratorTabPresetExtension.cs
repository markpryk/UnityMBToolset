using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEditor;

/// <summary>
/// Preset management extension for DecoratorTab.
/// Saves and loads only overlay layers - base layers are untouched.
/// </summary>
public static class DecoratorTabPresetExtension
{
    private static DecoratorPreset selectedPreset;
    private static List<DecoratorPreset> availablePresets = new List<DecoratorPreset>();
    private static bool presetsLoaded = false;
    private static bool showPresetSection = true;
    
    private static readonly Color PresetColor = new Color(0.7f, 0.5f, 0.9f);
    
    /// <summary>
    /// Draw the preset management section. Call this from DrawLayersTab() or DrawSettingsTab().
    /// </summary>
    public static void DrawPresetSection(MBTerrainDecorator decorator)
    {
        if (decorator == null) return;
        
        EditorGUILayout.Space(5);
        
        // Header
        EditorGUILayout.BeginHorizontal();
        
        Rect headerRect = EditorGUILayout.GetControlRect(GUILayout.Height(22));
        EditorGUI.DrawRect(new Rect(headerRect.x, headerRect.y, 4, headerRect.height), PresetColor);
        
        showPresetSection = EditorGUI.Foldout(
            new Rect(headerRect.x + 24, headerRect.y, headerRect.width - 8, headerRect.height),
            showPresetSection, "\uD83D\uDCBE Overlay Presets", true, EditorStyles.foldoutHeader);
        
        EditorGUILayout.EndHorizontal();
        
        if (!showPresetSection) return;
        
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.Space(3);
        
        // Overlay layer count
        int overlayCount = DecoratorPreset.CountOverlayLayers(decorator);
        EditorGUILayout.LabelField($"Current overlay layers: {overlayCount}", EditorStyles.miniLabel);
        
        // Refresh presets if needed
        EditorGUILayout.BeginHorizontal();
        if (!presetsLoaded || GUILayout.Button("\u21BB Refresh", GUILayout.Width(70)))
        {
            RefreshPresetList();
        }
        EditorGUILayout.EndHorizontal();
        
        // Preset selector
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("Preset", GUILayout.Width(50));
        
        string[] presetNames = availablePresets.Select(p => p != null ? p.presetName : "(null)").ToArray();
        if (presetNames.Length == 0) presetNames = new string[] { "(none)" };
        
        int currentIndex = selectedPreset != null ? availablePresets.IndexOf(selectedPreset) : -1;
        if (currentIndex < 0) currentIndex = 0;
        
        int newIndex = EditorGUILayout.Popup(currentIndex, presetNames);
        if (newIndex >= 0 && newIndex < availablePresets.Count)
        {
            selectedPreset = availablePresets[newIndex];
        }
        
        // Quick select button
        if (GUILayout.Button("\u25C9", GUILayout.Width(24)))
        {
            if (selectedPreset != null)
            {
                EditorGUIUtility.PingObject(selectedPreset);
                Selection.activeObject = selectedPreset;
            }
        }
        
        EditorGUILayout.EndHorizontal();
        
        // Show preset info if selected
        if (selectedPreset != null)
        {
            EditorGUILayout.LabelField(selectedPreset.GetSummary(), EditorStyles.miniLabel);
            
            if (!string.IsNullOrEmpty(selectedPreset.description))
            {
                EditorGUILayout.LabelField(selectedPreset.description, EditorStyles.wordWrappedMiniLabel);
            }
        }
        
        EditorGUILayout.Space(5);
        
        // Action buttons
        EditorGUILayout.BeginHorizontal();
        
        // Load - replaces overlay layers only
        GUI.enabled = selectedPreset != null;
        GUI.backgroundColor = new Color(0.4f, 0.6f, 0.9f);
        if (GUILayout.Button("Load", GUILayout.Height(24)))
        {
            if (EditorUtility.DisplayDialog("Load Overlay Preset",
                $"Load '{selectedPreset.presetName}' ({selectedPreset.layers.Count} overlay layers)?\n\n" +
                "Existing overlay layers will be replaced.\nBase layers are preserved.",
                "Load", "Cancel"))
            {
                selectedPreset.LoadToDecorator(decorator, false);
            }
        }
        
        // Merge - appends overlay layers
        GUI.backgroundColor = new Color(0.6f, 0.4f, 0.9f);
        if (GUILayout.Button("Merge", GUILayout.Height(24)))
        {
            if (EditorUtility.DisplayDialog("Merge Overlay Preset",
                $"Add {selectedPreset.layers.Count} overlay layer(s) from '{selectedPreset.presetName}'?\n\n" +
                "Existing layers (base and overlay) are kept.",
                "Merge", "Cancel"))
            {
                selectedPreset.LoadToDecorator(decorator, true);
            }
        }
        GUI.enabled = true;
        
        GUILayout.FlexibleSpace();
        
        // Update existing
        GUI.enabled = selectedPreset != null && overlayCount > 0;
        GUI.backgroundColor = new Color(0.9f, 0.7f, 0.3f);
        if (GUILayout.Button("Update", GUILayout.Height(24)))
        {
            if (EditorUtility.DisplayDialog("Update Overlay Preset",
                $"Save {overlayCount} overlay layer(s) to '{selectedPreset.presetName}'?\n\n" +
                "This will overwrite the existing preset.",
                "Update", "Cancel"))
            {
                Undo.RecordObject(selectedPreset, "Update Decorator Preset");
                selectedPreset.SaveFromDecorator(decorator);
                AssetDatabase.SaveAssets();
            }
        }
        GUI.enabled = true;
        
        // Save as new
        GUI.enabled = overlayCount > 0;
        GUI.backgroundColor = new Color(0.4f, 0.8f, 0.4f);
        if (GUILayout.Button("Save New", GUILayout.Height(24)))
        {
            SaveAsNewPreset(decorator);
        }
        GUI.enabled = true;
        
        GUI.backgroundColor = Color.white;
        EditorGUILayout.EndHorizontal();
        
        if (overlayCount == 0)
        {
            EditorGUILayout.Space(2);
            EditorGUILayout.HelpBox("No overlay layers to save. Add overlay layers first.", MessageType.Info);
        }
        
        EditorGUILayout.Space(3);
        EditorGUILayout.EndVertical();
    }
    
    /// <summary>
    /// Refresh the list of available presets from the project.
    /// </summary>
    public static void RefreshPresetList()
    {
        availablePresets.Clear();
        
        string[] guids = AssetDatabase.FindAssets("t:DecoratorPreset");
        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var preset = AssetDatabase.LoadAssetAtPath<DecoratorPreset>(path);
            if (preset != null)
            {
                availablePresets.Add(preset);
            }
        }
        
        availablePresets = availablePresets.OrderBy(p => p.presetName).ToList();
        presetsLoaded = true;
        
        Debug.Log($"[DecoratorTab] Found {availablePresets.Count} decorator presets");
    }
    
    /// <summary>
    /// Show save dialog and create a new preset from current overlay layers.
    /// </summary>
    private static void SaveAsNewPreset(MBTerrainDecorator decorator)
    {
        string defaultPath = "Assets/Presets";
        if (!AssetDatabase.IsValidFolder(defaultPath))
        {
            defaultPath = "Assets";
        }
        
        string path = EditorUtility.SaveFilePanelInProject(
            "Save Overlay Preset",
            "NewOverlayPreset",
            "asset",
            "Save overlay layers as a new preset",
            defaultPath);
        
        if (string.IsNullOrEmpty(path)) return;
        
        var newPreset = ScriptableObject.CreateInstance<DecoratorPreset>();
        newPreset.presetName = Path.GetFileNameWithoutExtension(path);
        newPreset.SaveFromDecorator(decorator);
        
        AssetDatabase.CreateAsset(newPreset, path);
        AssetDatabase.SaveAssets();
        
        RefreshPresetList();
        selectedPreset = newPreset;
        
        EditorGUIUtility.PingObject(newPreset);
        Debug.Log($"[DecoratorTab] Created new overlay preset: {path}");
    }
    
    public static DecoratorPreset GetSelectedPreset() => selectedPreset;
    public static void SetSelectedPreset(DecoratorPreset preset) => selectedPreset = preset;
}