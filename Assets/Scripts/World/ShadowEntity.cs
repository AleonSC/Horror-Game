using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// A tall pitch-black silhouette that materializes in the distance and watches the player.
///
/// - While the player isn't looking, it creeps a few metres closer every few seconds.
///   If a creep would bring it within kill distance, it takes the player (game over).
/// - If the player approaches while shining the flashlight on it, it vibrates and its eyes widen
///   while the player slows down until they can't get any closer. Then it builds to a crescendo,
///   "poofs" and rematerializes far away with blazing eyes. That's the only way to push it back.
/// - If the player comes near without the flashlight on it, it slowly grows and advances on them,
///   with a pounding audio cue. If it reaches them, they die.
/// - While the player is inside a lit lamp's light (or holding a lit torch or lighter) it can't hurt them or enter the light.
///   Instead, whenever it isn't being watched, it closes in and circles just outside the light.
///
/// The body is unlit black, so the flashlight doesn't light it: it shows as a solid shadow against
/// lit trees and ground. Eyes use HDR colors, so they glow if the camera has Bloom.
/// DarkForestGenerator spawns the first one; every lit lamp spawns another.
/// </summary>
public class ShadowEntity : MonoBehaviour
{
    public static readonly List<ShadowEntity> All = new List<ShadowEntity>();

    /// <summary>Seconds before the first entity of a run appears.</summary>
    public const float FirstEntityDelay = 60f;

    public static ShadowEntity Spawn() => new GameObject("Shadow Entity").AddComponent<ShadowEntity>();

    /// <summary>Spawns an entity that stays hidden for the given number of seconds before it first appears.</summary>
    public static ShadowEntity Spawn(float firstAppearDelay)
    {
        var entity = Spawn();
        entity.initialHideTime = firstAppearDelay;
        return entity;
    }

    [Tooltip("Who to watch. Found automatically if left empty.")]
    public Transform target;

    [Header("Spawning")]
    [Tooltip("Seconds before it first appears.")]
    public float initialHideTime = 5f;
    public float minSpawnDistance = 22f;
    public float maxSpawnDistance = 32f;
    [Tooltip("Spawns within this many degrees of where the player is facing, so it's likely to be seen.")]
    public float spawnViewAngle = 70f;
    public float minRespawnDelay = 10f;
    public float maxRespawnDelay = 18f;
    [Tooltip("If the player gets this far away, it silently vanishes and rematerializes closer.")]
    public float relocateDistance = 50f;

    [Header("Approach")]
    [Tooltip("Within this distance it starts vibrating and the player starts slowing down.")]
    public float reactDistance = 14f;
    [Tooltip("The player can't get closer than this. Reaching it triggers the crescendo.")]
    public float stopDistance = 2.5f;
    [Tooltip("The crescendo starts once the player is within this much of the stop distance.")]
    public float triggerMargin = 0.4f;

    [Header("Creeping")]
    [Tooltip("Seconds between attempts to creep closer.")]
    public float minCreepInterval = 4f;
    public float maxCreepInterval = 8f;
    [Tooltip("Metres it moves per creep.")]
    public float creepStep = 2.5f;
    [Tooltip("It only creeps when it's further than this many degrees from the centre of the player's view (or hidden behind something).")]
    public float lookAngle = 55f;
    [Tooltip("If a creep would bring it this close, it takes the player.")]
    public float killDistance = 4f;

    [Header("Flashlight")]
    [Tooltip("Extra degrees beyond the edge of the flashlight beam that still count as shining on it.")]
    public float beamMargin = 5f;

    [Header("Advancing (no flashlight on it)")]
    [Tooltip("Metres per second it closes in when the player is near without the flashlight on it.")]
    public float advanceSpeed = 0.5f;
    [Tooltip("How fast it grows while advancing (scale per second).")]
    public float growRate = 0.03f;
    public float maxGrowth = 1.8f;
    [Tooltip("If it gets this close while advancing, it takes the player.")]
    public float touchDistance = 1.5f;

    [Header("Provoking It")]
    [Tooltip("Clicking the flashlight on and off at it this many times...")]
    public int flashesToProvoke = 4;
    [Tooltip("...within this many seconds makes it charge.")]
    public float flashWindow = 2.2f;
    [Tooltip("Metres per second it charges at you.")]
    public float chargeSpeed = 22f;
    [Tooltip("It stops this far from your face, and vanishes.")]
    public float chargeStopDistance = 1.2f;
    [Tooltip("After a charge it's back this quickly...")]
    public float minChargeRespawn = 3f;
    public float maxChargeRespawn = 6f;
    [Tooltip("...and hunts harder: each charge makes it creep sooner and further, spawn closer and advance faster (up to 4 times).")]
    public int maxAggression = 4;
    public float chargeVolume = 0.6f;

    [Header("Extra Arms")]
    [Tooltip("Chance it has extra arms growing from lower down its torso.")]
    [Range(0f, 1f)] public float extraArmsChance = 0.25f;
    [Tooltip("Most extra pairs it can have.")]
    public int maxExtraArmPairs = 2;

    [Header("Poses")]
    [Tooltip("Chance it appears peeking out from behind a tree trunk instead of standing in the open.")]
    [Range(0f, 1f)] public float peekChance = 0.35f;

    [Header("Watchtower")]
    [Tooltip("While the player is up the watchtower, it rushes to the tower's base at this speed when not looked at directly.")]
    public float poolSpeed = 3f;
    [Tooltip("Only looking within this many degrees of it counts as looking 'directly' (freezes it at the tower).")]
    public float directLookAngle = 25f;

    [Header("Circling Lamps")]
    [Tooltip("How far outside a lamp's light it circles.")]
    public float ringMargin = 1.5f;
    public float circleSpeed = 1.2f;

    [Header("Vibration")]
    public float maxVibration = 0.04f;
    public float crescendoVibration = 0.25f;

    [Header("Eyes")]
    public Color eyeColor = new Color(1f, 0.08f, 0.05f);
    [Tooltip("Normal eye brightness (HDR multiplier).")]
    public float eyeIntensity = 1f;
    [Tooltip("Eye brightness right after it materializes.")]
    public float spawnEyeIntensity = 12f;
    [Tooltip("Eye brightness at the very start of a hunt, while it's still far away.")]
    public float startHuntEyeIntensity = 0.25f;
    [Tooltip("Eye brightness when the hunt is nearly over (it's almost on the player).")]
    public float endHuntEyeIntensity = 5f;
    [Tooltip("Seconds for the eyes to dim from spawn brightness to normal.")]
    public float eyeDimTime = 6f;
    [Tooltip("How much wider the eyes get when the player is at the stop distance.")]
    public float maxEyeWiden = 1.6f;
    [Tooltip("Strength of the red light the eyes cast on the surroundings.")]
    public float eyeLightStrength = 0.3f;
    [Tooltip("Eyeshine: how bright the eyes flare when the flashlight beam crosses them, like an animal's in headlights.")]
    public float eyeshineBrightness = 9f;
    public Color eyeshineColor = new Color(1f, 0.55f, 0.35f);
    [Tooltip("Facing it down: over the crescendo its eyes burn from red to this...")]
    public Color dismissColor = new Color(0.25f, 0.55f, 1f);
    [Tooltip("...and the glow around them swells this many times stronger, and wider, as they do.")]
    public float dismissGlow = 6f;

    [Header("Mouth")]
    [Tooltip("The mouth starts to split open within this distance...")]
    public float mouthOpenDistance = 6f;
    [Tooltip("...and is fully open at this distance.")]
    public float mouthFullDistance = 2.8f;

