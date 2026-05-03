using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEditor;

/// <summary>
/// Export tab: Splatmaps, Elevation, Props, Tint, and SCO repack operations.
/// Consolidates all export functionality into a single organized tab.
/// </summary>
public class ExportTab : SceneDataManagerTabBase
{
    public override string TabName => "Export";
    public override int Order => 999;

    // Subtab enum for internal navigation - added Tint
    private enum ExportSubTab
    {
        Splatmaps,
        Elevation,
        Tint,
        Props,
        SCO
    }

    private ExportSubTab currentSubTab = ExportSubTab.Splatmaps;

    // Serialized properties
    private SerializedProperty splatmapSettingsProp;
    private SerializedProperty elevationSettingsProp;
    private SerializedProperty propsSettingsProp;
    private SerializedProperty scoSettingsProp;

    // Foldout states
    private bool showExportLog = false;
    private bool showElevationDebug = false;
    private bool showSCOSettings = true;

    public override void OnEnable(MBSceneDataManager manager, SerializedObject serializedObject)
    {
        base.OnEnable(manager, serializedObject);

        splatmapSettingsProp = serializedObject.FindProperty("splatmapSettings");
        elevationSettingsProp = serializedObject.FindProperty("elevationSettings");
        propsSettingsProp = serializedObject.FindProperty("propsSettings");
        scoSettingsProp = serializedObject.FindProperty("scoSettings");
    }

    public override void DrawTab()
    {
        // Export All button at top
        DrawExportAllSection();

        EditorGUILayout.Space(5);
        DrawUILine(Color.grey);

        // Sub-tab bar
        DrawSubTabBar();
        EditorGUILayout.Space(10);

        // Draw current sub-tab content
        switch (currentSubTab)
        {
            case ExportSubTab.Splatmaps:
                DrawSplatmapsSection();
                break;
            case ExportSubTab.Elevation:
                DrawElevationSection();
                break;
            case ExportSubTab.Tint:
                DrawTintSection();
                break;
            case ExportSubTab.Props:
                DrawPropsSection();
                break;
            case ExportSubTab.SCO:
                DrawSCOSection();
                break;
        }
    }

    private void DrawExportAllSection()
    {
        GUI.backgroundColor = new Color(0.4f, 0.9f, 0.4f);
        GUI.enabled = manager.Terrain != null && manager.Module != null;

        if (GUILayout.Button("Export All Scene Data", GUILayout.Height(35)))
        {
            var result = manager.ExportAll();

            // Also export tint if available
            int tintFiles = 0;
            if (manager.TintData != null && manager.TintData.HasLayers)
            {
                var tintResult = ExportTintPPM();
                if (tintResult.success)
                    tintFiles = tintResult.exportedFiles?.Count ?? 0;
            }

            int totalFiles = result.TotalFileCount + tintFiles;
            EditorUtility.DisplayDialog(result.success ? "Success" : "Partial/Failed",
                $"Exported {totalFiles} files to:\n{manager.EditorScoSceneDataPath}", "OK");
        }

        GUI.enabled = true;
        GUI.backgroundColor = Color.white;
    }

    private void DrawSubTabBar()
    {
        EditorGUILayout.BeginHorizontal();

        // Updated to include Tint
        string[] subTabNames = { "Splatmaps", "Elevation", "Tint", "Props", "SCO" };

        for (int i = 0; i < subTabNames.Length; i++)
        {
            // Color coding by type
            Color tabColor = Color.white;
            if ((int)currentSubTab == i)
            {
                tabColor = new Color(0.7f, 0.85f, 0.7f); // Active
            }

            GUI.backgroundColor = tabColor;

            if (GUILayout.Button(subTabNames[i], EditorStyles.miniButton, GUILayout.Height(20)))
            {
                currentSubTab = (ExportSubTab)i;
            }
        }

        GUI.backgroundColor = Color.white;
        EditorGUILayout.EndHorizontal();
    }

