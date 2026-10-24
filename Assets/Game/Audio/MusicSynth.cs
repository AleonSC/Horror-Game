using System;

namespace Game.Audio
{
    /// <summary>
    /// The music, made from code (section 5.3): a loop per mood (Tension, Danger, Watcher) and a theme for each
    /// big entrance, scored to it: an intro that follows the entrance beat for beat, then a loop that carries on
    /// while it hunts. Mono samples at <see cref="SampleRate"/>. Plain C#, so it runs on a background thread at
    /// startup. Your own tracks replace any of it (AudioConfig > Music).
    /// </summary>
    public static class MusicSynth
    {
        public const int SampleRate = 24000;

        // ------------------------------------------------------------------ mood loops (seamless)

        /// <summary>Something's near: a low drone with a slow, uneasy pulse (16 s).</summary>
        public static float[] Tension(int seed)
        {
            var rng = new Random(seed);
            var s = Make(16f);
            float lp = 0f;
            for (int i = 0; i < s.Length; i++)
            {
                float t = T(i);
                float drone = Saw(Note("D2"), t, 3) * 0.25f + Sine(Note("D2") * 1.004f, t) * 0.3f + Sine(Note("A2"), t) * 0.12f;
                float swell = 0.6f + 0.4f * (float)Math.Sin(2 * Math.PI * t / 16f);
                float beat = t % 2f;                                       // 30 bpm heartbeat pulse
                float pulse = Sine(42f, t) * (Decay(beat, 9f) + 0.7f * Decay(beat - 0.28f, 9f));
                lp += (Noise(rng) - lp) * 0.02f;
                s[i] = drone * swell + pulse * 0.6f + lp * 0.5f;
            }
            return Loopable(s, 0.5f);
        }

        /// <summary>Something's on you: driving low strings and a quick pulse (8 s).</summary>
        public static float[] Danger(int seed)
        {
            var rng = new Random(seed);
            var s = Make(8f);
            string[] line = { "D2", "D2", "Eb2", "D2", "D2", "D2", "A1", "Eb2" };
            for (int i = 0; i < s.Length; i++)
            {
                float t = T(i);
                float eighth = 0.25f;                                       // 120 bpm eighths
                int step = (int)(t / eighth) % line.Length;
                float local = t % eighth;
                float bass = Saw(Note(line[step]), t, 6) * Decay(local, 5f) * 0.5f;
                float trem = Saw(Note("D3"), t, 5) * 0.12f * (0.5f + 0.5f * (float)Math.Sin(2 * Math.PI * 8 * t)) + Saw(Note("Eb3"), t, 5) * 0.08f;
                float kick = (t % 0.5f) < 0.2f ? Sine(55f + 60f * Decay(t % 0.5f, 30f), t) * Decay(t % 0.5f, 14f) : 0f;
                float hat = Noise(rng) * Decay(t % 0.25f, 60f) * 0.08f;
                s[i] = bass + trem + kick * 0.8f + hat;
            }
            return Loopable(s, 0.6f);
        }

        /// <summary>The eye is open, the giant walks: a vast slow choir over a sub drone (16 s).</summary>
        public static float[] Watcher(int seed)
        {
            var s = Make(16f);
            for (int i = 0; i < s.Length; i++)
            {
                float t = T(i);
                float sub = Sine(Note("D1"), t) * 0.5f;
                float chord = t % 16f < 8f
                    ? Choir(t, Note("D3"), Note("A3"), Note("Eb4"))
                    : Choir(t, Note("D3"), Note("G#3"), Note("D4"));
                float fade = Math.Min(1f, Math.Min((t % 8f) / 2f, (8f - t % 8f) / 2f) + 0.3f);
                s[i] = sub + chord * 0.6f * fade;
            }
            return Loopable(s, 0.55f);
        }

        // ------------------------------------------------------------------ the blind one

        /// <summary>
        /// Its entrance: a sub-bass drone, a drum hit at 3.5 s as it breaks out of the ground, a roar with a
        /// dissonant brass chord (D, Eb, A, G#) at 7.6 s, two stamps at 13.1 s (16 s).
        /// </summary>
        public static float[] BlindIntro(int seed)
        {
            var rng = new Random(seed);
            var s = Make(16f);
            float lp = 0f;
            for (int i = 0; i < s.Length; i++)
            {
                float t = T(i);
                float drone = Sine(Note("D1"), t) * 0.5f * Env(t, 2.5f, 16f, 2f) + Saw(Note("D2"), t, 4) * 0.12f * Env(t, 4f, 16f, 2f);
                float hit = Drum(t - 3.5f, 48f, rng) * 1.2f;
                lp += (Noise(rng) - lp) * 0.08f;
                float roar = t > 7.6f ? lp * 2.2f * Env(t - 7.6f, 0.15f, 3.2f, 2f) : 0f;
                float chord = t > 7.6f ? Brass(t - 7.6f, 5.5f, Note("D3"), Note("Eb3"), Note("A3"), Note("G#4")) : 0f;
                float stamps = Drum(t - 13.1f, 40f, rng) + Drum(t - 13.55f, 40f, rng);
                s[i] = drone + hit + roar + chord * 0.7f + stamps;
            }
            return SoftEnd(Normalize(s, 0.8f), 0.5f);
        }

