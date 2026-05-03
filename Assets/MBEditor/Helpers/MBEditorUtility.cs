using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MountAndBlade.Data;
using MountAndBlade.ModdingToolkit;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

public static class MBEditorUtility
{
    #region Model Prefab Creation (from BRF DataBase)

    /// <summary>
    /// Creates Model prefabs with MBModel component from ModBrfDataBase.
    /// Iterates all BrfModelGroups across every BRF asset and generates
    /// prefabs via the BRF data directly - no ModModelsDataBase needed.
    /// </summary>
    public static void CreateModelPrefabs(MBModule module,
        MBModuleImportContext moduleCtx = null,
        MBModuleImportContext nativeCtx = null)
    {
        string dbPath = MBPathHelpers.ModBRFDataBasePath(module.ID);
        var brfDataBase = AssetDatabase.LoadAssetAtPath<ModBrfDataBase>(dbPath);

        if (brfDataBase == null || brfDataBase.BrfCount == 0)
        {
            Debug.LogError($"BRF DataBase not found or empty for {module.ID} at {dbPath}");
            return;
        }

        brfDataBase.BuildLookups();

        int totalGroups = 0;
        int created = 0;
        int skipped = 0;

        foreach (var brf in brfDataBase.BrfAssets)
        {
            if (brf?.ModelGroups != null)
                totalGroups += brf.ModelGroups.Count;
        }

        int processed = 0;

        try
        {
            foreach (var brfData in brfDataBase.BrfAssets)
            {
                if (brfData?.ModelGroups == null) continue;

                string prefabDir = MBPathHelpers.ModSourcePrefabBRFPath(module.ID, brfData.BrfName);
                if (!Directory.Exists(prefabDir))
                    Directory.CreateDirectory(prefabDir);

                foreach (var group in brfData.ModelGroups)
                {
                    processed++;

                    if (group.MeshEntries.Count == 0)
                    {
                        skipped++;
                        continue;
                    }

                    if (processed % 50 == 0)
                    {
                        EditorUtility.DisplayProgressBar(
                            "Creating Model Prefabs",
                            $"Processing {group.GroupID} ({processed}/{totalGroups})",
                            (float)processed / totalGroups);
                    }

                    string safeName = PrefabPathUtil.SanitizeFileName(group.GroupID);
                    string prefabPath = Path.Combine(prefabDir, $"{safeName}.prefab");

                    if (File.Exists(prefabPath))
                    {
                        skipped++;
                        continue;
                    }

                    Material barrierOverride = IsDefaultBarrierProp(group.GroupID)
                        ? ImporterProps.BuildData.BarrierMaterial
                        : null;

                    var prefab = BuildModelPrefabFromGroup(
                        module.ID, brfData, group, prefabPath,brfDataBase,
                        moduleCtx, nativeCtx, barrierOverride);

                    if (prefab != null)
                        created++;
                }
            }
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log($"Model Prefabs: {created} created, {skipped} skipped " +
                  $"out of {totalGroups} groups for {module.ID}");
    }

    /// <summary>
    /// Build a single MBModel prefab from a BrfModelGroup.
    /// </summary>
    private static GameObject BuildModelPrefabFromGroup(
        string moduleName,
        MBBrfData brfData,
        BrfModelGroup group,
        string prefabPath,
        ModBrfDataBase brfDataBase,
        MBModuleImportContext moduleCtx,
        MBModuleImportContext nativeCtx,
        Material materialOverride = null)
    {
        var root = new GameObject(group.GroupID);

        var mbModel = root.AddComponent<MBModel>();
        mbModel.ModelID = group.GroupID;
        mbModel.BRFSource = brfData.BrfName;
        mbModel.PrefabID = group.GroupID;
        mbModel.SourceModule = moduleName;

        foreach (var meshEntry in group.MeshEntries)
        {
            if (meshEntry.UnityMesh == null) continue;

            Material material = materialOverride;

            if (material == null && !string.IsNullOrEmpty(meshEntry.MaterialName))
            {
                // 1. Same BRF (fast path)
                var matEntry = brfData.GetMaterialEntry(meshEntry.MaterialName);
                material = matEntry?.UnityMaterial;

                // 2. Cross-BRF via database
                if (material == null)
                {
                    var crossEntry = brfDataBase?.FindMaterialEntry(meshEntry.MaterialName);
                    material = crossEntry?.UnityMaterial;
                }

                // 3. Context fallbacks
                if (material == null)
                {
                    material = moduleCtx?.GetMaterial(meshEntry.MaterialName)
                               ?? nativeCtx?.GetMaterial(meshEntry.MaterialName);
                }
            }

            mbModel.AddMesh(meshEntry.UnityMesh, material, meshEntry.MaterialName, meshEntry.Flags);
        }

        foreach (var lodEntry in group.LodMeshEntries)
        {
            if (lodEntry.UnityMesh == null) continue;

            Material material = materialOverride ?? mbModel.PrimaryMaterial;

            if (materialOverride == null && !string.IsNullOrEmpty(lodEntry.MaterialName))
            {
                var matEntry = brfData.GetMaterialEntry(lodEntry.MaterialName);
                material = matEntry?.UnityMaterial ?? material;

                if (matEntry?.UnityMaterial == null)
                {
                    var crossEntry = brfDataBase?.FindMaterialEntry(lodEntry.MaterialName);
                    if (crossEntry?.UnityMaterial != null)
                        material = crossEntry.UnityMaterial;
                }
            }

            mbModel.AddLodMesh(lodEntry.UnityMesh, material, lodEntry.LodLevel);
        }

        if (group.CollisionBody != null)
        {
            mbModel.Collision = BrfDataPopulator.ConvertBodyEntryToCollision(group.CollisionBody);
        }

        mbModel.Compose();

        prefabPath = PrefabPathUtil.MakeSafePrefabAssetPath(prefabPath);

        var prefab = PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
        Object.DestroyImmediate(root);

        return prefab;
    }

    #endregion

    #region Module Data Management

    public static void CreateEditorModuleData(string modName)
    {
        MBModule module = ScriptableObject.CreateInstance<MBModule>();
        ModBrfDataBase brfDataBase = ScriptableObject.CreateInstance<ModBrfDataBase>();
        MBModuleIni moduleIni = ScriptableObject.CreateInstance<MBModuleIni>();

        module.ID = modName;
        module.Path = MBPathHelpers.ModPath(modName);
        module.BRFDataBase = brfDataBase;
        module.ModuleIni = moduleIni;

        MBModuleIniHelpers.ImportFromFile(module.ModuleIni,
            Path.Combine(MBPathHelpers.ModSourcePath(modName), "module.ini"));

        if (!Directory.Exists(MBPathHelpers.ModConfigsPath(modName)))
            Directory.CreateDirectory(MBPathHelpers.ModConfigsPath(modName));

        AssetDatabase.CreateAsset(brfDataBase, MBPathHelpers.ModBRFDataBasePath(modName));
        AssetDatabase.CreateAsset(module, MBPathHelpers.ModAssetPath(modName));
        AssetDatabase.CreateAsset(moduleIni, MBPathHelpers.ModINIPath(modName));
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
    }

    public static void RemoveModuleData(string modName)
    {
        if (Directory.Exists(MBPathHelpers.ModPath(modName)))
        {
            Directory.Delete(MBPathHelpers.ModPath(modName), true);
            AssetDatabase.Refresh();
        }
    }

    /// <summary>
    /// Reload module materials - swap textures using BRF database lookups
    /// instead of scanning global folders.
    /// </summary>
    public static void ReloadModuleMaterials(MBModule module)
    {
        string dbPath = MBPathHelpers.ModBRFDataBasePath(module.ID);
        var brfDb = AssetDatabase.LoadAssetAtPath<ModBrfDataBase>(dbPath);
        if (brfDb == null) return;

        brfDb.BuildLookups();

        ModBrfDataBase nativeDb = null;
        if (module.ID != "Native")
        {
            string nativeDbPath = MBPathHelpers.ModBRFDataBasePath("Native");
            nativeDb = AssetDatabase.LoadAssetAtPath<ModBrfDataBase>(nativeDbPath);
            nativeDb?.BuildLookups();
        }

        // Slot → BrfMaterialEntry field mapping for fallback resolution
        var slotMapping = new (string slot, System.Func<BrfMaterialEntry, string> getName)[]
        {
            ("_MainTex",      e => e.DiffuseA),
            ("_SpecGlossMap",  e => e.Spec),
            ("_BumpMap",       e => e.Bump),
        };

        // Build search paths for direct file lookups (used when DB entries have null UnityTexture)
        var searchFolders = new List<string>();
        string modResourcePath = MBPathHelpers.ModResourcePath(module.ID);
        if (Directory.Exists(modResourcePath))
            searchFolders.Add(modResourcePath);
        if (module.ID != "Native")
        {
            string nativeResourcePath = MBPathHelpers.ModResourcePath("Native");
            if (Directory.Exists(nativeResourcePath))
                searchFolders.Add(nativeResourcePath);
        }

        int reassigned = 0;

        foreach (var matEntry in brfDb.GetAllMaterials())
        {
            if (matEntry?.UnityMaterial == null) continue;

            var material = matEntry.UnityMaterial;

            foreach (var (slot, getName) in slotMapping)
            {
                if (!material.HasProperty(slot)) continue;

                var currentTex = material.GetTexture(slot);

                if (currentTex != null)
                {
                    // Slot has a texture - try to refresh it (e.g. reimported DDS)
                    Texture2D replacement = brfDb.FindTexture(currentTex.name);
                    replacement ??= nativeDb?.FindTexture(currentTex.name);

                    if (replacement != null && replacement != currentTex)
                    {
                        material.SetTexture(slot, replacement);
                        EditorUtility.SetDirty(material);
                        reassigned++;
                    }
                }
                else
                {
                    // Slot is EMPTY - try to assign using BRF metadata names
                    string brfTexName = getName(matEntry);
                    if (string.IsNullOrEmpty(brfTexName) || brfTexName == "none") continue;

                    string lookupName = Path.GetFileNameWithoutExtension(brfTexName);

                    // Try DB lookup first (fast)
                    Texture2D tex = brfDb.FindTexture(lookupName);
                    tex ??= nativeDb?.FindTexture(lookupName);

                    // Fallback: direct AssetDatabase search (handles textures
                    // that were copied after PopulateBrfData ran)
                    if (tex == null)
                        tex = FindTextureByAssetSearch(lookupName, searchFolders);

                    if (tex != null)
                    {
                        material.SetTexture(slot, tex);
                        EditorUtility.SetDirty(material);
                        reassigned++;
                    }
                }
            }
        }

        AssetDatabase.SaveAssets();

        if (reassigned > 0)
            Debug.Log($"ReloadModuleMaterials: reassigned {reassigned} texture slots for {module.ID}");
    }

    /// <summary>
    /// Search for a texture by name directly in the AssetDatabase.
    /// Used as a fallback when BrfTextureEntry.UnityTexture is null
    /// (texture file was copied after PopulateBrfData ran).
    /// </summary>
    private static Texture2D FindTextureByAssetSearch(string textureName, List<string> searchFolders)
    {
        if (searchFolders == null || searchFolders.Count == 0) return null;

        string[] guids = AssetDatabase.FindAssets(
            $"{textureName} t:Texture2D",
            searchFolders.ToArray());

        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            string fileName = Path.GetFileNameWithoutExtension(path);

            // Exact name match (FindAssets does substring matching)
            if (fileName.Equals(textureName, StringComparison.OrdinalIgnoreCase))
            {
                var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                if (tex != null) return tex;
            }
        }

        return null;
    }

