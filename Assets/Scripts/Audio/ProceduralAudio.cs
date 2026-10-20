using UnityEngine;

/// <summary>
/// Synthesizes all of the game's sounds in code, so it works without any audio files.
/// Each clip is generated once on first use and then shared.
/// Every script that plays these also has AudioClip slots for swapping in real recordings.
/// </summary>
public static class ProceduralAudio
{
    const int Rate = 44100;
    const float TwoPi = Mathf.PI * 2f;

    static readonly System.Random rng = new System.Random(1234);
    static float White() => (float)(rng.NextDouble() * 2.0 - 1.0);

    static AudioClip drone, wind, tension, whisper, materialize, crescendo, poof, creep, screech, twigSnap, moan,
        lampClick, lampIgnite, lampHum, leaves, treeCreak, flashlightClick, batteryInsert, squeak, skitter, wingFlaps, deerAlarm, gallop, advance, lampSpark, lampPowerDown;

    // ---------------------------------------------------------------- Ambience

    /// <summary>Low, dull, beating drone. Loops seamlessly.</summary>
    public static AudioClip Drone => drone != null ? drone : (drone = MakeDrone());
    /// <summary>Gusting wind. Loops seamlessly.</summary>
    public static AudioClip Wind => wind != null ? wind : (wind = MakeWind());
    /// <summary>High, dissonant shimmer that fades in when an entity is near. Loops seamlessly.</summary>
    public static AudioClip Tension => tension != null ? tension : (tension = MakeTension());
    public static AudioClip TwigSnap => twigSnap != null ? twigSnap : (twigSnap = MakeTwigSnap());
    public static AudioClip Moan => moan != null ? moan : (moan = MakeMoan());

    // ---------------------------------------------------------------- Flashlight

    /// <summary>Plasticky switch click.</summary>
    public static AudioClip FlashlightClick => flashlightClick != null ? flashlightClick : (flashlightClick = MakeFlashlightClick());
    /// <summary>Battery sliding in and snapping into place, with a little charge-up whine.</summary>
    public static AudioClip BatteryInsert => batteryInsert != null ? batteryInsert : (batteryInsert = MakeBatteryInsert());

    // ---------------------------------------------------------------- Wildlife

    /// <summary>A rat's quick, high chirps.</summary>
    public static AudioClip Squeak => squeak != null ? squeak : (squeak = MakeSqueak());
    /// <summary>Tiny claws scrabbling over leaves.</summary>
    public static AudioClip Skitter => skitter != null ? skitter : (skitter = MakeSkitter());
    /// <summary>Leathery wing beats passing overhead.</summary>
    public static AudioClip WingFlaps => wingFlaps != null ? wingFlaps : (wingFlaps = MakeWingFlaps());
    /// <summary>A deer's sharp alarm snort.</summary>
    public static AudioClip DeerAlarm => deerAlarm != null ? deerAlarm : (deerAlarm = MakeDeerAlarm());
    /// <summary>Hooves drumming the ground at a gallop. Loops seamlessly.</summary>
    public static AudioClip Gallop => gallop != null ? gallop : (gallop = MakeGallop());

    // ---------------------------------------------------------------- Weather

    /// <summary>Leaves and needles rustling in the wind. Loops seamlessly.</summary>
    public static AudioClip Leaves => leaves != null ? leaves : (leaves = MakeLeaves());
    /// <summary>A trunk groaning as it bends.</summary>
    public static AudioClip TreeCreak => treeCreak != null ? treeCreak : (treeCreak = MakeTreeCreak());

    // ---------------------------------------------------------------- Entity

    /// <summary>Breathy whisper with a low growl underneath. Loops seamlessly.</summary>
    public static AudioClip Whisper => whisper != null ? whisper : (whisper = MakeWhisper());
    public static AudioClip Materialize => materialize != null ? materialize : (materialize = MakeMaterialize());
    public static AudioClip Crescendo => crescendo != null ? crescendo : (crescendo = MakeCrescendo());
    public static AudioClip Poof => poof != null ? poof : (poof = MakePoof());
    /// <summary>Rustle and crackle of something moving through the undergrowth.</summary>
    public static AudioClip Creep => creep != null ? creep : (creep = MakeCreep());
    public static AudioClip Screech => screech != null ? screech : (screech = MakeScreech());
    /// <summary>Pounding heartbeat under a deep, swelling groan: something is coming. Loops seamlessly.</summary>
    public static AudioClip Advance => advance != null ? advance : (advance = MakeAdvance());

