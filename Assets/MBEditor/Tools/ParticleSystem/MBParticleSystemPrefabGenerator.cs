using System.Collections.Generic;
using System.IO;
using MountAndBlade.Data;
using UnityEditor;
using UnityEngine;
using WarbandParticles;

public static partial class MBParticleSystemPrefabGenerator
{
    /// <summary>
    /// Generate prefabs for all particle systems in the module.
    /// Each prefab gets a WarbandParticleEmitter component with the
    /// MBParticleSystemData assigned, plus mesh and material resolved
    /// from the BRF database.
    /// </summary>
    public static void ProcessParticleSystems(
        MBModule module,
        ModBrfDataBase brfDataBase,
        ModBrfDataBase nativeBrfDataBase,
        Dictionary<string, GameObject> modelPrefabCache,
        Dictionary<string, GameObject> nativePrefabCache)
    {
        var particleSystems = module.particleSystems;
        if (particleSystems == null || particleSystems.Count == 0)
            return;

        string outputDir = Path.Combine(
            MBPathHelpers.ModPrefabParticlesDataPath(module.ID));

        if (!Directory.Exists(outputDir))
            Directory.CreateDirectory(outputDir);

        int createdCount = 0;
        int withMeshCount = 0;

        try
        {
            for (int i = 0; i < particleSystems.Count; i++)
            {
                var psData = particleSystems[i];
                if (psData == null) continue;

                if (EditorUtility.DisplayCancelableProgressBar(
                        "Generating Particle System Prefabs",
                        $"Processing: {psData.ParticleSystemID}",
                        (float)i / particleSystems.Count))
                    break;

                // Resolve mesh and material from BRF data
                Mesh particleMesh = null;
                Material particleMaterial = null;

                if (!string.IsNullOrEmpty(psData.MeshName))
                {
                    ResolveParticleMeshAndMaterial(
                        psData.MeshName,
                        brfDataBase, nativeBrfDataBase,
                        out particleMesh, out particleMaterial);

                    if (particleMesh != null)
                        withMeshCount++;
                }

                CreateParticleSystemPrefab(
                    module.ID, psData, particleMesh, particleMaterial, outputDir);

                createdCount++;
            }
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }

        Debug.Log($"[PrefabGenerator] Created {createdCount} particle system prefabs " +
                  $"({withMeshCount} with meshes)");
    }

    /// <summary>
    /// Create a single particle system prefab.
    /// Structure:
    ///   ParticleSystemID (root)
    ///          - ParticleData → MBParticleSystemData SO
    ///          - Mesh → resolved from BRF
    ///          - Material → resolved from BRF
    /// </summary>
    private static void CreateParticleSystemPrefab(
        string moduleName,
        MBParticleSystemData psData,
        Mesh particleMesh,
        Material particleMaterial,
        string outputDir)
    {
        string safeName = PrefabPathUtil.SanitizeFileName(psData.ParticleSystemID);
        string path = Path.Combine(outputDir, $"{safeName}.prefab");
        path = PrefabPathUtil.MakeSafePrefabAssetPath(path);

        var rootObject = new GameObject(psData.ParticleSystemID);

        var emitter = rootObject.AddComponent<WarbandParticleEmitter>();

        // Use serialized fields since the properties may trigger initialization
        // that we don't want during prefab construction
        var so = new SerializedObject(emitter);
        so.FindProperty("_particleData").objectReferenceValue = psData;

        if (particleMesh != null)
            so.FindProperty("_particleMesh").objectReferenceValue = particleMesh;

        if (particleMaterial != null)
            so.FindProperty("_particleMaterial").objectReferenceValue = particleMaterial;

        // Defaults for prefab - preview off so it doesn't run on load
        so.FindProperty("_previewInEditor").boolValue = true;
        so.FindProperty("_emitOnEnable").boolValue = true;

        so.ApplyModifiedPropertiesWithoutUndo();

        PrefabUtility.SaveAsPrefabAsset(rootObject, path);
        Object.DestroyImmediate(rootObject);
    }

    /// <summary>
    /// Resolve a particle mesh name to a Unity Mesh and its associated Material.
    /// 
    /// Particle meshes in M&B are referenced by name in the BRF data.
    /// The mesh entry contains a MaterialName which we then resolve through
    /// the same fallback chain used elsewhere.
    /// 
    /// Lookup order (same pattern as model material resolution):
    ///   1. Module BRF database - find mesh by name → get Mesh + MaterialName
    ///   2. Native BRF database - fallback for shared assets
    /// </summary>
    private static void ResolveParticleMeshAndMaterial(
        string meshName,
        ModBrfDataBase moduleDb,
        ModBrfDataBase nativeDb,
        out Mesh mesh,
        out Material material)
    {
        mesh = null;
        material = null;

        // Try module database first
        var meshEntry = moduleDb?.FindMesh(meshName);

        // Fallback to native
        if (meshEntry == null)
            meshEntry = nativeDb?.FindMesh(meshName);

        if (meshEntry == null)
            return;

        mesh = meshEntry.UnityMesh;

        // Resolve associated material
        if (!string.IsNullOrEmpty(meshEntry.MaterialName))
        {
            // Same BRF → cross-BRF → native chain
            var matEntry = moduleDb?.FindMaterialEntry(meshEntry.MaterialName);
            matEntry ??= nativeDb?.FindMaterialEntry(meshEntry.MaterialName);

            material = matEntry?.UnityMaterial;
        }
    }
}