    [Header("Crescendo & Poof")]
    public float crescendoTime = 2.5f;
    [Tooltip("Each lamp the player has lit makes the crescendo this much faster (0.15 = 15% of the base speed per lamp).")]
    public float crescendoSpeedupPerLamp = 0.15f;
    [Tooltip("However many lamps are lit, the crescendo never gets shorter than this.")]
    public float minCrescendoTime = 0.8f;
    [Tooltip("Degrees of camera shake at the peak of the crescendo.")]
    public float maxCameraShake = 2.5f;
    public int fragmentCount = 40;
    public float poofTime = 1.2f;
    public float materializeTime = 1.2f;

    [Header("Catching The Player")]
    [Tooltip("Seconds of jumpscare before the screen fades out.")]
    public float killTime = 1.6f;

    [Header("Look")]
    public float height = 2.4f;
    [Tooltip("How thick its limbs and body are (1 = rail thin).")]
    public float bulk = 1.3f;

    [Header("Audio")]
    [Range(0f, 1f)] public float volume = 0.9f;
    [Range(0f, 1f), Tooltip("Volume of the shriek as it builds up before poofing (relative to Volume).")]
    public float crescendoVolume = 0.35f;
    [Range(0f, 1f), Tooltip("Volume of the scream when it catches the player (relative to Volume).")]
    public float killVolume = 0.4f;
    [Tooltip("Leave clips empty to use the built-in synthesized sounds.")]
    public AudioClip whisperLoop;
    public AudioClip materializeSound;
    public AudioClip creepSound;
    public AudioClip crescendoSound;
    public AudioClip poofSound;
    public AudioClip killSound;
    public AudioClip advanceLoop;

    public enum State { Hidden, Materializing, Watching, Crescendo, Killing, Charging }
    public State CurrentState { get; private set; }
    /// <summary>
    /// How far its hunt has got, 0 to 1: how close it has worked its way to the player, and how much it has grown
    /// stalking them in the dark. Only goes up; starts over when it's driven off. Its eyes brighten with it.
    /// </summary>
    public float HuntProgress { get; private set; }

    public bool IsVisible => CurrentState == State.Watching || CurrentState == State.Crescendo || CurrentState == State.Killing
        || CurrentState == State.Charging;

    /// <summary>How riled up it is: goes up each time frantic flashing makes it charge. Never calms down this run.</summary>
    public int Aggression { get; private set; }

    FirstPersonController player;
    DarkForestGenerator generator;
    AudioSource sfx, whisper, advanceSource;
    Flashlight flashlight;
    float growth = 1f;
    float circleDirection = 1f;
    float stepSoundTimer;
    float poolAngle = -1f;

    Transform visual, posture;
    Renderer[] bodyRenderers;
    Transform leftEye, rightEye;
    Transform headPivot, leftArm, rightArm;
    readonly List<Transform> fingers = new List<Transform>();
    readonly List<Transform> extraArms = new List<Transform>();
    readonly List<Quaternion> extraArmRest = new List<Quaternion>();
    readonly List<Quaternion> fingerRest = new List<Quaternion>();
    Quaternion headTarget = Quaternion.identity;
    float twitchTimer;
    Vector3 eyeBaseScale;
    Material eyeMat;
    Light eyeLight;
    float scale; // height / 2.4, proportions below are for a 2.4m figure

    /// <summary>
    /// Body poses. Every time it creeps closer unseen, it's in a new one when you look back: head lolled
    /// sideways, an arm reaching for you, hunched low, turned away with its head twisted round to face you...
    /// </summary>
    enum Pose { Neutral, HeadTilt, Reach, Crouch, TurnedAway, ArmsUp, HangingHead, Peek, Lunge }
    static readonly Pose[] CreepPoses = { Pose.Neutral, Pose.HeadTilt, Pose.Reach, Pose.Crouch, Pose.TurnedAway, Pose.ArmsUp, Pose.HangingHead };
    Pose pose;
    Quaternion poseHead = Quaternion.identity, poseLeftArm = Quaternion.identity, poseRightArm = Quaternion.identity;
    bool peeking; // half hidden behind a trunk
    float eyeshine;
    float eyeBrightness;   // as shown, eyeshine included
    float crescendoStartBrightness, crescendoStartShine, crescendoStartLight;
    int seenToggles = -1;
    float lastLitTime = -10f;
    readonly Queue<float> flashTimes = new Queue<float>();
    Transform mouth;
    float mouthOpen, mouthTarget;

    float timer;
    float creepTimer;
    float eyeFlare; // 1 right after spawning, fades to 0
    bool gameOverStarted;

    // Fragments used for the poof and materialize effects.
    Transform fragmentRoot;
    Transform[] fragments;
    Vector3[] fragStart, fragEnd, fragVelocity;
    float[] fragAge, fragLife, fragSize;
    bool fragmentsConverge;
    bool fragmentsActive;

    void Awake()
    {
        scale = height / 2.4f;
        BuildSilhouette();
        BuildFragments();

        sfx = ProceduralAudio.AddSource(gameObject, null, false, true, volume, 6f, 80f);
        whisper = ProceduralAudio.AddSource(gameObject, whisperLoop != null ? whisperLoop : ProceduralAudio.Whisper, true, true, 0f, 2f, 30f);
        whisper.pitch = Random.Range(0.85f, 1.1f); // so several entities don't sound identical
        advanceSource = ProceduralAudio.AddSource(gameObject, advanceLoop != null ? advanceLoop : ProceduralAudio.Advance, true, true, 0f, 8f, 60f);
        advanceSource.spatialBlend = 0.6f; // mostly 3D, but always clearly audible
        circleDirection = Random.value < 0.5f ? -1f : 1f;
    }

    void Start()
    {
        generator = FindAnyObjectByType<DarkForestGenerator>();
        WorldWrap.RegisterDynamic(transform);
        WorldWrap.RegisterDynamic(fragmentRoot);
        Hide(initialHideTime);
    }

    void OnEnable() => All.Add(this);

    void OnDisable()
    {
        All.Remove(this);
        ReleasePlayer();
    }

    void OnDestroy()
    {
        if (fragmentRoot != null) Destroy(fragmentRoot.gameObject);
    }

    void Update()
    {
        if (target == null)
        {
            player = FindAnyObjectByType<FirstPersonController>();
            if (player == null) return;
            target = player.transform;
        }
        else if (player == null)
        {
            player = target.GetComponent<FirstPersonController>();
        }

        UpdateFragments();
        if (IsVisible) UpdateTwitch(Time.deltaTime);

        switch (CurrentState)
        {
            case State.Hidden:
                timer -= Time.deltaTime;
                if (timer <= 0f) StartMaterialize();
                break;

            case State.Materializing:
                timer -= Time.deltaTime;
                if (timer <= 0f) FinishMaterialize();
                break;

            case State.Watching:
                UpdateWatching();
                break;

            case State.Crescendo:
                UpdateCrescendo();
                break;

            case State.Killing:
                UpdateKilling();
                break;

            case State.Charging:
                UpdateCharging();
                break;
        }

        // The mouth splits open slowly, and snaps shut when it's gone.
        if (CurrentState == State.Hidden || CurrentState == State.Materializing) mouthTarget = mouthOpen = 0f;
        mouthOpen = Mathf.MoveTowards(mouthOpen, mouthTarget, Time.deltaTime * 2.5f);
        mouth.localScale = new Vector3(1f, Mathf.Max(mouthOpen, 0.001f), 1f);
        bool showMouth = mouthOpen > 0.01f;
        if (mouth.gameObject.activeSelf != showMouth) mouth.gameObject.SetActive(showMouth);
    }

    // ---------------------------------------------------------------- States

    void Hide(float duration)
    {
        HuntProgress = 0f;
        CurrentState = State.Hidden;
        timer = duration;
        SetVisible(false);
        whisper.Stop();
        advanceSource.Stop();
        SetGrowth(1f);
        ReleasePlayer();
        peeking = false;
        SetPose(Pose.Neutral, 1);
    }

