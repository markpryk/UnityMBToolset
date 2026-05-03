using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

/// <summary>
/// Helper methods for importing/exporting MBModuleIniTracked with proper usage tracking.
/// </summary>
public static class MBModuleIniHelpers
{
    #region Import Methods

    /// <summary>
    /// Parse a module.ini file and populate the ScriptableObject.
    /// Only parameters found in the file will be marked as IsUsed = true.
    /// </summary>
    public static void ImportFromFile(MBModuleIni ini, string filePath)
    {
        if (!File.Exists(filePath))
        {
            Debug.LogError($"Module.ini file not found: {filePath}");
            return;
        }

        // Reset all to defaults first (marks everything as unused)
        ini.ResetAllToDefaults();

        string[] lines = File.ReadAllLines(filePath);

        foreach (string rawLine in lines)
        {
            string line = rawLine.Trim();

            // Skip empty lines and comments
            if (string.IsNullOrEmpty(line) || line.StartsWith("#"))
                continue;

            // Remove inline comments
            int commentIndex = line.IndexOf('#');
            if (commentIndex > 0)
                line = line.Substring(0, commentIndex).Trim();

            // Parse key = value
            int equalsIndex = line.IndexOf('=');
            if (equalsIndex < 0)
                continue;

            string key = line.Substring(0, equalsIndex).Trim().ToLowerInvariant();
            string value = line.Substring(equalsIndex + 1).Trim();

            ParseKeyValue(ini, key, value);
        }

        Debug.Log($"Imported module.ini: {ini.moduleName.Value} with {ini.resources.Count} resources");
    }

