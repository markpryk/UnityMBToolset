using System.IO;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor.PackageManager;
#endif

public static class MBPathHelpers
{
    public const string MBModulesFolder = "Assets/MBModules";

    // -----------------------------------------------------------------------
    // Package root resolution
    // Works whether MBToolset is installed from Assets/ or Packages/ (git URL).
    // Returns a Unity-relative path such as:
    //   "Assets/MBEditor"          (local Assets install)
    //   "Packages/com.markpryk.mbtoolset"  (UPM git install)
    // -----------------------------------------------------------------------
    private static string _packageRoot;
    public static string PackageRoot
    {
        get
        {
            if (_packageRoot != null) return _packageRoot;
#if UNITY_EDITOR
            var info = UnityEditor.PackageManager.PackageInfo.FindForAssembly(typeof(MBPathHelpers).Assembly);
            if (info != null)
            {
                _packageRoot = info.assetPath; // e.g. "Packages/com.markpryk.mbtoolset"
                return _packageRoot;
            }
#endif
            // Fallback: legacy Assets/MBEditor layout
            _packageRoot = "Assets/MBEditor";
            return _packageRoot;
        }
    }

    // Package-internal asset path helper
    public static string PackagePath(string relativePath)
    {
        return $"{PackageRoot}/{relativePath}";
    }

    // Package-internal absolute filesystem path helper (for exe tools etc.)
    public static string PackageFullPath(string relativePath)
    {
        var projectRoot = Path.GetDirectoryName(Application.dataPath);
        return Path.GetFullPath(Path.Combine(projectRoot, PackageRoot, relativePath)).Replace('\\', '/');
    }

    // MBEditorSettings asset path – always package-internal
    public static string MBEditorSettings => PackagePath("Core/ConfigsData/MBEditorSettings.asset");

    public static string UnityProjectFolderPath()
    {
        return Path.GetDirectoryName(Application.dataPath);
    }

    public static string FullToUnityPath(string path)
    {
        return path.Replace(Application.dataPath, "Assets");
    }

    public static string ConvertToUnityPath(string assetPath)
    {
        assetPath = Path.GetFullPath(assetPath).Replace('\\', '/');
        string dataPath = Path.GetFullPath(Application.dataPath).Replace('\\', '/');
        return "Assets" + assetPath.Substring(dataPath.Length);
    }

    #region ExternalTools

    public static string TexConvPath()
    {
        return PackageFullPath("ExternalTools/texconv.exe");
    }

    public static string MBDataJsonConverterPath()
    {
        return PackageFullPath("ExternalTools/mb_data_to_json.exe");
    }

    public static string MBDataBaseJsonConvertersPath()
    {
        return PackageFullPath("ExternalTools/Converters");
    }

    public static string MBScoUnpackToolPath()
    {
        return PackageFullPath("ExternalTools/MabScoTools/mab_sco_unpack.exe");
    }

    public static string MBScoRepackToolPath()
    {
        return PackageFullPath("ExternalTools/MabScoTools/mab_sco_repack.exe");
    }

    public static string MBTerrainGeneratorToolPath()
    {
        return PackageFullPath("ExternalTools/TerrainGenerator/TerrainGenerator.exe");
    }

    public static string BRFSyncToolPath()
    {
        return PackageFullPath("ExternalTools/BrfSync/brf_sync.exe");
    }

    #endregion

    public static string EngineDataFolder()
    {
        return MBEditorManager.MbEditorSettings.MbPath + "/Data";
    }
    public static string EngineModulesFolder()
    {
        return MBEditorManager.MbEditorSettings.MbPath + "/Modules";
    }
    public static string EngineModuleScenesFolder(string moduleName)
    {
        return MBEditorManager.MbEditorSettings.MbPath + $"/Modules/{moduleName}/SceneObj";
    }

    public static string CommonImporterScene()
    {
        return PackagePath("Core/ConfigsData/Scenes/SceneImporterTemplate.unity");
    }
    public static string CommonImporterPropsBuildData()
    {
        return PackagePath("Core/ConfigsData/ImporterBuildData.asset");
    }
    public static string CommonTerrainPalette()
    {
        return PackagePath("Core/ConfigsData/TerrainPalette/TerrainPalette.asset");
    }

    #region ModPaths

    public static string ModPath(string moduleName)
    {
        return $"{MBModulesFolder}/{moduleName}";
    }