    void StartMaterialize()
    {
        if (GameOverScreen.IsActive) { timer = 1f; return; }

        int peekSide = Random.value < 0.5f ? -1 : 1;
        Vector3 peekSpot = Vector3.zero;
        peeking = Random.value < peekChance && TryPickPeekSpot(out peekSpot);
        transform.position = peeking ? peekSpot : PickSpawnPoint();
        FacePlayer();
        ResetVisual();
        SetPose(peeking ? Pose.Peek : RandomPose(), peekSide);

        CurrentState = State.Materializing;
        timer = materializeTime;
        StartFragments(converge: true);
        PlaySound(materializeSound != null ? materializeSound : ProceduralAudio.Materialize);
    }

    void FinishMaterialize()
    {
        SetVisible(true);
        eyeFlare = 1f;
        creepTimer = NextCreepDelay();
        whisper.volume = 0f;
        whisper.Play();
        advanceSource.volume = 0f;
        advanceSource.Play();
        CurrentState = State.Watching;
    }

    void UpdateWatching()
    {
        FacePlayer();

        float dist = HorizontalDistanceToTarget();
        if (dist > relocateDistance)
        {
            Hide(Random.Range(minRespawnDelay, maxRespawnDelay));
            return;
        }

        // A lamp was lit right next to it: it's driven off.
        if (SafeZones.Contains(transform.position))
        {
            Poof();
            return;
        }

        eyeFlare = Mathf.MoveTowards(eyeFlare, 0f, Time.deltaTime / Mathf.Max(eyeDimTime, 0.01f));

        // 0 when the player is far away, 1 when they're at the stop distance.
        float closeness = Mathf.Clamp01(Mathf.InverseLerp(reactDistance, stopDistance, dist));

        bool playerSafe = SafeZones.Contains(target.position, out SafeZones.ISource safeLight);
        Watchtower tower = Watchtower.TowerPlayerIsOn(target.position, player);
        bool advancing = false;

        if (tower != null)
        {
            // Up the tower (or on its ladder) the player can't be caught. It gathers at the foot of the tower
            // instead, freezing only while stared at directly.
            ReleasePlayer();
            if (!IsPlayerLooking(directLookAngle)) PoolAtTower(tower);
        }
        else if (playerSafe)
        {
            // Can't touch them in the light. Close in and prowl around its edge while unwatched.
            ReleasePlayer();
            if (!IsPlayerLooking()) CircleLight(safeLight);
        }
        else if (IsLitByFlashlight())
        {
            // Facing it down with the light: the player slows, and reaching it sets off the crescendo.
            if (player != null) player.SetApproachLimit(this, transform.position, reactDistance, stopDistance);
            if (dist <= stopDistance + triggerMargin)
            {
                StartCrescendo();
                return;
            }
        }
        else
        {
            // Near it in the dark: it grows and comes for them.
            ReleasePlayer();
            if (dist < reactDistance)
            {
                advancing = true;
                if (dist <= touchDistance)
                {
                    StartKill();
                    return;
                }
                Vector3 toPlayer = target.position - transform.position;
                toPlayer.y = 0f;
                Vector3 next = transform.position + toPlayer.normalized * (advanceSpeed * (1f + 0.3f * Aggression) * Time.deltaTime);
                next.y = GroundHeight(next);
                transform.position = next;
            }
        }

        SetGrowth(advancing
            ? Mathf.Min(maxGrowth, growth + growRate * Time.deltaTime)
            : Mathf.MoveTowards(growth, 1f, growRate * 2f * Time.deltaTime));

        Vibrate(maxVibration * Mathf.Max(closeness, advancing ? 0.6f : 0f));
        // Hunt progress: how close it has got (from the far edge of where it spawns, to the distance it kills from),
        // or how much it has grown. Rises gradually, never falls.
        float distanceProgress = Mathf.InverseLerp(maxSpawnDistance, killDistance, dist);
        float growthProgress = Mathf.InverseLerp(1f, maxGrowth, growth);
        float targetProgress = Mathf.Max(distanceProgress, growthProgress);
        if (targetProgress > HuntProgress)
            HuntProgress = Mathf.MoveTowards(HuntProgress, targetProgress, Time.deltaTime * 0.4f);

        // Eyes: dim at the start of the hunt, brighter and brighter as it goes on. The flash when it appears is
        // scaled the same way, so a far-off entity only glints.
        float huntBrightness = Mathf.Lerp(startHuntEyeIntensity, endHuntEyeIntensity, HuntProgress * HuntProgress);
        float flashBrightness = spawnEyeIntensity * Mathf.Lerp(0.1f, 1f, HuntProgress);
        float brightness = Mathf.Lerp(huntBrightness, Mathf.Max(huntBrightness, flashBrightness), eyeFlare);
        // Eyeshine: the beam crossing its eyes makes them flare back at you.
        bool eyesLit = EyesInBeam();
        eyeshine = Mathf.MoveTowards(eyeshine, eyesLit ? 1f : 0f, Time.deltaTime * 6f);
        if (WatchForFlashing(eyesLit)) return;
        SetEyes(Mathf.Lerp(1f, maxEyeWiden, closeness), Mathf.Max(brightness, eyeshine * eyeshineBrightness), eyeshine);
        eyeLight.intensity = brightness * eyeLightStrength; // a reflection: it doesn't light up the trees

        // The last few metres: a too-wide mouth splits open.
        mouthTarget = Mathf.Clamp01(Mathf.InverseLerp(mouthOpenDistance, mouthFullDistance, dist));
        whisper.volume = Mathf.Lerp(0.25f, 1f, closeness) * volume;

        float advanceTarget = advancing ? Mathf.Lerp(0.5f, 1f, closeness) * volume : 0f;
        advanceSource.volume = Mathf.MoveTowards(advanceSource.volume, advanceTarget, Time.deltaTime * 1.5f);
        advanceSource.pitch = Mathf.Lerp(0.9f, 1.3f, Mathf.InverseLerp(1f, maxGrowth, growth));

        if (!playerSafe && tower == null)
        {
            creepTimer -= Time.deltaTime;
            if (creepTimer <= 0f) TryCreep();
        }
    }

    void CircleLight(SafeZones.ISource light)
    {
        Vector3 center = light.Center;
        float ring = light.Radius + ringMargin;

        Vector3 offset = transform.position - center;
        offset.y = 0f;
        float r = offset.magnitude;
        float angle = r > 0.01f ? Mathf.Atan2(offset.x, offset.z) : Random.Range(0f, Mathf.PI * 2f);

        // Head in to the edge of the light, then walk around it.
        if (r > ring + 0.2f)
            r = Mathf.MoveTowards(r, ring, circleSpeed * Time.deltaTime);
        else
        {
            r = ring;
            angle += circleDirection * circleSpeed * Time.deltaTime / ring;
        }

        Vector3 next = center + new Vector3(Mathf.Sin(angle), 0f, Mathf.Cos(angle)) * r;
        next.y = GroundHeight(next);
        transform.position = next;

        stepSoundTimer -= Time.deltaTime;
        if (stepSoundTimer <= 0f)
        {
            stepSoundTimer = Random.Range(1.5f, 3.5f);
            PlaySound(creepSound != null ? creepSound : ProceduralAudio.Creep);
        }
    }

