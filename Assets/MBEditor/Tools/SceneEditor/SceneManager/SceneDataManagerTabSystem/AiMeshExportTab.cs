using System.Collections.Generic;
using System.IO;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.ProBuilder;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.ProBuilder;
using UnityEngine.SceneManagement;
using EditorUtility = UnityEditor.EditorUtility;

/// <summary>
/// AI Mesh Tab - part of the MBSceneDataManager inspector pipeline.
///
/// Unified workflow:
///   Surface → Bake NavMesh → Convert to ProBuilder → Edit → Export OBJ
///
/// Each step is optional - you can jump in at any point:
///  • Have a NavMeshSurface? Bake it. Don't have one? Create it or skip to import.
///  • Have baked NavMesh data? Convert to ProBuilder. Have an OBJ? Load it instead.
///  • Have a ProBuilder mesh? Edit it, then export. Want a quick export? Skip editing.
///
/// Output always resolves to: {EditorScoSceneDataPath}/ai_mesh.obj
/// </summary>
public class AiMeshExportTab : SceneDataManagerTabBase
{
    public override string TabName => "AI Mesh";
    public override int    Order   => 85;

    private int  _areaFilter      = ~0;
    private bool _swapYZ          = true;
    private bool _flipFaceWinding = false;

    private bool _showSettings = false;

    private string      _status     = string.Empty;
    private MessageType _statusType = MessageType.None;

    private const string PbMeshName = "AiMesh_ProBuilder";
    private static readonly string[] AreaNames = NavMesh.GetAreaNames();

    // Lifecycle

    public override void OnEnable(MBSceneDataManager manager, SerializedObject serializedObject)
    {
        base.OnEnable(manager, serializedObject);
        _status = string.Empty;
    }

    // Main draw

    public override void DrawTab()
    {
        // Cache scene lookups once per frame
        var surface = FindSceneNavMeshSurface();
        var pbMesh  = FindScenePbMesh();
        NavMeshTriangulation tri = NavMesh.CalculateTriangulation();
        bool hasNavData = tri.vertices != null && tri.vertices.Length > 0;

        DrawSceneStatePanel(surface, pbMesh, hasNavData, tri);
        DrawSpace(6);

        DrawNavMeshSurfaceSection(surface);
        DrawSpace(4);

        DrawUILine(new Color(0.35f, 0.35f, 0.35f));
        DrawSpace(4);

        DrawProBuilderSection(surface, pbMesh, hasNavData);
        DrawSpace(4);

        DrawUILine(new Color(0.35f, 0.35f, 0.35f));
        DrawSpace(4);

        DrawExportSection(pbMesh);
        DrawSpace(4);

        DrawSettingsSection();
        DrawSpace(4);

        DrawStatusDisplay();
    }

    // 1. Scene State - single consolidated status box

