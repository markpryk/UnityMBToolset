using System;
using UnityEngine;
using UnityEditor;
using GameObject = UnityEngine.GameObject;
using Object = UnityEngine.Object;

/// <summary>
/// Regenerate tab: Hash generation, layer status, and terrain regeneration controls.
/// This is the RGL Generator functionality.
/// </summary>
public class MBRGLGeneratorTab : SceneDataManagerTabBase
{
    public override string TabName => "RGL Generator";
    public override int Order => 20;
    
    // Serialized properties
    private SerializedProperty regenerationSettingsProp;
    
    // UI state
    private bool showCurrentHash = true;
    private bool showHashGenerator = true;
    private bool showLayerStatus = true;
    private bool showRegenerationSettings = true;
    private bool showSizePreview = true;
    
    // Hash generator state
    private MBTerrainGeneratorData editableData;
    private string generatedHash = "";
    private bool hashGeneratorInitialized = false;
    
    // Paste hash input
    private string pasteHashInput = "";
    

    
    public override void OnEnable(MBSceneDataManager manager, SerializedObject serializedObject)
    {
        base.OnEnable(manager, serializedObject);
        
        regenerationSettingsProp = serializedObject.FindProperty("regenerationSettings");
        hashGeneratorInitialized = false;

    }
    
    public override void OnManagerChanged(MBSceneDataManager manager)
    {
        base.OnManagerChanged(manager);
        hashGeneratorInitialized = false;
    }
    
    public override void DrawTab()
    {
        DrawHeader("Terrain Regeneration");
        
        // Initialize hash generator
        InitializeHashGenerator();
        
        // Layer Status Section
        DrawLayerStatusSection();
        EditorGUILayout.Space(5);
        
        // Hash Generator Section
        DrawHashGeneratorSection();
        EditorGUILayout.Space(5);
        
        // Validation
        var (isValid, validationMessage) = manager.ValidateForRegeneration();
        if (!isValid)
        {
            EditorGUILayout.HelpBox(validationMessage, MessageType.Warning);
            EditorGUILayout.Space(5);
        }
        
        // Current Hash Section
        DrawCurrentHashSection();
        EditorGUILayout.Space(5);
        

            
        // Action Buttons
        DrawRegenerationButtons(isValid);
        EditorGUILayout.Space(10);
    }
    
    #region Initialize
    
    private void InitializeHashGenerator()
    {
        if (hashGeneratorInitialized && editableData != null)
            return;
        
        // Try to load from current terrain hash
        string currentHash = manager.GetCurrentTerrainHash();
        if (!string.IsNullOrEmpty(currentHash) && MBTerrainRegenerator.ValidateHash(currentHash))
        {
            editableData = MBTerrainRegenerator.ParseHash(currentHash);
            generatedHash = currentHash;
        }
        
        // Use defaults if no valid hash
        if (editableData == null)
        {
            editableData = new MBTerrainGeneratorData
            {
                SizeX = 200,
                SizeY = 200,
                PolygonSize = 3,
                TerrainType = 3, // Plain
                HillHeight = 50,
                Valley = 50,
                Ruggedness = 50,
                Vegetation = 50,
                PlaceRiver = false,
                DeepWater = false,
                ShadeOcclude = true,
                DisableGrass = false,
                TerrainSeed = 12345,
                RiverSeed = 23456,
                FloraSeed = 34567
            };
            generatedHash = MBTerrainRegenerator.GenerateHash(editableData);
        }
        
        hashGeneratorInitialized = true;
    }
    
    #endregion
    
    #region Current Hash
    
