using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using B83.Image.BMP;
using BDT.GUI.Helpers;
using MountAndBlade.ModdingToolkit;
using UnityEditor;
using UnityEngine;

public class ImportModuleWindow : EditorWindow
{
    private Dictionary<string, string> _availableModules;
    private ModuleCoreData _selectedModuleCoreData;
    private int _selectedModuleID;
    private Vector2 _scrollPosition;

    // Embedded wizard for the selected module
    private MBModuleImportWizard _importWizard;
    private MBModule _wizardModule;
    private bool _showWizard;

    [MenuItem("MBToolset/Module Importer")]
    public static void ShowWindow()
    {
        GetWindow<ImportModuleWindow>("Module Importer");
    }

    private void OnEnable()
    {
        _importWizard = new MBModuleImportWizard();
        RefreshModuleList();
    }

    private void OnGUI()
    {
        GUILayout.Space(8);
        EditorGUILayout.LabelField("Import Module", GetHeaderStyle());
        GUILayout.Space(4);
        DrawUILine(Color.gray);

        var settings = MBEditorManager.MbEditorSettings;

        // Gate 1: Paths must be configured
        if (!settings.MBValidPath())
        {
            EditorGUILayout.HelpBox(
                "Mount&Blade game path must be configured first.\n\n" +
                "Go to MBToolset → Settings to configure paths.",
                MessageType.Error);

            if (GUILayout.Button("Open Settings"))
            {
                MBEditorSettingsWindow.ShowWindow();
            }

            return;
        }

        // Gate 2: Native must be fully imported
        if (!settings.IsNativeReady())
        {
            var nativeStatus = settings.GetNativeStatus();

            string statusLabel = nativeStatus switch
            {
                MBEditorSettings.NativeStatus.NotImported => "Not imported",
                MBEditorSettings.NativeStatus.ModuleCreated => "Module created, resources not imported",
                MBEditorSettings.NativeStatus.ResourcesImported => "Resources imported, database not populated",
                MBEditorSettings.NativeStatus.DatabasePopulated => "Database populated, models not created",
                _ => "Unknown"
            };

            EditorGUILayout.HelpBox(
                "Native module must be fully imported before importing other modules.\n\n" +
                $"Current status: {statusLabel}",
                MessageType.Warning);

            if (GUILayout.Button("Open Settings to Import Native", GUILayout.Height(28)))
            {
                MBEditorSettingsWindow.ShowWindow();
            }

            return;
        }

        // Module Selection
        if (_availableModules == null || _availableModules.Count == 0)
        {
            EditorGUILayout.HelpBox(
                "No modules available for import.\n" +
                "Make sure your Mount&Blade Modules folder contains valid modules.",
                MessageType.Info);

            if (GUILayout.Button("Refresh"))
            {
                RefreshModuleList();
            }

            return;
        }

        EditorGUI.BeginChangeCheck();
        _selectedModuleID = EditorGUILayout.Popup("Module", _selectedModuleID,
            _availableModules.Keys.ToArray());
        if (EditorGUI.EndChangeCheck())
        {
            RefreshSelectionData();
            _showWizard = false;
            _wizardModule = null;
        }

        DrawUILine(Color.gray);

        if (_selectedModuleCoreData == null)
            return;

        _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);

        // Module image
        if (_selectedModuleCoreData.ModuleImage != null)
        {
            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            GUILayout.Label(_selectedModuleCoreData.ModuleImage,
                GUILayout.Width(340), GUILayout.Height(275));
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
        }

        // Import status
        string moduleName = _selectedModuleCoreData.ModuleName;
        string modAssetPath = MBPathHelpers.ModAssetPath(moduleName);
        bool alreadyImported = File.Exists(modAssetPath);

        if (alreadyImported)
        {
            EditorGUILayout.LabelField("(Already Imported)",
                GetLabelStyle(new Color(1f, 0.6f, 0f), true, TextAnchor.UpperCenter));
        }
        else
        {
            EditorGUILayout.LabelField($"( {moduleName} )",
                GetLabelStyle(Color.green, true, TextAnchor.UpperCenter));
        }

        DrawUILine(Color.gray);

        // Action Buttons
        if (!_showWizard)
        {
            string buttonLabel = alreadyImported ? "Reimport Module" : "Import Module";

            if (GUILayout.Button(buttonLabel, GUILayout.Height(28)))
            {
                if (alreadyImported)
                {
                    if (EditorUtility.DisplayDialog(
                            "Confirm Reimport",
                            $"This will delete and reimport {moduleName}.\n" +
                            "This may take several minutes.",
                            "Reimport", "Cancel"))
                    {
                        MBEditorUtility.RemoveModuleData(moduleName);
                        GC.Collect();
                        BeginModuleImport(moduleName);
                    }
                }
                else
                {
                    BeginModuleImport(moduleName);
                }
            }

            // Show wizard button for already-imported modules
            if (alreadyImported)
            {
                GUILayout.Space(4);
                if (GUILayout.Button("Open Import Wizard"))
                {
                    LoadModuleForWizard(moduleName);
                }
            }
        }

