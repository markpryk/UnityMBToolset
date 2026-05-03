
using System.Diagnostics;
using System.IO;
using UnityEngine;
using Debug = UnityEngine.Debug;

public static class BRFProcessHandler
{
    /// <summary>
    /// Executes the openBrf.exe process with the specified arguments.
    /// </summary>
    /// <param name="sourcePath">Path to the source file.</param>
    /// <param name="destinationPath">Path to the destination folder.</param>
    /// <param name="parentFolderName">Name of the parent folder.</param>
    public static void ExecuteBRFProcess(string sourcePath, string destinationPath, string parentFolderName)
    {
        if (string.IsNullOrEmpty(sourcePath) || !Directory.Exists(sourcePath))
        {
            Debug.LogError(sourcePath);
            Debug.LogError("Invalid source path. File does not exist.");
            return;
        }

        if (string.IsNullOrEmpty(destinationPath) || !Directory.Exists(destinationPath))
        {
            Debug.LogError("Invalid destination path. Directory does not exist.");
            return;
        }

        string executablePath = "";
        
        if (!File.Exists(executablePath))
        {
            Debug.LogError("openBrf.exe not found at: " + executablePath);
            return;
        }

        ProcessStartInfo processInfo = new ProcessStartInfo
        {
            FileName = executablePath,
            Arguments = $"\"{sourcePath}\" \"{destinationPath}\" \"{parentFolderName}\"",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        try
        {
            using (Process process = Process.Start(processInfo))
            {
                process.WaitForExit();

                if (process.ExitCode == 0)
                {
                    Debug.Log("BRF Process completed successfully.");
                }
                else
                {
                    string errorOutput = process.StandardError.ReadToEnd();
                    Debug.LogError("BRF Process failed with error: " + errorOutput);
                }
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogError("BRF An error occurred while executing the process: " + ex.Message);
        }
    }
}
