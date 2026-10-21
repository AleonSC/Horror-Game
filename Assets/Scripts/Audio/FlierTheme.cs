using UnityEngine;

/// <summary>
/// The flier's theme (the 7th lamp's boss): a procedurally synthesized music cue, scored to its entrance
/// (FlyingEntity) and then carrying on under its search for about a minute and a half.
///
///   0.0s   a cold, wordless choir drifting high above, a wind rising, a deep pulse like a heartbeat
///          speeding up as it comes down out of the sky
///   6.85s  it passes right overhead: a reversed swell sucks in, a huge hit, and a howling glissando falls
///   10.8s  it screams as it climbs: another hit and a brass stab (E, G, C over a low E)
///   12s    its eye opens: a high, cold bell, and the search begins: a restless string ostinato in E phrygian
///          (6/8), timpani on the bars, the choir moving through Em, F, Dm, E, and the whoosh of great wings
///          every two bars, building, until it falls away at the end
///
/// Generated in the background when the game starts (FlyingEntitySpawner calls Prepare); MusicCue plays it.
/// </summary>
public static class FlierTheme
{
    const int Rate = 44100;
    const float TwoPi = Mathf.PI * 2f;
    const float Length = 100f;

    static MusicCue Cue => MusicCue.Get("Flier Theme", Synthesize, 0.85f);

    public static void Prepare() => Cue.Prepare();
    public static void Play() => Cue.Play();

    /// <summary>Fades the theme out (the flier is gone).</summary>
    public static void FadeOut()
    {
        foreach (var cue in Object.FindObjectsByType<MusicCue>())
            if (cue.name == "Flier Theme") cue.FadeOut();
    }

