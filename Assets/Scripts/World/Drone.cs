using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>
/// The remote-control drone from the cabin's store room. Carried, it's an item in the hotbar; F sets it
/// down on the ground in front of you, and its slot becomes the remote. With the remote in hand, F flies it
/// (from wherever you are): your view is its camera, looking out ahead of it with its little lamp on, and you
/// stand where you are, deaf and blind to what's round you except for what you can hear. F again puts the
/// remote down and the drone holds where it is, hovering, a red light flashing on it that can be seen across
/// the sky (it shows on the ground too); bring it down to the ground before letting go and it lands there,
/// to be picked up again (it goes back to being the drone).
///
/// Flying: WASD, the mouse to turn and look, Space to climb, Ctrl (or C) to sink, Shift to go faster. The
/// signal only reaches so far from you: the picture breaks up toward the edge of it and it won't go further.
/// Its motors buzz, loud enough that things listening hear it (the blind one comes to it), and if something
/// comes near you while you're flying, the screen warns you. Getting caught, grabbed, or a crawler on your
/// face cuts the link at once. While it's being flown, the blind one senses it from far off and goes for it.
///
/// Battery (Charge, kept with the drone when it's picked up): about flightTime of flying; left alone (set
/// down or hovering) it trickles away at a tenth of that. When it runs out the drone dies: if it's in the
/// air it drops out of the sky, and lies wherever it fell for you to find.
/// </summary>
public class DroneUnit : MonoBehaviour
{
    /// <summary>The drone, set down somewhere in the world (only ever one).</summary>
    public static DroneUnit Deployed { get; private set; }
    /// <summary>True while the player is flying it (their own body standing still).</summary>
    public static bool Piloting => Deployed != null && Deployed.piloting;
    internal static void ResetStatics() { Deployed = null; Charge = 1f; StaticReset.Drop(ref staticTexture); }

    /// <summary>The drone's battery, 1 full to 0 dead (there's only the one drone; it keeps it when picked up).</summary>
    public static float Charge = 1f;
    public static bool Dead => Charge <= 0f;
    [Tooltip("Seconds of flying on a full battery.")]
    public float flightTime = 600f;
    [Range(0f, 1f), Tooltip("Left alone, it drains at this fraction of the flying rate.")]
    public float idleDrain = 0.1f;
    bool falling;

    [Tooltip("Top speed, flying flat out (Shift: a good deal more).")]
    public float speed = 5f;
    public float boostSpeed = 10f;
    public float climbSpeed = 3f;
    [Tooltip("How far from you the signal reaches.")]
    public float range = 300f;
    [Tooltip("How high above the ground it can climb.")]
    public float maxAltitude = 45f;
    public float lookSensitivity = 1f;
    [Tooltip("How far its motors are heard by things listening (the blind one comes to it).")]
    public float buzzNoise = 9f;
    [Range(0f, 1f)] public float volume = 0.6f;

    bool piloting, landing, hovering;
    [Tooltip("Let go of it lower than this (metres above the ground) and it lands; higher, and it hovers there.")]
    public float landBelow = 1.5f;
    [Tooltip("Seconds between flashes of its beacon while it's left alone.")]
    public float beaconEvery = 1.2f;
    Transform beacon;
    Material beaconMat;
    Light beaconLight;
    float beaconClock, hoverY;
    int startedFrame, stoppedFrame = -1;
    float yaw, camPitch, buzzTimer;
    Vector3 velocity;
    Transform body, camPivot;
    readonly List<Transform> rotors = new List<Transform>();
    Camera view;
    Light lamp;
    AudioSource motors, cockpit;
    FirstPersonController player;
    Camera mainCam;
    int mainMask;
    BoxCollider grab;
    ItemPickup pickup;
    DarkForestGenerator generator;
    static Texture2D staticTexture;
    float staticAt;
    GUIStyle hudStyle, bigStyle;

    // ---------------------------------------------------------------- Placing and picking up

    /// <summary>Sets the drone down at a spot on the ground (replacing any already set down).</summary>
    public static DroneUnit Place(Vector3 at, float facing)
    {
        ClearAll();
        var go = new GameObject("Drone");
        go.transform.SetPositionAndRotation(at, Quaternion.Euler(0f, facing, 0f));
        var drone = go.AddComponent<DroneUnit>();
        drone.yaw = facing;
        return drone;
    }

    /// <summary>Removes the drone from the world (the run resets).</summary>
    public static void ClearAll()
    {
        if (Deployed == null) return;
        Deployed.StopPiloting();
        Destroy(Deployed.gameObject);
        Deployed = null;
        Charge = 1f; // (the run resets: a fresh drone, charged)
    }

    void Awake()
    {
        Tuning.Apply(this);
        Deployed = this;
        body = new GameObject("Body").transform;
        body.SetParent(transform, false);
        body.localPosition = Vector3.up * 0.09f; // resting on its legs
        BuildModel(body, rotors, out lamp);
        lamp.enabled = false;

        // The camera under its nose.
        camPivot = new GameObject("Camera Pivot").transform;
        camPivot.SetParent(body, false);
        camPivot.localPosition = new Vector3(0f, -0.03f, 0.14f);

        motors = ProceduralAudio.AddSource(gameObject, ProceduralAudio.LampHum, true, true, 0f, 2f, 40f);
        cockpit = ProceduralAudio.AddSource(gameObject, ProceduralAudio.LampHum, true, false, 0f);

        // Sitting on the ground it can be picked up again, as the drone.
        grab = gameObject.AddComponent<BoxCollider>();
        grab.isTrigger = true;
        grab.center = new Vector3(0f, 0.12f, 0f);
        grab.size = new Vector3(0.7f, 0.3f, 0.7f);
        pickup = gameObject.AddComponent<ItemPickup>();
        pickup.item = Hotbar.Item.Drone;
        WorldWrap.RegisterDynamic(transform);
        BuildBeacon();
    }

