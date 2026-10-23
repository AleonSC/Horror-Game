using Game.DevTools;
using Game.Player;
using Game.Rendering;
using NUnit.Framework;

namespace Game.Tests
{
    public class LookTests
    {
        [Test]
        public void EveryThemeExists_AndReferenceIsTheDefault()
        {
            foreach (var name in LookThemes.Names) Assert.IsNotNull(LookThemes.Get(name), name);
            Assert.IsNull(LookThemes.Get("No such theme"));

            var reference = LookThemes.Get(LookThemes.Default);
            var plain = new LookSettings();
            Assert.AreEqual(plain.fogColor, reference.fogColor);
            Assert.AreEqual(plain.fogDensity, reference.fogDensity);
        }

        [Test]
        public void ReferenceTheme_UsesThePlansPaleFog()
        {
            var s = LookThemes.Get("Reference");
            Assert.AreEqual(0.16f, s.fogColor.r, 1e-4f);
            Assert.AreEqual(0.17f, s.fogColor.g, 1e-4f);
            Assert.AreEqual(0.20f, s.fogColor.b, 1e-4f);
            Assert.AreEqual(0.06f, s.fogDensity, 1e-4f);
            Assert.AreEqual(-1.9f, s.exposure, 1e-4f);
        }

        [Test]
        public void Themes_AreIndependentCopies()
        {
            var a = LookThemes.Get("Reference");
            a.fogDensity = 0.2f;
            Assert.AreEqual(0.06f, LookThemes.Get("Reference").fogDensity, 1e-4f);
        }

        [Test]
        public void FogThickness_MatchesThePlan()
        {
            // Density 0.05 -> about 17 m (0.85 / density), clamped to at least 1 m.
            Assert.AreEqual(17f, LookController.MeanFreePath(0.05f), 1e-3f);
            Assert.AreEqual(1f, LookController.MeanFreePath(10f));
        }

        [Test]
        public void ExposureMultiplier_IsHdrpsFormula()
        {
            Assert.AreEqual(1f / 1.2f, LookController.ExposureMultiplier(0f), 1e-5f);
            Assert.Greater(LookController.ExposureMultiplier(-1.9f), LookController.ExposureMultiplier(0f));
        }
    }

    public class MistNoiseTests
    {
        [Test]
        public void Density_TilesSeamlesslyAcross()
        {
            for (float v = 0.05f; v < 1f; v += 0.3f)
            for (float w = 0f; w < 1f; w += 0.17f)
            {
                Assert.AreEqual(MistNoise.Density(0f, v, w, 0.65f, 1), MistNoise.Density(1f, v, w, 0.65f, 1), 1e-4f);
                Assert.AreEqual(MistNoise.Density(w, v, 0f, 0.65f, 1), MistNoise.Density(w, v, 1f, 0.65f, 1), 1e-4f);
            }
        }

        [Test]
        public void Density_IsThickestLow_AndGoneAtTheTop()
        {
            float low = 0f, high = 0f;
            for (float u = 0f; u < 1f; u += 0.05f)
            {
                low += MistNoise.Density(u, 0.05f, 0.3f, 0f, 1);
                high += MistNoise.Density(u, 0.7f, 0.3f, 0f, 1);
                Assert.AreEqual(0f, MistNoise.Density(u, 1f, 0.3f, 0.65f, 1), 1e-5f);
            }
            Assert.Greater(low, high * 2f);
        }

        [Test]
        public void NoPatchiness_IsEven()
        {
            float a = MistNoise.Density(0.1f, 0.2f, 0.3f, 0f, 1);
            float b = MistNoise.Density(0.7f, 0.2f, 0.9f, 0f, 1);
            Assert.AreEqual(a, b, 1e-5f);
        }
    }

    public class FlashlightCookieTests
    {
        [Test]
        public void HotCentre_DarkOutside()
        {
            float centre = FlashlightCookie.Value(0f, 0f, 0f);
            float middle = FlashlightCookie.Value(0.5f, 0f, 0f);
            Assert.Greater(centre, 0.8f);
            Assert.Greater(centre, middle);
            Assert.AreEqual(0f, FlashlightCookie.Value(1f, 0.1f, 0f));
        }

        [Test]
        public void Feather_SoftensTheEdge()
        {
            Assert.Greater(FlashlightCookie.Value(0.8f, 0f, 0f), FlashlightCookie.Value(0.8f, 0f, 1f));
        }
    }

    public class SettingsDrawerTests
    {
        [TestCase("mistPatchSize", "Mist patch size")]
        [TestCase("exposure", "Exposure")]
        public void Nicify(string field, string expected) => Assert.AreEqual(expected, SettingsDrawer.Nicify(field));
    }
}
