using System;

namespace Game.Audio
{
    /// <summary>
    /// Builds the game's sounds from code (section 5.2): each generator returns mono samples at
    /// <see cref="SampleRate"/>. Plain C#, so it can run on a background thread and be tested. Every sound
    /// can be replaced with a recorded clip later.
    /// </summary>
    public static class SoundSynth
    {
        public const int SampleRate = 44100;

        /// <summary>A scour creeping closer: a dry, breathy rustle with a soft knock.</summary>
        public static float[] Creep(int seed)
        {
            var rng = new Random(seed);
            var s = Make(0.55f);
            float lp = 0f, bp = 0f;
            for (int i = 0; i < s.Length; i++)
            {
                float t = Time(i);
                float env = Env(t, 0.02f, 0.5f);
                float n = Noise(rng);
                lp += (n - lp) * 0.25f;
                bp += (n - lp - bp) * 0.4f;
                float knock = (float)Math.Sin(2 * Math.PI * 70 * t) * Decay(t, 18f) * 0.6f;
                s[i] = (bp * 0.7f * env + knock) * 0.8f;
            }
            return Normalize(s, 0.6f);
        }

        /// <summary>It materializes: a hiss that swells and cuts off, with a low tone under it.</summary>
        public static float[] Appear(int seed)
        {
            var rng = new Random(seed);
            var s = Make(1.3f);
            float lp = 0f;
            for (int i = 0; i < s.Length; i++)
            {
                float t = Time(i);
                float swell = (float)Math.Pow(t / 1.3f, 2.5f) * Env(t, 0f, 1.25f, 0.05f);
                lp += (Noise(rng) - lp) * (0.05f + 0.3f * t / 1.3f);
                float tone = (float)Math.Sin(2 * Math.PI * (55 + 30 * t) * t) * 0.5f;
                s[i] = (lp * 1.5f + tone) * swell;
            }
            return Normalize(s, 0.7f);
        }

        /// <summary>A deep pound: the beat while it advances on you in the dark.</summary>
        public static float[] Pound(int seed)
        {
            var s = Make(0.7f);
            for (int i = 0; i < s.Length; i++)
            {
                float t = Time(i);
                float f = 48f + 40f * Decay(t, 25f);
                s[i] = (float)Math.Sin(2 * Math.PI * f * t) * Decay(t, 6f) + (float)Math.Sin(2 * Math.PI * 96 * t) * Decay(t, 14f) * 0.3f;
            }
            return Normalize(s, 0.9f);
        }

        /// <summary>Facing it down: a rising, shuddering whine that builds to a snap (2.5 s).</summary>
        public static float[] Crescendo(int seed)
        {
            var rng = new Random(seed);
            var s = Make(2.5f);
            float lp = 0f;
            for (int i = 0; i < s.Length; i++)
            {
                float t = Time(i);
                float u = t / 2.5f;
                float f = 180f + 900f * u * u;
                float trem = 1f + 0.5f * (float)Math.Sin(2 * Math.PI * (6 + 30 * u) * t);
                lp += (Noise(rng) - lp) * 0.3f;
                s[i] = ((float)Math.Sin(2 * Math.PI * f * t) * 0.4f + lp * 0.5f) * u * u * trem;
            }
            return Normalize(s, 0.8f);
        }

        /// <summary>The portal: a deep tearing whoosh.</summary>
        public static float[] Portal(int seed)
        {
            var rng = new Random(seed);
            var s = Make(1.6f);
            float lp = 0f;
            for (int i = 0; i < s.Length; i++)
            {
                float t = Time(i);
                lp += (Noise(rng) - lp) * (0.02f + 0.1f * Decay(t, 2f));
                float rumble = (float)Math.Sin(2 * Math.PI * (40 - 15 * t / 1.6f) * t);
                s[i] = (lp * 3f + rumble * 0.6f) * Env(t, 0.15f, 1.6f, 0.9f);
            }
            return Normalize(s, 0.8f);
        }

        /// <summary>A shriek: harsh, rasping and falling (provoked, or as it takes you).</summary>
        public static float[] Shriek(int seed)
        {
            var rng = new Random(seed);
            var s = Make(1.1f);
            double phase = 0, mod = 0;
            for (int i = 0; i < s.Length; i++)
            {
                float t = Time(i);
                float f = 1400f - 700f * t + 80f * (float)Math.Sin(2 * Math.PI * 9 * t);
                mod += 2 * Math.PI * f * 1.48 / SampleRate;
                phase += 2 * Math.PI * f / SampleRate + Math.Sin(mod) * 2.2 / SampleRate * f;
                float rasp = Noise(rng) * 0.35f;
                s[i] = ((float)Math.Sin(phase) + rasp) * Env(t, 0.02f, 1.1f, 0.6f);
            }
            return Normalize(s, 0.85f);
        }

