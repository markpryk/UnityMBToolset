using System;
using System.Collections.Generic;
using System.Linq;
using MountAndBlade.Data;
using UnityEditor;
using UnityEngine;

//  Flora Entry Browser - Visual grid picker for decorator layer entries

/// <summary>
/// EditorWindow that displays all registered flora library entries in a
/// visual grid with thumbnails, filtering, search, and multi-select.
///
/// Opened from the Decorator subtab's "Browse..." button.
/// Selected entries are added to the target <see cref="FloraDecoratorLayer"/>.
/// </summary>
public class FloraEntryBrowser : EditorWindow
{
    private MBFloraLibrary _library;
    private FloraPopulatorConfig _config;
    private FloraDecoratorLayer _targetLayer;
    private Action _onEntriesAdded;

    private string _searchText = "";
    private FloraCategory? _categoryFilter = null;
    private FloraBiome _biomeFilter = FloraBiome.None;
    private bool _hideAlreadyAdded = true;
    private SortMode _sortMode = SortMode.Category;

    private readonly HashSet<string> _selectedIds = new HashSet<string>();

    private Vector2 _scrollPos;
    private float _thumbSize = 72f;
    private const float THUMB_MIN = 48f;
    private const float THUMB_MAX = 128f;

    private readonly Dictionary<string, Texture2D> _previewCache = new Dictionary<string, Texture2D>();

    private List<FloraLibraryEntry> _filteredEntries;
    private bool _filterDirty = true;

    private enum SortMode { Category, Name, Biome, ProtoIndex }

    //  Public API - Open the browser

    /// <summary>
    /// Opens the browser window targeting a specific decorator layer.
    /// </summary>
    /// <param name="library">The flora library to browse.</param>
    /// <param name="config">The decorator config (for Undo).</param>
    /// <param name="layer">The layer to add entries to.</param>
    /// <param name="onAdded">Optional callback when entries are added.</param>
    public static FloraEntryBrowser Open(
        MBFloraLibrary library,
        FloraPopulatorConfig config,
        FloraDecoratorLayer layer,
        Action onAdded = null)
    {
        var window = GetWindow<FloraEntryBrowser>(true, "Flora Entry Browser", true);

        window._library = library;
        window._config = config;
        window._targetLayer = layer;
        window._onEntriesAdded = onAdded;
        window._selectedIds.Clear();
        window._filterDirty = true;
        window._previewCache.Clear();

        window.minSize = new Vector2(480, 400);

        // Center on screen
        #if !UNITY_EDITOR_OSX
        float w = 680, h = 560;
        window.position = new Rect(
            (Screen.currentResolution.width - w) * 0.5f,
            (Screen.currentResolution.height - h) * 0.4f,
            w, h);
        #endif

        window.Show();
        window.Focus();
        return window;
    }

    //  GUI

    private void OnGUI()
    {
        if (_library == null || _targetLayer == null)
        {
            EditorGUILayout.HelpBox("No library or target layer assigned. Open this window from the Decorator tab.", MessageType.Warning);
            return;
        }

        DrawToolbar();
        DrawGrid();
        DrawFooter();
    }

    private void OnFocus()
    {
        _filterDirty = true;
    }

    private void OnLostFocus()
    {
        // Keep window open - user might be checking something else
    }


    private void DrawToolbar()
    {
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);

        // Row 1: Search + thumb size
        EditorGUILayout.BeginHorizontal();

        EditorGUILayout.LabelField("🔍", GUILayout.Width(18));
        EditorGUI.BeginChangeCheck();
        _searchText = EditorGUILayout.TextField(_searchText, EditorStyles.toolbarSearchField);
        if (EditorGUI.EndChangeCheck()) _filterDirty = true;

        if (!string.IsNullOrEmpty(_searchText))
        {
            if (GUILayout.Button("✕", EditorStyles.miniButton, GUILayout.Width(20)))
            {
                _searchText = "";
                _filterDirty = true;
                GUI.FocusControl(null);
            }
        }

        GUILayout.Space(12);
        EditorGUILayout.LabelField("Size", GUILayout.Width(28));
        _thumbSize = GUILayout.HorizontalSlider(_thumbSize, THUMB_MIN, THUMB_MAX, GUILayout.Width(80));

        EditorGUILayout.EndHorizontal();

        // Row 2: Filters
        EditorGUILayout.BeginHorizontal();

