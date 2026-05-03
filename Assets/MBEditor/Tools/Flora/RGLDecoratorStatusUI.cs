using UnityEngine;
using UnityEditor;

//  RGL Status Display for DecoratorTab
//
//  Integration: In DecoratorTab.DrawLayersTab(), after the preset section:
//    RGLDecoratorStatusUI.DrawRGLStatus(Decorator);
//

public static class RGLDecoratorStatusUI
{
    private static readonly Color RGLColor = new Color(0.5f, 0.7f, 1f);
    private static bool _showSection = true;
    
    /// <summary>
    /// Draw compact RGL status in the Decorator tab.
    /// Only shows if a vegetation mask is assigned or generator layers exist.
    /// </summary>
    public static void DrawRGLStatus(MBTerrainDecorator decorator)
    {
        if (decorator == null) return;
        
        bool hasGen = RGLDecoratorBridge.HasGeneratorRules(decorator);
        bool hasMask = RGLDecoratorBridge.HasVegetationMask(decorator);
        if (!hasGen && !hasMask) return;
        
        EditorGUILayout.BeginHorizontal();
        Rect r = EditorGUILayout.GetControlRect(GUILayout.Height(20));
        EditorGUI.DrawRect(new Rect(r.x, r.y, 4, r.height), RGLColor);
        
        _showSection = EditorGUI.Foldout(
            new Rect(r.x + 8, r.y, r.width - 8, r.height),
            _showSection, "RGL Status", true, EditorStyles.foldoutHeader);
        EditorGUILayout.EndHorizontal();
        
        if (!_showSection) return;
        
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        
        // Status
        string status = RGLDecoratorBridge.GetStatusString(decorator);
        EditorGUILayout.LabelField(status, EditorStyles.centeredGreyMiniLabel);
        
        // Terrain info
        var gen = decorator.GetComponent<LayeredHeightmapGenerator>();
        if (gen != null && !string.IsNullOrEmpty(gen.CurrentTerrainHash))
        {
            var data = MBTerrainRegenerator.ParseHash(gen.CurrentTerrainHash);
            if (data != null)
            {
                EditorGUILayout.LabelField(
                    $"Terrain: {MBTerrainRegenerator.GetTerrainTypeName(data.TerrainType)} (ID:{data.TerrainType})",
                    EditorStyles.miniLabel);
            }
        }
        
        // Vegetation mask info
        if (hasMask)
        {
            EditorGUILayout.Space(2);
            
            string texName = decorator.vegetationMaskTexture != null 
                ? decorator.vegetationMaskTexture.name : "-";
            int texW = decorator.vegetationMaskTexture != null ? decorator.vegetationMaskTexture.width : 0;
            int texH = decorator.vegetationMaskTexture != null ? decorator.vegetationMaskTexture.height : 0;
            
            EditorGUILayout.LabelField(
                $"  Mask: {texName} ({texW}×{texH})  |  " +
                $"Channel: {decorator.vegetationMaskChannel}  |  " +
                $"Strength: {decorator.vegetationMaskStrength:F2}" +
                (decorator.vegetationMaskInvert ? "  |  Inverted" : ""),
                EditorStyles.miniLabel);
            
            // Small preview
            if (decorator.vegetationMaskTexture != null)
            {
                Rect previewRect = EditorGUILayout.GetControlRect(GUILayout.Height(48));
                previewRect.x += 16;
                previewRect.width = 48;
                GUI.DrawTexture(previewRect, decorator.vegetationMaskTexture, ScaleMode.ScaleToFit);
            }
        }
        
        // Clear button
        if (hasMask)
        {
            EditorGUILayout.Space(2);
            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            
            Color orig = GUI.backgroundColor;
            GUI.backgroundColor = new Color(1f, 0.75f, 0.6f);
            if (GUILayout.Button("Clear Vegetation Mask", EditorStyles.miniButton, GUILayout.Width(150)))
            {
                if (EditorUtility.DisplayDialog("Clear Vegetation Mask",
                    "Remove the vegetation mask from the decorator?\n" +
                    "Generator layers will be kept.",
                    "Clear", "Cancel"))
                {
                    RGLDecoratorBridge.ClearVegetationMask(decorator);
                }
            }
            GUI.backgroundColor = orig;
            
            EditorGUILayout.EndHorizontal();
        }
        
        EditorGUILayout.EndVertical();
        EditorGUILayout.Space(3);
    }
}
