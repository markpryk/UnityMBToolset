using UnityEngine;
using UnityEditor;
using MBEditor.Tools.Flora.Logic;

//  RGL Decorator Integration - UI for FloraPopulatorSubTab
//
//  Integration points in FloraPopulatorSubTab.cs:
//
//  1. In DrawSelectedLayer(), when LayerType == RGL_Automatic:
//       RGLDecoratorIntegrationUI.DrawRGLDecoratorSection(parent, layer, _config);
//
//  2. In DrawSpawnActions(), after the Warband Spawn button:
//       RGLDecoratorIntegrationUI.DrawApplyToDecoratorButton(parent, _config);
//

public static class RGLDecoratorIntegrationUI
{
    private static string _lastApplyMessage = "";
    
    //  Per-Layer RGL Section (inside selected layer detail)
    
    /// <summary>
    /// Draw the RGL → Decorator section within a selected RGL_Automatic layer.
    /// Shows: decorator status, mask strength, apply/rebuild/clear buttons.
    /// Splatmap masks are drawn separately by the caller (DrawSplatmapMasks).
    /// </summary>
    public static void DrawRGLDecoratorSection(
        FloraTab parent, FloraDecoratorLayer layer, FloraPopulatorConfig config)
    {
        if (layer.LayerType != FloraLayerType.RGL_Automatic) return;
        
        EditorGUILayout.Space(6);
        EditorGUILayout.LabelField("RGL → Decorator", EditorStyles.boldLabel);
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        
        var decorator = parent.SceneTerrain?.GetComponent<MBTerrainDecorator>();
        bool canApply = decorator != null && parent.SceneTerrain != null;
        
        var hashInfo = GetTerrainInfo(parent);
        
        if (hashInfo.terrainType < 0)
        {
            EditorGUILayout.HelpBox(
                "Terrain hash not found. Ensure LayeredHeightmapGenerator has a valid hash, " +
                "or enter a Warband terrain code in Global Settings.",
                MessageType.Warning);
            EditorGUILayout.EndVertical();
            return;
        }
        
        EditorGUILayout.LabelField(
            $"Terrain: {hashInfo.typeName} (ID:{hashInfo.terrainType})",
            EditorStyles.miniLabel);
        
        EditorGUILayout.Space(6);
        EditorGUILayout.BeginHorizontal();
        
        EditorGUI.BeginDisabledGroup(!canApply);
        Color orig = GUI.backgroundColor;
        
        // Apply - generates mask, saves PNG, assigns to decorator
        GUI.backgroundColor = new Color(0.5f, 0.7f, 1f);
        if (GUILayout.Button("⬇ Apply to Decorator", GUILayout.Height(26)))
        {
            DoApply(parent, layer, config, rebuildDecorator: false);
        }
        
        // Rebuild - force full rebuild from hash + reapply mask
        GUI.backgroundColor = new Color(0.7f, 0.5f, 1f);
        if (GUILayout.Button("⟳ Rebuild", GUILayout.Width(70), GUILayout.Height(26)))
        {
            if (EditorUtility.DisplayDialog("Rebuild Decorator",
                "This will rebuild all decorator layers from the terrain hash.\n" +
                "Custom rules you've added manually will be lost.\n\nContinue?",
                "Rebuild", "Cancel"))
            {
                DoApply(parent, layer, config, rebuildDecorator: true);
            }
        }
        
        // Clear vegetation mask
        GUI.backgroundColor = new Color(1f, 0.75f, 0.6f);
        bool hasMask = decorator != null && RGLDecoratorBridge.HasVegetationMask(decorator);
        EditorGUI.BeginDisabledGroup(!hasMask);
        if (GUILayout.Button("✕ Clear", GUILayout.Width(60), GUILayout.Height(26)))
        {
            RGLDecoratorBridge.ClearVegetationMask(decorator);
            _lastApplyMessage = "Vegetation mask cleared.";
        }
        EditorGUI.EndDisabledGroup();
        
        GUI.backgroundColor = orig;
        EditorGUI.EndDisabledGroup();
        
        EditorGUILayout.EndHorizontal();
        
        if (!canApply && decorator == null)
            EditorGUILayout.HelpBox("Add MBTerrainDecorator to the terrain.", MessageType.Warning);
        
        if (!string.IsNullOrEmpty(_lastApplyMessage))
            EditorGUILayout.LabelField(_lastApplyMessage, EditorStyles.centeredGreyMiniLabel);
        
        EditorGUILayout.EndVertical();
    }
    