    private static void ParseKeyValue(MBModuleIni ini, string key, string value)
    {
        switch (key)
        {
            // Module General - each SetFromFile marks IsUsed = true
            case "module_name": ini.moduleName.SetFromFile(value); break;
            case "module_version": ini.moduleVersion.SetFromFile(ParseInt(value)); break;
            case "compatible_module_version": ini.compatibleModuleVersion.SetFromFile(ParseInt(value)); break;
            case "compatible_multiplayer_version_no": ini.compatibleMultiplayerVersionNo.SetFromFile(ParseInt(value)); break;
            case "compatible_savegame_module_version": ini.compatibleSavegameModuleVersion.SetFromFile(ParseInt(value)); break;
            case "compatible_with_warband": ini.compatibleWithWarband.SetFromFile(ParseBool(value)); break;
            case "operation_set_version": ini.operationSetVersion.SetFromFile(ParseFloat(value)); break;

            // Resources
            case "load_resource":
                ini.resources.Add(new MBModuleIni.ResourceEntry(value, MBModuleIni.ResourceLoadType.LoadResource));
                ini.ResourcesUsed = true;
                break;
            case "load_resource_nofast":
                ini.resources.Add(new MBModuleIni.ResourceEntry(value, MBModuleIni.ResourceLoadType.LoadResourceNoFast));
                ini.ResourcesUsed = true;
                break;
            case "load_mod_resource":
                ini.resources.Add(new MBModuleIni.ResourceEntry(value, MBModuleIni.ResourceLoadType.LoadModResource));
                ini.ResourcesUsed = true;
                break;
            case "load_module_resource":
                ini.resources.Add(new MBModuleIni.ResourceEntry(value, MBModuleIni.ResourceLoadType.LoadModuleResource));
                ini.ResourcesUsed = true;
                break;

            case "scan_module_sounds": ini.scanModuleSounds.SetFromFile(ParseBool(value)); break;
            case "scan_module_textures": ini.scanModuleTextures.SetFromFile(ParseBool(value)); break;
            case "use_case_insensitive_mesh_searches": ini.useCaseInsensitiveMeshSearches.SetFromFile(ParseBool(value)); break;

            // Content
            case "has_custom_battle": ini.hasCustomBattle.SetFromFile(ParseBool(value)); break;
            case "has_multiplayer": ini.hasMultiplayer.SetFromFile(ParseBool(value)); break;
            case "has_single_player": ini.hasSinglePlayer.SetFromFile(ParseBool(value)); break;
            case "has_tutorial": ini.hasTutorial.SetFromFile(ParseBool(value)); break;
            case "enable_quick_battles": ini.enableQuickBattles.SetFromFile(ParseBool(value)); break;

            // Map
            case "map_min_x": ini.mapMinX.SetFromFile(ParseInt(value)); break;
            case "map_max_x": ini.mapMaxX.SetFromFile(ParseInt(value)); break;
            case "map_min_y": ini.mapMinY.SetFromFile(ParseInt(value)); break;
            case "map_max_y": ini.mapMaxY.SetFromFile(ParseInt(value)); break;
            case "map_max_distance": ini.mapMaxDistance.SetFromFile(ParseFloat(value)); break;
            case "map_min_elevation": ini.mapMinElevation.SetFromFile(ParseFloat(value)); break;
            case "map_max_elevation": ini.mapMaxElevation.SetFromFile(ParseFloat(value)); break;
            case "map_sea_direction": ini.mapSeaDirection.SetFromFile(ParseInt(value)); break;
            case "map_sea_wave_rotation": ini.mapSeaWaveRotation.SetFromFile(ParseInt(value)); break;
            case "map_sea_speed_x": ini.mapSeaSpeedX.SetFromFile(ParseFloat(value)); break;
            case "map_sea_speed_y": ini.mapSeaSpeedY.SetFromFile(ParseFloat(value)); break;
            case "map_river_direction": ini.mapRiverDirection.SetFromFile(ParseInt(value)); break;
            case "map_river_speed_x": ini.mapRiverSpeedX.SetFromFile(ParseFloat(value)); break;
            case "map_river_speed_y": ini.mapRiverSpeedY.SetFromFile(ParseFloat(value)); break;
            case "map_tree_types": ini.mapTreeTypes.SetFromFile(ParseInt(value)); break;
            case "map_snow_tree_types": ini.mapSnowTreeTypes.SetFromFile(ParseInt(value)); break;
            case "map_steppe_tree_types": ini.mapSteppeTreeTypes.SetFromFile(ParseInt(value)); break;
            case "map_desert_tree_types": ini.mapDesertTreeTypes.SetFromFile(ParseInt(value)); break;

            // Time
            case "time_multiplier": ini.timeMultiplier.SetFromFile(ParseFloat(value)); break;

            // Leveling
            case "attribute_points_per_level": ini.attributePointsPerLevel.SetFromFile(ParseFloat(value)); break;
            case "attribute_required_per_skill_level": ini.attributeRequiredPerSkillLevel.SetFromFile(ParseInt(value)); break;
            case "skill_points_per_level": ini.skillPointsPerLevel.SetFromFile(ParseInt(value)); break;
            case "weapon_points_per_level": ini.weaponPointsPerLevel.SetFromFile(ParseInt(value)); break;
            case "level_boundary_multiplier": ini.levelBoundaryMultiplier.SetFromFile(ParseFloat(value)); break;
            case "can_run_faster_with_skills": ini.canRunFasterWithSkills.SetFromFile(ParseBool(value)); break;
            case "player_xp_multiplier": ini.playerXpMultiplier.SetFromFile(ParseFloat(value)); break;
            case "hero_xp_multiplier": ini.heroXpMultiplier.SetFromFile(ParseFloat(value)); break;
            case "regulars_xp_multiplier": ini.regularsXpMultiplier.SetFromFile(ParseFloat(value)); break;
            case "player_wounded_treshold": ini.playerWoundedThreshold.SetFromFile(ParseInt(value)); break;
            case "hero_wounded_treshold": ini.heroWoundedThreshold.SetFromFile(ParseInt(value)); break;
            case "skill_leadership_bonus": ini.skillLeadershipBonus.SetFromFile(ParseInt(value)); break;
            case "skill_prisoner_management_bonus": ini.skillPrisonerManagementBonus.SetFromFile(ParseInt(value)); break;
            case "base_companion_limit": ini.baseCompanionLimit.SetFromFile(ParseInt(value)); break;
            case "track_spotting_multiplier": ini.trackSpottingMultiplier.SetFromFile(ParseFloat(value)); break;

            // Item Display
            case "display_wp_archery": ini.displayWpArchery.SetFromFile(ParseBool(value)); break;
            case "display_wp_crossbows": ini.displayWpCrossbows.SetFromFile(ParseBool(value)); break;
            case "display_wp_firearms": ini.displayWpFirearms.SetFromFile(ParseBool(value)); break;
            case "display_wp_one_handed": ini.displayWpOneHanded.SetFromFile(ParseBool(value)); break;
            case "display_wp_polearms": ini.displayWpPolearms.SetFromFile(ParseBool(value)); break;
            case "display_wp_throwing": ini.displayWpThrowing.SetFromFile(ParseBool(value)); break;
            case "display_wp_two_handed": ini.displayWpTwoHanded.SetFromFile(ParseBool(value)); break;
            case "meek_modifier_speed_bonus": ini.meekModifierSpeedBonus.SetFromFile(ParseFloat(value)); break;
            case "timid_modifier_speed_bonus": ini.timidModifierSpeedBonus.SetFromFile(ParseFloat(value)); break;
            case "use_crossbow_as_firearm": ini.useCrossbowAsFirearm.SetFromFile(ParseBool(value)); break;

            // Party
            case "auto_compute_party_radius": ini.autoComputePartyRadius.SetFromFile(ParseBool(value)); break;
            case "seeing_range": ini.seeingRange.SetFromFile(ParseFloat(value)); break;
            case "show_party_ids_instead_of_names": ini.showPartyIdsInsteadOfNames.SetFromFile(ParseBool(value)); break;
            case "use_strict_pathfinding_for_ships": ini.useStrictPathfindingForShips.SetFromFile(ParseBool(value)); break;
            case "disable_disband_on_terrain_type":
                int terrainVal = ParseInt(value);
                ini.DisableDisbandOnTerrainType = terrainVal >= 0 
                    ? (MBModuleIni.RegionType)terrainVal 
                    : MBModuleIni.RegionType.None;
                ini.DisableDisbandOnTerrainTypeIsUsed = true;
                break;

            // Game Menu
            case "auto_create_note_indices": ini.autoCreateNoteIndices.SetFromFile(ParseBool(value)); break;
            case "disable_force_leaving_conversations": ini.disableForceLeavingConversations.SetFromFile(ParseBool(value)); break;
            case "show_troop_upgrades_button": ini.showTroopUpgradesButton.SetFromFile(ParseBool(value)); break;
            case "show_quest_notes": ini.showQuestNotes.SetFromFile(ParseBool(value)); break;

            // Combat
            case "crush_through_treshold": ini.crushThroughThreshold.SetFromFile(ParseFloat(value)); break;
            case "damage_interrupt_attack_threshold": ini.damageInterruptAttackThreshold.SetFromFile(ParseFloat(value)); break;
            case "damage_interrupt_attack_threshold_mp": ini.damageInterruptAttackThresholdMp.SetFromFile(ParseFloat(value)); break;
            case "shield_penetration_offset": ini.shieldPenetrationOffset.SetFromFile(ParseFloat(value)); break;
            case "shield_penetration_factor": ini.shieldPenetrationFactor.SetFromFile(ParseFloat(value)); break;
            case "armor_soak_factor_against_cut": ini.armorSoakFactorAgainstCut.SetFromFile(ParseFloat(value)); break;
            case "armor_soak_factor_against_pierce": ini.armorSoakFactorAgainstPierce.SetFromFile(ParseFloat(value)); break;
            case "armor_soak_factor_against_blunt": ini.armorSoakFactorAgainstBlunt.SetFromFile(ParseFloat(value)); break;
            case "armor_reduction_factor_against_cut": ini.armorReductionFactorAgainstCut.SetFromFile(ParseFloat(value)); break;
            case "armor_reduction_factor_against_pierce": ini.armorReductionFactorAgainstPierce.SetFromFile(ParseFloat(value)); break;
            case "armor_reduction_factor_against_blunt": ini.armorReductionFactorAgainstBlunt.SetFromFile(ParseFloat(value)); break;
            case "extra_penetration_factor_soak": ini.extraPenetrationFactorSoak.SetFromFile(ParseFloat(value)); break;
            case "extra_penetration_factor_reduction": ini.extraPenetrationFactorReduction.SetFromFile(ParseFloat(value)); break;
            case "horse_charge_damage_multiplier": ini.horseChargeDamageMultiplier.SetFromFile(ParseFloat(value)); break;
            case "couched_lance_damage_multiplier": ini.couchedLanceDamageMultiplier.SetFromFile(ParseFloat(value)); break;
            case "fall_damage_multiplier": ini.fallDamageMultiplier.SetFromFile(ParseFloat(value)); break;
            case "missile_damage_speed_power": ini.missileDamageSpeedPower.SetFromFile(ParseFloat(value)); break;
            case "melee_damage_speed_power": ini.meleeDamageSpeedPower.SetFromFile(ParseFloat(value)); break;
            case "ai_decide_direction_according_to_damage": ini.aiDecideDirectionAccordingToDamage.SetFromFile(ParseBool(value)); break;
            case "apply_all_ammo_damage_modifiers": ini.applyAllAmmoDamageModifiers.SetFromFile(ParseBool(value)); break;
            case "brace_rotation_limit": ini.braceRotationLimit.SetFromFile(ParseFloat(value)); break;
            case "lance_pike_effect_speed": ini.lancePikeEffectSpeed.SetFromFile(ParseFloat(value)); break;
            case "no_friendly_fire_for_bots": ini.noFriendlyFireForBots.SetFromFile(ParseBool(value)); break;
            case "consider_weapon_length_for_weapon_quality": ini.considerWeaponLengthForWeaponQuality.SetFromFile(ParseBool(value)); break;

            // Battle
            case "battle_size_min": ini.battleSizeMin.SetFromFile(ParseInt(value)); break;
            case "battle_size_max": ini.battleSizeMax.SetFromFile(ParseInt(value)); break;
            case "far_plane_distance": ini.farPlaneDistance.SetFromFile(ParseFloat(value)); break;

            // Physics
            case "air_friction_arrow": ini.airFrictionArrow.SetFromFile(ParseFloat(value)); break;
            case "air_friction_bullet": ini.airFrictionBullet.SetFromFile(ParseFloat(value)); break;

            // Scene
            case "disable_moveable_flag_optimization": ini.disableMoveableFlagOptimization.SetFromFile(ParseBool(value)); break;
            case "mission_object_prune_time": ini.missionObjectPruneTime.SetFromFile(ParseInt(value)); break;

            // Features
            case "can_crouch": ini.canCrouch.SetFromFile(ParseBool(value)); break;
            case "can_objects_make_sound": ini.canObjectsMakeSound.SetFromFile(ParseBool(value)); break;
            case "can_reload_while_moving": ini.canReloadWhileMoving.SetFromFile(ParseBool(value)); break;
            case "can_use_scene_props_in_single_player": ini.canUseScenePropsInSinglePlayer.SetFromFile(ParseBool(value)); break;
            case "disable_food_slot": ini.disableFoodSlot.SetFromFile(ParseBool(value)); break;
            case "has_forced_particles": ini.hasForcedParticles.SetFromFile(ParseBool(value)); break;
            case "horses_rear_with_attack": ini.horsesRearWithAttack.SetFromFile(ParseBool(value)); break;
            case "horses_try_running_away": ini.horsesTryRunningAway.SetFromFile(ParseBool(value)); break;
            case "num_hints": ini.numHints.SetFromFile(ParseInt(value)); break;
            case "shorter_pistol_aiming": ini.shorterPistolAiming.SetFromFile(ParseBool(value)); break;
            case "use_advanced_formation": ini.useAdvancedFormation.SetFromFile(ParseBool(value)); break;
            case "use_phased_reload": ini.usePhasedReload.SetFromFile(ParseBool(value)); break;
            case "can_adjust_camera_distance": ini.canAdjustCameraDistance.SetFromFile(ParseBool(value)); break;
            case "disable_zoom": ini.disableZoom.SetFromFile(ParseBool(value)); break;
            case "disable_attack_while_jumping": ini.disableAttackWhileJumping.SetFromFile(ParseBool(value)); break;
            case "has_accessories_for_female": ini.hasAccessoriesForFemale.SetFromFile(ParseBool(value)); break;

            // Graphics
            case "add_set_neighbors_to_tangent_flag_to_shader": ini.addSetNeighborsToTangentFlagToShader.SetFromFile(ParseBool(value)); break;
            case "blood_multiplier": ini.bloodMultiplier.SetFromFile(ParseFloat(value)); break;
            case "disable_high_hdr": ini.disableHighHdr.SetFromFile(ParseBool(value)); break;
            case "fix_gamma_on_dx7_operation_colors": ini.fixGammaOnDx7OperationColors.SetFromFile(ParseBool(value)); break;
            case "use_bordered_shadow_sampler": ini.useBorderedShadowSampler.SetFromFile(ParseBool(value)); break;
            case "screenshot_format":
                ini.ScreenshotFileFormat = (MBModuleIni.ScreenshotFormat)ParseInt(value);
                ini.ScreenshotFormatIsUsed = true;
                break;
            case "limit_hair_colors": ini.limitHairColors.SetFromFile(ParseBool(value)); break;
            case "show_faction_color": ini.showFactionColor.SetFromFile(ParseBool(value)); break;

            // Multiplayer
            case "multiplayer_walk_enabled": ini.multiplayerWalkEnabled.SetFromFile(ParseBool(value)); break;
            case "restrict_attacks_more_in_multiplayer": ini.restrictAttacksMoreInMultiplayer.SetFromFile(ParseBool(value)); break;
            case "show_multiplayer_gold": ini.showMultiplayerGold.SetFromFile(ParseBool(value)); break;
            case "sync_block_directions": ini.syncBlockDirections.SetFromFile(ParseBool(value)); break;
            case "sync_ragdoll_effects": ini.syncRagdollEffects.SetFromFile(ParseBool(value)); break;

            // Performance
            case "dont_supress_initial_warnings": ini.dontSupressInitialWarnings.SetFromFile(ParseBool(value)); break;
            case "give_performance_warnings": ini.givePerformanceWarnings.SetFromFile(ParseBool(value)); break;
            case "maximum_number_of_notification_messages": ini.maximumNumberOfNotificationMessages.SetFromFile(ParseInt(value)); break;
            case "reduce_texture_loader_memory_usage": ini.reduceTextureLoaderMemoryUsage.SetFromFile(ParseBool(value)); break;
            case "supports_directx_7": ini.supportsDirectx7.SetFromFile(ParseBool(value)); break;
            case "use_scene_unloading": ini.useSceneUnloading.SetFromFile(ParseBool(value)); break;
            case "use_texture_degration_cache": ini.useTextureDegrationCache.SetFromFile(ParseBool(value)); break;

            // Savegame
            case "dont_load_regular_troop_inventories": ini.dontLoadRegularTroopInventories.SetFromFile(ParseBool(value)); break;
        }
    }

