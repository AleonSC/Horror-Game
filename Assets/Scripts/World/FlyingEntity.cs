using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// The flier. Once seven different lamps have been lit, something enormous comes out of the sky: a vast,
/// ragged black bird, near forty metres across the wings, with a single eye that burns like a searchlight.
///
/// Its entrance: a cry from far off, then it comes down out of the clouds in a long dive straight over the
/// player, low enough to shake the ground and tear the leaves up, climbs away screaming, and lights its eye.
///
/// After that it circles high over the woods, sweeping the ground with its beam, drifting after the player
/// and toward any loud noise. Trees, buildings and lit safe areas hide you from it. Caught in the beam in
/// the open, the light turns red and locks on; get out of sight before it finishes locking, or it drops out
/// of the sky and carries you off. Lose it and it searches hard round where it last saw you.
/// The watchtower floodlights drive it away. FlyingEntitySpawner brings it in; a run reset removes it.
/// </summary>
public class FlyingEntity : MonoBehaviour
{
    public static readonly List<FlyingEntity> All = new List<FlyingEntity>();

    [Tooltip("Height it circles at above the ground.")]
    public float altitude = 48f;
    [Tooltip("Radius of the circles it flies round its search area.")]
    public float circleRadius = 38f;
    public float flySpeed = 15f;
    [Tooltip("How fast its search area drifts after the player.")]
    public float driftSpeed = 2.5f;
    [Tooltip("How far its beam sweeps round the search area.")]
    public float sweepRadius = 26f;
    [Tooltip("Half-angle of the beam (degrees).")]
    public float beamHalfAngle = 9f;
    public float beamIntensity = 6000f;
    public Color beamColor = new Color(0.9f, 0.92f, 0.8f);
    public Color lockColor = new Color(1f, 0.08f, 0.04f);
    [Tooltip("Seconds in the beam before it comes for you.")]
    public float lockTime = 2.6f;
    [Tooltip("How long it searches hard where it lost you.")]
    public float searchTime = 15f;
    [Range(0f, 1f)] public float volume = 1f;

    enum State { Entering, Searching, Locking, Diving, Carrying }
    State state = State.Entering;

    FirstPersonController player;
    DarkForestGenerator generator;
    Transform body, head, eye, neck;
    readonly Transform[] shoulders = new Transform[2], elbows = new Transform[2], legs = new Transform[2];
    Light beam;
    Transform beamCone;
    Material beamMaterial, eyeMaterial;
    AudioSource wings, voice, hum, near, whoosh;

    Vector3 center;          // the middle of where it's searching
    Vector3 aim;             // where the beam is pointed
    float orbitAngle, orbitDir = 1f;
    float timer, lockAmount, hardSearch, flapPhase, flapStrength, lastFlapSign, bank;
    Vector3 lastPosition;
    bool musicTaken;

    // Entrance flight path.
    Vector3 enterStart, enterLow, enterEnd;
    float entrance;
    int entranceBeat;

    // Diving and carrying.
    Vector3 diveStart, camOffset, carryDir;
    CharacterController playerBody;
    float carrySpeed;

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

    /// <summary>Brings it in, starting its entrance over the player.</summary>
    public static FlyingEntity Spawn(FirstPersonController player)
    {
        var e = new GameObject("Flying Entity").AddComponent<FlyingEntity>();
        e.player = player;
        e.BeginEntrance();
        return e;
    }

    void Awake()
    {
        generator = FindAnyObjectByType<DarkForestGenerator>();
        Build();
        wings = ProceduralAudio.AddSource(gameObject, null, false, true, volume, 25f, 400f);
        voice = ProceduralAudio.AddSource(gameObject, null, false, true, volume, 40f, 600f);
        hum = ProceduralAudio.AddSource(eye.gameObject, ProceduralAudio.LampHum, true, true, 0f, 10f, 120f);
        near = ProceduralAudio.AddSource(gameObject, null, false, false, volume); // felt everywhere
        whoosh = ProceduralAudio.AddSource(gameObject, null, false, false, volume);
        WorldWrap.RegisterDynamic(transform);
    }

