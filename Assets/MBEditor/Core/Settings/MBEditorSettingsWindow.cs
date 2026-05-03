using System;
using UnityEditor;
using UnityEngine;
using BDT.GUI.Helpers;
using MountAndBlade.ModdingToolkit;

public class MBEditorSettingsWindow : EditorWindow
{
    private MBModuleImportWizard _nativeWizard;
    private MBModule _nativeModule;
    private bool _showWizard;
    private Vector2 _scrollPosition;

    [MenuItem("MBToolset/Settings")]
    public static void ShowWindow()
    {
        GetWindow<MBEditorSettingsWindow>("MBToolset Settings");
    }

    private void OnEnable()
    {
        _nativeWizard = new MBModuleImportWizard();
        TryLoadNativeModule();
    }

    private void OnGUI()
    {
        var settings = MBEditorManager.MbEditorSettings;

        UIHelpers.DrawUILine(UIColors.GrayLine, 1, 4);
        EditorGUILayout.LabelField("MBToolset Settings", StylesHelpers.BoxHeader());
        GUILayout.Space(2);
        UIHelpers.DrawUILine(UIColors.GrayLine, 1, 4);

        EditorGUILayout.HelpBox(
            $" MBToolset Version {"0.1.6"} {Environment.NewLine} M&B/M&B:Warband compatible.",
            MessageType.None);
        UIHelpers.DrawUILine(UIColors.GrayLine, 1, 4);

        _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);

        var validPathGame = settings.MBValidPath();

        // Mount&Blade Game Path
        DrawPathSection(
            "Mount&Blade Game Path",
            validPathGame,
            "Mount&Blade game path Validated ✔",
            "Mount&Blade game folder path not Validated ⚠",
            "Enter your Mount&Blade game path.\n Example: \n ../steamapps/common/MountBlade Warband",
            settings.MbPath,
            newPath =>
            {
                settings.SetMbPath(newPath);
                SaveDataHelper.SaveScriptableObject(settings);
            },
            () =>
            {
                string path = EditorUtility.OpenFolderPanel(
                    "Select Mount&Blade Installation",
                    settings.MbPath ?? "", "");
                if (!string.IsNullOrEmpty(path))
                {
                    settings.SetMbPath(path);
                    SaveDataHelper.SaveScriptableObject(settings);
                }
            });

        // Native Module
        if (validPathGame)
        {
            DrawNativeSection(settings);
        }
        else
        {
            EditorGUILayout.HelpBox(
                "Configure both paths above to enable Native data import.",
                MessageType.Info);
        }

