using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// The flier. Once lamps have been lit seven times, something enormous comes out of the sky: a vast,
/// ragged black bird, near forty metres across the wings, with a single eye that burns like a searchlight.
///
/// Its entrance: a cry from far off, then it comes down out of the clouds in a long dive straight over the
/// player, low enough to shake the ground and tear the leaves up, climbs away screaming, and lights its eye.
///
/// After that it scouts the map: circling over one stretch of forest after another, sweeping the ground
/// with its beam. For the first while (avoidTime) its beam deliberately keeps off the player. It never
/// goes looking for you, but if its beam happens to pass close by, it snaps onto you and holds there, and
/// keeps the light on you until you get out of sight (trees, roofs and lit safe areas hide you). Standing
/// still in it is safe. Move in it for alarmMoveTime seconds in all and it screeches: the light turns red,
/// and every scour comes in at once and begins a fresh hunt. It never takes you itself.
/// The watchtower floodlights drive it away. FlyingEntitySpawner brings it in; a run reset removes it.
/// </summary>
public class FlyingEntity : MonoBehaviour
{
    public static readonly List<FlyingEntity> All = new List<FlyingEntity>();

    // Static state is cleared when Play starts, so nothing carries over from the last session if the
    // editor's Enter Play Mode settings skip reloading scripts (no domain reload).
    /// <summary>Clears this class's static state (StaticReset calls it when Play starts).</summary>
    internal static void ResetStatics() => All.Clear();

    [Tooltip("Height it circles at above the ground.")]
    public float altitude = 48f;
    [Tooltip("It patrols within this distance of the player (wherever they go): its circles never take it further out.")]
    public float patrolRadius = 100f;
    [Tooltip("Radius of the circles it flies round its search area.")]
    public float circleRadius = 34f;
    public float flySpeed = 12f;
    [Tooltip("How fast its search area moves across the map as it scouts.")]
    public float driftSpeed = 4f;
    [Tooltip("Seconds after it arrives that its beam deliberately keeps off the player.")]
    public float avoidTime = 15f;
    [Tooltip("How far it keeps its beam from the player while avoiding them.")]
    public float avoidDistance = 25f;
    [Tooltip("If its sweeping beam passes this close to the player (and can see them), it snaps onto them.")]
    public float snapDistance = 9f;
    [Tooltip("Seconds of moving in its light, in all, before it screeches and raises the alarm.")]
    public float alarmMoveTime = 5f;
    [Tooltip("Seconds out of its sight before it loses you and goes back to scouting.")]
    public float loseTime = 4f;
    [Tooltip("How far its beam sweeps round the search area.")]
    public float sweepRadius = 26f;
    [Tooltip("Half-angle of the beam (degrees).")]
    public float beamHalfAngle = 9f;
    public float beamIntensity = 6000f;
    public Color beamColor = new Color(0.9f, 0.92f, 0.8f);
    public Color lockColor = new Color(1f, 0.08f, 0.04f);
    [Tooltip("How fast the beam surveys the ground (lower is slower).")]
    public float surveySpeed = 0.35f;
    [Tooltip("Moving faster than this in the beam gives you away (m/s).")]
    public float movementThreshold = 0.3f;
    [Range(0f, 1f)] public float volume = 1f;

    enum State { Entering, Searching, Locking, Banished }
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
    float orbitAngle, orbitDir = 1f, surveyTime;
    float timer, flapPhase, flapStrength, lastFlapSign, bank;
    Vector3 lastPosition;
    bool musicTaken;

    // Entrance flight path.
    Vector3 enterStart, enterLow, enterEnd;
    float entrance;
    int entranceBeat;

    // Scouting and tracking.
    Vector3 scoutGoal;
    float avoidTimer, scoutTimer, movedInLight, hiddenFor, alarm;
    bool alarmed;

    void OnEnable()
    {
        All.Add(this);
    }

