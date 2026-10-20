using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// A lamp post that starts off. The player clicks it repeatedly to fill a meter (which slowly
/// drains if they stop). A full meter lights the lamp, making a safe zone that entities can't
/// enter or creep into. Every lamp lit also summons another ShadowEntity.
///
/// A lit lamp burns through its energy. It flickers more and more as the energy runs low, then
/// goes out and needs to recharge before it can be lit again, crackling with faint blue sparks meanwhile.
/// LampSpawner places these around the map; LampInteractor handles the clicking and HUD.
/// </summary>
public class Lamp : MonoBehaviour, SafeZones.ISource
{
    public static readonly List<Lamp> All = new List<Lamp>();

    [Header("Charging")]
    [Range(0.01f, 1f), Tooltip("How much each click fills the meter.")]
    public float chargePerClick = 0.06f;
    [Tooltip("Seconds without clicking before the meter starts draining.")]
    public float drainDelay = 1.5f;
    [Tooltip("Meter lost per second while draining.")]
    public float drainRate = 0.08f;

    [Header("When Lit")]
    [Tooltip("Radius of the safe zone.")]
    public float safeRadius = 13f;
    public float lightIntensity = 4f;
    public Color lightColor = new Color(1f, 0.75f, 0.45f);
    [Tooltip("Summon another entity each time a lamp is lit.")]
    public bool spawnEntityWhenLit = true;

    [Header("Energy")]
    [Tooltip("Once lit, a lamp stays on for good (energy doesn't run down).")]
    public bool stayLit = true;
    [Tooltip("Seconds a lit lamp lasts before going out (when it doesn't stay lit).")]
    public float energyDuration = 90f;
    [Range(0f, 1f), Tooltip("Below this much energy the light starts flickering, more as it gets lower.")]
    public float flickerBelow = 0.35f;
    [Tooltip("Seconds after going out before the lamp can be lit again.")]
    public float cooldownTime = 30f;
    [Tooltip("Colour of the little electrical sparks while the lamp recharges.")]
    public Color sparkColor = new Color(0.45f, 0.65f, 1f);
    [Tooltip("Average sparks per second while recharging.")]
    public float sparksPerSecond = 1.2f;

    [Header("Finding It (while unlit)")]
    [Tooltip("A small red light on top blinks slowly, seen through the fog from far off, until the lamp is lit.")]
    public Color markerColor = new Color(1f, 0.08f, 0.04f);
    [Tooltip("Seconds between the marker's blinks.")]
    public float markerPeriod = 2.4f;
    [Tooltip("The marker's for finding lamps from far off: it fades out as you come within markerFadeFar, gone inside markerFadeNear.")]
    public float markerFadeNear = 10f;
    public float markerFadeFar = 25f;
    [Tooltip("A faint electrical buzz you can follow to it, audible out to buzzRange metres.")]
    [Range(0f, 1f)] public float buzzVolume = 0.2f;
    public float buzzRange = 40f;

    [Header("Audio (optional)")]
    public AudioClip clickSound;
    public AudioClip igniteSound;
    public AudioClip humLoop;
    public AudioClip powerDownSound;
    public AudioClip flickerSound;

    /// <summary>
    /// Re-applies settings that are baked in when the lamp is built (colours, light range), after they've been
    /// changed (LampSpawner pushes its settings to every lamp through this).
    /// </summary>
    public void RefreshSettings()
    {
        if (lampLight != null)
        {
            lampLight.color = lightColor;
            lampLight.range = safeRadius * 1.6f;
            if (IsLit) lampLight.intensity = lightIntensity;
        }
        if (sparkMat != null) MaterialFactory.SetColor(sparkMat, sparkColor * 4f);
        if (sparkLight != null) sparkLight.color = sparkColor;
    }

    public bool IsLit { get; private set; }
    /// <summary>Lit at least once this run (the lone door opens once every lamp has been).</summary>
    public bool HasBeenLit { get; private set; }