    private void DrawSceneStatePanel(NavMeshSurface surface, ProBuilderMesh pbMesh,
                                      bool hasNavData, NavMeshTriangulation tri)
    {
        DrawHeader("Scene State");
        BeginBox();

        // Surface row
        BeginHorizontal();
        if (surface != null)
        {
            string agent = GetAgentTypeName(surface.agentTypeID);
            DrawColorLabel($"✔  Surface: {surface.gameObject.name}  (Agent: {agent})", StatusColor.Good);
        }
        else
        {
            DrawColorLabel("✕  No NavMeshSurface in scene", StatusColor.Warn);
        }
        if (surface != null && GUILayout.Button("Ping", EditorStyles.miniButton, GUILayout.Width(36)))
            EditorGUIUtility.PingObject(surface.gameObject);
        EndHorizontal();

        // NavMesh data row
        BeginHorizontal();
        if (hasNavData)
        {
            int vc = tri.vertices.Length;
            int tc = tri.indices.Length / 3;
            DrawColorLabel($"✔  NavMesh: {vc:N0} verts / {tc:N0} tris", StatusColor.Good);
        }
        else
        {
            DrawColorLabel("✕  NavMesh: No baked data", StatusColor.Warn);
        }
        EndHorizontal();

        // ProBuilder mesh row
        BeginHorizontal();
        if (pbMesh != null)
        {
            var mf = pbMesh.GetComponent<MeshFilter>();
            int vc = mf?.sharedMesh?.vertexCount ?? 0;
            int tc = (mf?.sharedMesh?.triangles?.Length ?? 0) / 3;
            DrawColorLabel($"✔  ProBuilder: {vc:N0} verts / {tc:N0} tris", StatusColor.Good);
        }
        else
        {
            EditorGUILayout.LabelField("○  ProBuilder: No AI mesh", EditorStyles.miniLabel);
        }
        if (pbMesh != null && GUILayout.Button("Select", EditorStyles.miniButton, GUILayout.Width(48)))
            SelectObject(pbMesh.gameObject);
        EndHorizontal();

        // Export file row
        BeginHorizontal();
        string outputPath = GetOutputPath();
        bool fileExists = File.Exists(outputPath);
        if (fileExists)
            DrawColorLabel("✔  ai_mesh.obj exported", StatusColor.Good);
        else
            EditorGUILayout.LabelField("○  ai_mesh.obj not yet exported", EditorStyles.miniLabel);
        if (fileExists && GUILayout.Button("Show", EditorStyles.miniButton, GUILayout.Width(42)))
            EditorUtility.RevealInFinder(outputPath);
        EndHorizontal();

        EndBox();
    }

    // 2. NavMesh Surface - create / bake / clear / settings

    private void DrawNavMeshSurfaceSection(NavMeshSurface surface)
    {
        DrawHeader("NavMesh Surface");

        if (surface == null)
        {
            BeginHorizontal();

            if (DrawButton("＋  Create Surface", new Color(0.3f, 0.85f, 0.5f), 28))
                CreateNavMeshSurface();

            if (DrawButton("⚙  Agent Settings", new Color(0.65f, 0.75f, 1f), 28))
                OpenNavigationWindow();

            EndHorizontal();

            DrawHelpBox(
                "No NavMeshSurface in scene. Create one on the terrain, " +
                "or open Agent Settings to configure before creating.",
                MessageType.Info);
        }
        else
        {
            BeginHorizontal();

            if (DrawButton("▶  Bake NavMesh", new Color(0.3f, 0.8f, 0.4f), 30))
                BakeNavMeshSurface(surface);

            if (DrawButton("✕  Clear", new Color(1f, 0.4f, 0.35f), 30, 64))
                ClearNavMeshSurface(surface);

            EndHorizontal();

            BeginHorizontal();

            if (DrawButton("⚙  Agent Settings", new Color(0.65f, 0.75f, 1f), 24))
                OpenNavigationWindow();

            if (DrawButton("Select Surface", 24))
                SelectObject(surface.gameObject);

            EndHorizontal();
        }
    }

    // 3. ProBuilder Mesh - convert / load / edit / remove

    private void DrawProBuilderSection(NavMeshSurface surface, ProBuilderMesh pbMesh, bool hasNavData)
    {
        DrawHeader("ProBuilder Mesh");

        if (pbMesh == null)
        {
            BeginHorizontal();

            GUI.enabled = hasNavData;
            if (DrawButton("NavMesh → ProBuilder", new Color(0.3f, 0.75f, 0.4f), 28))
                ConvertNavMeshToProBuilder();
            GUI.enabled = true;

            bool hasObjFile = File.Exists(GetOutputPath());
            GUI.enabled = hasObjFile;
            if (DrawButton("Load OBJ → ProBuilder", new Color(0.4f, 0.65f, 1f), 28))
                LoadObjAsProBuilder();
            GUI.enabled = true;

            EndHorizontal();

            if (!hasNavData)
                DrawHelpBox("Bake the NavMesh first, then convert to ProBuilder for editing.", MessageType.None);
        }
        else
        {
            DrawProBuilderEditModeRow(pbMesh);

            DrawSpace(2);

            BeginHorizontal();

            // Rebuild: re-bake navmesh into a fresh PB mesh (replaces current)
            GUI.enabled = hasNavData;
            if (DrawButton("↻  Rebuild from NavMesh", new Color(0.55f, 0.7f, 1f), 24))
                ConvertNavMeshToProBuilder();
            GUI.enabled = true;

            if (DrawButton("✕  Remove", new Color(1f, 0.4f, 0.35f), 24, 70))
                RemovePbMesh();

            EndHorizontal();
        }
    }

