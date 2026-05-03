using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MountAndBlade.Data;
using MountAndBlade.Data.Editor; // JsonHelper, MBScritableObjectDataCreator
using Newtonsoft.Json;
using UnityEditor;
using UnityEngine;

namespace MountAndBlade.ModdingToolkit
{
    /// <summary>
    /// Executes BRF synchronization operations - applies selected diffs from BrfDiffResult
    /// by reusing the existing import pipeline (BrfDataImporter, BrfDataPopulator, etc.).
    /// </summary>
    public static class BrfSyncExecutor
    {
        // BRF SYNC - Phase 1 (Asset Pipeline) partial re-run

        /// <summary>
        /// Apply selected BRF changes for a single BRF.
        /// Overwrites data.json, re-creates materials, and re-populates the MBBrfData asset.
        /// </summary>
        public static void ApplyBrfChanges(
            MBModule module,
            MBBrfData brfData,
            BrfDiffResult diff,
            string tempExportFolder,
            MBModuleImportContext moduleContext,
            MBModuleImportContext nativeContext)
        {
            if (brfData == null || diff == null) return;

            string brfFolder = brfData.FolderPath;
            if (string.IsNullOrEmpty(brfFolder) || !Directory.Exists(brfFolder))
            {
                Debug.LogError($"BRF folder not found: {brfFolder}");
                return;
            }

            try
            {
                EditorUtility.DisplayProgressBar("BRF Sync", $"Syncing {diff.BrfName}...", 0.1f);

                // 1. Replace data.json with the new version
                string oldJson = Path.Combine(brfFolder, "data.json");
                string newJson = Path.Combine(tempExportFolder, "data.json");

                if (File.Exists(newJson))
                {
                    File.Copy(newJson, oldJson, true);
                    Debug.Log($"Updated data.json for {diff.BrfName}");
                }

                EditorUtility.DisplayProgressBar("BRF Sync", "Copying new mesh files...", 0.2f);

                // 2. Copy new/modified OBJ mesh files
                CopyNewFiles(tempExportFolder, brfFolder, "Meshes", "*.obj");

                EditorUtility.DisplayProgressBar("BRF Sync", "Copying new collision files...", 0.3f);

                // 3. Copy new/modified collision body files
                CopyNewFiles(tempExportFolder, brfFolder, "Bodies", "*.obj");

                EditorUtility.DisplayProgressBar("BRF Sync", "Creating materials...", 0.5f);

                // 4. Re-create materials using BrfDataImporter (same as wizard Phase 1 Step 3)
                var importer = new BrfDataImporter(
                    brfData.ModuleName, brfFolder, moduleContext, nativeContext);

                if (importer.LoadDataJson())
                {
                    var materialsPath = Path.Combine(brfFolder, "Materials");
                    if (!Directory.Exists(materialsPath))
                        Directory.CreateDirectory(materialsPath);

                    var materials = importer.CreateAllMaterials(materialsPath);
                    Debug.Log($"Created/updated {materials.Count} materials for {diff.BrfName}");

                    // Also re-export compatibility JSON files
                    importer.ExportMaterialDataJson(brfFolder);
                    importer.ExportMeshDataJson(brfFolder);
                }

                EditorUtility.DisplayProgressBar("BRF Sync", "Importing new assets...", 0.65f);

                // CRITICAL: Refresh AFTER copying files and materials, but BEFORE PopulateBrfData
                // PopulateBrfData calls AssetDatabase.LoadAssetAtPath<Mesh>() for each .obj
                // Without refresh, newly copied .obj files aren't imported and return null
                AssetDatabase.Refresh();

                EditorUtility.DisplayProgressBar("BRF Sync", "Populating BRF data...", 0.75f);

                // 5. Re-populate MBBrfData asset (same as wizard Phase 1 Step 4)
                // NOW the .obj meshes are imported so UnityMesh references will be valid
                var populateResult = BrfDataPopulator.PopulateBrfData(
                    brfData, brfFolder, moduleContext, nativeContext);

                EditorUtility.SetDirty(brfData);
                AssetDatabase.SaveAssets();

                EditorUtility.DisplayProgressBar("BRF Sync", "Creating model prefabs...", 0.90f);

                // 6. Create model prefabs for new mesh groups
                // Skips existing prefabs automatically
                MBEditorUtility.CreateModelPrefabs(module, moduleContext, nativeContext);

                Debug.Log($"BRF Sync complete for {diff.BrfName}: " +
                          $"{populateResult.MeshCount} meshes, " +
                          $"{populateResult.MaterialCount} materials, " +
                          $"{populateResult.ModelGroupCount} model groups");
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        }

        /// <summary>
        /// Rebuild the ModBrfDataBase after BRF changes have been applied.
        /// </summary>
        public static void RebuildBrfDataBase(MBModule module)
        {
            string dbPath = MBPathHelpers.ModBRFDataBasePath(module.ID);
            var brfDataBase = AssetDatabase.LoadAssetAtPath<ModBrfDataBase>(dbPath);

            if (brfDataBase == null)
            {
                Debug.LogWarning($"BRF DataBase not found at {dbPath}, skipping rebuild.");
                return;
            }

            brfDataBase.Clear();
            brfDataBase.ModuleName = module.ID;

            string resourcePath = MBPathHelpers.ModResourcePath(module.ID);

            foreach (var brfFolder in Directory.GetDirectories(resourcePath))
            {
                var resourceName = Path.GetFileName(brfFolder);
                string dataJsonPath = Path.Combine(brfFolder, "data.json");
                string brfDataPath = Path.Combine(brfFolder, $"{resourceName}.asset");

                if (!File.Exists(dataJsonPath) || !File.Exists(brfDataPath)) continue;

                var brfData = AssetDatabase.LoadAssetAtPath<MBBrfData>(brfDataPath);
                if (brfData != null)
                {
                    brfDataBase.AddBrf(brfData);
                }
            }

            brfDataBase.BuildLookups();
            EditorUtility.SetDirty(brfDataBase);
            AssetDatabase.SaveAssets();

            Debug.Log($"BRF DataBase rebuilt: {brfDataBase.BrfCount} BRF assets");
        }

        // JSON SYNC - Phase 2 (Data Conversion) partial re-run

        /// <summary>
        /// Re-run the Python converter for a specific data type and incrementally
        /// update ScriptableObjects. Does not clear existing data.
        /// </summary>
        public static int SyncJsonData(
            MBModule module,
            string converterFileName,
            string outputJsonName,
            Action<string, MBModule> importAction)
        {
            if (module == null) return 0;

            string moduleSystemPath = module.ModuleSystemPath;
            if (string.IsNullOrEmpty(moduleSystemPath) || !Directory.Exists(moduleSystemPath))
            {
                Debug.LogError("Module System Path not configured. Set it in the Module Importer.");
                return 0;
            }

            string exportPath = MBPathHelpers.ModDataBaseJsonDirectoryPath(module.ID);
            if (!Directory.Exists(exportPath))
                Directory.CreateDirectory(exportPath);

            string converterFolder = MBPathHelpers.MBDataBaseJsonConvertersPath();
            string exePath = Path.Combine(converterFolder, "ms_converter.exe");

            if (!File.Exists(exePath))
            {
                Debug.LogError($"ms_converter.exe not found: {exePath}");
                return 0;
            }

            // Extract converter name from script name (e.g. "convert_items.py" -> "items")
            string converterName = converterFileName.Replace("convert_", "").Replace(".py", "");

            try
            {
                EditorUtility.DisplayProgressBar("JSON Sync", $"Running {converterName} converter...", 0.3f);

                var startInfo = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = exePath,
                    Arguments = $"--converter {converterName} --source \"{moduleSystemPath}\" --output \"{exportPath}\" --quiet",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };

                string stdout, stderr;
                int exitCode;

                using (var process = System.Diagnostics.Process.Start(startInfo))
                {
                    var stderrTask = process.StandardError.ReadToEndAsync();
                    stdout = process.StandardOutput.ReadToEnd();

                    if (!process.WaitForExit(60_000)) // 60s timeout
                    {
                        process.Kill();
                        Debug.LogError($"Converter {converterName} timed out after 60s - killed.");
                        return 0;
                    }

                    stderr = stderrTask.Result;
                    exitCode = process.ExitCode;
                }

                if (exitCode != 0)
                {
                    Debug.LogError($"Converter {converterName} failed (exit code {exitCode}):\n{stderr}\n{stdout}");
                    return 0;
                }

                // ms_converter puts the JSON directly in exportPath
                string targetJson = Path.Combine(exportPath, outputJsonName);

                if (File.Exists(targetJson))
                {
                    Debug.Log($"✓ Converted: {converterName} → {outputJsonName}");
                }
                else
                {
                    Debug.LogError($"Output JSON not found: {targetJson}");
                    return 0;
                }

                EditorUtility.DisplayProgressBar("JSON Sync", "Updating ScriptableObjects...", 0.7f);

                // Incrementally import - the import action handles creation/update
                importAction(targetJson, module);

                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();

                return 1;
            }
            catch (Exception ex)
            {
                Debug.LogError($"JSON sync failed: {ex.Message}\n{ex.StackTrace}");
                return 0;
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        }

        /// <summary>
        /// Incrementally sync scene props from JSON.
        /// Creates new MBScenePropData SOs for added entries, updates existing ones.
        /// </summary>
        public static void SyncSceneProps(string jsonPath, MBModule module)
        {
            string json = File.ReadAllText(jsonPath);
            var propsJson = JsonHelper.FromJson<MBScenePropJsonDecoder.ScenePropJsonData>(json);
            var decoded = MBScenePropJsonDecoder.DecodeSceneProps(propsJson);

            string outputPath = Path.Combine(
                MBPathHelpers.ModDataBaseDirectoryPath(module.ID), "SceneProps");

            if (!Directory.Exists(outputPath))
                Directory.CreateDirectory(outputPath);

            // Build lookup of existing scene props by ID
            var existingLookup = new Dictionary<string, MBScenePropData>(StringComparer.OrdinalIgnoreCase);
            foreach (var existing in module.sceneProps)
            {
                if (existing != null && !string.IsNullOrEmpty(existing.PropID))
                    existingLookup[existing.PropID] = existing;
            }

            int added = 0, updated = 0;

            foreach (var prop in decoded)
            {
                if (existingLookup.TryGetValue(prop.propId, out var existingAsset))
                {
                    // Update existing
                    PopulateSceneProp(prop, existingAsset);
                    EditorUtility.SetDirty(existingAsset);
                    updated++;
                }
                else
                {
                    // Create new
                    var newAsset = ScriptableObject.CreateInstance<MBScenePropData>();
                    PopulateSceneProp(prop, newAsset);

                    string sanitizedName = SanitizeFileName(prop.propId);
                    string assetPath = Path.Combine(outputPath, $"{sanitizedName}.asset");
                    AssetDatabase.CreateAsset(newAsset, assetPath);

                    module.sceneProps.Add(newAsset);
                    added++;
                }
            }

            EditorUtility.SetDirty(module);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"Scene Props Sync: {added} added, {updated} updated");
        }

