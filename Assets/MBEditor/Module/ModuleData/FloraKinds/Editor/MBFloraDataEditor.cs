using UnityEngine;
using UnityEditor;
using System.Numerics;

namespace MountAndBlade.Data
{
    [CustomEditor(typeof(MBFloraData))]
    public class MBFloraDataEditor : UnityEditor.Editor
    {
        // Foldout states
        private bool showBasicInfo = true;
        private bool showTerrainConditions = true;
        private bool showBehaviorFlags = true;
        private bool showTypeFlags = true;
        private bool showColonySettings = false;
        private bool showMeshes = true;
        private bool showRawData = false;
        
        // Decoded flag data
        private FloraDecodedFlags decodedFlags;
        
        // Validation
        private bool hasWarnings = false;
        private string warningMessage = "";
        
        // Styles
        private GUIStyle headerStyle;
        private GUIStyle warningStyle;
        private GUIStyle infoBoxStyle;
        
        private void OnEnable()
        {
            UpdateDecodedFlags();
        }
        
        private void InitStyles()
        {
            if (headerStyle == null)
            {
                headerStyle = new GUIStyle(EditorStyles.boldLabel);
                headerStyle.fontSize = 12;
            }
            
            if (warningStyle == null)
            {
                warningStyle = new GUIStyle(EditorStyles.helpBox);
                warningStyle.normal.textColor = new Color(1f, 0.6f, 0f);
            }
            
            if (infoBoxStyle == null)
            {
                infoBoxStyle = new GUIStyle(EditorStyles.helpBox);
                infoBoxStyle.normal.textColor = new Color(0.6f, 0.8f, 1f);
            }
        }
        
        public override void OnInspectorGUI()
        {
            InitStyles();
            
            MBFloraData floraData = (MBFloraData)target;
            
            EditorGUI.BeginChangeCheck();
            
            // Title
            EditorGUILayout.Space(5);
            EditorGUILayout.LabelField("Mount & Blade Flora Configuration", headerStyle);
            EditorGUILayout.Space(10);
            
            // Preset buttons
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Quick Presets:", GUILayout.Width(100));
            
            if (GUILayout.Button("Grass", GUILayout.Width(60)))
            {
                ApplyGrassPreset();
            }
            if (GUILayout.Button("Tree", GUILayout.Width(60)))
            {
                ApplyTreePreset();
            }
            if (GUILayout.Button("Rock", GUILayout.Width(60)))
            {
                ApplyRockPreset();
            }
            if (GUILayout.Button("Bush", GUILayout.Width(60)))
            {
                ApplyBushPreset();
            }
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.Space(5);
            
            // Validation warnings
            ValidateData(floraData);
            if (hasWarnings)
            {
                EditorGUILayout.HelpBox(warningMessage, MessageType.Warning);
                EditorGUILayout.Space(5);
            }
            
            // Basic Information Section
            DrawBasicInfoSection(floraData);
            
            // Terrain Conditions Section
            DrawTerrainConditionsSection(floraData);
            
            // Behavior Flags Section
            DrawBehaviorFlagsSection(floraData);
            
            // Type Classification Section
            DrawTypeClassificationSection(floraData);
            
            // Colony Settings Section
            DrawColonySettingsSection(floraData);
            
            // Meshes Section
            DrawMeshesSection(floraData);
            
            // Raw Data Section (for debugging)
            DrawRawDataSection(floraData);
            
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(floraData, "Modify Flora Data");
                EditorUtility.SetDirty(floraData);
                
                // Encode the modified flags back to the asset
                EncodeFlags(floraData);
            }
        }
        
        // SECTION DRAWING METHODS
        