    private void DrawCurrentHashSection()
    {
        if (!DrawFoldout(ref showCurrentHash, "Current Terrain Hash"))
            return;
        
        BeginBox();
        
        string currentHash = manager.GetCurrentTerrainHash();
        
        EditorGUILayout.BeginHorizontal();
        EditorGUI.BeginDisabledGroup(true);
        EditorGUILayout.TextField("Active Hash", currentHash);
        EditorGUI.EndDisabledGroup();
        
        if (GUILayout.Button("Copy", GUILayout.Width(50)))
        {
            EditorGUIUtility.systemCopyBuffer = currentHash;
            Debug.Log("Hash copied to clipboard");
        }
        EditorGUILayout.EndHorizontal();
        
        // Show parsed info
        if (!string.IsNullOrEmpty(currentHash))
        {
            var data = MBTerrainRegenerator.ParseHash(currentHash);
            if (data != null)
            {
                var (actualX, actualY, vertX, vertY, _, _) = MBTerrainRegenerator.CalculateActualSize(
                    data.SizeX, data.SizeY, data.PolygonSize);
                
                EditorGUILayout.LabelField(
                    $"Type: {MBTerrainRegenerator.GetTerrainTypeName(data.TerrainType)}, " +
                    $"Size: {actualX}x{actualY}m, Vertices: {vertX}x{vertY}", 
                    EditorStyles.miniLabel);
            }
        }
        else
        {
            EditorGUILayout.HelpBox("No hash configured. Use the generator below or paste a hash.", MessageType.Info);
        }
        
        // Paste hash input
        EditorGUILayout.Space(5);
        EditorGUILayout.BeginHorizontal();
        pasteHashInput = EditorGUILayout.TextField("Paste Hash", pasteHashInput);
        
        if (GUILayout.Button("Paste", GUILayout.Width(50)))
        {
            pasteHashInput = EditorGUIUtility.systemCopyBuffer;
        }
        
        GUI.enabled = !string.IsNullOrEmpty(pasteHashInput) && MBTerrainRegenerator.ValidateHash(pasteHashInput);
        if (GUILayout.Button("Load", GUILayout.Width(50)))
        {
            editableData = MBTerrainRegenerator.ParseHash(pasteHashInput);
            generatedHash = pasteHashInput;
            pasteHashInput = "";
        }
        GUI.enabled = true;
        EditorGUILayout.EndHorizontal();
        
        EndBox();
    }
    
    #endregion
    
    #region Layer Status
    
    private void DrawLayerStatusSection()
    {
        if (!DrawFoldout(ref showLayerStatus, "Layer Status & Mode"))
            return;
        
        BeginBox();
        
        // Regeneration mode flags
        DrawRegenerationModeFlags();
        EditorGUILayout.Space(5);
        
        // Current layer states
        DrawCurrentLayerStates();
        EditorGUILayout.Space(5);
        
        // Quick presets
        DrawQuickPresets();
        EditorGUILayout.Space(5);
        
        EndBox();
    }
    
    private void DrawRegenerationModeFlags()
    {
        EditorGUILayout.LabelField("Regeneration Mode", EditorStyles.boldLabel);
        
        var currentMode = manager.CurrentRegenerationMode;
        var newMode = (MBRegenerationHelper.RegenerationMode)EditorGUILayout.EnumFlagsField("Mode Flags", currentMode);
        
        if (newMode != currentMode)
        {
            Undo.RecordObject(manager, "Change Regeneration Mode");
            manager.CurrentRegenerationMode = newMode;
            ApplyModeToLayers();
        }
    }
    
