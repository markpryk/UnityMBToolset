using System;
using System.Collections.Generic;
using System.IO;
using MountAndBlade.Data;
using UnityEditor;
using UnityEngine;
using WarbandParticles;
using Object = UnityEngine.Object;

/// <summary>
/// Generates entity prefabs (Items, SceneProps, Flora) by linking
/// ScriptableObject data to MBModel prefabs created from the BRF pipeline.
/// 
/// Uses ModBrfDataBase for model group lookups when a mesh name doesn't
/// directly match a prefab name (e.g., mesh "castle_wall.1" → group "castle_wall").
/// </summary>
public static class MBPrefabsGenerator
{
    private static MBModule _currentModule;
    private static ModBrfDataBase _brfDataBase;
    private static ModBrfDataBase _nativeBrfDataBase;
    private static Dictionary<string, GameObject> _modelPrefabCache;
    private static Dictionary<string, GameObject> _nativePrefabCache;
    private static Dictionary<string, MBParticleSystemData> _particleDataCache;
    private static Dictionary<string, GameObject> _particlePrefabCache;
    private static Dictionary<string, GameObject> _nativeParticlePrefabCache;
    private static bool _cacheValid;
    private static bool _onlyNew;

    #region Public API

    public static void GeneratePrefabs(MBModule module)
    {
        GeneratePrefabs(module, false);
    }

    /// <summary>
    /// Generate entity prefabs. When onlyNew is true, existing prefabs are skipped.
    /// </summary>
    public static void GeneratePrefabs(MBModule module, bool onlyNew)
    {
        _currentModule = module;
        _onlyNew = onlyNew;

        EnsureDirectories();
        RebuildCaches();

        // Particle prefabs must be created first - scene props reference them
        MBParticleSystemPrefabGenerator.ProcessParticleSystems(
            module, _brfDataBase, _nativeBrfDataBase,
            _modelPrefabCache, _nativePrefabCache);

        // Rebuild particle prefab cache after generation
        RebuildParticlePrefabCaches();

        ProcessItems();
        ProcessSceneProps();
        ProcessFlora();

        MBFloraLibraryBuilder.BuildLibrary(module);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
    }

    #endregion

    #region Directory Setup

    private static void EnsureDirectories()
    {
        string[] directories =
        {
            MBPathHelpers.ModPrefabItemsPath(_currentModule.ID),
            MBPathHelpers.ModPrefabScenePropsPath(_currentModule.ID),
            MBPathHelpers.ModPrefabFloraPath(_currentModule.ID),
            MBPathHelpers.ModPrefabParticlesDataPath(_currentModule.ID),
        };

        foreach (var dir in directories)
        {
            if (!Directory.Exists(dir))
                Directory.CreateDirectory(dir);
        }

        AssetDatabase.Refresh();
    }

    #endregion

    #region Cache Building

    private static void RebuildCaches()
    {
        // Load module BRF database
        string dbPath = MBPathHelpers.ModBRFDataBasePath(_currentModule.ID);
        _brfDataBase = AssetDatabase.LoadAssetAtPath<ModBrfDataBase>(dbPath);
        _brfDataBase?.BuildLookups();

        // Build module prefab cache
        _modelPrefabCache = BuildPrefabCache(MBPathHelpers.ModResourcePath(_currentModule.ID));

        // Load Native BRF database and prefab cache as fallback
        if (_currentModule.ID != "Native")
        {
            string nativeDbPath = MBPathHelpers.ModBRFDataBasePath("Native");
            _nativeBrfDataBase = AssetDatabase.LoadAssetAtPath<ModBrfDataBase>(nativeDbPath);
            _nativeBrfDataBase?.BuildLookups();

            _nativePrefabCache = BuildPrefabCache(MBPathHelpers.ModResourcePath("Native"));
        }
        else
        {
            _nativeBrfDataBase = null;
            _nativePrefabCache = null;
        }

        _cacheValid = true;

        Debug.Log($"[PrefabGenerator] Module cache: {_modelPrefabCache.Count} prefabs, " +
                  $"Native cache: {_nativePrefabCache?.Count ?? 0} prefabs, " +
                  $"BRF DB: {(_brfDataBase != null ? _brfDataBase.BrfCount + " BRFs" : "not found")}");

        // Build particle system data lookup (module + native fallback)
        _particleDataCache = BuildParticleDataCache(_currentModule);

        if (_currentModule.ID != "Native")
        {
            string nativeModPath = MBPathHelpers.ModAssetPath("Native");
            var nativeMod = AssetDatabase.LoadAssetAtPath<MBModule>(nativeModPath);
            if (nativeMod?.particleSystems != null)
            {
                foreach (var ps in nativeMod.particleSystems)
                {
                    if (ps == null) continue;
                    string key = ps.ParticleSystemID.StartsWith("psys_", StringComparison.OrdinalIgnoreCase)
                        ? ps.ParticleSystemID
                        : $"psys_{ps.ParticleSystemID}";
                    _particleDataCache.TryAdd(key, ps);
                }
            }
        }

        Debug.Log($"[PrefabGenerator] Particle data cache: {_particleDataCache.Count} systems");
    }

