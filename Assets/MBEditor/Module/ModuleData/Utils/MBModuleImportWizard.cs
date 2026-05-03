using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BDT.GUI.Helpers;
using MountAndBlade.Data;
using MountAndBlade.Data.Editor;
using UnityEditor;
using UnityEngine;

namespace MountAndBlade.ModdingToolkit
{
    /// <summary>
    /// Unified Module Import Wizard - Step-by-step guided import process
    /// Phase 1: Asset Pipeline (Resources, Textures, Materials, BRF Data)
    /// Phase 2: Data Conversion (JSON conversion, ScriptableObjects)
    /// Phase 3: Model Pipeline (BRF data, prefabs)
    /// </summary>
    public class MBModuleImportWizard
    {
        #region Import Steps Definition

        public enum ImportStep
        {
            // Phase 1: Asset Pipeline
            ImportResources = 0,
            ImportTextures = 1,
            CreateMaterials = 2,
            PopulateBrfData = 3,

            // Phase 2: Data Conversion  
            ConfigurePaths = 4,
            ConvertToJson = 5,
            CreateScriptableObjects = 6,

            // Phase 3: Model Pipeline
            CreateModelData = 7,
            AssignMaterials = 8,
            CreateTerrainPalette = 9,
            CreateModels = 10,
            CreatePrefabs = 11,

            Complete = 12
        }

        public enum ImportPhase
        {
            AssetPipeline,
            DataConversion,
            ModelPipeline
        }

        private class StepInfo
        {
            public string Name;
            public string Description;
            public ImportPhase Phase;
            public Func<MBModule, bool> CheckComplete;
            public Action<MBModule> Execute;
            public ImportStep[] Dependencies;
            public bool IsManualStep;
        }

        #endregion

        #region State

        private MBModule _module;
        private Dictionary<ImportStep, StepInfo> _steps;
        private Dictionary<ImportStep, StepStatus> _stepStatuses = new();
        private Vector2 _scrollPosition;
        private bool _isProcessing;
        private string _statusMessage = "";

        // Sub-components
        private MBScritableObjectDataCreator _dataCreator = new();
        private MBModuleImportContext _moduleContext;
        private MBModuleImportContext _nativeContext;

        // UI State
        private bool _showAdvancedOptions;
        private bool _autoAdvance = true;

        private Dictionary<ImportPhase, bool> _phaseFoldouts = new()
        {
            { ImportPhase.AssetPipeline, true },
            { ImportPhase.DataConversion, true },
            { ImportPhase.ModelPipeline, true }
        };

        public bool Imported
        {
            get
            {
                int completedSteps = _stepStatuses.Count(s =>
                    s.Key != ImportStep.Complete && s.Value == StepStatus.Complete);
                int totalSteps = _steps.Count - 1; // Exclude "Complete"

                return completedSteps == totalSteps;
            }
        }

        #endregion

        #region Initialization

        public MBModuleImportWizard()
        {
            InitializeSteps();
        }

        private void InitializeSteps()
        {
            _steps = new Dictionary<ImportStep, StepInfo>
            {
                // PHASE 1: ASSET PIPELINE

                [ImportStep.ImportResources] = new StepInfo
                {
                    Name = "Import Resources",
                    Description = "Import BRF resource files (meshes, material definitions)",
                    Phase = ImportPhase.AssetPipeline,
                    CheckComplete = m => MBEditorUtility.DirectoryExistsWithFiles(
                        MBPathHelpers.ModResourcePath(m.ID)),
                    Execute = m => ExecuteImportResources(m),
                    Dependencies = Array.Empty<ImportStep>()
                },

                [ImportStep.ImportTextures] = new StepInfo
                {
                    Name = "Import Textures",
                    Description = "Import DDS texture files into BRF folders",
                    Phase = ImportPhase.AssetPipeline,
                    CheckComplete = m => CheckTexturesImported(m.ID),
                    Execute = m => ImportTextures(m.ID),
                    Dependencies = new[] { ImportStep.ImportResources }
                },
                
                [ImportStep.CreateMaterials] = new StepInfo
                {
                    Name = "Create Materials",
                    Description = "Generate Unity materials from material definitions",
                    Phase = ImportPhase.AssetPipeline,
                    CheckComplete = m => CheckMaterialsCreated(m.ID),
                    Execute = ExecuteCreateMaterials,
                    Dependencies = new[] { ImportStep.ImportResources, ImportStep.ImportTextures }
                },

                [ImportStep.PopulateBrfData] = new StepInfo
                {
                    Name = "Populate BRF Data",
                    Description = "Build MBBrfData assets with mesh, material, and collision references",
                    Phase = ImportPhase.AssetPipeline,
                    CheckComplete = m => CheckBrfDataPopulated(m.ID),
                    Execute = ExecutePopulateBrfData,
                    Dependencies = new[] { ImportStep.CreateMaterials }
                },

                // PHASE 2: DATA CONVERSION

                [ImportStep.ConfigurePaths] = new StepInfo
                {
                    Name = "Configure Module System Path",
                    Description = "Set path to M&B module system folder with Python source files",
                    Phase = ImportPhase.DataConversion,
                    CheckComplete = m => CheckJsonFilesExist(m) || 
                                         (!string.IsNullOrEmpty(m?.ModuleSystemPath) &&
                                         Directory.Exists(m.ModuleSystemPath) &&
                                         MBDataJsonImporter.ConverterConfigs.All(c => c.IsFound)),
                    Execute = null, // Manual step
                    IsManualStep = true,
                    Dependencies = Array.Empty<ImportStep>()
                },

                [ImportStep.ConvertToJson] = new StepInfo
                {
                    Name = "Convert Module Data → JSON",
                    Description = "Run Python converters to extract data from M&B module files",
                    Phase = ImportPhase.DataConversion,
                    CheckComplete = m => CheckJsonFilesExist(m),
                    Execute = ExecuteJsonConversion,
                    Dependencies = new[] { ImportStep.ConfigurePaths }
                },

                [ImportStep.CreateScriptableObjects] = new StepInfo
                {
                    Name = "Create ScriptableObjects",
                    Description = "Import JSON files into Unity ScriptableObject assets",
                    Phase = ImportPhase.DataConversion,
                    CheckComplete = m => CheckScriptableObjectsExist(m),
                    Execute = ExecuteScriptableObjectCreation,
                    Dependencies = new[] { ImportStep.ConvertToJson }
                },

                // PHASE 3: MODEL PIPELINE

                [ImportStep.CreateModelData] = new StepInfo
                {
                    Name = "Create BRF DataBase",
                    Description = "Aggregate all BRF data into module-level database",
                    Phase = ImportPhase.ModelPipeline,
                    CheckComplete = m => CheckBrfDataBaseExists(m.ID),
                    Execute = CreateBRFDataBase,
                    Dependencies = new[] { ImportStep.PopulateBrfData, ImportStep.CreateScriptableObjects }
                },

                [ImportStep.AssignMaterials] = new StepInfo
                {
                    Name = "Assign Materials",
                    Description = "Resolve and link Unity materials across all BRF model groups",
                    Phase = ImportPhase.ModelPipeline,
                    CheckComplete = m => CheckMaterialsAssigned(m.ID),
                    Execute = ExecuteAssignMaterials,
                    Dependencies = new[] { ImportStep.CreateMaterials, ImportStep.CreateModelData }
                },

                [ImportStep.CreateTerrainPalette] = new StepInfo
                {
                    Name = "Create Terrain Palette",
                    Description = "Generate and configure Unity terrain layers with proper UV scaling and materials",
                    Phase = ImportPhase.ModelPipeline,
                    CheckComplete = m => AssetDatabase.LoadAssetAtPath<MBTerrainPalette>(MBPathHelpers.ModTerrainPalette(m.ID)) != null,
                    Execute = m => TerrainPaletteHelpers.CreateUniqueTerrainPalette(m),
                    Dependencies = new[] { ImportStep.AssignMaterials, ImportStep.CreateScriptableObjects }
                },

                [ImportStep.CreateModels] = new StepInfo
                {
                    Name = "Create Models",
                    Description = "Generate model prefab data",
                    Phase = ImportPhase.ModelPipeline,
                    CheckComplete = m => MBEditorUtility.ResourcesHasModels(m.ID),
                    Execute = m =>
                    {
                        EnsureContexts();
                        MBEditorUtility.CreateModelPrefabs(m,_moduleContext,_nativeContext);
                    },
                    Dependencies = new[] { ImportStep.CreateTerrainPalette }
                },

                [ImportStep.CreatePrefabs] = new StepInfo
                {
                    Name = "Create Prefabs",
                    Description = "Generate Unity prefabs for all models",
                    Phase = ImportPhase.ModelPipeline,
                    CheckComplete = m => MBEditorUtility.DirectoryExistsWithFiles(
                        MBPathHelpers.ModPrefabsPath(m.ID),"*.prefab*"),
                    Execute = m => MBPrefabsGenerator.GeneratePrefabs(m),
                    Dependencies = new[] { ImportStep.CreateModels }
                },

                // COMPLETE

                [ImportStep.Complete] = new StepInfo
                {
                    Name = "Import Complete",
                    Description = "All module data has been imported successfully",
                    Phase = ImportPhase.ModelPipeline,
                    CheckComplete = m => true,
                    Execute = null,
                    Dependencies = new[] { ImportStep.CreatePrefabs, ImportStep.CreateScriptableObjects, ImportStep.CreateTerrainPalette }
                }
            };
        }

