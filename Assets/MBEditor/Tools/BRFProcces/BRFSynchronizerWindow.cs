using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BDT.GUI.Helpers;
using MountAndBlade.Data;
using MountAndBlade.Data.Editor;
using Newtonsoft.Json;
using UnityEditor;
using UnityEngine;

namespace MountAndBlade.ModdingToolkit
{
    /// <summary>
    /// BRF Synchronizer Window - tabbed UI for detecting and applying changes
    /// across BRF files, Scene Props, and Items without a full project reimport.
    /// </summary>
    public class BRFSynchronizerWindow : EditorWindow
    {
        // CONSTANTS

        // Tab order matches the required sync workflow
        private enum SyncTab { BRF, Textures, SceneProps, Items }

        private static class Styles
        {
            public static readonly Color Added = new(0.2f, 0.85f, 0.2f);
            public static readonly Color Removed = new(0.9f, 0.3f, 0.3f);
            public static readonly Color Modified = new(1f, 0.75f, 0.1f);
            public static readonly Color Unchanged = new(0.6f, 0.6f, 0.6f);
            public static readonly Color Warning = new(1f, 0.5f, 0.1f);
            public static readonly Color MissingDep = new(1f, 0.35f, 0.35f);
            public static readonly Color Locked = new(0.45f, 0.45f, 0.45f);
        }

        private static readonly string[] TabNames = { "🗂 BRF Files", "🖼 Textures", "📋 Scene Props", "⚔ Items" };

        // STATE

        private MBModule _module;
        private string[] _availableModules;
        private int _selectedModuleIndex;
        private SyncTab _activeTab = SyncTab.BRF;
        private bool _isScanning;
        private string _statusMessage = "";

        // BRF tab state
        private Dictionary<string, BrfDiffResult> _brfScanResults = new();
        private Dictionary<string, string> _brfScanErrors = new();
        private string _selectedBrfName;
        private Vector2 _brfListScroll;
        private Vector2 _brfDetailScroll;
        private bool _showMeshes = true, _showMaterials = true, _showTextures = true, _showBodies = true;
        private bool _showBrfUnchanged;

        // Scene Props tab state
        private ScenePropDiffResult _scenePropDiff;
        private Vector2 _scenePropScroll;
        private bool _showScenePropUnchanged;
        private string _scenePropFilter = "";

        // Items tab state
        private ItemDiffResult _itemDiff;
        private Vector2 _itemScroll;
        private bool _showItemUnchanged;
        private string _itemFilter = "";

        // Textures tab state
        private TextureDiffResult _textureDiff;
        private Vector2 _textureScroll;
        private bool _showTextureUnchanged;
        private string _textureFilter = "";

        // Prefab options
        private bool _onlyNewPrefabs = true; // default: skip existing, only create missing

        // Step completion flags - gates the sync workflow
        private bool _brfSyncDone;
        private bool _textureSyncDone;

        // Shared
        private HashSet<string> _knownMeshNames;
        private MBModuleImportContext _moduleContext;
        private MBModuleImportContext _nativeContext;

        // MENU ITEM

        [MenuItem("MBToolset/Sync/BRF Synchronizer")]
        public static void ShowWindow()
        {
            var window = GetWindow<BRFSynchronizerWindow>("BRF Synchronizer");
            window.minSize = new Vector2(750, 500);
        }

        // LIFECYCLE

        private void OnEnable() => RefreshModuleList();

        private void OnDisable()
        {
            _moduleContext?.Dispose();
            _nativeContext?.Dispose();
            _moduleContext = null;
            _nativeContext = null;
        }

        // MAIN GUI

        private void OnGUI()
        {
            DrawHeader();
            UIHelpers.DrawUILine(UIColors.GrayLine, 1, 2);

            if (_module == null)
            {
                EditorGUILayout.HelpBox(
                    "Select a module to begin synchronizing BRF data, scene props, and items.",
                    MessageType.Info);
                return;
            }

            // Tab bar - tabs are gated by workflow step completion
            EditorGUILayout.BeginHorizontal();
            for (int i = 0; i < TabNames.Length; i++)
            {
                var tab = (SyncTab)i;
                bool isActive = _activeTab == tab;
                bool isLocked = IsTabLocked(tab);

                var style = new GUIStyle(EditorStyles.toolbarButton);
                if (isActive)
                    style.fontStyle = FontStyle.Bold;
                if (isLocked)
                    style.normal.textColor = Styles.Locked;

                // Show badge on tab if changes detected
                string badge = GetTabBadge(tab);
                string lockIcon = isLocked ? "🔒 " : "";
                string label = string.IsNullOrEmpty(badge)
                    ? $"{lockIcon}{TabNames[i]}"
                    : $"{lockIcon}{TabNames[i]} {badge}";

                EditorGUI.BeginDisabledGroup(isLocked);
                if (GUILayout.Toggle(isActive, label, style))
                    _activeTab = tab;
                EditorGUI.EndDisabledGroup();
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(2);

            // Tab content
            switch (_activeTab)
            {
                case SyncTab.BRF:
                    DrawBrfTab();
                    break;
                case SyncTab.Textures:
                    DrawTexturesTab();
                    break;
                case SyncTab.SceneProps:
                    DrawScenePropsTab();
                    break;
                case SyncTab.Items:
                    DrawItemsTab();
                    break;
            }
        }

        private string GetTabBadge(SyncTab tab)
        {
            return tab switch
            {
                SyncTab.BRF when _brfScanResults.Values.Any(r => r.HasChanges) =>
                    $"({_brfScanResults.Values.Count(r => r.HasChanges)})",
                SyncTab.Textures when _textureDiff is { HasChanges: true } =>
                    $"({_textureDiff.TotalAdded + _textureDiff.TotalModified})",
                SyncTab.SceneProps when _scenePropDiff is { HasChanges: true } =>
                    $"({_scenePropDiff.TotalMissingSO})",
                SyncTab.Items when _itemDiff is { HasChanges: true } =>
                    $"({_itemDiff.TotalMissingSO})",
                _ => ""
            };
        }

        /// <summary>
        /// Checks if a tab is locked due to incomplete prerequisites.
        /// Flow: BRF → Textures → SceneProps/Items
        /// A tab unlocks when its prerequisite was synced OR scanned with no changes.
        /// </summary>
        private bool IsTabLocked(SyncTab tab)
        {
            return tab switch
            {
                SyncTab.BRF      => false,             // always available
                SyncTab.Textures => !IsBrfReady,       // requires BRF up to date
                SyncTab.SceneProps => !IsTexturesReady, // requires Textures up to date
                SyncTab.Items    => !IsTexturesReady,   // requires Textures up to date
                _ => false
            };
        }

        /// <summary>BRFs are ready when synced OR scanned with no pending changes.</summary>
        private bool IsBrfReady =>
            _brfSyncDone ||
            (_brfScanResults.Count > 0 && _brfScanResults.Values.All(r => !r.HasChanges));

        /// <summary>Textures are ready when synced OR scanned with no pending changes.</summary>
        private bool IsTexturesReady =>
            _textureSyncDone ||
            (IsBrfReady && _textureDiff != null && !_textureDiff.HasChanges);

        // HEADER

        private void DrawHeader()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.BeginHorizontal();
            {
                EditorGUILayout.LabelField("BRF Synchronizer", EditorStyles.boldLabel, GUILayout.Width(140));

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
                    EditorGUILayout.LabelField(_statusMessage, EditorStyles.miniLabel, GUILayout.MaxWidth(350));
                }
            }
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.EndVertical();
        }