    /// <summary>The beacon: a soft red glow that flashes, kept the same size on screen however far off it is.</summary>
    void BuildBeacon()
    {
        Shader glow = Shader.Find("Horror/Glow");
        var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
        quad.name = "Drone Beacon";
        Destroy(quad.GetComponent<Collider>());
        beacon = quad.transform;
        beaconMat = glow != null ? new Material(glow) : MaterialFactory.NoFog(new Color(1f, 0.1f, 0.05f) * 3f);
        beaconMat.SetColor("_Color", new Color(1f, 0.12f, 0.06f));
        if (beaconMat.HasProperty("_Intensity")) beaconMat.SetFloat("_Intensity", 2.5f);
        var r = quad.GetComponent<Renderer>();
        r.sharedMaterial = beaconMat;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        r.receiveShadows = false;
        quad.SetActive(false);

        beaconLight = new GameObject("Beacon Light").AddComponent<Light>();
        beaconLight.transform.SetParent(body, false);
        beaconLight.transform.localPosition = Vector3.up * 0.06f;
        beaconLight.type = LightType.Point;
        beaconLight.color = new Color(1f, 0.12f, 0.06f);
        beaconLight.range = 6f;
        beaconLight.intensity = 2f;
        beaconLight.shadows = LightShadows.None;
        beaconLight.enabled = false;
    }

    /// <summary>Flashing while it's left alone, wherever it is; drawn nearer (and smaller) past the camera's reach.</summary>
    void LateUpdate()
    {
        UpdateLampGlows();
        DrawHeat();
        if (beacon == null) return;
        var cam = Camera.main;
        beaconClock += Time.deltaTime;
        bool flash = !piloting && !Dead && cam != null && Mathf.Repeat(beaconClock, beaconEvery) < 0.14f;
        beacon.gameObject.SetActive(flash);
        if (beaconLight != null) beaconLight.enabled = flash;
        if (!flash) return;
        Vector3 eye = cam.transform.position;
        Vector3 at = body.position + body.up * 0.07f;
        Vector3 to = at - eye;
        float d = to.magnitude;
        float reach = cam.farClipPlane * 0.85f;
        if (d > reach) { at = eye + to * (reach / d); d = reach; }
        beacon.position = at;
        beacon.rotation = Quaternion.LookRotation(at - eye, cam.transform.up);
        beacon.localScale = Vector3.one * Mathf.Max(0.25f, d * 0.02f);
    }

    // ---------------------------------------------------------------- Lamps, from up here

    [Tooltip("Through the drone's camera, how big the glow round each lit lamp is (on screen, as a share of the distance).")]
    public float lampGlowSize = 0.035f;
    readonly List<Transform> lampGlows = new List<Transform>();
    Material lampGlowMat;

    /// <summary>
    /// Seen through the drone's camera, the lit lamps shine out through the fog and the dark as soft warm
    /// glows, so you can see where they are from up above (only through the drone: your own eyes see them as
    /// usual). Unlit ones show as faint dim points.
    /// </summary>
    void UpdateLampGlows()
    {
        int used = 0;
        if (piloting && view != null)
        {
            if (lampGlowMat == null)
            {
                Shader glow = Shader.Find("Horror/Glow");
                lampGlowMat = glow != null ? new Material(glow) : MaterialFactory.NoFog(new Color(1f, 0.75f, 0.45f));
                lampGlowMat.SetColor("_Color", Color.white);
            }
            Vector3 eye = view.transform.position;
            float reach = view.farClipPlane * 0.85f;
            foreach (var lamp in Lamp.All)
            {
                if (lamp == null) continue;
                Vector3 at = WorldWrap.NearestImage(lamp.BulbPosition, eye);
                Vector3 to = at - eye;
                float d = to.magnitude;
                if (d < 2f) continue;
                if (d > reach) { at = eye + to * (reach / d); d = reach; }
                if (used == lampGlows.Count)
                {
                    var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
                    quad.name = "Lamp Glow (drone)";
                    Destroy(quad.GetComponent<Collider>());
                    var r = quad.GetComponent<Renderer>();
                    r.sharedMaterial = lampGlowMat;
                    r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    r.receiveShadows = false;
                    lampGlows.Add(quad.transform);
                }
                var g = lampGlows[used++];
                g.gameObject.SetActive(true);
                g.SetPositionAndRotation(at, Quaternion.LookRotation(at - eye, view.transform.up));
                // The dark ones (the ones still to find) stand out most: bigger, cold blue-white, pulsing.
                float pulse = 0.65f + 0.35f * Mathf.Sin(Time.time * 4f + lamp.transform.position.x);
                float size = Mathf.Max(0.6f, d * lampGlowSize) * (lamp.IsLit ? 1f : 1.6f * (0.85f + 0.15f * pulse));
                g.localScale = Vector3.one * size;
                var block = new MaterialPropertyBlock();
                Color c = lamp.IsLit ? lamp.lightColor * 1.4f : new Color(0.55f, 0.85f, 1f) * (1.6f * pulse);
                block.SetColor("_Color", c);
                g.GetComponent<Renderer>().SetPropertyBlock(block);
            }
        }
        for (int i = used; i < lampGlows.Count; i++)
            if (lampGlows[i] != null && lampGlows[i].gameObject.activeSelf) lampGlows[i].gameObject.SetActive(false);
    }

