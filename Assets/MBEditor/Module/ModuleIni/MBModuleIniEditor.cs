using System.Collections.Generic;
using System.IO;
using BDT.GUI.Helpers;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Static editor class for drawing MBModuleIni configuration GUI.
/// Can be embedded in other editor windows.
/// </summary>
public static class MBModuleIniEditor
{
    #region Tab System

    private static int _mainTabIndex;

    private static readonly string[] _mainTabs =
    {
        "General",
        "World Map",
        "Leveling",
        "Combat",
        "Features",
        "Graphics",
        "Multiplayer",
        "Resources"
    };

    // Foldout states
    private static bool _foldVersioning = true;
    private static bool _foldMainMenu = true;
    private static bool _foldResourceLoading = true;
    private static bool _foldMapBoundaries = true;
    private static bool _foldMapCamera = true;
    private static bool _foldMapWater = true;
    private static bool _foldMapTrees = true;
    private static bool _foldTime = true;
    private static bool _foldAttributes = true;
    private static bool _foldXpMultipliers = true;
    private static bool _foldWounded = true;
    private static bool _foldPartyBonuses = true;
    private static bool _foldWeaponProficiency = true;
    private static bool _foldHorseModifiers = true;
    private static bool _foldPartySettings = true;
    private static bool _foldGameMenu = true;
    private static bool _foldDamageThresholds = true;
    private static bool _foldShieldPenetration = true;
    private static bool _foldArmorSoak = true;
    private static bool _foldArmorReduction = true;
    private static bool _foldExtraPenetration = true;
    private static bool _foldDamageMultipliers = true;
    private static bool _foldDamageSpeedScaling = true;
    private static bool _foldCombatAI = true;
    private static bool _foldBattleSettings = true;
    private static bool _foldPhysics = true;
    private static bool _foldSceneSettings = true;
    private static bool _foldMovementCombat = true;
    private static bool _foldHorses = true;
    private static bool _foldSceneEffects = true;
    private static bool _foldFirearmsFormations = true;
    private static bool _foldUICamera = true;
    private static bool _foldShaders = true;
    private static bool _foldGraphicsEffects = true;
    private static bool _foldScreenshotsHair = true;
    private static bool _foldMultiplayer = true;
    private static bool _foldPerformance = true;
    private static bool _foldSavegame = true;
    private static bool _foldResources = true;

    private static Vector2 _scrollPosition;
    private static Vector2 _resourceScrollPosition;

    #endregion

    // Reference to current target for Undo operations
    private static MBModuleIni _currentTarget;
    private static MBModule _currentModule;

    #region Main Draw Method

    public static void DrawModuleIniGUI(MBModule module)
    {
        var ini = module.ModuleIni;

        if (ini == null)
        {
            EditorGUILayout.HelpBox("No MBModuleIni assigned.", MessageType.Warning);
            return;
        }

        _currentModule = module;
        _currentTarget = ini;

        // Draw usage stats
        var stats = ini.GetUsageStats();
        EditorGUILayout.BeginHorizontal();
        GUILayout.Label($"Parameters: {stats.used} / {stats.total} used", StylesHelpers.MiniLabel(UIColors.Cyan));
        GUILayout.FlexibleSpace();
        if (GUILayout.Button("Reset All", GUILayout.Width(80)))
        {
            if (EditorUtility.DisplayDialog("Reset Module.ini",
                    "Reset all parameters to default values?", "Yes", "No"))
            {
                Undo.RecordObject(ini, "Reset All Module.ini Parameters");
                ini.ResetAllToDefaults();
                EditorUtility.SetDirty(ini);
            }
        }

        EditorGUILayout.EndHorizontal();

        GUILayout.Space(4);
        _mainTabIndex = GUILayout.Toolbar(_mainTabIndex, _mainTabs, GUILayout.Height(22));
        GUILayout.Space(4);
        UIHelpers.DrawUILine(UIColors.GrayLine);

        _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);

        switch (_mainTabIndex)
        {
            case 0: DrawGeneralTab(ini); break;
            case 1: DrawWorldMapTab(ini); break;
            case 2: DrawLevelingTab(ini); break;
            case 3: DrawCombatTab(ini); break;
            case 4: DrawFeaturesTab(ini); break;
            case 5: DrawGraphicsTab(ini); break;
            case 6: DrawMultiplayerTab(ini); break;
            case 7: DrawResourcesTab(ini); break;
        }