        // TAB 1: BRF FILES

        private void DrawBrfTab()
        {
            // Toolbar
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            {
                EditorGUI.BeginDisabledGroup(_isScanning);
                if (GUILayout.Button("🔍 Scan BRFs", EditorStyles.toolbarButton, GUILayout.Width(100)))
                    ScanAllBrfs();
                EditorGUI.EndDisabledGroup();

                GUILayout.FlexibleSpace();

                if (_brfScanResults.Count > 0)
                {
                    int changes = _brfScanResults.Values.Sum(r => r.TotalAdded + r.TotalRemoved + r.TotalModified);
                    int brfsChanged = _brfScanResults.Values.Count(r => r.HasChanges);
                    EditorGUILayout.LabelField(
                        $"{brfsChanged}/{_brfScanResults.Count} BRFs changed | {changes} total",
                        EditorStyles.miniLabel, GUILayout.Width(200));
                }
            }
            EditorGUILayout.EndHorizontal();

            // Two-panel layout
            EditorGUILayout.BeginHorizontal();
            {
                EditorGUILayout.BeginVertical(GUILayout.Width(220), GUILayout.ExpandHeight(true));
                DrawBrfListPanel();
                EditorGUILayout.EndVertical();

                var sep = EditorGUILayout.GetControlRect(GUILayout.Width(1), GUILayout.ExpandHeight(true));
                EditorGUI.DrawRect(sep, new Color(0.3f, 0.3f, 0.3f));

                EditorGUILayout.BeginVertical(GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
                DrawBrfDetailPanel();
                EditorGUILayout.EndVertical();
            }
            EditorGUILayout.EndHorizontal();

            // BRF action bar
            DrawBrfActionBar();
        }

        private void DrawBrfListPanel()
        {
            _brfListScroll = EditorGUILayout.BeginScrollView(_brfListScroll);

            if (_brfScanResults.Count == 0)
            {
                EditorGUILayout.HelpBox("Click Scan BRFs to detect changes.", MessageType.None);
            }

            foreach (var kvp in _brfScanResults.OrderByDescending(r => r.Value.HasChanges).ThenBy(r => r.Key))
            {
                bool isSelected = kvp.Key == _selectedBrfName;
                string icon = kvp.Value.HasChanges ? "⚠" : "✓";
                Color color = kvp.Value.HasChanges ? Styles.Modified : Styles.Unchanged;

                EditorGUILayout.BeginHorizontal(isSelected ? EditorStyles.helpBox : GUIStyle.none);

                var iconStyle = new GUIStyle(EditorStyles.label) { normal = { textColor = color } };
                EditorGUILayout.LabelField(icon, iconStyle, GUILayout.Width(16));

                EditorGUILayout.BeginVertical();
                if (GUILayout.Button(kvp.Key, EditorStyles.label))
                    _selectedBrfName = kvp.Key;

                if (kvp.Value.HasChanges)
                {
                    var sumStyle = new GUIStyle(EditorStyles.miniLabel) { normal = { textColor = Styles.Modified } };
                    EditorGUILayout.LabelField(kvp.Value.GetSummary(), sumStyle);
                }
                EditorGUILayout.EndVertical();
                EditorGUILayout.EndHorizontal();
            }

            foreach (var kvp in _brfScanErrors)
            {
                var errStyle = new GUIStyle(EditorStyles.label) { normal = { textColor = Styles.Removed } };
                EditorGUILayout.LabelField($"✗ {kvp.Key}", errStyle);
            }

            EditorGUILayout.EndScrollView();
        }

        private void DrawBrfDetailPanel()
        {
            if (string.IsNullOrEmpty(_selectedBrfName) || !_brfScanResults.ContainsKey(_selectedBrfName))
            {
                EditorGUILayout.HelpBox("Select a BRF from the list.", MessageType.None);
                return;
            }

            var diff = _brfScanResults[_selectedBrfName];

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(diff.BrfName, EditorStyles.boldLabel);
            GUILayout.FlexibleSpace();
            if (diff.HasChanges)
            {
                if (GUILayout.Button("All", EditorStyles.miniButton, GUILayout.Width(35)))
                    SetAllSelected(diff, true);
                if (GUILayout.Button("None", EditorStyles.miniButton, GUILayout.Width(40)))
                    SetAllSelected(diff, false);
            }
            EditorGUILayout.EndHorizontal();

            _showBrfUnchanged = EditorGUILayout.Toggle("Show Unchanged", _showBrfUnchanged);

            _brfDetailScroll = EditorGUILayout.BeginScrollView(_brfDetailScroll);

            DrawDiffSection("Meshes", diff.Meshes, ref _showMeshes, _showBrfUnchanged);
            DrawDiffSection("Materials", diff.Materials, ref _showMaterials, _showBrfUnchanged);
            DrawDiffSection("Textures", diff.Textures, ref _showTextures, _showBrfUnchanged);
            DrawDiffSection("Bodies", diff.Bodies, ref _showBodies, _showBrfUnchanged);

            EditorGUILayout.EndScrollView();
        }

        private void DrawBrfActionBar()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);

            int selected = _brfScanResults.Values.Sum(r => r.SelectedCount);

