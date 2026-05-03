using System;
using System.Collections.Generic;
using System.Linq;
using BDT.GUI.Helpers;
using MountAndBlade.Data;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>
/// Flora Populator subtab: browse library entries, toggle registration,
/// edit per-entry terrain settings, sync prototypes to terrain, and
/// auto-register by scene biome.
/// </summary>
internal class FloraLibrarySubTab : IFloraSubTab
{
    public string SubTabName => "Flora Library";

    // -- Filter state --
    private FloraCategory _selectedCategory = FloraCategory.Detail;
    private FloraBiome _biomeFilter = FloraBiome.All;
    private string _searchFilter = "";
    private bool _showRegisteredOnly;
    private SortMode _sortMode = SortMode.Name;

    // -- Selection --
    private FloraLibraryEntry _selectedEntry;
    private Editor _meshPreviewEditor;
    
    // -- Scroll / foldouts --
    private Vector2 _listScroll;
    private float _detailHeight;
    private bool _showTerrainSync = true;
    private bool _foldIdentity = true;
    private bool _foldPrefabs = true;
    private bool _foldSettings = true;
    private bool _foldFlags = true;
    private bool _foldVariants = true;
    private bool _foldPreview = true;

    // -- Preview / thumbnail cache --
    private readonly Dictionary<string, Texture2D> _thumbnailCache = new Dictionary<string, Texture2D>();

    // -- Filtered list cache --
    private List<FloraLibraryEntry> _filteredEntries;
    private bool _filterDirty = true;

    // -- Constants --
    private const float THUMB_SIZE = 64;
    private const float ROW_HEIGHT = 36f;
    private const float LIST_WIDTH_RATIO = 0.42f;

    private enum SortMode { Name, Biome, ProtoIndex, Status }

    // -- Cached styles (lazy-init, reset on domain reload) --
    private static GUIStyle _rowLabelStyle;
    private static GUIStyle _rowLabelSelectedStyle;
    private static GUIStyle _variantLinkStyle;
    private static GUIStyle _variantLinkBoldStyle;

    private static GUIStyle RowLabelStyle => _rowLabelStyle ??= new GUIStyle(EditorStyles.label)
    {
        richText = true, fontSize = 11,
    };
    private static GUIStyle RowLabelSelectedStyle => _rowLabelSelectedStyle ??= new GUIStyle(EditorStyles.label)
    {
        richText = true, fontSize = 11, fontStyle = FontStyle.Bold,
    };
    private static GUIStyle VariantLinkStyle => _variantLinkStyle ??= new GUIStyle(EditorStyles.linkLabel)
    {
        fontStyle = FontStyle.Normal,
    };
    private static GUIStyle VariantLinkBoldStyle => _variantLinkBoldStyle ??= new GUIStyle(EditorStyles.linkLabel)
    {
        fontStyle = FontStyle.Bold,
    };

    // -- Biome buttons --
    private static readonly (string label, FloraBiome biome)[] BiomeButtons =
    {
        ("All",    FloraBiome.All),
        ("Plain",  FloraBiome.Plain),
        ("Steppe", FloraBiome.Steppe),
        ("Snow",   FloraBiome.Snow),
        ("Desert", FloraBiome.Desert),
    };

    // -- Presets --
    private static readonly (string name, float minW, float maxW, float minH, float maxH, float density, float coverage, float noise)[] DetailPresets =
    {
        ("Grass",   0.5f, 1.5f, 0.3f, 0.8f, 0.8f, 0.6f, 0.15f),
        ("Bush",    1.0f, 2.5f, 0.8f, 1.8f, 0.4f, 0.3f, 0.25f),
        ("Rock",    0.8f, 3.0f, 0.6f, 2.0f, 0.2f, 0.15f, 0.4f),
        ("Flower",  0.3f, 0.8f, 0.2f, 0.5f, 0.6f, 0.4f, 0.1f),
    };

    private static readonly (string name, float minW, float maxW, float minH, float maxH, float bend)[] TreePresets =
    {
        ("Small Tree",  0.8f,  1.5f, 0.8f, 1.5f, 0.5f),
        ("Large Tree",  1.0f,  2.0f, 1.0f, 2.0f, 0.3f),
        ("Rock/Boulder",0.5f,  2.5f, 0.5f, 2.0f, 0.0f),
        ("Prop",        0.8f,  1.2f, 0.8f, 1.2f, 0.0f),
    };

    // -- Lifecycle --

    public void OnEnable(FloraTab parent)
    {
        _filterDirty = true;
        InvalidateCachedStyles();
    }

    public void OnDisable()
    {
        CleanupPreview();
        _thumbnailCache.Clear();
    }

    public void OnManagerChanged(FloraTab parent)
    {
        _selectedEntry = null;
        _filterDirty = true;
        CleanupPreview();
        _thumbnailCache.Clear();
    }

    private static void InvalidateCachedStyles()
    {
        _rowLabelStyle = null;
        _rowLabelSelectedStyle = null;
        _variantLinkStyle = null;
        _variantLinkBoldStyle = null;
    }

    //  Main Draw

