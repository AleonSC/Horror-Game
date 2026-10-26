using System;

namespace Game.Audio
{
    /// <summary>
    /// The blind one's theme, from the HDRP build (its BlindEntityTheme), made here at this build's sample rate:
    /// scored to its entrance, then carrying on under the hunt.
    ///
    ///   0.0s   a sub-bass drone on low D creeps in under a rising, whining cluster as the ground rumbles
    ///   3.5s   it breaks out of the earth: a huge drum hit, and the drone swells
    ///   7.6s   the roar: another hit, and a blaring, dissonant brass chord (D, Eb, A, G#) that opens up and dies away
    ///   13.1s  it stamps twice: two hits
    ///   15s    the hunt: a limping drum rhythm like its gait, a low driving cello ostinato, a thin tremolo of
    ///          high minor seconds, and its clicking, all building
    ///
    /// Made once (80 s) and cut in two for the music manager: <see cref="Intro"/>, the first 67.8 s (the entrance
    /// and the building hunt), and <see cref="Loop"/>, the last five bars of the hunt at its fullest (55.8-67.8 s),
    /// which carries on seamlessly from the intro's end and loops on itself. Plain C#, so it can be made off the
    /// main thread.
    /// </summary>
    public static class BlindTheme
    {
        static int Rate => MusicSynth.SampleRate;
        const float TwoPi = MathF.PI * 2f;
        const float Length = 80f;
        const float LoopStart = 55.8f, LoopEnd = 67.8f; // (bar lines: 15 s + 17 and 22 bars of 2.4 s)

        static float[] whole;
        static readonly object gate = new object();

        static float[] Whole()
        {
            lock (gate) return whole ??= Synthesize();
        }

        /// <summary>The entrance and the building hunt (67.8 s), straight into <see cref="Loop"/>.</summary>
        public static float[] Intro()
        {
            var s = Whole();
            int n = Math.Min(s.Length, (int)(LoopEnd * Rate));
            var o = new float[n];
            Array.Copy(s, o, n);
            for (int i = 0; i < n; i++) o[i] *= Gain;
            return o;
        }

        /// <summary>
        /// The hunt at its fullest, five bars (12 s), looping: it carries on where the intro stops, and its start is
        /// faded in over what comes after its end, so the loop has no seam.
        /// </summary>
        public static float[] Loop()
        {
            var s = Whole();
            int a = (int)(LoopStart * Rate), n = (int)((LoopEnd - LoopStart) * Rate), fade = Rate / 4;
            var o = new float[n];
            for (int i = 0; i < n; i++) o[i] = s[a + i] * Gain;
            for (int i = 0; i < fade && a + n + i < s.Length; i++)
            {
                float k = i / (float)fade;
                o[i] = (s[a + i] * k + s[a + n + i] * (1f - k)) * Gain;
            }
            return o;
        }

        const float Gain = 0.85f; // (beside the other music)

        static float Clamp01(float x) => x < 0f ? 0f : x > 1f ? 1f : x;
        static float InverseLerp(float a, float b, float x) => Math.Abs(b - a) < 1e-9f ? 0f : Clamp01((x - a) / (b - a));
        static float SmoothStep01(float x) => x * x * (3f - 2f * x);
        static int FloorToInt(float x) => (int)MathF.Floor(x);

    static float Note(float semitonesFromA4) => 440f * MathF.Pow(2f, semitonesFromA4 / 12f);
    static float Smooth(float a, float b, float x) => SmoothStep01(InverseLerp(a, b, x));
    static float Coef(float hz) => 1f - MathF.Exp(-TwoPi * hz / Rate);
    static float Wrap(float phase) => phase >= TwoPi ? phase - TwoPi : phase;

