using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using UnityEngine;
using UnityEditor;
using MountAndBlade.Data;

namespace MountAndBlade.Editor
{
    [CustomEditor(typeof(MBItemData))]
    public class MBItemDataEditor : UnityEditor.Editor
    {
        private MBItemData itemData;
        
        // Foldout states
        private bool showBasicInfo = true;
        private bool showMeshes = true;
        private bool showFlags = true;
        private bool showCapabilities = true;
        private bool showStats = true;
        private bool showModifiers = true;
        private bool showTriggers = false;
        private bool showFactions = true;
        
        // Decoded data display states
        private bool showDecodedFlags = false;
        private bool showDecodedCapabilities = false;
        private bool showDecodedModifiers = false;
        
        // Decoded data cache
        private string decodedFlagsText = "";
        private string decodedCapabilitiesText = "";
        private string decodedModifiersText = "";
        private string itemTypeInfo = "";
        
        // Editor GUI styles
        private GUIStyle headerStyle;
        private GUIStyle boxStyle;
        private GUIStyle decodedStyle;
        private GUIStyle helpBoxStyle;
        private GUIStyle warningStyle;
        
        private void OnEnable()
        {
            itemData = (MBItemData)target;
        }
        
        private void InitStyles()
        {
            if (headerStyle == null)
            {
                headerStyle = new GUIStyle(EditorStyles.boldLabel)
                {
                    fontSize = 13,
                    margin = new RectOffset(0, 0, 10, 5)
                };
            }
            
            if (boxStyle == null)
            {
                boxStyle = new GUIStyle(EditorStyles.helpBox)
                {
                    padding = new RectOffset(10, 10, 10, 10)
                };
            }
            
            if (decodedStyle == null)
            {
                decodedStyle = new GUIStyle(EditorStyles.textArea)
                {
                    wordWrap = true,
                    richText = true
                };
            }

            if (helpBoxStyle == null)
            {
                helpBoxStyle = new GUIStyle(EditorStyles.helpBox)
                {
                    richText = true,
                    fontSize = 11
                };
            }

            if (warningStyle == null)
            {
                warningStyle = new GUIStyle(EditorStyles.helpBox)
                {
                    richText = true,
                    fontSize = 11,
                    normal = { textColor = new Color(0.8f, 0.5f, 0.1f) }
                };
            }
        }
        