    public void DrawSubTab(FloraTab tab)
    {
        DrawToolbar(tab);
        EditorGUILayout.Space(2);
        DrawCategoryBar();
        EditorGUILayout.Space(2);
        DrawFilterBar();
        UIHelpers.DrawUILine();

        float listWidth = EditorGUIUtility.currentViewWidth * LIST_WIDTH_RATIO;

        EditorGUILayout.BeginHorizontal();

        // Left panel: list with own scroll, height driven by detail panel
        using (new EditorGUILayout.VerticalScope(GUILayout.Width(listWidth)))
        {
            float scrollHeight = Mathf.Max(_detailHeight, 540f);
            _listScroll = EditorGUILayout.BeginScrollView(_listScroll,
                GUILayout.Height(scrollHeight));
            DrawEntryList(tab, listWidth);
            EditorGUILayout.EndScrollView();
            
            UIHelpers.DrawUILine();
            DrawBulkActions(tab);
        }

        UIHelpers.DrawUILineVertical(UIColors.GrayLine,lenght:(int)Mathf.Max(_detailHeight, 540f), padding:1, thickness:0);

        // Right panel: detail at natural height, tracks height for list scroll
        using (var detailScope = new EditorGUILayout.VerticalScope())
        {
            float prevLabelWidth = EditorGUIUtility.labelWidth;
            EditorGUIUtility.labelWidth = 100f;

            DrawEntryDetail(tab);

            EditorGUIUtility.labelWidth = prevLabelWidth;

            if (Event.current.type == EventType.Repaint)
                _detailHeight = detailScope.rect.height;
        }

        EditorGUILayout.EndHorizontal();

        UIHelpers.DrawUILine();
        DrawTerrainSyncSection(tab);
    }

    //  Toolbar

    private void DrawToolbar(FloraTab tab)
    {
        EditorGUILayout.BeginHorizontal();

        if (tab.Library != null)
        {
            int registered = tab.Library.Entries.Count(e => e.IsRegistered);
            GUILayout.Label(
                $"{tab.Library.Entries.Count} variants | {tab.Library.DetailCount} detail | " +
                $"{tab.Library.TreeCount} tree | {registered} registered",
                StylesHelpers.MiniLabel(UIColors.Wheat));
        }
        else
        {
            GUILayout.Label("No library loaded", StylesHelpers.MiniLabel(UIColors.Orange));
        }

        GUILayout.FlexibleSpace();

        if (GUILayout.Button("Build Library", StylesHelpers.AddButtonStyle(), GUILayout.Height(22), GUILayout.Width(110)))
        {
            tab.Library = MBFloraLibraryBuilder.BuildLibrary(tab.manager.Module);
            _selectedEntry = null;
            _filterDirty = true;
            _thumbnailCache.Clear();
            CleanupPreview();
        }

        EditorGUILayout.EndHorizontal();
    }

    //  Category Bar

    private void DrawCategoryBar()
    {
        int catIdx = _selectedCategory == FloraCategory.Detail ? 0 : 1;
        int newCat = GUILayout.Toolbar(catIdx,
            new[] { "Detail (grass / plants)", "Tree (trees / rocks)" },
            GUILayout.Height(22));

        if (newCat != catIdx)
        {
            _selectedCategory = newCat == 0 ? FloraCategory.Detail : FloraCategory.Tree;
            _selectedEntry = null;
            _filterDirty = true;
            CleanupPreview();
        }
    }

    //  Filter Bar

    private void DrawFilterBar()
    {
        // Row 1: Biome toggles
        EditorGUILayout.BeginHorizontal();
        GUILayout.Label("Biome", StylesHelpers.MiniLabel(UIColors.Cyan, bold: true), GUILayout.Width(42));

        for (int i = 0; i < BiomeButtons.Length; i++)
        {
            var (label, biome) = BiomeButtons[i];
            bool active = _biomeFilter == biome;

            if (DrawBiomeToggle(label, active))
            {
                _biomeFilter = biome;
                _selectedEntry = null;
                _filterDirty = true;
                CleanupPreview();
            }
        }
        EditorGUILayout.EndHorizontal();

        // Row 2: Search + Registered + Sort
        EditorGUILayout.BeginHorizontal();

        var searchIcon = EditorGUIUtility.IconContent("Search Icon");
        GUILayout.Label(searchIcon, GUILayout.Width(18), GUILayout.Height(16));

        EditorGUI.BeginChangeCheck();
        _searchFilter = EditorGUILayout.TextField(_searchFilter, EditorStyles.toolbarSearchField);
        if (EditorGUI.EndChangeCheck()) _filterDirty = true;

        if (!string.IsNullOrEmpty(_searchFilter))
        {
            if (GUILayout.Button("x", StylesHelpers.RemoveButtonStyle(), GUILayout.Width(20)))
            {
                _searchFilter = "";
                _filterDirty = true;
                GUI.FocusControl(null);
            }
        }

        GUILayout.Space(8);

        EditorGUI.BeginChangeCheck();
        _showRegisteredOnly = GUILayout.Toggle(_showRegisteredOnly, "Registered",
            EditorStyles.toolbarButton, GUILayout.Width(72));
        if (EditorGUI.EndChangeCheck()) _filterDirty = true;

        GUILayout.Space(4);
        GUILayout.Label("Sort:", StylesHelpers.MiniLabel(bold: true), GUILayout.Width(30));
        EditorGUI.BeginChangeCheck();
        _sortMode = (SortMode)EditorGUILayout.EnumPopup(_sortMode, GUILayout.Width(80));
        if (EditorGUI.EndChangeCheck()) _filterDirty = true;

        EditorGUILayout.EndHorizontal();
    }