    // ---------------------------------------------------------------- Lamps

    public static AudioClip LampClick => lampClick != null ? lampClick : (lampClick = MakeLampClick());
    public static AudioClip LampIgnite => lampIgnite != null ? lampIgnite : (lampIgnite = MakeLampIgnite());
    /// <summary>Electrical mains hum. Loops seamlessly.</summary>
    public static AudioClip LampHum => lampHum != null ? lampHum : (lampHum = MakeLampHum());
    /// <summary>Short electrical crackle when the lamp flickers.</summary>
    public static AudioClip LampSpark => lampSpark != null ? lampSpark : (lampSpark = MakeLampSpark());
    /// <summary>Buzz winding down to a clunk as the lamp dies.</summary>
    public static AudioClip LampPowerDown => lampPowerDown != null ? lampPowerDown : (lampPowerDown = MakeLampPowerDown());

    // ---------------------------------------------------------------- Playback helpers

    /// <summary>Adds an AudioSource set up for 2D (spatial = false) or 3D playback.</summary>
    public static AudioSource AddSource(GameObject go, AudioClip clip, bool loop, bool spatial, float volume,
        float minDistance = 4f, float maxDistance = 60f)
    {
        var src = go.AddComponent<AudioSource>();
        src.clip = clip;
        src.loop = loop;
        src.volume = volume;
        src.playOnAwake = false;
        src.spatialBlend = spatial ? 1f : 0f;
        src.rolloffMode = AudioRolloffMode.Logarithmic;
        src.minDistance = minDistance;
        src.maxDistance = maxDistance;
        src.dopplerLevel = 0f;
        return src;
    }

    /// <summary>Plays a clip once at a world position, then cleans up.</summary>
    public static void PlayAt(AudioClip clip, Vector3 position, float volume = 1f, float pitch = 1f, float minDistance = 6f)
    {
        if (clip == null) return;
        var go = new GameObject("One Shot: " + clip.name);
        go.transform.position = position;
        var src = AddSource(go, clip, false, true, volume, minDistance, 80f);
        src.pitch = pitch;
        src.Play();
        Object.Destroy(go, clip.length / Mathf.Max(pitch, 0.01f) + 0.1f);
    }

    // ---------------------------------------------------------------- Synthesis

    // Loops: every periodic part uses frequencies that complete a whole number of cycles in the
    // loop length, and the noise is crossfaded across the seam, so there's no click when it wraps.
    static AudioClip Loop(string name, float seconds, System.Func<float, float> sample, float peak)
    {
        int n = (int)(seconds * Rate);
        int fade = Rate / 2;
        var raw = new float[n + fade];
        for (int i = 0; i < raw.Length; i++) raw[i] = sample(i / (float)Rate);

        var data = new float[n];
        for (int i = 0; i < n; i++)
        {
            if (i < fade)
            {
                float k = i / (float)fade;
                data[i] = raw[i] * k + raw[n + i] * (1f - k);
            }
            else
            {
                data[i] = raw[i];
            }
        }
        return Create(name, data, peak);
    }

    static AudioClip Shot(string name, float seconds, System.Func<float, float> sample, float peak)
    {
        var data = new float[(int)(seconds * Rate)];
        for (int i = 0; i < data.Length; i++) data[i] = sample(i / (float)Rate);
        return Create(name, data, peak);
    }

    static AudioClip Create(string name, float[] data, float peak)
    {
        float max = 0.0001f;
        foreach (float v in data) max = Mathf.Max(max, Mathf.Abs(v));
        float gain = peak / max;
        for (int i = 0; i < data.Length; i++) data[i] *= gain;

        var clip = AudioClip.Create(name, data.Length, 1, Rate, false);
        clip.SetData(data, 0);
        return clip;
    }

    static float Sin(float hz, float t) => Mathf.Sin(TwoPi * hz * t);
    static float Tanh(float x) => (float)System.Math.Tanh(x);

    static AudioClip MakeDrone()
    {
        float brown = 0f;
        return Loop("Drone", 12f, t =>
        {
            brown = (brown + White() * 0.02f) * 0.998f;
            float lfo1 = 0.5f + 0.5f * Sin(1f / 12f, t);
            float lfo2 = 0.5f + 0.5f * Mathf.Sin(TwoPi * 2f / 12f * t + 1.3f);
            return 0.30f * Sin(41.25f, t)
                 + 0.26f * Sin(41.5f, t)            // beats against the first tone
                 + 0.18f * Sin(55f, t) * lfo1
                 + 0.10f * Sin(82.5f, t) * lfo2
                 + 0.05f * Sin(123.75f, t) * (1f - lfo1)
                 + brown * 0.5f;
        }, 0.8f);
    }

