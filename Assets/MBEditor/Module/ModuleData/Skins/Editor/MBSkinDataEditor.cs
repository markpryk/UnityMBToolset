using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Numerics;

namespace MountAndBlade.Data
{
    [CustomEditor(typeof(MBSkinData))]
    public class MBSkinDataEditor : UnityEditor.Editor
    {
        private MBSkinData skinData;
        
        // Foldout states
        private bool showBasicInfo = true;
        private bool showBodyMeshes = true;
        private bool showFaceGeneration = true;
        private bool showCustomization = true;
        private bool showAudio = true;
        private bool showTechnical = true;
        private bool showFlagInfo = false;

        // Skin flag selection
        private string[] skinFlagNames;
        private int selectedFlagIndex = 0;

        private void OnEnable()
        {
            skinData = (MBSkinData)target;
            
            // Build skin flag names array
            BuildSkinFlagArray();
            
            // Set initial flag index
            UpdateSelectedFlagIndex();
        }

        private void BuildSkinFlagArray()
        {
            var flags = SkinFlagPropertyDecoder.GetSkinFlags();
            skinFlagNames = new string[flags.Count + 1];
            skinFlagNames[0] = "None (0)";
            
            int index = 1;
            foreach (var flag in flags)
            {
                skinFlagNames[index] = flag.Key;
                index++;
            }
        }

        private void UpdateSelectedFlagIndex()
        {
            string currentFlag = SkinFlagPropertyDecoder.DecodeSkinFlag(skinData.SkinFlags);
            
            for (int i = 0; i < skinFlagNames.Length; i++)
            {
                if (skinFlagNames[i] == currentFlag)
                {
                    selectedFlagIndex = i;
                    return;
                }
            }
            
            selectedFlagIndex = 0;
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            DrawHeader();
            EditorGUILayout.Space(10);

            // Basic Info Section
            showBasicInfo = EditorGUILayout.Foldout(showBasicInfo, "Basic Info", true, EditorStyles.foldoutHeader);
            if (showBasicInfo)
            {
                EditorGUILayout.BeginVertical("box");
                DrawBasicInfo();
                EditorGUILayout.EndVertical();
            }
            EditorGUILayout.Space(5);

            // Body Meshes Section
            showBodyMeshes = EditorGUILayout.Foldout(showBodyMeshes, "Body Meshes", true, EditorStyles.foldoutHeader);
            if (showBodyMeshes)
            {
                EditorGUILayout.BeginVertical("box");
                DrawBodyMeshes();
                EditorGUILayout.EndVertical();
            }
            EditorGUILayout.Space(5);

            // Face Generation Section
            showFaceGeneration = EditorGUILayout.Foldout(showFaceGeneration, "Face Generation", true, EditorStyles.foldoutHeader);
            if (showFaceGeneration)
            {
                EditorGUILayout.BeginVertical("box");
                DrawFaceGeneration();
                EditorGUILayout.EndVertical();
            }
            EditorGUILayout.Space(5);

            // Customization Options Section
            showCustomization = EditorGUILayout.Foldout(showCustomization, "Customization Options", true, EditorStyles.foldoutHeader);
            if (showCustomization)
            {
                EditorGUILayout.BeginVertical("box");
                DrawCustomizationOptions();
                EditorGUILayout.EndVertical();
            }
            EditorGUILayout.Space(5);

            // Audio Section
            showAudio = EditorGUILayout.Foldout(showAudio, "Audio (Voice Entries)", true, EditorStyles.foldoutHeader);
            if (showAudio)
            {
                EditorGUILayout.BeginVertical("box");
                DrawAudio();
                EditorGUILayout.EndVertical();
            }
            EditorGUILayout.Space(5);

            // Technical Section
            showTechnical = EditorGUILayout.Foldout(showTechnical, "Technical", true, EditorStyles.foldoutHeader);
            if (showTechnical)
            {
                EditorGUILayout.BeginVertical("box");
                DrawTechnical();
                EditorGUILayout.EndVertical();
            }
            EditorGUILayout.Space(5);

            // Flag Information Section
            showFlagInfo = EditorGUILayout.Foldout(showFlagInfo, "📘 Skin Flag Information", true, EditorStyles.foldoutHeader);
            if (showFlagInfo)
            {
                EditorGUILayout.BeginVertical("box");
                DrawFlagInformation();
                EditorGUILayout.EndVertical();
            }

            serializedObject.ApplyModifiedProperties();
        }

