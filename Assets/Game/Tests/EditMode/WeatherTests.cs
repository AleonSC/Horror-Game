using Game.Core;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests
{
    public class WindTests
    {
        [Test]
        public void Calm_IsJustTheBreeze()
        {
            var w = new Wind { Breeze = 0.2f, Gusts = 0f };
            for (float t = 0f; t < 10f; t += 0.7f)
                Assert.LessOrEqual(w.At(12f, 34f, t).magnitude, 0.2f + 0.001f);
        }

        [Test]
        public void Gusts_RollDownwind()
        {
            // With the wind along +x, a gust felt at x = 0 reaches x = GustSpeed one second later.
            var w = new Wind { Breeze = 0f, Gusts = 1f, Direction = new Vector2(1f, 0f), GustSpeed = 10f, GustSpacing = 80f };
            float best = 0f, bestT = 0f;
            for (float t = 0f; t < 8f; t += 0.05f)
            {
                float g = w.At(0f, 0f, t).magnitude;
                if (g > best) { best = g; bestT = t; }
            }
            Assert.Greater(best, 0.3f);
            Assert.AreEqual(best, w.At(10f, 0f, bestT + 1f).magnitude, 0.05f); // (the patchiness drifts a little)
        }

        [Test]
        public void Shockwave_PassesAsARing()
        {
            var w = new Wind { Breeze = 0f, Gusts = 0f };
            w.AddShock(new Vector3(0f, 0f, 0f), 0f, 200f);
            // One second on, the ring is 200 m out: strong there, nothing at 50 m or 400 m.
            Assert.Greater(w.At(200f, 0f, 1f).magnitude, 1f);
            Assert.Less(w.At(50f, 0f, 1f).magnitude, 0.01f);
            Assert.Less(w.At(400f, 0f, 1f).magnitude, 0.01f);
            // And it pushes outward.
            Assert.Greater(w.At(200f, 0f, 1f).x, 0f);
        }
    }
}
