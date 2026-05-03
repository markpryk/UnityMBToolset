using System.Collections.Generic;
using MountAndBlade.Data;
using UnityEngine;

/// <summary>
/// Light controller for props/items with trigger-defined point lights.
/// Lives on the Lights/ child alongside MBParticleSystem on Particles/.
/// </summary>
public class MBLightSystem : MonoBehaviour
{
    [Header("Lights")]
    public List<MBTriggerLight> Lights = new();

    [Header("Source Data")]
    public List<TriggerLightEntry> TriggerEntries = new();

    public int LightCount => Lights.Count;

    public void EnableAll()
    {
        foreach (var l in Lights)
            if (l != null) l.enabled = true;
    }

    public void DisableAll()
    {
        foreach (var l in Lights)
            if (l != null) l.enabled = false;
    }

    public void SetNightMode(bool isNight)
    {
        foreach (var l in Lights)
        {
            if (l == null) continue;
            if (l.NightOnly)
                l.gameObject.SetActive(isNight);
        }
    }

    public void SetIntensityScale(float scale)
    {
        foreach (var l in Lights)
        {
            if (l == null) continue;
            var light = l.GetComponent<Light>();
            if (light != null)
                light.intensity = l.BaseIntensity * scale;
        }
    }
}