        private void DrawBasicInfoSection(MBFloraData floraData)
        {
            showBasicInfo = EditorGUILayout.BeginFoldoutHeaderGroup(showBasicInfo, "Basic Information");
            
            if (showBasicInfo)
            {
                EditorGUI.indentLevel++;
                
                // Flora ID
                EditorGUILayout.BeginHorizontal();
                floraData.FloraID = EditorGUILayout.TextField(
                    new GUIContent("Flora ID", 
                        "Unique identifier for this flora type. Used in module system and scene definitions."),
                    floraData.FloraID
                );
                EditorGUILayout.EndHorizontal();
                
                EditorGUILayout.Space(5);
                
                // Density slider
                decodedFlags.Density = EditorGUILayout.IntSlider(
                    new GUIContent("Spawn Density", 
                        "Controls how densely this flora spawns (0-65535).\n" +
                        "• Grass: 5000-15000\n" +
                        "• Trees: 100-500\n" +
                        "• Rocks: 50-200\n" +
                        "Higher values = more flora instances"),
                    decodedFlags.Density,
                    0,
                    65535
                );
                
                // Quick Info Display
                EditorGUILayout.Space(5);
                EditorGUILayout.LabelField("Quick Info", EditorStyles.miniBoldLabel);
                
                string terrainInfo = FloraFlagsDecoder.GetTerrainDescription(decodedFlags.Terrain);
                string typeInfo = FloraFlagsDecoder.GetFloraTypeString(decodedFlags.Type);
                
                EditorGUILayout.BeginVertical(infoBoxStyle);
                EditorGUILayout.LabelField($"Type: {typeInfo}", EditorStyles.miniLabel);
                EditorGUILayout.LabelField($"Spawns on: {terrainInfo}", EditorStyles.miniLabel);
                EditorGUILayout.LabelField($"Density: {decodedFlags.Density}", EditorStyles.miniLabel);
                EditorGUILayout.EndVertical();
                
                EditorGUI.indentLevel--;
                EditorGUILayout.Space(5);
            }
            
            EditorGUILayout.EndFoldoutHeaderGroup();
        }
        
        private void DrawTerrainConditionsSection(MBFloraData floraData)
        {
            showTerrainConditions = EditorGUILayout.BeginFoldoutHeaderGroup(showTerrainConditions, "Terrain Conditions");
            
            if (showTerrainConditions)
            {
                EditorGUI.indentLevel++;
                
                EditorGUILayout.HelpBox(
                    "Select which terrain types this flora can spawn on. Flora will only appear in scenes with matching terrain codes.",
                    MessageType.Info
                );
                
                EditorGUILayout.Space(5);
                EditorGUILayout.LabelField("Basic Terrains", EditorStyles.miniBoldLabel);
                
                decodedFlags.Terrain.Plain = EditorGUILayout.Toggle(
                    new GUIContent("Plain", 
                        "Spawns on plain/grassland terrain. Most common terrain type in Calradia."),
                    decodedFlags.Terrain.Plain
                );
                
                decodedFlags.Terrain.Steppe = EditorGUILayout.Toggle(
                    new GUIContent("Steppe", 
                        "Spawns on dry grassland/steppe terrain. Common in Khergit territories."),
                    decodedFlags.Terrain.Steppe
                );
                
                decodedFlags.Terrain.Snow = EditorGUILayout.Toggle(
                    new GUIContent("Snow", 
                        "Spawns on snowy terrain. Found in Nord territories and high elevations."),
                    decodedFlags.Terrain.Snow
                );
                
                decodedFlags.Terrain.Desert = EditorGUILayout.Toggle(
                    new GUIContent("Desert", 
                        "Spawns on desert/sandy terrain. Common in Sarranid territories."),
                    decodedFlags.Terrain.Desert
                );
                
                EditorGUILayout.Space(5);
                EditorGUILayout.LabelField("Forest Terrains", EditorStyles.miniBoldLabel);
                
                decodedFlags.Terrain.PlainForest = EditorGUILayout.Toggle(
                    new GUIContent("Plain Forest", 
                        "Spawns in forests on plain terrain. Enables denser tree placement."),
                    decodedFlags.Terrain.PlainForest
                );
                
                decodedFlags.Terrain.SteppeForest = EditorGUILayout.Toggle(
                    new GUIContent("Steppe Forest", 
                        "Spawns in forests on steppe terrain. Sparse tree coverage typical of steppe woodlands."),
                    decodedFlags.Terrain.SteppeForest
                );
                
                decodedFlags.Terrain.SnowForest = EditorGUILayout.Toggle(
                    new GUIContent("Snow Forest", 
                        "Spawns in forests on snowy terrain. Typically coniferous trees."),
                    decodedFlags.Terrain.SnowForest
                );
                
                decodedFlags.Terrain.DesertForest = EditorGUILayout.Toggle(
                    new GUIContent("Desert Forest", 
                        "Spawns in oases or forested areas in desert terrain."),
                    decodedFlags.Terrain.DesertForest
                );
                
                EditorGUI.indentLevel--;
                EditorGUILayout.Space(5);
            }
            
            EditorGUILayout.EndFoldoutHeaderGroup();
        }
        
