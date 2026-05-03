using System;
using System.Collections.Generic;
using System.Numerics;
using System.Text;

namespace MountAndBlade.Data
{
    /// <summary>
    /// Decodes and encodes Mount & Blade Warband face codes.
    /// Based on Swyter's reverse engineering work (2022).
    /// 
    /// A face code consists of four 64-bit hexadecimal blocks:
    /// 0x[block0:16 digits][block1:16 digits][block2:16 digits][block3:16 digits]
    /// 
    /// Example: 0x000000018000004136db6db6db6db6fb7fffff6d77bf36db0000000000000000
    ///             ^^^^^^^^^^^^^^^^ ^^^^^^^^^^^^^^^^ ^^^^^^^^^^^^^^^^ ^^^^^^^^^^^^^^^^
    ///                 Block 0          Block 1          Block 2          Block 3 (unused)
    /// 
    /// IMPORTANT NOTES:
    /// - Only face_key_1 (block 0) and face_key_2 (block 1) are saved/loaded when exporting characters
    /// - Block 2 contains morph keys 21-42 which are LOST during character export/import
    /// - Block 3 is always empty/unused
    /// - Each morph key uses 3 bits (values 0-7), except morph key 42 which uses only 1 bit
    /// - In-game sliders snap to 8 positions (0-7) even if they appear smoother
    /// </summary>
    public static class FaceCodeDecoder
    {
        // FACE CODE STRUCTURE (Block 0 - Appearance)
        
        /// <summary>
        /// Decode a complete face code into all its components
        /// </summary>
        public static FaceCodeData DecodeFaceCode(string faceCode)
        {
            // Remove 0x prefix and validate
            faceCode = faceCode.Replace("0x", "").Replace("0X", "");
            
            if (faceCode.Length != 64)
                throw new ArgumentException("Face code must be exactly 64 hexadecimal digits (after 0x prefix)");

            FaceCodeData data = new FaceCodeData();

            // Split into 4 blocks of 16 hex digits each
            string block0 = faceCode.Substring(0, 16);
            string block1 = faceCode.Substring(16, 16);
            string block2 = faceCode.Substring(32, 16);
            string block3 = faceCode.Substring(48, 16);

            // Decode Block 0 (appearance attributes)
            data.Hair = ExtractBits(block0, 0, 6);
            data.Beard = ExtractBits(block0, 6, 6);
            data.Skin = ExtractBits(block0, 12, 6);
            data.HairTexture = ExtractBits(block0, 18, 6);
            data.HairColor = ExtractBits(block0, 24, 6);
            data.Age = ExtractBits(block0, 30, 6);
            data.SkinColor = ExtractBits(block0, 36, 6);

            // Decode Block 1 (morph keys 0-20)
            for (int i = 0; i <= 20; i++)
            {
                data.MorphKeys[i] = ExtractBits(block1, i * 3, 3);
            }

            // Decode Block 2 (morph keys 21-42)
            for (int i = 21; i <= 41; i++)
            {
                data.MorphKeys[i] = ExtractBits(block2, (i - 21) * 3, 3);
            }
            
            // Morph key 42 only has 1 bit
            data.MorphKeys[42] = ExtractBits(block2, 63, 1);

            return data;
        }

        /// <summary>
        /// Encode face code data into a hex string
        /// </summary>
        public static string EncodeFaceCode(FaceCodeData data)
        {
            StringBuilder result = new StringBuilder("0x");

            // Build Block 0
            ulong block0 = 0;
            block0 |= ((ulong)data.Hair & 0x3F);
            block0 |= ((ulong)data.Beard & 0x3F) << 6;
            block0 |= ((ulong)data.Skin & 0x3F) << 12;
            block0 |= ((ulong)data.HairTexture & 0x3F) << 18;
            block0 |= ((ulong)data.HairColor & 0x3F) << 24;
            block0 |= ((ulong)data.Age & 0x3F) << 30;
            block0 |= ((ulong)data.SkinColor & 0x3F) << 36;
            
            result.Append(block0.ToString("x16"));

            // Build Block 1 (morph keys 0-20)
            ulong block1 = 0;
            for (int i = 0; i <= 20; i++)
            {
                block1 |= ((ulong)data.MorphKeys[i] & 0x7) << (i * 3);
            }
            result.Append(block1.ToString("x16"));

            // Build Block 2 (morph keys 21-42)
            ulong block2 = 0;
            for (int i = 21; i <= 41; i++)
            {
                block2 |= ((ulong)data.MorphKeys[i] & 0x7) << ((i - 21) * 3);
            }
            // Morph key 42 (only 1 bit)
            block2 |= ((ulong)data.MorphKeys[42] & 0x1) << 63;
            result.Append(block2.ToString("x16"));

            // Block 3 (always empty)
            result.Append("0000000000000000");

            return result.ToString();
        }

