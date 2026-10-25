using UnityEngine;

namespace Game.Core
{
    /// <summary>Small sums Unity doesn't have.</summary>
    public static class MathUtil
    {
        /// <summary>
        /// A smooth threshold: 0 below <paramref name="from"/>, 1 above <paramref name="to"/>, easing between (the
        /// shader smoothstep). Not Unity's Mathf.SmoothStep, which blends between its first two values instead.
        /// </summary>
        public static float SmoothThreshold(float from, float to, float x)
        {
            float t = Mathf.Clamp01((x - from) / (to - from));
            return t * t * (3f - 2f * t);
        }
    }
}