    static AudioClip MakeWind()
    {
        float lp1 = 0f, lp2 = 0f;
        return Loop("Wind", 16f, t =>
        {
            float gust = 0.5f + 0.5f * Sin(1f / 16f, t);
            float swell = 0.3f + 0.7f * gust * gust * (0.7f + 0.3f * Sin(3f / 16f, t));
            float a = 0.02f + 0.04f * swell; // brighter when gusting
            lp1 += (White() - lp1) * a * 2f;
            lp2 += (lp1 - lp2) * a;
            return (lp1 - lp2) * swell;
        }, 0.7f);
    }

    static AudioClip MakeFlashlightClick()
    {
        float lp = 0f;
        return Shot("Flashlight Click", 0.08f, t =>
        {
            float w = White();
            lp += (w - lp) * 0.4f;
            float click = Burst(t, 0f) + Burst(t, 0.035f) * 0.6f; // press and release
            return ((w - lp) * 0.7f + Sin(3200f, t) * 0.3f) * click;
        }, 0.8f);
    }

    static AudioClip MakeBatteryInsert()
    {
        const float len = 0.7f;
        float lp = 0f, phase = 0f;
        return Shot("Battery Insert", len, t =>
        {
            float w = White();
            lp += (w - lp) * 0.15f;
            float slide = t < 0.18f ? lp * Mathf.Sin(Mathf.PI * t / 0.18f) * 0.5f : 0f;
            float snap = (w * 0.8f + Sin(1800f, t) * 0.5f) * Burst(t, 0.2f) * 2f;
            phase += TwoPi * Mathf.Lerp(600f, 2400f, Mathf.Clamp01((t - 0.25f) / 0.4f)) / Rate;
            float whine = t > 0.25f ? Mathf.Sin(phase) * 0.15f * Mathf.Sin(Mathf.PI * (t - 0.25f) / (len - 0.25f)) : 0f;
            return slide + snap + whine;
        }, 0.8f);
    }

    static AudioClip MakeSqueak()
    {
        float phase = 0f;
        return Shot("Squeak", 0.35f, t =>
        {
            // Three short chirps, each sweeping downward.
            float env = 0f, local = 0f;
            for (int i = 0; i < 3; i++)
            {
                float start = i * 0.1f;
                float d = t - start;
                if (d >= 0f && d < 0.07f)
                {
                    env = Mathf.Sin(Mathf.PI * d / 0.07f);
                    local = d;
                }
            }
            phase += TwoPi * (4200f - local * 18000f) / Rate;
            return Mathf.Sin(phase + 0.6f * Mathf.Sin(phase * 0.5f)) * env;
        }, 0.6f);
    }

    static AudioClip MakeSkitter()
    {
        const float len = 0.6f;
        float click = 0f, lp = 0f;
        return Shot("Skitter", len, t =>
        {
            if (rng.NextDouble() < 0.006) click = (float)(rng.NextDouble() * 0.8 + 0.2);
            click *= 0.9f;
            float w = White();
            lp += (w - lp) * 0.4f;
            return (w - lp) * click * Mathf.Sin(Mathf.PI * t / len);
        }, 0.7f);
    }

    static AudioClip MakeWingFlaps()
    {
        const float len = 1.6f;
        float lp = 0f;
        return Shot("Wing Flaps", len, t =>
        {
            float beat = (t * 7f) % 1f;                 // seven beats a second
            float flap = Mathf.Exp(-beat * 9f);
            lp += (White() - lp) * 0.08f;               // soft, papery whoosh
            return lp * flap * Mathf.Sin(Mathf.PI * t / len);
        }, 0.7f);
    }

    static AudioClip MakeDeerAlarm()
    {
        const float len = 0.6f;
        float lp = 0f, hp = 0f;
        return Shot("Deer Alarm", len, t =>
        {
            // A forceful burst of breath through the nose, with a little throaty buzz.
            lp += (White() - lp) * 0.35f;
            hp += (lp - hp) * 0.06f;
            float env = t < 0.02f ? t / 0.02f : Mathf.Exp(-(t - 0.02f) * 7f);
            float buzz = Mathf.Sin(TwoPi * 140f * t) * 0.25f * Mathf.Exp(-t * 10f);
            return ((lp - hp) + buzz) * env;
        }, 0.8f);
    }

