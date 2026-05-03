using System.Collections.Generic;
using MountAndBlade.Data;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(MBFlora))]
public class MBFloraEditor : Editor
{
    private MBFlora _flora;
    private SerializedProperty _floraDataProp;
    private SerializedProperty _variantIdProp;
    private SerializedProperty _modelProp;

    // Cached variant labels for the popup
    private string[] _variantLabels;
    private int _lastMeshCount = -1;

    private void OnEnable()
    {
        _flora = (MBFlora)target;
        _floraDataProp = serializedObject.FindProperty("FloraData");
        _variantIdProp = serializedObject.FindProperty("FloraVariantID");
        _modelProp = serializedObject.FindProperty("Model");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        EditorGUILayout.LabelField("Flora Instance", EditorStyles.boldLabel);
        EditorGUILayout.Space(2);

        // Standard fields
        EditorGUILayout.PropertyField(serializedObject.FindProperty("_prefabId"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("_sourceModule"));
        EditorGUILayout.PropertyField(_floraDataProp);

        EditorGUILayout.Space(4);

        var floraData = _flora.FloraData;
        if (floraData == null)
        {
            EditorGUILayout.HelpBox("No FloraData assigned.", MessageType.Warning);
            serializedObject.ApplyModifiedProperties();
            return;
        }

        if (!string.IsNullOrEmpty(floraData.Flags))
        {
            DrawFlagsSummary(floraData.Flags);
            EditorGUILayout.Space(4);
        }

        int meshCount = floraData.Meshes?.Count ?? 0;

        if (meshCount == 0)
        {
            EditorGUILayout.HelpBox("FloraData has no meshes defined.", MessageType.Info);
            EditorGUILayout.PropertyField(_modelProp);
            serializedObject.ApplyModifiedProperties();
            return;
        }

        // Rebuild labels when mesh count changes
        if (_variantLabels == null || _lastMeshCount != meshCount)
        {
            RebuildVariantLabels(floraData);
            _lastMeshCount = meshCount;
        }

        EditorGUILayout.LabelField("Variant Selection", EditorStyles.boldLabel);

        EditorGUI.BeginChangeCheck();
        int newVariant = EditorGUILayout.Popup(
            "Mesh Variant",
            Mathf.Clamp(_variantIdProp.intValue, 0, meshCount - 1),
            _variantLabels);

        if (EditorGUI.EndChangeCheck())
        {
            _variantIdProp.intValue = newVariant;
            serializedObject.ApplyModifiedProperties();

            // Swap the model on this instance
            SwapModelVariant(_flora, newVariant);
            return; // SwapModelVariant handles serialization
        }

        var currentMesh = floraData.Meshes[Mathf.Clamp(_variantIdProp.intValue, 0, meshCount - 1)];
        using (new EditorGUI.IndentLevelScope())
        {
            EditorGUILayout.LabelField("Mesh", string.IsNullOrEmpty(currentMesh.Mesh) ? "(none)" : currentMesh.Mesh);
            EditorGUILayout.LabelField("Collision",
                string.IsNullOrEmpty(currentMesh.MeshCollision) ? "(none)" : currentMesh.MeshCollision);

            if (!string.IsNullOrEmpty(currentMesh.AlternativeMesh))
                EditorGUILayout.LabelField("Alt Mesh", currentMesh.AlternativeMesh);
            if (!string.IsNullOrEmpty(currentMesh.AlternativeMeshCollision))
                EditorGUILayout.LabelField("Alt Collision", currentMesh.AlternativeMeshCollision);
        }

        EditorGUILayout.Space(4);

        EditorGUILayout.PropertyField(_modelProp);

        EditorGUILayout.Space(4);
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Refresh Model", GUILayout.Height(24)))
            {
                SwapModelVariant(_flora, _variantIdProp.intValue);
            }

            if (meshCount > 1 && GUILayout.Button("Cycle →", GUILayout.Width(70), GUILayout.Height(24)))
            {
                int next = (_variantIdProp.intValue + 1) % meshCount;
                _variantIdProp.intValue = next;
                serializedObject.ApplyModifiedProperties();
                SwapModelVariant(_flora, next);
                return;
            }
        }

        serializedObject.ApplyModifiedProperties();
    }

    // Variant label building

    private void RebuildVariantLabels(MBFloraData floraData)
    {
        var labels = new List<string>();
        for (int i = 0; i < floraData.Meshes.Count; i++)
        {
            var mesh = floraData.Meshes[i];
            string meshName = string.IsNullOrEmpty(mesh.Mesh) ? "(empty)" : mesh.Mesh;
            labels.Add($"[{i}] {meshName}");
        }

        _variantLabels = labels.ToArray();
    }

