using System;
using UnityEngine;
using UnityEditor;

/// <summary>
/// Erosion tab: Terrain erosion using Hatchling's algorithm.
/// Provides GPU-accelerated hydraulic erosion simulation.
/// Algorithm based on: https://www.proceduralpixels.com/blog/terrain-hack-fastest-erosion-algorithm-ever
/// </summary>
public class ErosionTab : SceneDataManagerTabBase
{
    public override string TabName => "Erosion";
    public override int Order => 6;

    // Subtab navigation
    private enum ErosionSubTab
    {
        Erosion,
        Advanced
    }

    private ErosionSubTab currentSubTab = ErosionSubTab.Erosion;

    // Foldout states
    private bool showParameters = true;
    private bool showMultiPass = false;
    private bool showAdaptiveKernel = false;
    private bool showHeightRange = false;
    private bool showPreview = true;
    private bool showAlgorithmInfo = false;

    // Erosion settings (stored here for editor persistence)
    private int kernelSize = 7;
    private int layersCount = 120;
    private float erosionStrength = 1f;
    private int passes = 1;
    private float passStrengthMultiplier = 0.5f;
    private bool useAdaptiveKernel = true;
    private float kernelMinScale = 0.5f;
    private float kernelMaxScale = 1.5f;
    private bool useCustomHeightRange = false;
    private float customMinHeight = 0f;
    private float customMaxHeight = 100f;

    // Preview state
    private Texture2D beforePreview;
    private Texture2D afterPreview;
    private float[,] originalHeights;
    private float lastProcessingTime;
    private bool hasPreviewData;

    // Compute shader reference
    private ComputeShader erosionShader;

    // Reference to processor component (if exists on terrain)
    private TerrainErosionProcessor Processor
    {
        get
        {
            if (manager?.Terrain == null) return null;
            return manager.Terrain.GetComponent<TerrainErosionProcessor>();
        }
    }

    public override void OnEnable(MBSceneDataManager manager, SerializedObject serializedObject)
    {
        base.OnEnable(manager, serializedObject);
        FindShader();
        LoadSettingsFromProcessor();
    }

    public override void OnManagerChanged(MBSceneDataManager manager)
    {
        base.OnManagerChanged(manager);
        LoadSettingsFromProcessor();
    }

    public override void DrawTab()
    {
        if (manager?.Terrain == null)
        {
            DrawNoTerrainWarning();
            return;
        }

        DrawErosionTab();
    }

    #region Main Erosion Tab

    private void DrawErosionTab()
    {
        // Quick Actions
        DrawQuickActions();

        EditorGUILayout.Space(5);
        DrawUILine(Color.grey);

        // Parameters section
        DrawParametersSection();

        EditorGUILayout.Space(5);
        // Adaptive Kernel section
        DrawAdaptiveKernelSection();

        EditorGUILayout.Space(5);

        // Height Range section
        DrawHeightRangeSection();
    }

    private void DrawQuickActions()
    {
        BeginBox();

        // Apply Erosion button
        GUI.backgroundColor = new Color(0.9f, 0.7f, 0.4f);
        if (GUILayout.Button(new GUIContent("Apply Erosion", "Apply erosion to terrain heightmap"),
                GUILayout.Height(30)))
        {
            ApplyErosion();
        }

        GUI.backgroundColor = Color.white;

        EndBox();
    }

    private void DrawParametersSection()
    {
        if (!DrawFoldout(ref showParameters, "Erosion Parameters"))
            return;

        BeginBox();

        EditorGUI.BeginChangeCheck();

        // Kernel Size
        EditorGUILayout.BeginHorizontal();
        kernelSize = EditorGUILayout.IntSlider(
            new GUIContent("Kernel Size",
                "Search window radius in pixels. Larger values spread erosion further but are slower."),
            kernelSize, 1, 32);
        DrawMiniLabel($"({kernelSize * 2 + 1}x{kernelSize * 2 + 1} window)");
        EditorGUILayout.EndHorizontal();

        // Layers Count
        layersCount = EditorGUILayout.IntSlider(
            new GUIContent("Layer Count",
                "Number of threshold layers. More layers = smoother results but slower processing."),
            layersCount, 8, 256);

        // Erosion Strength
        erosionStrength = EditorGUILayout.Slider(
            new GUIContent("Strength", "Blend factor for erosion effect. 0 = no change, 1 = full erosion."),
            erosionStrength, 0f, 1f);

        if (EditorGUI.EndChangeCheck())
        {
            SyncToProcessor();
        }

        EditorGUILayout.Space(5);
        DrawMultiPassSection();

        // Performance estimate
        EditorGUILayout.Space(5);
        DrawPerformanceEstimate();

        EndBox();
    }

