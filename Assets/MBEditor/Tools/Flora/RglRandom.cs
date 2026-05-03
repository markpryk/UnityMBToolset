using UnityEngine;

namespace MBEditor.Tools.Flora.Core
{
    /// <summary>
    /// Reimplementation of Warband's custom LCG PRNG (rglSrand/rglRand).
    /// Uses standard MSVC LCG constants: a = 214013, c = 2531011.
    /// </summary>
    public static class RglRandom
    {
        private static int _seed = 0;

        public static void Srand(int seed)
        {
            _seed = seed;
        }

        public static int Rand()
        {
            _seed = (_seed * 214013 + 2531011);
            return (_seed >> 16) & 0x7FFF;
        }

        public static int Rand(int max)
        {
            if (max <= 0) return 0;
            return Rand() % max;
        }

        public static int Rand(int min, int max)
        {
            if (max <= min) return min;
            return min + (Rand() % (max - min));
        }

        public static float Randf()
        {
            // Warband's rglRandf() typically returns [0, 1)
            // Using standard rand() / RAND_MAX scaling
            return (float)Rand() / 32767.0f;
        }

        public static float Randf(float max)
        {
            return Randf() * max;
        }

        public static float Randf(float min, float max)
        {
            return min + Randf() * (max - min);
        }
    }
}
