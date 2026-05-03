using System;
using System.Collections.Generic;
using System.IO;
using System.Globalization;
using System.Text;
using UnityEngine;

/// <summary>
/// PPM file format helper with consistent conventions:
/// - All Color[,] arrays use [row, col] indexing (row-major, same as PPM file order)
/// - Row 0 is the TOP of the image (PPM standard)
/// - Unity textures have row 0 at BOTTOM, so FlipVertical is needed on import/export
/// </summary>
public static class PpmHelper
{
    #region Read Operations

    /// <summary>
    /// Reads a PPM file. Returns Color[rows, cols] with row 0 = top of image.
    /// Data is in sRGB color space (as stored in file).
    /// </summary>
    public static Color[,] ReadPpm(string filePath)
    {
        using (var reader = new BinaryReader(File.Open(filePath, FileMode.Open)))
        {
            string magicNumber = ReadLine(reader);
            if (magicNumber != "P6" && magicNumber != "P3")
                throw new FormatException($"Unsupported PPM format: {magicNumber}");

            // Skip comments, read dimensions
            string line;
            do { line = ReadLine(reader); } while (line.StartsWith("#"));

            var dimensions = line.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
            int width = int.Parse(dimensions[0]);   // columns
            int height = int.Parse(dimensions[1]);  // rows
            int maxVal = int.Parse(ReadLine(reader));

            // Array is [rows, cols] = [height, width]
            Color[,] pixels = new Color[height, width];

            if (magicNumber == "P3")
                ReadPlainPpm(reader, pixels, width, height, maxVal);
            else
                ReadRawPpm(reader, pixels, width, height, maxVal);

            return pixels;
        }
    }

    private static void ReadRawPpm(BinaryReader reader, Color[,] pixels, int width, int height, int maxVal)
    {
        bool twoBytes = maxVal >= 256;
        float scale = 1f / maxVal;

        for (int row = 0; row < height; row++)
        {
            for (int col = 0; col < width; col++)
            {
                float r, g, b;
                if (twoBytes)
                {
                    r = ((reader.ReadByte() << 8) | reader.ReadByte()) * scale;
                    g = ((reader.ReadByte() << 8) | reader.ReadByte()) * scale;
                    b = ((reader.ReadByte() << 8) | reader.ReadByte()) * scale;
                }
                else
                {
                    r = reader.ReadByte() * scale;
                    g = reader.ReadByte() * scale;
                    b = reader.ReadByte() * scale;
                }
                pixels[row, col] = new Color(r, g, b);
            }
        }
    }

