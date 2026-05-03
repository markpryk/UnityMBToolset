using System.Collections.Generic;
using MountAndBlade.Data;
using UnityEditor;
using UnityEngine;
using UnityEngine.Serialization;

[CreateAssetMenu(fileName = "MBModule", menuName = "MBEditor/MBModule")]
[System.Serializable]
public class MBModule : ScriptableObject
{
    public string name;
    public string ID;
    public string Path;
    public ModBrfDataBase BRFDataBase;
   
    [SerializeField]
    private MBModuleIni _moduleIni;

    public MBModuleIni ModuleIni
    {
        get
        {
            if(_moduleIni == null)
            {
                _moduleIni = AssetDatabase.LoadAssetAtPath<MBModuleIni>(MBPathHelpers.ModINIPath(ID));
            }
            
            return _moduleIni;
        }
        set 
        {
            _moduleIni = value;
        }
    }

    public string ModuleSystemPath;
    
    public MBGroundSpecsData groundSpecs;
    
    public List<MBFactionData> factions = new List<MBFactionData>();
    public List<MBFloraData> flora = new List<MBFloraData>();
    public List<MBItemData> items = new List<MBItemData>();
    public List<MBMapIconData> mapIcons = new List<MBMapIconData>();
    public List<MBPartyData> parties = new List<MBPartyData>();
    public List<MBPartyTemplateData> partyTemplates = new List<MBPartyTemplateData>();
    public List<MBScenePropData> sceneProps = new List<MBScenePropData>();
    public List<MBSceneData> scenes = new List<MBSceneData>();
    public List<MBSkinData> skins = new List<MBSkinData>();
    public List<MBTroopData> troops = new List<MBTroopData>();
    public List<MBParticleSystemData> particleSystems = new List<MBParticleSystemData>();
    
}