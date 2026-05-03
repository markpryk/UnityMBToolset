using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEngine;
using UnityEngine.ProBuilder;

/// <summary>
/// Minimal .obj file parser that produces ProBuilder-compatible vertex/face data.
///
/// Supports:
///   - v  (vertices)
///   - f  (faces with v, v/vt, v//vn, v/vt/vn formats; fan-triangulated for quads/ngons)
///
/// Does NOT support:
///   - object groups across multiple meshes (takes first renderable geometry only)
///   - materials, normals or UV reconstruction (ProBuilder recalculates everything)
///
/// Coordinate conversion:
///   When swapYZ=true the input is assumed to be in M&amp;B Z-up space. Each vertex
///   (x,y,z) is remapped to Unity Y-up space as (x,z,y).  Face winding is also
///   reversed to undo the reflection that was written during export.
/// </summary>
public static class SimpleObjParser
{
    /// <summary>
    /// Parse an OBJ file into ProBuilder-compatible data.
    /// </summary>
    /// <param name="path">Absolute path to the .obj file.</param>
    /// <param name="positions">Parsed vertex positions in Unity space.</param>
    /// <param name="faces">ProBuilder Face list (one per triangle).</param>
    /// <param name="swapYZ">
    ///   True  → assumes the OBJ was exported in M&amp;B Z-up space; applies Y↔Z swap + winding
    ///           reversal to restore Unity Y-up space.<br/>
    ///   False → takes coordinates as-is.
    /// </param>
    /// <param name="error">Error description if the method returns false.</param>
    /// <returns>True on success, false if the file could not be parsed.</returns>
    public static bool TryParse(
        string          path,
        out List<Vector3> positions,
        out List<Face>    faces,
        bool            swapYZ,
        out string      error)
    {
        positions = new List<Vector3>();
        faces     = new List<Face>();
        error     = null;

        if (!File.Exists(path))
        {
            error = $"File not found: {path}";
            return false;
        }

        try
        {
            string[] lines = File.ReadAllLines(path);

            foreach (string raw in lines)
            {
                string line = raw.Trim();
                if (line.Length == 0 || line[0] == '#') continue;

                if (line.StartsWith("v ", StringComparison.Ordinal))
                {
                    string[] p = Split(line);
                    if (p.Length < 4) continue;

                    float x = Float(p[1]);
                    float y = Float(p[2]);
                    float z = Float(p[3]);

                    // M&B exports (x,z,y) → restore to Unity (x,y,z) by swapping y↔z
                    positions.Add(swapYZ ? new Vector3(x, z, y) : new Vector3(x, y, z));
                }

                else if (line.StartsWith("f ", StringComparison.Ordinal))
                {
                    string[] p = Split(line);
                    if (p.Length < 4) continue;   // need at least 3 face vertices

                    // Collect 0-based indices (handle v, v/vt, v//vn, v/vt/vn)
                    int vertexCount = p.Length - 1;
                    int[] idx = new int[vertexCount];
                    bool valid = true;

                    for (int i = 0; i < vertexCount; i++)
                    {
                        string token  = p[i + 1].Split('/')[0];
                        if (!int.TryParse(token, NumberStyles.Integer,
                                         CultureInfo.InvariantCulture, out int v))
                        {
                            valid = false;
                            break;
                        }
                        // OBJ is 1-based; negative = relative to current vertex count
                        idx[i] = v > 0 ? v - 1 : positions.Count + v;
                    }

                    if (!valid) continue;

                    // Fan triangulation: pivot at idx[0], walk remaining vertices
                    // If swapYZ is active, reverse winding to undo the export's reflection
                    for (int i = 1; i < idx.Length - 1; i++)
                    {
                        int a = idx[0];
                        int b = idx[i];
                        int c = idx[i + 1];

                        // Validate indices are in range (guard against malformed OBJ)
                        if (a >= positions.Count || b >= positions.Count || c >= positions.Count)
                            continue;

                        faces.Add(swapYZ
                            ? new Face(new[] { a, c, b })   // undo export's winding reversal
                            : new Face(new[] { a, b, c }));
                    }
                }
            }

            if (positions.Count == 0)
            {
                error = "No vertex data found in OBJ file.";
                return false;
            }

            if (faces.Count == 0)
            {
                error = "No face data found in OBJ file.";
                return false;
            }

            return true;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return false;
        }
    }


    private static string[] Split(string line) =>
        line.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);

    private static float Float(string s) =>
        float.Parse(s, CultureInfo.InvariantCulture);
}
