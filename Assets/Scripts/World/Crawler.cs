using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// A crawler: a head, no body, four long thin arms growing out of the stump of its neck, that it crawls on
/// like a spider. It lies low in the mist (and in the dark under it), waiting. It doesn't hunt. Come close
/// enough, walking or running, and it springs up at your face and clamps on: you can't see, you can't move,
/// it shrieks (loud enough for the blind one to hear), and you have to tear it off (mash Space) before it
/// lets go of its own accord. Then it drops, and scuttles off into the dark to settle somewhere else.
///
/// Fair warning: its two pale eyes shine back in the flashlight beam from several metres off, and close up
/// you can hear it clicking to itself. Creep past it crouched and it doesn't notice you. It stays out of the
/// light round lamps and lit torches. CrawlerSpawner keeps a few of them round the player from the 2nd lamp.
/// </summary>
public class Crawler : MonoBehaviour
{
    public static readonly List<Crawler> All = new List<Crawler>();
    internal static void ResetStatics() { All.Clear(); Holding = null; StaticReset.Drop(ref vignette); }

    /// <summary>The crawler that has the player right now, if any.</summary>
    public static Crawler Holding { get; private set; }

    [Tooltip("It springs at a player walking within this distance of it (running, a little further).")]
    public float grabRadius = 1.8f;
    public float runningGrabRadius = 2.5f;
    [Tooltip("Presses of Space to tear it off.")]
    public int pressesToEscape = 8;
    [Tooltip("If you don't tear it off, it lets go after this long anyway.")]
    public float maxHold = 6f;
    [Tooltip("How far its shriek carries (the blind one hears it).")]
    public float shriekNoise = 30f;
    [Tooltip("Seconds its leap at your face takes.")]
    public float leapTime = 0.35f;
    [Range(0f, 1f)] public float volume = 0.8f;

