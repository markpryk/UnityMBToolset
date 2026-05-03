using System;
using System.IO;
using UnityEngine;

public class PfmHelper
{
    /// <summary>
    /// Reads a PFM file and loads it into a Texture2D object.
    /// </summary>
    public static Texture2D ReadPFM(string path)
    {
        if (!File.Exists(path))
        {
            Debug.LogError($"File not found: {path}");
            return null;
        }

        try
        {
            using (FileStream fs = new FileStream(path, FileMode.Open, FileAccess.Read))
            using (BinaryReader reader = new BinaryReader(fs))
            {
                // Read header
                string format = ReadHeaderLine(reader);
                bool isColor = format == "PF";
                if (!isColor && format != "Pf")
                {
                    Debug.LogError("Unsupported PFM format.");
                    return null;
                }

                // Read width, height, and scale factor
                string[] dimensions = ReadHeaderLine(reader).Split(' ');
                int width = int.Parse(dimensions[0]);
                int height = int.Parse(dimensions[1]);

                float scale = float.Parse(ReadHeaderLine(reader));
                bool isLittleEndian = scale < 0;
                scale = Math.Abs(scale);

                Debug.Log($"Reading PFM: {path}\nWidth: {width}, Height: {height}, Scale: {scale}, Color: {isColor}");

                // Read image data
                Texture2D texture = new Texture2D(width, height,
                    isColor ? TextureFormat.RGBAFloat : TextureFormat.RFloat, false);
                float[] pixels = new float[width * height * (isColor ? 3 : 1)];

                for (int i = height - 1; i >= 0; i--) // Flip vertically
                {
                    for (int j = 0; j < width; j++)
                    {
                        if (isColor)
                        {
                            pixels[(i * width + j) * 3 + 0] = ReadFloat(reader, isLittleEndian);
                            pixels[(i * width + j) * 3 + 1] = ReadFloat(reader, isLittleEndian);
                            pixels[(i * width + j) * 3 + 2] = ReadFloat(reader, isLittleEndian);
                        }
                        else
                        {
                            pixels[i * width + j] = ReadFloat(reader, isLittleEndian);
                        }
                    }
                }

                // Assign pixel data to the texture
                texture.SetPixelData(pixels, 0);
                texture.Apply();

                return texture;
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"Error reading PFM file: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Reads a PFM file into a 2D float array.
    /// </summary>
    public static float[,] ReadFloats(string path)
    {
        if (!File.Exists(path))
        {
            return null;
        }

        try
        {
            using (FileStream fs = new FileStream(path, FileMode.Open, FileAccess.Read))
            using (BinaryReader reader = new BinaryReader(fs))
            {
                // Read header
                string format = ReadHeaderLine(reader);
                bool isColor = format == "PF";
                if (!isColor && format != "Pf")
                {
                    Debug.LogError("Unsupported PFM format.");
                    return null;
                }

                // Read width, height, and scale factor
                string[] dimensions = ReadHeaderLine(reader).Split(' ');
                int width = int.Parse(dimensions[0]);
                int height = int.Parse(dimensions[1]);

                float scale = float.Parse(ReadHeaderLine(reader));
                bool isLittleEndian = scale < 0;
                scale = Math.Abs(scale);

                // Debug.Log($"Reading PFM: {path}\nWidth: {width}, Height: {height}, Scale: {scale}, Color: {isColor}");

                // Read float data into a 2D array
                float[,] floatData = new float[height, width];

                for (int i = height - 1; i >= 0; i--) // Flip vertically
                {
                    for (int j = 0; j < width; j++)
                    {
                        floatData[i, j] = ReadFloat(reader, isLittleEndian);
                    }
                }

                return floatData;
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"Error reading PFM file: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Writes a Texture2D object to a PFM file.
    /// </summary>
    public static void WritePFM(Texture2D texture, string path, float scale = 1.0f)
    {
        try
        {
            using (FileStream fs = new FileStream(path, FileMode.Create, FileAccess.Write))
            using (BinaryWriter writer = new BinaryWriter(fs))
            {
                bool isColor = texture.format == TextureFormat.RGBAFloat;

                // Write header
                writer.Write((isColor ? "PF" : "Pf") + "\n");
                writer.Write($"{texture.width} {texture.height}\n");
                writer.Write($"{(BitConverter.IsLittleEndian ? -scale : scale)}\n");

                // Write pixel data
                float[] pixels = new float[texture.width * texture.height * (isColor ? 3 : 1)];
                texture.GetPixelData<float>(0).CopyTo(pixels);

                for (int i = texture.height - 1; i >= 0; i--) // Flip vertically
                {
                    for (int j = 0; j < texture.width; j++)
                    {
                        if (isColor)
                        {
                            writer.Write(BitConverter.GetBytes(pixels[(i * texture.width + j) * 3 + 0]));
                            writer.Write(BitConverter.GetBytes(pixels[(i * texture.width + j) * 3 + 1]));
                            writer.Write(BitConverter.GetBytes(pixels[(i * texture.width + j) * 3 + 2]));
                        }
                        else
                        {
                            writer.Write(BitConverter.GetBytes(pixels[i * texture.width + j]));
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"Error writing PFM file: {ex.Message}");
        }
    }

    /// <summary>
    /// Reads a single line from the binary stream.
    /// </summary>
    private static string ReadHeaderLine(BinaryReader reader)
    {
        string line = string.Empty;
        char c;
        while ((c = reader.ReadChar()) != '\n')
        {
            line += c;
        }

        return line.Trim();
    }

    /// <summary>
    /// Reads a float value from the binary stream with optional byte swapping.
    /// </summary>
    private static float ReadFloat(BinaryReader reader, bool isLittleEndian)
    {
        byte[] bytes = reader.ReadBytes(4);
        if (BitConverter.IsLittleEndian != isLittleEndian)
        {
            Array.Reverse(bytes);
        }

        return BitConverter.ToSingle(bytes, 0);
    }

    /// <summary>
    /// Writes a 2D float array to a PFM file.
    /// </summary>
    public static void WriteFloats(float[,] data, string path, float scale = 1.0f)
    {
        if (data == null)
        {
            Debug.LogError("Cannot write null data to PFM.");
            return;
        }

        try
        {
            int height = data.GetLength(0);
            int width = data.GetLength(1);

            using (FileStream fs = new FileStream(path, FileMode.Create, FileAccess.Write))
            using (BinaryWriter writer = new BinaryWriter(fs))
            {
                // Write header (grayscale format "Pf")
                WriteHeaderLine(writer, "Pf");
                WriteHeaderLine(writer, $"{width} {height}");
                WriteHeaderLine(writer, $"{(BitConverter.IsLittleEndian ? -scale : scale)}");

                // Write pixel data (bottom-to-top for PFM format)
                for (int i = height - 1; i >= 0; i--)
                {
                    for (int j = 0; j < width; j++)
                    {
                        WriteFloat(writer, data[i, j]);
                    }
                }
            }

            Debug.Log($"Successfully wrote PFM: {path} ({width}x{height})");
        }
        catch (Exception ex)
        {
            Debug.LogError($"Error writing PFM file: {ex.Message}");
        }
    }

    /// <summary>
    /// Gets dimensions of a PFM file without loading all data.
    /// </summary>
    public static (int width, int height) GetDimensions(string path)
    {
        if (!File.Exists(path))
        {
            Debug.LogError($"File not found: {path}");
            return (0, 0);
        }

        try
        {
            using (FileStream fs = new FileStream(path, FileMode.Open, FileAccess.Read))
            using (BinaryReader reader = new BinaryReader(fs))
            {
                ReadHeaderLine(reader); // Skip format line
                string[] dimensions = ReadHeaderLine(reader).Split(' ');
                return (int.Parse(dimensions[0]), int.Parse(dimensions[1]));
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"Error reading PFM dimensions: {ex.Message}");
            return (0, 0);
        }
    }

    private static void WriteHeaderLine(BinaryWriter writer, string line)
    {
        foreach (char c in line)
        {
            writer.Write((byte)c);
        }

        writer.Write((byte)'\n');
    }

    private static void WriteFloat(BinaryWriter writer, float value)
    {
        writer.Write(value); // BinaryWriter writes in system endianness
    }
}