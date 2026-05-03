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
    /// Scene Synchronizer Window - detects and applies changes to scene data
    /// (module_scenes.py) without a full project reimport.
    /// </summary>
    public class SceneSynchronizerWindow : EditorWindow
    {

        // CONSTANTS


        private static class Styles
        {
            public static readonly Color Added = new(0.2f, 0.85f, 0.2f);
            public static readonly Color Removed = new(0.9f, 0.3f, 0.3f);
            public static readonly Color Modified = new(1f, 0.75f, 0.1f);
            public static readonly Color Unchanged = new(0.6f, 0.6f, 0.6f);
            public static readonly Color CategoryTag = new(0.5f, 0.7f, 0.9f);
        }


        // STATE


        private MBModule _module;
        private string[] _availableModules;
        private int _selectedModuleIndex;
        private bool _isScanning;
        private string _statusMessage = "";

        // Scan results
        private SceneDiffResult _sceneDiff;
        private Vector2 _listScroll;
        private bool _showUnchanged;
        private string _filter = "";

        // Category filter
        private string[] _categories;
        private int _selectedCategoryIndex; // 0 = "All"


        // MENU ITEM


        [MenuItem("MBToolset/Sync/Scene Synchronizer", false, 41)]
        public static void ShowWindow()
        {
            var window = GetWindow<SceneSynchronizerWindow>("Scene Synchronizer");
            window.minSize = new Vector2(650, 400);
        }


        // LIFECYCLE


        private void OnEnable() => RefreshModuleList();


        // MAIN GUI


        private void OnGUI()
        {
            DrawHeader();
            UIHelpers.DrawUILine(UIColors.GrayLine, 1, 2);

            if (_module == null)
            {
                EditorGUILayout.HelpBox(
                    "Select a module to begin synchronizing scene data.",
                    MessageType.Info);
                return;
            }

            DrawToolbar();
            DrawSceneList();
            DrawActionBar();
        }


        // HEADER


        private void DrawHeader()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.BeginHorizontal();
            {
                EditorGUILayout.LabelField("Scene Synchronizer", EditorStyles.boldLabel, GUILayout.Width(150));

                if (_availableModules != null && _availableModules.Length > 0)
                {
                    EditorGUI.BeginChangeCheck();
                    _selectedModuleIndex = EditorGUILayout.Popup(_selectedModuleIndex, _availableModules);
                    if (EditorGUI.EndChangeCheck())
                        LoadModule(_availableModules[_selectedModuleIndex]);
                }

                GUILayout.FlexibleSpace();

                if (!string.IsNullOrEmpty(_statusMessage))
                {
                    EditorGUILayout.LabelField(_statusMessage, EditorStyles.miniLabel, GUILayout.MaxWidth(400));
                }
            }
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.EndVertical();
        }


        // TOOLBAR


        private void DrawToolbar()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            {
                bool hasModSys = !string.IsNullOrEmpty(_module?.ModuleSystemPath);

                EditorGUI.BeginDisabledGroup(!hasModSys || _isScanning);
                if (GUILayout.Button("🔍 Scan Scenes", EditorStyles.toolbarButton, GUILayout.Width(110)))
                    ScanScenes();
                EditorGUI.EndDisabledGroup();

                GUILayout.Space(8);
                _filter = EditorGUILayout.TextField(_filter, EditorStyles.toolbarSearchField, GUILayout.Width(180));

                // Category dropdown
                if (_categories != null && _categories.Length > 1)
                {
                    GUILayout.Space(4);
                    _selectedCategoryIndex = EditorGUILayout.Popup(
                        _selectedCategoryIndex, _categories, EditorStyles.toolbarPopup, GUILayout.Width(100));
                }

                GUILayout.FlexibleSpace();

                _showUnchanged = GUILayout.Toggle(_showUnchanged, "Show All", EditorStyles.toolbarButton, GUILayout.Width(60));

                if (_sceneDiff != null)
                {
                    EditorGUILayout.LabelField(_sceneDiff.GetSummary(), EditorStyles.miniLabel, GUILayout.Width(300));
                }
            }
            EditorGUILayout.EndHorizontal();

            if (_module != null && string.IsNullOrEmpty(_module.ModuleSystemPath))
            {
                EditorGUILayout.HelpBox(
                    "Module System Path not configured. Set it in the Module Importer to enable Scenes scan.",
                    MessageType.Warning);
            }
        }


        // SCENE LIST


        private void DrawSceneList()
        {
            if (_sceneDiff == null)
            {
                EditorGUILayout.HelpBox("Click Scan to compare module scenes against existing data.", MessageType.Info);
                return;
            }

            _listScroll = EditorGUILayout.BeginScrollView(_listScroll);

            var visible = _sceneDiff.Entries.AsEnumerable();

            if (!_showUnchanged)
                visible = visible.Where(e => e.HasChanges);

            if (!string.IsNullOrEmpty(_filter))
                visible = visible.Where(e => e.Name.IndexOf(_filter, StringComparison.OrdinalIgnoreCase) >= 0);

            // Category filter
            if (_selectedCategoryIndex > 0 && _categories != null && _selectedCategoryIndex < _categories.Length)
            {
                string selectedCategory = _categories[_selectedCategoryIndex];
                visible = visible.Where(e =>
                {
                    var snap = e.NewValue ?? e.OldValue;
                    return snap != null && string.Equals(snap.Category, selectedCategory, StringComparison.OrdinalIgnoreCase);
                });
            }

            var list = visible.ToList();

            if (list.Count == 0)
            {
                EditorGUILayout.LabelField(
                    _showUnchanged ? "No scenes found." : "No scene changes detected.",
                    EditorStyles.centeredGreyMiniLabel);
            }
            else
            {
                foreach (var entry in list)
                {
                    DrawSceneEntry(entry);
                }
            }

            EditorGUILayout.EndScrollView();
        }

        private void DrawSceneEntry(BrfDiffEntry<SceneSnapshot> entry)
        {
            var snap = entry.NewValue ?? entry.OldValue;

            Color statusColor = entry.Status switch
            {
                DiffStatus.Added => Styles.Added,
                DiffStatus.Removed => Styles.Removed,
                DiffStatus.Modified => Styles.Modified,
                _ => Styles.Unchanged
            };

            string statusIcon = entry.Status switch
            {
                DiffStatus.Added => "+",
                DiffStatus.Removed => "−",
                DiffStatus.Modified => "~",
                _ => "·"
            };

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.BeginHorizontal();


            if (entry.HasChanges)
                entry.Selected = EditorGUILayout.Toggle(entry.Selected, GUILayout.Width(16));
            else
                GUILayout.Space(20);


            var badgeStyle = new GUIStyle(EditorStyles.miniLabel)
            {
                normal = { textColor = statusColor },
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
            EditorGUILayout.LabelField(statusIcon, badgeStyle, GUILayout.Width(14));


            var nameStyle = new GUIStyle(EditorStyles.label)
            {
                normal = { textColor = entry.HasChanges ? Color.white : Styles.Unchanged }
            };
            EditorGUILayout.LabelField(entry.Name, nameStyle, GUILayout.MinWidth(200));


            if (snap != null && !string.IsNullOrEmpty(snap.Category))
            {
                var catStyle = new GUIStyle(EditorStyles.miniLabel)
                {
                    normal = { textColor = Styles.CategoryTag }
                };
                EditorGUILayout.LabelField($"[{snap.Category}]", catStyle, GUILayout.Width(90));
            }


            if (snap != null)
            {
                var infoStyle = new GUIStyle(EditorStyles.miniLabel)
                {
                    normal = { textColor = new Color(0.65f, 0.65f, 0.65f) }
                };

                string meshInfo = !string.IsNullOrEmpty(snap.MeshName) && snap.MeshName != "none"
                    ? $"mesh: {snap.MeshName}"
                    : "";
                if (!string.IsNullOrEmpty(meshInfo))
                    EditorGUILayout.LabelField(meshInfo, infoStyle, GUILayout.Width(120));
            }

            EditorGUILayout.EndHorizontal();


            if (entry.Status == DiffStatus.Modified && entry.ChangedFields.Count > 0)
            {
                var detailStyle = new GUIStyle(EditorStyles.miniLabel)
                {
                    normal = { textColor = new Color(0.75f, 0.75f, 0.75f) },
                    wordWrap = true
                };
                EditorGUI.indentLevel++;
                EditorGUILayout.LabelField(string.Join(" | ", entry.ChangedFields), detailStyle);
                EditorGUI.indentLevel--;
            }

            EditorGUILayout.EndVertical();
        }


        // ACTION BAR


        private void DrawActionBar()
        {
            if (_sceneDiff == null) return;

            EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);

            int selected = _sceneDiff.Entries.Count(e => e.Selected && e.HasChanges);

            EditorGUI.BeginDisabledGroup(selected == 0 || _isScanning);
            if (GUILayout.Button($"🔄 Sync Scenes ({selected})", GUILayout.Height(22)))
                ApplySyncScenes();
            EditorGUI.EndDisabledGroup();

            GUILayout.FlexibleSpace();

            EditorGUI.BeginDisabledGroup(!_sceneDiff.HasChanges);
            if (GUILayout.Button("Select All", EditorStyles.miniButton, GUILayout.Width(75)))
            {
                foreach (var e in _sceneDiff.Entries.Where(e => e.HasChanges))
                    e.Selected = true;
            }
            if (GUILayout.Button("Deselect All", EditorStyles.miniButton, GUILayout.Width(75)))
            {
                foreach (var e in _sceneDiff.Entries)
                    e.Selected = false;
            }
            EditorGUI.EndDisabledGroup();

            EditorGUILayout.EndHorizontal();
        }


        // SCAN LOGIC


        private void ScanScenes()
        {
            if (_module == null) return;

            _isScanning = true;
            _statusMessage = "Scanning scenes...";

            try
            {
                EditorUtility.DisplayProgressBar("Scenes Scan", "Running converter...", 0.2f);

                // Run the Python converter to get fresh JSON
                int result = BrfSyncExecutor.SyncJsonData(
                    _module,
                    "convert_scenes.py",
                    "scenes_full.json",
                    (_, __) => { }); // Don't import yet, just generate JSON

                if (result == 0)
                {
                    _statusMessage = "Scenes converter failed.";
                    return;
                }

                EditorUtility.DisplayProgressBar("Scenes Scan", "Comparing...", 0.6f);

                // Load the fresh JSON
                string jsonPath = Path.Combine(
                    MBPathHelpers.ModDataBaseJsonDirectoryPath(_module.ID),
                    "scenes_full.json");

                if (!File.Exists(jsonPath))
                {
                    _statusMessage = "scenes_full.json not found.";
                    return;
                }

                string json = File.ReadAllText(jsonPath);
                var decoded = MBSceneJsonDecoder.DecodeScenes(
                    JsonHelper.FromJson<MBSceneJsonDecoder.SceneJsonData>(json));

                // Merge Native's existing scenes into the baseline
                var existingScenes = new List<MBSceneData>(_module.scenes);
                if (_module.ID != "Native")
                {
                    var nativeModule = LoadNativeModule();
                    if (nativeModule != null)
                        existingScenes.AddRange(nativeModule.scenes);
                }

                // Compare
                _sceneDiff = CompareScenes(decoded, existingScenes);

                // Build category list for dropdown
                RebuildCategoryList();

                _statusMessage = $"Scenes: {_sceneDiff.GetSummary()}";
            }
            catch (Exception ex)
            {
                _statusMessage = $"Scenes scan failed: {ex.Message}";
                Debug.LogError($"Scenes scan: {ex}");
            }
            finally
            {
                EditorUtility.ClearProgressBar();
                _isScanning = false;
                Repaint();
            }
        }

        /// <summary>
        /// Compare decoded scenes against existing MBSceneData SOs with field-level diffing.
        /// </summary>
        private static SceneDiffResult CompareScenes(
            MBSceneJsonDecoder.DecodedSceneData[] decoded,
            List<MBSceneData> existing)
        {
            var result = new SceneDiffResult();

            // Build lookup of existing scenes by ID
            var existingLookup = new Dictionary<string, MBSceneData>(StringComparer.OrdinalIgnoreCase);
            if (existing != null)
            {
                foreach (var asset in existing)
                {
                    if (asset != null && !string.IsNullOrEmpty(asset.SceneID))
                        existingLookup[asset.SceneID] = asset;
                }
            }

            // Track which existing scenes we've seen in the decoded data
            var seenIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            if (decoded != null)
            {
                foreach (var d in decoded)
                {
                    if (d == null || string.IsNullOrEmpty(d.sceneId)) continue;

                    seenIds.Add(d.sceneId);
                    var newSnap = SceneSnapshot.FromDecoded(d);

                    if (existingLookup.TryGetValue(d.sceneId, out var existingAsset))
                    {
                        // Exists - check for modifications
                        var oldSnap = SceneSnapshot.FromAsset(existingAsset);
                        var changedFields = SceneSnapshot.GetChangedFields(oldSnap, newSnap);

                        var entry = new BrfDiffEntry<SceneSnapshot>
                        {
                            Name = d.sceneId,
                            Status = changedFields.Count > 0 ? DiffStatus.Modified : DiffStatus.Unchanged,
                            OldValue = oldSnap,
                            NewValue = newSnap,
                            ChangedFields = changedFields,
                            Selected = changedFields.Count > 0
                        };

                        result.Entries.Add(entry);
                    }
                    else
                    {
                        // New - missing SO
                        var entry = new BrfDiffEntry<SceneSnapshot>
                        {
                            Name = d.sceneId,
                            Status = DiffStatus.Added,
                            OldValue = null,
                            NewValue = newSnap,
                            Selected = true
                        };
                        entry.ChangedFields.Add("New Scene");
                        result.Entries.Add(entry);
                    }
                }
            }

            // Check for removed scenes (exist in SOs but not in decoded JSON)
            if (existing != null)
            {
                foreach (var asset in existing)
                {
                    if (asset == null || string.IsNullOrEmpty(asset.SceneID)) continue;
                    if (seenIds.Contains(asset.SceneID)) continue;

                    var oldSnap = SceneSnapshot.FromAsset(asset);
                    var entry = new BrfDiffEntry<SceneSnapshot>
                    {
                        Name = asset.SceneID,
                        Status = DiffStatus.Removed,
                        OldValue = oldSnap,
                        NewValue = null,
                        Selected = false // Don't auto-select removals
                    };
                    entry.ChangedFields.Add("Removed from code");
                    result.Entries.Add(entry);
                }
            }

            // Sort: Added first, then Modified, then Removed, then Unchanged
            result.Entries.Sort((a, b) =>
            {
                int orderA = GetSortOrder(a.Status);
                int orderB = GetSortOrder(b.Status);
                int order = orderA.CompareTo(orderB);
                return order != 0 ? order : string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase);
            });

            return result;
        }

        private static int GetSortOrder(DiffStatus status) => status switch
        {
            DiffStatus.Added => 0,
            DiffStatus.Modified => 1,
            DiffStatus.Removed => 2,
            DiffStatus.Unchanged => 3,
            _ => 4
        };


        // APPLY LOGIC


        private void ApplySyncScenes()
        {
            if (_sceneDiff == null) return;

            int addCount = _sceneDiff.Entries.Count(e => e.Selected && e.Status == DiffStatus.Added);
            int modCount = _sceneDiff.Entries.Count(e => e.Selected && e.Status == DiffStatus.Modified);
            int remCount = _sceneDiff.Entries.Count(e => e.Selected && e.Status == DiffStatus.Removed);

            string message = $"Sync {addCount + modCount + remCount} scene change(s)?";
            var details = new List<string>();
            if (addCount > 0) details.Add($"{addCount} new");
            if (modCount > 0) details.Add($"{modCount} modified");
            if (remCount > 0) details.Add($"{remCount} removed");
            message += $"\n({string.Join(", ", details)})";

            if (remCount > 0)
                message += "\n\n⚠ Removed scenes will have their ScriptableObjects deleted.";

            if (!EditorUtility.DisplayDialog("Scene Sync", message, "Sync", "Cancel"))
                return;

            _isScanning = true;

            try
            {
                // Use the existing JSON (already generated during scan)
                string jsonPath = Path.Combine(
                    MBPathHelpers.ModDataBaseJsonDirectoryPath(_module.ID),
                    "scenes_full.json");

                // Sync added + modified via the executor
                if ((addCount > 0 || modCount > 0) && File.Exists(jsonPath))
                {
                    BrfSyncExecutor.SyncScenes(jsonPath, _module);
                }

                // Handle removals
                if (remCount > 0)
                {
                    var removedEntries = _sceneDiff.Entries
                        .Where(e => e.Selected && e.Status == DiffStatus.Removed)
                        .ToList();

                    foreach (var entry in removedEntries)
                    {
                        var assetToRemove = _module.scenes
                            .FirstOrDefault(s => s != null &&
                                string.Equals(s.SceneID, entry.Name, StringComparison.OrdinalIgnoreCase));

                        if (assetToRemove != null)
                        {
                            _module.scenes.Remove(assetToRemove);
                            string assetPath = AssetDatabase.GetAssetPath(assetToRemove);
                            if (!string.IsNullOrEmpty(assetPath))
                                AssetDatabase.DeleteAsset(assetPath);
                        }
                    }

                    EditorUtility.SetDirty(_module);
                    AssetDatabase.SaveAssets();
                    AssetDatabase.Refresh();
                }

                // Reload module from disk to pick up newly-created SOs
                string modAssetPath = AssetDatabase.GetAssetPath(_module);
                _module = AssetDatabase.LoadAssetAtPath<MBModule>(modAssetPath);

                // Re-compare against the existing JSON (no need to re-run converter)
                if (File.Exists(jsonPath))
                {
                    string json = File.ReadAllText(jsonPath);
                    var decoded = MBSceneJsonDecoder.DecodeScenes(
                        JsonHelper.FromJson<MBSceneJsonDecoder.SceneJsonData>(json));

                    var existingScenes = new List<MBSceneData>(_module.scenes);
                    if (_module.ID != "Native")
                    {
                        var nativeModule = LoadNativeModule();
                        if (nativeModule != null)
                            existingScenes.AddRange(nativeModule.scenes);
                    }

                    _sceneDiff = CompareScenes(decoded, existingScenes);
                    RebuildCategoryList();
                }

                _statusMessage = $"Synced: +{addCount} ~{modCount} -{remCount} - {_sceneDiff?.GetSummary()}";
            }
            catch (Exception ex)
            {
                _statusMessage = $"Sync failed: {ex.Message}";
                Debug.LogError($"Scene sync: {ex}");
            }
            finally
            {
                _isScanning = false;
                Repaint();
            }
        }


        // HELPERS


        private void RefreshModuleList()
        {
            var modules = new List<string>();
            string modulesPath = MBPathHelpers.EngineModulesFolder();

            if (!string.IsNullOrEmpty(modulesPath) && Directory.Exists(modulesPath))
            {
                foreach (string dir in Directory.GetDirectories(modulesPath))
                {
                    string name = Path.GetFileName(dir);
                    string iniPath = Path.Combine(dir, "module.ini");
                    string modAssetPath = MBPathHelpers.ModAssetPath(name);

                    if (File.Exists(iniPath) && File.Exists(modAssetPath))
                        modules.Add(name);
                }
            }

            _availableModules = modules.ToArray();
            if (_availableModules.Length > 0)
            {
                _selectedModuleIndex = 0;
                LoadModule(_availableModules[0]);
            }
        }

        private void LoadModule(string moduleName)
        {
            string modAssetPath = MBPathHelpers.ModAssetPath(moduleName);
            _module = AssetDatabase.LoadAssetAtPath<MBModule>(modAssetPath);

            _sceneDiff = null;
            _categories = null;
            _selectedCategoryIndex = 0;
            _statusMessage = "";
        }

        private MBModule LoadNativeModule()
        {
            string nativeAssetPath = MBPathHelpers.ModAssetPath("Native");
            return AssetDatabase.LoadAssetAtPath<MBModule>(nativeAssetPath);
        }

        private void RebuildCategoryList()
        {
            if (_sceneDiff == null)
            {
                _categories = null;
                return;
            }

            var cats = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var entry in _sceneDiff.Entries)
            {
                var snap = entry.NewValue ?? entry.OldValue;
                if (snap != null && !string.IsNullOrEmpty(snap.Category))
                    cats.Add(snap.Category);
            }

            var list = new List<string> { "All" };
            list.AddRange(cats.OrderBy(c => c));
            _categories = list.ToArray();
            _selectedCategoryIndex = 0;
        }
    }
}