        // Category filter
        EditorGUILayout.LabelField("Category:", GUILayout.Width(58));
        EditorGUI.BeginChangeCheck();
        if (DrawFilterToggle("All", _categoryFilter == null)) _categoryFilter = null;
        if (DrawFilterToggle("Detail", _categoryFilter == FloraCategory.Detail)) _categoryFilter = FloraCategory.Detail;
        if (DrawFilterToggle("Tree", _categoryFilter == FloraCategory.Tree)) _categoryFilter = FloraCategory.Tree;
        if (EditorGUI.EndChangeCheck()) _filterDirty = true;

        GUILayout.Space(12);

        // Biome filter
        EditorGUILayout.LabelField("Biome:", GUILayout.Width(42));
        EditorGUI.BeginChangeCheck();
        var newBiome = (FloraBiome)EditorGUILayout.EnumFlagsField(_biomeFilter, GUILayout.Width(100));
        if (EditorGUI.EndChangeCheck()) { _biomeFilter = newBiome; _filterDirty = true; }

        GUILayout.FlexibleSpace();

        // Sort
        EditorGUILayout.LabelField("Sort:", GUILayout.Width(30));
        EditorGUI.BeginChangeCheck();
        _sortMode = (SortMode)EditorGUILayout.EnumPopup(_sortMode, GUILayout.Width(80));
        if (EditorGUI.EndChangeCheck()) _filterDirty = true;

        EditorGUILayout.EndHorizontal();

        // Row 3: Options
        EditorGUILayout.BeginHorizontal();

        EditorGUI.BeginChangeCheck();
        _hideAlreadyAdded = EditorGUILayout.ToggleLeft("Hide already added", _hideAlreadyAdded, GUILayout.Width(140));
        if (EditorGUI.EndChangeCheck()) _filterDirty = true;

        GUILayout.FlexibleSpace();

        // Quick select buttons
        if (GUILayout.Button("Select All Visible", EditorStyles.miniButton, GUILayout.Width(110)))
        {
            RebuildFilteredIfDirty();
            foreach (var e in _filteredEntries)
                _selectedIds.Add(e.EntryID);
        }
        if (GUILayout.Button("Deselect All", EditorStyles.miniButton, GUILayout.Width(80)))
            _selectedIds.Clear();

        EditorGUILayout.EndHorizontal();

        // Target info
        EditorGUILayout.LabelField(
            $"Target: \"{_targetLayer.Name}\"  |  Library: {_library.Entries.Count} total, " +
            $"{_library.Entries.Count(IsSpawnable)} registered",
            EditorStyles.centeredGreyMiniLabel);