    private static bool DrawBiomeToggle(string label, bool active)
    {
        Color orig = GUI.backgroundColor;
        if (active) GUI.backgroundColor = UIColors.VividCerulean;

        bool clicked = GUILayout.Button(label,
            active ? EditorStyles.toolbarButton : EditorStyles.miniButton,
            GUILayout.MinWidth(36));

        GUI.backgroundColor = orig;
        return clicked;
    }

    //  Entry List

    private void DrawEntryList(FloraTab tab, float listWidth)
    {
        if (tab.Library == null)
        {
            EditorGUILayout.HelpBox("Click 'Build Library' to generate flora entries from module data.",
                MessageType.Info);
            return;
        }

        RebuildFilteredIfDirty(tab.Library);

        UIHelpers.LabelHeader($"{_selectedCategory}  ({_filteredEntries.Count})");

        if (_biomeFilter == FloraBiome.All && !_showRegisteredOnly
            && string.IsNullOrEmpty(_searchFilter) && _sortMode == SortMode.Name)
            DrawGroupedByBiome(_filteredEntries, tab, listWidth);
        else
            DrawFlatList(_filteredEntries, tab, listWidth);

    }

    private void DrawGroupedByBiome(List<FloraLibraryEntry> entries, FloraTab tab, float listWidth)
    {
        var biomeGroups = new Dictionary<string, List<FloraLibraryEntry>>();
        var untagged = new List<FloraLibraryEntry>();

        foreach (var entry in entries)
        {
            if (entry.Biomes == FloraBiome.None) { untagged.Add(entry); continue; }
            string biomeLabel = GetPrimaryBiomeLabel(entry.Biomes);
            if (!biomeGroups.ContainsKey(biomeLabel))
                biomeGroups[biomeLabel] = new List<FloraLibraryEntry>();
            biomeGroups[biomeLabel].Add(entry);
        }

        foreach (var kvp in biomeGroups.OrderBy(k => k.Key))
        {
            GUILayout.Label($"-- {kvp.Key} ({kvp.Value.Count}) --",
                StylesHelpers.MiniLabel(UIColors.Wheat, anchor: TextAnchor.MiddleCenter));
            foreach (var entry in kvp.Value) DrawEntryRow(entry, tab, listWidth);
        }

        if (untagged.Count > 0)
        {
            GUILayout.Label($"-- Untagged ({untagged.Count}) --",
                StylesHelpers.MiniLabel(UIColors.Disable, anchor: TextAnchor.MiddleCenter));
            foreach (var entry in untagged) DrawEntryRow(entry, tab, listWidth);
        }
    }

    private void DrawFlatList(List<FloraLibraryEntry> entries, FloraTab tab, float listWidth)
    {
        for (int i = 0; i < entries.Count; i++)
            DrawEntryRow(entries[i], tab, listWidth, i);
    }

    private void DrawEntryRow(FloraLibraryEntry entry, FloraTab tab, float listWidth, int index = -1)
    {
        bool isSelected = _selectedEntry == entry;

        if (index >= 0 && index % 2 == 1 && !isSelected)
        {
            Rect rowBg = GUILayoutUtility.GetRect(0, 0);
            rowBg.height = ROW_HEIGHT + 2;
            rowBg.width = listWidth;
            EditorGUI.DrawRect(rowBg, new Color(0f, 0f, 0f, 0.06f));
        }

        Color origBg = GUI.backgroundColor;
        if (isSelected) GUI.backgroundColor = UIColors.VividCerulean * new Color(1, 1, 1, 0.4f);

        using (new EditorGUILayout.HorizontalScope(
                   isSelected ? "selectionRect" : GUIStyle.none,
                   GUILayout.Height(ROW_HEIGHT)))
        {
            if (isSelected) GUI.backgroundColor = origBg;

            DrawEntryThumbnail(entry);
            DrawStatusDot(entry);

            string label = BuildEntryLabel(entry);
            var labelStyle = isSelected ? RowLabelSelectedStyle : RowLabelStyle;
            Rect labelRect = GUILayoutUtility.GetRect(new GUIContent(label), labelStyle, GUILayout.ExpandWidth(true));

            if (GUI.Button(labelRect, label, labelStyle))
            {
                if (Event.current.button == 1) ShowEntryContextMenu(entry, tab);
                else { _selectedEntry = entry; CleanupPreview(); }
            }

            DrawRegistrationToggle(entry, tab);
        }
        GUILayout.Space(4);
    }