        /// <summary>
        /// Extract character export data (only blocks 0 and 1)
        /// This is what gets saved when exporting characters
        /// </summary>
        public static (long faceKey1, long faceKey2) GetExportKeys(string faceCode)
        {
            faceCode = faceCode.Replace("0x", "").Replace("0X", "");
            
            string block0 = faceCode.Substring(0, 16);
            string block1 = faceCode.Substring(16, 16);

            long faceKey1 = Convert.ToInt64(block0, 16);
            long faceKey2 = Convert.ToInt64(block1, 16);

            return (faceKey1, faceKey2);
        }

        /// <summary>
        /// Create face code from character export keys
        /// Note: This loses morph keys 21-42 which are in block 2
        /// </summary>
        public static string CreateFromExportKeys(long faceKey1, long faceKey2)
        {
            StringBuilder result = new StringBuilder("0x");
            result.Append(faceKey1.ToString("x16"));
            result.Append(faceKey2.ToString("x16"));
            result.Append("0000000000000000"); // Block 2 (lost)
            result.Append("0000000000000000"); // Block 3 (unused)
            return result.ToString();
        }

        /// <summary>
        /// Randomize face code with optional constraints
        /// </summary>
        public static FaceCodeData Randomize(Random rng = null, int? maxHairIndex = null, int? maxBeardIndex = null, int? maxSkinIndex = null)
        {
            if (rng == null) rng = new Random();

            FaceCodeData data = new FaceCodeData();
            
            data.Hair = rng.Next(0, maxHairIndex ?? 64);
            data.Beard = rng.Next(0, maxBeardIndex ?? 64);
            data.Skin = rng.Next(0, maxSkinIndex ?? 64);
            data.HairTexture = rng.Next(0, 64);
            data.HairColor = rng.Next(0, 64);
            data.Age = rng.Next(0, 64);
            data.SkinColor = rng.Next(0, 64);

            // Randomize all morph keys (0-7 for most, 0-1 for key 42)
            for (int i = 0; i <= 41; i++)
            {
                data.MorphKeys[i] = rng.Next(0, 8);
            }
            data.MorphKeys[42] = rng.Next(0, 2);

            return data;
        }

        /// <summary>
        /// Reset all morph keys to middle position (value 3 or 4)
        /// </summary>
        public static FaceCodeData ResetMorphKeys(FaceCodeData data)
        {
            for (int i = 0; i <= 41; i++)
            {
                data.MorphKeys[i] = 3; // Middle of 0-7 range
            }
            data.MorphKeys[42] = 0;
            return data;
        }

        // UTILITY METHODS

        /// <summary>
        /// Extract bits from a hex string at given position and length
        /// </summary>
        private static int ExtractBits(string hexBlock, int bitPosition, int bitCount)
        {
            // Convert hex to binary (64-bit)
            ulong value = Convert.ToUInt64(hexBlock, 16);
            
            // Create mask and extract
            ulong mask = ((1UL << bitCount) - 1) << bitPosition;
            return (int)((value & mask) >> bitPosition);
        }

        /// <summary>
        /// Convert morph key value (0-7) to normalized float (-1.0 to 1.0)
        /// Used for actual face deformation
        /// </summary>
        public static float MorphKeyToNormalized(int morphValue)
        {
            // 0 = -1.0, 3 = 0.0, 7 = +1.0
            return (morphValue - 3.5f) / 3.5f;
        }

        /// <summary>
        /// Convert normalized float to morph key value
        /// </summary>
        public static int NormalizedToMorphKey(float normalized)
        {
            int value = (int)Math.Round((normalized * 3.5f) + 3.5f);
            return Math.Clamp(value, 0, 7);
        }

        /// <summary>
        /// Get friendly descriptions for appearance values
        /// </summary>
        public static string GetFieldDescription(string fieldName)
        {
            switch (fieldName.ToLower())
            {
                case "hair":
                    return "Hair mesh index (0 = bald, 1+ = hair meshes from skin definition). Range: 0-63.";
                case "beard":
                    return "Beard mesh index (0 = none, 1+ = beard meshes from skin definition). Range: 0-63.";
                case "skin":
                    return "Face texture index. Controls which face material/texture is used. Also affects hair texture and skin color pairing. Range: 0-63.";
                case "hairtexture":
                    return "Hair texture override (usually 0). Doesn't seem exposed in editor. Range: 0-63.";
                case "haircolor":
                    return "Hair color variation. Interpolates between two colors defined in face texture. Range: 0-63.";
                case "age":
                    return "Age slider. Interpolates between young and old face textures (diffuseA → diffuseB). Range: 0-63.";
                case "skincolor":
                    return "Skin color variation (usually 0). Used by face_keys_[set/get]_skin_color operations. Range: 0-63.";
                default:
                    return "";
            }
        }

