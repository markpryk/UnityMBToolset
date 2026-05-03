using UnityEditor;
using UnityEngine;

//  FloraTab - Top-level tab with internal subtab routing

/// <summary>
/// Flora tab for MBSceneDataManager.
/// Contains two subtabs:
///   1. Populator  - library browser, registration, terrain prototype sync
///   2. Decorator  - layered flora painting/rules (future implementation)
///
/// Subtabs share the same MBSceneDataManager context (terrain, module, library).
/// </summary>
public class FloraTab : SceneDataManagerTabBase
{
    public override string TabName => "Flora";
    public override int Order => 6;

    // Shared state across subtabs
    internal Terrain SceneTerrain;

    private int _activeSubTab;
    private IFloraSubTab[] _subTabs;

    public MBFloraLibrary Library
    {
        get => manager.FloraLibrary;
        set => manager.FloraLibrary = value;
    }

    public override void OnEnable(MBSceneDataManager manager, SerializedObject serializedObject)
    {
        base.OnEnable(manager, serializedObject);

        _subTabs = new IFloraSubTab[]
        {
            new FloraLibrarySubTab(),
            new FloraPopulatorSubTab(),
        };

        RefreshSharedState();

        foreach (var sub in _subTabs)
            sub.OnEnable(this);
    }

    public override void OnDisable()
    {
        if (_subTabs != null)
        {
            foreach (var sub in _subTabs)
                sub.OnDisable();
        }

        base.OnDisable();
    }

    public override void OnManagerChanged(MBSceneDataManager manager)
    {
        base.OnManagerChanged(manager);
        RefreshSharedState();

        if (_subTabs != null)
        {
            foreach (var sub in _subTabs)
                sub.OnManagerChanged(this);
        }
    }

    public override void DrawTab()
    {
        if (manager == null)
        {
            DrawHelpBox("No Scene Data Manager.", MessageType.Warning);
            return;
        }

        if (manager.Module == null)
        {
            DrawHelpBox("Assign a Module in the General tab to enable flora tools.", MessageType.Warning);
            return;
        }

        if (SceneTerrain == null)
        {
            DrawHelpBox("Assign a Terrain in the General tab first.", MessageType.Info);
            return;
        }

        DrawSubTabBar();
        DrawSpace(4);

        if (_subTabs != null && _activeSubTab >= 0 && _activeSubTab < _subTabs.Length)
            _subTabs[_activeSubTab].DrawSubTab(this);
    }

    private void DrawSubTabBar()
    {
        if (_subTabs == null) return;

        EditorGUILayout.BeginHorizontal();

        for (int i = 0; i < _subTabs.Length; i++)
        {
            GUI.backgroundColor = _activeSubTab == i
                ? new Color(0.55f, 0.85f, 0.55f)
                : Color.white;

            if (GUILayout.Button(_subTabs[i].SubTabName, EditorStyles.toolbarButton))
                _activeSubTab = i;
        }

        GUI.backgroundColor = Color.white;
        EditorGUILayout.EndHorizontal();
    }


    internal void RefreshSharedState()
    {
        SceneTerrain = manager?.Terrain;
        if (manager != null && manager.Module != null)
        {
            // Only reload if the library is missing - don't nuke a
            // perfectly good in-memory reference on every refresh.
            if (manager.FloraLibrary == null)
                TryLoadLibrary();
        }
    }

    internal void TryLoadLibrary()
    {
        if (manager?.Module == null) return;
        string path = MBPathHelpers.ModFloraLibraryAssetPath(manager.Module.ID);
        manager.FloraLibrary = AssetDatabase.LoadAssetAtPath<MBFloraLibrary>(path);
        manager.FloraLibrary?.RebuildLookups();
    }
}

//  Subtab contract

/// <summary>
/// Interface for Flora subtabs. Each subtab receives the parent FloraTab
/// for access to shared state (manager, terrain, library, base helpers).
/// </summary>
internal interface IFloraSubTab
{
    string SubTabName { get; }
    void OnEnable(FloraTab parent);
    void OnDisable();
    void OnManagerChanged(FloraTab parent);
    void DrawSubTab(FloraTab parent);
}