    static float Note(float semitonesFromA4) => 440f * Mathf.Pow(2f, semitonesFromA4 / 12f);
    static float Smooth(float a, float b, float x) => Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(a, b, x));
    static float Coef(float hz) => 1f - Mathf.Exp(-TwoPi * hz / Rate);
    static float Wrap(float phase) => phase >= TwoPi ? phase - TwoPi : phase;

    static float[] Synthesize()
    {
        int n = (int)(Length * Rate);
        var mix = new float[n];
        var rng = new System.Random(4242);
        float White() => (float)(rng.NextDouble() * 2.0 - 1.0);

        const float Overhead = 6.85f, Climb = 10.8f, Eye = 12f, SearchEnd = 90f;
        const float Bar = 1.8f; // 6/8 at 100 dotted-crotchets a minute

        // ---- A hit: a deep drum, pitch falling, with a thud of noise.
        void Hit(float at, float gain, float pitch)
        {
            int i0 = (int)(at * Rate), len = (int)(2.2f * Rate);
            float phase = 0f, lp = 0f;
            for (int i = 0; i < len && i0 + i < n; i++)
            {
                float t = i / (float)Rate;
                phase += TwoPi * (pitch + pitch * 1.5f * Mathf.Exp(-t * 10f)) / Rate;
                lp += (White() - lp) * 0.1f;
                mix[i0 + i] += (Mathf.Sin(phase) * Mathf.Exp(-t * 2.4f) + lp * Mathf.Exp(-t * 30f) * 2f) * gain * 0.5f;
            }
        }

        // ---- A held voice: detuned, vibrato, through a soft vocal-like filter ("ooh" low, "aah" bright).
        void Voice(float from, float to, float semis, float gain, float bright, float attack = 1.5f, float release = 2f)
        {
            int i0 = (int)(from * Rate), i1 = Mathf.Min(n, (int)((to + release) * Rate));
            float hz = Note(semis);
            float p1 = 0f, p2 = 0f, p3 = 0f, lp = 0f, lp2 = 0f;
            float vibPhase = (float)rng.NextDouble() * 6f;
            for (int i = i0; i < i1; i++)
            {
                float t = i / (float)Rate;
                float env = Smooth(from, from + attack, t) * (1f - Smooth(to, to + release, t));
                float vib = 1f + 0.006f * Mathf.Sin(TwoPi * 5.2f * t + vibPhase);
                p1 = Wrap(p1 + TwoPi * hz * vib / Rate);
                p2 = Wrap(p2 + TwoPi * hz * 1.004f * vib / Rate);
                p3 = Wrap(p3 + TwoPi * hz * 0.996f * vib / Rate);
                float saw = (p1 + p2 + p3) / TwoPi - 1.5f; // three detuned saws
                lp += (saw - lp) * Coef(500f + 1600f * bright);
                lp2 += (lp - lp2) * Coef(700f + 1800f * bright);
                mix[i] += lp2 * env * gain;
            }
        }

        // ---------------- The entrance.
        // The choir, high and cold, drifting in as it comes (E minor, high).
        Voice(0f, Eye, 7f, 0.07f, 0.2f, 3f, 3f);    // E5
        Voice(0.5f, Eye, 10f, 0.05f, 0.2f, 3f, 3f); // G5
        Voice(1f, Eye, 14f, 0.04f, 0.3f, 3f, 3f);   // B5
        // The pulse: a heartbeat, quickening as it dives.
        for (float t = 0.6f; t < Overhead - 0.3f;)
        {
            float u = t / Overhead;
            Hit(t, 0.35f + 0.4f * u, 45f);
            Hit(t + 0.16f, 0.2f + 0.25f * u, 45f);
            t += Mathf.Lerp(1.1f, 0.38f, u);
        }
        // Wind rising, then a reversed swell sucking in to the moment it's overhead.
        {
            float lp = 0f;
            for (int i = 0; i < (int)(Overhead * Rate); i++)
            {
                float t = i / (float)Rate;
                lp += (White() - lp) * Coef(300f + 2500f * Smooth(0f, Overhead, t));
                float swell = Mathf.Pow(Smooth(0f, Overhead, t), 3f);
                mix[i] += lp * (0.08f + 0.6f * swell);
            }
        }
        Hit(Overhead, 1.6f, 34f);
        // The howl: a falling glissando, an octave and a half, out of the overhead hit.
        {
            float phase = 0f, lp = 0f;
            for (int i = (int)(Overhead * Rate); i < (int)((Overhead + 3.5f) * Rate); i++)
            {
                float u = (i / (float)Rate - Overhead) / 3.5f;
                float hz = Note(19f - 18f * u * u) * (1f + 0.01f * Mathf.Sin(TwoPi * 7f * u * 3.5f));
                phase = Wrap(phase + TwoPi * hz / Rate);
                float saw = phase / Mathf.PI - 1f;
                lp += (saw - lp) * Coef(2500f - 1800f * u);
                mix[i] += lp * Mathf.Clamp01(u * 20f) * (1f - u) * 0.28f;
            }
        }
        Hit(Climb, 1.3f, 38f);
        // The stab: E, G, C over a low E, blaring and dying away.
        foreach (float semis in new[] { -29f, -17f, -14f, -9f })
            Voice(Climb, Climb + 0.6f, semis, 0.13f, 0.9f, 0.04f, 1.6f);

        // The eye opens: a high bell, bright and cold.
        {
            int i0 = (int)(Eye * Rate);
            for (int i = 0; i < (int)(4f * Rate) && i0 + i < n; i++)
            {
                float t = i / (float)Rate;
                float bell = Mathf.Sin(TwoPi * Note(19f) * t) + 0.5f * Mathf.Sin(TwoPi * Note(19f) * 2.76f * t) + 0.25f * Mathf.Sin(TwoPi * Note(19f) * 5.4f * t);
                mix[i0 + i] += bell * Mathf.Exp(-t * 1.3f) * 0.12f;
            }
        }

        // ---------------- The search.
        // Timpani on each bar (and the half bar, later), building.
        for (float bar = Eye + 1f; bar < SearchEnd; bar += Bar)
        {
            float build = Smooth(Eye, SearchEnd - 15f, bar);
            Hit(bar, 0.45f + 0.4f * build, 52f);
            if (build > 0.4f) Hit(bar + Bar * 0.5f, 0.25f * build, 52f);
        }
        // The choir through the chords, a bar each pair: Em, F, Dm, E.
        int[][] chords = { new[] { -5, -2, 2 }, new[] { -4, 0, 3 }, new[] { -7, -4, 0 }, new[] { -5, -1, 2 } };
        int chord = 0;
        for (float at = Eye + 1f; at < SearchEnd; at += Bar * 2f, chord++)
        {
            float build = Smooth(Eye, SearchEnd - 15f, at);
            foreach (int semis in chords[chord % chords.Length])
                Voice(at, at + Bar * 2f - 0.3f, semis, 0.035f + 0.03f * build, 0.25f + 0.4f * build, 1f, 0.8f);
        }
        // Wing beats: a great whoosh every two bars.
        for (float at = Eye + 2f; at < SearchEnd; at += Bar * 2f)
        {
            int i0 = (int)(at * Rate);
            float lp = 0f, lp2 = 0f;
            for (int i = 0; i < (int)(1.2f * Rate) && i0 + i < n; i++)
            {
                float t = i / (float)Rate;
                lp += (White() - lp) * Coef(400f);
                lp2 += (lp - lp2) * Coef(900f);
                mix[i0 + i] += lp2 * Mathf.Sin(Mathf.PI * t / 1.2f) * 0.35f;
            }
        }

        // Per-sample: the low string ostinato, staccato eighths in E phrygian: E E F E G F E D.
        int[] ostinato = { -29, -29, -28, -29, -26, -28, -29, -31 };
        float eighth = Bar / 6f, sp = 0f, slp = 0f;
        for (int i = (int)((Eye + 1f) * Rate); i < n; i++)
        {
            float t = i / (float)Rate;
            float since = t - (Eye + 1f);
            int k = Mathf.FloorToInt(since / eighth);
            float inNote = since - k * eighth;
            float build = Smooth(Eye, SearchEnd - 15f, t);
            float end = 1f - Smooth(SearchEnd - 3f, Length - 1f, t);
            float hz = Note(ostinato[k % ostinato.Length]);
            sp = Wrap(sp + TwoPi * hz / Rate);
            float saw = sp / Mathf.PI - 1f;
            slp += (saw - slp) * Coef(350f + 900f * build);
            float bow = Mathf.Clamp01(inNote / 0.01f) * Mathf.Exp(-inNote * 9f);
            mix[i] += slp * bow * (0.3f + 0.2f * build) * end * Smooth(Eye + 1f, Eye + 3f, t);
        }

        // A big, cold hall round all of it.
        var wet = Reverb(mix);
        float peak = 0.0001f;
        for (int i = 0; i < n; i++)
        {
            float x = (float)System.Math.Tanh((mix[i] * 0.8f + wet[i] * 0.4f) * 1.2f);
            mix[i] = x;
            peak = Mathf.Max(peak, Mathf.Abs(x));
        }
        float gain = 0.9f / peak;
        for (int i = 0; i < n; i++) mix[i] *= gain;
        int edge = (int)(0.05f * Rate);
        for (int i = 0; i < edge; i++) { mix[i] *= i / (float)edge; mix[n - 1 - i] *= i / (float)edge; }
        return mix;
    }

    static float[] Reverb(float[] dry)
    {
        int n = dry.Length;
        var wet = new float[n];
        foreach (int delay in new[] { 1687, 1601, 2053, 2251 })
        {
            int d = delay * 2;
            var buf = new float[d];
            int idx = 0;
            float lp = 0f;
            for (int i = 0; i < n; i++)
            {
                float y = buf[idx];
                lp += (y - lp) * 0.35f;
                buf[idx] = dry[i] + lp * 0.84f;
                idx = (idx + 1) % d;
                wet[i] += y * 0.25f;
            }
        }
        foreach (int delay in new[] { 556, 441 })
        {
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
