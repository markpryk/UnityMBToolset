using UnityEngine;

public static class MBMainEditorGUIData
{
    private static bool _moduleConfig = false;
    public static bool ModuleConfig
    {
        get => _moduleConfig;
        set => _moduleConfig = value;
    }
}