        EditorGUILayout.EndScrollView();
    }

    #endregion

    #region Tab: General

    private static void DrawGeneralTab(MBModuleIni ini)
    {
        // Versioning Section
        _foldVersioning = UIHelpers.BlockElementStart("Module Identity & Versioning", _foldVersioning);
        if (_foldVersioning)
        {
            DrawStringParam(ini.moduleName, "Module Name",
                "The name of your module. Shows up in rgl_log.txt for debugging.");

            DrawIntParam(ini.moduleVersion, "Module Version",
                "Version number for multiplayer and savegame versioning.");

            DrawIntParam(ini.compatibleModuleVersion, "Compatible Module Version",
                "Oldest version number compatible with this one for multiplayer.");

            DrawIntParam(ini.compatibleMultiplayerVersionNo, "Multiplayer Version",
                "Current multiplayer version. Used with compatible_module_version to determine server connections.");

            DrawIntParam(ini.compatibleSavegameModuleVersion, "Savegame Version",
                "Oldest savegame version compatible with this module.");

            DrawBoolParam(ini.compatibleWithWarband, "Compatible with Warband",
                "Mark original M&B mods playable with Warband (deprecated).");

            DrawFloatParam(ini.operationSetVersion, "Operation Set Version",
                "Triggers VC game key check if >= 1.160. Can be read with get_operation_set_version.");

            UIHelpers.BlockElementEnd();
        }

        // Main Menu Section
        _foldMainMenu = UIHelpers.BlockElementStart("Main Menu Options", _foldMainMenu);
        if (_foldMainMenu)
        {
            DrawBoolParam(ini.hasSinglePlayer, "Single Player",
                "Show 'New Game' and 'Load Game' on main menu.");

            DrawBoolParam(ini.hasMultiplayer, "Multiplayer",
                "Show 'Multiplayer' on main menu.");

            DrawBoolParam(ini.hasCustomBattle, "Custom Battle",
                "Show 'Custom Battle' on main menu.");

            DrawBoolParam(ini.hasTutorial, "Tutorial",
                "Show 'Tutorial' on main menu.");

            DrawBoolParam(ini.enableQuickBattles, "Quick Battles (1.011)",
                "Show 'Quick Battles' on main menu. Only works in vanilla M&B 1.011.");

            UIHelpers.BlockElementEnd();
        }

        // Resource Loading Options
        _foldResourceLoading = UIHelpers.BlockElementStart("Resource Loading Options", _foldResourceLoading);
        if (_foldResourceLoading)
        {
            DrawBoolParam(ini.scanModuleTextures, "Scan Module Textures",
                "Scan the module's /Textures/ folder for loose texture files.");

            DrawBoolParam(ini.scanModuleSounds, "Scan Module Sounds",
                "Scan the module's /Sounds/ folder for loose sound files.");

            DrawBoolParam(ini.useCaseInsensitiveMeshSearches, "Case Insensitive Meshes",
                "Allow case insensitive mesh name references (sword_a == Sword_a).");

            UIHelpers.BlockElementEnd();
        }

        // Game Menu Settings
        _foldGameMenu = UIHelpers.BlockElementStart("Game Menu Settings", _foldGameMenu);
        if (_foldGameMenu)
        {
            DrawBoolParam(ini.autoCreateNoteIndices, "Auto Create Note Indices",
                "Automatically search troops/factions/towns for note text.");

            DrawBoolParam(ini.disableForceLeavingConversations, "Disable Force Leave Conversations",
                "Disable TAB to force-end conversations.");

            DrawBoolParam(ini.showTroopUpgradesButton, "Show Troop Upgrades",
                "Show the troop upgrade button in party screen.");

            DrawBoolParam(ini.showQuestNotes, "Show Quest Notes",
                "Show notes on the quest screen.");

            UIHelpers.BlockElementEnd();
        }

        // Savegame Settings
        _foldSavegame = UIHelpers.BlockElementStart("Savegame Settings", _foldSavegame);
        if (_foldSavegame)
        {
            DrawBoolParam(ini.dontLoadRegularTroopInventories, "Skip Troop Inventories",
                "Don't save inventories for regular troops (smaller saves, may crash if changed mid-game).");

            UIHelpers.BlockElementEnd();
        }
    }

    #endregion

    #region Tab: World Map

    private static void DrawWorldMapTab(MBModuleIni ini)
    {
        // Map Boundaries
        _foldMapBoundaries = UIHelpers.BlockElementStart("Map Boundaries", _foldMapBoundaries);
        if (_foldMapBoundaries)
        {
            EditorGUILayout.BeginHorizontal();
            DrawIntParam(ini.mapMinX, "Min X (East)", "Eastern boundary of world map.", 220);
            DrawIntParam(ini.mapMaxX, "Max X (West)", "Western boundary of world map.", 220);
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            DrawIntParam(ini.mapMinY, "Min Y (North)", "Northern boundary of world map.", 220);
            DrawIntParam(ini.mapMaxY, "Max Y (South)", "Southern boundary of world map.", 220);
            EditorGUILayout.EndHorizontal();

            DrawFloatParam(ini.mapMaxDistance, "Max Zoom Distance",
                "Maximum zoom out distance on world map (max ~251).");

            UIHelpers.BlockElementEnd();
        }

        // Map Camera
        _foldMapCamera = UIHelpers.BlockElementStart("Map Camera", _foldMapCamera);
        if (_foldMapCamera)
        {
            DrawFloatParam(ini.mapMinElevation, "Min Elevation",
                "Minimum camera distance from terrain. Also affects viewing angle.");

            DrawFloatParam(ini.mapMaxElevation, "Max Elevation",
                "Maximum camera distance from terrain on world map.");

            UIHelpers.BlockElementEnd();
        }

        // Map Water
        _foldMapWater = UIHelpers.BlockElementStart("Map Water Shaders", _foldMapWater);
        if (_foldMapWater)
        {
            UIHelpers.LabelHeader("Sea Settings");
            DrawIntParam(ini.mapSeaDirection, "Sea Direction",
                "Angle for sea wave foam direction.");

            DrawIntParam(ini.mapSeaWaveRotation, "Sea Wave Rotation",
                "Angle where the tear artifact is visible on the sea.");

            EditorGUILayout.BeginHorizontal();
            DrawFloatParam(ini.mapSeaSpeedX, "Sea Speed X", "Flow speed on X-axis.", 220);
            DrawFloatParam(ini.mapSeaSpeedY, "Sea Speed Y", "Flow speed on Y-axis.", 220);
            EditorGUILayout.EndHorizontal();

            GUILayout.Space(8);
            UIHelpers.LabelHeader("River Settings");
            DrawIntParam(ini.mapRiverDirection, "River Direction",
                "Angle for river flow direction.");

            EditorGUILayout.BeginHorizontal();
            DrawFloatParam(ini.mapRiverSpeedX, "River Speed X", "Flow speed on X-axis.", 220);
            DrawFloatParam(ini.mapRiverSpeedY, "River Speed Y", "Flow speed on Y-axis.", 220);
            EditorGUILayout.EndHorizontal();

            UIHelpers.BlockElementEnd();
        }

        // Map Trees
        _foldMapTrees = UIHelpers.BlockElementStart("Map Tree Types", _foldMapTrees);
        if (_foldMapTrees)
        {
            EditorGUILayout.HelpBox(
                "Tree meshes must be named: map_tree_a, map_tree_b... for forest, " +
                "snow_tree_a, snow_tree_b... for snow, etc. Max 64 per type.",
                MessageType.Info);

            DrawIntParam(ini.mapTreeTypes, "Forest Trees (map_tree_*)",
                "Number of tree types for normal terrain (1-64).", 0, 1, 64);

            DrawIntParam(ini.mapSnowTreeTypes, "Snow Trees (snow_tree_*)",
                "Number of tree types for snow terrain (1-64).", 0, 1, 64);

            DrawIntParam(ini.mapSteppeTreeTypes, "Steppe Trees (steppe_tree_*)",
                "Number of tree types for steppe terrain (1-64).", 0, 1, 64);

            DrawIntParam(ini.mapDesertTreeTypes, "Desert Trees (desert_tree_*)",
                "Number of tree types for desert terrain (1-64).", 0, 1, 64);

            UIHelpers.BlockElementEnd();
        }

        // Time Settings
        _foldTime = UIHelpers.BlockElementStart("Time Settings", _foldTime);
        if (_foldTime)
        {
            DrawFloatParam(ini.timeMultiplier, "Time Multiplier",
                "Speed multiplier for map time. 0.25 = 1 game hour per 4 real seconds. " +
                "2.4 = 1 game day per 10 real seconds.");

            UIHelpers.BlockElementEnd();
        }
    }

    #endregion

    #region Tab: Leveling

    private static void DrawLevelingTab(MBModuleIni ini)
    {
        // Attributes & Skills
        _foldAttributes = UIHelpers.BlockElementStart("Attributes & Skills", _foldAttributes);
        if (_foldAttributes)
        {
            DrawFloatParam(ini.attributePointsPerLevel, "Attribute Points/Level",
                "Attribute points gained per level. Native = 1.");

            DrawIntParam(ini.attributeRequiredPerSkillLevel, "Attribute per Skill Level",
                "Attribute points required per skill level. Native = 3. Max skill = attribute / this value.");

            DrawIntParam(ini.skillPointsPerLevel, "Skill Points/Level",
                "Skill points gained per level. Native = 1.");

            DrawIntParam(ini.weaponPointsPerLevel, "Weapon Points/Level",
                "Weapon proficiency points gained per level. Native = 10.");

            DrawFloatParam(ini.levelBoundaryMultiplier, "Level Boundary Multiplier",
                "Multiplier for XP needed for troop upgrades. Native = 1.");

            DrawBoolParam(ini.canRunFasterWithSkills, "Run Faster with Skills",
                "Use enhanced formula where agility/athletics have greater effect on run speed.");

            UIHelpers.BlockElementEnd();
        }

        // XP Multipliers
        _foldXpMultipliers = UIHelpers.BlockElementStart("XP Multipliers", _foldXpMultipliers);
        if (_foldXpMultipliers)
        {
            DrawFloatParam(ini.playerXpMultiplier, "Player XP Multiplier",
                "XP multiplier for player kills. Max ~10-15.", 0, 0.1f, 15f);

            DrawFloatParam(ini.heroXpMultiplier, "Hero XP Multiplier",
                "XP multiplier for companion/hero kills. Max ~10-15.", 0, 0.1f, 15f);

            DrawFloatParam(ini.regularsXpMultiplier, "Regular Troop XP Multiplier",
                "XP multiplier for regular troop kills. Max ~10-15.", 0, 0.1f, 15f);

            UIHelpers.BlockElementEnd();
        }

        // Wounded Thresholds
        _foldWounded = UIHelpers.BlockElementStart("Wounded Thresholds", _foldWounded);
        if (_foldWounded)
        {
            DrawIntParam(ini.playerWoundedThreshold, "Player Wounded HP",
                "Minimum HP for player to appear in battles and contribute to party skills.");

            DrawIntParam(ini.heroWoundedThreshold, "Hero Wounded HP",
                "Minimum HP for heroes to appear in battles and contribute to party skills.");

            UIHelpers.BlockElementEnd();
        }

        // Party Bonuses
        _foldPartyBonuses = UIHelpers.BlockElementStart("Party Bonuses", _foldPartyBonuses);
        if (_foldPartyBonuses)
        {
            DrawIntParam(ini.skillLeadershipBonus, "Leadership Bonus",
                "Additional troops per leadership skill point.");

            DrawIntParam(ini.skillPrisonerManagementBonus, "Prisoner Mgmt Bonus",
                "Additional prisoners per prisoner management skill point.");

            DrawIntParam(ini.baseCompanionLimit, "Base Companion Limit",
                "Maximum companions without leadership skill.");

            DrawFloatParam(ini.trackSpottingMultiplier, "Track Spotting Multiplier",
                "Tracking skill multiplier. 0 = no tracking marks appear.");

            UIHelpers.BlockElementEnd();
        }

        // Weapon Proficiency Display
        _foldWeaponProficiency = UIHelpers.BlockElementStart("Weapon Proficiency Display", _foldWeaponProficiency);
        if (_foldWeaponProficiency)
        {
            EditorGUILayout.HelpBox(
                "These only hide the proficiency in the character menu. " +
                "Weapons and leveling still work.", MessageType.Info);

            EditorGUILayout.BeginHorizontal();
            DrawBoolParam(ini.displayWpOneHanded, "One-Handed", "", 100);
            DrawBoolParam(ini.displayWpTwoHanded, "Two-Handed", "", 100);
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            DrawBoolParam(ini.displayWpPolearms, "Polearms", "", 100);
            DrawBoolParam(ini.displayWpArchery, "Archery", "", 100);
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            DrawBoolParam(ini.displayWpCrossbows, "Crossbows", "", 100);
            DrawBoolParam(ini.displayWpThrowing, "Throwing", "", 100);
            EditorGUILayout.EndHorizontal();

            DrawBoolParam(ini.displayWpFirearms, "Firearms", "Show firearms proficiency (hidden in Native).");

            UIHelpers.BlockElementEnd();
        }

        // Horse Speed Modifiers
        _foldHorseModifiers = UIHelpers.BlockElementStart("Horse Speed Modifiers", _foldHorseModifiers);
        if (_foldHorseModifiers)
        {
            DrawFloatParam(ini.meekModifierSpeedBonus, "Meek Speed Bonus",
                "Speed modifier for horses with 'meek' modifier.");

            DrawFloatParam(ini.timidModifierSpeedBonus, "Timid Speed Bonus",
                "Speed modifier for horses with 'timid' modifier.");

            UIHelpers.BlockElementEnd();
        }
    }

    #endregion

    #region Tab: Combat

    private static void DrawCombatTab(MBModuleIni ini)
    {
        // Damage Thresholds
        _foldDamageThresholds = UIHelpers.BlockElementStart("Damage Thresholds", _foldDamageThresholds);
        if (_foldDamageThresholds)
        {
            DrawFloatParam(ini.crushThroughThreshold, "Crush Through Threshold",
                "Damage required to crush through overhead blocks (itp_crush_through weapons).");

            DrawFloatParam(ini.damageInterruptAttackThreshold, "Interrupt Threshold (SP)",
                "Damage below this won't interrupt melee attacks in singleplayer.");

            DrawFloatParam(ini.damageInterruptAttackThresholdMp, "Interrupt Threshold (MP)",
                "Damage below this won't interrupt melee attacks in multiplayer.");

            UIHelpers.BlockElementEnd();
        }

        // Shield Penetration
        _foldShieldPenetration = UIHelpers.BlockElementStart("Shield Penetration", _foldShieldPenetration);
        if (_foldShieldPenetration)
        {
            EditorGUILayout.HelpBox(
                "Missiles penetrate if: damage > offset + factor × shield_value",
                MessageType.Info);

            DrawFloatParam(ini.shieldPenetrationOffset, "Penetration Offset",
                "Base offset for shield penetration calculation.");

            DrawFloatParam(ini.shieldPenetrationFactor, "Penetration Factor",
                "Factor multiplied by shield value.");

            UIHelpers.BlockElementEnd();
        }

        // Armor Soak
        _foldArmorSoak = UIHelpers.BlockElementStart("Armor Soak (Direct Subtraction)", _foldArmorSoak);
        if (_foldArmorSoak)
        {
            EditorGUILayout.HelpBox(
                "Amount directly subtracted from damage. Can be negative to ADD damage.",
                MessageType.Info);

            DrawFloatParam(ini.armorSoakFactorAgainstCut, "vs Cut", "Soak factor against cut damage.");
            DrawFloatParam(ini.armorSoakFactorAgainstPierce, "vs Pierce", "Soak factor against pierce damage.");
            DrawFloatParam(ini.armorSoakFactorAgainstBlunt, "vs Blunt", "Soak factor against blunt damage.");

            UIHelpers.BlockElementEnd();
        }

        // Armor Reduction
        _foldArmorReduction = UIHelpers.BlockElementStart("Armor Reduction (Percentage)", _foldArmorReduction);
        if (_foldArmorReduction)
        {
            DrawFloatParam(ini.armorReductionFactorAgainstCut, "vs Cut", "Reduction % against cut damage.");
            DrawFloatParam(ini.armorReductionFactorAgainstPierce, "vs Pierce", "Reduction % against pierce damage.");
            DrawFloatParam(ini.armorReductionFactorAgainstBlunt, "vs Blunt", "Reduction % against blunt damage.");

            UIHelpers.BlockElementEnd();
        }

        // Extra Penetration
        _foldExtraPenetration =
            UIHelpers.BlockElementStart("Extra Penetration (itp_extra_penetration)", _foldExtraPenetration);
        if (_foldExtraPenetration)
        {
            EditorGUILayout.HelpBox(
                "Only affects weapons with itp_extra_penetration flag. Set to 1.0 to disable.",
                MessageType.Info);

            DrawFloatParam(ini.extraPenetrationFactorSoak, "Soak Factor",
                "Soak factor for extra penetration weapons. 0 = ignore armor.");

            DrawFloatParam(ini.extraPenetrationFactorReduction, "Reduction Factor",
                "Reduction factor for extra penetration weapons. 0 = ignore armor.");

            UIHelpers.BlockElementEnd();
        }

        // Damage Multipliers
        _foldDamageMultipliers = UIHelpers.BlockElementStart("Damage Multipliers", _foldDamageMultipliers);
        if (_foldDamageMultipliers)
        {
            DrawFloatParam(ini.horseChargeDamageMultiplier, "Horse Charge",
                "Multiplier for horse charge damage when ramming agents.");

            DrawFloatParam(ini.couchedLanceDamageMultiplier, "Couched Lance",
                "Multiplier for couched lance damage. Native = 0.65.");

            DrawFloatParam(ini.fallDamageMultiplier, "Fall Damage",
                "Multiplier for fall damage.");

            UIHelpers.BlockElementEnd();
        }

        // Damage Speed Scaling
        _foldDamageSpeedScaling = UIHelpers.BlockElementStart("Damage Speed Scaling", _foldDamageSpeedScaling);
        if (_foldDamageSpeedScaling)
        {
            EditorGUILayout.HelpBox(
                "2.0 = damage scales with speed². 1.0 = linear scaling.",
                MessageType.Info);

            DrawFloatParam(ini.missileDamageSpeedPower, "Missile Speed Power",
                "Power for missile damage speed scaling.");

            DrawFloatParam(ini.meleeDamageSpeedPower, "Melee Speed Power",
                "Power for melee damage speed scaling.");

            UIHelpers.BlockElementEnd();
        }

        // Combat AI
        _foldCombatAI = UIHelpers.BlockElementStart("Combat AI & Misc", _foldCombatAI);
        if (_foldCombatAI)
        {
            DrawBoolParam(ini.aiDecideDirectionAccordingToDamage, "AI Uses Damage Direction",
                "AI considers damage when choosing attack direction.");

            DrawBoolParam(ini.applyAllAmmoDamageModifiers, "Apply All Ammo Modifiers",
                "All ammo applies Blunt/Sharp/Cutting modifier damage.");

            DrawFloatParam(ini.braceRotationLimit, "Brace Rotation Limit",
                "Rotation speed limit while bracing pikes. 0.5 = disable canceling effect.");

            DrawFloatParam(ini.lancePikeEffectSpeed, "Lance/Pike Effect Speed",
                "Horse speed required to trigger pike rear animation.");

            DrawBoolParam(ini.noFriendlyFireForBots, "No FF for Bots (MP)",
                "Disable friendly fire for AI in multiplayer.");

            DrawBoolParam(ini.considerWeaponLengthForWeaponQuality, "Consider Weapon Length",
                "AI prefers longer weapons when choosing from inventory.");

            UIHelpers.BlockElementEnd();
        }

        // Battle Settings
        _foldBattleSettings = UIHelpers.BlockElementStart("Battle Settings", _foldBattleSettings);
        if (_foldBattleSettings)
        {
            DrawIntParam(ini.battleSizeMin, "Min Battle Size",
                "Minimum value for battle size slider.");

            DrawIntParam(ini.battleSizeMax, "Max Battle Size",
                "Maximum value for battle size slider.");

            DrawFloatParam(ini.farPlaneDistance, "Far Plane Distance",
                "Render distance in cm. Default 1250. Increase for larger terrain views.");

            UIHelpers.BlockElementEnd();
        }

        // Physics
        _foldPhysics = UIHelpers.BlockElementStart("Physics - Air Friction", _foldPhysics);
        if (_foldPhysics)
        {
            DrawFloatParam(ini.airFrictionArrow, "Arrow Friction",
                "Drag coefficient for arrows. Native = 0.002.");

            DrawFloatParam(ini.airFrictionBullet, "Bullet Friction",
                "Drag coefficient for firearm projectiles. Native = 0.002.");

            UIHelpers.BlockElementEnd();
        }

        // Scene Settings
        _foldSceneSettings = UIHelpers.BlockElementStart("Scene Settings", _foldSceneSettings);
        if (_foldSceneSettings)
        {
            DrawBoolParam(ini.disableMoveableFlagOptimization, "All Props Moveable",
                "Enable moveable physics on ALL scene props without sokf_moveable flag.");

            DrawIntParam(ini.missionObjectPruneTime, "Object Prune Time",
                "Seconds before dropped items/arrows despawn. 0 = never (not recommended).");

            UIHelpers.BlockElementEnd();
        }
    }

    #endregion

    #region Tab: Features

    private static void DrawFeaturesTab(MBModuleIni ini)
    {
        // Movement & Combat
        _foldMovementCombat = UIHelpers.BlockElementStart("Movement & Combat", _foldMovementCombat);
        if (_foldMovementCombat)
        {
            DrawBoolParam(ini.canCrouch, "Can Crouch",
                "Allow agents to crouch. Requires ui_crouch string in ui.csv.");

            DrawBoolParam(ini.canReloadWhileMoving, "Reload While Moving",
                "Allow crossbow/firearm reload while walking/running.");

            DrawBoolParam(ini.disableAttackWhileJumping, "Disable Jump Attacks",
                "Prevent attacks while jumping.");

            DrawBoolParam(ini.disableZoom, "Disable Zoom",
                "Disable shift-zoom functionality.");

            DrawBoolParam(ini.useCrossbowAsFirearm, "Crossbow as Firearm",
                "Use crossbow reload animations for firearms (legacy workaround).");

            UIHelpers.BlockElementEnd();
        }

        // Horses
        _foldHorses = UIHelpers.BlockElementStart("Horse Behavior", _foldHorses);
        if (_foldHorses)
        {
            DrawBoolParam(ini.horsesRearWithAttack, "Rear on Attack",
                "Horses rear when struck with enough damage.");

            DrawBoolParam(ini.horsesTryRunningAway, "Flee When Riderless",
                "Riderless horses flee from aggressive agents.");

            UIHelpers.BlockElementEnd();
        }

        // Scene & Effects
        _foldSceneEffects = UIHelpers.BlockElementStart("Scene & Effects", _foldSceneEffects);
        if (_foldSceneEffects)
        {
            DrawBoolParam(ini.canObjectsMakeSound, "Objects Make Sound",
                "Allow scene props to emit sounds.");

            DrawBoolParam(ini.canUseScenePropsInSinglePlayer, "Use Props in SP",
                "Allow scene prop interaction in singleplayer (like MP doors).");

            DrawBoolParam(ini.hasForcedParticles, "Forced Particles",
                "Force particle effects (prevent disabling for advantage).");

            UIHelpers.BlockElementEnd();
        }

        // Firearms & Formations
        _foldFirearmsFormations = UIHelpers.BlockElementStart("Firearms & Formations (NW/VC)", _foldFirearmsFormations);
        if (_foldFirearmsFormations)
        {
            DrawBoolParam(ini.usePhasedReload, "Phased Reload",
                "Multi-phase reload for muskets/pistols (NW style).");

            DrawBoolParam(ini.useAdvancedFormation, "Advanced Formations",
                "Use NW/WFaS formation system with row formations and fire commands.");

            DrawBoolParam(ini.shorterPistolAiming, "Shorter Pistol Aim",
                "Shortened aiming animation for pistols.");

            UIHelpers.BlockElementEnd();
        }

        // UI & Camera
        _foldUICamera = UIHelpers.BlockElementStart("UI & Camera", _foldUICamera);
        if (_foldUICamera)
        {
            DrawBoolParam(ini.canAdjustCameraDistance, "Adjust Camera Distance",
                "Allow numpad +/- to adjust camera position.");

            DrawBoolParam(ini.disableFoodSlot, "Disable Food Slot",
                "Hide legacy food slot in inventory (deprecated feature).");

            DrawBoolParam(ini.hasAccessoriesForFemale, "Female Accessories",
                "Replace 'beard' with 'accessories' for female character creation.");

            DrawIntParam(ini.numHints, "Number of Hints",
                "Total loading screen hints.");

            UIHelpers.BlockElementEnd();
        }

        // Party Settings
        _foldPartySettings = UIHelpers.BlockElementStart("Party Settings", _foldPartySettings);
        if (_foldPartySettings)
        {
            DrawBoolParam(ini.autoComputePartyRadius, "Auto Party Radius",
                "Use party icon model to determine interaction radius.");

            DrawFloatParam(ini.seeingRange, "Seeing Range",
                "Default party visibility radius on world map.");

            DrawBoolParam(ini.showPartyIdsInsteadOfNames, "Show Party IDs",
                "Debug: Show party IDs instead of names.");

            DrawBoolParam(ini.useStrictPathfindingForShips, "Strict Ship Paths",
                "Keep ships at sea for sea-to-sea routes.");

            // Region type dropdown
            EditorGUILayout.BeginHorizontal();
            var regionContent = new GUIContent("Disable Disband Terrain",
                "Terrain type where parties cannot disband.");
            EditorGUILayout.PrefixLabel(regionContent);

            EditorGUI.BeginChangeCheck();
            var currentRegion = ini.DisableDisbandOnTerrainType;
            var newRegion = (MBModuleIni.RegionType)EditorGUILayout.EnumPopup(currentRegion);
            if (EditorGUI.EndChangeCheck() && newRegion != currentRegion)
            {
                RecordUndo("Change Disable Disband Terrain");
                ini.DisableDisbandOnTerrainType = newRegion;
                ini.DisableDisbandOnTerrainTypeIsUsed = true;
            }

            ini.DisableDisbandOnTerrainTypeIsUsed =
                DrawUsedIndicatorBool(ini.DisableDisbandOnTerrainTypeIsUsed, "Disable Disband Terrain");
            EditorGUILayout.EndHorizontal();

            UIHelpers.BlockElementEnd();
        }
    }

    #endregion

    #region Tab: Graphics

    private static void DrawGraphicsTab(MBModuleIni ini)
    {
        // Shaders
        _foldShaders = UIHelpers.BlockElementStart("Shader Parameters", _foldShaders);
        if (_foldShaders)
        {
            DrawBoolParam(ini.addSetNeighborsToTangentFlagToShader, "Neighbor Tangents",
                "Pass neighboring face tangent info to shaders.");

            DrawBoolParam(ini.fixGammaOnDx7OperationColors, "Fix DX7 Gamma",
                "Fix gamma on DX7 operation colors for ambient light ops.");

            DrawBoolParam(ini.useBorderedShadowSampler, "Bordered Shadow Sampler",
                "Use bordered shadow sampler (unused by engine).");

            UIHelpers.BlockElementEnd();
        }

        // Effects
        _foldGraphicsEffects = UIHelpers.BlockElementStart("Visual Effects", _foldGraphicsEffects);
        if (_foldGraphicsEffects)
        {
            DrawFloatParam(ini.bloodMultiplier, "Blood Multiplier",
                "Blood particle effect amount/size multiplier.");

            DrawBoolParam(ini.disableHighHdr, "Disable High HDR",
                "Disable high HDR effects in module.");

            UIHelpers.BlockElementEnd();
        }

        // Screenshots & Hair
        _foldScreenshotsHair = UIHelpers.BlockElementStart("Screenshots & Hair", _foldScreenshotsHair);
        if (_foldScreenshotsHair)
        {
            // Screenshot format dropdown
            EditorGUILayout.BeginHorizontal();
            var formatContent = new GUIContent("Screenshot Format",
                "File format for Ctrl+Insert screenshots. Saved to Screenshots/<ModuleName>/");
            EditorGUILayout.PrefixLabel(formatContent);

            EditorGUI.BeginChangeCheck();
            var currentFormat = ini.ScreenshotFileFormat;
            var newFormat = (MBModuleIni.ScreenshotFormat)EditorGUILayout.EnumPopup(currentFormat);
            if (EditorGUI.EndChangeCheck() && newFormat != currentFormat)
            {
                RecordUndo("Change Screenshot Format");
                ini.ScreenshotFileFormat = newFormat;
                ini.ScreenshotFormatIsUsed = true;
            }

            ini.ScreenshotFormatIsUsed = DrawUsedIndicatorBool(ini.ScreenshotFormatIsUsed, "Screenshot Format");
            EditorGUILayout.EndHorizontal();

            DrawBoolParam(ini.limitHairColors, "Limit Hair Colors",
                "Use vertex coloring for hair. If 0, expects hair_red, beard_black textures etc.");

            DrawBoolParam(ini.showFactionColor, "Show Faction Colors",
                "Show faction colors on party names on world map.");

            UIHelpers.BlockElementEnd();
        }

        // Performance
        _foldPerformance = UIHelpers.BlockElementStart("Performance & Debug", _foldPerformance);
        if (_foldPerformance)
        {
            DrawBoolParam(ini.dontSupressInitialWarnings, "Show Initial Warnings",
                "Show loading warnings in rgl_log.txt (set 0 for release).");

            DrawBoolParam(ini.givePerformanceWarnings, "Performance Warnings",
                "Show warnings for complex collision meshes.");

            DrawIntParam(ini.maximumNumberOfNotificationMessages, "Max Notifications",
                "Maximum battle notification messages.");

            DrawBoolParam(ini.reduceTextureLoaderMemoryUsage, "Multithread Textures",
                "Improve loading time with multithread texture loading.");

            DrawBoolParam(ini.supportsDirectx7, "Support DirectX 7",
                "Support DX7 mode for old hardware.");

            DrawBoolParam(ini.useSceneUnloading, "Scene Unloading",
                "Unload scenes from RAM when leaving (saves RAM, slower revisits).");

            DrawBoolParam(ini.useTextureDegrationCache, "Texture Cache",
                "Cache degraded textures to disk.");

            UIHelpers.BlockElementEnd();
        }
    }

    #endregion

    #region Tab: Multiplayer

    private static void DrawMultiplayerTab(MBModuleIni ini)
    {
        _foldMultiplayer = UIHelpers.BlockElementStart("Multiplayer Settings", _foldMultiplayer);
        if (_foldMultiplayer)
        {
            DrawBoolParam(ini.multiplayerWalkEnabled, "Walk Enabled",
                "Allow walking in multiplayer (gk_walk key). Needs disable_zoom = 1.");

            DrawBoolParam(ini.restrictAttacksMoreInMultiplayer, "Restrict Attacks",
                "More restrictive attack rotation (reduces spam/feinting).");

            DrawBoolParam(ini.showMultiplayerGold, "Show Gold",
                "Show player gold amount in HUD (icon always visible).");

            DrawBoolParam(ini.syncBlockDirections, "Sync Block Directions",
                "Sync block directions between client and server.");

            DrawBoolParam(ini.syncRagdollEffects, "Sync Ragdolls",
                "Sync ragdoll effects between client and server.");

            UIHelpers.BlockElementEnd();
        }
    }

    #endregion

    #region Tab: Resources

    private static void DrawResourcesTab(MBModuleIni ini)
    {
        // Refresh resource cache if needed
        RefreshResourceCache(ini);

        _foldResources = UIHelpers.BlockElementStart($"BRF Resources ({ini.resources.Count})", _foldResources);
        if (_foldResources)
        {
            EditorGUILayout.HelpBox(
                "Load Types:\n" +
                "• Load Resource: Searches CommonRes first, then module (use for shared/Native resources)\n" +
                "• Load Resource (No Fast): Same but skips fast-load optimization\n" +
                "• Load Module Resource: Module only (faster, use for module-specific resources)\n\n" +
                "Status Icons:\n" +
                "● Green = Found in module Resource folder\n" +
                "● Blue = Found in Native Resource folder\n" +
                "⚠ Orange = Folder not found\n" +
                "⚠ Red = Load type doesn't match folder location",
                MessageType.Info);

            // Top toolbar
            EditorGUILayout.BeginHorizontal();

            // Refresh button
            if (GUILayout.Button(new GUIContent("↻ Refresh", "Re-scan resource folders"), GUILayout.Width(80)))
            {
                InvalidateResourceCache();
                RefreshResourceCache(ini);
            }

            GUILayout.FlexibleSpace();

            // Add button
            if (GUILayout.Button("+ Add Resource", StylesHelpers.AddButtonStyle(), GUILayout.Width(120)))
            {
                RecordUndo("Add Resource");
                ini.AddResource("");
                InvalidateResourceCache();
            }

            EditorGUILayout.EndHorizontal();

            GUILayout.Space(4);

            // Column headers
            // Column headers
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            GUILayout.Label("", GUILayout.Width(20)); // Status icon
            GUILayout.Label("", GUILayout.Width(20)); // Mismatch warning
            GUILayout.Label("#", GUILayout.Width(30));
            GUILayout.Label("Resource Folder", GUILayout.MinWidth(100));
            GUILayout.Label("Load Type", GUILayout.Width(140));
            GUILayout.Label("", GUILayout.Width(76)); // Order + Move buttons
            GUILayout.Label("", GUILayout.Width(24)); // Remove
            EditorGUILayout.EndHorizontal();

            // Resource list
            _resourceScrollPosition = EditorGUILayout.BeginScrollView(_resourceScrollPosition,
                GUILayout.MaxHeight(400));

            for (int i = 0; i < ini.resources.Count; i++)
            {
                var resource = ini.resources[i];
                var status = GetResourceStatus(resource.resourceName);

                // Check for load type mismatch
                bool hasMismatch = IsLoadTypeMismatch(resource.loadType, status, out string mismatchWarning);

                // Row background based on status
                Color bgColor = GUI.backgroundColor;
                if (status == ResourceFolderStatus.NotFound)
                {
                    GUI.backgroundColor = new Color(1f, 0.9f, 0.7f, 1f);
                }
                else if (hasMismatch)
                {
                    GUI.backgroundColor = new Color(1f, 0.7f, 0.7f, 1f); // Red tint for mismatch
                }

                EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);
                GUI.backgroundColor = bgColor;

                // Status indicator icon
                DrawResourceStatusIcon(status, resource.resourceName);

                // Mismatch warning icon (or spacer)
                if (hasMismatch)
                {
                    DrawLoadTypeMismatchWarning(mismatchWarning);
                }
                else
                {
                    GUILayout.Space(20); // Keep alignment
                }

                // Index
                GUILayout.Label($"{i + 1}.", GUILayout.Width(30));

                // Resource folder field
                DrawResourceFolderField(ini, resource, status);

                // Load type popup (with filtered options)
                EditorGUI.BeginChangeCheck();
                var newLoadType = DrawLoadTypePopup(resource.loadType);
                if (EditorGUI.EndChangeCheck() && newLoadType != resource.loadType)
                {
                    RecordUndo("Change Resource Load Type");
                    resource.loadType = newLoadType;
                }

                // Set Order button - opens popup to set specific position
                if (GUILayout.Button(new GUIContent("#", "Set specific order position"), GUILayout.Width(24)))
                {
                    int capturedIndex = i; // Capture for closure
                    Rect buttonRect = GUILayoutUtility.GetLastRect();
                    SetOrderPopupWindow.Show(buttonRect, capturedIndex, ini.resources.Count,
                        (newOrder) => { MoveResourceToOrder(ini, capturedIndex, newOrder); });
                }

                // Move Up button
                GUI.enabled = i > 0;
                if (GUILayout.Button("▲", GUILayout.Width(24)))
                {
                    RecordUndo("Move Resource Up");
                    SwapResources(ini, i, i - 1);
                }

                // Move Down button
                GUI.enabled = i < ini.resources.Count - 1;
                if (GUILayout.Button("▼", GUILayout.Width(24)))
                {
                    RecordUndo("Move Resource Down");
                    SwapResources(ini, i, i + 1);
                }

                GUI.enabled = true;

                // Remove button
                if (GUILayout.Button("×", StylesHelpers.RemoveButtonStyle(), GUILayout.Width(24)))
                {
                    RecordUndo("Remove Resource");
                    ini.resources.RemoveAt(i);
                    InvalidateResourceCache();
                    i--;
                }

                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.EndScrollView();

            // Drag-drop zone for adding multiple folders at once
            DrawResourceDragDropZone(ini);

            // Resource stats
            DrawResourceStats(ini);

            UIHelpers.BlockElementEnd();
        }
    }

    /// <summary>
    /// Draw the resource folder field - as ObjectField if found, text field if not
    /// </summary>
    private static void DrawResourceFolderField(MBModuleIni ini, MBModuleIni.ResourceEntry resource,
        ResourceFolderStatus status)
    {
        string folderPath = GetResourceFolderPath(resource.resourceName, status);

        // Case 1: Found - show as ObjectField with the folder
        if (status != ResourceFolderStatus.NotFound && !string.IsNullOrEmpty(folderPath))
        {
            string unityPath = MBPathHelpers.FullToUnityPath(folderPath);
            DefaultAsset folderAsset = null;

            if (!string.IsNullOrEmpty(unityPath))
            {
                folderAsset = AssetDatabase.LoadAssetAtPath<DefaultAsset>(unityPath);
            }

            DrawFolderObjectField(ini, resource, folderAsset);
        }
        // Case 2: Not found but has a name - show editable text field with warning
        else if (!string.IsNullOrEmpty(resource.resourceName))
        {
            var style = new GUIStyle(EditorStyles.textField);
            style.normal.textColor = UIColors.Orange;

            EditorGUI.BeginChangeCheck();
            var newName = EditorGUILayout.TextField(resource.resourceName, style);
            if (EditorGUI.EndChangeCheck() && newName != resource.resourceName)
            {
                RecordUndo("Change Resource Name");
                resource.resourceName = newName;
                InvalidateResourceCache();
            }
        }
        // Case 3: Not found and no name - show empty ObjectField for drag-drop
        else
        {
            DrawFolderObjectField(ini, resource, null);
        }
    }

    /// <summary>
    /// Draw a folder ObjectField and handle selection changes
    /// </summary>
    private static void DrawFolderObjectField(MBModuleIni ini, MBModuleIni.ResourceEntry resource,
        DefaultAsset currentAsset)
    {
        EditorGUI.BeginChangeCheck();

        var newFolder = EditorGUILayout.ObjectField(
            currentAsset,
            typeof(DefaultAsset),
            false) as DefaultAsset;

        if (EditorGUI.EndChangeCheck() && newFolder != currentAsset)
        {
            if (newFolder != null)
            {
                string newPath = AssetDatabase.GetAssetPath(newFolder);

                if (AssetDatabase.IsValidFolder(newPath))
                {
                    string newFolderName = Path.GetFileName(newPath);

                    if (IsValidResourceFolder(newPath))
                    {
                        RecordUndo("Change Resource Folder");
                        resource.resourceName = newFolderName;
                        InvalidateResourceCache();
                    }
                    else
                    {
                        EditorUtility.DisplayDialog("Invalid Folder",
                            $"The folder must be inside a module's Resource folder.\n\n" +
                            $"Expected locations:\n" +
                            $"• {MBPathHelpers.ModResourcePath(_currentModule.ID)}/\n" +
                            $"• {MBPathHelpers.ModResourcePath("Native")}/",
                            "OK");
                    }
                }
                else
                {
                    EditorUtility.DisplayDialog("Invalid Selection",
                        "Please select a folder, not a file.", "OK");
                }
            }
            else
            {
                // Cleared the field - reset to empty string
                RecordUndo("Clear Resource Folder");
                resource.resourceName = "";
                InvalidateResourceCache();
            }
        }
    }

    /// <summary>
    /// Get the full path to a resource folder based on its status
    /// </summary>
    private static string GetResourceFolderPath(string resourceName, ResourceFolderStatus status)
    {
        // Guard against empty names
        if (string.IsNullOrEmpty(resourceName))
            return null;

        switch (status)
        {
            case ResourceFolderStatus.InModule:
                return Path.Combine(MBPathHelpers.ModResourcePath(_cachedModuleName), resourceName);
            case ResourceFolderStatus.InNative:
                return Path.Combine(MBPathHelpers.ModResourcePath("Native"), resourceName);
            default:
                return null;
        }
    }

    /// <summary>
    /// Check if a Unity asset path is a valid Resource folder location
    /// </summary>
    private static bool IsValidResourceFolder(string assetPath)
    {
        if (string.IsNullOrEmpty(assetPath))
            return false;

        // Fun logic :)
        string moduleResourcePath = MBPathHelpers.ModResourcePath(_currentModule.ID);
        moduleResourcePath = MBPathHelpers.FullToUnityPath(moduleResourcePath) + "/";
        string nativeResourcePath = MBPathHelpers.ModResourcePath("Native");
        nativeResourcePath = MBPathHelpers.FullToUnityPath(nativeResourcePath) + "/";

        var brfFolderName = Path.GetFileName(assetPath);
        var restedPath = assetPath.Replace(moduleResourcePath, "");
        var restedPathNative = assetPath.Replace(nativeResourcePath, "");

        return brfFolderName == restedPath || brfFolderName == restedPathNative;
    }

    /// <summary>
    /// Draw a drag-drop zone for adding multiple resource folders at once
    /// </summary>
    private static void DrawResourceDragDropZone(MBModuleIni ini)
    {
        GUILayout.Space(4);

        Rect dropArea = GUILayoutUtility.GetRect(0, 40, GUILayout.ExpandWidth(true));

        var style = new GUIStyle(EditorStyles.helpBox);
        style.alignment = TextAnchor.MiddleCenter;
        style.normal.textColor = UIColors.GrayLine;

        GUI.Box(dropArea, "Drag & Drop Resource folders here to add", style);

        // Handle drag and drop
        Event evt = Event.current;
        switch (evt.type)
        {
            case EventType.DragUpdated:
            case EventType.DragPerform:
                if (!dropArea.Contains(evt.mousePosition))
                    break;

                // Check if any dragged objects are valid folders
                bool hasValidFolder = false;
                foreach (var obj in DragAndDrop.objectReferences)
                {
                    string path = AssetDatabase.GetAssetPath(obj);
                    if (AssetDatabase.IsValidFolder(path) && IsValidResourceFolder(path))
                    {
                        hasValidFolder = true;
                        break;
                    }
                }

                if (hasValidFolder)
                {
                    DragAndDrop.visualMode = DragAndDropVisualMode.Copy;

                    if (evt.type == EventType.DragPerform)
                    {
                        DragAndDrop.AcceptDrag();

                        RecordUndo("Add Resource Folders");

                        foreach (var obj in DragAndDrop.objectReferences)
                        {
                            string path = AssetDatabase.GetAssetPath(obj);
                            if (AssetDatabase.IsValidFolder(path) && IsValidResourceFolder(path))
                            {
                                string folderName = Path.GetFileName(path);

                                // Check if already exists
                                if (!ini.HasResource(folderName))
                                {
                                    ini.AddResource(folderName);
                                }
                            }
                        }

                        InvalidateResourceCache();
                    }
                }
                else
                {
                    DragAndDrop.visualMode = DragAndDropVisualMode.Rejected;
                }

                evt.Use();
                break;
        }
    }

    /// <summary>
    /// Draw resource statistics at the bottom
    /// </summary>
    private static void DrawResourceStats(MBModuleIni ini)
    {
        var loadResource = ini.resources.FindAll(r =>
            r.loadType == MBModuleIni.ResourceLoadType.LoadResource ||
            r.loadType == MBModuleIni.ResourceLoadType.LoadResourceNoFast).Count;
        var loadMod = ini.resources.FindAll(r =>
            r.loadType == MBModuleIni.ResourceLoadType.LoadModResource ||
            r.loadType == MBModuleIni.ResourceLoadType.LoadModuleResource).Count;

        // Count by status
        int foundInModule = 0, foundInNative = 0, notFound = 0;
        foreach (var r in ini.resources)
        {
            var s = GetResourceStatus(r.resourceName);
            switch (s)
            {
                case ResourceFolderStatus.InModule: foundInModule++; break;
                case ResourceFolderStatus.InNative: foundInNative++; break;
                case ResourceFolderStatus.NotFound: notFound++; break;
            }
        }

        GUILayout.Space(4);
        EditorGUILayout.BeginHorizontal();
        GUILayout.Label($"CommonRes/Module: {loadResource} | Module Only: {loadMod}",
            StylesHelpers.MiniLabel(UIColors.Cyan));
        GUILayout.FlexibleSpace();

        // Status summary with color coding
        string statusText = $"Found: {foundInModule} module, {foundInNative} native";

        if (notFound > 0)
        {
            GUILayout.Label(statusText, StylesHelpers.MiniLabel(UIColors.Cyan));
            GUILayout.Label(" | ", StylesHelpers.MiniLabel(UIColors.GrayLine));
            GUILayout.Label($"{notFound} missing", StylesHelpers.MiniLabel(UIColors.Orange));
        }
        else
        {
            GUILayout.Label(statusText, StylesHelpers.MiniLabel(UIColors.Green));
        }

        EditorGUILayout.EndHorizontal();
    }

    /// <summary>
    /// Draw status icon for resource folder validation
    /// </summary>
    private static void DrawResourceStatusIcon(ResourceFolderStatus status, string resourceName)
    {
        string icon;
        Color color;
        string tooltip;

        switch (status)
        {
            case ResourceFolderStatus.InModule:
                icon = "●";
                color = UIColors.Green;
                tooltip =
                    $"✓ Found in module Resource folder:\n{MBPathHelpers.ModResourcePath(_cachedModuleName)}/{resourceName}";
                break;
            case ResourceFolderStatus.InNative:
                icon = "●";
                color = UIColors.Cyan;
                tooltip =
                    $"✓ Found in Native Resource folder:\n{MBPathHelpers.ModResourcePath("Native")}/{resourceName}";
                break;
            case ResourceFolderStatus.NotFound:
            default:
                icon = "⚠";
                color = UIColors.Orange;
                tooltip =
                    $"✗ Resource folder not found!\nThis entry will be ignored by the game.\n\nExpected at:\n" +
                    $"• {MBPathHelpers.ModResourcePath(_cachedModuleName)}/{resourceName}\n" +
                    $"• {MBPathHelpers.ModResourcePath("Native")}/{resourceName}";
                break;
        }

        var style = new GUIStyle(EditorStyles.miniLabel)
        {
            alignment = TextAnchor.MiddleCenter,
            fontSize = 12
        };
        style.normal.textColor = color;

        GUILayout.Label(new GUIContent(icon, tooltip), style, GUILayout.Width(20));
    }

    /// <summary>
    /// Swap two resources in the list
    /// </summary>
    private static void SwapResources(MBModuleIni ini, int indexA, int indexB)
    {
        var temp = ini.resources[indexA];
        ini.resources[indexA] = ini.resources[indexB];
        ini.resources[indexB] = temp;
        EditorUtility.SetDirty(ini);
    }

    /// <summary>
    /// Move a resource to a specific order position, shifting others accordingly.
    /// </summary>
    private static void MoveResourceToOrder(MBModuleIni ini, int currentIndex, int targetOrder)
    {
        int targetIndex = Mathf.Clamp(targetOrder - 1, 0, ini.resources.Count - 1);

        if (currentIndex == targetIndex)
            return;

        RecordUndo("Set Resource Order");

        var resource = ini.resources[currentIndex];
        ini.resources.RemoveAt(currentIndex);
        ini.resources.Insert(targetIndex, resource);

        EditorUtility.SetDirty(ini);
    }

    /// <summary>
    /// Check if resource load type matches the folder location
    /// </summary>
    private static bool IsLoadTypeMismatch(MBModuleIni.ResourceLoadType loadType, ResourceFolderStatus status,
        out string warning)
    {
        warning = null;

        // Only check if we actually found the folder
        if (status == ResourceFolderStatus.NotFound)
            return false;

        bool isModuleOnly = loadType == MBModuleIni.ResourceLoadType.LoadModResource ||
                            loadType == MBModuleIni.ResourceLoadType.LoadModuleResource;

        // load_mod_resource / load_module_resource but folder is in Native
        if (isModuleOnly && status == ResourceFolderStatus.InNative)
        {
            warning = "load_module_resource only searches the module's Resource folder.\n" +
                      "This Native folder won't be found at runtime!\n\n" +
                      "Fix: Change to 'Load Resource' or move folder to module.";
            return true;
        }

        // load_resource / load_resource_nofast but folder is in Module
        if (!isModuleOnly && status == ResourceFolderStatus.InModule)
        {
            warning = "load_resource searches CommonRes first, then module's Resource folder.\n" +
                      "Since this folder is only in the module, consider using 'Load Module Resource'.\n\n" +
                      "Fix: Change to 'Load Module Resource'.";
            return true;
        }

        // load_resource / load_resource_nofast but folder is in Module (not an error, but suboptimal)
        // Actually this is fine - load_resource checks both CommonRes AND module
        // So no warning needed for this case

        return false;
    }

    /// <summary>
    /// Draw warning icon for load type mismatch
    /// </summary>
    private static void DrawLoadTypeMismatchWarning(string warning)
    {
        var style = new GUIStyle(EditorStyles.miniLabel)
        {
            alignment = TextAnchor.MiddleCenter,
            fontSize = 12
        };
        style.normal.textColor = UIColors.Red;

        GUILayout.Label(new GUIContent("⚠", warning), style, GUILayout.Width(20));
    }

    /// <summary>
    /// Draw load type popup, hiding the duplicate LoadModResource option
    /// </summary>
    private static MBModuleIni.ResourceLoadType DrawLoadTypePopup(MBModuleIni.ResourceLoadType currentType)
    {
        // Map LoadModResource to LoadModuleResource for display
        int displayIndex = currentType switch
        {
            MBModuleIni.ResourceLoadType.LoadResource => 0,
            MBModuleIni.ResourceLoadType.LoadResourceNoFast => 1,
            MBModuleIni.ResourceLoadType.LoadModResource => 2, // Show as LoadModuleResource
            MBModuleIni.ResourceLoadType.LoadModuleResource => 2,
            _ => 0
        };

        string[] displayNames =
        {
            "Load Resource", // CommonRes + Module
            "Load Resource (No Fast)", // CommonRes + Module, no fast load
            "Load Module Resource" // Module only
        };

        string[] tooltips =
        {
            "Searches CommonRes first, then module's Resource folder",
            "Same as Load Resource but skips fast-loading optimization",
            "Only searches the module's Resource folder (faster)"
        };

        EditorGUI.BeginChangeCheck();
        int newIndex = EditorGUILayout.Popup(
            new GUIContent("", tooltips[displayIndex]),
            displayIndex,
            displayNames,
            GUILayout.Width(140));

        if (EditorGUI.EndChangeCheck() && newIndex != displayIndex)
        {
            return newIndex switch
            {
                0 => MBModuleIni.ResourceLoadType.LoadResource,
                1 => MBModuleIni.ResourceLoadType.LoadResourceNoFast,
                2 => MBModuleIni.ResourceLoadType.LoadModuleResource,
                _ => currentType
            };
        }

        return currentType;
    }

    #endregion

    #region Resource Validation Cache

    /// <summary>
    /// Status of a resource folder validation
    /// </summary>
    public enum ResourceFolderStatus
    {
        NotFound, // No folder found - will be ignored
        InModule, // Found in module's Resource folder
        InNative // Found in Native module's Resource folder
    }

    private static Dictionary<string, ResourceFolderStatus> _resourceStatusCache =
        new Dictionary<string, ResourceFolderStatus>();

    private static string _cachedModuleName = null;
    private static bool _resourceCacheDirty = true;

    /// <summary>
    /// Invalidate cache when module changes or resources modified
    /// </summary>
    public static void InvalidateResourceCache()
    {
        _resourceCacheDirty = true;
    }

    /// <summary>
    /// Build/refresh resource folder status cache
    /// </summary>
    private static void RefreshResourceCache(MBModuleIni ini)
    {
        string moduleName = _currentModule.ID;

        // Check if cache needs refresh
        if (!_resourceCacheDirty && _cachedModuleName == moduleName)
            return;

        _resourceStatusCache.Clear();
        _cachedModuleName = moduleName;

        string moduleResourcePath = MBPathHelpers.ModResourcePath(moduleName);
        string nativeResourcePath = MBPathHelpers.ModResourcePath("Native");

        foreach (var resource in ini.resources)
        {
            string resourceName = resource.resourceName;

            if (_resourceStatusCache.ContainsKey(resourceName))
                continue;

            // Check module folder first
            string moduleFolder = Path.Combine(moduleResourcePath, resourceName);
            if (Directory.Exists(moduleFolder))
            {
                _resourceStatusCache[resourceName] = ResourceFolderStatus.InModule;
                continue;
            }

            // Check Native folder
            string nativeFolder = Path.Combine(nativeResourcePath, resourceName);
            if (Directory.Exists(nativeFolder))
            {
                _resourceStatusCache[resourceName] = ResourceFolderStatus.InNative;
                continue;
            }

            // Not found
            _resourceStatusCache[resourceName] = ResourceFolderStatus.NotFound;
        }

        _resourceCacheDirty = false;
    }

    /// <summary>
    /// Get cached status for a resource
    /// </summary>
    private static ResourceFolderStatus GetResourceStatus(string resourceName)
    {
        if (_resourceStatusCache.TryGetValue(resourceName, out var status))
            return status;
        return ResourceFolderStatus.NotFound;
    }

    #endregion

    #region Parameter Drawing Helpers

    private static void DrawStringParam(MBParamString param, string label, string tooltip, float width = 0)
    {
        EditorGUILayout.BeginHorizontal();
        var content = new GUIContent(label, tooltip);

        EditorGUI.BeginChangeCheck();
        string newValue;
        if (width > 0)
            newValue = EditorGUILayout.TextField(content, param.Value, GUILayout.Width(width));
        else
            newValue = EditorGUILayout.TextField(content, param.Value);

        if (EditorGUI.EndChangeCheck() && newValue != param.Value)
        {
            RecordUndo($"Change {label}");
            param.Value = newValue;
        }

        DrawUsedIndicator(param, label);
        EditorGUILayout.EndHorizontal();
    }

    private static void DrawIntParam(MBParamInt param, string label, string tooltip,
        float width = 0, int min = int.MinValue, int max = int.MaxValue)
    {
        EditorGUILayout.BeginHorizontal();
        var content = new GUIContent(label, tooltip);

        EditorGUI.BeginChangeCheck();
        int newValue;
        if (width > 0)
            newValue = EditorGUILayout.IntField(content, param.Value, GUILayout.Width(width));
        else
            newValue = EditorGUILayout.IntField(content, param.Value);

        newValue = Mathf.Clamp(newValue, min, max);

        if (EditorGUI.EndChangeCheck() && newValue != param.Value)
        {
            RecordUndo($"Change {label}");
            param.Value = newValue;
        }

        DrawUsedIndicator(param, label);
        EditorGUILayout.EndHorizontal();
    }

    private static void DrawFloatParam(MBParamFloat param, string label, string tooltip,
        float width = 0, float min = float.MinValue, float max = float.MaxValue)
    {
        EditorGUILayout.BeginHorizontal();
        var content = new GUIContent(label, tooltip);

        EditorGUI.BeginChangeCheck();
        float newValue;
        if (width > 0)
            newValue = EditorGUILayout.FloatField(content, param.Value, GUILayout.Width(width));
        else
            newValue = EditorGUILayout.FloatField(content, param.Value);

        newValue = Mathf.Clamp(newValue, min, max);

        if (EditorGUI.EndChangeCheck() && !Mathf.Approximately(newValue, param.Value))
        {
            RecordUndo($"Change {label}");
            param.Value = newValue;
        }

        DrawUsedIndicator(param, label);
        EditorGUILayout.EndHorizontal();
    }

    private static void DrawBoolParam(MBParamBool param, string label, string tooltip, float width = 0)
    {
        EditorGUILayout.BeginHorizontal();
        var content = new GUIContent(label, tooltip);

        EditorGUI.BeginChangeCheck();
        bool newValue;
        if (width > 0)
            newValue = EditorGUILayout.Toggle(content, param.Value, GUILayout.Width(width));
        else
            newValue = EditorGUILayout.Toggle(content, param.Value);

        if (EditorGUI.EndChangeCheck() && newValue != param.Value)
        {
            RecordUndo($"Change {label}");
            param.Value = newValue;
        }

        DrawUsedIndicator(param, label);
        EditorGUILayout.EndHorizontal();
    }

    /// <summary>
    /// Draws a clickable indicator that shows and toggles the IsUsed state.
    /// Uses IMBParam interface for type-agnostic access.
    /// </summary>
    private static void DrawUsedIndicator(IMBParam param, string label)
    {
        var isUsed = param.IsUsed;
        var color = isUsed ? UIColors.Green : UIColors.GrayLine;
        var hoverColor = isUsed ? UIColors.Orange : UIColors.Cyan;
        var icon = isUsed ? "●" : "○";
        var tooltip = isUsed
            ? "Parameter is SET (will be exported)\nClick to mark as unused"
            : "Using DEFAULT value (won't be exported)\nClick to mark as used";

        var style = new GUIStyle(EditorStyles.miniLabel);
        style.normal.textColor = color;
        style.hover.textColor = hoverColor;
        style.active.textColor = hoverColor;
        style.alignment = TextAnchor.MiddleCenter;
        style.fontSize = 14;

        if (GUILayout.Button(new GUIContent(icon, tooltip), style, GUILayout.Width(18), GUILayout.Height(18)))
        {
            RecordUndo($"Toggle {label} Used");
            param.IsUsed = !isUsed;
        }
    }

    /// <summary>
    /// Draws a clickable indicator for raw bool IsUsed fields (enums).
    /// Returns the new value after potential toggle.
    /// </summary>
    private static bool DrawUsedIndicatorBool(bool isUsed, string label)
    {
        var color = isUsed ? UIColors.Green : UIColors.GrayLine;
        var hoverColor = isUsed ? UIColors.Orange : UIColors.Cyan;
        var icon = isUsed ? "●" : "○";
        var tooltip = isUsed
            ? "Parameter is SET (will be exported)\nClick to mark as unused"
            : "Using DEFAULT value (won't be exported)\nClick to mark as used";

        var style = new GUIStyle(EditorStyles.miniLabel);
        style.normal.textColor = color;
        style.hover.textColor = hoverColor;
        style.active.textColor = hoverColor;
        style.alignment = TextAnchor.MiddleCenter;
        style.fontSize = 14;

        if (GUILayout.Button(new GUIContent(icon, tooltip), style, GUILayout.Width(18), GUILayout.Height(18)))
        {
            RecordUndo($"Toggle {label} Used");
            return !isUsed;
        }

        return isUsed;
    }

    /// <summary>
    /// Records an undo operation on the current target.
    /// </summary>
    private static void RecordUndo(string actionName)
    {
        if (_currentTarget != null)
        {
            Undo.RecordObject(_currentTarget, actionName);
            EditorUtility.SetDirty(_currentTarget);
        }
    }

    #endregion


    public static bool HasAndIsValidResource(MBModule currentMod, GameObject prefab)
    {
        var assetPath = AssetDatabase.GetAssetPath(prefab);

        string parent = Path.GetDirectoryName(assetPath); // 1 level up
        parent = Path.GetDirectoryName(parent); // 2 levels up
        parent = parent.Replace(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

        var pathRest = MBPathHelpers.ModResourcePath(currentMod.ID) + "/";
        var resourceName = parent.Replace(pathRest, "");

        _currentModule = currentMod;
        RefreshResourceCache(currentMod.ModuleIni);

        // check if hast status and is valid for all checks
        var status = GetResourceStatus(resourceName);
        bool hasMismatch = IsLoadTypeMismatch(MBModuleIni.ResourceLoadType.LoadModResource, status, out string mismatchWarning);
        
        return status != ResourceFolderStatus.NotFound && !hasMismatch;
    }
}