    float Ground(Vector3 p) => generator != null ? generator.GetGroundHeight(p.x, p.z) : 0f;

    // ---------------------------------------------------------------- Entrance

    void BeginEntrance()
    {
        Vector3 you = player.transform.position;
        // From high up behind wherever the player is looking, so it comes over their head into view.
        Vector3 look = Camera.main != null ? Camera.main.transform.forward : player.transform.forward;
        look.y = 0f;
        look = look.sqrMagnitude > 0.01f ? look.normalized : Vector3.forward;
        Vector3 from = Quaternion.Euler(0f, Random.Range(-25f, 25f), 0f) * -look;
        enterStart = you + from * 320f + Vector3.up * 160f;
        enterLow = you + Vector3.up * 22f;
        enterEnd = you - from * 220f + Vector3.up * 70f;
        transform.position = enterStart;
        lastPosition = enterStart;
        state = State.Entering;
        entrance = 0f;
        entranceBeat = 0;
        SetBeam(false);
    }

    const float DiveTime = 7f, ClimbTime = 5f;

    void UpdateEntrance(float dt)
    {
        entrance += dt;
        float t = entrance;
        Vector3 you = player.transform.position;

        if (Beat(0, 0f))
        {
            // A cry from far off, everywhere at once.
            near.pitch = 0.42f;
            near.PlayOneShot(ProceduralAudio.Screech, 0.7f);
            if (MusicManager.Instance != null) { MusicManager.Instance.SetOverride(MusicManager.Mood.Danger); musicTaken = true; }
        }
        if (Beat(1, DiveTime - 1.8f))
        {
            // The rush of air as it comes down.
            near.pitch = 0.5f;
            near.PlayOneShot(ProceduralAudio.DistantRumble, 1f);
            whoosh.pitch = 0.35f;
            whoosh.PlayOneShot(ProceduralAudio.WingFlaps, 1f);
        }
        if (Beat(2, DiveTime - 0.15f))
        {
            // Right overhead: the ground shakes, the leaves tear up, and it screams.
            voice.pitch = 0.5f;
            voice.PlayOneShot(ProceduralAudio.Screech, 1f);
            LeafBurst.Spawn(new Bounds(you + Vector3.up * 1.5f, new Vector3(30f, 3f, 30f)));
        }
        if (Beat(3, DiveTime + ClimbTime - 1.2f))
        {
            voice.pitch = 0.38f;
            voice.PlayOneShot(ProceduralAudio.Scream, 0.9f);
        }

        // The path: a long dive down over the player, then a climb away.
        Vector3 p;
        if (t < DiveTime)
        {
            float u = Mathf.SmoothStep(0f, 1f, t / DiveTime);
            u = Mathf.Lerp(t / DiveTime, u, 0.5f);
            Vector3 control = Vector3.Lerp(enterStart, enterLow, 0.6f) + Vector3.up * 30f;
            p = Bezier(enterStart, control, enterLow, u);
            flapStrength = t > DiveTime - 2f ? 1f : 0.15f; // gliding down, then beating hard as it pulls up
        }
        else
        {
            float u = Mathf.Clamp01((t - DiveTime) / ClimbTime);
            Vector3 control = enterLow + (enterLow - enterStart).normalized * 60f;
            p = Bezier(enterLow, control, enterEnd, u);
            flapStrength = 1f;
        }
        transform.position = p;
        FaceAlong(dt, 4f);

        // How close it is decides how hard the ground shakes.
        float dist = Vector3.Distance(p, you);
        float shake = Mathf.Lerp(3f, 0f, Mathf.InverseLerp(25f, 140f, dist));
        if (shake > 0.02f) player.AddCameraShake(shake);

        if (t >= DiveTime + ClimbTime)
        {
            // Away up into the dark, then its eye opens.
            center = you;
            Vector3 off = transform.position - center;
            orbitAngle = Mathf.Atan2(off.z, off.x);
            aim = center;
            SetBeam(true);
            ProceduralAudio.PlayAt(ProceduralAudio.Floodlight, transform.position, 1f, 0.5f, 40f);
            if (musicTaken && MusicManager.Instance != null) MusicManager.Instance.ClearOverride();
            musicTaken = false;
            state = State.Searching;
        }
    }

