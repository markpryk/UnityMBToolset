using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Cached module context to avoid repeated file system and asset database operations.
/// Create once per import session, dispose when done.
/// </summary>
public class MBModuleImportContext : IDisposable
{
    public string ModuleName { get; }
    public string ResourcePath { get; }
    public string MaterialsPath { get; }
    public string PrefabsPath { get; }
    public string TexturesPath { get; }

    // Extensions that Unity can import as Texture2D
    private static readonly string[] TextureSearchPatterns =
        { "*.png", "*.tga", "*.psd", "*.jpg", "*.jpeg", "*.bmp", "*.dds" };

    // Cached file listings
    private Dictionary<string, string> _objFiles;          // baseName -> path
    private Dictionary<string, string> _materialFiles;     // name -> path
    private Dictionary<string, string> _textureFiles;      // name -> path (all formats)
    private Dictionary<string, MBMeshData> _meshDataCache; // meshName -> data

    // Cached assets
    private Dictionary<string, Material> _loadedMaterials;
    private Dictionary<string, Texture2D> _loadedTextures;
    private Dictionary<string, Mesh> _loadedMeshes;

    public MBModuleImportContext(string moduleName)
    {
        ModuleName = moduleName;
        ResourcePath = MBPathHelpers.ModResourcePath(moduleName);
        MaterialsPath = MBPathHelpers.ModMaterialsPath(moduleName);
        PrefabsPath = MBPathHelpers.ModPrefabsPath(moduleName);
        TexturesPath = MBPathHelpers.ModTexturesPath(moduleName);

        _loadedMaterials = new Dictionary<string, Material>(StringComparer.OrdinalIgnoreCase);
        _loadedTextures = new Dictionary<string, Texture2D>(StringComparer.OrdinalIgnoreCase);
        _loadedMeshes = new Dictionary<string, Mesh>(StringComparer.OrdinalIgnoreCase);
    }

    public void BuildFileCache()
    {
        // Single pass for OBJ files
        _objFiles = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (Directory.Exists(ResourcePath))
        {
            foreach (var file in Directory.EnumerateFiles(ResourcePath, "*.obj", SearchOption.AllDirectories))
            {
                var name = Path.GetFileNameWithoutExtension(file);
                _objFiles.TryAdd(name, file);
            }
        }

        // Single pass for materials - scan per-BRF Materials/ folders
        _materialFiles = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (Directory.Exists(ResourcePath))
        {
            foreach (var file in Directory.EnumerateFiles(ResourcePath, "*.mat", SearchOption.AllDirectories))
            {
                var name = Path.GetFileNameWithoutExtension(file);
                _materialFiles.TryAdd(name, file);
            }
        }

        // Textures - scan per-BRF Textures/ subfolders for all image formats
        _textureFiles = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (Directory.Exists(ResourcePath))
        {
            // Scan all BRF/Textures/ subfolders under Resource/
            foreach (var brfDir in Directory.GetDirectories(ResourcePath))
            {
                string texDir = Path.Combine(brfDir, "Textures");
                if (Directory.Exists(texDir))
                {
                    ScanTextureFolder(texDir);
                }
            }
        }

        // Also check legacy global textures path if it exists
        if (Directory.Exists(TexturesPath))
        {
            ScanTextureFolder(TexturesPath);
        }
    }

    /// <summary>
    /// Scan a folder for texture files and add to the file cache.
    /// First-found wins - BRF-local textures are found first if scanning BRF folders in order.
    /// </summary>
    private void ScanTextureFolder(string folder)
    {
        foreach (var pattern in TextureSearchPatterns)
        {
            foreach (var file in Directory.EnumerateFiles(folder, pattern))
            {
                var name = Path.GetFileNameWithoutExtension(file);
                _textureFiles.TryAdd(name, file);
            }
        }
    }

    public void BuildMeshDataCache()
    {
        _meshDataCache = new Dictionary<string, MBMeshData>(StringComparer.OrdinalIgnoreCase);

        if (!Directory.Exists(ResourcePath)) return;

        foreach (var jsonPath in Directory.EnumerateFiles(ResourcePath, "meshes.json", SearchOption.AllDirectories))
        {
            try
            {
                var json = File.ReadAllText(jsonPath);
                var meshes = JsonConvert.DeserializeObject<MBMeshData[]>(json);
                foreach (var mesh in meshes)
                {
                    _meshDataCache.TryAdd(mesh.name, mesh);
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"Failed to parse {jsonPath}: {ex.Message}");
            }
        }
    }

    public Material GetMaterial(string name)
    {
        if (string.IsNullOrEmpty(name)) return null;

        if (_loadedMaterials.TryGetValue(name, out var cached))
            return cached;

        if (_materialFiles != null && _materialFiles.TryGetValue(name, out var path))
        {
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat != null)
            {
                _loadedMaterials[name] = mat;
                return mat;
            }
        }

        return null;
    }

    public Texture2D GetTexture(string name)
    {
        if (string.IsNullOrEmpty(name) || name == "none") return null;

        // Strip extension - BRF may reference "armor_a.dds" but file is armor_a.png
        var lookupName = Path.GetFileNameWithoutExtension(name);

        if (_loadedTextures.TryGetValue(lookupName, out var cached))
            return cached;

        if (_textureFiles != null && _textureFiles.TryGetValue(lookupName, out var path))
        {
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (tex != null)
            {
                _loadedTextures[lookupName] = tex;
                return tex;
            }
        }

        return null;
    }

    /// <summary>
    /// Get all cached texture file paths. 
    /// Used by BrfDataPopulator.BuildTextureFileCache() for cross-BRF resolution.
    /// </summary>
    public IEnumerable<string> GetTextureFiles()
    {
        return _textureFiles?.Values;
    }

    /// <summary>
    /// Check if a texture exists in this context (by name, without loading).
    /// </summary>
    public bool HasTexture(string name)
    {
        if (string.IsNullOrEmpty(name) || name == "none") return false;
        var lookupName = Path.GetFileNameWithoutExtension(name);
        return _textureFiles != null && _textureFiles.ContainsKey(lookupName);
    }

    /// <summary>
    /// Get the file path for a texture by name, or null if not found.
    /// </summary>
    public string GetTexturePath(string name)
    {
        if (string.IsNullOrEmpty(name) || name == "none") return null;
        var lookupName = Path.GetFileNameWithoutExtension(name);
        return _textureFiles != null && _textureFiles.TryGetValue(lookupName, out var path) ? path : null;
    }

    public bool TryGetMeshData(string meshName, out MBMeshData data)
    {
        return _meshDataCache.TryGetValue(meshName, out data);
    }

    public IEnumerable<string> GetObjFiles() => _objFiles?.Values;

    public void Dispose()
    {
        _objFiles?.Clear();
        _materialFiles?.Clear();
        _textureFiles?.Clear();
        _meshDataCache?.Clear();
        _loadedMaterials?.Clear();
        _loadedTextures?.Clear();
        _loadedMeshes?.Clear();
    }
}