    void OnDestroy()
    {
        foreach (var g in lampGlows) if (g != null) Destroy(g.gameObject);
        if (lampGlowMat != null) Destroy(lampGlowMat);
        if (beacon != null) Destroy(beacon.gameObject);
        if (beaconMat != null) Destroy(beaconMat);
        if (piloting) StopPiloting();
        if (Deployed == this) Deployed = null;
    }

    // ---------------------------------------------------------------- Flying

    /// <summary>Takes control of it from wherever the player is.</summary>
    public void StartPiloting(FirstPersonController pilot)
    {
        if (piloting || pilot == null || Time.frameCount == stoppedFrame || Dead || falling) return; // (the same F that let go)
        player = pilot;
        mainCam = Camera.main;
        if (mainCam == null) return;
        piloting = true;
        landing = false;
        hovering = false;
        startedFrame = Time.frameCount;
        grab.enabled = false;
        lamp.enabled = true;

        if (view == null)
        {
            view = new GameObject("Drone Camera").AddComponent<Camera>();
            view.transform.SetParent(camPivot, false);
            view.CopyFrom(mainCam);
            view.transform.localPosition = Vector3.zero;
            view.transform.localRotation = Quaternion.identity;
            view.nearClipPlane = 0.05f;
            view.ResetProjectionMatrix(); // (the player's camera sets its own)
            view.fieldOfView = 75f; // a wide little lens
            var data = view.GetUniversalAdditionalCameraData();
            if (data != null)
            {
                data.requiresDepthTexture = true; // (the mist needs it)
                data.renderPostProcessing = true;
            }
        }
        view.depth = mainCam.depth + 1f;
        view.enabled = true;
        // Your own eyes see nothing while you're looking through it (they're still there, for everything
        // that asks where you're looking from, and you still hear from where you stand).
        mainMask = mainCam.cullingMask;
        mainCam.cullingMask = 0;
        ProceduralAudio.PlayAt(ProceduralAudio.FobClick, transform.position, 0.5f, 1.4f, 2f);
    }

    /// <summary>Lets go of it: your view comes back, and it hovers where it is (or, low down, lands).</summary>
    public void StopPiloting()
    {
        if (!piloting) return;
        piloting = false;
        float altitude = transform.position.y - Ground(transform.position);
        landing = altitude < landBelow;
        hovering = !landing;
        hoverY = transform.position.y;
        stoppedFrame = Time.frameCount;
        SetThermal(false);
        if (view != null) view.enabled = false;
        if (mainCam != null) mainCam.cullingMask = mainMask;
    }

    void Update()
    {
        float dt = Time.deltaTime;
        if (generator == null) generator = FindAnyObjectByType<DarkForestGenerator>();

        // The battery: flying uses it; left alone it trickles away. Flat, it dies (and falls, if it's up).
        if (!Dead && !PauseMenu.IsPaused)
        {
            Charge = Mathf.Max(0f, Charge - dt / Mathf.Max(flightTime, 1f) * (piloting ? (thermal ? thermalDrain : 1f) : idleDrain));
            if (Dead) Die();
        }
        if (falling) Fall(dt);

        bool airborne = piloting || landing || hovering;
        float spin = airborne ? 1f : 0f;
        motors.volume = Mathf.MoveTowards(motors.volume, airborne ? volume : 0f, dt * 2f);
        cockpit.volume = Mathf.MoveTowards(cockpit.volume, piloting ? volume * 0.35f : 0f, dt * 3f);
        motors.pitch = cockpit.pitch = 2.4f + velocity.magnitude * 0.06f;
        if (airborne && !motors.isPlaying) { motors.Play(); cockpit.Play(); }
        else if (!airborne && motors.volume <= 0f && motors.isPlaying) { motors.Stop(); cockpit.Stop(); }
        foreach (var r in rotors) r.Rotate(Vector3.up, 2600f * spin * dt, Space.Self);

        if (piloting)
        {
            if (PauseMenu.IsPaused) return;
            bool cut = GameOverScreen.IsActive || Crawler.Holding != null || ShadowEntity.Capturing != null
                       || player == null || PortalDoor.PlayerInRoom;
            if (cut || HorrorInput.InteractPressed)
            {
                StopPiloting();
                return;
            }
            if (HorrorInput.FlashlightPressed && Time.frameCount != startedFrame) SetThermal(!thermal);
            Fly(dt);
            Buzz(dt);
        }
        else if (hovering)
        {
            // Holding its place in the air, bobbing a little in the wind, buzzing away.
            velocity = Vector3.Lerp(velocity, Vector3.zero, 1f - Mathf.Exp(-2f * dt));
            Vector3 p = transform.position + velocity * dt;
            p.y = hoverY + Mathf.Sin(Time.time * 1.3f) * 0.08f;
            transform.position = p;
            body.localRotation = Quaternion.Slerp(body.localRotation, Quaternion.Euler(Mathf.Sin(Time.time * 0.9f) * 2f, 0f, Mathf.Sin(Time.time * 1.1f) * 2f), 1f - Mathf.Exp(-3f * dt));
            Buzz(dt);
        }
        else if (landing)
        {
            // Sinking down to settle on the ground.
            velocity = Vector3.Lerp(velocity, Vector3.down * 1.2f, 1f - Mathf.Exp(-3f * dt));
            Vector3 p = transform.position + velocity * dt;
            float ground = Ground(p);
            if (Physics.SphereCast(transform.position + Vector3.up * 0.2f, 0.2f, Vector3.down, out var hit, 0.2f + velocity.magnitude * dt + 0.05f, ~0, QueryTriggerInteraction.Ignore))
                ground = Mathf.Max(ground, hit.point.y);
            if (p.y <= ground)
            {
                p.y = ground;
                landing = false;
                velocity = Vector3.zero;
                grab.enabled = true;
                lamp.enabled = false;
            }
            transform.position = p;
            body.localRotation = Quaternion.Slerp(body.localRotation, Quaternion.identity, 1f - Mathf.Exp(-4f * dt));
        }
    }

