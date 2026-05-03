using UnityEngine;
using UnityEditor;
using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;

namespace MountAndBlade.Data.Editor
{
    /// <summary>
    /// Unified JSON importer window for all Mount & Blade data types.
    /// Automatically detects JSON file types and imports them into organized ScriptableObject hierarchies.
    /// Assets are registered with the target MBModuleData for centralized access.
    /// </summary>
    public class MBScritableObjectDataCreator
    {
        // WINDOW STATE

        private MBModule _targetModuleData = null;
        private string _jsonFolderPath = "";
        private Vector2 _scrollPosition;
        private Dictionary<string, ImporterEntry> _availableImporters = new Dictionary<string, ImporterEntry>();
        private List<DetectedJsonFile> _detectedFiles = new List<DetectedJsonFile>();
        private string _outputRootPath = "Assets/MountAndBlade/Data";

        private bool _showSettings = true;
        private bool _showDetectedFiles = true;
        private bool _showImportLog = false;
        private List<string> _importLog = new List<string>();

        // Import statistics
        private ImportStats _lastImportStats;
        
        // IMPORTER INITIALIZATION

        public void InitializeImporters(MBModule mod)
        {
            _availableImporters.Clear();

             _targetModuleData =mod;
            _jsonFolderPath = MBPathHelpers.ModDataBaseJsonDirectoryPath(_targetModuleData.ID);
            _outputRootPath = MBPathHelpers.ModDataBaseDirectoryPath(_targetModuleData.ID);

            // Register all importers with their detection patterns and handlers
            // Now passing MBModuleData to each importer
            RegisterImporter("Factions", new[] { "factions_full.json", "faction" }, ImportFactions);
            RegisterImporter("Flora", new[] { "flora_full.json", "flora" }, ImportFlora);
            RegisterImporter("Items", new[] { "items_full.json", "item" }, ImportItems);
            RegisterImporter("Map Icons", new[] { "map_icons_full.json", "map_icon" }, ImportMapIcons);
            RegisterImporter("Parties", new[] { "parties_full.json", "partie" }, ImportParties);
            RegisterImporter("Party Templates", new[] { "party_templates_full.json", "party_template" },
                ImportPartyTemplates);
            RegisterImporter("Scene Props", new[] { "scene_props_full.json", "scene_prop" }, ImportSceneProps);
            RegisterImporter("Scenes", new[] { "scenes_full.json", "scene" }, ImportScenes);
            RegisterImporter("Skins", new[] { "skins_full.json", "skin" }, ImportSkins);
            RegisterImporter("Troops", new[] { "troops_full.json", "troop" }, ImportTroops);
            RegisterImporter("Particle Systems", new[] { "particle_systems_full.json", "particle_system" }, ImportParticleSystems);
            RegisterImporter("Ground Specs", new[] { "ground_specs_full.json", "ground_spec" }, ImportGroundSpecs);
        }

        private void RegisterImporter(string name, string[] patterns, Action<string, MBModule> importAction)
        {
            _availableImporters[name] = new ImporterEntry
            {
                name = name,
                patterns = patterns,
                importAction = importAction
            };
        }

        // FILE DETECTION

        public void ScanForJsonFiles()
        {
            _detectedFiles.Clear();
            _importLog.Clear();

            if (string.IsNullOrEmpty(_jsonFolderPath) || !Directory.Exists(_jsonFolderPath))
            {
                return;
            }

            string[] jsonFiles = Directory.GetFiles(_jsonFolderPath, "*.json", SearchOption.AllDirectories);

            foreach (string filePath in jsonFiles)
            {
                DetectedJsonFile detected = new DetectedJsonFile
                {
                    filePath = filePath,
                    shouldImport = true
                };

                // Try to detect file type
                string fileName = Path.GetFileName(filePath).ToLower();
                bool typeDetected = false;

                foreach (var importer in _availableImporters.Values)
                {
                    foreach (string pattern in importer.patterns)
                    {
                        if (fileName.Contains(pattern.ToLower()))
                        {
                            detected.detectedType = importer.name;
                            detected.status = FileStatus.Ready;
                            detected.statusMessage = "Ready to import";
                            typeDetected = true;
                            break;
                        }
                    }

                    if (typeDetected) break;
                }

                if (!typeDetected)
                {
                    detected.detectedType = "Unknown";
                    detected.status = FileStatus.Warning;
                    detected.statusMessage = "Unknown file type";
                }

                _detectedFiles.Add(detected);
            }
        }

