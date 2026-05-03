using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

//  FloraPresetToolbar - Reusable preset UI for both subtabs
//
//  Drop-in UI strip providing:
//    • Save current state as new preset (or overwrite existing)
//    • Load preset from file browser
//    • Quick-apply from a dropdown of all project presets
//    • Preview report before applying (match/miss count)
//
//  Integration:
//    1. Add a FloraPresetToolbar field to your subtab
//    2. Call DrawPresetToolbar() where you want the UI
//    3. Pass the relevant config/library objects

internal class FloraPresetToolbar
{
    private FloraPreset _lastLoadedPreset;
    private PresetApplyReport _lastReport;
    private bool _showReport;
    private bool _showPresetFoldout;

    private List<FloraPreset> _cachedPresets;
    private double _cacheTime;
    private const double CACHE_DURATION = 5.0; // seconds

    //  Decorator Toolbar

    /// <summary>
    /// Draw the full preset toolbar for the Decorator subtab.
    /// Place this where you want Save/Load controls in your UI.
    /// </summary>
    public void DrawPopulatorToolbar(FloraPopulatorConfig config, MBFloraLibrary library,
        string moduleId = null, string sceneName = null)
    {
        _showPresetFoldout = EditorGUILayout.Foldout(_showPresetFoldout, "Presets", true, EditorStyles.foldoutHeader);
        if (!_showPresetFoldout) return;

        EditorGUILayout.BeginVertical(EditorStyles.helpBox);

        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("Save", EditorStyles.miniLabel, GUILayout.Width(35));

        if (GUILayout.Button("Save New...", EditorStyles.miniButtonLeft, GUILayout.Height(20)))
        {
            string defaultName = !string.IsNullOrEmpty(sceneName)
                ? $"Flora_{sceneName}" : "FloraPreset";

            var preset = FloraPresetManager.SavePresetDialog(defaultName);
            if (preset != null)
            {
                FloraPresetManager.CaptureAll(preset, config, library, moduleId, sceneName);
                FloraPresetManager.OverwritePreset(preset);
                _lastLoadedPreset = preset;
                _lastReport = null;
                InvalidateCache();
                Debug.Log($"[FloraPreset] Saved decorator preset: {preset.PresetName}");
            }
        }

        EditorGUI.BeginDisabledGroup(_lastLoadedPreset == null);
        if (GUILayout.Button("Overwrite", EditorStyles.miniButtonRight, GUILayout.Height(20)))
        {
            if (_lastLoadedPreset != null && EditorUtility.DisplayDialog("Overwrite Preset",
                    $"Overwrite '{_lastLoadedPreset.PresetName}' with current configuration?",
                    "Overwrite", "Cancel"))
            {
                FloraPresetManager.CaptureAll(_lastLoadedPreset, config, library, moduleId, sceneName);
                FloraPresetManager.OverwritePreset(_lastLoadedPreset);
                _lastReport = null;
            }
        }
        EditorGUI.EndDisabledGroup();

        EditorGUILayout.EndHorizontal();

        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("Load", EditorStyles.miniLabel, GUILayout.Width(35));

        if (GUILayout.Button("Browse...", EditorStyles.miniButtonLeft, GUILayout.Height(20)))
        {
            var preset = FloraPresetManager.LoadPresetDialog();
            if (preset != null)
            {
                _lastLoadedPreset = preset;
                _lastReport = library != null
                    ? FloraPresetManager.PreviewApply(preset, library)
                    : null;
                _showReport = true;
            }
        }

        if (GUILayout.Button("Quick ▾", EditorStyles.miniButtonRight, GUILayout.Height(20)))
            ShowQuickApplyMenu(config, library, PresetTarget.Decorator);

        EditorGUILayout.EndHorizontal();

        if (_lastLoadedPreset != null)
        {
            EditorGUILayout.Space(2);
            DrawLoadedPresetInfo(config, library, PresetTarget.Decorator);
        }

        DrawReport();

        EditorGUILayout.EndVertical();
    }


    //  Populator Toolbar

