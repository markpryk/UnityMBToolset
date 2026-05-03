using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;
using UnityEngine;

namespace MountAndBlade.Data
{
    // DATA: Parsed point light from trigger scripts

    /// <summary>
    /// A point light extracted from a scene prop / item trigger.
    ///
    /// From header_operations.py:
    ///   set_position_delta          = 1955  (x, y, z) - centiunits, stateful
    ///   set_current_color           = 1950  (r, g, b) - 0-255 range, stateful
    ///   add_point_light             = 1960  ([flicker_mag], [flicker_interval], [range]) - item triggers
    ///   add_point_light_to_entity   = 1961  ([flicker_mag], [flicker_interval], [range]) - scene prop triggers
    ///
    /// Color is often computed dynamically via store_mul with literal coefficients
    /// and prop scale. We evaluate the coefficients at unit scale (scale=1.0)
    /// to extract the base color. Values frequently exceed 255 → HDR.
    /// </summary>
    [Serializable]
    public class TriggerLightEntry
    {
        /// <summary>
        /// Light color at unit scale (1.0), normalized to 0-1 range.
        /// Extracted from store_mul coefficients or set_current_color.
        /// </summary>
        public Color LightColor = Color.white;

        /// <summary>
        /// HDR intensity multiplier. When M&B color components exceed 255,
        /// the max component becomes intensity and the color is normalized.
        /// Typical values: 1.3 – 2.5 for torches/fires.
        /// </summary>
        public float Intensity = 1f;

        /// <summary>
        /// Raw color values before normalization (at unit scale).
        /// Useful for debugging and exact reproduction.
        /// </summary>
        public Vector3 RawColorValues;

        /// <summary>
        /// Position offset in Unity space (meters).
        /// Converted from set_position_delta centiunits.
        /// </summary>
        public Vector3 PositionOffsetUnity;

        /// <summary>Raw position from set_position_delta (centiunits, M&amp;B space)</summary>
        public Vector3 PositionDeltaRaw;

        /// <summary>
        /// Flicker magnitude (0-100). 0 = steady light.
        /// Controls how much the light intensity varies.
        /// </summary>
        public int FlickerMagnitude;

        /// <summary>
        /// Flicker interval in 1/100th of a second.
        /// 30 = flickers every 0.3 seconds.
        /// </summary>
        public int FlickerInterval;

        /// <summary>
        /// Light range in meters (optional, from extended WSE signature).
        /// 0 = use default range.
        /// </summary>
        public float Range;

        /// <summary>Whether this light is night-only (preceded by is_currently_night check)</summary>
        public bool NightOnly;

        /// <summary>Whether color is scale-dependent (uses prop_instance_get_scale)</summary>
        public bool ScaleDependent;

        /// <summary>The trigger condition block</summary>
        public string TriggerCondition;

        /// <summary>Flicker interval in seconds</summary>
        public float FlickerIntervalSeconds => FlickerInterval * 0.01f;

        /// <summary>Flicker magnitude as 0-1 fraction</summary>
        public float FlickerMagnitudeNormalized => FlickerMagnitude * 0.01f;
    }

    // PARSER

    /// <summary>
    /// Parses M&amp;B trigger strings to extract point light definitions.
    ///
    /// Handles two color patterns:
    ///
    /// 1. Direct set_current_color:
    ///    (set_current_color, 200, 150, 50),
    ///
    /// 2. Computed via store_mul (scale-dependent, most common):
    ///    (store_mul, ":red",   3 * 200, ":scale"),
    ///    (store_mul, ":green", 3 * 145, ":scale"),
    ///    (store_mul, ":blue",  3 *  45, ":scale"),
    ///    (val_div, ":red", 100),
    ///    → At unit scale: color = (600, 435, 135) → HDR
    ///    → Normalized: (1.0, 0.72, 0.23) with intensity 2.35
    ///
    /// Also detects:
    ///   - is_currently_night → NightOnly flag
    ///   - prop_instance_get_scale → ScaleDependent flag
    ///   - light_sphere mesh reference → additional identification hint
    /// </summary>
    public static class ScenePropLightParser
    {
        private const float CENTIUNIT_TO_METER = 0.01f;


        // set_position_delta, x, y, z
        private static readonly Regex SetPositionDeltaPattern = new(
            @"set_position_delta\s*,\s*(-?[\d.]+)\s*,\s*(-?[\d.]+)\s*,\s*(-?[\d.]+)",
            RegexOptions.Compiled);

