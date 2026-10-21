using UnityEngine;

/// <summary>
/// The giant's theme (the 5th lamp's boss): a procedurally synthesized music cue that starts the instant it
/// hits the ground (WatcherEvent) and carries on under its walk for about a minute and a half.
///
///   0.0s   the landing: an enormous hit, the ground dropping out under it, and a blaring low brass cluster
///          (D, E flat, A over a deep D) that sours as it dies, a long metallic shriek of chain fading high up
///   0.8s   a swell sucks in to the shockwave at 2.5s: another hit, and the whole hall rings
///   4s     its walk: slow, heavy drums on every beat like its steps (66 a minute), a low pulsing string
///          ostinato in D (D D E flat D, C sharp D...), and high violins trembling on a dissonant second,
///          creeping in; every eight bars a brass swell rises out of the dark; it all builds, then sinks away
///
/// Generated in the background when the game starts (WatcherEvent calls Prepare); MusicCue plays it.
/// </summary>
public static class GiantTheme
{
    const int Rate = 44100;
    const float TwoPi = Mathf.PI * 2f;
    const float Length = 95f;

    static MusicCue Cue => MusicCue.Get("Giant Theme", Synthesize, 0.9f);

    public static void Prepare() => Cue.Prepare();
    public static void Play() => Cue.Play();

    /// <summary>Fades the theme out (the run restarts).</summary>
    public static void FadeOut()
    {
        foreach (var cue in Object.FindObjectsByType<MusicCue>())
            if (cue.name == "Giant Theme") cue.FadeOut();
    }