        /// <summary>
        /// Validate face code format
        /// </summary>
        public static bool IsValidFaceCode(string faceCode)
        {
            if (string.IsNullOrWhiteSpace(faceCode))
                return false;

            faceCode = faceCode.Replace("0x", "").Replace("0X", "");

            if (faceCode.Length != 64)
                return false;

            // Check if all characters are valid hex
            foreach (char c in faceCode)
            {
                if (!Uri.IsHexDigit(c))
                    return false;
            }

            return true;
        }
    }

    // DATA STRUCTURE

    /// <summary>
    /// Represents all data in a Mount & Blade face code
    /// </summary>
    [Serializable]
    public class FaceCodeData
    {
        // Block 0: Appearance attributes (6 bits each)
        public int Hair { get; set; }          // Range: 0-63
        public int Beard { get; set; }         // Range: 0-63
        public int Skin { get; set; }          // Range: 0-63 (face texture)
        public int HairTexture { get; set; }   // Range: 0-63 (usually 0)
        public int HairColor { get; set; }     // Range: 0-63
        public int Age { get; set; }           // Range: 0-63
        public int SkinColor { get; set; }     // Range: 0-63 (usually 0)

        // Blocks 1-2: Morph keys (face shape)
        // Keys 0-41: 3 bits each (range 0-7)
        // Key 42: 1 bit (range 0-1)
        public int[] MorphKeys { get; set; } = new int[43];

        public FaceCodeData()
        {
            // Default to middle values
            Hair = 0;
            Beard = 0;
            Skin = 0;
            HairTexture = 0;
            HairColor = 32;
            Age = 18;
            SkinColor = 0;

            for (int i = 0; i < 43; i++)
                MorphKeys[i] = 3; // Middle of 0-7 range
        }

        public override string ToString()
        {
            return FaceCodeDecoder.EncodeFaceCode(this);
        }

        /// <summary>
        /// Create a copy of this face code data
        /// </summary>
        public FaceCodeData Clone()
        {
            FaceCodeData clone = new FaceCodeData
            {
                Hair = this.Hair,
                Beard = this.Beard,
                Skin = this.Skin,
                HairTexture = this.HairTexture,
                HairColor = this.HairColor,
                Age = this.Age,
                SkinColor = this.SkinColor
            };

            Array.Copy(this.MorphKeys, clone.MorphKeys, 43);
            return clone;
        }
    }

    /// <summary>
    /// Morph key names based on typical Mount & Blade face key definitions
    /// These may vary by mod
    /// </summary>
    public static class MorphKeyNames
    {
        public static readonly string[] DefaultNames = new string[]
        {
            "Chin Size",           // 0
            "Chin Shape",          // 1
            "Chin Forward",        // 2
            "Jaw Width",           // 3
            "Jaw Position",        // 4
            "Mouth-Nose Distance", // 5
            "Mouth Width",         // 6
            "Cheeks",              // 7
            "Nose Height",         // 8
            "Nose Width",          // 9
            "Nose Size",           // 10
            "Nose Shape",          // 11
            "Nose Bridge",         // 12
            "Cheek Bones",         // 13
            "Eye Width",           // 14
            "Eye to Eye Dist",     // 15
            "Eye Shape",           // 16
            "Eye Depth",           // 17
            "Eyelids",             // 18
            "Eyebrow Position",    // 19
            "Eyebrow Height",      // 20
            "Eyebrow Depth",       // 21
            "Eyebrow Shape",       // 22
            "Temple Width",        // 23
            "Face Texture",        // 24
            "Face Depth",          // 25
            "Face Ratio",          // 26
            "Face Width",          // 27
            "Morph Key 28",        // 28
            "Morph Key 29",        // 29
            "Morph Key 30",        // 30
            "Morph Key 31",        // 31
            "Morph Key 32",        // 32
            "Morph Key 33",        // 33
            "Morph Key 34",        // 34
            "Morph Key 35",        // 35
            "Morph Key 36",        // 36
            "Morph Key 37",        // 37
            "Morph Key 38",        // 38
            "Morph Key 39",        // 39
            "Morph Key 40",        // 40
            "Morph Key 41",        // 41
            "Morph Key 42"         // 42 (only 1 bit)
        };

        public static string GetName(int index)
        {
            if (index >= 0 && index < DefaultNames.Length)
                return DefaultNames[index];
            return $"Morph Key {index}";
        }
    }
}
