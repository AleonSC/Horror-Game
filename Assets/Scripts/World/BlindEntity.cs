using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// The blind one. Once three lamps have been lit, a single larger entity comes out of the dark: hunched,
/// long-armed, its head a smooth eyeless shape with a split for a mouth. It can't see: the flashlight means
/// nothing to it. It roams the woods around the player, stopping to cock its head and listen, clicking
/// softly to itself. Any noise in earshot (Noise.Emit: footsteps, running, bushes, doors, lamp cranking,
/// the flare gun, a glowstick clattering down) and it turns and rushes to the spot, then searches there.
/// If the player is in reach, it takes them. Crouch, move slowly, keep still when it's close. It won't
/// come into lit lamps or other lights. BlindEntitySpawner brings it in; a run reset removes it.
/// </summary>
public class BlindEntity : MonoBehaviour
{
    public static readonly List<BlindEntity> All = new List<BlindEntity>();

    public float height = 3.4f;
    [Tooltip("Walking speed while it roams.")]
    public float roamSpeed = 1.1f;
    [Tooltip("Speed when it rushes toward a noise.")]
    public float rushSpeed = 4.2f;
    [Tooltip("It roams within this distance of the player.")]
    public float roamRadius = 40f;
    [Tooltip("Moving at all within this distance of it, it hears you (even crouched).")]
    public float closeHearing = 3f;
    [Tooltip("Within this distance it takes the player.")]
    public float grabDistance = 1.7f;
    [Tooltip("How long it searches where it heard something.")]
    public float searchTime = 5f;
    [Range(0f, 1f)] public float volume = 0.8f;

    enum State { Appearing, Roaming, Listening, Rushing, Searching, Taking }

    State state = State.Appearing;
    Vector3 goal;
    float timer, stride, clickTimer, stepTimer;
    Quaternion headTarget = Quaternion.identity;
    FirstPersonController player;
    DarkForestGenerator generator;
    Transform body, head, mouth;
    readonly Transform[] legs = new Transform[2], arms = new Transform[2];
    readonly List<Renderer> renderers = new List<Renderer>();
    AudioSource sfx;
    float fadeIn;
    float scale;

    void OnEnable()
    {
        All.Add(this);
        Noise.Heard += OnNoise;
    }

    void OnDisable()
    {
        All.Remove(this);
        Noise.Heard -= OnNoise;
    }

    /// <summary>Brings one in, somewhere out in the dark near the player.</summary>
    public static BlindEntity Spawn(Vector3 near)
    {
        var go = new GameObject("Blind Entity");
        var e = go.AddComponent<BlindEntity>();
        e.Place(near);
        return e;
    }

    void Awake()
    {
        scale = height / 2.4f;
        Build();
        sfx = ProceduralAudio.AddSource(gameObject, null, false, true, volume, 4f, 45f);
        generator = FindAnyObjectByType<DarkForestGenerator>();
        WorldWrap.RegisterDynamic(transform);
    }

    void Place(Vector3 near)
    {
        if (generator == null) generator = FindAnyObjectByType<DarkForestGenerator>();
        Vector3 p = near;
        for (int i = 0; i < 20; i++)
        {
            Vector2 d = Random.insideUnitCircle.normalized * Random.Range(35f, 50f);
            p = Settle(near + new Vector3(d.x, 0f, d.y));
            if (!SafeZones.Contains(p)) break;
        }
        transform.position = p;
        goal = p;
        fadeIn = 0f;
        SetAlpha(0f);
        state = State.Appearing;
        timer = 0f;
    }

    // ---------------------------------------------------------------- Hearing

    void OnNoise(Vector3 at, float radius)
    {
        if (state == State.Taking || state == State.Appearing) return;
        Vector3 d = at - transform.position;
        d.y = 0f;
        float dist = d.magnitude;
        if (dist > radius) return;

        // It knows roughly where: the further off, the rougher its guess.
        Vector2 miss = Random.insideUnitCircle * dist * 0.12f;
        goal = Settle(at + new Vector3(miss.x, 0f, miss.y));
        if (state != State.Rushing)
        {
            // A sharp turn of the head toward it, a beat of stillness, then it goes.
            state = State.Listening;
            timer = Random.Range(0.25f, 0.6f);
            PlaySound(ProceduralAudio.Creep, 0.6f, 1f);
        }
    }