        public override void OnInspectorGUI()
        {
            InitStyles();
            serializedObject.Update();
            
            // Title Bar
            EditorGUILayout.Space(5);
            EditorGUILayout.LabelField("Mount & Blade Item Editor", new GUIStyle(EditorStyles.boldLabel) 
            { 
                fontSize = 16, 
                alignment = TextAnchor.MiddleCenter 
            });
            EditorGUILayout.Space(10);
            
            // Quick Info Bar
            DrawQuickInfoBar();
            
            EditorGUILayout.Space(5);
            
            // BASIC INFORMATION
            showBasicInfo = EditorGUILayout.BeginFoldoutHeaderGroup(showBasicInfo, "📋 Basic Information");
            if (showBasicInfo)
            {
                EditorGUILayout.BeginVertical(boxStyle);
                
                EditorGUILayout.LabelField("Item Identity", EditorStyles.boldLabel);
                itemData.ItemID = EditorGUILayout.TextField(
                    new GUIContent("Item ID", "Unique identifier. Prefix 'itm_' is added automatically."), 
                    itemData.ItemID);
                
                itemData.ItemName = EditorGUILayout.TextField(
                    new GUIContent("Display Name", "Name shown in inventory and merchant windows."), 
                    itemData.ItemName);
                
                EditorGUILayout.Space(5);
                EditorGUILayout.LabelField("Economics", EditorStyles.boldLabel);
                itemData.Price = EditorGUILayout.IntField(
                    new GUIContent("Base Price", "Base value in denars. Modifiers affect final price."), 
                    itemData.Price);
                
                EditorGUILayout.Space(3);
                EditorGUILayout.HelpBox(
                    "Item ID and display name.\n" +
                    "Base item value (price).",
                    MessageType.None);
                
                EditorGUILayout.EndVertical();
            }
            EditorGUILayout.EndFoldoutHeaderGroup();
            
            EditorGUILayout.Space(5);
            
            // MESHES (Tuple Field 3)
            showMeshes = EditorGUILayout.BeginFoldoutHeaderGroup(showMeshes, "🎨 Mesh Configuration");
            if (showMeshes)
            {
                EditorGUILayout.BeginVertical(boxStyle);
                
                DrawMeshList();
                
                EditorGUILayout.Space(5);
                
                EditorGUILayout.EndVertical();
            }
            EditorGUILayout.EndFoldoutHeaderGroup();
            
            EditorGUILayout.Space(5);
            
            // FLAGS (Tuple Field 4)
            showFlags = EditorGUILayout.BeginFoldoutHeaderGroup(showFlags, "🚩 Property Flags");
            if (showFlags)
            {
                EditorGUILayout.BeginVertical(boxStyle);
                
                EditorGUILayout.Space(5);
                
                EditorGUILayout.LabelField("Raw Flags Value:", EditorStyles.boldLabel);
                string newFlags = EditorGUILayout.TextField(itemData.Flags);
                if (newFlags != itemData.Flags)
                {
                    itemData.Flags = newFlags;
                    decodedFlagsText = "";
                    itemTypeInfo = "";
                }
                    
                if (GUILayout.Button("⚙️ Flags Editor", GUILayout.Height(25)))
                {
                    ItemFlagPropertiesWindow.ShowWindow(itemData);
                }
                
                EditorGUILayout.EndVertical();
                EditorGUILayout.Space(5);
            }
        
            
            EditorGUILayout.EndFoldoutHeaderGroup();
            
            EditorGUILayout.Space(5);
            
            // CAPABILITIES (Tuple Field 5)
            showCapabilities = EditorGUILayout.BeginFoldoutHeaderGroup(showCapabilities, "⚔️ Item Capabilities");
            if (showCapabilities)
            {
                EditorGUILayout.BeginVertical(boxStyle);
                
                EditorGUILayout.Space(5);
                
                EditorGUILayout.LabelField("Raw Capabilities Value:", EditorStyles.boldLabel);
                string newCaps = EditorGUILayout.TextField(itemData.Capabilities);
                if (newCaps != itemData.Capabilities)
                {
                    itemData.Capabilities = newCaps;
                    decodedCapabilitiesText = "";
                }
                  
                if (GUILayout.Button("⚙️ Capabilities Editor", GUILayout.Height(25)))
                {
                    ItemCapabilitiesWindow.ShowWindow(itemData);
                }
                
                EditorGUILayout.EndVertical();
                EditorGUILayout.Space(5);
              
            }
            EditorGUILayout.EndFoldoutHeaderGroup();
            
            EditorGUILayout.Space(5);
            
            // STATS (Tuple Field 7)
            showStats = EditorGUILayout.BeginFoldoutHeaderGroup(showStats, "📊 Item Statistics");
            if (showStats)
            {
                EditorGUILayout.BeginVertical(boxStyle);
                
                EditorGUILayout.Space(5);
                
                EditorGUILayout.LabelField("Raw Stats Value:", EditorStyles.boldLabel);
                itemData.Stats = EditorGUILayout.TextField(itemData.Stats);
                
                if (GUILayout.Button("⚙️ Stats Editor", GUILayout.Height(25)))
                {
                    ItemStatsWindow.ShowWindow(itemData);
                }
                
                EditorGUILayout.EndVertical();
                EditorGUILayout.Space(5);
             
            }
            EditorGUILayout.EndFoldoutHeaderGroup();
            
            EditorGUILayout.Space(5);
            
            // MODIFIER BITS (Tuple Field 8)
            showModifiers = EditorGUILayout.BeginFoldoutHeaderGroup(showModifiers, "✨ Modifier Bits");
            if (showModifiers)
            {
                EditorGUILayout.BeginVertical(boxStyle);
                
                EditorGUILayout.LabelField("Raw Modifier Bits:", EditorStyles.boldLabel);
                string newMods = EditorGUILayout.TextField(itemData.ModifierBits);
                if (newMods != itemData.ModifierBits)
                {
                    itemData.ModifierBits = newMods;
                    decodedModifiersText = "";
                }
                
                if (GUILayout.Button("⚙️ Modifiers Editor", GUILayout.Height(25)))
                {
                    ItemModifierBitsWindow.ShowWindow(itemData);
                }
                
                EditorGUILayout.Space(5);
                EditorGUILayout.EndVertical();
            }
            EditorGUILayout.EndFoldoutHeaderGroup();
            
            EditorGUILayout.Space(5);
            
            // FACTIONS (Tuple Field 10)
            showFactions = EditorGUILayout.BeginFoldoutHeaderGroup(showFactions, "🏰 Associated Factions");
            if (showFactions)
            {
                EditorGUILayout.BeginVertical(boxStyle);
                
                DrawFactionList();
                
                EditorGUILayout.EndVertical();
            }
            EditorGUILayout.EndFoldoutHeaderGroup();
            
            EditorGUILayout.Space(5);
            
            // TRIGGERS (Tuple Field 9 - Advanced)
            showTriggers = EditorGUILayout.BeginFoldoutHeaderGroup(showTriggers, "⚡ Trigger Code (Advanced)");
            if (showTriggers)
            {
                EditorGUILayout.BeginVertical(boxStyle);
                
                EditorGUILayout.Space(5);
                EditorGUILayout.LabelField("Python Trigger Code:", EditorStyles.boldLabel);
                itemData.TriggersCode = EditorGUILayout.TextArea(
                    itemData.TriggersCode, 
                    GUILayout.MinHeight(100));
                
                EditorGUILayout.EndVertical();
            }
            EditorGUILayout.EndFoldoutHeaderGroup();
            
            EditorGUILayout.Space(10);
            
            serializedObject.ApplyModifiedProperties();
            
            if (GUI.changed)
            {
                EditorUtility.SetDirty(itemData);
            }
        }
        