    public static string ModModelsDataBasePath(string moduleName)
    {
        return $"{MBModulesFolder}/{moduleName}/Configs/ModelsDataBase.asset";
    }
    public static string ModBRFDataBasePath(string moduleName)
    {
        return $"{MBModulesFolder}/{moduleName}/Configs/BRFDataBase.asset";
    }

    public static string ModDataBaseDirectoryPath(string moduleName)
    {
        return $"{MBModulesFolder}/{moduleName}/DataBase/Data";
    }

    public static string ModDataBaseJsonDirectoryPath(string moduleName)
    {
        return $"{MBModulesFolder}/{moduleName}/DataBase/JSON";
    }
    public static string ModPrefabParticlesDataPath(string moduleName)
    {
        return $"{MBModulesFolder}/{moduleName}/DataBase/ParticleSystems";
    }
    public static string ModAssetPath(string moduleName)
    {
        return $"{MBModulesFolder}/{moduleName}/{moduleName}.asset";
    }
    public static string ModINIPath(string moduleName)
    {
        return $"{MBModulesFolder}/{moduleName}/{moduleName}_INI.asset";
    }

    public static string ModTexturesPath(string moduleName)
    {
        return $"{MBModulesFolder}/{moduleName}/Textures";
    }

    public static string ModResourcePath(string moduleName)
    {
        return $"{MBModulesFolder}/{moduleName}/Resource";
    }

    public static string ModMaterialsPath(string moduleName)
    {
        return $"{MBModulesFolder}/{moduleName}/Materials";
    }

    public static string ModPrefabsPath(string moduleName)
    {
        return $"{MBModulesFolder}/{moduleName}/Prefabs";
    }
    public static string ModPrefabItemsPath(string moduleName)
    {
        return $"{MBModulesFolder}/{moduleName}/Prefabs/Items";
    }

    public static string ModPrefabScenePropsPath(string moduleName)
    {
        return $"{MBModulesFolder}/{moduleName}/Prefabs/SceneProps";
    }

    public static string ModPrefabFloraPath(string moduleName)
    {
        return $"{MBModulesFolder}/{moduleName}/Prefabs/Flora";
    }

    public static string ModSourcePrefabBRFPath(string moduleName, string brfName)
    {
        return $"{ModResourcePath(moduleName)}/{brfName}/Prefabs";
    }
    public static string ModConfigsPath(string moduleName)
    {
        return $"{MBModulesFolder}/{moduleName}/Configs";
    }

    public static string ModSourcePath(string moduleName)
    {
        return $"{MBEditorManager.MbEditorSettings.MbPath}/Modules/{moduleName}";
    }

    public static string ModDataPath(string moduleName)
    {
        return $"{Application.dataPath}/MBModules/{moduleName}/DataBase";
    }

    public static string ModFloraPrototypesPath(string moduleName)
    {
        return $"{Application.dataPath}/MBModules/{moduleName}/DataBase/FloraPrototypes";
    }
    public static string ModFloraLibraryAssetPath(string moduleName)
    {
        return $"{MBModulesFolder}/{moduleName}/Configs/{moduleName}_FloraLibrary.asset";
    }
    public static string ModFloraDataJsonPath(string moduleName)
    {
        return $"{Application.dataPath}/MBModules/{moduleName}/DataBase/JSON/flora_full.json";
    }
    public static string ModTerrainPalette(string moduleName)
    {
        return $"{MBModulesFolder}/{moduleName}/Configs/{moduleName}_TerrainPalette.asset";
    }

    public static string ModTerrainPaletteLayers(string moduleName)
    {
        return $"{MBModulesFolder}/{moduleName}/Configs/PaletteLayers";
    }

    public static string ModTerrainPaletteNormals(string moduleName)
    {
        return $"{MBModulesFolder}/{moduleName}/Configs/PaletteNormals";
    }

    #endregion

    #region ModScenePaths

    public static string ModScenesPath(string moduleName)
    {
        return $"{MBModulesFolder}/{moduleName}/MBScenes";
    }

    public static string ModScenesPathFull(string moduleName)
    {
        return $"{Application.dataPath}/MBModules/{moduleName}/MBScenes";
    }

    public static string ModScenePath(string moduleName, string sceneName)
    {
        return $"{Application.dataPath}/MBModules/{moduleName}/MBScenes/{sceneName}";
    }
    public static string ModSceneDataGrassMaskPath(string moduleName, string sceneName, string extension = ".png")
    {
        return $"{Application.dataPath}/MBModules/{moduleName}/MBScenes/{sceneName}/TerrainData/grass_mask{extension}";
    }
    #endregion
}