        // set_current_color, r, g, b (direct literal values)
        private static readonly Regex SetCurrentColorPattern = new(
            @"set_current_color\s*,\s*(-?[\d.]+)\s*,\s*(-?[\d.]+)\s*,\s*(-?[\d.]+)",
            RegexOptions.Compiled);

        // store_mul, ":red", COEFFICIENT, ":scale"
        // Captures the coefficient which may be an expression like "3 * 200"
        // In the raw trigger string, Python pre-evaluates "3 * 200" to "600"
        // But we also handle the literal "3 * 200" form just in case
        private static readonly Regex StoreMulRedPattern = new(
            @"store_mul\s*,\s*"":red""\s*,\s*([\d*\s]+)\s*,",
            RegexOptions.Compiled);
        private static readonly Regex StoreMulGreenPattern = new(
            @"store_mul\s*,\s*"":green""\s*,\s*([\d*\s]+)\s*,",
            RegexOptions.Compiled);
        private static readonly Regex StoreMulBluePattern = new(
            @"store_mul\s*,\s*"":blue""\s*,\s*([\d*\s]+)\s*,",
            RegexOptions.Compiled);

        // add_point_light or add_point_light_to_entity, [flicker_mag], [flicker_interval], [range]
        private static readonly Regex AddPointLightPattern = new(
            @"add_point_light(?:_to_entity)?\s*(?:,\s*(\d+))?(?:\s*,\s*(\d+))?(?:\s*,\s*(\d+))?",
            RegexOptions.Compiled);

        // Trigger condition
        private static readonly Regex TriggerConditionPattern = new(
            @"\(\s*(ti_\w+)",
            RegexOptions.Compiled);


        /// <summary>
        /// Parse a raw trigger string and extract all point light definitions.
        /// </summary>
        public static List<TriggerLightEntry> Parse(string triggerString)
        {
            var results = new List<TriggerLightEntry>();

            if (string.IsNullOrWhiteSpace(triggerString))
                return results;

            // Stateful context
            Vector3 currentDeltaRaw = Vector3.zero;
            Vector3 currentColorRaw = new Vector3(255, 255, 255); // Default white
            bool colorFromStoreMul = false;
            bool nightOnly = false;
            bool scaleDependent = false;
            string currentTriggerCondition = "";

            var lines = triggerString.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);

            foreach (string line in lines)
            {
                string trimmed = line.Trim();

                if (trimmed.StartsWith("#"))
                    continue;

                var condMatch = TriggerConditionPattern.Match(trimmed);
                if (condMatch.Success && !trimmed.Contains("add_point_light"))
                {
                    currentTriggerCondition = condMatch.Groups[1].Value;
                }

                if (trimmed.Contains("is_currently_night"))
                {
                    nightOnly = true;
                    continue;
                }

                if (trimmed.Contains("prop_instance_get_scale"))
                {
                    scaleDependent = true;
                    continue;
                }

                var deltaMatch = SetPositionDeltaPattern.Match(trimmed);
                if (deltaMatch.Success)
                {
                    currentDeltaRaw = new Vector3(
                        ParseFloat(deltaMatch.Groups[1].Value),
                        ParseFloat(deltaMatch.Groups[2].Value),
                        ParseFloat(deltaMatch.Groups[3].Value));
                    continue;
                }

                var colorMatch = SetCurrentColorPattern.Match(trimmed);
                if (colorMatch.Success)
                {
                    // Only use direct color if we haven't seen store_mul pattern
                    if (!colorFromStoreMul)
                    {
                        currentColorRaw = new Vector3(
                            ParseFloat(colorMatch.Groups[1].Value),
                            ParseFloat(colorMatch.Groups[2].Value),
                            ParseFloat(colorMatch.Groups[3].Value));
                    }

                    continue;
                }

                // These override set_current_color since they compute the actual values
                var redMatch = StoreMulRedPattern.Match(trimmed);
                if (redMatch.Success)
                {
                    currentColorRaw.x = EvaluateCoefficient(redMatch.Groups[1].Value);
                    colorFromStoreMul = true;
                    continue;
                }

                var greenMatch = StoreMulGreenPattern.Match(trimmed);
                if (greenMatch.Success)
                {
                    currentColorRaw.y = EvaluateCoefficient(greenMatch.Groups[1].Value);
                    colorFromStoreMul = true;
                    continue;
                }

                var blueMatch = StoreMulBluePattern.Match(trimmed);
                if (blueMatch.Success)
                {
                    currentColorRaw.z = EvaluateCoefficient(blueMatch.Groups[1].Value);
                    colorFromStoreMul = true;
                    continue;
                }

                var lightMatch = AddPointLightPattern.Match(trimmed);
                if (lightMatch.Success && trimmed.Contains("add_point_light"))
                {
                    int flickerMag = lightMatch.Groups[1].Success
                        ? int.Parse(lightMatch.Groups[1].Value) : 0;
                    int flickerInt = lightMatch.Groups[2].Success
                        ? int.Parse(lightMatch.Groups[2].Value) : 0;
                    float range = lightMatch.Groups[3].Success
                        ? int.Parse(lightMatch.Groups[3].Value) : 0f;

                    // Decompose HDR color → normalized color + intensity
                    DecomposeColor(currentColorRaw,
                        out Color normalizedColor, out float intensity);

                    results.Add(new TriggerLightEntry
                    {
                        LightColor = normalizedColor,
                        Intensity = intensity,
                        RawColorValues = currentColorRaw,
                        PositionDeltaRaw = currentDeltaRaw,
                        PositionOffsetUnity = ConvertToUnityOffset(currentDeltaRaw),
                        FlickerMagnitude = flickerMag,
                        FlickerInterval = flickerInt,
                        Range = range,
                        NightOnly = nightOnly,
                        ScaleDependent = scaleDependent,
                        TriggerCondition = currentTriggerCondition
                    });

                    // Reset per-light state (position/color persist, flags don't)
                    colorFromStoreMul = false;
                }
            }

