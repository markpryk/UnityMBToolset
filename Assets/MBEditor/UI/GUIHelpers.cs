using System;
using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public static class GUIHelpers 
{

    private static GUIStyle titleStyle = null;
    public static GUIStyle foldoutStyle = null;
    public static GUIStyle foldoutBackgroundStyle = null;

    public static readonly GUIContent[] HeightmapResolutionOptions =
  {
        new GUIContent("33"),
        new GUIContent("65"),
        new GUIContent("129"),
        new GUIContent("257"),
        new GUIContent("513"),
        new GUIContent("1025"),
        new GUIContent("2049"),
        new GUIContent("4097")
    };

    public static readonly GUIContent[] MapResolutions =
  {
        new GUIContent("16"),
        new GUIContent("32"),
        new GUIContent("64"),
        new GUIContent("128"),
        new GUIContent("256"),
        new GUIContent("512"),
        new GUIContent("1024"),
        new GUIContent("2048"),
        new GUIContent("4096")
    };
    public enum GUIHelperColors
    {
        AppleGreen,
        OriolesOrange,
        VividCerulean,
        SelectiveYellow,
        Nickel,
        DarkLiver,
        AdobeGrey,
        AdobeBrown,
    }

    public static void DrawUILineVertical(Color color, int thickness = 1, int padding = 8, int lenght = 2)
    {
        Rect r = EditorGUILayout.GetControlRect(GUILayout.Width(padding + thickness));
        r.height = thickness;
        r.x += padding / 2;
        r.y -= 2;
        r.height += 6 + lenght;
        EditorGUI.DrawRect(r, color);
    }

    public static void DrawUILine(Color color, int thickness = 1, int padding = 8)
    {
        Rect r = EditorGUILayout.GetControlRect(GUILayout.Height(padding + thickness));
        r.height = thickness;
        r.y += padding / 2;
        r.x -= 2;
        r.width += 6;
        EditorGUI.DrawRect(r, color);
    }
    public static void InitGUIStyles()
    {
        if (titleStyle != null && foldoutStyle != null)
            return;

        titleStyle = new GUIStyle(EditorStyles.label);
        titleStyle.fontSize = 14;
        titleStyle.fontStyle = FontStyle.Bold;
        titleStyle.wordWrap = true;

        foldoutStyle = new GUIStyle(EditorStyles.foldout);
        foldoutStyle.fontSize = 11;
        foldoutStyle.fontStyle = FontStyle.Bold;

        foldoutBackgroundStyle = new GUIStyle(GUI.skin.box);
        foldoutBackgroundStyle.normal.background = MakeTex(2, 2, new Color(0.16f, 0.16f, 0.16f, 0.75f));
    }

    private static Texture2D MakeTex(int width, int height, Color col)
    {
        Color[] pix = new Color[width * height];
        for (int i = 0; i < pix.Length; i++)
            pix[i] = col;

        Texture2D result = new Texture2D(width, height);
        result.SetPixels(pix);
        result.Apply();
        return result;
    }

    public static bool DrawBoldFoldout(bool foldout, string content)
    {
        InitGUIStyles();

        EditorGUILayout.BeginHorizontal(foldoutBackgroundStyle);
        bool result = EditorGUILayout.Foldout(foldout, content, foldoutStyle);
        EditorGUILayout.EndHorizontal();

        return result;
    }
    public static bool DrawBoldFoldoutWithButton(bool foldout, string content, string buttonLabel, Color buttonColor, Vector2 buttonSize, Action cornerButtonAction = null)
    {
        InitGUIStyles();

        EditorGUILayout.BeginHorizontal(foldoutBackgroundStyle);
    
        // Draw Foldout
        bool result = EditorGUILayout.Foldout(foldout, content, foldoutStyle);

        // Flexible space to push button to right
        GUILayout.FlexibleSpace();

        // Apply button color
        Color originalColor = GUI.backgroundColor;
        GUI.backgroundColor = buttonColor;

        // Disable button if no action is assigned
        GUI.enabled = cornerButtonAction != null;

        // Draw button
        if (GUILayout.Button(buttonLabel, GUILayout.Width(buttonSize.x), GUILayout.Height(buttonSize.y)))
        {
            cornerButtonAction?.Invoke();
        }

        // Restore original GUI state
        GUI.enabled = true;
        GUI.backgroundColor = originalColor;

        EditorGUILayout.EndHorizontal();

        return result;
    }


    public static void DrawBoldLabel(string content)
    {
        InitGUIStyles();

        GUILayout.Label(content, titleStyle);
    }

    public static void BeginContents()
    {
        EditorGUI.indentLevel++;
        EditorGUILayout.BeginVertical(GUI.skin.GetStyle("HelpBox"));
    }

    public static void EndContents()
    {
        EditorGUILayout.EndVertical();
        EditorGUI.indentLevel--;
        GUILayout.Space(6);
    }

    public static Color HelperColor(GUIHelperColors id)
    {
        switch (id)
        {
            case GUIHelperColors.AppleGreen:
                return new Color32(124, 187, 0, 255);
            case GUIHelperColors.OriolesOrange:
                return new Color32(246, 83, 20, 255);
            case GUIHelperColors.VividCerulean:
                return new Color32(0, 161, 241, 255);
            case GUIHelperColors.SelectiveYellow:
                return new Color32(255, 187, 0, 255);
            case GUIHelperColors.Nickel:
                return new Color32(115, 115, 115, 255);
            case GUIHelperColors.DarkLiver:
                return new Color32(80, 80, 80, 255);
            case GUIHelperColors.AdobeGrey:
                return new Color32(106, 115, 123, 255);
            case GUIHelperColors.AdobeBrown:
                return new Color32(138, 121, 103, 255);
            default:
                return Color.clear;
        }

    }
}