    static AudioClip MakeGallop()
    {
        float lp = 0f;
        return Loop("Gallop", 1.2f, t =>
        {
            // Three strides per loop, four hoof strikes per stride.
            float stride = t % 0.4f;
            float hits = Hoof(stride, 0f) + Hoof(stride, 0.05f) * 0.8f + Hoof(stride, 0.13f) + Hoof(stride, 0.17f) * 0.9f;
            lp += (White() - lp) * 0.2f;
            return hits * (Mathf.Sin(TwoPi * 70f * t) * 0.8f + lp * 0.6f);
        }, 0.8f);
    }

    static float Hoof(float t, float start) => t >= start ? Mathf.Exp(-(t - start) * 45f) : 0f;

    static AudioClip MakeLeaves()
    {
        float lp = 0f, hp = 0f, flutter = 0f;
        return Loop("Leaves", 8f, t =>
        {
            lp += (White() - lp) * 0.7f;
            hp += (lp - hp) * 0.3f;
            // Rustles come in quick, uneven bursts.
            flutter += ((float)rng.NextDouble() - flutter) * 0.0015f;
            float bursts = Mathf.Clamp01(flutter * 2.2f - 0.4f);
            float swell = 0.6f + 0.4f * Sin(0.25f, t);
            return (lp - hp) * (0.2f + bursts) * swell;
        }, 0.6f);
    }

    static AudioClip MakeTreeCreak()
    {
        const float len = 1.8f;
        float y1 = 0f, y2 = 0f, z1 = 0f, z2 = 0f, pulsePhase = 0f;
        // Two resonances, like a hollow trunk: y[n] = 2r cos(w) y[n-1] - r^2 y[n-2] + x[n]
        float r = 0.996f;
        float c1 = 2f * r * Mathf.Cos(TwoPi * 380f / Rate);
        float c2 = 2f * r * Mathf.Cos(TwoPi * 910f / Rate);
        return Shot("Tree Creak", len, t =>
        {
            float p = t / len;
            // Stick-slip friction: a pulse train that slows as the creak drags on.
            float rateHz = Mathf.Lerp(38f, 14f, p) * (1f + 0.15f * White());
            pulsePhase += rateHz / Rate;
            float x = 0f;
            if (pulsePhase >= 1f)
            {
                pulsePhase -= 1f;
                x = 1f;
            }
            float a = c1 * y1 - r * r * y2 + x;
            y2 = y1; y1 = a;
            float b = c2 * z1 - r * r * z2 + x;
            z2 = z1; z1 = b;
            float env = Mathf.Sin(Mathf.PI * p);
            return (a + b * 0.5f) * env;
        }, 0.8f);
    }

    static AudioClip MakeTension()
    {
        return Loop("Tension", 8f, t =>
        {
            float tremolo = 0.6f + 0.4f * Sin(0.75f, t);
            return (0.30f * Sin(311.125f, t)
                  + 0.30f * Sin(311.875f, t)
                  + 0.20f * Sin(466.25f, t) * (0.5f + 0.5f * Sin(1f / 8f, t))
                  + 0.15f * Sin(622.25f, t)) * tremolo;
        }, 0.6f);
    }

    static AudioClip MakeWhisper()
    {
        float lp = 0f, hp = 0f;
        return Loop("Whisper", 6f, t =>
        {
            lp += (White() - lp) * 0.25f;
            hp += (lp - hp) * 0.05f;
            float breath = lp - hp;
            float flutter = 0.5f + 0.5f * Sin(7f, t) * Mathf.Sin(TwoPi * 1.5f * t + 0.7f);
            float swell = 0.5f + 0.5f * Sin(1f / 3f, t);
            float envelope = 0.4f + 0.6f * swell * swell * swell;
            float growl = Mathf.Sin(TwoPi * 65f * t + 2f * Sin(32.5f, t)) * 0.25f * (0.5f + 0.5f * Sin(1f / 6f, t));
            return breath * flutter * envelope * 3f + growl;
        }, 0.8f);
    }