    static float[] Synthesize()
    {
        int n = (int)(Length * Rate);
        var mix = new float[n];
        var rng = new System.Random(1337);
        float White() => (float)(rng.NextDouble() * 2.0 - 1.0);

        const float Erupt = 3.5f, Roar = 7.6f, RoarEnd = 12.6f, Hunt = 15f, HuntEnd = 70f;

        // ---- Hits: a huge, low drum: a pitch-dropping body, a thud of noise, and a long sub tail.
        void Hit(float at, float gain)
        {
            int i0 = (int)(at * Rate);
            int len = (int)(2.5f * Rate);
            float phase = 0f, lp = 0f;
            for (int i = 0; i < len && i0 + i < n; i++)
            {
                float t = i / (float)Rate;
                float hz = 32f + 48f * MathF.Exp(-t * 9f);
                phase += TwoPi * hz / Rate;
                float body = MathF.Sin(phase) * MathF.Exp(-t * 2.2f);
                lp += (White() - lp) * 0.08f;
                float thud = lp * MathF.Exp(-t * 28f) * 2.5f;
                mix[i0 + i] += (body + thud) * gain * 0.55f;
            }
        }
        Hit(Erupt, 1.3f);
        Hit(Roar, 1.5f);
        Hit(RoarEnd + 0.5f, 1.1f);
        Hit(RoarEnd + 1.1f, 1.1f);

        // The hunt's drums limp like its gait: a heavy beat, a stumble, a beat. Building as it goes.
        const float Bar = 2.4f;
        for (float bar = Hunt; bar < HuntEnd; bar += Bar)
        {
            float build = Smooth(Hunt, HuntEnd - 15f, bar);
            Hit(bar, 0.75f + 0.35f * build);
            Hit(bar + 0.9f, 0.3f + 0.25f * build);
            Hit(bar + 1.2f, 0.55f + 0.3f * build);
            if (build > 0.5f) Hit(bar + 1.95f, 0.25f * build); // extra pushes late on
        }

        // ---- Per-sample voices.
        float dPhase = 0f, d2Phase = 0f, aPhase = 0f, risePhase = 0f;
        float droneLp = 0f, brassLp = 0f, celloLp = 0f, riseLp = 0f;
        float[] brassPhase = new float[10];
        float[] brassHz = { Note(-31), Note(-30), Note(-24), Note(-19), Note(-13) }; // D2, Eb2, A2, D3, G#3
        float tremPhaseA = 0f, tremPhaseB = 0f, celloPhase = 0f;
        // The cello ostinato, an eighth note each: D D D Eb D D C Db (semitones from A4).
        float[] cello = { -31, -31, -31, -30, -31, -31, -33, -32 };
        const float Eighth = Bar / 8f;

        for (int i = 0; i < n; i++)
        {
            float t = i / (float)Rate;
            float end = 1f - Smooth(HuntEnd - 2f, Length - 1f, t);

            // Drone: low D, slowly beating, with a faint fifth. Swells at each beat of the entrance.
            float droneEnv = Smooth(0f, Erupt, t) * 0.5f + Smooth(Erupt, Erupt + 1f, t) * 0.2f + Smooth(Roar - 0.5f, Roar, t) * 0.3f
                             - Smooth(RoarEnd, Hunt, t) * 0.35f;
            dPhase = Wrap(dPhase + TwoPi * Note(-43) / Rate);            // D1
            d2Phase = Wrap(d2Phase + TwoPi * (Note(-43) + 0.31f) / Rate); // beating against it
            aPhase = Wrap(aPhase + TwoPi * Note(-36) / Rate);            // A1
            float saw = 2f * ((dPhase / TwoPi) % 1f) - 1f;
            droneLp += (saw - droneLp) * Coef(120f);
            float drone = (MathF.Sin(dPhase) + MathF.Sin(d2Phase)) * 0.35f + droneLp * 0.4f + MathF.Sin(aPhase) * 0.12f;
            float sample = drone * droneEnv * 0.8f;

            // The omen: a whining cluster rising a minor sixth, cut off dead as it breaks the ground.
            if (t < Erupt)
            {
                float u = t / Erupt;
                float hz = Note(-19) * MathF.Pow(2f, 8f * u * u / 12f);
                risePhase += TwoPi * hz / Rate;
                riseLp += (White() - riseLp) * Coef(900f + 2500f * u);
                float rise = MathF.Sin(risePhase) * 0.5f + MathF.Sin(risePhase * 1.059f) * 0.4f + riseLp * 0.3f;
                sample += rise * u * u * 0.45f;
            }

            // The roar: a blaring, dissonant brass chord, its filter flaring open then closing as it dies.
            if (t >= Roar && t < RoarEnd + 1f)
            {
                float u = t - Roar;
                float env = Clamp01(u / 0.12f) * (1f - Smooth(RoarEnd - 2.5f, RoarEnd + 1f, t));
                float brass = 0f;
                for (int v = 0; v < brassHz.Length; v++)
                {
                    for (int d = 0; d < 2; d++)
                    {
                        int k = v * 2 + d;
                        brassPhase[k] = Wrap(brassPhase[k] + TwoPi * brassHz[v] * (d == 0 ? 1f : 1.006f) / Rate);
                        brass += 2f * ((brassPhase[k] / TwoPi) % 1f) - 1f;
                    }
                }
                float cutoff = 250f + 2200f * MathF.Exp(-u * 0.8f) * Clamp01(u / 0.3f);
                brassLp += (brass * 0.12f - brassLp) * Coef(cutoff);
                sample += brassLp * env * 1.1f;
            }

            // The hunt.
            if (t >= Hunt - 0.5f)
            {
                float build = Smooth(Hunt, HuntEnd - 10f, t);
                float inHunt = Smooth(Hunt - 0.5f, Hunt + 2f, t) * end;

                // Cello ostinato: short, driving eighths, the filter opening as it builds.
                float sinceHunt = t - Hunt;
                int eighth = FloorToInt(MathF.Max(sinceHunt, 0f) / Eighth);
                float inNote = MathF.Max(sinceHunt, 0f) - eighth * Eighth;
                float hz = Note(cello[eighth % cello.Length]);
                celloPhase = Wrap(celloPhase + TwoPi * hz / Rate);
                float cs = 2f * ((celloPhase / TwoPi) % 1f) - 1f;
                celloLp += (cs - celloLp) * Coef(300f + 900f * build);
                float bow = Clamp01(inNote / 0.015f) * MathF.Exp(-inNote * 6f);
                sample += celloLp * bow * (0.35f + 0.25f * build) * inHunt;

                // Tremolo: high minor seconds, D and Eb, shivering, in long swells.
                tremPhaseA = Wrap(tremPhaseA + TwoPi * Note(5) / Rate);  // D5
                tremPhaseB = Wrap(tremPhaseB + TwoPi * Note(6) / Rate);  // Eb5
                float shiver = 0.6f + 0.4f * MathF.Sin(TwoPi * 9f * t);
                float swell = 0.5f - 0.5f * MathF.Cos(TwoPi * t / (Bar * 4f));
                sample += (MathF.Sin(tremPhaseA) + MathF.Sin(tremPhaseB)) * shiver * swell * (0.04f + 0.06f * build) * inHunt;
            }

            mix[i] += sample;
        }

        // Its clicking, scattered through the hunt: little bursts of dry ticks.
        for (float at = Hunt + 1.3f; at < HuntEnd; at += 1.6f + (float)rng.NextDouble() * 3f)
        {
            int clicks = 2 + rng.Next(3);
            for (int c = 0; c < clicks; c++)
            {
                int i0 = (int)((at + c * (0.09f + (float)rng.NextDouble() * 0.06f)) * Rate);
                float hz = 2200f + (float)rng.NextDouble() * 900f;
                for (int i = 0; i < (int)(0.02f * Rate) && i0 + i < n; i++)
                {
                    float u = i / (float)Rate;
                    mix[i0 + i] += MathF.Sin(TwoPi * hz * u) * MathF.Exp(-u * 400f) * 0.12f;
                }
            }
        }

        // A big dark room round it all: a simple reverb (four combs into two all-passes), mixed in.
        var wet = Reverb(mix);
        float peak = 0.0001f;
        for (int i = 0; i < n; i++)
        {
            float x = mix[i] * 0.8f + wet[i] * 0.35f;
            x = MathF.Tanh(x * 1.2f); // a little warmth on the peaks
            mix[i] = x;
            peak = MathF.Max(peak, MathF.Abs(x));
        }
        float gain = 0.9f / peak;
        for (int i = 0; i < n; i++) mix[i] *= gain;
        // Fade the very ends so nothing clicks.
        for (int i = 0; i < (int)(0.05f * Rate); i++) { mix[i] *= i / (0.05f * Rate); mix[n - 1 - i] *= i / (0.05f * Rate); }
        return mix;
    }

    static float[] Reverb(float[] dry)
    {
        int n = dry.Length;
        var wet = new float[n];
        int[] combs = { 1557, 1617, 1491, 1422 };
        foreach (int delay in combs)
        {
            int d = Math.Max(1, (int)(delay * 2 * Rate / 44100f)); // a bigger space (the delays are for 44.1 kHz)
            var buf = new float[d];
            int idx = 0;
            float lp = 0f;
            for (int i = 0; i < n; i++)
            {
                float y = buf[idx];
                lp += (y - lp) * 0.3f; // damping: the highs die first
                buf[idx] = dry[i] + lp * 0.82f;
                idx = (idx + 1) % d;
                wet[i] += y * 0.25f;
            }
        }
        foreach (int delay0 in new[] { 556, 441 })
        {
            int delay = Math.Max(1, (int)(delay0 * Rate / 44100f));
            var buf = new float[delay];
            int idx = 0;
            for (int i = 0; i < n; i++)
            {
                float b = buf[idx];
                float y = -wet[i] + b;
                buf[idx] = wet[i] + b * 0.5f;
                idx = (idx + 1) % delay;
                wet[i] = y;
            }
        }
        return wet;
    }
    }
}