        // IMPORT OPERATIONS

        public void ImportAllFiles()
        {
            foreach (var file in _detectedFiles)
            {
                file.shouldImport = true;
            }

            ImportSelectedFiles();
        }
        private void ImportSelectedFiles()
        {
            if (_targetModuleData == null)
            {
                EditorUtility.DisplayDialog("Import Failed", "Please select a Module Data asset first.", "OK");
                return;
            }
            
            CreateDirectories();
            CleanModuleData(_targetModuleData);

            var filesToImport = _detectedFiles
                .Where(f => f.shouldImport && f.status == FileStatus.Ready)
                .ToList();

            if (filesToImport.Count == 0)
            {
                return;
            }

            // Reset import stats
            _lastImportStats = new ImportStats();

            EditorUtility.DisplayProgressBar("Importing JSON Files", "Starting...", 0f);

            int successCount = 0;
            int failCount = 0;

            try
            {
                // Batch asset database operations for performance
                AssetDatabase.StartAssetEditing();

                for (int i = 0; i < filesToImport.Count; i++)
                {
                    var file = filesToImport[i];
                    float progress = (float)i / filesToImport.Count;

                    EditorUtility.DisplayProgressBar("Importing JSON Files",
                        $"Importing {Path.GetFileName(file.filePath)}... ({i + 1}/{filesToImport.Count})", progress);

                    try
                    {
                        ImportSingleFile(file);
                        successCount++;
                    }
                    catch (Exception ex)
                    {
                        failCount++;
                    }
                }
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
            }

            EditorUtility.SetDirty(_targetModuleData);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            EditorUtility.ClearProgressBar();

            if (failCount > 0)
            {
                EditorUtility.DisplayDialog("Import Completed with Errors",
                    $"Imported {successCount} files successfully.\n{failCount} files failed.\n\n" +
                    $"Module '{_targetModuleData.ID}' now contains {_targetModuleData.ID} entries.\n\n" +
                    "Check the import log for details.", "OK");
            }
            else
            {
                EditorUtility.DisplayDialog("Import Complete",
                    $"Successfully imported {successCount} files!\n\n" +
                    $"Module '{_targetModuleData.ID}' now contains {_targetModuleData.ID} entries.\n\n" +
                    _lastImportStats.GetSummary(), "OK");
            }
        }

        private void ImportSingleFile(DetectedJsonFile file)
        {
            if (_targetModuleData == null)
            {
                file.status = FileStatus.Error;
                file.statusMessage = "No module selected";
                return;
            }

            if (!_availableImporters.ContainsKey(file.detectedType))
            {
                file.status = FileStatus.Error;
                file.statusMessage = "No importer";
                return;
            }

            try
            {
                // Pass module data to the importer
                _availableImporters[file.detectedType].importAction(file.filePath, _targetModuleData);

                file.status = FileStatus.Imported;
                file.statusMessage = "Imported successfully";
            }
            catch (Exception ex)
            {
                file.status = FileStatus.Error;
                file.statusMessage = $"Error: {ex.Message}";
                throw; // Re-throw to be caught by batch importer
            }
        }

        // TYPE-SPECIFIC IMPORTERS

        private void ImportFactions(string filePath, MBModule moduleData)
        {
            string json = File.ReadAllText(filePath);
            var factionsJson = JsonFactionDeserializeHelper.FromJson<MBFactionJsonDecoder.FactionJsonData>(json);
            var decoded = MBFactionJsonDecoder.DecodeFactions(factionsJson);

            int count = 0;
            foreach (var faction in decoded)
            {
                string outputPath = Path.Combine(_outputRootPath, "Factions");
                var asset = CreateAndRegisterAsset<MBFactionData, MBFactionJsonDecoder.DecodedFactionData>(
                    faction, outputPath, faction.factionId, PopulateFactionAsset);

                if (asset != null)
                {
                    moduleData.factions.Add(asset);
                    count++;
                }
            }

            _lastImportStats.factions += count;
        }

