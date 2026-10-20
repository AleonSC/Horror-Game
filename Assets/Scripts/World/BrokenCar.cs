using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// A broken-down car abandoned in the woods: rusted, one tyre flat, the windscreen gone. Open the driver's
/// door and click the headlight switch inside to flick the headlights on. Standing in their beams is safe:
/// the entities won't come into them. The battery is nearly dead, though: the lights dim and flicker as it
/// runs down, and once it's flat that's it (until the run restarts). Turning them off saves what's left.
/// DarkForestGenerator parks one somewhere at random.
/// </summary>
public class BrokenCar : MonoBehaviour, SafeZones.IShapedSource
{
    public static readonly List<BrokenCar> All = new List<BrokenCar>();

    [Tooltip("Seconds of headlights the battery has left.")]
    public float batteryTime = 120f;
    [Tooltip("How far the safe beam reaches in front of the car.")]
    public float beamLength = 16f;
    [Tooltip("Half-angle the safe beam spreads at, in degrees.")]
    public float beamSpread = 20f;
    public Color lightColor = new Color(1f, 0.93f, 0.78f);
    public float lightIntensity = 4f;

    public bool LightsOn { get; private set; }
    public float Battery { get; private set; } = 1f;
    public bool IsDead => Battery <= 0f;
    public bool DoorOpen => door != null && door.IsOpen;

    // The car is 4.2m long, nose toward +z; the bumper is at z = 2.1.
    const float FrontZ = 2.1f;

    ShackDoor door;
    readonly List<Light> headlights = new List<Light>();
    readonly List<Renderer> lamps = new List<Renderer>();
    Renderer dashGlow;
    Material lampOff, lampOn;
    AudioSource sfx;
    float flickerTimer;

    bool SafeZones.ISource.IsActive => LightsOn;
    Vector3 SafeZones.ISource.Center => transform.TransformPoint(new Vector3(0f, 0f, FrontZ + beamLength * 0.5f));
    float SafeZones.ISource.Radius => beamLength * 0.5f;

    bool SafeZones.IShapedSource.Contains(Vector3 point)
    {
        Vector3 local = transform.InverseTransformPoint(point);
        float ahead = local.z - FrontZ + 0.5f; // a little behind the bumper counts too
        if (ahead < 0f || ahead > beamLength) return false;
        float halfWidth = 1.1f + ahead * Mathf.Tan(beamSpread * Mathf.Deg2Rad);
        return Mathf.Abs(local.x) < halfWidth;
    }

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

    /// <summary>Called by the switch inside the car.</summary>
    public void ToggleLights()
    {
        if (IsDead)
        {
            Play(ProceduralAudio.LampClick, 0.6f, 0.7f); // click... nothing
            return;
        }
        LightsOn = !LightsOn;
        Noise.Emit(transform.position, 9f);
        Play(ProceduralAudio.LampClick, 0.8f, LightsOn ? 1.2f : 0.9f);
        if (LightsOn) Play(ProceduralAudio.LampSpark, 0.3f, 1.4f);
    }

    /// <summary>Lights off, battery back, door shut (used when the run resets).</summary>
    public void ResetCar()
    {
        LightsOn = false;
        Battery = 1f;
        if (door != null) door.Close();
    }

    void Update()
    {
        if (LightsOn)
        {
            Battery -= Time.deltaTime / Mathf.Max(batteryTime, 0.01f);
            if (Battery <= 0f)
            {
                Battery = 0f;
                LightsOn = false;
                Play(ProceduralAudio.LampPowerDown, 0.7f, 1.2f);
            }
        }

        // Dims as the battery runs down, and stutters badly near the end.
        float level = LightsOn ? Mathf.Lerp(0.35f, 1f, Mathf.Clamp01(Battery / 0.5f)) : 0f;
        if (LightsOn && Battery < 0.2f)
        {
            flickerTimer -= Time.deltaTime;
            if (flickerTimer <= 0f)
            {
                flickerTimer = Random.Range(0.05f, 0.6f) * (Battery / 0.2f + 0.2f);
                if (Random.value < 0.4f) Play(ProceduralAudio.LampSpark, 0.15f, Random.Range(1.2f, 1.6f));
            }
            if (flickerTimer < 0.08f) level *= Random.Range(0.1f, 0.5f);
        }

        foreach (var light in headlights)
        {
            light.enabled = level > 0f;
            light.intensity = lightIntensity * level;
        }
        foreach (var lamp in lamps) lamp.sharedMaterial = level > 0f ? lampOn : lampOff;
        if (dashGlow != null) dashGlow.enabled = level > 0f;
    }