    /// <summary>The battery's gone: the link drops, the motors stop, and down it comes (or just goes dark).</summary>
    void Die()
    {
        StopPiloting();
        hovering = landing = false;
        lamp.enabled = false;
        ProceduralAudio.PlayAt(ProceduralAudio.LampPowerDown, transform.position, 0.7f, 1.6f, 3f);
        float altitude = transform.position.y - Ground(transform.position);
        falling = altitude > 0.05f;
        if (!falling) grab.enabled = true;
    }

    /// <summary>Dropping out of the sky, tumbling, until it hits the ground (or something on the way).</summary>
    void Fall(float dt)
    {
        velocity += Vector3.down * 9.8f * dt;
        velocity.x *= 1f - dt * 0.5f;
        velocity.z *= 1f - dt * 0.5f;
        Vector3 step = velocity * dt;
        float ground = Ground(transform.position + step);
        bool hitSomething = Physics.SphereCast(transform.position + Vector3.up * 0.12f, 0.15f, step.normalized, out var hit, step.magnitude + 0.02f, ~0, QueryTriggerInteraction.Ignore);
        body.localRotation *= Quaternion.Euler(140f * dt, 60f * dt, 90f * dt); // tumbling
        if (transform.position.y + step.y <= ground || (hitSomething && hit.normal.y > 0.5f))
        {
            Vector3 p = transform.position + step;
            p.y = hitSomething && hit.normal.y > 0.5f ? Mathf.Max(hit.point.y, ground) : ground;
            transform.position = p;
            body.localRotation = Quaternion.Euler(Random.Range(-25f, 25f), Random.Range(0f, 360f), Random.Range(150f, 210f) * (Random.value < 0.5f ? 1f : 0f) + Random.Range(-20f, 20f));
            body.localPosition = Vector3.up * 0.06f;
            falling = false;
            velocity = Vector3.zero;
            grab.enabled = true;
            ProceduralAudio.PlayAt(ProceduralAudio.TwigSnap, transform.position, 0.9f, 0.55f, 3f); // the crack as it lands
            Noise.Emit(transform.position, 8f);
            return;
        }
        if (hitSomething) velocity = Vector3.Reflect(velocity, hit.normal) * 0.3f; // glancing off a branch or a wall
        else transform.position += step;
    }

    // ---------------------------------------------------------------- Thermal

    [Tooltip("Thermal mode drains the battery this many times as fast.")]
    public float thermalDrain = 2f;
    [Range(0f, 1f), Tooltip("How much of the fog the thermal camera still sees.")]
    public float thermalFog = 0.08f;
    [Tooltip("The cold world's brightness in thermal: an even grey light on everything, so the shapes of the land and trees read.")]
    public Color thermalAmbient = new Color(0.32f, 0.32f, 0.34f);
    GroundMist mist;
    bool mistWasOn;
    Color ambientKept;
    UnityEngine.Rendering.AmbientMode ambientModeKept;
    public Color heatColor = new Color(1f, 0.92f, 0.8f);
    bool thermal;
    Volume thermalVolume;
    Material heatMat;
    float fogKept = -1f;
    const int ThermalLayer = 31; // (a spare layer: only the drone's camera looks at it for its post-processing)
    readonly Dictionary<Component, MeshFilter[]> heatParts = new Dictionary<Component, MeshFilter[]>();
    float heatRefresh;
    Mesh bodyMesh;

    /// <summary>
    /// Thermal (F while flying): the picture goes grey and stark, the fog mostly gone from it, and anything
    /// warm (the hunters, crawlers, you) burns white. Easier to see by, at twice the battery.
    /// </summary>
    void SetThermal(bool on)
    {
        if (thermal == on) return;
        thermal = on;
        if (on)
        {
            if (thermalVolume == null)
            {
                var go = new GameObject("Drone Thermal") { layer = ThermalLayer };
                go.transform.SetParent(transform, false);
                thermalVolume = go.AddComponent<Volume>();
                thermalVolume.isGlobal = true;
                thermalVolume.priority = 50f;
                var profile = ScriptableObject.CreateInstance<VolumeProfile>();
                var grade = profile.Add<ColorAdjustments>(true);
                grade.saturation.Override(-100f);
                grade.contrast.Override(45f);
                grade.postExposure.Override(1.2f);
                grade.colorFilter.Override(new Color(0.92f, 0.95f, 1f));
                var grain = profile.Add<FilmGrain>(true);
                grain.intensity.Override(0.35f);
                var vig = profile.Add<Vignette>(true);
                vig.intensity.Override(0.35f);
                thermalVolume.sharedProfile = profile;
            }
            if (heatMat == null) heatMat = MaterialFactory.NoFog(heatColor * 3f);
            if (view != null)
            {
                var data = view.GetUniversalAdditionalCameraData();
                if (data != null) data.volumeLayerMask |= 1 << ThermalLayer;
            }
        }
        if (thermalVolume != null) thermalVolume.gameObject.SetActive(on);
        ProceduralAudio.PlayAt(ProceduralAudio.FobClick, transform.position, 0.4f, on ? 1.6f : 1.2f, 2f);
    }