        // Embedded Wizard
        if (_showWizard && _wizardModule != null)
        {
            UIHelpers.DrawUILine(UIColors.GrayLine, 1, 4);
            _importWizard.DrawWizardUI(_wizardModule);

            if (_importWizard.Imported)
            {
                GUILayout.Space(4);
                EditorGUILayout.HelpBox(
                    $" {moduleName} import complete ✔",
                    MessageType.None);

                if (GUILayout.Button("Close Wizard"))
                {
                    _showWizard = false;
                }
            }
        }

        EditorGUILayout.EndScrollView();
    }

    // Module List

    private void RefreshModuleList()
    {
        _availableModules = new Dictionary<string, string>();
        string modulesPath = MBPathHelpers.EngineModulesFolder();

        if (!Directory.Exists(modulesPath))
            return;

        foreach (string dir in Directory.GetDirectories(modulesPath))
        {
            string name = Path.GetFileName(dir);
            string iniPath = Path.Combine(dir, "module.ini");

            // Native is handled in Settings window
            if (name.Equals("Native", StringComparison.OrdinalIgnoreCase))
                continue;

            if (File.Exists(iniPath))
            {
                _availableModules.Add(name, dir);
            }
        }

        _selectedModuleID = 0;
        _selectedModuleCoreData = null;
        _showWizard = false;
        _wizardModule = null;

        if (_availableModules.Count > 0)
        {
            RefreshSelectionData();
        }
    }

    private void RefreshSelectionData()
    {
        if (_availableModules.Count == 0 || _selectedModuleID >= _availableModules.Count)
            return;

        string selectedPath = _availableModules.ElementAt(_selectedModuleID).Value;
        string moduleName = _availableModules.ElementAt(_selectedModuleID).Key;
        string picturePath = Path.Combine(selectedPath, "main.bmp");

        Texture2D moduleImage = null;
        if (File.Exists(picturePath))
        {
            try
            {
                BMPLoader bmpLoader = new BMPLoader();
                BMPImage bmpImg = bmpLoader.LoadBMP(picturePath);
                moduleImage = bmpImg.ToTexture2D();
                moduleImage.Apply();
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"Failed to load module image: {ex.Message}");
            }
        }

        _selectedModuleCoreData = new ModuleCoreData
        {
            ModulePath = selectedPath,
            ModuleName = moduleName,
            ModuleImage = moduleImage
        };
    }

    // Import Actions

    private void BeginModuleImport(string moduleName)
    {
        string modAssetPath = MBPathHelpers.ModAssetPath(moduleName);
        if (!File.Exists(modAssetPath))
        {
            MBEditorUtility.CreateEditorModuleData(moduleName);
        }

        LoadModuleForWizard(moduleName);
    }

    private void LoadModuleForWizard(string moduleName)
    {
        string modAssetPath = MBPathHelpers.ModAssetPath(moduleName);
        _wizardModule = AssetDatabase.LoadAssetAtPath<MBModule>(modAssetPath);

        if (_wizardModule == null)
        {
            EditorUtility.DisplayDialog("Error",
                $"Failed to load module asset for {moduleName}.", "OK");
            return;
        }

        _importWizard = new MBModuleImportWizard();
        _showWizard = true;
    }

    // UI Helpers

    private void DrawUILine(Color color, int thickness = 1, int padding = 10)
    {
        Rect rect = EditorGUILayout.GetControlRect(GUILayout.Height(padding + thickness));
        rect.height = thickness;
        rect.y += padding / 2;
        EditorGUI.DrawRect(rect, color);
    }

    private GUIStyle GetHeaderStyle()
    {
        return new GUIStyle(EditorStyles.boldLabel)
        {
            alignment = TextAnchor.MiddleCenter,
            fontSize = 14
        };
    }

    private GUIStyle GetLabelStyle(Color color, bool bold = false,
        TextAnchor alignment = TextAnchor.MiddleLeft)
    {
        var style = new GUIStyle(EditorStyles.miniLabel)
        {
            normal = { textColor = color },
            alignment = alignment,
            fontSize = 11
        };
        if (bold)
            style.fontStyle = FontStyle.Bold;
        return style;
    }
}

public class ModuleCoreData
{
    public string ModuleName { get; set; }
    public string ModulePath { get; set; }
    public Texture2D ModuleImage { get; set; }
}