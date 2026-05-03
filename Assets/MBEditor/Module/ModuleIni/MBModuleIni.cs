using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
/// <summary>
/// ScriptableObject replication of Mount & Blade's module.ini configuration file.
/// Each parameter tracks whether it was explicitly set (IsUsed) or is just a default.
/// </summary>
[CreateAssetMenu(fileName = "MBModuleIniTracked", menuName = "MB Tools/Module INI Configuration (Tracked)")]
public class MBModuleIni : ScriptableObject
{
    #region Enums

    public enum ResourceLoadType
    {
        LoadResource,
        LoadResourceNoFast,
        LoadModResource,
        LoadModuleResource
    }

    public enum RegionType
    {
        None = -1,
        Ocean = 0,
        Mountain = 1,
        Steppe = 2,
        Plain = 3,
        Snow = 4,
        Desert = 5,
        Bridge = 7,
        River = 8,
        MountainForest = 9,
        SteppeForest = 10,
        Forest = 11,
        SnowForest = 12,
        DesertForest = 13,
        Shore = 21,
        Foam = 22,
        Waves = 23
    }

    public enum ScreenshotFormat
    {
        JPG = 0,
        PNG = 1,
        BMP = 2
    }

    #endregion

    #region Resource Entry Class

    [Serializable]
    public class ResourceEntry
    {
        public string resourceName;
        public ResourceLoadType loadType = ResourceLoadType.LoadResource;

        public ResourceEntry()
        {
        }

        public ResourceEntry(string name, ResourceLoadType type = ResourceLoadType.LoadResource)
        {
            resourceName = name;
            loadType = type;
        }

        public string GetIniKey()
        {
            return loadType switch
            {
                ResourceLoadType.LoadResource => "load_resource",
                ResourceLoadType.LoadResourceNoFast => "load_resource_nofast",
                ResourceLoadType.LoadModResource => "load_mod_resource",
                ResourceLoadType.LoadModuleResource => "load_module_resource",
                _ => "load_resource"
            };
        }
    }

    #endregion

    #region Module General Parameters - Versioning

    [Header("Module Identity")] public MBParamString moduleName = new MBParamString("Native");

    [Header("Versioning")] public MBParamInt moduleVersion = new MBParamInt(0);
    public MBParamInt compatibleModuleVersion = new MBParamInt(0);
    public MBParamInt compatibleMultiplayerVersionNo = new MBParamInt(1170);
    public MBParamInt compatibleSavegameModuleVersion = new MBParamInt(0);
    public MBParamBool compatibleWithWarband = new MBParamBool(true);
    public MBParamFloat operationSetVersion = new MBParamFloat(1.168f);

    #endregion

    #region Loading Resources

    [Header("Resource Loading")] public List<ResourceEntry> resources = new List<ResourceEntry>();

    // Track if resources section was present
    [SerializeField] private bool _resourcesUsed = false;

    public bool ResourcesUsed
    {
        get => _resourcesUsed;
        set => _resourcesUsed = value;
    }

    public MBParamBool scanModuleSounds = new MBParamBool(false);
    public MBParamBool scanModuleTextures = new MBParamBool(false);
    public MBParamBool useCaseInsensitiveMeshSearches = new MBParamBool(false);

    #endregion

    #region Enabling Content - Main Menu

    [Header("Main Menu Options")] public MBParamBool hasCustomBattle = new MBParamBool(false);
    public MBParamBool hasMultiplayer = new MBParamBool(false);
    public MBParamBool hasSinglePlayer = new MBParamBool(true);
    public MBParamBool hasTutorial = new MBParamBool(false);
    public MBParamBool enableQuickBattles = new MBParamBool(false);

    #endregion

    #region World Map Parameters

    [Header("Map Boundaries")] public MBParamInt mapMinX = new MBParamInt(-180);
    public MBParamInt mapMaxX = new MBParamInt(180);
    public MBParamInt mapMinY = new MBParamInt(-145);
    public MBParamInt mapMaxY = new MBParamInt(145);
    public MBParamFloat mapMaxDistance = new MBParamFloat(175f);

    [Header("Map Camera")] public MBParamFloat mapMinElevation = new MBParamFloat(0.2f);
    public MBParamFloat mapMaxElevation = new MBParamFloat(1.0f);

    [Header("Map Water - Sea")] public MBParamInt mapSeaDirection = new MBParamInt(-40);
    public MBParamInt mapSeaWaveRotation = new MBParamInt(300);
    public MBParamFloat mapSeaSpeedX = new MBParamFloat(0.02f);
    public MBParamFloat mapSeaSpeedY = new MBParamFloat(-0.02f);

    [Header("Map Water - River")] public MBParamInt mapRiverDirection = new MBParamInt(140);
    public MBParamFloat mapRiverSpeedX = new MBParamFloat(0.01f);
    public MBParamFloat mapRiverSpeedY = new MBParamFloat(-0.01f);