    #endregion

    #region Export Methods

    /// <summary>
    /// Export configuration to a module.ini file.
    /// </summary>
    /// <param name="ini">The configuration to export</param>
    /// <param name="filePath">Output file path</param>
    /// <param name="onlyUsed">If true, only export parameters that are marked as used</param>
    public static void ExportToFile(MBModuleIni ini, string filePath, bool onlyUsed = false)
    {
        StringBuilder sb = new StringBuilder();

        // Module General
        WriteParam(sb, "module_name", ini.moduleName, onlyUsed);
        WriteParam(sb, "compatible_with_warband", ini.compatibleWithWarband, onlyUsed);
        WriteParam(sb, "compatible_multiplayer_version_no", ini.compatibleMultiplayerVersionNo, onlyUsed);
        sb.AppendLine();

        WriteParam(sb, "supports_directx_7", ini.supportsDirectx7, onlyUsed);
        WriteParam(sb, "reduce_texture_loader_memory_usage", ini.reduceTextureLoaderMemoryUsage, onlyUsed);
        WriteParam(sb, "use_case_insensitive_mesh_searches", ini.useCaseInsensitiveMeshSearches, onlyUsed);
        WriteParam(sb, "use_texture_degration_cache", ini.useTextureDegrationCache, onlyUsed);
        sb.AppendLine();

        WriteParam(sb, "num_hints", ini.numHints, onlyUsed);
        WriteParam(sb, "auto_create_note_indices", ini.autoCreateNoteIndices, onlyUsed);
        sb.AppendLine();

        // Map boundaries
        WriteParam(sb, "map_min_x", ini.mapMinX, onlyUsed);
        WriteParam(sb, "map_max_x", ini.mapMaxX, onlyUsed);
        WriteParam(sb, "map_min_y", ini.mapMinY, onlyUsed);
        WriteParam(sb, "map_max_y", ini.mapMaxY, onlyUsed);
        WriteParam(sb, "map_sea_direction", ini.mapSeaDirection, onlyUsed);
        WriteParam(sb, "map_sea_wave_rotation", ini.mapSeaWaveRotation, onlyUsed);
        WriteParamFloat(sb, "map_sea_speed_x", ini.mapSeaSpeedX, onlyUsed);
        WriteParamFloat(sb, "map_sea_speed_y", ini.mapSeaSpeedY, onlyUsed);
        WriteParam(sb, "map_river_direction", ini.mapRiverDirection, onlyUsed);
        WriteParamFloat(sb, "map_river_speed_x", ini.mapRiverSpeedX, onlyUsed);
        WriteParamFloat(sb, "map_river_speed_y", ini.mapRiverSpeedY, onlyUsed);
        sb.AppendLine();

        // Physics
        WriteParamFloat(sb, "air_friction_arrow", ini.airFrictionArrow, onlyUsed);
        WriteParamFloat(sb, "air_friction_bullet", ini.airFrictionBullet, onlyUsed);
        sb.AppendLine();

        // Tree types
        WriteParam(sb, "map_tree_types", ini.mapTreeTypes, onlyUsed);
        WriteParam(sb, "map_snow_tree_types", ini.mapSnowTreeTypes, onlyUsed);
        WriteParam(sb, "map_steppe_tree_types", ini.mapSteppeTreeTypes, onlyUsed);
        WriteParam(sb, "map_desert_tree_types", ini.mapDesertTreeTypes, onlyUsed);
        sb.AppendLine();

        WriteParamFloat(sb, "map_max_distance", ini.mapMaxDistance, onlyUsed);
        WriteParam(sb, "has_tutorial", ini.hasTutorial, onlyUsed);
        sb.AppendLine();

        WriteParamFloat(sb, "time_multiplier", ini.timeMultiplier, onlyUsed);
        WriteParamFloat(sb, "seeing_range", ini.seeingRange, onlyUsed);
        WriteParamFloat(sb, "track_spotting_multiplier", ini.trackSpottingMultiplier, onlyUsed);
        sb.AppendLine();

        // Wounded thresholds
        WriteParam(sb, "player_wounded_treshold", ini.playerWoundedThreshold, onlyUsed);
        WriteParam(sb, "hero_wounded_treshold", ini.heroWoundedThreshold, onlyUsed);
        sb.AppendLine();

        // Skills
        WriteParam(sb, "skill_prisoner_management_bonus", ini.skillPrisonerManagementBonus, onlyUsed);
        WriteParam(sb, "skill_leadership_bonus", ini.skillLeadershipBonus, onlyUsed);
        WriteParam(sb, "base_companion_limit", ini.baseCompanionLimit, onlyUsed);
        sb.AppendLine();

        // XP
        WriteParamFloat(sb, "player_xp_multiplier", ini.playerXpMultiplier, onlyUsed);
        WriteParamFloat(sb, "hero_xp_multiplier", ini.heroXpMultiplier, onlyUsed);
        WriteParamFloat(sb, "regulars_xp_multiplier", ini.regularsXpMultiplier, onlyUsed);
        sb.AppendLine();

        WriteParam(sb, "display_wp_firearms", ini.displayWpFirearms, onlyUsed);
        sb.AppendLine();

        // Damage thresholds
        WriteParamFloat(sb, "damage_interrupt_attack_threshold", ini.damageInterruptAttackThreshold, onlyUsed);
        WriteParamFloat(sb, "damage_interrupt_attack_threshold_mp", ini.damageInterruptAttackThresholdMp, onlyUsed);
        sb.AppendLine();

        // Extra penetration
        WriteParamFloat(sb, "extra_penetration_factor_soak", ini.extraPenetrationFactorSoak, onlyUsed);
        WriteParamFloat(sb, "extra_penetration_factor_reduction", ini.extraPenetrationFactorReduction, onlyUsed);
        sb.AppendLine();

        // Armor factors
        WriteParamFloat(sb, "armor_soak_factor_against_cut", ini.armorSoakFactorAgainstCut, onlyUsed);
        WriteParamFloat(sb, "armor_soak_factor_against_pierce", ini.armorSoakFactorAgainstPierce, onlyUsed);
        WriteParamFloat(sb, "armor_soak_factor_against_blunt", ini.armorSoakFactorAgainstBlunt, onlyUsed);
        sb.AppendLine();
        WriteParamFloat(sb, "armor_reduction_factor_against_cut", ini.armorReductionFactorAgainstCut, onlyUsed);
        WriteParamFloat(sb, "armor_reduction_factor_against_pierce", ini.armorReductionFactorAgainstPierce, onlyUsed);
        WriteParamFloat(sb, "armor_reduction_factor_against_blunt", ini.armorReductionFactorAgainstBlunt, onlyUsed);
        sb.AppendLine();

        // Damage multipliers
        WriteParamFloat(sb, "horse_charge_damage_multiplier", ini.horseChargeDamageMultiplier, onlyUsed);
        WriteParamFloat(sb, "couched_lance_damage_multiplier", ini.couchedLanceDamageMultiplier, onlyUsed);
        WriteParamFloat(sb, "fall_damage_multiplier", ini.fallDamageMultiplier, onlyUsed);
        sb.AppendLine();

        // Shield penetration
        WriteParamFloat(sb, "shield_penetration_offset", ini.shieldPenetrationOffset, onlyUsed);
        WriteParamFloat(sb, "shield_penetration_factor", ini.shieldPenetrationFactor, onlyUsed);
        sb.AppendLine();

        // Speed power
        WriteParamFloat(sb, "missile_damage_speed_power", ini.missileDamageSpeedPower, onlyUsed);
        WriteParamFloat(sb, "melee_damage_speed_power", ini.meleeDamageSpeedPower, onlyUsed);
        sb.AppendLine();

        WriteParam(sb, "multiplayer_walk_enabled", ini.multiplayerWalkEnabled, onlyUsed);
        WriteParam(sb, "mission_object_prune_time", ini.missionObjectPruneTime, onlyUsed);
        WriteParam(sb, "disable_food_slot", ini.disableFoodSlot, onlyUsed);
        sb.AppendLine();

        WriteParam(sb, "scan_module_textures", ini.scanModuleTextures, onlyUsed);
        WriteParam(sb, "scan_module_sounds", ini.scanModuleSounds, onlyUsed);
        sb.AppendLine();

        // Export resources if used (or if not filtering by used)
        if (!onlyUsed || ini.ResourcesUsed)
        {
            foreach (var entry in ini.resources)
            {
                sb.AppendLine($"{entry.GetIniKey()} = {entry.resourceName}");
            }
            sb.AppendLine();
        }

        // Remaining settings
        WriteParam(sb, "limit_hair_colors", ini.limitHairColors, onlyUsed);
        WriteParam(sb, "show_faction_color", ini.showFactionColor, onlyUsed);
        WriteParam(sb, "show_quest_notes", ini.showQuestNotes, onlyUsed);
        WriteParam(sb, "dont_load_regular_troop_inventories", ini.dontLoadRegularTroopInventories, onlyUsed);
        WriteParam(sb, "disable_moveable_flag_optimization", ini.disableMoveableFlagOptimization, onlyUsed);
        WriteParam(sb, "show_party_ids_instead_of_names", ini.showPartyIdsInsteadOfNames, onlyUsed);
        WriteParamFloat(sb, "crush_through_treshold", ini.crushThroughThreshold, onlyUsed);
        sb.AppendLine();

        // Feature toggles
        WriteParam(sb, "can_crouch", ini.canCrouch, onlyUsed);
        WriteParam(sb, "can_objects_make_sound", ini.canObjectsMakeSound, onlyUsed);
        WriteParam(sb, "disable_zoom", ini.disableZoom, onlyUsed);
        WriteParam(sb, "use_advanced_formation", ini.useAdvancedFormation, onlyUsed);
        WriteParam(sb, "use_crossbow_as_firearm", ini.useCrossbowAsFirearm, onlyUsed);
        WriteParam(sb, "can_reload_while_moving", ini.canReloadWhileMoving, onlyUsed);
        WriteParam(sb, "can_run_faster_with_skills", ini.canRunFasterWithSkills, onlyUsed);
        WriteParam(sb, "use_phased_reload", ini.usePhasedReload, onlyUsed);
        WriteParam(sb, "horses_try_running_away", ini.horsesTryRunningAway, onlyUsed);
        WriteParam(sb, "horses_rear_with_attack", ini.horsesRearWithAttack, onlyUsed);
        WriteParamFloat(sb, "lance_pike_effect_speed", ini.lancePikeEffectSpeed, onlyUsed);
        WriteParam(sb, "no_friendly_fire_for_bots", ini.noFriendlyFireForBots, onlyUsed);
        WriteParam(sb, "can_adjust_camera_distance", ini.canAdjustCameraDistance, onlyUsed);
        WriteParam(sb, "sync_ragdoll_effects", ini.syncRagdollEffects, onlyUsed);
        WriteParam(sb, "has_forced_particles", ini.hasForcedParticles, onlyUsed);
        WriteParam(sb, "can_use_scene_props_in_single_player", ini.canUseScenePropsInSinglePlayer, onlyUsed);
        WriteParam(sb, "disable_attack_while_jumping", ini.disableAttackWhileJumping, onlyUsed);
        WriteParam(sb, "disable_high_hdr", ini.disableHighHdr, onlyUsed);
        WriteParam(sb, "has_accessories_for_female", ini.hasAccessoriesForFemale, onlyUsed);
        WriteParam(sb, "restrict_attacks_more_in_multiplayer", ini.restrictAttacksMoreInMultiplayer, onlyUsed);

        File.WriteAllText(filePath, sb.ToString());
        Debug.Log($"Exported module.ini to: {filePath}");
    }

