using System;
using System.Diagnostics;
using System.IO;
using UnityEngine;
using Debug = UnityEngine.Debug;

/// <summary>
/// Static helper class for M&B SCO (Scene Object) packing operations.
/// Wraps the mab_sco_repack.exe tool with a clean API.
/// </summary>
public static class MBSCOHelpers
{
    #region Enums
    
    /// <summary>How to handle each SCO section during repacking</summary>
    public enum SectionMode
    {
        /// <summary>Repack from unpacked folder data</summary>
        Repack,
        /// <summary>Keep existing data in output SCO</summary>
        Keep,
        /// <summary>Clear section entirely</summary>
        Empty,
        /// <summary>Copy from a donor SCO file</summary>
        Donor
    }
    
    #endregion
    
    #region Settings
    
    /// <summary>Settings for SCO repack operation</summary>
    [Serializable]
    public class SCORepackSettings
    {
        [Header("Paths")]
        public string repackExePath = "";
        public string inputFolder = "";
        public string outputSCOPath = "";
        
        public SectionMode missionObjectsMode = SectionMode.Repack;
        public SectionMode aiMeshMode = SectionMode.Repack;
        public SectionMode terrainMode = SectionMode.Repack;
        
        [Header("Donor Paths (when mode = Donor)")]
        public string missionObjectsDonor = "";
        public string aiMeshDonor = "";
        public string terrainDonor = "";
        
        [Header("Post-Process")]
        public bool copyToDestination = false;
        public string destinationFolder = "";
        public bool openFolderAfter = false;
        
        /// <summary>Validate settings before execution</summary>
        public bool Validate(out string error)
        {
            if (string.IsNullOrEmpty(repackExePath))
            {
                error = "Repack executable path is not set";
                return false;
            }
            
            if (!File.Exists(repackExePath))
            {
                error = $"Repack executable not found: {repackExePath}";
                return false;
            }
            
            if (string.IsNullOrEmpty(inputFolder))
            {
                error = "Input folder is not set";
                return false;
            }
            
            if (!Directory.Exists(inputFolder))
            {
                error = $"Input folder not found: {inputFolder}";
                return false;
            }
            
            // Validate donor paths if needed
            if (missionObjectsMode == SectionMode.Donor && !File.Exists(missionObjectsDonor))
            {
                error = $"Mission objects donor not found: {missionObjectsDonor}";
                return false;
            }
            
            if (aiMeshMode == SectionMode.Donor && !File.Exists(aiMeshDonor))
            {
                error = $"AI mesh donor not found: {aiMeshDonor}";
                return false;
            }
            
            if (terrainMode == SectionMode.Donor && !File.Exists(terrainDonor))
            {
                error = $"Terrain donor not found: {terrainDonor}";
                return false;
            }
            
            error = null;
            return true;
        }
        
        /// <summary>Auto-generate output path from input folder</summary>
        public void AutoGenerateOutputPath()
        {
            if (!string.IsNullOrEmpty(inputFolder) && string.IsNullOrEmpty(outputSCOPath))
            {
                outputSCOPath = inputFolder + ".sco";
            }
        }
    }
    
    #endregion
    
    #region Result
    
    /// <summary>Result of SCO repack operation</summary>
    public class SCORepackResult
    {
        public bool success;
        public int exitCode;
        public string message;
        public string standardOutput;
        public string standardError;
        public string outputPath;
        public string copiedToPath;
        public Exception exception;
        
        public static SCORepackResult Success(string outputPath, string stdout, string copiedTo = null)
        {
            return new SCORepackResult
            {
                success = true,
                exitCode = 0,
                message = "SCO repacking completed successfully",
                standardOutput = stdout,
                outputPath = outputPath,
                copiedToPath = copiedTo
            };
        }
        
        public static SCORepackResult Failure(string message, int exitCode = -1, string stderr = null, Exception ex = null)
        {
            return new SCORepackResult
            {
                success = false,
                exitCode = exitCode,
                message = message,
                standardError = stderr,
                exception = ex
            };
        }
    }
    
    #endregion
    
    #region Main API
    
