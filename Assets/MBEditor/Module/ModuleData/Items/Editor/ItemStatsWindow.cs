using System;
using System.Collections.Generic;
using System.Numerics;
using MountAndBlade.Data;
using UnityEngine;
using UnityEditor;

namespace MountAndBlade.Editor
{
    public class ItemStatsWindow : EditorWindow
    {
        private MBItemData targetItem;
        private UnityEngine.Vector2 scrollPosition;
        private string lastEncodedValue = ""; // Track what we last encoded

        // Editing state
        private ItemStatDecoder.ItemStats currentStats;

        // UI Styles
        private GUIStyle headerStyle;
        private GUIStyle subHeaderStyle;
        private GUIStyle warningStyle;
        private GUIStyle sectionStyle;

        // View options
        private bool debugMode = true;
        public static void ShowWindow(MBItemData item)
        {
            var window = GetWindow<ItemStatsWindow>("Item Stats Editor");
            window.targetItem = item;
            window.minSize = new UnityEngine.Vector2(700, 800);
            window.LoadCurrentStats();
        }

        private void OnEnable()
        {
            InitializeStyles();
            if (targetItem != null)
            {
                LoadCurrentStats();
            }
        }

        private void InitializeStyles()
        {
            headerStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 16,
                alignment = TextAnchor.MiddleCenter
            };

            subHeaderStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 12
            };

            warningStyle = new GUIStyle(EditorStyles.label)
            {
                normal = { textColor = new Color(1f, 0.5f, 0f) },
                fontStyle = FontStyle.Bold
            };