    [Header("Map Trees")] public MBParamInt mapTreeTypes = new MBParamInt(17);
    public MBParamInt mapSnowTreeTypes = new MBParamInt(3);
    public MBParamInt mapSteppeTreeTypes = new MBParamInt(5);
    public MBParamInt mapDesertTreeTypes = new MBParamInt(4);

    #endregion

    #region Time Parameters

    [Header("Time Settings")] public MBParamFloat timeMultiplier = new MBParamFloat(0.25f);

    #endregion

    #region Leveling Parameters

    [Header("Leveling - Attributes & Skills")]
    public MBParamFloat attributePointsPerLevel = new MBParamFloat(1.0f);

    public MBParamInt attributeRequiredPerSkillLevel = new MBParamInt(3);
    public MBParamInt skillPointsPerLevel = new MBParamInt(1);
    public MBParamInt weaponPointsPerLevel = new MBParamInt(10);
    public MBParamFloat levelBoundaryMultiplier = new MBParamFloat(1.0f);
    public MBParamBool canRunFasterWithSkills = new MBParamBool(false);

    [Header("Leveling - XP Multipliers")] public MBParamFloat playerXpMultiplier = new MBParamFloat(2.0f);
    public MBParamFloat heroXpMultiplier = new MBParamFloat(2.0f);
    public MBParamFloat regularsXpMultiplier = new MBParamFloat(3.0f);

    [Header("Leveling - Wounded Thresholds")]
    public MBParamInt playerWoundedThreshold = new MBParamInt(5);

    public MBParamInt heroWoundedThreshold = new MBParamInt(15);

    [Header("Leveling - Party Bonuses")] public MBParamInt skillLeadershipBonus = new MBParamInt(3);
    public MBParamInt skillPrisonerManagementBonus = new MBParamInt(5);
    public MBParamInt baseCompanionLimit = new MBParamInt(20);
    public MBParamFloat trackSpottingMultiplier = new MBParamFloat(0.8f);

    #endregion

    #region Item Parameters

    [Header("Weapon Proficiency Display")] public MBParamBool displayWpArchery = new MBParamBool(true);
    public MBParamBool displayWpCrossbows = new MBParamBool(true);
    public MBParamBool displayWpFirearms = new MBParamBool(false);
    public MBParamBool displayWpOneHanded = new MBParamBool(true);
    public MBParamBool displayWpPolearms = new MBParamBool(true);
    public MBParamBool displayWpThrowing = new MBParamBool(true);
    public MBParamBool displayWpTwoHanded = new MBParamBool(true);

    [Header("Horse Speed Modifiers")] public MBParamFloat meekModifierSpeedBonus = new MBParamFloat(0f);
    public MBParamFloat timidModifierSpeedBonus = new MBParamFloat(0f);
    public MBParamBool useCrossbowAsFirearm = new MBParamBool(false);

    #endregion

    #region Party Parameters

    [Header("Party Settings")] public MBParamBool autoComputePartyRadius = new MBParamBool(true);
    public MBParamFloat seeingRange = new MBParamFloat(6.5f);
    public MBParamBool showPartyIdsInsteadOfNames = new MBParamBool(false);
    public MBParamBool useStrictPathfindingForShips = new MBParamBool(true);

    [SerializeField] private MBParamInt _disableDisbandOnTerrainTypeValue = new MBParamInt(-1);

    public RegionType DisableDisbandOnTerrainType
    {
        get => (RegionType)_disableDisbandOnTerrainTypeValue.Value;
        set => _disableDisbandOnTerrainTypeValue.Value = (int)value;
    }

    public bool DisableDisbandOnTerrainTypeIsUsed
    {
        get => _disableDisbandOnTerrainTypeValue.IsUsed;
        set => _disableDisbandOnTerrainTypeValue.IsUsed = value;
    }

    #endregion

    #region Game Menu Parameters

    [Header("Game Menu Settings")] public MBParamBool autoCreateNoteIndices = new MBParamBool(false);
    public MBParamBool disableForceLeavingConversations = new MBParamBool(true);
    public MBParamBool showTroopUpgradesButton = new MBParamBool(false);
    public MBParamBool showQuestNotes = new MBParamBool(true);

    #endregion

    #region Combat Parameters

    [Header("Combat - Damage Thresholds")] public MBParamFloat crushThroughThreshold = new MBParamFloat(2.4f);
    public MBParamFloat damageInterruptAttackThreshold = new MBParamFloat(3.0f);
    public MBParamFloat damageInterruptAttackThresholdMp = new MBParamFloat(1.0f);

    [Header("Combat - Shield Penetration")]
    public MBParamFloat shieldPenetrationOffset = new MBParamFloat(30.0f);

    public MBParamFloat shieldPenetrationFactor = new MBParamFloat(3.0f);