        private void DrawBehaviorFlagsSection(MBFloraData floraData)
        {
            showBehaviorFlags = EditorGUILayout.BeginFoldoutHeaderGroup(showBehaviorFlags, "Behavior Flags");
            
            if (showBehaviorFlags)
            {
                EditorGUI.indentLevel++;
                
                EditorGUILayout.HelpBox(
                    "Control how this flora behaves in the game world, including placement, alignment, and special effects.",
                    MessageType.Info
                );
                
                EditorGUILayout.Space(5);
                EditorGUILayout.LabelField("Placement & Alignment", EditorStyles.miniBoldLabel);
                
                decodedFlags.Behavior.AlignWithGround = EditorGUILayout.Toggle(
                    new GUIContent("Align With Ground", 
                        "Flora aligns its orientation to match terrain normal. Recommended for most flora except trees.\n" +
                        "• Trees: Usually disabled for vertical orientation\n" +
                        "• Grass/Rocks: Usually enabled to follow terrain contours"),
                    decodedFlags.Behavior.AlignWithGround
                );
                
                decodedFlags.Behavior.PointUp = EditorGUILayout.Toggle(
                    new GUIContent("Point Up (Auto Quad)", 
                        "Generates automatic quad geometry facing upward. Used for simple grass sprites.\n" +
                        "• Creates a simple flat quad plane\n" +
                        "• Efficient for low-detail ground cover\n" +
                        "• Typically used with grass textures"),
                    decodedFlags.Behavior.PointUp
                );
                
                decodedFlags.Behavior.OnGreenGround = EditorGUILayout.Toggle(
                    new GUIContent("On Green Ground", 
                        "Only spawns where vegetation/green areas exist. Prevents spawning on bare terrain.\n" +
                        "• Useful for grass and shrubs\n" +
                        "• Avoids spawning on rocky or barren areas\n" +
                        "• Works with terrain vegetation map"),
                    decodedFlags.Behavior.OnGreenGround
                );
                
                EditorGUILayout.Space(5);
                EditorGUILayout.LabelField("Spawn Behavior", EditorStyles.miniBoldLabel);
                
                decodedFlags.Behavior.Guarantee = EditorGUILayout.Toggle(
                    new GUIContent("Guarantee Spawn", 
                        "Forces this flora to always spawn when conditions are met. Ignores density randomization.\n" +
                        "• Use sparingly - can cause performance issues\n" +
                        "• Good for critical visual elements\n" +
                        "• Overrides random spawn distribution"),
                    decodedFlags.Behavior.Guarantee
                );
                
                EditorGUILayout.Space(5);
                EditorGUILayout.LabelField("Visual Effects", EditorStyles.miniBoldLabel);
                
                decodedFlags.Behavior.Snowy = EditorGUILayout.Toggle(
                    new GUIContent("Snowy", 
                        "Applies snow shader/material to this flora when in snowy conditions.\n" +
                        "• Automatically adds snow coverage to trees/rocks\n" +
                        "• Works with winter weather system\n" +
                        "• Common for evergreen trees and rocks"),
                    decodedFlags.Behavior.Snowy
                );
                
                EditorGUILayout.Space(5);
                EditorGUILayout.LabelField("Advanced/Legacy", EditorStyles.miniBoldLabel);
                
                decodedFlags.Behavior.RealtimeLighting = EditorGUILayout.Toggle(
                    new GUIContent("Realtime Lighting [DEPRECATED]", 
                        "Legacy flag - no longer functional in Warband. Was used for dynamic lighting in earlier versions."),
                    decodedFlags.Behavior.RealtimeLighting
                );
                
                EditorGUI.BeginDisabledGroup(true);
                
                decodedFlags.Behavior.SpeedTree = EditorGUILayout.Toggle(
                    new GUIContent("SpeedTree [NON-FUNCTIONAL]", 
                        "SpeedTree support flag - not functional in Mount & Blade. Module system includes it but engine doesn't use it."),
                    decodedFlags.Behavior.SpeedTree
                );
                EditorGUI.EndDisabledGroup();
                
                decodedFlags.Behavior.HasColonyProps = EditorGUILayout.Toggle(
                    new GUIContent("Has Colony Props", 
                        "Enables colony system for this flora. Allows multiple props to spawn together as a group.\n" +
                        "• Requires Colony Radius and Threshold settings\n" +
                        "• Used for clustering rocks, shrubs, or tree groves\n" +
                        "• Creates more natural, grouped distributions"),
                    decodedFlags.Behavior.HasColonyProps
                );
                
                EditorGUI.indentLevel--;
                EditorGUILayout.Space(5);
            }
            
            EditorGUILayout.EndFoldoutHeaderGroup();
        }
        
