using System;
using System.IO;
using UnityEditor;
using UnityEngine;

static class PrefabPathUtil
{
    public static string NormalizeAssetPath(string path)
        => string.IsNullOrEmpty(path) ? path : path.Replace('\\', '/');

    public static string SanitizeFileName(string fileName)
    {
        if (string.IsNullOrEmpty(fileName))
            return "unnamed";

        // Unity hates dot-prefixed names for assets (hidden files)
        fileName = fileName.Trim();
        fileName = fileName.TrimStart('.');

        // Replace invalid filename chars
        foreach (var c in Path.GetInvalidFileNameChars())
            fileName = fileName.Replace(c, '_');

        // Also avoid some common problematic characters Unity tooling dislikes
        fileName = fileName.Replace(':', '_');

        if (string.IsNullOrEmpty(fileName))
            fileName = "unnamed";

        return fileName;
    }

    public static string MakeSafePrefabAssetPath(string prefabPath)
    {
        prefabPath = NormalizeAssetPath(prefabPath);

        // Force .prefab extension
        if (!prefabPath.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase))
            prefabPath += ".prefab";

        // Sanitize only the filename portion
        var dir = Path.GetDirectoryName(prefabPath)?.Replace('\\', '/') ?? "Assets";
        var file = Path.GetFileName(prefabPath);

        file = SanitizeFileName(file);

        // If filename lost extension due to sanitization, restore
        if (!file.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase))
            file = Path.ChangeExtension(file, ".prefab");

        return $"{dir}/{file}";
    }

    public static void EnsureAssetDirectoryExists(string assetPath)
    {
        assetPath = NormalizeAssetPath(assetPath);
        var dir = Path.GetDirectoryName(assetPath)?.Replace('\\', '/');
        if (string.IsNullOrEmpty(dir) || dir == "Assets")
            return;

        if (AssetDatabase.IsValidFolder(dir))
            return;

        var parts = dir.Split('/');
        var current = parts[0]; // "Assets"
        for (int i = 1; i < parts.Length; i++)
        {
            var next = $"{current}/{parts[i]}";
            if (!AssetDatabase.IsValidFolder(next))
                AssetDatabase.CreateFolder(current, parts[i]);
            current = next;
        }
    }

    public static void ValidatePrefabAssetPathOrThrow(string assetPath)
    {
        assetPath = NormalizeAssetPath(assetPath);

        if (string.IsNullOrEmpty(assetPath))
            throw new ArgumentException("Prefab path is null/empty.");

        if (!assetPath.StartsWith("Assets/", StringComparison.Ordinal))
            throw new ArgumentException($"Prefab path must start with 'Assets/': {assetPath}");

        if (assetPath.Contains("\\"))
            throw new ArgumentException($"Prefab path must not contain backslashes: {assetPath}");

        if (!assetPath.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException($"Prefab path must end with .prefab: {assetPath}");

        var fileName = Path.GetFileName(assetPath);
        if (fileName.StartsWith(".", StringComparison.Ordinal))
            throw new ArgumentException($"Prefab filename must not start with '.': {assetPath}");

        // Must exist at save time
        var dir = Path.GetDirectoryName(assetPath)?.Replace('\\', '/');
        if (!string.IsNullOrEmpty(dir) && dir != "Assets" && !AssetDatabase.IsValidFolder(dir))
            throw new ArgumentException($"Prefab directory does not exist: {dir}");
    }
}