    #region Splatmaps

    private void DrawSplatmapsSection()
    {
        DrawHeader("Splatmap Export");

        if (manager.Terrain == null)
        {
            EditorGUILayout.HelpBox("Assign terrain in General tab.", MessageType.Warning);
            return;
        }

        // Settings
        BeginBox();

        EditorGUILayout.PropertyField(splatmapSettingsProp.FindPropertyRelative("blendMode"));
        EditorGUILayout.PropertyField(splatmapSettingsProp.FindPropertyRelative("resolutionMode"));

        var resMode = (MBExportHelpers.ResolutionMode)splatmapSettingsProp
            .FindPropertyRelative("resolutionMode").enumValueIndex;

        if (resMode == MBExportHelpers.ResolutionMode.Custom)
        {
            EditorGUI.indentLevel++;
            EditorGUILayout.PropertyField(splatmapSettingsProp.FindPropertyRelative("customWidth"));
            EditorGUILayout.PropertyField(splatmapSettingsProp.FindPropertyRelative("customHeight"));
            EditorGUI.indentLevel--;
        }

        EditorGUILayout.PropertyField(splatmapSettingsProp.FindPropertyRelative("applyEdgeFeathering"));
        if (splatmapSettingsProp.FindPropertyRelative("applyEdgeFeathering").boolValue)
        {
            EditorGUI.indentLevel++;
            EditorGUILayout.PropertyField(splatmapSettingsProp.FindPropertyRelative("featherRadius"));
            EditorGUILayout.PropertyField(splatmapSettingsProp.FindPropertyRelative("featherValue"));
            EditorGUI.indentLevel--;
        }

        EndBox();

        EditorGUILayout.Space(10);

        // Export buttons
        EditorGUILayout.BeginHorizontal();

        GUI.enabled = manager.Module != null;

        if (DrawButton("Export Splatmaps", new Color(0.4f, 0.8f, 0.4f), 30))
        {
            var result = manager.ExportSplatmaps();
            EditorUtility.DisplayDialog(result.success ? "Success" : "Failed", result.message, "OK");
        }

        if (DrawButton("Export To...", new Color(0.7f, 0.7f, 1f), 30))
        {
            string folder = EditorUtility.OpenFolderPanel("Export To", manager.SplatmapsExportPath, "");
            if (!string.IsNullOrEmpty(folder))
            {
                var result = manager.ExportSplatmaps(folder);
                EditorUtility.DisplayDialog(result.success ? "Success" : "Failed", result.message, "OK");
            }
        }

        GUI.enabled = true;

        EditorGUILayout.EndHorizontal();
    }

    #endregion

    #region Elevation