    private void DrawProBuilderEditModeRow(ProBuilderMesh pbMesh)
    {
        if (pbMesh == null) return;

        bool isInEditMode = ProBuilderEditor.instance != null
                            && Selection.activeGameObject == pbMesh.gameObject;

        Color editColor = isInEditMode
            ? new Color(1f, 0.55f, 0.3f)
            : new Color(0.5f, 0.8f, 1f);

        string editLabel = isInEditMode
            ? "✎  Exit Edit Mode"
            : "✎  Enter ProBuilder Edit Mode";

        if (DrawButton(editLabel, editColor, 28))
        {
            if (isInEditMode)
            {
                Selection.activeGameObject = null;
                ProBuilderEditor.selectMode = SelectMode.Vertex;
            }
            else
            {
                Selection.activeGameObject = pbMesh.gameObject;
                ProBuilderEditor.selectMode = SelectMode.Face;
                EditorApplication.ExecuteMenuItem("Tools/ProBuilder/ProBuilder Window");
            }
        }
    }

    // 4. Export - single smart section

    private void DrawExportSection(ProBuilderMesh pbMesh)
    {
        DrawHeader("Export");

        bool canExport = manager.Module != null && !string.IsNullOrEmpty(manager.SceneName);

        if (!canExport)
        {
            DrawHelpBox("Assign Module & Scene Name (General tab) to enable export.", MessageType.Warning);
            return;
        }

        string outputPath = GetOutputPath();
        DrawDisabledField("Output", outputPath);

        DrawSpace(2);

        bool hasPbMesh = pbMesh != null;

        if (hasPbMesh)
        {
            // Primary: export from ProBuilder mesh (user edited it)
            if (DrawButton("▶  Export ProBuilder → ai_mesh.obj", new Color(0.3f, 0.8f, 0.4f), 32))
                ExportProBuilderMesh(pbMesh);

            DrawMiniLabel("Exports the current ProBuilder mesh with Y→Z conversion.");
        }

        // Secondary: direct export from NavMesh (skip ProBuilder)
        NavMeshTriangulation tri = NavMesh.CalculateTriangulation();
        bool hasNavData = tri.vertices != null && tri.vertices.Length > 0;

        GUI.enabled = hasNavData;
        string directLabel = hasPbMesh
            ? "Export NavMesh Directly (skip ProBuilder)"
            : "▶  Export NavMesh → ai_mesh.obj";

        float directHeight = hasPbMesh ? 24 : 32;
        Color directColor  = hasPbMesh
            ? new Color(0.55f, 0.7f, 1f)
            : new Color(0.3f, 0.8f, 0.4f);

        if (DrawButton(directLabel, directColor, directHeight))
            QuickBakeAndExport();
        GUI.enabled = true;

        DrawSpace(2);

        if (Directory.Exists(manager.EditorScoSceneDataPath))
        {
            if (DrawButton("Open ScoData Folder", 22))
                EditorUtility.RevealInFinder(manager.EditorScoSceneDataPath);
        }
    }

    // 5. Settings - foldout: area filter + conversion options

