using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Unity.Mathematics;
using Unity.VisualScripting;
using UnityEditor;
using UnityEngine;

public static class ImporterProps
{
    private static Matrix4x4 _globalTransformationMatrix;
    private static Mesh _assetMesh;
    private static MBModule _currentModule;

    private static ImporterBuildData _buildDataInstance;

    private static Transform _rootTransform;
    private static Transform _propsTransform;
    private static Transform _floraTransform;
    private static Transform _itemsTransform;
    private static Transform _particleTransform;
    private static Transform _entryPointTransform;
    private static Transform _passageTransform;
    private static Transform _missingTransform;

    public static ImporterBuildData BuildData
    {
        get
        {
            if (_buildDataInstance == null)
            {
                _buildDataInstance =
                    AssetDatabase.LoadAssetAtPath<ImporterBuildData>(MBPathHelpers.CommonImporterPropsBuildData());
                if (_buildDataInstance == null)
                {
                    Debug.LogError("ImporterPropsBuildData not found in Resources!");
                }
            }

            return _buildDataInstance;
        }
    }

    public static void RemoveAllProps()
    {
        // prop, entry, item, passage, plant
        GameObject[] allObjects = GameObject.FindGameObjectsWithTag("prop");
        GameObject[] allObjects2 = GameObject.FindGameObjectsWithTag("entry");
        GameObject[] allObjects3 = GameObject.FindGameObjectsWithTag("item");
        GameObject[] allObjects4 = GameObject.FindGameObjectsWithTag("passage");
        GameObject[] allObjects5 = GameObject.FindGameObjectsWithTag("plant");


        List<GameObject> objects = new List<GameObject>();
        objects.AddRange(allObjects);
        objects.AddRange(allObjects2);
        objects.AddRange(allObjects3);
        objects.AddRange(allObjects4);
        objects.AddRange(allObjects5);


        foreach (GameObject obj in objects)
        {
            Object.DestroyImmediate(obj);
        }


        foreach (GameObject obj in allObjects)
        {
            Object.DestroyImmediate(obj);
        }


        // //clear console
        // var logEntries = System.Type.GetType("UnityEditor.LogEntries, UnityEditor.dll");
        // var clearMethod = logEntries.GetMethod("Clear",
        //     System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public);
        // clearMethod.Invoke(null, null);
    }


    public static void LoadPropsFromJson(MBModule currentModule, string jsonPath)
    {
        _currentModule = currentModule;

        if (_currentModule == null)
        {
            Debug.LogError("Current module is null. Cannot load props.");
            return;
        }

        if (!File.Exists(jsonPath))
        {
            Debug.LogError($"JSON file not found at path: {jsonPath}");
            return;
        }

        string jsonContent = File.ReadAllText(jsonPath);
        List<MBScenePropEntityData> propList;

        try
        {
            propList = JsonConvert.DeserializeObject<List<MBScenePropEntityData>>(jsonContent);
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"Error parsing JSON: {ex.Message}");
            return;
        }

        if (propList == null || propList.Count == 0)
        {
            Debug.LogError("No props found in the JSON file.");
            return;
        }
        
        _rootTransform = new GameObject("SceneData").transform;
        _propsTransform = new GameObject("Props").transform;
        _propsTransform.parent = _rootTransform;
        _floraTransform = new GameObject("Flora").transform;
        _floraTransform.parent = _rootTransform;
        _itemsTransform = new GameObject("Items").transform;
        _itemsTransform.parent = _rootTransform;
        _particleTransform = new GameObject("Particles").transform;
        _particleTransform.parent = _rootTransform;
        _entryPointTransform = new GameObject("EntryPoints").transform;
        _entryPointTransform.parent = _rootTransform;
        _passageTransform = new GameObject("Passages").transform;
        _passageTransform.parent = _rootTransform;
        _missingTransform = new GameObject("Missing").transform;
        _missingTransform.parent = _rootTransform;

        foreach (var prop in propList)
        {
            PopuplateProps(prop);
        }