        /// <summary>A low, wavering moan.</summary>
        public static float[] Moan(int seed)
        {
            var rng = new Random(seed);
            var s = Make(2.2f);
            float lp = 0f;
            for (int i = 0; i < s.Length; i++)
            {
                float t = Time(i);
                float f = 85f + 12f * (float)Math.Sin(2 * Math.PI * 0.7 * t) - 20f * t / 2.2f;
                lp += (Noise(rng) - lp) * 0.08f;
                double p = 2 * Math.PI * f * t;
                s[i] = ((float)(Math.Sin(p) + 0.5 * Math.Sin(2 * p) + 0.25 * Math.Sin(3 * p)) * 0.5f + lp) * Env(t, 0.4f, 2.2f, 0.8f);
            }
            return Normalize(s, 0.7f);
        }

        /// <summary>One heartbeat: lub-dub.</summary>
        public static float[] Heartbeat(int seed)
        {
            var s = Make(0.6f);
            for (int i = 0; i < s.Length; i++)
            {
                float t = Time(i);
                float lub = (float)Math.Sin(2 * Math.PI * 55 * t) * Decay(t, 22f);
                float t2 = t - 0.22f;
                float dub = t2 > 0 ? (float)Math.Sin(2 * Math.PI * 65 * t2) * Decay(t2, 26f) * 0.7f : 0f;
                s[i] = lub + dub;
            }
            return Normalize(s, 0.9f);
        }

        /// <summary>A slow breath in and out, close behind your head.</summary>
        public static float[] Breath(int seed)
        {
            var rng = new Random(seed);
            var s = Make(3.2f);
            float lp = 0f, lp2 = 0f;
            for (int i = 0; i < s.Length; i++)
            {
                float t = Time(i);
                float inhale = Env(t, 0.5f, 1.3f, 0.4f);
                float exhale = t > 1.5f ? Env(t - 1.5f, 0.2f, 1.6f, 1.0f) : 0f;
                lp += (Noise(rng) - lp) * 0.12f;
                lp2 += (lp - lp2) * 0.3f;
                s[i] = (lp - lp2) * (inhale * 0.7f + exhale);
            }
            return Normalize(s, 0.6f);
        }

        /// <summary>A small mechanical click (flashlight switch, lamp crank).</summary>
        public static float[] Click(int seed)
        {
            var rng = new Random(seed);
            var s = Make(0.06f);
            for (int i = 0; i < s.Length; i++)
            {
                float t = Time(i);
                s[i] = (Noise(rng) * 0.6f + (float)Math.Sin(2 * Math.PI * 2400 * t) * 0.4f) * Decay(t, 120f);
            }
            return Normalize(s, 0.6f);
        }

        /// <summary>A lamp coming on: a heavy clunk and a warm hum swelling up.</summary>
        public static float[] LampOn(int seed)
        {
            var rng = new Random(seed);
            var s = Make(1.8f);
            for (int i = 0; i < s.Length; i++)
            {
                float t = Time(i);
                float clunk = ((float)Math.Sin(2 * Math.PI * 70 * t) + Noise(rng) * 0.4f) * Decay(t, 14f);
                float hum = ((float)Math.Sin(2 * Math.PI * 100 * t) + 0.4f * (float)Math.Sin(2 * Math.PI * 200 * t)) * Env(t, 0.4f, 1.8f, 0.6f) * 0.25f;
                s[i] = clunk + hum;
            }
            return Normalize(s, 0.8f);
        }

        // ------------------------------------------------------------------ helpers

        static float[] Make(float seconds) => new float[Math.Max(1, (int)(seconds * SampleRate))];
        static float Time(int i) => i / (float)SampleRate;
        static float Noise(Random rng) => (float)(rng.NextDouble() * 2.0 - 1.0);
        static float Decay(float t, float rate) => (float)Math.Exp(-t * rate);

        /// <summary>Attack, then hold, then a release over the last part (fractions in seconds).</summary>
        static float Env(float t, float attack, float length, float release = 0.2f)
        {
            if (t < 0f || t > length) return 0f;
            float a = attack > 0f ? Math.Min(1f, t / attack) : 1f;
            float r = release > 0f ? Math.Min(1f, (length - t) / release) : 1f;
            return a * r;
        }

        /// <summary>Scale so the loudest sample is <paramref name="peak"/>; NaNs become silence.</summary>
        public static float[] Normalize(float[] s, float peak)
        {
            float max = 0f;
            for (int i = 0; i < s.Length; i++)
            {
                if (float.IsNaN(s[i]) || float.IsInfinity(s[i])) s[i] = 0f;
                max = Math.Max(max, Math.Abs(s[i]));
            }
            if (max <= 1e-6f) return s;
            float k = peak / max;
            for (int i = 0; i < s.Length; i++) s[i] *= k;
            return s;
        }
    }
}