    #endregion

    #region Prefab Getters

    public static GameObject GetPrefab(string moduleName, string prefabName)
    {
        string prefabPath = Path.Combine(MBPathHelpers.ModPrefabsPath(moduleName), $"{prefabName}.prefab");
        return AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
    }

    public static GameObject GetScenePropPrefab(string moduleName, string prefabName)
    {
        string prefabPath = Path.Combine(MBPathHelpers.ModPrefabScenePropsPath(moduleName), $"{prefabName}.prefab");
        return AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
    }

    public static GameObject GetFloraPrefab(string moduleName, string prefabName)
    {
        string prefabPath = Path.Combine(MBPathHelpers.ModPrefabFloraPath(moduleName), $"{prefabName}.prefab");
        return AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
    }

    public static GameObject GetItemPrefab(string moduleName, string prefabName)
    {
        string prefabPath = Path.Combine(MBPathHelpers.ModPrefabItemsPath(moduleName), $"{prefabName}.prefab");
        return AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
    }

    /// <summary>
    /// Get a model prefab by ID (searches Resource folder tree).
    /// </summary>
    public static GameObject GetModelPrefab(string moduleName, string modelID)
    {
        var resource = MBPathHelpers.ModResourcePath(moduleName);
        if (!Directory.Exists(resource)) return null;

        var prefabFiles = Directory.GetFiles(resource, "*.prefab", SearchOption.AllDirectories);

        foreach (var prefabFile in prefabFiles)
        {
            var prefabName = Path.GetFileNameWithoutExtension(prefabFile);
            if (prefabName.Equals(modelID, StringComparison.OrdinalIgnoreCase))
                return AssetDatabase.LoadAssetAtPath<GameObject>(prefabFile);
        }

        return null;
    }
    