    private void DrawElevationSection()
    {
        DrawHeader("Elevation Export");

        if (manager.HeightmapGenerator == null)
        {
            EditorGUILayout.HelpBox(
                "LayeredHeightmapGenerator not found.\n\n" +
                "Required for elevation export.", MessageType.Warning);
            return;
        }

        BeginBox();

        EditorGUILayout.PropertyField(elevationSettingsProp.FindPropertyRelative("resolutionMode"));

        var resMode = (MBExportHelpers.ResolutionMode)elevationSettingsProp
            .FindPropertyRelative("resolutionMode").enumValueIndex;

        if (resMode == MBExportHelpers.ResolutionMode.Custom)
        {
            EditorGUI.indentLevel++;
            EditorGUILayout.PropertyField(elevationSettingsProp.FindPropertyRelative("customWidth"));
            EditorGUILayout.PropertyField(elevationSettingsProp.FindPropertyRelative("customHeight"));
            EditorGUI.indentLevel--;
        }

        EditorGUILayout.Space(5);
        DrawResolutionPreview(resMode);

        EndBox();

        EditorGUILayout.Space(5);

        // Terrain metrics
        DrawHeader("Terrain Metrics");
        BeginBox();

        EditorGUI.BeginDisabledGroup(true);
        EditorGUILayout.FloatField("Base Height (m)", manager.HeightmapGenerator.BaseTerrainHeight);
        EditorGUILayout.FloatField("Base Offset (m)", manager.HeightmapGenerator.BaseTerrainOffset);
        EditorGUILayout.Space(2);
        EditorGUILayout.FloatField("Final Height (m)", manager.HeightmapGenerator.TerrainHeight);
        EditorGUILayout.FloatField("Final Offset (m)", manager.HeightmapGenerator.TerrainOffset);
        EditorGUI.EndDisabledGroup();

        EndBox();

        EditorGUILayout.Space(10);

        // Export buttons
        EditorGUILayout.BeginHorizontal();

        GUI.enabled = manager.Module != null;

        if (DrawButton("Export Elevation", new Color(0.4f, 0.8f, 0.4f), 30))
        {
            var result = manager.ExportElevation();
            EditorUtility.DisplayDialog(result.success ? "Success" : "Failed", result.message, "OK");
            AssetDatabase.Refresh();
        }

        if (DrawButton("Save As...", new Color(0.7f, 0.7f, 1f), 30))
        {
            string path = EditorUtility.SaveFilePanel("Export Elevation",
                Path.GetDirectoryName(manager.ElevationExportPath), "layer_ground_elevation.pfm", "pfm");
            if (!string.IsNullOrEmpty(path))
            {
                var result = manager.ExportElevation(path);
                EditorUtility.DisplayDialog(result.success ? "Success" : "Failed", result.message, "OK");
                AssetDatabase.Refresh();
            }
        }

        GUI.enabled = true;

        EditorGUILayout.EndHorizontal();
    }

    private void DrawResolutionPreview(MBExportHelpers.ResolutionMode resMode)
    {
        string previewText = "";

        switch (resMode)
        {
            case MBExportHelpers.ResolutionMode.Native:
                int nativeRes = manager.Terrain?.terrainData?.heightmapResolution ?? 0;
                previewText = $"Will export at: {nativeRes} x {nativeRes} (Unity heightmap resolution)";
                break;

            case MBExportHelpers.ResolutionMode.Custom:
                var customW = elevationSettingsProp.FindPropertyRelative("customWidth").intValue;
                var customH = elevationSettingsProp.FindPropertyRelative("customHeight").intValue;
                previewText = $"Will export at: {customW} x {customH} (custom)";
                break;

            case MBExportHelpers.ResolutionMode.MatchGenerator:
                if (manager.HeightmapGenerator != null &&
                    !string.IsNullOrEmpty(manager.HeightmapGenerator.CurrentTerrainHash))
                {
                    var data = MBTerrainGeneratorHelpers.ParseTerrainCode(
                        manager.HeightmapGenerator.CurrentTerrainHash);
                    if (data != null)
                    {
                        var geoData = MBTerrainGeneratorHelpers.CalculateTerrainGeometryData(
                            data.SizeX, data.SizeY, data.PolygonSize);
                        previewText = $"Will export at: {geoData.NumVerticesX} x {geoData.NumVerticesY} (from hash)";
                    }
                }
                else
                {
                    previewText = "Will export at: (no hash available)";
                }

                break;

            case MBExportHelpers.ResolutionMode.MatchOriginalPFM:
                string pfmPath = manager.GetOriginalElevationPath();
                if (!string.IsNullOrEmpty(pfmPath))
                {
                    string fullPath = Path.GetFullPath(pfmPath);
                    if (File.Exists(fullPath))
                    {
                        var (w, h) = PfmHelper.GetDimensions(fullPath);
                        previewText = $"Will export at: {w} x {h} (original PFM)";
                    }
                    else
                    {
                        previewText = "Will export at: (original PFM not found)";
                    }
                }
                else
                {
                    previewText = "Will export at: (no original PFM path)";
                }

                break;

            case MBExportHelpers.ResolutionMode.MatchSplatmaps:
                string splatmapFolder = manager.EditorScoSceneDataPath;
                if (Directory.Exists(splatmapFolder))
                {
                    string[] pgmFiles = Directory.GetFiles(splatmapFolder, "layer_*.pgm");
                    if (pgmFiles.Length > 0)
                    {
                        var (w, h) = MBExportHelpers.ReadPgmDimensions(pgmFiles[0]);
                        previewText = $"Will export at: {w} x {h} (from splatmaps)";
                    }
                    else
                    {
                        previewText = "Will export at: (no splatmaps found)";
                    }
                }
                else
                {
                    previewText = "Will export at: (ScoData folder not found)";
                }

                break;
        }

        EditorGUILayout.LabelField(previewText, EditorStyles.miniLabel);
    }