        /// <summary>Its hunt: a limping rhythm (a heavy beat, then a dragged one) under the drone (8 s).</summary>
        public static float[] BlindLoop(int seed)
        {
            var rng = new Random(seed);
            var s = Make(8f);
            for (int i = 0; i < s.Length; i++)
            {
                float t = T(i);
                float bar = t % 2f;
                float limp = Drum(bar, 44f, rng) + 0.55f * Drum(bar - 0.75f, 58f, rng);
                float drone = Sine(Note("D1"), t) * 0.4f + Saw(Note("Eb2"), t, 3) * 0.06f;
                float click = (t % 1f) > 0.5f && (t % 1f) < 0.52f ? Noise(rng) * 0.15f : 0f;
                s[i] = limp + drone + click;
            }
            return Loopable(s, 0.7f);
        }

        // ------------------------------------------------------------------ the giant

        /// <summary>
        /// Its landing: a huge hit, a low brass cluster (D, Eb, A over D), the chain's shriek, a swell to the
        /// shockwave at 2.5 s (8 s).
        /// </summary>
        public static float[] GiantIntro(int seed)
        {
            var rng = new Random(seed);
            var s = Make(8f);
            float lp = 0f;
            for (int i = 0; i < s.Length; i++)
            {
                float t = T(i);
                float hit = Drum(t, 30f, rng) * 1.6f;
                float cluster = Brass(t, 7.5f, Note("D2"), Note("Eb2"), Note("A2"), Note("D1"));
                float shriek = t > 0.3f && t < 2f ? (float)Math.Sin(2 * Math.PI * (2400f + 600f * (float)Math.Sin(t * 37f)) * t) * 0.08f * Env(t - 0.3f, 0.05f, 1.7f, 0.8f) : 0f;
                lp += (Noise(rng) - lp) * 0.05f;
                float swell = t < 2.5f ? lp * (float)Math.Pow(t / 2.5f, 3f) * 1.5f : 0f;
                float wave = Drum(t - 2.5f, 26f, rng) * 1.4f;
                s[i] = hit + cluster * 0.8f + shriek + swell + wave;
            }
            return SoftEnd(Normalize(s, 0.85f), 0.5f);
        }

        /// <summary>Its walk: a 66 bpm march of drums and pulsing strings (about 7.3 s: eight beats).</summary>
        public static float[] GiantLoop(int seed)
        {
            var rng = new Random(seed);
            float beat = 60f / 66f;
            var s = Make(beat * 8f);
            for (int i = 0; i < s.Length; i++)
            {
                float t = T(i);
                float b = t % beat;
                int n = (int)(t / beat);
                float drum = Drum(b, n % 4 == 0 ? 34f : 44f, rng) * (n % 4 == 0 ? 1.2f : 0.7f);
                float pulse = 0.5f + 0.5f * (float)Math.Cos(2 * Math.PI * t / beat);
                float strings = (Saw(Note("D3"), t, 6) + Saw(Note("A3"), t, 6) * 0.7f + Saw(Note(n % 8 < 4 ? "Eb4" : "D4"), t, 6) * 0.5f) * 0.12f * pulse;
                s[i] = drum + strings + Sine(Note("D1"), t) * 0.25f;
            }
            return Loopable(s, 0.7f);
        }

        // ------------------------------------------------------------------ the flier

        /// <summary>
        /// Its entrance: a cold high choir, a reversed swell into a hit as it passes overhead at 6.85 s, a brass
        /// stab as it climbs, a bell as its eye opens at 12 s (15 s).
        /// </summary>
        public static float[] FlierIntro(int seed)
        {
            var rng = new Random(seed);
            var s = Make(15f);
            float lp = 0f;
            for (int i = 0; i < s.Length; i++)
            {
                float t = T(i);
                float choir = Choir(t, Note("E5"), Note("F5"), Note("B5")) * 0.4f * Env(t, 2f, 15f, 3f);
                lp += (Noise(rng) - lp) * 0.1f;
                float reverse = t > 4.5f && t < 6.85f ? lp * (float)Math.Pow((t - 4.5f) / 2.35f, 4f) * 2f : 0f;
                float hit = Drum(t - 6.85f, 36f, rng) * 1.3f;
                float stab = t > 8.4f ? Brass(t - 8.4f, 1.2f, Note("E3"), Note("F3"), Note("B3"), Note("E2")) : 0f;
                float bell = Bell(t - 12f, Note("E5"));
                s[i] = choir + reverse + hit + stab * 0.8f + bell * 0.5f;
            }
            return SoftEnd(Normalize(s, 0.8f), 0.5f);
        }