    /// <summary>True once every lamp in the forest has been lit at least once this run.</summary>
    public static bool AllHaveBeenLit
    {
        get
        {
            if (All.Count == 0) return false;
            foreach (var lamp in All) if (!lamp.HasBeenLit) return false;
            return true;
        }
    }

    /// <summary>How many different lamps have been lit at least once this run.</summary>
    public static int LitCount
    {
        get
        {
            int lit = 0;
            foreach (var lamp in All) if (lamp.HasBeenLit) lit++;
            return lit;
        }
    }

    /// <summary>How many lamps have never been lit this run.</summary>
    public static int DarkCount
    {
        get
        {
            int dark = 0;
            foreach (var lamp in All) if (!lamp.HasBeenLit) dark++;
            return dark;
        }
    }
    /// <summary>Meter from 0 to 1.</summary>
    public float Charge { get; private set; }
    /// <summary>Remaining energy from 1 (just lit) to 0 (goes out).</summary>
    public float Energy { get; private set; }
    public float CooldownRemaining { get; private set; }
    public bool IsCoolingDown => CooldownRemaining > 0f;
    /// <summary>Cooldown progress from 0 (just went out) to 1 (ready).</summary>
    public float CooldownProgress => cooldownTime > 0f ? 1f - CooldownRemaining / cooldownTime : 1f;

    /// <summary>How many times any lamp has been turned on this run.</summary>
    public static int TimesLit { get; private set; }
    public static void ResetTimesLit() { TimesLit = 0; LastLit = null; }

    /// <summary>Dev tools: lights it now, as if it had been cranked up (if it isn't already).</summary>
    public void ForceLight()
    {
        if (!IsLit) TurnOn();
    }

    /// <summary>Dev tools: sets the count of lamps lit (the narration, the entities and the giant go by it).</summary>
    public static void SetTimesLit(int count)
    {
        TimesLit = Mathf.Max(0, count);
        if (TimesLit == 0) LastLit = null;
    }

    /// <summary>The lamp lit most recently: where the player comes back after being caught.</summary>
    public static Lamp LastLit { get; private set; }

    /// <summary>How many lamps are burning right now.</summary>
    public static int CurrentlyLitCount
    {
        get
        {
            int count = 0;
            foreach (var lamp in All) if (lamp.IsLit) count++;
            return count;
        }
    }

    /// <summary>True if the point is inside any lit lamp's safe zone.</summary>
    public static bool IsInSafeZone(Vector3 point) => IsInSafeZone(point, out _);

    /// <summary>True if the point is inside any lit lamp's safe zone, and which lamp.</summary>
    public static bool IsInSafeZone(Vector3 point, out Lamp lamp)
    {
        foreach (var l in All)
        {
            if (!l.IsLit) continue;
            Vector3 d = point - l.transform.position;
            d.y = 0f;
            if (d.sqrMagnitude < l.safeRadius * l.safeRadius)
            {
                lamp = l;
                return true;
            }
        }
        lamp = null;
        return false;
    }

    Light lampLight;
    Light sparkLight;
    Transform sparkRoot;
    Material sparkMat;
    float sparkTimer;
    Material bulbMat;
    AudioSource hum, sfx, buzz;
    Transform marker;
    Material markerMat;
    float markerPhase, crackleTimer;
    float lastClickTime;
    float flash;
    float flickerTimer;
    float flickerLevel = 1f;

    const float PoleHeight = 3f;
    float markerFade = 1f;
    const float HumVolume = 0.2f;

    /// <summary>World position of the bulb (moths gather here).</summary>
    public Vector3 BulbPosition => transform.position + Vector3.up * (PoleHeight - 0.05f);