    // ---------------------------------------------------------------- Behaviour

    void Update()
    {
        if (player == null)
        {
            player = FindAnyObjectByType<FirstPersonController>();
            if (player == null) return;
        }
        float dt = Time.deltaTime;
        Vector3 me = transform.position;
        Vector3 you = player.transform.position;
        Vector3 toYou = you - me;
        toYou.y = 0f;

        switch (state)
        {
            case State.Appearing:
                fadeIn = Mathf.MoveTowards(fadeIn, 1f, dt / 2.5f);
                SetAlpha(fadeIn);
                if (fadeIn >= 1f) { state = State.Roaming; PickRoamGoal(); }
                return; // rising up out of the ground: nothing else yet

            case State.Roaming:
                // Never strays far from where the player is; drifts after them.
                if ((goal - you).sqrMagnitude > roamRadius * roamRadius * 1.5f) PickRoamGoal();
                if (MoveToward(goal, roamSpeed, dt))
                {
                    state = State.Searching;
                    timer = Random.Range(2f, 4f); // a pause to listen
                }
                break;

            case State.Listening:
                timer -= dt;
                Face(goal, dt * 12f);
                if (timer <= 0f) state = State.Rushing;
                break;

            case State.Rushing:
                if (MoveToward(goal, rushSpeed, dt))
                {
                    state = State.Searching;
                    timer = searchTime;
                }
                break;

            case State.Searching:
                timer -= dt;
                // Head sweeping, cocking side to side, listening hard.
                if (timer <= 0f) { state = State.Roaming; PickRoamGoal(); }
                break;

            case State.Taking:
                UpdateTaking(dt);
                return;
        }

        // Moving at all right next to it gives you away, however softly.
        if (toYou.magnitude < closeHearing && player.CurrentSpeed > 0.3f && state != State.Rushing && !PortalDoor.PlayerInRoom)
            OnNoise(you, closeHearing + 1f);

        // In reach: it has you. (Not up the watchtower, not in the light, not through the door.)
        bool reachable = Mathf.Abs(you.y - me.y) < 2.2f && !SafeZones.Contains(you) && !PortalDoor.PlayerInRoom && !GameOverScreen.IsActive;
        if (reachable && toYou.magnitude < grabDistance) StartTaking();

        Animate(dt);
        Clicks(dt);
    }

    void PickRoamGoal()
    {
        Vector3 you = player != null ? player.transform.position : transform.position;
        for (int i = 0; i < 10; i++)
        {
            Vector2 d = Random.insideUnitCircle * roamRadius;
            Vector3 p = Settle(you + new Vector3(d.x, 0f, d.y));
            if (!SafeZones.Contains(p)) { goal = p; return; }
        }
        goal = transform.position;
    }

    /// <summary>Walks toward a point, round trunks and rocks, never into the light. True once it's there.</summary>
    bool MoveToward(Vector3 target, float speed, float dt)
    {
        Vector3 to = target - transform.position;
        to.y = 0f;
        float dist = to.magnitude;
        if (dist < 0.6f) return true;
        Vector3 next = Settle(transform.position + to / dist * Mathf.Min(speed * dt, dist));
        if (SafeZones.Contains(next)) return true; // the light stops it: it gives up here
        transform.position = next;
        Face(target, dt * 4f);
        stride += dt * speed * 1.6f;
        stepTimer -= dt;
        if (stepTimer <= 0f)
        {
            stepTimer = Mathf.Lerp(0.9f, 0.35f, Mathf.InverseLerp(roamSpeed, rushSpeed, speed));
            PlaySound(ProceduralAudio.Footstep(Random.Range(0, 4)), Random.Range(0.5f, 0.6f), speed > roamSpeed + 0.5f ? 1f : 0.6f);
        }
        return false;
    }

