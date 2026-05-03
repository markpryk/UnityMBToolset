using UnityEngine;
using UnityEditor;

public class DDSUtility
{
    public static void FixSRGBNormalSettings(Texture2D texture)
    {
        if (texture == null)
        {
            return;
        }
        
        string assetPath = AssetDatabase.GetAssetPath(texture);
        if (string.IsNullOrEmpty(assetPath))
        {
            Debug.LogError("Cannot find asset path for texture");
            return;
        }
        
        // Get the importer
        AssetImporter importer = AssetImporter.GetAtPath(assetPath);
        if (importer == null)
        {
            Debug.LogError("Failed to get importer");
            return;
        }
        
        string importerType = importer.GetType().Name;
        
        if (importerType == "IHVImageFormatImporter")
        {
            HandleIHVImageFormatImporter(importer);
        }
        else if (importerType == "TextureImporter")
        {
            TextureImporter texImporter = (TextureImporter)importer;
            texImporter.textureType = TextureImporterType.NormalMap;
            texImporter.SaveAndReimport();
        }
        else
        {
            Debug.LogError($"Unsupported importer type: {importerType}");
        }
    }
    
    private static void HandleIHVImageFormatImporter(AssetImporter importer)
    {
        // For IHVImageFormatImporter we need to use reflection or SerializedObject
        // since Unity doesn't expose these properties directly
        
        SerializedObject serializedImporter = new SerializedObject(importer);
        
        // Try to find sRGB property (may not exist for all formats)
        SerializedProperty sRGBProp = serializedImporter.FindProperty("m_sRGBTexture");
        if (sRGBProp != null)
        {
            sRGBProp.boolValue = false;
            serializedImporter.ApplyModifiedProperties();
            importer.SaveAndReimport();
        }
        else
        {
            Debug.Log("sRGB property not found for this importer");
        }
    }
}