    [Header("Combat - Armor Soak")] public MBParamFloat armorSoakFactorAgainstCut = new MBParamFloat(0.8f);
    public MBParamFloat armorSoakFactorAgainstPierce = new MBParamFloat(0.65f);
    public MBParamFloat armorSoakFactorAgainstBlunt = new MBParamFloat(0.5f);

    [Header("Combat - Armor Reduction")] public MBParamFloat armorReductionFactorAgainstCut = new MBParamFloat(1.0f);
    public MBParamFloat armorReductionFactorAgainstPierce = new MBParamFloat(0.5f);
    public MBParamFloat armorReductionFactorAgainstBlunt = new MBParamFloat(0.75f);

    [Header("Combat - Extra Penetration")] public MBParamFloat extraPenetrationFactorSoak = new MBParamFloat(1.0f);
    public MBParamFloat extraPenetrationFactorReduction = new MBParamFloat(1.0f);

    [Header("Combat - Damage Multipliers")]
    public MBParamFloat horseChargeDamageMultiplier = new MBParamFloat(1.0f);

    public MBParamFloat couchedLanceDamageMultiplier = new MBParamFloat(0.65f);
    public MBParamFloat fallDamageMultiplier = new MBParamFloat(1.0f);

    [Header("Combat - Damage Speed Scaling")]
    public MBParamFloat missileDamageSpeedPower = new MBParamFloat(1.9f);

    public MBParamFloat meleeDamageSpeedPower = new MBParamFloat(2.0f);

    [Header("Combat - AI & Miscellaneous")]
    public MBParamBool aiDecideDirectionAccordingToDamage = new MBParamBool(true);

    public MBParamBool applyAllAmmoDamageModifiers = new MBParamBool(true);
    public MBParamFloat braceRotationLimit = new MBParamFloat(0.012f);
    public MBParamFloat lancePikeEffectSpeed = new MBParamFloat(3.0f);
    public MBParamBool noFriendlyFireForBots = new MBParamBool(false);
    public MBParamBool considerWeaponLengthForWeaponQuality = new MBParamBool(true);

    #endregion

    #region Battle Parameters

    [Header("Battle Settings")] public MBParamInt battleSizeMin = new MBParamInt(150);
    public MBParamInt battleSizeMax = new MBParamInt(750);
    public MBParamFloat farPlaneDistance = new MBParamFloat(5000f);

    #endregion

    #region Physics Parameters

    [Header("Physics - Air Friction")] public MBParamFloat airFrictionArrow = new MBParamFloat(0.002f);
    public MBParamFloat airFrictionBullet = new MBParamFloat(0.002f);

    #endregion

    #region Scene Parameters

    [Header("Scene Settings")] public MBParamBool disableMoveableFlagOptimization = new MBParamBool(false);
    public MBParamInt missionObjectPruneTime = new MBParamInt(180);

    #endregion

    #region Feature Toggles

    [Header("Feature Toggles - Movement & Combat")]
    public MBParamBool canCrouch = new MBParamBool(false);

    public MBParamBool canReloadWhileMoving = new MBParamBool(false);
    public MBParamBool disableAttackWhileJumping = new MBParamBool(false);
    public MBParamBool disableZoom = new MBParamBool(false);

    [Header("Feature Toggles - Horses")] public MBParamBool horsesRearWithAttack = new MBParamBool(true);
    public MBParamBool horsesTryRunningAway = new MBParamBool(false);

    [Header("Feature Toggles - Scene & Effects")]
    public MBParamBool canObjectsMakeSound = new MBParamBool(false);

    public MBParamBool canUseScenePropsInSinglePlayer = new MBParamBool(false);
    public MBParamBool hasForcedParticles = new MBParamBool(false);

    [Header("Feature Toggles - Firearms & Formations")]
    public MBParamBool usePhasedReload = new MBParamBool(false);

    public MBParamBool useAdvancedFormation = new MBParamBool(false);
    public MBParamBool shorterPistolAiming = new MBParamBool(true);

    [Header("Feature Toggles - UI & Camera")]
    public MBParamBool canAdjustCameraDistance = new MBParamBool(true);

    public MBParamBool disableFoodSlot = new MBParamBool(true);
    public MBParamBool hasAccessoriesForFemale = new MBParamBool(false);
    public MBParamInt numHints = new MBParamInt(12);

    #endregion

    #region Graphical Parameters

    [Header("Graphics - Shaders")] public MBParamBool addSetNeighborsToTangentFlagToShader = new MBParamBool(true);
    public MBParamBool fixGammaOnDx7OperationColors = new MBParamBool(true);
    public MBParamBool useBorderedShadowSampler = new MBParamBool(true);

    [Header("Graphics - Effects")] public MBParamFloat bloodMultiplier = new MBParamFloat(6.0f);
    public MBParamBool disableHighHdr = new MBParamBool(false);

    [Header("Graphics - Screenshots & Hair")] [SerializeField]
    private MBParamInt _screenshotFormatValue = new MBParamInt(0);