        private void ImportFlora(string filePath, MBModule moduleData)
        {
            string json = File.ReadAllText(filePath);
            var floraJson = JsonHelper.FromJson<MBFloraJsonDecoder.FloraJsonData>(json);
            var decoded = MBFloraJsonDecoder.DecodeFlora(floraJson);

            int count = 0;
            foreach (var flora in decoded)
            {
                string outputPath = Path.Combine(_outputRootPath, "Flora");
                var asset = CreateAndRegisterAsset<MBFloraData, MBFloraJsonDecoder.DecodedFloraData>(
                    flora, outputPath, flora.floraId, PopulateFloraAsset);

                if (asset != null)
                {
                    moduleData.flora.Add(asset);
                    count++;
                }
            }

            _lastImportStats.flora += count;
        }

        private void ImportItems(string filePath, MBModule moduleData)
        {
            string json = File.ReadAllText(filePath);
            var itemsJson = JsonHelper.FromJson<MBItemJsonDecoder.ItemJsonData>(json);
            var decoded = MBItemJsonDecoder.DecodeItems(itemsJson);

            int count = 0;
            foreach (var item in decoded)
            {
                string outputPath = Path.Combine(_outputRootPath, "Items");
                var asset = CreateAndRegisterAsset<MBItemData, MBItemJsonDecoder.DecodedItemData>(
                    item, outputPath, item.itemId, PopulateItemAsset);

                if (asset != null)
                {
                    moduleData.items.Add(asset);
                    count++;
                }
            }

            _lastImportStats.items += count;
        }

        private void ImportMapIcons(string filePath, MBModule moduleData)
        {
            string json = File.ReadAllText(filePath);
            var iconsJson = JsonHelper.FromJson<MBMapIconJsonDecoder.MapIconJsonData>(json);
            var decoded = MBMapIconJsonDecoder.DecodeMapIcons(iconsJson);

            int count = 0;
            foreach (var icon in decoded)
            {
                string outputPath = Path.Combine(_outputRootPath, "MapIcons");
                var asset = CreateAndRegisterAsset<MBMapIconData, MBMapIconJsonDecoder.DecodedMapIconData>(
                    icon, outputPath, icon.iconId, PopulateMapIconAsset);

                if (asset != null)
                {
                    moduleData.mapIcons.Add(asset);
                    count++;
                }
            }

            _lastImportStats.mapIcons += count;
        }

        private void ImportParties(string filePath, MBModule moduleData)
        {
            string json = File.ReadAllText(filePath);
            var partiesJson = JsonHelper.FromJson<MBPartyJsonDecoder.PartyJsonData>(json);
            var decoded = MBPartyJsonDecoder.DecodeParties(partiesJson);

            int count = 0;
            foreach (var party in decoded)
            {
                string outputPath = Path.Combine(_outputRootPath, "Parties");
                var asset = CreateAndRegisterAsset<MBPartyData, MBPartyJsonDecoder.DecodedPartyData>(
                    party, outputPath, party.partyId, PopulatePartyAsset);

                if (asset != null)
                {
                    moduleData.parties.Add(asset);
                    count++;
                }
            }

            _lastImportStats.parties += count;
        }

        private void ImportPartyTemplates(string filePath, MBModule moduleData)
        {
            string json = File.ReadAllText(filePath);
            var templatesJson = JsonHelper.FromJson<MBPartyTemplateJsonDecoder.TemplateJsonData>(json);
            var decoded = MBPartyTemplateJsonDecoder.DecodePartyTemplates(templatesJson);

            int count = 0;
            foreach (var template in decoded)
            {
                string outputPath = Path.Combine(_outputRootPath, "PartyTemplates");
                var asset =
                    CreateAndRegisterAsset<MBPartyTemplateData, MBPartyTemplateJsonDecoder.DecodedPartyTemplateData>(
                        template, outputPath, template.templateId, PopulatePartyTemplateAsset);

                if (asset != null)
                {
                    moduleData.partyTemplates.Add(asset);
                    count++;
                }
            }

            _lastImportStats.partyTemplates += count;
        }