    private static void ReadPlainPpm(BinaryReader reader, Color[,] pixels, int width, int height, int maxVal)
    {
        // Read remaining content as text
        var remaining = new List<byte>();
        try { while (true) remaining.Add(reader.ReadByte()); }
        catch (EndOfStreamException) { }

        string content = Encoding.ASCII.GetString(remaining.ToArray());
        string[] tokens = content.Split(new[] { ' ', '\t', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);

        float scale = 1f / maxVal;
        int tokenIndex = 0;

        for (int row = 0; row < height; row++)
        {
            for (int col = 0; col < width; col++)
            {
                float r = int.Parse(tokens[tokenIndex++]) * scale;
                float g = int.Parse(tokens[tokenIndex++]) * scale;
                float b = int.Parse(tokens[tokenIndex++]) * scale;
                pixels[row, col] = new Color(r, g, b);
            }
        }
    }

    private static string ReadLine(BinaryReader reader)
    {
        var sb = new StringBuilder();
        char c;
        while ((c = reader.ReadChar()) != '\n')
        {
            if (c != '\r') sb.Append(c);
        }
        return sb.ToString().Trim();
    }

    #endregion

    #region Write Operations

    /// <summary>
    /// Writes Color[rows, cols] to PPM P6 (binary) format.
    /// Row 0 of array = top of image (PPM standard).
    /// </summary>
    public static void WritePpm(Color[,] data, string filepath)
    {
        int height = data.GetLength(0);  // rows
        int width = data.GetLength(1);   // cols

        using (var fs = new FileStream(filepath, FileMode.Create))
        {
            byte[] header = Encoding.ASCII.GetBytes($"P6\n{width} {height}\n255\n");
            fs.Write(header, 0, header.Length);

            for (int row = 0; row < height; row++)
            {
                for (int col = 0; col < width; col++)
                {
                    Color c = data[row, col];
                    fs.WriteByte((byte)(Mathf.Clamp01(c.r) * 255));
                    fs.WriteByte((byte)(Mathf.Clamp01(c.g) * 255));
                    fs.WriteByte((byte)(Mathf.Clamp01(c.b) * 255));
                }
            }
        }
    }

    /// <summary>
    /// Writes Color[rows, cols] to PPM P3 (plain text) format.
    /// </summary>
    public static void WritePpmPlain(Color[,] data, string filepath)
    {
        int height = data.GetLength(0);
        int width = data.GetLength(1);

        using (var sw = new StreamWriter(filepath))
        {
            sw.WriteLine("P3");
            sw.WriteLine($"{width} {height}");
            sw.WriteLine("255");

            for (int row = 0; row < height; row++)
            {
                for (int col = 0; col < width; col++)
                {
                    Color c = data[row, col];
                    int r = (int)(Mathf.Clamp01(c.r) * 255);
                    int g = (int)(Mathf.Clamp01(c.g) * 255);
                    int b = (int)(Mathf.Clamp01(c.b) * 255);
                    sw.Write($"{r} {g} {b} ");
                }
                sw.WriteLine();
            }
        }
    }

    #endregion

    #region Transform Operations

    /// <summary>Flip vertically (mirror along horizontal axis). Row 0 becomes last row.</summary>
    public static Color[,] FlipVertical(Color[,] data)
    {
        int height = data.GetLength(0);
        int width = data.GetLength(1);
        var result = new Color[height, width];

        for (int row = 0; row < height; row++)
        {
            int srcRow = height - 1 - row;
            for (int col = 0; col < width; col++)
            {
                result[row, col] = data[srcRow, col];
            }
        }
        return result;
    }

    /// <summary>Flip horizontally (mirror along vertical axis). Col 0 becomes last col.</summary>
    public static Color[,] FlipHorizontal(Color[,] data)
    {
        int height = data.GetLength(0);
        int width = data.GetLength(1);
        var result = new Color[height, width];

        for (int row = 0; row < height; row++)
        {
            for (int col = 0; col < width; col++)
            {
                result[row, col] = data[row, width - 1 - col];
            }
        }
        return result;
    }

    /// <summary>Transpose: swap rows and columns. [h,w] becomes [w,h].</summary>
    public static Color[,] Transpose(Color[,] data)
    {
        int height = data.GetLength(0);
        int width = data.GetLength(1);
        var result = new Color[width, height];  // Note: dimensions swapped

        for (int row = 0; row < height; row++)
        {
            for (int col = 0; col < width; col++)
            {
                result[col, row] = data[row, col];
            }
        }
        return result;
    }

    /// <summary>Rotate 90° clockwise.</summary>
    public static Color[,] Rotate90CW(Color[,] data)
    {
        return FlipHorizontal(Transpose(data));
    }

    /// <summary>Rotate 90° counter-clockwise.</summary>
    public static Color[,] Rotate90CCW(Color[,] data)
    {
        return FlipVertical(Transpose(data));
    }

    #endregion

    #region Color Space Conversion

    /// <summary>Convert linear color space to sRGB gamma-encoded (for export to file).</summary>
    public static Color[,] LinearToSRGB(Color[,] data)
    {
        int height = data.GetLength(0);
        int width = data.GetLength(1);
        var result = new Color[height, width];

        for (int row = 0; row < height; row++)
        {
            for (int col = 0; col < width; col++)
            {
                Color c = data[row, col];
                result[row, col] = new Color(
                    LinearToSRGBChannel(c.r),
                    LinearToSRGBChannel(c.g),
                    LinearToSRGBChannel(c.b),
                    c.a
                );
            }
        }
        return result;
    }

    /// <summary>Convert sRGB gamma-encoded to linear color space (after import from file).</summary>
    public static Color[,] SRGBToLinear(Color[,] data)
    {
        int height = data.GetLength(0);
        int width = data.GetLength(1);
        var result = new Color[height, width];

        for (int row = 0; row < height; row++)
        {
            for (int col = 0; col < width; col++)
            {
                Color c = data[row, col];
                result[row, col] = new Color(
                    SRGBToLinearChannel(c.r),
                    SRGBToLinearChannel(c.g),
                    SRGBToLinearChannel(c.b),
                    c.a
                );
            }
        }
        return result;
    }

    private static float LinearToSRGBChannel(float linear)
    {
        if (linear <= 0.0031308f)
            return linear * 12.92f;
        return 1.055f * Mathf.Pow(linear, 1f / 2.4f) - 0.055f;
    }

    private static float SRGBToLinearChannel(float srgb)
    {
        if (srgb <= 0.04045f)
            return srgb / 12.92f;
        return Mathf.Pow((srgb + 0.055f) / 1.055f, 2.4f);
    }

    #endregion

    #region Alpha Compositing

    /// <summary>Composite over white background (removes transparency).</summary>
    public static Color[,] CompositeOverWhite(Color[,] data)
    {
        return CompositeOverColor(data, Color.white);
    }

    /// <summary>Composite over specified background color.</summary>
    public static Color[,] CompositeOverColor(Color[,] data, Color background)
    {
        int height = data.GetLength(0);
        int width = data.GetLength(1);
        var result = new Color[height, width];

        for (int row = 0; row < height; row++)
        {
            for (int col = 0; col < width; col++)
            {
                Color c = data[row, col];
                float a = c.a;
                result[row, col] = new Color(
                    c.r * a + background.r * (1f - a),
                    c.g * a + background.g * (1f - a),
                    c.b * a + background.b * (1f - a),
                    1f
                );
            }
        }
        return result;
    }

    #endregion

   #region M&B Terrain Specific

/// <summary>
/// M&B terrain PPM to Unity texture.
/// M&B uses a different coordinate system that requires 180° rotation.
/// </summary>
public static Texture2D MBTerrainPpmToTexture(Color[,] ppmData, bool convertToLinear = false)
{
    // M&B terrain coordinates need 180° rotation (flip both axes)
    // ToTexture2D already handles Unity's vertical flip internally,
    // so we apply horizontal flip here + let ToTexture2D handle vertical
    var rotated = FlipHorizontal(ppmData);
    // Skip the extra vertical flip since ToTexture2D does one
    return ToTexture2DRaw(rotated, convertToLinear);  // New method without internal flip
}

/// <summary>
/// Unity texture to M&B terrain PPM.
/// Applies inverse 180° rotation for M&B compatibility.
/// </summary>
public static Color[,] TextureToMBTerrainPpm(Texture2D tex, bool convertToSRGB = false)
{
    var ppmData = FromTexture2DRaw(tex, convertToSRGB);  // New method without internal flip
    // Apply 180° rotation for M&B
    return FlipHorizontal(ppmData);
}

/// <summary>
/// Convert PPM data to Texture2D WITHOUT automatic vertical flip.
/// Use when you need manual control over orientation.
/// </summary>
public static Texture2D ToTexture2DRaw(Color[,] ppmData, bool convertToLinear = false)
{
    int height = ppmData.GetLength(0);
    int width = ppmData.GetLength(1);

    var tex = new Texture2D(width, height, TextureFormat.RGB24, false);
    tex.wrapMode = TextureWrapMode.Clamp;
    tex.filterMode = FilterMode.Bilinear;

    var pixels = new Color[width * height];

    for (int row = 0; row < height; row++)
    {
        for (int col = 0; col < width; col++)
        {
            Color c = ppmData[row, col];
            if (convertToLinear)
            {
                c = new Color(
                    SRGBToLinearChannel(c.r),
                    SRGBToLinearChannel(c.g),
                    SRGBToLinearChannel(c.b)
                );
            }
            // Direct mapping, no flip
            pixels[row * width + col] = c;
        }
    }

    tex.SetPixels(pixels);
    tex.Apply();
    return tex;
}

/// <summary>
/// Convert Texture2D to PPM data WITHOUT automatic vertical flip.
/// </summary>
public static Color[,] FromTexture2DRaw(Texture2D tex, bool convertToSRGB = false)
{
    int width = tex.width;
    int height = tex.height;

    var ppmData = new Color[height, width];
    var pixels = tex.GetPixels();

    for (int row = 0; row < height; row++)
    {
        for (int col = 0; col < width; col++)
        {
            Color c = pixels[row * width + col];
            if (convertToSRGB)
            {
                c = new Color(
                    LinearToSRGBChannel(c.r),
                    LinearToSRGBChannel(c.g),
                    LinearToSRGBChannel(c.b)
                );
            }
            ppmData[row, col] = c;
        }
    }

    return ppmData;
}

#endregion

    #region Resize

    /// <summary>Bilinear resize of PPM data.</summary>
    public static Color[,] Resize(Color[,] data, int newWidth, int newHeight)
    {
        int srcHeight = data.GetLength(0);
        int srcWidth = data.GetLength(1);

        var result = new Color[newHeight, newWidth];

        for (int row = 0; row < newHeight; row++)
        {
            float srcRow = row * (srcHeight - 1f) / (newHeight - 1f);
            int row0 = Mathf.FloorToInt(srcRow);
            int row1 = Mathf.Min(row0 + 1, srcHeight - 1);
            float rowFrac = srcRow - row0;

            for (int col = 0; col < newWidth; col++)
            {
                float srcCol = col * (srcWidth - 1f) / (newWidth - 1f);
                int col0 = Mathf.FloorToInt(srcCol);
                int col1 = Mathf.Min(col0 + 1, srcWidth - 1);
                float colFrac = srcCol - col0;

                Color c00 = data[row0, col0];
                Color c10 = data[row0, col1];
                Color c01 = data[row1, col0];
                Color c11 = data[row1, col1];

                Color top = Color.Lerp(c00, c10, colFrac);
                Color bottom = Color.Lerp(c01, c11, colFrac);
                result[row, col] = Color.Lerp(top, bottom, rowFrac);
            }
        }

        return result;
    }

    #endregion
}