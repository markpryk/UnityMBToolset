using System.IO;
using UnityEngine;

public static class MBPathHelpers
{
    public const string MBEditorSettings = "Assets/MBEditor/Core/ConfigsData/MBEditorSettings.asset";
    public const string MBModulesFolder = "Assets/MBModules";

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
        return $"{Application.dataPath}/MBEditor/ExternalTools/texconv.exe";
    }
    
    public static string MBDataJsonConverterPath()
    {
        return $"{Application.dataPath}/MBEditor/ExternalTools/mb_data_to_json.exe";
    }
    public static string MBDataBaseJsonConvertersPath()
    {
        return $"{Application.dataPath}/MBEditor/ExternalTools/Converters";
    }
    
    public static string MBScoUnpackToolPath()
    {
        return Application.dataPath + "/MBEditor/ExternalTools/MabScoTools/mab_sco_unpack.exe";
    }
    
    public static string MBScoRepackToolPath()
    {
        return Application.dataPath + "/MBEditor/ExternalTools/MabScoTools/mab_sco_repack.exe";
    }
    
    public static string MBTerrainGeneratorToolPath()
    {
        return $"{Application.dataPath}/MBEditor/ExternalTools/TerrainGenerator/TerrainGenerator.exe";
    }
    
    public static string BRFSyncToolPath()
    {
        return $"{Application.dataPath}/MBEditor/ExternalTools/BrfSync/brf_sync.exe";
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
        return "Assets/MBEditor/Core/ConfigsData/Scenes/SceneImporterTemplate.unity";
    }       
    public static string CommonImporterPropsBuildData()
    {
        return "Assets/MBEditor/Core/ConfigsData/ImporterBuildData.asset";
    }   
    public static string CommonTerrainPalette()
    {
        return "Assets/MBEditor/Core/ConfigsData/TerrainPalette/TerrainPalette.asset";
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
        // each brf represent a folder inside Resource
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

    public static string ModSourcePrefabBRFPath(string moduleName,string brfName)
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

    public static string ModScenePath(string moduleName,string sceneName)
    {
        return $"{Application.dataPath}/MBModules/{moduleName}/MBScenes/{sceneName}";
    }
    public static string ModSceneDataGrassMaskPath(string moduleName,string sceneName,string extension = ".png")
    {
        return $"{Application.dataPath}/MBModules/{moduleName}/MBScenes/{sceneName}/TerrainData/grass_mask{extension}";
    }
    #endregion

}