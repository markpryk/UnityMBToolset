using System.Collections.Generic;
using System.IO;
using MountAndBlade.Data;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Builds an MBFloraLibrary from module flora data.
///
/// Key behaviors:
///   1. Expands variants: each FloraMesh entry → separate FloraLibraryEntry
///      tree_a with Meshes[0]=trunk_green, Meshes[1]=trunk_snowy → 2 entries
///
///   2. Preserves sub-meshes: model prefab children (MeshFilter+MeshRenderer)
///      are copied into the prototype prefab maintaining the full structure
///      that Unity terrain needs.
///
/// Model prefab from BRF pipeline:
///   tree_a (root, MBModel)
///
/// Generated prototype prefab:
///   tree_a_v0_proto (root)
///
/// Output folder: .../Prefabs/FloraPrototypes/
/// </summary>
public static class MBFloraLibraryBuilder
{
    private static string _prototypeDir;

    public static MBFloraLibrary BuildLibrary(MBModule module)
    {
        if (module == null || module.flora == null)
        {
            Debug.LogError("[FloraLibraryBuilder] Module or flora list is null.");
            return null;
        }

        _prototypeDir = MBPathHelpers.ModFloraPrototypesPath(module.ID);

        if (!Directory.Exists(_prototypeDir))
            Directory.CreateDirectory(_prototypeDir);

        AssetDatabase.Refresh();

        string libraryPath =MBPathHelpers.ModFloraLibraryAssetPath(module.ID);

        var library = AssetDatabase.LoadAssetAtPath<MBFloraLibrary>(libraryPath);
        if (library == null)
        {
            library = ScriptableObject.CreateInstance<MBFloraLibrary>();
            AssetDatabase.CreateAsset(library, libraryPath);
        }

        library.ModuleID = module.ID;
        library.Entries.Clear();

        MBFloraVariantResolver.ClearCaches();

        int totalEntries = 0;
        int prototypesCreated = 0;
        int texturesResolved = 0;

        try
        {

            for (int i = 0; i < module.flora.Count; i++)
            {
                var floraData = module.flora[i];
                if (floraData == null) continue;

                if (EditorUtility.DisplayCancelableProgressBar(
                        "Building Flora Library",
                        $"Processing: {floraData.FloraID}",
                        (float)i / module.flora.Count))
                    break;

                FloraDecodedFlags decoded = default;
                bool hasFlags = !string.IsNullOrEmpty(floraData.Flags);
                FloraCategory category = FloraCategory.Detail;
                DetailMode detailMode = DetailMode.Mesh;
                string terrainConditions = "";
                string behaviorSummary = "";
                bool hasPointUp = false;
                bool hasAlignToGround = false;
                float density = 0.1f;

                if (hasFlags)
                {
                    decoded = FloraFlagsDecoder.DecodeAll(floraData.Flags);
                    category = ClassifyFlora(decoded);
                    terrainConditions = FloraFlagsDecoder.GetTerrainDescription(decoded.Terrain);
                    behaviorSummary = BuildBehaviorSummary(decoded.Behavior);
                    hasPointUp = decoded.Behavior.PointUp;
                    hasAlignToGround = decoded.Behavior.AlignWithGround;
                    density = MapDensity(decoded.Density);

                    if (category == FloraCategory.Detail)
                        detailMode = decoded.Behavior.PointUp ? DetailMode.Billboard : DetailMode.Mesh;
                }

                GameObject floraPrefab = null;
                string floraPrefabPath = FindFloraPrefab(module.ID, floraData.FloraID);
                if (!string.IsNullOrEmpty(floraPrefabPath))
                    floraPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(floraPrefabPath);

                int meshCount = floraData.Meshes?.Count ?? 0;
                if (meshCount == 0)
                {
                    // No meshes - create a single entry with no prototype
                    var entry = CreateBaseEntry(floraData, 0, null, category, detailMode,
                        terrainConditions, behaviorSummary, hasPointUp, hasAlignToGround, density, decoded);
                    entry.Prefab = floraPrefab;
                    library.Entries.Add(entry);
                    totalEntries++;
                    continue;
                }

                for (int v = 0; v < meshCount; v++)
                {
                    string meshName = floraData.Meshes[v].Mesh;
                    string collisionMesh = floraData.Meshes[v].MeshCollision;

                    // Resolve model FIRST - determines both category and detail mode
                    GameObject modelPrefab = null;
                    if (!string.IsNullOrEmpty(meshName))
                        modelPrefab = MBFloraVariantResolver.ResolveModelPrefab(module.ID, meshName);

                    //   Has collision mesh OR multiple sub-meshes → Tree (TreePrototype)
                    //   Single simple mesh, no collision         → Detail (DetailPrototype)
                    //   No model resolved + fkf_point_up         → Detail/Billboard
                    FloraCategory variantCategory = category; // start with flag-based hint
                    DetailMode variantDetailMode = detailMode;

                    bool hasCollision = !string.IsNullOrEmpty(collisionMesh) && collisionMesh != "0";
                    bool hasGeometry = modelPrefab != null &&
                        modelPrefab.GetComponentInChildren<MeshFilter>() != null;
                    int subMeshCount = 0;

                    if (hasGeometry)
                    {
                        var meshFilters = modelPrefab.GetComponentsInChildren<MeshFilter>(false);
                        subMeshCount = 0;
                        foreach (var mf in meshFilters)
                        {
                            if (mf.sharedMesh != null)
                                subMeshCount++;
                        }

                        // Model-based override: complex model → Tree, simple → Detail
                        if (hasCollision || subMeshCount > 1)
                        {
                            variantCategory = FloraCategory.Tree;
                            variantDetailMode = DetailMode.Mesh;
                        }
                        else
                        {
                            variantCategory = FloraCategory.Detail;
                            variantDetailMode = DetailMode.Mesh;
                        }
                    }
                    else if (!hasGeometry && hasPointUp)
                    {
                        // No model + point_up → billboard fallback
                        variantCategory = FloraCategory.Detail;
                        variantDetailMode = DetailMode.Billboard;
                    }

                    var entry = CreateBaseEntry(floraData, v, meshName, variantCategory, variantDetailMode,
                        terrainConditions, behaviorSummary, hasPointUp, hasAlignToGround, density, decoded);
                    entry.Prefab = floraPrefab;

                    if (modelPrefab != null)
                    {
                        if (variantDetailMode == DetailMode.Billboard)
                        {
                            entry.BillboardTexture = ExtractDiffuseTexture(modelPrefab);
                            if (entry.BillboardTexture != null) texturesResolved++;
                        }
                        else
                        {
                            entry.PrototypePrefab = CreatePrototypePrefab(
                                entry.EntryID, modelPrefab, variantCategory);
                            if (entry.PrototypePrefab != null) prototypesCreated++;
                        }
                    }

                    library.Entries.Add(entry);
                    totalEntries++;
                }
            }
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        library.RebuildLookups();
        EditorUtility.SetDirty(library);
        AssetDatabase.SaveAssets();

        Debug.Log($"[FloraLibraryBuilder] Built library for '{module.ID}': " +
                  $"{totalEntries} variant entries from {module.flora.Count} flora " +
                  $"(Detail: {library.DetailCount}, Tree: {library.TreeCount})\n" +
                  $"  Prototypes: {prototypesCreated}, Billboard textures: {texturesResolved}");

        return library;
    }

    // Entry Creation

    private static FloraLibraryEntry CreateBaseEntry(
        MBFloraData floraData, int variantIndex, string meshName,
        FloraCategory category, DetailMode detailMode,
        string terrainConditions, string behaviorSummary,
        bool hasPointUp, bool hasAlignToGround, float density,
        FloraDecodedFlags decoded = default)
    {
        string entryId = variantIndex == 0
            ? floraData.FloraID
            : $"{floraData.FloraID}_v{variantIndex}";

        var entry = new FloraLibraryEntry
        {
            EntryID = entryId,
            FloraData = floraData,
            VariantIndex = variantIndex,
            MeshName = meshName ?? "",
            Category = category,
            DetailMode = detailMode,
            Biomes = ConvertToBiome(decoded.Terrain),
            TerrainConditions = terrainConditions,
            BehaviorSummary = behaviorSummary,
            HasPointUp = hasPointUp,
            HasAlignToGround = hasAlignToGround,
            DetailDensity = density,
        };

        ApplyDefaultSizes(entry);
        return entry;
    }

    /// <summary>
    /// Convert M&B terrain condition flags to FloraBiome flags.
    /// </summary>
    private static FloraBiome ConvertToBiome(FloraTerrainConditions terrain)
    {
        FloraBiome biome = FloraBiome.None;
        if (terrain.Plain || terrain.PlainForest)   biome |= FloraBiome.Plain;
        if (terrain.Steppe || terrain.SteppeForest) biome |= FloraBiome.Steppe;
        if (terrain.Snow || terrain.SnowForest)     biome |= FloraBiome.Snow;
        if (terrain.Desert || terrain.DesertForest) biome |= FloraBiome.Desert;
        return biome;
    }

    // Prototype Prefab Generation

    /// <summary>
    /// Creates a terrain-ready prototype prefab with a SINGLE MeshFilter + MeshRenderer
    /// on the root GameObject.
    ///
    /// Unity terrain (both DetailPrototype and TreePrototype) reads exactly ONE
    /// MeshFilter + MeshRenderer from the prototype prefab root. Multiple children
    /// with separate renderers are rejected ("no valid mesh renderer").
    ///
    /// For multi sub-mesh models from the BRF pipeline:
    ///   model_root (MBModel)
    ///
    /// We combine into a single Mesh with sub-mesh indices:
    ///   flora_proto (root)
    ///
    /// The combined mesh asset is saved alongside the prefab as:
    ///   FloraPrototypes/{entryId}_proto_mesh.asset
    /// </summary>
    private static GameObject CreatePrototypePrefab(string entryId, GameObject modelPrefab, FloraCategory category)
    {
        // Collect all valid MeshFilter + MeshRenderer pairs from the model hierarchy
        var sourceMeshFilters = modelPrefab.GetComponentsInChildren<MeshFilter>(false);

        var validPairs = new List<(MeshFilter mf, MeshRenderer mr)>();
        foreach (var mf in sourceMeshFilters)
        {
            if (mf.sharedMesh == null) continue;
            var mr = mf.GetComponent<MeshRenderer>();
            if (mr != null)
                validPairs.Add((mf, mr));
        }

        if (validPairs.Count == 0)
        {
            Debug.LogWarning($"[FloraLibraryBuilder] No valid mesh pairs for '{entryId}'");
            return null;
        }

        string protoPath = Path.Combine(_prototypeDir, $"{entryId}_proto.prefab");
        protoPath = MBPathHelpers.ConvertToUnityPath(protoPath);
        string meshAssetPath = Path.Combine(_prototypeDir, $"{entryId}_proto_mesh.asset");
        meshAssetPath = MBPathHelpers.ConvertToUnityPath(meshAssetPath);
        
        Mesh finalMesh;
        Material[] finalMaterials;

        if (validPairs.Count == 1)
        {
            finalMesh = validPairs[0].mf.sharedMesh;
            finalMaterials = validPairs[0].mr.sharedMaterials;
        }
        else
        {
            finalMesh = CombineSubMeshes(entryId, validPairs, modelPrefab.transform,
                out finalMaterials);

            if (finalMesh == null)
            {
                Debug.LogWarning($"[FloraLibraryBuilder] Mesh combine failed for '{entryId}'");
                return null;
            }

            var existingMesh = AssetDatabase.LoadAssetAtPath<Mesh>(meshAssetPath);
            if (existingMesh != null)
            {
                EditorUtility.CopySerialized(finalMesh, existingMesh);
                finalMesh = existingMesh;
            }
            else
            {
                AssetDatabase.CreateAsset(finalMesh, meshAssetPath);
            }
        }

        var protoRoot = new GameObject($"{entryId}_proto");
        
        bool useLoD = category == FloraCategory.Tree;

        if (useLoD)
        {
            //   root (LODGroup)
            //
            // Required for terrain trees to support rotation, billboard, lighting.
            
            var lod0 = new GameObject("LOD0");
            lod0.transform.SetParent(protoRoot.transform);
            lod0.transform.localPosition = Vector3.zero;
            lod0.transform.localRotation = Quaternion.identity;
            lod0.transform.localScale = Vector3.one;

            var mf = lod0.AddComponent<MeshFilter>();
            mf.sharedMesh = finalMesh;

            var mr = lod0.AddComponent<MeshRenderer>();
            mr.sharedMaterials = finalMaterials;

            var lodGroup = protoRoot.AddComponent<LODGroup>();
            lodGroup.SetLODs(new LOD[]
            {
                new LOD(0.05f, new Renderer[] { mr })
            });
            lodGroup.RecalculateBounds();
        }
        else
        {
            //   root (MeshFilter + MeshRenderer)
            //
            // DetailPrototype reads mesh/material directly from root.
            // No LODGroup - details are GPU instanced by the terrain system.
            
            var mf = protoRoot.AddComponent<MeshFilter>();
            mf.sharedMesh = finalMesh;

            var mr = protoRoot.AddComponent<MeshRenderer>();
            mr.sharedMaterials = finalMaterials;
        }

        var savedPrefab = PrefabUtility.SaveAsPrefabAsset(protoRoot, protoPath);
        Object.DestroyImmediate(protoRoot);

        return savedPrefab;
    }

    /// <summary>
    /// Combines multiple child meshes into a single Mesh with sub-mesh indices.
    /// Each source MeshFilter becomes one sub-mesh, each source material maps to
    /// the corresponding sub-mesh index in sharedMaterials.
    ///
    /// Transforms are baked relative to the model root so geometry is in local space.
    /// </summary>
    private static Mesh CombineSubMeshes(
        string entryId,
        List<(MeshFilter mf, MeshRenderer mr)> pairs,
        Transform modelRoot,
        out Material[] materials)
    {
        var materialList = new List<Material>();
        var combineInstances = new List<CombineInstance>();

        foreach (var (mf, mr) in pairs)
        {
            var srcMesh = mf.sharedMesh;

            // Each source mesh can itself have multiple sub-meshes (material slots).
            // We expand all of them so the final mesh has one sub-mesh per material.
            int srcSubMeshCount = srcMesh.subMeshCount;
            var srcMaterials = mr.sharedMaterials;

            // Compute transform relative to model root to bake child offsets into vertices
            Matrix4x4 relativeTransform = modelRoot.worldToLocalMatrix * mf.transform.localToWorldMatrix;

            for (int s = 0; s < srcSubMeshCount; s++)
            {
                combineInstances.Add(new CombineInstance
                {
                    mesh = srcMesh,
                    subMeshIndex = s,
                    transform = relativeTransform,
                });

                // Map material: use source material at this sub-mesh index if available
                Material mat = (s < srcMaterials.Length) ? srcMaterials[s] : null;
                materialList.Add(mat);
            }
        }

        materials = materialList.ToArray();

        // CombineMeshes with mergeSubMeshes=false keeps each CombineInstance as a separate sub-mesh
        var combined = new Mesh();
        combined.name = $"{entryId}_combined";
        combined.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        combined.CombineMeshes(combineInstances.ToArray(), false, true);

        combined.RecalculateBounds();

        return combined;
    }

    // Texture Extraction

    private static Texture2D ExtractDiffuseTexture(GameObject modelPrefab)
    {
        var renderer = modelPrefab.GetComponentInChildren<MeshRenderer>();
        if (renderer == null || renderer.sharedMaterial == null)
            return null;

        var mat = renderer.sharedMaterial;

        if (mat.HasProperty("_MainTex"))
        {
            var tex = mat.GetTexture("_MainTex") as Texture2D;
            if (tex != null) return tex;
        }
        if (mat.HasProperty("_DiffuseMap"))
        {
            var tex = mat.GetTexture("_DiffuseMap") as Texture2D;
            if (tex != null) return tex;
        }
        if (mat.HasProperty("_BaseMap"))
        {
            var tex = mat.GetTexture("_BaseMap") as Texture2D;
            if (tex != null) return tex;
        }
        if (mat.HasProperty("MB_DiffuseMap"))
        {
            var tex = mat.GetTexture("MB_DiffuseMap") as Texture2D;
            if (tex != null) return tex;
        }

        return null;
    }

    // Classification & Helpers

    private static FloraCategory ClassifyFlora(FloraDecodedFlags decoded)
    {
        // Flag-based hint (overridden per-variant by model inspection)
        if (decoded.Type.IsGrass) return FloraCategory.Detail;
        if (decoded.Type.IsTree) return FloraCategory.Tree;
        if (decoded.Type.IsRock) return FloraCategory.Tree; // rocks use TreePrototype
        if (decoded.Behavior.PointUp) return FloraCategory.Detail;
        return FloraCategory.Detail; // default to detail
    }

    private static string BuildBehaviorSummary(FloraBehaviorFlags behavior)
    {
        var flags = new List<string>();
        if (behavior.AlignWithGround) flags.Add("AlignGround");
        if (behavior.PointUp) flags.Add("PointUp");
        if (behavior.OnGreenGround) flags.Add("OnGreen");
        if (behavior.Guarantee) flags.Add("Guarantee");
        if (behavior.Snowy) flags.Add("Snowy");
        if (behavior.HasColonyProps) flags.Add("Colony");
        return flags.Count > 0 ? string.Join(", ", flags) : "None";
    }

    private static float MapDensity(int mbDensity)
    {
        if (mbDensity <= 0) return 0.1f;
        return (mbDensity / 65535f)*100;
    }

    private static void ApplyDefaultSizes(FloraLibraryEntry entry)
    {
        switch (entry.Category)
        {
            case FloraCategory.Detail:
                if (entry.DetailMode == DetailMode.Billboard)
                {
                    entry.MinWidth = 0.5f; entry.MaxWidth = 1.5f;
                    entry.MinHeight = 0.3f; entry.MaxHeight = 0.8f;
                }
                else
                {
                    entry.MinWidth = 0.8f; entry.MaxWidth = 1.5f;
                    entry.MinHeight = 0.8f; entry.MaxHeight = 1.5f;
                }
                break;
            case FloraCategory.Tree:
                entry.MinWidth = 0.8f; entry.MaxWidth = 1.2f;
                entry.MinHeight = 0.8f; entry.MaxHeight = 1.2f;
                entry.BendFactor = 0.5f;
                break;
        }
    }

    private static string FindFloraPrefab(string moduleId, string floraId)
    {
        string modulePath = Path.Combine(MBPathHelpers.ModPrefabFloraPath(moduleId), $"{floraId}.prefab");
        if (File.Exists(modulePath)) return modulePath;

        if (moduleId != "Native")
        {
            string nativePath = Path.Combine(MBPathHelpers.ModPrefabFloraPath("Native"), $"{floraId}.prefab");
            if (File.Exists(nativePath)) return nativePath;
        }

        return null;
    }
}