    /// <summary>
    /// Execute SCO repacking with the given settings.
    /// </summary>
    /// <param name="settings">Repack settings</param>
    /// <returns>Result of the operation</returns>
    public static SCORepackResult Repack(SCORepackSettings settings)
    {
        // Validate
        if (!settings.Validate(out string validationError))
        {
            return SCORepackResult.Failure(validationError);
        }
        
        // Auto-generate output if not set
        settings.AutoGenerateOutputPath();
        
        try
        {
            string args = BuildCommandArgs(settings);
            
            ProcessStartInfo startInfo = new ProcessStartInfo
            {
                FileName = settings.repackExePath,
                Arguments = args,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                WorkingDirectory = Path.GetDirectoryName(settings.inputFolder)
            };
            
            Debug.Log($"[MBSCOHelpers] Running: {settings.repackExePath} {args}");
            
            using (Process process = new Process())
            {
                process.StartInfo = startInfo;
                process.Start();
                
                string stdout = process.StandardOutput.ReadToEnd();
                string stderr = process.StandardError.ReadToEnd();
                
                process.WaitForExit();
                
                if (process.ExitCode != 0)
                {
                    string errorMessage = process.ExitCode switch
                    {
                        1 => "Input folder does not exist or is invalid",
                        2 => "Layer image dimensions don't match - all layer_*.pgm files must have the same width/height",
                        4 => "Non-manifold edge detected in AI mesh",
                        69 => "Donor file data is missing",
                        _ => $"Unknown error (exit code {process.ExitCode})"
                    };
                    
                    string errorDetails = $"Repack failed: {errorMessage}";
                    if (!string.IsNullOrEmpty(stdout))
                        errorDetails += $"\n\nTool Output:\n{stdout}";
                    if (!string.IsNullOrEmpty(stderr))
                        errorDetails += $"\n\nStdErr:\n{stderr}";
                    
                    Debug.LogError($"[MBSCOHelpers] {errorDetails}");
                    
                    return SCORepackResult.Failure(
                        errorMessage,
                        process.ExitCode,
                        stderr);
                }
                
                // Post-process: copy to destination
                string copiedTo = null;
                if (settings.copyToDestination && 
                    !string.IsNullOrEmpty(settings.destinationFolder) &&
                    Directory.Exists(settings.destinationFolder) &&
                    File.Exists(settings.outputSCOPath))
                {
                    copiedTo = Path.Combine(settings.destinationFolder, 
                        Path.GetFileName(settings.outputSCOPath));
                    
                    bool copied = TryCopyFile(settings.outputSCOPath, copiedTo, out string copyError);
                    if (copied)
                    {
                        Debug.Log($"[MBSCOHelpers] Copied to: {copiedTo}");
                    }
                    else
                    {
                        Debug.LogWarning($"[MBSCOHelpers] Could not copy to destination: {copyError}");
                        copiedTo = null;
                    }
                }
                
                // Open folder if requested
                if (settings.openFolderAfter)
                {
                    string folderToOpen = !string.IsNullOrEmpty(copiedTo) 
                        ? Path.GetDirectoryName(copiedTo) 
                        : Path.GetDirectoryName(settings.outputSCOPath);
                    
                    OpenFolder(folderToOpen);
                }
                
                return SCORepackResult.Success(settings.outputSCOPath, stdout, copiedTo);
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"[MBSCOHelpers] Exception: {ex}");
            return SCORepackResult.Failure($"Exception during repack: {ex.Message}", ex: ex);
        }
    }
    
    /// <summary>
    /// Simple repack with minimal settings.
    /// </summary>
    /// <param name="exePath">Path to mab_sco_repack.exe</param>
    /// <param name="inputFolder">Unpacked SCO folder</param>
    /// <param name="outputPath">Output SCO path (optional, auto-generated if null)</param>
    /// <returns>Result of the operation</returns>
    public static SCORepackResult RepackSimple(string exePath, string inputFolder, string outputPath = null)
    {
        var settings = new SCORepackSettings
        {
            repackExePath = exePath,
            inputFolder = inputFolder,
            outputSCOPath = outputPath ?? (inputFolder + ".sco")
        };
        
        return Repack(settings);
    }
    
