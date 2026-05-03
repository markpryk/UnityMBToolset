using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;
using UnityEngine;

namespace MountAndBlade.Data
{
    // DATA: Parsed particle attachment info from trigger scripts

    /// <summary>
    /// A particle system reference extracted from a scene prop / item trigger.
    /// Captures the psys ID, position delta, color, and emission parameters.
    ///
    /// From header_operations.py:
    ///   set_position_delta      = 1955  (x, y, z) - centiunits, stateful
    ///   set_current_color       = 1950  (r, g, b) - stateful
    ///   particle_system_add_new = 1965  (par_sys_id, [position])
    ///   particle_system_emit    = 1968  (par_sys_id, num_particles, period)
    ///   particle_system_burst   = 1969  (par_sys_id, position, [burst_strength_pct])
    /// </summary>
    [Serializable]
    public class TriggerParticleEntry
    {
        /// <summary>Particle system ID (e.g., "psys_torch_fire")</summary>
        public string ParticleSystemID;

        /// <summary>
        /// Raw position offset from set_position_delta in M&amp;B centiunits.
        /// M&amp;B coordinate space: X=right, Y=forward, Z=up.
        /// </summary>
        public Vector3 PositionDeltaRaw;

        /// <summary>
        /// Position offset converted to Unity world space (meters).
        /// M&amp;B (X,Y,Z) → Unity (X,Z,Y), centiunits→meters (/100).
        /// </summary>
        public Vector3 PositionOffsetUnity;

        /// <summary>
        /// Color from set_current_color (0-255 range normalized to 0-1).
        /// White if no set_current_color precedes this entry.
        /// </summary>
        public Color EmitColor = Color.white;

        /// <summary>
        /// Emit strength from particle_system_emit (num_particles param).
        /// 0 = continuous (add_new only, uses AlwaysEmit flag from data).
        /// </summary>
        public int EmitStrength;

        /// <summary>
        /// Emit period from particle_system_emit (in 1/100th seconds).
        /// 0 = not specified.
        /// </summary>
        public int EmitPeriod;

        /// <summary>
        /// Burst strength percentage from particle_system_burst.
        /// 0 = not a burst. 100 = full strength.
        /// </summary>
        public int BurstStrengthPercent;

        /// <summary>The operation that created this entry</summary>
        public ParticleOperation Operation;

        /// <summary>The trigger condition block (e.g., "ti_on_init_scene_prop")</summary>
        public string TriggerCondition;
    }

    public enum ParticleOperation
    {
        AddNew,     // particle_system_add_new - continuous emitter
        Emit,       // particle_system_emit    - emit N particles over period
        Burst       // particle_system_burst   - one-shot burst
    }

    // PARSER

    /// <summary>
    /// Parses M&amp;B module_scene_props.py / module_items.py trigger strings
    /// to extract particle system references with position, color, and emit params.
    ///
    /// The position delta and color are stateful: they persist until the next
    /// set_position_delta / set_current_color call, exactly like the M&amp;B engine.
    ///
    /// Supports raw Python trigger format as stored in MBScenePropData.Triggers:
    ///   (set_position_delta, 0, -35, 48),
    ///   (particle_system_add_new, "psys_torch_fire"),
    ///   (particle_system_emit, "psys_fire_glow_1", 9000000),
    ///   (particle_system_burst, "psys_explosion", pos1, 50),
    /// </summary>
    public static class ScenePropParticleTriggerParser
    {
        // Centiunit → meter conversion
        private const float CENTIUNIT_TO_METER = 0.01f;