    private void DrawSettingsSection()
    {
        _showSettings = EditorGUILayout.Foldout(_showSettings, "Settings", true);
        if (!_showSettings) return;

        EditorGUI.indentLevel++;

        // Area filter
        BeginBox();
        DrawMiniBoldLabel("NavMesh Area Filter");
        DrawSpace(2);

        for (int i = 0; i < AreaNames.Length; i++)
        {
            int  bit   = NavMesh.GetAreaFromName(AreaNames[i]);
            bool wasOn = (_areaFilter & (1 << bit)) != 0;
            bool isOn  = EditorGUILayout.Toggle(AreaNames[i], wasOn);
            if (isOn != wasOn)
            {
                if (isOn) _areaFilter |=  (1 << bit);
                else      _areaFilter &= ~(1 << bit);
            }
        }
        EndBox();

        DrawSpace(2);

        // Conversion options
        BeginBox();
        DrawMiniBoldLabel("Export Options");
        DrawSpace(2);

        _swapYZ = EditorGUILayout.Toggle(
            new GUIContent("Y → Z Axis Conversion",
                           "Export: Unity (x,y,z) → M&B (x,z,y) with winding fix.\n" +
                           "Load:   M&B (x,z,y) → Unity (x,y,z) with winding fix.\n" +
                           "Should almost always be ON."),
            _swapYZ);

        _flipFaceWinding = EditorGUILayout.Toggle(
            new GUIContent("Flip Face Winding (extra)",
                           "Additional winding reversal.\n" +
                           "Enable if M&B renders faces inside-out."),
            _flipFaceWinding);
        EndBox();

        EditorGUI.indentLevel--;
    }

    // Status

    private void DrawStatusDisplay()
    {
        if (!string.IsNullOrEmpty(_status))
            DrawHelpBox(_status, _statusType);
    }

    // Actions - NavMesh Surface

    private void CreateNavMeshSurface()
    {
        GameObject target = manager.Terrain != null
            ? manager.Terrain.gameObject
            : manager.gameObject;

        var existing = target.GetComponent<NavMeshSurface>();
        if (existing != null)
        {
            SetStatus($"NavMeshSurface already exists on '{target.name}'.", MessageType.Warning);
            SelectObject(target);
            return;
        }

        Undo.RecordObject(target, "Create NavMesh Surface");
        var surface = Undo.AddComponent<NavMeshSurface>(target);

        surface.collectObjects = CollectObjects.All;
        surface.useGeometry    = NavMeshCollectGeometry.RenderMeshes;

        SaveNavMeshAssetToSceneFolder(surface);
        SelectObject(target);
        SetStatus($"Created NavMeshSurface on '{target.name}'. Configure agent settings, then bake.",
                  MessageType.Info);
    }

    private void BakeNavMeshSurface(NavMeshSurface surface)
    {
        if (surface == null) { SetStatus("No NavMeshSurface to bake.", MessageType.Warning); return; }

        EditorUtility.DisplayProgressBar("Baking NavMesh", "Building NavMesh Surface…", 0.5f);

        try
        {
            surface.BuildNavMesh();
            SaveNavMeshAssetToSceneFolder(surface);

            NavMeshTriangulation tri = NavMesh.CalculateTriangulation();
            int vc = tri.vertices?.Length ?? 0;
            int tc = tri.indices != null ? tri.indices.Length / 3 : 0;

            EditorUtility.ClearProgressBar();
            SetStatus($"NavMesh baked - {vc:N0} vertices, {tc:N0} triangles.", MessageType.Info);
            EditorUtility.SetDirty(surface);
        }
        catch (System.Exception ex)
        {
            EditorUtility.ClearProgressBar();
            SetStatus($"NavMesh bake failed: {ex.Message}", MessageType.Error);
            Debug.LogError($"[AiMeshExportTab] NavMesh bake error: {ex}");
        }
    }

    private void ClearNavMeshSurface(NavMeshSurface surface)
    {
        if (surface == null) return;
        Undo.RecordObject(surface, "Clear NavMesh Surface");
        surface.RemoveData();
        EditorUtility.SetDirty(surface);
        SetStatus("NavMesh data cleared.", MessageType.Info);
    }

    // Actions - ProBuilder mesh

