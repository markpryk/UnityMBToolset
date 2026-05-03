using UnityEngine;
using UnityEditor;

/// <summary>
/// Unified material inspector for all M&B/ shaders.
/// Adapts UI based on which shader variant is active.
/// </summary>
public class MBShaderGUI : ShaderGUI
{
    bool showAdvanced = false;
    bool showDebug = false;

    public override void OnGUI(MaterialEditor editor, MaterialProperty[] props)
    {
        Material mat = editor.target as Material;
        string shaderName = mat.shader.name;

        EditorGUILayout.LabelField("Mount & Blade Material", EditorStyles.boldLabel);

        // Show shader type badge
        string badge = shaderName switch
        {
            "M&B/M&B_Standard" => "STANDARD (Opaque)",
            "M&B/M&B_Cutout" => "CUTOUT (Alpha Test)",
            "M&B/M&B_Transparent" => "TRANSPARENT (Alpha Blend)",
            "M&B/M&B_Additive" => "ADDITIVE (Glow/Effects)",
            "M&B/Particles/AlphaBlend" => "PARTICLE (Alpha Blend / Modulate)",
            "M&B/Particles/Additive" => "PARTICLE (Additive)",
            "M&B/Particles/SunFlare" => "PARTICLE (Sun Flare)",
            _ => shaderName
        };

        var badgeColor = shaderName switch
        {
            "M&B/M&B_Standard" => new Color(0.3f, 0.5f, 0.8f),
            "M&B/M&B_Cutout" => new Color(0.3f, 0.7f, 0.3f),
            "M&B/M&B_Transparent" => new Color(0.7f, 0.5f, 0.8f),
            "M&B/M&B_Additive" => new Color(0.9f, 0.6f, 0.2f),
            "M&B/Particles/AlphaBlend" => new Color(0.2f, 0.8f, 0.8f),
            "M&B/Particles/Additive" => new Color(1f, 0.8f, 0.3f),
            "M&B/Particles/SunFlare" => new Color(1f, 0.95f, 0.4f),
            _ => Color.gray
        };

        var prevBg = GUI.backgroundColor;
        GUI.backgroundColor = badgeColor;
        EditorGUILayout.HelpBox(badge, MessageType.None);
        GUI.backgroundColor = prevBg;

        EditorGUILayout.Space(6);

        // ---- Textures ----
        EditorGUILayout.LabelField("Textures", EditorStyles.boldLabel);

        DrawTexture(editor, props, "_MainTex", "Diffuse A (Slot 0)",
            "Primary albedo/color texture. Alpha used for cutout or iron shininess.");

        if (HasProperty(props, "_BumpMap"))
            DrawTexture(editor, props, "_BumpMap", "Normal Map (Slot 1)",
                "Tangent-space normal map. DXT5nm by default.");

        if (HasProperty(props, "_SpecGlossMap"))
            DrawTexture(editor, props, "_SpecGlossMap", "Specular Map (Slot 2)",
                "Specular intensity map.");

        if (HasProperty(props, "_DetailAlbedoMap"))
            DrawTexture(editor, props, "_DetailAlbedoMap", "Diffuse B (Detail)",
                "Secondary detail texture, blended with diffuse A.");

        if (HasProperty(props, "_EnviroMap"))
            DrawTexture(editor, props, "_EnviroMap", "Environment Map",
                "Cubemap for reflections.");

        EditorGUILayout.Space(6);

        // ---- Material Properties ----
        EditorGUILayout.LabelField("Material Properties", EditorStyles.boldLabel);

        // DrawProperty(editor, props, "_Color", "Color Tint");

        if (HasProperty(props, "_MBSpecColor"))
            DrawProperty(editor, props, "_MBSpecColor", "Specular Color");

        if (HasProperty(props, "_Specular"))
        {
            DrawProperty(editor, props, "_Specular", "Specular (BRF 0-100)");

            // Show computed smoothness
            float specVal = FindProperty("_Specular", props).floatValue;
            EditorGUI.BeginDisabledGroup(true);
            EditorGUI.indentLevel++;
            EditorGUILayout.FloatField("→ Smoothness", specVal / 100f);
            EditorGUILayout.FloatField("→ Blinn-Phong Power", specVal * 1.28f);
            EditorGUI.indentLevel--;
            EditorGUI.EndDisabledGroup();
        }

        if (HasProperty(props, "_SpecIntensity"))
            DrawProperty(editor, props, "_SpecIntensity", "Specular Intensity");

        if (HasProperty(props, "_Cutoff"))
        {
            EditorGUILayout.Space(4);
            DrawProperty(editor, props, "_Cutoff", "Alpha Cutoff");
        }

        if (HasProperty(props, "_Intensity"))
            DrawProperty(editor, props, "_Intensity", "Intensity");

        EditorGUILayout.Space(6);

        // ---- Warband Mode (Standard shader only) ----
        if (HasProperty(props, "_WBMode"))
        {
            EditorGUILayout.LabelField("Warband Shader Mode", EditorStyles.boldLabel);

            var modeProp = FindProperty("_WBMode", props);
            EditorGUI.BeginChangeCheck();

            string[] modeNames = { "Plain (NM_PLAIN)", "Iron (NM_IRON)", "Shine (NM_SHINE)", "Preshaded" };
            int currentMode = (int)modeProp.floatValue;
            currentMode = EditorGUILayout.Popup("Mode", currentMode, modeNames);

            if (EditorGUI.EndChangeCheck())
            {
                modeProp.floatValue = currentMode;
                UpdateModeKeywords(mat, currentMode);
            }

            // Mode description
            string desc = currentMode switch
            {
                0 => "Standard normal-mapped. Specular from material value.",
                1 => "Metal/Iron: Diffuse alpha channel drives per-pixel shininess.",
                2 => "Specular map: Dedicated spec texture controls highlights.",
                3 => "Vertex-lit: Baked/pre-computed lighting in vertex colors.",
                _ => ""
            };
            EditorGUILayout.HelpBox(desc, MessageType.Info);
            EditorGUILayout.Space(4);
        }

        // ---- Rendering Options ----
        EditorGUILayout.LabelField("Options", EditorStyles.boldLabel);

        if (HasProperty(props, "_AGnm"))
        {
            EditorGUI.BeginChangeCheck();
            DrawProperty(editor, props, "_AGnm", "AGnm normals");
            if (EditorGUI.EndChangeCheck())
            {
                bool on = FindProperty("_AGnm", props).floatValue > 0.5f;
                SetKeyword(mat, "_AG_NORMAL", on);
            }
        }
        
        if (HasProperty(props, "_RGBnm"))
        {
            EditorGUI.BeginChangeCheck();
            DrawProperty(editor, props, "_RGBnm", "RGB normal");
            if (EditorGUI.EndChangeCheck())
            {
                bool on = FindProperty("_RGBnm", props).floatValue > 0.5f;
                SetKeyword(mat, "_RGB_NORMAL", on);
            }
        }

        if (HasProperty(props, "_UseVertexColor"))
        {
            EditorGUI.BeginChangeCheck();
            DrawProperty(editor, props, "_UseVertexColor", "Vertex Colors");
            if (EditorGUI.EndChangeCheck())
            {
                bool on = FindProperty("_UseVertexColor", props).floatValue > 0.5f;
                SetKeyword(mat, "_USEVERTEXCOLOR_ON", on);
            }
        }

        if (HasProperty(props, "_UseEnvMap"))
        {
            EditorGUI.BeginChangeCheck();
            DrawProperty(editor, props, "_UseEnvMap", "Environment Map");
            if (EditorGUI.EndChangeCheck())
            {
                bool on = FindProperty("_UseEnvMap", props).floatValue > 0.5f;
                SetKeyword(mat, "_USEENVMAP_ON", on);
            }
        }

        if (HasProperty(props, "_TwoSided"))
            DrawProperty(editor, props, "_TwoSided", "Two Sided");

        if (HasProperty(props, "_ZWrite"))
            DrawProperty(editor, props, "_ZWrite", "Z Write");

        if (HasProperty(props, "_SoftParticles"))
        {
            EditorGUI.BeginChangeCheck();
            DrawProperty(editor, props, "_SoftParticles", "Soft Particles");
            if (EditorGUI.EndChangeCheck())
            {
                bool on = FindProperty("_SoftParticles", props).floatValue > 0.5f;
                SetKeyword(mat, "_SOFTPARTICLES_ON", on);
            }
            if (FindProperty("_SoftParticles", props).floatValue > 0.5f)
                DrawProperty(editor, props, "_SoftFactor", "Soft Factor");
        }

        EditorGUILayout.Space(6);

        // ---- Advanced Lighting ----
        showAdvanced = EditorGUILayout.Foldout(showAdvanced, "Lighting Adjustments", true);
        if (showAdvanced)
        {
            EditorGUI.indentLevel++;

            if (HasProperty(props, "_AmbientBoost"))
                DrawProperty(editor, props, "_AmbientBoost", "Ambient Boost");
            if (HasProperty(props, "_DiffuseWrap"))
                DrawProperty(editor, props, "_DiffuseWrap", "Diffuse Wrap");
            if (HasProperty(props, "_EnvMapStrength"))
                DrawProperty(editor, props, "_EnvMapStrength", "Env Map Strength");
            if (HasProperty(props, "_DetailStrength"))
                DrawProperty(editor, props, "_DetailStrength", "Detail Blend Strength");
            if (HasProperty(props, "_SubsurfaceAmount"))
                DrawProperty(editor, props, "_SubsurfaceAmount", "Subsurface Scatter");

            EditorGUILayout.Space(4);
            EditorGUILayout.HelpBox(
                "These adjust the Blinn-Phong lighting to better match Warband's look:\n" +
                "• Ambient Boost: Warband scenes tend to be darker than Unity defaults\n" +
                "• Diffuse Wrap: Softens shadow terminator line\n" +
                "• Subsurface: Adds backlight scatter for foliage",
                MessageType.None);

            EditorGUI.indentLevel--;
        }

        EditorGUILayout.Space(4);

        // ---- Debug ----
        showDebug = EditorGUILayout.Foldout(showDebug, "Debug / BRF Data", true);
        if (showDebug)
        {
            EditorGUI.indentLevel++;

            if (HasProperty(props, "_BrfFlags"))
            {
                var flagsProp = FindProperty("_BrfFlags", props);
                long flags = (long)flagsProp.floatValue;

                EditorGUI.BeginDisabledGroup(true);
                EditorGUILayout.LongField("BRF Flags", flags);
                EditorGUILayout.TextField("Hex", $"0x{flags:X16}");
                EditorGUI.EndDisabledGroup();
            }

            EditorGUILayout.Space(2);
            editor.RenderQueueField();
            editor.EnableInstancingField();
            editor.DoubleSidedGIField();

            EditorGUI.indentLevel--;
        }
    }

