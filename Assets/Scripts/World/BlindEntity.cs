using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// The blind one. Once three lamps have been lit, a single huge entity (6.5m) comes out of the dark: hunched,
/// long-armed, its head a smooth eyeless shape with a split for a mouth. It can't see: the flashlight means
/// nothing to it. It roams the woods around the player, stopping to cock its head and listen, clicking
/// softly to itself. Any noise in earshot (Noise.Emit: footsteps, running, bushes, doors, lamp cranking,
/// the flare gun, a glowstick clattering down) and it turns and rushes to the spot, then searches there.
/// If the player is in reach, it takes them. Crouch, move slowly, keep still when it's close. It won't
/// come into lit lamps or other lights. Trees don't stop it either: it shoulders straight into them and
/// knocks them down (TreeFall), crashing and splintering. Rocks, boulders and buildings it steps round. BlindEntitySpawner brings it in; a run reset removes it.
/// </summary>
public class BlindEntity : MonoBehaviour
{
    public static readonly List<BlindEntity> All = new List<BlindEntity>();

    public float height = 6.5f;
    [Tooltip("Walking speed while it roams.")]
    public float roamSpeed = 1.5f;
    [Tooltip("Speed when it rushes toward a noise.")]
    public float rushSpeed = 5.2f;
    [Tooltip("It roams within this distance of the player.")]
    public float roamRadius = 40f;
    [Tooltip("Moving at all within this distance of it, it hears you (even crouched).")]
    public float closeHearing = 4f;
    [Tooltip("Within this distance it takes the player.")]
    public float grabDistance = 2.6f;
    [Tooltip("How long it searches where it heard something.")]
    public float searchTime = 5f;
    [Range(0f, 1f)] public float volume = 0.8f;
    [Tooltip("How loud its footsteps are, and how far they carry.")]
    [Range(0f, 1f)] public float footstepVolume = 1f;
    public float footstepRange = 160f;
    [Tooltip("Camera shake (degrees) from a footstep right beside you; fades out with distance.")]
    public float footstepShake = 1.4f;
    [Tooltip("How far off it rises up out of the ground when it first comes, in front of the player.")]
    public float entranceDistance = 45f;

    enum State { Appearing, Roaming, Listening, Rushing, Searching, Taking }

