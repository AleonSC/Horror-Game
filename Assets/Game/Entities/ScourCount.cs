using UnityEngine;

namespace Game.Entities
{
    /// <summary>
    /// How many scours there are (section 6.7): one after the calm start, one more per lamp lit, and one more
    /// every minute for the first ten minutes after the calm (the plan's ScourClock), both scaled by difficulty.
    /// No cap. Plain C#, so it can be tested.
    /// </summary>
    public static class ScourCount
    {
        /// <summary>How many the clock has added by <paramref name="elapsed"/> seconds into the run.</summary>
        /// <param name="timeFactor">Difficulty's "time between extra scours" (1 Normal).</param>
        /// <param name="countFactor">Difficulty's "how many extra scours" (1 Normal).</param>
        public static int FromClock(float elapsed, ScourSettings s, float timeFactor, float countFactor)
        {
            float every = Mathf.Max(1f, s.extraEvery * Mathf.Max(0.01f, timeFactor));
            int steps = Mathf.Clamp(Mathf.FloorToInt((elapsed - s.firstDelay) / every), 0, s.extraCount);
            return Mathf.FloorToInt(steps * Mathf.Max(0f, countFactor) + 0.0001f);
        }

        /// <summary>All of them: the first, one per lamp, and the clock's.</summary>
        public static int Total(float elapsed, int lampsLit, ScourSettings s, float timeFactor, float countFactor) =>
            1 + Mathf.Max(0, lampsLit) + FromClock(elapsed, s, timeFactor, countFactor);
    }
}
