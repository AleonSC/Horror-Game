using Game.Audio;
using Game.Core;
using Game.Player;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests
{
    public class StaminaTests
    {
        static readonly PlayerSettings S = new PlayerSettings();

        static void Run(Stamina st, float seconds, bool sprint, bool moving, int lamps = 0)
        {
            for (float t = 0f; t < seconds; t += 0.05f) st.Tick(0.05f, sprint, moving, S, lamps);
        }

        [Test]
        public void Sprinting_LastsNineSeconds_ThenWinded()
        {
            var st = new Stamina();
            Run(st, 8.5f, true, true);
            Assert.IsFalse(st.Winded);
            Assert.Greater(st.Value, 0f);
            Run(st, 1f, true, true);
            Assert.IsTrue(st.Winded);
            Assert.IsFalse(st.CanSprint);
        }

        [Test]
        public void Winded_UntilBackTo35Percent()
        {
            var st = new Stamina();
            Run(st, 9.1f, true, true); // out at 9 s (then a moment of walking recovery)
            Assert.IsTrue(st.Winded);
            Run(st, 7f * 0.3f, false, false); // about 30% back
            Assert.IsTrue(st.Winded);
            Run(st, 7f * 0.1f, false, false); // 40% back
            Assert.IsFalse(st.Winded);
        }

        [Test]
        public void Walking_RecoversSlower_AndLampsSlowerStill()
        {
            var standing = new Stamina();
            var walking = new Stamina();
            var lamps = new Stamina();
            foreach (var st in new[] { standing, walking, lamps }) Run(st, 10f, true, true);
            Run(standing, 3f, false, false);
            Run(walking, 3f, false, true);
            Run(lamps, 3f, false, false, 5);
            Assert.Greater(standing.Value, walking.Value);
            Assert.Greater(standing.Value, lamps.Value);
        }

        [Test]
        public void SnackBonus_MakesSprintLast()
        {
            var st = new Stamina { Bonus = 0.25f };
            Run(st, 10f, true, true);
            Assert.IsFalse(st.Winded); // 9 s x 1.25 = 11.25 s
        }

        [Test]
        public void WindedEvent_FiresBothWays()
        {
            var st = new Stamina();
            int on = 0, off = 0;
            st.WindedChanged += w => { if (w) on++; else off++; };
            Run(st, 10f, true, true);
            st.Refill();
            Assert.AreEqual(1, on);
            Assert.AreEqual(1, off);
        }
    }

    public class BatteryTests
    {
        [Test]
        public void Drains_OverItsLife()
        {
            var b = new FlashlightBattery();
            b.Drain(75f, 150f);
            Assert.AreEqual(0.5f, b.Charge, 1e-4f);
            b.Drain(100f, 150f);
            Assert.IsTrue(b.Empty);
        }

        [Test]
        public void Spend_Add_AtLeast()
        {
            var b = new FlashlightBattery();
            b.Spend(0.95f);
            b.Add(0.1f);
            Assert.AreEqual(0.15f, b.Charge, 1e-4f);
            b.AtLeast(0.3f);
            Assert.AreEqual(0.3f, b.Charge, 1e-4f);
            b.Add(5f);
            Assert.AreEqual(1f, b.Charge);
        }

        [Test]
        public void DimsBelowTheLowMark()
        {
            var b = new FlashlightBattery();
            Assert.AreEqual(1f, b.Brightness(0.2f, 0.45f));
            b.Spend(0.9f); // 10% left: halfway into the low band
            Assert.AreEqual(Mathf.Lerp(1f, 0.45f, 0.5f), b.Brightness(0.2f, 0.45f), 1e-4f);
        }
    }

    public class SafeZoneTests
    {
        [Test]
        public void Contains_IgnoresHeight_AndRespectsActive()
        {
            var zones = new SafeZones();
            var lamp = new SafeZone("Lamp", new Vector3(10f, 0f, 0f), 13f);
            zones.Add(lamp);
            Assert.IsTrue(zones.Contains(new Vector3(20f, 50f, 0f)));
            Assert.IsFalse(zones.Contains(new Vector3(24f, 0f, 0f)));
            Assert.IsTrue(zones.Contains(new Vector3(24f, 0f, 0f), 1.5f));
            lamp.Active = false;
            Assert.IsFalse(zones.Contains(new Vector3(10f, 0f, 0f)));
        }

        [Test]
        public void Contains_GivesTheZoneYoureDeepestIn()
        {
            var zones = new SafeZones();
            var big = new SafeZone("Big", Vector3.zero, 20f);
            var small = new SafeZone("Small", new Vector3(5f, 0f, 0f), 3f);
            zones.Add(big);
            zones.Add(small);
            Assert.IsTrue(zones.Contains(new Vector3(5f, 0f, 0f), out var z));
            Assert.AreSame(big, z); // 20 m deep in the big one vs 3 m in the small one
        }
    }

    public class SoundSynthTests
    {
        [Test]
        public void EverySound_IsFiniteAndInRange()
        {
            var sounds = new[]
            {
                SoundSynth.Creep(1), SoundSynth.Appear(1), SoundSynth.Pound(1), SoundSynth.Crescendo(1), SoundSynth.Portal(1),
                SoundSynth.Shriek(1), SoundSynth.Moan(1), SoundSynth.Heartbeat(1), SoundSynth.Breath(1), SoundSynth.Click(1), SoundSynth.LampOn(1),
            };
            foreach (var s in sounds)
            {
                Assert.Greater(s.Length, 100);
                float peak = 0f;
                foreach (float v in s)
                {
                    Assert.IsFalse(float.IsNaN(v) || float.IsInfinity(v));
                    peak = Mathf.Max(peak, Mathf.Abs(v));
                }
                Assert.LessOrEqual(peak, 1f);
                Assert.Greater(peak, 0.1f);
            }
        }

        [Test]
        public void SameSeed_SameSound()
        {
            CollectionAssert.AreEqual(SoundSynth.Creep(3), SoundSynth.Creep(3));
        }
    }

    public class HudTests
    {
        [Test]
        public void Spaced() => Assert.AreEqual("C R A N K", Game.UI.Hud.Spaced("Crank"));
    }
}
