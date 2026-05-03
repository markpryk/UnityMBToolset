using System.Collections.Generic;
using System.IO;
using System.Linq;
using MountAndBlade.Data;
using Newtonsoft.Json;
using UnityEditor;
using UnityEngine;

public static class ExporterProps
{
    private static MBModule _currentModule;

    public static void ExportPropsToJson(MBModule currentModule, string jsonPath, Terrain sceneTerrain,
        MBFloraLibrary floraLib)
    {
        _currentModule = currentModule;

        if (_currentModule == null)
        {
            Debug.LogError("Current module is null. Cannot export props.");
            return;
        }

        List<MBEntity> allProps;
        allProps = Object.FindObjectsByType<MBEntity>(FindObjectsInactive.Exclude, FindObjectsSortMode.None).ToList();

        if (allProps.Count == 0)
        {
            Debug.LogWarning("No props found in the scene to export.");
            return;
        }

        List<MBScenePropEntityData> propDataList = new List<MBScenePropEntityData>();

        // Store original parents BEFORE reparenting
        var originalParents = new Dictionary<MBEntity, Transform>();
        foreach (MBEntity prop in allProps)
        {
            originalParents[prop] = prop.transform.parent;
        }

        GameObject flipper = new GameObject("Flipper");

        foreach (MBEntity prop in allProps)
        {
            prop.transform.SetParent(flipper.transform);
        }

        // flip to M&B coords for export
        flipper.transform.localScale = new Vector3(-1, 1, 1);
        flipper.transform.eulerAngles = new Vector3(-90, 180, 0);

        // export in M&B space
        foreach (MBEntity prop in allProps)
        {
            MBScenePropEntityData scenePropEntityData = ConvertGameObjectToPropData(prop);
            if (scenePropEntityData != null)
            {
                propDataList.Add(scenePropEntityData);
            }
        }

        if (sceneTerrain != null && floraLib != null)
        {
            var terrainFlora = ExporterTerrainFlora.CollectTerrainFlora(
                sceneTerrain,
                floraLib,
                exportDetails: false);
            propDataList.AddRange(terrainFlora);
            Debug.Log($"[Export] Added {terrainFlora.Count} terrain flora entries");
        }

        string jsonContent = JsonConvert.SerializeObject(propDataList, Formatting.Indented,
            new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore });
        File.WriteAllText(jsonPath, jsonContent);

        // flip back to Unity coords BEFORE restoring parents
        flipper.transform.eulerAngles = Vector3.zero;
        flipper.transform.localScale = Vector3.one;

        // restore original parents
        foreach (MBEntity prop in allProps)
        {
            prop.transform.SetParent(originalParents[prop]);
        }

        GameObject.DestroyImmediate(flipper);
        Debug.Log($"Successfully exported {propDataList.Count} props to {jsonPath}");
    }

    private static MBScenePropEntityData ConvertGameObjectToPropData(MBEntity entityObject)
    {
        MBScenePropEntityData scenePropEntityData = new MBScenePropEntityData();


        // Reverse the transformation applied during import
        Transform transform = entityObject.transform;

        // Position: Convert back from Unity to Warband coordinate system
        scenePropEntityData.pos = new float[]
        {
            transform.position.x,
            transform.position.y,
            transform.position.z
        };

        // Scale: Invert the scale conversion from import
        scenePropEntityData.scale = new float[]
        {
            transform.localScale.x, // Negate x to reverse the mirror
            transform.localScale.z, // z and y were swapped
            transform.localScale.y
        };

        // Rotation: Convert Unity quaternion back to Warband rotation matrix
        // First undo the additional rotation applied during import
        Quaternion undoAdditionalRotation = Quaternion.Euler(0, -180, 0) * Quaternion.Euler(-90, 0, 0);
        Quaternion originalRotation = transform.rotation * undoAdditionalRotation;

        Matrix4x4 rotMatrix = Matrix4x4.Rotate(originalRotation);
        rotMatrix = rotMatrix.inverse; // Apply the inverse to match the import logic

        // Convert to the expected 3x3 format
        scenePropEntityData.rotation_matrix = new float[3][];
        for (int i = 0; i < 3; i++)
        {
            scenePropEntityData.rotation_matrix[i] = new float[3];
            for (int j = 0; j < 3; j++)
            {
                scenePropEntityData.rotation_matrix[i][j] = rotMatrix[i, j];
            }
        }

        if (entityObject is MBItem)
        {
            scenePropEntityData.str = "itm_" + ((MBItem)entityObject).ItemData.ItemID;
            scenePropEntityData.type = "item";
        }
        else if (entityObject is MBSceneProp)
        {
            scenePropEntityData.str = "spr_" + ((MBSceneProp)entityObject).ScenePropData.PropID;
            scenePropEntityData.type = "prop";
        }
        else if (entityObject is MBEntryPoint)
        {
            scenePropEntityData.type = "entry";
            scenePropEntityData.entry_no = ((MBEntryPoint)entityObject).EntryNumber;
        }
        else if (entityObject is MBPassage)
        {
            scenePropEntityData.type = "passage";
            scenePropEntityData.id = ((MBPassage)entityObject).PassageId;
            scenePropEntityData.entry_no = ((MBPassage)entityObject).EntryNumber;
            scenePropEntityData.menu_entry_no = ((MBPassage)entityObject).MenuNumber;
        }
        else if (entityObject is MBFlora)
        {
            scenePropEntityData.type = "plant";
            scenePropEntityData.str = ((MBFlora)entityObject).FloraData.FloraID;
            scenePropEntityData.entry_no = ((MBFlora)entityObject).FloraVariantID;
        }

        return scenePropEntityData;
    }
}