    public ScreenshotFormat ScreenshotFileFormat
    {
        get => (ScreenshotFormat)_screenshotFormatValue.Value;
        set => _screenshotFormatValue.Value = (int)value;
    }

    public bool ScreenshotFormatIsUsed
    {
        get => _screenshotFormatValue.IsUsed;
        set => _screenshotFormatValue.IsUsed = value;
    }

    public MBParamBool limitHairColors = new MBParamBool(true);
    public MBParamBool showFactionColor = new MBParamBool(true);

    #endregion

    #region Multiplayer Parameters

    [Header("Multiplayer")] public MBParamBool multiplayerWalkEnabled = new MBParamBool(false);
    public MBParamBool restrictAttacksMoreInMultiplayer = new MBParamBool(false);
    public MBParamBool showMultiplayerGold = new MBParamBool(false);
    public MBParamBool syncBlockDirections = new MBParamBool(false);
    public MBParamBool syncRagdollEffects = new MBParamBool(false);

    #endregion

    #region Performance and Debug

    [Header("Performance & Debug")] public MBParamBool dontSupressInitialWarnings = new MBParamBool(true);
    public MBParamBool givePerformanceWarnings = new MBParamBool(false);
    public MBParamInt maximumNumberOfNotificationMessages = new MBParamInt(4);
    public MBParamBool reduceTextureLoaderMemoryUsage = new MBParamBool(false);
    public MBParamBool supportsDirectx7 = new MBParamBool(true);
    public MBParamBool useSceneUnloading = new MBParamBool(true);
    public MBParamBool useTextureDegrationCache = new MBParamBool(false);

    #endregion

    #region Savegame Parameters

    [Header("Savegame")] public MBParamBool dontLoadRegularTroopInventories = new MBParamBool(true);

    #endregion

    #region Utility Methods