    private void DrawEntryThumbnail(FloraLibraryEntry entry)
    {
        Rect thumbRect = GUILayoutUtility.GetRect(THUMB_SIZE, THUMB_SIZE,
            GUILayout.Width(THUMB_SIZE), GUILayout.Height(THUMB_SIZE));

        Texture2D thumb = GetThumbnail(entry);
        if (thumb != null)
        {
            GUI.DrawTexture(thumbRect, thumb, ScaleMode.ScaleToFit);
        }
        else
        {
            EditorGUI.DrawRect(thumbRect, new Color(0.2f, 0.2f, 0.2f, 0.3f));
            string iconName = entry.Category == FloraCategory.Tree
                ? "d_TreeEditor.Leaf On" : "d_TerrainInspector.TerrainToolPlants";
            var icon = EditorGUIUtility.IconContent(iconName);
            if (icon?.image != null)
            {
                Rect iconRect = new Rect(
                    thumbRect.x + (thumbRect.width - 16) * 0.5f,
                    thumbRect.y + (thumbRect.height - 16) * 0.5f, 16, 16);
                GUI.DrawTexture(iconRect, icon.image, ScaleMode.ScaleToFit);
            }
        }
    }

    private static void DrawStatusDot(FloraLibraryEntry entry)
    {
        var oldColor = GUI.color;
        GUI.color = FloraSharedUtils.GetStatusColor(entry);
        GUILayout.Label(UIHelpers.Content("*", FloraSharedUtils.GetStatusTooltip(entry)), GUILayout.Width(14));
        GUI.color = oldColor;
    }

    private static string BuildEntryLabel(FloraLibraryEntry entry)
    {
        string label = entry.EntryID ?? "(null)";
        if (entry.Category == FloraCategory.Detail)
            label += entry.DetailMode == DetailMode.Billboard ? "  [B]" : "  [M]";
        if (entry.VariantIndex > 0)
            label += $"  v{entry.VariantIndex}";
        return label;
    }

    private void DrawRegistrationToggle(FloraLibraryEntry entry, FloraTab tab)
    {
        bool wasReg = entry.IsRegistered;
        entry.IsRegistered = EditorGUILayout.Toggle(entry.IsRegistered, GUILayout.Width(20));
        if (wasReg != entry.IsRegistered) MarkLibraryDirty(tab);
    }

    private void ShowEntryContextMenu(FloraLibraryEntry entry, FloraTab tab)
    {
        var menu = new GenericMenu();

        if (entry.IsRegistered)
            menu.AddItem(new GUIContent("Unregister"), false, () => { entry.IsRegistered = false; MarkLibraryDirty(tab); });
        else
            menu.AddItem(new GUIContent("Register"), false, () => { entry.IsRegistered = true; MarkLibraryDirty(tab); });

        menu.AddSeparator("");

        var otherCat = entry.Category == FloraCategory.Detail ? FloraCategory.Tree : FloraCategory.Detail;
        menu.AddItem(new GUIContent($"Move to {otherCat}"), false, () =>
        {
            entry.Category = otherCat;
            tab.Library.RebuildLookups();
            MarkLibraryDirty(tab);
        });

        if (entry.FloraData != null)
        {
            var variants = tab.Library.GetVariants(entry.FloraData.FloraID);
            if (variants.Count > 1)
                menu.AddItem(new GUIContent($"Register All Variants ({variants.Count})"), false, () =>
                {
                    foreach (var v in variants) v.IsRegistered = true;
                    MarkLibraryDirty(tab);
                });
        }

        menu.AddSeparator("");

        if (entry.PrototypePrefab != null)
            menu.AddItem(new GUIContent("Ping Terrain Prototype"), false, () => EditorGUIUtility.PingObject(entry.PrototypePrefab));
        if (entry.Prefab != null)
            menu.AddItem(new GUIContent("Ping Scene Prefab"), false, () => EditorGUIUtility.PingObject(entry.Prefab));
        if (entry.FloraData != null)
            menu.AddItem(new GUIContent("Ping Flora Data"), false, () => EditorGUIUtility.PingObject(entry.FloraData));

        menu.AddSeparator("");
        menu.AddDisabledItem(new GUIContent($"Proto#{entry.PrototypeIndex}  |  {entry.Category}  |  {entry.Biomes}"));
        menu.ShowAsContext();
    }

    // -- Bulk actions --

    private void DrawBulkActions(FloraTab tab)
    {
        if (tab.Library == null) return;

        EditorGUILayout.BeginHorizontal();
        GUILayout.Label("Visible", StylesHelpers.MiniLabel(UIColors.Disable), GUILayout.Width(42));

        if (GUILayout.Button("All +", StylesHelpers.AddButtonStyle()))
        {
            foreach (var e in _filteredEntries) e.IsRegistered = true;
            MarkLibraryDirty(tab);
        }
        if (GUILayout.Button("None x", StylesHelpers.RemoveButtonStyle()))
        {
            foreach (var e in _filteredEntries) e.IsRegistered = false;
            MarkLibraryDirty(tab);
        }
        if (GUILayout.Button("Has Assets", EditorStyles.miniButton))
        {
            foreach (var e in _filteredEntries) e.IsRegistered = e.HasRequiredAssets;
            MarkLibraryDirty(tab);
        }
        if (GUILayout.Button("V0 Only", EditorStyles.miniButton))
        {
            foreach (var e in _filteredEntries) e.IsRegistered = e.VariantIndex == 0 && e.HasRequiredAssets;
            MarkLibraryDirty(tab);
        }
        EditorGUILayout.EndHorizontal();
    }

