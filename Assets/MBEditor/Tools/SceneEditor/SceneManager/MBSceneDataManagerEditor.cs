using UnityEngine;
using UnityEditor;

/// <summary>
/// Main custom editor for MBSceneDataManager.
/// Uses a modular tab system for easy extension and maintenance.
/// 
/// To add a new tab:
/// 1. Create a class implementing ISceneDataManagerTab (or extending SceneDataManagerTabBase)
/// 2. Register it in SceneDataManagerTabRegistry.Initialize() or call RegisterTab() at runtime
/// 
/// To remove a tab:
/// 1. Call SceneDataManagerTabRegistry.UnregisterTab<YourTabType>()
/// </summary>
[CustomEditor(typeof(MBSceneDataManager))]
public class MBSceneDataManagerEditor : Editor
{
    private MBSceneDataManager manager;
    
    private void OnEnable()
    {
        manager = (MBSceneDataManager)target;
        
        // Initialize the tab system
        SceneDataManagerTabRegistry.OnEnable(manager, serializedObject);
    }
    
    private void OnDisable()
    {
        SceneDataManagerTabRegistry.OnDisable();
    }
    
    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        
        // Draw header
        DrawHeader();
        
        // Draw tab bar
        SceneDataManagerTabRegistry.DrawTabBar();
        
        EditorGUILayout.Space(10);
        
        // Draw current tab content
        SceneDataManagerTabRegistry.DrawCurrentTab();
        
        serializedObject.ApplyModifiedProperties();
    }
    
    private void DrawHeader()
    {
        EditorGUILayout.LabelField("M&B Scene Data Manager", EditorStyles.boldLabel);
        
        // Scene name field (always visible)
        var sceneNameProp = serializedObject.FindProperty("sceneName");
        EditorGUILayout.PropertyField(sceneNameProp, new GUIContent("Scene Name"));
        
        DrawUILine(Color.grey);
    }
    
    private void DrawUILine(Color color, int thickness = 1, int padding = 10)
    {
        Rect r = EditorGUILayout.GetControlRect(GUILayout.Height(padding + thickness));
        r.height = thickness;
        r.y += padding / 2f;
        r.x -= 2;
        r.width += 6;
        EditorGUI.DrawRect(r, color);
    }
}