    /// <summary>
    /// Resets all parameters to their defaults and marks them as unused.
    /// </summary>
    public void ResetAllToDefaults()
    {
        // Module Identity
        moduleName.Reset("Native");
        moduleVersion.Reset(0);
        compatibleModuleVersion.Reset(0);
        compatibleMultiplayerVersionNo.Reset(1170);
        compatibleSavegameModuleVersion.Reset(0);
        compatibleWithWarband.Reset(true);
        operationSetVersion.Reset(1.168f);

        // Resources
        resources.Clear();
        _resourcesUsed = false;
        scanModuleSounds.Reset(false);
        scanModuleTextures.Reset(false);
        useCaseInsensitiveMeshSearches.Reset(false);

        // Main Menu
        hasCustomBattle.Reset(false);
        hasMultiplayer.Reset(false);
        hasSinglePlayer.Reset(true);
        hasTutorial.Reset(false);
        enableQuickBattles.Reset(false);

        // Map Boundaries
        mapMinX.Reset(-180);
        mapMaxX.Reset(180);
        mapMinY.Reset(-145);
        mapMaxY.Reset(145);
        mapMaxDistance.Reset(175f);
        mapMinElevation.Reset(0.2f);
        mapMaxElevation.Reset(1.0f);

        // Map Water
        mapSeaDirection.Reset(-40);
        mapSeaWaveRotation.Reset(300);
        mapSeaSpeedX.Reset(0.02f);
        mapSeaSpeedY.Reset(-0.02f);
        mapRiverDirection.Reset(140);
        mapRiverSpeedX.Reset(0.01f);
        mapRiverSpeedY.Reset(-0.01f);

        // Map Trees
        mapTreeTypes.Reset(17);
        mapSnowTreeTypes.Reset(3);
        mapSteppeTreeTypes.Reset(5);
        mapDesertTreeTypes.Reset(4);

        // Time
        timeMultiplier.Reset(0.25f);

        // Leveling
        attributePointsPerLevel.Reset(1.0f);
        attributeRequiredPerSkillLevel.Reset(3);
        skillPointsPerLevel.Reset(1);
        weaponPointsPerLevel.Reset(10);
        levelBoundaryMultiplier.Reset(1.0f);
        canRunFasterWithSkills.Reset(false);
        playerXpMultiplier.Reset(2.0f);
        heroXpMultiplier.Reset(2.0f);
        regularsXpMultiplier.Reset(3.0f);
        playerWoundedThreshold.Reset(5);
        heroWoundedThreshold.Reset(15);
        skillLeadershipBonus.Reset(3);
        skillPrisonerManagementBonus.Reset(5);
        baseCompanionLimit.Reset(20);
        trackSpottingMultiplier.Reset(0.8f);

        // Item Display
        displayWpArchery.Reset(true);
        displayWpCrossbows.Reset(true);
        displayWpFirearms.Reset(false);
        displayWpOneHanded.Reset(true);
        displayWpPolearms.Reset(true);
        displayWpThrowing.Reset(true);
        displayWpTwoHanded.Reset(true);
        meekModifierSpeedBonus.Reset(0f);
        timidModifierSpeedBonus.Reset(0f);
        useCrossbowAsFirearm.Reset(false);

        // Party
        autoComputePartyRadius.Reset(true);
        seeingRange.Reset(6.5f);
        showPartyIdsInsteadOfNames.Reset(false);
        useStrictPathfindingForShips.Reset(true);
        _disableDisbandOnTerrainTypeValue.Reset(-1);

        // Game Menu
        autoCreateNoteIndices.Reset(false);
        disableForceLeavingConversations.Reset(true);
        showTroopUpgradesButton.Reset(false);
        showQuestNotes.Reset(true);

        // Combat
        crushThroughThreshold.Reset(2.4f);
        damageInterruptAttackThreshold.Reset(3.0f);
        damageInterruptAttackThresholdMp.Reset(1.0f);
        shieldPenetrationOffset.Reset(30.0f);
        shieldPenetrationFactor.Reset(3.0f);
        armorSoakFactorAgainstCut.Reset(0.8f);
        armorSoakFactorAgainstPierce.Reset(0.65f);
        armorSoakFactorAgainstBlunt.Reset(0.5f);
        armorReductionFactorAgainstCut.Reset(1.0f);
        armorReductionFactorAgainstPierce.Reset(0.5f);
        armorReductionFactorAgainstBlunt.Reset(0.75f);
        extraPenetrationFactorSoak.Reset(1.0f);
        extraPenetrationFactorReduction.Reset(1.0f);
        horseChargeDamageMultiplier.Reset(1.0f);
        couchedLanceDamageMultiplier.Reset(0.65f);
        fallDamageMultiplier.Reset(1.0f);
        missileDamageSpeedPower.Reset(1.9f);
        meleeDamageSpeedPower.Reset(2.0f);
        aiDecideDirectionAccordingToDamage.Reset(true);
        applyAllAmmoDamageModifiers.Reset(true);
        braceRotationLimit.Reset(0.012f);
        lancePikeEffectSpeed.Reset(3.0f);
        noFriendlyFireForBots.Reset(false);
        considerWeaponLengthForWeaponQuality.Reset(true);

        // Battle
        battleSizeMin.Reset(150);
        battleSizeMax.Reset(750);
        farPlaneDistance.Reset(5000f);

        // Physics
        airFrictionArrow.Reset(0.002f);
        airFrictionBullet.Reset(0.002f);

        // Scene
        disableMoveableFlagOptimization.Reset(false);
        missionObjectPruneTime.Reset(180);

        // Features
        canCrouch.Reset(false);
        canReloadWhileMoving.Reset(false);
        disableAttackWhileJumping.Reset(false);
        disableZoom.Reset(false);
        horsesRearWithAttack.Reset(true);
        horsesTryRunningAway.Reset(false);
        canObjectsMakeSound.Reset(false);
        canUseScenePropsInSinglePlayer.Reset(false);
        hasForcedParticles.Reset(false);
        usePhasedReload.Reset(false);
        useAdvancedFormation.Reset(false);
        shorterPistolAiming.Reset(true);
        canAdjustCameraDistance.Reset(true);
        disableFoodSlot.Reset(true);
        hasAccessoriesForFemale.Reset(false);
        numHints.Reset(12);

        // Graphics
        addSetNeighborsToTangentFlagToShader.Reset(true);
        fixGammaOnDx7OperationColors.Reset(true);
        useBorderedShadowSampler.Reset(true);
        bloodMultiplier.Reset(6.0f);
        disableHighHdr.Reset(false);
        _screenshotFormatValue.Reset(0);
        limitHairColors.Reset(true);
        showFactionColor.Reset(true);

        // Multiplayer
        multiplayerWalkEnabled.Reset(false);
        restrictAttacksMoreInMultiplayer.Reset(false);
        showMultiplayerGold.Reset(false);
        syncBlockDirections.Reset(false);
        syncRagdollEffects.Reset(false);

        // Performance
        dontSupressInitialWarnings.Reset(true);
        givePerformanceWarnings.Reset(false);
        maximumNumberOfNotificationMessages.Reset(4);
        reduceTextureLoaderMemoryUsage.Reset(false);
        supportsDirectx7.Reset(true);
        useSceneUnloading.Reset(true);
        useTextureDegrationCache.Reset(false);

        // Savegame
        dontLoadRegularTroopInventories.Reset(true);
    }