    /// <summary>
    /// Build particle prefab caches after MBParticleSystemPrefabGenerator has run.
    /// Must be called after ProcessParticleSystems so the prefabs exist on disk.
    /// </summary>
    private static void RebuildParticlePrefabCaches()
    {
        _particlePrefabCache = BuildPrefabCache(
            MBPathHelpers.ModPrefabParticlesDataPath(_currentModule.ID));

        if (_currentModule.ID != "Native")
        {
            _nativeParticlePrefabCache = BuildPrefabCache(
                MBPathHelpers.ModPrefabParticlesDataPath("Native"));
        }
        else
        {
            _nativeParticlePrefabCache = null;
        }

        Debug.Log($"[PrefabGenerator] Particle prefab cache: {_particlePrefabCache.Count} module, " +
                  $"{_nativeParticlePrefabCache?.Count ?? 0} native");
    }

    private static Dictionary<string, GameObject> BuildPrefabCache(string resourcePath)
    {
        var cache = new Dictionary<string, GameObject>(StringComparer.OrdinalIgnoreCase);

        if (Directory.Exists(resourcePath))
        {
            var guids = AssetDatabase.FindAssets("t:Prefab", new[] { resourcePath });

            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);

                if (prefab != null && !cache.ContainsKey(prefab.name))
                {
                    cache[prefab.name] = prefab;
                }
            }
        }

        return cache;
    }

    /// <summary>
    /// Resolve a mesh name to a model prefab.
    /// 
    /// Lookup order:
    ///   1. Module prefab cache (direct name match)
    ///   2. Module BRF database (mesh → group → prefab)
    ///   3. Native prefab cache (direct name match)
    ///   4. Native BRF database (mesh → group → prefab)
    /// </summary>
    private static GameObject ResolveModelPrefab(string meshName)
    {
        if (string.IsNullOrEmpty(meshName))
            return null;

        // 1. Module - direct match
        if (_modelPrefabCache.TryGetValue(meshName, out var prefab))
            return prefab;

        // 2. Module - BRF database lookup
        prefab = ResolveViaDatabase(meshName, _brfDataBase, _modelPrefabCache);
        if (prefab != null)
            return prefab;

        // 3. Native - direct match
        if (_nativePrefabCache != null && _nativePrefabCache.TryGetValue(meshName, out prefab))
            return prefab;

        // 4. Native - BRF database lookup
        prefab = ResolveViaDatabase(meshName, _nativeBrfDataBase, _nativePrefabCache);
        if (prefab != null)
            return prefab;

        return null;
    }

    /// <summary>
    /// Resolve a particle system ID to its prefab.
    /// Tries both "psys_xxx" and "xxx" forms, module → native fallback.
    /// </summary>
    private static GameObject ResolveParticlePrefab(string psysId)
    {
        if (string.IsNullOrEmpty(psysId))
            return null;

        string withoutPrefix = psysId.StartsWith("psys_", StringComparison.OrdinalIgnoreCase)
            ? psysId.Substring(5) : psysId;
        string withPrefix = psysId.StartsWith("psys_", StringComparison.OrdinalIgnoreCase)
            ? psysId : $"psys_{psysId}";

        // Module particle prefabs
        if (_particlePrefabCache.TryGetValue(psysId, out var prefab))
            return prefab;
        if (_particlePrefabCache.TryGetValue(withoutPrefix, out prefab))
            return prefab;
        if (_particlePrefabCache.TryGetValue(withPrefix, out prefab))
            return prefab;

        // Native fallback
        if (_nativeParticlePrefabCache != null)
        {
            if (_nativeParticlePrefabCache.TryGetValue(psysId, out prefab))
                return prefab;
            if (_nativeParticlePrefabCache.TryGetValue(withoutPrefix, out prefab))
                return prefab;
            if (_nativeParticlePrefabCache.TryGetValue(withPrefix, out prefab))
                return prefab;
        }

        return null;
    }

    private static GameObject ResolveViaDatabase(
        string meshName,
        ModBrfDataBase database,
        Dictionary<string, GameObject> prefabCache)
    {
        if (database == null || prefabCache == null)
            return null;

        // Try mesh entry → BaseName
        var meshEntry = database.FindMesh(meshName);
        if (meshEntry != null && !string.IsNullOrEmpty(meshEntry.BaseName))
        {
            if (prefabCache.TryGetValue(meshEntry.BaseName, out var prefab))
                return prefab;
        }

        // Try model group lookup
        var group = database.FindModelGroup(meshName);
        if (group != null && prefabCache.TryGetValue(group.GroupID, out var groupPrefab))
            return groupPrefab;

        return null;
    }

    #endregion

    #region Flora Processing

    private static void ProcessFlora()
    {
        var flora = _currentModule.flora;
        int createdCount = 0;
        int skippedCount = 0;
        int withModelCount = 0;

        string prefabDir = MBPathHelpers.ModPrefabFloraPath(_currentModule.ID);

        try
        {
            for (int i = 0; i < flora.Count; i++)
            {
                var floraData = flora[i];
                if (floraData == null)
                    continue;

                if (EditorUtility.DisplayCancelableProgressBar(
                        "Generating Flora Prefabs",
                        $"Processing: {floraData.name}",
                        (float)i / flora.Count))
                    break;

                // Skip existing if onlyNew mode
                if (_onlyNew)
                {
                    string existingPath = Path.Combine(prefabDir, $"{floraData.FloraID}.prefab");
                    if (File.Exists(existingPath))
                    {
                        skippedCount++;
                        continue;
                    }
                }

                GameObject modelPrefab = null;
                if (floraData.Meshes != null && floraData.Meshes.Count > 0)
                {
                    string meshName = floraData.Meshes[0].Mesh;
                    modelPrefab = ResolveModelPrefab(meshName);
                    if (modelPrefab != null)
                        withModelCount++;
                }

                CreateFloraPrefab(floraData, modelPrefab);
                createdCount++;
            }
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }

        Debug.Log($"[PrefabGenerator] Flora: {createdCount} created, {skippedCount} skipped ({withModelCount} with models)");
    }

    private static void CreateFloraPrefab(MBFloraData floraData, GameObject modelPrefab)
    {
        string path = Path.Combine(
            MBPathHelpers.ModPrefabFloraPath(_currentModule.ID),
            $"{floraData.FloraID}.prefab");

        var rootObject = new GameObject(floraData.FloraID);
        var flora = rootObject.AddComponent<MBFlora>();
        flora.FloraData = floraData;
        flora.PrefabID = floraData.FloraID;
        flora.SourceModule = _currentModule.ID;
        flora.FloraVariantID = 0; // ← Default variant explicitly set

        if (modelPrefab != null)
        {
            var modelRoot = new GameObject("Model");
            modelRoot.transform.SetParent(rootObject.transform);

            var modelInstance = (GameObject)PrefabUtility.InstantiatePrefab(modelPrefab, modelRoot.transform);

            var modelComponent = modelInstance.GetComponent<MBModel>();
            if (modelComponent == null)
            {
                modelComponent = modelInstance.AddComponent<MBModel>();
                modelComponent.ModelID = modelPrefab.name;
            }

            flora.Model = modelComponent;
        }

        PrefabUtility.SaveAsPrefabAsset(rootObject, path);
        Object.DestroyImmediate(rootObject);
    }

    #endregion

    #region SceneProps Processing

    private static void ProcessSceneProps()
    {
        var sceneProps = _currentModule.sceneProps;
        int createdCount = 0;
        int skippedCount = 0;
        int withModelCount = 0;
        int withParticlesCount = 0;
        int withLightsCount = 0;

        string prefabDir = MBPathHelpers.ModPrefabScenePropsPath(_currentModule.ID);

        try
        {
            for (int i = 0; i < sceneProps.Count; i++)
            {
                var sceneProp = sceneProps[i];
                if (sceneProp == null) continue;

                if (EditorUtility.DisplayCancelableProgressBar(
                        "Generating SceneProp Prefabs",
                        $"Processing: {sceneProp.name}",
                        (float)i / sceneProps.Count))
                    break;

                // Skip existing if onlyNew mode
                if (_onlyNew)
                {
                    string existingPath = Path.Combine(prefabDir, $"{sceneProp.PropID}.prefab");
                    if (File.Exists(existingPath))
                    {
                        skippedCount++;
                        continue;
                    }
                }

                // Resolve model
                GameObject modelPrefab = null;
                if (!string.IsNullOrEmpty(sceneProp.Mesh))
                {
                    modelPrefab = ResolveModelPrefab(sceneProp.Mesh);
                    if (modelPrefab != null)
                        withModelCount++;
                }

                // Parse trigger particle references
                List<TriggerParticleEntry> particleEntries = null;
                if (ScenePropParticleTriggerParser.HasParticleSystems(sceneProp.Triggers))
                {
                    particleEntries = ScenePropParticleTriggerParser.Parse(sceneProp.Triggers);
                    if (particleEntries.Count > 0)
                        withParticlesCount++;
                }

                // Parse trigger light references
                List<TriggerLightEntry> lightEntries = null;
                if (ScenePropLightParser.HasPointLights(sceneProp.Triggers))
                {
                    lightEntries = ScenePropLightParser.Parse(sceneProp.Triggers);
                    if (lightEntries.Count > 0)
                        withLightsCount++;
                }

                CreateScenePropPrefab(sceneProp, modelPrefab, particleEntries, lightEntries);
                createdCount++;
            }
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }

        Debug.Log($"[PrefabGenerator] SceneProps: {createdCount} created, {skippedCount} skipped " +
                  $"({withModelCount} with models, {withParticlesCount} with particles, {withLightsCount} with lights)");
    }

    private static void CreateScenePropPrefab(
        MBScenePropData sceneProp,
        GameObject modelPrefab,
        List<TriggerParticleEntry> particleEntries = null,
        List<TriggerLightEntry> lightEntries = null)
    {
        string path = Path.Combine(
            MBPathHelpers.ModPrefabScenePropsPath(_currentModule.ID),
            $"{sceneProp.PropID}.prefab");

        var rootObject = new GameObject(sceneProp.PropID);
        var prop = rootObject.AddComponent<MBSceneProp>();
        prop.ScenePropData = sceneProp;
        prop.PrefabID = sceneProp.PropID;
        prop.SourceModule = _currentModule.ID;

        if (modelPrefab != null)
        {
            var modelRoot = new GameObject("Model");
            modelRoot.transform.SetParent(rootObject.transform);

            var modelInstance = (GameObject)PrefabUtility.InstantiatePrefab(modelPrefab, modelRoot.transform);

            var modelComponent = modelInstance.GetComponent<MBModel>();
            if (modelComponent == null)
            {
                modelComponent = modelInstance.AddComponent<MBModel>();
                modelComponent.ModelID = modelPrefab.name;
            }

            prop.Model = modelComponent;
        }

        if (particleEntries != null && particleEntries.Count > 0)
        {
            AttachParticleSystems(rootObject, particleEntries);
        }

        if (lightEntries != null && lightEntries.Count > 0)
        {
            ScenePropLightAttacher.AttachLights(rootObject, lightEntries);

            if (prop.Model != null && prop.Model.ModelID == "light_sphere")
            {
                foreach (var renderer in prop.Model.Renderers)
                {
                    renderer.gameObject.SetActive(false);
                }
            }
        }

        path = PrefabPathUtil.MakeSafePrefabAssetPath(path);
        PrefabUtility.SaveAsPrefabAsset(rootObject, path);
        Object.DestroyImmediate(rootObject);
    }

    #endregion

    #region Items Processing

    private static void ProcessItems()
    {
        var items = _currentModule.items;
        int createdCount = 0;
        int skippedCount = 0;
        int withModelCount = 0;

        string prefabDir = MBPathHelpers.ModPrefabItemsPath(_currentModule.ID);

        try
        {
            for (int i = 0; i < items.Count; i++)
            {
                var item = items[i];
                if (item == null)
                    continue;

                if (EditorUtility.DisplayCancelableProgressBar(
                        "Generating Item Prefabs",
                        $"Processing: {item.name}",
                        (float)i / items.Count))
                    break;

                // Skip existing if onlyNew mode
                if (_onlyNew)
                {
                    string existingPath = Path.Combine(prefabDir, $"{item.ItemID}.prefab");
                    if (File.Exists(existingPath))
                    {
                        skippedCount++;
                        continue;
                    }
                }

                GameObject modelPrefab = null;
                if (item.Meshes != null && item.Meshes.Count > 0)
                {
                    string meshName = item.Meshes[0].MeshName;
                    modelPrefab = ResolveModelPrefab(meshName);
                    if (modelPrefab != null)
                        withModelCount++;
                }

                CreateItemPrefab(item, modelPrefab);
                createdCount++;
            }
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }

        Debug.Log($"[PrefabGenerator] Items: {createdCount} created, {skippedCount} skipped ({withModelCount} with models)");
    }

    private static void CreateItemPrefab(MBItemData item, GameObject modelPrefab)
    {
        string path = Path.Combine(
            MBPathHelpers.ModPrefabItemsPath(_currentModule.ID),
            $"{item.ItemID}.prefab");

        var rootObject = new GameObject(item.ItemID);
        var mbItem = rootObject.AddComponent<MBItem>();
        mbItem.ItemData = item;
        mbItem.PrefabID = item.ItemID;
        mbItem.SourceModule = _currentModule.ID;

        if (modelPrefab != null)
        {
            var modelRoot = new GameObject("Model");
            modelRoot.transform.SetParent(rootObject.transform);

            var modelInstance = (GameObject)PrefabUtility.InstantiatePrefab(modelPrefab, modelRoot.transform);

            var modelComponent = modelInstance.GetComponent<MBModel>();
            if (modelComponent == null)
            {
                modelComponent = modelInstance.AddComponent<MBModel>();
                modelComponent.ModelID = modelPrefab.name;
            }

            mbItem.Model = modelComponent;
        }

        PrefabUtility.SaveAsPrefabAsset(rootObject, path);
        Object.DestroyImmediate(rootObject);
    }

    #endregion

    #region Particle Helpers

    /// <summary>
    /// Build psys ID → MBParticleSystemData lookup from module data.
    /// Stores both "psys_xxx" and "xxx" forms for flexible matching.
    /// </summary>
    public static Dictionary<string, MBParticleSystemData> BuildParticleDataCache(MBModule module)
    {
        var cache = new Dictionary<string, MBParticleSystemData>(StringComparer.OrdinalIgnoreCase);

        if (module?.particleSystems == null) return cache;

        foreach (var psData in module.particleSystems)
        {
            if (psData == null) continue;

            string id = psData.ParticleSystemID;

            string withPrefix = id.StartsWith("psys_", StringComparison.OrdinalIgnoreCase)
                ? id : $"psys_{id}";
            cache.TryAdd(withPrefix, psData);

            string withoutPrefix = id.StartsWith("psys_", StringComparison.OrdinalIgnoreCase)
                ? id.Substring(5) : id;
            cache.TryAdd(withoutPrefix, psData);
        }

        return cache;
    }

    /// <summary>
    /// Attach particle system prefabs as children of a scene prop root.
    /// Instantiates existing prefabs from ModPrefabParticlesDataPath
    ///
    /// Lookup: module particle prefabs → native particle prefabs → fallback create from data
    ///
    /// Structure:
    ///   spr_torch (root)
    /// </summary>
    public static void AttachParticleSystems(
        GameObject root,
        List<TriggerParticleEntry> entries)
    {
        if (entries == null || entries.Count == 0) return;

        var particlesRoot = new GameObject("Particles");
        particlesRoot.transform.SetParent(root.transform);
        particlesRoot.transform.localPosition = Vector3.zero;

        var mbParticle = particlesRoot.AddComponent<MBParticleSystem>();
        mbParticle.TriggerEntries = new List<TriggerParticleEntry>(entries);

        int attached = 0;
        int fromPrefab = 0;

        foreach (var entry in entries)
        {
            WarbandParticleEmitter emitter = null;

            // Try to instantiate existing particle prefab (module → native)
            var psPrefab = ResolveParticlePrefab(entry.ParticleSystemID);

            if (psPrefab != null)
            {
                var psInstance = (GameObject)PrefabUtility.InstantiatePrefab(
                    psPrefab, particlesRoot.transform);

                psInstance.name = entry.ParticleSystemID;
                psInstance.transform.localPosition = entry.PositionOffsetUnity;

                emitter = psInstance.GetComponent<WarbandParticleEmitter>();
                if (emitter != null)
                {
                    // Override trigger-specific settings on the instance
                    var so = new SerializedObject(emitter);
                    so.FindProperty("_emitOnEnable").boolValue = true;
                    so.FindProperty("_previewInEditor").boolValue = true;

                    if (entry.EmitStrength > 0)
                        so.FindProperty("_burstStrength").intValue = entry.EmitStrength;

                    so.ApplyModifiedPropertiesWithoutUndo();
                }

                fromPrefab++;
            }
            else
            {
                // fallback create emitter from scratch 
                Debug.LogWarning($"[ParticleAttach] {root.name}: " +
                                 $"No prefab found for '{entry.ParticleSystemID}', creating from data");

                var psObject = new GameObject(entry.ParticleSystemID);
                psObject.transform.SetParent(particlesRoot.transform);
                psObject.transform.localPosition = entry.PositionOffsetUnity;

                emitter = psObject.AddComponent<WarbandParticleEmitter>();
                var so = new SerializedObject(emitter);

                MBParticleSystemData psData = null;
                _particleDataCache?.TryGetValue(entry.ParticleSystemID, out psData);

                so.FindProperty("_particleData").objectReferenceValue = psData;

                if (psData != null && !string.IsNullOrEmpty(psData.MeshName))
                {
                    ResolvePsysMeshAndMaterial(psData.MeshName, _brfDataBase, _nativeBrfDataBase,
                        out Mesh mesh, out Material material);

                    if (mesh != null)
                        so.FindProperty("_particleMesh").objectReferenceValue = mesh;
                    if (material != null)
                        so.FindProperty("_particleMaterial").objectReferenceValue = material;
                }

                so.FindProperty("_emitOnEnable").boolValue = true;
                so.FindProperty("_previewInEditor").boolValue = false;

                if (entry.EmitStrength > 0)
                    so.FindProperty("_burstStrength").intValue = entry.EmitStrength;

                so.ApplyModifiedPropertiesWithoutUndo();
            }

            if (emitter != null)
            {
                mbParticle.Emitters.Add(emitter);
                attached++;
            }
        }

        if (attached > 0)
        {
            Debug.Log($"[ParticleAttach] {root.name}: Attached {attached} emitters " +
                      $"({fromPrefab} from prefabs, {attached - fromPrefab} created)");
        }
    }

    /// <summary>
    /// Resolve a particle mesh name to Mesh + Material via BRF database.
    /// Same module → native fallback chain as model resolution.
    /// </summary>
    public static void ResolvePsysMeshAndMaterial(
        string meshName,
        ModBrfDataBase moduleDb,
        ModBrfDataBase nativeDb,
        out Mesh mesh,
        out Material material)
    {
        mesh = null;
        material = null;

        var meshEntry = moduleDb?.FindMesh(meshName);
        meshEntry ??= nativeDb?.FindMesh(meshName);

        if (meshEntry == null) return;

        mesh = meshEntry.UnityMesh;

        if (!string.IsNullOrEmpty(meshEntry.MaterialName))
        {
            var matEntry = moduleDb?.FindMaterialEntry(meshEntry.MaterialName);
            matEntry ??= nativeDb?.FindMaterialEntry(meshEntry.MaterialName);
            material = matEntry?.UnityMaterial;
        }
    }

    #endregion
}