    private void MarkLibraryDirty(FloraTab tab)
    {
        EditorUtility.SetDirty(tab.Library);
        _filterDirty = true;
    }

    //  Entry Detail

    private void DrawEntryDetail(FloraTab tab)
    {
        if (_selectedEntry == null)
        {
            EditorGUILayout.HelpBox("Select an entry from the list.", MessageType.Info);
            return;
        }

        var entry = _selectedEntry;

        // Header
        EditorGUILayout.BeginHorizontal();
        UIHelpers.LabelHeader(entry.EntryID);
        GUILayout.FlexibleSpace();

        Color origBg = GUI.backgroundColor;
        GUI.backgroundColor = entry.IsRegistered ? UIColors.Green : UIColors.Disable;
        bool wasReg = entry.IsRegistered;
        entry.IsRegistered = GUILayout.Toggle(entry.IsRegistered,
            entry.IsRegistered ? " + " : " x ", "Button", GUILayout.Width(32), GUILayout.Height(18));
        if (wasReg != entry.IsRegistered) MarkLibraryDirty(tab);
        GUI.backgroundColor = origBg;
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(4);

        // Identity
        _foldIdentity = UIHelpers.BlockElementStart("Identity", _foldIdentity);
        if (_foldIdentity)
        {
            EditorGUI.indentLevel++;
            EditorGUI.BeginDisabledGroup(true);
            EditorGUILayout.ObjectField("Flora Data", entry.FloraData, typeof(MBFloraData), false);
            EditorGUILayout.IntField("Variant", entry.VariantIndex);
            EditorGUILayout.TextField("Mesh", entry.MeshName);
            EditorGUI.EndDisabledGroup();

            EditorGUI.BeginChangeCheck();
            entry.Category = (FloraCategory)EditorGUILayout.EnumPopup("Category", entry.Category);
            if (EditorGUI.EndChangeCheck()) { tab.Library.RebuildLookups(); MarkLibraryDirty(tab); }

            EditorGUI.BeginChangeCheck();
            entry.Biomes = (FloraBiome)EditorGUILayout.EnumFlagsField("Biomes", entry.Biomes);
            if (EditorGUI.EndChangeCheck()) EditorUtility.SetDirty(tab.Library);

            if (entry.Category == FloraCategory.Detail)
            {
                EditorGUI.BeginChangeCheck();
                entry.DetailMode = (DetailMode)EditorGUILayout.EnumPopup("Detail Mode", entry.DetailMode);
                if (EditorGUI.EndChangeCheck()) EditorUtility.SetDirty(tab.Library);
            }
            EditorGUI.indentLevel--;
            UIHelpers.BlockElementEnd();
        }

        // Prefabs
        _foldPrefabs = UIHelpers.BlockElementStart("Prefabs", _foldPrefabs);
        if (_foldPrefabs)
        {
            EditorGUI.indentLevel++;
            EditorGUI.BeginDisabledGroup(true);
            EditorGUILayout.ObjectField("Scene Prefab", entry.Prefab, typeof(GameObject), false);
            EditorGUI.EndDisabledGroup();

            entry.PrototypePrefab = (GameObject)EditorGUILayout.ObjectField(
                "Terrain Prototype", entry.PrototypePrefab, typeof(GameObject), false);

            if (entry.Category == FloraCategory.Detail && entry.DetailMode == DetailMode.Billboard)
                entry.BillboardTexture = (Texture2D)EditorGUILayout.ObjectField(
                    "Billboard Texture", entry.BillboardTexture, typeof(Texture2D), false);

            if (!entry.HasRequiredAssets)
            {
                string msg = (entry.Category == FloraCategory.Detail && entry.DetailMode == DetailMode.Billboard)
                    ? "Billboard texture required."
                    : "Terrain Prototype required. Assign or rebuild library.";
                EditorGUILayout.HelpBox(msg, MessageType.Warning);
            }
            EditorGUI.indentLevel--;
            UIHelpers.BlockElementEnd();
        }

        // Terrain Settings
        _foldSettings = UIHelpers.BlockElementStart("Terrain Settings", _foldSettings);
        if (_foldSettings)
        {
            EditorGUI.indentLevel++;

            if (entry.Category == FloraCategory.Detail)
            {
                entry.DetailDensity = EditorGUILayout.Slider("Density", entry.DetailDensity, 0f, 1f);
                entry.TargetCoverage = EditorGUILayout.Slider("Coverage", entry.TargetCoverage, 0f, 1f);
                entry.NoiseSpread = EditorGUILayout.Slider("Noise Spread", entry.NoiseSpread, 0.01f, 1f);
                entry.PositionJitter = EditorGUILayout.Slider("Jitter", entry.PositionJitter, 0f, 1f);
            }

            DrawMinMaxRange("Width", ref entry.MinWidth, ref entry.MaxWidth, 0.1f, 10f);
            DrawMinMaxRange("Height", ref entry.MinHeight, ref entry.MaxHeight, 0.1f, 10f);

            entry.HealthyColor = EditorGUILayout.ColorField("Healthy Color", entry.HealthyColor);
            entry.DryColor = EditorGUILayout.ColorField("Dry Color", entry.DryColor);

            if (entry.Category == FloraCategory.Tree)
                entry.BendFactor = EditorGUILayout.FloatField("Bend Factor", entry.BendFactor);

            EditorGUI.indentLevel--;

            UIHelpers.DrawUILine(UIColors.GrayLine, thickness: 1, padding: 4);
            DrawPresetsRow(entry, tab);
            UIHelpers.BlockElementEnd();
        }

        // M&B Flags
        if (!string.IsNullOrEmpty(entry.TerrainConditions) || !string.IsNullOrEmpty(entry.BehaviorSummary))
        {
            _foldFlags = UIHelpers.BlockElementStart("M&B Flags", _foldFlags);
            if (_foldFlags)
            {
                EditorGUI.indentLevel++;
                if (!string.IsNullOrEmpty(entry.TerrainConditions))
                    GUILayout.Label($"Terrain: {entry.TerrainConditions}", StylesHelpers.MiniLabel(wordWrap: true));
                if (!string.IsNullOrEmpty(entry.BehaviorSummary))
                    GUILayout.Label($"Behavior: {entry.BehaviorSummary}", StylesHelpers.MiniLabel(wordWrap: true));
                EditorGUI.indentLevel--;
                UIHelpers.BlockElementEnd();
            }
        }

        // Variants
        if (entry.FloraData != null)
        {
            var variants = tab.Library.GetVariants(entry.FloraData.FloraID);
            if (variants.Count > 1)
            {
                _foldVariants = UIHelpers.BlockElementStart($"Variants ({variants.Count})", _foldVariants);
                if (_foldVariants)
                {
                    EditorGUI.indentLevel++;
                    foreach (var v in variants)
                    {
                        bool isCurrent = v == entry;
                        string hasProto = v.PrototypePrefab != null ? " +" : "";
                        var style = isCurrent ? VariantLinkBoldStyle : VariantLinkStyle;
                        string prefix = isCurrent ? ">" : " ";

                        if (GUILayout.Button($"{prefix} v{v.VariantIndex}  {v.MeshName}{hasProto}", style))
                        { _selectedEntry = v; CleanupPreview(); }
                    }
                    EditorGUI.indentLevel--;
                    UIHelpers.BlockElementEnd();
                }
            }
        }

        // Preview
        _foldPreview = UIHelpers.BlockElementStart("Preview", _foldPreview);
        if (_foldPreview)
        {
            DrawPreview(entry);
            UIHelpers.BlockElementEnd();
        }
    }