    /// <summary>Rushes to its own spot in a ring around the foot of the tower.</summary>
    void PoolAtTower(Watchtower tower)
    {
        if (poolAngle < 0f) poolAngle = Random.Range(0f, Mathf.PI * 2f);
        float r = 3.5f + (poolAngle % 1f) * 2.5f; // 3.5 - 6 m out, different for each entity
        Vector3 spot = tower.transform.position + new Vector3(Mathf.Sin(poolAngle), 0f, Mathf.Cos(poolAngle)) * r;

        Vector3 to = spot - transform.position;
        to.y = 0f;
        if (to.magnitude < 0.1f) return;
        Vector3 next = transform.position + Vector3.ClampMagnitude(to, poolSpeed * Time.deltaTime);
        next.y = GroundHeight(next);
        transform.position = next;

        stepSoundTimer -= Time.deltaTime;
        if (stepSoundTimer <= 0f)
        {
            stepSoundTimer = Random.Range(0.8f, 2f);
            PlaySound(creepSound != null ? creepSound : ProceduralAudio.Creep);
        }
    }

    /// <summary>Called when the player grabs a watchtower ladder: if it's waiting to appear, it comes now.</summary>
    public void Summon()
    {
        if (CurrentState == State.Hidden) timer = Mathf.Min(timer, Random.Range(0.5f, 2.5f));
    }

    void StartCrescendo()
    {
        CurrentState = State.Crescendo;
        timer = 0f;
        // Carry on from exactly how the eyes looked a moment ago (brightness, eyeshine tint, glow), no jump.
        crescendoStartBrightness = Mathf.Max(eyeBrightness, eyeIntensity);
        crescendoStartShine = eyeshine;
        crescendoStartLight = eyeLight.intensity / Mathf.Max(eyeLightStrength, 0.0001f);
        // The sound speeds up to match a shortened crescendo.
        PlaySound(crescendoSound != null ? crescendoSound : ProceduralAudio.Crescendo, crescendoTime / CrescendoDuration, crescendoVolume);
    }

    /// <summary>How long the crescendo takes now: every lamp the player has lit makes them quicker at dismissing it.</summary>
    float CrescendoDuration =>
        Mathf.Max(minCrescendoTime, crescendoTime / (1f + crescendoSpeedupPerLamp * Lamp.TimesLit));

    /// <summary>
    /// Counts clicks of the flashlight while it's aimed at this entity (switched on onto it, or off just after
    /// being on it). Enough of them in quick succession and it charges. Returns true if it just did.
    /// </summary>
    bool WatchForFlashing(bool eyesLit)
    {
        if (flashlight == null) return false;
        bool aimedAt = eyesLit || IsLitByFlashlight() || (flashlight.IsOn && InBeam(transform.position + Vector3.up * scale, 10f, peeking));
        if (aimedAt) lastLitTime = Time.time;

        if (seenToggles < 0) seenToggles = flashlight.ToggleCount;
        while (seenToggles < flashlight.ToggleCount)
        {
            seenToggles++;
            // On: it has to be in the beam now. Off: it had to be in the beam a moment ago.
            bool counts = flashlight.IsOn ? aimedAt : Time.time - lastLitTime < 0.4f;
            if (counts) flashTimes.Enqueue(Time.time);
        }
        while (flashTimes.Count > 0 && Time.time - flashTimes.Peek() > flashWindow) flashTimes.Dequeue();

        if (flashTimes.Count < flashesToProvoke) return false;
        flashTimes.Clear();
        StartCharge();
        return true;
    }

    /// <summary>Provoked: a shriek, and it rushes straight at the player's face.</summary>
    void StartCharge()
    {
        CurrentState = State.Charging;
        timer = 0f;
        ReleasePlayer();
        peeking = false;
        SetPose(Pose.Lunge, 1);
        ResetVisual();
        mouthTarget = 1.6f;
        advanceSource.Stop();
        PlaySound(ProceduralAudio.Screech, 1.3f, chargeVolume);
    }

    void UpdateCharging()
    {
        timer += Time.deltaTime;
        FacePlayer();
        Vibrate(crescendoVibration);
        SetEyes(maxEyeWiden * 1.8f, spawnEyeIntensity * 1.5f);
        mouthTarget = 1.6f;

        Vector3 toPlayer = target.position - transform.position;
        toPlayer.y = 0f;
        float dist = toPlayer.magnitude;
        if (player != null) player.AddCameraShake(Mathf.Lerp(0.3f, maxCameraShake * 1.5f, Mathf.InverseLerp(15f, chargeStopDistance, dist)));

        if (dist <= chargeStopDistance || timer > 3f)
        {
            // Right in your face... and gone. It'll be back soon, angrier.
            if (player != null) player.AddCameraShake(maxCameraShake * 2f);
            Aggression = Mathf.Min(maxAggression, Aggression + 1);
            StartFragments(converge: false);
            PlaySound(poofSound != null ? poofSound : ProceduralAudio.Poof, 0.7f);
            ResetVisual();
            Hide(Random.Range(minChargeRespawn, maxChargeRespawn));
            return;
        }

        float step = Mathf.Min(chargeSpeed * Time.deltaTime, dist - chargeStopDistance);
        Vector3 next = transform.position + toPlayer / dist * step;
        next.y = GroundHeight(next);
        transform.position = next;
    }

    /// <summary>Seconds until its next creep: sooner the angrier it is.</summary>
    float NextCreepDelay() => Random.Range(minCreepInterval, maxCreepInterval) / (1f + 0.5f * Aggression);

    /// <summary>Metres per creep: further the angrier it is.</summary>
    float CreepStep => creepStep * (1f + 0.25f * Aggression);

    bool IsLitByFlashlight() => InBeam(transform.position + Vector3.up * (1.4f * scale * growth), beamMargin, false);

    /// <summary>True if the flashlight beam is on its eyes. Peeking round a trunk doesn't hide them.</summary>
    bool EyesInBeam() => InBeam((leftEye.position + rightEye.position) * 0.5f, 0f, peeking);

    bool InBeam(Vector3 point, float margin, bool ignoreTrees)
    {
        if (flashlight == null) flashlight = FindAnyObjectByType<Flashlight>();
        if (flashlight == null || !flashlight.IsOn) return false;

        Light beam = flashlight.flashlight;
        Vector3 to = point - beam.transform.position;
        float dist = to.magnitude;

        if (dist > beam.range) return false;
        if (Vector3.Angle(beam.transform.forward, to) > beam.spotAngle * 0.5f + margin) return false;
        // Trees in the way block the beam.
        if (!ignoreTrees && Physics.Raycast(beam.transform.position, to / dist, dist - 0.3f, ~0, QueryTriggerInteraction.Ignore))
            return false;
        return true;
    }

    void SetGrowth(float value)
    {
        growth = value;
        transform.localScale = Vector3.one * growth;
    }

    void TryCreep()
    {
        if (IsPlayerLooking() || SafeZones.Contains(target.position) || GameOverScreen.IsActive)
        {
            creepTimer = Random.Range(1f, 2f); // try again shortly
            return;
        }
        creepTimer = NextCreepDelay();

        Vector3 toPlayer = target.position - transform.position;
        toPlayer.y = 0f;
        float dist = toPlayer.magnitude;
        if (dist < 0.01f) return;
        Vector3 dir = toPlayer / dist;

        if (dist - CreepStep <= killDistance)
        {
            StartKill();
            return;
        }

        // Step closer, drifting a little to one side so it isn't a perfectly straight line.
        Vector3 side = Vector3.Cross(Vector3.up, dir) * Random.Range(-1f, 1f);
        Vector3 next = transform.position + dir * CreepStep + side;
        next.y = GroundHeight(next);
        if (SafeZones.Contains(next)) return;

        transform.position = next;
        FacePlayer();
        // Stepped out from behind its tree, and into a new pose for when you look back.
        peeking = false;
        SetPose(RandomPose(), Random.value < 0.5f ? -1 : 1);
        PlaySound(creepSound != null ? creepSound : ProceduralAudio.Creep);
    }