    // ---- Utility Methods ----

    void DrawProperty(MaterialEditor editor, MaterialProperty[] props, string name, string label)
    {
        var prop = FindProperty(name, props, false);
        if (prop != null)
            editor.ShaderProperty(prop, label);
    }

    void DrawTexture(MaterialEditor editor, MaterialProperty[] props, string name, string label, string tooltip)
    {
        var prop = FindProperty(name, props, false);
        if (prop != null)
            editor.TexturePropertySingleLine(new GUIContent(label, tooltip), prop);
    }

    bool HasProperty(MaterialProperty[] props, string name)
    {
        return FindProperty(name, props, false) != null;
    }

    void SetKeyword(Material mat, string keyword, bool on)
    {
        if (on) mat.EnableKeyword(keyword);
        else mat.DisableKeyword(keyword);
    }

    void UpdateModeKeywords(Material mat, int mode)
    {
        mat.DisableKeyword("_WBMODE_PLAIN");
        mat.DisableKeyword("_WBMODE_IRON");
        mat.DisableKeyword("_WBMODE_SHINE");
        mat.DisableKeyword("_WBMODE_PRESHADED");

        string keyword = mode switch
        {
            0 => "_WBMODE_PLAIN",
            1 => "_WBMODE_IRON",
            2 => "_WBMODE_SHINE",
            3 => "_WBMODE_PRESHADED",
            _ => "_WBMODE_PLAIN"
        };

        mat.EnableKeyword(keyword);
    }
}