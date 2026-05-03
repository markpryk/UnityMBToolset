using UnityEngine;
using System;
using System.Collections.Generic;

namespace MountAndBlade.Data
{
    /// <summary>
    /// ScriptableObject representation of a Mount & Blade Warband skin.
    /// Corresponds to skin tuples in module_skins.py
    /// 
    /// Skins define the visual and audio characteristics of character types (male, female, races).
    /// They control body meshes, face generation, customization options, and audio.
    /// </summary>
    [CreateAssetMenu(fileName = "New MB Skin", menuName = "Mount & Blade/Skin Data", order = 2)]
    public class MBSkinData : ScriptableObject
    {
        [Header("Basic Info")]
        [Tooltip("Unique identifier for this skin (e.g., 'man', 'woman', 'undead')")]
        public string SkinID;
        
        [Tooltip("Skin flags - determines which morph key/frame to use for body transformations")]
        public int SkinFlags = 0;

        [Header("Body Meshes")]
        [Tooltip("Main body mesh reference")]
        public string BodyMesh;
        
        [Tooltip("Left calf mesh reference")]
        public string CalfMeshLeft;
        
        [Tooltip("Left hand mesh reference")]
        public string HandMeshLeft;
        
        [Tooltip("Head mesh reference")]
        public string HeadMesh;

        [Header("Face Generation")]
        [Tooltip("Face keys control procedural face generation parameters")]
        public List<MBFaceKey> FaceKeys = new List<MBFaceKey>();
        
        [Tooltip("Optional: Face key constraints for valid face generation")]
        public List<MBFaceKeyConstraint> FaceKeyConstraints = new List<MBFaceKeyConstraint>();

        [Header("Customization Options")]
        [Tooltip("Available hair mesh options")]
        public List<string> HairMeshes = new List<string>();
        
        [Tooltip("Available beard mesh options")]
        public List<string> BeardMeshes = new List<string>();
        
        [Tooltip("Available hair texture options")]
        public List<string> HairTextures = new List<string>();
        
        [Tooltip("Available beard texture options")]
        public List<string> BeardTextures = new List<string>();
        
        [Tooltip("Face texture variations with associated colors")]
        public List<MBFaceTexture> FaceTextures = new List<MBFaceTexture>();

        [Header("Audio")]
        [Tooltip("Voice sound entries for different events")]
        public List<MBVoiceEntry> VoiceSounds = new List<MBVoiceEntry>();

        [Header("Technical")]
        [Tooltip("Skeleton name (e.g., 'skel_human')")]
        public string SkeletonName = "skel_human";
        
        [Tooltip("Scale multiplier (1.0 = normal). Affects skeleton, hitboxes, and meshes. Use carefully!")]
        [Range(0.3f, 2.0f)]
        public float Scale = 1.0f;
        
        [Tooltip("Optional: Custom blood particle system 1")]
        public string BloodParticles1;
        
        [Tooltip("Optional: Custom blood particle system 2")]
        public string BloodParticles2;

        [Header("Debug Info")]
        [Tooltip("Decoded skin flags for editor visualization")]
        [SerializeField] private string _decodedFlags;

        /// <summary>
        /// Update the decoded flags display (call in editor)
        /// </summary>
        public void UpdateDecodedFlags()
        {
            // _decodedFlags = SkinFlagDecoder.DecodeSkinFlag(SkinFlags);
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            UpdateDecodedFlags();
        }
#endif
    }

    // SUPPORTING DATA STRUCTURES

    /// <summary>
    /// Face key defines a single morphable parameter for procedural face generation
    /// </summary>
    [System.Serializable]
    public class MBFaceKey
    {
        [Tooltip("Face key identifier/index")]
        public int KeyID;
        
        [Tooltip("Reserved field (always 0 in native)")]
        public int Reserved = 0;
        
        [Tooltip("Minimum value for this morph parameter")]
        public float MinValue;
        
        [Tooltip("Maximum value for this morph parameter")]
        public float MaxValue;
        
        [Tooltip("Human-readable name (e.g., 'Chin Size', 'Nose Width')")]
        public string DisplayName;

        public MBFaceKey(int keyID, int reserved, float minValue, float maxValue, string displayName)
        {
            KeyID = keyID;
            Reserved = reserved;
            MinValue = minValue;
            MaxValue = maxValue;
            DisplayName = displayName;
        }
    }