        EditorGUILayout.EndVertical();
    }

    /// <summary>Mini toggle button for toolbar filters.</summary>
    private static bool DrawFilterToggle(string label, bool active)
    {
        Color orig = GUI.backgroundColor;
        if (active) GUI.backgroundColor = new Color(0.55f, 0.85f, 0.55f);
        bool clicked = GUILayout.Button(label,
            active ? EditorStyles.toolbarButton : EditorStyles.miniButton,
            GUILayout.Width(44));
        GUI.backgroundColor = orig;
        return clicked;
    }


    private void DrawGrid()
    {
        RebuildFilteredIfDirty();

        if (_filteredEntries.Count == 0)
        {
            EditorGUILayout.Space(20);
            EditorGUILayout.LabelField("No matching registered entries found.",
                EditorStyles.centeredGreyMiniLabel);
            EditorGUILayout.Space(4);

            if (!string.IsNullOrEmpty(_searchText) || _categoryFilter != null || _biomeFilter != FloraBiome.None)
            {
                EditorGUILayout.BeginHorizontal();
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("Clear Filters", GUILayout.Width(100)))
                {
                    _searchText = "";
                    _categoryFilter = null;
                    _biomeFilter = FloraBiome.None;
                    _hideAlreadyAdded = true;
                    _filterDirty = true;
                }
                GUILayout.FlexibleSpace();
                EditorGUILayout.EndHorizontal();
            }
            return;
        }

        float cellSize = _thumbSize + 8f;
        float availableWidth = position.width - 20f;
        int columns = Mathf.Max(1, Mathf.FloorToInt(availableWidth / cellSize));

        _scrollPos = EditorGUILayout.BeginScrollView(_scrollPos);

        int index = 0;
        while (index < _filteredEntries.Count)
        {
            EditorGUILayout.BeginHorizontal();

            for (int col = 0; col < columns && index < _filteredEntries.Count; col++, index++)
            {
                DrawEntryCell(_filteredEntries[index], cellSize);
            }

            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
        }

        EditorGUILayout.EndScrollView();
    }

    private void DrawEntryCell(FloraLibraryEntry entry, float cellSize)
    {
        bool isSelected = _selectedIds.Contains(entry.EntryID);
        bool alreadyInLayer = _targetLayer.EntryIDs.Contains(entry.EntryID);

        // Cell background
        Color origBg = GUI.backgroundColor;
        if (isSelected)
            GUI.backgroundColor = new Color(0.4f, 0.8f, 0.4f);
        else if (alreadyInLayer)
            GUI.backgroundColor = new Color(0.65f, 0.65f, 0.65f);
        else
            GUI.backgroundColor = new Color(0.92f, 0.92f, 0.92f);

        EditorGUILayout.BeginVertical(EditorStyles.helpBox,
            GUILayout.Width(cellSize), GUILayout.Height(cellSize + 20f));
        GUI.backgroundColor = origBg;

        // Thumbnail button
        Texture2D preview = GetPreview(entry);

        GUIStyle thumbStyle = new GUIStyle(GUI.skin.button)
        {
            imagePosition = ImagePosition.ImageAbove,
            alignment = TextAnchor.MiddleCenter,
            padding = new RectOffset(2, 2, 2, 2),
            margin = new RectOffset(0, 0, 0, 0),
            fixedHeight = _thumbSize,
            fixedWidth = _thumbSize
        };

        // Dim if already added
        if (alreadyInLayer && !isSelected) GUI.color = new Color(1f, 1f, 1f, 0.45f);

        if (GUILayout.Button(preview, thumbStyle))
        {
            if (Event.current.button == 0)
            {
                // Left click: toggle selection
                if (Event.current.shift)
                {
                    // Shift+click: range select (toggle)
                    if (isSelected) _selectedIds.Remove(entry.EntryID);
                    else _selectedIds.Add(entry.EntryID);
                }
                else if (Event.current.control || Event.current.command)
                {
                    // Ctrl/Cmd+click: additive toggle
                    if (isSelected) _selectedIds.Remove(entry.EntryID);
                    else _selectedIds.Add(entry.EntryID);
                }
                else
                {
                    // Plain click: exclusive select
                    _selectedIds.Clear();
                    _selectedIds.Add(entry.EntryID);
                }
            }
            else if (Event.current.button == 1)
            {
                // Right-click: context menu
                ShowEntryCellContextMenu(entry);
            }
        }

        GUI.color = Color.white;

        string catChar = entry.Category == FloraCategory.Tree ? "T" : "D";
        if (entry.DetailMode == DetailMode.Billboard && entry.Category == FloraCategory.Detail)
            catChar = "B";

        string shortName = entry.EntryID;
        if (shortName.Length > 14) shortName = shortName.Substring(0, 12) + "…";

        // Status icons
        string statusPrefix = alreadyInLayer ? "✓ " : "";
        if (isSelected && !alreadyInLayer) statusPrefix = "● ";

        EditorGUILayout.LabelField(
            $"{statusPrefix}[{catChar}] {shortName}",
            new GUIStyle(EditorStyles.miniLabel) { alignment = TextAnchor.MiddleCenter, clipping = TextClipping.Clip },
            GUILayout.Width(cellSize), GUILayout.Height(14));

        EditorGUILayout.EndVertical();

        // Tooltip overlay on hover
        Rect cellRect = GUILayoutUtility.GetLastRect();
        if (cellRect.Contains(Event.current.mousePosition))
        {
            string biomeStr = entry.Biomes == FloraBiome.None ? "Any" : entry.Biomes.ToString();
            string sizeStr = entry.Category == FloraCategory.Detail
                ? $"W:{entry.MinWidth:F1}-{entry.MaxWidth:F1}  H:{entry.MinHeight:F1}-{entry.MaxHeight:F1}"
                : $"Bend:{entry.BendFactor:F2}";

            string tooltip = $"{entry.EntryID}\n" +
                             $"Category: {entry.Category}" +
                             (entry.Category == FloraCategory.Detail ? $" ({entry.DetailMode})" : "") + "\n" +
                             $"Biome: {biomeStr}\n" +
                             $"Proto#: {entry.PrototypeIndex}\n" +
                             sizeStr;

            if (alreadyInLayer) tooltip += "\n\n✓ Already in this layer";
            GUI.Label(cellRect, new GUIContent("", tooltip));
        }
    }

    private void ShowEntryCellContextMenu(FloraLibraryEntry entry)
    {
        var menu = new GenericMenu();
        bool alreadyInLayer = _targetLayer.EntryIDs.Contains(entry.EntryID);
        bool isSelected = _selectedIds.Contains(entry.EntryID);

        // Add / Remove single
        if (alreadyInLayer)
        {
            menu.AddItem(new GUIContent("Remove from Layer"), false, () =>
            {
                Undo.RecordObject(_config, "Remove Entry");
                _targetLayer.EntryIDs.Remove(entry.EntryID);
                EditorUtility.SetDirty(_config);
                _filterDirty = true;
            });
        }
        else
        {
            menu.AddItem(new GUIContent("Add to Layer"), false, () =>
            {
                Undo.RecordObject(_config, "Add Entry");
                _targetLayer.EntryIDs.Add(entry.EntryID);
                EditorUtility.SetDirty(_config);
                _onEntriesAdded?.Invoke();
            });
        }

        menu.AddSeparator("");

        // Select helpers
        if (!isSelected)
            menu.AddItem(new GUIContent("Select"), false, () => _selectedIds.Add(entry.EntryID));
        else
            menu.AddItem(new GUIContent("Deselect"), false, () => _selectedIds.Remove(entry.EntryID));

        // Select same category
        menu.AddItem(new GUIContent($"Select All {entry.Category}"), false, () =>
        {
            RebuildFilteredIfDirty();
            foreach (var e in _filteredEntries.Where(e => e.Category == entry.Category))
                _selectedIds.Add(e.EntryID);
        });

        // Select same biome
        if (entry.Biomes != FloraBiome.None)
        {
            menu.AddItem(new GUIContent($"Select Biome: {entry.Biomes}"), false, () =>
            {
                RebuildFilteredIfDirty();
                foreach (var e in _filteredEntries.Where(e => e.MatchesBiome(entry.Biomes)))
                    _selectedIds.Add(e.EntryID);
            });
        }

        menu.AddSeparator("");

        // Ping asset
        if (entry.PrototypePrefab != null)
            menu.AddItem(new GUIContent("Ping Prototype Prefab"), false,
                () => EditorGUIUtility.PingObject(entry.PrototypePrefab));
        if (entry.Prefab != null)
            menu.AddItem(new GUIContent("Ping Scene Prefab"), false,
                () => EditorGUIUtility.PingObject(entry.Prefab));

        // Info
        menu.AddDisabledItem(new GUIContent($"Proto#{entry.PrototypeIndex}  |  {entry.Category}  |  {entry.Biomes}"));

        menu.ShowAsContext();
    }


    private void DrawFooter()
    {
        RebuildFilteredIfDirty();

        EditorGUILayout.BeginVertical(EditorStyles.helpBox);

        // Selection info
        int selectedCount = _selectedIds.Count;
        int selectedNotInLayer = _selectedIds.Count(id => !_targetLayer.EntryIDs.Contains(id));
        int selectedInLayer = selectedCount - selectedNotInLayer;

        EditorGUILayout.BeginHorizontal();

        // Left: status
        string statusText = $"Showing {_filteredEntries.Count} entries";
        if (selectedCount > 0)
        {
            statusText += $"  |  {selectedCount} selected";
            if (selectedInLayer > 0)
                statusText += $" ({selectedInLayer} already added)";
        }
        EditorGUILayout.LabelField(statusText, EditorStyles.miniLabel);

        EditorGUILayout.EndHorizontal();

        // Action buttons
        EditorGUILayout.BeginHorizontal();

        // Add selected
        Color orig = GUI.backgroundColor;
        GUI.backgroundColor = new Color(0.45f, 0.88f, 0.45f);
        EditorGUI.BeginDisabledGroup(selectedNotInLayer == 0);

        string addLabel = selectedNotInLayer > 0
            ? $"Add {selectedNotInLayer} Selected to Layer"
            : "Add Selected to Layer";

        if (GUILayout.Button(addLabel, GUILayout.Height(26)))
        {
            AddSelectedToLayer();
        }
        EditorGUI.EndDisabledGroup();
        GUI.backgroundColor = orig;

        // Add all visible (not yet in layer)
        int visibleNotInLayer = _filteredEntries.Count(e => !_targetLayer.EntryIDs.Contains(e.EntryID));
        EditorGUI.BeginDisabledGroup(visibleNotInLayer == 0);
        if (GUILayout.Button($"Add All Visible ({visibleNotInLayer})", GUILayout.Height(26), GUILayout.Width(150)))
        {
            AddAllVisibleToLayer();
        }
        EditorGUI.EndDisabledGroup();

        // Close
        if (GUILayout.Button("Close", GUILayout.Height(26), GUILayout.Width(60)))
            Close();

        EditorGUILayout.EndHorizontal();
        EditorGUILayout.EndVertical();
    }

    //  Actions

    private void AddSelectedToLayer()
    {
        if (_config == null || _targetLayer == null) return;

        Undo.RecordObject(_config, "Add Selected Flora Entries");
        int added = 0;
        foreach (var id in _selectedIds)
        {
            if (_targetLayer.EntryIDs.Contains(id)) continue;
            var entry = _library.FindByID(id);
            if (!IsSpawnable(entry)) continue;

            _targetLayer.EntryIDs.Add(id);
            added++;
        }

        if (added > 0)
        {
            EditorUtility.SetDirty(_config);
            _onEntriesAdded?.Invoke();
            _selectedIds.Clear();
            _filterDirty = true;
            Debug.Log($"[FloraEntryBrowser] Added {added} entries to layer \"{_targetLayer.Name}\"");
        }
    }

    private void AddAllVisibleToLayer()
    {
        if (_config == null || _targetLayer == null) return;

        RebuildFilteredIfDirty();
        Undo.RecordObject(_config, "Add All Visible Flora Entries");
        int added = 0;

        foreach (var entry in _filteredEntries)
        {
            if (_targetLayer.EntryIDs.Contains(entry.EntryID)) continue;
            _targetLayer.EntryIDs.Add(entry.EntryID);
            added++;
        }

        if (added > 0)
        {
            EditorUtility.SetDirty(_config);
            _onEntriesAdded?.Invoke();
            _filterDirty = true;
            Debug.Log($"[FloraEntryBrowser] Added {added} visible entries to layer \"{_targetLayer.Name}\"");
        }
    }

    //  Filtering & Sorting

    private void RebuildFilteredIfDirty()
    {
        if (!_filterDirty && _filteredEntries != null) return;
        _filterDirty = false;

        _filteredEntries = new List<FloraLibraryEntry>();

        foreach (var entry in _library.Entries)
        {
            // Must be registered
            if (!IsSpawnable(entry)) continue;

            // Category filter
            if (_categoryFilter.HasValue && entry.Category != _categoryFilter.Value)
                continue;

            // Biome filter
            if (_biomeFilter != FloraBiome.None && !entry.MatchesBiome(_biomeFilter))
                continue;

            // Hide already added
            if (_hideAlreadyAdded && _targetLayer.EntryIDs.Contains(entry.EntryID))
                continue;

            // Search text (match against EntryID, MeshName, biome, conditions)
            if (!string.IsNullOrEmpty(_searchText))
            {
                string search = _searchText.ToLowerInvariant();
                bool match = false;

                if (entry.EntryID != null && entry.EntryID.ToLowerInvariant().Contains(search)) match = true;
                if (!match && entry.MeshName != null && entry.MeshName.ToLowerInvariant().Contains(search)) match = true;
                if (!match && entry.TerrainConditions != null && entry.TerrainConditions.ToLowerInvariant().Contains(search)) match = true;
                if (!match && entry.Biomes.ToString().ToLowerInvariant().Contains(search)) match = true;
                if (!match && entry.Category.ToString().ToLowerInvariant().Contains(search)) match = true;

                if (!match) continue;
            }

            _filteredEntries.Add(entry);
        }

        // Sort
        switch (_sortMode)
        {
            case SortMode.Category:
                _filteredEntries.Sort((a, b) =>
                {
                    int catCmp = a.Category.CompareTo(b.Category);
                    return catCmp != 0 ? catCmp : string.Compare(a.EntryID, b.EntryID, StringComparison.OrdinalIgnoreCase);
                });
                break;
            case SortMode.Name:
                _filteredEntries.Sort((a, b) => string.Compare(a.EntryID, b.EntryID, StringComparison.OrdinalIgnoreCase));
                break;
            case SortMode.Biome:
                _filteredEntries.Sort((a, b) =>
                {
                    int biomeCmp = a.Biomes.CompareTo(b.Biomes);
                    return biomeCmp != 0 ? biomeCmp : string.Compare(a.EntryID, b.EntryID, StringComparison.OrdinalIgnoreCase);
                });
                break;
            case SortMode.ProtoIndex:
                _filteredEntries.Sort((a, b) => a.PrototypeIndex.CompareTo(b.PrototypeIndex));
                break;
        }
    }

    //  Helpers

    private static bool IsSpawnable(FloraLibraryEntry entry)
    {
        return FloraSharedUtils.IsSpawnable(entry);
    }

    private Texture2D GetPreview(FloraLibraryEntry entry)
    {
        return FloraSharedUtils.GetEntryPreview(entry, _previewCache);
    }
}