    private static void DrawMinMaxRange(string label, ref float min, ref float max, float rangeMin, float rangeMax)
    {
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField(label, GUILayout.Width(60));
        min = EditorGUILayout.FloatField(min, GUILayout.Width(40));
        EditorGUILayout.MinMaxSlider(ref min, ref max, rangeMin, rangeMax);
        max = EditorGUILayout.FloatField(max, GUILayout.Width(40));
        if (min > max) max = min;
        EditorGUILayout.EndHorizontal();
    }

    private void DrawPresetsRow(FloraLibraryEntry entry, FloraTab tab)
    {
        GUILayout.Label("Preset:", StylesHelpers.MiniLabel(UIColors.Cyan, bold: true));

        if (entry.Category == FloraCategory.Detail)
        {
            // Two rows of 2 to avoid overflow
            EditorGUILayout.BeginHorizontal();
            for (int i = 0; i < DetailPresets.Length; i++)
            {
                var p = DetailPresets[i];
                if (GUILayout.Button(p.name, EditorStyles.miniButton, GUILayout.MinWidth(50)))
                {
                    Undo.RecordObject(tab.Library, $"Apply Preset: {p.name}");
                    entry.MinWidth = p.minW; entry.MaxWidth = p.maxW;
                    entry.MinHeight = p.minH; entry.MaxHeight = p.maxH;
                    entry.DetailDensity = p.density; entry.TargetCoverage = p.coverage;
                    entry.NoiseSpread = p.noise;
                    EditorUtility.SetDirty(tab.Library);
                }
                if (i == 1) { EditorGUILayout.EndHorizontal(); EditorGUILayout.BeginHorizontal(); }
            }
            EditorGUILayout.EndHorizontal();
        }
        else
        {
            EditorGUILayout.BeginHorizontal();
            for (int i = 0; i < TreePresets.Length; i++)
            {
                var p = TreePresets[i];
                if (GUILayout.Button(p.name, EditorStyles.miniButton, GUILayout.MinWidth(50)))
                {
                    Undo.RecordObject(tab.Library, $"Apply Preset: {p.name}");
                    entry.MinWidth = p.minW; entry.MaxWidth = p.maxW;
                    entry.MinHeight = p.minH; entry.MaxHeight = p.maxH;
                    entry.BendFactor = p.bend;
                    EditorUtility.SetDirty(tab.Library);
                }
                if (i == 1) { EditorGUILayout.EndHorizontal(); EditorGUILayout.BeginHorizontal(); }
            }
            EditorGUILayout.EndHorizontal();
        }
    }