    private void DrawCurrentLayerStates()
    {
        EditorGUILayout.LabelField("Current Layer States", EditorStyles.boldLabel);
        
        // Heightmap layers
        if (manager.HeightmapGenerator != null)
        {
            BeginBox();
            EditorGUILayout.LabelField("Heightmap Layers:", EditorStyles.miniBoldLabel);
            
            foreach (var layer in manager.HeightmapGenerator.layers)
            {
                string status = layer.active ? "●" : "○";
                string typeName = layer.filterType.ToString();
                Color labelColor = layer.active ? Color.green : Color.gray;
                
                EditorGUILayout.BeginHorizontal();
                GUI.color = labelColor;
                EditorGUILayout.LabelField($"  {status} [{typeName}] {layer.name}", EditorStyles.miniLabel);
                GUI.color = Color.white;
                EditorGUILayout.EndHorizontal();
            }
            EndBox();
        }
        
        // Decorator rules summary
        if (manager.Decorator != null)
        {
            BeginBox();
            EditorGUILayout.LabelField("Decorator Rules:", EditorStyles.miniBoldLabel);
            
            int genActive = 0, genTotal = 0;
            int pgmActive = 0, pgmTotal = 0;
            int otherActive = 0, otherTotal = 0;
            
            foreach (var layer in manager.Decorator.layers)
            {
                foreach (var rule in layer.rules)
                {
                    switch (rule.filter)
                    {
                        case MBTerrainDecorator.FilterType.generator:
                            genTotal++;
                            if (rule.active) genActive++;
                            break;
                        case MBTerrainDecorator.FilterType.pgm:
                            pgmTotal++;
                            if (rule.active) pgmActive++;
                            break;
                        default:
                            otherTotal++;
                            if (rule.active) otherActive++;
                            break;
                    }
                }
            }
            
            EditorGUILayout.LabelField($"  Generator (tlt_): {genActive}/{genTotal} active", EditorStyles.miniLabel);
            EditorGUILayout.LabelField($"  PGM (paint): {pgmActive}/{pgmTotal} active", EditorStyles.miniLabel);
            EditorGUILayout.LabelField($"  Other (height/slope): {otherActive}/{otherTotal} active", EditorStyles.miniLabel);
            EndBox();
        }
    }
    
    private void DrawQuickPresets()
    {
        EditorGUILayout.LabelField("Quick Presets", EditorStyles.boldLabel);
        
        EditorGUILayout.BeginHorizontal();
        
        // Generator Only
        if (DrawButton(new GUIContent("Gen Only", "Generator heightmap only\n(no PFM, no PGM paint)"), 
            new Color(0.7f, 1f, 0.7f), 25))
        {
            Undo.RecordObject(manager, "Set Generator Only");
            manager.CurrentRegenerationMode = MBRegenerationHelper.RegenerationMode.GeneratorOnly |
                                              MBRegenerationHelper.RegenerationMode.DecoratorGeneratorOnly;
            ApplyModeToLayers();
        }
        
        // Generator + PFM
        if (DrawButton(new GUIContent("Gen + PFM", "Generator + PFM heightmap\n(no PGM paint)"),
            new Color(0.8f, 1f, 0.8f), 25))
        {
            Undo.RecordObject(manager, "Set Generator + PFM");
            manager.CurrentRegenerationMode = MBRegenerationHelper.RegenerationMode.GeneratorAndPFM |
                                              MBRegenerationHelper.RegenerationMode.DecoratorGeneratorOnly;
            ApplyModeToLayers();
        }
        
        // Full mode
        if (DrawButton(new GUIContent("Full", "All heightmap layers\n+ all decorator rules"),
            new Color(0.7f, 0.85f, 1f), 25))
        {
            Undo.RecordObject(manager, "Set Full Mode");
            manager.CurrentRegenerationMode = MBRegenerationHelper.RegenerationMode.FullRegeneration;
            ApplyModeToLayers();
        }
        
        EditorGUILayout.EndHorizontal();
        
        EditorGUILayout.BeginHorizontal();
        
        // Decorator tlt only
        if (DrawButton(new GUIContent("Dec (tlt)", "Decorator tlt_ rules only\n(no heightmap regen)"),
            new Color(0.85f, 0.7f, 1f), 25))
        {
            Undo.RecordObject(manager, "Set Decorator tlt Only");
            manager.CurrentRegenerationMode = MBRegenerationHelper.RegenerationMode.DecoratorGeneratorOnly;
            if (manager.Decorator != null)
                MBRegenerationHelper.SetDecoratorGeneratorOnlyMode(manager.Decorator);
        }
        
        // Decorator Full
        if (DrawButton(new GUIContent("Dec (Full)", "Decorator all rules\n(no heightmap regen)"),
            new Color(0.9f, 0.7f, 1f), 25))
        {
            Undo.RecordObject(manager, "Set Decorator Full");
            manager.CurrentRegenerationMode = MBRegenerationHelper.RegenerationMode.DecoratorFull;
            if (manager.Decorator != null)
                MBRegenerationHelper.SetDecoratorFullMode(manager.Decorator);
        }
        
        EditorGUILayout.EndHorizontal();
    }
    
