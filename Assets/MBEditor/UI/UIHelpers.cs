using System;
using UnityEditor;
using UnityEngine;

namespace BDT.GUI.Helpers
{
    public static class UIHelpers
    {
        private static float _editorLabelWidth;
        
        public static float EditorLabelWidth
        {
            get => _editorLabelWidth;
        }
        static UIHelpers()
        {
            _editorLabelWidth = EditorGUIUtility.labelWidth;
        }
        public static float CalculateTextWidth(string text, GUIStyle style = default)
        {
            GUIContent content = new GUIContent(text);
            return style.CalcSize(content).x;
        }

        public static void DrawUILineVerticalLarge(Color color, int spaceX = 0)
        {
            Rect r = EditorGUILayout.GetControlRect(GUILayout.Width(3));
            r.height = 44;
            r.x += spaceX;
            r.y -= 10;
            r.height += 32;
            EditorGUI.DrawRect(r, color);
        }

        public static void DrawUILineVertical(Color color = default, int thickness = 2, int padding = 10, int lenght = 4)
        {
            Rect r = EditorGUILayout.GetControlRect(GUILayout.Width(padding + thickness));
            r.height = thickness;
            r.x += padding / 2;
            r.y -= 2;
            r.height += 6 + lenght;
            EditorGUI.DrawRect(r, (color == default) ? UIColors.GrayLine : color);
        }

        public static void DrawUILine(Color color = default, int thickness = 1, int padding = 8)
        {
            Rect r = EditorGUILayout.GetControlRect(GUILayout.Height(padding + thickness));
            r.height = thickness;
            r.y += padding / 2;
            r.x -= 2;
            r.width += 6;
            EditorGUI.DrawRect(r, (color == default) ? UIColors.GrayLine : color);
        }

        public static bool BlockElementStart(string labelText = "",bool visible = true)
        {
            string arrow = visible ? " ▼" : " ►";
            if (GUILayout.Button(labelText + arrow, StylesHelpers.BoxHeaderButton(visible)))
            {
                visible = !visible;
            }
            
            if (visible)
            {
                EditorGUILayout.BeginVertical(StylesHelpers.RoundedBoxStyle()); // Use the custom style
            }
            
            return visible;

        }

        public static void BlockElementEnd()
        {
                EditorGUILayout.EndVertical();
                DrawUILine(thickness:4);
        }

        public static GUIContent Content(string label,string tooltip)
        {
            return new GUIContent(label, tooltip);
        }
        
        public static void RemoveButtonDialog(string label, string tooltip,string dialogTitle,string dialogMessage,float width,Action action)
        {
            if (GUILayout.Button(Content(label, tooltip), GUILayout.Width(width)))
            {
                if (EditorUtility.DisplayDialog(dialogTitle, dialogMessage, "Yes", "No"))
                {
                    action();
                }
            }
        }
        
        public static void LabelHeader(string label)
        {
            var style = StylesHelpers.Label(UIColors.Cyan,14,true);
            GUILayout.Label(label, style);
        }
    }
}