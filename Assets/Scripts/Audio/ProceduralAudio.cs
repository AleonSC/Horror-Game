using UnityEngine;

/// <summary>
/// Synthesizes all of the game's sounds in code, so it works without any audio files.
/// Each clip is generated once and then shared: in the background, all at once, as Play starts (Prepare),
/// so they're ready by the time anything asks; anything asked for before its turn is made there and then.
/// Every script that plays these also has AudioClip slots for swapping in real recordings.
/// </summary>
public static class ProceduralAudio
{
    const int Rate = 44100;
    const float TwoPi = Mathf.PI * 2f;

    // One random generator per thread: sounds are made on several background threads at once.
    [System.ThreadStatic] static System.Random threadRng;
    static System.Random rng => threadRng ?? (threadRng = new System.Random(1234 + System.Threading.Thread.CurrentThread.ManagedThreadId));
    static float White() => (float)(rng.NextDouble() * 2.0 - 1.0);

    // ---------------------------------------------------------------- Made in the background

    // Samples made on a background thread, waiting to be turned into clips (which only the main thread may
    // do), by the name of the field they're for.
    static readonly System.Collections.Concurrent.ConcurrentDictionary<string, (string name, float[] data)> prepared =
        new System.Collections.Concurrent.ConcurrentDictionary<string, (string, float[])>();
    // Set on a background thread while it makes a sound: Create hands the samples over instead of making a clip.
    [System.ThreadStatic] static System.Collections.Generic.List<(string name, float[] data)> capture;
    static System.Threading.Tasks.Task preparing;

    /// <summary>
    /// Starts making every sound's samples on background threads, all at once. Each becomes a clip the first
    /// time it's asked for. Called as Play starts (after StaticReset).
    /// </summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Prepare()
    {
        const System.Reflection.BindingFlags flags = System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic;
        var jobs = new System.Collections.Generic.List<(string field, System.Reflection.MethodInfo make)>();
        foreach (var method in typeof(ProceduralAudio).GetMethods(flags))
        {
            if (!method.Name.StartsWith("Make") || method.Name.Length < 5 || method.GetParameters().Length != 0 || method.ReturnType != typeof(AudioClip)) continue;
            string field = char.ToLowerInvariant(method.Name[4]) + method.Name.Substring(5);
            var f = typeof(ProceduralAudio).GetField(field, flags);
            if (f != null && f.FieldType == typeof(AudioClip) && f.GetValue(null) as AudioClip == null) jobs.Add((field, method));
        }
        preparing = System.Threading.Tasks.Task.Run(() =>
            System.Threading.Tasks.Parallel.ForEach(jobs, job =>
            {
                try
                {
                    capture = new System.Collections.Generic.List<(string, float[])>();
                    job.make.Invoke(null, null);
                    if (capture.Count == 1) prepared[job.field] = capture[0];
                }
                catch (System.Exception) { /* made on the main thread when it's asked for instead */ }
                finally { capture = null; }
            }));
    }

    /// <summary>The clip for a field, if its samples are ready (made into a clip now, on the main thread); else null.</summary>
    static AudioClip Take(string field)
    {
        if (!prepared.TryRemove(field, out var made)) return null;
        var clip = AudioClip.Create(made.name, made.data.Length, 1, Rate, false);
        clip.SetData(made.data, 0);
        return clip;
    }

    /// <summary>Forgets samples made for an earlier play (StaticReset calls this).</summary>
    internal static void ResetStatics()
    {
        prepared.Clear();
        preparing = null;
    }

    static AudioClip drone, wind, tension, whisper, materialize, crescendo, poof, creep, screech, twigSnap, moan,
        lampClick, lampIgnite, lampHum, leaves, bushRustle, breath, thunder, owlHoot, owlScreech, deerGrunt, deerBleat, deerSnort, treeCreak, flashlightClick, batteryInsert, squeak, skitter, wingFlaps, deerAlarm, gallop, caw,
        doorCreak, pickup, lighterFlick, torchIgnite, fireLoop, burnOut, floodlight, flareShot, flareBurn, treeSnap, treeImpact, eyeOpen, distantRumble, giantStep, scream, advance, lampSpark, lampPowerDown;

    // ---------------------------------------------------------------- Ambience

    /// <summary>Low, dull, beating drone. Loops seamlessly.</summary>
    public static AudioClip Drone => drone != null ? drone : (drone = Take(nameof(drone)) ?? MakeDrone());
    /// <summary>The night's chorus: crickets chirping, each at its own pitch and pace, and a frog now and then. Loops.</summary>
    public static AudioClip NightChorus => nightChorus != null ? nightChorus : (nightChorus = Take(nameof(nightChorus)) ?? MakeNightChorus());
    static AudioClip nightChorus;
    /// <summary>One heartbeat, heard from inside: a deep, soft "lub" and a quicker, lighter "dub".</summary>
    public static AudioClip Heartbeat => heartbeat != null ? heartbeat : (heartbeat = Take(nameof(heartbeat)) ?? MakeHeartbeat());
    static AudioClip heartbeat;
    // Doors: wooden ones (shacks, the lone door), the car's metal doors and trunk, and its plastic glove box.
    /// <summary>A wooden door opening: the latch clacks back, then the hinge creaks as it swings.</summary>
    public static AudioClip DoorOpen => doorOpen != null ? doorOpen : (doorOpen = Take(nameof(doorOpen)) ?? MakeDoorOpen());
    /// <summary>A wooden door swinging shut: a lower, quicker creak (DoorShut is the bang as it lands).</summary>
    public static AudioClip DoorClose => doorClose != null ? doorClose : (doorClose = Take(nameof(doorClose)) ?? MakeDoorClose());
    /// <summary>A wooden door landing in its frame: a heavy, hollow thud and the latch dropping in.</summary>
    public static AudioClip DoorShut => doorShut != null ? doorShut : (doorShut = Take(nameof(doorShut)) ?? MakeDoorShut());
    /// <summary>A locked door: the handle rattling, the door knocking against its frame, not giving.</summary>
    public static AudioClip DoorRattle => doorRattle != null ? doorRattle : (doorRattle = Take(nameof(doorRattle)) ?? MakeDoorRattle());
    /// <summary>A car door unlatching: a metallic pop and a little squeak of the hinge.</summary>
    public static AudioClip CarDoorOpen => carDoorOpen != null ? carDoorOpen : (carDoorOpen = Take(nameof(carDoorOpen)) ?? MakeCarDoorOpen());
    /// <summary>A car door slammed: a heavy metal thunk and the panel ringing.</summary>
    public static AudioClip CarDoorShut => carDoorShut != null ? carDoorShut : (carDoorShut = Take(nameof(carDoorShut)) ?? MakeCarDoorShut());
    /// <summary>The glove box dropping open: a plastic click and a soft flop.</summary>
    public static AudioClip GloveBoxOpen => gloveBoxOpen != null ? gloveBoxOpen : (gloveBoxOpen = Take(nameof(gloveBoxOpen)) ?? MakeGloveBoxOpen());
    /// <summary>The glove box snapped shut.</summary>
    public static AudioClip GloveBoxShut => gloveBoxShut != null ? gloveBoxShut : (gloveBoxShut = Take(nameof(gloveBoxShut)) ?? MakeGloveBoxShut());
    static AudioClip doorOpen, doorClose, doorShut, doorRattle, carDoorOpen, carDoorShut, gloveBoxOpen, gloveBoxShut;
    /// <summary>Three slow knocks on a wooden door, somewhere: knuckles on hollow wood, echoing.</summary>
    public static AudioClip Knocks => knocks != null ? knocks : (knocks = Take(nameof(knocks)) ?? MakeKnocks());
    static AudioClip knocks;

    /// <summary>A frog's croak: a low, throaty, rattling "rrribb", the throat pulsing.</summary>
    public static AudioClip FrogCroak => frogCroak != null ? frogCroak : (frogCroak = Take(nameof(frogCroak)) ?? MakeFrogCroak());
    /// <summary>Something small dropping into water: a plop and a little splash.</summary>
    public static AudioClip Plop => splash != null ? splash : (splash = Take(nameof(splash)) ?? MakeSplash());
    static AudioClip frogCroak, splash;

    static AudioClip MakeFrogCroak()
    {
        float phase = 0f, lp = 0f;
        return Shot("Frog Croak", 0.5f, t =>
        {
            // A buzzy low tone pulsed at about 30 a second (the rattle), swelling and dying, dipping in pitch.
            float env = Mathf.Sin(Mathf.PI * Mathf.Clamp01(t / 0.42f)) * Mathf.Clamp01(t * 60f);
            float pitch = Mathf.Lerp(330f, 240f, t / 0.45f);
            phase += pitch / Rate;
            float buzz = (Mathf.Repeat(phase, 1f) < 0.3f ? 1f : -0.4f);
            float rattle = 0.5f + 0.5f * Mathf.Sin(TwoPi * 32f * t);
            lp += (buzz * rattle * env - lp) * 0.25f;
            return lp + Ring(t, 0f, 520f, 30f) * 0.15f * env;
        }, 0.8f);
    }

    static AudioClip MakeSplash()
    {
        float lp = 0f;
        return Shot("Splash", 0.7f, t =>
        {
            // The plop (a quick falling tone), then a spray of water noise settling.
            float plop = t < 0.06f ? Mathf.Sin(TwoPi * Mathf.Lerp(900f, 300f, t / 0.06f) * t) * (1f - t / 0.06f) : 0f;
            lp += (White() - lp) * 0.3f;
            float spray = lp * Mathf.Exp(-t * 7f) * Mathf.Clamp01((t - 0.01f) * 80f);
            return plop * 0.8f + spray * 0.9f + Ring(t, 0.02f, 1400f, 25f) * 0.1f;
        }, 0.8f);
    }

