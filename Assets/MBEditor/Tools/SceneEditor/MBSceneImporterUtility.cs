using System;
using MountAndBlade.Data;
using UnityEngine;

/// <summary>
/// Static utility class for importing scene data from different formats
/// </summary>
public static class MBSceneImporterUtility
{
    /// <summary>
    /// Loads scene data from a file
    /// </summary>
    /// <param name="moduleId">The module ID</param>
    /// <param name="sceneId">The scene ID</param>
    public static MBSceneData LoadSceneData(MBModule module,string sceneID)
    {
        try
        {
            foreach (var sceneData in module.scenes)
            {
                // Debug.LogError($"{sceneID}");

                if (sceneData == null)
                {
                    continue;
                }
                if (sceneData.SceneID.Equals(sceneID, StringComparison.OrdinalIgnoreCase))
                {
                    return sceneData;
                }
            }
            return null;
        }
        catch (Exception ex)
        {
            Debug.LogError($"Error loading scene data: {ex.Message}");
            Debug.LogException(ex);
            return null;
        }
    }
}