        private void DrawTypeClassificationSection(MBFloraData floraData)
        {
            showTypeFlags = EditorGUILayout.BeginFoldoutHeaderGroup(showTypeFlags, "Type Classification");
            
            if (showTypeFlags)
            {
                EditorGUI.indentLevel++;
                
                EditorGUILayout.HelpBox(
                    "Classify what type of flora this is. These flags affect culling, LOD, and engine behavior. Only one should typically be set.",
                    MessageType.Info
                );
                
                EditorGUILayout.Space(5);
                
                // Color code based on what's selected
                Color originalColor = GUI.backgroundColor;
                
                GUI.backgroundColor = decodedFlags.Type.IsTree ? Color.green : originalColor;
                decodedFlags.Type.IsTree = EditorGUILayout.Toggle(
                    new GUIContent("Tree", 
                        "Large vegetation with vertical orientation.\n" +
                        "• Typically 3-20+ meters tall\n" +
                        "• Uses tree-specific LOD system\n" +
                        "• Usually has collision\n" +
                        "• Never aligns with ground (stays vertical)"),
                    decodedFlags.Type.IsTree
                );
                
                GUI.backgroundColor = decodedFlags.Type.IsGrass ? Color.green : originalColor;
                decodedFlags.Type.IsGrass = EditorGUILayout.Toggle(
                    new GUIContent("Grass", 
                        "Small ground-covering vegetation.\n" +
                        "• Typically < 2 meters tall\n" +
                        "• High density spawning\n" +
                        "• Usually no collision\n" +
                        "• Often uses Point Up flag for quads\n" +
                        "• Aggressive LOD culling for performance"),
                    decodedFlags.Type.IsGrass
                );
                
                GUI.backgroundColor = decodedFlags.Type.IsRock ? Color.green : originalColor;
                decodedFlags.Type.IsRock = EditorGUILayout.Toggle(
                    new GUIContent("Rock", 
                        "Stone formations and boulders.\n" +
                        "• Usually has collision\n" +
                        "• Aligns with ground\n" +
                        "• Medium to low density\n" +
                        "• Can block AI pathfinding"),
                    decodedFlags.Type.IsRock
                );
                
                GUI.backgroundColor = originalColor;
                
                // Warning if multiple or none selected
                int typeCount = 0;
                if (decodedFlags.Type.IsTree) typeCount++;
                if (decodedFlags.Type.IsGrass) typeCount++;
                if (decodedFlags.Type.IsRock) typeCount++;
                
                if (typeCount > 1)
                {
                    EditorGUILayout.HelpBox(
                        "Warning: Multiple type flags selected. Only one type should typically be active.",
                        MessageType.Warning
                    );
                }
                else if (typeCount == 0)
                {
                    EditorGUILayout.HelpBox(
                        "Note: No type flag selected. Flora will use generic behavior without type-specific optimizations.",
                        MessageType.Info
                    );
                }
                
                EditorGUI.indentLevel--;
                EditorGUILayout.Space(5);
            }
            
            EditorGUILayout.EndFoldoutHeaderGroup();
        }
        