        // QUICK INFO BAR
        private void DrawQuickInfoBar()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.BeginHorizontal();
            
            GUILayout.Label($"<b>ID:</b> {itemData.ItemID}", helpBoxStyle, GUILayout.Width(200));
            GUILayout.FlexibleSpace();
            GUILayout.Label($"<b>Price:</b> {itemData.Price}⚜", helpBoxStyle, GUILayout.Width(100));
            GUILayout.Label($"<b>Meshes:</b> {itemData.Meshes.Count}", helpBoxStyle, GUILayout.Width(100));
            
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.EndVertical();
        }
        
// MESH LIST DRAWER 
private void DrawMeshList()
{
    EditorGUILayout.LabelField($"Meshes ({itemData.Meshes.Count}):", EditorStyles.boldLabel);
    
    // Get allowed modifiers from ModifierBits field
    var allowedModifiers = GetAllowedModifiers();
    
    EditorGUILayout.HelpBox(
        "• Mesh 0 is always the default base mesh (locked to Default/0)\n" +
        "• Additional meshes can combine:\n" +
        "  - Usage Type (Default/Inventory/Carry/Flying)\n" +
        "  - Item Modifier (rusty, balanced, etc.) from 'Modifier Bits'\n" +
        "• Example: Carry + Rusty = rusty scabbard variant\n" +
        $"• Allowed modifiers: {(allowedModifiers.Count > 0 ? string.Join(", ", allowedModifiers.Select(m => m.Key.Replace("imodbit_", ""))) : "None - configure in Modifier Bits")}",
        MessageType.Info);
    
    // Validate mesh list
    ValidateMeshList();
    
    for (int i = 0; i < itemData.Meshes.Count; i++)
    {
        EditorGUILayout.BeginVertical(GUI.skin.box);
        EditorGUILayout.BeginHorizontal();
        
        // Mesh Configuration
        EditorGUILayout.BeginVertical();
        
        bool isBaseMesh = (i == 0);
        string meshLabel = isBaseMesh ? "🎨 Base Mesh (Default - Locked)" : $"🎨 Mesh Variant {i}";
        EditorGUILayout.LabelField(meshLabel, EditorStyles.boldLabel);
        
        // Mesh Name (always editable)
        itemData.Meshes[i].MeshName = EditorGUILayout.TextField(
            new GUIContent("Mesh Name", "Mesh resource name from OpenBRF"),
            itemData.Meshes[i].MeshName);
        
        EditorGUILayout.Space(3);
        
        // Usage Type (ixmesh) - LOCKED for base mesh
        EditorGUI.BeginDisabledGroup(isBaseMesh);
        
        if (isBaseMesh)
        {
            // Force base mesh to Default
            itemData.Meshes[i].UsageType = MBItemData.ItemMesh.MeshUsageType.Default;
        }
        
        var newUsageType = (MBItemData.ItemMesh.MeshUsageType)EditorGUILayout.EnumPopup(
            new GUIContent("Usage Type", GetMeshUsageTooltip(itemData.Meshes[i].UsageType)),
            itemData.Meshes[i].UsageType);
        
        // Check for duplicates when changing usage type
        if (!isBaseMesh && newUsageType != itemData.Meshes[i].UsageType)
        {
            if (!IsDuplicateMeshType(i, newUsageType, itemData.Meshes[i].ItemModifier))
            {
                itemData.Meshes[i].UsageType = newUsageType;
            }
            else
            {
                EditorUtility.DisplayDialog("Duplicate Mesh Type",
                    $"A mesh with {newUsageType} + modifier '{itemData.Meshes[i].ItemModifier}' already exists!",
                    "OK");
            }
        }
        
        EditorGUI.EndDisabledGroup();
        
        EditorGUILayout.Space(3);
        
        // Item Modifier - LOCKED ONLY for base mesh
        // All other mesh types (Default/Carry/Inventory/Flying) CAN have modifiers
        EditorGUI.BeginDisabledGroup(isBaseMesh);
        
        if (isBaseMesh)
        {
            // Force base mesh modifier to 0
            itemData.Meshes[i].ItemModifier = "0";
        }
        
        EditorGUILayout.BeginHorizontal();
        
        string modifierTooltip;
        if (isBaseMesh)
            modifierTooltip = "Base mesh must use default (0) modifier - locked";
        else if (allowedModifiers.Count == 0)
            modifierTooltip = "No modifiers configured - set up 'Modifier Bits' section first";
        else
            modifierTooltip = $"Modifier for this {itemData.Meshes[i].UsageType} mesh variant (e.g., rusty carry mesh)";
        
        itemData.Meshes[i].ItemModifier = EditorGUILayout.TextField(
            new GUIContent("Item Modifier", modifierTooltip),
            itemData.Meshes[i].ItemModifier);
        
        // Show picker button for all non-base meshes
        if (!isBaseMesh && GUILayout.Button("📋", GUILayout.Width(25)))
        {
            ShowModifierPicker(i, allowedModifiers);
        }
        
        EditorGUILayout.EndHorizontal();
        
        EditorGUI.EndDisabledGroup();
        
        // Validation Warnings
        if (isBaseMesh)
        {
            if (itemData.Meshes[i].UsageType != MBItemData.ItemMesh.MeshUsageType.Default)
            {
                EditorGUILayout.HelpBox("⚠ Base mesh forced to Default type", MessageType.Warning);
            }
            if (itemData.Meshes[i].ItemModifier != "0")
            {
                EditorGUILayout.HelpBox("⚠ Base mesh forced to modifier 0 (plain)", MessageType.Warning);
            }
        }
        else // Non-base mesh
        {
            // Validate modifier is allowed (if not 0)
            if (!string.IsNullOrEmpty(itemData.Meshes[i].ItemModifier) && 
                itemData.Meshes[i].ItemModifier != "0")
            {
                if (!IsModifierAllowed(itemData.Meshes[i].ItemModifier, allowedModifiers))
                {
                    EditorGUILayout.HelpBox(
                        $"⚠ Modifier '{itemData.Meshes[i].ItemModifier}' is not in allowed modifiers list!\n" +
                        "Configure allowed modifiers in 'Modifier Bits' section.",
                        MessageType.Error);
                }
                else
                {
                    // Show friendly name
                    string usageDesc = itemData.Meshes[i].UsageType == MBItemData.ItemMesh.MeshUsageType.Default 
                        ? "" : $" ({itemData.Meshes[i].UsageType})";
                    EditorGUILayout.LabelField(
                        $"→ {GetModifierDisplayName(itemData.Meshes[i].ItemModifier)}{usageDesc}", 
                        EditorStyles.miniLabel);
                }
            }
            else if (itemData.Meshes[i].ItemModifier == "0" && 
                     itemData.Meshes[i].UsageType != MBItemData.ItemMesh.MeshUsageType.Default)
            {
                // Show info for special mesh types without modifiers
                EditorGUILayout.LabelField(
                    $"→ Plain {itemData.Meshes[i].UsageType} mesh (no modifier)", 
                    EditorStyles.miniLabel);
            }
        }
        
        // Check for duplicates
        if (IsDuplicateMeshType(i, itemData.Meshes[i].UsageType, itemData.Meshes[i].ItemModifier))
        {
            EditorGUILayout.HelpBox(
                "⚠ DUPLICATE: Another mesh already uses this combination of Usage Type + Modifier!",
                MessageType.Error);
        }
        
        EditorGUILayout.EndVertical();
        
        // Remove Button (disabled for base mesh)
        EditorGUI.BeginDisabledGroup(isBaseMesh);
        if (GUILayout.Button("✖", GUILayout.Width(30), GUILayout.Height(EditorGUIUtility.singleLineHeight * 6)))
        {
            itemData.Meshes.RemoveAt(i);
            break;
        }
        EditorGUI.EndDisabledGroup();
        
        EditorGUILayout.EndHorizontal();
        EditorGUILayout.EndVertical();
        EditorGUILayout.Space(3);
    }
    
    // Add Mesh Button
    if (GUILayout.Button("➕ Add Mesh Variant"))
    {
        itemData.Meshes.Add(new MBItemData.ItemMesh());
    }
    
    // Auto-Generate Buttons (updated)
    if (allowedModifiers.Count > 0)
    {
        EditorGUILayout.Space(5);
        EditorGUILayout.LabelField("Auto-Generate Variants:", EditorStyles.boldLabel);
        
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("🔧 Default Variants", GUILayout.Height(30)))
        {
            AutoGenerateMeshVariants(MBItemData.ItemMesh.MeshUsageType.Default, allowedModifiers);
        }
        if (GUILayout.Button("🔧 Carry Variants", GUILayout.Height(30)))
        {
            AutoGenerateMeshVariants(MBItemData.ItemMesh.MeshUsageType.Carry, allowedModifiers);
        }
        EditorGUILayout.EndHorizontal();
        
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("🔧 Inventory Variants", GUILayout.Height(30)))
        {
            AutoGenerateMeshVariants(MBItemData.ItemMesh.MeshUsageType.Inventory, allowedModifiers);
        }
        if (GUILayout.Button("🔧 Flying Ammo Variants", GUILayout.Height(30)))
        {
            AutoGenerateMeshVariants(MBItemData.ItemMesh.MeshUsageType.FlyingAmmo, allowedModifiers);
        }
        EditorGUILayout.EndHorizontal();
        