        /// <summary>
        /// Incrementally sync items from JSON.
        /// Creates new MBItemData SOs for added entries, updates existing ones.
        /// </summary>
        public static void SyncItems(string jsonPath, MBModule module)
        {
            string json = File.ReadAllText(jsonPath);
            var itemsJson = JsonHelper.FromJson<MBItemJsonDecoder.ItemJsonData>(json);
            var decoded = MBItemJsonDecoder.DecodeItems(itemsJson);

            string outputPath = Path.Combine(
                MBPathHelpers.ModDataBaseDirectoryPath(module.ID), "Items");

            if (!Directory.Exists(outputPath))
                Directory.CreateDirectory(outputPath);

            // Build lookup of existing items by ID
            var existingLookup = new Dictionary<string, MBItemData>(StringComparer.OrdinalIgnoreCase);
            foreach (var existing in module.items)
            {
                if (existing != null && !string.IsNullOrEmpty(existing.ItemID))
                    existingLookup[existing.ItemID] = existing;
            }

            int added = 0, updated = 0;

            foreach (var item in decoded)
            {
                if (existingLookup.TryGetValue(item.itemId, out var existingAsset))
                {
                    // Update existing
                    PopulateItem(item, existingAsset);
                    EditorUtility.SetDirty(existingAsset);
                    updated++;
                }
                else
                {
                    // Create new
                    var newAsset = ScriptableObject.CreateInstance<MBItemData>();
                    PopulateItem(item, newAsset);

                    string sanitizedName = SanitizeFileName(item.itemId);
                    string assetPath = Path.Combine(outputPath, $"{sanitizedName}.asset");
                    AssetDatabase.CreateAsset(newAsset, assetPath);

                    module.items.Add(newAsset);
                    added++;
                }
            }

            EditorUtility.SetDirty(module);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"Items Sync: {added} added, {updated} updated");
        }

