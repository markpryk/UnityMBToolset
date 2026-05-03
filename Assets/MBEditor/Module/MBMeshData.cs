using System;
using System.Collections.Generic;

[Serializable]
public class MBMeshData
{
    public string name;
    public string material;

    public Nullable<int> faceCount;
    public Nullable<int> flags;
    public Nullable<int> vertexCount;

    public List<List<int>> faces;
    public List<Vertex> vertices;

    [Serializable]
    public class Vertex
    {
        public Nullable<uint> color;
        public Nullable<int> index;
        public Nullable<float>[] ta;
        public Nullable<float>[] tb;
    }
}