        private void ImportSceneProps(string filePath, MBModule moduleData)
        {
            string json = File.ReadAllText(filePath);
            var propsJson = JsonHelper.FromJson<MBScenePropJsonDecoder.ScenePropJsonData>(json);
            var decoded = MBScenePropJsonDecoder.DecodeSceneProps(propsJson);

            int count = 0;
            foreach (var prop in decoded)
            {
                string outputPath = Path.Combine(_outputRootPath, "SceneProps");
                var asset = CreateAndRegisterAsset<MBScenePropData, MBScenePropJsonDecoder.DecodedScenePropData>(
                    prop, outputPath, prop.propId, PopulateScenePropAsset);

                if (asset != null)
                {
                    moduleData.sceneProps.Add(asset);
                    count++;
                }
            }

            _lastImportStats.sceneProps += count;
        }

        private void ImportScenes(string filePath, MBModule moduleData)
        {
            string json = File.ReadAllText(filePath);
            var scenesJson = JsonHelper.FromJson<MBSceneJsonDecoder.SceneJsonData>(json);
            var decoded = MBSceneJsonDecoder.DecodeScenes(scenesJson);

            int count = 0;
            foreach (var scene in decoded)
            {
                string outputPath = Path.Combine(_outputRootPath, "Scenes");
                var asset = CreateAndRegisterAsset<MBSceneData, MBSceneJsonDecoder.DecodedSceneData>(
                    scene, outputPath, scene.sceneId, PopulateSceneAsset);

                if (asset != null)
                {
                    moduleData.scenes.Add(asset);
                    count++;
                }
            }

            _lastImportStats.scenes += count;
        }

        private void ImportSkins(string filePath, MBModule moduleData)
        {
            string json = File.ReadAllText(filePath);
            var skinsJson = JsonHelper.FromJson<MBSkinJsonDecoder.SkinJsonData>(json);
            var decoded = MBSkinJsonDecoder.DecodeSkins(skinsJson);

            int count = 0;
            foreach (var skin in decoded)
            {
                string outputPath = Path.Combine(_outputRootPath, "Skins");
                var asset = CreateAndRegisterAsset<MBSkinData, MBSkinJsonDecoder.DecodedSkinData>(
                    skin, outputPath, skin.skinId, PopulateSkinAsset);

                if (asset != null)
                {
                    moduleData.skins.Add(asset);
                    count++;
                }
            }

            _lastImportStats.skins += count;
        }

        private void ImportTroops(string filePath, MBModule moduleData)
        {
            string json = File.ReadAllText(filePath);
            var troopsJson = JsonHelper.FromJson<MBTroopJsonDecoder.TroopJsonData>(json);
            var decoded = MBTroopJsonDecoder.DecodeTroops(troopsJson);

            int count = 0;
            foreach (var troop in decoded)
            {
                string outputPath = Path.Combine(_outputRootPath, "Troops");
                var asset = CreateAndRegisterAsset<MBTroopData, MBTroopJsonDecoder.DecodedTroopData>(
                    troop, outputPath, troop.troopId, PopulateTroopAsset);

                if (asset != null)
                {
                    moduleData.troops.Add(asset);
                    count++;
                }
            }

            _lastImportStats.troops += count;
        }

        private void ImportGroundSpecs(string filePath, MBModule moduleData)
        {
            string json = File.ReadAllText(filePath);
            var groundSpecsJson = JsonHelper.FromJson<MBGroundSpecsJsonDecoder.GroundSpecJsonData>(json);
            var decoded = MBGroundSpecsJsonDecoder.DecodeGroundSpecs(groundSpecsJson);

            string outputPath = Path.Combine(_outputRootPath, "GroundSpecs");
            var asset = CreateAndRegisterAsset<MBGroundSpecsData, MBGroundSpecsJsonDecoder.DecodedGroundSpecsData>(
                decoded, outputPath, "MBGroundSpecsData", PopulateGroundSpecsAsset);

            if (asset != null)
            {
                moduleData.groundSpecs = asset;
                _lastImportStats.groundSpecs++;
            }
        }