    static AudioClip MakeMaterialize()
    {
        const float len = 1.5f;
        float lp = 0f, phase = 0f;
        return Shot("Materialize", len, t =>
        {
            float p = t / len;
            lp += (White() - lp) * (0.005f + 0.2f * p * p);  // noise opens up as it swells
            phase += TwoPi * (40f + 40f * p) / Rate;
            float env = p * p * p;
            return (lp * 1.5f + Mathf.Sin(phase) * 0.6f) * env;
        }, 0.9f);
    }

    static AudioClip MakeCrescendo()
    {
        const float len = 2.5f;
        float p1 = 0f, p2 = 0f, p3 = 0f, lp = 0f;
        return Shot("Crescendo", len, t =>
        {
            float p = t / len;
            float hz = 180f + 1200f * p * p;
            p1 += TwoPi * hz / Rate;
            p2 += TwoPi * hz * 1.06f / Rate;  // detuned: grinding dissonance
            p3 += TwoPi * hz * 0.75f / Rate;
            lp += (White() - lp) * 0.3f;
            float s = Mathf.Sin(p1) + Mathf.Sin(p2) * 0.8f + Mathf.Sin(p3) * 0.5f + lp * p * 2f;
            float env = 0.1f + 0.9f * p * p;
            return Tanh(s * env * (1f + 3f * p));
        }, 0.9f);
    }

    static AudioClip MakePoof()
    {
        float lp = 0f, phase = 0f;
        return Shot("Poof", 1.2f, t =>
        {
            lp += (White() - lp) * (0.5f * Mathf.Exp(-t * 3f) + 0.02f);
            phase += TwoPi * (90f * Mathf.Exp(-t * 4f) + 30f) / Rate;
            return lp * Mathf.Exp(-t * 6f) + Mathf.Sin(phase) * Mathf.Exp(-t * 5f) * 0.8f;
        }, 0.9f);
    }

    static AudioClip MakeCreep()
    {
        const float len = 0.7f;
        float crackle = 0f, lp = 0f, hp = 0f;
        return Shot("Creep", len, t =>
        {
            float p = t / len;
            if (rng.NextDouble() < 0.0015 * (1f - p))
                crackle = (float)(rng.NextDouble() * 0.8 + 0.2) * (White() > 0f ? 1f : -1f);
            crackle *= 0.97f;
            lp += (White() - lp) * 0.3f;
            hp += (lp - hp) * 0.08f;
            float rustle = (lp - hp) * Mathf.Sin(Mathf.PI * p) * 0.6f;
            return crackle + rustle;
        }, 0.8f);
    }

    static AudioClip MakeScreech()
    {
        const float len = 2f;
        float phase = 0f, mod = 0f;
        return Shot("Screech", len, t =>
        {
            float hz = 900f + 500f * Sin(7f, t);
            phase += TwoPi * hz / Rate;
            mod += TwoPi * hz * 1.41f / Rate;
            float s = Mathf.Sin(phase + 3f * Mathf.Sin(mod)) + White() * 0.5f;
            float env = t < 0.05f ? t / 0.05f : Mathf.Exp(-(t - 0.05f) * 0.8f);
            return Tanh(s * 2f) * env;
        }, 1f);
    }

    static AudioClip MakeAdvance()
    {
        float lp = 0f, breathLp = 0f, breathHp = 0f;
        return Loop("Advance", 4f, t =>
        {
            // Heartbeat: lub-dub once per second.
            float beat = t % 1f;
            float lub = beat < 0.3f ? Mathf.Sin(TwoPi * 48f * beat) * Mathf.Exp(-beat * 18f) : 0f;
            float dubT = beat - 0.25f;
            float dub = dubT >= 0f && dubT < 0.3f ? Mathf.Sin(TwoPi * 42f * dubT) * Mathf.Exp(-dubT * 22f) * 0.7f : 0f;

            // Groan: two detuned saws, low-passed, swelling with each pair of beats.
            float saw = (2f * (46f * t % 1f) - 1f) + (2f * (46.5f * t % 1f) - 1f);
            lp += (saw - lp) * 0.02f;
            float swell = 0.5f + 0.5f * Sin(0.5f, t);

            breathLp += (White() - breathLp) * 0.15f;
            breathHp += (breathLp - breathHp) * 0.04f;

            return (lub + dub) * 1.2f + lp * 0.5f * swell + (breathLp - breathHp) * 0.3f * swell;
        }, 0.9f);
    }