    #endregion

    #region Helper Methods

    private static void WriteParam(StringBuilder sb, string key, MBParamString param, bool onlyUsed)
    {
        if (!onlyUsed || param.IsUsed)
            sb.AppendLine($"{key} = {param.Value}");
    }

    private static void WriteParam(StringBuilder sb, string key, MBParamInt param, bool onlyUsed)
    {
        if (!onlyUsed || param.IsUsed)
            sb.AppendLine($"{key} = {param.Value}");
    }

    private static void WriteParam(StringBuilder sb, string key, MBParamBool param, bool onlyUsed)
    {
        if (!onlyUsed || param.IsUsed)
            sb.AppendLine($"{key} = {BoolToInt(param.Value)}");
    }

    private static void WriteParamFloat(StringBuilder sb, string key, MBParamFloat param, bool onlyUsed)
    {
        if (!onlyUsed || param.IsUsed)
            sb.AppendLine($"{key} = {F(param.Value)}");
    }

    private static int ParseInt(string value)
    {
        if (int.TryParse(value, out int result))
            return result;
        return 0;
    }

    private static float ParseFloat(string value)
    {
        if (float.TryParse(value, System.Globalization.NumberStyles.Float,
            System.Globalization.CultureInfo.InvariantCulture, out float result))
            return result;
        return 0f;
    }