    public static GameObject GetParticleSystemPrefab(string moduleName, string particleSystemId)
    {
        string prefabPath = Path.Combine(MBPathHelpers.ModPrefabParticlesDataPath(moduleName), $"{particleSystemId}.prefab");
        return AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
    }

    #endregion

    #region Utility Methods

    public static string[] ParseMeshName(string fileName)
    {
        return fileName.Split('.');
    }

    public static bool IsDefaultBarrierProp(string propId)
    {
        return Enum.IsDefined(typeof(BarrierType), propId);
    }

    public static bool DirectoryExistsWithFiles(string path,string searchPattern = "*.*") =>
        Directory.Exists(path) &&
        Directory.GetFiles(path, searchPattern, SearchOption.AllDirectories).Length > 0;

    public static bool ResourcesHasModels(string moduleID)
    {
        string resourcePath = MBPathHelpers.ModResourcePath(moduleID);
        if (!Directory.Exists(resourcePath)) return false;

        foreach (var brfDir in Directory.GetDirectories(resourcePath))
        {
            var prefabPath = MBPathHelpers.ModSourcePrefabBRFPath(moduleID, Path.GetFileName(brfDir));
            if (DirectoryExistsWithFiles(prefabPath))
                return true;
        }

        return false;
    }

    #endregion
}

public enum BarrierType
{
    barrier_2m,
    barrier_4m,
    barrier_8m,
    barrier_16m,
    barrier_20m,
    barrier_40m,
    barrier_box,
    barrier_capsule,
    barrier_cone,
    barrier_sphere,
    barrier_cylinder,
}
