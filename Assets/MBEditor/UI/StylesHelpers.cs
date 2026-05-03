using UnityEditor;
using UnityEngine;

namespace BDT.GUI.Helpers
{
    public static class StylesHelpers
    {
        public static GUIStyle BoxHeaderButton(bool isOpen)
        {
            GUIStyle style = new GUIStyle(EditorStyles.helpBox);
            style.margin.top = 0;
            style.margin.bottom = 0;
            style.padding.left = 10;
            style.padding.right = 10;
            style.padding.top = 10;
            style.padding.bottom = 10;
            style.fontSize = 16;
            style.alignment = TextAnchor.MiddleLeft;
            style.fontStyle = FontStyle.Bold;
            
            style.hover.textColor = isOpen ? UIColors.Orange : UIColors.Disable;
            style.active.textColor = isOpen ? UIColors.Orange : UIColors.Disable;
            style.normal.textColor = isOpen ? UIColors.Orange : UIColors.Disable;
            return style;
        }
        
        public static GUIStyle RoundedBoxStyle()
        {
            GUIStyle roundedBoxStyle;

            // Initialize the custom GUIStyle
            roundedBoxStyle = new GUIStyle(EditorStyles.helpBox);
            roundedBoxStyle.padding = new RectOffset(15, 15, 15, 15); // Add some padding
            roundedBoxStyle.margin = new RectOffset(5, 5, 5, 5); // Margin around the box

            // To create rounded corners, we usually need a texture with rounded corners.
            // Here, I'll use a simple workaround by setting the background color and border.
            // roundedBoxStyle.normal.background = EditorGUIUtility.Load("builtin skins/darkskin/images/node0.png") as Texture2D;
            roundedBoxStyle.border = new RectOffset(12, 12, 12, 12); // Adjust as needed

            return roundedBoxStyle;
        }

        public static GUIStyle BoxHeader(int fontSize = 22, bool bold = true, TextAnchor anchor = TextAnchor.MiddleLeft,
            Color color = default)
        {
            var style = new GUIStyle(EditorStyles.helpBox);

            style.padding = new RectOffset(15, 15, 15, 15); // Add some padding
            style.margin = new RectOffset(5, 5, 5, 5); // Margin around the box
            style.normal.textColor = (color == default) ? UIColors.Yellow : color;
            style.fontSize = fontSize;
            style.fontStyle = bold ? FontStyle.Bold : FontStyle.Normal;
            style.alignment = anchor;

            return style;
        }

        public static GUIStyle MiniLabel(Color col = default, bool bold = false, TextAnchor anchor = TextAnchor.MiddleLeft, bool wordWrap = false, int fontSize = 11)
        {
            GUIStyle style = new GUIStyle(bold ? EditorStyles.miniBoldLabel : EditorStyles.miniLabel);
            style.normal.textColor = (col == default) ? Color.gray : col;
            style.alignment = anchor;
            style.wordWrap = wordWrap;
            style.fontSize = fontSize;
            
            return style;
        }

        public static GUIStyle Label(Color col = default, int fontSize = 16, bool bold = false,
            TextAnchor anchor = TextAnchor.MiddleLeft, bool wordWrap = false)
        {
            GUIStyle style = new GUIStyle(bold ? EditorStyles.boldLabel : EditorStyles.label);
            style.normal.textColor = (col == default) ? Color.gray : col;
            style.alignment = anchor;
            style.fontSize = fontSize;
            style.wordWrap = wordWrap;
            return style;
        }
        
        public static GUIStyle AddButtonStyle(int textSize = 12, bool bold = true)
        {
            GUIStyle buttonStyle = new GUIStyle(EditorStyles.miniButton);
            buttonStyle.fontStyle = bold ? FontStyle.Bold : FontStyle.Normal;
            buttonStyle.hover.textColor = UIColors.Green;
            buttonStyle.fontSize = textSize;
            
            return buttonStyle;
        }
        public static GUIStyle RemoveButtonStyle(int textSize = 12, bool bold = true)
        {
            GUIStyle buttonStyle = new GUIStyle(EditorStyles.miniButton);
            buttonStyle.fontStyle = bold ? FontStyle.Bold : FontStyle.Normal;
            buttonStyle.hover.textColor = UIColors.Red;
            buttonStyle.fontSize = textSize;
            
            return buttonStyle;
        }
        
        public static GUIStyle WrapTextBoxStyle(int textSize = 12, bool bold = false)
        {
            GUIStyle buttonStyle = new GUIStyle(EditorStyles.textArea);
            buttonStyle.fontSize = textSize;
            buttonStyle.wordWrap = true;
            return buttonStyle;
        }
    }
}