        private void DrawHeader()
        {
            EditorGUILayout.BeginVertical("box");
            
            GUIStyle titleStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 16,
                alignment = TextAnchor.MiddleCenter
            };
            EditorGUILayout.LabelField("Mount & Blade Warband Skin", titleStyle);
            
            EditorGUILayout.Space(5);
            
            // Display decoded skin flag
            string decodedFlag = SkinFlagPropertyDecoder.DecodeSkinFlag(skinData.SkinFlags);
            string friendlyName = SkinFlagPropertyDecoder.FormatFriendlyName(decodedFlag);
            int morphKey = SkinFlagPropertyDecoder.GetMorphKeyNumber(skinData.SkinFlags);
            
            GUIStyle infoStyle = new GUIStyle(EditorStyles.helpBox)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 11,
                normal = { textColor = new Color(0.7f, 0.9f, 1f) }
            };
            
            string displayText = morphKey > 0 
                ? $"Current Morph Key: {morphKey} ({friendlyName})"
                : "No Morph Key Applied";
            
            EditorGUILayout.LabelField(displayText, infoStyle);
            
            EditorGUILayout.EndVertical();
        }

        private void DrawBasicInfo()
        {
            EditorGUILayout.LabelField("Identification", EditorStyles.boldLabel);
            
            skinData.SkinID = EditorGUILayout.TextField(
                new GUIContent("Skin ID", "Unique identifier (e.g., 'man', 'woman', 'undead')"),
                skinData.SkinID
            );

            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("Skin Flags", EditorStyles.boldLabel);
            
            // Skin flag dropdown
            EditorGUI.BeginChangeCheck();
            selectedFlagIndex = EditorGUILayout.Popup(
                new GUIContent("Morph Key", "Determines which vertex animation frame to use for body transformations"),
                selectedFlagIndex,
                skinFlagNames
            );
            
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(skinData, "Change Skin Flag");
                BigInteger newValue = SkinFlagPropertyDecoder.EncodeSkinFlag(skinFlagNames[selectedFlagIndex]);
                skinData.SkinFlags = (int)newValue;
                EditorUtility.SetDirty(skinData);
            }

            // Display current flag value
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.PrefixLabel("Flag Value");
            EditorGUILayout.SelectableLabel(
                $"0x{skinData.SkinFlags:X8}",
                EditorStyles.textField,
                GUILayout.Height(EditorGUIUtility.singleLineHeight)
            );
            EditorGUILayout.EndHorizontal();

            // Show description for current flag
            string currentFlag = skinFlagNames[selectedFlagIndex];
            string description = SkinFlagPropertyDecoder.GetFlagDescription(currentFlag);
            if (!string.IsNullOrEmpty(description))
            {
                EditorGUILayout.HelpBox(description, MessageType.Info);
            }
        }

        private void DrawBodyMeshes()
        {
            EditorGUILayout.LabelField("Body Mesh References", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("These reference meshes defined in body_meshes.brf", MessageType.Info);

            skinData.BodyMesh = EditorGUILayout.TextField(
                new GUIContent("Body Mesh", "Main body mesh"),
                skinData.BodyMesh
            );
            
            skinData.CalfMeshLeft = EditorGUILayout.TextField(
                new GUIContent("Calf Mesh (Left)", "Left calf mesh"),
                skinData.CalfMeshLeft
            );
            
            skinData.HandMeshLeft = EditorGUILayout.TextField(
                new GUIContent("Hand Mesh (Left)", "Left hand mesh"),
                skinData.HandMeshLeft
            );
            
            skinData.HeadMesh = EditorGUILayout.TextField(
                new GUIContent("Head Mesh", "Head mesh"),
                skinData.HeadMesh
            );
        }

        private void DrawFaceGeneration()
        {
            EditorGUILayout.LabelField("Face Keys", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Face keys control procedural face generation parameters. Each key defines a morphable trait (e.g., nose width, chin size).", MessageType.Info);

            SerializedProperty faceKeysProp = serializedObject.FindProperty("FaceKeys");
            EditorGUILayout.PropertyField(faceKeysProp, new GUIContent("Face Keys"), true);

            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("Face Key Constraints", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Constraints ensure generated faces stay within valid proportions.", MessageType.Info);

            SerializedProperty constraintsProp = serializedObject.FindProperty("FaceKeyConstraints");
            EditorGUILayout.PropertyField(constraintsProp, new GUIContent("Constraints"), true);

            if (GUILayout.Button("Add Face Key"))
            {
                Undo.RecordObject(skinData, "Add Face Key");
                skinData.FaceKeys.Add(new MBFaceKey(0, 0, 0f, 1f, "New Face Key"));
                EditorUtility.SetDirty(skinData);
            }
        }

        private void DrawCustomizationOptions()
        {
            EditorGUILayout.LabelField("Hair & Beard Options", EditorStyles.boldLabel);
            
            DrawStringList(ref skinData.HairMeshes, "Hair Meshes", "Available hair mesh options");
            EditorGUILayout.Space(5);
            
            DrawStringList(ref skinData.BeardMeshes, "Beard Meshes", "Available beard mesh options");
            EditorGUILayout.Space(5);
            
            DrawStringList(ref skinData.HairTextures, "Hair Textures", "Available hair texture options");
            EditorGUILayout.Space(5);
            
            DrawStringList(ref skinData.BeardTextures, "Beard Textures", "Available beard texture options");
            EditorGUILayout.Space(10);

            EditorGUILayout.LabelField("Face Textures", EditorStyles.boldLabel);
            SerializedProperty faceTexturesProp = serializedObject.FindProperty("FaceTextures");
            EditorGUILayout.PropertyField(faceTexturesProp, new GUIContent("Face Textures"), true);

            if (GUILayout.Button("Add Face Texture"))
            {
                Undo.RecordObject(skinData, "Add Face Texture");
                skinData.FaceTextures.Add(new MBFaceTexture("new_face_texture", "0xFFFFFF"));
                EditorUtility.SetDirty(skinData);
            }
        }

        private void DrawAudio()
        {
            EditorGUILayout.LabelField("Voice Sound Entries", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Map voice events (die, hit, yell, etc.) to sound IDs defined in module_sounds.py", MessageType.Info);

            SerializedProperty voicesProp = serializedObject.FindProperty("VoiceSounds");
            EditorGUILayout.PropertyField(voicesProp, new GUIContent("Voice Entries"), true);

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Add Voice Entry"))
            {
                Undo.RecordObject(skinData, "Add Voice Entry");
                skinData.VoiceSounds.Add(new MBVoiceEntry(VoiceEventType.Die, "snd_"));
                EditorUtility.SetDirty(skinData);
            }
            
            if (GUILayout.Button("Add Common Voices"))
            {
                Undo.RecordObject(skinData, "Add Common Voice Entries");
                AddCommonVoiceEntries();
                EditorUtility.SetDirty(skinData);
            }
            EditorGUILayout.EndHorizontal();

            // Show common voice setup info
            if (skinData.VoiceSounds.Count == 0)
            {
                EditorGUILayout.HelpBox("Male skins typically have 7 voice entries (die, hit, grunt, grunt_long, yell, stun, victory).\nFemale skins typically have 3 (die, hit, yell).", MessageType.Warning);
            }
        }

        private void AddCommonVoiceEntries()
        {
            string prefix = skinData.SkinID.ToLower();
            if (string.IsNullOrEmpty(prefix)) prefix = "skin";

            // Add common voice entries
            skinData.VoiceSounds.Add(new MBVoiceEntry(VoiceEventType.Die, $"snd_{prefix}_die"));
            skinData.VoiceSounds.Add(new MBVoiceEntry(VoiceEventType.Hit, $"snd_{prefix}_hit"));
            skinData.VoiceSounds.Add(new MBVoiceEntry(VoiceEventType.Grunt, $"snd_{prefix}_grunt"));
            skinData.VoiceSounds.Add(new MBVoiceEntry(VoiceEventType.GruntLong, $"snd_{prefix}_grunt_long"));
            skinData.VoiceSounds.Add(new MBVoiceEntry(VoiceEventType.Yell, $"snd_{prefix}_yell"));
            skinData.VoiceSounds.Add(new MBVoiceEntry(VoiceEventType.Stun, $"snd_{prefix}_stun"));
            skinData.VoiceSounds.Add(new MBVoiceEntry(VoiceEventType.Victory, $"snd_{prefix}_victory"));
        }

        private void DrawTechnical()
        {
            EditorGUILayout.LabelField("Skeleton & Scale", EditorStyles.boldLabel);
            
            skinData.SkeletonName = EditorGUILayout.TextField(
                new GUIContent("Skeleton Name", "Skeleton to use (e.g., 'skel_human')"),
                skinData.SkeletonName
            );
            
            skinData.Scale = EditorGUILayout.Slider(
                new GUIContent("Scale", "Scale multiplier. WARNING: Values != 1.0 have side effects!"),
                skinData.Scale,
                0.3f,
                2.0f
            );

            if (skinData.Scale != 1.0f)
            {
                EditorGUILayout.HelpBox(
                    "WARNING: Scale affects skeleton, hitboxes, and all meshes. Known issues:\n" +
                    "• Arrows/bolts stuck in agents scale with same factor\n" +
                    "• Scale > 1.0 may cause ragdoll bugs\n" +
                    "• Walk cycles speed changes with scale\n\n" +
                    "TIP: Viking Conquest uses scale ≤ 1.0 for all characters to avoid issues.",
                    MessageType.Warning
                );
            }

            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("Blood Particles (Optional)", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Leave empty to use default blood particles", MessageType.Info);
            
            skinData.BloodParticles1 = EditorGUILayout.TextField(
                new GUIContent("Blood Particles 1", "Custom blood particle system 1"),
                skinData.BloodParticles1
            );
            
            skinData.BloodParticles2 = EditorGUILayout.TextField(
                new GUIContent("Blood Particles 2", "Custom blood particle system 2"),
                skinData.BloodParticles2
            );
        }

        private void DrawFlagInformation()
        {
            EditorGUILayout.HelpBox(SkinFlagPropertyDecoder.GetGeneralInfo(), MessageType.Info);

            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("Available Morph Keys:", EditorStyles.boldLabel);

            var flags = SkinFlagPropertyDecoder.GetSkinFlags();
            foreach (var flag in flags)
            {
                EditorGUILayout.BeginVertical("box");
                
                string friendlyName = SkinFlagPropertyDecoder.FormatFriendlyName(flag.Key);
                EditorGUILayout.LabelField(friendlyName, EditorStyles.boldLabel);
                
                string description = SkinFlagPropertyDecoder.GetFlagDescription(flag.Key);
                EditorGUILayout.LabelField(description, EditorStyles.wordWrappedLabel);
                
                EditorGUILayout.LabelField($"Value: 0x{flag.Value:X8}", EditorStyles.miniLabel);
                
                EditorGUILayout.EndVertical();
                EditorGUILayout.Space(3);
            }
        }

        private void DrawStringList(ref List<string> list, string label, string tooltip)
        {
            EditorGUILayout.BeginVertical("box");
            
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(new GUIContent(label, tooltip), EditorStyles.boldLabel);
            if (GUILayout.Button("+", GUILayout.Width(30)))
            {
                Undo.RecordObject(skinData, $"Add {label}");
                list.Add("");
                EditorUtility.SetDirty(skinData);
            }
            EditorGUILayout.EndHorizontal();

            for (int i = 0; i < list.Count; i++)
            {
                EditorGUILayout.BeginHorizontal();
                
                list[i] = EditorGUILayout.TextField($"[{i}]", list[i]);
                
                if (GUILayout.Button("×", GUILayout.Width(25)))
                {
                    Undo.RecordObject(skinData, $"Remove {label}");
                    list.RemoveAt(i);
                    EditorUtility.SetDirty(skinData);
                    break;
                }
                
                EditorGUILayout.EndHorizontal();
            }

            if (list.Count == 0)
            {
                EditorGUILayout.LabelField("(No entries)", EditorStyles.centeredGreyMiniLabel);
            }

            EditorGUILayout.EndVertical();
        }
    }
}