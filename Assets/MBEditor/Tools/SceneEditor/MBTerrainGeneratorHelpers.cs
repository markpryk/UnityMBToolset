using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using UnityEngine;
using Debug = UnityEngine.Debug;

public static class MBTerrainGeneratorHelpers
{
    // Constants for terrain parsing
    private const int TerrainCodeLength = 48;



    /// <summary>
    /// Executes the TerrainGenerator tool with the provided hash and output directory.
    /// Output files generated:
    ///   - heightmap.txt
    ///   - mesh.txt
    ///   - normals.txt
    ///   - layers_info.txt
    ///   - [ground_type].txt (e.g., gray_stone.txt, turf.txt, earth.txt, etc.)
    /// </summary>
    public static bool ExtractHashData(string hash, string outputDirectory,string floraDbPath)
    {
        if (string.IsNullOrEmpty(hash))
        {
            Debug.LogError("Hash cannot be null or empty.");
            return false;
        }

        if (string.IsNullOrEmpty(outputDirectory))
        {
            Debug.LogError("Output directory cannot be null or empty.");
            return false;
        }

        if (!File.Exists(MBPathHelpers.MBTerrainGeneratorToolPath()))
        {
            Debug.LogError($"TerrainGenerator tool not found at path: {MBPathHelpers.MBTerrainGeneratorToolPath()}");
            return false;
        }

        // Arguments: "<flora_db_path>" "<hash>" --flora --flora-json --flora-db "<output_directory>"
        string arguments = $"\"{outputDirectory}\" \"{hash}\" --flora --flora-json --flora-db \"{floraDbPath}\"";
        
        Debug.Log(arguments);
        return RunProcess(MBPathHelpers.MBTerrainGeneratorToolPath(), arguments);
    }

    /// <summary>
    /// Runs an external process with the specified tool path and arguments.
    /// </summary>
    private static bool RunProcess(string toolPath, string arguments)
    {
        try
        {
            Process process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = toolPath,
                    Arguments = arguments,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                }
            };

            StringBuilder outputBuilder = new StringBuilder();
            StringBuilder errorBuilder = new StringBuilder();

            process.OutputDataReceived += (sender, args) =>
            {
                if (args.Data != null) outputBuilder.AppendLine(args.Data);
            };

            process.ErrorDataReceived += (sender, args) =>
            {
                if (args.Data != null) errorBuilder.AppendLine(args.Data);
            };

            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();
            process.WaitForExit();