    private static bool ParseBool(string value)
    {
        return value == "1" || value.ToLowerInvariant() == "true";
    }

    private static int BoolToInt(bool value) => value ? 1 : 0;

    private static string F(float value) => value.ToString(System.Globalization.CultureInfo.InvariantCulture);

    #endregion

    #region Validation

    /// <summary>
    /// Validate the configuration and return any warnings.
    /// </summary>
    public static List<string> Validate(MBModuleIni ini)
    {
        List<string> warnings = new List<string>();

        if (ini.moduleName.IsUsed && string.IsNullOrEmpty(ini.moduleName.Value))
            warnings.Add("Module name is empty");

        if (ini.mapMinX.IsUsed && ini.mapMaxX.IsUsed && ini.mapMinX.Value >= ini.mapMaxX.Value)
            warnings.Add("Map X bounds are invalid (min >= max)");

        if (ini.mapMinY.IsUsed && ini.mapMaxY.IsUsed && ini.mapMinY.Value >= ini.mapMaxY.Value)
            warnings.Add("Map Y bounds are invalid (min >= max)");

        if (ini.mapTreeTypes.IsUsed && (ini.mapTreeTypes.Value < 1 || ini.mapTreeTypes.Value > 64))
            warnings.Add($"Map tree types ({ini.mapTreeTypes.Value}) should be between 1-64");

        if (ini.timeMultiplier.IsUsed && ini.timeMultiplier.Value <= 0)
            warnings.Add("Time multiplier should be positive");

        if (ini.seeingRange.IsUsed && ini.seeingRange.Value <= 0)
            warnings.Add("Seeing range should be positive");

        if (ini.ResourcesUsed && ini.resources.Count == 0)
            warnings.Add("Resources marked as used but no resources configured");

        return warnings;
    }

    #endregion
}