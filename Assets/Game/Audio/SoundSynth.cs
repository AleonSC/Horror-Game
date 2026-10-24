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

        /// <summary>Thunder (and the giant's chain snapping): a crack, then a long rolling rumble.</summary>
        public static float[] Thunder(int seed)
        {
            var rng = new Random(seed);
            var s = Make(5f);
            float lp = 0f, lp2 = 0f;
            for (int i = 0; i < s.Length; i++)
            {
                float t = Time(i);
                float n = Noise(rng);
                lp += (n - lp) * 0.02f;
                lp2 += (lp - lp2) * 0.05f;
                float crack = n * Decay(t, 30f) * 0.8f;
                float roll = lp * 8f * Env(t, 0.05f, 5f, 3f) * (0.6f + 0.4f * (float)Math.Sin(t * 5.3f + Math.Sin(t * 1.7f) * 3));
                s[i] = crack + roll + lp2 * 6f * Decay(t, 0.5f);
            }
            return Normalize(s, 0.9f);
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

        /// <summary>Wind in the trees, to loop: a breathing, hollow rush (8 s).</summary>
        public static float[] WindLoop(int seed)
        {
            var rng = new Random(seed);
            var s = Make(8f);
            float lp = 0f, lp2 = 0f;
            for (int i = 0; i < s.Length; i++)
            {
                float t = Time(i);
                float swell = 0.65f + 0.35f * (float)Math.Sin(2 * Math.PI * t / 8f) * (float)Math.Sin(2 * Math.PI * t / 2.7f + 1f);
                lp += (Noise(rng) - lp) * (0.03f + 0.02f * swell);
                lp2 += (lp - lp2) * 0.2f;
                s[i] = (lp - lp2 * 0.6f) * swell;
            }
            return Loop(s, 0.5f);
        }

        /// <summary>
        /// The canopy in the wind, to loop: a soft, airy hiss of needles and leaves, with little bursts of rustle
        /// coming and going through it (10 s). Its volume follows the gusts.
        /// </summary>
        public static float[] LeavesLoop(int seed)
        {
            var rng = new Random(seed);
            var s = Make(10f);
            float hp = 0f, last = 0f, lp = 0f, burst = 0f;
            for (int i = 0; i < s.Length; i++)
            {
                float t = Time(i);
                float n = Noise(rng);
                hp = 0.97f * (hp + n - last);       // (high: the hiss of needles)
                last = n;
                lp += (hp - lp) * 0.35f;            // (taken down a little, so it's soft)
                // Rustles: the hiss swells in quick, uneven bursts, here and there.
                if (rng.NextDouble() < 2.5 / SampleRate) burst = 1f;
                burst *= 0.99985f;
                float swell = 0.5f + 0.3f * (float)Math.Sin(2 * Math.PI * t / 5f) * (float)Math.Sin(2 * Math.PI * t / 2.3f + 0.7f);
                s[i] = lp * (swell + burst * 0.8f);
            }
            return Loop(s, 0.4f);
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

        /// <summary>Rain in the forest, to loop: a soft hiss with drops pattering on leaves (6 s).</summary>
        public static float[] RainLoop(int seed)
        {
            var rng = new Random(seed);
            var s = Make(6f);
            float lp = 0f, hp = 0f, last = 0f;
            for (int i = 0; i < s.Length; i++)
            {
                float n = Noise(rng);
                hp = 0.9f * (hp + n - last);
                last = n;
                lp += (hp - lp) * 0.35f;
                float patter = rng.NextDouble() < 0.004 ? Noise(rng) * 3f : 0f;
                s[i] = lp * 0.6f + patter;
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
            return Normalize(s, 0.8f);
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
            return Normalize(s, 0.75f);
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
        /// A crow's harsh, rasping caw (the prototype's): a buzzy, rough voice, its pitch sagging, through two nasal
        /// resonances (0.42 s).
        /// </summary>
        public static float[] Caw(int seed)
        {
            var rng = new Random(seed);
            var s = Make(0.42f);
            double phase = 0;
            float y1 = 0f, y2 = 0f, z1 = 0f, z2 = 0f;
            const float r = 0.97f;
            float c1 = 2f * r * (float)Math.Cos(2 * Math.PI * 1200f / SampleRate);
            float c2 = 2f * r * (float)Math.Cos(2 * Math.PI * 2600f / SampleRate);
            for (int i = 0; i < s.Length; i++)
            {
                float t = Time(i), p = t / 0.42f;
                phase += (520f - 120f * p + 25f * Noise(rng)) / SampleRate;
                float source = (float)(2.0 * (phase % 1.0) - 1.0) + Noise(rng) * 0.4f;
                float a = c1 * y1 - r * r * y2 + source; y2 = y1; y1 = a;
                float b = c2 * z1 - r * r * z2 + source; z2 = z1; z1 = b;
                float env = (float)Math.Sin(Math.PI * Math.Min(1f, p * 1.2f)) * (p < 0.08f ? p / 0.08f : 1f);
                s[i] = (a + b * 0.7f) * env;
            }
            return Normalize(s, 0.8f);
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