        EditorGUILayout.EndScrollView();
    }

    // Native Section

    private void DrawNativeSection(MBEditorSettings settings)
    {
        EditorGUILayout.LabelField("Native Module", EditorStyles.boldLabel);
        UIHelpers.DrawUILine(UIColors.GrayLine, 1, 4);

        var status = settings.GetNativeStatus();

        if (status == MBEditorSettings.NativeStatus.FullyImported && !_showWizard)
        {
            EditorGUILayout.HelpBox(
                " Native module fully imported ✔\n" +
                " You can now import other modules via MBToolset → Module Importer.",
                MessageType.None);
            UIHelpers.DrawUILine(UIColors.GrayLine, 1, 4);

            EditorGUILayout.BeginHorizontal();

            if (GUILayout.Button("Show Import Wizard"))
            {
                EnsureNativeModule();
                _showWizard = true;
            }

            if (GUILayout.Button("Reimport Native"))
            {
                if (EditorUtility.DisplayDialog(
                        "Confirm Reimport",
                        "This will delete and reimport all Native data.\n" +
                        "This may take several minutes.\n\n" +
                        "All module data that depends on Native will need to be reimported.",
                        "Reimport", "Cancel"))
                {
                    MBEditorUtility.RemoveModuleData("Native");
                    GC.Collect();
                    EnsureNativeModule();
                    _nativeWizard = new MBModuleImportWizard();
                    _showWizard = true;
                }
            }

            EditorGUILayout.EndHorizontal();
            UIHelpers.DrawUILine(UIColors.GrayLine, 1, 4);
            return;
        }

        if (status == MBEditorSettings.NativeStatus.NotImported && !_showWizard)
        {
            EditorGUILayout.HelpBox(
                "Native module has not been imported yet.\n" +
                "This is required before importing any other module.",
                MessageType.Error);

            if (GUILayout.Button("Begin Native Import", GUILayout.Height(28)))
            {
                EnsureNativeModule();
                _showWizard = true;
            }

            UIHelpers.DrawUILine(UIColors.GrayLine, 1, 4);
            return;
        }

        if (!_showWizard)
        {
            string statusLabel = status switch
            {
                MBEditorSettings.NativeStatus.ModuleCreated =>
                    "Module created - resources not yet imported.",
                MBEditorSettings.NativeStatus.ResourcesImported =>
                    "Resources imported - BRF database not yet populated.",
                MBEditorSettings.NativeStatus.DatabasePopulated =>
                    "Database populated - model prefabs not yet created.",
                _ => ""
            };

            EditorGUILayout.HelpBox(
                $"Native import incomplete.\n{statusLabel}",
                MessageType.Warning);

            if (GUILayout.Button("Continue Native Import", GUILayout.Height(28)))
            {
                EnsureNativeModule();
                _showWizard = true;
            }

            UIHelpers.DrawUILine(UIColors.GrayLine, 1, 4);
            return;
        }

        if (_nativeModule != null)
        {
            _nativeWizard.DrawWizardUI(_nativeModule);

            if (_nativeWizard.Imported)
            {
                GUILayout.Space(4);
                EditorGUILayout.HelpBox(" Native import complete ✔", MessageType.None);

                if (GUILayout.Button("Close Wizard"))
                {
                    _showWizard = false;
                }
            }

            UIHelpers.DrawUILine(UIColors.GrayLine, 1, 4);
        }
    }

    // Native Module Management

    private void TryLoadNativeModule()
    {
        string path = MBPathHelpers.ModAssetPath("Native");
        if (System.IO.File.Exists(path))
        {
            _nativeModule = AssetDatabase.LoadAssetAtPath<MBModule>(path);
        }
    }

    private void EnsureNativeModule()
    {
        if (_nativeModule != null) return;

        string path = MBPathHelpers.ModAssetPath("Native");
        if (!System.IO.File.Exists(path))
        {
            MBEditorUtility.CreateEditorModuleData("Native");
        }

        _nativeModule = AssetDatabase.LoadAssetAtPath<MBModule>(path);
    }

    // Shared UI Helpers

    private void DrawPathSection(
        string label,
        bool isValid,
        string validMessage,
        string invalidMessage,
        string helpMessage,
        string currentValue,
        Action<string> onTextChanged,
        Action onBrowse)
    {
        EditorGUILayout.LabelField(label, EditorStyles.boldLabel);

        if (isValid)
        {
            EditorGUILayout.HelpBox(validMessage, MessageType.None);
        }
        else
        {
            EditorGUILayout.HelpBox(invalidMessage, MessageType.Error);
            EditorGUILayout.HelpBox(helpMessage, MessageType.Warning);
        }

        EditorGUILayout.BeginHorizontal();

        EditorGUI.BeginChangeCheck();
        string newValue = EditorGUILayout.TextField(currentValue);
        if (EditorGUI.EndChangeCheck())
        {
            onTextChanged?.Invoke(newValue);
        }

        if (GUILayout.Button("Browse", GUILayout.Width(60)))
        {
            onBrowse?.Invoke();
        }

        EditorGUILayout.EndHorizontal();
        UIHelpers.DrawUILine(UIColors.GrayLine, 1, 4);
    }
}