        /// <summary>
        /// Incrementally sync scenes from JSON.
        /// Creates new MBSceneData SOs for added entries, updates existing ones.
        /// </summary>
        public static void SyncScenes(string jsonPath, MBModule module)
        {
            string json = File.ReadAllText(jsonPath);
            var scenesJson = JsonHelper.FromJson<MBSceneJsonDecoder.SceneJsonData>(json);
            var decoded = MBSceneJsonDecoder.DecodeScenes(scenesJson);

            string outputPath = Path.Combine(
                MBPathHelpers.ModDataBaseDirectoryPath(module.ID), "Scenes");

            if (!Directory.Exists(outputPath))
                Directory.CreateDirectory(outputPath);

            // Build lookup of existing scenes by ID
            var existingLookup = new Dictionary<string, MBSceneData>(StringComparer.OrdinalIgnoreCase);
            foreach (var existing in module.scenes)
            {
                if (existing != null && !string.IsNullOrEmpty(existing.SceneID))
                    existingLookup[existing.SceneID] = existing;
            }

            int added = 0, updated = 0;

            foreach (var scene in decoded)
            {
                if (existingLookup.TryGetValue(scene.sceneId, out var existingAsset))
                {
                    // Update existing
                    PopulateScene(scene, existingAsset);
                    EditorUtility.SetDirty(existingAsset);
                    updated++;
                }
                else
                {
                    // Create new
                    var newAsset = ScriptableObject.CreateInstance<MBSceneData>();
                    PopulateScene(scene, newAsset);

                    string sanitizedName = SanitizeFileName(scene.sceneId);
                    string assetPath = Path.Combine(outputPath, $"{sanitizedName}.asset");
                    AssetDatabase.CreateAsset(newAsset, assetPath);

                    module.scenes.Add(newAsset);
                    added++;
                }
            }

            EditorUtility.SetDirty(module);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"Scenes Sync: {added} added, {updated} updated");
        }

        // TEXTURE SYNC - Copy new/modified DDS textures into BRF folders

        /// <summary>
        /// Copy selected DDS textures from the M&B source folder into each BRF's
        /// Textures/ subfolder. Runs DDSPostprocessor on changed folders and
        /// reloads materials to reassign texture references.
        /// </summary>
        public static int SyncTextures(TextureDiffResult diff, MBModule module)
        {
            if (diff == null) return 0;

            var selected = diff.Entries.Where(e => e.Selected && e.HasChanges).ToList();
            if (selected.Count == 0) return 0;

            int copied = 0;
            var affectedFolders = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            try
            {
                for (int i = 0; i < selected.Count; i++)
                {
                    var entry = selected[i];

                    EditorUtility.DisplayProgressBar("Texture Sync",
                        $"Copying {entry.TextureName} ({i + 1}/{selected.Count})...",
                        (float)i / selected.Count);

                    // Ensure destination folder exists
                    string destFolder = Path.GetDirectoryName(entry.DestPath);
                    if (!string.IsNullOrEmpty(destFolder) && !Directory.Exists(destFolder))
                        Directory.CreateDirectory(destFolder);

                    try
                    {
                        File.Copy(entry.SourcePath, entry.DestPath, true);
                        copied++;

                        // Track the BRF's Textures folder for post-processing
                        if (!string.IsNullOrEmpty(destFolder))
                            affectedFolders.Add(destFolder);
                    }
                    catch (Exception ex)
                    {
                        Debug.LogWarning($"Failed to copy texture {entry.TextureName}: {ex.Message}");
                    }
                }

                // Post-process DDS files (fix dimensions, mipmaps, etc.)
                EditorUtility.DisplayProgressBar("Texture Sync", "Post-processing DDS files...", 0.8f);
                foreach (var folder in affectedFolders)
                {
                    DDSPostprocessor.Procces(folder);
                }

                EditorUtility.DisplayProgressBar("Texture Sync", "Refreshing assets...", 0.9f);
                AssetDatabase.Refresh();

                // Reload materials so texture slots get reassigned
                EditorUtility.DisplayProgressBar("Texture Sync", "Reassigning material textures...", 0.95f);
                MBEditorUtility.ReloadModuleMaterials(module);
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }

            Debug.Log($"Texture Sync: {copied} textures copied across {affectedFolders.Count} BRF folders");
            return copied;
        }

        // PREFAB SYNC - Phase 3 (Model Pipeline) partial re-run

        /// <summary>
        /// Regenerate prefabs only for entities whose mesh names match the affected set.
        /// </summary>
        public static void RegenerateAffectedPrefabs(MBModule module, HashSet<string> affectedMeshNames)
        {
            if (module == null || affectedMeshNames == null || affectedMeshNames.Count == 0)
            {
                Debug.Log("No affected meshes - skipping prefab regeneration.");
                return;
            }

            int regenerated = 0;

            try
            {
                EditorUtility.DisplayProgressBar("Prefab Sync", "Finding affected prefabs...", 0.1f);

                // Check scene props
                var affectedSceneProps = module.sceneProps
                    .Where(sp => sp != null && affectedMeshNames.Contains(sp.Mesh))
                    .ToList();

                // Check items
                var affectedItems = module.items
                    .Where(item => item != null &&
                                   item.Meshes.Any(m => affectedMeshNames.Contains(m.MeshName)))
                    .ToList();

                int total = affectedSceneProps.Count + affectedItems.Count;
                if (total == 0)
                {
                    Debug.Log("No prefabs reference the affected meshes.");
                    return;
                }

                Debug.Log($"Found {affectedSceneProps.Count} scene props and {affectedItems.Count} items to regenerate.");

                // Regenerate by calling full prefab generation (it handles individual prefabs)
                EditorUtility.DisplayProgressBar("Prefab Sync", "Regenerating prefabs...", 0.5f);
                MBPrefabsGenerator.GeneratePrefabs(module);
                regenerated = total;
            }
            finally
            {
                EditorUtility.ClearProgressBar();
                Debug.Log($"Prefab regeneration complete: {regenerated} affected entities.");
            }
        }

        // HELPERS

        /// <summary>
        /// Collect all mesh names that were added or modified in BRF diffs.
        /// Used to determine which prefabs need regeneration.
        /// </summary>
        public static HashSet<string> CollectAffectedMeshNames(List<BrfDiffResult> diffs)
        {
            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var diff in diffs)
            {
                foreach (var mesh in diff.Meshes)
                {
                    if (mesh.Status is DiffStatus.Added or DiffStatus.Modified)
                        names.Add(mesh.Name);
                }
            }

            return names;
        }

        private static void CopyNewFiles(string sourceRoot, string destRoot, string subfolder, string pattern)
        {
            string sourceDir = Path.Combine(sourceRoot, subfolder);
            string destDir = Path.Combine(destRoot, subfolder);

            if (!Directory.Exists(sourceDir)) return;

            if (!Directory.Exists(destDir))
                Directory.CreateDirectory(destDir);

            foreach (var file in Directory.GetFiles(sourceDir, pattern))
            {
                string destFile = Path.Combine(destDir, Path.GetFileName(file));
                File.Copy(file, destFile, true);
            }
        }

        // Mirrors PopulateScenePropAsset from MBScritableObjectDataCreator
        private static void PopulateSceneProp(MBScenePropJsonDecoder.DecodedScenePropData data, MBScenePropData asset)
        {
            asset.PropID = data.propId;
            asset.Flags = data.flags;
            asset.Mesh = data.mesh;
            asset.Collision = data.collision;
            asset.Triggers = data.triggers;
            asset.TypeName = data.typeName;
            asset.HitPoints = data.hitPoints;
            asset.UseTime = data.useTime;
        }

        // Mirrors PopulateItemAsset from MBScritableObjectDataCreator
        private static void PopulateItem(MBItemJsonDecoder.DecodedItemData data, MBItemData asset)
        {
            asset.ItemID = data.itemId;
            asset.ItemName = data.itemName;
            asset.Price = data.price;
            asset.Flags = data.flags;
            asset.Capabilities = data.capabilities;
            asset.Stats = data.stats;
            asset.ModifierBits = data.modifierBits;
            asset.TriggersCode = data.triggersCode;
            asset.FactionIds = data.factionIds;
            asset.Meshes = data.meshes.Select(m => new MBItemData.ItemMesh
            {
                MeshName = m.meshName,
                ItemModifier = m.itemModifier,
                UsageType = m.usageType
            }).ToList();
        }

        // Mirrors PopulateSceneAsset from MBScritableObjectDataCreator
        private static void PopulateScene(MBSceneJsonDecoder.DecodedSceneData data, MBSceneData asset)
        {
            asset.SceneID = data.sceneId;
            asset.Flags = data.flags;
            asset.MeshName = data.meshName;
            asset.BodyName = data.bodyName;
            asset.MinPos = data.minPos;
            asset.MaxPos = data.maxPos;
            asset.WaterLevel = data.waterLevel;
            asset.TerrainCode = data.terrainCode;
            asset.AccessibleScenes = data.accessibleScenes;
            asset.Chests = data.chests;
            asset.OuterTerrainMesh = data.outerTerrainMesh;
        }

        private static string SanitizeFileName(string name)
        {
            if (string.IsNullOrEmpty(name)) return "unnamed";

            char[] invalid = Path.GetInvalidFileNameChars();
            foreach (char c in invalid)
                name = name.Replace(c, '_');

            return name;
        }

        }
    }
