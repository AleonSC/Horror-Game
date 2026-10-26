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

        /// <summary>
        /// Facing it down (2.5 s): a low, hollow swell, like breath drawn through a throat the size of a room, rising
        /// slowly in pitch and fullness under a soft rushing of air. It builds without whining: no high tone.
        /// </summary>
        public static float[] Crescendo(int seed)
        {
            var rng = new Random(seed);
            var s = Make(2.5f);
            float lp = 0f, bp = 0f;
            double p1 = 0, p2 = 0, p3 = 0;
            for (int i = 0; i < s.Length; i++)
            {
                float t = Time(i);
                float u = t / 2.5f;
                float f = 62f + 50f * u * u;
                p1 += 2 * Math.PI * f / SampleRate;
                p2 += 2 * Math.PI * f * 1.007 / SampleRate;
                p3 += 2 * Math.PI * f * 1.5 / SampleRate;
                float tone = (float)(Math.Sin(p1) + 0.6 * Math.Sin(p2) + 0.25 * Math.Sin(p3) * u);
                float n = Noise(rng);
                lp += (n - lp) * (0.02f + 0.06f * u);
                bp += (n - lp - bp) * 0.05f;
                float air = lp * 1.6f + bp * 0.25f * u;
                float waver = 1f + 0.12f * (float)Math.Sin(2 * Math.PI * (3 + 4 * u) * t);
                float swell = u * u * (3f - 2f * u) * Env(t, 0.05f, 2.5f, 0.12f);
                s[i] = (tone * 0.45f + air) * swell * waver;
            }
            return Normalize(s, 0.7f);
        }

        /// <summary>
        /// Driven off (1.6 s): a soft, deep thump as it gives, then a long airy sigh scattering away, like ash blown
        /// off a fire. Nothing sharp in it.
        /// </summary>
        public static float[] Disperse(int seed)
        {
            var rng = new Random(seed);
            var s = Make(1.6f);
            float lp = 0f, lp2 = 0f;
            for (int i = 0; i < s.Length; i++)
            {
                float t = Time(i);
                float thump = (float)Math.Sin(2 * Math.PI * (38f + 30f * Decay(t, 20f)) * t) * Decay(t, 7f) * 0.9f;
                float n = Noise(rng);
                lp += (n - lp) * (0.08f * Decay(t, 1.2f) + 0.01f);
                lp2 += (lp - lp2) * 0.3f;
                float sigh = (lp - lp2 * 0.5f) * 2.2f * Env(t, 0.08f, 1.6f, 1.2f);
                s[i] = thump + sigh;
            }
            return Normalize(s, 0.65f);
        }

        /// <summary>
        /// A scour clicking in the dark (about 3 s): a run of dry, wet clicks from deep in a throat, uneven,
        /// quickening, each with a hollow knock under it, and a faint rattle between; ringing off the trees after,
        /// a long dark tail of echo, so you can't quite tell where it came from.
        /// </summary>
        public static float[] ScourClick(int seed)
        {
            var rng = new Random(seed);
            var s = Make(1.05f);
            int clicks = 5 + rng.Next(4);
            float at = 0.02f;
            var starts = new float[clicks];
            for (int k = 0; k < clicks; k++)
            {
                starts[k] = at;
                at += (0.13f - 0.07f * k / clicks) * (0.7f + 0.6f * (float)rng.NextDouble());
            }
            float bp1 = 0f, bp2 = 0f, rattle = 0f;
            for (int i = 0; i < s.Length; i++)
            {
                float t = Time(i);
                float x = 0f;
                for (int k = 0; k < clicks; k++)
                {
                    float d = t - starts[k];
                    if (d < 0f || d > 0.06f) continue;
                    float loud = 0.7f + 0.3f * (k % 3 == 0 ? 1f : 0.5f);
                    x += Noise(rng) * Decay(d, 260f) * loud;                                                   // (the tick)
                    x += (float)Math.Sin(2 * Math.PI * (420f - 900f * d) * d) * Decay(d, 70f) * 0.5f * loud;  // (the hollow knock)
                }
                // A resonance (the throat) on the ticks: two band-passes.
                bp1 += (x - bp1) * 0.35f;
                bp2 += (bp1 - bp2) * 0.6f;
                float n = Noise(rng);
                rattle += (n - rattle) * 0.08f;
                float purr = rattle * 0.12f * (0.5f + 0.5f * (float)Math.Sin(2 * Math.PI * 28 * t)) * Env(t, 0.1f, at, 0.3f);
                s[i] = (bp1 - bp2 * 0.6f) + x * 0.25f + purr;
            }
            return Normalize(Reverb(s, 2f, 0.9f, 0.55f), 0.85f);
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

        /// <summary>Picking something up: a soft rustle and tap.</summary>
        public static float[] Pickup(int seed)
        {
            var rng = new Random(seed);
            var s = Make(0.25f);
            float lp = 0f;
            for (int i = 0; i < s.Length; i++)
            {
                float t = Time(i);
                lp += (Noise(rng) - lp) * 0.35f;
                s[i] = lp * Env(t, 0.01f, 0.25f, 0.15f) * 0.8f + (float)Math.Sin(2 * Math.PI * 520 * t) * Decay(t, 60f) * 0.3f;
            }
            return Normalize(s, 0.5f);
        }

        /// <summary>A flame catching (torch, lighter): a soft whoomp of rushing air.</summary>
        public static float[] Ignite(int seed)
        {
            var rng = new Random(seed);
            var s = Make(0.9f);
            float lp = 0f;
            for (int i = 0; i < s.Length; i++)
            {
                float t = Time(i);
                lp += (Noise(rng) - lp) * (0.04f + 0.25f * Decay(t, 5f));
                s[i] = lp * Env(t, 0.06f, 0.9f, 0.6f) * 2f;
            }
            return Normalize(s, 0.6f);
        }

        /// <summary>A lighter's wheel striking: a scrape and a tick.</summary>
        public static float[] Lighter(int seed)
        {
            var rng = new Random(seed);
            var s = Make(0.2f);
            for (int i = 0; i < s.Length; i++)
            {
                float t = Time(i);
                float scrape = t < 0.08f ? Noise(rng) * (float)(0.5 + 0.5 * Math.Sin(2 * Math.PI * 180 * t)) : 0f;
                float tick = t > 0.08f ? Noise(rng) * Decay(t - 0.08f, 90f) : 0f;
                s[i] = scrape * 0.5f + tick;
            }
            return Normalize(s, 0.5f);
        }

        /// <summary>The flare gun firing: a hard pop and a fizzing hiss.</summary>
        public static float[] FlareShot(int seed)
        {
            var rng = new Random(seed);
            var s = Make(1.6f);
            float hp = 0f, last = 0f;
            for (int i = 0; i < s.Length; i++)
            {
                float t = Time(i);
                float n = Noise(rng);
                hp = 0.9f * (hp + n - last);
                last = n;
                float pop = ((float)Math.Sin(2 * Math.PI * 90 * t) + n) * Decay(t, 30f);
                s[i] = pop + hp * Env(t, 0.02f, 1.6f, 0.8f) * 0.35f;
            }
            return Normalize(s, 0.9f);
        }

        /// <summary>A flare bursting high up: a far-off thump that rolls away.</summary>
        public static float[] FlareBurst(int seed)
        {
            var rng = new Random(seed);
            var s = Make(2.5f);
            float lp = 0f;
            for (int i = 0; i < s.Length; i++)
            {
                float t = Time(i);
                lp += (Noise(rng) - lp) * 0.03f;
                s[i] = (lp * 6f + (float)Math.Sin(2 * Math.PI * 45 * t) * 0.5f) * Decay(t, 1.8f) * Env(t, 0.01f, 2.5f, 0.5f);
            }
            return Normalize(s, 0.8f);
        }

        /// <summary>A glowstick cracking: a brittle snap.</summary>
        public static float[] Crack(int seed)
        {
            var rng = new Random(seed);
            var s = Make(0.15f);
            for (int i = 0; i < s.Length; i++)
            {
                float t = Time(i);
                s[i] = Noise(rng) * (Decay(t, 140f) + 0.6f * (t > 0.03f ? Decay(t - 0.03f, 160f) : 0f));
            }
            return Normalize(s, 0.6f);
        }

        /// <summary>The camera: a shutter clack and the flash's rising whine.</summary>
        public static float[] Shutter(int seed)
        {
            var rng = new Random(seed);
            var s = Make(0.9f);
            for (int i = 0; i < s.Length; i++)
            {
                float t = Time(i);
                float clack = Noise(rng) * (Decay(t, 80f) + (t > 0.06f ? Decay(t - 0.06f, 90f) : 0f));
                float whine = t > 0.15f ? (float)Math.Sin(2 * Math.PI * (2000 + 3000 * (t - 0.15f)) * t) * 0.08f * Env(t - 0.15f, 0.2f, 0.75f, 0.3f) : 0f;
                s[i] = clack + whine;
            }
            return Normalize(s, 0.7f);
        }

        /// <summary>The car answering its key fob: two bright chirps.</summary>
        public static float[] Chirp(int seed)
        {
            var s = Make(0.5f);
            for (int i = 0; i < s.Length; i++)
            {
                float t = Time(i);
                float a = Env(t, 0.005f, 0.12f, 0.03f), b = Env(t - 0.25f, 0.005f, 0.12f, 0.03f);
                s[i] = ((float)Math.Sin(2 * Math.PI * 1800 * t) + 0.5f * (float)Math.Sin(2 * Math.PI * 3600 * t)) * (a + b);
            }
            return Normalize(s, 0.7f);
        }

        /// <summary>A wooden door swinging: a slow creak.</summary>
        public static float[] DoorOpen(int seed)
        {
            var rng = new Random(seed);
            var s = Make(0.9f);
            float phase = 0f;
            for (int i = 0; i < s.Length; i++)
            {
                float t = Time(i);
                float f = 220f + 90f * (float)Math.Sin(t * 5f) + 40f * Noise(rng);
                phase += f / SampleRate;
                float stick = (phase % 1f) < 0.1f ? 1f : -0.1f; // (a stick-slip buzz)
                s[i] = stick * Env(t, 0.1f, 0.9f, 0.3f) * 0.4f;
            }
            return Normalize(s, 0.45f);
        }

        /// <summary>A door shutting: a wooden knock.</summary>
        public static float[] DoorShut(int seed)
        {
            var rng = new Random(seed);
            var s = Make(0.4f);
            for (int i = 0; i < s.Length; i++)
            {
                float t = Time(i);
                s[i] = ((float)Math.Sin(2 * Math.PI * 110 * t) + Noise(rng) * 0.5f) * Decay(t, 22f);
            }
            return Normalize(s, 0.8f);
        }

        /// <summary>A heavy lock giving way far off (the lone door unlocking): a deep metallic clunk with a long ring.</summary>
        public static float[] Clunk(int seed)
        {
            var rng = new Random(seed);
            var s = Make(3f);
            for (int i = 0; i < s.Length; i++)
            {
                float t = Time(i);
                float hit = ((float)Math.Sin(2 * Math.PI * 55 * t) + Noise(rng) * 0.5f) * Decay(t, 9f);
                float ring = ((float)Math.Sin(2 * Math.PI * 233 * t) + 0.6f * (float)Math.Sin(2 * Math.PI * 377 * t)) * Decay(t, 1.4f) * 0.3f;
                s[i] = hit + ring;
            }
            return Normalize(s, 0.9f);
        }

        /// <summary>Eating a snack: a wrapper crinkle and a crunch.</summary>
        public static float[] Eat(int seed)
        {
            var rng = new Random(seed);
            var s = Make(0.7f);
            for (int i = 0; i < s.Length; i++)
            {
                float t = Time(i);
                float crinkle = t < 0.3f ? Noise(rng) * (rng.NextDouble() < 0.02 ? 1f : 0.15f) : 0f;
                float crunch = t > 0.35f ? Noise(rng) * Decay(t - 0.35f, 12f) * 0.7f : 0f;
                s[i] = crinkle + crunch;
            }
            return Normalize(s, 0.6f);
        }

        /// <summary>
        /// A footstep on the forest floor: the heel coming down dull and soft on earth, the foot rolling through
        /// leaf litter (a scatter of little crackles, a twig now and then), then a lighter push off the toe. Each
        /// seed is a slightly different step, so a walk isn't one sound repeated.
        /// </summary>
        public static float[] Step(int seed)
        {
            var rng = new Random(seed);
            const float len = 0.42f;
            var s = Make(len);
            float heelHz = 50f + (float)rng.NextDouble() * 25f;
            float toeAt = 0.17f + (float)rng.NextDouble() * 0.06f;
            // The crackles of the leaves under the foot as it rolls.
            int crackles = 8 + rng.Next(10);
            var at = new float[crackles];
            var amp = new float[crackles];
            var width = new float[crackles];
            for (int c = 0; c < crackles; c++)
            {
                at[c] = 0.03f + (float)Math.Pow(rng.NextDouble(), 1.4) * 0.24f;
                amp[c] = 0.25f + (float)rng.NextDouble() * 0.75f;
                width[c] = 0.003f + (float)rng.NextDouble() * 0.012f;
            }
            bool twig = rng.NextDouble() < 0.25;
            float twigAt = 0.06f + (float)rng.NextDouble() * 0.12f;
            float lp = 0f, lp2 = 0f, dull = 0f, soft = 0f;
            for (int i = 0; i < s.Length; i++)
            {
                float t = Time(i);
                float n = Noise(rng);
                // The heel: a low, dull thump, more felt than heard, and the earth giving under it.
                float heel = (float)Math.Sin(2 * Math.PI * heelHz * t) * Decay(t, 28f) * Math.Min(1f, t / 0.004f);
                dull += (n - dull) * 0.04f;
                float earth = dull * Decay(t, 22f) * 3f;
                // The toe pushing off: softer.
                float tt = t - toeAt;
                float toe = tt > 0f ? ((float)Math.Sin(2 * Math.PI * heelHz * 1.3f * tt) * 0.35f + dull * 1.2f) * Decay(tt, 30f) * Math.Min(1f, tt / 0.006f) : 0f;
                // The leaves: bursts of filtered noise.
                lp += (n - lp) * 0.32f;
                lp2 += (lp - lp2) * 0.06f;
                float band = lp - lp2;
                float crackle = 0f;
                for (int c = 0; c < crackles; c++)
                {
                    float d = (t - at[c]) / width[c];
                    if (d > -1f && d < 4f) crackle += amp[c] * (float)Math.Exp(-Math.Max(0f, d)) * (d < 0f ? 1f + d : 1f);
                }
                float rustle = band * (0.12f * Env(t, 0.02f, len, 0.15f));
                float leaves = band * crackle * 0.8f + rustle;
                // Now and then, a twig snapping under it: a short, dry crack.
                float tw = t - twigAt;
                float snap = twig && tw > 0f && tw < 0.03f ? band * Decay(tw, 160f) * 2.2f : 0f;
                float v = heel * 0.55f + earth * 0.5f + toe * 0.4f + leaves + snap;
                // A touch of softening on top: nothing harsh.
                soft += (v - soft) * 0.55f;
                s[i] = soft;
            }
            return Normalize(s, 0.38f);
        }

        /// <summary>
        /// Thunder (about 8 s): no hiss in it. A first crack, sharp but deep (a burst of tearing cracks rung low,
        /// not bright noise), then the long roll: rumble taken right down low (four poles of filtering, nothing
        /// above a few hundred hertz), rolling in uneven surges as the sound comes back off the hills and clouds
        /// one stretch after another, a darker growl in the middle of it, and a tail dying away into the distance.
        /// </summary>
        public static float[] Thunder(int seed)
        {
            var rng = new Random(seed);
            const float length = 6.5f;
            var s = Make(length);
            float a1 = 0f, a2 = 0f, a3 = 0f, a4 = 0f;   // (the rumble: four poles low)
            float g1 = 0f, g2 = 0f;                     // (the growl, a little higher)
            float c1 = 0f, c2 = 0f;                     // (the crack, rung low)
            // The roll's surges: when each stretch of echo comes back, and how loud.
            var surges = new (float t, float w, float a)[7];
            for (int k = 0; k < surges.Length; k++)
                surges[k] = (0.15f + k * 0.75f + (float)rng.NextDouble() * 0.5f, 0.5f + (float)rng.NextDouble() * 0.8f, (1f - k / 8f) * (0.6f + 0.4f * (float)rng.NextDouble()));
            for (int i = 0; i < s.Length; i++)
            {
                float t = Time(i);
                float n = Noise(rng);
                a1 += (n - a1) * 0.016f; a2 += (a1 - a2) * 0.016f; a3 += (a2 - a3) * 0.025f; a4 += (a3 - a4) * 0.025f;
                g1 += (n - g1) * 0.05f; g2 += (g1 - g2) * 0.05f;
                // The crack: a quick run of impulses in the first fifth of a second, each rung through a low filter.
                float imp = t < 0.22f && rng.NextDouble() < 0.02 * (1.0 - t / 0.22) ? Noise(rng) * 6f : 0f;
                c1 += (imp - c1) * 0.16f; c2 += (c1 - c2) * 0.22f;
                float crack = c2 * 14f * Decay(t, 3f);
                float surge = 0f;
                foreach (var (st, sw, sa) in surges)
                {
                    float d = (t - st) / sw;
                    if (d > -1f && d < 3f) surge += sa * (float)Math.Exp(-d * d * (d < 0f ? 4f : 0.6f));
                }
                float tail = Env(t, 0.08f, length, 3f);
                float rumble = a4 * 450f * (0.25f + surge) * tail;
                float growl = g2 * 320f * surge * Decay(t, 0.25f);
                s[i] = crack + rumble + growl;
            }
            return Normalize(Reverb(s, 2.5f, 0.35f, 0.85f), 0.9f);
        }

        /// <summary>The giant landing: an enormous low boom.</summary>
        public static float[] Boom(int seed)
        {
            var rng = new Random(seed);
            var s = Make(6f);
            float lp = 0f;
            for (int i = 0; i < s.Length; i++)
            {
                float t = Time(i);
                lp += (Noise(rng) - lp) * 0.015f;
                float f = 28f + 30f * Decay(t, 6f);
                s[i] = (float)Math.Sin(2 * Math.PI * f * t) * Decay(t, 0.7f) + lp * 10f * Decay(t, 0.9f);
            }
            return Normalize(s, 1f);
        }

        /// <summary>One of the giant's steps: a deep thud that shakes the ground.</summary>
        public static float[] GiantStep(int seed)
        {
            var rng = new Random(seed);
            var s = Make(2.5f);
            float lp = 0f;
            for (int i = 0; i < s.Length; i++)
            {
                float t = Time(i);
                lp += (Noise(rng) - lp) * 0.02f;
                // The thud, a sub-bass weight under it that rolls on, and the ground's rumble.
                float thud = (float)Math.Sin(2 * Math.PI * (34f + 22f * Decay(t, 10f)) * t) * Decay(t, 1.4f);
                float sub = (float)Math.Sin(2 * Math.PI * 22f * t) * Decay(t, 0.9f) * Math.Min(1f, t / 0.03f) * 0.8f;
                s[i] = thud + sub + lp * 6f * Decay(t, 2f);
            }
            return Normalize(s, 0.95f);
        }

        /// <summary>The giant's chain jolting far off: a deep metallic groan and clank.</summary>
        public static float[] ChainJolt(int seed)
        {
            var rng = new Random(seed);
            var s = Make(3f);
            for (int i = 0; i < s.Length; i++)
            {
                float t = Time(i);
                float clank = (Noise(rng) * 0.5f + (float)Math.Sin(2 * Math.PI * 180 * t)) * Decay(t, 12f);
                float groan = (float)Math.Sin(2 * Math.PI * (70f + 15f * (float)Math.Sin(t * 3f)) * t) * Env(t, 0.2f, 3f, 1.5f) * 0.5f;
                float ring = (float)Math.Sin(2 * Math.PI * 413 * t) * Decay(t, 1.5f) * 0.2f;
                s[i] = clank + groan + ring;
            }
            return Normalize(s, 0.8f);
        }

        /// <summary>Pushing through a bush: a dry, leafy swish.</summary>
        /// <summary>
        /// Pushing through a bush (0.5 s): a soft, low brush of leaves and twigs against you, swelling in and
        /// fading, with nothing sharp in it.
        /// </summary>
        public static float[] BushRustle(int seed)
        {
            var rng = new Random(seed);
            var s = Make(0.5f);
            float hp = 0f, last = 0f, lp = 0f, lp2 = 0f;
            for (int i = 0; i < s.Length; i++)
            {
                float t = Time(i);
                float n = Noise(rng);
                hp = 0.9f * (hp + n - last);
                last = n;
                lp += (hp - lp) * 0.18f;
                lp2 += (lp - lp2) * 0.35f;
                float flutter = 0.75f + 0.25f * (float)Math.Sin(2 * Math.PI * 23 * t + Math.Sin(2 * Math.PI * 7 * t));
                float swell = (float)Math.Sin(Math.PI * Math.Min(1f, t / 0.5f));
                s[i] = lp2 * flutter * swell * swell;
            }
            return Normalize(s, 0.45f);
        }

        public static float[] Rustle(int seed)
        {
            var rng = new Random(seed);
            var s = Make(0.45f);
            float lp = 0f, hp = 0f, last = 0f;
            for (int i = 0; i < s.Length; i++)
            {
                float t = Time(i);
                float n = Noise(rng);
                hp = 0.95f * (hp + n - last);
                last = n;
                lp += (hp - lp) * 0.5f;
                float crackle = rng.NextDouble() < 0.01 ? Noise(rng) * 2f : 0f;
                s[i] = (lp + crackle * Decay(t, 4f)) * Env(t, 0.05f, 0.45f, 0.25f);
            }
            return Normalize(s, 0.5f);
        }

        /// <summary>A twig snapping somewhere out in the dark.</summary>
        public static float[] Twig(int seed)
        {
            var rng = new Random(seed);
            var s = Make(0.3f);
            for (int i = 0; i < s.Length; i++)
            {
                float t = Time(i);
                float snap = Noise(rng) * Decay(t, 90f) + (t > 0.04f ? Noise(rng) * Decay(t - 0.04f, 120f) * 0.6f : 0f);
                float body = (float)Math.Sin(2 * Math.PI * 900 * t) * Decay(t, 60f) * 0.3f;
                s[i] = snap + body;
            }
            return Normalize(s, 0.6f);
        }

        /// <summary>A tree creaking in a strong gust: a slow, grinding groan of wood.</summary>
        public static float[] Creak(int seed)
        {
            var rng = new Random(seed);
            var s = Make(1.6f);
            float phase = 0f;
            for (int i = 0; i < s.Length; i++)
            {
                float t = Time(i);
                float f = 95f + 40f * (float)Math.Sin(t * 2.1f) + 15f * Noise(rng);
                phase += f / SampleRate;
                float pulse = (phase % 1f) < 0.08f ? 1f : 0f;
                s[i] = pulse * Env(t, 0.3f, 1.6f, 0.6f);
            }
            return Normalize(s, 0.4f);
        }

        /// <summary>
        /// Wind through the forest, to loop (16 s): a deep, airy rush in two bands that swells and falls away in
        /// uneven gusts, a low rumble under it, and at the height of each gust a faint, hollow moan that slides up
        /// and down (the wind finding a gap). Its gusts are sums of waves that repeat in 16 s, so it loops clean.
        /// </summary>
        public static float[] WindLoop(int seed)
        {
            var rng = new Random(seed);
            const float length = 16f;
            var s = Make(length);
            // The gusts: slow swells of different periods (all dividing the loop), each its own phase.
            var periods = new[] { 16f, 8f, 16f / 3f, 4f, 16f / 7f };
            var weights = new[] { 0.45f, 0.3f, 0.25f, 0.15f, 0.08f };
            var phases = new float[periods.Length];
            for (int k = 0; k < phases.Length; k++) phases[k] = (float)(rng.NextDouble() * Math.PI * 2);
            float brown = 0f, pink1 = 0f, pink2 = 0f;
            float lowA = 0f, bandA = 0f, lowB = 0f, bandB = 0f, lowM = 0f, bandM = 0f;
            for (int i = 0; i < s.Length; i++)
            {
                float t = Time(i);
                float g = 0f;
                for (int k = 0; k < periods.Length; k++) g += weights[k] * (float)Math.Sin(2 * Math.PI * t / periods[k] + phases[k]);
                float gust = Math.Max(0f, Math.Min(1f, 0.45f + g * 0.6f));
                gust = gust * gust * (3f - 2f * gust);

                float n = Noise(rng);
                brown = brown * 0.998f + n * 0.015f;               // (the rumble)
                pink1 = pink1 * 0.97f + n * 0.03f;
                pink2 = pink2 * 0.6f + n * 0.4f;
                float air = pink1 * 0.7f + pink2 * 0.3f;

                // Two bands of rush, the higher one opening up as it gusts.
                Band(air, 220f + 120f * gust, 0.9f, ref lowA, ref bandA);
                Band(air, 550f + 500f * gust, 1.1f, ref lowB, ref bandB);
                // The moan: a narrow resonance gliding with the gust, heard only near its peak.
                float moanFreq = 280f + 260f * gust + 30f * (float)Math.Sin(2 * Math.PI * t / 2f + phases[1]);
                Band(air, moanFreq, 0.06f, ref lowM, ref bandM);
                float moan = bandM * Math.Max(0f, gust - 0.55f) * 2.2f;

                s[i] = bandA * (0.35f + 0.65f * gust) * 1.2f + bandB * gust * gust * 0.35f + brown * (0.5f + 0.5f * gust) * 1.5f + moan * 0.15f;
            }
            return Loop(s, 0.5f);
        }

        // One step of a state-variable band-pass filter (centre frequency fc, damping q: low is narrow and ringing).
        static void Band(float input, float fc, float q, ref float low, ref float band)
        {
            float f = 2f * (float)Math.Sin(Math.PI * Math.Min(fc, SampleRate / 6f) / SampleRate);
            low += f * band;
            float high = input - low - q * band;
            band += f * high;
        }

        /// <summary>
        /// The canopy in the wind, to loop: a soft, low hush of leaves, with slow swells of rustle coming and going
        /// through it, never sharp (10 s). Its volume follows the gusts.
        /// </summary>
        public static float[] LeavesLoop(int seed)
        {
            var rng = new Random(seed);
            var s = Make(10f);
            float hp = 0f, last = 0f, lp = 0f, lp2 = 0f, burst = 0f;
            for (int i = 0; i < s.Length; i++)
            {
                float t = Time(i);
                float n = Noise(rng);
                hp = 0.97f * (hp + n - last);       // (high: the hiss of needles)
                last = n;
                lp += (hp - lp) * 0.16f;            // (taken well down, twice, so it's a soft hush and not a hiss)
                lp2 += (lp - lp2) * 0.3f;
                // Rustles: the hiss swells in quick, uneven bursts, here and there.
                if (rng.NextDouble() < 1.2 / SampleRate) burst = Math.Max(burst, 0.5f + 0.5f * (float)rng.NextDouble());
                burst *= 0.99993f;                  // (rustles swelling and dying slowly, never sudden)
                float swell = 0.6f + 0.2f * (float)Math.Sin(2 * Math.PI * t / 5f) * (float)Math.Sin(2 * Math.PI * t / 2.5f + 0.7f);
                s[i] = lp2 * (swell + burst * 0.35f);
            }
            return Loop(s, 0.4f);
        }

        /// <summary>
        /// A campfire, to loop (8 s): the low breathy roar of the flames, a soft hiss, and the crackle and pop of
        /// the wood, each pop a little knock of resonant noise at its own pitch, with now and then a sharp snap.
        /// </summary>
        public static float[] FireLoop(int seed)
        {
            var rng = new Random(seed);
            var s = Make(8f);
            float brown = 0f, hp = 0f, last = 0f, pop = 0f, popY1 = 0f, popY2 = 0f, popC = 0f, popR = 0f;
            for (int i = 0; i < s.Length; i++)
            {
                float t = Time(i);
                float n = Noise(rng);
                brown = brown * 0.995f + n * 0.02f;                 // (the roar)
                hp = 0.9f * (hp + n - last);                        // (the hiss)
                last = n;
                float breathe = 0.75f + 0.25f * (float)Math.Sin(2 * Math.PI * t / 2.7f) * (float)Math.Sin(2 * Math.PI * t / 1.3f + 1.1f);
                // A crackle: a knock of noise rung through a resonance at its own pitch, dying away fast.
                if (rng.NextDouble() < 9.0 / SampleRate)
                {
                    pop = (rng.NextDouble() < 0.12 ? 1f : 0.35f) * (float)(0.5 + rng.NextDouble() * 0.5);
                    float f = 600f + (float)rng.NextDouble() * 3400f;
                    popR = 0.96f + (float)rng.NextDouble() * 0.025f;
                    popC = 2f * popR * (float)Math.Cos(2 * Math.PI * f / SampleRate);
                }
                float excite = pop * n;
                pop *= 0.992f;
                float y = popC * popY1 - popR * popR * popY2 + excite;
                popY2 = popY1;
                popY1 = y;
                s[i] = brown * 1.6f * breathe + hp * 0.05f * breathe + y * 0.12f + excite * 0.3f;
            }
            return Loop(s, 0.7f);
        }

        /// <summary>
        /// An old car's alarm, to loop: the horn blaring on and off, two flat, harsh notes together (a buzzy saw
        /// pair a third apart through a nasal resonance), four blasts in 2.4 s.
        /// </summary>
        public static float[] CarAlarm(int seed)
        {
            var s = Make(2.4f);
            double p1 = 0, p2 = 0;
            float y1 = 0f, y2 = 0f;
            const float r = 0.95f;
            float c = 2f * r * (float)Math.Cos(2 * Math.PI * 1500f / SampleRate);
            for (int i = 0; i < s.Length; i++)
            {
                float t = Time(i);
                float phase = t % 0.6f;
                float on = phase < 0.38f ? Math.Min(1f, phase / 0.01f) * Math.Min(1f, (0.38f - phase) / 0.01f) : 0f;
                p1 += 415.0 / SampleRate;
                p2 += 520.0 / SampleRate;
                float buzz = (float)((p1 % 1.0) * 2 - 1) + (float)((p2 % 1.0) * 2 - 1);
                float res = c * y1 - r * r * y2 + buzz; y2 = y1; y1 = res;
                s[i] = (buzz * 0.6f + res * 0.05f) * on;
            }
            return Loop(s, 0.85f);
        }

        /// <summary>The forest's low bed: a faint, wavering drone under everything (12 s, to loop).</summary>
        public static float[] Drone(int seed)
        {
            var s = Make(12f);
            for (int i = 0; i < s.Length; i++)
            {
                float t = Time(i);
                float w = 1f + 0.01f * (float)Math.Sin(2 * Math.PI * t / 6f);
                s[i] = (float)(Math.Sin(2 * Math.PI * 41.2 * w * t) * 0.6 + Math.Sin(2 * Math.PI * 61.7 * t) * 0.25 + Math.Sin(2 * Math.PI * 82.4 * w * t) * 0.15)
                       * (0.8f + 0.2f * (float)Math.Sin(2 * Math.PI * t / 12f));
            }
            return Loop(s, 0.35f);
        }

        /// <summary>
        /// The tension under it all when something's near, to loop: no high tones (they chirped), just a low,
        /// slowly beating drone (D and E flat a step apart, rubbing) and a dark breath of air that rises and falls (10 s).
        /// </summary>
        public static float[] TensionBed(int seed)
        {
            var rng = new Random(seed);
            var s = Make(10f);
            float lp = 0f, lp2 = 0f;
            for (int i = 0; i < s.Length; i++)
            {
                float t = Time(i);
                float drone = (float)(Math.Sin(2 * Math.PI * 73.4 * t) * 0.5 + Math.Sin(2 * Math.PI * 77.8 * t) * 0.35 + Math.Sin(2 * Math.PI * 146.8 * t) * 0.12);
                lp += (Noise(rng) - lp) * 0.03f;
                lp2 += (lp - lp2) * 0.05f;
                float breath = 0.5f + 0.5f * (float)Math.Sin(2 * Math.PI * t / 5f);
                s[i] = (drone * 0.5f + lp2 * 3f * breath) * (0.8f + 0.2f * (float)Math.Sin(2 * Math.PI * t / 10f));
            }
            return Loop(s, 0.4f);
        }

        /// <summary>Rain in the forest, to loop: a soft, dark wash with drops tapping on the leaves (6 s).</summary>
        public static float[] RainLoop(int seed)
        {
            var rng = new Random(seed);
            var s = Make(6f);
            float lp = 0f, lp2 = 0f, hp = 0f, last = 0f, drop = 0f, dropY = 0f;
            for (int i = 0; i < s.Length; i++)
            {
                float n = Noise(rng);
                hp = 0.9f * (hp + n - last);
                last = n;
                lp += (hp - lp) * 0.12f;            // (a soft, dark wash: not a hiss)
                lp2 += (lp - lp2) * 0.3f;
                // Drops on leaves: little soft taps, each its own, not clicks.
                if (rng.NextDouble() < 0.0025) drop = 0.6f + 0.4f * (float)rng.NextDouble();
                drop *= 0.996f;
                dropY += (n * drop - dropY) * 0.25f;
                s[i] = lp2 * 0.8f + dropY * 0.9f;
            }
            return Loop(s, 0.4f);
        }

        /// <summary>
        /// A tawny owl's call (the prototype's): a low hoot, a pause, then a long wavering hoo-hoo-hooo, soft and
        /// hollow, with breath in it (3.6 s).
        /// </summary>
        public static float[] Hoot(int seed)
        {
            var rng = new Random(seed);
            var s = Make(3.6f);
            double phase = 0;
            float lp = 0f;
            // (start, length, pitch at start, pitch at end)
            float[,] notes = { { 0f, 0.45f, 400f, 380f }, { 1.25f, 0.18f, 360f, 380f }, { 1.5f, 0.16f, 370f, 390f }, { 1.72f, 1.2f, 390f, 330f } };
            for (int i = 0; i < s.Length; i++)
            {
                float t = Time(i);
                float amp = 0f, pitch = 380f;
                for (int n = 0; n < notes.GetLength(0); n++)
                {
                    float u = (t - notes[n, 0]) / notes[n, 1];
                    if (u < 0f || u > 1f) continue;
                    amp = (float)Math.Sin(Math.PI * Math.Pow(u, 0.6));
                    pitch = (notes[n, 2] + (notes[n, 3] - notes[n, 2]) * u) * (1f + 0.02f * (float)Math.Sin(t * 2 * Math.PI * 7f) * u); // a waver as it draws out
                }
                phase += pitch / SampleRate;
                float tone = (float)(Math.Sin(2 * Math.PI * phase) + 0.15 * Math.Sin(2 * Math.PI * phase * 2));
                lp += (Noise(rng) - lp) * 0.1f;
                s[i] = (tone * 0.85f + lp * 0.25f) * amp;
            }
            return Normalize(Reverb(s, 3f, 0.7f, 0.75f), 0.8f); // (carried off through the dark trees)
        }

        /// <summary>A barn owl's screech (the prototype's): a harsh, breathy rasp with a strained falling tone, trailing off (1.4 s).</summary>
        public static float[] Screech(int seed)
        {
            var rng = new Random(seed);
            var s = Make(1.4f);
            float lp = 0f, hp = 0f;
            double phase = 0;
            for (int i = 0; i < s.Length; i++)
            {
                float t = Time(i), p = t / 1.4f;
                float env = Math.Min(1f, t / 0.05f) * (float)Math.Pow(1f - p, 1.6f);
                lp += (Noise(rng) - lp) * 0.55f;
                hp += (lp - hp) * 0.15f;
                float rasp = (lp - hp) * (0.7f + 0.3f * (float)Math.Sin(t * 2 * Math.PI * 60f));
                phase += (1900f - 600f * p) / SampleRate;
                s[i] = (rasp + (float)Math.Sin(2 * Math.PI * phase) * 0.25f) * env;
            }
            return Normalize(Reverb(s, 2.6f, 0.65f, 0.7f), 0.75f);
        }

        /// <summary>A frog: a low, rattling croak.</summary>
        public static float[] Croak(int seed)
        {
            var s = Make(0.45f);
            for (int i = 0; i < s.Length; i++)
            {
                float t = Time(i);
                float pulse = (t * 38f) % 1f < 0.35f ? 1f : 0.2f;
                s[i] = (float)Math.Sin(2 * Math.PI * (170f - 40f * t) * t) * pulse * Env(t, 0.02f, 0.45f, 0.15f);
            }
            return Normalize(s, 0.5f);
        }

        /// <summary>A bat's thin squeak.</summary>
        public static float[] Squeak(int seed)
        {
            var s = Make(0.12f);
            for (int i = 0; i < s.Length; i++)
            {
                float t = Time(i);
                s[i] = (float)Math.Sin(2 * Math.PI * (5200f - 18000f * t) * t) * Env(t, 0.005f, 0.12f, 0.05f);
            }
            return Normalize(s, 0.4f);
        }

        /// <summary>
        /// A crow calling: "kaah... kaah... kaah", two to four harsh caws a little under half a second apart, the
        /// last often longer and lower (2 s). Each caw is a rough, strained voice: a buzz of sharp pulses (a crow's
        /// syrinx forced hard) wavering in timing and strength, every other pulse weaker (the rattle in it), with
        /// breath through it, its pitch jumping up at the start and sagging away; shaped by a crow's broad throat
        /// resonances (round 1.3, 2.4 and 3.6 kHz), not a synth's narrow ones. Heard across the forest, with a
        /// little of the trees' echo.
        /// </summary>
        public static float[] Caw(int seed)
        {
            var rng = new Random(seed);
            var s = Make(2.1f);
            int caws = 2 + rng.Next(3);
            float at = 0.02f;
            for (int c = 0; c < caws; c++)
            {
                bool last = c == caws - 1;
                float len = (last ? 0.42f : 0.34f) * (0.9f + 0.2f * (float)rng.NextDouble());
                float pitch = (last ? 0.9f : 1f) * (0.95f + 0.1f * (float)rng.NextDouble());
                if (at + len > 2.0f) break;
                OneCaw(s, (int)(at * SampleRate), len, pitch, rng, c == 0 ? 1f : 0.85f + 0.15f * (float)rng.NextDouble());
                at += len + 0.12f + 0.12f * (float)rng.NextDouble();
            }
            return Normalize(Reverb(s, 1.6f, 0.3f, 0.6f), 0.8f);
        }

        static void OneCaw(float[] s, int start, float len, float pitch, Random rng, float loud)
        {
            int n = (int)(len * SampleRate);
            double phase = 0;
            float jitter = 0f, breath = 0f;
            float l1 = 0f, b1 = 0f, l2 = 0f, b2 = 0f, l3 = 0f, b3 = 0f, hp = 0f, prev = 0f;
            int pulse = 0;
            float pulseGain = 1f;
            for (int i = 0; i < n && start + i < s.Length; i++)
            {
                float t = i / (float)SampleRate, p = t / len;
                // Pitch: a jump up into it, then sagging away (around 480 Hz, lowered for the last, longer caw).
                float f0 = pitch * (420f + 160f * (float)Math.Exp(-p * 9f) * (p < 0.05f ? p / 0.05f : 1f) + 90f * (1f - p) - 60f * p * p);
                jitter += (Noise(rng) - jitter) * 0.02f;
                double before = phase;
                phase += f0 * (1f + 0.06f * jitter) / SampleRate;
                if (Math.Floor(phase) != Math.Floor(before))
                {
                    // A new pulse: its strength wavers, and every other one is weaker (the rattle).
                    pulse++;
                    pulseGain = (0.75f + 0.5f * (float)rng.NextDouble()) * (pulse % 2 == 0 ? 0.55f : 1f);
                }
                // A sharp pulse each period (rich in harmonics, like a strained voice), and the breath through it.
                float ph = (float)(phase - Math.Floor(phase));
                float src = (ph < 0.18f ? (float)Math.Sin(Math.PI * ph / 0.18f) : 0f) * pulseGain;
                breath += (Noise(rng) - breath) * 0.6f;
                src += breath * 0.35f;
                // A crow's throat: broad resonances.
                Band(src, 1300f, 0.55f, ref l1, ref b1);
                Band(src, 2400f, 0.7f, ref l2, ref b2);
                Band(src, 3600f, 0.9f, ref l3, ref b3);
                float v = b1 + b2 * 0.8f + b3 * 0.35f;
                hp = 0.995f * (hp + v - prev); prev = v; // (no rumble under it)
                // Hard onset, held, then trailing off (a little catch near the end).
                float env = Math.Min(1f, t / 0.012f) * (p < 0.6f ? 1f : (float)Math.Pow(1f - (p - 0.6f) / 0.4f, 1.6f));
                env *= 0.85f + 0.15f * (float)Math.Sin(p * 37f);
                s[start + i] += hp * env * loud;
            }
        }

        /// <summary>A step in water: a splash and slosh.</summary>
        public static float[] Splash(int seed)
        {
            var rng = new Random(seed);
            var s = Make(0.5f);
            float lp = 0f;
            for (int i = 0; i < s.Length; i++)
            {
                float t = Time(i);
                lp += (Noise(rng) - lp) * 0.3f;
                float bubbles = (float)Math.Sin(2 * Math.PI * (600f + 400f * (float)Math.Sin(t * 40f)) * t) * Decay(t, 9f) * 0.2f;
                s[i] = lp * Decay(t, 7f) + bubbles;
            }
            return Normalize(s, 0.5f);
        }

        /// <summary>Hooves drumming away (the deer bolting).</summary>
        public static float[] Hooves(int seed)
        {
            var rng = new Random(seed);
            var s = Make(1.6f);
            for (int i = 0; i < s.Length; i++)
            {
                float t = Time(i);
                float beat = (t % 0.18f);
                float hit = (float)Math.Sin(2 * Math.PI * 120 * beat) * Decay(beat, 40f) + Noise(rng) * Decay(beat, 60f) * 0.4f;
                s[i] = hit * Env(t, 0.05f, 1.6f, 0.8f);
            }
            return Normalize(s, 0.5f);
        }

        /// <summary>A small drone's four rotors: a buzzing whine with a beat between them (2 s, to loop).</summary>
        public static float[] Rotors(int seed)
        {
            var rng = new Random(seed);
            var s = Make(2f);
            float lp = 0f;
            double[] freq = { 196.0, 201.5, 207.3, 192.4 };
            double[] phase = new double[4];
            for (int i = 0; i < s.Length; i++)
            {
                float t = Time(i);
                float v = 0f;
                for (int r = 0; r < 4; r++)
                {
                    phase[r] += freq[r] / SampleRate;
                    double p = phase[r] % 1.0;
                    v += (float)(p * 2.0 - 1.0) * 0.25f; // (a saw: buzzy)
                }
                lp += (Noise(rng) - lp) * 0.3f;
                s[i] = v * 0.7f + lp * 0.25f;
            }
            return Loop(s, 0.4f);
        }

        /// <summary>A radio between stations: hiss and crackle (3 s, to loop).</summary>
        public static float[] Static(int seed)
        {
            var rng = new Random(seed);
            var s = Make(3f);
            float lp = 0f;
            for (int i = 0; i < s.Length; i++)
            {
                lp += (Noise(rng) - lp) * 0.5f;
                float crackle = rng.NextDouble() < 0.0008 ? Noise(rng) * 3f : 0f;
                s[i] = lp * 0.5f + crackle;
            }
            return Loop(s, 0.35f);
        }

        /// <summary>
        /// A CB radio left on in an empty lookout (24 s, to loop): hiss through a small speaker, the squelch
        /// breaking it off into dead air now and then; a carrier tone drifting under it; a voice coming and going
        /// that never quite makes words (syllables of a man's voice, too slow, ring-modulated into a warble); and,
        /// once, three flat beeps like a number station.
        /// </summary>
        public static float[] RadioCreep(int seed)
        {
            var rng = new Random(seed);
            const float length = 24.5f;
            var s = Make(length);
            // Where the voice speaks (start, length) and where the squelch cuts out (start, length).
            var said = new System.Collections.Generic.List<(float, float)>();
            for (float t = 1.5f; t < length - 3f; t += 2.5f + (float)rng.NextDouble() * 4f)
                said.Add((t, 1.2f + (float)rng.NextDouble() * 2.2f));
            var dead = new System.Collections.Generic.List<(float, float)>();
            for (float t = 4f; t < length - 2f; t += 5f + (float)rng.NextDouble() * 6f)
                dead.Add((t, 0.25f + (float)rng.NextDouble() * 0.9f));
            float beeps = 13f + (float)rng.NextDouble() * 4f;
            float hp = 0f, lp = 0f, f1a = 0f, f1b = 0f, f2a = 0f, f2b = 0f;
            double voicePhase = 0, carrier = 0;
            for (int i = 0; i < s.Length; i++)
            {
                float t = Time(i);
                // The hiss, band-limited like a cheap speaker.
                float n = Noise(rng);
                lp += (n - lp) * 0.35f;
                hp += (lp - hp) * 0.04f;
                float hiss = (lp - hp) * 0.55f;
                float crackle = rng.NextDouble() < 0.0006 ? Noise(rng) * 0.9f : 0f;
                // The carrier, wandering.
                float cf = 610f + 90f * (float)Math.Sin(2 * Math.PI * 0.05 * t) + 25f * (float)Math.Sin(2 * Math.PI * 0.31 * t);
                carrier += 2 * Math.PI * cf / SampleRate;
                float tone = (float)Math.Sin(carrier) * 0.06f * (0.5f + 0.5f * (float)Math.Sin(2 * Math.PI * 0.08 * t));
                // The voice: a buzz at a slow, sinking pitch through two moving formants, in syllables.
                float voice = 0f;
                foreach (var (at, len) in said)
                {
                    float d = t - at;
                    if (d < 0f || d > len) continue;
                    float syll = Math.Max(0f, (float)Math.Sin(Math.PI * d * (2.2f + 0.6f * (float)Math.Sin(at)))) * Env(d, 0.1f, len, 0.4f);
                    float pitch = 118f - 22f * d / len + 6f * (float)Math.Sin(2 * Math.PI * 3 * d);
                    voicePhase += pitch / SampleRate;
                    float buzz = (float)(voicePhase - Math.Floor(voicePhase)) * 2f - 1f;
                    float input = buzz * syll;
                    // (formants sliding between vowels)
                    float k1 = 0.06f + 0.03f * (float)Math.Sin(d * 5.1f + at), k2 = 0.16f + 0.06f * (float)Math.Sin(d * 3.3f + at * 2f);
                    f1a += (input - f1a - 0.6f * f1b) * k1; f1b += f1a * k1;
                    f2a += (input - f2a - 0.6f * f2b) * k2; f2b += f2a * k2;
                    voice = (f1b * 0.6f + f2b * 0.4f) * (1f + 0.6f * (float)Math.Sin(2 * Math.PI * 37 * t)); // (the warble)
                }
                // Three flat beeps, once.
                float bd = t - beeps;
                float beep = 0f;
                for (int b = 0; b < 3; b++)
                {
                    float x = bd - b * 0.7f;
                    if (x >= 0f && x < 0.32f) beep = (float)Math.Sin(2 * Math.PI * 880 * t) * 0.22f * Env(x, 0.01f, 0.32f, 0.02f);
                }
                float squelch = 1f;
                foreach (var (at, len) in dead)
                {
                    float d = t - at;
                    if (d >= 0f && d < len) squelch = d < 0.02f || d > len - 0.02f ? 0.6f : 0.04f;
                }
                s[i] = ((hiss + crackle) * 0.22f + tone + voice * 0.9f + beep) * squelch;
            }
            return Loop(s, 0.55f);
        }

        /// <summary>An old song through a small speaker: a slow waltz in a minor key, thin and crackling (16 s, to loop).</summary>
        public static float[] RadioSong(int seed)
        {
            var rng = new Random(seed);
            var s = Make(16f);
            // Bars of three beats; a melody over a bass note and a chord on beats 2 and 3.
            double[] melody = { 440, 523.3, 493.9, 440, 392, 349.2, 329.6, 349.2, 392, 440, 392, 349.2, 329.6, 293.7, 329.6, 349.2,
                                440, 523.3, 587.3, 523.3, 493.9, 440, 415.3, 440, 493.9, 440, 392, 349.2, 329.6, 349.2, 329.6, 293.7 };
            double[] bass = { 110, 87.3, 98, 82.4, 110, 87.3, 82.4, 110 };
            float beat = 0.5f;
            float hp = 0f, lp = 0f, prev = 0f;
            for (int i = 0; i < s.Length; i++)
            {
                float t = Time(i);
                int b = (int)(t / beat);
                float inBeat = t - b * beat;
                double note = melody[(b / 1) % melody.Length];
                double root = bass[(b / 6) % bass.Length];
                float mel = (float)Math.Sin(2 * Math.PI * note * t) * Decay(inBeat, 2.5f);
                mel += (float)Math.Sin(2 * Math.PI * note * 2 * t) * 0.2f * Decay(inBeat, 4f);
                float low = b % 3 == 0 ? (float)Math.Sin(2 * Math.PI * root * t) * Decay(inBeat, 3f) : 0f;
                float chord = b % 3 != 0 ? ((float)Math.Sin(2 * Math.PI * root * 2.5 * t) + (float)Math.Sin(2 * Math.PI * root * 3 * t)) * 0.2f * Decay(inBeat, 6f) : 0f;
                float v = mel * 0.6f + low * 0.5f + chord;
                // A small speaker: no lows, no highs, a little distortion; and the crackle of the air.
                hp += (v - prev) * 0.98f - hp * 0.02f;
                prev = v;
                lp += (hp - lp) * 0.35f;
                float speaker = (float)Math.Tanh(lp * 1.8f);
                float crackle = rng.NextDouble() < 0.001 ? Noise(rng) * 1.5f : 0f;
                s[i] = speaker * 0.8f + Noise(rng) * 0.04f + crackle;
            }
            return Loop(s, 0.45f);
        }

        // ------------------------------------------------------------------ deer (as the prototype made them)

        /// <summary>Two or three short, throaty pulses, each a little lower.</summary>
        public static float[] DeerGrunt(int seed)
        {
            var rng = new Random(seed);
            var s = Make(0.9f);
            float phase = 0f, lp = 0f;
            for (int i = 0; i < s.Length; i++)
            {
                float t = Time(i);
                float pulse = (t % 0.3f) / 0.3f;
                int n = (int)(t / 0.3f);
                float amp = (float)Math.Sin(Math.PI * Math.Min(1f, pulse * 1.4f)) * (n < 3 ? 1f - n * 0.25f : 0f);
                float pitch = 110f - n * 12f;
                phase += pitch / SampleRate;
                float voice = 2f * (phase % 1f) - 1f;
                lp += (voice + Noise(rng) * 0.4f - lp) * 0.08f; // (dark and muffled)
                s[i] = lp * amp;
            }
            return Normalize(s, 0.8f);
        }

        /// <summary>Nasal and reedy: rising, then breaking downward.</summary>
        public static float[] DeerBleat(int seed)
        {
            var rng = new Random(seed);
            const float len = 0.8f;
            var s = Make(len);
            float phase = 0f, lp = 0f;
            for (int i = 0; i < s.Length; i++)
            {
                float t = Time(i), p = t / len;
                float env = Math.Min(1f, t / 0.04f) * (float)Math.Pow(1f - p, 1.3f);
                float pitch = (p < 0.6f ? Lerp(420f, 560f, p / 0.6f) : Lerp(560f, 380f, (p - 0.6f) / 0.4f)) * (1f + 0.03f * (float)Math.Sin(t * 2 * Math.PI * 22f));
                phase += pitch / SampleRate;
                float reed = (2f * (phase % 1f) - 1f) * 0.6f + (float)Math.Sin(2 * Math.PI * phase * 3f) * 0.3f;
                lp += (reed + Noise(rng) * 0.15f - lp) * 0.3f;
                s[i] = lp * env;
            }
            return Normalize(s, 0.7f);
        }

        /// <summary>A sharp snort through the nose, the nostrils fluttering.</summary>
        public static float[] DeerSnort(int seed)
        {
            var rng = new Random(seed);
            var s = Make(0.5f);
            float lp = 0f, hp = 0f;
            for (int i = 0; i < s.Length; i++)
            {
                float t = Time(i);
                float env = Math.Min(1f, t / 0.01f) * (float)Math.Exp(-t * 9f);
                lp += (Noise(rng) - lp) * 0.35f;
                hp += (lp - hp) * 0.05f;
                float flutter = 0.7f + 0.3f * (float)Math.Sin(t * 2 * Math.PI * 35f);
                s[i] = (lp - hp) * env * flutter;
            }
            return Normalize(s, 0.8f);
        }

        /// <summary>The alarm as one bolts: a forceful burst of breath with a little throaty buzz.</summary>
        public static float[] DeerAlarm(int seed)
        {
            var rng = new Random(seed);
            var s = Make(0.6f);
            float lp = 0f, hp = 0f;
            for (int i = 0; i < s.Length; i++)
            {
                float t = Time(i);
                lp += (Noise(rng) - lp) * 0.35f;
                hp += (lp - hp) * 0.06f;
                float env = t < 0.02f ? t / 0.02f : (float)Math.Exp(-(t - 0.02f) * 7f);
                float buzz = (float)Math.Sin(2 * Math.PI * 140f * t) * 0.25f * (float)Math.Exp(-t * 10f);
                s[i] = ((lp - hp) + buzz) * env;
            }
            return Normalize(s, 0.8f);
        }

        /// <summary>Galloping: three strides a loop, four hoof strikes a stride.</summary>
        public static float[] Gallop(int seed)
        {
            var rng = new Random(seed);
            var s = Make(1.2f + 0.4f); // (a stride over, crossfaded back in by Loop)
            float lp = 0f;
            for (int i = 0; i < s.Length; i++)
            {
                float t = Time(i);
                float stride = t % 0.4f;
                float hits = Hoof(stride, 0f) + Hoof(stride, 0.05f) * 0.8f + Hoof(stride, 0.13f) + Hoof(stride, 0.17f) * 0.9f;
                lp += (Noise(rng) - lp) * 0.2f;
                s[i] = hits * ((float)Math.Sin(2 * Math.PI * 70f * t) * 0.8f + lp * 0.6f);
            }
            return Loop(s, 0.8f);
        }

        static float Hoof(float t, float start) => t >= start ? (float)Math.Exp(-(t - start) * 45f) : 0f;
        static float Lerp(float a, float b, float t) => a + (b - a) * t;

        // Crossfade the end into the start so it loops without a click.
        /// <summary>
        /// A scour arriving where it crept to (about 2.6 s): one loud, wet click from deep in its throat, a hard
        /// tick over a hollow knock, ringing away off the trees.
        /// </summary>
        public static float[] ScourSnap(int seed)
        {
            var rng = new Random(seed);
            var s = Make(0.25f);
            float bp1 = 0f, bp2 = 0f;
            for (int i = 0; i < s.Length; i++)
            {
                float d = Time(i);
                float x = Noise(rng) * Decay(d, 180f)
                        + (float)Math.Sin(2 * Math.PI * (330f - 700f * d) * d) * Decay(d, 45f) * 0.8f
                        + (float)Math.Sin(2 * Math.PI * 95f * d) * Decay(d, 22f) * 0.35f;
                bp1 += (x - bp1) * 0.3f;
                bp2 += (bp1 - bp2) * 0.55f;
                s[i] = (bp1 - bp2 * 0.5f) + x * 0.3f;
            }
            return Normalize(Reverb(s, 2.4f, 0.9f, 0.5f), 0.95f);
        }

        /// <summary>
        /// A tree giving way (about 3 s): a first loud crack of fibres going, then the trunk tearing, a run of
        /// splintering cracks, sparse and then quicker and quicker, rung through the wood's own hollow body, a deep
        /// groaning creak under them as it leans; ringing off the forest.
        /// </summary>
        public static float[] TreeBreak(int seed)
        {
            var rng = new Random(seed);
            const float length = 1.9f;
            var s = Make(length);
            // When each crack goes: the first, then quickening.
            var at = new System.Collections.Generic.List<(float t, float loud)> { (0f, 2.2f), (0.035f, 1.3f), (0.08f, 0.8f) };
            for (float t = 0.28f; t < length - 0.15f; t += 0.03f + 0.22f * (float)Math.Pow(1f - t / length, 2f) * (0.5f + (float)rng.NextDouble()))
                at.Add((t, 0.12f + 0.3f * (float)rng.NextDouble() * (0.4f + 0.6f * t / length)));
            float r1 = 0f, r2 = 0f, r3 = 0f, r4 = 0f, creakPhase = 0f, creakLp = 0f;
            for (int i = 0; i < s.Length; i++)
            {
                float t = Time(i);
                float x = 0f;
                foreach (var (ct, loud) in at)
                {
                    float d = t - ct;
                    if (d < 0f || d > 0.08f) continue;
                    x += Noise(rng) * Decay(d, 120f) * loud;
                }
                // The wood's body: two resonances it rings through (a hollow knock and a woody mid).
                r1 += (x - r1) * 0.05f; r2 += (r1 - r2) * 0.05f;   // (low)
                r3 += (x - r3) * 0.25f; r4 += (r3 - r4) * 0.4f;    // (mid)
                float wood = (r2 * 3.5f) + (r3 - r4) * 1.2f + x * 0.35f;
                // The groan: stick-slip pulses at a low wandering rate, smoothed, swelling as it leans.
                float f = 70f + 25f * (float)Math.Sin(t * 3.1f) + 30f * t / length + 10f * Noise(rng);
                creakPhase += f / SampleRate;
                float pulse = (creakPhase % 1f) < 0.1f ? 1f : 0f;
                creakLp += (pulse - creakLp) * 0.2f;
                float groan = (creakLp - 0.1f) * Env(t, 0.5f, length, 0.4f) * 0.5f;
                s[i] = wood + groan;
            }
            return Normalize(Reverb(s, 1.6f, 0.45f, 0.55f), 0.95f);
        }

        /// <summary>
        /// A tree going over (2.5 s): the rush of its crown through the air, building as it falls faster and faster
        /// (as the fall does: slow, then all at once), brushing and snapping through the branches of its
        /// neighbours on the way. It cuts off where it lands (the crash covers that).
        /// </summary>
        public static float[] TreeRush(int seed)
        {
            var rng = new Random(seed);
            const float length = 2.5f;
            var s = Make(length);
            float lowA = 0f, bandA = 0f, hp = 0f, last = 0f, lp = 0f, brush = 0f;
            for (int i = 0; i < s.Length; i++)
            {
                float t = Time(i);
                float u = t / 2.4f;
                float speed = Math.Min(1f, u * u * 1.1f);
                float n = Noise(rng);
                // The rush: band-limited air rising in pitch with the speed.
                Band(n, 250f + 900f * speed, 0.8f, ref lowA, ref bandA);
                // Leaves and twigs brushing: hiss in bursts.
                hp = 0.95f * (hp + n - last); last = n;
                lp += (hp - lp) * 0.3f;
                if (rng.NextDouble() < (2.0 + 14.0 * speed) / SampleRate) brush = Math.Max(brush, 0.5f + 0.5f * (float)rng.NextDouble());
                brush *= 0.9993f;
                float snap = rng.NextDouble() < 4.0 * speed / SampleRate ? Noise(rng) * 2f : 0f;
                float cut = t > 2.4f ? Math.Max(0f, 1f - (t - 2.4f) / 0.08f) : 1f;
                s[i] = (bandA * speed * 1.6f + lp * (0.15f + brush) * speed + snap) * cut;
            }
            return Normalize(s, 0.75f);
        }

        /// <summary>
        /// A tree hitting the ground (about 4 s): a heavy, deep thud you feel, the crash of its branches breaking
        /// under it (a burst of cracks thick at first, thinning), the trunk bouncing once, and the leaves and twigs
        /// raining down and settling; rolling away through the forest.
        /// </summary>
        public static float[] TreeImpact(int seed)
        {
            var rng = new Random(seed);
            const float length = 1.8f;
            var s = Make(length);
            float lp = 0f, hp = 0f, last = 0f, r1 = 0f, r2 = 0f;
            for (int i = 0; i < s.Length; i++)
            {
                float t = Time(i);
                float thud = (float)Math.Sin(2 * Math.PI * (42f + 30f * Decay(t, 12f)) * t) * Decay(t, 5f) * Math.Min(1f, t / 0.004f);
                float b = t - 0.32f; // (the bounce)
                if (b > 0f) thud += (float)Math.Sin(2 * Math.PI * (50f + 20f * Decay(b, 12f)) * b) * Decay(b, 8f) * 0.4f;
                float n = Noise(rng);
                lp += (n - lp) * 0.04f;
                float rumble = lp * 3f * Decay(t, 3f);
                // Branches breaking: cracks, dense at first, thinning out.
                float rate = 260f * Decay(t, 3.5f) + 6f;
                float crack = rng.NextDouble() < rate / SampleRate ? Noise(rng) * (1.5f + (float)rng.NextDouble()) * Decay(t, 1.5f) : 0f;
                r1 += (crack - r1) * 0.3f; r2 += (r1 - r2) * 0.5f;
                // Leaves and twigs settling: a hiss that falls away.
                hp = 0.93f * (hp + n - last); last = n;
                float settle = hp * 0.12f * Env(t, 0.05f, length, 1.2f) * Decay(t, 1.4f);
                s[i] = thud * 1.1f + rumble + (r1 - r2 * 0.5f) * 1.6f + crack * 0.25f + settle;
            }
            return Normalize(Reverb(s, 2.2f, 0.5f, 0.6f), 0.95f);
        }

        /// <summary>
        /// An old telephone ringing, to loop (6 s): two seconds of its bells (two of them, a little apart in pitch,
        /// struck turn about by the hammer twenty times a second, each ringing with a bell's uneven overtones and
        /// a faint rattle of the works), then four of silence.
        /// </summary>
        public static float[] PhoneRing(int seed)
        {
            var rng = new Random(seed);
            var s = Make(6f);
            float[] f0 = { 940f, 1010f };
            float[] partial = { 1f, 2.76f, 5.4f };
            float[] weight = { 1f, 0.45f, 0.18f };
            var amp = new float[2];
            var ph = new double[2, 3];
            for (int i = 0; i < s.Length; i++)
            {
                float t = Time(i);
                bool ringing = t < 2f;
                // The hammer: a strike every 1/40 s, each bell in turn.
                int strike = (int)(t * 40f);
                float sinceStrike = t * 40f - strike;
                if (ringing && sinceStrike < 1f / (SampleRate / 40f) * 1.5f) amp[strike % 2] = 1f;
                float x = 0f;
                for (int b = 0; b < 2; b++)
                {
                    for (int p = 0; p < 3; p++)
                    {
                        ph[b, p] += 2 * Math.PI * f0[b] * partial[p] / SampleRate;
                        x += (float)Math.Sin(ph[b, p]) * weight[p] * amp[b] * (p == 0 ? 1f : (float)Math.Exp(-p * 0.8));
                    }
                    amp[b] *= 0.99965f; // (a bell's ring dying away, but struck again before it's gone)
                }
                float rattle = ringing ? Noise(rng) * 0.04f * (sinceStrike < 0.2f ? 1f : 0.3f) : 0f;
                s[i] = x * 0.5f + rattle;
            }
            return Normalize(s, 0.7f);
        }

        /// <summary>
        /// An old TV left on with no signal, to loop (4 s): the bright hiss of snow, a low mains hum under it, a
        /// faint flutter at the frame rate, and now and then a crackle.
        /// </summary>
        public static float[] TvStatic(int seed)
        {
            var rng = new Random(seed);
            var s = Make(4.4f);
            float hp = 0f, last = 0f, lp = 0f;
            for (int i = 0; i < s.Length; i++)
            {
                float t = Time(i);
                float n = Noise(rng);
                hp = 0.6f * (hp + n - last);
                last = n;
                lp += (hp - lp) * 0.6f;
                float flutter = 0.85f + 0.15f * (float)Math.Sin(2 * Math.PI * 60 * t);
                float hum = (float)(Math.Sin(2 * Math.PI * 60 * t) * 0.05 + Math.Sin(2 * Math.PI * 120 * t) * 0.03);
                float crackle = rng.NextDouble() < 0.0004 ? Noise(rng) * 0.5f : 0f;
                s[i] = lp * 0.5f * flutter + hum + crackle;
            }
            return Loop(s, 0.5f);
        }

        /// <summary>A spray can spraying, to loop (2 s): a soft, airy hiss, wavering a little with the hand.</summary>
        public static float[] SprayHiss(int seed)
        {
            var rng = new Random(seed);
            var s = Make(2.3f);
            float hp = 0f, last = 0f, lp = 0f;
            for (int i = 0; i < s.Length; i++)
            {
                float t = Time(i);
                float n = Noise(rng);
                hp = 0.8f * (hp + n - last);
                last = n;
                lp += (hp - lp) * 0.45f;
                s[i] = lp * (0.85f + 0.15f * (float)Math.Sin(2 * Math.PI * 3.1 * t));
            }
            return Loop(s, 0.4f);
        }

        /// <summary>
        /// The giant, far off (about 11 s): a vast, slow groan, a throat the size of a hill. A low voice with a
        /// rough edge to it, sinking in pitch, its vowel slowly changing (two formants sliding), shuddering as it
        /// goes, a sub rumble under it you feel more than hear; heard across miles of forest, so dark and long in
        /// its echo.
        /// </summary>
        public static float[] GiantGroan(int seed)
        {
            var rng = new Random(seed);
            const float length = 7.5f;
            var s = Make(length);
            double phase = 0;
            float f1a = 0f, f1b = 0f, f2a = 0f, f2b = 0f, lp = 0f, sub1 = 0f, sub2 = 0f;
            for (int i = 0; i < s.Length; i++)
            {
                float t = Time(i), u = t / length;
                float pitch = 46f - 14f * u + 1.2f * (float)Math.Sin(2 * Math.PI * 5.5 * t) * (0.3f + u) + 0.6f * Noise(rng);
                phase += pitch / SampleRate;
                float p = (float)(phase - Math.Floor(phase));
                // A rough, pulsed voice: sharp pulses (many harmonics), a little noise in each.
                float voice = (p < 0.12f ? 1f - p / 0.12f : 0f) * 2f - 0.3f + Noise(rng) * 0.15f;
                // Two formants, an "aw" opening to an "oo".
                float k1 = 2f * (float)Math.Sin(Math.PI * (260f - 80f * u) / SampleRate);
                float k2 = 2f * (float)Math.Sin(Math.PI * (620f - 250f * u) / SampleRate);
                f1a += k1 * f1b; float h1 = voice - f1a - 0.25f * f1b; f1b += k1 * h1;
                f2a += k2 * f2b; float h2 = voice - f2a - 0.3f * f2b; f2b += k2 * h2;
                float throat = f1b * 1f + f2b * 0.45f;
                lp += (throat - lp) * 0.2f;
                // The sub rumble.
                sub1 += (Noise(rng) - sub1) * 0.004f; sub2 += (sub1 - sub2) * 0.004f;
                float env = Math.Min(1f, t / 1.4f) * (float)Math.Pow(Math.Max(0f, 1f - u), 0.7f);
                float shudder = 0.8f + 0.2f * (float)Math.Sin(2 * Math.PI * 3.1 * t + Math.Sin(t * 1.3));
                s[i] = (lp * shudder * 2.2f + sub2 * 22f) * env;
            }
            return Normalize(Reverb(s, 3.5f, 0.6f, 0.85f), 0.95f);
        }

        /// <summary>
        /// A dry sound with reverb added (a forest at night: the sound ringing off trunks): this many seconds of tail
        /// added on, decaying to silence; <paramref name="wet"/> how loud the echo is against the dry sound, and
        /// <paramref name="dark"/> (0-1) how much the echo loses its highs as it goes. Four combs into two
        /// all-passes (Schroeder), their delays chosen not to line up.
        /// </summary>
        public static float[] Reverb(float[] dry, float tail, float wet, float dark)
        {
            var o = new float[dry.Length + (int)(tail * SampleRate)];
            int[] combs = { 1557, 1617, 1491, 1422 };
            int[] passes = { 225, 556 };
            // Feedback for each comb so it falls 60 dB over the tail.
            var bufs = new float[combs.Length][];
            var idx = new int[combs.Length];
            var fb = new float[combs.Length];
            var damp = new float[combs.Length];
            for (int c = 0; c < combs.Length; c++)
            {
                int d = combs[c] * 2; // (twice the classic lengths: a bigger space)
                bufs[c] = new float[d];
                fb[c] = (float)Math.Pow(10.0, -3.0 * d / (tail * SampleRate));
            }
            var ap = new float[passes.Length][];
            var apIdx = new int[passes.Length];
            for (int a = 0; a < passes.Length; a++) ap[a] = new float[passes[a]];
            for (int i = 0; i < o.Length; i++)
            {
                float x = i < dry.Length ? dry[i] : 0f;
                float sum = 0f;
                for (int c = 0; c < combs.Length; c++)
                {
                    var buf = bufs[c];
                    float y = buf[idx[c]];
                    damp[c] = y * (1f - dark) + damp[c] * dark;
                    buf[idx[c]] = x + damp[c] * fb[c];
                    idx[c] = (idx[c] + 1) % buf.Length;
                    sum += y;
                }
                sum *= 0.25f;
                for (int a = 0; a < passes.Length; a++)
                {
                    var buf = ap[a];
                    float b = buf[apIdx[a]];
                    float y = -sum * 0.5f + b;
                    buf[apIdx[a]] = sum + b * 0.5f;
                    apIdx[a] = (apIdx[a] + 1) % buf.Length;
                    sum = y;
                }
                o[i] = x + sum * wet;
            }
            return o;
        }

        static float[] Loop(float[] s, float peak)
        {
            int fade = Math.Min(s.Length / 4, SampleRate / 3);
            var looped = new float[s.Length - fade];
            Array.Copy(s, looped, looped.Length);
            for (int i = 0; i < fade; i++)
            {
                float k = i / (float)fade;
                looped[i] = s[i] * k + s[looped.Length + i] * (1f - k);
            }
            return Normalize(looped, peak);
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
