using System.Collections.Generic;
using System.IO;
using UnityEngine;

public static class ModuleInspector
{
    private static string gamePath = MBPathHelpers.EngineModulesFolder();

    /// <summary>
    /// Gets a list of module paths that contain a module.ini file.
    /// </summary>
    public static List<string> GetModulePaths()
    {
        List<string> modulePaths = new List<string>();

        if (!Directory.Exists(gamePath))
        {
            Debug.LogError($"Game path not found: {gamePath}");
            return modulePaths;
        }

        string[] directories = Directory.GetDirectories(gamePath);

        foreach (var directory in directories)
        {
            string moduleIniPath = Path.Combine(directory, "module.ini");
            if (File.Exists(moduleIniPath))
            {
                modulePaths.Add(directory);
            }
        }

        return modulePaths;
    }

    /// <summary>
    /// Gets a list of .sco file names (without extensions) from the SceneObj folder.
    /// </summary>
    public static List<string> GetSceneObjFileNames(string moduleName = "Native")
    {
        List<string> sceneObjFiles = new List<string>();

        string sceneObjPath = Path.Combine(gamePath, moduleName, "SceneObj");

        if (!Directory.Exists(sceneObjPath))
        {
            Debug.LogError($"SceneObj path not found: {sceneObjPath}");
            return sceneObjFiles;
        }

        string[] files = Directory.GetFiles(sceneObjPath, "*.sco");

        foreach (var file in files)
        {
            sceneObjFiles.Add(Path.GetFileNameWithoutExtension(file));
        }

        return sceneObjFiles;
    }

    /// <summary>
    /// Gets the file path of a .sco file by matching its name (without extension).
    /// This is more reliable than index-based lookup since directory ordering can vary.
    /// </summary>
    /// <param name="moduleName">The name of the module.</param>
    /// <param name="sceneName">The scene name without extension (e.g., "scn_town_1").</param>
    /// <returns>Full file path, or null if not found.</returns>
    public static string GetSceneObjFileByName(string moduleName, string sceneName)
    {
        string sceneObjPath = Path.Combine(gamePath, moduleName, "SceneObj");

        if (!Directory.Exists(sceneObjPath))
        {
            Debug.LogError($"SceneObj path not found: {sceneObjPath}");
            return null;
        }

        string targetPath = Path.Combine(sceneObjPath,$"scn_{sceneName}.sco");

        if (File.Exists(targetPath))
            return targetPath;

        Debug.LogWarning($"[ModuleInspector] .sco file not found: {targetPath}");
        return null;
    }

    /// <summary>
    /// Gets the file path of a .sco file by its index.
    /// Note: ordering depends on filesystem, prefer GetSceneObjFileByName when possible.
    /// </summary>
    public static string GetSceneObjFileById(string moduleName, int id)
    {
        string sceneObjPath = Path.Combine(gamePath, moduleName, "SceneObj");
        string[] files = Directory.GetFiles(sceneObjPath, "*.sco");

        if (id >= 0 && id < files.Length)
        {
            return files[id];
        }

        Debug.LogError($"Invalid ID: {id}. Available range: 0 to {files.Length - 1}");
        return null;
    }

    /// <summary>
    /// Gets the preview image path for a scene if it exists.
    /// </summary>
    /// <param name="moduleName">The name of the module.</param>
    /// <param name="sceneId">The scene ID.</param>
    /// <returns>Full path to the image, or null if not found.</returns>
    public static string GetScenePreviewPath(string moduleName, string sceneId)
    {
        string thumbnailsPath = Path.Combine(gamePath, moduleName, "SceneObj", "thumbnails");
        
        if (!Directory.Exists(thumbnailsPath))
            return null;

        // Check for common image formats
        string jpgPath = Path.Combine(thumbnailsPath, $"{sceneId}.jpg");
        if (File.Exists(jpgPath)) return jpgPath;

        string pngPath = Path.Combine(thumbnailsPath, $"{sceneId}.png");
        if (File.Exists(pngPath)) return pngPath;

        return null;
    }
}