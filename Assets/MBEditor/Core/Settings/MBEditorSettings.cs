using System.IO;
using System.Linq;
using MountAndBlade.Data;
using UnityEditor;
using UnityEngine;

[CreateAssetMenu(fileName = "MBEditorSettings", menuName = "MBEditor/Settings")]
[System.Serializable]
public class MBEditorSettings : ScriptableObject
{
    [SerializeField] private string _mbPath;
    [SerializeField] private MBModule _currentModule;

    public string MbPath => _mbPath;

    public MBModule CurrentModule
    {
        get => _currentModule;
        set
        {
            _currentModule = value;
            SaveDataHelper.SaveScriptableObject(this);
        }
    }

    public void SetMbPath(string path)
    {
        _mbPath = path;
    }

    // Path Validation

    public bool MBValidPath()
    {
        if (string.IsNullOrEmpty(_mbPath) || !Directory.Exists(_mbPath))
            return false;

        bool hasExecutable = File.Exists(Path.Combine(_mbPath, "mb_warband.exe"));
        bool hasModules = Directory.Exists(Path.Combine(_mbPath, "Modules"));
        bool hasTextures = Directory.Exists(Path.Combine(_mbPath, "Textures"));

        return hasExecutable && hasModules && hasTextures;
    }

    // Native Import Status - multi-level checks

    /// <summary>
    /// Import readiness level for the Native module.
    /// Each level includes everything before it.
    /// </summary>
    public enum NativeStatus
    {
        /// <summary>No MBModule asset exists.</summary>
        NotImported,

        /// <summary>Module asset exists but BRF resources not imported yet.</summary>
        ModuleCreated,

        /// <summary>BRF resources are imported (meshes, textures, materials in per-BRF folders).</summary>
        ResourcesImported,

        /// <summary>BRF database is populated with resolved references.</summary>
        DatabasePopulated,

        /// <summary>Full pipeline complete: module data + BRF database + model prefabs.</summary>
        FullyImported
    }

    /// <summary>
    /// Evaluate how far the Native import has progressed.
    /// </summary>
    public NativeStatus GetNativeStatus()
    {
        // 1. Module asset must exist
        string moduleAssetPath = MBPathHelpers.ModAssetPath("Native");
        if (!File.Exists(moduleAssetPath))
            return NativeStatus.NotImported;

        // 2. Resources folder must have BRF subfolders with data.json
        string resourcePath = MBPathHelpers.ModResourcePath("Native");
        if (!Directory.Exists(resourcePath) || !HasBrfFoldersWithData(resourcePath))
            return NativeStatus.ModuleCreated;

        // 3. BRF database must exist and be populated
        string dbPath = MBPathHelpers.ModBRFDataBasePath("Native");
        var brfDb = AssetDatabase.LoadAssetAtPath<ModBrfDataBase>(dbPath);
        if (brfDb == null || brfDb.BrfCount == 0)
            return NativeStatus.ResourcesImported;

        // Check if database has actual resolved data
        bool hasResolvedData = brfDb.BrfAssets.Any(b =>
            b != null && (b.Meshes.Count > 0 || b.Materials.Count > 0));

        if (!hasResolvedData)
            return NativeStatus.ResourcesImported;

        // 4. Model prefabs must exist
        if (!MBEditorUtility.ResourcesHasModels("Native"))
            return NativeStatus.DatabasePopulated;

        return NativeStatus.FullyImported;
    }

    /// <summary>
    /// Quick check: is Native fully imported and ready for other modules to depend on it?
    /// </summary>
    public bool IsNativeReady()
    {
        return GetNativeStatus() == NativeStatus.FullyImported;
    }

    /// <summary>
    /// Backwards compat - returns true if at least the module asset exists.
    /// Use IsNativeReady() for the full check.
    /// </summary>
    public bool NativeCoreDataImported()
    {
        return GetNativeStatus() >= NativeStatus.ModuleCreated;
    }

    // Helpers

    private static bool HasBrfFoldersWithData(string resourcePath)
    {
        if (!Directory.Exists(resourcePath)) return false;

        foreach (var dir in Directory.GetDirectories(resourcePath))
        {
            if (File.Exists(Path.Combine(dir, "data.json")))
                return true;
        }

        return false;
    }
}
