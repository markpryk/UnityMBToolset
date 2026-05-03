using System.Collections.Generic;
using MountAndBlade.Data;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Resolves flora mesh names to model prefabs using the same
/// module → BRF database → Native fallback chain as MBPrefabsGenerator.
/// 
/// Used by MBFloraEditor for variant switching and ImporterProps for
/// scene placement with correct variant models.
/// 
/// This is a lightweight resolver that doesn't require MBPrefabsGenerator's
/// full cache infrastructure - it builds minimal caches on demand.
/// </summary>
public static class MBFloraVariantResolver
{
    // Per-module cache: moduleID → { meshName → prefab }
    private static readonly Dictionary<string, Dictionary<string, GameObject>> _prefabCaches
        = new Dictionary<string, Dictionary<string, GameObject>>();

    private static readonly Dictionary<string, ModBrfDataBase> _brfDatabases
        = new Dictionary<string, ModBrfDataBase>();

    /// <summary>
    /// Resolve a mesh name to a model prefab, using module → Native fallback.
    /// Caches results for repeated lookups within the same editor session.
    /// </summary>
    public static GameObject ResolveModelPrefab(string moduleId, string meshName)
    {
        if (string.IsNullOrEmpty(meshName))
            return null;

        // Try module
        var prefab = ResolveInModule(moduleId, meshName);
        if (prefab != null)
            return prefab;

        // Try Native fallback
        if (moduleId != "Native")
        {
            prefab = ResolveInModule("Native", meshName);
            if (prefab != null)
                return prefab;
        }

        return null;
    }

    /// <summary>
    /// Clear all cached data. Call when modules change or BRF databases are rebuilt.
    /// </summary>
    public static void ClearCaches()
    {
        _prefabCaches.Clear();
        _brfDatabases.Clear();
    }


    private static GameObject ResolveInModule(string moduleId, string meshName)
    {
        var cache = GetOrBuildPrefabCache(moduleId);
        var brfDb = GetOrLoadBrfDatabase(moduleId);

        // Direct name match (meshName == prefab.name)
        if (cache.TryGetValue(meshName, out var prefab))
            return prefab;

        // BRF database lookup (mesh → group → prefab)
        if (brfDb != null)
        {
            // Try mesh entry's BaseName
            var meshEntry = brfDb.FindMesh(meshName);
            if (meshEntry != null && !string.IsNullOrEmpty(meshEntry.BaseName))
            {
                if (cache.TryGetValue(meshEntry.BaseName, out prefab))
                    return prefab;

                Debug.Log($"[VariantResolver] '{meshName}' → mesh.BaseName='{meshEntry.BaseName}' but no prefab in cache");
            }
            else
            {
                Debug.Log($"[VariantResolver] '{meshName}' → FindMesh returned {(meshEntry == null ? "null" : $"entry with BaseName='{meshEntry.BaseName}'")}");
            }

            // Try model group
            var group = brfDb.FindModelGroup(meshName);
            if (group != null)
            {
                if (cache.TryGetValue(group.GroupID, out prefab))
                    return prefab;

                Debug.Log($"[VariantResolver] '{meshName}' → group.GroupID='{group.GroupID}' but no prefab in cache");
            }
            else
            {
                Debug.Log($"[VariantResolver] '{meshName}' → FindModelGroup returned null");
            }
        }
        else
        {
            Debug.LogWarning($"[VariantResolver] No BRF database found for module '{moduleId}'");
        }

        Debug.LogWarning($"[VariantResolver] FAILED to resolve '{meshName}' in module '{moduleId}'. Cache has {cache.Count} prefabs.");
        return null;
    }

    private static Dictionary<string, GameObject> GetOrBuildPrefabCache(string moduleId)
    {
        if (_prefabCaches.TryGetValue(moduleId, out var existing))
            return existing;

        var cache = new Dictionary<string, GameObject>(System.StringComparer.OrdinalIgnoreCase);

        // Search both resource path and prefabs path
        // Model prefabs from BRF pipeline live under ModPrefabsPath (in BRF subfolders)
        // Flora/SceneProp prefabs live under ModPrefabsPath as well
        var searchPaths = new List<string>();

        string resourcePath = MBPathHelpers.ModResourcePath(moduleId);
        if (System.IO.Directory.Exists(resourcePath))
            searchPaths.Add(resourcePath);

        string prefabsPath = MBPathHelpers.ModPrefabsPath(moduleId);
        if (System.IO.Directory.Exists(prefabsPath))
            searchPaths.Add(prefabsPath);

        if (searchPaths.Count > 0)
        {
            var guids = AssetDatabase.FindAssets("t:Prefab", searchPaths.ToArray());
            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab != null && !cache.ContainsKey(prefab.name))
                    cache[prefab.name] = prefab;
            }
        }

        _prefabCaches[moduleId] = cache;
        return cache;
    }

    private static ModBrfDataBase GetOrLoadBrfDatabase(string moduleId)
    {
        if (_brfDatabases.TryGetValue(moduleId, out var existing))
            return existing;

        string dbPath = MBPathHelpers.ModBRFDataBasePath(moduleId);
        var db = AssetDatabase.LoadAssetAtPath<ModBrfDataBase>(dbPath);
        db?.BuildLookups();

        _brfDatabases[moduleId] = db;
        return db;
    }
}
