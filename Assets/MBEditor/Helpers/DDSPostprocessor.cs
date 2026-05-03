using UnityEditor;
using System;
using System.IO;

public static class DDSPostprocessor
{
    public static void Procces(string texturesFolder)
    {
        string[] files = Directory.GetFiles(texturesFolder, "*.dds", SearchOption.AllDirectories);
        foreach (string file in files)
        {
            ProcessDDSTexture(file);
        }
    }

    public static void ProcessDDSTexture(string path)
    {
        if (!File.Exists(path))
        {
            UnityEngine.Debug.LogError($"DDS file not found: {path}");
            return;
        }

        byte[] fileData = File.ReadAllBytes(path);

        // Basic DDS header check
        if (fileData.Length < 128 || fileData[0] != 'D' || fileData[1] != 'D' || fileData[2] != 'S' || fileData[3] != ' ')
        {
            UnityEngine.Debug.LogError($"Invalid DDS file: {path} (Header mismatch)");
            return;
        }

        // Header size validation
        int headerSize = BitConverter.ToInt32(fileData, 4);
        if (headerSize != 124)
        {
            UnityEngine.Debug.LogError($"Invalid DDS header size: {path} (Expected 124, got {headerSize})");
            return;
        }

        // Get texture dimensions
        int height = BitConverter.ToInt32(fileData, 12);
        int width = BitConverter.ToInt32(fileData, 16);

        if (width <= 0 || height <= 0)
        {
            UnityEngine.Debug.LogError($"Invalid DDS dimensions: {path} (Width: {width}, Height: {height})");
            return;
        }

        // Check if dimensions are suitable for DXT compression
        if (HasInvalidDimensions(width, height))
        {
            DDSProccesHelpers.FixDimensions(path, width, height);
        }

        // Calculate expected mipmaps
        int expectedMipCount = (int)Math.Floor(Math.Log(Math.Max(width, height), 2)) + 1;

        // Read actual mipmap count from DDS header
        int mipMapCount = BitConverter.ToInt32(fileData, 28);
        if (mipMapCount != expectedMipCount)
        {
            DDSProccesHelpers.FixMipmaps(path, expectedMipCount);
        }

        // Format Check (DXT1, DXT5, etc.)
        int fourCC = BitConverter.ToInt32(fileData, 84);
        string format = GetDDSFormat(fourCC);
        if (format == "UNKNOWN")
        {
            UnityEngine.Debug.LogError($"Unsupported DDS format: {path}");
        }
    }

    private static bool HasInvalidDimensions(int width, int height)
    {
        if (width % 4 != 0 || height % 4 != 0)
        {
            UnityEngine.Debug.LogWarning($"Invalid dimensions: Width ({width}) and Height ({height}) must be multiples of 4.");
            return true;
        }
        return false;
    }
    
    private static string GetDDSFormat(int fourCC)
    {
        switch (fourCC)
        {
            case 0x31545844: return "DXT1"; // "DXT1"
            case 0x33545844: return "DXT3"; // "DXT3"
            case 0x35545844: return "DXT5"; // "DXT5"
            case 0x30315844: return "BC1";  // BC1
            case 0x30325844: return "BC2";  // BC2
            case 0x30335844: return "BC3";  // BC3
            default: return "UNKNOWN";
        }
    }
}