        // SCRIPTABLE OBJECT CREATION

        /// <summary>
        /// Creates or updates a ScriptableObject asset and returns it for registration with the module.
        /// </summary>
        private TAsset CreateAndRegisterAsset<TAsset, TData>(
            TData data,
            string folderPath,
            string assetName,
            Action<TData, TAsset> populateAction)
            where TAsset : ScriptableObject
            where TData : class
        {
            try
            {
                // Sanitize asset name
                 assetName = SanitizeFileName(assetName);

                // Normalize path separators and create asset path
                folderPath = NormalizePath(folderPath);
                string assetPath = $"{folderPath}/{assetName}.asset";

                // Check if asset already exists
                TAsset asset = AssetDatabase.LoadAssetAtPath<TAsset>(assetPath);
                bool isNew = asset == null;

                if (isNew)
                {
                    asset = ScriptableObject.CreateInstance<TAsset>();
                }

                // Populate asset with data
                populateAction(data, asset);

                if (isNew)
                {
                    string unityPath = assetPath.Replace(Application.dataPath, "Assets");
                    AssetDatabase.CreateAsset(asset, unityPath);
                }
                else
                {
                    EditorUtility.SetDirty(asset);
                }

                return asset;
            }
            catch (Exception ex)
            {
                return null;
            }
        }

        // ASSET POPULATION METHODS

        private void PopulateFactionAsset(MBFactionJsonDecoder.DecodedFactionData data, MBFactionData asset)
        {
            asset.factionId = data.factionId;
            asset.factionName = data.factionName;
            asset.flags = data.flags;
            asset.coherence = data.coherence;
            asset.factionColor = data.factionColor;
            asset.relations = data.relations;
        }

        private void PopulateFloraAsset(MBFloraJsonDecoder.DecodedFloraData data, MBFloraData asset)
        {
            asset.FloraID = data.floraId;
            asset.Flags = data.flags;
            asset.Density = data.density;
            asset.Meshes = data.meshes.Select(m => new FloraMesh
            {
                Mesh = m.meshName,
                MeshCollision = m.collisionObject
            }).ToList();
        }

        private void PopulateItemAsset(MBItemJsonDecoder.DecodedItemData data, MBItemData asset)
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

        private void PopulateMapIconAsset(MBMapIconJsonDecoder.DecodedMapIconData data, MBMapIconData asset)
        {
            asset.iconId = data.iconId;
            asset.displayName = data.displayName;
            asset.meshName = data.meshName;
            asset.scale = data.scale;
            asset.noShadow = data.noShadow;
            asset.soundId = data.soundId;
            asset.flagOffsetX = data.flagOffsetX;
            asset.flagOffsetY = data.flagOffsetY;
            asset.flagOffsetZ = data.flagOffsetZ;
            asset.triggerCode = data.triggerCode;
        }

        private void PopulatePartyAsset(MBPartyJsonDecoder.DecodedPartyData data, MBPartyData asset)
        {
            asset.partyId = data.partyId;
            asset.partyName = data.partyName;
            asset.mapIcon = data.mapIcon;
            asset.flags = data.flags;
            asset.menu = data.menu;
            asset.partyTemplate = data.partyTemplate;
            asset.aiBehavior = data.aiBehavior;
            asset.aiTarget = data.aiTarget;
            asset.faction = data.faction;
            asset.personality = data.personality;
            asset.direction = data.direction;
            asset.worldPosition = data.worldPosition;
            asset.troops = data.troops.Select(t => new MBTroopStack
            {
                TroopID = t.troopId,
                Count = t.count,
                StackFlags = t.stackFlags
            }).ToList();
        }