    private void DrawPreview(FloraLibraryEntry entry)
    {
        var previewTarget = entry.PrototypePrefab ?? entry.Prefab;
        if (previewTarget != null)
        {
            if (_meshPreviewEditor == null || _meshPreviewEditor.target != previewTarget)
            { CleanupPreview(); _meshPreviewEditor = Editor.CreateEditor(previewTarget); }
            _meshPreviewEditor?.OnInteractivePreviewGUI(
                GUILayoutUtility.GetRect(180, 180), EditorStyles.helpBox);
        }
        else if (entry.BillboardTexture != null)
        {
            var rect = GUILayoutUtility.GetRect(128, 128);
            EditorGUI.DrawPreviewTexture(rect, entry.BillboardTexture, null, ScaleMode.ScaleToFit);
        }
        else
        {
            GUILayout.Label("No preview available.",
                StylesHelpers.MiniLabel(UIColors.Disable, anchor: TextAnchor.MiddleCenter));
        }
    }

    //  Terrain Sync

    private void DrawTerrainSyncSection(FloraTab tab)
    {
        _showTerrainSync = UIHelpers.BlockElementStart("Terrain Sync", _showTerrainSync);
        if (!_showTerrainSync) return;

        if (tab.Library == null)
        {
            EditorGUILayout.HelpBox("Build a library first.", MessageType.Warning);
            UIHelpers.BlockElementEnd();
            return;
        }

        int detailReady = tab.Library.GetByCategory(FloraCategory.Detail)
            .Count(e => e.IsRegistered && e.HasRequiredAssets);
        int treeReady = tab.Library.GetByCategory(FloraCategory.Tree)
            .Count(e => e.IsRegistered && e.HasRequiredAssets);

        if (tab.SceneTerrain?.terrainData != null)
        {
            var td = tab.SceneTerrain.terrainData;
            GUILayout.Label(
                $"Ready: {detailReady} details, {treeReady} trees  |  " +
                $"Terrain: {td.detailPrototypes.Length} details, {td.treePrototypes.Length} trees",
                StylesHelpers.MiniLabel(UIColors.Wheat));

            bool detailMatch = td.detailPrototypes.Length == detailReady;
            bool treeMatch = td.treePrototypes.Length == treeReady;
            if (!detailMatch || !treeMatch)
                GUILayout.Label("! Terrain prototypes out of sync with library",
                    StylesHelpers.MiniLabel(UIColors.Orange));
        }
        else
        {
            GUILayout.Label($"Ready: {detailReady} details, {treeReady} trees", StylesHelpers.MiniLabel());
        }

        EditorGUILayout.Space(2);

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Sync Details", GUILayout.Height(26))) SyncDetailPrototypes(tab);
        if (GUILayout.Button("Sync Trees", GUILayout.Height(26))) SyncTreePrototypes(tab);
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(2);

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Sync All", StylesHelpers.AddButtonStyle(), GUILayout.Height(26)))
        { SyncDetailPrototypes(tab); SyncTreePrototypes(tab); }

        UIHelpers.RemoveButtonDialog(
            "Clear All", "Remove all prototypes and painted data from terrain",
            "Clear Prototypes",
            "Remove all prototypes and painted data from this terrain?\nThis cannot be undone.",
            0, () => ClearTerrainPrototypes(tab));
        EditorGUILayout.EndHorizontal();

        UIHelpers.BlockElementEnd();
    }



    //  Sync Operations

    private void SyncDetailPrototypes(FloraTab tab)
    {
        if (tab.SceneTerrain == null || tab.Library == null) return;
        var td = tab.SceneTerrain.terrainData;
        Undo.RecordObject(td, "Sync Detail Prototypes");

        var entries = tab.Library.GetByCategory(FloraCategory.Detail)
            .Where(e => e.IsRegistered && e.HasRequiredAssets).ToList();

        var prototypes = new DetailPrototype[entries.Count];
        for (int i = 0; i < entries.Count; i++)
        {
            var entry = entries[i];
            var proto = new DetailPrototype();

            if (entry.DetailMode == DetailMode.Mesh)
            {
                proto.prototype = entry.PrototypePrefab;
                proto.prototypeTexture = null;
                proto.usePrototypeMesh = true;
                proto.renderMode = DetailRenderMode.VertexLit;
                proto.useInstancing = true;
            }
            else
            {
                proto.prototype = null;
                proto.prototypeTexture = entry.BillboardTexture;
                proto.usePrototypeMesh = false;
                proto.renderMode = DetailRenderMode.GrassBillboard;
                proto.useInstancing = false;
            }

            proto.minWidth = entry.MinWidth; proto.maxWidth = entry.MaxWidth;
            proto.minHeight = entry.MinHeight; proto.maxHeight = entry.MaxHeight;
            proto.healthyColor = entry.HealthyColor; proto.dryColor = entry.DryColor;
            proto.density = entry.DetailDensity; proto.targetCoverage = entry.TargetCoverage;
            proto.noiseSpread = entry.NoiseSpread; proto.positionJitter = entry.PositionJitter;
            proto.alignToGround = entry.HasAlignToGround ? 1f : 0f;
            proto.useDensityScaling = true;

            if (!proto.Validate(out string error))
                Debug.LogWarning($"[FloraTab] '{entry.EntryID}': {error}");

            prototypes[i] = proto;
            entry.PrototypeIndex = i;
        }

        td.detailPrototypes = prototypes;
        td.RefreshPrototypes();
        EditorUtility.SetDirty(td);
        EditorUtility.SetDirty(tab.Library);
        Debug.Log($"[FloraTab] Synced {entries.Count} detail prototypes");
    }

