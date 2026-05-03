using System.Collections.Generic;
using UnityEngine;

public class BrfMesh
{
    public string Name { get; set; }
    public uint Flags { get; set; }
    public string Material { get; set; }
    public int LODLevel { get; set; } = 0;    // Default to 0
    public int PieceIndex { get; set; } = -1; // Default to -1
    public List<Vector3> Positions { get; set; } = new List<Vector3>();
    public List<Vector3> Normals { get; set; } = new List<Vector3>();
    public List<Vector2> UVs { get; set; } = new List<Vector2>();
    public List<int> Triangles { get; set; } = new List<int>();
    public List<BrfVert> Vertices { get; set; }
}


public class BrfVert
{
    public int Index { get; set; } // Vertex index
    public uint Color { get; set; } // Vertex color
    public Vector3 Normal { get; set; } // Vertex normal
    public Vector3 Tangent { get; set; } // Vertex tangent
    public float TangentHandedness { get; set; } // Tangent handedness
    public Vector2 TexCoordA { get; set; } // Texture coordinate A
    public Vector2 TexCoordB { get; set; } // Texture coordinate B
}

public class BrfFace
{
    public int[] Indices = new int[3]; // Triangle vertex indices
}