    void OnEnable()
    {
        RenderPipelineManager.beginCameraRendering += BeforeRender;
        RenderPipelineManager.endCameraRendering += AfterRender;
    }

    void OnDisable()
    {
        RenderPipelineManager.beginCameraRendering -= BeforeRender;
        RenderPipelineManager.endCameraRendering -= AfterRender;
        if (fogKept >= 0f)
        {
            RenderSettings.fogDensity = fogKept;
            fogKept = -1f;
            RenderSettings.ambientMode = ambientModeKept;
            RenderSettings.ambientLight = ambientKept;
            if (mist != null && mist.MistRenderer != null) mist.MistRenderer.enabled = mistWasOn;
        }
    }

    // Just for the thermal camera's own picture: nearly no fog, none of the ground mist, and the cold world
    // lit evenly grey so the land and the trees read as shapes.
    void BeforeRender(ScriptableRenderContext context, Camera cam)
    {
        if (!thermal || cam != view) return;
        fogKept = RenderSettings.fogDensity;
        RenderSettings.fogDensity = fogKept * thermalFog;
        ambientKept = RenderSettings.ambientLight;
        ambientModeKept = RenderSettings.ambientMode;
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
        RenderSettings.ambientLight = thermalAmbient;
        if (mist == null) mist = FindAnyObjectByType<GroundMist>();
        if (mist != null && mist.MistRenderer != null)
        {
            mistWasOn = mist.MistRenderer.enabled;
            mist.MistRenderer.enabled = false;
        }
    }

    void AfterRender(ScriptableRenderContext context, Camera cam)
    {
        if (cam != view || fogKept < 0f) return;
        RenderSettings.fogDensity = fogKept;
        fogKept = -1f;
        RenderSettings.ambientMode = ambientModeKept;
        RenderSettings.ambientLight = ambientKept;
        if (mist != null && mist.MistRenderer != null) mist.MistRenderer.enabled = mistWasOn;
    }

    /// <summary>Everything warm, drawn again in white heat (for the drone's camera only).</summary>
    void DrawHeat()
    {
        if (!thermal || !piloting || view == null) return;
        heatRefresh -= Time.deltaTime;
        if (heatRefresh <= 0f)
        {
            heatRefresh = 1f;
            heatParts.Clear();
            foreach (var e in ShadowEntity.All) if (e != null) heatParts[e] = e.GetComponentsInChildren<MeshFilter>();
            foreach (var b in BlindEntity.All) if (b != null) heatParts[b] = b.GetComponentsInChildren<MeshFilter>();
            foreach (var c in Crawler.All) if (c != null) heatParts[c] = c.GetComponentsInChildren<MeshFilter>();
            foreach (var f in FlyingEntity.All) if (f != null) heatParts[f] = f.GetComponentsInChildren<MeshFilter>();
        }
        foreach (var pair in heatParts)
        {
            if (pair.Key == null || !pair.Key.gameObject.activeInHierarchy) continue;
            if (pair.Key is ShadowEntity se && !se.IsVisible) continue;
            if (pair.Key is BlindEntity be && !be.IsPresent) continue;
            foreach (var f in pair.Value)
            {
                if (f == null || f.sharedMesh == null) continue;
                var r = f.GetComponent<Renderer>();
                if (r == null || !r.enabled || !r.gameObject.activeInHierarchy) continue;
                Graphics.DrawMesh(f.sharedMesh, f.transform.localToWorldMatrix * Matrix4x4.Scale(Vector3.one * 1.04f), heatMat, 0, view);
            }
        }
        // And you, standing where you left yourself.
        if (player != null)
        {
            if (bodyMesh == null)
            {
                var capsule = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                bodyMesh = capsule.GetComponent<MeshFilter>().sharedMesh;
                Destroy(capsule);
            }
            Vector3 you = WorldWrap.NearestImage(player.transform.position, transform.position);
            Graphics.DrawMesh(bodyMesh, Matrix4x4.TRS(you + Vector3.up * 0.9f, Quaternion.identity, new Vector3(0.5f, 0.9f, 0.5f)), heatMat, 0, view);
        }
    }

    /// <summary>Its motors, heard by things listening.</summary>
    void Buzz(float dt)
    {
        buzzTimer -= dt;
        if (buzzTimer > 0f) return;
        buzzTimer = 1.5f;
        Noise.Emit(transform.position, buzzNoise);
    }