        /// <summary>Its patrol: a 6/8 string ostinato in E phrygian (4 s: two bars).</summary>
        public static float[] FlierLoop(int seed)
        {
            string[] figure = { "E3", "F3", "G3", "F3", "E3", "B2", "E3", "F3", "A3", "G3", "F3", "D3" };
            float eighth = 4f / figure.Length;
            var s = Make(4f);
            for (int i = 0; i < s.Length; i++)
            {
                float t = T(i);
                int step = (int)(t / eighth) % figure.Length;
                float local = t % eighth;
                float note = Saw(Note(figure[step]), t, 6) * Decay(local, 4f) * 0.4f;
                float high = Choir(t, Note("E5"), Note("B5"), Note("F6")) * 0.12f;
                s[i] = note + high + Sine(Note("E1"), t) * 0.2f;
            }
            return Loopable(s, 0.6f);
        }

        // ------------------------------------------------------------------ instruments

        /// <summary>A note's frequency: "A4" = 440, "Eb3", "G#2".</summary>
        public static float Note(string name)
        {
            int[] steps = { 9, 11, 0, 2, 4, 5, 7 }; // A B C D E F G from C
            int semis = steps[name[0] - 'A'];
            int i = 1;
            if (name[i] == '#') { semis++; i++; }
            else if (name[i] == 'b') { semis--; i++; }
            int octave = name[i] - '0';
            int midi = 12 * (octave + 1) + semis;
            return 440f * (float)Math.Pow(2.0, (midi - 69) / 12.0);
        }

        static float Sine(float f, float t) => (float)Math.Sin(2 * Math.PI * f * t);

        // A soft saw from a few harmonics (no aliasing).
        static float Saw(float f, float t, int harmonics)
        {
            float v = 0f;
            for (int h = 1; h <= harmonics; h++) v += (float)Math.Sin(2 * Math.PI * f * h * t) / h;
            return v * 0.6f;
        }

        // Voices: slightly detuned sines with a slow vibrato, breathing in and out.
        static float Choir(float t, params float[] notes)
        {
            float v = 0f;
            for (int n = 0; n < notes.Length; n++)
            {
                float vib = 1f + 0.004f * (float)Math.Sin(2 * Math.PI * (4.8f + n * 0.3f) * t);
                v += Sine(notes[n] * vib, t) + 0.4f * Sine(notes[n] * 1.006f * vib, t) + 0.15f * Sine(notes[n] * 2f, t);
            }
            return v / notes.Length;
        }

        // A brass chord: a slow bite, then held and fading over `length` seconds.
        static float Brass(float t, float length, params float[] notes)
        {
            if (t < 0f || t > length) return 0f;
            float env = Math.Min(1f, t / 0.12f) * Math.Min(1f, (length - t) / (length * 0.6f));
            float bright = 3 + (int)(4f * Decay(t, 1.5f));
            float v = 0f;
            foreach (var f in notes) v += Saw(f, t, (int)bright + 2);
            return v / notes.Length * env;
        }

        // A big drum: a pitch drop and a burst of noise. t is time since the hit.
        static float Drum(float t, float pitch, Random rng)
        {
            if (t < 0f || t > 2.5f) return 0f;
            float f = pitch * (1f + 2.5f * Decay(t, 18f));
            return (float)Math.Sin(2 * Math.PI * f * t) * Decay(t, 3.2f) + Noise(rng) * Decay(t, 25f) * 0.3f;
        }

        // A bell: inharmonic partials ringing away.
        static float Bell(float t, float f)
        {
            if (t < 0f) return 0f;
            float[] ratios = { 1f, 2.76f, 5.4f, 8.93f };
            float v = 0f;
            for (int r = 0; r < ratios.Length; r++) v += Sine(f * ratios[r], t) * Decay(t, 0.6f + r * 0.9f) / (r + 1);
            return v;
        }

        // ------------------------------------------------------------------ helpers

        static float[] Make(float seconds) => new float[Math.Max(1, (int)(seconds * SampleRate))];
        static float T(int i) => i / (float)SampleRate;
        static float Noise(Random rng) => (float)(rng.NextDouble() * 2.0 - 1.0);
        static float Decay(float t, float rate) => t < 0f ? 0f : (float)Math.Exp(-t * rate);

        static float Env(float t, float attack, float length, float release)
        {
            if (t < 0f || t > length) return 0f;
            float a = attack > 0f ? Math.Min(1f, t / attack) : 1f;
            float r = release > 0f ? Math.Min(1f, (length - t) / release) : 1f;
            return a * r;
        }

        static float[] Normalize(float[] s, float peak) => SoundSynth.Normalize(s, peak);

        // Fade the last bit so an intro ends cleanly into its loop.
        static float[] SoftEnd(float[] s, float seconds)
        {
            int n = Math.Min(s.Length, (int)(seconds * SampleRate));
            for (int i = 0; i < n; i++) s[s.Length - 1 - i] *= i / (float)n;
            return s;
        }

        // Crossfade the end into the start, so it loops without a click.
        static float[] Loopable(float[] s, float peak)
        {
            int fade = Math.Min(s.Length / 4, SampleRate / 4);
            var looped = new float[s.Length - fade];
            Array.Copy(s, looped, looped.Length);
            for (int i = 0; i < fade; i++)
            {
                float k = i / (float)fade;
                looped[i] = s[i] * k + s[looped.Length + i] * (1f - k);
            }
            return Normalize(looped, peak);
        }
    }
}
