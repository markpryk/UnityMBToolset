using UnityEngine;

public  static class SaveDataHelper
{
    public static void SaveScriptableObject(Object data)
    {
        UnityEditor.EditorUtility.SetDirty(data);
        UnityEditor.AssetDatabase.SaveAssets();
        UnityEditor.AssetDatabase.Refresh();
    }
}
