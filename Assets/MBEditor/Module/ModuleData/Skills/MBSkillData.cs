using UnityEngine;

/// <summary>
/// ScriptableObject representation of a Mount & Blade Warband skill.
/// Corresponds to item tuples in module_skills.py
/// </summary>
[CreateAssetMenu(fileName = "New MB Skill", menuName = "Mount & Blade/Skill Data", order = 1)]
public class MBSkillData : ScriptableObject
{
    public string SkillID;
    public string SkillName;
    public string SkillFlags;
    public int MaxLevel;
    public string SkillDescription;
}