        EditorGUILayout.Space(3);
        EditorGUILayout.HelpBox(
            "💡 Tip: These buttons create mesh entries for all allowed modifiers.\n" +
            "You still need to set the mesh names manually.",
            MessageType.Info);
    }
}
// HELPER: Get allowed modifiers from ModifierBits field
private Dictionary<string, BigInteger> GetAllowedModifiers()
{
    var result = new Dictionary<string, BigInteger>();
    
    if (string.IsNullOrEmpty(itemData.ModifierBits) || itemData.ModifierBits == "0")
        return result;
    
    try
    {
        BigInteger modifierBits = ParseBigInteger(itemData.ModifierBits);
        var allModifiers = ItemModifierBitsDecoder.GetAllModifiers();
        
        foreach (var mod in allModifiers)
        {
            if (mod.Value == BigInteger.Zero) continue;
            
            // Check if this modifier bit is set
            if ((modifierBits & mod.Value) != BigInteger.Zero)
            {
                result.Add(mod.Key, mod.Value);
            }
        }
    }
    catch
    {
        // Invalid ModifierBits value
    }
    
    return result;
}

// HELPER: Check if modifier is in allowed list
private bool IsModifierAllowed(string modifier, Dictionary<string, BigInteger> allowedModifiers)
{
    if (modifier == "0") return true; // Plain is always allowed
    
    // Check by name
    if (allowedModifiers.ContainsKey(modifier))
        return true;
    
    // Check by numeric value
    if (BigInteger.TryParse(modifier, out BigInteger modValue))
    {
        return allowedModifiers.Values.Any(v => v == modValue);
    }
    
    return false;
}