    void Fly(float dt)
    {
        // Turning, and the camera tilting up and down.
        Vector2 look = HorrorInput.Look * lookSensitivity * (player != null ? player.mouseSensitivity : 1f);
        yaw += look.x;
        camPitch = Mathf.Clamp(camPitch - look.y, -70f, 35f);
        camPivot.localRotation = Quaternion.Euler(camPitch, 0f, 0f);

        Vector2 move = Vector2.ClampMagnitude(HorrorInput.Move, 1f);
        float top = HorrorInput.RunHeld ? boostSpeed : speed;
        Quaternion heading = Quaternion.Euler(0f, yaw, 0f);
        Vector3 want = heading * new Vector3(move.x, 0f, move.y) * top;
        want.y = (HorrorInput.JumpHeld ? climbSpeed : 0f) - (HorrorInput.CrouchHeld ? climbSpeed : 0f);
        velocity = Vector3.Lerp(velocity, want, 1f - Mathf.Exp(-2.5f * dt));

        // Bumping into things rather than through them.
        Vector3 step = velocity * dt;
        if (step.sqrMagnitude > 0.000001f && Physics.SphereCast(transform.position + Vector3.up * 0.12f, 0.22f, step.normalized, out var hit, step.magnitude, ~0, QueryTriggerInteraction.Ignore))
        {
            step = Vector3.ProjectOnPlane(step, hit.normal) * 0.5f;
            velocity = Vector3.ProjectOnPlane(velocity, hit.normal) * 0.5f;
        }
        Vector3 p = transform.position + step;

        // Above the ground, under its ceiling, and within reach of the signal.
        float ground = Ground(p);
        p.y = Mathf.Clamp(p.y, ground + 0.2f, ground + maxAltitude);
        Vector3 you = WorldWrap.NearestImage(player.transform.position, p);
        Vector3 off = p - you;
        off.y = 0f;
        if (off.magnitude > range)
        {
            p = new Vector3(you.x, p.y, you.z) + off.normalized * range;
            velocity = Vector3.ProjectOnPlane(velocity, off.normalized);
        }
        transform.position = p;
        transform.rotation = heading;

        // Tilting into the way it's going, as a drone does.
        Vector3 local = Quaternion.Inverse(heading) * velocity;
        body.localRotation = Quaternion.Slerp(body.localRotation, Quaternion.Euler(local.z * 3f, 0f, -local.x * 3f), 1f - Mathf.Exp(-6f * dt));
    }

    float Ground(Vector3 p) => generator != null ? generator.GetGroundHeight(p.x, p.z) : p.y - 1f;

    /// <summary>0 with a strong signal, rising toward 1 at the edge of its range.</summary>
    float SignalLoss
    {
        get
        {
            if (player == null) return 0f;
            Vector3 you = WorldWrap.NearestImage(player.transform.position, transform.position);
            Vector3 d = transform.position - you;
            d.y = 0f;
            return Mathf.Clamp01(Mathf.InverseLerp(range * 0.65f, range, d.magnitude));
        }
    }

    /// <summary>True if something is close to where you're standing.</summary>
    bool DangerNearYou()
    {
        if (player == null) return false;
        Vector3 you = player.transform.position;
        foreach (var e in ShadowEntity.All)
            if (e != null && e.IsVisible && (WorldWrap.NearestImage(e.transform.position, you) - you).sqrMagnitude < 14f * 14f) return true;
        foreach (var b in BlindEntity.All)
            if (b != null && b.IsPresent && (WorldWrap.NearestImage(b.transform.position, you) - you).sqrMagnitude < 25f * 25f) return true;
        return false;
    }

    // ---------------------------------------------------------------- The picture

    /// <summary>
    /// On the drone's screen, every lamp still dark is marked: a target bracket over it and how far it is from
    /// the drone, or, off the edge of the picture, an arrow at the edge pointing to it.
    /// </summary>
    void MarkDarkLamps(float unit)
    {
        if (view == null) return;
        float w = Screen.width, h = Screen.height, margin = 50f * unit;
        foreach (var lamp in Lamp.All)
        {
            if (lamp == null || lamp.IsLit) continue;
            Vector3 at = WorldWrap.NearestImage(lamp.BulbPosition, view.transform.position);
            Vector3 sp = view.WorldToScreenPoint(at);
            float dist = (at - view.transform.position).magnitude;
            bool behind = sp.z < 0f;
            if (behind) { sp.x = w - sp.x; sp.y = h - sp.y; }
            var pos = new Vector2(sp.x, h - sp.y);
            bool off = behind || pos.x < margin || pos.x > w - margin || pos.y < margin || pos.y > h - margin;
            Color c = new Color(0.6f, 0.9f, 1f, 0.75f + 0.25f * Mathf.Sin(Time.unscaledTime * 4f));
            GUI.color = c;
            if (off)
            {
                Vector2 centre = new Vector2(w, h) * 0.5f, dir = pos - centre;
                if (dir.sqrMagnitude < 1f) dir = Vector2.down;
                float sx = (w * 0.5f - margin) / Mathf.Max(Mathf.Abs(dir.x), 0.001f), sy = (h * 0.5f - margin) / Mathf.Max(Mathf.Abs(dir.y), 0.001f);
                pos = centre + dir * Mathf.Min(sx, sy);
                Matrix4x4 keep = GUI.matrix;
                GUIUtility.RotateAroundPivot(Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg, pos);
                GUI.DrawTexture(new Rect(pos.x - 14f * unit, pos.y - 2f * unit, 18f * unit, 4f * unit), Texture2D.whiteTexture);
                GUIUtility.RotateAroundPivot(35f, pos);
                GUI.DrawTexture(new Rect(pos.x - 9f * unit, pos.y - 2f * unit, 10f * unit, 4f * unit), Texture2D.whiteTexture);
                GUIUtility.RotateAroundPivot(-70f, pos);
                GUI.DrawTexture(new Rect(pos.x - 9f * unit, pos.y - 2f * unit, 10f * unit, 4f * unit), Texture2D.whiteTexture);
                GUI.matrix = keep;
            }
            else
            {
                // A bracket round it.
                float r = 14f * unit, t = 2f * unit, l = 7f * unit;
                foreach (var (cx, cy) in new[] { (-1f, -1f), (1f, -1f), (-1f, 1f), (1f, 1f) })
                {
                    Vector2 corner = pos + new Vector2(cx * r, cy * r);
                    GUI.DrawTexture(new Rect(cx < 0 ? corner.x : corner.x - l, corner.y - t * 0.5f, l, t), Texture2D.whiteTexture);
                    GUI.DrawTexture(new Rect(corner.x - t * 0.5f, cy < 0 ? corner.y : corner.y - l, t, l), Texture2D.whiteTexture);
                }
            }
            GUI.Label(new Rect(pos.x - 60f * unit, pos.y + 16f * unit, 120f * unit, 22f * unit), $"LAMP {dist:0}m", bigStyle != null ? new GUIStyle(hudStyle) { alignment = TextAnchor.MiddleCenter, fontSize = Mathf.RoundToInt(14f * unit) } : GUI.skin.label);
        }
        GUI.color = Color.white;
    }