    void UpdateCrescendo()
    {
        FacePlayer();
        if (player != null) player.SetApproachLimit(this, transform.position, reactDistance, stopDistance);

        timer += Time.deltaTime;
        float c = Mathf.Clamp01(timer / Mathf.Max(CrescendoDuration, 0.01f));
        float e = c * c; // ease in: slow build, violent finish

        Vibrate(Mathf.Lerp(maxVibration, crescendoVibration, e));
        visual.localScale = new Vector3(1f - 0.15f * e, 1f + 0.2f * e, 1f - 0.15f * e);

        float flicker = 1f + Random.Range(-0.3f, 0.3f) * e;
        mouthTarget = Mathf.Lerp(0.7f, 1.5f, e); // stretching wider and wider
        // Its eyes carry on from how they looked and burn blue as it's driven off, the glow round them swelling.
        float blue = Mathf.SmoothStep(0f, 1f, c);
        float brightness = Mathf.Lerp(crescendoStartBrightness, spawnEyeIntensity * 1.5f, e) * flicker;
        SetEyes(Mathf.Lerp(maxEyeWiden, maxEyeWiden * 1.8f, e), brightness, crescendoStartShine * (1f - blue), blue);
        eyeLight.intensity = Mathf.Lerp(crescendoStartLight, brightness, e) * eyeLightStrength * (1f + dismissGlow * blue);
        whisper.volume = volume * Mathf.Lerp(1f, crescendoVolume, e);
        whisper.pitch = Mathf.Lerp(1f, 1.6f, e);
        advanceSource.volume = Mathf.MoveTowards(advanceSource.volume, 0f, Time.deltaTime * 2f);

        if (player != null) player.AddCameraShake(maxCameraShake * e);

        if (c >= 1f) Poof();
    }

    void Poof()
    {
        StartFragments(converge: false);
        PlaySound(poofSound != null ? poofSound : ProceduralAudio.Poof);
        ResetVisual();
        whisper.pitch = Random.Range(0.85f, 1.1f);
        Hide(Random.Range(minRespawnDelay, maxRespawnDelay));
    }

    void StartKill()
    {
        if (GameOverScreen.IsActive) return;

        CurrentState = State.Killing;
        timer = 0f;
        gameOverStarted = false;
        ReleasePlayer();
        if (player != null) player.enabled = false;

        // Snap right in front of the player's face.
        Transform cam = Camera.main != null ? Camera.main.transform : target;
        Vector3 forward = cam.forward;
        forward.y = 0f;
        forward = forward.sqrMagnitude > 0.001f ? forward.normalized : target.forward;
        Vector3 p = cam.position + forward * 1.3f;
        p.y = GroundHeight(p);
        transform.position = p;
        FacePlayer();

        SetVisible(true);
        advanceSource.Stop();
        PlaySound(killSound != null ? killSound : ProceduralAudio.Screech, 1f, killVolume);
    }

    void UpdateKilling()
    {
        timer += Time.deltaTime;

        Vibrate(crescendoVibration * 0.6f);
        visual.localScale = new Vector3(0.85f, 1.2f, 0.85f);
        SetEyes(maxEyeWiden * 2f, spawnEyeIntensity * 2f * Random.Range(0.7f, 1.3f));
        mouthTarget = 1.6f;
        whisper.volume = volume * killVolume;

        // Force the camera to stare into its face.
        Transform cam = Camera.main != null ? Camera.main.transform : null;
        if (cam != null)
        {
            Vector3 head = transform.position + Vector3.up * (2.2f * scale * growth);
            Quaternion look = Quaternion.LookRotation(head - cam.position);
            cam.rotation = Quaternion.Slerp(cam.rotation, look, 1f - Mathf.Exp(-12f * Time.deltaTime));
            cam.rotation *= Quaternion.Euler(Random.insideUnitSphere * 1.5f);
        }

        if (!gameOverStarted && timer >= killTime)
        {
            gameOverStarted = true;
            GameOverScreen.Show();
        }
    }

    /// <summary>
    /// Wipes out its progress on the player: if it's out hunting it's blasted away (poofs) and starts over
    /// from a distance, back to normal size. Used by the watchtower floodlights.
    /// </summary>
    public void ResetHunt()
    {
        if (CurrentState == State.Watching || CurrentState == State.Crescendo || CurrentState == State.Materializing)
            Poof();
        creepTimer = NextCreepDelay();
    }

    void ReleasePlayer()
    {
        if (player == null) return;
        player.ClearApproachLimit(this);
    }

    // ---------------------------------------------------------------- Helpers

    bool IsPlayerLooking() => IsPlayerLooking(lookAngle);

    bool IsPlayerLooking(float withinDegrees)
    {
        Transform cam = Camera.main != null ? Camera.main.transform : target;
        Vector3 head = transform.position + Vector3.up * (2f * scale * growth);
        Vector3 toHead = head - cam.position;

        if (Vector3.Angle(cam.forward, toHead) > withinDegrees) return false;
        // Peeking round a trunk, its head is in plain view even though the tree is between you and its body.
        if (peeking) return true;

        // A tree or rock in the way means the player can't actually see it.
        float dist = toHead.magnitude;
        if (Physics.Raycast(cam.position, toHead / dist, dist - 0.3f, ~0, QueryTriggerInteraction.Ignore))
            return false;

        return true;
    }

    Vector3 PickSpawnPoint()
    {
        Vector3 forward = target.forward;
        forward.y = 0f;
        if (forward.sqrMagnitude < 0.001f) forward = Vector3.forward;
        forward.Normalize();

        Vector3 p = target.position;
        for (int attempt = 0; attempt < 15; attempt++)
        {
            Vector3 dir = Quaternion.Euler(0f, Random.Range(-spawnViewAngle, spawnViewAngle), 0f) * forward;
            // If big lights keep blocking the usual spots, look further out.
            float reach = attempt < 5 ? 1f : Mathf.Lerp(1f, 2.5f, (attempt - 5) / 9f);
            // Angrier, it turns up closer.
            p = target.position + dir * (Random.Range(minSpawnDistance, maxSpawnDistance) * reach * (1f - 0.12f * Aggression));

            // Keep it inside the generated forest (unless the map wraps around, where anywhere is fine).
            if (generator != null && !WorldWrap.IsActive)
            {
                float half = generator.areaSize * 0.5f - 3f;
                Vector3 center = generator.transform.position;
                p.x = Mathf.Clamp(p.x, center.x - half, center.x + half);
                p.z = Mathf.Clamp(p.z, center.z - half, center.z + half);
            }

            if (!SafeZones.Contains(p)) break;
            // Lit lamps ahead: try anywhere around the player instead.
            forward = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f) * Vector3.forward;
        }

