using UnityEngine;

/// <summary>
/// ScriptableObject representation of a Mount & Blade Warband troop.
/// Corresponds to item tuples in module_troops.py
/// </summary>
[CreateAssetMenu(fileName = "New MB Troop", menuName = "Mount & Blade/Troop Data", order = 1)]
public class MBTroopData : ScriptableObject
{
    public string TroopID;
    public string TroopName;
    public string TroopNamePlural;
    public string TroopFlags;
    public string Scene;
    public int EntryPoint = -1;
    public string FactionID;
    public string TroopAttributes;
    public string WeaponProficiencies;
    public string TroopSkills;
    public string FaceCode1;
    public string FaceCode2;
    public string TroopImageMesh;
    public string[] Inventory;
    public string[] UpgradePaths;
}
