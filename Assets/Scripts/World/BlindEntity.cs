using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// The blind one. Once three lamps have been lit, a single huge entity (7.4m) comes out of the dark: hunched,
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

    // Static state is cleared when Play starts, so nothing carries over from the last session if the
    // editor's Enter Play Mode settings skip reloading scripts (no domain reload).
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() { All.Clear(); BlindEntitySpawner.ResetRun(); }

    public float height = 7.4f;
    [Tooltip("How heavy-limbed it is: every limb's thickness is multiplied by this.")]
    public float bulk = 1.45f;
    [Tooltip("Walking speed while it roams.")]
    public float roamSpeed = 1.5f;
    [Tooltip("Speed when it rushes toward a noise.")]
    public float rushSpeed = 5.2f;
    [Tooltip("It roams within this distance of the player.")]
    public float roamRadius = 40f;
    [Header("Closing In")]
    [Tooltip("Left alone, the area it roams drifts toward wherever the player is and tightens: over this many seconds it narrows from roamRadius down to closeRoamRadius.")]
    public float closeInTime = 150f;
    public float closeRoamRadius = 12f;
    [Tooltip("However close it's closed in, it never picks somewhere to walk to within this distance of the player: it finds you by hearing you, not by heading for you.")]
    public float keepAway = 7f;
    Vector3 huntArea;
    float closing; // 0 (just arrived, or just lost you) to 1 (closed right in round where you are)
    [Tooltip("If the player stays further than this from it...")]
    public float tooFarDistance = 90f;
    [Tooltip("...for this many seconds, it sinks into the ground and comes up again somewhere near them.")]
    public float tooFarTime = 60f;
    [Tooltip("How far from the player it comes up when it does.")]
    public float catchUpDistance = 35f;
    float farTimer;

    [Header("Hearing You")]
    [Tooltip("Once it's heard you, it searches only round the last place it heard you for this many seconds (it can't follow someone who's gone quiet).")]
    public float investigateTime = 45f;
    [Tooltip("How widely it searches round that spot.")]
    public float investigateRadius = 10f;
    [Tooltip("Ordinary walking footsteps only give you away this close (crouching, only within closeHearing).")]
    public float walkingHearing = 5f;
    float lastHeardTime = -999f;
    Vector3 lastHeardAt;
    float growlTimer = 4f;
    bool Investigating => Time.time - lastHeardTime < investigateTime;
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

    enum State { Appearing, Roaming, Listening, Rushing, Searching, Taking, Banished }

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
        BlindEntityTheme.FadeOut();
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

    /// <summary>
    /// Left too far behind for too long (a minute more than 90m away), it doesn't trudge all that way: it
    /// sinks into the earth and rises again quietly somewhere round the player, not too close, any side.
    /// Not while it's chasing a sound or a radio, or taking someone.
    /// </summary>
    void CatchUp(float dt, Vector3 you)
    {
        bool wandering = state == State.Roaming || state == State.Searching || state == State.Listening;
        Vector3 d = WorldWrap.NearestImage(transform.position, you) - you;
        d.y = 0f;
        if (!wandering || d.magnitude < tooFarDistance || PortalDoor.PlayerInRoom) { farTimer = 0f; return; }
        farTimer += dt;
        if (farTimer < tooFarTime) return;
        farTimer = 0f;
        quietReturn = true;
        Place(you, catchUpDistance, false);
    }

    void Place(Vector3 near) => Place(near, entranceDistance, true);

    /// <summary>Puts it somewhere a distance from a point, rising up out of the ground: in view (in front) or anywhere round.</summary>
    void Place(Vector3 near, float distance, bool inView)
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
            float spread = inView && i < 20 ? 40f : 180f; // fall back to anywhere if nothing in front will do
            Vector3 d = Quaternion.Euler(0f, Random.Range(-spread, spread), 0f) * ahead * distance * Random.Range(0.85f, 1.15f);
            p = Settle(near + d);
            if (!SafeZones.Contains(p)) break;
        }
        transform.position = p;
        huntArea = p;  // it starts hunting from where it comes up, and closes in from there
        closing = 0f;
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
        if (state == State.Taking || state == State.Appearing || state == State.Banished) return;
        Vector3 d = at - transform.position;
        d.y = 0f;
        float dist = d.magnitude;
        if (dist > radius) return;
        // Quiet sounds (ordinary walking, a door, a click) only carry to it from close by.
        if (radius <= 7.5f && dist > walkingHearing) return;

        // It knows roughly where: the further off, the rougher its guess. That spot is all it knows: go quiet
        // and it can only search round the last place it heard you.
        Vector2 miss = Random.insideUnitCircle * dist * 0.12f;
        goal = Settle(at + new Vector3(miss.x, 0f, miss.y));
        huntArea = lastHeardAt = goal;
        lastHeardTime = Time.time;
        closing = Mathf.Min(closing, 0.4f);
        if (state != State.Rushing)
        {
            // A sharp turn of the head toward it, a snarl, a beat of stillness, then it goes.
            state = State.Listening;
            timer = Random.Range(0.35f, 0.7f);
            PlaySound(ProceduralAudio.BlindAlert, Random.Range(0.9f, 1.05f), 1f);
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

        UpdateHuntArea(dt, you);
        CatchUp(dt, you);

        switch (state)
        {
            case State.Appearing:
                UpdateEntrance(dt);
                return; // its entrance: nothing else yet

            case State.Banished:
                timer -= dt;
                if (timer <= 0f)
                {
                    // Back, quietly this time: it just rises up out of the dark somewhere near.
                    quietReturn = true;
                    Place(player.transform.position);
                }
                return;

            case State.Roaming:
                // The area it roams creeps toward wherever the player is and tightens round it, slowly.
                if ((goal - huntArea).sqrMagnitude > RoamRange * RoamRange * 2.25f) PickRoamGoal();
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
        Growl(dt);
    }

    /// <summary>How far round its hunting area it roams now: wide at first, narrowing as it closes in.</summary>
    float RoamRange => Investigating ? investigateRadius : Mathf.Lerp(roamRadius, closeRoamRadius, Mathf.SmoothStep(0f, 1f, closing));

    /// <summary>
    /// The slow tightening of the net. While it's wandering (not chasing a sound), the area it searches
    /// creeps toward the player, faster the longer it's gone without hearing them, and narrows round where
    /// they are. It never walks straight to them: it just ends up searching closer and closer by.
    /// </summary>
    void UpdateHuntArea(float dt, Vector3 you)
    {
        if (player == null) return;
        huntArea = WorldWrap.NearestImage(huntArea, you);
        if (Investigating)
        {
            // Searching round the last place it heard you, and nowhere else: it can't track you by silence.
            huntArea = WorldWrap.NearestImage(lastHeardAt, you);
            return;
        }
        if (state == State.Roaming || state == State.Searching)
            closing = Mathf.Clamp01(closing + dt / Mathf.Max(closeInTime, 1f));
        float creep = Mathf.Lerp(0.4f, 1.6f, closing); // metres a second: never quicker than you can walk
        Vector3 toYou = you - huntArea;
        toYou.y = 0f;
        float stopShort = keepAway * 0.5f;
        if (toYou.magnitude > stopShort)
            huntArea += toYou.normalized * Mathf.Min(creep * dt, toYou.magnitude - stopShort);
    }

    void PickRoamGoal()
    {
        Vector3 you = player != null ? player.transform.position : transform.position;
        float range = RoamRange;
        for (int i = 0; i < 14; i++)
        {
            Vector2 d = Random.insideUnitCircle * range;
            Vector3 p = Settle(huntArea + new Vector3(d.x, 0f, d.y));
            Vector3 fromYou = p - you;
            fromYou.y = 0f;
            if (SafeZones.Contains(p) || fromYou.magnitude < keepAway) continue; // never right to where you are
            goal = p;
            return;
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

    // ---------------------------------------------------------------- Lured and banished

    bool quietReturn;
    BlindStorm storm;

    /// <summary>
    /// Something it can't resist (the car radio): it rushes there, from however far away, and keeps
    /// coming as long as it's called.
    /// </summary>
    public void Lure(Vector3 at)
    {
        if (state == State.Taking || state == State.Appearing || state == State.Banished) return;
        Vector3 g = Settle(WorldWrap.NearestImage(at, transform.position)); // the nearest way round the looping map
        if (state == State.Rushing && (g - goal).sqrMagnitude < 1f) return;
        goal = g;
        state = State.Rushing;
    }

    /// <summary>
    /// Caught in the camera's flash: it shrieks and is gone, and doesn't come back for a while (then it
    /// rises up out of the ground near the player again, without the fanfare of its first entrance).
    /// </summary>
    public void Banish(float seconds)
    {
        if (state == State.Taking || state == State.Appearing || state == State.Banished) return;
        Vector3 head = transform.position + Vector3.up * height * 0.8f;
        ProceduralAudio.PlayAt(ProceduralAudio.Screech, head, 1f, 0.75f, 10f);
        ProceduralAudio.PlayAt(ProceduralAudio.Poof, head, 1f, 0.6f, 10f);
        SetAlpha(0f);
        transform.position += Vector3.down * 100f; // out of the way of anything measuring how close it is
        state = State.Banished;
        timer = seconds;
    }

    /// <summary>
    /// The player was caught (by anything): it's gone for a while, then rises again near them quietly. Unlike
    /// Banish this works whatever it's doing (except mid-entrance, which plays out).
    /// </summary>
    public void Retreat(float seconds)
    {
        if (state == State.Appearing && !quietReturn) return;
        SetAlpha(0f);
        mouth.localScale = new Vector3(1f, 0.25f, 1f); // (if it was taking the player, its mouth was open)
        transform.position += Vector3.down * 100f;
        quietReturn = false;
        state = State.Banished;
        timer = seconds;
    }

    /// <summary>True when it's out in the world, where it can be seen (and flashed).</summary>
    public bool IsPresent => state != State.Banished && (state != State.Appearing || fadeIn > 0.3f);

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
        if (quietReturn)
        {
            fadeIn = Mathf.MoveTowards(fadeIn, 1f, dt / 2.5f);
            SetAlpha(fadeIn);
            if (fadeIn >= 1f)
            {
                quietReturn = false;
                state = State.Roaming;
                PickRoamGoal();
            }
            return;
        }
        entrance += dt;
        float t = entrance;

        // Each beat happens once, in order.
        if (Beat(0, 0f))
        {
            omen.pitch = 0.6f;
            omen.PlayOneShot(ProceduralAudio.DistantRumble, 1f);
            omen.PlayOneShot(ProceduralAudio.Tension, 0.5f);
            BlindEntityTheme.Play(); // its own music, scored to all this
            if (player != null) player.DrawAttention(() => transform.position + Vector3.up * height * 0.6f, 9f, 0.55f); // look: over there
            storm = BlindStorm.Over(transform.position); // a black cloud rolls in over it, crackling red
        }
        if (Beat(1, OmenTime))
        {
            // It breaks out of the ground.
            if (storm != null) storm.Strike(true);
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
            shake = 3f;
        }
        if (Beat(4, RoarStart + 0.5f) && storm != null) storm.Strike(true);
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

    /// <summary>Now and then, as it goes, a long low growl rattling in its chest.</summary>
    void Growl(float dt)
    {
        if (state == State.Taking || state == State.Appearing || state == State.Banished) return;
        growlTimer -= dt;
        if (growlTimer > 0f) return;
        growlTimer = state == State.Searching || state == State.Listening ? Random.Range(4f, 8f) : Random.Range(6f, 14f);
        PlaySound(ProceduralAudio.BlindGrowl, Random.Range(0.85f, 1.1f), Random.Range(0.6f, 0.85f));
    }

    // ---------------------------------------------------------------- Taking the player

    float takeTimer, headRoll;
    int takeBeat;
    bool takeEnded;
    Vector3 camOffset, heldAt;
    CharacterController playerBody;

    void StartTaking()
    {
        state = State.Taking;
        takeTimer = 0f;
        takeBeat = 0;
        takeEnded = false;
        headRoll = 0f;
        player.enabled = false;
        playerBody = player.GetComponent<CharacterController>();
        if (playerBody != null) playerBody.enabled = false; // it moves the player now (ResetToSpawn turns it back on)
        var cam = Camera.main;
        camOffset = cam != null ? cam.transform.position - player.transform.position : Vector3.up * 1.6f;
        heldAt = player.transform.position + camOffset;
        Vector3 to = player.transform.position - transform.position;
        to.y = 0f;
        if (to.sqrMagnitude > 0.001f) transform.rotation = Quaternion.LookRotation(to);
        // No scream: a lunge, a heavy foot planted, and its hand closing round you.
        Footfall(1.3f);
        PlaySound(ProceduralAudio.TwigSnap, 0.55f, 1f);
    }

    /// <summary>
    /// It snatches you up in one long hand and lifts you to its face. Then it does nothing: it holds you
    /// there, breathing, head slowly tilting as it listens to you, a few quiet clicks. It goes still. Its
    /// mouth begins to open as it draws you in, and it cuts to black.
    /// </summary>
    void UpdateTaking(float dt)
    {
        takeTimer += dt;
        float t = takeTimer;
        var cam = Camera.main != null ? Camera.main.transform : null;

        if (TakeBeat(0, 0.9f)) PlaySound(ProceduralAudio.Breath, 0.5f, 1f);          // it breathes you in
        if (TakeBeat(1, 1.9f)) for (int i = 0; i < 3; i++) Invoke(nameof(Click), i * 0.14f);
        if (TakeBeat(2, 3.3f)) PlaySound(ProceduralAudio.Creep, 0.45f, 0.9f);        // the mouth parting
        if (TakeBeat(3, 4.1f) && !takeEnded)
        {
            takeEnded = true;
            GameOverScreen.Show(0.15f); // a hard cut, not a fade
        }

        // Leaning in over you to bring its face down to yours.
        float k = 1f - Mathf.Exp(-5f * dt);
        body.localRotation = Quaternion.Slerp(body.localRotation, Quaternion.Euler(38f, 0f, 0f), k);
        body.localPosition = Vector3.Lerp(body.localPosition, Vector3.zero, k);

        // The head: listening. It tilts slowly over, twitches once or twice, then goes completely still.
        if (t < 2.7f)
        {
            headRoll = Mathf.MoveTowards(headRoll, 28f, dt * 14f);
            if (Random.value < dt * 0.8f) headRoll += Random.Range(-6f, 6f);
        }
        head.localRotation = Quaternion.Slerp(head.localRotation, Quaternion.Euler(-12f, 0f, headRoll), 1f - Mathf.Exp(-8f * dt));

        // One arm out holding you, the other hanging.
        for (int i = 0; i < 2; i++)
        {
            Quaternion goal = i == 1 ? Quaternion.Euler(-70f, 0f, -12f) : Quaternion.Euler(8f, 0f, -6f);
            arms[i].localRotation = Quaternion.Slerp(arms[i].localRotation, goal, 1f - Mathf.Exp(-12f * dt));
            legs[i].localRotation = Quaternion.Slerp(legs[i].localRotation, Quaternion.identity, k);
        }

        // The mouth splits slowly open at the end.
        float open = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(3.2f, 4.1f, t));
        mouth.localScale = new Vector3(Mathf.Lerp(1f, 1.5f, open), Mathf.Lerp(0.25f, 1.4f, open), 1f);
        mouth.gameObject.SetActive(true);

        // You: snatched up off the ground to hang in front of its face, then drawn slowly closer at the end.
        float faceGap = Mathf.Lerp(1.3f * scale / 2.7f, 0.45f * scale / 2.7f, open);
        Vector3 hold = head.position + transform.forward * faceGap - Vector3.up * 0.15f * scale;
        float snatch = t < 0.6f ? 1f - Mathf.Exp(-9f * dt) : 1f - Mathf.Exp(-3f * dt);
        heldAt = Vector3.Lerp(heldAt, hold, snatch);
        // Struggling in its grip: a slow, heavy sway, not a jitter.
        float struggle = Mathf.Lerp(0.05f, 0.015f, Mathf.InverseLerp(0.5f, 3f, t));
        Vector3 sway = new Vector3(Mathf.PerlinNoise(t * 1.3f, 0.1f) - 0.5f, Mathf.PerlinNoise(0.4f, t * 1.1f) - 0.5f, 0f) * struggle * scale;
        player.transform.position = heldAt - camOffset + transform.rotation * sway;

        if (cam != null)
        {
            // Your head is pulled round to face it, slowly, and you hang a little askew in its hand.
            Quaternion look = Quaternion.LookRotation(head.position + Vector3.up * 0.1f * scale - cam.position);
            look *= Quaternion.Euler(0f, 0f, -9f + (Mathf.PerlinNoise(t * 0.7f, 5f) - 0.5f) * 6f);
            cam.rotation = Quaternion.Slerp(cam.rotation, look, 1f - Mathf.Exp((t < 0.6f ? -6f : -2.5f) * dt));
        }
    }

    bool TakeBeat(int index, float at)
    {
        if (takeBeat != index || takeTimer < at) return false;
        takeBeat++;
        return true;
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
        // Standing tall, leaning in a little; further forward when it rushes.
        body.localRotation = Quaternion.Slerp(body.localRotation, Quaternion.Euler(8f + 16f * rushing, 0f, 0f), 1f - Mathf.Exp(-3f * dt));
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
            // A ball of a hip joint, so the leg grows out of the pelvis however it swings.
            Part(legs[i], black, PrimitiveType.Sphere, Vector3.zero, Vector3.one * 0.16f * bulk * s, Quaternion.identity);
            Bone(legs[i], black, Vector3.zero, new Vector3(side * 0.05f, -0.5f, 0.18f) * s, 0.09f * s);
            Bone(legs[i], black, new Vector3(side * 0.05f, -0.5f, 0.18f) * s, new Vector3(side * 0.03f, -1.02f, -0.05f) * s, 0.06f * s);
            for (int toe = -1; toe <= 1; toe++)
                Bone(legs[i], black, new Vector3(side * 0.03f, -1.02f, -0.05f) * s, new Vector3(side * 0.03f + toe * 0.05f, -1.05f, 0.12f) * s, 0.02f * s);

            // Arms long enough to drag their knuckles.
            arms[i] = Pivot(body, "Shoulder", new Vector3(side * 0.3f, 1.85f, 0.05f) * s);
            Part(arms[i], black, PrimitiveType.Sphere, Vector3.zero, Vector3.one * 0.12f * bulk * s, Quaternion.identity);
            Bone(arms[i], black, Vector3.zero, new Vector3(side * 0.1f, -0.75f, -0.1f) * s, 0.06f * s);
            Bone(arms[i], black, new Vector3(side * 0.1f, -0.75f, -0.1f) * s, new Vector3(side * 0.08f, -1.5f, 0.15f) * s, 0.045f * s);
            for (int f = 0; f < 4; f++)
                Bone(arms[i], black, new Vector3(side * 0.08f, -1.5f, 0.15f) * s, new Vector3(side * 0.08f + (f - 1.5f) * 0.04f, -1.82f, 0.25f) * s, 0.014f * s);
        }

        // A broad pelvis joining the legs to the spine.
        Part(body, black, PrimitiveType.Capsule, new Vector3(0f, 1.07f, -0.01f) * s, new Vector3(0.2f * bulk, 0.2f, 0.17f * bulk) * s, Quaternion.Euler(0f, 0f, 90f));
        // A heavy, hunched back.
        Bone(body, black, new Vector3(0f, 1.05f, 0f) * s, new Vector3(0f, 1.5f, -0.12f) * s, 0.12f * s);
        Bone(body, black, new Vector3(0f, 1.5f, -0.12f) * s, new Vector3(0f, 1.9f, 0.08f) * s, 0.12f * s);
        Part(body, black, PrimitiveType.Capsule, new Vector3(0f, 1.6f, -0.05f) * s, new Vector3(0.42f * 1.3f, 0.3f * 1.15f, 0.3f * 1.3f) * s, Quaternion.Euler(20f, 0f, 0f));
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
        thickness *= bulk;
        Part(parent, mat, PrimitiveType.Cylinder, (a + b) * 0.5f, new Vector3(thickness, d.magnitude * 0.5f, thickness), Quaternion.FromToRotation(Vector3.up, d));
        // A round joint at each end, so bones meet without gaps or flat cut ends.
        Part(parent, mat, PrimitiveType.Sphere, a, Vector3.one * thickness, Quaternion.identity);
        Part(parent, mat, PrimitiveType.Sphere, b, Vector3.one * thickness, Quaternion.identity);
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
                if (c.name == "Stump") continue;                                       // what's left of the trees it knocked down
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

    // It comes once a run: after that it's only ever banished or retreating and coming back quietly, never
    // a second entrance. Cleared when the run restarts (or the count drops back below lampsToSpawn).
    static bool spawnedThisRun;
    public static void ResetRun() => spawnedThisRun = false;

    void Start() => BlindEntityTheme.Prepare(); // its music takes a moment to make: start now, in the background

    void Update()
    {
        if (Lamp.TimesLit < lampsToSpawn) { spawnedThisRun = false; return; }
        if (spawnedThisRun || BlindEntity.All.Count > 0 || GameOverScreen.IsActive) return;
        var player = FindAnyObjectByType<FirstPersonController>();
        if (player == null || PortalDoor.PlayerInRoom) return;
        spawnedThisRun = true;
        Debug.Log($"[Horror] The blind one comes: lamps lit {Lamp.LitCount}, times lit {Lamp.TimesLit} (needs {lampsToSpawn}).");
        BlindEntity.Spawn(player.transform.position);
    }
}