    /// <summary>
    /// Draw the full preset toolbar for the Populator subtab.
    /// </summary>
    public void DrawPopulatorToolbar(MBFloraLibrary library,
        string moduleId = null, string sceneName = null)
    {
        _showPresetFoldout = EditorGUILayout.Foldout(_showPresetFoldout, "Presets", true, EditorStyles.foldoutHeader);
        if (!_showPresetFoldout) return;

        EditorGUILayout.BeginVertical(EditorStyles.helpBox);

        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("Save", EditorStyles.miniLabel, GUILayout.Width(35));

        if (GUILayout.Button("Save New...", EditorStyles.miniButtonLeft, GUILayout.Height(20)))
        {
            string defaultName = !string.IsNullOrEmpty(sceneName)
                ? $"Flora_{sceneName}_Populator" : "FloraPopulatorPreset";

            var preset = FloraPresetManager.SavePresetDialog(defaultName);
            if (preset != null)
            {
                FloraPresetManager.CapturePopulator(preset, library);
                preset.SourceModule = moduleId ?? "";
                FloraPresetManager.OverwritePreset(preset);
                _lastLoadedPreset = preset;
                _lastReport = null;
                InvalidateCache();
            }
        }

        EditorGUI.BeginDisabledGroup(_lastLoadedPreset == null);
        if (GUILayout.Button("Overwrite", EditorStyles.miniButtonRight, GUILayout.Height(20)))
        {
            if (_lastLoadedPreset != null && EditorUtility.DisplayDialog("Overwrite Preset",
                    $"Overwrite '{_lastLoadedPreset.PresetName}' with current registration state?",
                    "Overwrite", "Cancel"))
            {
                FloraPresetManager.CapturePopulator(_lastLoadedPreset, library);
                FloraPresetManager.OverwritePreset(_lastLoadedPreset);
                _lastReport = null;
            }
        }
        EditorGUI.EndDisabledGroup();

        EditorGUILayout.EndHorizontal();

        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("Load", EditorStyles.miniLabel, GUILayout.Width(35));

        if (GUILayout.Button("Browse...", EditorStyles.miniButtonLeft, GUILayout.Height(20)))
        {
            var preset = FloraPresetManager.LoadPresetDialog();
            if (preset != null)
            {
                _lastLoadedPreset = preset;
                _lastReport = FloraPresetManager.PreviewApply(preset, library);
                _showReport = true;
            }
        }

        if (GUILayout.Button("Quick ▾", EditorStyles.miniButtonRight, GUILayout.Height(20)))
            ShowQuickApplyMenu(null, library, PresetTarget.Populator);

        EditorGUILayout.EndHorizontal();

        if (_lastLoadedPreset != null)
        {
            EditorGUILayout.Space(2);
            DrawLoadedPresetInfo(null, library, PresetTarget.Populator);
        }

        DrawReport();

        EditorGUILayout.EndVertical();
    }


    //  Shared UI Elements

    private enum PresetTarget { Decorator, Populator, Both }

    private void DrawLoadedPresetInfo(FloraPopulatorConfig config, MBFloraLibrary library, PresetTarget target)
    {
        EditorGUILayout.BeginHorizontal();

        // Preset name + metadata
        EditorGUILayout.BeginVertical();
        EditorGUILayout.LabelField($"▸ {_lastLoadedPreset.PresetName}", EditorStyles.boldLabel);

        // Content summary
        string summary = "";
        if (_lastLoadedPreset.HasDecoratorData)
            summary += $"{_lastLoadedPreset.populatorData.Layers.Count} decorator layers  ";
        if (_lastLoadedPreset.HasLibraryData)
            summary += $"{_lastLoadedPreset.libraryData.Entries.Count} populator entries";
        if (!string.IsNullOrEmpty(summary))
            EditorGUILayout.LabelField(summary, EditorStyles.centeredGreyMiniLabel);

        EditorGUILayout.EndVertical();

        // Apply button
        Color orig = GUI.backgroundColor;
        GUI.backgroundColor = new Color(0.5f, 0.85f, 0.5f);

        if (GUILayout.Button("Apply", GUILayout.Width(60), GUILayout.Height(36)))
        {
            PresetApplyReport report = null;
            switch (target)
            {
                case PresetTarget.Decorator:
                    report = FloraPresetManager.ApplyAll(_lastLoadedPreset, config, library);
                    break;
                case PresetTarget.Populator:
                    report = FloraPresetManager.ApplyPopulator(_lastLoadedPreset, library);
                    break;
                case PresetTarget.Both:
                    report = FloraPresetManager.ApplyAll(_lastLoadedPreset, config, library);
                    break;
            }

            if (report != null)
            {
                _lastReport = report;
                _showReport = true;
                Debug.Log($"[FloraPreset] Applied '{_lastLoadedPreset.PresetName}': {report.Summary()}");
            }
        }

        GUI.backgroundColor = orig;
        EditorGUILayout.EndHorizontal();
    }