    private void ConvertNavMeshToProBuilder()
    {
        _status = string.Empty;
        if (!TryTriangulateNavMesh(out var positions, out var pbFaces)) return;

        SpawnProBuilderMesh(positions, pbFaces,
            $"NavMesh → ProBuilder ({positions.Count:N0} verts, {pbFaces.Count:N0} tris).\n" +
            "Edit with ProBuilder tools, then export.");
    }

    private void BakeSurfaceThenProBuilder(NavMeshSurface surface)
    {
        _status = string.Empty;
        BakeNavMeshSurface(surface);

        NavMeshTriangulation tri = NavMesh.CalculateTriangulation();
        if (tri.vertices == null || tri.vertices.Length == 0)
        {
            SetStatus("Surface bake produced no NavMesh data - cannot convert.", MessageType.Warning);
            return;
        }

        if (!TryTriangulateNavMesh(out var positions, out var pbFaces)) return;

        SpawnProBuilderMesh(positions, pbFaces,
            $"⚡ Bake & Convert complete ({positions.Count:N0} verts, {pbFaces.Count:N0} tris).\n" +
            "Edit with ProBuilder tools, then export.");
    }

    private void LoadObjAsProBuilder()
    {
        _status = string.Empty;

        string path = GetOutputPath();
        if (!File.Exists(path))
        {
            SetStatus("ai_mesh.obj not found. Export one first.", MessageType.Warning);
            return;
        }

        EditorUtility.DisplayProgressBar("Loading OBJ", "Parsing ai_mesh.obj…", 0.3f);

        bool ok = SimpleObjParser.TryParse(path, out var positions, out var faces,
                                            swapYZ: _swapYZ, out string error);

        EditorUtility.ClearProgressBar();

        if (!ok)
        {
            SetStatus($"OBJ load failed: {error}", MessageType.Error);
            return;
        }

        SpawnProBuilderMesh(positions, faces,
            $"Loaded ai_mesh.obj → ProBuilder ({positions.Count:N0} verts, {faces.Count:N0} tris).");
    }

    private void SpawnProBuilderMesh(List<Vector3> positions, List<Face> faces, string successMessage)
    {
        RemovePbMesh();
        EditorUtility.DisplayProgressBar("Building ProBuilder Mesh", "Creating mesh…", 0.6f);

        try
        {
            ProBuilderMesh pb = ProBuilderMesh.Create(positions, faces);
            pb.gameObject.name = PbMeshName;
            pb.gameObject.transform.SetParent(manager.transform);

            var mr = pb.GetComponent<MeshRenderer>();
            if (mr != null)
                mr.sharedMaterial = BuildPreviewMat(new Color(0.1f, 0.85f, 0.35f, 0.4f));

            Undo.RegisterCreatedObjectUndo(pb.gameObject, $"Create {PbMeshName}");
            Selection.activeGameObject = pb.gameObject;

            EditorUtility.ClearProgressBar();
            SetStatus(successMessage, MessageType.Info);
        }
        catch (System.Exception ex)
        {
            EditorUtility.ClearProgressBar();
            SetStatus($"ProBuilder mesh creation failed: {ex.Message}", MessageType.Error);
            Debug.LogError($"[AiMeshExportTab] {ex}");
        }
    }

    private void RemovePbMesh()
    {
        var go = GameObject.Find(PbMeshName);
        if (go != null)
            Undo.DestroyObjectImmediate(go);
    }

    // Actions - Export

    private void ExportProBuilderMesh(ProBuilderMesh pbMesh)
    {
        if (pbMesh == null) { SetStatus("No ProBuilder AI mesh in scene.", MessageType.Warning); return; }

        var mf = pbMesh.GetComponent<MeshFilter>();
        if (mf == null || mf.sharedMesh == null)
        {
            SetStatus("ProBuilder mesh has no MeshFilter / compiled mesh.", MessageType.Error);
            return;
        }

        ExportMeshToObj(mf.sharedMesh, mf.sharedMesh.vertexCount, mf.sharedMesh.triangles.Length / 3);
    }