    private void DrawMultiPassSection()
    {
        BeginBox();

        EditorGUI.BeginChangeCheck();

        passes = EditorGUILayout.IntSlider(
            new GUIContent("Passes", "Number of erosion iterations. Multiple passes create deeper erosion."),
            passes, 1, 10);

        if (passes > 1)
        {
            EditorGUI.indentLevel++;
            passStrengthMultiplier = EditorGUILayout.Slider(
                new GUIContent("Strength Falloff",
                    "Strength multiplier per pass. Lower values reduce subsequent pass intensity."),
                passStrengthMultiplier, 0.1f, 1f);

            // Show effective strengths
            string strengthsInfo = "Effective: ";
            float currentStr = erosionStrength;
            for (int i = 0; i < passes; i++)
            {
                strengthsInfo += $"{currentStr:F2}";
                if (i < passes - 1)
                {
                    strengthsInfo += " → ";
                    currentStr *= passStrengthMultiplier;
                }
            }

            DrawMiniLabel(strengthsInfo);
            EditorGUI.indentLevel--;
        }

        if (EditorGUI.EndChangeCheck())
        {
            SyncToProcessor();
        }

        EndBox();
    }

    private void DrawPerformanceEstimate()
    {
        // Rough estimate based on parameters
        int resolution = manager?.Terrain?.terrainData?.heightmapResolution ?? 1024;
        int windowArea = (kernelSize * 2 + 1) * (kernelSize * 2 + 1);
        long operations = (long)resolution * resolution * windowArea * layersCount * passes;

        string complexity;
        Color indicatorColor;

        if (operations < 1_000_000_000)
        {
            complexity = "Fast";
            indicatorColor = new Color(0.4f, 0.8f, 0.4f);
        }
        else if (operations < 10_000_000_000)
        {
            complexity = "Medium";
            indicatorColor = new Color(0.9f, 0.8f, 0.3f);
        }
        else
        {
            complexity = "Slow";
            indicatorColor = new Color(0.9f, 0.5f, 0.4f);
        }

        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("Estimated:", GUILayout.Width(60));

        Color prev = GUI.color;
        GUI.color = indicatorColor;
        EditorGUILayout.LabelField(complexity, EditorStyles.boldLabel, GUILayout.Width(60));
        GUI.color = prev;

        EditorGUILayout.LabelField($"({operations / 1_000_000:N0}M ops)", EditorStyles.miniLabel);
        EditorGUILayout.EndHorizontal();
    }

    #endregion

    #region Advanced Tab

    private void DrawAdaptiveKernelSection()
    {
        if (!DrawFoldout(ref showAdaptiveKernel, "Adaptive Kernel"))
            return;

        BeginBox();

        EditorGUI.BeginChangeCheck();

        useAdaptiveKernel = EditorGUILayout.Toggle(
            new GUIContent("Enable Adaptive Kernel", "Vary kernel size based on terrain height"),
            useAdaptiveKernel);

        if (useAdaptiveKernel)
        {
            EditorGUI.indentLevel++;

            kernelMinScale = EditorGUILayout.Slider(
                new GUIContent("Min Scale (Low Points)", "Kernel scale multiplier at lowest terrain points"),
                kernelMinScale, 0.1f, 2f);

            kernelMaxScale = EditorGUILayout.Slider(
                new GUIContent("Max Scale (High Points)", "Kernel scale multiplier at highest terrain points"),
                kernelMaxScale, 0.5f, 3f);

            EditorGUILayout.Space(3);
            DrawMiniLabel(
                $"Effective kernel range: {Mathf.RoundToInt(kernelSize * kernelMinScale)} - {Mathf.RoundToInt(kernelSize * kernelMaxScale)}");

            EditorGUI.indentLevel--;
        }

        if (EditorGUI.EndChangeCheck())
        {
            SyncToProcessor();
        }

        EditorGUILayout.Space(3);
        EditorGUILayout.HelpBox(
            "Adaptive kernel uses larger erosion windows at higher elevations, " +
            "creating more erosion at peaks and less in valleys. This mimics natural erosion patterns.",
            MessageType.Info);

        EndBox();
    }

