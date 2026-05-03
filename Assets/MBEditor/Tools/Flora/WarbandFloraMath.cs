using UnityEngine;
using MBEditor.Tools.Flora.Core;

namespace MBEditor.Tools.Flora.Logic
{
    public static class WarbandFloraMath
    {
        public static float Clamp(float v, float min, float max) => Mathf.Clamp(v, min, max);

        /// <summary>
        /// Calculates earth intensity based on slope (normal.y in Unity matches normal.z in Warband).
        /// Warband: vertex->m_normal.z (up)
        /// Unity: vector.y (up)
        /// </summary>
        public static float ComputeEarthIntensity(Vector3 normal, float barrenness)
        {
            // Warband: 1.0f - rglClamp(((1.0f - vertex->m_normal.z) - m_barrenness) * 12.0f, 0.0f, 1.0f);
            // Unity normal.y is up.
            return 1.0f - Clamp(((1.0f - normal.y) - barrenness) * 12.0f, 0.0f, 1.0f);
        }

        /// <summary>
        /// Calculates green intensity based on position noise and earth intensity.
        /// </summary>
        public static float ComputeGreenIntensity(int x, int y, float earthIntensity)
        {
            // Warband: rglVector4 noise = rglPerlinOctave(15.0f, 0.6f, 3, false, rglVector4((float)x, (float)y, 0.0f));
            // Note: Warband uses cell coordinates (integers) for this noise, not world space floats.
            
            Vector4 noise = RglPerlin.Octave(15.0f, 0.61f, 3, false, new Vector4((float)x, (float)y, 0.0f, 0.0f));
            float intensity = Clamp((noise.x + noise.y + noise.z) * 4.5f + 0.7f, 0.0f, 1.0f);

            return earthIntensity * intensity;
        }

        public static bool IsRiverbed(float height, bool placeRiver)
        {
            return placeRiver && height < 0.0f;
        }
    }
}