    // Model swapping

    /// <summary>
    /// Swaps the Model child of an MBFlora instance to match the given variant index.
    /// Works on both scene instances and prefab editing mode.
    /// </summary>
    public static void SwapModelVariant(MBFlora flora, int variantIndex)
    {
        if (flora.FloraData == null || flora.FloraData.Meshes == null)
            return;

        var meshes = flora.FloraData.Meshes;
        if (variantIndex < 0 || variantIndex >= meshes.Count)
            return;

        string meshName = meshes[variantIndex].Mesh;
        if (string.IsNullOrEmpty(meshName))
        {
            Debug.LogWarning($"[MBFloraEditor] Variant {variantIndex} has no mesh name.");
            return;
        }

        Undo.RecordObject(flora, "Switch Flora Variant");

        // Resolve the model prefab through the same pipeline as MBPrefabsGenerator
        var modelPrefab = MBFloraVariantResolver.ResolveModelPrefab(flora.SourceModule, meshName);

        if (modelPrefab == null)
        {
            Debug.LogWarning($"[MBFloraEditor] Could not resolve prefab for mesh '{meshName}' (variant {variantIndex})");
            return;
        }

        // Remove existing Model child
        RemoveModelChild(flora);

        // Create new Model hierarchy
        var modelRoot = new GameObject("Model");
        Undo.RegisterCreatedObjectUndo(modelRoot, "Create Model Root");
        modelRoot.transform.SetParent(flora.transform);
        modelRoot.transform.localPosition = Vector3.zero;
        modelRoot.transform.localRotation = Quaternion.identity;
        modelRoot.transform.localScale = Vector3.one;

        var modelInstance = (GameObject)PrefabUtility.InstantiatePrefab(modelPrefab, modelRoot.transform);
        Undo.RegisterCreatedObjectUndo(modelInstance, "Instantiate Model");

        var modelComponent = modelInstance.GetComponent<MBModel>();
        if (modelComponent == null)
        {
            modelComponent = modelInstance.AddComponent<MBModel>();
            modelComponent.ModelID = modelPrefab.name;
        }

        flora.Model = modelComponent;
        flora.FloraVariantID = variantIndex;

        EditorUtility.SetDirty(flora);

        // If editing a prefab asset, mark the prefab stage dirty
        var prefabStage = UnityEditor.SceneManagement.PrefabStageUtility.GetCurrentPrefabStage();
        if (prefabStage != null)
        {
            EditorUtility.SetDirty(prefabStage.prefabContentsRoot);
        }

        Debug.Log($"[MBFloraEditor] Swapped '{flora.FloraData.FloraID}' to variant {variantIndex}: {meshName} → {modelPrefab.name}");
    }

    private static void RemoveModelChild(MBFlora flora)
    {
        // Find and destroy the "Model" child
        var existingModel = flora.transform.Find("Model");
        if (existingModel != null)
        {
            Undo.DestroyObjectImmediate(existingModel.gameObject);
        }

        // Also clear any orphaned MBModel reference
        flora.Model = null;
    }

    // Flags summary drawer

    private void DrawFlagsSummary(string rawFlags)
    {
        var decoded = FloraFlagsDecoder.DecodeAll(rawFlags);

        EditorGUILayout.LabelField("Flags Summary", EditorStyles.boldLabel);

        using (new EditorGUI.IndentLevelScope())
        {
            // Type
            string typeStr = FloraFlagsDecoder.GetFloraTypeString(decoded.Type);
            EditorGUILayout.LabelField("Type", typeStr);

            // Terrain
            string terrainStr = FloraFlagsDecoder.GetTerrainDescription(decoded.Terrain);
            EditorGUILayout.LabelField("Terrain", terrainStr);

            // Density
            EditorGUILayout.LabelField("Density (from flags)", decoded.Density.ToString());

            // Key behavior flags on one line
            var behaviors = new List<string>();
            if (decoded.Behavior.AlignWithGround) behaviors.Add("AlignGround");
            if (decoded.Behavior.PointUp) behaviors.Add("PointUp");
            if (decoded.Behavior.OnGreenGround) behaviors.Add("OnGreen");
            if (decoded.Behavior.Guarantee) behaviors.Add("Guarantee");
            if (decoded.Behavior.Snowy) behaviors.Add("Snowy");
            if (decoded.Behavior.HasColonyProps) behaviors.Add("Colony");

            if (behaviors.Count > 0)
                EditorGUILayout.LabelField("Behavior", string.Join(", ", behaviors));
        }
    }
}