    static AudioClip MakeKnocks()
    {
        float[] at = { 0f, 0.46f, 0.95f };
        int n = (int)(3.2f * Rate);
        var dry = new float[n];
        float lp = 0f;
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)Rate, v = 0f;
            for (int k = 0; k < at.Length; k++)
            {
                float hard = k == at.Length - 1 ? 1.15f : 1f; // the last one a little harder
                v += (Ring(t, at[k], 92f, 22f) * 1f + Ring(t, at[k], 178f, 34f) * 0.8f + Ring(t, at[k], 315f, 50f) * 0.45f
                      + Ring(t, at[k], 640f, 90f) * 0.2f) * hard;
                v += Burst(t, at[k], 0.012f) * 0.5f * hard;
            }
            lp += (v - lp) * 0.35f; // muffled, through the trees
            dry[i] = lp;
        }
        var wet = Reverb(dry, 0.8f, 0.45f);
        for (int i = 0; i < n; i++) dry[i] = Tanh((dry[i] + wet[i] * 1.1f) * 1.2f);
        return Create("Knocks", dry, 0.9f);
    }

    /// <summary>Moths round a light: soft, papery wingbeats, many at once, ticking against the lens. Loops.</summary>
    public static AudioClip MothFlutter => mothFlutter != null ? mothFlutter : (mothFlutter = Take(nameof(mothFlutter)) ?? MakeMothFlutter());
    static AudioClip mothFlutter;
    /// <summary>
    /// The blind one chanting: deep voices droning something like words in no language, slow and steady,
    /// echoing. Four different chants (variant 0-3).
    /// </summary>
    public static AudioClip BlindChant(int variant)
    {
        switch (((variant % 4) + 4) % 4)
        {
            case 0: return blindChant0 != null ? blindChant0 : (blindChant0 = Take(nameof(blindChant0)) ?? MakeBlindChant0());
            case 1: return blindChant1 != null ? blindChant1 : (blindChant1 = Take(nameof(blindChant1)) ?? MakeBlindChant1());
            case 2: return blindChant2 != null ? blindChant2 : (blindChant2 = Take(nameof(blindChant2)) ?? MakeBlindChant2());
            default: return blindChant3 != null ? blindChant3 : (blindChant3 = Take(nameof(blindChant3)) ?? MakeBlindChant3());
        }
    }
    static AudioClip blindChant0, blindChant1, blindChant2, blindChant3, giantChant;

    /// <summary>
    /// The giant, just landed, chanting: a few words, short and enormous, in a huge low choir of voices (a
    /// fifth above ringing with them), the echo going on and on.
    /// </summary>
    public static AudioClip GiantChant => giantChant != null ? giantChant : (giantChant = Take(nameof(giantChant)) ?? MakeGiantChant());
    static AudioClip MakeGiantChant() => MakeChant("Giant Chant", 777, true);
    static AudioClip MakeBlindChant0() => MakeChant("Blind Chant 0", 101);
    static AudioClip MakeBlindChant1() => MakeChant("Blind Chant 1", 202);
    static AudioClip MakeBlindChant2() => MakeChant("Blind Chant 2", 303);
    static AudioClip MakeBlindChant3() => MakeChant("Blind Chant 3", 404);

    /// <summary>Gusting wind. Loops seamlessly.</summary>
    public static AudioClip Wind => wind != null ? wind : (wind = Take(nameof(wind)) ?? MakeWind());
    /// <summary>High, dissonant shimmer that fades in when an entity is near. Loops seamlessly.</summary>
    public static AudioClip Tension => tension != null ? tension : (tension = Take(nameof(tension)) ?? MakeTension());
    public static AudioClip TwigSnap => twigSnap != null ? twigSnap : (twigSnap = Take(nameof(twigSnap)) ?? MakeTwigSnap());
    public static AudioClip Moan => moan != null ? moan : (moan = Take(nameof(moan)) ?? MakeMoan());

    // ---------------------------------------------------------------- Player

    static readonly AudioClip[] footsteps = new AudioClip[4];

    /// <summary>A soft footstep on dirt and leaves. There are a few variants (index wraps) so steps don't repeat.</summary>
    /// <summary>A footstep in water: a slap, a slosh, a few bubbles.</summary>
    public static AudioClip Splash => Wade(0);

    static AudioClip radioBroadcast, cameraFlash, rainLoop, carChirp, fobClick, tornadoRoar, pauseSong, dispelBuild, dispel, taken, flarePop, blindGrowl, blindAlert, wadeLoop;
    /// <summary>
    /// Moving through water: a soft, continuous swish and lap round the legs, surging and easing (loops).
    /// Footsteps plays it under the wading steps, as loud as you're moving through it.
    /// </summary>
    public static AudioClip WadeLoop => wadeLoop != null ? wadeLoop : (wadeLoop = Take(nameof(wadeLoop)) ?? MakeWadeLoop());
    /// <summary>The blind one, to itself: a long, low, wet growl rattling in its chest, rising and sinking (2.8s).</summary>
    public static AudioClip BlindGrowl => blindGrowl != null ? blindGrowl : (blindGrowl = Take(nameof(blindGrowl)) ?? MakeBlindGrowl());
    /// <summary>The blind one hearing something: a sharp sucked-in breath snapping into a short, ragged snarl.</summary>
    public static AudioClip BlindAlert => blindAlert != null ? blindAlert : (blindAlert = Take(nameof(blindAlert)) ?? MakeBlindAlert());
    /// <summary>
    /// A scour being faced down (2.5s, pitched to the crescendo's length): a ghostly cluster of glassy tones
    /// rising and trembling faster and faster over a long, breathy inhale.
    /// </summary>
    public static AudioClip DispelBuild => dispelBuild != null ? dispelBuild : (dispelBuild = Take(nameof(dispelBuild)) ?? MakeDispelBuild());
    /// <summary>
    /// A scour driven off: a deep, soft whump, a ghostly exhale echoing away, and a high glassy ring dying.
    /// </summary>
    public static AudioClip Dispel => dispel != null ? dispel : (dispel = Take(nameof(dispel)) ?? MakeDispel());
    /// <summary>
    /// Caught: a heavy low impact under a swelling, dissonant cluster of strings, a metallic scrape sliding
    /// down, all sinking away into the dark.
    /// </summary>
    public static AudioClip Taken => taken != null ? taken : (taken = Take(nameof(taken)) ?? MakeTaken());
    /// <summary>A flare bursting at the top of its climb: a sharp crack, a deep thump, and a crackling hiss of sparks.</summary>
    public static AudioClip FlarePop => flarePop != null ? flarePop : (flarePop = Take(nameof(flarePop)) ?? MakeFlarePop());
    /// <summary>
    /// The pause menu's song: a slow, music-box lullaby in a minor key on a warped old record, played down a
    /// bad radio line: wobbling, sagging, skipping, crackling, a hollow voice humming under it. Loops.
    /// </summary>
    public static AudioClip PauseSong => pauseSong != null ? pauseSong : (pauseSong = Take(nameof(pauseSong)) ?? MakePauseSong());
    /// <summary>A tornado far off: a low, endless, rushing roar, swelling and fading. Loops.</summary>
    public static AudioClip TornadoRoar => tornadoRoar != null ? tornadoRoar : (tornadoRoar = Take(nameof(tornadoRoar)) ?? MakeTornadoRoar());
    /// <summary>A car answering its key fob: two sharp, bright chirps.</summary>
    public static AudioClip CarChirp => carChirp != null ? carChirp : (carChirp = Take(nameof(carChirp)) ?? MakeCarChirp());
    /// <summary>The little click of a key fob's button.</summary>
    public static AudioClip FobClick => fobClick != null ? fobClick : (fobClick = Take(nameof(fobClick)) ?? MakeFobClick());
    /// <summary>Light rain on the forest: a soft hiss and a patter of drops on leaves and ground. Loops.</summary>
    public static AudioClip RainLoop => rainLoop != null ? rainLoop : (rainLoop = Take(nameof(rainLoop)) ?? MakeRainLoop());
    /// <summary>An old radio on a bad station: a scratchy minor waltz through hiss and crackle, drifting in and out. Loops.</summary>
    public static AudioClip RadioBroadcast => radioBroadcast != null ? radioBroadcast : (radioBroadcast = Take(nameof(radioBroadcast)) ?? MakeRadioBroadcast());
    /// <summary>A film camera's flash: the shutter's clack, the pop of the bulb, and the whine of it charging again.</summary>
    public static AudioClip CameraFlash => cameraFlash != null ? cameraFlash : (cameraFlash = Take(nameof(cameraFlash)) ?? MakeCameraFlash());

    const int WaterStepVariants = 6;
    static readonly AudioClip[] wades = new AudioClip[WaterStepVariants], puddleSteps = new AudioClip[WaterStepVariants];

    /// <summary>A step wading through deeper water: a full plunge, bubbles and a swirling slosh. Several variants.</summary>
    public static AudioClip Wade(int variant)
    {
        int i = ((variant % WaterStepVariants) + WaterStepVariants) % WaterStepVariants;
        return wades[i] != null ? wades[i] : (wades[i] = MakeWaterStep("Wade " + i, i, true));
    }

    /// <summary>A step in shallow water: a quick, bright slap and a few droplets. Several variants.</summary>
    public static AudioClip PuddleStep(int variant)
    {
        int i = ((variant % WaterStepVariants) + WaterStepVariants) % WaterStepVariants;
        return puddleSteps[i] != null ? puddleSteps[i] : (puddleSteps[i] = MakeWaterStep("Puddle Step " + i, i, false));
    }
    public static int WaterStepCount => WaterStepVariants;

    public static AudioClip Footstep(int variant)
    {
        int i = ((variant % footsteps.Length) + footsteps.Length) % footsteps.Length;
        return footsteps[i] != null ? footsteps[i] : (footsteps[i] = MakeFootstep(i));
    }

    // ---------------------------------------------------------------- Flashlight

    /// <summary>Plasticky switch click.</summary>
    public static AudioClip FlashlightClick => flashlightClick != null ? flashlightClick : (flashlightClick = Take(nameof(flashlightClick)) ?? MakeFlashlightClick());
    /// <summary>Battery sliding in and snapping into place, with a little charge-up whine.</summary>
    public static AudioClip BatteryInsert => batteryInsert != null ? batteryInsert : (batteryInsert = Take(nameof(batteryInsert)) ?? MakeBatteryInsert());

    // ---------------------------------------------------------------- Shacks & fire

    /// <summary>Old hinges groaning as a door swings.</summary>
    public static AudioClip DoorCreak => doorCreak != null ? doorCreak : (doorCreak = Take(nameof(doorCreak)) ?? MakeDoorCreak());
    /// <summary>Something picked up and stuffed in a pocket.</summary>
    public static AudioClip Pickup => pickup != null ? pickup : (pickup = Take(nameof(pickup)) ?? MakePickup());
    /// <summary>Flint wheel rasp, then the flame catching.</summary>
    public static AudioClip LighterFlick => lighterFlick != null ? lighterFlick : (lighterFlick = Take(nameof(lighterFlick)) ?? MakeLighterFlick());
    /// <summary>A torch head catching with a whoomph.</summary>
    public static AudioClip TorchIgnite => torchIgnite != null ? torchIgnite : (torchIgnite = Take(nameof(torchIgnite)) ?? MakeTorchIgnite());
    /// <summary>Crackling, popping flame. Loops seamlessly.</summary>
    public static AudioClip FireLoop => fireLoop != null ? fireLoop : (fireLoop = Take(nameof(fireLoop)) ?? MakeFireLoop());
    /// <summary>A heavy relay slamming shut and a big light buzzing to life.</summary>
    public static AudioClip Floodlight => floodlight != null ? floodlight : (floodlight = Take(nameof(floodlight)) ?? MakeFloodlight());
    /// <summary>A flare gun firing: a hard pop and a rising whoosh.</summary>
    public static AudioClip FlareShot => flareShot != null ? flareShot : (flareShot = Take(nameof(flareShot)) ?? MakeFlareShot());
    /// <summary>A flare burning: an angry fizzing hiss with crackles. Loops seamlessly.</summary>
    public static AudioClip FlareBurn => flareBurn != null ? flareBurn : (flareBurn = Take(nameof(flareBurn)) ?? MakeFlareBurn());
    /// <summary>A flame sputtering out.</summary>
    public static AudioClip BurnOut => burnOut != null ? burnOut : (burnOut = Take(nameof(burnOut)) ?? MakeBurnOut());

    // ---------------------------------------------------------------- The Watcher

    /// <summary>A long, anguished human scream from somewhere in the trees.</summary>
    public static AudioClip Scream => scream != null ? scream : (scream = Take(nameof(scream)) ?? MakeScream());
    /// <summary>A vast, low, dissonant swell as the eye in the sky opens.</summary>
    public static AudioClip EyeOpen => eyeOpen != null ? eyeOpen : (eyeOpen = Take(nameof(eyeOpen)) ?? MakeEyeOpen());
    /// <summary>Thunder-like rumbling from far away.</summary>
    public static AudioClip DistantRumble => distantRumble != null ? distantRumble : (distantRumble = Take(nameof(distantRumble)) ?? MakeDistantRumble());
    /// <summary>Thunder: a ripping crack, then a long, rolling, uneven rumble that dies away.</summary>
    public static AudioClip Thunder => thunder != null ? thunder : (thunder = Take(nameof(thunder)) ?? MakeThunder());
    /// <summary>One colossal footfall: a deep boom with a long rolling tail.</summary>
    public static AudioClip GiantStep => giantStep != null ? giantStep : (giantStep = Take(nameof(giantStep)) ?? MakeGiantStep());

    // ---------------------------------------------------------------- Wildlife

    /// <summary>A rat's quick, high chirps.</summary>
    public static AudioClip Squeak => squeak != null ? squeak : (squeak = Take(nameof(squeak)) ?? MakeSqueak());
    /// <summary>Tiny claws scrabbling over leaves.</summary>
    public static AudioClip Skitter => skitter != null ? skitter : (skitter = Take(nameof(skitter)) ?? MakeSkitter());
    /// <summary>Leathery wing beats passing overhead.</summary>
    public static AudioClip WingFlaps => wingFlaps != null ? wingFlaps : (wingFlaps = Take(nameof(wingFlaps)) ?? MakeWingFlaps());
    /// <summary>A crow's harsh, rasping caw.</summary>
    public static AudioClip Caw => caw != null ? caw : (caw = Take(nameof(caw)) ?? MakeCaw());
    /// <summary>A deer's low, short grunt: a couple of throaty pulses.</summary>
    public static AudioClip DeerGrunt => deerGrunt != null ? deerGrunt : (deerGrunt = Take(nameof(deerGrunt)) ?? MakeDeerGrunt());
    /// <summary>A deer's nasal bleat, rising then cracking.</summary>
    public static AudioClip DeerBleat => deerBleat != null ? deerBleat : (deerBleat = Take(nameof(deerBleat)) ?? MakeDeerBleat());
    /// <summary>A sharp snort through the nose, a blast of air.</summary>
    public static AudioClip DeerSnort => deerSnort != null ? deerSnort : (deerSnort = Take(nameof(deerSnort)) ?? MakeDeerSnort());
    /// <summary>A tawny owl's call: a low hoot, a pause, then a long wavering hoo-hoo-hooo.</summary>
    public static AudioClip OwlHoot => owlHoot != null ? owlHoot : (owlHoot = Take(nameof(owlHoot)) ?? MakeOwlHoot());
    /// <summary>A barn owl's screech: a harsh, breathy rasp that trails off.</summary>
    public static AudioClip OwlScreech => owlScreech != null ? owlScreech : (owlScreech = Take(nameof(owlScreech)) ?? MakeOwlScreech());
    /// <summary>A deer's sharp alarm snort.</summary>
    public static AudioClip DeerAlarm => deerAlarm != null ? deerAlarm : (deerAlarm = Take(nameof(deerAlarm)) ?? MakeDeerAlarm());
    /// <summary>Hooves drumming the ground at a gallop. Loops seamlessly.</summary>
    public static AudioClip Gallop => gallop != null ? gallop : (gallop = Take(nameof(gallop)) ?? MakeGallop());

    // ---------------------------------------------------------------- Weather

    /// <summary>Leaves and needles rustling in the wind. Loops seamlessly.</summary>
    public static AudioClip Leaves => leaves != null ? leaves : (leaves = Take(nameof(leaves)) ?? MakeLeaves());
    /// <summary>Pushing through a bush: a few leafy swishes and the odd twig snapping.</summary>
    public static AudioClip BushRustle => bushRustle != null ? bushRustle : (bushRustle = Take(nameof(bushRustle)) ?? MakeBushRustle());
    /// <summary>A trunk cracking through: a sharp report, then splintering.</summary>
    public static AudioClip TreeSnap => treeSnap != null ? treeSnap : (treeSnap = Take(nameof(treeSnap)) ?? MakeTreeSnap());
    /// <summary>A tree crashing to the ground: deep thud and smashing branches.</summary>
    public static AudioClip TreeImpact => treeImpact != null ? treeImpact : (treeImpact = Take(nameof(treeImpact)) ?? MakeTreeImpact());
    /// <summary>A trunk groaning as it bends.</summary>
    public static AudioClip TreeCreak => treeCreak != null ? treeCreak : (treeCreak = Take(nameof(treeCreak)) ?? MakeTreeCreak());

    // ---------------------------------------------------------------- Entity

    /// <summary>Breathy whisper with a low growl underneath. Loops seamlessly.</summary>
    public static AudioClip Whisper => whisper != null ? whisper : (whisper = Take(nameof(whisper)) ?? MakeWhisper());
    public static AudioClip Materialize => materialize != null ? materialize : (materialize = Take(nameof(materialize)) ?? MakeMaterialize());
    public static AudioClip Crescendo => crescendo != null ? crescendo : (crescendo = Take(nameof(crescendo)) ?? MakeCrescendo());
    public static AudioClip Poof => poof != null ? poof : (poof = Take(nameof(poof)) ?? MakePoof());
    /// <summary>Rustle and crackle of something moving through the undergrowth.</summary>
    public static AudioClip Creep => creep != null ? creep : (creep = Take(nameof(creep)) ?? MakeCreep());
    /// <summary>Slow, heavy breathing with a rasp: a long inhale, a pause, a ragged exhale. Loops seamlessly.</summary>
    public static AudioClip Breath => breath != null ? breath : (breath = Take(nameof(breath)) ?? MakeBreath());
    public static AudioClip Screech => screech != null ? screech : (screech = Take(nameof(screech)) ?? MakeScreech());
    /// <summary>Pounding heartbeat under a deep, swelling groan: something is coming. Loops seamlessly.</summary>
    public static AudioClip Advance => advance != null ? advance : (advance = Take(nameof(advance)) ?? MakeAdvance());

    // ---------------------------------------------------------------- Lamps

    public static AudioClip LampClick => lampClick != null ? lampClick : (lampClick = Take(nameof(lampClick)) ?? MakeLampClick());
    public static AudioClip LampIgnite => lampIgnite != null ? lampIgnite : (lampIgnite = Take(nameof(lampIgnite)) ?? MakeLampIgnite());
    /// <summary>Electrical mains hum. Loops seamlessly.</summary>
    public static AudioClip LampHum => lampHum != null ? lampHum : (lampHum = Take(nameof(lampHum)) ?? MakeLampHum());
    /// <summary>Short electrical crackle when the lamp flickers.</summary>
    public static AudioClip LampSpark => lampSpark != null ? lampSpark : (lampSpark = Take(nameof(lampSpark)) ?? MakeLampSpark());
    /// <summary>Buzz winding down to a clunk as the lamp dies.</summary>
    public static AudioClip LampPowerDown => lampPowerDown != null ? lampPowerDown : (lampPowerDown = Take(nameof(lampPowerDown)) ?? MakeLampPowerDown());

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

        if (capture != null) { capture.Add((name, data)); return null; } // (in the background: just the samples)
        var clip = AudioClip.Create(name, data.Length, 1, Rate, false);
        clip.SetData(data, 0);
        return clip;
    }

    static float Sin(float hz, float t) => Mathf.Sin(TwoPi * hz * t);
    static float Tanh(float x) => (float)System.Math.Tanh(x);

    /// <summary>A rift's hum: a low, wavering throb with a thin, glassy shimmer over it. Loops.</summary>
    public static AudioClip RiftHum => riftHum != null ? riftHum : (riftHum = Take(nameof(riftHum)) ?? MakeRiftHum());
    static AudioClip riftHum;

    static AudioClip MakeRiftHum()
    {
        float lp = 0f;
        return Loop("Rift Hum", 8f, t =>
        {
            lp += (White() - lp) * 0.05f;
            float throb = 0.6f + 0.4f * Sin(1.5f, t);
            float shimmer = 0.5f + 0.5f * Sin(0.25f, t);
            return 0.35f * Sin(48f, t) * throb
                 + 0.22f * Sin(48.6f, t)
                 + 0.12f * Sin(96.5f, t) * throb
                 + 0.05f * Sin(1312f, t) * shimmer
                 + 0.04f * Sin(1318f, t) * (1f - shimmer)
                 + 0.03f * Sin(1975f, t) * Sin(0.5f, t)
                 + lp * 0.35f * throb;
        }, 0.7f);
    }

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

    static AudioClip MakeFootstep(int variant)
    {
        const float len = 0.28f;
        float lp = 0f, hp = 0f, crunch = 0f, phase = 0f;
        float thudHz = 70f + variant * 9f;
        float crunchiness = 0.002f + variant * 0.0007f;
        return Shot("Footstep " + variant, len, t =>
        {
            // Heel then toe: a soft thud, and the crunch of leaves and grit underfoot.
            phase += TwoPi * thudHz / Rate;
            float thud = Mathf.Sin(phase) * Mathf.Exp(-t * 35f) + Mathf.Sin(phase * 1.3f) * Burst(t, 0.07f) * 0.3f;
            float w = White();
            lp += (w - lp) * 0.35f;
            hp += (lp - hp) * 0.1f;
            float scuff = (lp - hp) * (Mathf.Exp(-t * 25f) + 0.6f * Burst(t, 0.07f) * 20f / 25f);
            if (rng.NextDouble() < crunchiness * Mathf.Exp(-t * 12f)) crunch = (float)(rng.NextDouble() * 0.6 + 0.2);
            crunch *= 0.94f;
            return thud * 0.7f + scuff + crunch * w;
        }, 0.8f);
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

    static AudioClip MakeCaw()
    {
        const float len = 0.42f;
        float phase = 0f, y1 = 0f, y2 = 0f, z1 = 0f, z2 = 0f;
        float r = 0.97f;
        float c1 = 2f * r * Mathf.Cos(TwoPi * 1200f / Rate);
        float c2 = 2f * r * Mathf.Cos(TwoPi * 2600f / Rate);
        return Shot("Caw", len, t =>
        {
            float p = t / len;
            // A buzzy, rough source, pitch sagging, through two nasal resonances.
            phase += (520f - 120f * p + 25f * White()) / Rate;
            float source = (2f * (phase % 1f) - 1f) + White() * 0.4f;
            float a = c1 * y1 - r * r * y2 + source; y2 = y1; y1 = a;
            float b = c2 * z1 - r * r * z2 + source; z2 = z1; z1 = b;
            float env = Mathf.Sin(Mathf.PI * Mathf.Clamp01(p * 1.2f)) * (p < 0.08f ? p / 0.08f : 1f);
            return (a + b * 0.7f) * env;
        }, 0.8f);
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

    static AudioClip MakeDoorCreak()
    {
        const float len = 1.3f;
        float y1 = 0f, y2 = 0f, z1 = 0f, z2 = 0f, pulse = 0f;
        float r = 0.994f;
        float c1 = 2f * r * Mathf.Cos(TwoPi * 850f / Rate);
        float c2 = 2f * r * Mathf.Cos(TwoPi * 1550f / Rate);
        return Shot("Door Creak", len, t =>
        {
            float p = t / len;
            // Stick-slip friction in the hinge: the creak rises and falls in pitch as the door swings.
            float rateHz = (60f + 50f * Mathf.Sin(Mathf.PI * p)) * (1f + 0.2f * White());
            pulse += rateHz / Rate;
            float x = 0f;
            if (pulse >= 1f) { pulse -= 1f; x = 1f; }
            float a = c1 * y1 - r * r * y2 + x; y2 = y1; y1 = a;
            float b = c2 * z1 - r * r * z2 + x; z2 = z1; z1 = b;
            return (a + b * 0.6f) * Mathf.Sin(Mathf.PI * p);
        }, 0.7f);
    }

    // ---------------------------------------------------------------- Doors

    /// <summary>A struck resonance starting at `start`: a sine at f dying away at `decay` per second.</summary>
    static float Ring(float t, float start, float f, float decay)
    {
        float u = t - start;
        return u < 0f ? 0f : Mathf.Sin(TwoPi * f * u) * Mathf.Exp(-u * decay) * Mathf.Clamp01(u * 2000f);
    }

    /// <summary>A short burst of noise at `start`, `length` long, sharp at the front.</summary>
    static float Burst(float t, float start, float length)
    {
        float u = t - start;
        return u < 0f || u > length ? 0f : White() * (1f - u / length) * (1f - u / length);
    }

    /// <summary>
    /// A hinge creaking from `start` to `end`: stick-slip friction (a train of clicks speeding up and slowing
    /// down as it swings) ringing a squeaky metal pair and a woody body. Its state lives in the arrays given.
    /// </summary>
    static float Creak(float t, float start, float end, float slowest, float fastest, float squeak, float[] s)
    {
        if (t < start || t > end) return 0f;
        float p = (t - start) / (end - start);
        float rateHz = Mathf.Lerp(slowest, fastest, Mathf.Sin(Mathf.PI * Mathf.Pow(p, 0.8f))) * (1f + 0.25f * White());
        s[0] += rateHz / Rate;
        float x = 0f;
        if (s[0] >= 1f) { s[0] -= 1f; x = 1f; }
        // Two narrow metal resonances (the squeal), two broad wooden ones (the door's body), in the state slots.
        float Reso(int i, float f, float r)
        {
            float c = 2f * r * Mathf.Cos(TwoPi * f / Rate);
            float y = c * s[i] - r * r * s[i + 1] + x;
            s[i + 1] = s[i];
            s[i] = y;
            return y;
        }
        float metal = Reso(1, squeak, 0.995f) + 0.6f * Reso(3, squeak * 1.83f, 0.993f);
        float wood = Reso(5, 210f, 0.985f) * 0.8f + Reso(7, 470f, 0.98f) * 0.5f;
        float env = Mathf.Sin(Mathf.PI * p) * (0.6f + 0.4f * Mathf.Sin(TwoPi * 1.7f * (t - start)));
        return (metal * 0.012f + wood * 0.02f) * env;
    }

    static AudioClip MakeDoorOpen()
    {
        var s = new float[9];
        float air = 0f;
        return Shot("Door Open", 1.7f, t =>
        {
            // The latch: a clack, the bolt springing back.
            float latch = Burst(t, 0f, 0.012f) * 0.6f + Ring(t, 0f, 2600f, 70f) * 0.35f + Ring(t, 0.004f, 4100f, 90f) * 0.2f
                          + Burst(t, 0.055f, 0.008f) * 0.35f + Ring(t, 0.055f, 3100f, 80f) * 0.2f;
            float knock = Ring(t, 0f, 180f, 30f) * 0.3f; // the door shifting in its frame
            air += (White() - air) * 0.02f;
            float whoosh = air * Mathf.Sin(Mathf.PI * Mathf.Clamp01((t - 0.15f) / 1.4f)) * 0.5f;
            return latch + knock + whoosh + Creak(t, 0.12f, 1.6f, 25f, 95f, 780f, s);
        }, 0.8f);
    }

    static AudioClip MakeDoorClose()
    {
        var s = new float[9];
        return Shot("Door Close", 0.9f, t => Creak(t, 0f, 0.85f, 20f, 70f, 620f, s), 0.7f);
    }

    static AudioClip MakeDoorShut()
    {
        float lp = 0f;
        return Shot("Door Shut", 0.8f, t =>
        {
            // The bang: a deep, hollow thud of wood on wood, the frame ringing dully.
            float thud = Ring(t, 0f, 68f, 14f) * 1.2f + Ring(t, 0f, 132f, 18f) * 0.6f
                         + Ring(t, 0f, 190f, 26f) * 0.5f + Ring(t, 0f, 410f, 40f) * 0.3f + Ring(t, 0f, 760f, 60f) * 0.15f;
            lp += (Burst(t, 0f, 0.05f) - lp) * 0.2f;
            // The latch dropping in, and a little rattle.
            float latch = Burst(t, 0.035f, 0.006f) * 0.4f + Ring(t, 0.035f, 2800f, 90f) * 0.25f
                          + Ring(t, 0.11f, 3300f, 120f) * 0.08f;
            return Tanh((thud + lp * 1.5f + latch) * 1.3f);
        }, 0.9f);
    }

    static AudioClip MakeDoorRattle()
    {
        float[] at = { 0f, 0.085f, 0.15f, 0.29f, 0.37f, 0.45f };
        return Shot("Door Rattle", 0.75f, t =>
        {
            float s = 0f;
            for (int k = 0; k < at.Length; k++)
            {
                float level = k % 2 == 0 ? 1f : 0.65f;
                s += (Burst(t, at[k], 0.006f) * 0.4f + Ring(t, at[k], 2300f + k * 170f, 100f) * 0.22f
                      + Ring(t, at[k], 160f + k * 9f, 35f) * 0.45f) * level; // the handle, and the door knocking its frame
            }
            return Tanh(s * 1.4f);
        }, 0.8f);
    }

    static AudioClip MakeCarDoorOpen()
    {
        var s = new float[9];
        return Shot("Car Door Open", 0.6f, t =>
        {
            float pop = Burst(t, 0f, 0.008f) * 0.5f + Ring(t, 0f, 2200f, 45f) * 0.3f + Ring(t, 0f, 3700f, 60f) * 0.2f
                        + Ring(t, 0f, 120f, 25f) * 0.4f;
            return pop + Creak(t, 0.12f, 0.55f, 40f, 120f, 1800f, s) * 0.6f;
        }, 0.75f);
    }

    static AudioClip MakeCarDoorShut()
    {
        float lp = 0f;
        return Shot("Car Door Shut", 0.9f, t =>
        {
            // Heavy metal: a deep thunk, the panel ringing tinnily, the latch catching.
            float thunk = Ring(t, 0f, 86f, 12f) * 1.2f + Ring(t, 0f, 172f, 18f) * 0.5f;
            float panel = Ring(t, 0f, 380f, 8f) * 0.25f + Ring(t, 0f, 910f, 14f) * 0.15f + Ring(t, 0f, 1730f, 22f) * 0.08f;
            lp += (Burst(t, 0f, 0.04f) - lp) * 0.25f;
            float latch = Burst(t, 0.02f, 0.005f) * 0.35f + Ring(t, 0.02f, 3000f, 80f) * 0.2f;
            return Tanh((thunk + panel + lp * 1.4f + latch) * 1.4f);
        }, 0.9f);
    }

    static AudioClip MakeGloveBoxOpen()
    {
        return Shot("Glove Box Open", 0.45f, t =>
            Burst(t, 0f, 0.004f) * 0.5f + Ring(t, 0f, 1650f, 140f) * 0.3f
            + Ring(t, 0.13f, 160f, 30f) * 0.5f + Ring(t, 0.13f, 330f, 45f) * 0.25f + Burst(t, 0.13f, 0.02f) * 0.15f, 0.7f);
    }

    static AudioClip MakeGloveBoxShut()
    {
        return Shot("Glove Box Shut", 0.3f, t =>
            Burst(t, 0f, 0.005f) * 0.6f + Ring(t, 0f, 2200f, 160f) * 0.3f + Ring(t, 0f, 300f, 50f) * 0.45f, 0.75f);
    }

    static AudioClip snackEat;
    /// <summary>A crinkly wrapper torn open, then a few crunchy bites.</summary>
    public static AudioClip SnackEat => snackEat != null ? snackEat : (snackEat = Take(nameof(snackEat)) ?? MakeSnackEat());

    static AudioClip MakeSnackEat()
    {
        float lp = 0f, hp = 0f;
        float[] bites = { 0.75f, 1.15f, 1.5f, 1.95f, 2.3f };
        return Shot("Snack Eat", 2.7f, t =>
        {
            float w = White();
            lp += (w - lp) * 0.35f;
            hp = w - lp;
            // The wrapper: bright crackly rustling, in bursts, for the first half second.
            float crinkle = t < 0.6f ? hp * (0.4f + 0.6f * Mathf.Abs(Mathf.Sin(t * 37f) * Mathf.Sin(t * 11f))) * Mathf.Sin(Mathf.PI * t / 0.6f) : 0f;
            // Bites: a sharp crunch, then a dull chew.
            float crunch = 0f;
            foreach (float b in bites)
            {
                if (t < b || t > b + 0.3f) continue;
                float u = t - b;
                crunch += (w * Mathf.Exp(-u * 40f) * 0.9f + lp * Mathf.Exp(-u * 9f) * 0.6f) * (0.7f + 0.3f * Mathf.Sin(u * 230f));
            }
            return crinkle * 0.6f + crunch;
        }, 0.6f);
    }

    static AudioClip MakePickup()
    {
        float lp = 0f;
        return Shot("Pickup", 0.35f, t =>
        {
            float w = White();
            lp += (w - lp) * 0.2f;
            float rustle = lp * Mathf.Sin(Mathf.PI * Mathf.Clamp01(t / 0.25f)) * 0.7f;
            float clink = Sin(2600f, t) * Burst(t, 0.22f) * 0.6f;
            return rustle + clink;
        }, 0.7f);
    }

    static AudioClip MakeLighterFlick()
    {
        float lp = 0f;
        return Shot("Lighter Flick", 0.5f, t =>
        {
            float w = White();
            lp += (w - lp) * 0.08f;
            float rasp = t < 0.07f ? (w - lp) * (0.5f + 0.5f * Sin(180f, t)) : 0f;   // flint wheel
            float click = Burst(t, 0.07f) * w;
            float catchFlame = t > 0.08f ? lp * 1.5f * Mathf.Exp(-(t - 0.08f) * 6f) : 0f; // soft whoosh
            return rasp + click + catchFlame;
        }, 0.7f);
    }

    static AudioClip MakeTorchIgnite()
    {
        const float len = 1.2f;
        float lp = 0f, phase = 0f;
        return Shot("Torch Ignite", len, t =>
        {
            float w = White();
            lp += (w - lp) * (0.03f + 0.1f * Mathf.Exp(-t * 4f));
            float env = t < 0.15f ? t / 0.15f : Mathf.Exp(-(t - 0.15f) * 2.5f);
            phase += TwoPi * (60f + 30f * Mathf.Exp(-t * 5f)) / Rate;
            return (lp * 2f + Mathf.Sin(phase) * 0.4f) * env;
        }, 0.8f);
    }

    static AudioClip MakeFireLoop()
    {
        float lp = 0f, roar = 0f, pop = 0f;
        return Loop("Fire", 4f, t =>
        {
            float w = White();
            lp += (w - lp) * 0.3f;
            roar += (w - roar) * 0.02f;
            if (rng.NextDouble() < 0.0006) pop = (float)(rng.NextDouble() * 0.8 + 0.2);
            pop *= 0.985f;
            float flutter = 0.7f + 0.3f * Sin(1.5f, t) * Sin(2.25f, t);
            return (lp - roar) * 0.25f * flutter + roar * 0.6f + pop * w * 1.5f;
        }, 0.7f);
    }

    static AudioClip MakeFloodlight()
    {
        const float len = 1.5f;
        float lp = 0f, thump = 0f;
        return Shot("Floodlight", len, t =>
        {
            thump += TwoPi * (55f + 40f * Mathf.Exp(-t * 8f)) / Rate;
            float clunk = Mathf.Sin(thump) * Mathf.Exp(-t * 9f) + White() * Burst(t, 0f) * 0.8f;
            float saw = 2f * (120f * t % 1f) - 1f;
            lp += (saw - lp) * 0.15f;
            float buzz = lp * Mathf.SmoothStep(0f, 1f, t / 0.4f) * Mathf.Exp(-Mathf.Max(0f, t - 0.6f) * 2f) * 0.5f;
            return clunk + buzz;
        }, 0.9f);
    }

    static AudioClip MakeFlareShot()
    {
        const float len = 1.1f;
        float lp = 0f, hp = 0f, thump = 0f;
        return Shot("Flare Shot", len, t =>
        {
            float w = White();
            thump += TwoPi * (90f * Mathf.Exp(-t * 20f) + 40f) / Rate;
            float pop = w * Burst(t, 0f) * 1.5f + Mathf.Sin(thump) * Mathf.Exp(-t * 18f);
            lp += (w - lp) * Mathf.Lerp(0.05f, 0.4f, Mathf.Clamp01(t / len)); // whoosh brightening as it climbs
            hp += (lp - hp) * 0.05f;
            float whoosh = (lp - hp) * Mathf.Sin(Mathf.PI * Mathf.Clamp01(t / len)) * 0.8f;
            return pop + whoosh;
        }, 0.9f);
    }

    static AudioClip MakeFlareBurn()
    {
        float lp = 0f, crackle = 0f;
        return Loop("Flare Burn", 3f, t =>
        {
            float w = White();
            lp += (w - lp) * 0.5f;
            float hiss = (w - lp) * (0.8f + 0.2f * Sin(5f, t));
            if (rng.NextDouble() < 0.003) crackle = (float)rng.NextDouble();
            crackle *= 0.95f;
            return hiss * 0.6f + crackle * w;
        }, 0.7f);
    }

    static AudioClip MakeBurnOut()
    {
        const float len = 0.9f;
        float lp = 0f;
        return Shot("Burn Out", len, t =>
        {
            float w = White();
            lp += (w - lp) * 0.5f;
            float sputter = Sin(11f, t) > 0f ? 1f : 0.3f;
            return (w - lp) * Mathf.Exp(-t * 4f) * sputter;
        }, 0.6f);
    }

    static AudioClip MakeScream()
    {
        // A deep, drawn-out howl: it swells, holds and wavers, cracks, then sinks away into a long
        // moan, ringing on through the trees afterwards (the reverb is built into the clip).
        const float voiceLen = 6f, tail = 4.5f;
        int n = (int)((voiceLen + tail) * Rate);
        var dry = new float[n];

        float phase = 0f, subPhase = 0f;
        float a1 = 0f, a2 = 0f, b1 = 0f, b2 = 0f, c1 = 0f, c2 = 0f, lp = 0f;
        // Vocal tract resonances for a hollow "aw" (roughly 520, 880 and 2300 Hz): darker than an "ah".
        float r = 0.988f;
        float k1 = 2f * r * Mathf.Cos(TwoPi * 520f / Rate);
        float k2 = 2f * r * Mathf.Cos(TwoPi * 880f / Rate);
        float k3 = 2f * r * Mathf.Cos(TwoPi * 2300f / Rate);
        for (int i = 0; i < (int)(voiceLen * Rate); i++)
        {
            float t = i / (float)Rate;
            float p = t / voiceLen;
            // Low: swells up to ~230Hz, holds, wavers and cracks, then sinks to a ~85Hz moan.
            float rise = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(p / 0.15f));
            float fall = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((p - 0.45f) / 0.55f));
            float crack = p > 0.42f && p < 0.47f ? 0.85f : 1f;
            float pitch = (120f + 110f * rise - 145f * fall) * crack;
            pitch *= 1f + 0.03f * Mathf.Sin(TwoPi * 4.5f * t) + 0.015f * Mathf.Sin(TwoPi * 0.7f * t) + 0.01f * White();
            phase += pitch / Rate;
            subPhase += pitch * 0.5f / Rate;
            float voice = 2f * (phase % 1f) - 1f;
            float growl = Mathf.Sin(TwoPi * subPhase) * (0.5f + 0.5f * fall);   // an octave down, more as it sinks
            float breath = White() * (0.15f + 0.3f * fall);
            float x = voice + breath;

            float f1 = k1 * a1 - r * r * a2 + x; a2 = a1; a1 = f1;
            float f2 = k2 * b1 - r * r * b2 + x; b2 = b1; b1 = f2;
            float f3 = k3 * c1 - r * r * c2 + x; c2 = c1; c1 = f3;
            float sound = f1 + f2 * 0.6f + f3 * 0.12f;          // little of the top resonance: not shrill

            float env = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / 0.5f)) * Mathf.Pow(1f - Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((p - 0.6f) / 0.4f)), 1.3f);
            float v = Tanh(sound * 0.04f) + growl * 0.35f;      // driven gently: raw, but not harsh
            lp += (v - lp) * 0.35f;                             // take the edge off the very top
            dry[i] = lp * env;
        }

        float[] wet = Reverb(dry, 0.88f, 0.35f);
        var mixed = new float[n];
        for (int i = 0; i < n; i++) mixed[i] = dry[i] * 0.55f + wet[i] * 0.75f;
        return Create("Scream", mixed, 1f);
    }

    /// <summary>
    /// A big, dark Schroeder/Freeverb-style reverb: parallel damped combs into series allpasses.
    /// feedback sets how long it rings (0.88 = several seconds); damping darkens the tail.
    /// </summary>
    static float[] Reverb(float[] input, float feedback, float damping)
    {
        int[] combDelays = { 1557, 1617, 1491, 1422, 1277, 1356, 1188, 1116 };
        int[] allpassDelays = { 556, 441, 341, 225 };
        int preDelay = Rate * 35 / 1000;
        var output = new float[input.Length];

        foreach (int baseDelay in combDelays)
        {
            int delay = baseDelay * 2; // a larger space: double the classic room
            var buffer = new float[delay];
            int index = 0;
            float store = 0f;
            for (int i = 0; i < input.Length; i++)
            {
                float x = i >= preDelay ? input[i - preDelay] : 0f;
                float y = buffer[index];
                store = y * (1f - damping) + store * damping;
                buffer[index] = x + store * feedback;
                if (++index >= delay) index = 0;
                output[i] += y / combDelays.Length;
            }
        }

        foreach (int delay in allpassDelays)
        {
            var buffer = new float[delay];
            int index = 0;
            for (int i = 0; i < output.Length; i++)
            {
                float buffered = buffer[index];
                float x = output[i];
                output[i] = buffered - x;
                buffer[index] = x + buffered * 0.5f;
                if (++index >= delay) index = 0;
            }
        }
        return output;
    }

    static AudioClip MakeEyeOpen()
    {
        const float len = 9f;
        float rumble = 0f;
        return Shot("Eye Open", len, t =>
        {
            float p = t / len;
            float env = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(p * 2.5f)) * Mathf.SmoothStep(1f, 0f, Mathf.Clamp01((p - 0.7f) / 0.3f));
            rumble += (White() - rumble) * 0.004f;
            // Clashing low tones, slowly bending downward.
            float bend = 1f - 0.08f * p;
            float tones = Sin(43f * bend, t) + 0.8f * Sin(45.5f * bend, t) + 0.5f * Sin(64.5f * bend, t) + 0.3f * Sin(91f * bend, t);
            return (tones * 0.6f + rumble * 6f) * env;
        }, 0.9f);
    }

    static AudioClip MakeDistantRumble()
    {
        const float len = 6f;
        float lp = 0f, lp2 = 0f;
        return Shot("Distant Rumble", len, t =>
        {
            lp += (White() - lp) * 0.01f;
            lp2 += (lp - lp2) * 0.02f;
            float rolls = 0.5f + 0.5f * Mathf.PerlinNoise(t * 1.5f, 3.3f);
            float env = Mathf.Sin(Mathf.PI * t / len);
            return lp2 * rolls * env;
        }, 0.9f);
    }

    static AudioClip MakeGiantStep()
    {
        const float len = 4.5f;
        float phase = 0f, lp = 0f, lp2 = 0f;
        return Shot("Giant Step", len, t =>
        {
            phase += TwoPi * (28f + 25f * Mathf.Exp(-t * 3f)) / Rate;
            float boom = Mathf.Sin(phase) * Mathf.Exp(-t * 1.6f);
            lp += (White() - lp) * 0.015f;
            lp2 += (lp - lp2) * 0.03f;
            float rumble = lp2 * 8f * Mathf.Exp(-t * 0.9f);
            return boom + rumble;
        }, 1f);
    }

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

    static AudioClip MakeTreeSnap()
    {
        const float len = 1.4f;
        float lp = 0f, splinter = 0f, groan = 0f;
        return Shot("Tree Snap", len, t =>
        {
            float w = White();
            lp += (w - lp) * 0.5f;
            float report = (w - lp * 0.5f) * Mathf.Exp(-t * 40f) * 1.5f;              // the crack itself
            if (rng.NextDouble() < 0.004 * Mathf.Exp(-t * 2.5f))
                splinter = (float)(rng.NextDouble() * 0.8 + 0.2);                      // fibres tearing
            splinter *= 0.97f;
            groan += TwoPi * (70f + 20f * Mathf.Sin(t * 9f)) / Rate;
            float wood = Mathf.Sin(groan) * 0.3f * Mathf.Exp(-t * 2f);
            return report + splinter * w + wood;
        }, 1f);
    }

    static AudioClip MakeTreeImpact()
    {
        const float len = 2.5f;
        float phase = 0f, lp = 0f, crack = 0f;
        return Shot("Tree Impact", len, t =>
        {
            phase += TwoPi * (32f + 30f * Mathf.Exp(-t * 6f)) / Rate;
            float thud = Mathf.Sin(phase) * Mathf.Exp(-t * 3f);
            float w = White();
            lp += (w - lp) * 0.08f;
            float crash = lp * 2f * Mathf.Exp(-t * 2.2f);                               // branches smashing
            if (rng.NextDouble() < 0.003 * Mathf.Exp(-t * 1.5f)) crack = (float)rng.NextDouble();
            crack *= 0.96f;
            return thud + crash + crack * w * 0.8f;
        }, 1f);
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

    static AudioClip MakeDispelBuild()
    {
        const float len = 2.5f;
        float[] ratios = { 1f, 1.19f, 1.5f, 2.13f, 2.97f };
        float[] phases = new float[ratios.Length];
        float breath = 0f, breathLp = 0f;
        return Shot("Dispel Build", len, t =>
        {
            float p = t / len;
            float baseHz = 320f * Mathf.Pow(2f, p * 1.2f); // rising a little over an octave
            float s = 0f;
            for (int k = 0; k < ratios.Length; k++)
            {
                phases[k] += TwoPi * baseHz * ratios[k] * (1f + 0.004f * Mathf.Sin(t * (3f + k))) / Rate;
                s += Mathf.Sin(phases[k]) / (1f + k * 0.6f);
            }
            float tremolo = 0.6f + 0.4f * Mathf.Sin(TwoPi * Mathf.Lerp(3f, 16f, p * p) * t);
            // The inhale: hollow, filtered breath getting louder and brighter.
            breathLp += (White() - breathLp) * Mathf.Lerp(0.04f, 0.25f, p);
            breath = breath * 0.97f + breathLp * 0.03f;
            float env = Mathf.SmoothStep(0f, 1f, p * 1.4f);
            return s * tremolo * env * 0.5f + (breathLp - breath) * env * 1.6f;
        }, 0.8f);
    }

    static AudioClip MakeDispel()
    {
        // Not a chime: something being torn out of the world. A sharp, sucking inhale; then it glitches out
        // in a shriek that stutters and crushes as it collapses in pitch, crackling with black lightning, over
        // a deep drop; and a dark, choked breath dying away under it.
        const float len = 2.4f, at = 0.14f;
        float inhaleLp = 0f, shriekPhase = 0f, held = 0f, sub = 0f, crack = 0f, tailLp = 0f, tailHp = 0f;
        int hold = 0;
        return Shot("Dispel", len, t =>
        {
            float u = t - at;

            // The inhale: noise swelling and brightening, sucked in, cut off as it goes.
            float inhaleEnv = t < at ? Mathf.Pow(t / at, 3f) : Mathf.Exp(-u * 45f);
            inhaleLp += (White() - inhaleLp) * Mathf.Lerp(0.03f, 0.45f, Mathf.Clamp01(t / at));
            float inhale = inhaleLp * inhaleEnv * 1.3f;

            float shriek = 0f, boom = 0f, tail = 0f;
            if (u >= 0f)
            {
                // The shriek: a harsh buzz falling from a scream to a groan, chopped by a stutter that slows,
                // with a vowel-ish edge, crushed to a few levels and held in coarse steps (digital, wrong).
                float f = 170f + 760f * Mathf.Exp(-u * 3.2f);
                shriekPhase += f / Rate;
                float saw = (shriekPhase - Mathf.Floor(shriekPhase)) * 2f - 1f;
                float edge = Mathf.Sin(TwoPi * f * 2.6f * u) * 0.45f;
                float stutter = Mathf.Sin(TwoPi * (34f - 18f * Mathf.Clamp01(u / 1.2f)) * u) > -0.1f ? 1f : 0.1f;
                float env = Mathf.Exp(-u * 2f) * Mathf.Clamp01(u / 0.006f);
                float raw = (saw * 0.75f + edge) * stutter * env;
                if (--hold <= 0) { held = raw; hold = 4 + (int)(u * 28f); }
                shriek = Mathf.Round(held * 5f) / 5f;

                // The drop.
                sub += TwoPi * (64f * Mathf.Exp(-u * 4f) + 23f) / Rate;
                boom = Mathf.Sin(sub) * Mathf.Exp(-u * 2.6f) * 1.3f;

                // A choked breath going out under it all, low and dark.
                tailLp += (White() - tailLp) * 0.05f;
                tailHp += (tailLp - tailHp) * 0.01f;
                tail = (tailLp - tailHp) * Mathf.Clamp01(u / 0.3f) * Mathf.Exp(-u * 1.4f) * 1.4f;
            }

            // Black lightning: sharp, ragged crackles, thickest as it goes and dying away.
            float crackChance = u >= 0f ? 0.004f * Mathf.Exp(-u * 2.2f) : 0.0006f * inhaleEnv;
            if (rng.NextDouble() < crackChance) crack = White() >= 0f ? 1f : -1f;
            crack *= 0.93f;
            float crackle = crack * White() * 0.9f;

            return Tanh((inhale + shriek * 0.7f + boom + tail + crackle) * 1.35f);
        }, 0.9f);
    }

    static AudioClip MakeTaken()
    {
        const float len = 4.2f;
        float[] notes = { 82.4f, 87.3f, 116.5f, 123.5f, 174.6f }; // E2 F2 A#2 B2 F3: clashing semitones and a tritone
        float[] phases = new float[notes.Length];
        float lp = 0f, sub = 0f, scrapeLp = 0f, scrapeHp = 0f, hitLp = 0f;
        return Shot("Taken", len, t =>
        {
            // The impact.
            sub += TwoPi * (52f * Mathf.Exp(-t * 2.5f) + 26f) / Rate;
            hitLp += (White() - hitLp) * 0.08f;
            float hit = Mathf.Sin(sub) * Mathf.Exp(-t * 1.6f) * 1.3f + hitLp * Mathf.Exp(-t * 9f) * 2f;
            // The strings: detuned saws, swelling in fast, vibrato widening, closing down darker.
            float cluster = 0f;
            for (int k = 0; k < notes.Length; k++)
            {
                float vib = 1f + 0.006f * Mathf.Sin(TwoPi * (4.5f + k * 0.7f) * t) * Mathf.Clamp01(t);
                phases[k] += notes[k] * vib / Rate;
                float a = phases[k] - Mathf.Floor(phases[k]);
                cluster += (a * 2f - 1f) * (k == notes.Length - 1 ? 0.5f : 1f);
            }
            lp += (cluster - lp) * Mathf.Lerp(0.18f, 0.025f, Mathf.Clamp01(t / len));
            float strings = lp * Mathf.Clamp01(t / 0.06f) * Mathf.Exp(-t * 0.55f) * 0.35f;
            // The scrape: a narrow band of noise sliding down.
            float centre = Mathf.Lerp(0.35f, 0.04f, Mathf.Clamp01(t / 2.5f));
            scrapeLp += (White() - scrapeLp) * centre;
            scrapeHp += (scrapeLp - scrapeHp) * centre * 0.5f;
            float scrape = (scrapeLp - scrapeHp) * Mathf.Exp(-t * 1.1f) * Mathf.Clamp01(t / 0.15f) * 0.9f;
            return Tanh((hit + strings + scrape) * 1.2f);
        }, 0.95f);
    }

    static AudioClip MakeFlarePop()
    {
        const float len = 2.4f;
        float sub = 0f, lp = 0f, crackleLp = 0f;
        return Shot("Flare Pop", len, t =>
        {
            float crack = White() * Mathf.Exp(-t * 60f) * 1.6f;                    // the sharp crack
            sub += TwoPi * (90f * Mathf.Exp(-t * 8f) + 40f) / Rate;
            float thump = Mathf.Sin(sub) * Mathf.Exp(-t * 6f);                      // the thump
            lp += (White() - lp) * 0.3f;
            float hiss = lp * Mathf.Exp(-t * 1.6f) * 0.35f;                         // sparks hissing away
            crackleLp *= 0.9f;
            if (rng.NextDouble() < 0.004 * Mathf.Exp(-t * 1.2f)) crackleLp = White() * 1.2f; // and crackling
            return Tanh(crack + thump + hiss + crackleLp);
        }, 0.95f);
    }

    /// <summary>A growling voice: a buzzing low fundamental, rattled (the throat flapping), through two vocal-ish resonances.</summary>
    static System.Func<float, float> GrowlVoice(float baseHz, float rattleHz, float rattleDepth, System.Func<float, float> pitchCurve, System.Func<float, float> env)
    {
        float phase = 0f, lp1 = 0f, lp2 = 0f, bp = 0f, breath = 0f, jitter = 0f;
        return t =>
        {
            jitter += (White() * 0.02f - jitter) * 0.002f;
            float hz = baseHz * pitchCurve(t) * (1f + jitter);
            phase += hz / Rate;
            phase -= Mathf.Floor(phase);
            float buzz = (phase * 2f - 1f) + 0.5f * Mathf.Sin(TwoPi * phase * 2f);
            float rattle = 1f - rattleDepth * (0.5f + 0.5f * Mathf.Sin(TwoPi * rattleHz * t + Mathf.Sin(TwoPi * 3.1f * t) * 2f));
            float voiced = buzz * rattle;
            lp1 += (voiced - lp1) * 0.06f;   // the chest
            lp2 += (lp1 - lp2) * 0.06f;
            bp += (voiced - bp) * 0.25f;     // a rasp on top
            breath += (White() - breath) * 0.1f;
            return (lp2 * 1.8f + (bp - lp1) * 0.35f + breath * 0.12f * rattle) * env(t);
        };
    }

    static AudioClip MakeBlindGrowl()
    {
        const float len = 2.8f;
        var voice = GrowlVoice(62f, 24f, 0.7f,
            t => 1f + 0.12f * Mathf.Sin(Mathf.PI * t / len) + 0.03f * Mathf.Sin(TwoPi * 0.9f * t),
            t => Mathf.Clamp01(t / 0.5f) * Mathf.Clamp01((len - t) / 0.9f) * (0.75f + 0.25f * Mathf.Sin(TwoPi * 0.7f * t)));
        return Shot("Blind Growl", len, t => Tanh(voice(t) * 1.5f), 0.85f);
    }

    static AudioClip MakeBlindAlert()
    {
        const float len = 1.2f, inhale = 0.18f;
        float lp = 0f, hp = 0f;
        var snarl = GrowlVoice(105f, 31f, 0.85f,
            t => 1.25f - 0.35f * Mathf.Clamp01((t - inhale) / 0.9f),
            t => t < inhale ? 0f : Mathf.Clamp01((t - inhale) / 0.03f) * Mathf.Exp(-(t - inhale) * 2.8f));
        return Shot("Blind Alert", len, t =>
        {
            // The sharp sucked-in breath: rising, hissing noise.
            float u = Mathf.Clamp01(t / inhale);
            lp += (White() - lp) * Mathf.Lerp(0.1f, 0.5f, u);
            hp += (lp - hp) * 0.08f;
            float breath = t < inhale ? (lp - hp) * u * u * 1.4f : 0f;
            return Tanh(breath + snarl(t) * 2.2f);
        }, 0.95f);
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

    static AudioClip MakeBushRustle()
    {
        const float len = 0.9f;
        float crackle = 0f, lp = 0f, hp = 0f;
        float swishes = 2f + (float)rng.NextDouble() * 2f;
        return Shot("Bush Rustle", len, t =>
        {
            float p = t / len;
            if (rng.NextDouble() < 0.0008 * (1f - p))
                crackle = (float)(rng.NextDouble() * 0.5 + 0.2) * (White() > 0f ? 1f : -1f);
            crackle *= 0.95f;
            lp += (White() - lp) * 0.45f;
            hp += (lp - hp) * 0.12f;
            // Leaves brushing past in a few uneven swishes.
            float swish = Mathf.Pow(Mathf.Abs(Mathf.Sin(Mathf.PI * p * swishes + Mathf.Sin(p * 9f) * 0.6f)), 1.5f);
            float rustle = (lp - hp) * swish * Mathf.Sin(Mathf.PI * p) * 0.9f;
            return crackle * 0.6f + rustle;
        }, 0.8f);
    }

    static AudioClip MakeDeerGrunt()
    {
        const float len = 0.9f;
        float phase = 0f, lp = 0f;
        return Shot("Deer Grunt", len, t =>
        {
            // Two or three short, throaty pulses, each a little lower.
            float pulse = Mathf.Repeat(t, 0.3f) / 0.3f;
            int n = Mathf.FloorToInt(t / 0.3f);
            float amp = Mathf.Sin(Mathf.PI * Mathf.Clamp01(pulse * 1.4f)) * (n < 3 ? 1f - n * 0.25f : 0f);
            float pitch = 110f - n * 12f;
            phase += pitch / Rate;
            float voice = 2f * (phase % 1f) - 1f;
            lp += (voice + White() * 0.4f - lp) * 0.08f; // dark and muffled
            return lp * amp;
        }, 0.8f);
    }

    static AudioClip MakeDeerBleat()
    {
        const float len = 0.8f;
        float phase = 0f, lp = 0f;
        return Shot("Deer Bleat", len, t =>
        {
            float p = t / len;
            float env = Mathf.Clamp01(t / 0.04f) * Mathf.Pow(1f - p, 1.3f);
            // Nasal and reedy: rises, then breaks downward.
            float pitch = (p < 0.6f ? Mathf.Lerp(420f, 560f, p / 0.6f) : Mathf.Lerp(560f, 380f, (p - 0.6f) / 0.4f))
                          * (1f + 0.03f * Mathf.Sin(t * TwoPi * 22f));
            phase += pitch / Rate;
            float reed = (2f * (phase % 1f) - 1f) * 0.6f + Mathf.Sin(TwoPi * phase * 3f) * 0.3f;
            lp += (reed + White() * 0.15f - lp) * 0.3f;
            return lp * env;
        }, 0.7f);
    }

    static AudioClip MakeDeerSnort()
    {
        const float len = 0.5f;
        float lp = 0f, hp = 0f;
        return Shot("Deer Snort", len, t =>
        {
            float env = Mathf.Clamp01(t / 0.01f) * Mathf.Exp(-t * 9f);
            float w = White();
            lp += (w - lp) * 0.35f;
            hp += (lp - hp) * 0.05f;
            float flutter = 0.7f + 0.3f * Mathf.Sin(t * TwoPi * 35f); // nostrils fluttering
            return (lp - hp) * env * flutter;
        }, 0.8f);
    }

    static AudioClip MakeOwlHoot()
    {
        const float len = 3.6f;
        float phase = 0f, lp = 0f;
        // (start, length, pitch at start, pitch at end): one hoot, a pause, then the long wavering call.
        var notes = new[]
        {
            new Vector4(0.0f, 0.45f, 400f, 380f),
            new Vector4(1.25f, 0.18f, 360f, 380f),
            new Vector4(1.5f, 0.16f, 370f, 390f),
            new Vector4(1.72f, 1.2f, 390f, 330f),
        };
        return Shot("Owl Hoot", len, t =>
        {
            float amp = 0f, pitch = 380f;
            foreach (var n in notes)
            {
                float u = (t - n.x) / n.y;
                if (u < 0f || u > 1f) continue;
                amp = Mathf.Sin(Mathf.PI * Mathf.Pow(u, 0.6f));
                pitch = Mathf.Lerp(n.z, n.w, u) * (1f + 0.02f * Mathf.Sin(t * TwoPi * 7f) * u); // a waver as it draws out
            }
            phase += pitch / Rate;
            // Soft, hollow: mostly the fundamental, a touch of the octave, some breath.
            float tone = Mathf.Sin(TwoPi * phase) + 0.15f * Mathf.Sin(TwoPi * phase * 2f);
            lp += (White() - lp) * 0.1f;
            return (tone * 0.85f + lp * 0.25f) * amp;
        }, 0.8f);
    }

    static AudioClip MakeOwlScreech()
    {
        const float len = 1.4f;
        float lp = 0f, hp = 0f, phase = 0f;
        return Shot("Owl Screech", len, t =>
        {
            float p = t / len;
            float env = Mathf.Clamp01(t / 0.05f) * Mathf.Pow(1f - p, 1.6f);
            // Hissing, rasping noise with a strained, falling tone buried in it.
            float w = White();
            lp += (w - lp) * 0.55f;
            hp += (lp - hp) * 0.15f;
            float rasp = (lp - hp) * (0.7f + 0.3f * Mathf.Sin(t * TwoPi * 60f));
            phase += Mathf.Lerp(1900f, 1300f, p) / Rate;
            float tone = Mathf.Sin(TwoPi * phase) * 0.25f;
            return (rasp + tone) * env;
        }, 0.75f);
    }

    /// <summary>
    /// A step in water: a foot plunging in or slapping down. Built from the parts real splashes are made of:
    /// the dull "plosh" of the impact, a scatter of droplets falling back, bubbles ringing as they're
    /// trapped and pop (each a tiny rising whistle, low for big bubbles, high for small), and, wading, the
    /// slosh of water swirling round the leg. Every variant is a different random splash.
    /// </summary>
    static AudioClip MakeWaterStep(string name, int seed, bool deep)
    {
        var r = new System.Random(seed * 7919 + (deep ? 104729 : 0));
        float R() => (float)r.NextDouble();
        float N() => (float)(r.NextDouble() * 2.0 - 1.0);
        float len = deep ? 0.95f : 0.42f;
        int n = (int)(len * Rate);
        var data = new float[n];

        // The impact: a short burst of band-limited noise, low and full wading, a brighter slap in a puddle.
        {
            float fc = deep ? Mathf.Lerp(320f, 520f, R()) : Mathf.Lerp(850f, 1400f, R());
            float tau = deep ? Mathf.Lerp(0.05f, 0.09f, R()) : Mathf.Lerp(0.018f, 0.032f, R());
            float f = 2f * Mathf.Sin(Mathf.PI * fc / Rate), q = 0.9f, low = 0f, band = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)Rate;
                float high = N() - low - q * band;
                band += f * high;
                low += f * band;
                float env = Mathf.Clamp01(t / 0.003f) * Mathf.Exp(-t / tau);
                data[i] += band * env * (deep ? 1.1f : 0.8f);
            }
        }

        // Droplets falling back: lots of tiny, bright ticks, thickest just after the impact.
        {
            int count = deep ? 70 : 28;
            float spread = deep ? 0.45f : 0.16f;
            for (int g = 0; g < count; g++)
            {
                float t0 = 0.008f + Mathf.Pow(R(), 1.7f) * spread;
                float hz = Mathf.Lerp(1800f, 6500f, R());
                float tau = Mathf.Lerp(0.0008f, 0.0025f, R());
                float amp = Mathf.Lerp(0.04f, 0.18f, R()) * Mathf.Exp(-t0 / (spread * 0.5f));
                float ph = R() * TwoPi;
                int i0 = (int)(t0 * Rate);
                for (int i = i0; i < n && i < i0 + (int)(tau * 8f * Rate); i++)
                {
                    float u = (i - i0) / (float)Rate;
                    data[i] += Mathf.Sin(ph + TwoPi * hz * u) * Mathf.Exp(-u / tau) * amp;
                }
            }
        }

        // Bubbles: each rings at its resonant pitch, dies away quickly and glides slightly up as it rises.
        {
            int count = deep ? 16 : 6;
            float spread = deep ? 0.55f : 0.18f;
            for (int b = 0; b < count; b++)
            {
                float t0 = 0.012f + Mathf.Pow(R(), 1.4f) * spread;
                float f0 = deep ? Mathf.Lerp(300f, 1600f, R() * R()) : Mathf.Lerp(700f, 2600f, R());
                float damping = 0.043f * f0 + 0.0014f * Mathf.Pow(f0, 1.5f);
                float sweep = 0.1f * damping;
                float amp = Mathf.Lerp(0.12f, 0.4f, R()) * Mathf.Exp(-t0 / spread);
                int i0 = (int)(t0 * Rate);
                int life = (int)(7f / damping * Rate);
                float phase = 0f;
                for (int i = i0; i < n && i < i0 + life; i++)
                {
                    float u = (i - i0) / (float)Rate;
                    phase += TwoPi * f0 * (1f + sweep * u) / Rate;
                    data[i] += Mathf.Sin(phase) * Mathf.Exp(-damping * u) * Mathf.Clamp01(u / 0.0006f) * amp;
                }
            }
        }

        // The slosh: water swirling and lapping round the leg as it pushes through.
        {
            float lp = 0f, lp2 = 0f, hp = 0f;
            float body = deep ? 0.55f : 0.12f;
            float wobble = R() * 10f;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)Rate;
                float w = N();
                lp += (w - lp) * 0.09f;
                lp2 += (lp - lp2) * 0.09f;
                hp += (lp2 - hp) * 0.01f;
                float x = lp2 - hp; // roughly 150Hz to 600Hz
                float p = Mathf.Clamp01((t - 0.03f) / (len - 0.03f));
                float env = Mathf.Pow(Mathf.Sin(Mathf.PI * Mathf.Pow(p, 0.6f)), 1.5f);
                float lap = 0.55f + 0.45f * Mathf.PerlinNoise(t * 14f, wobble);
                data[i] += x * env * lap * body * 3f;
            }
        }

        // Fade the very end so nothing clicks.
        for (int i = 0; i < n; i++) data[i] *= Mathf.Clamp01((n - i) / (0.02f * Rate));
        return Create(name, data, 0.8f);
    }

    static AudioClip MakeWadeLoop()
    {
        float lp = 0f, lp2 = 0f, hp = 0f, gurgle = 0f, gurglePhase = 0f, gurgleHz = 400f;
        return Loop("Wade Loop", 4f, t =>
        {
            // The swish: low, soft, filtered water noise, surging with the legs (about two strides a second).
            float w = White();
            lp += (w - lp) * 0.07f;
            lp2 += (lp - lp2) * 0.07f;
            hp += (lp2 - hp) * 0.008f;
            float surge = 0.45f + 0.55f * Mathf.Pow(0.5f + 0.5f * Mathf.Sin(TwoPi * 1.05f * t + Mathf.Sin(TwoPi * 0.3f * t)), 1.6f);
            float swish = (lp2 - hp) * surge * 2.2f;
            // Now and then a small gurgle: a bubble of water folding over.
            if (rng.NextDouble() < 6.0 / Rate) { gurgle = 0.25f; gurgleHz = 250f + (float)rng.NextDouble() * 500f; }
            gurgle *= 0.9993f;
            gurglePhase += TwoPi * gurgleHz * (1f + 0.4f * (0.25f - gurgle)) / Rate;
            return swish + Mathf.Sin(gurglePhase) * gurgle * 0.3f;
        }, 0.5f);
    }

    static AudioClip MakeRadioBroadcast()
    {
        // A waltz in D minor, one note a beat (semitones from A4; 99 = rest), twelve bars of three.
        int[] melody = { -7, -3, 0, 1, 0, -3, -2, -5, -2, -7, 99, 99,
                         -7, -3, 0, 5, 3, 1, 0, -2, 1, 0, 99, 99,
                         -2, 1, 5, 3, 1, -2, 0, -3, -7, -5, -7, 99 };
        int[] bass = { -31, -24, -29, -26 }; // D, A, E, G, a note a bar (low)
        const float beat = 16f / 36f;
        float phase = 0f, bassPhase = 0f, bp1 = 0f, bp2 = 0f, hiss = 0f, crackleLp = 0f;
        return Loop("Radio Broadcast", 16f, t =>
        {
            int b = Mathf.FloorToInt(t / beat) % melody.Length;
            float inBeat = t - Mathf.Floor(t / beat) * beat;
            int note = melody[b];
            // Wow and flutter: the pitch wobbles like a worn record played down the line.
            float wobble = 1f + 0.006f * Mathf.Sin(TwoPi * 0.5f * t) + 0.003f * Mathf.Sin(TwoPi * 6f * t);
            float tone = 0f;
            if (note != 99)
            {
                float hz = 440f * Mathf.Pow(2f, note / 12f) * wobble;
                phase += TwoPi * hz / Rate;
                float env = Mathf.Clamp01(inBeat / 0.02f) * Mathf.Exp(-inBeat * 3f);
                tone = (Mathf.Sin(phase) + 0.5f * Mathf.Sin(phase * 2f) + 0.3f * Mathf.Sin(phase * 3f)) * env;
            }
            int bar = Mathf.FloorToInt(t / (beat * 3f)) % bass.Length;
            bassPhase += TwoPi * 440f * Mathf.Pow(2f, (bass[bar] + 12) / 12f) * wobble / Rate;
            float oompah = (inBeat < beat * 0.5f ? 1f : 0.4f) * Mathf.Exp(-inBeat * 4f);
            tone += Mathf.Sin(bassPhase) * oompah * 0.5f;

            // Through a tinny little speaker: only the middle of the sound comes through.
            bp1 += (tone - bp1) * 0.25f;
            bp2 += (bp1 - bp2) * 0.04f;
            float music = (bp1 - bp2) * 1.6f;
            music = Mathf.Clamp(music * 1.8f, -0.6f, 0.6f); // overdriven

            // The station drifts in and out under the hiss, with crackles and pops.
            float signal = 0.55f + 0.45f * Mathf.PerlinNoise(t * 0.35f, 4.2f);
            hiss += (White() - hiss) * 0.6f;
            crackleLp += (White() - crackleLp) * 0.3f;
            float crackle = rng.NextDouble() < 0.0006 ? White() * 2.5f : 0f;
            return music * signal + hiss * (0.12f + 0.25f * (1f - signal)) + crackle + crackleLp * 0.02f;
        }, 0.7f);
    }

    static AudioClip MakeHeartbeat()
    {
        // Two thumps: each a low sine sagging in pitch as it dies away, with a muffled knock of noise on its
        // front, all heavily low-passed, as if felt through your own chest.
        float lp = 0f;
        float Thump(float t, float start, float f0, float decay, float level)
        {
            float u = t - start;
            if (u < 0f) return 0f;
            float env = (1f - Mathf.Exp(-u * 300f)) * Mathf.Exp(-u * decay);
            float f = f0 * (1f + 0.6f * Mathf.Exp(-u * 30f));
            return level * env * Mathf.Sin(TwoPi * f * u);
        }
        return Shot("Heartbeat", 0.6f, t =>
        {
            float s = Thump(t, 0f, 46f, 14f, 1f) + Thump(t, 0.17f, 58f, 20f, 0.7f);
            float knock = (t < 0.03f ? 1f - t / 0.03f : 0f) + (t > 0.17f && t < 0.19f ? 0.6f * (1f - (t - 0.17f) / 0.02f) : 0f);
            s += White() * knock * 0.25f;
            lp += (s - lp) * 0.18f;
            return Tanh(lp * 1.6f);
        }, 0.9f);
    }

    static AudioClip MakeMothFlutter()
    {
        // A dozen moths, each a burst of wingbeats (papery noise chopped at its own buzzing rate) drifting
        // nearer and further, and now and then one tapping into the glass.
        const int moths = 12;
        var rate = new float[moths];
        var phase = new float[moths];
        var drift = new float[moths];
        for (int m = 0; m < moths; m++)
        {
            rate[m] = 28f + 22f * Mathf.Abs(White());
            phase[m] = Mathf.Abs(White()) * 10f;
            drift[m] = 1 + rng.Next(3); // whole cycles per loop, so it wraps cleanly
        }
        float hp = 0f, lp = 0f, tap = 0f;
        return Loop("Moth Flutter", 8f, t =>
        {
            float n = White();
            hp += (n - hp) * 0.25f;
            float paper = n - hp; // the hiss of wings, lows taken out
            float beats = 0f;
            for (int m = 0; m < moths; m++)
            {
                float flap = Mathf.Max(0f, Mathf.Sin(TwoPi * rate[m] * t + phase[m]));
                float near = 0.5f + 0.5f * Mathf.Sin(TwoPi * drift[m] * t / 8f + phase[m]);
                beats += flap * flap * near * near;
            }
            lp += (paper * beats - lp) * 0.35f;
            if (rng.NextDouble() < 0.0004) tap = 1f;
            tap *= 0.985f;
            return lp * 0.25f + tap * Mathf.Sin(TwoPi * 2400f * t) * 0.3f;
        }, 0.6f);
    }

    // ---------------------------------------------------------------- Voices

    // Vowels by their first three formants (Hz): the resonances of the mouth that make "ah" an "ah".
    static readonly float[][] Vowels =
    {
        new[] { 730f, 1090f, 2440f },  // ah
        new[] { 530f, 1840f, 2480f },  // eh
        new[] { 300f, 2200f, 3000f },  // ee
        new[] { 570f, 840f, 2410f },   // oh
        new[] { 320f, 870f, 2240f },   // oo
        new[] { 500f, 1400f, 2450f },  // uh
    };
    static readonly float[] HumFormants = { 260f, 1200f, 2300f };

    // Consonants: 0 none, 1 'k' (stop, mid burst), 2 't' (stop, high burst), 3 's' (hiss), 4 'sh', 5 'h'
    // (breath), 6 'm'/'n' (hummed, mouth nearly shut), 7 'r' (a rolled flutter).
    static readonly float[] ConsonantCentre = { 0f, 1800f, 4200f, 6000f, 2600f, 1500f, 260f, 900f };

    /// <summary>
    /// A chant in no language: a few deep voices (and one an octave down) buzzing through the shapes of
    /// vowels, gliding from one to the next, broken by stops and hisses and hums into syllables, two or three
    /// syllables to a word, a handful of words, in a slow, steady rhythm sinking in pitch as it goes; a
    /// little rasping, then echoing. seed picks the words.
    /// </summary>
    static AudioClip MakeChant(string name, int seed, bool giant = false)
    {
        var pick = new System.Random(seed);
        float P(float a, float b) => a + (float)pick.NextDouble() * (b - a);

        // The words: syllables of (consonant before, vowel, how long), with a pause after each word.
        var plan = new System.Collections.Generic.List<(int consonant, int vowel, float length, bool pauseAfter)>();
        int words = giant ? 2 : pick.Next(4, 7);
        float stretch = giant ? 1.5f : 1f; // the giant's words are slower, drawn out
        for (int w = 0; w < words; w++)
        {
            int syllables = pick.Next(2, 4);
            for (int sy = 0; sy < syllables; sy++)
            {
                int consonant = pick.NextDouble() < 0.75 ? pick.Next(1, ConsonantCentre.Length) : 0;
                bool last = sy == syllables - 1;
                plan.Add((consonant, pick.Next(Vowels.Length), (last ? P(0.45f, 0.65f) : P(0.26f, 0.36f)) * stretch * (giant && last && w == words - 1 ? 2f : 1f), last));
            }
        }

        const float consonantTime = 0.07f, pauseTime = 0.22f, lead = 0.3f;
        float total = lead;
        foreach (var p in plan) total += consonantTime + p.length + (p.pauseAfter ? pauseTime : 0f);
        total += giant ? 4.5f : 2.2f; // the echo dying away
        int n = (int)(total * Rate);
        var dry = new float[n];

        float basePitch = giant ? P(38f, 42f) : P(58f, 70f);
        float[] voicePitch = giant ? new[] { 1f, 1.006f, 0.994f, 1.012f, 0.5f, 0.503f, 1.5f, 1.497f } : new[] { 1f, 1.007f, 0.993f, 0.5f };
        float[] voiceLevel = giant ? new[] { 1f, 0.8f, 0.8f, 0.6f, 0.8f, 0.6f, 0.45f, 0.4f } : new[] { 1f, 0.7f, 0.7f, 0.6f };
        float formantScale = giant ? 0.8f : 1f; // a bigger throat
        var phase = new float[voicePitch.Length];
        float[] f = { 500f, 1500f, 2500f };
        var ry1 = new float[3];
        var ry2 = new float[3];
        float cy1 = 0f, cy2 = 0f, srcLp = 0f, rasp = 0f;

        int segment = 0;
        float segStart = lead;
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)Rate;
            // Where in the chant we are.
            while (segment < plan.Count && t > segStart + consonantTime + plan[segment].length + (plan[segment].pauseAfter ? pauseTime : 0f))
            {
                segStart += consonantTime + plan[segment].length + (plan[segment].pauseAfter ? pauseTime : 0f);
                segment++;
            }
            float voiced = 0f, noise = 0f;
            int consonant = 0, vowel = 5;
            if (segment < plan.Count && t >= lead)
            {
                var syl = plan[segment];
                float u = t - segStart;
                consonant = syl.consonant;
                vowel = syl.vowel;
                if (u < consonantTime)
                {
                    // The consonant: a stop is a gap and a burst, a hiss is noise, a hum is the voice muffled.
                    float c = u / consonantTime;
                    if (consonant == 1 || consonant == 2) { voiced = 0f; noise = c > 0.6f ? (1f - c) * 2.5f : 0f; }
                    else if (consonant >= 3 && consonant <= 5) { voiced = consonant == 5 ? 0.2f : 0.1f; noise = Mathf.Sin(Mathf.PI * c) * (consonant == 5 ? 0.6f : 0.9f); }
                    else if (consonant == 6) voiced = 0.45f;
                    else if (consonant == 7) voiced = 0.6f * (0.5f + 0.5f * Mathf.Sin(TwoPi * 28f * u));
                    else voiced = Mathf.Clamp01(c * 2f);
                }
                else
                {
                    // The vowel, swelling in and tailing off.
                    float v = (u - consonantTime) / syl.length;
                    voiced = v < 1f ? Mathf.Sin(Mathf.PI * Mathf.Min(1f, v * 1.6f) * 0.5f) * (v > 0.75f ? (1f - v) * 4f : 1f) : 0f;
                }
            }

            // The mouth moving: formants gliding toward this sound's (closed and low for a hum).
            float[] target = consonant == 6 && voiced > 0f ? HumFormants : Vowels[vowel];
            for (int k = 0; k < 3; k++) f[k] += (target[k] * formantScale - f[k]) * 0.0012f;

            // The voices: deep buzzes, slowly sinking through the chant, with a slight waver and rasp.
            float progress = t / total;
            float pitch = basePitch * (1f - 0.12f * progress) * (1f + 0.004f * Mathf.Sin(TwoPi * 4.5f * t));
            float source = 0f;
            for (int k = 0; k < phase.Length; k++)
            {
                phase[k] += pitch * voicePitch[k] / Rate;
                phase[k] -= Mathf.Floor(phase[k]);
                source += (1f - 2f * phase[k]) * voiceLevel[k];
            }
            rasp += (White() - rasp) * 0.3f;
            float fry = 0.75f + 0.25f * Mathf.Sin(TwoPi * 31f * t + Mathf.Sin(t * 3f) * 4f); // a creak in the throat
            srcLp += ((source * (giant ? 0.2f : 0.3f) + rasp * 0.25f) * fry * voiced - srcLp) * 0.35f;

            // Through the mouth: three resonances.
            float outV = 0f;
            for (int k = 0; k < 3; k++)
            {
                float bw = 80f + k * 60f;
                float r = Mathf.Exp(-Mathf.PI * bw / Rate);
                float c1 = 2f * r * Mathf.Cos(TwoPi * f[k] / Rate), c2 = -r * r;
                float y = (1f - r) * srcLp + c1 * ry1[k] + c2 * ry2[k];
                ry2[k] = ry1[k];
                ry1[k] = y;
                outV += y * (k == 0 ? 1f : k == 1 ? 0.7f : 0.35f);
            }

            // The consonant's own noise, through its resonance.
            float cOut = 0f;
            if (noise > 0f)
            {
                float cf = ConsonantCentre[Mathf.Clamp(consonant, 1, ConsonantCentre.Length - 1)];
                float r = Mathf.Exp(-Mathf.PI * 600f / Rate);
                float c1 = 2f * r * Mathf.Cos(TwoPi * cf / Rate), c2 = -r * r;
                float y = (1f - r) * White() * noise + c1 * cy1 + c2 * cy2;
                cy2 = cy1;
                cy1 = y;
                cOut = y * 0.6f;
            }
            dry[i] = outV * 6f + cOut;
        }

        // A big, cold space round it.
        var wet = Reverb(dry, giant ? 0.9f : 0.84f, giant ? 0.25f : 0.35f);
        for (int i = 0; i < n; i++) dry[i] = giant ? Tanh((dry[i] * 1.2f + wet[i] * 2.2f) * 1.6f) : Tanh((dry[i] + wet[i] * 1.6f) * 1.2f);
        return Create(name, dry, giant ? 1f : 0.9f);
    }

    static AudioClip MakeNightChorus()
    {
        // A few crickets, each a pure, high tone chopped into quick pulses (two to four to a chirp), chirping
        // at its own steady pace; under them, now and then, a frog's low, buzzing croak.
        const int crickets = 5;
        const float pulseSpacing = 0.045f, pulseLength = 0.03f;
        var freq = new float[crickets];
        var period = new float[crickets];
        var offset = new float[crickets];
        var pulses = new int[crickets];
        var level = new float[crickets];
        for (int c = 0; c < crickets; c++)
        {
            freq[c] = 3700f + c * 260f + White() * 120f;
            period[c] = 0.4f + 0.3f * Mathf.Abs(White());
            offset[c] = Mathf.Abs(White()) * period[c];
            pulses[c] = 2 + rng.Next(3);
            level[c] = 0.35f + 0.65f * Mathf.Abs(White());
        }
        float frogNext = 1.5f, frogStart = -10f, frogPitch = 1f, frogLength = 0.35f;
        return Loop("Night Chorus", 16f, t =>
        {
            float s = 0f;
            for (int c = 0; c < crickets; c++)
            {
                float local = (t + offset[c]) % period[c];
                int pulse = (int)(local / pulseSpacing);
                if (pulse >= pulses[c]) continue;
                float into = local - pulse * pulseSpacing;
                if (into >= pulseLength) continue;
                s += level[c] * Mathf.Sin(Mathf.PI * into / pulseLength) * Mathf.Sin(TwoPi * freq[c] * t);
            }

            if (t >= frogNext)
            {
                frogStart = t;
                frogNext = t + 2.5f + 5f * Mathf.Abs(White());
                frogPitch = 0.8f + 0.4f * Mathf.Abs(White());
                frogLength = 0.25f + 0.25f * Mathf.Abs(White());
            }
            float ft = t - frogStart;
            if (ft < frogLength)
            {
                float buzz = 0.5f + 0.5f * Mathf.Sin(TwoPi * 26f * ft);
                float f0 = 170f * frogPitch;
                float tone = Mathf.Sin(TwoPi * f0 * ft) + 0.6f * Mathf.Sin(TwoPi * f0 * 2f * ft) + 0.3f * Mathf.Sin(TwoPi * f0 * 3f * ft);
                s += 0.9f * Mathf.Sin(Mathf.PI * ft / frogLength) * buzz * buzz * tone;
            }
            return s;
        }, 0.5f);
    }

    static AudioClip MakeRainLoop()
    {
        // Rain, not running water: no pitched drops (those burble like a creek). A dense, airy sizzle of
        // countless tiny impacts, a soft wash underneath, and a patter of individual drops on top: each
        // just a tick of noise, bright on leaves, duller on the ground.
        float lpA = 0f, lpB = 0f, hpA = 0f, wash = 0f, washHp = 0f;
        float tickEnv = 0f, tickLp = 0f, tickBright = 0.5f, tickDecay = 0.99f;
        float densityPhase = 0f;
        return Loop("Rain", 14f, t =>
        {
            float w = White();
            // The sizzle: noise with the lows taken out and the very top softened.
            lpA += (w - lpA) * 0.55f;
            hpA += (lpA - hpA) * 0.12f;
            float sizzle = (lpA - hpA) * 0.5f;
            // Its grain: the sizzle comes in tiny random bursts, the way many small drops land.
            lpB += ((rng.NextDouble() < 0.12 ? 1f : 0f) - lpB) * 0.02f;
            sizzle *= 0.55f + 0.9f * lpB;
            // The wash: a soft, low hush underneath.
            wash += (w - wash) * 0.04f;
            washHp += (wash - washHp) * 0.004f;
            float hush = (wash - washHp) * 0.35f;

            // The patter: individual drops, ticks of filtered noise.
            densityPhase += 1f / Rate;
            float density = 110f + 40f * Mathf.PerlinNoise(t * 0.4f, 2.3f);
            if (rng.NextDouble() < density / Rate)
            {
                tickEnv = 0.15f + (float)rng.NextDouble() * 0.6f;
                bool leaf = rng.NextDouble() < 0.6;
                tickBright = leaf ? 0.6f + (float)rng.NextDouble() * 0.35f : 0.12f + (float)rng.NextDouble() * 0.2f;
                tickDecay = leaf ? 0.996f - (float)rng.NextDouble() * 0.006f : 0.9975f;
            }
            tickLp += (w - tickLp) * tickBright;
            float tick = (w * 0.5f + tickLp * 0.5f) * tickEnv;
            tickEnv *= tickDecay;

            float swell = 0.85f + 0.15f * Mathf.PerlinNoise(t * 0.25f, 7.1f);
            return (sizzle + hush + tick * 0.35f) * swell;
        }, 0.6f);
    }

    static AudioClip MakePauseSong()
    {
        // The tune: A minor, slow 3/4, a note a beat (semitones from A4; 99 a rest), 16 bars.
        int[] tune = { 0, 3, 7, 5, 3, 2, 0, 99, -2, 0, 2, 3, 2, 99, 99, 99,
                       0, 3, 7, 8, 7, 5, 3, 99, 2, 3, 2, -2, 0, 99, 99, 99,
                       7, 8, 7, 5, 3, 5, 3, 2, 0, 2, -2, -5, -4, 99, 99, 99 };
        int[] bass = { -24, -19, -22, -17 }; // A, E, G(ish), D: a chord a bar, under the hum
        const float beat = 0.62f;
        float len = tune.Length * beat;
        float boxPhase = 0f, humPhase = 0f, humPhase2 = 0f, lp = 0f, lp2 = 0f, hiss = 0f, sag = 0f;
        int lastNote = -1;
        float noteStart = 0f, skipUntil = -1f;
        return Loop("Pause Song", len, t =>
        {
            // The record warps, sags now and then, and once in a while skips a beat back.
            float wow = 1f + 0.012f * Mathf.Sin(TwoPi * 0.45f * t) + 0.004f * Mathf.Sin(TwoPi * 5.5f * t);
            sag = Mathf.Max(0f, sag - 1f / Rate * 0.8f);
            if (rng.NextDouble() < 0.06 / Rate) sag = 0.35f; // the motor drags
            wow *= 1f - sag * 0.3f;
            float tt = t;
            if (t < skipUntil) tt = t - beat;
            else if (rng.NextDouble() < 0.02 / Rate) skipUntil = t + beat * 0.5f;

            int b = Mathf.FloorToInt(tt / beat) % tune.Length;
            if (b < 0) b += tune.Length;
            if (b != lastNote) { lastNote = b; noteStart = t; }
            float inNote = t - noteStart;
            int note = tune[b];
            float sample = 0f;
            if (note != 99)
            {
                // The music box: a bright pluck with a metallic overtone, ringing away.
                float hz = 440f * Mathf.Pow(2f, (note + 12) / 12f) * wow;
                boxPhase += TwoPi * hz / Rate;
                float env = Mathf.Exp(-inNote * 3.2f) * Mathf.Clamp01(inNote / 0.004f);
                sample += (Mathf.Sin(boxPhase) + 0.35f * Mathf.Sin(boxPhase * 4.03f) * Mathf.Exp(-inNote * 9f)) * env * 0.6f;
            }
            // A hollow voice humming the chords underneath, a long way off.
            int bar = Mathf.FloorToInt(tt / (beat * 3f)) % bass.Length;
            if (bar < 0) bar += bass.Length;
            float humHz = 440f * Mathf.Pow(2f, bass[bar] / 12f) * wow;
            humPhase += TwoPi * humHz / Rate;
            humPhase2 += TwoPi * humHz * 1.498f / Rate;
            float hum = (Mathf.Sin(humPhase) + 0.6f * Mathf.Sin(humPhase2) + 0.3f * Mathf.Sin(humPhase * 2f)) * (0.55f + 0.45f * Mathf.Sin(TwoPi * 0.2f * t));
            sample += hum * 0.22f;

            // Down a bad line: thin, overdriven, hissing and crackling.
            lp += (sample - lp) * 0.3f;
            lp2 += (lp - lp2) * 0.03f;
            float line = (lp - lp2) * 1.8f;
            line = (float)System.Math.Tanh(line * 2.2f) * 0.6f;
            hiss += (White() - hiss) * 0.5f;
            float crackle = rng.NextDouble() < 0.0005 ? White() * 1.5f : 0f;
            float fadeIn = 0.6f + 0.4f * Mathf.PerlinNoise(t * 0.3f, 8.8f); // the signal drifts
            return line * fadeIn + hiss * 0.07f + crackle;
        }, 0.65f);
    }

    static AudioClip MakeTornadoRoar()
    {
        float lp1 = 0f, lp2 = 0f, lp3 = 0f, hp = 0f;
        return Loop("Tornado Roar", 16f, t =>
        {
            float w = White();
            lp1 += (w - lp1) * 0.02f;
            lp2 += (lp1 - lp2) * 0.03f;
            hp += (lp2 - hp) * 0.002f;
            float rumble = (lp2 - hp) * 6f;
            lp3 += (w - lp3) * 0.15f; // the rushing, higher up
            float rush = lp3 * 0.25f;
            float swell = 0.6f + 0.4f * Mathf.PerlinNoise(t * 0.2f, 4.4f);
            return (rumble + rush * swell) * swell;
        }, 0.7f);
    }

    static AudioClip MakeCarChirp()
    {
        const float len = 0.75f;
        return Shot("Car Chirp", len, t =>
        {
            // Two quick chirps, each a bright square-ish tone with a little upward blip.
            float on = (t < 0.12f || (t > 0.28f && t < 0.4f)) ? 1f : 0f;
            float local = t < 0.2f ? t : t - 0.28f;
            float hz = 2600f + 400f * Mathf.Exp(-local * 40f);
            float tone = Mathf.Sign(Mathf.Sin(TwoPi * hz * t)) * 0.6f + Mathf.Sin(TwoPi * hz * 2f * t) * 0.2f;
            float edge = Mathf.Clamp01(local / 0.004f) * Mathf.Clamp01((0.12f - local) / 0.01f);
            return tone * on * edge;
        }, 0.8f);
    }

    static AudioClip MakeFobClick()
    {
        float lp = 0f;
        return Shot("Fob Click", 0.06f, t =>
        {
            lp += (White() - lp) * 0.6f;
            return lp * Mathf.Exp(-t * 180f) + Mathf.Sin(TwoPi * 3800f * t) * Mathf.Exp(-t * 300f) * 0.3f;
        }, 0.7f);
    }

    static AudioClip MakeCameraFlash()
    {
        const float len = 1.6f;
        float lp = 0f, whine = 0f;
        return Shot("Camera Flash", len, t =>
        {
            float w = White();
            // The shutter: two hard little clacks.
            float clack = (t < 0.012f ? 1f : 0f) + (t > 0.045f && t < 0.055f ? 0.7f : 0f);
            lp += (w - lp) * 0.5f;
            float shutter = lp * clack;
            // The flash going off: a sharp, bright pop.
            float pop = w * Mathf.Exp(-Mathf.Max(0f, t - 0.02f) * 60f) * (t > 0.02f ? 0.9f : 0f);
            // And the capacitor whining back up, rising and fading.
            float u = Mathf.Clamp01((t - 0.25f) / (len - 0.25f));
            whine += TwoPi * Mathf.Lerp(1800f, 7500f, Mathf.Sqrt(u)) / Rate;
            float charge = t > 0.25f ? Mathf.Sin(whine) * 0.12f * Mathf.Sin(Mathf.PI * u) : 0f;
            return shutter * 1.2f + pop + charge;
        }, 0.9f);
    }

    static AudioClip MakeThunder()
    {
        const float len = 8f;
        float lp1 = 0f, lp2 = 0f, lp3 = 0f, crackLp = 0f;
        // A few swells as the rumble rolls round the hills.
        float[] swellAt = { 0.4f, 1.3f, 2.4f, 3.6f };
        float[] swellSize = { 1f, 0.7f, 0.85f, 0.5f };
        return Shot("Thunder", len, t =>
        {
            float w = White();
            // The crack: a bright, ripping burst right at the start.
            crackLp += (w - crackLp) * 0.5f;
            float crack = crackLp * Mathf.Exp(-t * 9f) * (0.6f + 0.4f * Mathf.Sin(t * 180f));

            // The rumble: deep filtered noise, swelling and fading unevenly.
            lp1 += (w - lp1) * 0.02f;
            lp2 += (lp1 - lp2) * 0.02f;
            lp3 += (w - lp3) * 0.006f;
            float swell = 0f;
            for (int i = 0; i < swellAt.Length; i++)
                swell += swellSize[i] * Mathf.Exp(-Mathf.Pow((t - swellAt[i]) / 0.7f, 2f));
            float tail = Mathf.Exp(-t * 0.45f);
            float rumble = (lp2 * 6f + lp3 * 10f) * (0.35f * tail + 0.65f * swell * tail);
            float fade = Mathf.Clamp01((len - t) / 1.5f);
            return (crack * 0.8f + rumble) * fade;
        }, 1f);
    }

    static AudioClip MakeBreath()
    {
        const float len = 5f;
        float lp = 0f, lp2 = 0f;
        return Shot("Breath", len, t =>
        {
            // In for 1.7s, hold, out for 2s, silence. Silent at both ends, so it loops without a seam.
            float inhale = t < 1.7f ? Mathf.Sin(Mathf.PI * t / 1.7f) : 0f;
            float exhale = t > 2.1f && t < 4.1f ? Mathf.Pow(Mathf.Sin(Mathf.PI * (t - 2.1f) / 2f), 0.7f) : 0f;
            float w = White();
            lp += (w - lp) * Mathf.Lerp(0.08f, 0.25f, inhale);   // airy, brighter as it draws in
            lp2 += (w - lp2) * 0.05f;                           // darker exhale
            float rasp = 1f + 0.6f * Mathf.Sin(t * 2f * Mathf.PI * 38f) * Mathf.Sin(t * 2f * Mathf.PI * 3f); // a wet rattle
            float growl = Mathf.Sin(t * 2f * Mathf.PI * 62f) * 0.12f * exhale;
            return lp * inhale * 0.7f + (lp2 * 1.4f * rasp + growl) * exhale;
        }, 0.8f);
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