    enum State { Lurking, Leaping, Holding, Dropping, Fleeing }
    State state = State.Lurking;
    FirstPersonController player;
    Flashlight flashlight;
    AudioSource voice;
    Renderer[] eyes;
    Material eyeDark, eyeShine;
    readonly List<Transform> arms = new List<Transform>();
    readonly List<Transform> elbows = new List<Transform>();
    Transform body;
    float timer, chitterTimer, noiseTimer, eyeGlow, jolt;
    float twitchSeed;
    int presses;
    Vector3 fleeDir, leapFrom, dropFrom;
    static Texture2D vignette;

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
        body.localRotation = Quaternion.identity;
        gameObject.SetActive(true);
    }

    // ---------------------------------------------------------------- Building

    /// <summary>
    /// A bald, grey, shrunken human head (face toward +z), held up off the ground on four long, thin,
    /// double-jointed arms that come out of the ragged stump of its neck: two reaching forward past its face,
    /// two back, each ending in a long-fingered hand flat on the ground. A gaping mouth of uneven teeth; two
    /// pale eyes in deep sockets.
    /// </summary>
    void Build()
    {
        var skin = MaterialFactory.Lit(new Color(0.15f, 0.135f, 0.125f), 0.3f);
        var raw = MaterialFactory.Lit(new Color(0.12f, 0.03f, 0.025f), 0.6f);
        var hole = MaterialFactory.Unlit(new Color(0.008f, 0.004f, 0.004f));
        var tooth = MaterialFactory.Lit(new Color(0.42f, 0.38f, 0.3f), 0.4f);
        eyeDark = MaterialFactory.Unlit(new Color(0.05f, 0.05f, 0.045f));
        eyeShine = MaterialFactory.NoFog(new Color(0.85f, 0.9f, 0.75f) * 2.2f);

        body = new GameObject("Body").transform;
        body.SetParent(transform, false);
        var head = new GameObject("Head").transform;
        head.SetParent(body, false);
        head.localPosition = new Vector3(0f, 0.2f, 0f);
        head.localRotation = Quaternion.Euler(12f, 0f, 0f); // chin tucked toward the ground, watching

        // Skull, cheeks, brow, a jutting jaw.
        Part(PrimitiveType.Sphere, skin, head, new Vector3(0f, 0.02f, -0.01f), new Vector3(0.2f, 0.22f, 0.24f), Quaternion.identity);
        Part(PrimitiveType.Sphere, skin, head, new Vector3(0f, 0.06f, 0.06f), new Vector3(0.17f, 0.06f, 0.1f), Quaternion.Euler(-10f, 0f, 0f));   // brow
        foreach (float x in new[] { -0.055f, 0.055f })
            Part(PrimitiveType.Sphere, skin, head, new Vector3(x, -0.02f, 0.075f), new Vector3(0.06f, 0.05f, 0.06f), Quaternion.identity);       // cheekbones
        Part(PrimitiveType.Sphere, skin, head, new Vector3(0f, -0.085f, 0.06f), new Vector3(0.13f, 0.06f, 0.1f), Quaternion.Euler(15f, 0f, 0f)); // jaw, hanging open
        Part(PrimitiveType.Sphere, skin, head, new Vector3(0f, 0.01f, 0.115f), new Vector3(0.03f, 0.05f, 0.035f), Quaternion.Euler(-15f, 0f, 0f)); // nose
        // The mouth, gaping, and its teeth.
        Part(PrimitiveType.Sphere, hole, head, new Vector3(0f, -0.055f, 0.1f), new Vector3(0.085f, 0.05f, 0.04f), Quaternion.identity);
        for (int i = 0; i < 7; i++)
        {
            float u = (i - 3f) / 3f;
            Part(PrimitiveType.Cube, tooth, head, new Vector3(u * 0.034f, -0.035f, 0.112f - u * u * 0.012f), new Vector3(0.009f, 0.016f + (i % 2) * 0.006f, 0.006f), Quaternion.Euler(0f, u * 30f, Random.Range(-12f, 12f)));
            Part(PrimitiveType.Cube, tooth, head, new Vector3(u * 0.03f, -0.077f, 0.108f - u * u * 0.012f), new Vector3(0.008f, 0.013f, 0.006f), Quaternion.Euler(0f, u * 30f, Random.Range(-12f, 12f)));
        }
        // Eyes deep in their sockets.
        eyes = new Renderer[2];
        for (int side = 0; side < 2; side++)
        {
            float x = side == 0 ? -0.04f : 0.04f;
            Part(PrimitiveType.Sphere, hole, head, new Vector3(x, 0.025f, 0.09f), new Vector3(0.045f, 0.035f, 0.03f), Quaternion.identity);
            eyes[side] = Part(PrimitiveType.Sphere, eyeDark, head, new Vector3(x, 0.025f, 0.098f), Vector3.one * 0.022f, Quaternion.identity).GetComponent<Renderer>();
        }
        // The ragged neck stump under the back of the skull, where the arms grow from.
        Part(PrimitiveType.Cylinder, skin, head, new Vector3(0f, -0.1f, -0.05f), new Vector3(0.09f, 0.04f, 0.09f), Quaternion.Euler(-25f, 0f, 0f));
        Part(PrimitiveType.Sphere, raw, head, new Vector3(0f, -0.14f, -0.07f), new Vector3(0.08f, 0.03f, 0.08f), Quaternion.Euler(-25f, 0f, 0f));

        // The arms: out of the neck, up to a high, sharp elbow, down to a hand flat on the ground.
        Vector3 neck = new Vector3(0f, 0.08f, -0.06f);
        for (int i = 0; i < 4; i++)
        {
            float side = i % 2 == 0 ? -1f : 1f;
            float fore = i < 2 ? 1f : -1f; // two reaching forward, two back
            var shoulder = new GameObject("Arm").transform;
            shoulder.SetParent(body, false);
            shoulder.localPosition = neck;
            Vector3 elbowAt = new Vector3(side * 0.24f, 0.32f, fore * 0.2f);
            Bone(skin, shoulder, Vector3.zero, elbowAt, 0.028f);
            var elbow = new GameObject("Elbow").transform;
            elbow.SetParent(shoulder, false);
            elbow.localPosition = elbowAt;
            Part(PrimitiveType.Sphere, skin, elbow, Vector3.zero, Vector3.one * 0.035f, Quaternion.identity);
            Vector3 wrist = new Vector3(side * 0.26f, -0.38f, fore * 0.26f);
            Bone(skin, elbow, Vector3.zero, wrist, 0.022f);
            // The hand, flat, fingers splayed out ahead of it.
            var hand = new GameObject("Hand").transform;
            hand.SetParent(elbow, false);
            hand.localPosition = wrist;
            hand.localRotation = Quaternion.LookRotation(new Vector3(side * 0.6f, 0f, fore).normalized, Vector3.up);
            Part(PrimitiveType.Sphere, skin, hand, new Vector3(0f, 0f, 0.03f), new Vector3(0.05f, 0.015f, 0.06f), Quaternion.identity);
            for (int f = 0; f < 4; f++)
            {
                float spread = (f - 1.5f) * 0.4f;
                Vector3 dir = new Vector3(Mathf.Sin(spread), -0.05f, Mathf.Cos(spread));
                Bone(skin, hand, new Vector3(0f, 0f, 0.05f), new Vector3(0f, 0f, 0.05f) + dir * (f == 0 || f == 3 ? 0.075f : 0.095f), 0.007f);
            }
            arms.Add(shoulder);
            elbows.Add(elbow);
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

    // ---------------------------------------------------------------- Behaviour

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
                    voice.pitch = Random.Range(0.8f, 1f);
                    voice.PlayOneShot(ProceduralAudio.Skitter, 0.25f * (1f - EntityDread.Hush));
                }
                // Its fingers and arms twitch now and then.
                for (int i = 0; i < arms.Count; i++)
                    arms[i].localRotation = Quaternion.Euler(Mathf.PerlinNoise(twitchSeed + i * 3.1f, Time.time * 0.5f) * 6f - 3f, Mathf.PerlinNoise(twitchSeed - i, Time.time * 0.4f) * 6f - 3f, 0f);

                bool reachable = Mathf.Abs(you.y - me.y) < 1.2f && !player.IsCrouching && player.CurrentSpeed > 0.4f
                                 && !SafeZones.Contains(you) && !PortalDoor.PlayerInRoom && !GameOverScreen.IsActive
                                 && Holding == null && ShadowEntity.Capturing == null && Camera.main != null;
                if (reachable && dist < (player.IsRunning ? runningGrabRadius : grabRadius)) Leap();
                break;

            case State.Leaping:
                UpdateLeap(dt);
                break;

            case State.Holding:
                UpdateHolding(dt, you);
                break;

            case State.Dropping:
                // Falling off your face to the ground in front of you, flailing.
                timer += dt;
                float u = Mathf.Clamp01(timer / 0.3f);
                Vector3 landing = dropFrom + fleeDir * 0.6f;
                landing.y = Ground(landing);
                transform.position = Vector3.Lerp(dropFrom, landing, u) + Vector3.up * Mathf.Sin(u * Mathf.PI) * 0.2f;
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(fleeDir, Vector3.up), 1f - Mathf.Exp(-12f * dt));
                Scuttle(26f);
                if (u >= 1f)
                {
                    state = State.Fleeing;
                    timer = 0f;
                    voice.pitch = Random.Range(0.8f, 0.95f);
                    voice.PlayOneShot(ProceduralAudio.Skitter, 0.8f);
                }
                break;

            case State.Fleeing:
                timer += dt;
                transform.position += fleeDir * 7f * dt;
                FitToGround();
                Scuttle(16f);
                if (timer > 1.8f) { Spent = true; gameObject.SetActive(false); }
                break;
        }
    }

    /// <summary>Its eyes, shining back pale when the flashlight's beam is on them.</summary>
    void UpdateEyes(float dt)
    {
        bool lit = false;
        if (state == State.Lurking && flashlight != null && flashlight.IsOn && flashlight.flashlight != null)
        {
            Transform beam = flashlight.flashlight.transform;
            Vector3 eye = eyes[0].transform.position;
            Vector3 to = eye - beam.position;
            float d = to.magnitude;
            if (d < 11f && Vector3.Angle(beam.forward, to) < flashlight.flashlight.spotAngle * 0.4f)
                lit = !Physics.Raycast(beam.position, to / d, d - 0.3f, ~0, QueryTriggerInteraction.Ignore);
        }
        eyeGlow = Mathf.MoveTowards(eyeGlow, lit ? 1f : 0f, dt * 6f);
        foreach (var e in eyes) e.sharedMaterial = eyeGlow > 0.4f ? eyeShine : eyeDark;
    }

    /// <summary>Where it clings: right in front of your eyes, its face to yours.</summary>
    Vector3 FacePoint(Transform cam) => cam.position + cam.forward * 0.25f - cam.up * 0.2f;

    /// <summary>It springs up off the ground at your face, screaming.</summary>
    void Leap()
    {
        state = State.Leaping;
        Holding = this;
        timer = 0f;
        presses = 0;
        leapFrom = transform.position;
        player.Snared = true;
        voice.pitch = Random.Range(0.7f, 0.8f);
        voice.PlayOneShot(ProceduralAudio.Screech, 1f);
        Noise.Emit(transform.position, shriekNoise);
    }

    void UpdateLeap(float dt)
    {
        var cam = Camera.main;
        if (cam == null) { Flee(player.transform.position); return; }
        timer += dt;
        float u = Mathf.Clamp01(timer / Mathf.Max(leapTime, 0.05f));
        Vector3 to = FacePoint(cam.transform);
        // A short, quick arc, rising as it comes.
        transform.position = Vector3.Lerp(leapFrom, to, u * u * (3f - 2f * u)) + Vector3.up * Mathf.Sin(u * Mathf.PI) * 0.4f;
        transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(cam.transform.position - transform.position, Vector3.up), 1f - Mathf.Exp(-14f * dt));
        Reach(u);
        if (u >= 1f) Latch();
    }

    /// <summary>On your face: you can't see, can't move, and it's shrieking. And its shriek calls one more scour into the hunt (every time: there's no limit).</summary>
    void Latch()
    {
        ShadowEntity.Spawn(Random.Range(4f, 9f));
        state = State.Holding;
        timer = 0f;
        noiseTimer = 0f;
        player.AddCameraShake(3f);
        voice.pitch = Random.Range(0.75f, 0.85f);
        voice.PlayOneShot(ProceduralAudio.Screech, 0.9f);
    }

    void UpdateHolding(float dt, Vector3 you)
    {
        var cam = Camera.main;
        if (cam == null) { Flee(you); return; }
        timer += dt;
        // Clamped on, face to face, its arms wrapped back round your head, writhing; jerked about as you tear at it.
        jolt = Mathf.MoveTowards(jolt, 0f, dt * 4f);
        Vector3 shove = (cam.transform.right * Random.Range(-1f, 1f) + cam.transform.up * Random.Range(-1f, 1f)) * (0.006f + jolt * 0.03f);
        transform.position = FacePoint(cam.transform) + shove + cam.transform.forward * jolt * 0.05f;
        transform.rotation = Quaternion.LookRotation(-cam.transform.forward, cam.transform.up);
        Grip();
        player.AddCameraShake(0.5f + jolt * 1.5f);

        // It keeps shrieking: the noise keeps carrying.
        noiseTimer -= dt;
        if (noiseTimer <= 0f)
        {
            noiseTimer = 1.5f;
            Noise.Emit(transform.position, shriekNoise * 0.7f);
            voice.pitch = Random.Range(0.6f, 0.75f);
            voice.PlayOneShot(ProceduralAudio.Screech, 0.55f);
        }

        if (HorrorInput.JumpPressed)
        {
            presses++;
            jolt = 1f;
            player.AddCameraShake(1.2f);
            voice.pitch = Random.Range(0.85f, 1.05f);
            voice.PlayOneShot(ProceduralAudio.Squeak, 0.5f);
        }
        if (presses >= pressesToEscape || timer > maxHold || GameOverScreen.IsActive || SafeZones.Contains(you)) Flee(you);
    }

    /// <summary>Torn off: it drops, then scuttles away into the dark.</summary>
    void Flee(Vector3 you)
    {
        Release();
        state = State.Dropping;
        timer = 0f;
        dropFrom = transform.position;
        // Away from you, and out of the light if it can.
        Vector3 away = -player.transform.forward;
        away.y = 0f;
        if (away.sqrMagnitude < 0.01f) away = Random.insideUnitSphere;
        away.y = 0f;
        fleeDir = Quaternion.Euler(0f, Random.Range(-70f, 70f), 0f) * away.normalized;
        Vector3 start = new Vector3(you.x, 0f, you.z) + player.transform.forward * 0.6f;
        for (int i = 0; i < 6 && SafeZones.Contains(start + fleeDir * 6f); i++)
            fleeDir = Quaternion.Euler(0f, 60f, 0f) * fleeDir;
        fleeDir.y = 0f;
        fleeDir.Normalize();
        voice.pitch = Random.Range(0.7f, 0.85f);
        voice.PlayOneShot(ProceduralAudio.Screech, 0.6f);
    }

    void Release()
    {
        if (player != null) player.Snared = false;
        if (Holding == this) Holding = null;
    }

    DarkForestGenerator generator;

    float Ground(Vector3 p)
    {
        if (generator == null) generator = FindAnyObjectByType<DarkForestGenerator>();
        return generator != null ? generator.GetGroundHeight(p.x, p.z) : p.y;
    }

    void FitToGround()
    {
        Vector3 p = transform.position;
        p.y = Ground(p);
        transform.position = p;
        transform.rotation = Quaternion.LookRotation(Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized + Vector3.forward * 0.0001f, Vector3.up);
    }

    /// <summary>Crawling: the arms swinging round from the neck in pairs, diagonally, the elbows rising as they go.</summary>
    void Scuttle(float speed)
    {
        for (int i = 0; i < arms.Count; i++)
        {
            float phase = Time.time * speed + (i == 0 || i == 3 ? 0f : Mathf.PI);
            arms[i].localRotation = Quaternion.Euler(Mathf.Max(0f, Mathf.Sin(phase)) * -18f, Mathf.Cos(phase) * 22f, 0f);
            elbows[i].localRotation = Quaternion.Euler(Mathf.Max(0f, Mathf.Sin(phase)) * 25f, 0f, 0f);
        }
        body.localPosition = Vector3.up * Mathf.Abs(Mathf.Sin(Time.time * speed)) * 0.02f;
    }

    /// <summary>Mid-leap, the arms fling forward, reaching for you.</summary>
    void Reach(float u)
    {
        for (int i = 0; i < arms.Count; i++)
        {
            float side = i % 2 == 0 ? -1f : 1f;
            arms[i].localRotation = Quaternion.Euler(-70f * u, side * 25f * u, side * -20f * u);
            elbows[i].localRotation = Quaternion.Euler(-40f * u, 0f, 0f);
        }
    }

    /// <summary>On your face, the arms wrapped back round your head, clenching.</summary>
    void Grip()
    {
        for (int i = 0; i < arms.Count; i++)
        {
            float side = i % 2 == 0 ? -1f : 1f;
            float clench = Mathf.Sin(Time.time * 9f + i * 1.3f) * 6f;
            arms[i].localRotation = Quaternion.Euler(-95f + clench, side * 35f, side * -30f);
            elbows[i].localRotation = Quaternion.Euler(-70f - clench, 0f, 0f);
        }
        body.localPosition = Vector3.zero;
    }

    void OnGUI()
    {
        if ((state != State.Holding && state != State.Leaping) || GameOverScreen.IsActive) return;
        float unit = Mathf.Max(1f, Screen.height / 1080f);
        var screen = new Rect(0f, 0f, Screen.width, Screen.height);

        // Blind: its face is all you can see, and barely that, black at the edges where its hands are.
        if (state == State.Holding)
        {
            float seep = 0.08f * presses / Mathf.Max(pressesToEscape, 1); // a little light getting in as it loosens
            GUI.color = new Color(0f, 0f, 0f, 0.82f - seep + jolt * 0.1f);
            GUI.DrawTexture(screen, Texture2D.whiteTexture);
            GUI.color = Color.white;
            GUI.DrawTexture(screen, Vignette());
        }
        else
        {
            float u = Mathf.Clamp01(timer / Mathf.Max(leapTime, 0.05f));
            GUI.color = new Color(0f, 0f, 0f, u * u * 0.8f);
            GUI.DrawTexture(screen, Texture2D.whiteTexture);
            GUI.color = Color.white;
            return;
        }

        var style = new GUIStyle(GUI.skin.label)
        {
            alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold, fontSize = Mathf.RoundToInt(22f * unit),
        };
        float shake = Mathf.Sin(Time.time * 40f) * 2f * unit;
        var r = new Rect(0f, Screen.height * 0.68f + shake, Screen.width, 40f * unit);
        const string prompt = "T E A R   I T   O F F  —  M A S H   S P A C E";
        GUI.color = new Color(0f, 0f, 0f, 0.8f);
        GUI.Label(new Rect(r.x + 2f, r.y + 2f, r.width, r.height), prompt, style);
        GUI.color = new Color(0.9f, 0.2f, 0.15f);
        GUI.Label(r, prompt, style);
        // How close you are to breaking free.
        float w = 240f * unit;
        float fill = presses / (float)Mathf.Max(pressesToEscape, 1);
        GUI.color = new Color(0f, 0f, 0f, 0.6f);
        GUI.DrawTexture(new Rect((Screen.width - w) * 0.5f, r.yMax + 6f * unit, w, 6f * unit), Texture2D.whiteTexture);
        GUI.color = new Color(0.9f, 0.2f, 0.15f);
        GUI.DrawTexture(new Rect((Screen.width - w) * 0.5f, r.yMax + 6f * unit, w * fill, 6f * unit), Texture2D.whiteTexture);
        GUI.color = Color.white;
    }

    /// <summary>Black round the edges, fading to clear in the middle.</summary>
    static Texture2D Vignette()
    {
        if (vignette != null) return vignette;
        const int n = 64;
        var px = new Color32[n * n];
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float dx = (x + 0.5f) / n * 2f - 1f, dy = (y + 0.5f) / n * 2f - 1f;
                float d = Mathf.Sqrt(dx * dx + dy * dy);
                px[y * n + x] = new Color32(0, 0, 0, (byte)(Mathf.SmoothStep(0.35f, 1.05f, d) * 255f));
            }
        vignette = new Texture2D(n, n, TextureFormat.RGBA32, false) { name = "Crawler Vignette", wrapMode = TextureWrapMode.Clamp };
        vignette.SetPixels32(px);
        vignette.Apply();
        return vignette;
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

        int wanted = Mathf.RoundToInt(count * Difficulty.Crawlers);
        bool active = wanted > 0 && Lamp.TimesLit >= Difficulty.CrawlerStartLamp && !GameOverScreen.IsActive;
        if (!active)
        {
            foreach (var c in crawlers) if (c != null && c.gameObject.activeSelf && Crawler.Holding != c) c.gameObject.SetActive(false);
            return;
        }
        while (crawlers.Count < wanted)
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