// HELPER: Check for duplicate mesh type + modifier combination
private bool IsDuplicateMeshType(int currentIndex, MBItemData.ItemMesh.MeshUsageType usageType, string modifier)
{
    for (int i = 0; i < itemData.Meshes.Count; i++)
    {
        if (i == currentIndex) continue; // Skip self
        
        if (itemData.Meshes[i].UsageType == usageType &&
            itemData.Meshes[i].ItemModifier == modifier)
        {
            return true; // Duplicate found
        }
    }
    
    return false;
}

// HELPER: Validate entire mesh list
private void ValidateMeshList()
{
    // Ensure at least one mesh exists
    if (itemData.Meshes.Count == 0)
    {
        itemData.Meshes.Add(new MBItemData.ItemMesh
        {
            MeshName = "",
            UsageType = MBItemData.ItemMesh.MeshUsageType.Default,
            ItemModifier = "0"
        });
    }
    
    // Ensure base mesh (index 0) is correct
    if (itemData.Meshes[0].UsageType != MBItemData.ItemMesh.MeshUsageType.Default)
    {
        itemData.Meshes[0].UsageType = MBItemData.ItemMesh.MeshUsageType.Default;
    }
    if (itemData.Meshes[0].ItemModifier != "0")
    {
        itemData.Meshes[0].ItemModifier = "0";
    }
}