        private void DrawColonySettingsSection(MBFloraData floraData)
        {
            showColonySettings = EditorGUILayout.BeginFoldoutHeaderGroup(showColonySettings, "Colony System Settings");
            
            if (showColonySettings)
            {
                EditorGUI.indentLevel++;
                
                EditorGUILayout.HelpBox(
                    "Colony system controls grouped spawning. Requires 'Has Colony Props' behavior flag to be enabled.",
                    MessageType.Info
                );
                
                bool colonyEnabled = decodedFlags.Behavior.HasColonyProps;
                EditorGUI.BeginDisabledGroup(!colonyEnabled);
                
                if (!colonyEnabled)
                {
                    EditorGUILayout.HelpBox(
                        "Colony settings are disabled. Enable 'Has Colony Props' in Behavior Flags to use these settings.",
                        MessageType.Warning
                    );
                }
                
                EditorGUILayout.Space(5);
                
                floraData.ColonyRadius = EditorGUILayout.FloatField(
                    new GUIContent("Colony Radius", 
                        "Maximum distance (in meters) between props in the same colony.\n" +
                        "• Typical values: 5-20 meters\n" +
                        "• Larger values create more spread out colonies\n" +
                        "• Smaller values create tight clusters"),
                    floraData.ColonyRadius
                );
                
                floraData.ColonyThreshold = EditorGUILayout.Slider(
                    new GUIContent("Colony Threshold", 
                        "Probability threshold for spawning additional colony members (0-1).\n" +
                        "• 0.0 = Every valid position spawns a prop (dense)\n" +
                        "• 0.5 = 50% chance per position\n" +
                        "• 1.0 = Minimal spawning (sparse)\n" +
                        "Typical: 0.3-0.7 for natural distribution"),
                    floraData.ColonyThreshold,
                    0f,
                    1f
                );
                
                EditorGUI.EndDisabledGroup();
                
                EditorGUI.indentLevel--;
                EditorGUILayout.Space(5);
            }
            
            EditorGUILayout.EndFoldoutHeaderGroup();
        }
        
        private void DrawMeshesSection(MBFloraData floraData)
        {
            showMeshes = EditorGUILayout.BeginFoldoutHeaderGroup(showMeshes, "Meshes & Models");
            
            if (showMeshes)
            {
                EditorGUI.indentLevel++;
                
                EditorGUILayout.HelpBox(
                    "Define mesh variations for this flora. Multiple meshes are randomly selected at spawn for variety.\n" +
                    "Collision meshes are used for player/AI interaction.",
                    MessageType.Info
                );
                
                EditorGUILayout.Space(5);
                
                // List all meshes
                for (int i = 0; i < floraData.Meshes.Count; i++)
                {
                    EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                    
                    EditorGUILayout.LabelField($"Mesh Variant {i + 1}", EditorStyles.boldLabel);
                    
                    floraData.Meshes[i].Mesh = EditorGUILayout.TextField(
                        new GUIContent("Primary Mesh", 
                            "Main mesh resource name (without .brf extension).\n" +
                            "Example: 'tree_a', 'grass_tall_a'"),
                        floraData.Meshes[i].Mesh
                    );
                    
                    floraData.Meshes[i].MeshCollision = EditorGUILayout.TextField(
                        new GUIContent("Collision Mesh", 
                            "Simplified collision mesh. Leave empty if no collision needed.\n" +
                            "• Grass: Usually empty (no collision)\n" +
                            "• Trees/Rocks: Simplified collision hull"),
                        floraData.Meshes[i].MeshCollision
                    );
                    
                    floraData.Meshes[i].AlternativeMesh = EditorGUILayout.TextField(
                        new GUIContent("Alternative Mesh", 
                            "Optional LOD or seasonal variant.\n" +
                            "Can be used for lower LOD levels or winter versions."),
                        floraData.Meshes[i].AlternativeMesh
                    );
                    
                    floraData.Meshes[i].AlternativeMeshCollision = EditorGUILayout.TextField(
                        new GUIContent("Alternative Collision", 
                            "Collision mesh for alternative mesh variant."),
                        floraData.Meshes[i].AlternativeMeshCollision
                    );
                    
                    EditorGUILayout.Space(5);
                    
                    // Remove button
                    if (GUILayout.Button($"Remove Variant {i + 1}"))
                    {
                        floraData.Meshes.RemoveAt(i);
                        break;
                    }
                    
                    EditorGUILayout.EndVertical();
                    EditorGUILayout.Space(5);
                }
                
                // Add new mesh button
                if (GUILayout.Button("Add Mesh Variant"))
                {
                    floraData.Meshes.Add(new FloraMesh());
                }
                
                EditorGUI.indentLevel--;
                EditorGUILayout.Space(5);
            }
            
            EditorGUILayout.EndFoldoutHeaderGroup();
        }
        
