using UnityEngine;

[System.Serializable]
[CreateAssetMenu(fileName = "ImporterPropsBuildData", menuName = "SceneImporter/ImporterPropsBuildData")]
public class ImporterBuildData : ScriptableObject
{
    [SerializeField] private GameObject _entryPointPrefab;
    [SerializeField] private GameObject _passagePrefab;
    [SerializeField] private Material _terrainBaseMaterial;
    [SerializeField] private Material _foundedMaterial;
    [SerializeField] private Material _missingMaterial;
    [SerializeField] private Material _entryMaterial;
    [SerializeField] private Material _passageMaterial;
    [SerializeField] private Material _barrierMaterial;

    public GameObject EntryPointPrefab => _entryPointPrefab;
    public GameObject PassagePrefab => _passagePrefab;
    public Material TerrainBaseMaterial => _terrainBaseMaterial;
    public Material FoundedMaterial => _foundedMaterial;
    public Material MissingMaterial => _missingMaterial;
    public Material EntryMaterial => _entryMaterial;
    public Material PassageMaterial => _passageMaterial;
    public Material BarrierMaterial => _barrierMaterial;
}