    /// <summary>
    /// Unpack an SCO file to a folder.
    /// </summary>
    /// <param name="unpackExePath">Path to mab_sco_unpack.exe</param>
    /// <param name="inputScoFile">Input .sco file to unpack</param>
    /// <param name="outputFolder">Output folder for unpacked data (optional)</param>
    /// <returns>Result of the operation</returns>
    public static SCORepackResult Unpack(string unpackExePath, string inputScoFile, string outputFolder = null)
    {
        if (string.IsNullOrEmpty(unpackExePath))
            return SCORepackResult.Failure("Unpack executable path is not set");
        
        if (!File.Exists(unpackExePath))
            return SCORepackResult.Failure($"Unpack executable not found: {unpackExePath}");
        
        if (string.IsNullOrEmpty(inputScoFile))
            return SCORepackResult.Failure("Input SCO file is not set");
        
        if (!File.Exists(inputScoFile))
            return SCORepackResult.Failure($"Input SCO file not found: {inputScoFile}");
        
        try
        {
            // Build arguments: mab_sco_unpack.exe <input.sco> [output_folder]
            string args = $"\"{inputScoFile}\"";
            if (!string.IsNullOrEmpty(outputFolder))
            {
                Directory.CreateDirectory(outputFolder);
                args += $" \"{outputFolder}\"";
            }
            
            ProcessStartInfo startInfo = new ProcessStartInfo
            {
                FileName = unpackExePath,
                Arguments = args,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                WorkingDirectory = Path.GetDirectoryName(inputScoFile)
            };
            
            Debug.Log($"[MBSCOHelpers] Unpacking: {unpackExePath} {args}");
            
            using (Process process = new Process())
            {
                process.StartInfo = startInfo;
                process.Start();
                
                string stdout = process.StandardOutput.ReadToEnd();
                string stderr = process.StandardError.ReadToEnd();
                
                process.WaitForExit();
                
                if (process.ExitCode != 0)
                {
                    return SCORepackResult.Failure(
                        $"Unpack failed with exit code {process.ExitCode}",
                        process.ExitCode,
                        stderr);
                }
                
                return SCORepackResult.Success(outputFolder ?? Path.GetDirectoryName(inputScoFile), stdout);
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"[MBSCOHelpers] Unpack exception: {ex}");
            return SCORepackResult.Failure($"Exception during unpack: {ex.Message}", ex: ex);
        }
    }
    
    /// <summary>
    /// Build command line arguments preview without executing.
    /// Useful for debugging or showing user what will be run.
    /// </summary>
    public static string PreviewCommand(SCORepackSettings settings)
    {
        return $"\"{settings.repackExePath}\" {BuildCommandArgs(settings)}";
    }
    
    #endregion
    
    #region Utility Methods
    
    /// <summary>
    /// Try to copy a file with retry logic for locked files.
    /// Will attempt to force-overwrite by clearing readonly and retrying.
    /// </summary>
    /// <param name="source">Source file path</param>
    /// <param name="destination">Destination file path</param>
    /// <param name="error">Error message if failed</param>
    /// <param name="maxRetries">Maximum retry attempts</param>
    /// <param name="retryDelayMs">Delay between retries in milliseconds</param>
    /// <returns>True if copy succeeded</returns>
    public static bool TryCopyFile(string source, string destination, out string error, int maxRetries = 3, int retryDelayMs = 100)
    {
        error = null;
        
        for (int attempt = 0; attempt < maxRetries; attempt++)
        {
            try
            {
                // Try to clear readonly attribute if file exists
                if (File.Exists(destination))
                {
                    try
                    {
                        FileAttributes attrs = File.GetAttributes(destination);
                        if ((attrs & FileAttributes.ReadOnly) == FileAttributes.ReadOnly)
                        {
                            File.SetAttributes(destination, attrs & ~FileAttributes.ReadOnly);
                        }
                    }
                    catch
                    {
                        // Ignore attribute errors, try copy anyway
                    }
                    
                    // Try to delete first to release any handles
                    try
                    {
                        File.Delete(destination);
                    }
                    catch
                    {
                        // If delete fails, try overwrite copy
                    }
                }
                
                // Attempt copy
                File.Copy(source, destination, true);
                return true;
            }
            catch (IOException ex) when (attempt < maxRetries - 1)
            {
                // File might be locked, wait and retry
                error = ex.Message;
                System.Threading.Thread.Sleep(retryDelayMs);
            }
            catch (UnauthorizedAccessException ex) when (attempt < maxRetries - 1)
            {
                // Permission issue, wait and retry
                error = ex.Message;
                System.Threading.Thread.Sleep(retryDelayMs);
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
        }
        
        return false;
    }
    
    /// <summary>
    /// Force copy a file, killing any processes that might have it locked.
    /// Use with caution - this is aggressive.
    /// </summary>
    public static bool ForceCopyFile(string source, string destination, out string error)
    {
        error = null;
        
        // First try normal copy
        if (TryCopyFile(source, destination, out error))
            return true;
        
        // If that failed, try using shell copy (cmd /c copy)
        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = "cmd.exe",
                Arguments = $"/c copy /Y \"{source}\" \"{destination}\"",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };
            
            using (var process = Process.Start(startInfo))
            {
                process.WaitForExit(5000);
                if (process.ExitCode == 0)
                    return true;
                    
                error = process.StandardError.ReadToEnd();
            }
        }
        catch (Exception ex)
        {
            error = $"Shell copy failed: {ex.Message}";
        }
        