        p.y = GroundHeight(p);
        return p;
    }

    /// <summary>
    /// A spot just behind a tree trunk (from the player's point of view), in view and at spawn distance.
    /// </summary>
    bool TryPickPeekSpot(out Vector3 spot)
    {
        spot = Vector3.zero;
        if (generator == null) return false;
        Vector3 from = target.position;
        Vector3 forward = target.forward;
        forward.y = 0f;
        if (forward.sqrMagnitude < 0.001f) return false;

        var candidates = new List<(Transform tree, float radius)>();
        foreach (var pivot in generator.SwayPivots)
        {
            if (pivot == null || !pivot.gameObject.activeInHierarchy || pivot.parent == null) continue;
            var trunk = pivot.parent.GetComponent<CapsuleCollider>();
            if (trunk == null || !trunk.enabled) continue;
            Vector3 to = pivot.position - from;
            to.y = 0f;
            float dist = to.magnitude;
            if (dist < minSpawnDistance - 6f || dist > maxSpawnDistance) continue;
            if (Vector3.Angle(forward, to) > spawnViewAngle) continue;
            if (SafeZones.Contains(pivot.position)) continue;
            candidates.Add((pivot, trunk.radius));
        }
        if (candidates.Count == 0) return false;

        var (tree, radius) = candidates[Random.Range(0, candidates.Count)];
        Vector3 away = tree.position - from;
        away.y = 0f;
        spot = tree.position + away.normalized * (radius + 0.45f);
        spot.y = GroundHeight(spot);
        return true;
    }

    Pose RandomPose()
    {
        Pose next = pose;
        for (int i = 0; i < 4 && next == pose; i++) next = CreepPoses[Random.Range(0, CreepPoses.Length)];
        return next;
    }

    /// <summary>Snaps the body into a pose. side (1 or -1) picks which way it leans or which arm it uses.</summary>
    void SetPose(Pose newPose, int side)
    {
        pose = newPose;
        posture.localPosition = Vector3.zero;
        posture.localRotation = Quaternion.identity;
        poseHead = poseLeftArm = poseRightArm = Quaternion.identity;
        ref Quaternion arm = ref (side < 0 ? ref poseLeftArm : ref poseRightArm);

        switch (newPose)
        {
            case Pose.HeadTilt:
                poseHead = Quaternion.Euler(10f, 0f, side * 75f); // lolled right over onto its shoulder
                break;
            case Pose.Reach:
                arm = Quaternion.Euler(-85f, 0f, side * -10f); // one long arm held out toward you
                poseHead = Quaternion.Euler(-5f, 0f, side * 15f);
                break;
            case Pose.Crouch:
                posture.localRotation = Quaternion.Euler(35f, 0f, 0f); // hunched low, head jutting at you
                poseHead = Quaternion.Euler(-40f, 0f, 0f);
                poseLeftArm = poseRightArm = Quaternion.Euler(-35f, 0f, 0f); // still dangling straight down
                break;
            case Pose.TurnedAway:
                posture.localRotation = Quaternion.Euler(0f, 180f, 0f); // its back to you...
                poseHead = Quaternion.Euler(0f, 180f, 0f);               // ...but its head turned all the way round
                break;
            case Pose.ArmsUp:
                poseLeftArm = Quaternion.Euler(-170f, 0f, -12f);
                poseRightArm = Quaternion.Euler(-170f, 0f, 12f);
                break;
            case Pose.HangingHead:
                poseHead = Quaternion.Euler(55f, 0f, side * -35f);
                break;
            case Pose.Lunge:
                posture.localRotation = Quaternion.Euler(20f, 0f, 0f);             // thrown forward
                poseLeftArm = Quaternion.Euler(-80f, 0f, -15f);                     // both arms out, grabbing
                poseRightArm = Quaternion.Euler(-80f, 0f, 15f);
                poseHead = Quaternion.Euler(-25f, 0f, 0f);
                break;
            case Pose.Peek:
                posture.localRotation = Quaternion.Euler(0f, 0f, -side * 28f); // leaning out from behind the trunk
                poseHead = Quaternion.Euler(0f, 0f, side * 20f);
                arm = Quaternion.Euler(-95f, 0f, side * -25f);                  // fingers curled round the bark
                break;
        }
    }

    float HorizontalDistanceToTarget()
    {
        Vector3 d = transform.position - target.position;
        d.y = 0f;
        return d.magnitude;
    }

    float GroundHeight(Vector3 p)
    {
        if (generator != null) return generator.GetGroundHeight(p.x, p.z);
        if (Physics.Raycast(p + Vector3.up * 30f, Vector3.down, out var hit, 100f, ~0, QueryTriggerInteraction.Ignore))
            return hit.point.y;
        return target.position.y;
    }

    void FacePlayer()
    {
        Vector3 look = target.position - transform.position;
        look.y = 0f;
        if (look.sqrMagnitude > 0.001f)
            transform.rotation = Quaternion.LookRotation(look);
    }

    void Vibrate(float amount)
    {
        visual.localPosition = Vector3.Scale(Random.insideUnitSphere, new Vector3(1f, 0.4f, 1f)) * amount;
        visual.localRotation = Quaternion.Euler(Random.insideUnitSphere * amount * 40f);
    }

    void ResetVisual()
    {
        visual.localPosition = Vector3.zero;
        visual.localRotation = Quaternion.identity;
        visual.localScale = Vector3.one;
        SetEyes(1f, eyeIntensity);
    }

    /// <summary>shine: eyeshine in the flashlight beam (0-1). blue: being dismissed (0-1).</summary>
    void SetEyes(float widen, float brightness, float shine = 0f, float blue = 0f)
    {
        Vector3 s = new Vector3(eyeBaseScale.x * Mathf.Lerp(1f, widen, 0.5f), eyeBaseScale.y * widen, eyeBaseScale.z);
        leftEye.localScale = s;
        rightEye.localScale = s;
        eyeBrightness = brightness;
        Color color = Color.Lerp(Color.Lerp(eyeColor, eyeshineColor, shine), dismissColor, blue);
        MaterialFactory.SetColor(eyeMat, color * brightness);
        eyeLight.color = Color.Lerp(eyeColor, dismissColor, blue);
        eyeLight.intensity = brightness * eyeLightStrength * (1f + dismissGlow * blue);
        eyeLight.range = Mathf.Lerp(6f, 12f, blue);
    }

    void SetVisible(bool visible)
    {
        foreach (var r in bodyRenderers) r.enabled = visible;
        eyeLight.enabled = visible;
    }

    void PlaySound(AudioClip clip, float pitch = 0f, float volumeScale = 1f)
    {
        if (clip == null) return;
        sfx.pitch = pitch > 0f ? pitch : Random.Range(0.9f, 1.1f);
        sfx.PlayOneShot(clip, volume * volumeScale);
    }

    // ---------------------------------------------------------------- Fragments

    void BuildFragments()
    {
        fragmentRoot = new GameObject("Shadow Entity Fragments").transform;

        Material black = MaterialFactory.Unlit(Color.black);
        Material ember = MaterialFactory.Unlit(eyeColor * 4f);

        fragments = new Transform[fragmentCount];
        fragStart = new Vector3[fragmentCount];
        fragEnd = new Vector3[fragmentCount];
        fragVelocity = new Vector3[fragmentCount];
        fragAge = new float[fragmentCount];
        fragLife = new float[fragmentCount];
        fragSize = new float[fragmentCount];

        for (int i = 0; i < fragmentCount; i++)
        {
            // A few glowing embers mixed in with the black smoke.
            var go = CreatePart(PrimitiveType.Sphere, i < 4 ? ember : black, fragmentRoot);
            go.GetComponent<Renderer>().enabled = false;
            fragments[i] = go.transform;
        }
    }

    void StartFragments(bool converge)
    {
        fragmentsConverge = converge;
        fragmentsActive = true;

        for (int i = 0; i < fragments.Length; i++)
        {
            Vector3 bodyPoint = transform.position + transform.rotation * new Vector3(
                Random.Range(-0.25f, 0.25f), Random.Range(0.1f, 2.3f), Random.Range(-0.12f, 0.12f)) * scale;

            fragSize[i] = Random.Range(0.08f, 0.22f) * scale;
            fragAge[i] = 0f;

            if (converge)
            {
                Vector3 offset = Random.onUnitSphere * Random.Range(2f, 4f) * scale;
                offset.y = Mathf.Abs(offset.y);
                fragStart[i] = bodyPoint + offset;
                fragEnd[i] = bodyPoint;
                fragLife[i] = materializeTime * Random.Range(0.6f, 1f);
            }
            else
            {
                Vector3 outward = Random.insideUnitSphere;
                outward.y = 0f;
                fragStart[i] = bodyPoint;
                fragVelocity[i] = outward.normalized * Random.Range(2f, 6f) + Vector3.up * Random.Range(0.5f, 3f);
                fragLife[i] = poofTime * Random.Range(0.5f, 1f);
            }

            fragments[i].position = fragStart[i];
            fragments[i].localScale = converge ? Vector3.zero : Vector3.one * fragSize[i];
            fragments[i].GetComponent<Renderer>().enabled = true;
        }
    }

    void UpdateFragments()
    {
        if (!fragmentsActive) return;

        bool anyAlive = false;
        float dt = Time.deltaTime;

        for (int i = 0; i < fragments.Length; i++)
        {
            if (fragAge[i] >= fragLife[i]) continue;

            fragAge[i] += dt;
            float p = Mathf.Clamp01(fragAge[i] / fragLife[i]);
            Transform f = fragments[i];

            if (fragmentsConverge)
            {
                f.position = Vector3.Lerp(fragStart[i], fragEnd[i], p * p);
                f.localScale = Vector3.one * (fragSize[i] * p);
            }
            else
            {
                fragVelocity[i] *= 1f - Mathf.Clamp01(2.5f * dt); // drag
                fragVelocity[i].y += 0.5f * dt;                   // smoke drifts upward
                f.position += fragVelocity[i] * dt;
                f.localScale = Vector3.one * (fragSize[i] * (1f - p));
            }

            if (p >= 1f) f.GetComponent<Renderer>().enabled = false;
            else anyAlive = true;
        }

        fragmentsActive = anyAlive;
    }

    // ---------------------------------------------------------------- Body

    void BuildSilhouette()
    {
        // Posture holds the pose; the silhouette under it vibrates.
        posture = new GameObject("Posture").transform;
        posture.SetParent(transform, false);
        visual = new GameObject("Silhouette").transform;
        visual.SetParent(posture, false);

        Material black = MaterialFactory.Unlit(Color.black);

        // Proportions are for a 2.4m figure (+z is forward). Everything is too long and too thin,
        // and the joints bend the wrong way.
        for (int side = -1; side <= 1; side += 2)
        {
            // Legs: thighs angled forward, shins sharply back, long splayed toes.
            Vector3 hip = new Vector3(side * 0.11f, 1.12f, -0.02f);
            Vector3 knee = new Vector3(side * 0.15f, 0.62f, 0.14f);
            Vector3 ankle = new Vector3(side * 0.13f, 0.14f, -0.1f);
            Bone(visual, black, hip, knee, 0.07f);
            Bone(visual, black, knee, ankle, 0.045f);
            Knob(visual, black, knee, 0.085f);
            Knob(visual, black, ankle, 0.05f);
            for (int toe = -1; toe <= 1; toe++)
                Bone(visual, black, ankle, new Vector3(side * 0.13f + toe * 0.045f, 0.01f, 0.16f), 0.018f);

            // Ribs curling round from the spine.
            for (int r = 0; r < 4; r++)
            {
                float y = 1.78f - r * 0.1f;
                Vector3 back = new Vector3(side * 0.03f, y, -0.08f);
                Vector3 outer = new Vector3(side * 0.16f, y - 0.04f, 0.0f);
                Vector3 front = new Vector3(side * 0.05f, y - 0.09f, 0.1f);
                Bone(visual, black, back, outer, 0.016f);
                Bone(visual, black, outer, front, 0.014f);
            }

            // Collarbones and bony shoulders.
            Vector3 shoulder = new Vector3(side * 0.26f, 1.9f, 0.02f);
            Bone(visual, black, new Vector3(0f, 1.88f, 0.08f), shoulder, 0.03f);
            Knob(visual, black, shoulder, 0.075f);

            // Arms hang past the knees; the elbow bends backwards.
            var arm = Pivot(visual, "Arm", shoulder);
            Vector3 elbow = new Vector3(side * 0.12f, -0.6f, -0.16f);
            Vector3 wrist = new Vector3(side * 0.06f, -1.22f, 0.1f);
            Bone(arm, black, Vector3.zero, elbow, 0.045f);
            Bone(arm, black, elbow, wrist, 0.035f);
            Knob(arm, black, elbow, 0.06f);

            // Long, curling fingers.
            var hand = Pivot(arm, "Hand", wrist);
            for (int f = 0; f < 4; f++)
            {
                float spread = (f - 1.5f) * 0.035f;
                var finger = Pivot(hand, "Finger", new Vector3(spread, 0f, 0.01f));
                Vector3 mid = new Vector3(spread * 0.6f, -0.17f, 0.02f);
                Vector3 tip = new Vector3(spread * 1.1f, -0.31f, 0.09f);
                Bone(finger, black, Vector3.zero, mid, 0.013f);
                Bone(finger, black, mid, tip, 0.01f);
                fingers.Add(finger);
                fingerRest.Add(finger.localRotation);
            }

            if (side < 0) leftArm = arm; else rightArm = arm;
        }

        // Some have more arms than they should, sprouting from the ribs below the real ones.
        if (Random.value < extraArmsChance)
        {
            int pairs = Random.Range(1, Mathf.Max(1, maxExtraArmPairs) + 1);
            for (int pair = 0; pair < pairs; pair++)
                for (int side = -1; side <= 1; side += 2)
                    BuildExtraArm(black, side, 1.68f - pair * 0.2f, pair);
        }

        // Hunched spine with knobbly vertebrae, a narrow pelvis and caved-in chest.
        Vector3[] spine =
        {
            new Vector3(0f, 1.12f, -0.03f), new Vector3(0f, 1.45f, -0.1f),
            new Vector3(0f, 1.78f, -0.04f), new Vector3(0f, 1.95f, 0.12f),
        };
        for (int i = 0; i < spine.Length - 1; i++)
        {
            Bone(visual, black, spine[i], spine[i + 1], 0.06f);
            Knob(visual, black, spine[i] + new Vector3(0f, 0f, -0.035f), 0.055f);
        }
        var pelvis = CreatePart(PrimitiveType.Sphere, black, visual).transform;
        pelvis.localPosition = new Vector3(0f, 1.12f, -0.02f) * scale;
        pelvis.localScale = new Vector3(0.24f * bulk, 0.13f, 0.15f * bulk) * scale;
        var chest = CreatePart(PrimitiveType.Capsule, black, visual).transform;
        chest.localPosition = new Vector3(0f, 1.66f, -0.02f) * scale;
        chest.localRotation = Quaternion.Euler(18f, 0f, 0f);
        chest.localScale = new Vector3(0.24f * bulk, 0.22f, 0.13f * bulk) * scale;

        // A long neck jutting forward, and a small, too-tall head.
        Vector3 neckTop = new Vector3(0f, 2.12f, 0.24f);
        Bone(visual, black, spine[spine.Length - 1], neckTop, 0.05f);
        headPivot = Pivot(visual, "Head", neckTop);
        var skull = CreatePart(PrimitiveType.Sphere, black, headPivot).transform;
        skull.localPosition = new Vector3(0f, 0.1f, 0.02f) * scale;
        skull.localScale = new Vector3(0.2f, 0.3f, 0.22f) * scale;
        var jaw = CreatePart(PrimitiveType.Sphere, black, headPivot).transform;
        jaw.localPosition = new Vector3(0f, -0.05f, 0.07f) * scale;
        jaw.localRotation = Quaternion.Euler(20f, 0f, 0f);
        jaw.localScale = new Vector3(0.12f, 0.1f, 0.16f) * scale;

        // Fog-free, so eyeshine glints back from far off in the mist.
        eyeMat = MaterialFactory.NoFog(eyeColor * eyeIntensity);
        eyeBaseScale = new Vector3(0.035f, 0.025f, 0.02f) * scale;
        leftEye = CreatePart(PrimitiveType.Sphere, eyeMat, headPivot).transform;
        leftEye.localPosition = new Vector3(-0.05f, 0.12f, 0.125f) * scale;
        rightEye = CreatePart(PrimitiveType.Sphere, eyeMat, headPivot).transform;
        rightEye.localPosition = new Vector3(0.05f, 0.12f, 0.125f) * scale;

        BuildMouth();

        eyeLight = new GameObject("Eye Glow").AddComponent<Light>();
        eyeLight.transform.SetParent(headPivot, false);
        eyeLight.transform.localPosition = new Vector3(0f, 0.12f, 0.25f) * scale;
        eyeLight.type = LightType.Point;
        eyeLight.color = eyeColor;
        eyeLight.range = 6f;
        eyeLight.shadows = LightShadows.None;

        bodyRenderers = visual.GetComponentsInChildren<Renderer>();
        SetEyes(1f, eyeIntensity);
    }

    /// <summary>
    /// An extra arm growing out of the side of the ribcage at a height: thinner and longer than the real
    /// ones, splayed out, with its own curling fingers. It moves out of step with everything else.
    /// </summary>
    void BuildExtraArm(Material black, int side, float y, int pair)
    {
        Vector3 root = new Vector3(side * 0.15f, y, -0.02f);
        Knob(visual, black, root, 0.045f);

        var arm = Pivot(visual, "Extra Arm", root);
        arm.localRotation = Quaternion.Euler(Random.Range(-25f, 15f), 0f, side * Random.Range(18f, 40f));
        float length = 1.15f + pair * 0.1f;
        Vector3 elbow = new Vector3(side * 0.08f, -0.55f * length, 0.12f);
        Vector3 wrist = new Vector3(side * 0.03f, -1.05f * length, -0.05f);
        Bone(arm, black, Vector3.zero, elbow, 0.03f);
        Bone(arm, black, elbow, wrist, 0.024f);
        Knob(arm, black, elbow, 0.045f);

        var hand = Pivot(arm, "Hand", wrist);
        for (int f = 0; f < 3; f++)
        {
            float spread = (f - 1f) * 0.03f;
            var finger = Pivot(hand, "Finger", new Vector3(spread, 0f, 0.01f));
            Vector3 mid = new Vector3(spread * 0.6f, -0.15f, 0.03f);
            Vector3 tip = new Vector3(spread * 1.2f, -0.28f, 0.1f);
            Bone(finger, black, Vector3.zero, mid, 0.01f);
            Bone(finger, black, mid, tip, 0.008f);
            fingers.Add(finger);
            fingerRest.Add(finger.localRotation);
        }

        extraArms.Add(arm);
        extraArmRest.Add(arm.localRotation);
    }

    /// <summary>
    /// A thin, too-wide split across the lower face, dark red inside, lined with narrow teeth. Scaled
    /// vertically by how open it is; hidden when shut.
    /// </summary>
    void BuildMouth()
    {
        mouth = Pivot(headPivot, "Mouth", new Vector3(0f, 0.01f, 0.115f));
        var inside = CreatePart(PrimitiveType.Sphere, MaterialFactory.NoFog(new Color(0.22f, 0.015f, 0.01f)), mouth).transform;
        inside.localScale = new Vector3(0.19f, 0.07f, 0.035f) * scale;
        // The corners curl up a little: a grin.
        for (int side = -1; side <= 1; side += 2)
        {
            var corner = CreatePart(PrimitiveType.Sphere, MaterialFactory.NoFog(new Color(0.22f, 0.015f, 0.01f)), mouth).transform;
            corner.localPosition = new Vector3(side * 0.085f, 0.012f, -0.012f) * scale;
            corner.localRotation = Quaternion.Euler(0f, 0f, side * 25f);
            corner.localScale = new Vector3(0.05f, 0.025f, 0.025f) * scale;
        }

        var toothMat = MaterialFactory.Unlit(new Color(0.55f, 0.53f, 0.46f));
        for (int row = -1; row <= 1; row += 2)
        {
            for (int i = 0; i < 9; i++)
            {
                float x = (i - 4) * 0.018f;
                var tooth = CreatePart(PrimitiveType.Cube, toothMat, mouth).transform;
                tooth.localPosition = new Vector3(x, row * 0.024f, 0.012f - x * x * 2f) * scale;
                tooth.localRotation = Quaternion.Euler(0f, x * 300f, 0f);
                tooth.localScale = new Vector3(0.007f, 0.024f, 0.006f) * scale;
            }
        }
        mouth.localScale = new Vector3(1f, 0.001f, 1f);
        mouth.gameObject.SetActive(false);
    }

    /// <summary>Unsettling idle motion: the head snaps to new angles, arms drift, fingers curl and uncurl.</summary>
    void UpdateTwitch(float dt)
    {
        twitchTimer -= dt;
        if (twitchTimer <= 0f)
        {
            twitchTimer = Random.Range(0.4f, 2.5f);
            headTarget = Quaternion.Euler(Random.Range(-15f, 20f), Random.Range(-30f, 30f), Random.Range(-40f, 40f));
        }
        headPivot.localRotation = Quaternion.Slerp(headPivot.localRotation, poseHead * headTarget, 1f - Mathf.Exp(-25f * dt));

        float t = Time.time;
        leftArm.localRotation = poseLeftArm * Quaternion.Euler(Mathf.Sin(t * 0.7f + 1f) * 6f, 0f, Mathf.Sin(t * 0.5f) * 4f);
        rightArm.localRotation = poseRightArm * Quaternion.Euler(Mathf.Sin(t * 0.6f + 2.5f) * 6f, 0f, Mathf.Sin(t * 0.45f + 1f) * 4f);
        // Extra arms grope about on their own, out of time with the rest.
        for (int i = 0; i < extraArms.Count; i++)
            extraArms[i].localRotation = extraArmRest[i] * Quaternion.Euler(
                Mathf.Sin(t * (0.9f + i * 0.23f) + i * 1.7f) * 16f, 0f, Mathf.Sin(t * (0.6f + i * 0.17f) + i) * 12f);

        for (int i = 0; i < fingers.Count; i++)
        {
            float curl = 15f + 25f * Mathf.Sin(t * 1.3f + i * 0.9f);
            fingers[i].localRotation = fingerRest[i] * Quaternion.Euler(curl, 0f, 0f);
        }
    }

    /// <summary>A thin cylinder between two points (in 2.4m-figure units, local to parent).</summary>
    Transform Bone(Transform parent, Material mat, Vector3 a, Vector3 b, float thickness)
    {
        a *= scale;
        b *= scale;
        Vector3 d = b - a;
        var bone = CreatePart(PrimitiveType.Cylinder, mat, parent).transform;
        bone.localPosition = (a + b) * 0.5f;
        bone.localRotation = Quaternion.FromToRotation(Vector3.up, d);
        bone.localScale = new Vector3(thickness * bulk * scale, d.magnitude * 0.5f, thickness * bulk * scale);
        return bone;
    }

    /// <summary>A knobbly joint.</summary>
    void Knob(Transform parent, Material mat, Vector3 at, float size)
    {
        var knob = CreatePart(PrimitiveType.Sphere, mat, parent).transform;
        knob.localPosition = at * scale;
        knob.localScale = Vector3.one * (size * Mathf.Lerp(1f, bulk, 0.6f) * scale);
    }

    Transform Pivot(Transform parent, string name, Vector3 at)
    {
        var pivot = new GameObject(name).transform;
        pivot.SetParent(parent, false);
        pivot.localPosition = at * scale;
        return pivot;
    }

    static GameObject CreatePart(PrimitiveType type, Material mat, Transform parent)
    {
        var go = GameObject.CreatePrimitive(type);
        go.name = type.ToString();
        go.transform.SetParent(parent, false);
        go.GetComponent<Renderer>().sharedMaterial = mat;
        go.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;

        // It's a ghost: no collisions with the player or trees.
        var col = go.GetComponent<Collider>();
        if (Application.isPlaying) Destroy(col);
        else DestroyImmediate(col);
        return go;
    }
}