        private void PopulatePartyTemplateAsset(MBPartyTemplateJsonDecoder.DecodedPartyTemplateData data,
            MBPartyTemplateData asset)
        {
            asset.ID = data.templateId;
            asset.TemplateName = data.templateName;
            asset.Flags = data.flags;
            asset.Menu = data.menu;
            asset.Faction = data.faction;
            asset.Personality = data.personality;
            asset.TemplateStacks = data.templateStacks.Select(s => new MBTemplateStack
            {
                TroopID = s.troopId,
                MinCount = s.minCount,
                MaxCount = s.maxCount,
                StackFlags = s.stackFlags
            }).ToList();
        }

        private void PopulateScenePropAsset(MBScenePropJsonDecoder.DecodedScenePropData data, MBScenePropData asset)
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

        private void PopulateSceneAsset(MBSceneJsonDecoder.DecodedSceneData data, MBSceneData asset)
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

        private void PopulateSkinAsset(MBSkinJsonDecoder.DecodedSkinData data, MBSkinData asset)
        {
            asset.SkinID = data.skinId;
            asset.SkinFlags = data.skinFlags;
            asset.SkeletonName = data.skeletonName;
            asset.BodyMesh = data.bodyMesh;
            asset.CalfMeshLeft = data.calfMeshLeft;
            asset.HandMeshLeft = data.handMeshLeft;
            asset.HeadMesh = data.headMesh;
            asset.FaceKeys = data.faceKeys.Select(k => new MBFaceKey(
                k.keyId, k.reserved, k.minValue, k.maxValue, k.displayName
            )).ToList();
            asset.HairMeshes = data.hairMeshes?.ToList() ?? new List<string>();
            asset.BeardMeshes = data.beardMeshes?.ToList() ?? new List<string>();
            asset.HairTextures = data.hairTextures?.ToList() ?? new List<string>();
            asset.BeardTextures = data.beardTextures?.ToList() ?? new List<string>();
            asset.FaceTextures = data.faceTextures.Select(t => new MBFaceTexture(t.textureName, t.baseSkinColor)
            {
                HairTextureOptions = t.hairTextureOptions?.ToList() ?? new List<string>(),
                SkinColorVariations = t.skinColorVariations?.ToList() ?? new List<string>()
            }).ToList();
            asset.VoiceSounds = data.voiceSounds.Select(v => new MBVoiceEntry(v.eventType, v.soundId)).ToList();
            asset.BloodParticles1 = data.bloodParticles1;
            asset.BloodParticles2 = data.bloodParticles2;
        }

        private void PopulateTroopAsset(MBTroopJsonDecoder.DecodedTroopData data, MBTroopData asset)
        {
            asset.TroopID = data.troopId;
            asset.TroopName = data.troopName;
            asset.TroopNamePlural = data.troopNamePlural;
            asset.TroopFlags = data.troopFlags;
            asset.Scene = data.scene;
            asset.EntryPoint = data.entryPoint;
            asset.FactionID = data.factionId;
            asset.TroopAttributes = data.troopAttributes;
            asset.WeaponProficiencies = data.weaponProficiencies;
            asset.TroopSkills = data.troopSkills;
            asset.FaceCode1 = data.faceCode1;
            asset.FaceCode2 = data.faceCode2;
            asset.TroopImageMesh = data.troopImageMesh;
            asset.Inventory = data.inventory;
            asset.UpgradePaths = data.upgradePaths;
        }

        private void PopulateGroundSpecsAsset(MBGroundSpecsJsonDecoder.DecodedGroundSpecsData data, MBGroundSpecsData asset)
        {
            asset.GroundSpecs.Clear();
            if (data.groundSpecs != null)
            {
                foreach (var item in data.groundSpecs)
                {
                    asset.GroundSpecs.Add(new MBGroundSpec
                    {
                        ID = item.id,
                        Index = item.index,
                        GroundConstant = item.groundConstant,
                        Flags = item.flags,
                        Material = item.material,
                        UVScale = item.uvScale,
                        MultitexMaterial = item.multitexMaterial,
                        HasColor = item.hasColor,
                        Color = item.color
                    });
                }
            }
            EditorUtility.SetDirty(asset);
        }

        // UTILITY METHODS

