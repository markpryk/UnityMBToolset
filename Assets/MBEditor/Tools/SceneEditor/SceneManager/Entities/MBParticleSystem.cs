using System.Collections.Generic;
using MountAndBlade.Data;
using UnityEngine;
using WarbandParticles;

/// <summary>
/// Particle system controller attached to props/items that have
/// trigger-defined particle effects. Lives on the Particles/ child
/// or root alongside MBSceneProp/MBItem.
///
/// Analogous to MBModel - a component that references runtime data,
/// not a prefab identity.
/// </summary>
public class MBParticleSystem : MonoBehaviour
{
    [Header("Emitters")]
    public List<WarbandParticleEmitter> Emitters = new();

    [Header("Source Data")]
    public List<TriggerParticleEntry> TriggerEntries = new();

    public int EmitterCount => Emitters.Count;

    public bool IsAlive
    {
        get
        {
            foreach (var e in Emitters)
                if (e != null && e.IsAlive) return true;
            return false;
        }
    }

    public void EmitAll(int strength)
    {
        foreach (var e in Emitters)
            e?.Emit(strength);
    }

    public void StopAll()
    {
        foreach (var e in Emitters)
            e?.Stop();
    }

    public void RestartAll()
    {
        foreach (var e in Emitters)
            e?.Restart();
    }
}