    void OnGUI()
    {
        if (!piloting || GameOverScreen.IsActive) return;
        float unit = Mathf.Max(0.6f, Screen.height / 1080f);
        float w = Screen.width, h = Screen.height;
        if (hudStyle == null)
        {
            hudStyle = new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold };
            bigStyle = new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
        }
        hudStyle.fontSize = Mathf.RoundToInt(18f * unit);
        bigStyle.fontSize = Mathf.RoundToInt(26f * unit);
        Color hud = new Color(0.85f, 0.95f, 0.85f, 0.85f);

        // Breaking up toward the edge of the signal (and always a little grain).
        float loss = SignalLoss;
        if (Time.unscaledTime - staticAt > 0.05f) { staticAt = Time.unscaledTime; RefreshStatic(); }
        GUI.color = new Color(1f, 1f, 1f, 0.06f + loss * 0.7f);
        GUI.DrawTexture(new Rect(0f, 0f, w, h), staticTexture);
        if (loss > 0.5f && Random.value < loss * 0.3f)
        {
            GUI.color = new Color(0f, 0f, 0f, 0.6f); // dropouts
            GUI.DrawTexture(new Rect(0f, Random.Range(0f, h), w, Random.Range(10f, 60f) * unit), Texture2D.whiteTexture);
        }

        MarkDarkLamps(unit);

        // The frame: corner marks, the crosshair, the readouts.
        GUI.color = hud;
        float m = 40f * unit, len = 50f * unit, t = 3f * unit;
        foreach (var c in new[] { new Vector2(m, m), new Vector2(w - m, m), new Vector2(m, h - m), new Vector2(w - m, h - m) })
        {
            float sx = c.x < w * 0.5f ? 1f : -1f, sy = c.y < h * 0.5f ? 1f : -1f;
            GUI.DrawTexture(new Rect(sx > 0 ? c.x : c.x - len, sy > 0 ? c.y : c.y - t, len, t), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(sx > 0 ? c.x : c.x - t, sy > 0 ? c.y : c.y - len, t, len), Texture2D.whiteTexture);
        }
        GUI.DrawTexture(new Rect(w * 0.5f - 12f * unit, h * 0.5f - t * 0.5f, 24f * unit, t), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(w * 0.5f - t * 0.5f, h * 0.5f - 12f * unit, t, 24f * unit), Texture2D.whiteTexture);

        float alt = transform.position.y - Ground(transform.position);
        Vector3 you = player != null ? WorldWrap.NearestImage(player.transform.position, transform.position) : transform.position;
        float dist = new Vector2(transform.position.x - you.x, transform.position.z - you.z).magnitude;
        int bars = Mathf.Clamp(Mathf.CeilToInt((1f - loss) * 4f), 0, 4);
        bool blink = Mathf.Repeat(Time.unscaledTime, 1f) < 0.5f;
        GUI.Label(new Rect(m + 14f * unit, m + 10f * unit, 400f * unit, 26f * unit), (blink ? "● " : "   ") + (thermal ? "LIVE · THERMAL" : "LIVE"), hudStyle);
        GUI.Label(new Rect(m + 14f * unit, m + 36f * unit, 400f * unit, 26f * unit), $"SIGNAL {new string('|', bars)}{new string('.', 4 - bars)}", hudStyle);
        GUI.Label(new Rect(w - m - 260f * unit, m + 10f * unit, 246f * unit, 26f * unit), $"ALT {alt:0}m", hudStyle);
        GUI.Label(new Rect(w - m - 260f * unit, m + 36f * unit, 246f * unit, 26f * unit), $"DIST {dist:0}m", hudStyle);
        int minutesLeft = Mathf.FloorToInt(Charge * flightTime / 60f), secondsLeft = Mathf.FloorToInt(Charge * flightTime % 60f);
        GUI.Label(new Rect(w - m - 260f * unit, m + 62f * unit, 246f * unit, 26f * unit), $"BATT {Mathf.CeilToInt(Charge * 100f)}%  {minutesLeft}:{secondsLeft:00}", hudStyle);
        if (Charge < 0.15f)
        {
            GUI.color = new Color(1f, 0.4f, 0.2f, blink ? 0.95f : 0.4f);
            GUI.Label(new Rect(0f, h * 0.36f, w, 40f * unit), "B A T T E R Y   L O W", bigStyle);
            GUI.color = hud;
        }
        GUI.Label(new Rect(m + 14f * unit, h - m - 36f * unit, w, 26f * unit), (thermal ? "F thermal off" : "F thermal (drains twice as fast)") + " · WASD fly · SPACE / CTRL up and down · SHIFT fast · E let go", hudStyle);