    private void QuickBakeAndExport()
    {
        _status = string.Empty;
        var mesh = TriangulateToMesh();
        if (mesh == null) return;
        ExportMeshToObj(mesh, mesh.vertexCount, mesh.triangles.Length / 3);
    }

    private void ExportMeshToObj(Mesh mesh, int vertCount, int triCount)
    {
        string outputPath = GetOutputPath();

        try
        {
            EditorUtility.DisplayProgressBar("Exporting AI Mesh", "Writing ai_mesh.obj…", 0.6f);
            ObjExporter.Export(mesh, outputPath, "ai_mesh", swapYZ: _swapYZ);
            EditorUtility.ClearProgressBar();

            string note = _swapYZ ? " [Y→Z]" : "";
            SetStatus($"Exported{note} → {outputPath}\n{vertCount:N0} verts, {triCount:N0} tris.",
                      MessageType.Info);
            Debug.Log($"[AiMeshExportTab] Exported ai_mesh.obj to: {outputPath}");
        }
        catch (System.Exception ex)
        {
            EditorUtility.ClearProgressBar();
            SetStatus($"Export failed: {ex.Message}", MessageType.Error);
            Debug.LogError($"[AiMeshExportTab] Export error: {ex}");
        }
    }

    // Shared triangulation

    private bool TryTriangulateNavMesh(out List<Vector3> positions, out List<Face> pbFaces)
    {
        positions = new List<Vector3>();
        pbFaces   = new List<Face>();

        NavMeshTriangulation tri = NavMesh.CalculateTriangulation();

        if (tri.vertices == null || tri.vertices.Length == 0)
        {
            SetStatus("No NavMesh data. Bake one first.", MessageType.Warning);
            return false;
        }

        int[] areas = tri.areas;

        for (int t = 0; t < tri.indices.Length / 3; t++)
        {
            if ((_areaFilter & (1 << areas[t])) == 0) continue;

            int baseIdx = positions.Count;
            positions.Add(tri.vertices[tri.indices[t * 3]]);
            positions.Add(tri.vertices[tri.indices[t * 3 + 1]]);
            positions.Add(tri.vertices[tri.indices[t * 3 + 2]]);

            pbFaces.Add(_flipFaceWinding
                ? new Face(new[] { baseIdx + 2, baseIdx + 1, baseIdx })
                : new Face(new[] { baseIdx, baseIdx + 1, baseIdx + 2 }));
        }

        if (positions.Count == 0)
        {
            SetStatus("Area filter produced zero triangles. Check settings.", MessageType.Warning);
            return false;
        }

        return true;
    }

