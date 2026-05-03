using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

public static class NativeTerrainUtils
{
   public static float[,] GenerateObjAndHeightmap(string filePath, string outputDirectory, bool flipNormals)
{
    if (!File.Exists(filePath))
    {
        EditorUtility.DisplayDialog("Error", "Input file not found!", "OK");
        return null;
    }

    try
    {
        var vertices = new List<Vector3>();
        var faces = new List<int[]>();
        List<float> heights = new List<float>();

        using (StreamReader reader = new StreamReader(filePath))
        {
            string line;
            bool parsingVertices = false;

            while ((line = reader.ReadLine()) != null)
            {
                // Check for the start of vertex data
                if (line.StartsWith("Vertices"))
                {
                    parsingVertices = true;
                    continue;
                }

                if (parsingVertices)
                {
                    string[] parts = line.Split();
                    if (parts.Length == 5 &&
                        float.TryParse(parts[2], out float posX) &&
                        float.TryParse(parts[3], out float posY) &&
                        float.TryParse(parts[4], out float posZ))
                    {
                        vertices.Add(new Vector3(posX, posZ, posY)); // Swap Y and Z for OBJ
                        heights.Add(posZ); // Use Z (height) for heightmap
                    }
                    else
                    {
                        Debug.LogWarning($"Skipped malformed line: {line}");
                    }
                }
            }
        }

        // Determine grid dimensions
        int rows = (int)Mathf.Sqrt(vertices.Count);
        int cols = vertices.Count / rows;

        for (int y = 0; y < rows - 1; y++)
        {
            for (int x = 0; x < cols - 1; x++)
            {
                // Calculate vertex indices for the current quad
                int v1 = y * cols + x + 1;
                int v2 = v1 + 1;
                int v3 = (y + 1) * cols + x + 1;
                int v4 = v3 + 1;

                // Flip winding order if flipNormals is true
                if (flipNormals)
                {
                    faces.Add(new int[] { v1, v2, v3 });
                    faces.Add(new int[] { v2, v4, v3 });
                }
                else
                {
                    faces.Add(new int[] { v1, v3, v2 });
                    faces.Add(new int[] { v2, v3, v4 });
                }
            }
        }

        // Write to OBJ
        using (StreamWriter writer = new StreamWriter(outputDirectory + "/terrain_base.obj"))
        {
            foreach (var vertex in vertices)
            {
                writer.WriteLine($"v {vertex.x} {vertex.y} {vertex.z}");
            }

            foreach (var face in faces)
            {
                writer.WriteLine($"f {face[0]} {face[1]} {face[2]}");
            }
        }

        // Generate and save heightmap
        SaveHeightmap(heights, rows, cols, outputDirectory + "/terrain_heightmap.png");

        return GetHeightMap(heights, rows, cols);
    }
    catch (System.Exception e)
    {
        EditorUtility.DisplayDialog("Error", $"An error occurred: {e.Message}", "OK");
        return null;
    }
}


    public static float GetMeshMinPoint(string filePath)
    {
        var vertices = new List<Vector3>();
        var faces = new List<int[]>();
        List<float> heights = new List<float>();

        using (StreamReader reader = new StreamReader(filePath))
        {
            string line;
            bool parsingVertices = false;

            while ((line = reader.ReadLine()) != null)
            {
                // Check for the start of vertex data
                if (line.StartsWith("Vertices"))
                {
                    parsingVertices = true;
                    continue;
                }

                if (parsingVertices)
                {
                    string[] parts = line.Split();
                    if (parts.Length == 5 &&
                        float.TryParse(parts[2], out float posX) &&
                        float.TryParse(parts[3], out float posY) &&
                        float.TryParse(parts[4], out float posZ))
                    {
                        vertices.Add(new Vector3(posX, posZ, posY)); // Swap Y and Z for OBJ
                        heights.Add(posZ); // Use Z (height) for heightmap
                    }
                    else
                    {
                        Debug.LogWarning($"Skipped malformed line: {line}");
                    }
                }
            }
        }
        
        float min = Mathf.Infinity;
        foreach (var vertex in vertices)
        {
            if (vertex.y < min)
            {
                min = vertex.y;
            }
        }
        
        return min;
    }
    
    private static float[,] GetHeightMap(List<float> heights, int rows, int cols)
    {
        float[,] heightMap = new float[cols, rows];
        for (int y = 0; y < rows; y++)
        {
            for (int x = 0; x < cols; x++)
            {
                int index = y * cols + x;
                heightMap[x, y] = heights[index];
            }
        }
        return heightMap;
    }

    private static void SaveHeightmap(List<float> heights, int rows, int cols, string path)
    {
        // Normalize heights
        float minHeight = Mathf.Min(heights.ToArray());
        float maxHeight = Mathf.Max(heights.ToArray());
    
        // Create texture
        Texture2D texture = new Texture2D(cols, rows);
        for (int y = 0; y < rows; y++)
        {
            for (int x = 0; x < cols; x++)
            {
                int index = y * cols + x;
                float normalizedHeight = Mathf.InverseLerp(minHeight, maxHeight, heights[index]);
                // normalizedHeight = heights[index];
                texture.SetPixel(x, y, new Color(normalizedHeight, normalizedHeight, normalizedHeight));
            }
        }
        texture.Apply();
    
        SaveAsPNG(texture, path);
        AssetDatabase.Refresh();
    }

