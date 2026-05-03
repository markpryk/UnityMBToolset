using UnityEngine;
using UnityEditor;

/// <summary>
/// Context for tint brush painting operations.
/// Similar to Unity's PaintContext but for our control texture system.
/// </summary>
public class TintBrushContext : System.IDisposable
{
    public Texture2D ControlTexture { get; private set; }
    public int Channel { get; private set; }
    public Terrain Terrain { get; private set; }
    
    // Brush parameters
    public float BrushSize { get; set; }
    public float BrushStrength { get; set; }
    public float BrushFalloff { get; set; }
    
    // Cached pixel data for the affected region
    private Color[] _pixels;
    private int _blockX, _blockY, _blockW, _blockH;
    private bool _isDirty;
    
    // Texture dimensions
    public int TextureWidth => ControlTexture?.width ?? 0;
    public int TextureHeight => ControlTexture?.height ?? 0;

    public TintBrushContext(Texture2D controlTexture, int channel, Terrain terrain)
    {
        ControlTexture = controlTexture;
        Channel = channel;
        Terrain = terrain;
        _isDirty = false;
    }

    /// <summary>
    /// Begin a paint stroke at the given UV position.
    /// Calculates the affected region and caches pixel data.
    /// </summary>
    public void BeginStroke(float u, float v)
    {
        if (ControlTexture == null || Terrain == null)
            return;

        int texW = ControlTexture.width;
        int texH = ControlTexture.height;

        Vector3 terrainSize = Terrain.terrainData.size;

        // Calculate brush radius in pixels (handle non-square)
        float brushPixelRadiusX = (BrushSize / terrainSize.x) * texW / 2f;
        float brushPixelRadiusY = (BrushSize / terrainSize.z) * texH / 2f;

        int radiusX = Mathf.CeilToInt(brushPixelRadiusX) + 1;
        int radiusY = Mathf.CeilToInt(brushPixelRadiusY) + 1;

        int centerX = Mathf.RoundToInt(u * (texW - 1));
        int centerY = Mathf.RoundToInt(v * (texH - 1));

        _blockX = Mathf.Clamp(centerX - radiusX, 0, texW - 1);
        int xMax = Mathf.Clamp(centerX + radiusX, 0, texW - 1);
        _blockY = Mathf.Clamp(centerY - radiusY, 0, texH - 1);
        int yMax = Mathf.Clamp(centerY + radiusY, 0, texH - 1);

        _blockW = xMax - _blockX + 1;
        _blockH = yMax - _blockY + 1;

        if (_blockW <= 0 || _blockH <= 0)
        {
            _pixels = null;
            return;
        }

        // Cache pixels
        _pixels = ControlTexture.GetPixels(_blockX, _blockY, _blockW, _blockH);
    }

    /// <summary>
    /// Apply brush at UV coordinates.
    /// </summary>
    public void ApplyBrush(float u, float v, bool erase)
    {
        if (_pixels == null || ControlTexture == null)
            return;

        int texW = ControlTexture.width;
        int texH = ControlTexture.height;

        Vector3 terrainSize = Terrain.terrainData.size;

        float brushPixelRadiusX = (BrushSize / terrainSize.x) * texW / 2f;
        float brushPixelRadiusY = (BrushSize / terrainSize.z) * texH / 2f;

        int centerX = Mathf.RoundToInt(u * (texW - 1));
        int centerY = Mathf.RoundToInt(v * (texH - 1));

        float falloffStart = 1f - BrushFalloff;

        for (int py = 0; py < _blockH; py++)
        {
            int pixelY = _blockY + py;
            float dy = (pixelY - centerY) / brushPixelRadiusY;

            for (int px = 0; px < _blockW; px++)
            {
                int pixelX = _blockX + px;
                float dx = (pixelX - centerX) / brushPixelRadiusX;

                float dist = Mathf.Sqrt(dx * dx + dy * dy);
                if (dist > 1f)
                    continue;

                // Smoothstep falloff
                float falloff;
                if (dist <= falloffStart)
                {
                    falloff = 1f;
                }
                else
                {
                    float t = (dist - falloffStart) / Mathf.Max(0.001f, BrushFalloff);
                    falloff = 1f - Smoothstep(t);
                }

                float paintAmount = BrushStrength * falloff;

                int idx = py * _blockW + px;
                Color pixel = _pixels[idx];

                float currentValue = GetChannel(pixel, Channel);
                float newValue;

                if (erase)
                {
                    newValue = Mathf.Max(0f, currentValue - paintAmount);
                }
                else
                {
                    newValue = Mathf.Min(1f, currentValue + paintAmount);
                }

                _pixels[idx] = SetChannel(pixel, Channel, newValue);
            }
        }

        _isDirty = true;
    }

