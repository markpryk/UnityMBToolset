using System;
using System.IO;
using System.Globalization;
using System.Text;
using UnityEngine;

public static class PgmHelper
{
    /// <summary>
    /// Reads a PGM file and returns its pixel data as a float array.
    /// </summary>
    /// <param name="filePath">Path to the PGM file.</param>
    /// <returns>2D float array containing normalized pixel values (0 to 1).</returns>
    public static float[,] ReadPgm(string filePath)
    {
        using (var reader = new BinaryReader(File.Open(filePath, FileMode.Open)))
        {
            // Read the magic number
            string magicNumber = ReadLine(reader);
            if (magicNumber != "P5" && magicNumber != "P2")
            {
                throw new FormatException($"Unsupported PGM format: {magicNumber}");
            }

            // Skip comments
            string line;
            do
            {
                line = ReadLine(reader);
            } while (line.StartsWith("#"));

            // Read image dimensions
            var dimensions = line.Split(' ');
            int width = int.Parse(dimensions[0]);
            int height = int.Parse(dimensions[1]);

            // Read maximum gray value
            int maxVal = int.Parse(ReadLine(reader));
            bool isPlainText = magicNumber == "P2";

            // Read pixel data
            float[,] pixels = new float[height, width];
            if (isPlainText)
            {
                // Plain format (P2) - ASCII text
                using (var sr = new StreamReader(filePath))
                {
                    // Skip header
                    for (int i = 0; i < 4 || sr.Peek() == '#'; i++)
                    {
                        ReadLine(sr);
                    }

                    for (int y = 0; y < height; y++)
                    {
                        for (int x = 0; x < width; x++)
                        {
                            int pixelValue = int.Parse(sr.ReadLine(), CultureInfo.InvariantCulture);
                            pixels[y, x] = pixelValue / (float)maxVal;
                        }
                    }
                }
            }
            else
            {
                // Raw format (P5) - Binary data
                for (int y = 0; y < height; y++)
                {
                    for (int x = 0; x < width; x++)
                    {
                        int pixelValue = maxVal < 256
                            ? reader.ReadByte()
                            : (reader.ReadByte() << 8) | reader.ReadByte();
                        pixels[y, x] = pixelValue / (float)maxVal;
                    }
                }
            }

            return pixels;
        }
    }

    /// <summary>
    /// Reads a single line of text from the binary reader.
    /// </summary>
    private static string ReadLine(BinaryReader reader)
    {
        string result = "";
        char c;
        while ((c = reader.ReadChar()) != '\n')
        {
            if (c != '\r')
                result += c;
        }
        return result.Trim();
    }

    /// <summary>
    /// Reads a single line from a stream reader.
    /// </summary>
    private static string ReadLine(StreamReader reader)
    {
        return reader.ReadLine()?.Trim() ?? string.Empty;
    }
    
    private static float[,] ResizePgmData(float[,] pgm, int targetWidth, int targetHeight)
    {
        int sourceWidth = pgm.GetLength(0);
        int sourceHeight = pgm.GetLength(1);

        float[,] resized = new float[targetWidth, targetHeight];

        for (int x = 0; x < targetWidth; x++)
        {
            for (int y = 0; y < targetHeight; y++)
            {
                // Map target coordinates to source coordinates
                float gx = x / (float)(targetWidth - 1) * (sourceWidth - 1);
                float gy = y / (float)(targetHeight - 1) * (sourceHeight - 1);

                int x0 = Mathf.FloorToInt(gx);
                int x1 = Mathf.Min(x0 + 1, sourceWidth - 1);
                int y0 = Mathf.FloorToInt(gy);
                int y1 = Mathf.Min(y0 + 1, sourceHeight - 1);

                // Bilinear interpolation
                float dx = gx - x0;
                float dy = gy - y0;

                float top = Mathf.Lerp(pgm[x0, y0], pgm[x1, y0], dx);
                float bottom = Mathf.Lerp(pgm[x0, y1], pgm[x1, y1], dx);

                resized[x, y] = Mathf.Lerp(top, bottom, dy);
            }
        }

        return resized;
    }
    
    /// <summary>Write float array to PGM file</summary>
    public static void WritePgm(float[,] data, string filepath, bool useColumnMajor = true)
    {
        int width = data.GetLength(0);
        int height = data.GetLength(1);
        
        using (FileStream fs = new FileStream(filepath, FileMode.Create))
        {
            byte[] header = Encoding.ASCII.GetBytes($"P5\n{width} {height}\n255\n");
            fs.Write(header, 0, header.Length);
            
            if (useColumnMajor)
            {
                for (int x = 0; x < width; x++)
                {
                    for (int y = 0; y < height; y++)
                    {
                        byte pixel = (byte)(Mathf.Clamp01(data[x, y]) * 255);
                        fs.WriteByte(pixel);
                    }
                }
            }
            else
            {
                for (int y = 0; y < height; y++)
                {
                    for (int x = 0; x < width; x++)
                    {
                        byte pixel = (byte)(Mathf.Clamp01(data[x, y]) * 255);
                        fs.WriteByte(pixel);
                    }
                }
            }
        }
    }

}