// HELPER: Auto-generate mesh variants for allowed modifiers
private void AutoGenerateMeshVariants(MBItemData.ItemMesh.MeshUsageType usageType, 
                                     Dictionary<string, BigInteger> allowedModifiers)
{
    if (allowedModifiers.Count == 0)
    {
        EditorUtility.DisplayDialog("No Modifiers", 
            "No modifiers are configured in 'Modifier Bits' section.", "OK");
        return;
    }
    
    int added = 0;
    
    foreach (var modifier in allowedModifiers)
    {
        // Skip if this combination already exists
        if (!IsDuplicateMeshType(-1, usageType, modifier.Key))
        {
            itemData.Meshes.Add(new MBItemData.ItemMesh
            {
                MeshName = "", // User fills this in
                UsageType = usageType,
                ItemModifier = modifier.Key
            });
            added++;
        }
    }
    
    if (added > 0)
    {
        EditorUtility.DisplayDialog("Variants Generated", 
            $"Added {added} mesh variant(s) for {usageType} type.\n\n" +
            "Please set mesh names for each variant.", "OK");
    }
    else
    {
        EditorUtility.DisplayDialog("No Variants Added", 
            "All modifier combinations for this usage type already exist.", "OK");
    }
}

// HELPER: Get modifier display name
private string GetModifierDisplayName(string modifierValue)
{
    // Try to parse as constant name first
    if (modifierValue.StartsWith("imodbit_"))
        return modifierValue.Replace("imodbit_", "").Replace("_", " ").ToUpperInvariant();
    
    // Try to decode numeric value
    if (BigInteger.TryParse(modifierValue, out BigInteger numValue))
    {
        var allMods = ItemModifierBitsDecoder.GetAllModifiers();
        var match = allMods.FirstOrDefault(x => x.Value == numValue);
        if (!string.IsNullOrEmpty(match.Key))
            return match.Key.Replace("imodbit_", "").Replace("_", " ").ToUpperInvariant();
    }
    
    return modifierValue;
}