    //  Global Apply Button (in Spawn Actions)
    
    /// <summary>
    /// Compact button in the spawn actions area. Applies all RGL_Automatic layers.
    /// </summary>
    public static void DrawApplyToDecoratorButton(FloraTab parent, FloraPopulatorConfig config)
    {
        if (config == null) return;
        
        // Count enabled RGL layers
        int rglCount = 0;
        FloraDecoratorLayer primaryRgl = null;
        foreach (var layer in config.Layers)
        {
            if (layer.Enabled && layer.LayerType == FloraLayerType.RGL_Automatic)
            {
                rglCount++;
                if (primaryRgl == null) primaryRgl = layer;
            }
        }
        
        if (rglCount == 0) return;
        
        var decorator = parent.SceneTerrain?.GetComponent<MBTerrainDecorator>();
        bool canApply = decorator != null && parent.SceneTerrain != null;
        
        EditorGUILayout.Space(2);
        EditorGUI.BeginDisabledGroup(!canApply);
        
        Color orig = GUI.backgroundColor;
        GUI.backgroundColor = new Color(0.5f, 0.7f, 1f);
        if (GUILayout.Button($"⬇ Apply RGL to Decorator", GUILayout.Height(24)))
        {
            DoApply(parent, primaryRgl, config, rebuildDecorator: false);
        }
        GUI.backgroundColor = orig;
        
        EditorGUI.EndDisabledGroup();
    }
    
    //  Apply Logic
    
    private static void DoApply(
        FloraTab parent, FloraDecoratorLayer layer, 
        FloraPopulatorConfig config, bool rebuildDecorator)
    {
        if (parent.SceneTerrain == null) return;
        
        string terrainCode = GetTerrainCode(parent);
        if (string.IsNullOrEmpty(terrainCode))
        {
            EditorUtility.DisplayDialog("No Terrain Code",
                "No terrain hash found on LayeredHeightmapGenerator and no Warband Code in Global Settings.",
                "OK");
            return;
        }
        
        // Generate composite mask (warband + splatmap masking)
        float[,] mask = null;
        EditorUtility.DisplayProgressBar("RGL Apply", "Generating vegetation mask...", 0.2f);
        mask = RGLDecoratorBridge.GenerateCompositeMask(parent.SceneTerrain, terrainCode, layer);
        
        // Apply to decorator
        float strength = layer.VegetationMaskStrength;
        var result = RGLDecoratorBridge.Apply(parent.SceneTerrain, mask, strength, rebuildDecorator);
        
        _lastApplyMessage = result.Message;
        EditorUtility.ClearProgressBar();
        
        if (!result.Success)
            EditorUtility.DisplayDialog("RGL Apply Failed", result.Message, "OK");
    }
    
    //  Helpers
    
    private static string GetTerrainCode(FloraTab parent)
    {
        if (parent.SceneTerrain != null)
        {
            var gen = parent.SceneTerrain.GetComponent<LayeredHeightmapGenerator>();
            if (gen != null && !string.IsNullOrEmpty(gen.CurrentTerrainHash))
                return gen.CurrentTerrainHash;
        }
        
        var config = parent.manager?.FloraPopulationConfig;
        if (config != null && !string.IsNullOrEmpty(config.WarbandTerrainCode))
            return config.WarbandTerrainCode;
        
        return null;
    }
    
    private struct TerrainInfo
    {
        public int terrainType;
        public string typeName;
    }
    
    private static TerrainInfo GetTerrainInfo(FloraTab parent)
    {
        string code = GetTerrainCode(parent);
        if (string.IsNullOrEmpty(code) || !MBTerrainRegenerator.ValidateHash(code))
            return new TerrainInfo { terrainType = -1 };
        
        var data = MBTerrainRegenerator.ParseHash(code);
        if (data == null)
            return new TerrainInfo { terrainType = -1 };
        
        return new TerrainInfo
        {
            terrainType = data.TerrainType,
            typeName = MBTerrainRegenerator.GetTerrainTypeName(data.TerrainType)
        };
    }
}