        if (loss > 0.9f)
        {
            GUI.color = new Color(1f, 0.85f, 0.5f, blink ? 0.9f : 0.5f);
            GUI.Label(new Rect(0f, h * 0.3f, w, 40f * unit), "S I G N A L   W E A K", bigStyle);
        }
        // Something near where you're standing, back there in the dark.
        if (DangerNearYou())
        {
            GUI.color = new Color(0.95f, 0.15f, 0.1f, 0.5f + 0.5f * Mathf.Abs(Mathf.Sin(Time.unscaledTime * 5f)));
            GUI.Label(new Rect(0f, h * 0.62f, w, 40f * unit), "S O M E T H I N G   I S   N E A R   Y O U", bigStyle);
        }
        GUI.color = Color.white;
    }

    static void RefreshStatic()
    {
        const int sw = 96, sh = 54;
        if (staticTexture == null)
            staticTexture = new Texture2D(sw, sh, TextureFormat.RGBA32, false) { name = "Drone Static", filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
        var px = new Color32[sw * sh];
        for (int i = 0; i < px.Length; i++)
        {
            byte v = (byte)Random.Range(0, 256);
            px[i] = new Color32(v, v, v, 255);
        }
        staticTexture.SetPixels32(px);
        staticTexture.Apply();
    }

    // ---------------------------------------------------------------- Model

    /// <summary>
    /// A small quadcopter (pivot at its middle; front +z): a rounded grey body, four arms out in an X, a motor
    /// and a two-bladed propeller on each, landing legs, red and green lights, a camera under its nose and a
    /// little lamp beside it. rotors collects the propellers (if given); lamp is its light (off).
    /// </summary>
    public static void BuildModel(Transform parent, List<Transform> rotors, out Light lamp)
    {
        var shell = MaterialFactory.Lit(new Color(0.2f, 0.21f, 0.22f), 0.45f);
        var dark = MaterialFactory.Lit(new Color(0.04f, 0.04f, 0.045f), 0.3f);
        var blade = MaterialFactory.Lit(new Color(0.08f, 0.08f, 0.09f), 0.5f);
        var red = MaterialFactory.NoFog(new Color(1f, 0.1f, 0.05f) * 2f);
        var green = MaterialFactory.NoFog(new Color(0.1f, 1f, 0.3f) * 2f);
        var lens = MaterialFactory.Lit(new Color(0.02f, 0.03f, 0.05f), 0.95f);

        Piece(PrimitiveType.Sphere, shell, parent, Vector3.zero, new Vector3(0.16f, 0.07f, 0.24f), Quaternion.identity);
        Piece(PrimitiveType.Cube, dark, parent, new Vector3(0f, -0.035f, 0f), new Vector3(0.1f, 0.03f, 0.16f), Quaternion.identity); // battery
        for (int i = 0; i < 4; i++)
        {
            float sx = i % 2 == 0 ? -1f : 1f, sz = i < 2 ? 1f : -1f;
            Vector3 tip = new Vector3(sx * 0.16f, 0.01f, sz * 0.16f);
            var arm = Piece(PrimitiveType.Cube, shell, parent, tip * 0.5f, new Vector3(0.025f, 0.018f, tip.magnitude), Quaternion.LookRotation(tip.normalized));
            arm.name = "Arm";
            Piece(PrimitiveType.Cylinder, dark, parent, tip + Vector3.up * 0.015f, new Vector3(0.035f, 0.02f, 0.035f), Quaternion.identity); // motor
            var rotor = new GameObject("Rotor").transform;
            rotor.SetParent(parent, false);
            rotor.localPosition = tip + Vector3.up * 0.04f;
            Piece(PrimitiveType.Cube, blade, rotor, Vector3.zero, new Vector3(0.17f, 0.003f, 0.018f), Quaternion.identity);
            Piece(PrimitiveType.Sphere, dark, rotor, Vector3.zero, Vector3.one * 0.016f, Quaternion.identity);
            rotor.localRotation = Quaternion.Euler(0f, i * 47f, 0f);
            rotors?.Add(rotor);
            // A landing leg under each motor, and a light at each front (red left, green right).
            Piece(PrimitiveType.Cylinder, dark, parent, tip * 0.7f + Vector3.down * 0.05f, new Vector3(0.01f, 0.045f, 0.01f), Quaternion.identity);
            if (sz > 0f) Piece(PrimitiveType.Sphere, sx < 0f ? red : green, parent, tip + Vector3.down * 0.004f, Vector3.one * 0.014f, Quaternion.identity);
        }
        // The camera, in a little ball under the nose, and the lamp beside it.
        Piece(PrimitiveType.Sphere, dark, parent, new Vector3(0f, -0.03f, 0.12f), Vector3.one * 0.05f, Quaternion.identity);
        Piece(PrimitiveType.Sphere, lens, parent, new Vector3(0f, -0.03f, 0.142f), new Vector3(0.026f, 0.026f, 0.01f), Quaternion.identity);
        var lampGo = new GameObject("Drone Lamp");
        lampGo.transform.SetParent(parent, false);
        lampGo.transform.localPosition = new Vector3(0f, -0.04f, 0.15f);
        lampGo.transform.localRotation = Quaternion.Euler(12f, 0f, 0f);
        lamp = lampGo.AddComponent<Light>();
        lamp.type = LightType.Spot;
        lamp.color = new Color(0.85f, 0.92f, 1f);
        lamp.range = 20f;
        lamp.spotAngle = 70f;
        lamp.intensity = 2.2f;
        lamp.shadows = LightShadows.None;
    }

    static Transform Piece(PrimitiveType type, Material mat, Transform parent, Vector3 pos, Vector3 scale, Quaternion rot)
    {
        var go = GameObject.CreatePrimitive(type);
        Object.Destroy(go.GetComponent<Collider>());
        go.transform.SetParent(parent, false);
        go.transform.localPosition = pos;
        go.transform.localRotation = rot;
        go.transform.localScale = scale;
        var r = go.GetComponent<Renderer>();
        r.sharedMaterial = mat;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        return go.transform;
    }
}
