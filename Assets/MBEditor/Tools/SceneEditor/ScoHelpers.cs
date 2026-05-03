using System.Diagnostics;
using UnityEngine;
using Debug = UnityEngine.Debug;

public static class ScoHelpers
{

    /// <summary>
    /// Calls the mab_sco_unpack tool with the provided input SCO path and output directory.
    /// </summary>
    /// <param name="filePath">Path to the .sco file.</param>
    /// <param name="outputDirectory">Path to the output directory where unpacked files will be saved.</param>
    public static void ExtractSco(string filePath, string outputDirectory)
    {
        if (string.IsNullOrEmpty(filePath) || string.IsNullOrEmpty(outputDirectory))
        {
            Debug.LogError("Input file path and output directory must be specified.");
            return;
        }

        if (!System.IO.File.Exists(filePath))
        {
            Debug.LogError($"File not found: {filePath}");
            return;
        }

        if (!System.IO.Directory.Exists(outputDirectory))
        {
            Debug.Log($"Output directory does not exist. Creating: {outputDirectory}");
            System.IO.Directory.CreateDirectory(outputDirectory);
        }

        string arguments = $"\"{filePath}\" -o \"{outputDirectory}\"";
        RunProcess(MBPathHelpers.MBScoUnpackToolPath(), arguments);
    }

    /// <summary>
    /// Runs the specified process with the provided arguments.
    /// </summary>
    /// <param name="toolPath">Path to the mab_sco_unpack tool.</param>
    /// <param name="arguments">Arguments to pass to the tool.</param>
    private static void RunProcess(string toolPath, string arguments)
    {
        try
        {
            if (!System.IO.File.Exists(toolPath))
            {
                Debug.LogError($"Tool not found at path: {toolPath}");
                return;
            }

            Process process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = toolPath,
                    Arguments = arguments,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                }
            };

            process.Start();

            string output = process.StandardOutput.ReadToEnd();
            string error = process.StandardError.ReadToEnd();

            process.WaitForExit();

            Debug.Log($"Output: {output}");
            if (!string.IsNullOrEmpty(error))
            {
                Debug.LogError($"Error: {error}");
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"Exception: {ex.Message}");
        }
    }
}
