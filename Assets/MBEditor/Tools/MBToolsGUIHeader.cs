using UnityEngine;

public static class MBToolsGUIHeader 
{
    private static bool _sceneTools = false;
    public static bool SceneTools
    {
        get => _sceneTools;
        set => _sceneTools = value;
    }
}