    private void SyncTreePrototypes(FloraTab tab)
    {
        if (tab.SceneTerrain == null || tab.Library == null) return;
        var td = tab.SceneTerrain.terrainData;
        Undo.RecordObject(td, "Sync Tree Prototypes");

        var entries = tab.Library.GetByCategory(FloraCategory.Tree)
            .Where(e => e.IsRegistered && e.HasRequiredAssets).ToList();

        var prototypes = new TreePrototype[entries.Count];
        for (int i = 0; i < entries.Count; i++)
        {
            prototypes[i] = new TreePrototype { prefab = entries[i].PrototypePrefab };
            entries[i].PrototypeIndex = i;
        }

        td.treePrototypes = prototypes;
        td.RefreshPrototypes();
        EditorUtility.SetDirty(td);
        EditorUtility.SetDirty(tab.Library);
        Debug.Log($"[FloraTab] Synced {entries.Count} tree prototypes");
    }

    private void ClearTerrainPrototypes(FloraTab tab)
    {
        if (tab.SceneTerrain == null) return;
        var td = tab.SceneTerrain.terrainData;
        Undo.RecordObject(td, "Clear Prototypes");

        td.detailPrototypes = new DetailPrototype[0];
        td.treePrototypes = new TreePrototype[0];
        td.treeInstances = new TreeInstance[0];
        td.RefreshPrototypes();

        if (tab.Library != null)
        {
            foreach (var e in tab.Library.Entries) e.PrototypeIndex = -1;
            EditorUtility.SetDirty(tab.Library);
        }
        EditorUtility.SetDirty(td);
    }



    //  Filtering & Sorting

    private void RebuildFilteredIfDirty(MBFloraLibrary library)
    {
        if (!_filterDirty && _filteredEntries != null) return;
        _filterDirty = false;

        var entries = library.GetByCategory(_selectedCategory);

        if (_biomeFilter != FloraBiome.All)
            entries = entries.Where(e => e.MatchesBiome(_biomeFilter)).ToList();

        if (!string.IsNullOrEmpty(_searchFilter))
        {
            string search = _searchFilter.ToLowerInvariant();
            entries = entries.Where(e =>
            {
                if (e.EntryID != null && e.EntryID.ToLowerInvariant().Contains(search)) return true;
                if (e.MeshName != null && e.MeshName.ToLowerInvariant().Contains(search)) return true;
                if (e.Biomes.ToString().ToLowerInvariant().Contains(search)) return true;
                return false;
            }).ToList();
        }

        if (_showRegisteredOnly)
            entries = entries.Where(e => e.IsRegistered).ToList();

        entries = _sortMode switch
        {
            SortMode.Name => entries.OrderBy(e => e.EntryID, StringComparer.OrdinalIgnoreCase).ToList(),
            SortMode.Biome => entries.OrderBy(e => e.Biomes).ThenBy(e => e.EntryID, StringComparer.OrdinalIgnoreCase).ToList(),
            SortMode.ProtoIndex => entries.OrderBy(e => e.PrototypeIndex).ThenBy(e => e.EntryID, StringComparer.OrdinalIgnoreCase).ToList(),
            SortMode.Status => entries.OrderByDescending(e => e.IsRegistered).ThenByDescending(e => e.HasRequiredAssets)
                .ThenBy(e => e.EntryID, StringComparer.OrdinalIgnoreCase).ToList(),
            _ => entries,
        };

        _filteredEntries = entries;
    }

    //  Helpers

    private Texture2D GetThumbnail(FloraLibraryEntry entry)
    {
        return FloraSharedUtils.GetEntryPreview(entry, _thumbnailCache);
    }

    private static string GetPrimaryBiomeLabel(FloraBiome biomes)
    {
        if ((biomes & FloraBiome.Plain) != 0) return "Plain";
        if ((biomes & FloraBiome.Steppe) != 0) return "Steppe";
        if ((biomes & FloraBiome.Snow) != 0) return "Snow";
        if ((biomes & FloraBiome.Desert) != 0) return "Desert";
        return "Untagged";
    }

    private static FloraBiome GetBiomeFromTerrainType(int terrainType)
    {
        return terrainType switch
        {
            3 or 11 => FloraBiome.Plain,
            2 or 10 => FloraBiome.Steppe,
            4 or 12 => FloraBiome.Snow,
            5 or 13 => FloraBiome.Desert,
            _ => FloraBiome.All,
        };
    }



    private void CleanupPreview()
    {
        if (_meshPreviewEditor != null)
        {
            Object.DestroyImmediate(_meshPreviewEditor);
            _meshPreviewEditor = null;
        }
    }
}