        private void CleanModuleData(MBModule moduleData)
        {
            moduleData.factions.Clear();
            moduleData.flora.Clear();
            moduleData.items.Clear();
            moduleData.mapIcons.Clear();
            moduleData.parties.Clear();
            moduleData.partyTemplates.Clear();
            moduleData.sceneProps.Clear();
            moduleData.scenes.Clear();
            moduleData.skins.Clear();
            moduleData.troops.Clear();
            moduleData.particleSystems.Clear();
            moduleData.groundSpecs = null;

            EditorUtility.SetDirty(moduleData);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }
        
        private void CreateDirectories()
        {
            //Create Directories
            if (!Directory.Exists(_outputRootPath))
            {
                Directory.CreateDirectory(_outputRootPath);
            }

            if (!Directory.Exists(Path.Combine(_outputRootPath, "Factions")))
            {
                Directory.CreateDirectory(Path.Combine(_outputRootPath, "Factions"));
            }

            if (!Directory.Exists(Path.Combine(_outputRootPath, "Flora")))
            {
                Directory.CreateDirectory(Path.Combine(_outputRootPath, "Flora"));
            }

            if (!Directory.Exists(Path.Combine(_outputRootPath, "Items")))
            {
                Directory.CreateDirectory(Path.Combine(_outputRootPath, "Items"));
            }

            if (!Directory.Exists(Path.Combine(_outputRootPath, "MapIcons")))
            {
                Directory.CreateDirectory(Path.Combine(_outputRootPath, "MapIcons"));
            }

            if (!Directory.Exists(Path.Combine(_outputRootPath, "Parties")))
            {
                Directory.CreateDirectory(Path.Combine(_outputRootPath, "Parties"));
            }

            if (!Directory.Exists(Path.Combine(_outputRootPath, "PartyTemplates")))
            {
                Directory.CreateDirectory(Path.Combine(_outputRootPath, "PartyTemplates"));
            }

            if (!Directory.Exists(Path.Combine(_outputRootPath, "SceneProps")))
            {
                Directory.CreateDirectory(Path.Combine(_outputRootPath, "SceneProps"));
            }

            if (!Directory.Exists(Path.Combine(_outputRootPath, "Scenes")))
            {
                Directory.CreateDirectory(Path.Combine(_outputRootPath, "Scenes"));
            }

            if (!Directory.Exists(Path.Combine(_outputRootPath, "Skins")))
            {
                Directory.CreateDirectory(Path.Combine(_outputRootPath, "Skins"));
            }

            if (!Directory.Exists(Path.Combine(_outputRootPath, "Troops")))
            {
                Directory.CreateDirectory(Path.Combine(_outputRootPath, "Troops"));
            }
            
            if (!Directory.Exists(Path.Combine(_outputRootPath, "ParticleSystems")))
            {
                Directory.CreateDirectory(Path.Combine(_outputRootPath, "ParticleSystems"));
            }

            if (!Directory.Exists(Path.Combine(_outputRootPath, "GroundSpecs")))
            {
                Directory.CreateDirectory(Path.Combine(_outputRootPath, "GroundSpecs"));
            }

            AssetDatabase.Refresh();
        }

        string NormalizePath(string path)
        {
            return Path.GetFullPath(path).Replace('\\', '/');
        }

        private string SanitizeFileName(string fileName)
        {
            // Remove invalid characters
            char[] invalidChars = Path.GetInvalidFileNameChars();
            foreach (char c in invalidChars)
            {
                fileName = fileName.Replace(c, '_');
            }

            return fileName;
        }

        // DATA STRUCTURES

        private class ImporterEntry
        {
            public string name;
            public string[] patterns;
            public Action<string, MBModule> importAction;
        }

        private class DetectedJsonFile
        {
            public string filePath;
            public string detectedType;
            public bool shouldImport;
            public FileStatus status;
            public string statusMessage = "";
        }

        private enum FileStatus
        {
            Ready,
            Warning,
            Error,
            Imported
        }

        private class ImportStats
        {
            public int factions;
            public int flora;
            public int items;
            public int mapIcons;
            public int parties;
            public int partyTemplates;
            public int sceneProps;
            public int scenes;
            public int skins;
            public int troops;
            public int particleSystems;
            public int groundSpecs;