    private void DrawReport()
    {
        if (_lastReport == null) return;

        _showReport = EditorGUILayout.Foldout(_showReport, "Apply Report", true);
        if (!_showReport) return;

        EditorGUI.indentLevel++;

        EditorGUILayout.LabelField(_lastReport.Summary(), EditorStyles.miniLabel);

        if (_lastReport.HasMissing)
        {
            GUI.color = new Color(1f, 0.7f, 0.4f);
            EditorGUILayout.LabelField("Missing entries (not found in current library):", EditorStyles.miniLabel);

            // Show first 10 missing, then "and N more..."
            int showCount = Mathf.Min(_lastReport.MissingEntries.Count, 10);
            for (int i = 0; i < showCount; i++)
                EditorGUILayout.LabelField($"  • {_lastReport.MissingEntries[i]}", EditorStyles.miniLabel);

            if (_lastReport.MissingEntries.Count > showCount)
                EditorGUILayout.LabelField(
                    $"  ... and {_lastReport.MissingEntries.Count - showCount} more",
                    EditorStyles.miniLabel);

            GUI.color = Color.white;
        }

        EditorGUI.indentLevel--;
    }


    //  Quick-Apply Dropdown

    private void ShowQuickApplyMenu(FloraPopulatorConfig config, MBFloraLibrary library, PresetTarget target)
    {
        RefreshCacheIfNeeded();

        var menu = new GenericMenu();

        if (_cachedPresets.Count == 0)
        {
            menu.AddDisabledItem(new GUIContent("No presets found in project"));
            menu.ShowAsContext();
            return;
        }

        // Group by source scene
        var byScene = new Dictionary<string, List<FloraPreset>>();
        foreach (var p in _cachedPresets)
        {
            string group = "Ungrouped";
            if (!byScene.ContainsKey(group)) byScene[group] = new List<FloraPreset>();
            byScene[group].Add(p);
        }

        foreach (var kvp in byScene.OrderBy(k => k.Key))
        {
            foreach (var preset in kvp.Value)
            {
                string label = kvp.Value.Count > 1 || byScene.Count > 1
                    ? $"{kvp.Key}/{preset.PresetName}"
                    : preset.PresetName;

                // Show content indicator
                string suffix = "";
                if (preset.HasDecoratorData && preset.HasLibraryData) suffix = " [D+P]";
                else if (preset.HasDecoratorData) suffix = " [Decorator]";
                else if (preset.HasLibraryData) suffix = " [Populator]";

                FloraPreset p = preset; // capture for lambda
                menu.AddItem(new GUIContent(label + suffix), false, () =>
                {
                    _lastLoadedPreset = p;

                    // Preview first
                    _lastReport = FloraPresetManager.PreviewApply(p, library);
                    _showReport = true;

                    // If no missing entries, apply directly; otherwise just load for review
                    if (!_lastReport.HasMissing)
                    {
                        PresetApplyReport report;
                        switch (target)
                        {
                            case PresetTarget.Decorator:
                                report = FloraPresetManager.ApplyAll(p, config, library);
                                break;
                            case PresetTarget.Populator:
                                report = FloraPresetManager.ApplyPopulator(p, library);
                                break;
                            default:
                                report = FloraPresetManager.ApplyAll(p, config, library);
                                break;
                        }
                        _lastReport = report;
                        Debug.Log($"[FloraPreset] Quick-applied '{p.PresetName}': {report.Summary()}");
                    }
                    else
                    {
                        Debug.Log($"[FloraPreset] Loaded '{p.PresetName}' - {_lastReport.MissingEntries.Count} " +
                                  "missing entries. Review and click Apply to proceed.");
                    }
                });
            }
        }

        menu.AddSeparator("");
        menu.AddItem(new GUIContent("Refresh List"), false, InvalidateCache);
        menu.ShowAsContext();
    }

    private void RefreshCacheIfNeeded()
    {
        if (_cachedPresets != null && EditorApplication.timeSinceStartup - _cacheTime < CACHE_DURATION)
            return;

        _cachedPresets = FloraPresetManager.FindAllPresets();
        _cacheTime = EditorApplication.timeSinceStartup;
    }

    private void InvalidateCache()
    {
        _cachedPresets = null;
        _cacheTime = 0;
    }
}
