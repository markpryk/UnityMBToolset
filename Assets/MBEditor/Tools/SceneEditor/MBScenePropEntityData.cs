using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class MBScenePropEntityData
{
    public string type;
    public long  id;
    public float[][] rotation_matrix; // Nested array for the matrix
    public float[] pos;
    public string str;
    public int entry_no;
    public int menu_entry_no;
    public float[] scale;
}

[System.Serializable]
public class PropList
{
    public List<MBScenePropEntityData> props;
}