        // set_position_delta, x, y, z
        private static readonly Regex SetPositionDeltaPattern = new(
            @"set_position_delta\s*,\s*(-?[\d.]+)\s*,\s*(-?[\d.]+)\s*,\s*(-?[\d.]+)",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        // set_current_color, r, g, b (values 0-255 range typically, or 0x hex)
        private static readonly Regex SetCurrentColorPattern = new(
            @"set_current_color\s*,\s*(-?[\d.]+)\s*,\s*(-?[\d.]+)\s*,\s*(-?[\d.]+)",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        // particle_system_add_new, "psys_xxx"
        private static readonly Regex ParticleAddNewPattern = new(
            @"particle_system_add_new\s*,\s*""(psys_[^""]+)""",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        // particle_system_emit, "psys_xxx", num_particles [, period]
        // header_operations.py: (particle_system_emit, <par_sys_id>, <value_num_particles>, <value_period>)
        private static readonly Regex ParticleEmitPattern = new(
            @"particle_system_emit\s*,\s*""(psys_[^""]+)""\s*,\s*(\d+)(?:\s*,\s*(\d+))?",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        // particle_system_burst, "psys_xxx", position_no [, percentage_burst_strength]
        // header_operations.py: (particle_system_burst, <par_sys_id>, <position>, [percentage_burst_strength])
        private static readonly Regex ParticleBurstPattern = new(
            @"particle_system_burst\s*,\s*""(psys_[^""]+)""\s*,\s*(\w+)(?:\s*,\s*(\d+))?",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        // Trigger condition identifiers
        private static readonly Regex TriggerConditionPattern = new(
            @"\(\s*(ti_\w+)",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);


        /// <summary>
        /// Parse a raw trigger string and extract all particle system references.
        /// Returns empty list if no particle operations found.
        /// </summary>
        public static List<TriggerParticleEntry> Parse(string triggerString)
        {
            var results = new List<TriggerParticleEntry>();

            if (string.IsNullOrWhiteSpace(triggerString))
                return results;

            // Stateful tracking (mirrors M&B engine behavior)
            Vector3 currentDeltaRaw = Vector3.zero;
            Color currentColor = Color.white;
            string currentTriggerCondition = "";

            // Process line by line to preserve operation order
            var lines = triggerString.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);

            foreach (string line in lines)
            {
                string trimmed = line.Trim();

                // Skip comments
                if (trimmed.StartsWith("#"))
                    continue;

                // Detect trigger condition blocks (ti_on_init_scene_prop, etc.)
                var condMatch = TriggerConditionPattern.Match(trimmed);
                if (condMatch.Success && !trimmed.Contains("particle_system"))
                {
                    currentTriggerCondition = condMatch.Groups[1].Value;
                }

                var deltaMatch = SetPositionDeltaPattern.Match(trimmed);
                if (deltaMatch.Success)
                {
                    float x = ParseFloat(deltaMatch.Groups[1].Value);
                    float y = ParseFloat(deltaMatch.Groups[2].Value);
                    float z = ParseFloat(deltaMatch.Groups[3].Value);
                    currentDeltaRaw = new Vector3(x, y, z);
                    continue;
                }

                var colorMatch = SetCurrentColorPattern.Match(trimmed);
                if (colorMatch.Success)
                {
                    float r = ParseFloat(colorMatch.Groups[1].Value);
                    float g = ParseFloat(colorMatch.Groups[2].Value);
                    float b = ParseFloat(colorMatch.Groups[3].Value);

                    // M&B color values: 0-255 int range → normalize to 0-1
                    // Some mods may use 0.0-1.0 floats; detect by max value
                    float maxVal = Mathf.Max(r, g, b);
                    if (maxVal > 1f)
                        currentColor = new Color(r / 255f, g / 255f, b / 255f, 1f);
                    else
                        currentColor = new Color(r, g, b, 1f);

                    continue;
                }

                var addMatch = ParticleAddNewPattern.Match(trimmed);
                if (addMatch.Success)
                {
                    results.Add(CreateEntry(
                        addMatch.Groups[1].Value,
                        currentDeltaRaw,
                        currentColor,
                        ParticleOperation.AddNew,
                        currentTriggerCondition));
                    continue;
                }

                var emitMatch = ParticleEmitPattern.Match(trimmed);
                if (emitMatch.Success)
                {
                    string psysId = emitMatch.Groups[1].Value;
                    int numParticles = int.Parse(emitMatch.Groups[2].Value);
                    int period = emitMatch.Groups[3].Success
                        ? int.Parse(emitMatch.Groups[3].Value)
                        : 0;

                    // Check if this psys was already added via add_new
                    // (emit often follows add_new for the same psys to configure burst)
                    var existing = results.FindLast(e =>
                        e.ParticleSystemID == psysId &&
                        e.Operation == ParticleOperation.AddNew &&
                        e.EmitStrength == 0);

                    if (existing != null)
                    {
                        // Update existing add_new entry with emit parameters
                        existing.EmitStrength = numParticles;
                        existing.EmitPeriod = period;
                    }
                    else
                    {
                        // Standalone emit (no prior add_new)
                        var entry = CreateEntry(
                            psysId,
                            currentDeltaRaw,
                            currentColor,
                            ParticleOperation.Emit,
                            currentTriggerCondition);
                        entry.EmitStrength = numParticles;
                        entry.EmitPeriod = period;
                        results.Add(entry);
                    }

                    continue;
                }

                var burstMatch = ParticleBurstPattern.Match(trimmed);
                if (burstMatch.Success)
                {
                    string psysId = burstMatch.Groups[1].Value;
                    // Groups[2] is position_no - typically a register reference, skip
                    int burstPct = burstMatch.Groups[3].Success
                        ? int.Parse(burstMatch.Groups[3].Value)
                        : 100;

                    var entry = CreateEntry(
                        psysId,
                        currentDeltaRaw,
                        currentColor,
                        ParticleOperation.Burst,
                        currentTriggerCondition);
                    entry.BurstStrengthPercent = burstPct;
                    results.Add(entry);
                }
            }

            return results;
        }

        /// <summary>
        /// Quick check if a trigger string contains any particle system operations.
        /// </summary>
        public static bool HasParticleSystems(string triggerString)
        {
            if (string.IsNullOrWhiteSpace(triggerString))
                return false;

            return triggerString.Contains("particle_system_add_new") ||
                   triggerString.Contains("particle_system_emit") ||
                   triggerString.Contains("particle_system_burst");
        }

        /// <summary>
        /// Get just the unique particle system IDs from a trigger string.
        /// </summary>
        public static HashSet<string> GetParticleSystemIDs(string triggerString)
        {
            var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            if (string.IsNullOrWhiteSpace(triggerString))
                return ids;

            foreach (Match m in ParticleAddNewPattern.Matches(triggerString))
                ids.Add(m.Groups[1].Value);
            foreach (Match m in ParticleEmitPattern.Matches(triggerString))
                ids.Add(m.Groups[1].Value);
            foreach (Match m in ParticleBurstPattern.Matches(triggerString))
                ids.Add(m.Groups[1].Value);

            return ids;
        }


        private static TriggerParticleEntry CreateEntry(
            string psysId, Vector3 deltaRaw, Color color,
            ParticleOperation op, string triggerCondition)
        {
            return new TriggerParticleEntry
            {
                ParticleSystemID = psysId,
                PositionDeltaRaw = deltaRaw,
                PositionOffsetUnity = ConvertToUnityOffset(deltaRaw),
                EmitColor = color,
                Operation = op,
                TriggerCondition = triggerCondition
            };
        }

        /// <summary>
        /// Convert M&amp;B centiunits (X=right, Y=forward, Z=up) →
        /// Unity meters (X=right, Y=up, Z=forward).
        /// </summary>
        private static Vector3 ConvertToUnityOffset(Vector3 mbDelta)
        {
            return new Vector3(
                mbDelta.x * CENTIUNIT_TO_METER,  // X → X (right)
                mbDelta.z * CENTIUNIT_TO_METER,  // Z → Y (up)
                mbDelta.y * CENTIUNIT_TO_METER   // Y → Z (forward)
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
