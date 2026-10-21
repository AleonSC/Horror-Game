using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// A crawler: a flat, many-legged thing lying low in the mist (and in the dark under it), waiting. It doesn't
/// hunt. Walk or run over it and it grabs your ankle: you're stuck, it shrieks (loud enough for the blind one
/// to hear), and you have to stamp it off (mash Space) before it lets go of its own accord. Then it scuttles
/// off into the dark and settles somewhere else.
///
/// Fair warning: its two pale eyes shine back in the flashlight beam from several metres off, and close up
/// you can hear it clicking to itself. Creep past it crouched and it doesn't notice you. It stays out of the
/// light round lamps and lit torches. CrawlerSpawner keeps a few of them round the player from the 2nd lamp.
/// </summary>
public class Crawler : MonoBehaviour
{
    public static readonly List<Crawler> All = new List<Crawler>();
    internal static void ResetStatics() { All.Clear(); Holding = null; }

    /// <summary>The crawler that has the player right now, if any.</summary>
    public static Crawler Holding { get; private set; }

    [Tooltip("It grabs a player walking within this distance of it (running, a little further).")]
    public float grabRadius = 0.9f;
    public float runningGrabRadius = 1.3f;
    [Tooltip("Presses of Space to break free.")]
    public int pressesToEscape = 8;
    [Tooltip("If you don't break free, it lets go after this long anyway.")]
    public float maxHold = 6f;
    [Tooltip("How far its shriek carries (the blind one hears it).")]
    public float shriekNoise = 30f;
    [Range(0f, 1f)] public float volume = 0.8f;

    enum State { Lurking, Holding, Fleeing }
    State state = State.Lurking;
    FirstPersonController player;
    Flashlight flashlight;
    AudioSource voice;
    Renderer[] eyes;
    Material eyeDark, eyeShine;
    readonly List<Transform> legs = new List<Transform>();
    Transform body;
    float timer, chitterTimer, noiseTimer, eyeGlow;
    float twitchSeed;
    int presses;
    Vector3 fleeDir;

    /// <summary>True once it's run off: the spawner moves it somewhere new.</summary>
    public bool Spent { get; private set; }

    void OnEnable() => All.Add(this);
    void OnDisable()
    {
        All.Remove(this);
        if (Holding == this) Release();
    }

    void Awake()
    {
        Tuning.Apply(this);
        twitchSeed = Random.Range(0f, 100f);
        voice = ProceduralAudio.AddSource(gameObject, null, false, true, volume, 1.5f, 40f);
        Build();
    }

    /// <summary>Puts it down on the ground at a spot, lurking, facing any way.</summary>
    public void Settle(Vector3 at)
    {
        transform.position = at;
        transform.rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
        state = State.Lurking;
        Spent = false;
        presses = 0;
        body.localPosition = Vector3.zero;
        gameObject.SetActive(true);
    }

    /// <summary>A flat black body, a small head low at the front, six long jointed legs splayed out, two pale eyes.</summary>
    void Build()
    {
        var black = MaterialFactory.Unlit(new Color(0.012f, 0.012f, 0.014f));
        eyeDark = MaterialFactory.Unlit(new Color(0.05f, 0.05f, 0.045f));
        eyeShine = MaterialFactory.NoFog(new Color(0.85f, 0.9f, 0.75f) * 2.2f);
        body = new GameObject("Body").transform;
        body.SetParent(transform, false);
        Part(PrimitiveType.Sphere, black, body, new Vector3(0f, 0.07f, 0f), new Vector3(0.42f, 0.11f, 0.6f), Quaternion.identity);
        Part(PrimitiveType.Sphere, black, body, new Vector3(0f, 0.06f, 0.34f), new Vector3(0.2f, 0.09f, 0.2f), Quaternion.identity);
        eyes = new Renderer[2];
        for (int side = 0; side < 2; side++)
        {
            float x = side == 0 ? -0.045f : 0.045f;
            eyes[side] = Part(PrimitiveType.Sphere, eyeDark, body, new Vector3(x, 0.09f, 0.42f), Vector3.one * 0.028f, Quaternion.identity).GetComponent<Renderer>();
        }
        // Legs: hip out and up to a sharp knee, then down to a point on the ground, splayed wide.
        for (int i = 0; i < 6; i++)
        {
            float side = i < 3 ? -1f : 1f;
            float z = (i % 3 - 1) * 0.2f;
            var hip = new GameObject("Leg").transform;
            hip.SetParent(body, false);
            hip.localPosition = new Vector3(side * 0.16f, 0.07f, z);
            Vector3 knee = new Vector3(side * 0.28f, 0.16f, z * 0.6f);
            Vector3 foot = new Vector3(side * 0.55f, -0.07f, z * 1.8f);
            Bone(black, hip, Vector3.zero, knee, 0.018f);
            Bone(black, hip, knee, foot, 0.012f);
            legs.Add(hip);
        }
        foreach (var r in GetComponentsInChildren<Renderer>()) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
    }