        return false;
    }
    
    private static string BuildCommandArgs(SCORepackSettings settings)
    {
        string args = $"\"{settings.inputFolder}\"";
        
        if (!string.IsNullOrEmpty(settings.outputSCOPath))
        {
            args += $" -o \"{settings.outputSCOPath}\"";
        }
        
        args += $" -mo {GetSectionArg(settings.missionObjectsMode, settings.missionObjectsDonor)}";
        args += $" -ai {GetSectionArg(settings.aiMeshMode, settings.aiMeshDonor)}";
        args += $" -te {GetSectionArg(settings.terrainMode, settings.terrainDonor)}";
        
        return args;
    }
    
    private static string GetSectionArg(SectionMode mode, string donorPath)
    {
        return mode switch
        {
            SectionMode.Repack => "repack",
            SectionMode.Keep => "keep",
            SectionMode.Empty => "empty",
            SectionMode.Donor => $"\"{donorPath}\"",
            _ => "repack"
        };
    }
    
    /// <summary>Open a folder in the system file explorer</summary>
    public static void OpenFolder(string path)
    {
        if (string.IsNullOrEmpty(path) || !Directory.Exists(path))
            return;
        
#if UNITY_EDITOR_WIN
        Process.Start("explorer.exe", path.Replace("/", "\\"));
#elif UNITY_EDITOR_OSX
        Process.Start("open", path);
#elif UNITY_EDITOR_LINUX
        Process.Start("xdg-open", path);
#endif
    }
    
    /// <summary>Open file location in system file explorer</summary>
    public static void RevealInExplorer(string filePath)
    {
        if (string.IsNullOrEmpty(filePath))
            return;
        
#if UNITY_EDITOR
        UnityEditor.EditorUtility.RevealInFinder(filePath);
#else
        OpenFolder(Path.GetDirectoryName(filePath));
#endif
    }
    
    /// <summary>Check if SCO repack executable exists at path</summary>
    public static bool ValidateExePath(string exePath)
    {
        return !string.IsNullOrEmpty(exePath) && File.Exists(exePath);
    }
    
    /// <summary>Check if folder looks like an unpacked SCO folder</summary>
    public static bool ValidateUnpackedFolder(string folderPath)
    {
        if (string.IsNullOrEmpty(folderPath) || !Directory.Exists(folderPath))
            return false;
        
        // Check for common SCO unpacked folder contents
        // Typically has mission_objects.txt, terrain files, etc.
        string[] expectedFiles = { "mission_objects.txt", "ai_mesh.txt" };
        
        foreach (var file in expectedFiles)
        {
            if (File.Exists(Path.Combine(folderPath, file)))
                return true;
        }
        
        // Also valid if it's just a folder with scene name pattern
        string folderName = Path.GetFileName(folderPath);
        return folderName.StartsWith("scn_") || folderName.Contains("scene");
    }
    
    #endregion
    
    #region Batch Operations
    
    /// <summary>
    /// Repack multiple SCO folders.
    /// </summary>
    /// <param name="exePath">Path to repack executable</param>
    /// <param name="inputFolders">List of unpacked SCO folders</param>
    /// <param name="outputDirectory">Directory to save all output SCO files</param>
    /// <returns>List of results for each operation</returns>
    public static SCORepackResult[] RepackBatch(string exePath, string[] inputFolders, string outputDirectory = null)
    {
        var results = new SCORepackResult[inputFolders.Length];
        
        for (int i = 0; i < inputFolders.Length; i++)
        {
            string inputFolder = inputFolders[i];
            string outputPath = null;
            
            // if (!string.IsNullOrEmpty(outputDirectory))
            // {
            //     string scoName = Path.GetFileName(inputFolder) + ".sco";
            //     outputPath = Path.Combine(outputDirectory, scoName);
            // }
            
            results[i] = RepackSimple(exePath, inputFolder, outputDirectory);
            
            Debug.Log($"[MBSCOHelpers] Batch {i + 1}/{inputFolders.Length}: " +
                      $"{(results[i].success ? "Success" : "Failed")} - {inputFolder}");
        }
        
        return results;
    }
    
    #endregion
}