using System.Collections.Generic;
using MountAndBlade.Data;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Attaches Unity Light components to prefabs based on parsed trigger data.
/// Call from MBPrefabsGenerator.CreateScenePropPrefab after particle attachment.
///
/// Structure:
///   spr_torch (MBSceneProp)
/// </summary>
public static class ScenePropLightAttacher
{
    /// <summary>
    /// Attach point lights as children of a prefab root.
    /// Returns number of lights attached.
    /// </summary>
    public static int AttachLights(GameObject root, List<TriggerLightEntry> entries)
    {
        if (entries == null || entries.Count == 0) return 0;

        var lightsRoot = new GameObject("Lights");
        lightsRoot.transform.SetParent(root.transform);
        lightsRoot.transform.localPosition = Vector3.zero;
        
        // At the start, on lightsRoot:
        var mbLightSystem = lightsRoot.AddComponent<MBLightSystem>();
        mbLightSystem.TriggerEntries = new List<TriggerLightEntry>(entries);

        int count = 0;
        foreach (var entry in entries)
        {
            string name = entry.NightOnly ? "PointLight_Night" : "PointLight";
            if (entries.Count > 1) name += $"_{count}";

            var lightObj = new GameObject(name);
            lightObj.transform.SetParent(lightsRoot.transform);
            lightObj.transform.localPosition = entry.PositionOffsetUnity;

            // Unity Light component
            var light = lightObj.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = entry.LightColor;
            light.intensity = entry.Intensity;

            if (entry.Range > 0f)
                light.range = entry.Range;
            else
                light.range = EstimateRange(entry.Intensity);

            light.shadows = LightShadows.Soft;
            light.renderMode = LightRenderMode.Auto;

            // Metadata component for runtime flicker + trigger data
            var triggerLight = lightObj.AddComponent<MBTriggerLight>();
            triggerLight.Entry = entry;
            triggerLight.FlickerMagnitude = entry.FlickerMagnitudeNormalized;
            triggerLight.FlickerInterval = entry.FlickerIntervalSeconds;
            triggerLight.NightOnly = entry.NightOnly;
            triggerLight.BaseIntensity = entry.Intensity;
            
            mbLightSystem.Lights.Add(triggerLight);

            count++;
        }

        return count;
    }

    /// <summary>
    /// Estimate a reasonable light range from intensity.
    /// M&amp;B doesn't always specify range - derive from intensity.
    /// </summary>
    private static float EstimateRange(float intensity)
    {
        // Typical M&B torch: intensity ~2.0 → range ~8-10m
        return Mathf.Clamp(intensity * 4f, 3f, 20f);
    }
}