    State state = State.Appearing;
    Vector3 goal;
    float timer, stride, clickTimer;
    int lastStep;
    Quaternion headTarget = Quaternion.identity;
    FirstPersonController player;
    DarkForestGenerator generator;
    Transform body, head, mouth;
    readonly Transform[] legs = new Transform[2], arms = new Transform[2];
    readonly List<Renderer> renderers = new List<Renderer>();
    AudioSource sfx, steps, roar, omen;
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
        if (musicTaken && MusicManager.Instance != null) MusicManager.Instance.ClearOverride();
        musicTaken = false;
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
        GroundLeaves.Register(transform, 0.9f);
        scale = height / 2.4f;
        Build();
        sfx = ProceduralAudio.AddSource(gameObject, null, false, true, volume, 4f, 45f);
        // Its footsteps and its roar carry a long way through the woods.
        steps = ProceduralAudio.AddSource(gameObject, null, false, true, footstepVolume, 10f, footstepRange);
        roar = ProceduralAudio.AddSource(gameObject, null, false, true, 1f, 18f, 300f);
        omen = ProceduralAudio.AddSource(gameObject, null, false, false, 1f); // the ground, everywhere
        generator = FindAnyObjectByType<DarkForestGenerator>();
        WorldWrap.RegisterDynamic(transform);
    }

    void Place(Vector3 near)
    {
        if (generator == null) generator = FindAnyObjectByType<DarkForestGenerator>();
        if (player == null) player = FindAnyObjectByType<FirstPersonController>();
        // Somewhere out in front, where you'll see it come up: whichever way you're looking.
        Vector3 ahead = Vector3.forward;
        var cam = Camera.main;
        if (cam != null) ahead = cam.transform.forward;
        else if (player != null) ahead = player.transform.forward;
        ahead.y = 0f;
        ahead = ahead.sqrMagnitude > 0.001f ? ahead.normalized : Vector3.forward;
        Vector3 p = near;
        for (int i = 0; i < 30; i++)
        {
            float spread = i < 20 ? 40f : 180f; // fall back to anywhere if nothing in front will do
            Vector3 d = Quaternion.Euler(0f, Random.Range(-spread, spread), 0f) * ahead * entranceDistance * Random.Range(0.85f, 1.15f);
            p = Settle(near + d);
            if (!SafeZones.Contains(p)) break;
        }
        transform.position = p;
        Vector3 toNear = near - p;
        toNear.y = 0f;
        if (toNear.sqrMagnitude > 0.01f) transform.rotation = Quaternion.LookRotation(toNear);
        goal = p;
        fadeIn = 0f;
        SetAlpha(0f);
        state = State.Appearing;
        timer = 0f;
        entrance = 0f;
        entranceBeat = 0;
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
                UpdateEntrance(dt);
                return; // its entrance: nothing else yet

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
        KnockDownTrees(speed);
        Face(target, dt * 4f);
        stride += dt * speed * 1.6f;
        // A footfall each time a leg comes down (every half swing of the stride).
        int step = Mathf.FloorToInt(stride / Mathf.PI);
        if (step != lastStep)
        {
            lastStep = step;
            Footfall(speed > roamSpeed + 0.5f ? 1f : 0.65f);
        }
        return false;
    }

    /// <summary>
    /// One of its feet coming down: a deep, heavy thud that carries a long way, the crunch of the forest
    /// floor under it, and the ground shaking under you when it's close.
    /// </summary>
    void Footfall(float weight)
    {
        if (steps != null)
        {
            steps.pitch = Random.Range(1.35f, 1.6f);
            steps.PlayOneShot(ProceduralAudio.GiantStep, footstepVolume * weight);
        }
        PlaySound(ProceduralAudio.Footstep(Random.Range(0, 4)), Random.Range(0.35f, 0.45f), weight);
        if (player != null)
        {
            float dist = Vector3.Distance(player.transform.position, transform.position);
            float shake = footstepShake * weight * (1f - Mathf.Clamp01(dist / 45f));
            if (shake > 0.02f) player.AddCameraShake(shake);
        }
    }

    // ---------------------------------------------------------------- Entrance

    // The first time it comes: the ground rumbles and shakes, lightning cracks over where it is, and it
    // bursts up out of the earth, throwing the trees round it down. It straightens, throws its head back and
    // its arms out and roars, the sky lighting up again; then it drops forward onto its knuckles, stamps
    // twice, and starts listening for you.
    const float OmenTime = 3.5f, RiseTime = 3.5f, RoarStart = OmenTime + RiseTime + 0.6f, RoarTime = 5f,
        EntranceEnd = RoarStart + RoarTime + 2.2f;

    float entrance, shake;
    int entranceBeat;
    bool musicTaken;

    void UpdateEntrance(float dt)
    {
        entrance += dt;
        float t = entrance;
        float bearing = 0f;
        if (player != null)
        {
            Vector3 to = transform.position - player.transform.position;
            bearing = Mathf.Atan2(to.x, to.z) * Mathf.Rad2Deg;
        }

        // Each beat happens once, in order.
        if (Beat(0, 0f))
        {
            omen.pitch = 0.6f;
            omen.PlayOneShot(ProceduralAudio.DistantRumble, 1f);
            omen.PlayOneShot(ProceduralAudio.Tension, 0.5f);
            if (MusicManager.Instance != null) { MusicManager.Instance.SetOverride(MusicManager.Mood.Danger); musicTaken = true; }
        }
        if (Beat(1, OmenTime))
        {
            // It breaks out of the ground.
            if (Lightning.Instance != null) Lightning.Instance.StrikeAt(bearing, 0.85f);
            PlaySound(ProceduralAudio.TreeImpact, 0.55f, 1f);
            steps.pitch = 0.8f;
            steps.PlayOneShot(ProceduralAudio.GiantStep, footstepVolume);
            LeafBurst.Spawn(new Bounds(transform.position + Vector3.up, new Vector3(9f, 2f, 9f)));
            ThrowDownTrees(7f);
            shake = 2.5f;
        }
        if (Beat(2, OmenTime + RiseTime * 0.5f)) PlaySound(ProceduralAudio.TreeSnap, 0.5f, 1f);
        if (Beat(3, RoarStart))
        {
            roar.pitch = 0.62f;
            roar.PlayOneShot(ProceduralAudio.Scream, 1f);
            omen.pitch = 0.45f;
            omen.PlayOneShot(ProceduralAudio.DistantRumble, 0.8f);
            if (MusicManager.Instance != null) MusicManager.Instance.PlayStinger();
            shake = 3f;
        }
        if (Beat(4, RoarStart + 0.5f) && Lightning.Instance != null) Lightning.Instance.StrikeAt(bearing + Random.Range(-15f, 15f), 1f);
        if (Beat(5, RoarStart + RoarTime + 0.5f)) { Footfall(1.3f); shake = 2f; }
        if (Beat(6, RoarStart + RoarTime + 1.1f)) { Footfall(1.3f); shake = 2f; }

        // The ground trembling, building as it comes and while it roars.
        float tremble = 0f;
        if (t < OmenTime) tremble = Mathf.Lerp(0.1f, 0.8f, t / OmenTime);
        else if (t < RoarStart) tremble = 0.6f;
        else if (t < RoarStart + RoarTime) tremble = 1.2f;
        shake = Mathf.MoveTowards(shake, 0f, dt * 1.2f);
        if (player != null) player.AddCameraShake(Mathf.Max(tremble, shake));

        // Rising up out of the earth, hunched and bowed, fast at first, then heaving the last of itself out.
        fadeIn = Mathf.Clamp01((t - OmenTime) / RiseTime);
        SetAlpha(1f - (1f - fadeIn) * (1f - fadeIn));

        // The pose: bowed as it comes up, then straightening, head and arms flung back to roar, then down.
        float straighten = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(OmenTime + RiseTime, RoarStart, t))
                         * (1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(RoarStart + RoarTime - 0.3f, RoarStart + RoarTime + 0.5f, t)));
        float k = 1f - Mathf.Exp(-6f * dt);
        body.localRotation = Quaternion.Slerp(body.localRotation, Quaternion.Euler(Mathf.Lerp(60f, -14f, straighten), 0f, 0f), k);
        Quaternion headGoal = Quaternion.Euler(Mathf.Lerp(30f, -55f, straighten), 0f, 0f);
        if (straighten > 0.5f) headGoal *= Quaternion.Euler(Random.insideUnitSphere * 4f * straighten); // shaking with it
        head.localRotation = Quaternion.Slerp(head.localRotation, headGoal, k);
        for (int i = 0; i < 2; i++)
        {
            float side = i == 0 ? -1f : 1f;
            arms[i].localRotation = Quaternion.Slerp(arms[i].localRotation,
                Quaternion.Euler(Mathf.Lerp(0f, -25f, straighten), 0f, side * Mathf.Lerp(6f, 65f, straighten)), k);
            legs[i].localRotation = Quaternion.Slerp(legs[i].localRotation, Quaternion.identity, k);
        }
        float open = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(RoarStart - 0.2f, RoarStart + 0.4f, t))
                   * (1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(RoarStart + RoarTime - 1f, RoarStart + RoarTime, t)));
        mouth.localScale = new Vector3(Mathf.Lerp(1f, 1.8f, open), Mathf.Lerp(0.25f, 1.5f, open), 1f);

        if (t >= EntranceEnd)
        {
            if (musicTaken && MusicManager.Instance != null) MusicManager.Instance.ClearOverride();
            musicTaken = false;
            mouth.localScale = new Vector3(1f, 0.25f, 1f);
            state = State.Searching; // a moment listening, then it goes looking
            timer = 2.5f;
        }
    }

    /// <summary>True once, the first frame the entrance passes the given time.</summary>
    bool Beat(int index, float at)
    {
        if (entranceBeat != index || entrance < at) return false;
        entranceBeat++;
        return true;
    }

    /// <summary>Bursting up out of the ground, it throws down every tree close round it, falling outward.</summary>
    void ThrowDownTrees(float radius)
    {
        int count = Physics.OverlapSphereNonAlloc(transform.position + Vector3.up, radius, overlaps, ~0, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < count; i++)
        {
            Transform sway = TreeSway(overlaps[i]);
            if (sway == null) continue;
            Vector3 dir = sway.position - transform.position;
            dir.y = 0f;
            dir = dir.sqrMagnitude > 0.01f ? dir.normalized : Random.insideUnitSphere;
            TreeFall.Fell(sway, dir, Random.Range(0f, 0.3f), 1.3f, volume);
        }
    }

    /// <summary>Any tree its body walks into is shoved over, falling the way it's going.</summary>
    void KnockDownTrees(float speed)
    {
        float r = BodyRadius;
        int count = Physics.OverlapCapsuleNonAlloc(transform.position + Vector3.up * (1f + r), transform.position + Vector3.up * (height - r),
            r, overlaps, ~0, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < count; i++)
        {
            Transform sway = TreeSway(overlaps[i]);
            if (sway == null) continue;
            Vector3 dir = Quaternion.Euler(0f, Random.Range(-35f, 35f), 0f) * transform.forward;
            bool rushing = speed > roamSpeed + 0.5f;
            // Shoved hard: a short crack, and over it goes.
            TreeFall.Fell(sway, dir, rushing ? 0.1f : 0.35f, rushing ? 1.1f : 0.6f, volume);
        }
    }

    /// <summary>The sway pivot of the tree a collider is the trunk of, or null if it isn't a standing tree.</summary>
    static Transform TreeSway(Collider c)
    {
        if (!(c is CapsuleCollider) || !c.enabled) return null;
        Transform sway = c.transform.Find("Sway");
        return sway != null && TreeFall.CanFell(sway) ? sway : null;
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
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
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

    float BodyRadius => 0.9f * scale / 2.7f;

    /// <summary>
    /// Puts a point on the ground, stepping round rocks, boulders and buildings. Trees don't count: it
    /// walks into them and knocks them down.
    /// </summary>
    Vector3 Settle(Vector3 p)
    {
        float r = BodyRadius;
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
                if (c is CapsuleCollider && c.transform.Find("Sway") != null) continue; // a tree: it goes through (and over)
                if (c.attachedRigidbody != null) continue;                             // fallen pieces and splinters
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