    #endregion

    #region Tint Export

    private void DrawTintSection()
    {
        DrawHeader("Tint Export (PPM)");

        if (manager.TintData == null)
        {
            EditorGUILayout.HelpBox(
                "No TerrainTintData component found.\n\n" +
                "Add it via the Tint tab to enable tint painting and export.",
                MessageType.Info);

            // Quick add button
            GUI.backgroundColor = new Color(0.6f, 0.9f, 0.6f);
            if (GUILayout.Button("Open Tint Tab", GUILayout.Height(25)))
            {
                // This would need to trigger tab switch in parent - for now just show message
                EditorUtility.DisplayDialog("Navigate to Tint Tab",
                    "Switch to the 'Tint' tab to add TerrainTintData and configure tint layers.", "OK");
            }

            GUI.backgroundColor = Color.white;
            return;
        }

        var tintData = manager.TintData;

        // Output info
        BeginBox();
        EditorGUILayout.LabelField("Output", EditorStyles.boldLabel);

        // Current baked texture
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("Baked PNG:", GUILayout.Width(80));
        EditorGUI.BeginDisabledGroup(true);
        EditorGUILayout.ObjectField(tintData.bakedTintMap, typeof(Texture2D), false);
        EditorGUI.EndDisabledGroup();
        EditorGUILayout.EndHorizontal();

        // Resolution info
        Vector2Int outputSize = GetTintOutputSize(tintData);
        EditorGUILayout.LabelField($"Output Size: {outputSize.x}×{outputSize.y}", EditorStyles.miniLabel);

        EndBox();

        EditorGUILayout.Space(10);

        // Export buttons
        EditorGUILayout.BeginHorizontal();

        GUI.enabled = tintData.HasLayers;

        // Main export button
        GUI.backgroundColor = new Color(1f, 0.85f, 0.5f);
        if (GUILayout.Button("Export Tint PPM", GUILayout.Height(30)))
        {
            var result = ExportTintPPM();
            EditorUtility.DisplayDialog(result.success ? "Success" : "Failed", result.message, "OK");
        }

        // Save As button
        GUI.backgroundColor = new Color(0.7f, 0.7f, 1f);
        if (GUILayout.Button("Save As...", GUILayout.Height(30)))
        {
            string defaultDir = Path.GetDirectoryName(manager.TintExportPath);
            string defaultName = Path.GetFileName(manager.TintExportPath);
            string path = EditorUtility.SaveFilePanel("Export Tint PPM", defaultDir, defaultName, "ppm");

            if (!string.IsNullOrEmpty(path))
            {
                var result = ExportTintPPM(path);
                EditorUtility.DisplayDialog(result.success ? "Success" : "Failed", result.message, "OK");
            }
        }

        GUI.backgroundColor = Color.white;
        GUI.enabled = true;

        EditorGUILayout.EndHorizontal();

        // Help text
        if (!tintData.HasLayers)
        {
            EditorGUILayout.HelpBox(
                "Add and activate tint layers in the Tint tab to enable export.",
                MessageType.Info);
        }
    }