            public int Total => factions + flora + items + mapIcons + parties +
                                partyTemplates + sceneProps + scenes + skins + troops + particleSystems + groundSpecs;

            public string GetSummary()
            {
                var parts = new List<string>();
                if (factions > 0) parts.Add($"{factions} factions");
                if (flora > 0) parts.Add($"{flora} flora");
                if (items > 0) parts.Add($"{items} items");
                if (mapIcons > 0) parts.Add($"{mapIcons} map icons");
                if (parties > 0) parts.Add($"{parties} parties");
                if (partyTemplates > 0) parts.Add($"{partyTemplates} party templates");
                if (sceneProps > 0) parts.Add($"{sceneProps} scene props");
                if (scenes > 0) parts.Add($"{scenes} scenes");
                if (skins > 0) parts.Add($"{skins} skins");
                if (troops > 0) parts.Add($"{troops} troops");
                if (particleSystems > 0) parts.Add($"{particleSystems} particle systems");
                if (groundSpecs > 0) parts.Add($"{groundSpecs} ground specs");

                return parts.Count > 0
                    ? $"Imported: {string.Join(", ", parts)}"
                    : "No items imported";
            }
        }
        
           
    

    // PARTICLES
    //
    
    private void ImportParticleSystems(string filePath, MBModule moduleData)
    {
        string json = File.ReadAllText(filePath);
        var psJson = JsonHelper.FromJson<MBParticleSystemJsonDecoder.ParticleSystemJsonData>(json);
        var decoded = MBParticleSystemJsonDecoder.DecodeParticleSystems(psJson);

        int count = 0;
        foreach (var ps in decoded)
        {
            string outputPath = Path.Combine(_outputRootPath, "ParticleSystems");
            var asset = CreateAndRegisterAsset<MBParticleSystemData, MBParticleSystemJsonDecoder.DecodedParticleSystemData>(
                ps, outputPath, ps.particleSystemId, PopulateParticleSystemAsset);

            if (asset != null)
            {
                moduleData.particleSystems.Add(asset);
                count++;
            }
        }

        _lastImportStats.particleSystems += count;
    }
    
    private static void PopulateParticleSystemAsset(MBParticleSystemJsonDecoder.DecodedParticleSystemData data, MBParticleSystemData asset)
    {
        asset.ParticleSystemID = data.particleSystemId;
        asset.Flags = data.flags;
        asset.MeshName = data.meshName;
        asset.NumParticlesPerSecond = data.numParticlesPerSecond;
        asset.ParticleLife = data.particleLife;
        asset.Damping = data.damping;
        asset.GravityStrength = data.gravityStrength;
        asset.TurbulenceSize = data.turbulenceSize;
        asset.TurbulenceStrength = data.turbulenceStrength;
        asset.AlphaKey1 = data.alphaKey1;
        asset.AlphaKey2 = data.alphaKey2;
        asset.RedKey1 = data.redKey1;
        asset.RedKey2 = data.redKey2;
        asset.GreenKey1 = data.greenKey1;
        asset.GreenKey2 = data.greenKey2;
        asset.BlueKey1 = data.blueKey1;
        asset.BlueKey2 = data.blueKey2;
        asset.ScaleKey1 = data.scaleKey1;
        asset.ScaleKey2 = data.scaleKey2;
        asset.EmitBoxSize = data.emitBoxSize;
        asset.EmitVelocity = data.emitVelocity;
        asset.EmitDirRandomness = data.emitDirRandomness;
        asset.RotationSpeed = data.rotationSpeed;
        asset.RotationDamping = data.rotationDamping;
    }
    
    }
 
    
    // JSON HELPER (for deserializing arrays)

    public static class JsonHelper
    {
        public static T[] FromJson<T>(string json)
        {
            Wrapper<T> wrapper = JsonUtility.FromJson<Wrapper<T>>("{\"items\":" + json + "}");
            return wrapper.items;
        }

        [Serializable]
        private class Wrapper<T>
        {
            public T[] items;
        }
    }
}