    void OnDisable()
    {
        All.Remove(this);
        if (musicTaken && MusicManager.Instance != null) MusicManager.Instance.ClearOverride();
        musicTaken = false;
        FlierTheme.FadeOut();
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
        Tuning.Apply(this); // saved tuning (Save Tuning on the component's menu), before anything uses it
        generator = FindAnyObjectByType<DarkForestGenerator>();
        Build();
        wings = ProceduralAudio.AddSource(gameObject, null, false, true, volume, 25f, 400f);
        voice = ProceduralAudio.AddSource(gameObject, null, false, true, volume, 40f, 600f);
        hum = ProceduralAudio.AddSource(eye.gameObject, ProceduralAudio.LampHum, true, true, 0f, 10f, 120f);
        near = ProceduralAudio.AddSource(gameObject, null, false, false, volume); // felt everywhere
        whoosh = ProceduralAudio.AddSource(gameObject, null, false, false, volume);
        // Not registered with WorldWrap: its flight is hundreds of metres across, more than half the looping
        // map, so folding it to the copy nearest the player would throw it about. It follows the player's
        // own jumps across the seams instead (FollowWrap).
    }

    Vector3 lastPlayerPos;
    bool trackingPlayer;

    /// <summary>
    /// When the player crosses the edge of the looping map they're moved to the other side; everything about
    /// the flier (where it is, its flight path, where it's searching) moves with them, so nothing jumps.
    /// </summary>
    void FollowWrap(Vector3 you)
    {
        Vector3 jump = you - lastPlayerPos;
        jump.y = 0f;
        if (trackingPlayer && jump.magnitude > 60f)
        {
            transform.position += jump;
            lastPosition += jump;
            enterStart += jump; enterLow += jump; enterEnd += jump;
            center += jump; aim += jump; scoutGoal += jump;
        }
        lastPlayerPos = you;
        trackingPlayer = true;
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
            near.PlayOneShot(ProceduralAudio.Screech, 0.25f);
            FlierTheme.Play(); // its own music, scored to all this
            player.DrawAttention(() => transform.position, 11f, 0.5f); // following it in as it comes
        }
        if (Beat(1, DiveTime - 1.8f))
        {
            // The rush of air as it comes down.
            near.pitch = 0.5f;
            near.PlayOneShot(ProceduralAudio.DistantRumble, 0.4f);
            whoosh.pitch = 0.35f;
            whoosh.PlayOneShot(ProceduralAudio.WingFlaps, 0.35f);
        }
        if (Beat(2, DiveTime - 0.15f))
        {
            // Right overhead: the ground shakes, the leaves tear up, and it screams.
            voice.pitch = 0.5f;
            voice.PlayOneShot(ProceduralAudio.Screech, 0.5f);
            LeafBurst.Spawn(new Bounds(you + Vector3.up * 1.5f, new Vector3(30f, 3f, 30f)));
        }
        if (Beat(3, DiveTime + ClimbTime - 1.2f))
        {
            voice.pitch = 0.38f;
            voice.PlayOneShot(ProceduralAudio.Scream, 0.4f);
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
        float shake = Mathf.Lerp(1.8f, 0f, Mathf.InverseLerp(25f, 140f, dist));
        if (shake > 0.02f) player.AddCameraShake(shake);

        if (t >= DiveTime + ClimbTime)
        {
            // Away up into the dark, then its eye opens.
            center = you;
            Vector3 off = transform.position - center;
            orbitAngle = Mathf.Atan2(off.z, off.x);
            aim = center;
            SetBeam(true);
            ProceduralAudio.PlayAt(ProceduralAudio.Floodlight, transform.position, 0.5f, 0.5f, 40f);
            if (musicTaken && MusicManager.Instance != null) MusicManager.Instance.ClearOverride();
            musicTaken = false;
            state = State.Searching;
            avoidTimer = avoidTime;
            NewScoutGoal(you);
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
        FollowWrap(you);

        switch (state)
        {
            case State.Entering:
                UpdateEntrance(dt);
                break;
            case State.Searching:
            case State.Locking:
                UpdateSearch(dt, you);
                break;
            case State.Banished:
                UpdateBanished(dt, you);
                break;
        }

        AnimateWings(dt);
        lastPosition = transform.position;
    }

    /// <summary>How far from the player the middle of its circles may go, so the circles stay within patrolRadius.</summary>
    float PatrolReach => Mathf.Max(10f, patrolRadius - circleRadius);

    /// <summary>
    /// Somewhere else to scout: another stretch of forest round the player (within its patrol), a good way
    /// from where it's been searching. It never heads straight for them.
    /// </summary>
    void NewScoutGoal(Vector3 you)
    {
        Vector3 best = you;
        float bestGap = -1f;
        for (int i = 0; i < 6; i++)
        {
            Vector2 d = Random.insideUnitCircle * PatrolReach;
            Vector3 p = you + new Vector3(d.x, 0f, d.y);
            float gap = Vector3.Distance(new Vector3(p.x, 0f, p.z), new Vector3(center.x, 0f, center.z));
            if (gap > bestGap) { bestGap = gap; best = p; }
        }
        scoutGoal = best;
        scoutTimer = Random.Range(25f, 45f);
    }

    void UpdateSearch(float dt, Vector3 you)
    {
        bool tracking = state == State.Locking;
        avoidTimer = Mathf.Max(0f, avoidTimer - dt);

        if (tracking)
        {
            // Holding you in its light: it circles right over you.
            center = Vector3.MoveTowards(center, you, driftSpeed * 3f * dt);
        }
        else
        {
            // Scouting: working its way across the map, one stretch of forest at a time. It never heads for you.
            scoutTimer -= dt;
            Vector3 toGoal = scoutGoal - center;
            toGoal.y = 0f;
            if (scoutTimer <= 0f || toGoal.magnitude < 5f) NewScoutGoal(you);
            center = Vector3.MoveTowards(center, scoutGoal, driftSpeed * dt);
            // While it's avoiding you, its whole search keeps its distance too.
            if (avoidTimer > 0f)
            {
                Vector3 fromYou = center - you;
                fromYou.y = 0f;
                float keep = avoidDistance + sweepRadius;
                if (fromYou.magnitude < keep)
                    center = you + (fromYou.sqrMagnitude > 0.01f ? fromYou.normalized : Vector3.forward) * Mathf.MoveTowards(fromYou.magnitude, keep, driftSpeed * 2f * dt);
            }
        }

        // However the player moves, its patrol stays round them.
        if (!tracking)
        {
            Vector3 fromYou = center - you;
            fromYou.y = 0f;
            if (fromYou.magnitude > PatrolReach)
                center = you + fromYou.normalized * Mathf.MoveTowards(fromYou.magnitude, PatrolReach, driftSpeed * 4f * dt);
            Vector3 goalFromYou = scoutGoal - you;
            goalFromYou.y = 0f;
            if (goalFromYou.magnitude > PatrolReach) NewScoutGoal(you);
        }

        // The watchtower's floodlights drive it off.
        foreach (var tower in Watchtower.All)
        {
            if (!tower.LightsOn) continue;
            Vector3 away = center - tower.transform.position;
            away.y = 0f;
            if (away.magnitude < tower.safeRadius * 2.5f)
                center += (away.sqrMagnitude > 0.01f ? away.normalized : Vector3.forward) * 20f * dt;
        }

        // Circling round its search area, tighter while it holds you.
        float radius = circleRadius * (tracking ? 0.6f : 1f);
        orbitAngle += orbitDir * flySpeed / radius * dt;
        Vector3 goal = center + new Vector3(Mathf.Cos(orbitAngle), 0f, Mathf.Sin(orbitAngle)) * radius;
        goal.y = Ground(goal) + altitude + Mathf.Sin(Time.time * 0.21f) * 4f;
        // Smoothly, but never faster than it can fly.
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

        // The beam: slowly sweeping the ground round its search area, or held on you.
        surveyTime += dt * surveySpeed;
        Vector3 target;
        if (tracking) target = you;
        else
        {
            float t = surveyTime;
            target = center + new Vector3(Mathf.Sin(t * 0.37f) + 0.35f * Mathf.Sin(t * 1.3f), 0f, Mathf.Cos(t * 0.23f) + 0.35f * Mathf.Cos(t * 1.1f)) * sweepRadius * 0.75f;
            if (avoidTimer > 0f)
            {
                // Deliberately steering round you.
                Vector3 off = target - you;
                off.y = 0f;
                if (off.magnitude < avoidDistance) target = you + (off.sqrMagnitude > 0.01f ? off.normalized : Vector3.forward) * avoidDistance;
            }
            target.y = Ground(target);
        }
        aim = Vector3.Lerp(aim, target, 1f - Mathf.Exp((tracking ? -4f : -0.8f) * dt));
        PointBeam();

        bool inBeam = InBeam(you);
        if (!tracking)
        {
            // It happens to sweep close by you, and sees you: the beam snaps onto you.
            Vector3 fromAim = aim - you;
            fromAim.y = 0f;
            if (avoidTimer <= 0f && fromAim.magnitude < snapDistance && Sees(you))
            {
                state = State.Locking;
                hiddenFor = 0f;
                ProceduralAudio.PlayAt(ProceduralAudio.LampClick, eye.position, 1f, 0.5f, 60f); // a heavy clunk as it fixes on you
            }
        }
        else
        {
            // Out of its sight long enough, and it loses you.
            hiddenFor = Sees(you) ? 0f : hiddenFor + dt;
            if (hiddenFor >= loseTime)
            {
                state = State.Searching;
                alarmed = false;
                movedInLight = 0f;
                avoidTimer = Mathf.Max(avoidTimer, 8f); // gives you a moment before it could find you again
                NewScoutGoal(you);
                if (musicTaken && MusicManager.Instance != null) MusicManager.Instance.ClearOverride();
                musicTaken = false;
            }
            // Moving in its light gives you away; standing still doesn't.
            else if (!alarmed && inBeam && player.CurrentSpeed > movementThreshold)
            {
                movedInLight += dt;
                if (movedInLight >= alarmMoveTime) RaiseAlarm();
            }
        }

        alarm = Mathf.MoveTowards(alarm, alarmed ? 1f : 0f, dt * 2f);
        Color c = Color.Lerp(beamColor, lockColor, Mathf.SmoothStep(0f, 1f, alarm));
        beam.color = c;
        beamMaterial.SetColor("_Color", new Color(c.r, c.g, c.b, 0.1f + 0.08f * alarm));
        MaterialFactory.SetColor(eyeMaterial, c * 6f);
        hum.volume = Mathf.MoveTowards(hum.volume, 0.6f * volume, dt);
        hum.pitch = 0.7f + (tracking ? 0.1f : 0f) + 0.3f * alarm;
    }

    /// <summary>The alarm: a screech, the light turns red, and every scour comes in to hunt afresh.</summary>
    void RaiseAlarm()
    {
        alarmed = true;
        voice.pitch = Random.Range(0.5f, 0.6f);
        voice.PlayOneShot(ProceduralAudio.Screech, 1f);
        near.pitch = 0.45f;
        near.PlayOneShot(ProceduralAudio.Screech, 0.35f);
        if (MusicManager.Instance != null) { MusicManager.Instance.SetOverride(MusicManager.Mood.Danger); musicTaken = true; }
        foreach (var scour in new List<ShadowEntity>(ShadowEntity.All)) scour.Rouse();
    }

    /// <summary>True if it can see the player from where it is: nothing in the way, not in a lit safe area.</summary>
    bool Sees(Vector3 you)
    {
        if (PortalDoor.PlayerInRoom || GameOverScreen.IsActive || SafeZones.Contains(you)) return false;
        Vector3 chest = you + Vector3.up * 1.2f;
        Vector3 to = chest - eye.position;
        if (to.magnitude > beam.range) return false;
        var hits = Physics.RaycastAll(eye.position, to.normalized, to.magnitude - 0.6f, ~0, QueryTriggerInteraction.Ignore);
        foreach (var h in hits)
            if (h.collider.GetComponentInParent<FirstPersonController>() == null) return false;
        return true;
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

    /// <summary>True while it's in the sky where it can be seen (and flashed).</summary>
    public bool IsPresent => state == State.Searching || state == State.Locking || state == State.Entering;

    /// <summary>
    /// Caught in the camera's flash: its eye goes out, it screams and beats away up into the dark, and stays
    /// away for a while before coming back to search again.
    /// </summary>
    public void Banish(float seconds)
    {
        if (state != State.Searching && state != State.Locking) return;
        state = State.Banished;
        banishTimer = seconds;
        alarmed = false;
        movedInLight = 0f;
        SetBeam(false);
        voice.pitch = 0.6f;
        voice.PlayOneShot(ProceduralAudio.Screech, 0.7f);
        flapStrength = 1f;
        if (musicTaken && MusicManager.Instance != null) MusicManager.Instance.ClearOverride();
        musicTaken = false;
        Vector3 away = transform.position - player.transform.position;
        away.y = 0f;
        banishDir = away.sqrMagnitude > 0.01f ? away.normalized : transform.forward;
    }

    float banishTimer;
    Vector3 banishDir;

    void UpdateBanished(float dt, Vector3 you)
    {
        banishTimer -= dt;
        if (banishTimer > 0f)
        {
            // Away and up, beating hard, until it's lost in the dark.
            transform.position += (banishDir * flySpeed + Vector3.up * 4f) * dt;
            flapStrength = Mathf.Max(flapStrength, 0.6f);
            FaceAlong(dt, 3f);
            return;
        }
        // Back to scouting (it flies back in from wherever it got to), its eye lit again, and it keeps off
        // you for a while, as when it first came.
        state = State.Searching;
        center = transform.position;
        aim = center;
        avoidTimer = avoidTime;
        NewScoutGoal(you);
        SetBeam(true);
    }

    /// <summary>Driven off (the watchtower floodlights): it loses any lock and its search moves well away.</summary>
    public void Repel(Vector3 from)
    {
        if (state != State.Searching && state != State.Locking) return;
        state = State.Searching;
        alarmed = false;
        movedInLight = 0f;
        avoidTimer = Mathf.Max(avoidTimer, 10f);
        if (musicTaken && MusicManager.Instance != null) MusicManager.Instance.ClearOverride();
        musicTaken = false;
        Vector3 away = center - from;
        away.y = 0f;
        center = from + (away.sqrMagnitude > 0.01f ? away.normalized : Vector3.forward) * 140f;
        voice.pitch = 0.45f;
        voice.PlayOneShot(ProceduralAudio.Screech, 0.7f);
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
        flapPhase += dt * Mathf.Lerp(0.6f, 1.6f, flapStrength) * Mathf.PI * 2f * 0.35f;
        float s = Mathf.Sin(flapPhase);
        float amp = Mathf.Lerp(4f, 38f, flapStrength);
        for (int i = 0; i < 2; i++)
        {
            float side = i == 0 ? -1f : 1f;
            // Wings swept back and tucked in when diving; beating up and down otherwise.
            shoulders[i].localRotation = Quaternion.Slerp(shoulders[i].localRotation,
                Quaternion.Euler(0f, side * -6f, side * (s * amp + 6f)), 1f - Mathf.Exp(-8f * dt));
            float lag = Mathf.Sin(flapPhase - 0.9f);
            elbows[i].localRotation = Quaternion.Slerp(elbows[i].localRotation,
                Quaternion.Euler(0f, side * -8f, side * (lag * amp * 0.7f - 4f)), 1f - Mathf.Exp(-8f * dt));
            // Legs trail behind.
            legs[i].localRotation = Quaternion.Slerp(legs[i].localRotation,
                Quaternion.Euler(55f, 0f, 0f), 1f - Mathf.Exp(-4f * dt));
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
        // Fog-free, so it stays a crisp black shape against the sky at any distance instead of melting into it.
        var black = MaterialFactory.NoFog(new Color(0.008f, 0.008f, 0.01f));
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

/// <summary>
/// Brings in the flier once lamps have been lit seven times in a run (the count at the top right of the screen,
/// the same one the blind one and the eye go by). DarkForestGenerator adds this.
/// </summary>
public class FlyingEntitySpawner : MonoBehaviour
{
    void Awake() => Tuning.Apply(this); // saved tuning (Inspector changes made in Play are kept)

    [Tooltip("Times lamps have been lit (the counter at the top right) before the flier comes.")]
    public int lampsToSpawn = 7;

    void Start() => FlierTheme.Prepare(); // its music takes a moment to make: start now, in the background

    void Update()
    {
        if (Lamp.TimesLit < lampsToSpawn || FlyingEntity.All.Count > 0 || GameOverScreen.IsActive) return;
        var player = FindAnyObjectByType<FirstPersonController>();
        if (player == null || PortalDoor.PlayerInRoom) return;
        FlyingEntity.Spawn(player);
    }
}