    private string GetLayerTypeIcon(TerrainTintData.LayerType type)
    {
        switch (type)
        {
            case TerrainTintData.LayerType.Paint: return "🖌";
            case TerrainTintData.LayerType.Texture: return "🖼";
            case TerrainTintData.LayerType.AO: return "◐";
            case TerrainTintData.LayerType.PPM: return "📄";
            default: return "?";
        }
    }

    /// <summary>
    /// Export tint layers to PPM file for M&B
    /// </summary>
    private MBExportHelpers.ExportResult ExportTintPPM(string outputPath = null)
    {
        var tintData = manager.TintData;

        if (tintData == null)
            return MBExportHelpers.ExportResult.Failure("No TerrainTintData found");

        if (!tintData.HasLayers)
            return MBExportHelpers.ExportResult.Failure("No tint layers to export");

        outputPath ??= manager.TintExportPath;

        try
        {
            EditorUtility.DisplayProgressBar("Exporting Tint PPM", "Compositing layers...", 0.3f);

            // Composite all layers to a single texture
            Texture2D baked = CompositeTintLayers(tintData);
            if (baked == null)
            {
                EditorUtility.ClearProgressBar();
                return MBExportHelpers.ExportResult.Failure("Failed to composite tint layers");
            }

            EditorUtility.DisplayProgressBar("Exporting Tint PPM", "Converting for M&B...", 0.6f);

            int width = baked.width;
            int height = baked.height;

            // Convert Unity texture to M&B PPM format (handles coordinate system + gamma)
            Color[,] ppmData = PpmHelper.TextureToMBTerrainPpm(baked, convertToSRGB: false);
            UnityEngine.Object.DestroyImmediate(baked);

            EditorUtility.DisplayProgressBar("Exporting Tint PPM", "Writing file...", 0.9f);

            // Ensure directory exists
            string dir = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            // Write PPM (binary P6 format)
            PpmHelper.WritePpm(ppmData, outputPath);

            EditorUtility.ClearProgressBar();

            Debug.Log($"[ExportTab] Tint exported to: {outputPath} ({width}×{height})");

            return new MBExportHelpers.ExportResult
            {
                success = true,
                message = $"Exported tint to:\n{outputPath}\n\nSize: {width}×{height}",
                exportedFiles = new List<string> { outputPath }
            };
        }
        catch (Exception e)
        {
            EditorUtility.ClearProgressBar();
            Debug.LogError($"[ExportTab] Tint PPM export failed: {e}");
            return MBExportHelpers.ExportResult.Failure($"PPM export failed: {e.Message}", e);
        }
    }


    /// <summary>
    /// Composite all active tint layers into a single texture
    /// </summary>
    private Texture2D CompositeTintLayers(TerrainTintData tintData)
    {
        Vector2Int size = GetTintOutputSize(tintData);
        if (size.x == 0 || size.y == 0)
            return null;

        var result = new Texture2D(size.x, size.y, TextureFormat.RGB24, false);

        // Start with white (no tint)
        Color[] pixels = new Color[size.x * size.y];
        for (int i = 0; i < pixels.Length; i++)
            pixels[i] = Color.white;

        // Composite each active layer (painter's algorithm - bottom to top)
        foreach (var layer in tintData.layers)
        {
            var layerTex = layer.EffectiveTexture;
            if (!layer.active || layer.opacity <= 0f || layerTex == null)
                continue;

            for (int y = 0; y < size.y; y++)
            {
                float v = (float)y / (size.y - 1);
                for (int x = 0; x < size.x; x++)
                {
                    float u = (float)x / (size.x - 1);
                    int idx = y * size.x + x;

                    Color layerColor = layerTex.GetPixelBilinear(u, v);

                    // Skip pure white (no tint effect)
                    if (layerColor.r >= 0.999f && layerColor.g >= 0.999f && layerColor.b >= 0.999f)
                        continue;

                    // Apply layer opacity
                    layerColor = Color.Lerp(Color.white, layerColor, layer.opacity);

                    // Apply blend mode
                    pixels[idx] = ApplyBlendMode(pixels[idx], layerColor, layer.blendMode);
                }
            }
        }

        result.SetPixels(pixels);
        result.Apply();
        return result;
    }

