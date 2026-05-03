using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

    /// <summary>
    /// Static registry and manager for all Scene Data Manager tabs.
    /// Handles tab registration, ordering, and lifecycle.
    /// 
    /// Usage:
    /// - Tabs are automatically registered on Initialize()
    /// - Call RegisterTab() to add custom tabs at runtime
    /// - Call UnregisterTab<T>() to remove tabs
    /// </summary>
    internal static class SceneDataManagerTabRegistry
    {
        private static List<ISceneDataManagerTab> registeredTabs = new List<ISceneDataManagerTab>();
        private static bool isInitialized = false;
        
        /// <summary>All registered tabs, sorted by order</summary>
        public static IReadOnlyList<ISceneDataManagerTab> Tabs => registeredTabs;
        
        /// <summary>Current active tab index</summary>
        public static int CurrentTabIndex { get; set; } = 0;
        
        /// <summary>
        /// Initialize the tab registry with default tabs.
        /// Call this once when the editor is enabled.
        /// </summary>
        public static void Initialize()
        {
            if (isInitialized)
                return;
            
            registeredTabs.Clear();
            
            RegisterTab(new GeneralTab());
            RegisterTab(new MBRGLGeneratorTab());

            RegisterTab(new HeightmapTab());   
            RegisterTab(new DecoratorTab());  
            RegisterTab(new TintTab());
            RegisterTab(new ErosionTab());
            RegisterTab(new FloraTab());

            RegisterTab(new AiMeshExportTab());
            RegisterTab(new ExportTab());
            
            SortTabs();
            isInitialized = true;
        }
        
        /// <summary>
        /// Register a new tab. Call this to add custom tabs.
        /// If a tab of the same type already exists, it will be replaced.
        /// </summary>
        /// <param name="tab">Tab instance to register</param>
        public static void RegisterTab(ISceneDataManagerTab tab)
        {
            if (tab == null)
                return;
            
            // Prevent duplicates - replace existing tab of same type
            for (int i = 0; i < registeredTabs.Count; i++)
            {
                if (registeredTabs[i].GetType() == tab.GetType())
                {
                    registeredTabs[i] = tab;
                    SortTabs();
                    return;
                }
            }
            
            registeredTabs.Add(tab);
            SortTabs();
        }
        
        /// <summary>
        /// Unregister a tab by type.
        /// </summary>
        /// <typeparam name="T">Type of tab to remove</typeparam>
        public static void UnregisterTab<T>() where T : ISceneDataManagerTab
        {
            registeredTabs.RemoveAll(t => t is T);
        }
        
        /// <summary>
        /// Unregister a tab by instance.
        /// </summary>
        /// <param name="tab">Tab instance to remove</param>
        public static void UnregisterTab(ISceneDataManagerTab tab)
        {
            registeredTabs.Remove(tab);
        }
        
        /// <summary>
        /// Get a registered tab by type.
        /// </summary>
        /// <typeparam name="T">Type of tab to find</typeparam>
        /// <returns>Tab instance or null if not found</returns>
        public static T GetTab<T>() where T : class, ISceneDataManagerTab
        {
            foreach (var tab in registeredTabs)
            {
                if (tab is T typedTab)
                    return typedTab;
            }
            return null;
        }
        
        /// <summary>
        /// Check if a tab of the specified type is registered.
        /// </summary>
        /// <typeparam name="T">Type of tab to check</typeparam>
        /// <returns>True if tab is registered</returns>
        public static bool HasTab<T>() where T : ISceneDataManagerTab
        {
            foreach (var tab in registeredTabs)
            {
                if (tab is T)
                    return true;
            }
            return false;
        }
        
        /// <summary>
        /// Get the count of registered tabs.
        /// </summary>
        public static int TabCount => registeredTabs.Count;
        
        /// <summary>
        /// Get the count of enabled (visible) tabs.
        /// </summary>
        public static int EnabledTabCount
        {
            get
            {
                int count = 0;
                foreach (var tab in registeredTabs)
                {
                    if (tab.IsEnabled)
                        count++;
                }
                return count;
            }
        }
        
        /// <summary>
        /// Called when the editor is enabled. Initializes all tabs.
        /// </summary>
        /// <param name="manager">The MBSceneDataManager being edited</param>
        /// <param name="serializedObject">The serialized object for the manager</param>
        public static void OnEnable(MBSceneDataManager manager, SerializedObject serializedObject)
        {
            Initialize();
            
            foreach (var tab in registeredTabs)
            {
                tab.OnEnable(manager, serializedObject);
            }
        }
        
        /// <summary>
        /// Called when the editor is disabled. Cleans up all tabs.
        /// </summary>
        public static void OnDisable()
        {
            foreach (var tab in registeredTabs)
            {
                tab.OnDisable();
            }
        }
        
        /// <summary>
        /// Called when the manager reference changes.
        /// </summary>
        /// <param name="manager">The new manager reference</param>
        public static void OnManagerChanged(MBSceneDataManager manager)
        {
            foreach (var tab in registeredTabs)
            {
                tab.OnManagerChanged(manager);
            }
        }
        
        /// <summary>
        /// Draw the tab bar split across two rows.
        /// Tabs are distributed evenly: ⌈N/2⌉ on the first row, the rest on the second.
        /// </summary>
        public static void DrawTabBar()
        {
            DrawTwoRowTabBar(new Color(0.6f, 0.8f, 1f), Color.white, EditorStyles.toolbarButton);
        }
        
        /// <summary>
        /// Draw the tab bar split across two rows with custom colours / style.
        /// </summary>
        public static void DrawTabBar(Color activeColor, Color inactiveColor, GUIStyle style = null)
        {
            DrawTwoRowTabBar(activeColor, inactiveColor, style ?? EditorStyles.toolbarButton);
        }
        
        
        private static void DrawTwoRowTabBar(Color activeColor, Color inactiveColor, GUIStyle style)
        {
            var enabledTabs   = GetEnabledTabs();
            int total         = enabledTabs.Count;
            int firstRowCount = Mathf.CeilToInt(total / 2f);  // upper half on row 1

            // Row 1
            EditorGUILayout.BeginHorizontal();
            for (int i = 0; i < firstRowCount; i++)
                DrawTabButton(enabledTabs[i], activeColor, inactiveColor, style);
            EditorGUILayout.EndHorizontal();

            // Row 2 (only if there are tabs that didn't fit on row 1)
            if (total > firstRowCount)
            {
                EditorGUILayout.BeginHorizontal();
                for (int i = firstRowCount; i < total; i++)
                    DrawTabButton(enabledTabs[i], activeColor, inactiveColor, style);
                EditorGUILayout.EndHorizontal();
            }

            GUI.backgroundColor = Color.white;
        }
        
        private static void DrawTabButton(
            ISceneDataManagerTab tab,
            Color activeColor, Color inactiveColor, GUIStyle style)
        {
            int actualIndex = registeredTabs.IndexOf(tab);
            GUI.backgroundColor = CurrentTabIndex == actualIndex ? activeColor : inactiveColor;
            if (GUILayout.Button(tab.TabName, style))
                CurrentTabIndex = actualIndex;
        }
        
        /// <summary>
        /// Draw the current tab content. Call this from the editor's OnInspectorGUI.
        /// </summary>
        public static void DrawCurrentTab()
        {
            // Validate current index
            if (CurrentTabIndex < 0 || CurrentTabIndex >= registeredTabs.Count)
            {
                CurrentTabIndex = 0;
            }
            
            // Ensure we have tabs and current tab is enabled
            if (registeredTabs.Count > 0)
            {
                var currentTab = registeredTabs[CurrentTabIndex];
                
                if (currentTab.IsEnabled)
                {
                    currentTab.DrawTab();
                }
                else
                {
                    // Find first enabled tab
                    for (int i = 0; i < registeredTabs.Count; i++)
                    {
                        if (registeredTabs[i].IsEnabled)
                        {
                            CurrentTabIndex = i;
                            registeredTabs[i].DrawTab();
                            return;
                        }
                    }
                    
                    // No enabled tabs
                    EditorGUILayout.HelpBox("No tabs available.", MessageType.Info);
                }
            }
            else
            {
                EditorGUILayout.HelpBox("No tabs registered.", MessageType.Warning);
            }
        }
        
        /// <summary>
        /// Switch to a specific tab by type.
        /// </summary>
        /// <typeparam name="T">Type of tab to switch to</typeparam>
        /// <returns>True if tab was found and switched to</returns>
        public static bool SwitchToTab<T>() where T : ISceneDataManagerTab
        {
            for (int i = 0; i < registeredTabs.Count; i++)
            {
                if (registeredTabs[i] is T && registeredTabs[i].IsEnabled)
                {
                    CurrentTabIndex = i;
                    return true;
                }
            }
            return false;
        }
        
        /// <summary>
        /// Switch to a specific tab by name.
        /// </summary>
        /// <param name="tabName">Name of the tab to switch to</param>
        /// <returns>True if tab was found and switched to</returns>
        public static bool SwitchToTab(string tabName)
        {
            for (int i = 0; i < registeredTabs.Count; i++)
            {
                if (registeredTabs[i].TabName == tabName && registeredTabs[i].IsEnabled)
                {
                    CurrentTabIndex = i;
                    return true;
                }
            }
            return false;
        }
        
        /// <summary>
        /// Reset the registry. Clears all tabs and resets state.
        /// Call Initialize() again after this to restore default tabs.
        /// </summary>
        public static void Reset()
        {
            OnDisable();
            registeredTabs.Clear();
            isInitialized = false;
            CurrentTabIndex = 0;
        }
        
        /// <summary>
        /// Force re-initialization. Clears and re-registers all default tabs.
        /// </summary>
        public static void Reinitialize()
        {
            Reset();
            Initialize();
        }
        
        #region Private Helpers
        
        private static void SortTabs()
        {
            registeredTabs.Sort((a, b) => a.Order.CompareTo(b.Order));
        }
        
        private static List<ISceneDataManagerTab> GetEnabledTabs()
        {
            var enabled = new List<ISceneDataManagerTab>();
            foreach (var tab in registeredTabs)
            {
                if (tab.IsEnabled)
                    enabled.Add(tab);
            }
            return enabled;
        }
        
        #endregion
    }
