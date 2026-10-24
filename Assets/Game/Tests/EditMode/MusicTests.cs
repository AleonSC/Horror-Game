using System;
using Game.Audio;
using NUnit.Framework;

namespace Game.Tests
{
    public class MusicTests
    {
        static void Sound(float[] s, float seconds, string name)
        {
            Assert.AreEqual(seconds, s.Length / (float)MusicSynth.SampleRate, 0.3f, $"{name} is the wrong length");
            float peak = 0f;
            foreach (var x in s)
            {
                Assert.IsFalse(float.IsNaN(x) || float.IsInfinity(x), $"{name} has a broken sample");
                peak = Math.Max(peak, Math.Abs(x));
            }
            Assert.Greater(peak, 0.2f, $"{name} is silent");
            Assert.LessOrEqual(peak, 1f, $"{name} clips");
        }

        [Test]
        public void MoodLoops_AreTheRightLength_AndSound()
        {
            Sound(MusicSynth.Tension(1), 15.75f, "Tension");
            Sound(MusicSynth.Danger(2), 7.75f, "Danger");
            Sound(MusicSynth.Watcher(3), 15.75f, "Watcher");
        }

        [Test]
        public void Themes_HitTheirMarks()
        {
            Sound(MusicSynth.BlindIntro(4), 16f, "Blind intro");
            Sound(MusicSynth.GiantIntro(6), 8f, "Giant intro");
            Sound(MusicSynth.FlierIntro(8), 15f, "Flier intro");
            Sound(MusicSynth.BlindLoop(5), 7.75f, "Blind loop");
            Sound(MusicSynth.FlierLoop(9), 3.75f, "Flier loop");

            // The blind one's drum hit lands at 3.5 s: much louder just after than just before.
            var blind = MusicSynth.BlindIntro(4);
            Assert.Greater(Loudness(blind, 3.52f), Loudness(blind, 3.4f) * 2f);
        }

        [Test]
        public void Notes_AreInTune()
        {
            Assert.AreEqual(440f, MusicSynth.Note("A4"), 0.01f);
            Assert.AreEqual(146.83f, MusicSynth.Note("D3"), 0.05f);
            Assert.AreEqual(155.56f, MusicSynth.Note("Eb3"), 0.05f);
            Assert.AreEqual(207.65f, MusicSynth.Note("G#3"), 0.05f);
        }

        static float Loudness(float[] s, float at)
        {
            int from = (int)(at * MusicSynth.SampleRate), n = MusicSynth.SampleRate / 20;
            float sum = 0f;
            for (int i = from; i < from + n; i++) sum += s[i] * s[i];
            return sum / n;
        }
    }
}