    private Vector2Int GetTintOutputSize(TerrainTintData tintData)
    {
        // Priority 1: Match generator resolution (from terrain hash)
        if (manager.HeightmapGenerator != null)
        {
            string hash = manager.GetCurrentTerrainHash();
            if (!string.IsNullOrEmpty(hash))
            {
                var data = MBTerrainGeneratorHelpers.ParseTerrainCode(hash);
                if (data != null)
                {
                    var geoData = MBTerrainGeneratorHelpers.CalculateTerrainGeometryData(
                        data.SizeX, data.SizeY, data.PolygonSize);
                    return new Vector2Int(geoData.NumVerticesX, geoData.NumVerticesY);
                }
            }
        }

        // Priority 2: Use first layer with texture
        if (tintData != null)
        {
            foreach (var layer in tintData.layers)
            {
                var tex = layer.EffectiveTexture;
                if (tex != null)
                    return new Vector2Int(tex.width, tex.height);
            }
        }

        // Default fallback
        return new Vector2Int(1024, 1024);
    }

    private Color ApplyBlendMode(Color baseColor, Color layerColor, TerrainTintData.BlendMode mode)
    {
        switch (mode)
        {
            case TerrainTintData.BlendMode.Multiply:
                return new Color(
                    baseColor.r * layerColor.r,
                    baseColor.g * layerColor.g,
                    baseColor.b * layerColor.b);

            case TerrainTintData.BlendMode.Screen:
                return new Color(
                    1f - (1f - baseColor.r) * (1f - layerColor.r),
                    1f - (1f - baseColor.g) * (1f - layerColor.g),
                    1f - (1f - baseColor.b) * (1f - layerColor.b));

            case TerrainTintData.BlendMode.Overlay:
                return new Color(
                    BlendOverlay(baseColor.r, layerColor.r),
                    BlendOverlay(baseColor.g, layerColor.g),
                    BlendOverlay(baseColor.b, layerColor.b));

            case TerrainTintData.BlendMode.SoftLight:
                return new Color(
                    BlendSoftLight(baseColor.r, layerColor.r),
                    BlendSoftLight(baseColor.g, layerColor.g),
                    BlendSoftLight(baseColor.b, layerColor.b));

            case TerrainTintData.BlendMode.Normal:
                return layerColor;

            default:
                // Default to multiply
                return new Color(
                    baseColor.r * layerColor.r,
                    baseColor.g * layerColor.g,
                    baseColor.b * layerColor.b);
        }
    }

    private float BlendOverlay(float b, float l)
    {
        return b < 0.5f ? (2f * b * l) : (1f - 2f * (1f - b) * (1f - l));
    }

    private float BlendSoftLight(float b, float l)
    {
        float d = b <= 0.25f ? ((16f * b - 12f) * b + 4f) * b : Mathf.Sqrt(b);
        return l < 0.5f ? b - (1f - 2f * l) * b * (1f - b) : b + (2f * l - 1f) * (d - b);
    }

    #endregion

    #region Props