    void Awake()
    {
        Build();
        hum = ProceduralAudio.AddSource(gameObject, humLoop != null ? humLoop : ProceduralAudio.LampHum, true, true, HumVolume, 2f, 20f);
        sfx = ProceduralAudio.AddSource(gameObject, null, false, true, 0.8f, 3f, 30f);
        SetBulb(0f);
        BuildMarker();
    }

    /// <summary>The red marker on top and the buzz of a dark lamp, so they can be found in the woods.</summary>
    void BuildMarker()
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        go.name = "Marker";
        go.transform.SetParent(transform, false);
        go.transform.localPosition = Vector3.up * (PoleHeight + 0.72f); // just above the finial
        Destroy(go.GetComponent<Collider>());
        markerMat = MaterialFactory.NoFog(markerColor); // seen through the fog
        var renderer = go.GetComponent<Renderer>();
        renderer.sharedMaterial = markerMat;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        marker = go.transform;
        markerPhase = Random.Range(0f, markerPeriod);

        buzz = ProceduralAudio.AddSource(gameObject, ProceduralAudio.LampHum, true, true, buzzVolume, 2f, buzzRange);
        buzz.rolloffMode = AudioRolloffMode.Linear; // carries evenly out to its range, then stops
        buzz.pitch = 0.62f;
        buzz.time = Random.Range(0f, buzz.clip.length * 0.9f);
        buzz.Play();
    }

    void LateUpdate()
    {
        if (marker == null) return;
        bool dark = !IsLit;
        marker.gameObject.SetActive(dark);
        if (buzz.isPlaying != dark) { if (dark) buzz.Play(); else buzz.Stop(); }
        if (!dark) return;

        // A slow blink, like a radio mast: a quick flare, fading away.
        float t = Mathf.Repeat(Time.time + markerPhase, Mathf.Max(markerPeriod, 0.1f));
        float glow = Mathf.Exp(-t * 3.5f);
        var cam = Camera.main;
        float dist = cam != null ? Vector3.Distance(cam.transform.position, marker.position) : 10f;

        // Only for finding it from far off: it fades away as you come close.
        Vector3 flat = cam != null ? cam.transform.position - transform.position : Vector3.forward * 100f;
        flat.y = 0f;
        float fade = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(markerFadeNear, markerFadeFar, flat.magnitude));
        markerFade = Mathf.MoveTowards(markerFade, fade, Time.deltaTime * 1.5f);
        marker.GetComponent<Renderer>().enabled = markerFade > 0.01f;
        MaterialFactory.SetColor(markerMat, markerColor * (0.25f + 4f * glow) * markerFade);

        // Kept big enough to see from far off.
        marker.localScale = Vector3.one * Mathf.Max(0.12f, dist * 0.006f);

        // Now and then the buzz crackles.
        buzz.volume = buzzVolume;
        crackleTimer -= Time.deltaTime;
        if (crackleTimer <= 0f)
        {
            crackleTimer = Random.Range(4f, 9f);
            if (dist < buzzRange) { sfx.pitch = Random.Range(0.7f, 0.9f); sfx.PlayOneShot(ProceduralAudio.LampSpark, 0.15f); }
        }
    }

    bool SafeZones.ISource.IsActive => IsLit;
    Vector3 SafeZones.ISource.Center => transform.position;
    float SafeZones.ISource.Radius => safeRadius;

    void OnEnable()
    {
        All.Add(this);
        SafeZones.Register(this);
    }

    void OnDisable()
    {
        All.Remove(this);
        SafeZones.Unregister(this);
    }

    /// <summary>One click's worth of charge. Lights the lamp when the meter is full.</summary>
    public void AddCharge()
    {
        if (IsLit || IsCoolingDown) return;

        Charge = Mathf.Clamp01(Charge + chargePerClick);
        lastClickTime = Time.time;
        Noise.Emit(transform.position, 9f); // the cranking carries
        flash = 1f;

        sfx.pitch = Mathf.Lerp(0.8f, 1.6f, Charge);
        sfx.PlayOneShot(clickSound != null ? clickSound : ProceduralAudio.LampClick, 0.7f);

        if (Charge >= 1f) TurnOn();
    }

    void Update()
    {
        if (IsLit)
        {
            UpdateLit();
            return;
        }

        if (IsCoolingDown)
        {
            CooldownRemaining = Mathf.Max(0f, CooldownRemaining - Time.deltaTime);
            UpdateSparks();
            if (!IsCoolingDown) EndSpark();
            return;
        }

        if (Charge > 0f && Time.time - lastClickTime > drainDelay)
            Charge = Mathf.Max(0f, Charge - drainRate * Time.deltaTime);

        // The bulb sputters on each click, brighter as the meter fills.
        flash = Mathf.MoveTowards(flash, 0f, Time.deltaTime * 4f);
        float glow = flash * Charge;
        lampLight.enabled = glow > 0.01f;
        lampLight.intensity = lightIntensity * 0.6f * glow * Random.Range(0.6f, 1f);
        SetBulb(glow);
    }

    void UpdateLit()
    {
        if (!stayLit) Energy = Mathf.Max(0f, Energy - Time.deltaTime / Mathf.Max(energyDuration, 0.01f));
        if (Energy <= 0f)
        {
            TurnOff();
            return;
        }

        // 0 while healthy, rising to 1 as the energy runs out.
        float low = flickerBelow > 0f ? Mathf.Clamp01(1f - Energy / flickerBelow) : 0f;

        if (flickerTimer > 0f)
        {
            flickerTimer -= Time.deltaTime;
            if (flickerTimer <= 0f) flickerLevel = 1f;
        }
        else
        {
            // Flickers per second: rare while healthy, constant sputtering near the end.
            float rate = Mathf.Lerp(0.3f, 9f, low * low);
            if (Random.value < rate * Time.deltaTime)
            {
                flickerTimer = Random.Range(0.04f, Mathf.Lerp(0.1f, 0.35f, low));
                flickerLevel = Random.Range(0f, Mathf.Lerp(0.6f, 0.25f, low));
                if (low > 0f) sfx.PlayOneShot(flickerSound != null ? flickerSound : ProceduralAudio.LampSpark, 0.3f + 0.4f * low);
            }
        }

        float level = flickerLevel * Mathf.Lerp(1f, 0.7f, low); // also dims overall near the end
        lampLight.intensity = lightIntensity * level;
        SetBulb(level);
        hum.volume = HumVolume * level;
    }

    /// <summary>While recharging, little blue sparks crackle around the lamp head now and then.</summary>
    void UpdateSparks()
    {
        if (sparkTimer > 0f)
        {
            sparkTimer -= Time.deltaTime;
            // Jitter the arcs and light while the spark lasts.
            sparkLight.intensity = Random.Range(0.6f, 1.6f);
            foreach (Transform arc in sparkRoot)
                arc.localRotation = Random.rotation;
            if (sparkTimer <= 0f) EndSpark();
            return;
        }

        MaterialFactory.SetColor(bulbMat, Color.Lerp(BulbOff, sparkColor * 0.4f, 0.15f)); // faint blue tinge

        if (Random.value < sparksPerSecond * Time.deltaTime)
        {
            sparkTimer = Random.Range(0.05f, 0.15f);
            sparkRoot.localPosition = new Vector3(Random.Range(-0.2f, 0.2f), PoleHeight - Random.Range(0f, 0.25f), Random.Range(-0.2f, 0.2f));
            sparkRoot.gameObject.SetActive(true);
            sparkLight.enabled = true;
            MaterialFactory.SetColor(bulbMat, sparkColor * 1.5f);
            sfx.pitch = Random.Range(1.3f, 1.8f);
            sfx.PlayOneShot(flickerSound != null ? flickerSound : ProceduralAudio.LampSpark, 0.15f);
        }
    }

    void EndSpark()
    {
        sparkTimer = 0f;
        sparkRoot.gameObject.SetActive(false);
        sparkLight.enabled = false;
        if (!IsLit) SetBulb(0f);
    }

    void TurnOn()
    {
        IsLit = true;
        TimesLit++;
        LastLit = this;
        HasBeenLit = true;
        Energy = 1f;
        flickerTimer = 0f;
        flickerLevel = 1f;
        Charge = 1f;
        lampLight.enabled = true;
        lampLight.intensity = lightIntensity;
        lampLight.range = safeRadius * 1.6f;
        SetBulb(1f);

        sfx.pitch = 1f;
        sfx.PlayOneShot(igniteSound != null ? igniteSound : ProceduralAudio.LampIgnite);
        hum.volume = HumVolume;
        hum.Play();

        if (spawnEntityWhenLit) ShadowEntity.Spawn();
    }

    void TurnOff()
    {
        IsLit = false;
        Energy = 0f;
        Charge = 0f;
        CooldownRemaining = cooldownTime;
        lampLight.enabled = false;
        lampLight.range = 6f;
        SetBulb(0f);
        hum.Stop();

        sfx.pitch = 1f;
        sfx.PlayOneShot(powerDownSound != null ? powerDownSound : ProceduralAudio.LampPowerDown);
    }

    /// <summary>Turns the lamp back off and empties the meter.</summary>
    public void ResetLamp()
    {
        IsLit = false;
        HasBeenLit = false;
        Charge = 0f;
        Energy = 0f;
        CooldownRemaining = 0f;
        EndSpark();
        flash = 0f;
        lampLight.enabled = false;
        lampLight.range = 6f;
        SetBulb(0f);
        hum.Stop();
    }

    static readonly Color BulbOff = new Color(0.08f, 0.07f, 0.06f);

    void SetBulb(float glow)
    {
        MaterialFactory.SetColor(bulbMat, Color.Lerp(BulbOff, lightColor * 6f, glow));
    }

    void Build()
    {
        bulbMat = MaterialFactory.Unlit(Color.black);
        BuildModel(transform, bulbMat);

        // Generous invisible box so the lamp is easy to click on.
        var aim = gameObject.AddComponent<BoxCollider>();
        aim.isTrigger = true;
        aim.center = new Vector3(0f, PoleHeight * 0.55f, 0f);
        aim.size = new Vector3(1.2f, PoleHeight + 0.4f, 1.2f);

        lampLight = new GameObject("Lamp Light").AddComponent<Light>();
        lampLight.transform.SetParent(transform, false);
        lampLight.transform.localPosition = new Vector3(0f, PoleHeight - 0.2f, 0f);
        lampLight.type = LightType.Point;
        lampLight.color = lightColor;
        lampLight.range = 6f; // just the sputter while charging; widened when lit
        lampLight.shadows = LightShadows.None;
        lampLight.enabled = false;

        // Recharge spark: a few thin, bright blue arcs and a small flash of blue light.
        sparkMat = MaterialFactory.Unlit(sparkColor * 4f);
        sparkRoot = new GameObject("Spark").transform;
        sparkRoot.SetParent(transform, false);
        for (int i = 0; i < 3; i++)
        {
            var arc = GameObject.CreatePrimitive(PrimitiveType.Cube);
            arc.transform.SetParent(sparkRoot, false);
            arc.transform.localScale = new Vector3(0.006f, 0.006f, Random.Range(0.06f, 0.14f));
            arc.GetComponent<Renderer>().sharedMaterial = sparkMat;
            arc.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            Destroy(arc.GetComponent<Collider>());
        }
        sparkLight = sparkRoot.gameObject.AddComponent<Light>();
        sparkLight.type = LightType.Point;
        sparkLight.color = sparkColor;
        sparkLight.range = 2.5f;
        sparkLight.shadows = LightShadows.None;
        sparkLight.enabled = false;
        sparkRoot.gameObject.SetActive(false);
    }

    /// <summary>
    /// The lamp itself, without its workings: used here and for the lamp posts by the lone door. Parts go
    /// under parent (its origin on the ground); the bulb, inside the lantern at BulbHeight, gets bulb.
    /// </summary>
    public static void BuildModel(Transform parent, Material bulb)
    {
        // An old Victorian street lamp: black cast iron with brass trim, a stepped plinth and fluted base, a
        // slim pole ringed with collars, a ladder bar with ball finials, and a four-sided glass lantern under
        // a pyramid roof with a finial on top.
        Material metal = MaterialFactory.Lit(new Color(0.045f, 0.05f, 0.05f), 0.55f);
        Material brass = MaterialFactory.Lit(new Color(0.5f, 0.38f, 0.16f), 0.75f);
        Material glass = MaterialFactory.LitTransparent(Texture2D.whiteTexture, new Color(1f, 0.92f, 0.75f, 0.22f), 0.95f);

        // Plinth and base.
        ModelPart(parent, PrimitiveType.Cylinder, metal, new Vector3(0f, 0.05f, 0f), new Vector3(0.62f, 0.05f, 0.62f));
        ModelPart(parent, PrimitiveType.Cylinder, metal, new Vector3(0f, 0.14f, 0f), new Vector3(0.5f, 0.05f, 0.5f));
        ModelPart(parent, PrimitiveType.Cylinder, metal, new Vector3(0f, 0.42f, 0f), new Vector3(0.3f, 0.24f, 0.3f));
        for (int i = 0; i < 8; i++) // flutes round the base
        {
            float a = i * Mathf.PI / 4f;
            ModelPart(parent, PrimitiveType.Cylinder, metal, new Vector3(Mathf.Cos(a) * 0.15f, 0.42f, Mathf.Sin(a) * 0.15f), new Vector3(0.05f, 0.22f, 0.05f));
        }
        ModelPart(parent, PrimitiveType.Cylinder, brass, new Vector3(0f, 0.68f, 0f), new Vector3(0.34f, 0.025f, 0.34f));
        ModelPart(parent, PrimitiveType.Cylinder, metal, new Vector3(0f, 0.76f, 0f), new Vector3(0.2f, 0.06f, 0.2f));

        // The pole, with collars up it.
        var pole = ModelPart(parent, PrimitiveType.Cylinder, metal, new Vector3(0f, PoleHeight * 0.5f, 0f), new Vector3(0.1f, PoleHeight * 0.5f, 0.1f), keepCollider: true);
        pole.name = "Pole";
        foreach (float y in new[] { 1.3f, 2.1f })
            ModelPart(parent, PrimitiveType.Cylinder, brass, new Vector3(0f, y, 0f), new Vector3(0.15f, 0.02f, 0.15f));
        // Ladder bar, with ball finials.
        ModelPart(parent, PrimitiveType.Cube, metal, new Vector3(0f, PoleHeight - 0.55f, 0f), new Vector3(0.7f, 0.035f, 0.035f));
        foreach (float x in new[] { -0.36f, 0.36f })
            ModelPart(parent, PrimitiveType.Sphere, brass, new Vector3(x, PoleHeight - 0.55f, 0f), Vector3.one * 0.07f);
        // Scrolled brackets up to the lantern.
        for (int i = 0; i < 4; i++)
        {
            float a = i * 90f + 45f;
            var bracket = ModelPart(parent, PrimitiveType.Cube, metal, Quaternion.Euler(0f, a, 0f) * new Vector3(0f, PoleHeight - 0.42f, 0.09f), new Vector3(0.025f, 0.2f, 0.025f));
            bracket.transform.localRotation = Quaternion.Euler(0f, a, 0f) * Quaternion.Euler(35f, 0f, 0f);
        }
        ModelPart(parent, PrimitiveType.Cylinder, brass, new Vector3(0f, PoleHeight - 0.33f, 0f), new Vector3(0.18f, 0.02f, 0.18f));

        // The lantern: a tray, corner posts, glass panes, a cap ring, and a pyramid roof with a finial.
        const float half = 0.2f, bottom = PoleHeight - 0.32f, top = PoleHeight + 0.2f;
        ModelPart(parent, PrimitiveType.Cube, metal, new Vector3(0f, bottom, 0f), new Vector3(half * 2f + 0.06f, 0.04f, half * 2f + 0.06f));
        ModelPart(parent, PrimitiveType.Cube, metal, new Vector3(0f, top, 0f), new Vector3(half * 2f + 0.08f, 0.05f, half * 2f + 0.08f));
        for (int i = 0; i < 4; i++)
        {
            float sx = i < 2 ? -1f : 1f, sz = i % 2 == 0 ? -1f : 1f;
            ModelPart(parent, PrimitiveType.Cube, metal, new Vector3(sx * half, (bottom + top) * 0.5f, sz * half), new Vector3(0.025f, top - bottom, 0.025f));
            var pane = ModelPart(parent, PrimitiveType.Cube, glass, Quaternion.Euler(0f, i * 90f, 0f) * new Vector3(0f, (bottom + top) * 0.5f, half), new Vector3(half * 2f - 0.02f, top - bottom - 0.04f, 0.005f));
            pane.transform.localRotation = Quaternion.Euler(0f, i * 90f, 0f);
            pane.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }
        var roof = new GameObject("Roof");
        roof.transform.SetParent(parent, false);
        roof.transform.localPosition = new Vector3(0f, top + 0.025f, 0f);
        roof.AddComponent<MeshFilter>().sharedMesh = Pyramid(half + 0.08f, 0.32f);
        roof.AddComponent<MeshRenderer>().sharedMaterial = metal;
        ModelPart(parent, PrimitiveType.Sphere, brass, new Vector3(0f, top + 0.37f, 0f), Vector3.one * 0.07f);
        ModelPart(parent, PrimitiveType.Cylinder, brass, new Vector3(0f, top + 0.45f, 0f), new Vector3(0.015f, 0.06f, 0.015f));
        ModelPart(parent, PrimitiveType.Sphere, bulb, new Vector3(0f, PoleHeight - 0.05f, 0f), Vector3.one * 0.2f);
    }

    public const float BulbHeight = PoleHeight - 0.05f;

    /// <summary>A four-sided pyramid (a lantern roof), its base centred at the origin, flat-shaded.</summary>
    static Mesh Pyramid(float half, float height)
    {
        var apex = new Vector3(0f, height, 0f);
        var corners = new[] { new Vector3(-half, 0f, -half), new Vector3(-half, 0f, half), new Vector3(half, 0f, half), new Vector3(half, 0f, -half) };
        var verts = new List<Vector3>();
        var tris = new List<int>();
        for (int i = 0; i < 4; i++)
        {
            int b = verts.Count;
            verts.Add(corners[i]);
            verts.Add(apex);
            verts.Add(corners[(i + 1) % 4]);
            tris.AddRange(new[] { b, b + 2, b + 1 }); // wound so the outside faces out
        }
        int u = verts.Count;
        verts.AddRange(corners);
        tris.AddRange(new[] { u, u + 2, u + 1, u, u + 3, u + 2 });
        var mesh = new Mesh { name = "Lantern Roof" };
        mesh.SetVertices(verts);
        mesh.SetTriangles(tris, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }

    static GameObject ModelPart(Transform parent, PrimitiveType type, Material mat, Vector3 pos, Vector3 scale, bool keepCollider = false)
    {
        var go = GameObject.CreatePrimitive(type);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = pos;
        go.transform.localScale = scale;
        go.GetComponent<Renderer>().sharedMaterial = mat;
        if (!keepCollider) Destroy(go.GetComponent<Collider>());
        return go;
    }
}