    private void ApplyModeToLayers()
    {
        var mode = manager.CurrentRegenerationMode;
        
        if (manager.HeightmapGenerator != null)
        {
            MBRegenerationHelper.ConfigureHeightmapLayers(manager.HeightmapGenerator, mode);
        }
        
        if (manager.Decorator != null)
        {
            MBRegenerationHelper.ConfigureDecoratorRules(manager.Decorator, mode);
        }
        
        Debug.Log($"[RegenerateTab] Applied mode {mode} to layers");
    }
    
    #endregion
    
    #region Hash Generator
    
    private void DrawHashGeneratorSection()
    {
        if (!DrawFoldout(ref showHashGenerator, "Hash Generator"))
            return;
        
        if (editableData == null)
            return;
        
        BeginBox();
        
        EditorGUI.BeginChangeCheck();
        
        // Dimensions
        EditorGUILayout.LabelField("Dimensions", EditorStyles.boldLabel);

        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.PrefixLabel("Size X");
        editableData.SizeX = EditorGUILayout.IntSlider(editableData.SizeX, MBTerrainRegenerator.MIN_SIZE, MBTerrainRegenerator.MAX_SIZE);
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.PrefixLabel("Size Y");
        editableData.SizeY = EditorGUILayout.IntSlider(editableData.SizeY, MBTerrainRegenerator.MIN_SIZE, MBTerrainRegenerator.MAX_SIZE);
        EditorGUILayout.EndHorizontal();
        
        // Polygon size
        string[] polygonLabels = { "2 meters", "3 meters", "4 meters", "5 meters" };
        int polygonIndex = editableData.PolygonSize - 2;
        polygonIndex = EditorGUILayout.Popup("Polygon Size", polygonIndex, polygonLabels);
        editableData.PolygonSize = polygonIndex + 2;
        
        // Size preview
        if (DrawFoldout(ref showSizePreview, "Size Preview"))
        {
            var (actualX, actualY, vertX, vertY, facesX, facesY) = MBTerrainRegenerator.CalculateActualSize(
                editableData.SizeX, editableData.SizeY, editableData.PolygonSize);
            
            EditorGUI.indentLevel++;
            EditorGUI.BeginDisabledGroup(true);
            EditorGUILayout.LabelField($"Actual Size: {actualX} × {actualY} meters");
            EditorGUILayout.LabelField($"Vertices: {vertX} × {vertY} ({vertX * vertY:N0} total)");
            EditorGUILayout.LabelField($"Faces: {facesX} × {facesY} ({facesX * facesY * 2:N0} triangles)");
            EditorGUI.EndDisabledGroup();
            EditorGUI.indentLevel--;
        }
        
        EditorGUILayout.Space(5);
        
        // Terrain type
        EditorGUILayout.LabelField("Terrain Type", EditorStyles.boldLabel);
        
        string[] typeNames = new string[MBTerrainRegenerator.TerrainTypes.Length];
        for (int i = 0; i < MBTerrainRegenerator.TerrainTypes.Length; i++)
        {
            typeNames[i] = MBTerrainRegenerator.TerrainTypes[i].name;
        }
        
        int typeIndex = MBTerrainRegenerator.GetTerrainTypeIndex(editableData.TerrainType);
        typeIndex = EditorGUILayout.Popup("Type", typeIndex, typeNames);
        editableData.TerrainType = MBTerrainRegenerator.TerrainTypes[typeIndex].id;
        
        EditorGUILayout.Space(5);
        
        // Features
        EditorGUILayout.LabelField("Features", EditorStyles.boldLabel);
        
        editableData.Vegetation = EditorGUILayout.IntSlider("Vegetation", editableData.Vegetation, 0, 127);
        editableData.Ruggedness = EditorGUILayout.IntSlider("Ruggedness", editableData.Ruggedness, 0, 127);
        editableData.Valley = EditorGUILayout.IntSlider("Valley", editableData.Valley, 0, 127);
        editableData.HillHeight = EditorGUILayout.IntSlider("Hill Height", editableData.HillHeight, 0, 127);
        
        EditorGUILayout.Space(5);
        
        // Flags
        EditorGUILayout.LabelField("Flags", EditorStyles.boldLabel);
        
        EditorGUILayout.BeginHorizontal();
        editableData.PlaceRiver = EditorGUILayout.Toggle("Place River", editableData.PlaceRiver);
        editableData.DeepWater = EditorGUILayout.Toggle("Deep Water", editableData.DeepWater);
        EditorGUILayout.EndHorizontal();
        
        EditorGUILayout.BeginHorizontal();
        editableData.ShadeOcclude = EditorGUILayout.Toggle("Shade Occlude", editableData.ShadeOcclude);
        editableData.DisableGrass = EditorGUILayout.Toggle("Disable Grass", editableData.DisableGrass);
        EditorGUILayout.EndHorizontal();
        
        EditorGUILayout.Space(5);
        
        // Seeds
        EditorGUILayout.LabelField("Seeds", EditorStyles.boldLabel);
        
        editableData.TerrainSeed = DrawSeedField("Terrain Seed", editableData.TerrainSeed);
        editableData.RiverSeed = DrawSeedField("River Seed", editableData.RiverSeed);
        editableData.FloraSeed = DrawSeedField("Flora Seed", editableData.FloraSeed);
        
        EditorGUILayout.BeginHorizontal();
        GUILayout.FlexibleSpace();
        if (GUILayout.Button("Randomize All Seeds", GUILayout.Width(140)))
        {
            editableData.TerrainSeed = MBTerrainRegenerator.GenerateRandomSeed();
            editableData.RiverSeed = MBTerrainRegenerator.GenerateRandomSeed();
            editableData.FloraSeed = MBTerrainRegenerator.GenerateRandomSeed();
        }
        EditorGUILayout.EndHorizontal();
        
        // Regenerate hash if changed
        if (EditorGUI.EndChangeCheck())
        {
            generatedHash = MBTerrainRegenerator.GenerateHash(editableData);
        }
        
        EditorGUILayout.Space(10);
        
        // Generated hash output
        EditorGUILayout.LabelField("Generated Hash", EditorStyles.boldLabel);
        
        EditorGUILayout.BeginHorizontal();
        
        GUIStyle hashStyle = new GUIStyle(EditorStyles.textField);
        hashStyle.fontStyle = FontStyle.Italic;
        hashStyle.alignment = TextAnchor.MiddleCenter;
        
        EditorGUILayout.SelectableLabel(generatedHash, hashStyle, GUILayout.Height(EditorGUIUtility.singleLineHeight));
        
        if (GUILayout.Button("Copy", GUILayout.Width(50)))
        {
            EditorGUIUtility.systemCopyBuffer = generatedHash;
            Debug.Log("Generated hash copied to clipboard");
        }
        EditorGUILayout.EndHorizontal();
        
        // Validate indicator
        bool isValidGenerated = MBTerrainRegenerator.ValidateHash(generatedHash);
        EditorGUILayout.LabelField(isValidGenerated ? "✓ Valid hash" : "✗ Invalid hash",
            isValidGenerated ? EditorStyles.miniLabel : EditorStyles.boldLabel);
        
        EndBox();
    }
    