    void Play(AudioClip clip, float volume, float pitch)
    {
        if (sfx == null || clip == null) return;
        sfx.pitch = pitch;
        sfx.PlayOneShot(clip, volume);
    }

    // ---------------------------------------------------------------- Building

    /// <summary>Builds the car around its pivot (on the ground, at the middle of the car).</summary>
    public void Build(System.Random rng)
    {
        float R(float a, float b) => a + (float)rng.NextDouble() * (b - a);

        Color[] paints = { new Color(0.22f, 0.07f, 0.05f), new Color(0.1f, 0.14f, 0.16f), new Color(0.18f, 0.17f, 0.13f), new Color(0.08f, 0.1f, 0.07f) };
        var paint = MaterialFactory.Lit(paints[rng.Next(paints.Length)], 0.25f);
        var rust = MaterialFactory.Lit(new Color(0.2f, 0.09f, 0.04f), 0.05f);
        var dark = MaterialFactory.Lit(new Color(0.03f, 0.03f, 0.03f), 0.1f);
        var tyre = MaterialFactory.Lit(new Color(0.04f, 0.04f, 0.04f), 0.05f);
        var chrome = MaterialFactory.Lit(new Color(0.35f, 0.35f, 0.35f), 0.7f);
        var seat = MaterialFactory.Lit(new Color(0.09f, 0.07f, 0.06f), 0.05f);
        lampOff = MaterialFactory.Lit(new Color(0.5f, 0.5f, 0.45f), 0.9f);
        lampOn = MaterialFactory.NoFog(lightColor * 3f);

        // Leaning: a flat front-left tyre drops that corner.
        var body = new GameObject("Body").transform;
        body.SetParent(transform, false);
        body.localRotation = Quaternion.Euler(2.5f, 0f, 3f); // nose and left side down

        // Lower body (solid: you can't walk through the car) and the roof, with open windows between.
        Solid(paint, body, new Vector3(0f, 0.58f, 0f), new Vector3(1.8f, 0.55f, 4.2f));
        Box(paint, body, new Vector3(0f, 0.9f, 1.35f), new Vector3(1.75f, 0.1f, 1.4f)).localRotation = Quaternion.Euler(-3f, 0f, 0f);    // hood
        Box(paint, body, new Vector3(0f, 0.9f, -1.55f), new Vector3(1.75f, 0.1f, 1.0f));                                               // boot
        Solid(paint, body, new Vector3(0f, 1.42f, -0.3f), new Vector3(1.7f, 0.08f, 1.9f));                                              // roof
        foreach (float x in new[] { -0.82f, 0.82f })
        {
            Box(paint, body, new Vector3(x, 1.15f, 0.62f), new Vector3(0.07f, 0.55f, 0.07f)).localRotation = Quaternion.Euler(-30f, 0f, 0f); // windscreen pillars
            Box(paint, body, new Vector3(x, 1.15f, -1.2f), new Vector3(0.07f, 0.55f, 0.12f)).localRotation = Quaternion.Euler(25f, 0f, 0f);  // rear pillars
        }
        Box(paint, body, new Vector3(0.84f, 1.05f, -0.25f), new Vector3(0.05f, 0.35f, 1.3f)); // passenger door top (window wound up, broken)
        // Rust patches and scrapes.
        for (int i = 0; i < 6; i++)
            Box(rust, body, new Vector3(R(-1f, 1f) > 0 ? 0.905f : -0.905f, R(0.4f, 0.8f), R(-1.8f, 1.8f)), new Vector3(0.02f, R(0.1f, 0.3f), R(0.2f, 0.7f)));
        Box(rust, body, new Vector3(R(-0.5f, 0.5f), 0.96f, R(0.9f, 1.6f)), new Vector3(R(0.3f, 0.6f), 0.02f, R(0.2f, 0.5f)));

        // Bumpers, grille, lamps.
        Box(chrome, body, new Vector3(0f, 0.38f, 2.13f), new Vector3(1.85f, 0.12f, 0.1f));
        Box(chrome, body, new Vector3(0f, 0.38f, -2.13f), new Vector3(1.85f, 0.12f, 0.1f));
        Box(dark, body, new Vector3(0f, 0.65f, 2.11f), new Vector3(0.8f, 0.2f, 0.03f));
        foreach (float x in new[] { -0.65f, 0.65f })
        {
            var lamp = Part(PrimitiveType.Cylinder, lampOff, body, new Vector3(x, 0.66f, 2.1f), new Vector3(0.24f, 0.03f, 0.24f));
            lamp.localRotation = Quaternion.Euler(90f, 0f, 0f);
            lamps.Add(lamp.GetComponent<Renderer>());

            var light = new GameObject("Headlight").AddComponent<Light>();
            light.transform.SetParent(body, false);
            light.transform.localPosition = new Vector3(x, 0.66f, 2.2f);
            light.transform.localRotation = Quaternion.Euler(6f, x * 6f, 0f);
            light.type = LightType.Spot;
            light.color = lightColor;
            light.range = beamLength * 1.6f;
            light.spotAngle = 55f;
            light.innerSpotAngle = 25f;
            light.shadows = x < 0f ? LightShadows.Soft : LightShadows.None; // one shadowed beam is enough
            light.enabled = false;
            headlights.Add(light);

            Box(MaterialFactory.Lit(new Color(0.3f, 0.03f, 0.02f), 0.6f), body, new Vector3(x, 0.7f, -2.1f), new Vector3(0.3f, 0.12f, 0.03f)); // tail lights
        }

        // Wheels, the front left one flat.
        foreach (var w in new[] { new Vector3(-0.85f, 0.32f, 1.35f), new Vector3(0.85f, 0.32f, 1.35f), new Vector3(-0.85f, 0.32f, -1.35f), new Vector3(0.85f, 0.32f, -1.35f) })
        {
            bool flat = w.x < 0f && w.z > 0f;
            var wheel = Part(PrimitiveType.Cylinder, tyre, transform, flat ? w + Vector3.down * 0.12f : w,
                flat ? new Vector3(0.72f, 0.12f, 0.5f) : new Vector3(0.64f, 0.12f, 0.64f));
            wheel.localRotation = Quaternion.Euler(0f, 0f, 90f);
            Part(PrimitiveType.Cylinder, chrome, wheel, new Vector3(0f, w.x < 0f ? 0.6f : -0.6f, 0f), new Vector3(0.45f, 0.1f, 0.45f)); // hubcap, outer side
        }

        // Inside: seats, dashboard, steering wheel.
        Box(seat, body, new Vector3(-0.4f, 0.95f, -0.2f), new Vector3(0.55f, 0.15f, 0.55f));
        Box(seat, body, new Vector3(-0.4f, 1.2f, -0.5f), new Vector3(0.55f, 0.55f, 0.12f)).localRotation = Quaternion.Euler(-12f, 0f, 0f);
        Box(seat, body, new Vector3(0.4f, 0.95f, -0.2f), new Vector3(0.55f, 0.15f, 0.55f));
        Box(seat, body, new Vector3(0.4f, 1.2f, -0.5f), new Vector3(0.55f, 0.55f, 0.12f)).localRotation = Quaternion.Euler(-5f, 0f, 0f);
        Box(seat, body, new Vector3(0f, 1.0f, -1.0f), new Vector3(1.5f, 0.15f, 0.5f)); // back seat
        Box(dark, body, new Vector3(0f, 1.03f, 0.62f), new Vector3(1.65f, 0.22f, 0.3f)); // dashboard
        var steering = Part(PrimitiveType.Cylinder, dark, body, new Vector3(-0.4f, 1.12f, 0.38f), new Vector3(0.36f, 0.015f, 0.36f));
        steering.localRotation = Quaternion.Euler(60f, 0f, 0f);
        var glow = Part(PrimitiveType.Cube, MaterialFactory.NoFog(new Color(0.3f, 0.8f, 0.4f) * 0.6f), body, new Vector3(-0.4f, 1.1f, 0.47f), new Vector3(0.2f, 0.06f, 0.01f));
        glow.localRotation = Quaternion.Euler(-30f, 0f, 0f);
        dashGlow = glow.GetComponent<Renderer>();
        dashGlow.enabled = false;

        // The headlight switch, a knob on the dash beside the wheel. Click it through the open door.
        var knob = Part(PrimitiveType.Cylinder, chrome, body, new Vector3(-0.68f, 1.06f, 0.48f), new Vector3(0.05f, 0.02f, 0.05f));
        knob.localRotation = Quaternion.Euler(90f, 0f, 0f);
        var switchGo = new GameObject("Headlight Switch");
        switchGo.transform.SetParent(body, false);
        switchGo.transform.localPosition = new Vector3(-0.62f, 1.06f, 0.45f);
        var trigger = switchGo.AddComponent<BoxCollider>();
        trigger.isTrigger = true;
        trigger.size = new Vector3(0.45f, 0.3f, 0.35f);
        switchGo.AddComponent<CarHeadlightSwitch>().car = this;

        // The driver's door, hinged at its front edge.
        var hinge = new GameObject("Driver Door").transform;
        hinge.SetParent(body, false);
        hinge.localPosition = new Vector3(-0.88f, 0f, 0.5f);
        var panel = Solid(paint, hinge, new Vector3(-0.02f, 0.62f, -0.6f), new Vector3(0.06f, 0.62f, 1.15f));
        Box(paint, hinge, new Vector3(-0.02f, 1.12f, -0.6f), new Vector3(0.04f, 0.04f, 1.1f)); // window frame
        Box(chrome, hinge, new Vector3(-0.07f, 0.8f, -1.05f), new Vector3(0.03f, 0.03f, 0.16f)); // handle
        // Collision for the window part too, so a shut door blocks the switch.
        var windowBlock = panel.gameObject.AddComponent<BoxCollider>();
        windowBlock.center = new Vector3(0f, 0.85f, 0f);
        windowBlock.size = new Vector3(1f, 0.75f, 1f);
        door = hinge.gameObject.AddComponent<ShackDoor>();
        door.openAngle = 70f; // swings outward

        sfx = ProceduralAudio.AddSource(gameObject, null, false, true, 0.8f, 2f, 30f);
    }

    Transform Box(Material mat, Transform parent, Vector3 pos, Vector3 scale) => Part(PrimitiveType.Cube, mat, parent, pos, scale);

    Transform Solid(Material mat, Transform parent, Vector3 pos, Vector3 scale) => Part(PrimitiveType.Cube, mat, parent, pos, scale, true);

    static Transform Part(PrimitiveType type, Material mat, Transform parent, Vector3 pos, Vector3 scale, bool keepCollider = false)
    {
        var go = GameObject.CreatePrimitive(type);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = pos;
        go.transform.localScale = scale;
        go.GetComponent<Renderer>().sharedMaterial = mat;
        if (!keepCollider)
        {
            var col = go.GetComponent<Collider>();
            if (Application.isPlaying) Destroy(col);
            else DestroyImmediate(col);
        }
        return go.transform;
    }
}

/// <summary>The headlight switch inside the car. LampInteractor clicks it.</summary>
public class CarHeadlightSwitch : MonoBehaviour
{
    public BrokenCar car;
}
