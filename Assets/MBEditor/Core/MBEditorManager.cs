using UnityEditor;
using UnityEngine;

public static class MBEditorManager
{
    private static MBEditorSettings _settingsInstance;

    public static MBEditorSettings MbEditorSettings
    {
        get
        {
            if (_settingsInstance == null)
            {
                _settingsInstance =
                    AssetDatabase.LoadAssetAtPath<MBEditorSettings>(MBPathHelpers.MBEditorSettings);
                if (_settingsInstance == null)
                {
                    Debug.LogError("MBEditorSettings not found in Editor Configs!");
                }
            }

            return _settingsInstance;
        }
    }
    
}