    static AudioClip MakeLampSpark()
    {
        float lp = 0f;
        return Shot("Lamp Spark", 0.12f, t =>
        {
            float w = White();
            lp += (w - lp) * 0.25f;
            float crackle = rng.NextDouble() < 0.02 ? White() * 3f : 0f;
            return ((w - lp) * 0.6f + crackle) * Mathf.Exp(-t * 30f);
        }, 0.8f);
    }

    static AudioClip MakeLampPowerDown()
    {
        const float len = 1.4f;
        float phase = 0f, lp = 0f, clunkPhase = 0f;
        return Shot("Lamp Power Down", len, t =>
        {
            float p = t / len;
            // Buzz sagging in pitch and stuttering as it dies.
            float hz = Mathf.Lerp(120f, 35f, p * p);
            phase += hz / Rate;
            float saw = 2f * (phase % 1f) - 1f;
            lp += (saw - lp) * 0.12f;
            float stutter = Sin(9f + 14f * p, t) > -0.2f ? 1f : 0.2f;
            float buzz = lp * (1f - p) * stutter;

            // Relay clunk at the very end.
            float ct = t - (len - 0.25f);
            float clunk = 0f;
            if (ct >= 0f)
            {
                clunkPhase += TwoPi * 70f / Rate;
                clunk = Mathf.Sin(clunkPhase) * Mathf.Exp(-ct * 25f);
            }
            return buzz + clunk * 0.8f;
        }, 0.9f);
    }

    static AudioClip MakeTwigSnap()
    {
        float lp = 0f;
        return Shot("Twig Snap", 0.3f, t =>
        {
            float env = Burst(t, 0f) + Burst(t, 0.045f) * 0.6f + Burst(t, 0.11f) * 0.4f;
            float w = White();
            lp += (w - lp) * 0.2f;
            return (w - lp) * env; // high-passed crack
        }, 0.9f);
    }

    static float Burst(float t, float start) => t >= start ? Mathf.Exp(-(t - start) * 120f) : 0f;

    static AudioClip MakeMoan()
    {
        const float len = 3.5f;
        float phase = 0f, breathLp = 0f, breathHp = 0f, outLp = 0f;
        return Shot("Moan", len, t =>
        {
            float p = t / len;
            float hz = 110f - 25f * p + 3f * Sin(5f, t);
            phase += TwoPi * hz / Rate;
            float voice = Mathf.Sin(phase) + 0.5f * Mathf.Sin(phase * 2f) + 0.25f * Mathf.Sin(phase * 3f);
            breathLp += (White() - breathLp) * 0.2f;
            breathHp += (breathLp - breathHp) * 0.05f;
            float env = Mathf.Sin(Mathf.PI * p);
            float s = (voice + (breathLp - breathHp) * 0.6f) * env * env;
            outLp += (s - outLp) * 0.08f; // muffled, like it's far away
            return outLp;
        }, 0.8f);
    }

    static AudioClip MakeLampClick()
    {
        float lp = 0f;
        return Shot("Lamp Click", 0.12f, t =>
        {
            float w = White();
            lp += (w - lp) * 0.3f;
            return (w - lp) * Mathf.Exp(-t * 80f) + Sin(2000f, t) * Mathf.Exp(-t * 120f) * 0.6f;
        }, 0.8f);
    }

    static AudioClip MakeLampIgnite()
    {
        const float len = 0.9f;
        float phase = 0f, lp = 0f, flicker = 1f;
        return Shot("Lamp Ignite", len, t =>
        {
            float p = t / len;
            phase += TwoPi * (60f * Mathf.Exp(-t * 6f) + 40f) / Rate;
            float clunk = Mathf.Sin(phase) * Mathf.Exp(-t * 10f);
            if (rng.NextDouble() < 0.002) flicker = (float)rng.NextDouble();
            float saw = 2f * (120f * t % 1f) - 1f;
            lp += (saw - lp) * 0.1f;
            float buzz = lp * Mathf.SmoothStep(0f, 1f, p) * Mathf.Lerp(flicker, 1f, p);
            return clunk + buzz * 0.5f;
        }, 0.9f);
    }

    static AudioClip MakeLampHum()
    {
        float lp = 0f;
        return Loop("Lamp Hum", 2f, t =>
        {
            float saw = 2f * (120f * t % 1f) - 1f;
            lp += (saw - lp) * 0.05f;
            return 0.4f * Sin(60f, t) + 0.3f * Sin(120f, t) + 0.15f * Sin(180f, t) + lp * 0.2f + White() * 0.01f;
        }, 0.6f);
    }
}