    Transform Part(PrimitiveType type, Material mat, Transform parent, Vector3 pos, Vector3 scale, Quaternion rot)
    {
        var go = GameObject.CreatePrimitive(type);
        Destroy(go.GetComponent<Collider>());
        go.transform.SetParent(parent, false);
        go.transform.localPosition = pos;
        go.transform.localRotation = rot;
        go.transform.localScale = scale;
        go.GetComponent<Renderer>().sharedMaterial = mat;
        return go.transform;
    }

    void Bone(Material mat, Transform parent, Vector3 a, Vector3 b, float thickness)
    {
        Vector3 d = b - a;
        Part(PrimitiveType.Cylinder, mat, parent, (a + b) * 0.5f, new Vector3(thickness, d.magnitude * 0.5f, thickness), Quaternion.FromToRotation(Vector3.up, d));
    }

    void Update()
    {
        if (player == null) player = FindAnyObjectByType<FirstPersonController>();
        if (flashlight == null) flashlight = FindAnyObjectByType<Flashlight>();
        if (player == null) return;
        float dt = Time.deltaTime;
        Vector3 you = player.transform.position;
        Vector3 me = transform.position;
        Vector3 flat = WorldWrap.NearestImage(you, me) - me;
        flat.y = 0f;
        float dist = flat.magnitude;

        UpdateEyes(dt);
        switch (state)
        {
            case State.Lurking:
                // Clicking quietly to itself when you're close.
                chitterTimer -= dt;
                if (dist < 6f && chitterTimer <= 0f)
                {
                    chitterTimer = Random.Range(2.5f, 5f);
                    voice.pitch = Random.Range(1.6f, 2f);
                    voice.PlayOneShot(ProceduralAudio.Skitter, 0.25f * (1f - EntityDread.Hush));
                }
                // The legs twitch now and then.
                for (int i = 0; i < legs.Count; i++)
                    legs[i].localRotation = Quaternion.Euler(Mathf.PerlinNoise(twitchSeed + i * 3.1f, Time.time * 0.5f) * 8f - 4f, 0f, 0f);

                bool reachable = Mathf.Abs(you.y - me.y) < 1.2f && !player.IsCrouching && player.CurrentSpeed > 0.4f
                                 && !SafeZones.Contains(you) && !PortalDoor.PlayerInRoom && !GameOverScreen.IsActive
                                 && Holding == null && ShadowEntity.Capturing == null;
                if (reachable && dist < (player.IsRunning ? runningGrabRadius : grabRadius)) Grab();
                break;

            case State.Holding:
                UpdateHolding(dt, you);
                break;

            case State.Fleeing:
                timer += dt;
                transform.position += fleeDir * 7f * dt;
                FitToGround();
                Scuttle(14f);
                if (timer > 1.6f) { Spent = true; gameObject.SetActive(false); }
                break;
        }
    }

    /// <summary>Its eyes, shining back pale when the flashlight's beam is on them.</summary>
    void UpdateEyes(float dt)
    {
        bool lit = false;
        if (flashlight != null && flashlight.IsOn && flashlight.flashlight != null)
        {
            Transform beam = flashlight.flashlight.transform;
            Vector3 eye = transform.position + transform.up * 0.09f + transform.forward * 0.42f;
            Vector3 to = eye - beam.position;
            float d = to.magnitude;
            if (d < 11f && Vector3.Angle(beam.forward, to) < flashlight.flashlight.spotAngle * 0.4f)
                lit = !Physics.Raycast(beam.position, to / d, d - 0.3f, ~0, QueryTriggerInteraction.Ignore);
        }
        eyeGlow = Mathf.MoveTowards(eyeGlow, lit ? 1f : 0f, dt * 6f);
        foreach (var e in eyes) e.sharedMaterial = eyeGlow > 0.4f ? eyeShine : eyeDark;
    }