    /// <summary>
    /// Commit changes to the texture.
    /// </summary>
    public void EndStroke()
    {
        if (!_isDirty || _pixels == null || ControlTexture == null)
            return;

        Undo.RegisterCompleteObjectUndo(ControlTexture, "Paint Tint Mask");

        ControlTexture.SetPixels(_blockX, _blockY, _blockW, _blockH, _pixels);
        ControlTexture.Apply(false, false);

        EditorUtility.SetDirty(ControlTexture);

        _isDirty = false;
    }

    public void Dispose()
    {
        if (_isDirty)
        {
            EndStroke();
        }
        _pixels = null;
    }

    private static float Smoothstep(float t)
    {
        t = Mathf.Clamp01(t);
        return t * t * (3f - 2f * t);
    }

    private static float GetChannel(Color c, int channel)
    {
        return channel switch
        {
            0 => c.r,
            1 => c.g,
            2 => c.b,
            3 => c.a,
            _ => 0f
        };
    }

    private static Color SetChannel(Color c, int channel, float value)
    {
        switch (channel)
        {
            case 0: c.r = value; break;
            case 1: c.g = value; break;
            case 2: c.b = value; break;
            case 3: c.a = value; break;
        }
        return c;
    }
}

/// <summary>
/// Static utility for terrain brush operations.
/// </summary>
public static class TintBrushUtility
{
    /// <summary>
    /// Raycast terrain using heightmap (more accurate than physics).
    /// </summary>
    public static bool RaycastTerrain(Terrain terrain, Ray ray, out Vector3 hitPoint, out Vector3 hitNormal)
    {
        hitPoint = Vector3.zero;
        hitNormal = Vector3.up;

        if (terrain == null || terrain.terrainData == null)
            return false;

        TerrainData td = terrain.terrainData;
        Vector3 terrainPos = terrain.transform.position;
        Vector3 terrainSize = td.size;

        // Quick physics test first
        if (Physics.Raycast(ray, out RaycastHit physicsHit, 10000f))
        {
            var hitTerrain = physicsHit.collider.GetComponent<Terrain>();
            if (hitTerrain == terrain)
            {
                hitPoint = physicsHit.point;
                float u = (hitPoint.x - terrainPos.x) / terrainSize.x;
                float v = (hitPoint.z - terrainPos.z) / terrainSize.z;
                hitNormal = td.GetInterpolatedNormal(u, v);
                return true;
            }
        }

        // Fallback: Binary search on terrain heightmap
        float tMin = 0f;
        float tMax = 10000f;

        for (int i = 0; i < 32; i++)
        {
            float t = (tMin + tMax) * 0.5f;
            Vector3 p = ray.GetPoint(t);

            float u = (p.x - terrainPos.x) / terrainSize.x;
            float v = (p.z - terrainPos.z) / terrainSize.z;

            if (u < 0 || u > 1 || v < 0 || v > 1)
            {
                tMax = t;
                continue;
            }

            float terrainHeight = terrain.SampleHeight(p) + terrainPos.y;
            float diff = p.y - terrainHeight;

            if (Mathf.Abs(diff) < 0.1f)
            {
                hitPoint = new Vector3(p.x, terrainHeight, p.z);
                hitNormal = td.GetInterpolatedNormal(u, v);
                return true;
            }

            if (diff > 0)
                tMin = t;
            else
                tMax = t;
        }

        return false;
    }

    /// <summary>
    /// Convert world position to terrain UV coordinates.
    /// </summary>
    public static Vector2 WorldToTerrainUV(Terrain terrain, Vector3 worldPos)
    {
        Vector3 terrainPos = terrain.transform.position;
        Vector3 terrainSize = terrain.terrainData.size;

        float u = (worldPos.x - terrainPos.x) / terrainSize.x;
        float v = (worldPos.z - terrainPos.z) / terrainSize.z;

        return new Vector2(Mathf.Clamp01(u), Mathf.Clamp01(v));
    }

    /// <summary>
    /// Draw brush preview in scene view.
    /// </summary>
    public static void DrawBrushPreview(Vector3 position, float size, float falloff, bool erase, float strength)
    {
        float radius = size / 2f;

        Color brushColor = erase
            ? new Color(1f, 0.3f, 0.3f, 0.8f)
            : new Color(0.3f, 1f, 0.3f, 0.8f);

        // Outer ring
        Handles.color = brushColor;
        Handles.DrawWireDisc(position, Vector3.up, radius);

        // Inner falloff ring
        if (falloff > 0.01f)
        {
            float innerRadius = radius * (1f - falloff);
            Handles.color = new Color(brushColor.r, brushColor.g, brushColor.b, 0.4f);
            Handles.DrawWireDisc(position, Vector3.up, innerRadius);
        }

        // Center dot
        Handles.color = brushColor;
        Handles.DrawSolidDisc(position, Vector3.up, Mathf.Max(0.5f, radius * 0.02f));

        // Info label
        Handles.color = Color.white;
        Vector3 labelPos = position + Vector3.up * 1f;
        Handles.Label(labelPos, $"Size: {size:F0}  Strength: {strength:P0}");
    }
}