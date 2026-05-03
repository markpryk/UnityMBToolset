using System;
using System.Diagnostics;
using System.IO;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace MountAndBlade.ModdingToolkit
{
    /// <summary>
    /// Handles communication with the BRF Synchronizer tool.
    /// Replaces the old BRFProcessHandler with the new JSON-based workflow.
    /// </summary>
    public static class BRFSyncHandler
    {
        public enum SyncOperation
        {
            Export, // BRF -> Folders (full: JSON + OBJ/SMD)
            Import, // Folders -> BRF (export from Unity)
            Info    // BRF -> Folder (fast: JSON metadata only, no OBJ/SMD)
        }

        public struct SyncResult
        {
            public bool Success;
            public string Message;
            public string OutputPath;
            public string DataJsonPath;
        }

        /// <summary>
        /// Gets the path to the BRF Sync tool executable from settings.
        /// </summary>
        private static string ToolPath => MBPathHelpers.BRFSyncToolPath();

        /// <summary>
        /// Export a BRF file to a folder structure with data.json.
        /// Use this when importing M&B resources INTO Unity.
        /// </summary>
        /// <param name="brfFilePath">Path to the .brf file</param>
        /// <param name="outputFolderPath">Destination folder for exported assets</param>
        /// <returns>Result containing success status and paths</returns>
        public static SyncResult ExportBRF(string brfFilePath, string outputFolderPath)
        {
            return ExecuteSync(SyncOperation.Export, brfFilePath, outputFolderPath);
        }

        /// <summary>
        /// Import a folder structure back into a BRF file.
        /// Use this when exporting modified assets FROM Unity back to M&B.
        /// </summary>
        /// <param name="inputFolderPath">Folder containing data.json and asset subfolders</param>
        /// <param name="brfFilePath">Destination .brf file path</param>
        /// <returns>Result containing success status</returns>
        public static SyncResult ImportToBRF(string inputFolderPath, string brfFilePath)
        {
            return ExecuteSync(SyncOperation.Import, inputFolderPath, brfFilePath);
        }

        /// <summary>
        /// Fast metadata-only scan of a BRF file.
        /// Generates only data.json without exporting OBJ/SMD files.
        /// Use this for change detection scans where you only need to compare metadata.
        /// Typically ~10x faster than full ExportBRF.
        /// </summary>
        /// <param name="brfFilePath">Path to the .brf file</param>
        /// <param name="outputFolderPath">Destination folder for data.json</param>
        /// <returns>Result containing success status and data.json path</returns>
        public static SyncResult InfoBRF(string brfFilePath, string outputFolderPath)
        {
            return ExecuteSync(SyncOperation.Info, brfFilePath, outputFolderPath);
        }

        /// <summary>
        /// Process all BRF files in a source directory.
        /// </summary>
        /// <param name="sourceDirectory">Directory containing .brf files</param>
        /// <param name="outputDirectory">Base output directory</param>
        /// <param name="progressCallback">Optional callback for progress updates</param>
        /// <returns>Number of successfully processed files</returns>
        public static int ExportAllBRFs(string sourceDirectory, string outputDirectory, 
            Action<string, float> progressCallback = null)
        {
            if (!Directory.Exists(sourceDirectory))
            {
                Debug.LogError($"Source directory not found: {sourceDirectory}");
                return 0;
            }

            var brfFiles = Directory.GetFiles(sourceDirectory, "*.brf", SearchOption.TopDirectoryOnly);
            int successCount = 0;
            int total = brfFiles.Length;

            for (int i = 0; i < brfFiles.Length; i++)
            {
                string brfFile = brfFiles[i];
                string brfName = Path.GetFileNameWithoutExtension(brfFile);
                string outputPath = Path.Combine(outputDirectory, brfName);

                progressCallback?.Invoke($"Exporting {brfName}...", (float)i / total);

                var result = ExportBRF(brfFile, outputPath);
                if (result.Success)
                {
                    successCount++;
                    Debug.Log($"Exported: {brfName}");
                }
                else
                {
                    Debug.LogWarning($"Failed to export {brfName}: {result.Message}");
                }
            }

            progressCallback?.Invoke("Complete", 1f);
            return successCount;
        }

        private static SyncResult ExecuteSync(SyncOperation operation, string inputPath, string outputPath)
        {
            var result = new SyncResult { Success = false };

            // Validate tool path
            if (string.IsNullOrEmpty(ToolPath) || !File.Exists(ToolPath))
            {
                result.Message = $"BRF Sync tool not found at: {ToolPath}. Configure path in MBEditorSettings.";
                Debug.LogError(result.Message);
                return result;
            }

            // Validate input
            bool inputIsFile = File.Exists(inputPath);
            bool inputIsDir = Directory.Exists(inputPath);

            if ((operation == SyncOperation.Export || operation == SyncOperation.Info) && !inputIsFile)
            {
                result.Message = $"BRF file not found: {inputPath}";
                Debug.LogError(result.Message);
                return result;
            }

            if (operation == SyncOperation.Import && !inputIsDir)
            {
                result.Message = $"Input folder not found: {inputPath}";
                Debug.LogError(result.Message);
                return result;
            }

            // Ensure output directory exists for export/info
            if (operation == SyncOperation.Export || operation == SyncOperation.Info)
            {
                if (!Directory.Exists(outputPath))
                    Directory.CreateDirectory(outputPath);
            }
            else
            {
                // For import, ensure parent directory exists
                string parentDir = Path.GetDirectoryName(outputPath);
                if (!string.IsNullOrEmpty(parentDir) && !Directory.Exists(parentDir))
                    Directory.CreateDirectory(parentDir);
            }

            // Build command
            string command = operation switch
            {
                SyncOperation.Export => "export",
                SyncOperation.Import => "import",
                SyncOperation.Info   => "info",
                _ => throw new ArgumentException($"Unknown operation: {operation}")
            };
            string arguments = $"{command} \"{inputPath}\" \"{outputPath}\"";

            var processInfo = new ProcessStartInfo
            {
                FileName = ToolPath,
                Arguments = arguments,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            try
            {
                using (var process = Process.Start(processInfo))
                {
                    string output = process.StandardOutput.ReadToEnd();
                    string error = process.StandardError.ReadToEnd();
                    process.WaitForExit();

                    if (process.ExitCode == 0)
                    {
                        result.Success = true;
                        result.Message = output;
                        result.OutputPath = outputPath;

                        if (operation == SyncOperation.Export)
                        {
                            result.DataJsonPath = Path.Combine(outputPath, "data.json");
                        }

                        Debug.Log($"BRF Sync {command} completed: {outputPath}");
                    }
                    else
                    {
                        result.Message = string.IsNullOrEmpty(error) ? output : error;
                        Debug.LogError($"BRF Sync {command} failed: {result.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                result.Message = $"Exception during BRF Sync: {ex.Message}";
                Debug.LogError(result.Message);
            }

            return result;
        }

        /// <summary>
        /// Validates that the BRF Sync tool is configured and accessible.
        /// </summary>
        public static bool ValidateToolSetup(out string errorMessage)
        {
            errorMessage = null;

            if (string.IsNullOrEmpty(ToolPath))
            {
                errorMessage = "BRF Sync tool path not configured in MBEditorSettings.";
                return false;
            }

            if (!File.Exists(ToolPath))
            {
                errorMessage = $"BRF Sync tool not found at: {ToolPath}";
                return false;
            }

            return true;
        }
    }
}