    void Grab()
    {
        state = State.Holding;
        Holding = this;
        timer = 0f;
        presses = 0;
        noiseTimer = 0f;
        player.Snared = true;
        voice.pitch = Random.Range(1.3f, 1.5f);
        voice.PlayOneShot(ProceduralAudio.Screech, 1f);
        Noise.Emit(transform.position, shriekNoise);
        player.AddCameraShake(2.5f);
        player.DrawAttention(() => transform.position + Vector3.up * 0.1f, 3f, 0.9f); // eyes down: what's got you
    }

    void UpdateHolding(float dt, Vector3 you)
    {
        timer += dt;
        // Wrapped round your ankle, in front of your feet, writhing.
        Vector3 ahead = player.transform.forward;
        ahead.y = 0f;
        transform.position = new Vector3(you.x, transform.position.y, you.z) + ahead.normalized * 0.25f;
        FitToGround();
        transform.rotation = Quaternion.LookRotation(-ahead.normalized, Vector3.up);
        Scuttle(22f);
        body.localPosition = new Vector3(Random.Range(-0.01f, 0.01f), 0f, Random.Range(-0.01f, 0.01f));

        // It keeps shrieking: the noise keeps carrying.
        noiseTimer -= dt;
        if (noiseTimer <= 0f)
        {
            noiseTimer = 1.5f;
            Noise.Emit(transform.position, shriekNoise * 0.7f);
        }

        if (HorrorInput.JumpPressed)
        {
            presses++;
            player.AddCameraShake(0.6f);
            voice.pitch = Random.Range(1.8f, 2.2f);
            voice.PlayOneShot(ProceduralAudio.Squeak, 0.5f);
        }
        if (presses >= pressesToEscape || timer > maxHold || GameOverScreen.IsActive || SafeZones.Contains(you)) Flee(you);
    }

    void Flee(Vector3 you)
    {
        Release();
        state = State.Fleeing;
        timer = 0f;
        Vector3 away = transform.position - you;
        away.y = 0f;
        fleeDir = (away.sqrMagnitude > 0.01f ? away.normalized : Random.insideUnitSphere).normalized;
        fleeDir.y = 0f;
        fleeDir.Normalize();
        transform.rotation = Quaternion.LookRotation(fleeDir, Vector3.up);
        voice.pitch = Random.Range(1.4f, 1.7f);
        voice.PlayOneShot(ProceduralAudio.Skitter, 0.8f);
    }

    void Release()
    {
        if (player != null) player.Snared = false;
        if (Holding == this) Holding = null;
    }

    DarkForestGenerator generator;

    void FitToGround()
    {
        if (generator == null) generator = FindAnyObjectByType<DarkForestGenerator>();
        if (generator == null) return;
        Vector3 p = transform.position;
        p.y = generator.GetGroundHeight(p.x, p.z);
        transform.position = p;
    }

    /// <summary>Legs scrabbling.</summary>
    void Scuttle(float speed)
    {
        for (int i = 0; i < legs.Count; i++)
            legs[i].localRotation = Quaternion.Euler(Mathf.Sin(Time.time * speed + i * 1.7f) * 25f, Mathf.Cos(Time.time * speed + i) * 15f, 0f);
    }