        private void DrawRawDataSection(MBFloraData floraData)
        {
            showRawData = EditorGUILayout.BeginFoldoutHeaderGroup(showRawData, "Raw Data (Advanced)");
            
            if (showRawData)
            {
                EditorGUI.indentLevel++;
                
                EditorGUILayout.HelpBox(
                    "Technical data for debugging and module system compatibility. " +
                    "Changes here will override visual flag settings above.",
                    MessageType.Info
                );
                
                EditorGUILayout.Space(5);
                
                // Raw flags as read-only (will be encoded from UI)
                EditorGUI.BeginDisabledGroup(true);
                EditorGUILayout.TextField(
                    new GUIContent("Encoded Flags (Hex)", 
                        "Raw hexadecimal flags value. Auto-generated from settings above."),
                    floraData.Flags
                );
                EditorGUI.EndDisabledGroup();
                
                // Density from flags
                EditorGUILayout.LabelField(
                    new GUIContent("Density (from flags)", 
                        "Spawn density extracted from upper bits of flags (0-65535).\n" +
                        "Higher values = denser spawning."),
                    decodedFlags.Density.ToString()
                );
                
                // Show decoded breakdown
                EditorGUILayout.Space(5);
                EditorGUILayout.LabelField("Flag Breakdown", EditorStyles.miniBoldLabel);
                EditorGUILayout.TextArea(GetFlagBreakdown(), GUILayout.Height(100));
                
                EditorGUI.indentLevel--;
                EditorGUILayout.Space(5);
            }
            
            EditorGUILayout.EndFoldoutHeaderGroup();
        }
        
        // HELPER METHODS
        
        private void UpdateDecodedFlags()
        {
            MBFloraData floraData = (MBFloraData)target;
            
            if (!string.IsNullOrEmpty(floraData.Flags))
            {
                decodedFlags = FloraFlagsDecoder.DecodeAll(floraData.Flags);
            }
            else
            {
                // Initialize with defaults
                decodedFlags = new FloraDecodedFlags();
            }
        }
        
        private void EncodeFlags(MBFloraData floraData)
        {
            BigInteger flags = 0;
            
            // Terrain conditions
            if (decodedFlags.Terrain.Plain) flags |= 0x00000004;
            if (decodedFlags.Terrain.Steppe) flags |= 0x00000008;
            if (decodedFlags.Terrain.Snow) flags |= 0x00000010;
            if (decodedFlags.Terrain.Desert) flags |= 0x00000020;
            if (decodedFlags.Terrain.PlainForest) flags |= 0x00000400;
            if (decodedFlags.Terrain.SteppeForest) flags |= 0x00000800;
            if (decodedFlags.Terrain.SnowForest) flags |= 0x00001000;
            if (decodedFlags.Terrain.DesertForest) flags |= 0x00002000;
            
            // Behavior flags
            if (decodedFlags.Behavior.RealtimeLighting) flags |= 0x00010000;
            if (decodedFlags.Behavior.PointUp) flags |= 0x00020000;
            if (decodedFlags.Behavior.AlignWithGround) flags |= 0x00040000;
            if (decodedFlags.Behavior.OnGreenGround) flags |= 0x00100000;
            if (decodedFlags.Behavior.Guarantee) flags |= 0x01000000;
            if (decodedFlags.Behavior.SpeedTree) flags |= 0x02000000;
            if (decodedFlags.Behavior.HasColonyProps) flags |= 0x04000000;
            if (decodedFlags.Behavior.Snowy) flags |= 0x00800000;
            
            // Type flags
            if (decodedFlags.Type.IsGrass) flags |= 0x00080000;
            if (decodedFlags.Type.IsRock) flags |= 0x00200000;
            if (decodedFlags.Type.IsTree) flags |= 0x00400000;
            
            // Density in upper bits
            BigInteger density = decodedFlags.Density & 0xFFFF;
            flags |= (density << 32);
            
            floraData.Flags = flags.ToString();
            
            // Also update the separate Density field for consistency
            floraData.Density = decodedFlags.Density;
        }
        
