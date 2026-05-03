using UnityEngine;
using UnityEditor;

    /// <summary>
    /// Base interface for all Scene Data Manager tabs.
    /// Implement this to create a new tab.
    /// </summary>
    public interface ISceneDataManagerTab
    {
        /// <summary>Tab display name shown in the tab bar</summary>
        string TabName { get; }
        
        /// <summary>Tab order (lower = further left)</summary>
        int Order { get; }
        
        /// <summary>Whether this tab is currently enabled/visible</summary>
        bool IsEnabled { get; }
        
        /// <summary>Called when the tab is first created or the editor is enabled</summary>
        void OnEnable(MBSceneDataManager manager, SerializedObject serializedObject);
        
        /// <summary>Called when the editor is disabled</summary>
        void OnDisable();
        
        /// <summary>Draw the tab content</summary>
        void DrawTab();
        
        /// <summary>Called when the manager reference changes</summary>
        void OnManagerChanged(MBSceneDataManager manager);
    }

    /// <summary>
    /// Base class for tabs with common functionality.
    /// Extend this for easier tab creation.
    /// </summary>
    public abstract class SceneDataManagerTabBase : ISceneDataManagerTab
    {
        public MBSceneDataManager manager;
        protected SerializedObject serializedObject;
        
        /// <summary>Tab display name - must override</summary>
        public abstract string TabName { get; }
        
        /// <summary>Tab order - override to change position (default: 100)</summary>
        public virtual int Order => 100;
        
        /// <summary>Whether tab is enabled - override to conditionally hide</summary>
        public virtual bool IsEnabled => true;
        
        public virtual void OnEnable(MBSceneDataManager manager, SerializedObject serializedObject)
        {
            this.manager = manager;
            this.serializedObject = serializedObject;
        }
        
        public virtual void OnDisable() { }
        
        /// <summary>Draw the tab content - must override</summary>
        public abstract void DrawTab();
        
        public virtual void OnManagerChanged(MBSceneDataManager manager)
        {
            this.manager = manager;
        }
        
        #region UI Helper Methods
        
        /// <summary>Draw a bold header label</summary>
        protected void DrawHeader(string title)
        {
            EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
        }
        
        /// <summary>Draw a horizontal line separator</summary>
        protected void DrawUILine(Color color, int thickness = 1, int padding = 10)
        {
            Rect r = EditorGUILayout.GetControlRect(GUILayout.Height(padding + thickness));
            r.height = thickness;
            r.y += padding / 2f;
            r.x -= 2;
            r.width += 6;
            EditorGUI.DrawRect(r, color);
        }
        
        /// <summary>Draw a path field with browse button</summary>
        protected void DrawPathField(SerializedProperty prop, string label, bool isFolder, string extension = "")
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.PropertyField(prop, new GUIContent(label));
            
            if (GUILayout.Button("...", GUILayout.Width(25)))
            {
                string currentPath = prop.stringValue;
                string newPath;
                
                if (isFolder)
                {
                    newPath = EditorUtility.OpenFolderPanel($"Select {label}", currentPath, "");
                }
                else
                {
                    newPath = EditorUtility.OpenFilePanel($"Select {label}", 
                        string.IsNullOrEmpty(currentPath) ? "" : System.IO.Path.GetDirectoryName(currentPath), extension);
                }
                
                if (!string.IsNullOrEmpty(newPath))
                    prop.stringValue = newPath;
            }
            
            EditorGUILayout.EndHorizontal();
        }
        
        /// <summary>Draw a foldout and return its state</summary>
        protected bool DrawFoldout(ref bool foldout, string label)
        {
            foldout = EditorGUILayout.Foldout(foldout, label, true);
            return foldout;
        }
        
        /// <summary>Begin a box/container section</summary>
        protected void BeginBox()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        }
        
        /// <summary>End a box/container section</summary>
        protected void EndBox()
        {
            EditorGUILayout.EndVertical();
        }
        
        /// <summary>Draw a disabled text field (read-only display)</summary>
        protected void DrawDisabledField(string label, string value)
        {
            EditorGUI.BeginDisabledGroup(true);
            EditorGUILayout.TextField(label, value);
            EditorGUI.EndDisabledGroup();
        }
        
        /// <summary>Draw a disabled object field (read-only display)</summary>
        protected void DrawDisabledObjectField<T>(string label, T obj) where T : UnityEngine.Object
        {
            EditorGUI.BeginDisabledGroup(true);
            EditorGUILayout.ObjectField(label, obj, typeof(T), true);
            EditorGUI.EndDisabledGroup();
        }
        
        /// <summary>Draw a simple button</summary>
        protected bool DrawButton(string label, float height = 25f)
        {
            return GUILayout.Button(label, GUILayout.Height(height));
        }
        
        /// <summary>Draw a button with custom background color</summary>
        protected bool DrawButton(string label, Color backgroundColor, float height = 25f)
        {
            Color originalColor = GUI.backgroundColor;
            GUI.backgroundColor = backgroundColor;
            bool result = GUILayout.Button(label, GUILayout.Height(height));
            GUI.backgroundColor = originalColor;
            return result;
        }
        
        /// <summary>Draw a button with GUIContent and custom background color</summary>
        protected bool DrawButton(GUIContent content, Color backgroundColor, float height = 25f)
        {
            Color originalColor = GUI.backgroundColor;
            GUI.backgroundColor = backgroundColor;
            bool result = GUILayout.Button(content, GUILayout.Height(height));
            GUI.backgroundColor = originalColor;
            return result;
        }
        
        /// <summary>Draw a button with fixed width</summary>
        protected bool DrawButton(string label, float height, float width)
        {
            return GUILayout.Button(label, GUILayout.Height(height), GUILayout.Width(width));
        }
        
        /// <summary>Draw a button with custom color and fixed width</summary>
        protected bool DrawButton(string label, Color backgroundColor, float height, float width)
        {
            Color originalColor = GUI.backgroundColor;
            GUI.backgroundColor = backgroundColor;
            bool result = GUILayout.Button(label, GUILayout.Height(height), GUILayout.Width(width));
            GUI.backgroundColor = originalColor;
            return result;
        }
        
        /// <summary>Draw a mini label</summary>
        protected void DrawMiniLabel(string text)
        {
            EditorGUILayout.LabelField(text, EditorStyles.miniLabel);
        }
        
        /// <summary>Draw a bold mini label</summary>
        protected void DrawMiniBoldLabel(string text)
        {
            EditorGUILayout.LabelField(text, EditorStyles.miniBoldLabel);
        }
        
        /// <summary>Draw a help box</summary>
        protected void DrawHelpBox(string message, MessageType type = MessageType.Info)
        {
            EditorGUILayout.HelpBox(message, type);
        }
        
        /// <summary>Draw vertical space</summary>
        protected void DrawSpace(float pixels = 5f)
        {
            EditorGUILayout.Space(pixels);
        }
        
        /// <summary>Begin a horizontal layout group</summary>
        protected void BeginHorizontal()
        {
            EditorGUILayout.BeginHorizontal();
        }
        
        /// <summary>End a horizontal layout group</summary>
        protected void EndHorizontal()
        {
            EditorGUILayout.EndHorizontal();
        }
        
        /// <summary>Set GUI enabled state</summary>
        protected void SetEnabled(bool enabled)
        {
            GUI.enabled = enabled;
        }
        
        /// <summary>Reset GUI enabled state to true</summary>
        protected void ResetEnabled()
        {
            GUI.enabled = true;
        }
        
        #endregion
    }