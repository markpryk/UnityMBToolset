using UnityEngine;
using System.Diagnostics;
using System.IO;
using UnityEditor;
using Debug = UnityEngine.Debug;

public static class DDSProccesHelpers
{
    public static void FixMipmaps(string path, int expectedCount)
    {
        string texconvPath = MBPathHelpers.TexConvPath();
        if (!File.Exists(texconvPath))
        {
            Debug.LogError("texconv.exe not found at: " + texconvPath);
            return;
        }

        string outputPath = Path.GetDirectoryName(path);

        ProcessStartInfo processInfo = new ProcessStartInfo()
        {
            FileName = texconvPath,
            Arguments = $"-m {expectedCount} -keepcoverage 1.0 -y -o \"" + outputPath + "\" \"" + path + "\"",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            WorkingDirectory = Path.GetDirectoryName(texconvPath)
        };

        using (Process process = Process.Start(processInfo))
        {
            process.WaitForExit();
        }
    }

    public static void FixDimensions(string path, int width, int height)
    {
        string texconvPath = MBPathHelpers.TexConvPath();
        if (!File.Exists(texconvPath))
        {
            Debug.LogError("texconv.exe not found at: " + texconvPath);
            return;
        }

        // Ensure dimensions are multiples of 4 (required for DXT/BC formats)
        int fixedWidth = (width + 3) & ~3; // Round up to nearest multiple of 4
        int fixedHeight = (height + 3) & ~3; // Round up to nearest multiple of 4

        if (fixedWidth == width && fixedHeight == height)
        {
            Debug.Log($"Dimensions are already valid for {Path.GetFileName(path)}: {width}x{height}");
            return;
        }

        Debug.LogWarning(
            $"Fixing dimensions for {Path.GetFileName(path)}: {width}x{height} -> {fixedWidth}x{fixedHeight}");

        string outputPath = Path.GetDirectoryName(path);

        // Run texconv to fix dimensions
        string arguments = $"-w {fixedWidth} -h {fixedHeight} -keepcoverage 1.0 -y -o \"{outputPath}\" \"{path}\"";

        ProcessStartInfo processInfo = new ProcessStartInfo()
        {
            FileName = texconvPath,
            Arguments = arguments,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            WorkingDirectory = Path.GetDirectoryName(texconvPath)
        };

        using (Process process = Process.Start(processInfo))
        {
            process.WaitForExit();
            if (process.ExitCode == 0)
            {
                Debug.Log(
                    $"DDS texture dimensions fixed: {Path.GetFileName(path)} (Dimensions: {fixedWidth}x{fixedHeight})");
            }
            else
            {
                string error = process.StandardError.ReadToEnd();
                Debug.LogError($"Failed to fix DDS dimensions for {Path.GetFileName(path)}. Error: {error}");
            }
        }
    }
}