        private void ValidateData(MBFloraData floraData)
        {
            hasWarnings = false;
            warningMessage = "";
            
            // Check for missing ID
            if (string.IsNullOrEmpty(floraData.FloraID))
            {
                hasWarnings = true;
                warningMessage = "Flora ID is required for module system export.";
                return;
            }
            
            // Check for no terrain conditions
            bool hasTerrainCondition = 
                decodedFlags.Terrain.Plain || decodedFlags.Terrain.Steppe ||
                decodedFlags.Terrain.Snow || decodedFlags.Terrain.Desert ||
                decodedFlags.Terrain.PlainForest || decodedFlags.Terrain.SteppeForest ||
                decodedFlags.Terrain.SnowForest || decodedFlags.Terrain.DesertForest;
            
            if (!hasTerrainCondition)
            {
                hasWarnings = true;
                warningMessage = "No terrain conditions selected. This flora won't spawn anywhere!";
                return;
            }
            
            // Check for no meshes
            if (floraData.Meshes.Count == 0)
            {
                hasWarnings = true;
                warningMessage = "No mesh variants defined. At least one mesh is required.";
                return;
            }
            
            // Check mesh names
            foreach (var mesh in floraData.Meshes)
            {
                if (string.IsNullOrEmpty(mesh.Mesh))
                {
                    hasWarnings = true;
                    warningMessage = "One or more mesh variants are missing a primary mesh name.";
                    return;
                }
            }
            
            // Check colony settings
            if (decodedFlags.Behavior.HasColonyProps)
            {
                if (floraData.ColonyRadius <= 0)
                {
                    hasWarnings = true;
                    warningMessage = "Colony Radius must be greater than 0 when Colony Props is enabled.";
                    return;
                }
            }
            
            // Check for Point Up with Tree (unusual combination)
            if (decodedFlags.Behavior.PointUp && decodedFlags.Type.IsTree)
            {
                hasWarnings = true;
                warningMessage = "Point Up flag is unusual for trees. This creates flat quad geometry instead of 3D models.";
                return;
            }
        }
        
        private string GetFlagBreakdown()
        {
            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            
            sb.AppendLine("TERRAIN CONDITIONS:");
            if (decodedFlags.Terrain.Plain) sb.AppendLine("  • Plain (0x00000004)");
            if (decodedFlags.Terrain.Steppe) sb.AppendLine("  • Steppe (0x00000008)");
            if (decodedFlags.Terrain.Snow) sb.AppendLine("  • Snow (0x00000010)");
            if (decodedFlags.Terrain.Desert) sb.AppendLine("  • Desert (0x00000020)");
            if (decodedFlags.Terrain.PlainForest) sb.AppendLine("  • Plain Forest (0x00000400)");
            if (decodedFlags.Terrain.SteppeForest) sb.AppendLine("  • Steppe Forest (0x00000800)");
            if (decodedFlags.Terrain.SnowForest) sb.AppendLine("  • Snow Forest (0x00001000)");
            if (decodedFlags.Terrain.DesertForest) sb.AppendLine("  • Desert Forest (0x00002000)");
            
            sb.AppendLine("\nBEHAVIOR FLAGS:");
            if (decodedFlags.Behavior.AlignWithGround) sb.AppendLine("  • Align With Ground (0x00040000)");
            if (decodedFlags.Behavior.PointUp) sb.AppendLine("  • Point Up (0x00020000)");
            if (decodedFlags.Behavior.OnGreenGround) sb.AppendLine("  • On Green Ground (0x00100000)");
            if (decodedFlags.Behavior.Guarantee) sb.AppendLine("  • Guarantee (0x01000000)");
            if (decodedFlags.Behavior.Snowy) sb.AppendLine("  • Snowy (0x00800000)");
            if (decodedFlags.Behavior.HasColonyProps) sb.AppendLine("  • Has Colony Props (0x04000000)");
            
            sb.AppendLine("\nTYPE FLAGS:");
            if (decodedFlags.Type.IsGrass) sb.AppendLine("  • Grass (0x00080000)");
            if (decodedFlags.Type.IsTree) sb.AppendLine("  • Tree (0x00400000)");
            if (decodedFlags.Type.IsRock) sb.AppendLine("  • Rock (0x00200000)");
            
            sb.AppendLine($"\nDENSITY: {decodedFlags.Density} (upper 16 bits)");
            
            return sb.ToString();
        }
        