    /// <summary>
    /// Draw a seed field with random button. Returns the new value.
    /// </summary>
    private uint DrawSeedField(string label, uint currentValue)
    {
        uint result = currentValue;
        
        EditorGUILayout.BeginHorizontal();
        result = (uint)EditorGUILayout.IntField(label, (int)currentValue);
        if (GUILayout.Button("Random", GUILayout.Width(60)))
        {
            result = MBTerrainRegenerator.GenerateRandomSeed();
        }
        EditorGUILayout.EndHorizontal();
        
        return result;
    }
    
    #endregion
    
    #region Regeneration Buttons
    
    private void DrawRegenerationButtons(bool isValid)
    {
        BeginBox();
        
        bool hasGeneratedHash = !string.IsNullOrEmpty(generatedHash) && MBTerrainRegenerator.ValidateHash(generatedHash);
        
        // Primary: Generate with current setup
        EditorGUILayout.LabelField("Generate with Current Layer Setup", EditorStyles.miniBoldLabel);
        
        EditorGUILayout.BeginHorizontal();
        
        GUI.enabled = manager.Terrain != null && hasGeneratedHash;
        
        if (DrawButton(new GUIContent("▶ Generate (Current Setup)",
            "Generate terrain using the layer configuration shown above.\nDoes NOT change any layer states."),
            new Color(0.4f, 0.95f, 0.4f), 35))
        {
            string modeSummary = manager.CurrentRegenerationMode.ToString();
            
            if (EditorUtility.DisplayDialog("Generate with Current Setup",
                $"Generate terrain with current layer configuration:\n\n" +
                $"Mode: {modeSummary}\n" +
                $"Hash: {generatedHash}\n\n" +
                "Layer states will NOT be modified. Continue?",
                "Generate", "Cancel"))
            {
                
                if ((manager.CurrentRegenerationMode & MBRegenerationHelper.RegenerationMode.DecoratorGeneratorOnly) != 0)
                {
                    manager.SetupDecoratorFromCurrentHash(generatorRulesOnly: true, currentHash: generatedHash);
                }
                else if ((manager.CurrentRegenerationMode & MBRegenerationHelper.RegenerationMode.DecoratorFull) != 0)
                {
                    manager.SetupDecoratorFromCurrentHash(generatorRulesOnly: false, currentHash: generatedHash);
                }
                
                var result = manager.RegenerateFromHashKeepLayers(generatedHash);

                string message = result.success
                    ? $"Generation complete!\n\nTime: {result.totalTime:F2}s"
                    : result.message;
                
                EditorUtility.DisplayDialog(result.success ? "Success" : "Failed", message, "OK");
                hashGeneratorInitialized = false;
            }
        }
        
        EditorGUILayout.EndHorizontal();
        
        EditorGUILayout.Space(10);
        
        // Partial regeneration
        EditorGUILayout.LabelField("Alternative Generator", EditorStyles.miniBoldLabel);
        
        EditorGUILayout.BeginHorizontal();
        
        bool hasDecorator = manager.Decorator != null;
        GUI.enabled = isValid && hasDecorator;
        
        EditorGUILayout.EndHorizontal();
        
        if (!hasDecorator)
        {
            EditorGUILayout.HelpBox("MBTerrainDecorator not found on terrain.", MessageType.None);
        }
        
        // Utility buttons
        EditorGUILayout.BeginHorizontal();
        
        if (DrawButton("Open Swyter's Generator", 22))
        {
            Application.OpenURL($"https://swyter.github.io/mab-tools/terrain#{generatedHash}");
        }
        
        EditorGUILayout.EndHorizontal();
        
        EndBox();
    }
    
    #endregion
}