    bool Beat(int index, float at)
    {
        if (entranceBeat != index || entrance < at) return false;
        entranceBeat++;
        return true;
    }

    static Vector3 Bezier(Vector3 a, Vector3 b, Vector3 c, float t)
    {
        float s = 1f - t;
        return s * s * a + 2f * s * t * b + t * t * c;
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
        Vector3 you = player.transform.position;

        switch (state)
        {
            case State.Entering:
                UpdateEntrance(dt);
                break;
            case State.Searching:
            case State.Locking:
                UpdateSearch(dt, you);
                break;
            case State.Diving:
                UpdateDive(dt);
                break;
            case State.Carrying:
                UpdateCarry(dt);
                break;
        }

        AnimateWings(dt);
        lastPosition = transform.position;
    }

    void UpdateSearch(float dt, Vector3 you)
    {
        center = WorldWrap.NearestImage(center, you); // wherever the map has looped round to
        hardSearch = Mathf.Max(0f, hardSearch - dt);

        // The search area drifts after the player (slowly, unless it's already seen them).
        if (state == State.Locking) center = Vector3.MoveTowards(center, you, driftSpeed * 4f * dt);
        else if (hardSearch <= 0f) center = Vector3.MoveTowards(center, you, driftSpeed * dt);

        // The watchtower's floodlights drive it off.
        foreach (var tower in Watchtower.All)
        {
            if (!tower.LightsOn) continue;
            Vector3 away = center - tower.transform.position;
            away.y = 0f;
            if (away.magnitude < tower.safeRadius * 2.5f)
                center += (away.sqrMagnitude > 0.01f ? away.normalized : Vector3.forward) * 20f * dt;
        }

        // Circling round the search area, tighter while it's searching hard.
        float radius = circleRadius * (hardSearch > 0f || state == State.Locking ? 0.7f : 1f);
        orbitAngle += orbitDir * flySpeed / radius * dt;
        Vector3 goal = center + new Vector3(Mathf.Cos(orbitAngle), 0f, Mathf.Sin(orbitAngle)) * radius;
        goal.y = Ground(goal) + altitude + Mathf.Sin(Time.time * 0.21f) * 4f;
        // Smoothly, but never faster than it can fly (coming in from far off after its entrance).
        Vector3 next = Vector3.Lerp(transform.position, goal, 1f - Mathf.Exp(-1.5f * dt));
        transform.position = Vector3.MoveTowards(transform.position, next, flySpeed * 2.5f * dt);
        FaceAlong(dt, 3f);

        // Flapping: long glides broken by a few slow, heavy beats.
        timer -= dt;
        if (timer <= 0f)
        {
            timer = Random.Range(5f, 10f);
            flapStrength = 1f;
        }
        flapStrength = Mathf.MoveTowards(flapStrength, 0f, dt * 0.35f);

        // The beam: sweeping the ground round the search area, or held on you.
        Vector3 target;
        if (state == State.Locking) target = you;
        else
        {
            float t = Time.time;
            float sweep = sweepRadius * (hardSearch > 0f ? 0.6f : 1f);
            target = center + new Vector3(Mathf.Sin(t * 0.37f) + 0.4f * Mathf.Sin(t * 1.1f), 0f, Mathf.Cos(t * 0.23f) + 0.4f * Mathf.Cos(t * 0.9f)) * sweep * 0.75f;
            target.y = Ground(target);
        }
        aim = Vector3.Lerp(aim, target, 1f - Mathf.Exp((state == State.Locking ? -6f : -1.4f) * dt));
        PointBeam();

        // Caught in the light?
        bool seen = InBeam(you);
        if (seen)
        {
            if (state != State.Locking)
            {
                state = State.Locking;
                voice.pitch = Random.Range(0.55f, 0.65f);
                voice.PlayOneShot(ProceduralAudio.Screech, 0.9f);
                if (MusicManager.Instance != null) { MusicManager.Instance.SetOverride(MusicManager.Mood.Danger); musicTaken = true; }
            }
            lockAmount = Mathf.MoveTowards(lockAmount, 1f, dt / lockTime);
            if (lockAmount >= 1f) StartDive();
        }
        else
        {
            lockAmount = Mathf.MoveTowards(lockAmount, 0f, dt / (lockTime * 0.6f));
            if (state == State.Locking && lockAmount <= 0f)
            {
                // Lost you: it searches hard round where it last saw you.
                state = State.Searching;
                hardSearch = searchTime;
                center = you;
                if (musicTaken && MusicManager.Instance != null) MusicManager.Instance.ClearOverride();
                musicTaken = false;
            }
        }
        Color c = Color.Lerp(beamColor, lockColor, Mathf.SmoothStep(0f, 1f, lockAmount));
        beam.color = c;
        beamMaterial.SetColor("_Color", new Color(c.r, c.g, c.b, 0.1f + 0.08f * lockAmount));
        MaterialFactory.SetColor(eyeMaterial, c * 6f);
        hum.volume = Mathf.MoveTowards(hum.volume, 0.6f * volume, dt);
        hum.pitch = 0.7f + 0.3f * lockAmount;
    }