    /// <summary>
    /// Face key constraint ensures generated faces stay within valid proportions
    /// Format: [threshold, comparison_type, (weight1, key_index1), (weight2, key_index2), ...]
    /// Example: [1.7, comp_greater_than, (1.0, face_width), (1.0, temple_width)]
    /// Means: 1.7 > (1.0 * face_width + 1.0 * temple_width)
    /// </summary>
    [System.Serializable]
    public class MBFaceKeyConstraint
    {
        [Tooltip("Threshold value for comparison")]
        public float Threshold;
        
        [Tooltip("Comparison operator: -1 = less than, 1 = greater than")]
        public int ComparisonType;
        
        [Tooltip("Weighted face key combinations")]
        public List<WeightedFaceKey> WeightedKeys = new List<WeightedFaceKey>();

        [System.Serializable]
        public class WeightedFaceKey
        {
            [Tooltip("Weight multiplier for this key")]
            public float Weight;
            
            [Tooltip("Face key index")]
            public int KeyIndex;

            public WeightedFaceKey(float weight, int keyIndex)
            {
                Weight = weight;
                KeyIndex = keyIndex;
            }
        }
    }

    /// <summary>
    /// Face texture definition with base color and available hair/skin color variations
    /// </summary>
    [System.Serializable]
    public class MBFaceTexture
    {
        [Tooltip("Texture name/ID")]
        public string TextureName;
        
        [Tooltip("Base skin tone color (hex format: 0xRRGGBB)")]
        public string BaseSkinColor;
        
        [Tooltip("Compatible hair texture options for this face")]
        public List<string> HairTextureOptions = new List<string>();
        
        [Tooltip("Available skin tone variations (hex format: 0xRRGGBB)")]
        public List<string> SkinColorVariations = new List<string>();

        public MBFaceTexture(string textureName, string baseSkinColor)
        {
            TextureName = textureName;
            BaseSkinColor = baseSkinColor;
        }

        /// <summary>
        /// Convert hex color string to Unity Color
        /// </summary>
        public Color GetBaseSkinColorAsColor()
        {
            return HexToColor(BaseSkinColor);
        }

        /// <summary>
        /// Get all skin color variations as Unity Colors
        /// </summary>
        public List<Color> GetSkinColorVariationsAsColors()
        {
            List<Color> colors = new List<Color>();
            foreach (string hexColor in SkinColorVariations)
            {
                colors.Add(HexToColor(hexColor));
            }
            return colors;
        }

        private Color HexToColor(string hex)
        {
            if (string.IsNullOrEmpty(hex))
                return Color.white;

            // Remove 0x prefix if present
            hex = hex.Replace("0x", "").Replace("0X", "");

            // Parse RGB (RRGGBB format)
            if (hex.Length == 6)
            {
                byte r = byte.Parse(hex.Substring(0, 2), System.Globalization.NumberStyles.HexNumber);
                byte g = byte.Parse(hex.Substring(2, 2), System.Globalization.NumberStyles.HexNumber);
                byte b = byte.Parse(hex.Substring(4, 2), System.Globalization.NumberStyles.HexNumber);
                return new Color32(r, g, b, 255);
            }

            return Color.white;
        }
    }

    /// <summary>
    /// Voice entry maps voice events to sound IDs
    /// </summary>
    [System.Serializable]
    public class MBVoiceEntry
    {
        [Tooltip("Voice event type")]
        public VoiceEventType EventType;
        
        [Tooltip("Sound ID to play")]
        public string SoundID;

        public MBVoiceEntry(VoiceEventType eventType, string soundID)
        {
            EventType = eventType;
            SoundID = soundID;
        }
    }

    /// <summary>
    /// Voice event types corresponding to header_skins.py
    /// </summary>
    public enum VoiceEventType
    {
        Die = 0,              // Agent dies
        Hit = 1,              // Agent gets hit
        Grunt = 2,            // Unmounted agent attacks with weapon
        GruntLong = 3,        // Unmounted agent attacks powerfully
        Yell = 4,             // AI agent comes close or player holds attack after sprinting
        WarCry = 5,           // Unused in native
        Victory = 6,          // Agent makes victory cheer
        Stun = 7              // Agent gets stunned
    }
}