        // PRESET CONFIGURATIONS
        
        private void ApplyGrassPreset()
        {
            MBFloraData floraData = (MBFloraData)target;
            
            // Reset all flags
            decodedFlags = new FloraDecodedFlags();
            
            // Common grass configuration
            decodedFlags.Terrain.Plain = true;
            decodedFlags.Terrain.Steppe = true;
            
            decodedFlags.Behavior.AlignWithGround = true;
            decodedFlags.Behavior.PointUp = true;
            decodedFlags.Behavior.OnGreenGround = true;
            
            decodedFlags.Type.IsGrass = true;
            decodedFlags.Density = 8000;
            
            floraData.ColonyRadius = 0;
            floraData.ColonyThreshold = 0;
            
            EncodeFlags(floraData);
            EditorUtility.SetDirty(floraData);
        }
        
        private void ApplyTreePreset()
        {
            MBFloraData floraData = (MBFloraData)target;
            
            // Reset all flags
            decodedFlags = new FloraDecodedFlags();
            
            // Common tree configuration
            decodedFlags.Terrain.PlainForest = true;
            decodedFlags.Terrain.SteppeForest = true;
            
            decodedFlags.Behavior.AlignWithGround = false;
            
            decodedFlags.Type.IsTree = true;
            decodedFlags.Density = 250;
            
            floraData.ColonyRadius = 0;
            floraData.ColonyThreshold = 0;
            
            EncodeFlags(floraData);
            EditorUtility.SetDirty(floraData);
        }
        
        private void ApplyRockPreset()
        {
            MBFloraData floraData = (MBFloraData)target;
            
            // Reset all flags
            decodedFlags = new FloraDecodedFlags();
            
            // Common rock configuration with colony
            decodedFlags.Terrain.Plain = true;
            decodedFlags.Terrain.Steppe = true;
            decodedFlags.Terrain.Snow = true;
            decodedFlags.Terrain.Desert = true;
            
            decodedFlags.Behavior.AlignWithGround = true;
            decodedFlags.Behavior.HasColonyProps = true;
            
            decodedFlags.Type.IsRock = true;
            decodedFlags.Density = 150;
            
            floraData.ColonyRadius = 10f;
            floraData.ColonyThreshold = 0.5f;
            
            EncodeFlags(floraData);
            EditorUtility.SetDirty(floraData);
        }
        
        private void ApplyBushPreset()
        {
            MBFloraData floraData = (MBFloraData)target;
            
            // Reset all flags
            decodedFlags = new FloraDecodedFlags();
            
            // Bush/shrub configuration
            decodedFlags.Terrain.Plain = true;
            decodedFlags.Terrain.Steppe = true;
            
            decodedFlags.Behavior.AlignWithGround = true;
            decodedFlags.Behavior.OnGreenGround = true;
            
            decodedFlags.Type.IsGrass = true; // Bushes use grass type in M&B
            decodedFlags.Density = 2000;
            
            floraData.ColonyRadius = 0;
            floraData.ColonyThreshold = 0;
            
            EncodeFlags(floraData);
            EditorUtility.SetDirty(floraData);
        }
    }
}