    /// <summary>
    /// True if the player stands in the beam where it can see them: inside its cone, with nothing between
    /// (trees, roofs, walls all hide you), and not inside a lit safe area.
    /// </summary>
    bool InBeam(Vector3 you)
    {
        if (PortalDoor.PlayerInRoom || GameOverScreen.IsActive || SafeZones.Contains(you)) return false;
        Vector3 chest = you + Vector3.up * 1.2f;
        Vector3 to = chest - eye.position;
        if (to.magnitude > beam.range) return false;
        if (Vector3.Angle(to, aim - eye.position) > beamHalfAngle * 0.9f) return false;
        // Anything in the way? (Ignore the player's own collider.)
        var hits = Physics.RaycastAll(eye.position, to.normalized, to.magnitude - 0.6f, ~0, QueryTriggerInteraction.Ignore);
        foreach (var h in hits)
            if (h.collider.GetComponentInParent<FirstPersonController>() == null) return false;
        return true;
    }

    void OnNoise(Vector3 at, float radius)
    {
        if (state != State.Searching) return;
        // Only loud noises carry up to it: running, the flare gun, crashing trees.
        if (radius < 12f) return;
        Vector3 d = at - transform.position;
        d.y = 0f;
        if (d.magnitude > radius * 4f) return;
        center = at;
        hardSearch = searchTime * 0.6f;
    }

    /// <summary>Driven off (the watchtower floodlights): it loses any lock and its search moves well away.</summary>
    public void Repel(Vector3 from)
    {
        if (state != State.Searching && state != State.Locking) return;
        state = State.Searching;
        lockAmount = 0f;
        hardSearch = 0f;
        Vector3 away = center - from;
        away.y = 0f;
        center = from + (away.sqrMagnitude > 0.01f ? away.normalized : Vector3.forward) * 140f;
        voice.pitch = 0.45f;
        voice.PlayOneShot(ProceduralAudio.Screech, 0.7f);
    }

    // ---------------------------------------------------------------- Taking the player

    void StartDive()
    {
        state = State.Diving;
        timer = 0f;
        diveStart = transform.position;
        player.enabled = false;
        playerBody = player.GetComponent<CharacterController>();
        if (playerBody != null) playerBody.enabled = false; // it moves the player now (ResetToSpawn turns it back on)
        var cam = Camera.main;
        camOffset = cam != null ? cam.transform.position - player.transform.position : Vector3.up * 1.6f;
        voice.pitch = 0.5f;
        voice.PlayOneShot(ProceduralAudio.Scream, 1f);
        whoosh.pitch = 0.4f;
        whoosh.PlayOneShot(ProceduralAudio.WingFlaps, 1f);
    }