            return results;
        }

        /// <summary>
        /// Quick check if a trigger string contains any point light operations.
        /// </summary>
        public static bool HasPointLights(string triggerString)
        {
            if (string.IsNullOrWhiteSpace(triggerString))
                return false;

            return triggerString.Contains("add_point_light");
        }


        /// <summary>
        /// Decompose raw M&amp;B color values into Unity-friendly color + intensity.
        ///
        /// M&amp;B light colors frequently exceed 255 (HDR). We extract:
        ///   - Normalized color (0-1 range, preserving hue)
        ///   - Intensity multiplier (max component / 255)
        ///
        /// Example: (600, 435, 135) → Color(1.0, 0.72, 0.23), Intensity=2.35
        /// </summary>
        private static void DecomposeColor(Vector3 rawColor, out Color color, out float intensity)
        {
            float maxComponent = Mathf.Max(rawColor.x, Mathf.Max(rawColor.y, rawColor.z));

            if (maxComponent <= 0f)
            {
                color = Color.white;
                intensity = 0f;
                return;
            }

            // Normalize by max component to get hue-correct color
            color = new Color(
                rawColor.x / maxComponent,
                rawColor.y / maxComponent,
                rawColor.z / maxComponent,
                1f);

            // Intensity = how far above standard 255 range
            intensity = maxComponent / 255f;
        }


        /// <summary>
        /// Evaluate a coefficient string that may be a simple number or
        /// a multiplication expression (e.g., "600" or "3 * 200").
        ///
        /// Python pre-evaluates "3 * 200" to 600 before writing the trigger,
        /// but the raw source has the expression form. We handle both.
        /// </summary>
        private static float EvaluateCoefficient(string expr)
        {
            string trimmed = expr.Trim();

            // Check for multiplication expression: "3 * 200"
            if (trimmed.Contains("*"))
            {
                var parts = trimmed.Split('*');
                float result = 1f;
                foreach (var part in parts)
                {
                    result *= ParseFloat(part.Trim());
                }
                return result;
            }

            return ParseFloat(trimmed);
        }

        private static Vector3 ConvertToUnityOffset(Vector3 mbDelta)
        {
            return new Vector3(
                mbDelta.x * CENTIUNIT_TO_METER,  // X → X
                mbDelta.z * CENTIUNIT_TO_METER,  // Z → Y
                mbDelta.y * CENTIUNIT_TO_METER   // Y → Z
            );
        }

        private static float ParseFloat(string value)
        {
            float.TryParse(value,
                NumberStyles.Float | NumberStyles.AllowLeadingSign,
                CultureInfo.InvariantCulture, out float result);
            return result;
        }
    }
}