// HELPER: Get mesh usage tooltip
private string GetMeshUsageTooltip(MBItemData.ItemMesh.MeshUsageType type)
{
    switch (type)
    {
        case MBItemData.ItemMesh.MeshUsageType.Default:
            return "Default mesh shown in world/combat";
        case MBItemData.ItemMesh.MeshUsageType.Inventory:
            return "Mesh shown in inventory screen (ixmesh_inventory)";
        case MBItemData.ItemMesh.MeshUsageType.Carry:
            return "Mesh shown when carried/sheathed (ixmesh_carry) - scabbards, quivers";
        case MBItemData.ItemMesh.MeshUsageType.FlyingAmmo:
            return "Mesh for projectiles in flight (ixmesh_flying_ammo)";
        default:
            return "";
    }
}

// HELPER: Show modifier picker (only allowed modifiers)
private void ShowModifierPicker(int meshIndex, Dictionary<string, BigInteger> allowedModifiers)
{
    GenericMenu menu = new GenericMenu();
    
    menu.AddItem(new GUIContent("None (0)"), false, () => {
        itemData.Meshes[meshIndex].ItemModifier = "0";
    });
    
    if (allowedModifiers.Count == 0)
    {
        menu.AddDisabledItem(new GUIContent("(No modifiers allowed - configure Modifier Bits first)"));
        menu.ShowAsContext();
        return;
    }
    
    menu.AddSeparator("");
    
    foreach (var mod in allowedModifiers.OrderBy(x => x.Key))
    {
        string displayName = mod.Key.Replace("imodbit_", "").Replace("_", " ");
        bool alreadyExists = IsDuplicateMeshType(meshIndex, itemData.Meshes[meshIndex].UsageType, mod.Key);
        
        if (alreadyExists)
        {
            menu.AddDisabledItem(new GUIContent($"{displayName} (already exists)"));
        }
        else
        {
            string modKey = mod.Key; // Capture for lambda
            menu.AddItem(new GUIContent(displayName), false, () => {
                itemData.Meshes[meshIndex].ItemModifier = modKey;
            });
        }
    }
    
    menu.ShowAsContext();
}
        
        // FACTION LIST DRAWER
        private void DrawFactionList()
        {
            EditorGUILayout.LabelField($"Factions ({itemData.FactionIds.Count}):", EditorStyles.boldLabel);
            
            if (itemData.FactionIds.Count == 0)
            {
                EditorGUILayout.HelpBox("No factions specified - item available to all factions.", MessageType.Info);
            }
            
            for (int i = 0; i < itemData.FactionIds.Count; i++)
            {
                EditorGUILayout.BeginHorizontal();
                
                itemData.FactionIds[i] = EditorGUILayout.TextField(
                    new GUIContent($"Faction {i + 1}", "Faction ID (e.g., fac_kingdom_1)"),
                    itemData.FactionIds[i]);
                
                if (GUILayout.Button("✖", GUILayout.Width(25)))
                {
                    itemData.FactionIds.RemoveAt(i);
                    break;
                }
                
                EditorGUILayout.EndHorizontal();
            }
            
            if (GUILayout.Button("➕ Add Faction"))
            {
                itemData.FactionIds.Add("");
            }
        }
        
        // HELPER: Parse various number formats
        private BigInteger ParseBigInteger(string input)
        {
            input = input.Trim();
            
            // Handle hex (0x prefix)
            if (input.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            {
                return BigInteger.Parse(
                    input.Substring(2), 
                    System.Globalization.NumberStyles.HexNumber);
            }
            
            // Handle decimal
            return BigInteger.Parse(input);
        }
    }
}