        #endregion

        #region Main UI

        public void DrawWizardUI(MBModule module)
        {
            if (module == null)
            {
                EditorGUILayout.HelpBox("Select a module to begin import process.", MessageType.Info);
                return;
            }

            // Module changed - refresh status
            if (_module != module)
            {
                _module = module;
                if (!string.IsNullOrEmpty(_module.ModuleSystemPath))
                {
                    ScanForModuleFiles();
                }

                RefreshAllStepStatuses();
            }

            _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);

            DrawProgressHeader();
            EditorGUILayout.Space(10);

            // Draw phases with foldouts
            DrawPhase(ImportPhase.AssetPipeline, "Phase 1: Asset Pipeline",
                "Import resources, textures, and create materials");

            EditorGUILayout.Space(5);

            DrawPhase(ImportPhase.DataConversion, "Phase 2: Data Conversion",
                "Convert M&B module data to Unity ScriptableObjects");

            EditorGUILayout.Space(5);

            DrawPhase(ImportPhase.ModelPipeline, "Phase 3: Model Pipeline",
                "Build models and prefabs from imported assets");

            EditorGUILayout.EndScrollView();
        }

        private void DrawProgressHeader()
        {
            int completedSteps = _stepStatuses.Count(s =>
                s.Key != ImportStep.Complete && s.Value == StepStatus.Complete);
            int totalSteps = _steps.Count - 1; // Exclude "Complete"
            float overallProgress = (float)completedSteps / totalSteps;

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            // Title row
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Module Import Progress", EditorStyles.boldLabel);
            GUILayout.FlexibleSpace();
            EditorGUILayout.LabelField($"{_module.ID}", EditorStyles.miniLabel);
            EditorGUILayout.EndHorizontal();

            // Progress bar
            Rect progressRect = EditorGUILayout.GetControlRect(false, 22);
            EditorGUI.ProgressBar(progressRect, overallProgress,
                $"{completedSteps}/{totalSteps} Steps Complete ({overallProgress:P0})");

            // Status message
            if (!string.IsNullOrEmpty(_statusMessage))
            {
                EditorGUILayout.Space(3);
                EditorGUILayout.LabelField(_statusMessage, EditorStyles.wordWrappedMiniLabel);
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawPhase(ImportPhase phase, string title, string description)
        {
            // Calculate phase progress
            var phaseSteps = _steps.Where(s => s.Value.Phase == phase && s.Key != ImportStep.Complete).ToList();
            int completed = phaseSteps.Count(s => GetStepStatus(s.Key) == StepStatus.Complete);
            int total = phaseSteps.Count;
            bool allComplete = completed == total;

            // Phase header with foldout
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            EditorGUILayout.BeginHorizontal();

            // Status indicator
            GUI.color = allComplete ? new Color(0.2f, 0.8f, 0.2f) : Color.white;
            string phaseIcon = allComplete ? "✓" : "○";
            EditorGUILayout.LabelField(phaseIcon, GUILayout.Width(20));
            GUI.color = Color.white;

            // Foldout
            _phaseFoldouts[phase] = EditorGUILayout.Foldout(_phaseFoldouts[phase],
                $"{title} ({completed}/{total})", true, EditorStyles.foldoutHeader);

            GUILayout.FlexibleSpace();

            EditorGUILayout.EndHorizontal();

            if (_phaseFoldouts[phase])
            {
                EditorGUILayout.LabelField(description, EditorStyles.miniLabel);
                EditorGUILayout.Space(5);

                // Draw steps in this phase
                foreach (var stepKvp in phaseSteps.OrderBy(s => (int)s.Key))
                {
                    DrawStepRow(stepKvp.Key);
                }

                // Special handling for ConfigurePaths - show inline config
                if (phase == ImportPhase.DataConversion && _phaseFoldouts[phase])
                {
                    DrawPathConfigurationInline();
                }
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawStepRow(ImportStep step)
        {
            var info = _steps[step];
            var status = GetStepStatus(step);
            bool canExecute = CanExecuteStep(step);

            EditorGUILayout.BeginHorizontal();

            // Indent
            GUILayout.Space(20);

            // Status icon
            string icon = status switch
            {
                StepStatus.Complete => "✓",
                StepStatus.InProgress => "⟳",
                StepStatus.Error => "✗",
                StepStatus.Pending => "○",
                StepStatus.Blocked => "◌",
                _ => "?"
            };

            GUI.color = GetStatusColor(status);
            EditorGUILayout.LabelField(icon, GUILayout.Width(20));
            GUI.color = Color.white;

            // Step info
            EditorGUILayout.BeginVertical();
            EditorGUILayout.LabelField(info.Name,
                status == StepStatus.Complete ? EditorStyles.boldLabel : EditorStyles.label);

            // Show blocked reason
            if (status == StepStatus.Blocked)
            {
                var missingDeps = info.Dependencies
                    .Where(d => GetStepStatus(d) != StepStatus.Complete)
                    .Select(d => _steps[d].Name);
                var req = string.Join(", ", missingDeps);
                if (!string.IsNullOrEmpty(req))
                {
                    EditorGUILayout.LabelField($"Requires: {req}", EditorStyles.miniLabel);
                }
            }

            EditorGUILayout.EndVertical();

            GUILayout.FlexibleSpace();

            // Action button
            if (info.IsManualStep)
            {
                var col = status == StepStatus.Complete ? UIColors.Green : UIColors.Orange;
                var style = StylesHelpers.Label(col,11,true,TextAnchor.MiddleRight,true);
                EditorGUILayout.LabelField(status == StepStatus.Complete ? "Configured" : "Configure Below",style, GUILayout.Width(100));
                GUI.color = Color.white;
            }
            else
            {
                EditorGUI.BeginDisabledGroup(!canExecute || _isProcessing);

                if (status != StepStatus.Complete)
                {
                    if (GUILayout.Button("Run", GUILayout.Width(50)))
                    {
                        ExecuteStep(step);
                    }
                }

                EditorGUI.EndDisabledGroup();
            }

            EditorGUILayout.EndHorizontal();
        }

        private bool _showConvertersList = false;

        private void DrawPathConfigurationInline()
        {
            EditorGUILayout.Space(5);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            EditorGUILayout.LabelField("Module System Configuration", EditorStyles.boldLabel);

            // Path field
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Path:", GUILayout.Width(40));

            string newPath = EditorGUILayout.TextField(_module.ModuleSystemPath);
            if (newPath != _module.ModuleSystemPath)
            {
                _module.ModuleSystemPath = newPath;
                EditorUtility.SetDirty(_module);
            }

            if (GUILayout.Button("Browse", GUILayout.Width(60)))
            {
                string path = EditorUtility.OpenFolderPanel("Select Module System Folder",
                    _module.ModuleSystemPath, "");
                if (!string.IsNullOrEmpty(path))
                {
                    _module.ModuleSystemPath = path;
                    EditorUtility.SetDirty(_module);
                    ScanForModuleFiles();
                    RefreshAllStepStatuses();
                }
            }

            if (GUILayout.Button("Scan", GUILayout.Width(50)))
            {
                ScanForModuleFiles();
                RefreshAllStepStatuses();
            }

            EditorGUILayout.EndHorizontal();

            // Converter status summary
            int found = MBDataJsonImporter.ConverterConfigs.Count(c => c.IsFound);
            int total = MBDataJsonImporter.ConverterConfigs.Count;

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField($"Module files found: {found}/{total}",
                found == total ? EditorStyles.boldLabel : EditorStyles.label);

            if (found < total)
            {
                GUI.color = new Color(1f, 0.7f, 0.2f);
                var missing = MBDataJsonImporter.ConverterConfigs
                    .Where(c => !c.IsFound)
                    .Select(c => c.RequiredModuleFile)
                    .Take(3);
                EditorGUILayout.LabelField($"Missing: {string.Join(", ", missing)}...",
                    EditorStyles.miniLabel);
                GUI.color = Color.white;
            }

            EditorGUILayout.EndHorizontal();

            // Converters list foldout
            _showConvertersList = EditorGUILayout.Foldout(_showConvertersList, "Advanced: Converters List");
            if (_showConvertersList)
            {
                EditorGUI.indentLevel++;
                foreach (var config in MBDataJsonImporter.ConverterConfigs)
                {
                    GUI.backgroundColor = config.IsFound ? new Color(0.9f, 1f, 0.9f) : new Color(1f, 0.85f, 0.85f);
                    EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);
                    GUI.backgroundColor = Color.white;
                    
                    GUI.color = config.IsFound ? new Color(0.1f, 0.6f, 0.1f) : new Color(0.8f, 0.1f, 0.1f);
                    EditorGUILayout.LabelField(config.IsFound ? "✔" : "✘", EditorStyles.boldLabel, GUILayout.Width(18));
                    GUI.color = Color.white;

                    string cName = config.ConverterFileName.Replace("convert_", "").Replace(".py", "");
                    EditorGUILayout.LabelField(cName, EditorStyles.boldLabel, GUILayout.Width(110));
                    
                    string newFileName = EditorGUILayout.TextField(config.RequiredModuleFile, GUILayout.Width(150));
                    if (newFileName != config.RequiredModuleFile)
                    {
                        config.RequiredModuleFile = newFileName;
                    }

                    string displayPath = config.IsFound ? 
                        (string.IsNullOrEmpty(config.ManualPath) ? "Auto" : "Manual") : 
                        "Missing";
                    
                    GUI.color = config.IsFound ? Color.gray : new Color(0.8f, 0.2f, 0.2f);
                    EditorGUILayout.LabelField(displayPath, EditorStyles.miniLabel, GUILayout.Width(60));
                    GUI.color = Color.white;

                    if (GUILayout.Button(config.IsFound ? "Change..." : "Locate...", EditorStyles.miniButton, GUILayout.Width(70)))
                    {
                        string startDir = Directory.Exists(_module.ModuleSystemPath) ? _module.ModuleSystemPath : "";
                        string file = EditorUtility.OpenFilePanel("Select " + config.RequiredModuleFile, startDir, "py");
                        if (!string.IsNullOrEmpty(file))
                        {
                            config.ManualPath = file;
                            config.IsFound = true;
                            RefreshAllStepStatuses();
                        }
                    }

                    EditorGUILayout.EndHorizontal();
                }
                EditorGUI.indentLevel--;
            }

            EditorGUILayout.EndVertical();
        }

        #endregion

        #region Step Execution

        private void ExecuteStep(ImportStep step)
        {
            var info = _steps[step];
            if (info.Execute == null) return;

            _isProcessing = true;
            _stepStatuses[step] = StepStatus.InProgress;
            _statusMessage = $"Running: {info.Name}...";

            try
            {
                EditorUtility.DisplayProgressBar("Import Progress", info.Name, 0.5f);

                info.Execute(_module);

                RefreshStepStatus(step);

                if (_stepStatuses[step] == StepStatus.Complete)
                {
                    _statusMessage = $"✓ Completed: {info.Name}";
                }
            }
            catch (Exception ex)
            {
                _stepStatuses[step] = StepStatus.Error;
                _statusMessage = $"✗ Error in {info.Name}: {ex.Message}";
                Debug.LogError($"Import step failed: {ex}");
            }
            finally
            {
                _isProcessing = false;
                EditorUtility.ClearProgressBar();
                AssetDatabase.Refresh();

                // Rebuild context after asset-creating steps
                if (step is ImportStep.ImportResources or ImportStep.ImportTextures or ImportStep.CreateMaterials)
                {
                    RebuildContexts();
                }
            }
        }

        #endregion

        #region Step Implementations

        #region Phase 1: Asset Pipeline

        /// <summary>
        /// Step 1: Import Resources using BRF Synchronizer
        /// Exports BRF files to folder structure with data.json
        /// </summary>
        private void ExecuteImportResources(MBModule module)
        {
            bool isNative = module.ID == "Native";

            string sourcePath = isNative
                ? Path.Combine(MBEditorManager.MbEditorSettings.MbPath, "CommonRes")
                : Path.Combine(MBPathHelpers.ModSourcePath(module.ID), "Resource");

            string destinationPath = MBPathHelpers.ModResourcePath(module.ID);

            if (!Directory.Exists(sourcePath))
            {
                EditorUtility.DisplayDialog("Error", 
                    $"Source path not found:\n{sourcePath}", "OK");
                return;
            }

            if (!Directory.Exists(destinationPath))
                Directory.CreateDirectory(destinationPath);

            var brfFiles = Directory.GetFiles(sourcePath, "*.brf", SearchOption.TopDirectoryOnly);

            if (brfFiles.Length == 0)
            {
                EditorUtility.DisplayDialog("Warning", 
                    $"No BRF files found in:\n{sourcePath}", "OK");
                return;
            }

            int total = brfFiles.Length;
            int success = 0;
            int failed = 0;
            var failedFiles = new List<string>();

            try
            {
                for (int i = 0; i < brfFiles.Length; i++)
                {
                    string brfFile = brfFiles[i];
                    string brfName = Path.GetFileNameWithoutExtension(brfFile);
                    string outputFolder = Path.Combine(destinationPath, brfName);

                    EditorUtility.DisplayProgressBar(
                        "Importing BRF Resources",
                        $"Processing {brfName} ({i + 1}/{total})",
                        (float)i / total);

                    var result = BRFSyncHandler.ExportBRF(brfFile, outputFolder);

                    if (result.Success)
                    {
                        success++;

                        if (File.Exists(result.DataJsonPath))
                        {
                            ExportCompatibilityJsonFiles(module.ID, brfName, outputFolder);
                            
                            var data = ScriptableObject.CreateInstance<MBBrfData>();
                            data.name = brfName;
                            data.BrfName = brfName;
                            data.ModuleName = module.ID;
                            data.FolderPath = outputFolder;
                            data.SourceBrfPath = brfFile;
                            AssetDatabase.CreateAsset(data, Path.Combine(outputFolder, brfName + ".asset"));
                        }
                    }
                    else
                    {
                        failed++;
                        failedFiles.Add($"{brfName}: {result.Message}");
                    }
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            _statusMessage = $"Imported {success}/{total} BRF files";
            
            if (failed > 0)
            {
                _statusMessage += $" ({failed} failed)";
                Debug.LogWarning($"Failed BRF imports:\n{string.Join("\n", failedFiles)}");
            }

            Debug.Log($"BRF Import Complete: {success} succeeded, {failed} failed");
        }

        #region Texture Import

        /// <summary>
        /// Import textures into BRF-specific folders based on material references.
        /// </summary>
        public static void ImportTextures(string moduleName)
        {
            bool isNative = moduleName == "Native";

            string sourceTexturesPath = isNative
                ? Path.Combine(MBEditorManager.MbEditorSettings.MbPath, "Textures")
                : Path.Combine(MBEditorManager.MbEditorSettings.MbPath, "Modules", moduleName, "Textures");

            string nativeTexturesPath = Path.Combine(MBEditorManager.MbEditorSettings.MbPath, "Textures");
            string resourcePath = MBPathHelpers.ModResourcePath(moduleName);

            if (!Directory.Exists(sourceTexturesPath) && !Directory.Exists(nativeTexturesPath))
            {
                Debug.LogError($"Textures source path not found: {sourceTexturesPath}");
                return;
            }

            var availableTextures = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            if (isNative)
            {
                if (Directory.Exists(nativeTexturesPath))
                {
                    foreach (string ddsFile in Directory.GetFiles(nativeTexturesPath, "*.dds"))
                    {
                        string texName = Path.GetFileName(ddsFile);
                        availableTextures[texName] = ddsFile;
                    }
                }
            }
            else
            {
                if (Directory.Exists(sourceTexturesPath))
                {
                    foreach (string ddsFile in Directory.GetFiles(sourceTexturesPath, "*.dds"))
                    {
                        string texName = Path.GetFileName(ddsFile);
                        availableTextures[texName] = ddsFile;
                    }
                }
            }

            if (availableTextures.Count == 0)
            {
                Debug.LogWarning("No textures found to import");
                return;
            }

            var brfFolders = Directory.GetDirectories(resourcePath)
                .Where(d => File.Exists(Path.Combine(d, "data.json")))
                .ToList();

            int totalCopied = 0;
            int totalBrfs = brfFolders.Count;

            try
            {
                for (int i = 0; i < brfFolders.Count; i++)
                {
                    string brfFolder = brfFolders[i];
                    string brfName = Path.GetFileName(brfFolder);

                    EditorUtility.DisplayProgressBar(
                        "Importing Textures",
                        $"Processing {brfName} ({i + 1}/{totalBrfs})",
                        (float)i / totalBrfs);

                    int copied = ImportTexturesForBrf(brfFolder, availableTextures);
                    totalCopied += copied;
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }

            ProcessImportedTextures(resourcePath);

            AssetDatabase.Refresh();
            Debug.Log($"Textures imported: {totalCopied} files across {brfFolders.Count} BRF folders");
        }

        private static int ImportTexturesForBrf(string brfFolder, Dictionary<string, string> availableTextures)
        {
            string dataJsonPath = Path.Combine(brfFolder, "data.json");
            if (!File.Exists(dataJsonPath))
                return 0;

            string brfName = Path.GetFileName(brfFolder);
            var importer = new BrfDataImporter(brfName, brfFolder);
            if (!importer.LoadDataJson())
                return 0;

            var textures = importer.GetTextures();
            if (textures == null || textures.Count == 0)
                return 0;

            string texturesFolder = Path.Combine(brfFolder, "Textures");
            if (!Directory.Exists(texturesFolder))
                Directory.CreateDirectory(texturesFolder);

            int copied = 0;
            foreach (var texName in textures)
            {
                if (availableTextures.TryGetValue(texName.name, out string sourcePath))
                {
                    Debug.LogError(sourcePath);
                    string destPath = Path.Combine(texturesFolder, Path.GetFileName(sourcePath));

                    if (File.Exists(destPath))
                        continue;

                    try
                    {
                        File.Copy(sourcePath, destPath, true);
                        copied++;
                    }
                    catch (Exception ex)
                    {
                        Debug.LogWarning($"Failed to copy texture {texName}: {ex.Message}");
                    }
                }
            }

            return copied;
        }

        private static void AddTextureReference(HashSet<string> set, string textureName)
        {
            if (string.IsNullOrEmpty(textureName) || 
                textureName.Equals("none", StringComparison.OrdinalIgnoreCase))
                return;

            string name = Path.GetFileNameWithoutExtension(textureName);
            set.Add(name);
        }

        private static void ProcessImportedTextures(string resourcePath)
        {
            var allTexturesFolders = Directory.GetDirectories(resourcePath, "Textures", SearchOption.AllDirectories);

            foreach (var texturesFolder in allTexturesFolders)
            {
                string fullPath = Path.Combine(MBPathHelpers.UnityProjectFolderPath(), texturesFolder);
                if (Directory.Exists(fullPath))
                {
                    DDSPostprocessor.Procces(fullPath);
                }
            }
        }

        private static bool CheckTexturesImported(string moduleName)
        {
            string resourcePath = MBPathHelpers.ModResourcePath(moduleName);
            if (!Directory.Exists(resourcePath))
                return false;

            var brfFolders = Directory.GetDirectories(resourcePath);
            foreach (var brfFolder in brfFolders)
            {
                string texturesFolder = Path.Combine(brfFolder, "Textures");
                if (Directory.Exists(texturesFolder) && 
                    Directory.GetFiles(texturesFolder).Length > 0)
                {
                    return true;
                }
            }

            return false;
        }

        #endregion

        private void ExportCompatibilityJsonFiles(string moduleName, string brfName, string brfFolderPath)
        {
            var importer = new BrfDataImporter(moduleName, brfFolderPath);

            if (!importer.LoadDataJson())
                return;

            importer.ExportMaterialDataJson(brfFolderPath);
            importer.ExportMeshDataJson(brfFolderPath);
        }

        /// <summary>
        /// Step 3: Create Materials using BRF data with proper flag handling
        /// </summary>
        private void ExecuteCreateMaterials(MBModule module)
        {
            EnsureContexts();

            string resourcePath = MBPathHelpers.ModResourcePath(module.ID);

            if (!Directory.Exists(resourcePath))
            {
                Debug.LogError($"Resource path not found: {resourcePath}");
                return;
            }

            var brfFolders = Directory.GetDirectories(resourcePath)
                .Where(d => File.Exists(Path.Combine(d, "data.json")))
                .ToList();

            if (brfFolders.Count == 0)
            {
                Debug.Log("No BRF Sync data found, using legacy material creation");
                return;
            }

            int totalMaterials = 0;
            int totalBrfs = brfFolders.Count;

            try
            {
                for (int i = 0; i < brfFolders.Count; i++)
                {
                    string brfFolder = brfFolders[i];
                    string brfName = Path.GetFileName(brfFolder);

                    EditorUtility.DisplayProgressBar(
                        "Creating Materials",
                        $"Processing {brfName} ({i + 1}/{totalBrfs})",
                        (float)i / totalBrfs);

                    var importer = new BrfDataImporter(
                        module.ID, brfFolder, _moduleContext, _nativeContext);

                    if (!importer.LoadDataJson() || importer.GetMaterials().Count == 0)
                        continue;

                    var materialsPath = Path.Combine(brfFolder, "Materials");
                    if (!Directory.Exists(materialsPath))
                        Directory.CreateDirectory(materialsPath);
                        
                    var materials = importer.CreateAllMaterials(materialsPath);
                    totalMaterials += materials.Count;
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }

            _moduleContext?.BuildFileCache();

            _statusMessage = $"Created {totalMaterials} materials from {brfFolders.Count} BRF files";
            Debug.Log(_statusMessage);
        }

        private static bool CheckMaterialsCreated(string moduleName)
        {
            string resourcePath = MBPathHelpers.ModResourcePath(moduleName);
            if (!Directory.Exists(resourcePath))
                return false;

            var brfFolders = Directory.GetDirectories(resourcePath);
            foreach (var brfFolder in brfFolders)
            {
                string materialsFolder = Path.Combine(brfFolder, "Materials");
                if (Directory.Exists(materialsFolder) && 
                    Directory.GetFiles(materialsFolder, "*.mat").Length > 0)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Step 4: Populate MBBrfData assets with mesh, material, and collision references
        /// </summary>
        private void ExecutePopulateBrfData(MBModule module)
        {
            EnsureContexts();

            string resourcePath = MBPathHelpers.ModResourcePath(module.ID);

            if (!Directory.Exists(resourcePath))
            {
                Debug.LogError($"Resource path not found: {resourcePath}");
                return;
            }

            var brfFolders = Directory.GetDirectories(resourcePath)
                .Where(d => File.Exists(Path.Combine(d, "data.json")))
                .ToList();

            int totalPopulated = 0;
            int totalModelGroups = 0;
            int totalBrfs = brfFolders.Count;

            try
            {
                for (int i = 0; i < brfFolders.Count; i++)
                {
                    string brfFolder = brfFolders[i];
                    string brfName = Path.GetFileName(brfFolder);

                    EditorUtility.DisplayProgressBar(
                        "Populating BRF Data",
                        $"Processing {brfName} ({i + 1}/{totalBrfs})",
                        (float)i / totalBrfs);

                    string assetPath = Path.Combine(brfFolder, $"{brfName}.asset");
                    var brfData = AssetDatabase.LoadAssetAtPath<MBBrfData>(assetPath);

                    if (brfData == null)
                    {
                        brfData = ScriptableObject.CreateInstance<MBBrfData>();
                        brfData.name = brfName;
                        brfData.BrfName = brfName;
                        brfData.ModuleName = module.ID;
                        brfData.FolderPath = brfFolder;
                        AssetDatabase.CreateAsset(brfData, assetPath);
                    }

                    var result = BrfDataPopulator.PopulateBrfData(
                        brfData, brfFolder, _moduleContext, _nativeContext);

                    if (result.MeshCount > 0 || result.MaterialCount > 0)
                    {
                        totalPopulated++;
                        totalModelGroups += result.ModelGroupCount;
                    }

                    EditorUtility.SetDirty(brfData);
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }

            AssetDatabase.SaveAssets();

            _statusMessage = $"Populated {totalPopulated} BRF assets with {totalModelGroups} model groups";
            Debug.Log(_statusMessage);
        }

        private static bool CheckBrfDataPopulated(string moduleName)
        {
            string resourcePath = MBPathHelpers.ModResourcePath(moduleName);
            if (!Directory.Exists(resourcePath))
                return false;

            var brfAssets = Directory.GetFiles(resourcePath, "*.asset", SearchOption.AllDirectories);
            foreach (var assetPath in brfAssets)
            {
                var brfData = AssetDatabase.LoadAssetAtPath<MBBrfData>(assetPath);
                if (brfData != null && brfData.Meshes.Count > 0)
                {
                    return true;
                }
            }

            return false;
        }

        #endregion

        #region Phase 3: Model Pipeline

        /// <summary>
        /// Step 7: Create BRF DataBase - aggregate all MBBrfData into ModBrfDataBase
        /// </summary>
        private void CreateBRFDataBase(MBModule module)
        {
            EnsureContexts();

            string dbPath = MBPathHelpers.ModBRFDataBasePath(module.ID);

            // Ensure config directory exists
            string configDir = Path.GetDirectoryName(dbPath);
            if (!string.IsNullOrEmpty(configDir) && !Directory.Exists(configDir))
                Directory.CreateDirectory(configDir);

            var brfDataBase = AssetDatabase.LoadAssetAtPath<ModBrfDataBase>(dbPath);

            if (brfDataBase == null)
            {
                brfDataBase = ScriptableObject.CreateInstance<ModBrfDataBase>();
                brfDataBase.name = $"{module.ID}_BrfDataBase";
                AssetDatabase.CreateAsset(brfDataBase, dbPath);
            }

            brfDataBase.Clear();
            brfDataBase.ModuleName = module.ID;

            string resourcePath = MBPathHelpers.ModResourcePath(module.ID);
            int addedCount = 0;

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
                    addedCount++;
                }
            }

            brfDataBase.BuildLookups();

            EditorUtility.SetDirty(brfDataBase);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            _statusMessage = $"BRF DataBase built with {addedCount} BRF assets";
            Debug.Log(_statusMessage);
        }

        /// <summary>
        /// Check if the BRF DataBase asset exists and has entries.
        /// </summary>
        private static bool CheckBrfDataBaseExists(string moduleName)
        {
            string dbPath = MBPathHelpers.ModBRFDataBasePath(moduleName);
            var db = AssetDatabase.LoadAssetAtPath<ModBrfDataBase>(dbPath);
            return db != null && db.BrfCount > 0;
        }

        /// <summary>
        /// Step 8: Assign Materials - resolve Unity Materials for every mesh in every
        /// model group using the ModBrfDataBase with cross-BRF + context fallback.
        /// 
        /// Resolution order per mesh MaterialName:
        ///   1. Same BRF's BrfMaterialEntry.UnityMaterial  (local)
        ///   2. Cross-BRF via ModBrfDataBase.FindMaterial() (sibling BRFs)
        ///   3. Module context fallback                      (file system scan)
        ///   4. Native context fallback                      (game base files)
        /// </summary>
        private void ExecuteAssignMaterials(MBModule module)
        {
            EnsureContexts();

            string dbPath = MBPathHelpers.ModBRFDataBasePath(module.ID);
            var brfDataBase = AssetDatabase.LoadAssetAtPath<ModBrfDataBase>(dbPath);

            if (brfDataBase == null)
            {
                Debug.LogError($"BRF DataBase not found for {module.ID} at {dbPath}");
                return;
            }

            brfDataBase.BuildLookups();

            int totalAssigned = 0;
            int totalUnresolved = 0;
            int totalGroups = 0;

            try
            {
                int brfIndex = 0;
                int brfCount = brfDataBase.BrfAssets.Count;

                foreach (var brfData in brfDataBase.BrfAssets)
                {
                    if (brfData == null) continue;

                    brfIndex++;
                    EditorUtility.DisplayProgressBar(
                        "Assigning Materials",
                        $"Processing {brfData.BrfName} ({brfIndex}/{brfCount})",
                        (float)brfIndex / brfCount);

                    foreach (var group in brfData.ModelGroups)
                    {
                        totalGroups++;

                        // Assign materials to base meshes
                        foreach (var meshEntry in group.MeshEntries)
                        {
                            var mat = ResolveMaterialForMesh(meshEntry, brfData, brfDataBase);

                            if (mat != null)
                                totalAssigned++;
                            else if (!string.IsNullOrEmpty(meshEntry.MaterialName))
                                totalUnresolved++;
                        }

                        // Assign materials to LOD meshes
                        foreach (var lodEntry in group.LodMeshEntries)
                        {
                            var mat = ResolveMaterialForMesh(lodEntry, brfData, brfDataBase);

                            if (mat != null)
                                totalAssigned++;
                            else if (!string.IsNullOrEmpty(lodEntry.MaterialName))
                                totalUnresolved++;
                        }
                    }

                    EditorUtility.SetDirty(brfData);
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }

            AssetDatabase.SaveAssets();

            // Rebuild terrain palette and module material cache
            TerrainPaletteHelpers.CreateUniqueTerrainPalette(module);

            _statusMessage = $"Materials: {totalAssigned} assigned, {totalUnresolved} unresolved across {totalGroups} groups";

            if (totalUnresolved > 0)
                Debug.LogWarning($"Material assignment: {totalUnresolved} materials could not be resolved");

            Debug.Log(_statusMessage);
        }

        /// <summary>
        /// Resolve the Unity Material for a mesh entry using the full fallback chain.
        /// Backfills the BrfMaterialEntry.UnityMaterial when resolved via fallback.
        /// </summary>
        private Material ResolveMaterialForMesh(
            BrfMeshEntry meshEntry,
            MBBrfData ownerBrf,
            ModBrfDataBase brfDataBase)
        {
            if (string.IsNullOrEmpty(meshEntry.MaterialName))
                return null;

            string matName = meshEntry.MaterialName;

            // 1. Same BRF - populated during PopulateBrfData step
            var localEntry = ownerBrf.GetMaterialEntry(matName);
            if (localEntry?.UnityMaterial != null)
                return localEntry.UnityMaterial;

            // 2. Cross-BRF - another BRF in the same module
            var crossEntry = brfDataBase.FindMaterialEntry(matName);
            if (crossEntry?.UnityMaterial != null)
            {
                BackfillMaterial(localEntry, crossEntry.UnityMaterial);
                return crossEntry.UnityMaterial;
            }

            // 3. Module context - file system scan fallback
            Material contextMat = _moduleContext?.GetMaterial(matName);
            if (contextMat != null)
            {
                BackfillMaterial(localEntry, contextMat);
                return contextMat;
            }

            // 4. Native context - game base files
            contextMat = _nativeContext?.GetMaterial(matName);
            if (contextMat != null)
            {
                BackfillMaterial(localEntry, contextMat);
                return contextMat;
            }

            return null;
        }

        /// <summary>
        /// Backfill a local BrfMaterialEntry with a resolved material reference
        /// so future lookups don't need the fallback chain.
        /// </summary>
        private static void BackfillMaterial(BrfMaterialEntry localEntry, Material material)
        {
            if (localEntry != null && localEntry.UnityMaterial == null)
                localEntry.UnityMaterial = material;
        }

        /// <summary>
        /// Check if materials have been assigned across BRF model groups.
        /// </summary>
        private static bool CheckMaterialsAssigned(string moduleName)
        {
            string dbPath = MBPathHelpers.ModBRFDataBasePath(moduleName);
            var db = AssetDatabase.LoadAssetAtPath<ModBrfDataBase>(dbPath);
            if (db == null || db.BrfCount == 0) return false;

            foreach (var brf in db.BrfAssets)
            {
                if (brf == null) continue;
                if (brf.Materials.Any(m => m.UnityMaterial != null))
                    return true;
            }

            return false;
        }

        #endregion

        /// <summary>
        /// Finds and validates the directory containing required header files.
        /// Searches the module system path and subdirectories.
        /// </summary>
        /// <param name="moduleSystemPath">Base module system path to search</param>
        /// <param name="foundPath">Output: actual directory containing header files</param>
        /// <param name="errorMessage">Output: error message if validation fails</param>
        /// <returns>True if header files found, false otherwise</returns>
        private void ExecuteJsonConversion(MBModule module)
        {
            string exportPath = MBPathHelpers.ModDataBaseJsonDirectoryPath(module.ID);
            if (!Directory.Exists(exportPath))
                Directory.CreateDirectory(exportPath);

            var foundConverters = MBDataJsonImporter.ConverterConfigs.Where(c => c.IsFound).ToList();

            if (foundConverters.Count == 0)
                throw new Exception("No converter files found. Configure paths first.");

            int success = 0;
            int failed = 0;
            string converterFolder = MBPathHelpers.MBDataBaseJsonConvertersPath();

            Debug.Log($"=== JSON Conversion Started ===");
            Debug.Log($"Export Path: {exportPath}");
            Debug.Log($"Converter Folder: {converterFolder}");
            Debug.Log($"Found {foundConverters.Count} converters to run");

            string moduleSystemPath = module.ModuleSystemPath;
            Debug.Log($"Module System Path (configured): {moduleSystemPath}");

            string exePath = Path.Combine(converterFolder, "ms_converter.exe");
            if (!File.Exists(exePath))
            {
                Debug.LogError($"ms_converter.exe not found: {exePath}");
                _statusMessage = "JSON: ms_converter.exe not found";
                return;
            }

            foreach (var config in foundConverters)
            {
                string workingDir = string.IsNullOrEmpty(config.ManualPath)
                    ? module.ModuleSystemPath
                    : Path.GetDirectoryName(config.ManualPath);

                string fileName = string.IsNullOrEmpty(config.ManualPath)
                    ? config.RequiredModuleFile
                    : Path.GetFileName(config.ManualPath);

                string converterName = config.ConverterFileName.Replace("convert_", "").Replace(".py", "");

                Debug.Log($"\n--- Processing: {converterName} ---");
                Debug.Log($"Working Directory: {workingDir}");

                try
                {
                    if (!Directory.Exists(workingDir))
                    {
                        Debug.LogError($"Working directory not found: {workingDir}");
                        failed++;
                        continue;
                    }

                    var startInfo = new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = exePath,
                        Arguments = $"--converter {converterName} --source \"{workingDir}\" --filename \"{fileName}\" --output \"{exportPath}\" --quiet",
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        CreateNoWindow = true
                    };

                    string stdout = "";
                    string stderr = "";
                    int exitCode = -1;

                    using (var process = System.Diagnostics.Process.Start(startInfo))
                    {
                        stdout = process.StandardOutput.ReadToEnd();
                        stderr = process.StandardError.ReadToEnd();
                        
                        process.WaitForExit();
                        exitCode = process.ExitCode;
                    }

                    Debug.Log($"Exit Code: {exitCode}");

                    if (exitCode == 0)
                    {
                        if (!string.IsNullOrEmpty(stdout)) Debug.Log($"STDOUT:\n{stdout}");
                        if (!string.IsNullOrEmpty(stderr)) Debug.LogWarning($"STDERR:\n{stderr}");

                        string targetJson = Path.Combine(exportPath, config.OutputJsonName);
                        if (File.Exists(targetJson))
                        {
                            success++;
                            Debug.Log($"✓ SUCCESS: {converterName} → {config.OutputJsonName}");
                        }
                        else
                        {
                            failed++;
                            Debug.LogError($"✗ FAILED: Output JSON not found: {targetJson}");
                        }
                    }
                    else
                    {
                        failed++;
                        Debug.LogError($"✗ FAILED: Converter {converterName} exited with code {exitCode}");
                        if (!string.IsNullOrEmpty(stdout)) Debug.LogError($"STDOUT (Failed):\n{stdout}");
                        if (!string.IsNullOrEmpty(stderr)) Debug.LogError($"STDERR (Failed):\n{stderr}");
                    }
                }
                catch (Exception ex)
                {
                    failed++;
                    Debug.LogError($"✗ EXCEPTION running {converterName}:\n{ex.Message}\n{ex.StackTrace}");
                }
            }

            Debug.Log($"\n=== JSON Conversion Complete ===");
            Debug.Log($"Success: {success}, Failed: {failed}");
            _statusMessage = $"JSON: {success} succeeded, {failed} failed";
        }

        private void ExecuteScriptableObjectCreation(MBModule module)
        {
            string jsonFolder = MBPathHelpers.ModDataBaseJsonDirectoryPath(module.ID);
            string outputPath = MBPathHelpers.ModDataBaseDirectoryPath(module.ID);

            EnsureDirectoriesExist(outputPath);
            ClearModuleData(module);

            _dataCreator.InitializeImporters(module);
            _dataCreator.ScanForJsonFiles();
            _dataCreator.ImportAllFiles();
        }

        #endregion

        #region Status Management

        private enum StepStatus
        {
            Pending,
            InProgress,
            Complete,
            Error,
            Blocked
        }

        private void RefreshAllStepStatuses()
        {
            foreach (ImportStep step in Enum.GetValues(typeof(ImportStep)))
            {
                RefreshStepStatus(step);
            }
        }

        private void RefreshStepStatus(ImportStep step)
        {
            var info = _steps[step];

            if (!AreDependenciesMet(step))
            {
                _stepStatuses[step] = StepStatus.Blocked;
                return;
            }

            if (info.CheckComplete != null && info.CheckComplete(_module))
            {
                _stepStatuses[step] = StepStatus.Complete;
            }
            else
            {
                _stepStatuses[step] = StepStatus.Pending;
            }
        }

        private StepStatus GetStepStatus(ImportStep step)
        {
            return _stepStatuses.TryGetValue(step, out var status) ? status : StepStatus.Pending;
        }

        private bool CanExecuteStep(ImportStep step)
        {
            return AreDependenciesMet(step);
        }

        private bool AreDependenciesMet(ImportStep step)
        {
            var info = _steps[step];

            foreach (var dep in info.Dependencies)
            {
                if (GetStepStatus(dep) != StepStatus.Complete)
                    return false;
            }

            return true;
        }

        private Color GetStatusColor(StepStatus status)
        {
            return status switch
            {
                StepStatus.Complete => new Color(0.2f, 0.8f, 0.2f),
                StepStatus.InProgress => new Color(0.2f, 0.6f, 1f),
                StepStatus.Error => new Color(0.9f, 0.2f, 0.2f),
                StepStatus.Blocked => new Color(0.5f, 0.5f, 0.5f),
                _ => Color.white
            };
        }

        #endregion

        #region Helper Methods

        private void ScanForModuleFiles()
        {
            if (string.IsNullOrEmpty(_module?.ModuleSystemPath) ||
                !Directory.Exists(_module.ModuleSystemPath))
                return;

            foreach (var config in MBDataJsonImporter.ConverterConfigs)
            {
                string modulePath = Path.Combine(_module.ModuleSystemPath, config.RequiredModuleFile);
                config.IsFound = File.Exists(modulePath);

                if (!config.IsFound)
                {
                    var files = Directory.GetFiles(_module.ModuleSystemPath, config.RequiredModuleFile,
                        SearchOption.AllDirectories);
                    if (files.Length > 0)
                    {
                        config.ManualPath = files[0];
                        config.IsFound = true;
                    }
                }
            }
        }

        private bool CheckJsonFilesExist(MBModule module)
        {
            string jsonFolder = MBPathHelpers.ModDataBaseJsonDirectoryPath(module.ID);
            if (!Directory.Exists(jsonFolder)) return false;
            return Directory.GetFiles(jsonFolder, "*.json").Length >= 5;
        }

        private bool CheckScriptableObjectsExist(MBModule module)
        {
            return module.factions.Count > 0 || module.troops.Count > 0 || module.items.Count > 0 || module.particleSystems.Count > 0;
        }

        private void EnsureContexts()
        {
            if (_moduleContext == null)
            {
                _moduleContext = new MBModuleImportContext(_module.ID);
                _moduleContext.BuildFileCache();
                _moduleContext.BuildMeshDataCache();
            }

            if (_nativeContext == null && _module.ID != "Native")
            {
                _nativeContext = new MBModuleImportContext("Native");
                _nativeContext.BuildFileCache();
                _nativeContext.BuildMeshDataCache();
            }
        }

        private void RebuildContexts()
        {
            _moduleContext?.Dispose();
            _nativeContext?.Dispose();
            _moduleContext = null;
            _nativeContext = null;
        }

        private void EnsureDirectoriesExist(string rootPath)
        {
            string[] subdirs =
            {
                "Factions", "Flora", "Items", "MapIcons", "Parties",
                "PartyTemplates", "SceneProps", "Scenes", "Skins", "Troops","ParticleSystems"
            };

            if (!Directory.Exists(rootPath))
                Directory.CreateDirectory(rootPath);

            foreach (var sub in subdirs)
            {
                string path = Path.Combine(rootPath, sub);
                if (!Directory.Exists(path))
                    Directory.CreateDirectory(path);
            }

            AssetDatabase.Refresh();
        }

        private void ClearModuleData(MBModule module)
        {
            module.factions.Clear();
            module.flora.Clear();
            module.items.Clear();
            module.mapIcons.Clear();
            module.parties.Clear();
            module.partyTemplates.Clear();
            module.sceneProps.Clear();
            module.scenes.Clear();
            module.skins.Clear();
            module.troops.Clear();
            module.particleSystems.Clear();
        }

        private void ClearAllModuleData()
        {
            ClearModuleData(_module);

            EditorUtility.SetDirty(_module);
            AssetDatabase.SaveAssets();
        }

        #endregion
    }
}