    public static void SaveAsPNG(Texture2D texture, string path, bool normalize = true)
    {
        try
        {
            // Get pixel data from the texture
            float[] pixelData = new float[texture.width * texture.height];
            texture.GetPixelData<float>(0).CopyTo(pixelData);

            float minVal = Mathf.Infinity;
            float maxVal = Mathf.NegativeInfinity;

            if (normalize)
            {
                // Calculate min and max for normalization
                foreach (float value in pixelData)
                {
                    if (value < minVal) minVal = value;
                    if (value > maxVal) maxVal = value;
                }
            }

            // Create a new texture for saving
            Texture2D pngTexture = new Texture2D(texture.width, texture.height, TextureFormat.RGB24, false);

            // Set pixel colors
            for (int y = 0; y < texture.height; y++)
            {
                for (int x = 0; x < texture.width; x++)
                {
                    int index = y * texture.width + x;
                    float value = pixelData[index];

                    if (normalize)
                    {
                        value = Mathf.InverseLerp(minVal, maxVal, value); // Normalize the value
                    }

                    pngTexture.SetPixel(x, y, new Color(value, value, value)); // Grayscale
                }
            }

            pngTexture.Apply();

            // Save the texture as a PNG
            File.WriteAllBytes(path, pngTexture.EncodeToPNG());
            Debug.Log($"PNG saved at: {path}");
        }
        catch (Exception ex)
        {
            Debug.LogError($"Error saving PNG: {ex.Message}");
        }
    }

    public static float[,] ReadHeightmap(string filePath)
    {
        string[] lines = File.ReadAllLines(filePath);
        if (lines.Length < 2)
        {
            throw new ArgumentException("The file does not contain enough data.");
        }

        // Parse width and height from the first line
        string[] dimensions = lines[0].Split(',');
        if (dimensions.Length != 2)
        {
            throw new FormatException("The first line should specify the width and height in the format 'Width: value, Height: value'.");
        }

        int width = int.Parse(dimensions[0].Split(':')[1].Trim());
        int height = int.Parse(dimensions[1].Split(':')[1].Trim());

        // Initialize the heightmap array
        float[,] heightmap = new float[height, width];

        // Parse height values from subsequent lines
        int row = 0, col = 0;
        for (int i = 1; i < lines.Length; i++)
        {
            string[] values = lines[i].Split(',');
            foreach (string value in values)
            {
                if (!string.IsNullOrWhiteSpace(value))
                {
                    heightmap[row, col] = float.Parse(value, CultureInfo.InvariantCulture);
                    col++;

                    // Move to the next row if we've filled the current row
                    if (col >= width)
                    {
                        col = 0;
                        row++;
                        if (row >= height)
                        {
                            break; // Stop if we've filled the entire heightmap
                        }
                    }
                }
            }

            if (row >= height)
            {
                break; // Exit if all rows are filled
            }
        }

        // Ensure the heightmap was completely filled
        if (row < height || col != 0)
        {
            throw new FormatException("The file does not contain enough data to fill the specified heightmap dimensions.");
        }

        return heightmap;
    }
    
    public static float[,] ReadSplatmapLayer(string filePath)
    {
        string[] lines = File.ReadAllLines(filePath);
        if (lines.Length < 2)
        {
            throw new ArgumentException("The file does not contain enough data.");
        }

        // Parse width and height from the first line
        string[] dimensions = lines[0].Split(',');
        if (dimensions.Length != 2)
        {
            throw new FormatException("The first line should specify the width and height in the format 'Width: value, Height: value'.");
        }

        int width = int.Parse(dimensions[0].Split(':')[1].Trim());
        int height = int.Parse(dimensions[1].Split(':')[1].Trim());

        // Initialize the heightmap array
        float[,] heightmap = new float[height, width];

        // Fill the heightmap with the grid data
        for (int row = 0; row < height; row++)
        {
            if (row + 1 >= lines.Length)
            {
                throw new FormatException($"Missing data for row {row + 1}. Expected {height} rows of data.");
            }

            string[] cells = lines[row + 1].Split(',');
            if (cells.Length != width)
            {
                throw new FormatException($"Row {row + 1} does not match the expected width of {width}.");
            }

            for (int col = 0; col < width; col++)
            {
                if (!float.TryParse(cells[col].Trim(), out float value))
                {
                    throw new FormatException($"Invalid float value at row {row + 1}, column {col + 1}: {cells[col]}");
                }
                heightmap[row, col] = value;
            }
        }

        return heightmap;
    }

    public static bool VerifySplatmap(float[,] heightmap)
    {
        for (int i = 0; i < heightmap.GetLength(0); i++)
        {
            for (int j = 0; j < heightmap.GetLength(1); j++)
            {
                if (heightmap[i, j] != 0 && heightmap[i, j] != 1)
                {
                    return true;
                }
            }
        }
        
        return false;
    }
    public static void WriteSplatmapLayer(float[,] heightmap, string filePath)
    {
        if (heightmap == null)
        {
            throw new ArgumentNullException(nameof(heightmap), "Heightmap cannot be null.");
        }

        int height = heightmap.GetLength(0); // Number of rows
        int width = heightmap.GetLength(1);  // Number of columns

        // Use a StringBuilder for efficient string concatenation
        StringBuilder fileContent = new StringBuilder();

        // Write the dimensions on the first line
        fileContent.AppendLine($"Width: {width}, Height: {height}");

        // Write each row of the heightmap
        for (int row = 0; row < height; row++)
        {
            string[] rowValues = new string[width];
            for (int col = 0; col < width; col++)
            {
                rowValues[col] = heightmap[row, col].ToString("F4"); // Format to 4 decimal places
            }

            // Join the row values with commas and append to the StringBuilder
            fileContent.AppendLine(string.Join(",", rowValues));
        }

        // Write the content to the file
        File.WriteAllText(filePath, fileContent.ToString());
    }

}