    static float Note(float semitonesFromA4) => 440f * Mathf.Pow(2f, semitonesFromA4 / 12f);
    static float Smooth(float a, float b, float x) => Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(a, b, x));
    static float Coef(float hz) => 1f - Mathf.Exp(-TwoPi * hz / Rate);
    static float Wrap(float phase) => phase >= TwoPi ? phase - TwoPi : phase;

    static float[] Synthesize()
    {
        int n = (int)(Length * Rate);
        var mix = new float[n];
        var rng = new System.Random(5151);
        float White() => (float)(rng.NextDouble() * 2.0 - 1.0);

        const float Shock = 2.5f, Walk = 4f, WalkEnd = 86f;
        const float Beat = 60f / 66f, BarLen = Beat * 4f;

        // ---- A hit: a deep drum, pitch falling, with a thud of noise.
        void Hit(float at, float gain, float pitch, float decay = 2.2f)
        {
            int i0 = (int)(at * Rate), len = (int)(3f * Rate);
            float phase = 0f, lp = 0f;
            for (int i = 0; i < len && i0 + i < n; i++)
            {
                float t = i / (float)Rate;
                phase += TwoPi * (pitch + pitch * 1.8f * Mathf.Exp(-t * 9f)) / Rate;
                lp += (White() - lp) * 0.08f;
                mix[i0 + i] += (Mathf.Sin(phase) * Mathf.Exp(-t * decay) + lp * Mathf.Exp(-t * 25f) * 2.2f) * gain * 0.5f;
            }
        }

        // ---- A brass-ish voice: detuned saws through a filter that opens with the swell.
        void Brass(float from, float to, float semis, float gain, float bright, float attack, float release, float sourBend = 0f)
        {
            int i0 = (int)(from * Rate), i1 = Mathf.Min(n, (int)((to + release) * Rate));
            float p1 = 0f, p2 = 0f, lp = 0f, lp2 = 0f;
            for (int i = i0; i < i1; i++)
            {
                float t = i / (float)Rate;
                float env = Smooth(from, from + attack, t) * (1f - Smooth(to, to + release, t));
                float bend = Mathf.Pow(2f, sourBend * Smooth(from, to + release, t) / 12f); // sagging flat as it dies
                float hz = Note(semis) * bend;
                p1 = Wrap(p1 + TwoPi * hz / Rate);
                p2 = Wrap(p2 + TwoPi * hz * 1.005f / Rate);
                float saw = (p1 + p2) / TwoPi - 1f;
                lp += (saw - lp) * Coef(300f + 2200f * bright * env);
                lp2 += (lp - lp2) * Coef(500f + 2500f * bright * env);
                mix[i] += lp2 * env * gain;
            }
        }

        // ---------------- The landing.
        Hit(0f, 2f, 28f, 1.2f);
        Hit(0.02f, 1f, 55f);
        // The ground dropping out: a sub falling away.
        {
            float phase = 0f;
            for (int i = 0; i < (int)(4f * Rate); i++)
            {
                float t = i / (float)Rate;
                phase = Wrap(phase + TwoPi * (40f - 18f * Smooth(0f, 4f, t)) / Rate);
                mix[i] += Mathf.Sin(phase) * Mathf.Exp(-t * 0.7f) * 0.5f;
            }
        }
        // The brass cluster, blaring and souring.
        foreach (float semis in new[] { -43f, -31f, -30f, -24f, -19f })
            Brass(0f, 1.2f, semis, 0.11f, 1f, 0.03f, 2.5f, -0.6f);
        // The chain: a long metallic shriek, high, dying away (inharmonic partials, shimmering).
        {
            float[] partials = { 1f, 2.41f, 3.93f, 5.6f };
            for (int i = 0; i < (int)(5f * Rate); i++)
            {
                float t = i / (float)Rate;
                float s = 0f;
                for (int k = 0; k < partials.Length; k++)
                    s += Mathf.Sin(TwoPi * 880f * partials[k] * t * (1f - 0.02f * t)) / (k + 1);
                mix[i] += s * Mathf.Exp(-t * 0.9f) * (0.6f + 0.4f * Mathf.Sin(TwoPi * 7f * t)) * 0.05f;
            }
        }
        // A swell sucking in to the shockwave, then the hit.
        {
            float lp = 0f;
            for (int i = (int)(0.8f * Rate); i < (int)(Shock * Rate); i++)
            {
                float t = i / (float)Rate;
                lp += (White() - lp) * Coef(200f + 3000f * Smooth(0.8f, Shock, t));
                mix[i] += lp * Mathf.Pow(Smooth(0.8f, Shock, t), 3f) * 0.6f;
            }
        }
        Hit(Shock, 1.7f, 31f, 1.4f);
        foreach (float semis in new[] { -43f, -36f, -31f })
            Brass(Shock, Shock + 0.4f, semis, 0.08f, 0.8f, 0.03f, 2f);

        // ---------------- The walk.
        // Drums on every beat, like its steps, building; a heavier one on each bar.
        for (float at = Walk; at < WalkEnd; at += Beat)
        {
            float build = Smooth(Walk, WalkEnd - 20f, at);
            bool bar = Mathf.Repeat(at - Walk, BarLen) < 0.01f;
            Hit(at, (bar ? 0.75f : 0.4f) + 0.35f * build, bar ? 36f : 46f);
        }
        // Brass swells rising out of the dark every eight bars: Dm, then Bb over D, then Gm, then A.
        int[][] swells = { new[] { -31, -27, -24 }, new[] { -35, -31, -26 }, new[] { -38, -34, -31 }, new[] { -36, -32, -29 } };
        int which = 0;
        for (float at = Walk + BarLen * 4f; at < WalkEnd - BarLen * 2f; at += BarLen * 8f, which++)
        {
            float build = Smooth(Walk, WalkEnd - 20f, at);
            foreach (int semis in swells[which % swells.Length])
                Brass(at, at + BarLen * 3f, semis, 0.05f + 0.05f * build, 0.4f + 0.5f * build, BarLen * 2.5f, BarLen);
        }

        // Per-sample: the low string ostinato (sixteenths) and the high trembling violins.
        int[] ostinato = { -31, -31, -30, -31, -32, -31, -30, -28 }; // D D Eb D C# D Eb F
        float sixteenth = Beat / 4f, sp = 0f, slp = 0f, v1 = 0f, v2 = 0f, vlp = 0f;
        for (int i = (int)(Walk * Rate); i < n; i++)
        {
            float t = i / (float)Rate;
            float since = t - Walk;
            int k = Mathf.FloorToInt(since / sixteenth);
            float inNote = since - k * sixteenth;
            float build = Smooth(Walk, WalkEnd - 20f, t);
            float end = 1f - Smooth(WalkEnd - 4f, Length - 1f, t);
            float fadeIn = Smooth(Walk, Walk + BarLen * 2f, t);

            float hz = Note(ostinato[(k / 2) % ostinato.Length]);
            sp = Wrap(sp + TwoPi * hz / Rate);
            float saw = sp / Mathf.PI - 1f;
            slp += (saw - slp) * Coef(300f + 900f * build);
            float bow = Mathf.Clamp01(inNote / 0.008f) * Mathf.Exp(-inNote * 14f);
            mix[i] += slp * bow * (0.28f + 0.22f * build) * end * fadeIn;

            // The violins: A5 against B flat 5, trembling (fast tremolo), creeping in.
            v1 = Wrap(v1 + TwoPi * Note(12f) * (1f + 0.004f * Mathf.Sin(TwoPi * 5.5f * t)) / Rate);
            v2 = Wrap(v2 + TwoPi * Note(13f) * (1f + 0.004f * Mathf.Sin(TwoPi * 6.1f * t + 1f)) / Rate);
            float vs = (v1 + v2) / TwoPi - 1f;
            vlp += (vs - vlp) * Coef(2200f);
            float tremolo = 0.55f + 0.45f * Mathf.Sin(TwoPi * 11f * t);
            mix[i] += vlp * tremolo * Smooth(Walk + 8f, WalkEnd - 25f, t) * 0.07f * end;
        }

        // A huge dark hall round all of it.
        var wet = Reverb(mix);
        float peak = 0.0001f;
        for (int i = 0; i < n; i++)
        {
            float x = (float)System.Math.Tanh((mix[i] * 0.8f + wet[i] * 0.45f) * 1.2f);
            mix[i] = x;
            peak = Mathf.Max(peak, Mathf.Abs(x));
        }
        float gain = 0.9f / peak;
        for (int i = 0; i < n; i++) mix[i] *= gain;
        int edge = (int)(0.01f * Rate);
        for (int i = 0; i < edge; i++) mix[n - 1 - i] *= i / (float)edge;
        return mix;
    }

    static float[] Reverb(float[] dry)
    {
        int n = dry.Length;
        var wet = new float[n];
        foreach (int delay in new[] { 1931, 1789, 2287, 2539 })
        {
            int d = delay * 2;
            var buf = new float[d];
            int idx = 0;
            float lp = 0f;
            for (int i = 0; i < n; i++)
            {
                float y = buf[idx];
                lp += (y - lp) * 0.3f;
                buf[idx] = dry[i] + lp * 0.86f;
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