    private void DrawHeightRangeSection()
    {
        if (!DrawFoldout(ref showHeightRange, "Height Range"))
            return;

        BeginBox();

        EditorGUI.BeginChangeCheck();

        useCustomHeightRange = EditorGUILayout.Toggle(
            new GUIContent("Custom Height Range", "Override automatic height detection"),
            useCustomHeightRange);

        if (useCustomHeightRange)
        {
            EditorGUI.indentLevel++;

            customMinHeight = EditorGUILayout.FloatField("Min Height (m)", customMinHeight);
            customMaxHeight = EditorGUILayout.FloatField("Max Height (m)", customMaxHeight);

            if (customMaxHeight <= customMinHeight)
            {
                EditorGUILayout.HelpBox("Max height must be greater than min height!", MessageType.Warning);
            }

            EditorGUI.indentLevel--;
        }
        else
        {
            // Show auto-detected range
            if (manager?.Terrain != null)
            {
                var terrainData = manager.Terrain.terrainData;
                float terrainHeight = terrainData.size.y;
                EditorGUILayout.LabelField($"Auto-detected range: 0 - {terrainHeight:F1}m", EditorStyles.miniLabel);
            }
        }

        if (EditorGUI.EndChangeCheck())
        {
            SyncToProcessor();
        }

        EndBox();
    }

    #endregion

    #region Actions

    private void ApplyErosion()
    {
        if (manager?.Terrain == null) return;

        Undo.RegisterCompleteObjectUndo(manager.Terrain.terrainData, "Apply Terrain Erosion");

        var settings = CreateCurrentSettings();
        lastProcessingTime = TerrainErosionProcessor.ProcessTerrain(manager.Terrain, settings, erosionShader);
    }

    private TerrainErosionProcessor.ErosionSettings CreateCurrentSettings()
    {
        return new TerrainErosionProcessor.ErosionSettings
        {
            kernelSize = this.kernelSize,
            layersCount = this.layersCount,
            erosionStrength = this.erosionStrength,
            passes = this.passes,
            passStrengthMultiplier = this.passStrengthMultiplier,
            useAdaptiveKernel = this.useAdaptiveKernel,
            kernelMinScale = this.kernelMinScale,
            kernelMaxScale = this.kernelMaxScale,
            useCustomHeightRange = this.useCustomHeightRange,
            customMinHeight = this.customMinHeight,
            customMaxHeight = this.customMaxHeight
        };
    }

    #endregion

    #region Processor Sync

    private void LoadSettingsFromProcessor()
    {
        var processor = Processor;
        if (processor == null) return;

        kernelSize = processor.kernelSize;
        layersCount = processor.layersCount;
        erosionStrength = processor.erosionStrength;
        passes = processor.passes;
        passStrengthMultiplier = processor.passStrengthMultiplier;
        useAdaptiveKernel = processor.useAdaptiveKernel;
        kernelMinScale = processor.kernelMinScale;
        kernelMaxScale = processor.kernelMaxScale;
        useCustomHeightRange = processor.useCustomHeightRange;
        customMinHeight = processor.customMinHeight;
        customMaxHeight = processor.customMaxHeight;

        if (processor.erosionShader != null)
        {
            erosionShader = processor.erosionShader;
        }
    }

    private void SyncToProcessor()
    {
        var processor = Processor;
        if (processor == null) return;

        Undo.RecordObject(processor, "Update Erosion Settings");

        processor.kernelSize = kernelSize;
        processor.layersCount = layersCount;
        processor.erosionStrength = erosionStrength;
        processor.passes = passes;
        processor.passStrengthMultiplier = passStrengthMultiplier;
        processor.useAdaptiveKernel = useAdaptiveKernel;
        processor.kernelMinScale = kernelMinScale;
        processor.kernelMaxScale = kernelMaxScale;
        processor.useCustomHeightRange = useCustomHeightRange;
        processor.customMinHeight = customMinHeight;
        processor.customMaxHeight = customMaxHeight;

        EditorUtility.SetDirty(processor);
    }

    #endregion

    #region Helpers

    private void DrawNoTerrainWarning()
    {
        EditorGUILayout.HelpBox(
            "No terrain assigned.\n\n" +
            "Assign a terrain in the General tab to enable erosion tools.",
            MessageType.Warning);
    }

    private void FindShader()
    {
        if (erosionShader != null) return;

        string[] guids = AssetDatabase.FindAssets("HatchlingErosion t:ComputeShader");
        if (guids.Length > 0)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[0]);
            erosionShader = AssetDatabase.LoadAssetAtPath<ComputeShader>(path);
        }
    }

    #endregion
}