    /// <summary>
    /// Gets a list of all parameters that are currently marked as used.
    /// </summary>
    public List<string> GetUsedParameterNames()
    {
        var used = new List<string>();

        // Module General - Versioning
        if (moduleName.IsUsed) used.Add("module_name");
        if (moduleVersion.IsUsed) used.Add("module_version");
        if (compatibleModuleVersion.IsUsed) used.Add("compatible_module_version");
        if (compatibleMultiplayerVersionNo.IsUsed) used.Add("compatible_multiplayer_version_no");
        if (compatibleSavegameModuleVersion.IsUsed) used.Add("compatible_savegame_module_version");
        if (compatibleWithWarband.IsUsed) used.Add("compatible_with_warband");
        if (operationSetVersion.IsUsed) used.Add("operation_set_version");

        // Resource Loading
        if (_resourcesUsed) used.Add("resources");
        if (scanModuleSounds.IsUsed) used.Add("scan_module_sounds");
        if (scanModuleTextures.IsUsed) used.Add("scan_module_textures");
        if (useCaseInsensitiveMeshSearches.IsUsed) used.Add("use_case_insensitive_mesh_searches");

        // Enabling Content - Main Menu
        if (hasCustomBattle.IsUsed) used.Add("has_custom_battle");
        if (hasMultiplayer.IsUsed) used.Add("has_multiplayer");
        if (hasSinglePlayer.IsUsed) used.Add("has_single_player");
        if (hasTutorial.IsUsed) used.Add("has_tutorial");
        if (enableQuickBattles.IsUsed) used.Add("enable_quick_battles");

        // World Map - Map Constants
        if (mapMinX.IsUsed) used.Add("map_min_x");
        if (mapMaxX.IsUsed) used.Add("map_max_x");
        if (mapMinY.IsUsed) used.Add("map_min_y");
        if (mapMaxY.IsUsed) used.Add("map_max_y");
        if (mapMaxDistance.IsUsed) used.Add("map_max_distance");
        if (mapMinElevation.IsUsed) used.Add("map_min_elevation");
        if (mapMaxElevation.IsUsed) used.Add("map_max_elevation");

        // World Map - Water
        if (mapSeaDirection.IsUsed) used.Add("map_sea_direction");
        if (mapSeaWaveRotation.IsUsed) used.Add("map_sea_wave_rotation");
        if (mapSeaSpeedX.IsUsed) used.Add("map_sea_speed_x");
        if (mapSeaSpeedY.IsUsed) used.Add("map_sea_speed_y");
        if (mapRiverDirection.IsUsed) used.Add("map_river_direction");
        if (mapRiverSpeedX.IsUsed) used.Add("map_river_speed_x");
        if (mapRiverSpeedY.IsUsed) used.Add("map_river_speed_y");

        // World Map - Trees
        if (mapTreeTypes.IsUsed) used.Add("map_tree_types");
        if (mapSnowTreeTypes.IsUsed) used.Add("map_snow_tree_types");
        if (mapSteppeTreeTypes.IsUsed) used.Add("map_steppe_tree_types");
        if (mapDesertTreeTypes.IsUsed) used.Add("map_desert_tree_types");

        // Time Parameters
        if (timeMultiplier.IsUsed) used.Add("time_multiplier");

        // Leveling - Attributes & Skills
        if (attributePointsPerLevel.IsUsed) used.Add("attribute_points_per_level");
        if (attributeRequiredPerSkillLevel.IsUsed) used.Add("attribute_required_per_skill_level");
        if (skillPointsPerLevel.IsUsed) used.Add("skill_points_per_level");
        if (weaponPointsPerLevel.IsUsed) used.Add("weapon_points_per_level");
        if (levelBoundaryMultiplier.IsUsed) used.Add("level_boundary_multiplier");
        if (canRunFasterWithSkills.IsUsed) used.Add("can_run_faster_with_skills");

        // Leveling - XP Multipliers
        if (playerXpMultiplier.IsUsed) used.Add("player_xp_multiplier");
        if (heroXpMultiplier.IsUsed) used.Add("hero_xp_multiplier");
        if (regularsXpMultiplier.IsUsed) used.Add("regulars_xp_multiplier");

        // Leveling - Wounded Thresholds
        if (playerWoundedThreshold.IsUsed) used.Add("player_wounded_treshold");
        if (heroWoundedThreshold.IsUsed) used.Add("hero_wounded_treshold");

        // Leveling - Party Bonuses
        if (skillLeadershipBonus.IsUsed) used.Add("skill_leadership_bonus");
        if (skillPrisonerManagementBonus.IsUsed) used.Add("skill_prisoner_management_bonus");
        if (baseCompanionLimit.IsUsed) used.Add("base_companion_limit");
        if (trackSpottingMultiplier.IsUsed) used.Add("track_spotting_multiplier");

        // Item Parameters - Weapon Proficiency Display
        if (displayWpArchery.IsUsed) used.Add("display_wp_archery");
        if (displayWpCrossbows.IsUsed) used.Add("display_wp_crossbows");
        if (displayWpFirearms.IsUsed) used.Add("display_wp_firearms");
        if (displayWpOneHanded.IsUsed) used.Add("display_wp_one_handed");
        if (displayWpPolearms.IsUsed) used.Add("display_wp_polearms");
        if (displayWpThrowing.IsUsed) used.Add("display_wp_throwing");
        if (displayWpTwoHanded.IsUsed) used.Add("display_wp_two_handed");

        // Item Parameters - Horse Speed Modifiers
        if (meekModifierSpeedBonus.IsUsed) used.Add("meek_modifier_speed_bonus");
        if (timidModifierSpeedBonus.IsUsed) used.Add("timid_modifier_speed_bonus");
        if (useCrossbowAsFirearm.IsUsed) used.Add("use_crossbow_as_firearm");

        // Party Parameters
        if (autoComputePartyRadius.IsUsed) used.Add("auto_compute_party_radius");
        if (seeingRange.IsUsed) used.Add("seeing_range");
        if (showPartyIdsInsteadOfNames.IsUsed) used.Add("show_party_ids_instead_of_names");
        if (useStrictPathfindingForShips.IsUsed) used.Add("use_strict_pathfinding_for_ships");
        if (DisableDisbandOnTerrainTypeIsUsed) used.Add("disable_disband_on_terrain_type");

        // Game Menu Parameters
        if (autoCreateNoteIndices.IsUsed) used.Add("auto_create_note_indices");
        if (disableForceLeavingConversations.IsUsed) used.Add("disable_force_leaving_conversations");
        if (showTroopUpgradesButton.IsUsed) used.Add("show_troop_upgrades_button");
        if (showQuestNotes.IsUsed) used.Add("show_quest_notes");

        // Combat - Damage Thresholds
        if (crushThroughThreshold.IsUsed) used.Add("crush_through_treshold");
        if (damageInterruptAttackThreshold.IsUsed) used.Add("damage_interrupt_attack_threshold");
        if (damageInterruptAttackThresholdMp.IsUsed) used.Add("damage_interrupt_attack_threshold_mp");

        // Combat - Shield Penetration
        if (shieldPenetrationOffset.IsUsed) used.Add("shield_penetration_offset");
        if (shieldPenetrationFactor.IsUsed) used.Add("shield_penetration_factor");

        // Combat - Armor Soak
        if (armorSoakFactorAgainstCut.IsUsed) used.Add("armor_soak_factor_against_cut");
        if (armorSoakFactorAgainstPierce.IsUsed) used.Add("armor_soak_factor_against_pierce");
        if (armorSoakFactorAgainstBlunt.IsUsed) used.Add("armor_soak_factor_against_blunt");

        // Combat - Armor Reduction
        if (armorReductionFactorAgainstCut.IsUsed) used.Add("armor_reduction_factor_against_cut");
        if (armorReductionFactorAgainstPierce.IsUsed) used.Add("armor_reduction_factor_against_pierce");
        if (armorReductionFactorAgainstBlunt.IsUsed) used.Add("armor_reduction_factor_against_blunt");

        // Combat - Extra Penetration
        if (extraPenetrationFactorSoak.IsUsed) used.Add("extra_penetration_factor_soak");
        if (extraPenetrationFactorReduction.IsUsed) used.Add("extra_penetration_factor_reduction");

        // Combat - Damage Multipliers
        if (horseChargeDamageMultiplier.IsUsed) used.Add("horse_charge_damage_multiplier");
        if (couchedLanceDamageMultiplier.IsUsed) used.Add("couched_lance_damage_multiplier");
        if (fallDamageMultiplier.IsUsed) used.Add("fall_damage_multiplier");

        // Combat - Damage Speed Scaling
        if (missileDamageSpeedPower.IsUsed) used.Add("missile_damage_speed_power");
        if (meleeDamageSpeedPower.IsUsed) used.Add("melee_damage_speed_power");

        // Combat - AI & Miscellaneous
        if (aiDecideDirectionAccordingToDamage.IsUsed) used.Add("ai_decide_direction_according_to_damage");
        if (applyAllAmmoDamageModifiers.IsUsed) used.Add("apply_all_ammo_damage_modifiers");
        if (braceRotationLimit.IsUsed) used.Add("brace_rotation_limit");
        if (lancePikeEffectSpeed.IsUsed) used.Add("lance_pike_effect_speed");
        if (noFriendlyFireForBots.IsUsed) used.Add("no_friendly_fire_for_bots");
        if (considerWeaponLengthForWeaponQuality.IsUsed) used.Add("consider_weapon_length_for_weapon_quality");

        // Battle Parameters
        if (battleSizeMin.IsUsed) used.Add("battle_size_min");
        if (battleSizeMax.IsUsed) used.Add("battle_size_max");
        if (farPlaneDistance.IsUsed) used.Add("far_plane_distance");

        // Physics Parameters
        if (airFrictionArrow.IsUsed) used.Add("air_friction_arrow");
        if (airFrictionBullet.IsUsed) used.Add("air_friction_bullet");

        // Scene Parameters
        if (disableMoveableFlagOptimization.IsUsed) used.Add("disable_moveable_flag_optimization");
        if (missionObjectPruneTime.IsUsed) used.Add("mission_object_prune_time");

        // Feature Toggles - Movement & Combat
        if (canCrouch.IsUsed) used.Add("can_crouch");
        if (canReloadWhileMoving.IsUsed) used.Add("can_reload_while_moving");
        if (disableAttackWhileJumping.IsUsed) used.Add("disable_attack_while_jumping");
        if (disableZoom.IsUsed) used.Add("disable_zoom");

        // Feature Toggles - Horses
        if (horsesRearWithAttack.IsUsed) used.Add("horses_rear_with_attack");
        if (horsesTryRunningAway.IsUsed) used.Add("horses_try_running_away");

        // Feature Toggles - Scene & Effects
        if (canObjectsMakeSound.IsUsed) used.Add("can_objects_make_sound");
        if (canUseScenePropsInSinglePlayer.IsUsed) used.Add("can_use_scene_props_in_single_player");
        if (hasForcedParticles.IsUsed) used.Add("has_forced_particles");

        // Feature Toggles - Firearms & Formations
        if (usePhasedReload.IsUsed) used.Add("use_phased_reload");
        if (useAdvancedFormation.IsUsed) used.Add("use_advanced_formation");
        if (shorterPistolAiming.IsUsed) used.Add("shorter_pistol_aiming");

        // Feature Toggles - UI & Camera
        if (canAdjustCameraDistance.IsUsed) used.Add("can_adjust_camera_distance");
        if (disableFoodSlot.IsUsed) used.Add("disable_food_slot");
        if (hasAccessoriesForFemale.IsUsed) used.Add("has_accessories_for_female");
        if (numHints.IsUsed) used.Add("num_hints");

        // Graphics - Shaders
        if (addSetNeighborsToTangentFlagToShader.IsUsed) used.Add("add_set_neighbors_to_tangent_flag_to_shader");
        if (fixGammaOnDx7OperationColors.IsUsed) used.Add("fix_gamma_on_dx7_operation_colors");
        if (useBorderedShadowSampler.IsUsed) used.Add("use_bordered_shadow_sampler");

        // Graphics - Effects
        if (bloodMultiplier.IsUsed) used.Add("blood_multiplier");
        if (disableHighHdr.IsUsed) used.Add("disable_high_hdr");

        // Graphics - Screenshots & Hair
        if (ScreenshotFormatIsUsed) used.Add("screenshot_format");
        if (limitHairColors.IsUsed) used.Add("limit_hair_colors");
        if (showFactionColor.IsUsed) used.Add("show_faction_color");

        // Multiplayer
        if (multiplayerWalkEnabled.IsUsed) used.Add("multiplayer_walk_enabled");
        if (restrictAttacksMoreInMultiplayer.IsUsed) used.Add("restrict_attacks_more_in_multiplayer");
        if (showMultiplayerGold.IsUsed) used.Add("show_multiplayer_gold");
        if (syncBlockDirections.IsUsed) used.Add("sync_block_directions");
        if (syncRagdollEffects.IsUsed) used.Add("sync_ragdoll_effects");

        // Performance & Debug
        if (dontSupressInitialWarnings.IsUsed) used.Add("dont_supress_initial_warnings");
        if (givePerformanceWarnings.IsUsed) used.Add("give_performance_warnings");
        if (maximumNumberOfNotificationMessages.IsUsed) used.Add("maximum_number_of_notification_messages");
        if (reduceTextureLoaderMemoryUsage.IsUsed) used.Add("reduce_texture_loader_memory_usage");
        if (supportsDirectx7.IsUsed) used.Add("supports_directx_7");
        if (useSceneUnloading.IsUsed) used.Add("use_scene_unloading");
        if (useTextureDegrationCache.IsUsed) used.Add("use_texture_degration_cache");

        // Savegame
        if (dontLoadRegularTroopInventories.IsUsed) used.Add("dont_load_regular_troop_inventories");

        return used;
    }

    /// <summary>
    /// Gets count of used vs total parameters.
    /// </summary>
    public (int used, int total) GetUsageStats()
    {
        int used = 0;
        int total = 0;

        // Count all parameter fields using reflection or manual counting
        // For now, a simplified version:
        total = 100; // Approximate total
        used = GetUsedParameterNames().Count;

        return (used, total);
    }

    #endregion

    #region Resource Methods

    public void AddResource(string resourceName, ResourceLoadType loadType = ResourceLoadType.LoadResource)
    {
        resources.Add(new ResourceEntry(resourceName, loadType));
        _resourcesUsed = true;
    }

    public bool RemoveResource(string resourceName)
    {
        return resources.RemoveAll(r => r.resourceName == resourceName) > 0;
    }

    public bool HasResource(string resourceName)
    {
        return resources.Exists(r => r.resourceName == resourceName);
    }
    
    #endregion
}