            EditorGUI.BeginDisabledGroup(selected == 0 || _isScanning);
            if (GUILayout.Button($"🔄 Sync BRF Changes ({selected})", GUILayout.Height(22)))
                ApplySelectedBrfChanges();
            EditorGUI.EndDisabledGroup();

            GUILayout.FlexibleSpace();

            EditorGUI.BeginDisabledGroup(!_brfScanResults.Values.Any(r => r.HasChanges) || _isScanning);
            if (GUILayout.Button("🏗 Rebuild Prefabs", GUILayout.Height(22)))
                RegeneratePrefabs();
            EditorGUI.EndDisabledGroup();

            EditorGUILayout.EndHorizontal();
        }

        // TAB 2: SCENE PROPS

        private void DrawScenePropsTab()
        {
            // Toolbar
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            {
                bool hasModSys = !string.IsNullOrEmpty(_module?.ModuleSystemPath);

                EditorGUI.BeginDisabledGroup(!hasModSys || _isScanning);
                if (GUILayout.Button("🔍 Scan Scene Props", EditorStyles.toolbarButton, GUILayout.Width(140)))
                    ScanSceneProps();
                EditorGUI.EndDisabledGroup();

                GUILayout.Space(8);
                _scenePropFilter = EditorGUILayout.TextField(_scenePropFilter, EditorStyles.toolbarSearchField, GUILayout.Width(180));

                GUILayout.FlexibleSpace();

                _showScenePropUnchanged = GUILayout.Toggle(_showScenePropUnchanged, "Show All", EditorStyles.toolbarButton, GUILayout.Width(60));

                if (_scenePropDiff != null)
                {
                    EditorGUILayout.LabelField(_scenePropDiff.GetSummary(), EditorStyles.miniLabel, GUILayout.Width(300));
                }
            }
            EditorGUILayout.EndHorizontal();

            if (_module != null && string.IsNullOrEmpty(_module.ModuleSystemPath))
            {
                EditorGUILayout.HelpBox(
                    "Module System Path not configured. Set it in the Module Importer to enable Scene Props scan.",
                    MessageType.Warning);
                return;
            }

            // Missing mesh dependency banner
            if (_scenePropDiff != null && _scenePropDiff.TotalWithMissingMeshes > 0)
            {
                EditorGUILayout.HelpBox(
                    $"⚠ {_scenePropDiff.TotalWithMissingMeshes} scene props reference meshes not found in BRF data. " +
                    "Sync BRF files first (BRF Files tab) to resolve dependencies.",
                    MessageType.Warning);
            }

            if (_scenePropDiff == null)
            {
                EditorGUILayout.HelpBox("Click Scan to compare module scene props against existing data.", MessageType.Info);
                return;
            }

            // Warn if model prefabs folder is empty - scene prop prefabs need them
            bool hasModelPrefabs = Directory.GetFiles(
                MBPathHelpers.ModResourcePath(_module.ID), "*.prefab", SearchOption.AllDirectories).Length > 0;
            if (!hasModelPrefabs)
            {
                EditorGUILayout.HelpBox(
                    "No model prefabs found. Sync BRF files first so model prefabs exist before generating scene prop prefabs.",
                    MessageType.Warning);
            }

            // Entry list
            _scenePropScroll = EditorGUILayout.BeginScrollView(_scenePropScroll);
            DrawEntityDiffList(_scenePropDiff.Entries, _showScenePropUnchanged, _scenePropFilter, DrawScenePropEntry);
            EditorGUILayout.EndScrollView();

            // Action bar
            DrawScenePropActionBar();
        }

        private void DrawScenePropEntry(BrfDiffEntry<ScenePropSnapshot> entry)
        {
            var snap = entry.NewValue ?? entry.OldValue;
            string meshInfo = !string.IsNullOrEmpty(snap?.Mesh) && snap.Mesh != "0" ? $"  mesh: {snap.Mesh}" : "";
            string typeInfo = !string.IsNullOrEmpty(snap?.TypeName) ? $"  [{snap.TypeName}]" : "";

            DrawEntityEntry(entry, $"{meshInfo}{typeInfo}");
        }

        private void DrawScenePropActionBar()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);

            int selected = _scenePropDiff?.Entries.Count(e => e.Selected && e.HasChanges && !e.HasMissingDependencies) ?? 0;
            int blocked = _scenePropDiff?.Entries.Count(e => e.Selected && e.HasChanges && e.HasMissingDependencies) ?? 0;

            EditorGUI.BeginDisabledGroup(selected == 0 || _isScanning);
            if (GUILayout.Button($"📋 Sync Scene Props ({selected})", GUILayout.Height(22)))
                ApplySyncSceneProps();
            EditorGUI.EndDisabledGroup();

            if (blocked > 0)
            {
                var warnStyle = new GUIStyle(EditorStyles.miniLabel) { normal = { textColor = Styles.Warning } };
                EditorGUILayout.LabelField($"⚠ {blocked} skipped (missing meshes)", warnStyle, GUILayout.Width(170));
            }

            GUILayout.Space(8);
            _onlyNewPrefabs = GUILayout.Toggle(_onlyNewPrefabs, new GUIContent("Only New Prefabs",
                "When ON: skip existing prefabs, only create missing ones.\nWhen OFF: recreate all prefabs."),
                GUILayout.Width(110));

            GUILayout.FlexibleSpace();

            EditorGUI.BeginDisabledGroup(_scenePropDiff == null || !_scenePropDiff.HasChanges);
            if (GUILayout.Button("Select All", EditorStyles.miniButton, GUILayout.Width(75)))
            {
                foreach (var e in _scenePropDiff.Entries.Where(e => e.HasChanges && !e.HasMissingDependencies))
                    e.Selected = true;
            }
            if (GUILayout.Button("Deselect All", EditorStyles.miniButton, GUILayout.Width(75)))
            {
                foreach (var e in _scenePropDiff.Entries)
                    e.Selected = false;
            }
            EditorGUI.EndDisabledGroup();

            EditorGUILayout.EndHorizontal();
        }

        // TAB 3: ITEMS

        private void DrawItemsTab()
        {
            // Toolbar
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            {
                bool hasModSys = !string.IsNullOrEmpty(_module?.ModuleSystemPath);

                EditorGUI.BeginDisabledGroup(!hasModSys || _isScanning);
                if (GUILayout.Button("🔍 Scan Items", EditorStyles.toolbarButton, GUILayout.Width(100)))
                    ScanItems();
                EditorGUI.EndDisabledGroup();

                GUILayout.Space(8);
                _itemFilter = EditorGUILayout.TextField(_itemFilter, EditorStyles.toolbarSearchField, GUILayout.Width(180));

                GUILayout.FlexibleSpace();

                _showItemUnchanged = GUILayout.Toggle(_showItemUnchanged, "Show All", EditorStyles.toolbarButton, GUILayout.Width(60));

                if (_itemDiff != null)
                {
                    EditorGUILayout.LabelField(_itemDiff.GetSummary(), EditorStyles.miniLabel, GUILayout.Width(300));
                }
            }
            EditorGUILayout.EndHorizontal();

            if (_module != null && string.IsNullOrEmpty(_module.ModuleSystemPath))
            {
                EditorGUILayout.HelpBox(
                    "Module System Path not configured. Set it in the Module Importer to enable Items scan.",
                    MessageType.Warning);
                return;
            }

            // Missing mesh dependency banner
            if (_itemDiff != null && _itemDiff.TotalWithMissingMeshes > 0)
            {
                EditorGUILayout.HelpBox(
                    $"⚠ {_itemDiff.TotalWithMissingMeshes} items reference meshes not found in BRF data. " +
                    "Sync BRF files first (BRF Files tab) to resolve dependencies.",
                    MessageType.Warning);
            }

            if (_itemDiff == null)
            {
                EditorGUILayout.HelpBox("Click Scan to compare module items against existing data.", MessageType.Info);
                return;
            }

            // Warn if model prefabs folder is empty
            bool hasModelPrefabsItems = Directory.GetFiles(
                MBPathHelpers.ModResourcePath(_module.ID), "*.prefab", SearchOption.AllDirectories).Length > 0;
            if (!hasModelPrefabsItems)
            {
                EditorGUILayout.HelpBox(
                    "No model prefabs found. Sync BRF files first so model prefabs exist before generating item prefabs.",
                    MessageType.Warning);
            }

            // Entry list
            _itemScroll = EditorGUILayout.BeginScrollView(_itemScroll);
            DrawEntityDiffList(_itemDiff.Entries, _showItemUnchanged, _itemFilter, DrawItemEntry);
            EditorGUILayout.EndScrollView();

            // Action bar
            DrawItemActionBar();
        }

        private void DrawItemEntry(BrfDiffEntry<ItemSnapshot> entry)
        {
            var snap = entry.NewValue ?? entry.OldValue;
            int meshCount = snap?.MeshNames?.Count ?? 0;
            string priceInfo = snap != null ? $"  ${snap.Price}" : "";
            string meshInfo = meshCount > 0 ? $"  ({meshCount} meshes)" : "";

            DrawEntityEntry(entry, $"{priceInfo}{meshInfo}");
        }

        private void DrawItemActionBar()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);

            int selected = _itemDiff?.Entries.Count(e => e.Selected && e.HasChanges && !e.HasMissingDependencies) ?? 0;
            int blocked = _itemDiff?.Entries.Count(e => e.Selected && e.HasChanges && e.HasMissingDependencies) ?? 0;

            EditorGUI.BeginDisabledGroup(selected == 0 || _isScanning);
            if (GUILayout.Button($"⚔ Sync Items ({selected})", GUILayout.Height(22)))
                ApplySyncItems();
            EditorGUI.EndDisabledGroup();

            if (blocked > 0)
            {
                var warnStyle = new GUIStyle(EditorStyles.miniLabel) { normal = { textColor = Styles.Warning } };
                EditorGUILayout.LabelField($"⚠ {blocked} skipped (missing meshes)", warnStyle, GUILayout.Width(170));
            }

            GUILayout.Space(8);
            _onlyNewPrefabs = GUILayout.Toggle(_onlyNewPrefabs, new GUIContent("Only New Prefabs",
                "When ON: skip existing prefabs, only create missing ones.\nWhen OFF: recreate all prefabs."),
                GUILayout.Width(110));

            GUILayout.FlexibleSpace();

            EditorGUI.BeginDisabledGroup(_itemDiff == null || !_itemDiff.HasChanges);
            if (GUILayout.Button("Select All", EditorStyles.miniButton, GUILayout.Width(75)))
            {
                foreach (var e in _itemDiff.Entries.Where(e => e.HasChanges && !e.HasMissingDependencies))
                    e.Selected = true;
            }
            if (GUILayout.Button("Deselect All", EditorStyles.miniButton, GUILayout.Width(75)))
            {
                foreach (var e in _itemDiff.Entries)
                    e.Selected = false;
            }
            EditorGUI.EndDisabledGroup();

            EditorGUILayout.EndHorizontal();
        }

        // TAB 4: TEXTURES

        private void DrawTexturesTab()
        {
            // Toolbar
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            {
                EditorGUI.BeginDisabledGroup(_isScanning);
                if (GUILayout.Button("🔍 Scan Textures", EditorStyles.toolbarButton, GUILayout.Width(120)))
                    ScanTextures();
                EditorGUI.EndDisabledGroup();

                GUILayout.Space(8);
                _textureFilter = EditorGUILayout.TextField(_textureFilter, EditorStyles.toolbarSearchField, GUILayout.Width(180));

                GUILayout.FlexibleSpace();

                _showTextureUnchanged = GUILayout.Toggle(_showTextureUnchanged, "Show All", EditorStyles.toolbarButton, GUILayout.Width(60));

                if (_textureDiff != null)
                {
                    EditorGUILayout.LabelField(_textureDiff.GetSummary(), EditorStyles.miniLabel, GUILayout.Width(300));
                }
            }
            EditorGUILayout.EndHorizontal();

            if (_textureDiff == null)
            {
                EditorGUILayout.HelpBox("Click Scan to compare module textures against imported DDS files.", MessageType.Info);
                return;
            }

            // Entry list
            _textureScroll = EditorGUILayout.BeginScrollView(_textureScroll);
            DrawTextureDiffList();
            EditorGUILayout.EndScrollView();

            // Action bar
            DrawTextureActionBar();
        }

        private void DrawTextureDiffList()
        {
            var visible = _textureDiff.Entries.AsEnumerable();

            if (!_showTextureUnchanged)
                visible = visible.Where(e => e.HasChanges);

            if (!string.IsNullOrEmpty(_textureFilter))
                visible = visible.Where(e => e.TextureName.IndexOf(_textureFilter, StringComparison.OrdinalIgnoreCase) >= 0);

            var list = visible.ToList();

            if (list.Count == 0)
            {
                EditorGUILayout.LabelField(
                    _showTextureUnchanged ? "No textures found." : "No texture changes detected.",
                    EditorStyles.centeredGreyMiniLabel);
                return;
            }

            foreach (var entry in list)
            {
                DrawTextureEntry(entry);
            }
        }

        private void DrawTextureEntry(TextureDiffEntry entry)
        {
            Color statusColor = entry.Status switch
            {
                DiffStatus.Added => Styles.Added,
                DiffStatus.Modified => Styles.Modified,
                _ => Styles.Unchanged
            };

            string statusIcon = entry.Status switch
            {
                DiffStatus.Added => "+",
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
            EditorGUILayout.LabelField(entry.TextureName, nameStyle, GUILayout.MinWidth(200));

            var brfStyle = new GUIStyle(EditorStyles.miniLabel)
            {
                normal = { textColor = new Color(0.5f, 0.7f, 0.9f) }
            };
            EditorGUILayout.LabelField(entry.BrfName, brfStyle, GUILayout.Width(120));

            var sizeStyle = new GUIStyle(EditorStyles.miniLabel)
            {
                normal = { textColor = new Color(0.65f, 0.65f, 0.65f) }
            };
            EditorGUILayout.LabelField(entry.SizeSummary, sizeStyle, GUILayout.Width(120));

            EditorGUILayout.EndHorizontal();
            EditorGUILayout.EndVertical();
        }

        private void DrawTextureActionBar()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);

            int selected = _textureDiff?.SelectedCount ?? 0;

            EditorGUI.BeginDisabledGroup(selected == 0 || _isScanning);
            if (GUILayout.Button($"🖼 Sync Textures ({selected})", GUILayout.Height(22)))
                ApplySyncTextures();
            EditorGUI.EndDisabledGroup();

            GUILayout.FlexibleSpace();

            EditorGUI.BeginDisabledGroup(_textureDiff == null || !_textureDiff.HasChanges);
            if (GUILayout.Button("Select All", EditorStyles.miniButton, GUILayout.Width(75)))
            {
                foreach (var e in _textureDiff.Entries.Where(e => e.HasChanges))
                    e.Selected = true;
            }
            if (GUILayout.Button("Deselect All", EditorStyles.miniButton, GUILayout.Width(75)))
            {
                foreach (var e in _textureDiff.Entries)
                    e.Selected = false;
            }
            EditorGUI.EndDisabledGroup();

            EditorGUILayout.EndHorizontal();
        }

        // SHARED UI HELPERS

        private void DrawDiffSection<T>(
            string title, List<BrfDiffEntry<T>> entries, ref bool foldout, bool showUnchanged) where T : class
        {
            var visible = showUnchanged ? entries : entries.Where(e => e.HasChanges).ToList();
            if (visible.Count == 0 && !showUnchanged) return;

            int changes = entries.Count(e => e.HasChanges);
            string label = changes > 0
                ? $"{title} ({changes} changes / {entries.Count})"
                : $"{title} ({entries.Count})";

            foldout = EditorGUILayout.Foldout(foldout, label, true, EditorStyles.foldoutHeader);
            if (!foldout) return;

            EditorGUI.indentLevel++;
            foreach (var entry in visible)
            {
                EditorGUILayout.BeginHorizontal();

                if (entry.HasChanges)
                    entry.Selected = EditorGUILayout.Toggle(entry.Selected, GUILayout.Width(16));
                else
                    GUILayout.Space(20);

                Color c = entry.Status switch
                {
                    DiffStatus.Added => Styles.Added,
                    DiffStatus.Removed => Styles.Removed,
                    DiffStatus.Modified => Styles.Modified,
                    _ => Styles.Unchanged
                };
                string badge = entry.Status switch
                {
                    DiffStatus.Added => "+", DiffStatus.Removed => "−",
                    DiffStatus.Modified => "~", _ => "="
                };

                var bs = new GUIStyle(EditorStyles.miniLabel) { normal = { textColor = c }, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
                EditorGUILayout.LabelField(badge, bs, GUILayout.Width(14));

                var ns = new GUIStyle(EditorStyles.label) { normal = { textColor = entry.HasChanges ? Color.white : Styles.Unchanged } };
                EditorGUILayout.LabelField(entry.Name, ns);

                EditorGUILayout.EndHorizontal();

                if (entry.Status == DiffStatus.Modified && entry.ChangedFields.Count > 0)
                {
                    EditorGUI.indentLevel++;
                    var ds = new GUIStyle(EditorStyles.miniLabel) { normal = { textColor = new Color(0.7f, 0.7f, 0.7f) }, wordWrap = true };
                    EditorGUILayout.LabelField(string.Join(", ", entry.ChangedFields), ds);
                    EditorGUI.indentLevel--;
                }
            }
            EditorGUI.indentLevel--;
            EditorGUILayout.Space(4);
        }

        /// <summary>
        /// Draw a list of entity diff entries (scene props or items) with filtering.
        /// </summary>
        private void DrawEntityDiffList<T>(
            List<BrfDiffEntry<T>> entries,
            bool showUnchanged,
            string filter,
            Action<BrfDiffEntry<T>> drawEntryAction) where T : class
        {
            var visible = entries.AsEnumerable();

            if (!showUnchanged)
                visible = visible.Where(e => e.HasChanges || e.HasMissingDependencies);

            if (!string.IsNullOrEmpty(filter))
                visible = visible.Where(e => e.Name.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0);

            var list = visible.ToList();

            if (list.Count == 0)
            {
                EditorGUILayout.LabelField(showUnchanged ? "No entries found." : "No changes detected.",
                    EditorStyles.centeredGreyMiniLabel);
                return;
            }

            foreach (var entry in list)
            {
                drawEntryAction(entry);
            }
        }

        /// <summary>
        /// Shared entry rendering for SceneProps/Items tabs.
        /// </summary>
        private void DrawEntityEntry<T>(BrfDiffEntry<T> entry, string extraInfo) where T : class
        {
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
            EditorGUILayout.LabelField(entry.Name, nameStyle, GUILayout.MinWidth(150));

            if (!string.IsNullOrEmpty(extraInfo))
            {
                var infoStyle = new GUIStyle(EditorStyles.miniLabel)
                {
                    normal = { textColor = new Color(0.65f, 0.65f, 0.65f) }
                };
                EditorGUILayout.LabelField(extraInfo, infoStyle);
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

            // Missing mesh dependency warning
            if (entry.HasMissingDependencies)
            {
                var warnStyle = new GUIStyle(EditorStyles.miniLabel)
                {
                    normal = { textColor = Styles.MissingDep },
                    wordWrap = true
                };
                EditorGUI.indentLevel++;
                EditorGUILayout.LabelField(
                    $"⚠ Missing meshes: {string.Join(", ", entry.MissingMeshDependencies)}",
                    warnStyle);
                EditorGUI.indentLevel--;
            }

            EditorGUILayout.EndVertical();
        }

        // SCAN LOGIC

        private void ScanAllBrfs()
        {
            if (_module == null) return;

            _isScanning = true;
            _brfScanResults.Clear();
            _brfScanErrors.Clear();
            _selectedBrfName = null;
            _statusMessage = "Scanning BRFs...";

            try
            {
                string resourcePath = MBPathHelpers.ModResourcePath(_module.ID);
                if (!Directory.Exists(resourcePath))
                {
                    _statusMessage = $"Resource path not found: {resourcePath}";
                    return;
                }

                var brfFolders = Directory.GetDirectories(resourcePath)
                    .Where(d => File.Exists(Path.Combine(d, "data.json")))
                    .ToList();

                bool isNative = _module.ID == "Native";
                string sourceBrfDir = isNative
                    ? Path.Combine(MBEditorManager.MbEditorSettings.MbPath, "CommonRes")
                    : Path.Combine(MBPathHelpers.ModSourcePath(_module.ID), "Resource");

                for (int i = 0; i < brfFolders.Count; i++)
                {
                    string brfFolder = brfFolders[i];
                    string brfName = Path.GetFileName(brfFolder);

                    EditorUtility.DisplayProgressBar("BRF Scan", $"Scanning {brfName} ({i + 1}/{brfFolders.Count})",
                        (float)i / brfFolders.Count);

                    try
                    {
                        ScanSingleBrf(brfName, brfFolder, sourceBrfDir);
                    }
                    catch (Exception ex)
                    {
                        _brfScanErrors[brfName] = ex.Message;
                    }
                }

                // Rebuild known mesh names for dependency checking
                RebuildKnownMeshNames();

                int changes = _brfScanResults.Values.Sum(r => r.TotalAdded + r.TotalRemoved + r.TotalModified);
                _statusMessage = $"BRF scan: {_brfScanResults.Values.Count(r => r.HasChanges)} BRFs with {changes} changes";
            }
            finally
            {
                EditorUtility.ClearProgressBar();
                _isScanning = false;
                Repaint();
            }
        }

        private void ScanSingleBrf(string brfName, string brfFolder, string sourceBrfDir)
        {
            string sourceBrfPath = Path.Combine(sourceBrfDir, $"{brfName}.brf");

            if (!File.Exists(sourceBrfPath))
            {
                _brfScanResults[brfName] = new BrfDiffResult { BrfName = brfName, BrfFolderPath = brfFolder };
                return;
            }

            string existingJsonPath = Path.Combine(brfFolder, "data.json");
            BrfData oldData = null;

            if (File.Exists(existingJsonPath))
            {
                string oldJson = File.ReadAllText(existingJsonPath);
                oldData = JsonConvert.DeserializeObject<BrfData>(oldJson);
            }

            // Use fast 'info' command - metadata only, no OBJ/SMD export.
            // This is ~10x faster than full export since it skips all geometry I/O.
            string tempFolder = Path.Combine(brfFolder, "_sync_temp");
            if (Directory.Exists(tempFolder))
                Directory.Delete(tempFolder, true);

            var infoResult = BRFSyncHandler.InfoBRF(sourceBrfPath, tempFolder);

            if (!infoResult.Success)
            {
                if (Directory.Exists(tempFolder))
                    Directory.Delete(tempFolder, true);
                _brfScanErrors[brfName] = $"Info scan failed: {infoResult.Message}";
                return;
            }

            BrfData newData = null;
            string newJsonPath = Path.Combine(tempFolder, "data.json");
            if (File.Exists(newJsonPath))
            {
                string newJson = File.ReadAllText(newJsonPath);
                newData = JsonConvert.DeserializeObject<BrfData>(newJson);
            }

            if (newData == null)
            {
                if (Directory.Exists(tempFolder))
                    Directory.Delete(tempFolder, true);
                _brfScanErrors[brfName] = "Failed to load new data.json";
                return;
            }

            var diff = BrfDiffEngine.Compare(oldData, newData, brfName);
            diff.BrfFolderPath = brfFolder;
            diff.SourceBrfPath = sourceBrfPath;
            _brfScanResults[brfName] = diff;

            // Always clean up temp folder after scan - the full export
            // will be done later in ApplySelectedBrfChanges if needed.
            if (Directory.Exists(tempFolder))
                Directory.Delete(tempFolder, true);
        }

        private void ScanSceneProps()
        {
            if (_module == null) return;

            _isScanning = true;
            _statusMessage = "Scanning scene props...";

            try
            {
                EditorUtility.DisplayProgressBar("Scene Props Scan", "Running converter...", 0.2f);

                // Run the Python converter to get fresh JSON
                int result = BrfSyncExecutor.SyncJsonData(
                    _module,
                    "convert_scene_props.py",
                    "scene_props_full.json",
                    (_, __) => { }); // Don't import yet, just generate JSON

                if (result == 0)
                {
                    _statusMessage = "Scene props converter failed.";
                    return;
                }

                EditorUtility.DisplayProgressBar("Scene Props Scan", "Comparing...", 0.6f);

                // Load the fresh JSON
                string jsonPath = Path.Combine(
                    MBPathHelpers.ModDataBaseJsonDirectoryPath(_module.ID),
                    "scene_props_full.json");

                if (!File.Exists(jsonPath))
                {
                    _statusMessage = "scene_props_full.json not found.";
                    return;
                }

                string json = File.ReadAllText(jsonPath);
                var decoded = JsonHelper.FromJson<MBScenePropJsonDecoder.ScenePropJsonData>(json);
                var decodedData = MBScenePropJsonDecoder.DecodeSceneProps(decoded);

                // Ensure known mesh names are up to date
                RebuildKnownMeshNames();

                // Merge Native's existing scene props into the comparison baseline
                // so entries already imported in Native aren't flagged as "Added"
                var existingProps = new List<MBScenePropData>(_module.sceneProps);
                if (_module.ID != "Native")
                {
                    var nativeModule = LoadNativeModule();
                    if (nativeModule != null)
                        existingProps.AddRange(nativeModule.sceneProps);
                }

                // Compare - check SO existence + prefab existence
                string scenePropPrefabsPath = MBPathHelpers.ModPrefabScenePropsPath(_module.ID);
                _scenePropDiff = JsonDiffEngine.CompareSceneProps(decodedData, existingProps, _knownMeshNames, scenePropPrefabsPath);
                _statusMessage = $"Scene props: {_scenePropDiff.GetSummary()}";
            }
            catch (Exception ex)
            {
                _statusMessage = $"Scene props scan failed: {ex.Message}";
                Debug.LogError($"Scene props scan: {ex}");
            }
            finally
            {
                EditorUtility.ClearProgressBar();
                _isScanning = false;
                Repaint();
            }
        }

        private void ScanItems()
        {
            if (_module == null) return;

            _isScanning = true;
            _statusMessage = "Scanning items...";

            try
            {
                EditorUtility.DisplayProgressBar("Items Scan", "Running converter...", 0.2f);

                int result = BrfSyncExecutor.SyncJsonData(
                    _module,
                    "convert_items.py",
                    "items_full.json",
                    (_, __) => { });

                if (result == 0)
                {
                    _statusMessage = "Items converter failed.";
                    return;
                }

                EditorUtility.DisplayProgressBar("Items Scan", "Comparing...", 0.6f);

                string jsonPath = Path.Combine(
                    MBPathHelpers.ModDataBaseJsonDirectoryPath(_module.ID),
                    "items_full.json");

                if (!File.Exists(jsonPath))
                {
                    _statusMessage = "items_full.json not found.";
                    return;
                }

                string json = File.ReadAllText(jsonPath);
                var decoded = JsonHelper.FromJson<MBItemJsonDecoder.ItemJsonData>(json);
                var decodedData = MBItemJsonDecoder.DecodeItems(decoded);

                RebuildKnownMeshNames();

                // Merge Native's existing items into the comparison baseline
                var existingItems = new List<MBItemData>(_module.items);
                if (_module.ID != "Native")
                {
                    var nativeModule = LoadNativeModule();
                    if (nativeModule != null)
                        existingItems.AddRange(nativeModule.items);
                }

                string itemPrefabsPath = MBPathHelpers.ModPrefabItemsPath(_module.ID);
                _itemDiff = JsonDiffEngine.CompareItems(decodedData, existingItems, _knownMeshNames, itemPrefabsPath);
                _statusMessage = $"Items: {_itemDiff.GetSummary()}";
            }
            catch (Exception ex)
            {
                _statusMessage = $"Items scan failed: {ex.Message}";
                Debug.LogError($"Items scan: {ex}");
            }
            finally
            {
                EditorUtility.ClearProgressBar();
                _isScanning = false;
                Repaint();
            }
        }

        private void ScanTextures()
        {
            if (_module == null) return;

            _isScanning = true;
            _statusMessage = "Scanning textures...";

            try
            {
                EditorUtility.DisplayProgressBar("Texture Scan", "Comparing source DDS files...", 0.3f);

                _textureDiff = TextureDiffEngine.Scan(_module);
                _statusMessage = $"Textures: {_textureDiff.GetSummary()}";
            }
            catch (Exception ex)
            {
                _statusMessage = $"Texture scan failed: {ex.Message}";
                Debug.LogError($"Texture scan: {ex}");
            }
            finally
            {
                EditorUtility.ClearProgressBar();
                _isScanning = false;
                Repaint();
            }
        }

        // APPLY LOGIC

        private void ApplySyncTextures()
        {
            if (_textureDiff == null) return;

            int count = _textureDiff.SelectedCount;
            if (!EditorUtility.DisplayDialog("Texture Sync",
                    $"Copy {count} texture(s) into BRF folders and reassign materials?", "Sync", "Cancel"))
                return;

            _isScanning = true;

            try
            {
                int copied = BrfSyncExecutor.SyncTextures(_textureDiff, _module);

                _statusMessage = $"Synced {copied} textures. → Scene Props & Items tabs unlocked.";
                _textureSyncDone = true;
                ScanTextures();
            }
            catch (Exception ex)
            {
                _statusMessage = $"Texture sync failed: {ex.Message}";
            }
            finally
            {
                _isScanning = false;
                Repaint();
            }
        }

        private void ApplySelectedBrfChanges()
        {
            var toSync = _brfScanResults.Where(r => r.Value.HasChanges && r.Value.SelectedCount > 0).ToList();
            if (toSync.Count == 0) return;

            if (!EditorUtility.DisplayDialog("BRF Sync",
                    $"Apply changes to {toSync.Count} BRF file(s)?", "Sync", "Cancel"))
                return;

            _isScanning = true;
            EnsureContexts();

            try
            {
                for (int i = 0; i < toSync.Count; i++)
                {
                    string brfName = toSync[i].Key;
                    var diff = toSync[i].Value;
                    string tempFolder = Path.Combine(diff.BrfFolderPath, "_sync_temp");

                    EditorUtility.DisplayProgressBar("BRF Sync",
                        $"Exporting {brfName} ({i + 1}/{toSync.Count})...",
                        (float)i / toSync.Count);

                    // Now do the full export (with OBJ/SMD files) - the scan phase
                    // only used 'info' for fast metadata comparison.
                    if (!string.IsNullOrEmpty(diff.SourceBrfPath) && File.Exists(diff.SourceBrfPath))
                    {
                        if (Directory.Exists(tempFolder))
                            Directory.Delete(tempFolder, true);

                        var exportResult = BRFSyncHandler.ExportBRF(diff.SourceBrfPath, tempFolder);
                        if (!exportResult.Success)
                        {
                            Debug.LogWarning($"Full export failed for {brfName}: {exportResult.Message}");
                            continue;
                        }
                    }

                    if (!Directory.Exists(tempFolder)) continue;

                    string assetPath = Path.Combine(diff.BrfFolderPath, $"{brfName}.asset");
                    var brfData = AssetDatabase.LoadAssetAtPath<MBBrfData>(assetPath);
                    if (brfData == null) continue;

                    BrfSyncExecutor.ApplyBrfChanges(_module, brfData, diff, tempFolder, _moduleContext, _nativeContext);

                    if (Directory.Exists(tempFolder))
                        Directory.Delete(tempFolder, true);
                }

                BrfSyncExecutor.RebuildBrfDataBase(_module);
                RebuildKnownMeshNames();

                // Reassign textures to materials - BRF sync can break material references
                // on existing model prefabs when material assets are recreated
                MBEditorUtility.ReloadModuleMaterials(_module);

                AssetDatabase.Refresh();

                _statusMessage = $"Synced {toSync.Count} BRF(s). → Textures tab unlocked."; 
                _brfSyncDone = true;
                ScanAllBrfs();
            }
            catch (Exception ex)
            {
                _statusMessage = $"Sync failed: {ex.Message}";
            }
            finally
            {
                EditorUtility.ClearProgressBar();
                _isScanning = false;
                Repaint();
            }
        }

        private void ApplySyncSceneProps()
        {
            if (_scenePropDiff == null) return;

            int count = _scenePropDiff.Entries.Count(e => e.Selected && e.HasChanges);
            if (!EditorUtility.DisplayDialog("Scene Props Sync",
                    $"Sync {count} scene prop change(s) and generate prefabs?", "Sync", "Cancel"))
                return;

            // Use the existing JSON (already generated during scan)
            string jsonPath = Path.Combine(
                MBPathHelpers.ModDataBaseJsonDirectoryPath(_module.ID),
                "scene_props_full.json");

            if (File.Exists(jsonPath))
            {
                BrfSyncExecutor.SyncSceneProps(jsonPath, _module);

                // Ensure materials have their textures assigned before generating prefabs
                MBEditorUtility.ReloadModuleMaterials(_module);

                // Generate prefabs - onlyNew=true means skip existing, false=rebuild all
                MBPrefabsGenerator.GeneratePrefabs(_module, _onlyNewPrefabs);

                _statusMessage = _onlyNewPrefabs
                    ? "Scene props synced + new prefabs created."
                    : "Scene props synced + all prefabs rebuilt.";
                ScanSceneProps();
            }
        }

        private void ApplySyncItems()
        {
            if (_itemDiff == null) return;

            int count = _itemDiff.Entries.Count(e => e.Selected && e.HasChanges);
            if (!EditorUtility.DisplayDialog("Items Sync",
                    $"Sync {count} item change(s) and generate prefabs?", "Sync", "Cancel"))
                return;

            string jsonPath = Path.Combine(
                MBPathHelpers.ModDataBaseJsonDirectoryPath(_module.ID),
                "items_full.json");

            if (File.Exists(jsonPath))
            {
                BrfSyncExecutor.SyncItems(jsonPath, _module);

                // Ensure materials have their textures assigned before generating prefabs
                MBEditorUtility.ReloadModuleMaterials(_module);

                // Generate prefabs - onlyNew=true means skip existing, false=rebuild all
                MBPrefabsGenerator.GeneratePrefabs(_module, _onlyNewPrefabs);

                _statusMessage = _onlyNewPrefabs
                    ? "Items synced + new prefabs created."
                    : "Items synced + all prefabs rebuilt.";
                ScanItems();
            }
        }

        private void RegeneratePrefabs()
        {
            var affected = BrfSyncExecutor.CollectAffectedMeshNames(_brfScanResults.Values.ToList());

            if (affected.Count == 0)
            {
                if (EditorUtility.DisplayDialog("Prefab Rebuild",
                        "No specific changes. Run full regeneration?", "Yes", "Cancel"))
                {
                    MBPrefabsGenerator.GeneratePrefabs(_module);
                    _statusMessage = "Full prefab regeneration complete.";
                }
            }
            else
            {
                BrfSyncExecutor.RegenerateAffectedPrefabs(_module, affected);
                _statusMessage = $"Rebuilt prefabs for {affected.Count} meshes.";
            }

            Repaint();
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

            _brfScanResults.Clear();
            _brfScanErrors.Clear();
            _selectedBrfName = null;
            _scenePropDiff = null;
            _itemDiff = null;
            _textureDiff = null;
            _knownMeshNames = null;
            _brfSyncDone = false;
            _textureSyncDone = false;
            _statusMessage = "";

            _moduleContext?.Dispose();
            _nativeContext?.Dispose();
            _moduleContext = null;
            _nativeContext = null;
        }

        private void EnsureContexts()
        {
            if (_moduleContext == null && _module != null)
            {
                _moduleContext = new MBModuleImportContext(_module.ID);
                _moduleContext.BuildFileCache();
                _moduleContext.BuildMeshDataCache();
            }

            if (_nativeContext == null && _module != null && _module.ID != "Native")
            {
                _nativeContext = new MBModuleImportContext("Native");
                _nativeContext.BuildFileCache();
                _nativeContext.BuildMeshDataCache();
            }
        }

        private void RebuildKnownMeshNames()
        {
            if (_module == null) return;

            string dbPath = MBPathHelpers.ModBRFDataBasePath(_module.ID);
            var db = AssetDatabase.LoadAssetAtPath<ModBrfDataBase>(dbPath);

            _knownMeshNames = JsonDiffEngine.BuildKnownMeshNames(db);

            // Also add meshes from Native if this isn't the Native module
            if (_module.ID != "Native")
            {
                string nativeDbPath = MBPathHelpers.ModBRFDataBasePath("Native");
                var nativeDb = AssetDatabase.LoadAssetAtPath<ModBrfDataBase>(nativeDbPath);
                if (nativeDb != null)
                {
                    foreach (var name in JsonDiffEngine.BuildKnownMeshNames(nativeDb))
                        _knownMeshNames.Add(name);
                }
            }
        }

        /// <summary>
        /// Load the Native module's MBModule asset for cross-referencing.
        /// </summary>
        private MBModule LoadNativeModule()
        {
            string nativeAssetPath = MBPathHelpers.ModAssetPath("Native");
            return AssetDatabase.LoadAssetAtPath<MBModule>(nativeAssetPath);
        }

        private static void SetAllSelected(BrfDiffResult diff, bool selected)
        {
            foreach (var e in diff.Meshes.Where(e => e.HasChanges)) e.Selected = selected;
            foreach (var e in diff.Materials.Where(e => e.HasChanges)) e.Selected = selected;
            foreach (var e in diff.Textures.Where(e => e.HasChanges)) e.Selected = selected;
            foreach (var e in diff.Bodies.Where(e => e.HasChanges)) e.Selected = selected;
        }
    }
}