    const float DiveDuration = 1.8f;

    void UpdateDive(float dt)
    {
        timer += dt;
        float u = Mathf.Clamp01(timer / DiveDuration);
        Vector3 you = player.transform.position;
        // Wings folded, it drops straight at you, talons first.
        Vector3 grabAt = you + Vector3.up * 5.5f;
        Vector3 control = new Vector3(grabAt.x, diveStart.y, grabAt.z) + (diveStart - grabAt).normalized * 10f;
        transform.position = Bezier(diveStart, control, grabAt, u * u);
        Vector3 to = grabAt - transform.position;
        to.y = 0f;
        if (to.sqrMagnitude > 0.01f) transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(to), 1f - Mathf.Exp(-5f * dt));
        flapStrength = 0f;
        aim = you;
        PointBeam();
        player.AddCameraShake(Mathf.Lerp(0.5f, 3f, u));

        var cam = Camera.main;
        if (cam != null)
        {
            // You look up and see it coming.
            Quaternion look = Quaternion.LookRotation(head.position - cam.transform.position);
            cam.transform.rotation = Quaternion.Slerp(cam.transform.rotation, look, 1f - Mathf.Exp(-4f * dt));
        }

        if (u >= 1f)
        {
            state = State.Carrying;
            timer = 0f;
            carrySpeed = 4f;
            carryDir = transform.forward;
            carryDir.y = 0f;
            carryDir = carryDir.sqrMagnitude > 0.01f ? carryDir.normalized : Vector3.forward;
            SetBeam(false);
            near.pitch = 0.5f;
            near.PlayOneShot(ProceduralAudio.TreeImpact, 1f); // the impact of being hit
            GameOverScreen.Show(4f);
        }
    }

    void UpdateCarry(float dt)
    {
        timer += dt;
        // Up and away into the dark, beating hard, the ground falling away under you.
        carrySpeed = Mathf.MoveTowards(carrySpeed, 30f, dt * 10f);
        transform.position += (carryDir * 0.8f + Vector3.up * 0.6f).normalized * carrySpeed * dt;
        transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(carryDir) * Quaternion.Euler(-20f, 0f, 0f), 1f - Mathf.Exp(-3f * dt));
        flapStrength = 1f;

        // Held in its talons.
        Vector3 talons = (legs[0].TransformPoint(0f, -3.8f, 1f) + legs[1].TransformPoint(0f, -3.8f, 1f)) * 0.5f;
        player.transform.position = talons + Vector3.down * 0.5f - camOffset; // your eyes just under its grip
        var cam = Camera.main;
        if (cam != null)
        {
            Quaternion look = Quaternion.LookRotation(carryDir + Vector3.down * 1.4f);
            look *= Quaternion.Euler(Mathf.Sin(timer * 2.3f) * 4f, Mathf.Sin(timer * 1.7f) * 6f, Mathf.Sin(timer * 1.3f) * 8f);
            cam.transform.rotation = Quaternion.Slerp(cam.transform.rotation, look, 1f - Mathf.Exp(-2f * dt));
        }
        player.AddCameraShake(0.8f);
    }

    // ---------------------------------------------------------------- Body

    void FaceAlong(float dt, float rate)
    {
        Vector3 v = (transform.position - lastPosition) / Mathf.Max(dt, 0.0001f);
        if (v.sqrMagnitude < 0.5f) return;
        Vector3 flat = new Vector3(v.x, 0f, v.z);
        Quaternion heading = flat.sqrMagnitude > 0.01f ? Quaternion.LookRotation(flat) : transform.rotation;
        // Bank into the turn.
        float turn = Vector3.SignedAngle(transform.forward, flat, Vector3.up);
        bank = Mathf.Lerp(bank, Mathf.Clamp(-turn * 6f, -35f, 35f), 1f - Mathf.Exp(-2f * dt));
        float climb = Mathf.Clamp(-Mathf.Atan2(v.y, flat.magnitude) * Mathf.Rad2Deg, -35f, 35f);
        Quaternion goal = heading * Quaternion.Euler(climb, 0f, bank);
        transform.rotation = Quaternion.Slerp(transform.rotation, goal, 1f - Mathf.Exp(-rate * dt));
    }

    void AnimateWings(float dt)
    {
        bool folded = state == State.Diving;
        flapPhase += dt * Mathf.Lerp(0.6f, 1.6f, flapStrength) * Mathf.PI * 2f * 0.35f;
        float s = Mathf.Sin(flapPhase);
        float amp = folded ? 0f : Mathf.Lerp(4f, 38f, flapStrength);
        for (int i = 0; i < 2; i++)
        {
            float side = i == 0 ? -1f : 1f;
            float fold = folded ? 1f : 0f;
            // Wings swept back and tucked in when diving; beating up and down otherwise.
            shoulders[i].localRotation = Quaternion.Slerp(shoulders[i].localRotation,
                Quaternion.Euler(0f, side * Mathf.Lerp(-6f, -55f, fold), side * (s * amp + 6f)), 1f - Mathf.Exp(-8f * dt));
            float lag = Mathf.Sin(flapPhase - 0.9f);
            elbows[i].localRotation = Quaternion.Slerp(elbows[i].localRotation,
                Quaternion.Euler(0f, side * Mathf.Lerp(-8f, -70f, fold), side * (lag * amp * 0.7f - 4f)), 1f - Mathf.Exp(-8f * dt));
            // Legs trail, or reach forward for you.
            legs[i].localRotation = Quaternion.Slerp(legs[i].localRotation,
                Quaternion.Euler(state == State.Diving || state == State.Carrying ? -40f : 55f, 0f, 0f), 1f - Mathf.Exp(-4f * dt));
        }
        body.localPosition = Vector3.up * -s * amp * 0.03f;

        // A deep whump on each downstroke, when it's beating hard enough to hear.
        float sign = Mathf.Sign(s);
        if (sign < 0f && lastFlapSign > 0f && amp > 15f)
        {
            wings.pitch = Random.Range(0.28f, 0.34f);
            wings.PlayOneShot(ProceduralAudio.WingFlaps, volume * Mathf.InverseLerp(15f, 38f, amp));
        }
        lastFlapSign = sign;

        // The head turns to look along its beam.
        if (beam != null && beam.enabled)
        {
            Vector3 look = aim - head.position;
            if (look.sqrMagnitude > 0.01f)
                head.rotation = Quaternion.Slerp(head.rotation, Quaternion.LookRotation(look, transform.up), 1f - Mathf.Exp(-4f * dt));
        }
        else head.localRotation = Quaternion.Slerp(head.localRotation, Quaternion.Euler(10f, 0f, 0f), 1f - Mathf.Exp(-4f * dt));
    }

    void PointBeam()
    {
        if (beam == null) return;
        Vector3 to = aim - eye.position;
        if (to.sqrMagnitude < 0.01f) return;
        beam.transform.SetPositionAndRotation(eye.position, Quaternion.LookRotation(to));
        float length = to.magnitude * 1.08f;
        float radius = length * Mathf.Tan(beamHalfAngle * Mathf.Deg2Rad);
        beamCone.localScale = new Vector3(radius, radius, length);
    }

    void SetBeam(bool on)
    {
        if (beam == null) return;
        beam.enabled = on;
        beamCone.gameObject.SetActive(on);
        MaterialFactory.SetColor(eyeMaterial, on ? beamColor * 6f : new Color(0.25f, 0.02f, 0.01f));
        if (hum != null && on && !hum.isPlaying) hum.Play();
        if (hum != null && !on) hum.Stop();
    }

    /// <summary>
    /// The bird, about 38m across the wings: a heavy body, a long crooked neck, a narrow head with a long
    /// hooked beak and one great eye, ragged wings of separate long feathers, a fanned tail, and legs with
    /// hooked talons folded back under it. All black, a silhouette against the sky.
    /// </summary>
    void Build()
    {
        var black = MaterialFactory.Unlit(new Color(0.01f, 0.01f, 0.012f));
        body = Pivot(transform, "Body", Vector3.zero);

        Part(body, black, PrimitiveType.Sphere, new Vector3(0f, 0f, 0f), new Vector3(3.4f, 2.6f, 9f), Vector3.zero);
        Part(body, black, PrimitiveType.Sphere, new Vector3(0f, 0.4f, 2.6f), new Vector3(3.6f, 2.8f, 4.5f), Vector3.zero); // chest

        // The neck: crooked, reaching forward and a little down.
        neck = Pivot(body, "Neck", new Vector3(0f, 0.4f, 4.2f));
        Bone(neck, black, Vector3.zero, new Vector3(0f, -0.4f, 2.6f), 1.3f);
        Bone(neck, black, new Vector3(0f, -0.4f, 2.6f), new Vector3(0f, -0.2f, 5f), 1f);
        head = Pivot(neck, "Head", new Vector3(0f, -0.2f, 5.2f));
        Part(head, black, PrimitiveType.Sphere, new Vector3(0f, 0f, 0.4f), new Vector3(1.6f, 1.5f, 2.8f), Vector3.zero);
        // The beak: long, narrow, hooked down at the tip.
        Bone(head, black, new Vector3(0f, -0.1f, 1.5f), new Vector3(0f, -0.4f, 4.6f), 0.5f);
        Bone(head, black, new Vector3(0f, -0.4f, 4.6f), new Vector3(0f, -1.1f, 5.1f), 0.25f);
        // One great eye under the brow, looking down.
        eyeMaterial = MaterialFactory.NoFog(beamColor * 6f);
        eye = Part(head, eyeMaterial, PrimitiveType.Sphere, new Vector3(0f, -0.5f, 1.1f), new Vector3(0.9f, 0.6f, 0.9f), Vector3.zero);

        // Wings: an arm and a hand, with long, separate flight feathers along the trailing edge and splayed
        // like fingers at the tip.
        for (int i = 0; i < 2; i++)
        {
            float side = i == 0 ? -1f : 1f;
            shoulders[i] = Pivot(body, "Shoulder", new Vector3(side * 1.4f, 0.8f, 1.6f));
            Part(shoulders[i], black, PrimitiveType.Cube, new Vector3(side * 4.2f, 0f, -0.6f), new Vector3(8.4f, 0.45f, 3.6f), Vector3.zero);
            for (int f = 0; f < 6; f++) // secondaries, hanging back off the arm
                Part(shoulders[i], black, PrimitiveType.Cube, new Vector3(side * (1.2f + f * 1.25f), -0.05f, -3.4f + Random.Range(-0.3f, 0.3f)),
                    new Vector3(1.1f, 0.12f, 3.6f + Random.Range(-0.6f, 0.6f)), new Vector3(0f, side * Random.Range(-6f, 6f), 0f));

            elbows[i] = Pivot(shoulders[i], "Elbow", new Vector3(side * 8.2f, 0f, -0.4f));
            Part(elbows[i], black, PrimitiveType.Cube, new Vector3(side * 3f, 0f, -0.3f), new Vector3(6.2f, 0.35f, 2.6f), new Vector3(0f, side * 8f, 0f));
            for (int f = 0; f < 7; f++)
            {
                // Primaries: long fingers fanning out from the tip, ragged and uneven.
                float a = Mathf.Lerp(-5f, 70f, f / 6f);
                float len = Random.Range(6f, 8.5f) * (1f - f * 0.06f);
                var feather = Pivot(elbows[i], "Primary", new Vector3(side * 5.8f, 0f, -0.6f - f * 0.2f));
                feather.localRotation = Quaternion.Euler(0f, side * a, 0f);
                Part(feather, black, PrimitiveType.Cube, new Vector3(side * len * 0.5f, 0f, 0f), new Vector3(len, 0.1f, Random.Range(0.6f, 0.9f)),
                    new Vector3(0f, 0f, side * Random.Range(-3f, 3f)));
            }
        }

        // The tail: a ragged fan.
        for (int f = 0; f < 7; f++)
        {
            float a = Mathf.Lerp(-30f, 30f, f / 6f);
            var feather = Pivot(body, "Tail", new Vector3(0f, 0.2f, -4f));
            feather.localRotation = Quaternion.Euler(0f, a, 0f);
            Part(feather, black, PrimitiveType.Cube, new Vector3(0f, 0f, -3f), new Vector3(0.9f, 0.1f, Random.Range(5f, 6.5f)), Vector3.zero);
        }

        // Legs and talons.
        for (int i = 0; i < 2; i++)
        {
            float side = i == 0 ? -1f : 1f;
            legs[i] = Pivot(body, "Leg", new Vector3(side * 0.9f, -1f, -1f));
            Bone(legs[i], black, Vector3.zero, new Vector3(0f, -3f, 0f), 0.45f);
            for (int t = -1; t <= 1; t++)
            {
                Vector3 knuckle = new Vector3(t * 0.3f, -3f, 0f);
                Bone(legs[i], black, knuckle, knuckle + new Vector3(t * 0.4f, -0.6f, 1.1f), 0.18f);
                Bone(legs[i], black, knuckle + new Vector3(t * 0.4f, -0.6f, 1.1f), knuckle + new Vector3(t * 0.5f, -1.3f, 1.2f), 0.12f);
            }
        }

        // The searchlight from its eye, and the visible beam through the air.
        beam = new GameObject("Eye Beam").AddComponent<Light>();
        beam.transform.SetParent(transform, false);
        beam.type = LightType.Spot;
        beam.spotAngle = beamHalfAngle * 2f;
        beam.innerSpotAngle = beamHalfAngle * 1.2f;
        beam.range = 160f;
        beam.intensity = beamIntensity;
        beam.color = beamColor;
        beam.shadows = LightShadows.Soft; // trees throw long shadows across the pool of light as it sweeps

        Shader beamShader = Shader.Find("Horror/Beam");
        beamMaterial = beamShader != null ? new Material(beamShader) : MaterialFactory.NoFog(beamColor * 0.1f, 3000, false);
        beamMaterial.SetColor("_Color", new Color(beamColor.r, beamColor.g, beamColor.b, 0.1f));
        var cone = new GameObject("Beam Cone");
        cone.transform.SetParent(beam.transform, false);
        cone.AddComponent<MeshFilter>().sharedMesh = WatcherEvent.BuildBeamMesh();
        var r = cone.AddComponent<MeshRenderer>();
        r.sharedMaterial = beamMaterial;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        r.receiveShadows = false;
        beamCone = cone.transform;
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
        var t = Part(parent, mat, PrimitiveType.Capsule, (a + b) * 0.5f, new Vector3(thickness, d.magnitude * 0.5f + thickness * 0.5f, thickness), Vector3.zero);
        t.localRotation = Quaternion.FromToRotation(Vector3.up, d);
    }

    Transform Part(Transform parent, Material mat, PrimitiveType type, Vector3 pos, Vector3 scale, Vector3 euler)
    {
        var go = GameObject.CreatePrimitive(type);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = pos;
        go.transform.localRotation = Quaternion.Euler(euler);
        go.transform.localScale = scale;
        var renderer = go.GetComponent<Renderer>();
        renderer.sharedMaterial = mat;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; // its shadow would be everywhere at once
        Destroy(go.GetComponent<Collider>());
        return go.transform;
    }
}

/// <summary>Brings in the flier the first time seven different lamps have been lit in a run. DarkForestGenerator adds this.</summary>
public class FlyingEntitySpawner : MonoBehaviour
{
    [Tooltip("Different lamps lit before the flier comes.")]
    public int lampsToSpawn = 7;

    void Update()
    {
        if (Lamp.LitCount < lampsToSpawn || FlyingEntity.All.Count > 0 || GameOverScreen.IsActive) return;
        var player = FindAnyObjectByType<FirstPersonController>();
        if (player == null || PortalDoor.PlayerInRoom) return;
        FlyingEntity.Spawn(player);
    }
}