        FlipTransformations();
    }

    private static void PopuplateProps(MBScenePropEntityData mbScenePropEntityData)
    {
        GameObject propObject = new GameObject();
        propObject.tag = mbScenePropEntityData.type;
        propObject.name = mbScenePropEntityData.str;

        ApplyPropTransformations(mbScenePropEntityData, propObject);

        if (mbScenePropEntityData.type == "entry")
        {
            CreateEntryPoint(mbScenePropEntityData, propObject);
        }
        else if (mbScenePropEntityData.type == "prop")
        {
            CreateModuleSceneProp(mbScenePropEntityData, propObject);
        }
        else if (mbScenePropEntityData.type == "item")
        {
            CreateItem(mbScenePropEntityData, propObject);
        }
        else if (mbScenePropEntityData.type == "passage")
        {
            CreatePassage(mbScenePropEntityData, propObject);
        }
        else if (mbScenePropEntityData.type == "plant")
        {
            CreateFlora(mbScenePropEntityData, propObject);
        }
        else
        {
            CreateMissingProp(propObject, mbScenePropEntityData.str, "");
        }
    }


    private static void ApplyPropTransformations(MBScenePropEntityData mbScenePropEntityData, GameObject propObject)
    {
        // Set position with Z-axis (Warband) becoming Y-axis (Unity)
        propObject.transform.position = new Vector3(
            mbScenePropEntityData.pos[0],
            mbScenePropEntityData.pos[1],
            mbScenePropEntityData.pos[2]
        );

        // Set scale with Z = -1 to account for mirroring
        if (mbScenePropEntityData.scale != null && mbScenePropEntityData.scale.Length == 3)
        {
            propObject.transform.localScale = new Vector3(
                -mbScenePropEntityData.scale[0],
                mbScenePropEntityData.scale[2],
                mbScenePropEntityData.scale[1]
            );
        }
        else
        {
            propObject.transform.localScale = new Vector3(-1, 1, 1);
        }

        // Adjust the rotation matrix for the coordinate system difference
        if (mbScenePropEntityData.rotation_matrix != null && mbScenePropEntityData.rotation_matrix.Length == 3)
        {
            // "rotation_matrix": [
            // [0.9213221669197083, 0.3887999951839447, -0.0],
            // [-0.3887999951839447, 0.9213221669197083, 0.0],
            // [0.0, 0.0, 1.0]
            // ],

            Matrix4x4 matrix = new Matrix4x4();

            matrix.SetRow(0,
                new Vector4(mbScenePropEntityData.rotation_matrix[0][0], mbScenePropEntityData.rotation_matrix[0][1],
                    mbScenePropEntityData.rotation_matrix[0][2], 0));
            matrix.SetRow(1,
                new Vector4(mbScenePropEntityData.rotation_matrix[1][0], mbScenePropEntityData.rotation_matrix[1][1],
                    mbScenePropEntityData.rotation_matrix[1][2], 0));
            matrix.SetRow(2,
                new Vector4(mbScenePropEntityData.rotation_matrix[2][0], mbScenePropEntityData.rotation_matrix[2][1],
                    mbScenePropEntityData.rotation_matrix[2][2], 0));
            matrix.SetRow(3, new Vector4(0, 0, 0, 1));

            Quaternion rotation = matrix.inverse.rotation;
            propObject.transform.rotation = rotation;

            // additional rotation
            propObject.transform.Rotate(90, 0, 0);
            propObject.transform.Rotate(0, 180, 0);
        }
        else
        {
            propObject.transform.rotation = Quaternion.identity;
        }
    }

    private static void CreateItem(MBScenePropEntityData itemEntityData, GameObject itemObject)
    {
        // remove a spr_ prefix if exists
        string normalizedId = itemEntityData.str.StartsWith("itm_")
            ? itemEntityData.str.Substring(4)
            : itemEntityData.str;

        var data = _currentModule.items.FirstOrDefault(p => p?.ItemID == normalizedId);

        if (data == null)
        {
            Debug.LogWarning($"Item '{normalizedId}' not found in DataBase.");
            CreateMissingProp(itemObject, normalizedId, "ITEM", true);
            return;
        }

        var modelName = data.ItemID;

        // Try exact name in current module first
        var prefab = MBEditorUtility.GetItemPrefab(_currentModule.ID, modelName);
        if (prefab == null)
        {
            // Try Native module
            prefab = MBEditorUtility.GetItemPrefab("Native", modelName);
        }

        if (prefab != null)
        {
            var item = PrefabUtility.InstantiatePrefab(prefab) as GameObject;
            item.transform.position = itemObject.transform.position;
            item.transform.rotation = itemObject.transform.rotation;
            item.transform.localScale = itemObject.transform.localScale;
            item.tag = itemObject.tag;
            item.name = itemObject.name;
            item.transform.SetParent(_itemsTransform);
            
            Object.DestroyImmediate(itemObject);
        }
        else
        {
            Debug.LogWarning(
                $"Prefab for Item '{normalizedId}' with mesh '{modelName}' not found, creating a placeholder.");
            CreateMissingProp(itemObject, modelName, "ITEM");
        }
    }

    private static void CreateFlora(MBScenePropEntityData mbScenePropEntityData, GameObject propObject)
{
    var data = _currentModule.flora.FirstOrDefault(p => p?.FloraID == mbScenePropEntityData.str);

    if (data == null)
    {
        Debug.LogWarning($"Flora '{mbScenePropEntityData.str}' not found in DataBase.");
        CreateMissingProp(propObject, mbScenePropEntityData.str, "FLORA", true);
        return;
    }

    var modelName = data.FloraID;

    // Try to load the flora prefab
    var prefab = MBEditorUtility.GetFloraPrefab(_currentModule.ID, modelName);
    if (prefab == null)
    {
        prefab = MBEditorUtility.GetFloraPrefab("Native", modelName);
    }

    if (prefab != null)
    {
        var flora = PrefabUtility.InstantiatePrefab(prefab) as GameObject;
        flora.transform.position = propObject.transform.position;
        flora.transform.rotation = propObject.transform.rotation;
        flora.transform.localScale = propObject.transform.localScale;
        flora.tag = propObject.tag;
        flora.name = propObject.name;
        flora.transform.SetParent(_floraTransform);

        var mbFlora = flora.GetComponent<MBFlora>();
        if (mbFlora != null)
        {
            int variantId = mbScenePropEntityData.entry_no;
            int meshCount = data.Meshes?.Count ?? 0;

            if (variantId > 0 && variantId < meshCount)
            {
                // Variant differs from the default (index 0) baked into the prefab
                MBFloraEditor.SwapModelVariant(mbFlora, variantId);
            }
            else
            {
                // Either variant 0 (already the default) or out of range - just store the ID
                mbFlora.FloraVariantID = Mathf.Clamp(variantId, 0, Mathf.Max(0, meshCount - 1));
            }
        }

        Object.DestroyImmediate(propObject);
    }
    else
    {
        Debug.LogWarning($"Prefab for Flora '{modelName}' not found, creating a placeholder.");
        CreateMissingProp(propObject, modelName, "FLORA");
    }
}



    private static void CreateModuleSceneProp(MBScenePropEntityData mbScenePropEntityData, GameObject propObject)
    {
        // remove a spr_ prefix if exists
        string normalizedId = mbScenePropEntityData.str.StartsWith("spr_")
            ? mbScenePropEntityData.str.Substring(4)
            : mbScenePropEntityData.str;

        var data = _currentModule.sceneProps.FirstOrDefault(p => p?.PropID == normalizedId);

        if (data == null)
        {
            Debug.LogWarning($"Scene prop '{normalizedId}' not found in DataBase.");
            CreateMissingProp(propObject, normalizedId, "PROP", true);
            return;
        }

        var modelName = data.PropID;

        // Try to Load the Prefab
        var prefab = MBEditorUtility.GetScenePropPrefab(_currentModule.ID, modelName);
        if (prefab == null)
        {
            // Try to Find in Native Prefabs
            prefab = MBEditorUtility.GetScenePropPrefab("Native", modelName);
        }

        if (prefab == null)
        {
            CreateMissingProp(propObject, modelName);
        }
        else
        {
            var prop = PrefabUtility.InstantiatePrefab(prefab) as GameObject;
            prop.transform.position = propObject.transform.position;
            prop.transform.rotation = propObject.transform.rotation;
            prop.transform.localScale = propObject.transform.localScale;
            prop.tag = propObject.tag;
            prop.name = propObject.name;
            prop.transform.SetParent(_propsTransform);

            Object.DestroyImmediate(propObject);
        }
    }

    private static void CreateMissingProp(GameObject propObject, string modelName, string type = "PROP",
        bool isData = false)
    {
        var miss = isData ? "Data Missing" : "Missing";

        Debug.LogWarning($"Prefab for {modelName} not found, creating a placeholder.");
        GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
        cube.name = $"{type}_{modelName}({miss})";
        propObject.name += "(Missing)";

        cube.transform.parent = propObject.transform;
        cube.GetComponent<MeshRenderer>().material = BuildData.MissingMaterial;
        cube.transform.localPosition = Vector3.zero;
        cube.transform.localScale = Vector3.one * 0.5f;
        cube.transform.localRotation = Quaternion.identity;

        propObject.transform.SetParent(_missingTransform);
    }

    private static void CreateEntryPoint(MBScenePropEntityData mbScenePropEntityData, GameObject propObject)
    {
        var nm = "EntryPoint_" + mbScenePropEntityData.entry_no;
        GameObject obj = PrefabUtility.InstantiatePrefab(BuildData.EntryPointPrefab) as GameObject;
        PrefabUtility.UnpackPrefabInstance(obj, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
        MBEntryPoint entry = obj.GetComponent<MBEntryPoint>();
        entry.EntryNumber = mbScenePropEntityData.entry_no;
        obj.name = nm;

        obj.transform.position = propObject.transform.position;
        obj.transform.rotation = propObject.transform.rotation;
        obj.transform.localScale = propObject.transform.localScale;
        obj.transform.SetParent(_entryPointTransform);

        Object.DestroyImmediate(propObject);
    }

    private static void CreatePassage(MBScenePropEntityData mbScenePropEntityData, GameObject propObject)
    {
        var nm = "Passage_" + mbScenePropEntityData.menu_entry_no;
        GameObject obj = PrefabUtility.InstantiatePrefab(BuildData.PassagePrefab) as GameObject;
        PrefabUtility.UnpackPrefabInstance(obj, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
        MBPassage passage = obj.GetComponent<MBPassage>();
        passage.EntryNumber = mbScenePropEntityData.entry_no;
        passage.MenuNumber = mbScenePropEntityData.menu_entry_no;
        passage.PassageId = mbScenePropEntityData.id;
        obj.name = nm;

        obj.transform.position = propObject.transform.position;
        obj.transform.rotation = propObject.transform.rotation;
        obj.transform.localScale = propObject.transform.localScale;
        obj.transform.SetParent(_passageTransform);

        Object.DestroyImmediate(propObject);
    }
    public static void FlipTransformations()
    {
        GameObject[] allObjects = GameObject.FindGameObjectsWithTag("prop");
        GameObject[] allObjects2 = GameObject.FindGameObjectsWithTag("entry");
        GameObject[] allObjects3 = GameObject.FindGameObjectsWithTag("item");
        GameObject[] allObjects4 = GameObject.FindGameObjectsWithTag("passage");
        GameObject[] allObjects5 = GameObject.FindGameObjectsWithTag("plant");

        List<GameObject> objects = new List<GameObject>();
        objects.AddRange(allObjects);
        objects.AddRange(allObjects2);
        objects.AddRange(allObjects3);
        objects.AddRange(allObjects4);
        objects.AddRange(allObjects5);

        var originalParents = new Dictionary<GameObject, Transform>();
        foreach (var prop in objects)
        {
            originalParents[prop] = prop.transform.parent;
        }

        var flipper = new GameObject("Props_Flipper");

        foreach (var prop in objects)
        {
            prop.transform.SetParent(flipper.transform);
        }

        flipper.transform.localScale = new Vector3(-1, 1, 1);
        flipper.transform.eulerAngles = new Vector3(-90, 180, 0);

        // Restore original parents instead of setting to null
        foreach (var prop in objects)
        {
            prop.transform.SetParent(originalParents[prop]);
        }

        Object.DestroyImmediate(flipper);
    }
}