            sectionStyle = new GUIStyle(EditorStyles.helpBox)
            {
                padding = new RectOffset(10, 10, 10, 10)
            };
        }

        private void LoadCurrentStats()
        {
            if (string.IsNullOrEmpty(targetItem.Stats))
            {
                currentStats = CreateDefaultStats(targetItem.ItemType);
                lastEncodedValue = "";
            }
            else
            {
                try
                {
                    BigInteger statValue = BigInteger.Parse(targetItem.Stats);
                    currentStats = ItemStatDecoder.DecodeAllStats(statValue, targetItem.ItemType);
                    lastEncodedValue = targetItem.Stats;
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"Failed to parse stats for {targetItem.name}: {ex.Message}");
                    currentStats = CreateDefaultStats(targetItem.ItemType);
                    lastEncodedValue = "";
                }
            }
        }

        private ItemStatDecoder.ItemStats CreateDefaultStats(string itemType)
        {
            return new ItemStatDecoder.ItemStats
            {
                ItemType = itemType,
                Weight = 1.0f,
                Difficulty = 0,
                Abundance = 100
            };
        }

        private void OnGUI()
        {
            if (headerStyle == null)
                InitializeStyles();

            EditorGUILayout.Space(10);

            // HEADER
            EditorGUILayout.LabelField("Mount & Blade Item Stats Editor", headerStyle);
            EditorGUILayout.Space(10);

            // TARGET ITEM SELECTION
            EditorGUILayout.BeginVertical(sectionStyle);
            EditorGUILayout.LabelField("Target Item", subHeaderStyle);

            var newTarget = (MBItemData)EditorGUILayout.ObjectField(
                "Item Data",
                targetItem,
                typeof(MBItemData),
                false);

            if (newTarget != targetItem)
            {
                targetItem = newTarget;
                if (targetItem != null)
                {
                    LoadCurrentStats();
                }
            }

            EditorGUILayout.EndVertical();

            if (targetItem == null)
            {
                EditorGUILayout.HelpBox(
                    "Select an MBItemData asset to edit its stats.",
                    MessageType.Info);
                return;
            }

            EditorGUILayout.Space(5);

            // Raw preview
            DrawRawPreview();

            // CHECK FOR EXTERNAL CHANGES (Undo/Redo, other windows, etc.)
            // Only reload if the Stats field changed externally (not from our own encoding)
            if (!string.IsNullOrEmpty(targetItem.Stats) &&
                targetItem.Stats != lastEncodedValue)
            {
                LoadCurrentStats();
            }

            // STAT EDITING
            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);

            DrawUniversalStats();
            EditorGUILayout.Space(10);

            DrawItemTypeSpecificStats();

            EditorGUILayout.EndScrollView();
        }

        /// <summary>
        /// Snaps weight to the nearest 0.25 increment (Mount & Blade's weight encoding limitation)
        /// </summary>
        private float SnapWeightToValid(float weight)
        {
            // Mount & Blade stores weight as (value * 4) in 8 bits
            // This means only increments of 0.25 kg are possible
            return Mathf.Round(weight * 4f) * 0.25f;
        }

        private void DrawUniversalStats()
        {
            EditorGUILayout.BeginVertical(sectionStyle);
            EditorGUILayout.LabelField("Universal Stats", subHeaderStyle);

            if (debugMode)
            {
                EditorGUILayout.HelpBox("These stats apply to all item types.", MessageType.None);
                EditorGUILayout.Space(5);
            }

            EditorGUI.BeginChangeCheck();

            // Weight with snapping to 0.25 increments
            float newWeight = EditorGUILayout.Slider(
                new GUIContent("Weight (kg)", ItemStatsTooltips.GetTooltip(0)),
                currentStats.Weight, 0f, 63.75f);

            currentStats.Difficulty = EditorGUILayout.IntSlider(
                new GUIContent("Difficulty", ItemStatsTooltips.GetTooltip(39)),
                currentStats.Difficulty, 0, 255);

            currentStats.Abundance = EditorGUILayout.IntSlider(
                new GUIContent("Abundance", ItemStatsTooltips.GetTooltip(11)),
                currentStats.Abundance, 0, 255);

            if (EditorGUI.EndChangeCheck())
            {
                currentStats.Weight = SnapWeightToValid(newWeight);
                EncodeSave();
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawItemTypeSpecificStats()
        {
            EditorGUILayout.BeginVertical(sectionStyle);
            EditorGUILayout.LabelField($"{GetItemCategoryName()} Stats", subHeaderStyle);

            EditorGUI.BeginChangeCheck();

            switch (targetItem.ItemType)
            {
                case "itp_type_one_handed_wpn":
                case "itp_type_two_handed_wpn":
                case "itp_type_polearm":
                    DrawMeleeWeaponStats();
                    break;

                case "itp_type_bow":
                case "itp_type_crossbow":
                case "itp_type_pistol":
                case "itp_type_musket":
                    DrawRangedWeaponStats();
                    break;

                case "itp_type_shield":
                    DrawShieldStats();
                    break;

                case "itp_type_horse":
                case "itp_type_animal":
                    DrawHorseStats();
                    break;

                case "itp_type_head_armor":
                case "itp_type_body_armor":
                case "itp_type_foot_armor":
                case "itp_type_hand_armor":
                    DrawArmorStats();
                    break;

                case "itp_type_arrows":
                case "itp_type_bolts":
                case "itp_type_bullets":
                    DrawAmmoStats();
                    break;

                case "itp_type_thrown":
                    DrawThrownWeaponStats();
                    break;

                case "itp_type_goods":
                    DrawGoodsStats();
                    break;
            }

            if (EditorGUI.EndChangeCheck())
            {
                EncodeSave();
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawMeleeWeaponStats()
        {
            currentStats.WeaponLengthOrShieldWidthOrHorseScale = EditorGUILayout.IntSlider(
                new GUIContent("Weapon Length (cm)", ItemStatsTooltips.GetTooltip(71)),
                currentStats.WeaponLengthOrShieldWidthOrHorseScale, 0, 1023);

            currentStats.SpeedRatingOrHorseManeuver = EditorGUILayout.IntSlider(
                new GUIContent("Speed Rating", ItemStatsTooltips.GetTooltip(55)),
                currentStats.SpeedRatingOrHorseManeuver, 0, 255);

            EditorGUILayout.Space(5);
            EditorGUILayout.LabelField("Swing Damage", EditorStyles.boldLabel);

            currentStats.SwingDamage = EditorGUILayout.IntSlider(
                new GUIContent("Damage", ItemStatsTooltips.GetTooltip(95)),
                currentStats.SwingDamage, 0, 255);

            currentStats.SwingDamageType = (ItemStatDecoder.DamageType)EditorGUILayout.EnumPopup(
                new GUIContent("Type", ItemStatsTooltips.GetDamageTypeTooltip((int)currentStats.SwingDamageType)),
                currentStats.SwingDamageType);

            EditorGUILayout.Space(5);
            EditorGUILayout.LabelField("Thrust Damage", EditorStyles.boldLabel);

            currentStats.ThrustDamageOrHorseCharge = EditorGUILayout.IntSlider(
                new GUIContent("Damage", ItemStatsTooltips.GetTooltip(87)),
                currentStats.ThrustDamageOrHorseCharge, 0, 255);

            currentStats.ThrustDamageType = (ItemStatDecoder.DamageType)EditorGUILayout.EnumPopup(
                new GUIContent("Type", ItemStatsTooltips.GetDamageTypeTooltip((int)currentStats.ThrustDamageType)),
                currentStats.ThrustDamageType);
        }

        private void DrawRangedWeaponStats()
        {
            currentStats.SpeedRatingOrHorseManeuver = EditorGUILayout.IntSlider(
                new GUIContent("Reload Speed", ItemStatsTooltips.GetTooltip(55)),
                currentStats.SpeedRatingOrHorseManeuver, 0, 255);

            currentStats.ShootSpeedOrShieldHeightOrHorseSpeed = EditorGUILayout.IntSlider(
                new GUIContent("Missile Speed", ItemStatsTooltips.GetTooltip(63)),
                currentStats.ShootSpeedOrShieldHeightOrHorseSpeed, 0, 1023);

            if (currentStats.ShootSpeedOrShieldHeightOrHorseSpeed > 200)
            {
                EditorGUILayout.HelpBox(
                    "Missile speeds above 200 m/s may cause projectiles to clip through nearby enemies!",
                    MessageType.Warning);
            }

            currentStats.LegArmorOrAccuracy = EditorGUILayout.IntSlider(
                new GUIContent("Accuracy", ItemStatsTooltips.GetTooltip(140)),
                currentStats.LegArmorOrAccuracy, 0, 255);

            EditorGUILayout.Space(5);
            EditorGUILayout.LabelField("Damage", EditorStyles.boldLabel);

            currentStats.ThrustDamageOrHorseCharge = EditorGUILayout.IntSlider(
                new GUIContent("Damage", ItemStatsTooltips.GetTooltip(87)),
                currentStats.ThrustDamageOrHorseCharge, 0, 255);

            currentStats.ThrustDamageType = (ItemStatDecoder.DamageType)EditorGUILayout.EnumPopup(
                new GUIContent("Type", ItemStatsTooltips.GetDamageTypeTooltip((int)currentStats.ThrustDamageType)),
                currentStats.ThrustDamageType);

            if (targetItem.ItemType == "itp_type_crossbow" ||
                targetItem.ItemType == "itp_type_pistol" ||
                targetItem.ItemType == "itp_type_musket")
            {
                currentStats.MaxAmmo = EditorGUILayout.IntSlider(
                    new GUIContent("Max Ammo", ItemStatsTooltips.GetTooltip(79)),
                    currentStats.MaxAmmo, 0, 255);
            }
        }

        private void DrawShieldStats()
        {
            currentStats.HitPoints = EditorGUILayout.IntSlider(
                new GUIContent("Hit Points", ItemStatsTooltips.GetTooltip(47)),
                currentStats.HitPoints, 0, 65535);

            currentStats.BodyArmorOrShieldArmorOrHorseArmor = EditorGUILayout.IntSlider(
                new GUIContent("Shield Armor", ItemStatsTooltips.GetTooltip(25)),
                currentStats.BodyArmorOrShieldArmorOrHorseArmor, 0, 255);

            currentStats.SpeedRatingOrHorseManeuver = EditorGUILayout.IntSlider(
                new GUIContent("Speed Rating", ItemStatsTooltips.GetTooltip(55)),
                currentStats.SpeedRatingOrHorseManeuver, 0, 255);

            currentStats.WeaponLengthOrShieldWidthOrHorseScale = EditorGUILayout.IntSlider(
                new GUIContent("Shield Width", ItemStatsTooltips.GetTooltip(71)),
                currentStats.WeaponLengthOrShieldWidthOrHorseScale, 0, 1023);

            currentStats.ShootSpeedOrShieldHeightOrHorseSpeed = EditorGUILayout.IntSlider(
                new GUIContent("Shield Height", ItemStatsTooltips.GetTooltip(63)),
                currentStats.ShootSpeedOrShieldHeightOrHorseSpeed, 0, 1023);
        }

        private void DrawHorseStats()
        {
            currentStats.BodyArmorOrShieldArmorOrHorseArmor = EditorGUILayout.IntSlider(
                new GUIContent("Horse Armor", ItemStatsTooltips.GetTooltip(25)),
                currentStats.BodyArmorOrShieldArmorOrHorseArmor, 0, 255);

            currentStats.ShootSpeedOrShieldHeightOrHorseSpeed = EditorGUILayout.IntSlider(
                new GUIContent("Horse Speed", ItemStatsTooltips.GetTooltip(103)),
                currentStats.ShootSpeedOrShieldHeightOrHorseSpeed, 0, 1023);

            currentStats.SpeedRatingOrHorseManeuver = EditorGUILayout.IntSlider(
                new GUIContent("Maneuverability", ItemStatsTooltips.GetTooltip(110)),
                currentStats.SpeedRatingOrHorseManeuver, 0, 255);

            currentStats.ThrustDamageOrHorseCharge = EditorGUILayout.IntSlider(
                new GUIContent("Charge Damage", ItemStatsTooltips.GetTooltip(117)),
                currentStats.ThrustDamageOrHorseCharge, 0, 255);

            float scale = currentStats.WeaponLengthOrShieldWidthOrHorseScale / 100f;
            scale = EditorGUILayout.Slider(
                new GUIContent("Horse Scale", ItemStatsTooltips.GetTooltip(123)),
                scale, 0.01f, 10.23f);
            currentStats.WeaponLengthOrShieldWidthOrHorseScale = Mathf.RoundToInt(scale * 100f);

            currentStats.HitPoints = EditorGUILayout.IntSlider(
                new GUIContent("Hit Points", ItemStatsTooltips.GetTooltip(47)),
                currentStats.HitPoints, 0, 65535);
        }

        private void DrawArmorStats()
        {
            bool isHeadArmor = targetItem.ItemType == "itp_type_head_armor";
            bool isBodyArmor = targetItem.ItemType == "itp_type_body_armor";
            bool isFootArmor = targetItem.ItemType == "itp_type_foot_armor";
            bool isHandArmor = targetItem.ItemType == "itp_type_hand_armor";

            if (isHeadArmor)
            {
                currentStats.HeadArmorOrFoodQuality = EditorGUILayout.IntSlider(
                    new GUIContent("Head Armor", ItemStatsTooltips.GetTooltip(18)),
                    currentStats.HeadArmorOrFoodQuality, 0, 255);
            }

            if (isBodyArmor || isHandArmor || isHeadArmor)
            {
                currentStats.BodyArmorOrShieldArmorOrHorseArmor = EditorGUILayout.IntSlider(
                    new GUIContent("Body Armor", ItemStatsTooltips.GetTooltip(25)),
                    currentStats.BodyArmorOrShieldArmorOrHorseArmor, 0, 255);
            }

            if (isBodyArmor || isFootArmor || isHeadArmor)
            {
                currentStats.LegArmorOrAccuracy = EditorGUILayout.IntSlider(
                    new GUIContent("Leg Armor", ItemStatsTooltips.GetTooltip(32)),
                    currentStats.LegArmorOrAccuracy, 0, 255);
            }

            currentStats.HitPoints = EditorGUILayout.IntSlider(
                new GUIContent("Hit Points", ItemStatsTooltips.GetTooltip(47)),
                currentStats.HitPoints, 0, 65535);
        }

        private void DrawAmmoStats()
        {
            currentStats.WeaponLengthOrShieldWidthOrHorseScale = EditorGUILayout.IntSlider(
                new GUIContent("Projectile Length (cm)", ItemStatsTooltips.GetTooltip(71)),
                currentStats.WeaponLengthOrShieldWidthOrHorseScale, 0, 1023);

            EditorGUILayout.Space(5);
            EditorGUILayout.LabelField("Damage", EditorStyles.boldLabel);

            currentStats.ThrustDamageOrHorseCharge = EditorGUILayout.IntSlider(
                new GUIContent("Damage Bonus", ItemStatsTooltips.GetTooltip(87)),
                currentStats.ThrustDamageOrHorseCharge, 0, 255);

            currentStats.ThrustDamageType = (ItemStatDecoder.DamageType)EditorGUILayout.EnumPopup(
                new GUIContent("Type", ItemStatsTooltips.GetDamageTypeTooltip((int)currentStats.ThrustDamageType)),
                currentStats.ThrustDamageType);

            currentStats.MaxAmmo = EditorGUILayout.IntSlider(
                new GUIContent("Stack Size", ItemStatsTooltips.GetTooltip(79)),
                currentStats.MaxAmmo, 1, 255);
        }

        private void DrawThrownWeaponStats()
        {
            currentStats.SpeedRatingOrHorseManeuver = EditorGUILayout.IntSlider(
                new GUIContent("Reload Speed", ItemStatsTooltips.GetTooltip(55)),
                currentStats.SpeedRatingOrHorseManeuver, 0, 255);

            currentStats.ShootSpeedOrShieldHeightOrHorseSpeed = EditorGUILayout.IntSlider(
                new GUIContent("Missile Speed", ItemStatsTooltips.GetTooltip(63)),
                currentStats.ShootSpeedOrShieldHeightOrHorseSpeed, 0, 1023);

            if (currentStats.ShootSpeedOrShieldHeightOrHorseSpeed > 200)
            {
                EditorGUILayout.HelpBox(
                    "Missile speeds above 200 m/s may cause projectiles to clip through nearby enemies!",
                    MessageType.Warning);
            }

            currentStats.WeaponLengthOrShieldWidthOrHorseScale = EditorGUILayout.IntSlider(
                new GUIContent("Weapon Length (cm)", ItemStatsTooltips.GetTooltip(71)),
                currentStats.WeaponLengthOrShieldWidthOrHorseScale, 0, 1023);

            EditorGUILayout.Space(5);
            EditorGUILayout.LabelField("Damage", EditorStyles.boldLabel);

            currentStats.ThrustDamageOrHorseCharge = EditorGUILayout.IntSlider(
                new GUIContent("Damage", ItemStatsTooltips.GetTooltip(87)),
                currentStats.ThrustDamageOrHorseCharge, 0, 255);

            currentStats.ThrustDamageType = (ItemStatDecoder.DamageType)EditorGUILayout.EnumPopup(
                new GUIContent("Type", ItemStatsTooltips.GetDamageTypeTooltip((int)currentStats.ThrustDamageType)),
                currentStats.ThrustDamageType);

            currentStats.MaxAmmo = EditorGUILayout.IntSlider(
                new GUIContent("Stack Size", ItemStatsTooltips.GetTooltip(79)),
                currentStats.MaxAmmo, 1, 255);
        }

        private void DrawGoodsStats()
        {
            currentStats.HeadArmorOrFoodQuality = EditorGUILayout.IntSlider(
                new GUIContent("Food Quality", ItemStatsTooltips.GetTooltip(132)),
                currentStats.HeadArmorOrFoodQuality, 0, 255);

            currentStats.MaxAmmo = EditorGUILayout.IntSlider(
                new GUIContent("Consumable Parts", ItemStatsTooltips.GetTooltip(79)),
                currentStats.MaxAmmo, 0, 255);
        }

        private void DrawRawPreview()
        {
            EditorGUILayout.BeginVertical(sectionStyle);
            // Debug mode toggle
            debugMode = EditorGUILayout.ToggleLeft(
                new GUIContent("Debug Mode",
                    "When enabled, all flags and options will display their internal names instead of formatted names."),
                debugMode);

            if (debugMode)
            {
                EditorGUILayout.LabelField("Raw Value Preview", subHeaderStyle);
                EditorGUILayout.SelectableLabel(lastEncodedValue, GUILayout.Height(16));
                EditorGUILayout.LabelField($"Item Type: {targetItem.ItemType}", EditorStyles.miniLabel);
                EditorGUILayout.Space(5);
                EditorGUILayout.HelpBox(
                    $"Item type controls which stats are available for editing.",
                    MessageType.Info);
            }

            EditorGUILayout.EndVertical();
        }

        private string GetItemCategoryName()
        {
            if (targetItem.ItemType.Contains("wpn") || targetItem.ItemType.Contains("polearm"))
                return "Melee Weapon";
            if (targetItem.ItemType.Contains("bow") || targetItem.ItemType.Contains("crossbow") ||
                targetItem.ItemType.Contains("pistol") || targetItem.ItemType.Contains("musket"))
                return "Ranged Weapon";
            if (targetItem.ItemType.Contains("armor"))
                return "Armor";
            if (targetItem.ItemType.Contains("horse") || targetItem.ItemType.Contains("animal"))
                return "Mount";
            if (targetItem.ItemType == "itp_type_shield")
                return "Shield";
            if (targetItem.ItemType.Contains("arrow") || targetItem.ItemType.Contains("bolt") ||
                targetItem.ItemType.Contains("bullet"))
                return "Ammunition";
            if (targetItem.ItemType == "itp_type_thrown")
                return "Thrown Weapon";
            if (targetItem.ItemType == "itp_type_goods")
                return "Goods";
            return "Item";
        }

        private void EncodeSave()
        {
            try
            {
                string encodedValue = EncodeStats();
                targetItem.Stats = encodedValue;
                lastEncodedValue = encodedValue; // Remember what WE encoded
                EditorUtility.SetDirty(targetItem);
            }
            catch (Exception ex)
            {
                Debug.LogError($"Failed to encode stats: {ex.Message}\n{ex.StackTrace}");
            }
        }

        private string EncodeStats()
        {
            BigInteger encoded = BigInteger.Zero;

            // UNIVERSAL STATS (Bits 0-39)

            // Bits 0-7: Head Armor / Food Quality
            int headArmorBits = Mathf.Clamp(currentStats.HeadArmorOrFoodQuality, 0, 255);
            encoded |= new BigInteger(headArmorBits) << ItemStatDecoder.IBF_HEAD_ARMOR_BITS;

            // Bits 8-15: Body Armor / Shield Armor / Horse Armor
            int bodyArmorBits = Mathf.Clamp(currentStats.BodyArmorOrShieldArmorOrHorseArmor, 0, 255);
            encoded |= new BigInteger(bodyArmorBits) << ItemStatDecoder.IBF_BODY_ARMOR_BITS;

            // Bits 16-23: Leg Armor / Accuracy
            int legArmorBits = Mathf.Clamp(currentStats.LegArmorOrAccuracy, 0, 255);
            encoded |= new BigInteger(legArmorBits) << ItemStatDecoder.IBF_LEG_ARMOR_BITS;

            // Bits 24-31: Weight
            int weightBits = Mathf.Clamp(Mathf.RoundToInt(currentStats.Weight * 4f), 0, 255);
            encoded |= new BigInteger(weightBits) << ItemStatDecoder.IBF_WEIGHT_BITS;

            // Bits 32-39: Difficulty
            int difficultyBits = Mathf.Clamp(currentStats.Difficulty, 0, 255);
            encoded |= new BigInteger(difficultyBits) << ItemStatDecoder.IBF_DIFFICULTY_BITS;

            // HIT POINTS vs DAMAGE FIELDS (Bits 40-69)
            // These fields OVERLAP! Use hit points for shields/armor, damage for weapons

            bool isWeapon = IsWeaponType(targetItem.ItemType);

            if (isWeapon)
            {
                // For weapons: encode damage fields (which overlap hit points)

                // Bits 50-57: Swing Damage (8 bits)
                int swingDamageBits = Mathf.Clamp(currentStats.SwingDamage, 0, 255);
                encoded |= new BigInteger(swingDamageBits) << ItemStatDecoder.IWF_SWING_DAMAGE_BITS;

                // Bits 58-59: Swing Damage Type (2 bits)
                int swingTypeBits = (int)currentStats.SwingDamageType & 0x3;
                encoded |= new BigInteger(swingTypeBits) << ItemStatDecoder.IWF_SWING_DAMAGE_TYPE_BITS;

                // Bits 60-67: Thrust Damage (8 bits)
                int thrustDamageBits = Mathf.Clamp(currentStats.ThrustDamageOrHorseCharge, 0, 255);
                encoded |= new BigInteger(thrustDamageBits) << ItemStatDecoder.IWF_THRUST_DAMAGE_BITS;

                // Bits 68-69: Thrust Damage Type (2 bits)
                int thrustTypeBits = (int)currentStats.ThrustDamageType & 0x3;
                encoded |= new BigInteger(thrustTypeBits) << ItemStatDecoder.IWF_THRUST_DAMAGE_TYPE_BITS;
            }
            else
            {
                // For shields/armor/horses: encode hit points (bits 40-55)
                int hitPointsBits = Mathf.Clamp(currentStats.HitPoints, 0, 65535);
                encoded |= new BigInteger(hitPointsBits) << ItemStatDecoder.IBF_HITPOINTS_BITS;
            }

            // REMAINING WEAPON/SHIELD/HORSE STATS (Bits 70+)

            // Bits 70-79: Weapon Length / Shield Width / Horse Scale
            int lengthBits = Mathf.Clamp(currentStats.WeaponLengthOrShieldWidthOrHorseScale, 0, 1023);
            encoded |= new BigInteger(lengthBits) << ItemStatDecoder.IWF_WEAPON_LENGTH_BITS;

            // Bits 80-87: Speed Rating / Horse Maneuver
            int speedBits = Mathf.Clamp(currentStats.SpeedRatingOrHorseManeuver, 0, 255);
            encoded |= new BigInteger(speedBits) << ItemStatDecoder.IWF_SPEED_RATING_BITS;

            // Bits 90-99: Shoot Speed / Shield Height / Horse Speed
            int shootSpeedBits = Mathf.Clamp(currentStats.ShootSpeedOrShieldHeightOrHorseSpeed, 0, 1023);
            encoded |= new BigInteger(shootSpeedBits) << ItemStatDecoder.IWF_SHOOT_SPEED_BITS;

            // Bits 100-107: Max Ammo
            int ammoBits = Mathf.Clamp(currentStats.MaxAmmo, 0, 255);
            encoded |= new BigInteger(ammoBits) << ItemStatDecoder.IWF_MAX_AMMO_BITS;

            // Bits 110-117: Abundance
            int abundanceBits = (currentStats.Abundance == 100) ? 0 : Mathf.Clamp(currentStats.Abundance, 0, 255);
            encoded |= new BigInteger(abundanceBits) << ItemStatDecoder.IWF_ABUNDANCE_BITS;

            return encoded.ToString();
        }

        private bool IsWeaponType(string itemType)
        {
            return itemType == "itp_type_one_handed_wpn" ||
                   itemType == "itp_type_two_handed_wpn" ||
                   itemType == "itp_type_polearm" ||
                   itemType == "itp_type_bow" ||
                   itemType == "itp_type_crossbow" ||
                   itemType == "itp_type_pistol" ||
                   itemType == "itp_type_musket" ||
                   itemType == "itp_type_thrown" ||
                   itemType == "itp_type_arrows" ||
                   itemType == "itp_type_bolts" ||
                   itemType == "itp_type_bullets";
        }
    }
}