    private void DrawPropsSection()
    {
        DrawHeader("Props Export");

        // Settings
        BeginBox();
        EditorGUILayout.PropertyField(propsSettingsProp.FindPropertyRelative("includeProps"));
        EditorGUILayout.PropertyField(propsSettingsProp.FindPropertyRelative("includeEntries"));
        EditorGUILayout.PropertyField(propsSettingsProp.FindPropertyRelative("includeItems"));
        EditorGUILayout.PropertyField(propsSettingsProp.FindPropertyRelative("includePassages"));
        EditorGUILayout.PropertyField(propsSettingsProp.FindPropertyRelative("includePlants"));
        EndBox();

        var stats = manager.GetStatistics();
        EditorGUILayout.LabelField($"Objects to export: {stats.TotalObjectCount}", EditorStyles.miniLabel);

        EditorGUILayout.Space(10);

        // Export buttons
        EditorGUILayout.BeginHorizontal();

        GUI.enabled = manager.Module != null;

        if (DrawButton("Export Props", new Color(0.4f, 0.8f, 0.4f), 30))
        {
            var result = manager.ExportProps();
            EditorUtility.DisplayDialog(result.success ? "Success" : "Failed", result.message, "OK");
        }

        if (DrawButton("Save As...", new Color(0.7f, 0.7f, 1f), 30))
        {
            string path = EditorUtility.SaveFilePanel("Export Props",
                Path.GetDirectoryName(manager.PropsExportPath), $"{manager.SceneName}_props.json", "json");
            if (!string.IsNullOrEmpty(path))
            {
                var result = manager.ExportProps(path);
                EditorUtility.DisplayDialog(result.success ? "Success" : "Failed", result.message, "OK");
            }
        }

        GUI.enabled = true;

        EditorGUILayout.EndHorizontal();
    }

    #endregion

    #region SCO

    private void DrawSCOSection()
    {
        DrawHeader("SCO Repack");

        // Tool check
        string toolPath = MBPathHelpers.MBScoRepackToolPath();
        if (!File.Exists(toolPath))
        {
            EditorGUILayout.HelpBox($"Repack tool not found at:\n{toolPath}", MessageType.Error);
        }

        EditorGUILayout.Space(5);

        BeginBox();
        EditorGUILayout.HelpBox(
            "repack = Convert from unpacked folder\n" +
            "keep = Preserve existing data in output SCO\n" +
            "empty = Clear this section entirely\n" +
            "Donor = Copy from another SCO file",
            MessageType.None);
        EditorGUILayout.Space(5);
        DrawSCOSectionMode("Mission Objects", "missionObjectsMode", "missionObjectsDonor");
        DrawSCOSectionMode("AI Mesh", "aiMeshMode", "aiMeshDonor");
        DrawSCOSectionMode("Terrain", "terrainMode", "terrainDonor");
        EndBox();

        EditorGUILayout.Space(5);

        // Repack buttons
        bool canRepack = manager.Module != null &&
                         File.Exists(toolPath) &&
                         Directory.Exists(manager.EditorScoSceneDataPath);

        EditorGUILayout.BeginHorizontal();

        GUI.enabled = canRepack;

        if (DrawButton("Repack SCO", new Color(0.4f, 0.8f, 0.4f), 30))
        {
            var result = manager.RepackSCO();
            string message = result.success
                ? $"SCO repacked successfully!\n\nLocal: {result.outputPath}\nWarband: {result.copiedToPath}"
                : result.message;
            EditorUtility.DisplayDialog(result.success ? "Success" : "Failed", message, "OK");
        }

        EditorGUILayout.EndHorizontal();

        GUI.enabled = true;

        if (!canRepack)
        {
            if (manager.Module == null)
                EditorGUILayout.HelpBox("Assign Module in General tab.", MessageType.Warning);
            else if (!Directory.Exists(manager.EditorScoSceneDataPath))
                EditorGUILayout.HelpBox("ScoData folder does not exist. Export scene data first.", MessageType.Warning);
        }
    }

    private void DrawSCOSectionMode(string label, string modeProp, string donorProp)
    {
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField(label, GUILayout.Width(100));
        EditorGUILayout.PropertyField(scoSettingsProp.FindPropertyRelative(modeProp), GUIContent.none);
        EditorGUILayout.EndHorizontal();

        var mode = (MBSCOHelpers.SectionMode)scoSettingsProp.FindPropertyRelative(modeProp).enumValueIndex;
        if (mode == MBSCOHelpers.SectionMode.Donor)
        {
            EditorGUI.indentLevel++;
            DrawPathField(scoSettingsProp.FindPropertyRelative(donorProp), "Donor", false, "sco");
            EditorGUI.indentLevel--;
        }
    }

    #endregion
}