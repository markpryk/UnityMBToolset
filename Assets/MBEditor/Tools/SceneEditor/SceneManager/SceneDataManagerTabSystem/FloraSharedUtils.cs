using System.Collections.Generic;
using BDT.GUI.Helpers;
using UnityEditor;
using UnityEngine;

//  FloraSharedUtils - Single source of truth for shared flora helpers

/// <summary>
/// Shared utility methods used across Flora subtabs, the entry browser,
/// and any other flora-related UI. Centralises logic that was previously
/// duplicated in FloraLibrarySubTab, FloraPopulatorSubTab, and
/// FloraEntryBrowser.
/// </summary>
internal static class FloraSharedUtils
{
    //  Spawnable Check

    /// <summary>
    /// Returns true if this entry is fully registered with the terrain and
    /// ready to spawn.  Single source of truth - used by Library, Populator,
    /// and EntryBrowser.
    /// </summary>
    public static bool IsSpawnable(FloraLibraryEntry entry)
    {
        return entry != null
               && entry.IsRegistered
               && entry.PrototypeIndex >= 0
               && entry.HasRequiredAssets;
    }

    //  Entry Preview / Thumbnail

    /// <summary>
    /// Gets the best available preview texture for a flora library entry.
    /// Priority: BillboardTexture (billboard details) → PrototypePrefab
    /// asset preview → Prefab asset preview → white fallback.
    ///
    /// Callers should provide their own Dictionary cache for the lifetime
    /// of the owning editor window / subtab.
    /// </summary>
    public static Texture2D GetEntryPreview(
        FloraLibraryEntry entry,
        Dictionary<string, Texture2D> cache)
    {
        if (entry == null)
            return EditorGUIUtility.IconContent("console.warnicon").image as Texture2D
                   ?? Texture2D.whiteTexture;

        string key = entry.EntryID;
        if (string.IsNullOrEmpty(key)) return Texture2D.whiteTexture;

        if (cache.TryGetValue(key, out var cached) && cached != null && cached)
            return cached;

        Texture2D preview = null;

        // Billboard details → use the texture directly (project asset, stable)
        if (entry.Category == FloraCategory.Detail
            && entry.DetailMode == DetailMode.Billboard
            && entry.BillboardTexture != null)
        {
            preview = entry.BillboardTexture;
            cache[key] = preview; // project asset - no copy needed
            return preview;
        }

        // Prefer PrototypePrefab (the lightweight registered prefab)
        if (preview == null && entry.PrototypePrefab != null)
        {
            preview = AssetPreview.GetAssetPreview(entry.PrototypePrefab);
            if (preview == null
                && AssetPreview.IsLoadingAssetPreview(entry.PrototypePrefab.GetInstanceID()))
                return null;
        }

        // Fallback to full scene prefab
        if (preview == null && entry.Prefab != null)
        {
            preview = AssetPreview.GetAssetPreview(entry.Prefab);
            if (preview == null
                && AssetPreview.IsLoadingAssetPreview(entry.Prefab.GetInstanceID()))
                return null;
        }

        // Make a persistent copy - AssetPreview textures are transient and
        // Unity destroys them between frames, causing flicker.
        if (preview != null)
        {
            var copy = new Texture2D(preview.width, preview.height, preview.format, false);
            copy.SetPixels(preview.GetPixels());
            copy.Apply(false, false);
            copy.hideFlags = HideFlags.HideAndDontSave;
            cache[key] = copy;
            return copy;
        }

        return Texture2D.whiteTexture;
    }

    //  Status Helpers

    /// <summary>
    /// Gets the colour for the status dot displayed in the library list.
    /// </summary>
    public static Color GetStatusColor(FloraLibraryEntry entry)
    {
        if (!entry.HasRequiredAssets) return UIColors.Red;
        if (entry.IsRegistered && entry.PrototypeIndex >= 0) return UIColors.Green;
        if (entry.IsRegistered) return UIColors.Yellow;
        return UIColors.Disable;
    }

    /// <summary>
    /// Gets a tooltip string for the status dot.
    /// </summary>
    public static string GetStatusTooltip(FloraLibraryEntry entry)
    {
        if (!entry.HasRequiredAssets)
            return "Missing required assets (prototype or billboard)";
        if (entry.IsRegistered && entry.PrototypeIndex >= 0)
            return $"Synced - prototype index {entry.PrototypeIndex}";
        if (entry.IsRegistered)
            return "Registered - needs terrain sync";
        return "Not registered";
    }

    //  Grass Mask Export

    /// <summary>
    /// Bakes a float[,] intensity map to a PNG at the grass_mask path.
    /// Returns the project-relative asset path for loading as Texture2D.
    /// 
    /// Moved here from FloraPopulatorSubTab so spawn logic isn't mixed
    /// with UI code.
    /// </summary>
    public static string ExportGrassMask(float[,] mask, string moduleName, string sceneName)
    {
        int w = mask.GetLength(0);
        int h = mask.GetLength(1);

        var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
        for (int y = 0; y < h; y++)
        for (int x = 0; x < w; x++)
            tex.SetPixel(x, y, new Color(mask[x, y], 0, 0, 1));
        tex.Apply();

        string absPath = MBPathHelpers.ModSceneDataGrassMaskPath(moduleName, sceneName);

        // Ensure directory exists
        string dir = System.IO.Path.GetDirectoryName(absPath);
        if (!System.IO.Directory.Exists(dir))
            System.IO.Directory.CreateDirectory(dir);

        System.IO.File.WriteAllBytes(absPath, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);

        // Convert to project-relative path for AssetDatabase
        string relPath = "Assets" + absPath.Substring(Application.dataPath.Length);
        AssetDatabase.ImportAsset(relPath, ImportAssetOptions.ForceUpdate);

        // Set import settings: Default type, linear, uncompressed, R channel carries data
        var importer = (TextureImporter)AssetImporter.GetAtPath(relPath);
        if (importer != null)
        {
            importer.textureType = TextureImporterType.Default;
            importer.sRGBTexture = false;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.isReadable = true;
            importer.SaveAndReimport();
        }

        return relPath;
    }
}
