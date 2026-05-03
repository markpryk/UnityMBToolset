using MountAndBlade.Data;
using UnityEngine;

/// <summary>
/// Runtime component for M&amp;B trigger-defined point lights.
/// Handles flicker animation and night-only toggling.
/// Stores the original trigger data for round-trip editing.
/// </summary>
public class MBTriggerLight : MonoBehaviour
{
    [Header("Trigger Data")]
    public TriggerLightEntry Entry;

    [Header("Flicker")]
    [Tooltip("0 = steady, 1 = full intensity variation")]
    [Range(0f, 1f)]
    public float FlickerMagnitude;

    [Tooltip("Seconds between flicker cycles")]
    public float FlickerInterval = 0.3f;

    [Header("Base")]
    public float BaseIntensity = 1f;
    public bool NightOnly;

    private Light _light;
    private float _flickerTimer;
    private float _flickerTarget;
    private float _flickerCurrent;

    private void Awake()
    {
        _light = GetComponent<Light>();
        _flickerCurrent = BaseIntensity;
        _flickerTarget = BaseIntensity;
    }

    private void Update()
    {
        if (_light == null || FlickerMagnitude <= 0f) return;

        _flickerTimer -= Time.deltaTime;
        if (_flickerTimer <= 0f)
        {
            _flickerTimer = FlickerInterval * Random.Range(0.7f, 1.3f);
            float variation = BaseIntensity * FlickerMagnitude;
            _flickerTarget = BaseIntensity + Random.Range(-variation, variation);
        }

        _flickerCurrent = Mathf.Lerp(_flickerCurrent, _flickerTarget,
            Time.deltaTime / Mathf.Max(FlickerInterval * 0.5f, 0.01f));

        _light.intensity = Mathf.Max(0f, _flickerCurrent);
    }
}