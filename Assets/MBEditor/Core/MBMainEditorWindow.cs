using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BDT.GUI.Helpers;
using MountAndBlade.ModdingToolkit;
using UnityEditor;
using UnityEngine;

public class MBMainEditorWindow : EditorWindow
{
    private MBModule _currentModule;
    private int _toolbarOption;
    private string[] _toolbarTexts = { "Tools", "module.ini" };

    private MBModuleImportWizard _importWizard;
    private MBTools _mbTools;

    [MenuItem("MBToolset/MB Editor", false, 3)]
    public static void ShowWindow()
    {
        GetWindow<MBMainEditorWindow>("MB Editor");
    }

    private void OnEnable()
    {
        _importWizard = new MBModuleImportWizard();
        _mbTools = new MBTools();
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField("MB Editor", StylesHelpers.BoxHeader());
        UIHelpers.DrawUILine(UIColors.GrayLine, 1, 4);

        DrawModuleSelection();

        if (_currentModule != null)
        {
            EditorGUILayout.Space(4);
            DrawToolbarGUI();
        }
    }

    private void DrawModuleSelection()
    {
        EditorGUILayout.LabelField("Current Module", EditorStyles.boldLabel);
        EditorGUI.BeginChangeCheck();
        _currentModule = (MBModule)EditorGUILayout.ObjectField(_currentModule, typeof(MBModule), false);
        if (EditorGUI.EndChangeCheck())
        {
            OnModuleChanged();
        }
    }

    private void OnModuleChanged()
    {
        MBEditorManager.MbEditorSettings.CurrentModule = _currentModule;
        SaveDataHelper.SaveScriptableObject(MBEditorManager.MbEditorSettings);
    }

    private void DrawToolbarGUI()
    {
        if (!_importWizard.Imported)
        {
            _importWizard.DrawWizardUI(_currentModule);
            
        }
        else
        {
            _toolbarOption = GUILayout.Toolbar(_toolbarOption, _toolbarTexts, GUILayout.Height(32));
            GUILayout.Space(2);
            GUIHelpers.DrawUILine(Color.grey);

            switch (_toolbarOption)
            {
                case 0:
                    _mbTools.DrawToolsGUI(_currentModule);
                    break;
                case 1:
                    MBModuleIniEditor.DrawModuleIniGUI(_currentModule);
                    break;
            }
        }
    }
}