    void Face(Vector3 target, float rate)
    {
        Vector3 to = target - transform.position;
        to.y = 0f;
        if (to.sqrMagnitude < 0.01f) return;
        transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(to), 1f - Mathf.Exp(-rate));
    }

    /// <summary>Soft clicks of its tongue, now and then: you hear it before you see it.</summary>
    void Clicks(float dt)
    {
        clickTimer -= dt;
        if (clickTimer > 0f) return;
        clickTimer = state == State.Searching || state == State.Listening ? Random.Range(0.6f, 1.5f) : Random.Range(2.5f, 6f);
        int clicks = Random.Range(2, 5);
        for (int i = 0; i < clicks; i++) Invoke(nameof(Click), i * Random.Range(0.09f, 0.16f));
    }

    void Click() => PlaySound(ProceduralAudio.TwigSnap, Random.Range(2.2f, 2.8f), 0.35f);

    // ---------------------------------------------------------------- Taking the player

    float takeTimer;

    void StartTaking()
    {
        state = State.Taking;
        takeTimer = 0f;
        player.enabled = false;
        Vector3 to = player.transform.position - transform.position;
        to.y = 0f;
        transform.rotation = Quaternion.LookRotation(to);
        PlaySound(ProceduralAudio.Screech, 0.6f, 1f);
    }

    void UpdateTaking(float dt)
    {
        takeTimer += dt;
        // It bends right down to your face, mouth splitting open, and holds you there.
        var cam = Camera.main != null ? Camera.main.transform : null;
        Vector3 dir = player.transform.position - transform.position;
        dir.y = 0f;
        dir = dir.sqrMagnitude > 0.001f ? dir.normalized : transform.forward;
        transform.position = Vector3.Lerp(transform.position, player.transform.position - dir * 1.1f, 1f - Mathf.Exp(-6f * dt));
        body.localRotation = Quaternion.Slerp(body.localRotation, Quaternion.Euler(55f, 0f, 0f), 1f - Mathf.Exp(-4f * dt));
        mouth.localScale = new Vector3(1f, Mathf.MoveTowards(mouth.localScale.y, 1f, dt * 1.5f), 1f);
        mouth.gameObject.SetActive(true);
        if (cam != null)
        {
            Quaternion look = Quaternion.LookRotation(head.position - cam.position);
            cam.rotation = Quaternion.Slerp(cam.rotation, look, 1f - Mathf.Exp(-10f * dt)) * Quaternion.Euler(Random.insideUnitSphere * 1.2f);
        }
        if (takeTimer > 1.8f) GameOverScreen.Show(2.5f);
    }

    // ---------------------------------------------------------------- Body

    void Animate(float dt)
    {
        bool moving = state == State.Roaming || state == State.Rushing;
        float rushing = state == State.Rushing ? 1f : 0f;
        float swing = moving ? Mathf.Sin(stride) : 0f;
        for (int i = 0; i < 2; i++)
        {
            float s = i == 0 ? swing : -swing;
            legs[i].localRotation = Quaternion.Euler(s * (20f + 15f * rushing), 0f, 0f);
            arms[i].localRotation = Quaternion.Euler(-s * (12f + 20f * rushing) + 10f * rushing, 0f, (i == 0 ? -1f : 1f) * 6f);
        }
        body.localRotation = Quaternion.Slerp(body.localRotation, Quaternion.Euler(25f + 20f * rushing, 0f, 0f), 1f - Mathf.Exp(-3f * dt));
        body.localPosition = Vector3.up * Mathf.Abs(Mathf.Cos(stride)) * (moving ? 0.05f * scale : 0f);

        // Listening: the head cocks sharply this way and that.
        if (state == State.Searching || state == State.Listening || Random.value < 0.01f)
        {
            if (Random.value < dt * 1.5f)
                headTarget = Quaternion.Euler(Random.Range(-25f, 15f), Random.Range(-60f, 60f), Random.Range(-50f, 50f));
        }
        else if (Random.value < dt * 0.3f) headTarget = Quaternion.Euler(Random.Range(-10f, 10f), Random.Range(-20f, 20f), Random.Range(-15f, 15f));
        head.localRotation = Quaternion.Slerp(head.localRotation, headTarget, 1f - Mathf.Exp(-14f * dt));
    }

    void Build()
    {
        var black = MaterialFactory.Unlit(new Color(0.012f, 0.012f, 0.014f));
        float s = scale;
        body = Pivot(transform, "Body", Vector3.zero);

        for (int i = 0; i < 2; i++)
        {
            float side = i == 0 ? -1f : 1f;
            legs[i] = Pivot(body, "Hip", new Vector3(side * 0.14f, 1.05f, 0f) * s);
            Bone(legs[i], black, Vector3.zero, new Vector3(side * 0.05f, -0.5f, 0.18f) * s, 0.09f * s);
            Bone(legs[i], black, new Vector3(side * 0.05f, -0.5f, 0.18f) * s, new Vector3(side * 0.03f, -1.02f, -0.05f) * s, 0.06f * s);
            for (int toe = -1; toe <= 1; toe++)
                Bone(legs[i], black, new Vector3(side * 0.03f, -1.02f, -0.05f) * s, new Vector3(side * 0.03f + toe * 0.05f, -1.05f, 0.12f) * s, 0.02f * s);

            // Arms long enough to drag their knuckles.
            arms[i] = Pivot(body, "Shoulder", new Vector3(side * 0.3f, 1.85f, 0.05f) * s);
            Bone(arms[i], black, Vector3.zero, new Vector3(side * 0.1f, -0.75f, -0.1f) * s, 0.06f * s);
            Bone(arms[i], black, new Vector3(side * 0.1f, -0.75f, -0.1f) * s, new Vector3(side * 0.08f, -1.5f, 0.15f) * s, 0.045f * s);
            for (int f = 0; f < 4; f++)
                Bone(arms[i], black, new Vector3(side * 0.08f, -1.5f, 0.15f) * s, new Vector3(side * 0.08f + (f - 1.5f) * 0.04f, -1.82f, 0.25f) * s, 0.014f * s);
        }

        // A heavy, hunched back.
        Bone(body, black, new Vector3(0f, 1.05f, 0f) * s, new Vector3(0f, 1.5f, -0.12f) * s, 0.12f * s);
        Bone(body, black, new Vector3(0f, 1.5f, -0.12f) * s, new Vector3(0f, 1.9f, 0.08f) * s, 0.12f * s);
        Part(body, black, PrimitiveType.Capsule, new Vector3(0f, 1.6f, -0.05f) * s, new Vector3(0.42f, 0.3f, 0.3f) * s, Quaternion.Euler(20f, 0f, 0f));
        Bone(body, black, new Vector3(-0.3f, 1.85f, 0.05f) * s, new Vector3(0.3f, 1.85f, 0.05f) * s, 0.07f * s);
        Bone(body, black, new Vector3(0f, 1.9f, 0.08f) * s, new Vector3(0f, 2.02f, 0.3f) * s, 0.07f * s);

        // The head: big, smooth, eyeless, with a vertical split for a mouth and holes where ears should be.
        head = Pivot(body, "Head", new Vector3(0f, 2.02f, 0.3f) * s);
        Part(head, black, PrimitiveType.Sphere, new Vector3(0f, 0.08f, 0.06f) * s, new Vector3(0.26f, 0.32f, 0.3f) * s, Quaternion.identity);
        var hole = MaterialFactory.NoFog(new Color(0.12f, 0.01f, 0.01f));
        for (int side = -1; side <= 1; side += 2)
            Part(head, hole, PrimitiveType.Sphere, new Vector3(side * 0.13f, 0.1f, 0.04f) * s, new Vector3(0.02f, 0.06f, 0.04f) * s, Quaternion.identity);
        mouth = Pivot(head, "Mouth", new Vector3(0f, 0.04f, 0.2f) * s);
        Part(mouth, hole, PrimitiveType.Sphere, Vector3.zero, new Vector3(0.05f, 0.24f, 0.03f) * s, Quaternion.identity);
        mouth.localScale = new Vector3(1f, 0.25f, 1f); // a thin split, until it opens

        foreach (var r in GetComponentsInChildren<Renderer>()) renderers.Add(r);
    }

    /// <summary>Fades in from the dark (as it first appears) by shrinking up out of the ground: it has no transparency.</summary>
    void SetAlpha(float a)
    {
        foreach (var r in renderers) r.enabled = a > 0.02f;
        body.localPosition = Vector3.down * (1f - a) * height;
    }

    Transform Pivot(Transform parent, string name, Vector3 at)
    {
        var t = new GameObject(name).transform;
        t.SetParent(parent, false);
        t.localPosition = at;
        return t;
    }

    void Bone(Transform parent, Material mat, Vector3 a, Vector3 b, float thickness)
    {
        Vector3 d = b - a;
        Part(parent, mat, PrimitiveType.Cylinder, (a + b) * 0.5f, new Vector3(thickness, d.magnitude * 0.5f, thickness), Quaternion.FromToRotation(Vector3.up, d));
    }

    Transform Part(Transform parent, Material mat, PrimitiveType type, Vector3 pos, Vector3 scale, Quaternion rot)
    {
        var go = GameObject.CreatePrimitive(type);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = pos;
        go.transform.localRotation = rot;
        go.transform.localScale = scale;
        var r = go.GetComponent<Renderer>();
        r.sharedMaterial = mat;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        Destroy(go.GetComponent<Collider>()); // a ghost to physics
        return go.transform;
    }

    void PlaySound(AudioClip clip, float pitch, float vol)
    {
        if (clip == null || sfx == null) return;
        sfx.pitch = pitch;
        sfx.PlayOneShot(clip, volume * vol);
    }

    // ---------------------------------------------------------------- Ground and obstacles

    float GroundHeight(Vector3 p) => generator != null ? generator.GetGroundHeight(p.x, p.z) : p.y;

    static readonly Collider[] overlaps = new Collider[16];

    /// <summary>Puts a point on the ground, pushed out of any trunk or rock its body would be inside.</summary>
    Vector3 Settle(Vector3 p)
    {
        const float r = 0.45f;
        p.y = GroundHeight(p);
        for (int iteration = 0; iteration < 4; iteration++)
        {
            int count = Physics.OverlapCapsuleNonAlloc(p + Vector3.up * (0.8f + r), p + Vector3.up * (height - r), r, overlaps, ~0, QueryTriggerInteraction.Ignore);
            bool pushed = false;
            Vector3 probe = p + Vector3.up * 1.3f;
            for (int i = 0; i < count; i++)
            {
                Collider c = overlaps[i];
                if (c is CharacterController || c.GetComponentInParent<FirstPersonController>() != null) continue;
                if (c is MeshCollider ground && ground.sharedMesh != null && ground.sharedMesh.name == "Forest Ground") continue;
                Bounds b = c.bounds;
                Vector3 push;
                float clearance;
                if (c is MeshCollider mesh && !mesh.convex) { push = probe - b.center; clearance = Mathf.Max(b.extents.x, b.extents.z) * 0.85f + r; }
                else
                {
                    push = probe - c.ClosestPoint(probe);
                    push.y = 0f;
                    clearance = r + 0.02f;
                    if (push.sqrMagnitude < 0.0001f) { push = probe - b.center; clearance = r + Mathf.Max(b.extents.x, b.extents.z); }
                }
                push.y = 0f;
                if (push.sqrMagnitude < 0.0001f) push = transform.right;
                float d = push.magnitude;
                if (d < clearance) { p += push / d * (clearance - d); pushed = true; }
            }
            p.y = GroundHeight(p);
            if (!pushed) break;
        }
        return p;
    }
}

/// <summary>Brings in the blind entity the first time enough lamps have been lit in a run. DarkForestGenerator adds this.</summary>
public class BlindEntitySpawner : MonoBehaviour
{
    [Tooltip("Lamps lit before the blind one comes.")]
    public int lampsToSpawn = 3;

    void Update()
    {
        if (Lamp.TimesLit < lampsToSpawn || BlindEntity.All.Count > 0 || GameOverScreen.IsActive) return;
        var player = FindAnyObjectByType<FirstPersonController>();
        if (player == null || PortalDoor.PlayerInRoom) return;
        BlindEntity.Spawn(player.transform.position);
    }
}