    void OnGUI()
    {
        if (state != State.Holding || GameOverScreen.IsActive) return;
        float unit = Mathf.Max(1f, Screen.height / 1080f);
        var style = new GUIStyle(GUI.skin.label)
        {
            alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold, fontSize = Mathf.RoundToInt(22f * unit),
        };
        float shake = Mathf.Sin(Time.time * 40f) * 2f * unit;
        var r = new Rect(0f, Screen.height * 0.68f + shake, Screen.width, 40f * unit);
        GUI.color = new Color(0f, 0f, 0f, 0.8f);
        GUI.Label(new Rect(r.x + 2f, r.y + 2f, r.width, r.height), "S T A M P   I T   O F F  —  M A S H   S P A C E", style);
        GUI.color = new Color(0.9f, 0.2f, 0.15f);
        GUI.Label(r, "S T A M P   I T   O F F  —  M A S H   S P A C E", style);
        // How close you are to breaking free.
        float w = 240f * unit;
        float fill = presses / (float)Mathf.Max(pressesToEscape, 1);
        GUI.color = new Color(0f, 0f, 0f, 0.6f);
        GUI.DrawTexture(new Rect((Screen.width - w) * 0.5f, r.yMax + 6f * unit, w, 6f * unit), Texture2D.whiteTexture);
        GUI.color = new Color(0.9f, 0.2f, 0.15f);
        GUI.DrawTexture(new Rect((Screen.width - w) * 0.5f, r.yMax + 6f * unit, w * fill, 6f * unit), Texture2D.whiteTexture);
        GUI.color = Color.white;
    }
}

/// <summary>
/// Keeps a few crawlers lurking in the mist round the player, from the 2nd lamp lit on: each settles
/// somewhere ahead (mistiest spots preferred, never in the light), and once one has been passed, left far
/// behind, or has run off after grabbing someone, it's moved to a fresh spot. DarkForestGenerator adds this.
/// </summary>
public class CrawlerSpawner : MonoBehaviour
{
    void Awake() => Tuning.Apply(this);

    public int lampsToStart = 2;
    public int count = 4;
    public float minDistance = 14f, maxDistance = 45f;
    [Tooltip("A crawler further than this from the player is moved somewhere nearer.")]
    public float leaveDistance = 70f;

    readonly List<Crawler> crawlers = new List<Crawler>();
    DarkForestGenerator generator;
    GroundMist mist;
    float checkTimer;

    void Update()
    {
        var player = FindAnyObjectByType<FirstPersonController>();
        if (player == null) return;
        if (generator == null) generator = FindAnyObjectByType<DarkForestGenerator>();
        if (mist == null) mist = FindAnyObjectByType<GroundMist>();

        bool active = Lamp.TimesLit >= lampsToStart && !GameOverScreen.IsActive;
        if (!active)
        {
            foreach (var c in crawlers) if (c != null && c.gameObject.activeSelf && Crawler.Holding != c) c.gameObject.SetActive(false);
            return;
        }
        while (crawlers.Count < count)
        {
            var c = new GameObject("Crawler").AddComponent<Crawler>();
            c.gameObject.SetActive(false);
            crawlers.Add(c);
        }

        checkTimer -= Time.deltaTime;
        if (checkTimer > 0f) return;
        checkTimer = 1f;
        Vector3 you = player.transform.position;
        foreach (var c in crawlers)
        {
            if (c == null || Crawler.Holding == c) continue;
            Vector3 d = WorldWrap.NearestImage(c.transform.position, you) - you;
            d.y = 0f;
            bool lost = !c.gameObject.activeSelf || c.Spent || d.magnitude > leaveDistance;
            if (lost && TryFindSpot(player.transform, out Vector3 spot)) c.Settle(spot);
        }
    }

    /// <summary>A hiding place ahead of the player: the mistiest of a few tries, out of the light, not too close.</summary>
    bool TryFindSpot(Transform player, out Vector3 spot)
    {
        spot = Vector3.zero;
        if (generator == null) return false;
        float best = -1f;
        Vector3 forward = player.forward;
        forward.y = 0f;
        for (int i = 0; i < 10; i++)
        {
            // Mostly the way you're heading (that's where you'll walk), sometimes anywhere round you.
            Vector2 r = Random.insideUnitCircle.normalized;
            Vector3 dir = new Vector3(r.x, 0f, r.y);
            if (Random.value < 0.7f && forward.sqrMagnitude > 0.01f) dir = Quaternion.Euler(0f, Random.Range(-50f, 50f), 0f) * forward.normalized;
            Vector3 p = player.position + dir * Random.Range(minDistance, maxDistance);
            p.y = generator.GetGroundHeight(p.x, p.z);
            if (SafeZones.Contains(p) || Water.InWater(p, out _)) continue;
            float score = (mist != null ? mist.MistAt(p.x, p.z) : 0f) + Random.value * 0.2f;
            if (score > best) { best = score; spot = p; }
        }
        return best >= 0f;
    }
}