    private Mesh TriangulateToMesh()
    {
        NavMeshTriangulation tri = NavMesh.CalculateTriangulation();

        if (tri.vertices == null || tri.vertices.Length == 0)
        {
            SetStatus("No NavMesh data. Bake one first.", MessageType.Warning);
            return null;
        }

        var verts = new List<Vector3>();
        var tris  = new List<int>();
        int[] areas = tri.areas;

        for (int t = 0; t < tri.indices.Length / 3; t++)
        {
            if ((_areaFilter & (1 << areas[t])) == 0) continue;

            int baseIdx = verts.Count;
            verts.Add(tri.vertices[tri.indices[t * 3]]);
            verts.Add(tri.vertices[tri.indices[t * 3 + 1]]);
            verts.Add(tri.vertices[tri.indices[t * 3 + 2]]);

            if (_flipFaceWinding)
            { tris.Add(baseIdx + 2); tris.Add(baseIdx + 1); tris.Add(baseIdx); }
            else
            { tris.Add(baseIdx); tris.Add(baseIdx + 1); tris.Add(baseIdx + 2); }
        }

        if (verts.Count == 0)
        {
            SetStatus("Area filter produced zero triangles. Check settings.", MessageType.Warning);
            return null;
        }

        var mesh = new Mesh
        {
            name        = "ai_mesh",
            indexFormat = verts.Count > 65535
                ? UnityEngine.Rendering.IndexFormat.UInt32
                : UnityEngine.Rendering.IndexFormat.UInt16
        };
        mesh.SetVertices(verts);
        mesh.SetTriangles(tris, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }

    // NavMesh asset save

    private void SaveNavMeshAssetToSceneFolder(NavMeshSurface surface)
    {
        string scenePath = SceneManager.GetActiveScene().path;
        if (string.IsNullOrEmpty(scenePath)) return;

        string sceneDir  = Path.GetDirectoryName(scenePath);
        string sceneName = Path.GetFileNameWithoutExtension(scenePath);
        string navFolder = Path.Combine(sceneDir, sceneName);

        if (!AssetDatabase.IsValidFolder(navFolder))
        {
            string parent = sceneDir.Replace("\\", "/");
            AssetDatabase.CreateFolder(parent, sceneName);
        }

        string assetPath = Path.Combine(navFolder, "NavMesh.asset").Replace("\\", "/");

        if (surface.navMeshData != null)
        {
            var existing = AssetDatabase.LoadAssetAtPath<NavMeshData>(assetPath);
            if (existing != null && existing != surface.navMeshData)
            {
                EditorUtility.CopySerialized(surface.navMeshData, existing);
                surface.navMeshData = existing;
                AssetDatabase.SaveAssets();
            }
            else if (existing == null)
            {
                AssetDatabase.CreateAsset(surface.navMeshData, assetPath);
                AssetDatabase.SaveAssets();
            }

            Debug.Log($"[AiMeshExportTab] NavMesh asset saved to: {assetPath}");
        }
    }

    // Utility helpers

    private string GetOutputPath() =>
        Path.Combine(manager.EditorScoSceneDataPath, "ai_mesh.obj");

    private void SetStatus(string msg, MessageType type)
    {
        _status     = msg;
        _statusType = type;
    }

    private static NavMeshSurface FindSceneNavMeshSurface() =>
        Object.FindObjectOfType<NavMeshSurface>();

    private static ProBuilderMesh FindScenePbMesh()
    {
        var go = GameObject.Find(PbMeshName);
        return go != null ? go.GetComponent<ProBuilderMesh>() : null;
    }

    private static void OpenNavigationWindow() =>
        EditorApplication.ExecuteMenuItem("Window/AI/Navigation");

    private static void SelectObject(GameObject go)
    {
        Selection.activeGameObject = go;
        EditorGUIUtility.PingObject(go);
    }

    private static string GetAgentTypeName(int agentTypeID)
    {
        int count = NavMesh.GetSettingsCount();
        for (int i = 0; i < count; i++)
        {
            NavMeshBuildSettings settings = NavMesh.GetSettingsByIndex(i);
            if (settings.agentTypeID == agentTypeID)
            {
                string name = NavMesh.GetSettingsNameFromID(agentTypeID);
                return string.IsNullOrEmpty(name) ? "Humanoid" : name;
            }
        }
        return "Humanoid";
    }


    private enum StatusColor { Good, Warn }

    private void DrawColorLabel(string text, StatusColor color)
    {
        var style = new GUIStyle(EditorStyles.miniLabel)
        {
            normal =
            {
                textColor = color == StatusColor.Good
                    ? new Color(0.3f, 0.9f, 0.4f)
                    : new Color(1f, 0.6f, 0.3f)
            }
        };
        EditorGUILayout.LabelField(text, style);
    }

    private static Material BuildPreviewMat(Color color)
    {
        var mat = new Material(Shader.Find("Standard") ?? Shader.Find("Unlit/Color"))
        {
            name  = "AiMesh_PB_Preview",
            color = color
        };
        mat.SetFloat("_Mode", 3);
        mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        mat.SetInt("_ZWrite", 0);
        mat.EnableKeyword("_ALPHABLEND_ON");
        mat.renderQueue = 3000;
        return mat;
    }
}