            if (process.ExitCode == 0)
            {
                Debug.Log($"TerrainGenerator completed successfully.\nOutput:\n{outputBuilder}");
                return true;
            }
            else
            {
                Debug.LogError($"TerrainGenerator failed with exit code {process.ExitCode}");
                if (errorBuilder.Length > 0)
                {
                    Debug.LogError($"Errors:\n{errorBuilder}");
                }
                return false;
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"Exception while running TerrainGenerator: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Reads a layer intensity file (e.g., turf.txt) and returns a 2D float array.
    /// </summary>
    public static float[,] ReadLayerFile(string filePath)
    {
        if (!File.Exists(filePath))
        {
            Debug.LogError($"Layer file not found: {filePath}");
            return null;
        }

        try
        {
            string[] lines = File.ReadAllLines(filePath);
            if (lines.Length < 2)
            {
                Debug.LogError("Invalid layer file format");
                return null;
            }

            // Parse header: "Width: X, Height: Y"
            string header = lines[0];
            string[] headerParts = header.Split(new[] { ':', ',' }, StringSplitOptions.RemoveEmptyEntries);
            int width = int.Parse(headerParts[1].Trim());
            int height = int.Parse(headerParts[3].Trim());

            float[,] data = new float[width, height];

            for (int y = 0; y < height && y + 1 < lines.Length; y++)
            {
                string[] values = lines[y + 1].Split(',');
                for (int x = 0; x < width && x < values.Length; x++)
                {
                    if (float.TryParse(values[x], out float val))
                    {
                        data[x, y] = val;
                    }
                }
            }

            return data;
        }
        catch (Exception ex)
        {
            Debug.LogError($"Error reading layer file: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Reads the heightmap file and returns a 2D float array.
    /// </summary>
    public static float[,] ReadHeightmap(string outputDirectory)
    {
        return ReadLayerFile(Path.Combine(outputDirectory, "heightmap.txt"));
    }

    /// <summary>
    /// Gets a list of all generated layer files in the output directory.
    /// </summary>
    public static string[] GetGeneratedLayerFiles(string outputDirectory)
    {
        if (!Directory.Exists(outputDirectory))
            return new string[0];

        List<string> layerFiles = new List<string>();
        string[] groundTypes = { "gray_stone", "brown_stone", "turf", "steppe", "snow", 
                                  "earth", "desert", "forest", "pebbles", "village", "path" };

        foreach (string groundType in groundTypes)
        {
            string filePath = Path.Combine(outputDirectory, $"{groundType}.txt");
            if (File.Exists(filePath))
            {
                layerFiles.Add(filePath);
            }
        }

        return layerFiles.ToArray();
    }

    /// <summary>
    /// Generates a hash based on terrain data.
    /// </summary>
    public static string GenerateHash(MBTerrainGeneratorData data)
    {
        if (data == null)
        {
            Debug.LogError("Terrain data cannot be null.");
            return null;
        }

        uint[] hashParts = new uint[6];

        hashParts[0] = data.TerrainSeed & 0x7FFFFFFF;
        hashParts[1] = (data.RiverSeed & 0x7FFFFFFF) | (data.DeepWater ? 0x80000000u : 0u);
        hashParts[2] = data.FloraSeed & 0x7FFFFFFF;
        hashParts[3] = (uint)((data.SizeX & 0x3FF) | ((data.SizeY & 0x3FF) << 10) |
                              (data.ShadeOcclude ? 1 << 30 : 0) | (data.PlaceRiver ? 1 << 31 : 0));
        hashParts[4] = (uint)((data.Valley & 0x7F) | ((data.HillHeight & 0x7F) << 7) |
                              ((data.Ruggedness & 0x7F) << 14) | ((data.Vegetation & 0x7F) << 21) |
                              ((data.TerrainType & 0xF) << 28));
        hashParts[5] = (uint)((data.PolygonSize - 2) | (data.DisableGrass ? 1 << 2 : 0));

        StringBuilder hashBuilder = new StringBuilder("0x");
        for (int i = hashParts.Length - 1; i >= 0; i--)
        {
            hashBuilder.Append(hashParts[i].ToString("x8"));
        }

        return hashBuilder.ToString();
    }

    /// <summary>
    /// Parses a hash into terrain data.
    /// </summary>
    public static MBTerrainGeneratorData ParseTerrainCode(string code)
    {
        if (string.IsNullOrWhiteSpace(code) || !code.StartsWith("0x"))
        {
            return null;
        }

        code = code.Substring(2).PadLeft(TerrainCodeLength, '0');

        if (code.Length != TerrainCodeLength || !System.Text.RegularExpressions.Regex.IsMatch(code, @"^[0-9a-fA-F]+$"))
        {
            return null;
        }

        string parsedHex = ParseHex(code);
        string[] parts = SplitHexToParts(parsedHex);

        var data = new MBTerrainGeneratorData
        {
            TerrainSeed = (uint)ExtractBits(parts[0], 0, 31),
            RiverSeed = (uint)ExtractBits(parts[1], 0, 31),
            DeepWater = ExtractBits(parts[1], 31, 1) == 1,
            FloraSeed = (uint)ExtractBits(parts[2], 0, 31),
            SizeX = ExtractBits(parts[3], 0, 10),
            SizeY = ExtractBits(parts[3], 10, 10),
            ShadeOcclude = ExtractBits(parts[3], 30, 1) == 1,
            PlaceRiver = ExtractBits(parts[3], 31, 1) == 1,
            Valley = ExtractBits(parts[4], 0, 7),
            HillHeight = ExtractBits(parts[4], 7, 7),
            Ruggedness = ExtractBits(parts[4], 14, 7),
            Vegetation = ExtractBits(parts[4], 21, 7),
            TerrainType = ExtractBits(parts[4], 28, 4),
            PolygonSize = ExtractBits(parts[5], 0, 2) + 2,
            DisableGrass = ExtractBits(parts[5], 2, 1) == 1,
        };

        return data;
    }

    public static bool ValidateHashCode(string code)
    {
        if (string.IsNullOrWhiteSpace(code) || code == "0")
            return false;

        code = code.Trim();

        if (!code.StartsWith("0x"))
            return false;

        code = code.Substring(2).PadLeft(TerrainCodeLength, '0');

        if (code.Length != TerrainCodeLength || !System.Text.RegularExpressions.Regex.IsMatch(code, @"^[0-9a-fA-F]+$"))
        {
            Debug.LogError("Invalid terrain code. Ensure it is 48 hexadecimal characters long after padding.");
            return false;
        }

        return true;
    }

    private static string ParseHex(string input)
    {
        if (input.StartsWith("0x"))
            input = input.Substring(2);

        input = input.PadLeft(48, '0');

        if (input.Length != 48)
        {
            throw new ArgumentException("Invalid hash format");
        }

        return input;
    }

    static string[] SplitHexToParts(string hex)
    {
        var parts = new string[6];
        for (int i = 0; i < 6; i++)
        {
            parts[i] = hex.Substring(hex.Length - 8 * (i + 1), 8);
        }

        return parts;
    }

    static int ExtractBits(string hexBlock, int shift, int numBits)
    {
        int blockValue = Convert.ToInt32(hexBlock, 16);
        int mask = (1 << numBits) - 1;
        return (blockValue >> shift) & mask;
    }

    public struct TerrainGeometryData
    {
        public int TerrainSizeX;
        public int TerrainSizeY;
        public int NumFacesX;
        public int NumFacesY;
        public int NumVerticesX;
        public int NumVerticesY;
        public int PolygonSize;
    }

    public static TerrainGeometryData CalculateTerrainGeometryData(int scoSizeX, int scoSizeY, int scoPolygonSize)
    {
        const int MIN_NUM_TERRAIN_FACES_PER_AXIS = 40;
        const int MAX_NUM_TERRAIN_FACES_PER_AXIS = 250;

        int numFacesX = Mathf.Clamp(scoSizeX / scoPolygonSize, MIN_NUM_TERRAIN_FACES_PER_AXIS,
            MAX_NUM_TERRAIN_FACES_PER_AXIS);
        int numFacesY = Mathf.Clamp(scoSizeY / scoPolygonSize, MIN_NUM_TERRAIN_FACES_PER_AXIS,
            MAX_NUM_TERRAIN_FACES_PER_AXIS);

        int numVerticesX = numFacesX + 1;
        int numVerticesY = numFacesY + 1;

        int terrainSizeX = numFacesX * scoPolygonSize;
        int terrainSizeY = numFacesY * scoPolygonSize;

        var terrainData = new TerrainGeometryData
        {
            TerrainSizeX = terrainSizeX,
            TerrainSizeY = terrainSizeY,
            NumFacesX = numFacesX,
            NumFacesY = numFacesY,
            NumVerticesX = numVerticesX,
            NumVerticesY = numVerticesY,
            PolygonSize = scoPolygonSize
        };

        return terrainData;
    }
}