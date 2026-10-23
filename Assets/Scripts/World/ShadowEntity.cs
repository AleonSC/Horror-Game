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

    // Static state is cleared when Play starts, so nothing carries over from the last session if the
    // editor's Enter Play Mode settings skip reloading scripts (no domain reload).
    /// <summary>Clears this class's static state (StaticReset calls it when Play starts).</summary>
    internal static void ResetStatics() { All.Clear(); Capturing = null; }

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

    /// <summary>Spawns one that comes up right at a spot, almost at once (the dev tools' stick).</summary>
    public static ShadowEntity SpawnAt(Vector3 at)
    {
        var entity = Spawn(0.2f);
        entity.forcedSpawn = at;
        return entity;
    }
    Vector3? forcedSpawn;

    /// <summary>
    /// Summons a crowd drawn to the giant's searchlights: they appear one after another round the edge of the
    /// light and circle it. (They're ordinary entities otherwise: step out of the light and they come for you.)
    /// </summary>
    public static void SpawnCrowd(int count)
    {
        for (int i = 0; i < count; i++)
        {
            var entity = Spawn(0.3f + i * Random.Range(0.1f, 0.25f));
            entity.crowdMember = true;
            // Two ranks: one standing right at the rim of the light, silhouetted against it, and one just
            // behind, so you're walled in wherever you look.
            entity.crowdOffset = i % 2 == 0 ? Random.Range(-0.9f, -0.2f) : Random.Range(0.8f, 2.2f);
        }
    }

    /// <summary>Part of the crowd round the giant's searchlights: it always rematerializes at the light's edge.</summary>
    [HideInInspector] public bool crowdMember;
    /// <summary>Where in the crowd it stands: metres outside (+) or inside (-) the edge of the light.</summary>
    [HideInInspector] public float crowdOffset = 1.5f;

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
    [Tooltip("If it's already this close when it creeps, it takes the player. (A creep never brings it nearer than this.)")]
    public float killDistance = 3f;

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
    [Range(0f, 1f), Tooltip("Of the peekers, the share that duck back behind the trunk the moment you look at them, and lean out again once you look away (the rest stay peeking out).")]
    public float duckChance = 0.5f;

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
    [Range(0f, 1f), Tooltip("Share of the flashlight's battery it costs to face one down.")]
    public float dismissCost = 0.1f;
    [Tooltip("Each lamp the player has lit makes the crescendo this much faster (0.15 = 15% of the base speed per lamp).")]
    public float crescendoSpeedupPerLamp = 0.15f;
    [Tooltip("However many lamps are lit, the crescendo never gets shorter than this.")]
    public float minCrescendoTime = 0.8f;
    [Tooltip("Degrees of camera shake at the peak of the crescendo.")]
    public float maxCameraShake = 2.5f;
    public int fragmentCount = 40;
    public float poofTime = 1.2f;
    public float materializeTime = 1.2f;

    [Header("Taking the Player")]
    [Tooltip("Once it has you, you can't move: your eyes are dragged to it and it walks up to you this slowly (metres a second)...")]
    public float approachSpeed = 1.1f;
    [Tooltip("...starting at least this far off...")]
    public float approachMinDistance = 3.5f;
    [Tooltip("...then takes hold of you over this many seconds...")]
    public float graspTime = 1.4f;
    [Tooltip("...a hole tears open in the air behind it over this many seconds...")]
    public float portalOpenTime = 1f;
    [Tooltip("...it drags you back through it over this many seconds...")]
    public float dragTime = 2.8f;
    [Tooltip("...and the screen fades to black over this many seconds.")]
    public float fadeTime = 4f;

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

    public enum State { Hidden, Materializing, Watching, Crescendo, Killing, Charging, Retreating }
    public State CurrentState { get; private set; }
    /// <summary>
    /// How far its hunt has got, 0 to 1: how close it has worked its way to the player, and how much it has grown
    /// stalking them in the dark. Only goes up; starts over when it's driven off. Its eyes brighten with it.
    /// </summary>
    public float HuntProgress { get; private set; }

    public bool IsVisible => CurrentState == State.Watching || CurrentState == State.Crescendo || CurrentState == State.Killing
        || CurrentState == State.Charging || CurrentState == State.Retreating;

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
    bool ducker;  // ...and one of the ones that ducks back when looked at
    int peekSide;
    float peekOut = 1f, unwatched;
    Transform peekTree;
    float peekTreeRadius;
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
        Tuning.Apply(this); // saved tuning (Save Tuning on the component's menu), before anything uses it
        GroundLeaves.Register(transform, 0.35f); // even unseen, the leaves where it steps give it away
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
        if (Capturing == this) Capturing = null;
    }

    void OnDestroy()
    {
        if (fragmentRoot != null) Destroy(fragmentRoot.gameObject);
    }

    void Update()
    {
        if (MainMenu.Showing) return; // (nothing comes for you while the main menu is up)
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

        // Gone into the dark room: they can't follow. They wait, out of sight, until you come back out.
        if (PortalDoor.PlayerInRoom && CurrentState != State.Killing)
        {
            if (CurrentState != State.Hidden) Hide(Random.Range(minRespawnDelay, maxRespawnDelay));
            UpdateFragments();
            return;
        }

        UpdateFragments();
        if (IsVisible) UpdateTwitch(Time.deltaTime);
        UpdateBeamHalo();

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

            case State.Retreating:
                UpdateRetreating();
                break;
        }

        // The mouth splits open slowly, and snaps shut when it's gone.
        if (CurrentState == State.Hidden || CurrentState == State.Materializing) mouthTarget = mouthOpen = 0f;
        mouthOpen = Mathf.MoveTowards(mouthOpen, mouthTarget, Time.deltaTime * 2.5f);
        mouth.localScale = new Vector3(Mathf.Lerp(1f, 1.25f, Mathf.Clamp01(mouthOpen - 1f)), Mathf.Max(mouthOpen, 0.001f), 1f); // gaping wider past fully open
        bool showMouth = mouthOpen > 0.01f;
        if (mouth.gameObject.activeSelf != showMouth) mouth.gameObject.SetActive(showMouth);
        UpdateMouthGlare();
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
        if (Capturing == this) Capturing = null;
        if (retreatPortal != null) retreatPortal.Close();
        retreatPortal = null;
    }

    void StartMaterialize()
    {
        if (GameOverScreen.IsActive) { timer = 1f; return; }

        peekSide = Random.value < 0.5f ? -1 : 1;
        Vector3 peekSpot = Vector3.zero;
        var watcher = WatcherEvent.Instance;
        bool atLight = crowdMember && watcher != null && watcher.SearchlightsOn;
        peeking = !atLight && !forcedSpawn.HasValue && Random.value < peekChance && TryPickPeekSpot(out peekSpot);
        transform.position = forcedSpawn.HasValue ? forcedSpawn.Value : atLight ? PickCrowdSpot(watcher) : peeking ? peekSpot : PickSpawnPoint();
        forcedSpawn = null;
        FacePlayer();
        ResetVisual();
        SetPose(peeking ? Pose.Peek : RandomPose(), peekSide);
        ducker = peeking && Random.value < duckChance;
        peekOut = 1f;
        unwatched = 0f;

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
        // Peeking: as you move, it slides round the trunk to keep the tree between you, never out from behind it.
        if (peeking)
        {
            if (peekTree != null && peekTree.gameObject.activeInHierarchy)
            {
                Vector3 away = peekTree.position - target.position;
                away.y = 0f;
                if (ducker) Duck();
                if (away.sqrMagnitude > 0.01f)
                {
                    // (Ducked back, it's further in behind the trunk: all of it hidden.)
                    float behind = ducker ? (1f - peekOut) * 0.3f : 0f;
                    Vector3 spot = peekTree.position + away.normalized * (peekTreeRadius + BodyRadius + 0.12f + behind);
                    spot.y = GroundHeight(spot);
                    transform.position = Vector3.MoveTowards(transform.position, spot, 6f * Time.deltaTime);
                }
            }
            else peeking = false; // its tree fell: it's in the open now
        }

        FacePlayer();

        float dist = HorizontalDistanceToTarget();
        if (dist > relocateDistance)
        {
            Hide(Random.Range(minRespawnDelay, maxRespawnDelay));
            return;
        }

        // A lamp was lit right next to it: it's driven off. (Not the crowd round the giant's searchlight:
        // they're past caring about lamps.)
        var watcherNow = WatcherEvent.Instance;
        bool withCrowd = crowdMember && watcherNow != null && watcherNow.SearchlightsOn;
        if (!withCrowd && SafeZones.Contains(transform.position))
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
            if (!IsPlayerLooking()) CircleLight(withCrowd ? watcherNow : safeLight);
        }
        else if (IsLitByFlashlight())
        {
            // Close enough to face it down: its eyes turn blue, so you know it can be driven off.
            if (dist < reactDistance) beamLitFrame = Time.frameCount;
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
                transform.position = Settle(next);
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
        SetEyes(Mathf.Lerp(1f, maxEyeWiden, closeness), Mathf.Max(brightness, eyeshine * eyeshineBrightness), eyeshine * (1f - beamGlow), Mathf.SmoothStep(0f, 1f, beamGlow)); // the blue it burns when faced down
        eyeLight.intensity = brightness * eyeLightStrength; // a reflection: it doesn't light up the trees

        // The last few metres: its mouth drops open, light glaring out of it.
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
        float ring = light.Radius + (crowdMember ? crowdOffset : ringMargin);

        Vector3 offset = transform.position - center;
        offset.y = 0f;
        float r = offset.magnitude;
        float angle = r > 0.01f ? Mathf.Atan2(offset.x, offset.z) : Random.Range(0f, Mathf.PI * 2f);

        // Head in to the edge of the light, then walk around it.
        if (r > ring + 0.2f)
            r = Mathf.MoveTowards(r, ring, (crowdMember ? 4f : circleSpeed) * Time.deltaTime); // the crowd keeps up with the moving light
        else
        {
            r = ring;
            angle += circleDirection * circleSpeed * Time.deltaTime / ring;
        }

        Vector3 next = center + new Vector3(Mathf.Sin(angle), 0f, Mathf.Cos(angle)) * r;
        transform.position = Settle(next); // steps round trunks on its way

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
        transform.position = Settle(next);

        stepSoundTimer -= Time.deltaTime;
        if (stepSoundTimer <= 0f)
        {
            stepSoundTimer = Random.Range(0.8f, 2f);
            PlaySound(creepSound != null ? creepSound : ProceduralAudio.Creep);
        }
    }

    /// <summary>Called when the player grabs a watchtower ladder: if it's waiting to appear, it comes now.</summary>
    float banishedUntil = -1f;

    /// <summary>True while it's out where it can be seen and caught in the camera's flash (not while it's taking you).</summary>
    public bool CanBeFlashed => CurrentState == State.Materializing || CurrentState == State.Watching
                                || CurrentState == State.Crescendo || CurrentState == State.Charging;

    /// <summary>Caught in the camera's flash: it bursts apart and stays gone for a while.</summary>
    public void Banish(float seconds)
    {
        if (!CanBeFlashed) return;
        StartFragments(converge: false);
        PlaySound(poofSound != null ? poofSound : ProceduralAudio.Poof);
        ResetVisual();
        Hide(seconds);
        banishedUntil = Time.time + seconds;
    }

    /// <summary>
    /// Called in by the flier's alarm: it comes in now (bursting apart first if it was already out) and
    /// starts a fresh hunt. Not while it's charging or taking someone, or while it's been flashed away.
    /// </summary>
    public void Rouse()
    {
        if (CurrentState == State.Killing || CurrentState == State.Charging || Time.time < banishedUntil) return;
        if (CanBeFlashed)
        {
            StartFragments(converge: false);
            PlaySound(poofSound != null ? poofSound : ProceduralAudio.Poof);
            ResetVisual();
        }
        Hide(Random.Range(0.5f, 2.5f));
        creepTimer = NextCreepDelay();
    }

    /// <summary>The player was caught: it melts away and comes back to start its hunt over, after a while.</summary>
    public void Retreat(float seconds)
    {
        if (CanBeFlashed || CurrentState == State.Killing)
        {
            StartFragments(converge: false);
            ResetVisual();
        }
        Hide(seconds);
        creepTimer = NextCreepDelay();
    }

    /// <summary>Back now, after Retreat (the giant's searchlight has found the player again).</summary>
    public void Recall()
    {
        if (CurrentState == State.Hidden) timer = Random.Range(0.3f, 4f);
    }

    public void Summon()
    {
        if (Time.time < banishedUntil) return; // it's been driven off: not even the watchtower brings it back early
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
        PlaySound(crescendoSound != null ? crescendoSound : ProceduralAudio.DispelBuild, crescendoTime / CrescendoDuration, crescendoVolume);
    }

    /// <summary>How long the crescendo takes now: every lamp the player has lit makes them quicker at dismissing it.</summary>
    float CrescendoDuration =>
        Mathf.Max(minCrescendoTime, crescendoTime / (1f + crescendoSpeedupPerLamp * Lamp.TimesLit)) * Difficulty.DismissTime;

    /// <summary>
    /// Counts clicks of the flashlight while it's aimed at this entity (switched on onto it, or off just after
    /// being on it). Enough of them in quick succession and it charges. Returns true if it just did.
    /// </summary>
    bool WatchForFlashing(bool eyesLit)
    {
        if (flashlight == null) return false;
        bool aimedAt = eyesLit || IsLitByFlashlight() || (flashlight.IsOn && InBeam(transform.position + Vector3.up * scale, 10f, PeekingOut));
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
        transform.position = Settle(next);
    }

    /// <summary>Seconds until its next creep: sooner the angrier it is.</summary>
    float NextCreepDelay() => Random.Range(minCreepInterval, maxCreepInterval) / (1f + 0.5f * Aggression);

    /// <summary>Metres per creep: further the angrier it is.</summary>
    float CreepStep => creepStep * (1f + 0.25f * Aggression);

    bool IsLitByFlashlight() => InBeam(transform.position + Vector3.up * (1.4f * scale * growth), beamMargin, false);

    /// <summary>True if the flashlight beam is on its eyes. Peeking round a trunk doesn't hide them.</summary>
    bool EyesInBeam() => InBeam((leftEye.position + rightEye.position) * 0.5f, 0f, PeekingOut);

    /// <summary>Peeking with its head out round the trunk (a ducker ducked back in is hidden by the tree).</summary>
    bool PeekingOut => peeking && !(ducker && peekOut < 0.3f);

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

        // Only once it's right there does it take you; a creep stops short, just out of reach.
        if (dist <= killDistance)
        {
            StartKill();
            return;
        }
        float step = Mathf.Min(CreepStep, dist - killDistance * 0.85f);
        if (step < 0.3f) step = dist - killDistance * 0.85f;

        // Step closer, drifting a little to one side so it isn't a perfectly straight line.
        Vector3 side = Vector3.Cross(Vector3.up, dir) * Random.Range(-1f, 1f) * Mathf.Clamp01(step / CreepStep);
        Vector3 next = Settle(transform.position + dir * step + side);
        if (SafeZones.Contains(next)) return;

        transform.position = next;
        FacePlayer();
        // Stepped out from behind its tree, and into a new pose for when you look back.
        peeking = false;
        SetPose(RandomPose(), Random.value < 0.5f ? -1 : 1);
        PlaySound(creepSound != null ? creepSound : ProceduralAudio.Creep);
    }

    /// <summary>
    /// Facing it down: held in the light, it shudders harder and harder, stretching, its mouth splitting
    /// wider, its eyes burning from red to blue. Then it's gone (Dispel).
    /// </summary>
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
        float blue = Mathf.Max(Mathf.SmoothStep(0f, 1f, c), Mathf.SmoothStep(0f, 1f, beamGlow)); // (already blue from the beam)
        float brightness = Mathf.Lerp(crescendoStartBrightness, spawnEyeIntensity * 1.5f, e) * flicker;
        // Its eyes stretch wide open, staring, as it's driven off.
        SetEyes(Mathf.Lerp(maxEyeWiden, maxEyeWiden * 2.8f, Mathf.SmoothStep(0f, 1f, c)), brightness, crescendoStartShine * (1f - blue), blue);
        eyeLight.intensity = Mathf.Lerp(crescendoStartLight, brightness, e) * eyeLightStrength * (1f + dismissGlow * blue);
        whisper.volume = volume * Mathf.Lerp(1f, crescendoVolume, e);
        whisper.pitch = Mathf.Lerp(1f, 1.6f, e);
        advanceSource.volume = Mathf.MoveTowards(advanceSource.volume, 0f, Time.deltaTime * 2f);

        if (player != null) player.AddCameraShake(maxCameraShake * e);

        if (c >= 1f) Dispel();
    }

    /// <summary>
    /// Faced down: a hole tears open in the air behind it, black, its edge burning red, and it's dragged back
    /// into it (Retreating). The light pays for it.
    /// </summary>
    void Dispel()
    {
        PlaySound(poofSound != null ? poofSound : ProceduralAudio.Dispel, 1f);
        if (flashlight != null) flashlight.Drain(dismissCost);
        ResetVisual();
        mouthTarget = 0f;

        Vector3 toPlayer = target.position - transform.position;
        toPlayer.y = 0f;
        retreatDir = toPlayer.sqrMagnitude > 0.001f ? -toPlayer.normalized : -transform.forward;
        retreatFrom = transform.position;
        Vector3 centre = transform.position + retreatDir * 0.45f * scale + Vector3.up * 1.25f * scale;
        retreatPortal = ScourPortal.Open(centre, -retreatDir, new Vector2(0.8f, 1.45f) * scale);
        if (retreatPortal == null) { Vanish(); return; } // (no shader for it: the old burst)
        CurrentState = State.Retreating;
        timer = 0f;
    }

    /// <summary>Dragged back through the hole behind it, shuddering, faster and faster; then it shuts.</summary>
    void UpdateRetreating()
    {
        timer += Time.deltaTime;
        float t = Mathf.Clamp01((timer - RetreatDelay) / RetreatTime);
        float pull = t * t * t; // slow to give, then snatched away
        transform.position = retreatFrom + retreatDir * pull * 2.2f * scale;
        Vibrate(Mathf.Lerp(maxVibration, crescendoVibration, 1f - t * 0.5f));
        visual.localScale = new Vector3(1f - 0.25f * pull, 1f + 0.1f * pull, 1f - 0.25f * pull);
        SetEyes(Mathf.Lerp(maxEyeWiden * 2.8f, 0.3f, pull), spawnEyeIntensity * (1f - pull), 0f, 1f - t);
        whisper.volume = volume * crescendoVolume * (1f - t);
        if (player != null) player.AddCameraShake(maxCameraShake * 0.5f * (1f - t));
        if (t >= 1f)
        {
            ResetVisual();
            whisper.pitch = Random.Range(0.85f, 1.1f);
            Hide(Random.Range(minRespawnDelay, maxRespawnDelay)); // (and the hole shuts behind it)
        }
    }

    /// <summary>The old way out: it bursts apart in a ghostly breath and is gone.</summary>
    void Vanish()
    {
        StartFragments(converge: false);
        whisper.pitch = Random.Range(0.85f, 1.1f);
        Hide(Random.Range(minRespawnDelay, maxRespawnDelay));
    }

    ScourPortal retreatPortal;
    Vector3 retreatDir, retreatFrom;
    const float RetreatDelay = 0.35f, RetreatTime = 1.3f;

    void Poof()
    {
        StartFragments(converge: false);
        PlaySound(poofSound != null ? poofSound : ProceduralAudio.Poof);
        ResetVisual();
        whisper.pitch = Random.Range(0.85f, 1.1f);
        Hide(Random.Range(minRespawnDelay, maxRespawnDelay));
    }

    // ---------------------------------------------------------------- Caught in the beam

    int beamLitFrame = -10;
    float beamGlow;

    /// <summary>
    /// The tell that the flashlight has it, and holding it there will drive it off: its eyes fade from red to
    /// the cold blue they burn when it's faced down while the beam's on it, and back when the beam leaves.
    /// </summary>
    void UpdateBeamHalo()
    {
        bool lit = (CurrentState == State.Watching && Time.frameCount - beamLitFrame <= 1) || CurrentState == State.Crescendo;
        beamGlow = Mathf.MoveTowards(beamGlow, lit ? 1f : 0f, Time.deltaTime / (lit ? 0.5f : 0.6f));
    }

    /// <summary>The one taking the player right now, if any: only one at a time.</summary>
    public static ShadowEntity Capturing { get; private set; }

    // The taking, in stages (seconds since it caught you): it walks up to you, takes hold, a hole tears open
    // in the air behind it, and it drags you back through it into the dark.
    float approachTime;
    Vector3 approachFrom, takeStart, takeForward, portalCentre;

    void StartKill()
    {
        if (GameOverScreen.IsActive || (Capturing != null && Capturing != this)) return;
        Capturing = this;

        CurrentState = State.Killing;
        timer = 0f;
        gameOverStarted = false;
        ReleasePlayer();
        if (player != null) player.enabled = false; // held in place: no moving, no looking away
        var body = target.GetComponent<CharacterController>();
        if (body != null) body.enabled = false;     // it moves the player from here on

        // Where it stands now (pushed back a little if it's right on top of you, so you see it come).
        takeStart = target.position;
        Vector3 away = transform.position - target.position;
        away.y = 0f;
        if (away.sqrMagnitude < 0.01f)
        {
            Transform cam = Camera.main != null ? Camera.main.transform : target;
            away = Vector3.ProjectOnPlane(cam.forward, Vector3.up);
            if (away.sqrMagnitude < 0.001f) away = target.forward;
        }
        takeForward = away.normalized;
        float dist = Mathf.Max(away.magnitude, approachMinDistance);
        approachFrom = target.position + takeForward * dist;
        approachFrom.y = GroundHeight(approachFrom);
        transform.position = approachFrom;
        approachTime = Mathf.Clamp((dist - 0.9f) / Mathf.Max(approachSpeed, 0.1f), 2f, 6f);
        if (growth < 1f) SetGrowth(1f);
        FacePlayer();

        SetVisible(true);
        ResetVisual();
        SetPose(Pose.Neutral, 1);
        advanceSource.Stop();
        PlaySound(killSound != null ? killSound : ProceduralAudio.Screech, 0.7f, killVolume * 0.6f); // a low, quiet shriek: it has you
        PlaySound(ProceduralAudio.Breath, 0.8f, 0.9f);
    }

    void UpdateKilling()
    {
        timer += Time.deltaTime;
        float dt = Time.deltaTime;
        Transform cam = Camera.main != null ? Camera.main.transform : null;
        float eye = 2.2f * scale * growth;
        float t = timer;
        float tGrasp = approachTime, tPortal = tGrasp + graspTime, tDrag = tPortal + portalOpenTime;

        SetEyes(Mathf.Lerp(1.2f, maxEyeWiden * 2f, Mathf.Clamp01(t / tPortal)), spawnEyeIntensity * 2f * Random.Range(0.85f, 1.15f));
        whisper.volume = volume * killVolume;
        visual.localScale = Vector3.one;

        if (t < tGrasp)
        {
            // It walks up to you: slowly, lurching, its eyes on yours. You can't move; your gaze is dragged to it.
            float u = t / approachTime;
            float steps = Mathf.Max(2f, approachTime * 1.3f);
            float stepPhase = u * steps;
            float lurch = Mathf.SmoothStep(0f, 1f, Mathf.Floor(stepPhase) / steps + Mathf.SmoothStep(0f, 1f, stepPhase - Mathf.Floor(stepPhase)) / steps);
            Vector3 stopAt = takeStart + takeForward * 0.9f;
            Vector3 p = Vector3.Lerp(approachFrom, stopAt, lurch);
            p.y = GroundHeight(p) + Mathf.Abs(Mathf.Sin(stepPhase * Mathf.PI)) * 0.06f;
            transform.position = p;
            FacePlayer();
            Vibrate(crescendoVibration * 0.15f);
            mouthTarget = Mathf.Lerp(0.3f, 1.2f, u);
            if (u > 0.75f && pose != Pose.Reach) SetPose(Pose.Reach, Random.value < 0.5f ? -1 : 1); // a hand coming up for you
            LookAtIt(cam, eye, 2.2f, Mathf.Lerp(0.3f, 2.2f, u * u), dt);
            return;
        }

        if (t < tPortal)
        {
            // It takes hold of you: both hands, its face right up to yours.
            if (pose != Pose.Lunge) { SetPose(Pose.Lunge, 1); PlaySound(ProceduralAudio.Moan, 0.6f, 0.8f); }
            float u = (t - tGrasp) / graspTime;
            Vector3 p = takeStart + takeForward * Mathf.Lerp(0.9f, 0.75f, u);
            p.y = GroundHeight(p);
            transform.position = p;
            FacePlayer();
            Vibrate(crescendoVibration * 0.25f);
            mouthTarget = 1.6f;
            LookAtIt(cam, eye, 6f, 2.6f, dt);
            return;
        }

        // Behind it a hole tears open in the air, black, its edge burning red. It holds you there, staring.
        Vector3 hold = takeStart + takeForward * 0.75f;
        float toPlane = 0.75f + 0.55f * scale; // from you to the hole's face
        if (retreatPortal == null && !gameOverStarted)
        {
            portalCentre = hold + takeForward * 0.55f * scale + Vector3.up * 1.25f * scale;
            portalCentre.y = GroundHeight(hold) + 1.25f * scale;
            retreatPortal = ScourPortal.Open(portalCentre, -takeForward, new Vector2(0.85f, 1.5f) * scale);
            PlaySound(ProceduralAudio.Moan, 0.5f, 0.9f);
        }
        if (t < tDrag)
        {
            Vector3 p = hold;
            p.y = GroundHeight(p);
            transform.position = p;
            FacePlayer();
            Vibrate(crescendoVibration * 0.3f);
            mouthTarget = 1.6f;
            LookAtIt(cam, eye, 6f, 3f, dt);
            return;
        }

        // It backs into the hole, dragging you after it, slowly and then all at once, until there's only the
        // black of it; and the screen fades out.
        float d = Mathf.Clamp01((t - tDrag) / Mathf.Max(dragTime, 0.01f));
        float pull = d * d * (3f - 2f * d) * d;
        float moved = pull * (toPlane - 0.18f); // (stopping just short of it: all you see is the black)
        Vector3 you = takeStart + takeForward * moved;
        you.y = Mathf.Lerp(takeStart.y, GroundHeight(you), 0.5f) + 0.15f * d; // feet dragging, lifted a little
        target.position = you;
        Vector3 it = takeStart + takeForward * (0.75f + moved);
        it.y = GroundHeight(it);
        transform.position = it;
        FacePlayer();
        Vibrate(crescendoVibration * Mathf.Lerp(0.3f, 0.05f, d));
        if (cam != null)
        {
            Quaternion look = Quaternion.LookRotation(portalCentre - cam.position);
            cam.rotation = Quaternion.Slerp(cam.rotation, look, 1f - Mathf.Exp(-4f * dt));
            cam.rotation *= Quaternion.Euler(Random.insideUnitSphere * Mathf.Lerp(2.5f, 0.6f, d));
        }
        if (player != null) player.AddCameraShake(maxCameraShake * 0.4f * (1f - d));
        if (!gameOverStarted && d > 0.55f)
        {
            gameOverStarted = true;
            GameOverScreen.Show(fadeTime); // (which plays the sting)
        }
    }

    /// <summary>Drags the held player's gaze onto its face (pull: how fast) and shakes it with fear (degrees).</summary>
    void LookAtIt(Transform cam, float eyeHeight, float pull, float fear, float dt)
    {
        if (cam == null) return;
        Vector3 head = transform.position + Vector3.up * eyeHeight;
        Quaternion look = Quaternion.LookRotation(head - cam.position);
        cam.rotation = Quaternion.Slerp(cam.rotation, look, 1f - Mathf.Exp(-pull * dt));
        // Trembling: a fast shiver on top of a slower, ragged unsteadiness.
        float tt = Time.time;
        Vector3 shake = Random.insideUnitSphere * fear * 0.35f
                        + new Vector3(Mathf.PerlinNoise(tt * 6f, 1.3f) - 0.5f, Mathf.PerlinNoise(2.7f, tt * 6f) - 0.5f, 0f) * fear * 1.6f;
        cam.rotation *= Quaternion.Euler(shake);
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
        if (PeekingOut) return true;

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

            p.y = GroundHeight(p);
            if (!SafeZones.Contains(p) && !Blocked(p)) break;
            // Lit lamps ahead: try anywhere around the player instead.
            forward = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f) * Vector3.forward;
        }

        return Settle(p); // last resort: nudged out of whatever it's in
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

        for (int attempt = 0; attempt < 6 && candidates.Count > 0; attempt++)
        {
            int pick = Random.Range(0, candidates.Count);
            var (tree, radius) = candidates[pick];
            candidates.RemoveAt(pick);
            Vector3 away = tree.position - from;
            away.y = 0f;
            spot = tree.position + away.normalized * (radius + BodyRadius + 0.12f);
            spot.y = GroundHeight(spot);
            if (!Blocked(spot))
            {
                peekTree = tree;
                peekTreeRadius = radius;
                return true; // not jammed against another tree
            }
        }
        return false;
    }

    /// <summary>A spot just outside the searchlight's pool, all round it.</summary>
    Vector3 PickCrowdSpot(WatcherEvent watcher)
    {
        var light = (SafeZones.ISource)watcher;
        Vector3 p = light.Center;
        for (int attempt = 0; attempt < 10; attempt++)
        {
            float angle = Random.Range(0f, Mathf.PI * 2f);
            p = light.Center + new Vector3(Mathf.Sin(angle), 0f, Mathf.Cos(angle)) * (light.Radius + crowdOffset);
            p.y = GroundHeight(p);
            if (!Blocked(p)) return p;
        }
        return Settle(p);
    }

    // ---------------------------------------------------------------- Not walking through things

    /// <summary>Its body, for keeping out of trunks and rocks: a thin upright capsule.</summary>
    float BodyRadius => 0.32f * Mathf.Max(1f, growth);
    static readonly Collider[] overlaps = new Collider[16];

    /// <summary>
    /// The solid things (trunks, rocks, walls...) its body would be inside, standing at a point. The capsule
    /// starts knee-high, and the ground and the player don't count.
    /// </summary>
    int Overlapping(Vector3 feet)
    {
        float r = BodyRadius;
        Vector3 bottom = feet + Vector3.up * (0.7f + r);
        Vector3 top = feet + Vector3.up * Mathf.Max(0.7f + r, height * growth - r);
        int count = Physics.OverlapCapsuleNonAlloc(bottom, top, r, overlaps, ~0, QueryTriggerInteraction.Ignore);
        int kept = 0;
        for (int i = 0; i < count; i++)
        {
            Collider c = overlaps[i];
            if (c is CharacterController || c.GetComponentInParent<FirstPersonController>() != null) continue;
            if (c is MeshCollider ground && ground.sharedMesh != null && ground.sharedMesh.name == "Forest Ground") continue;
            overlaps[kept++] = c;
        }
        return kept;
    }

    bool Blocked(Vector3 feet) => Overlapping(feet) > 0;

    /// <summary>
    /// Puts a point on the ground and pushes it out of any trunk or rock it would be inside, so it slides
    /// round them instead of through.
    /// </summary>
    Vector3 Settle(Vector3 p)
    {
        p.y = GroundHeight(p);
        float r = BodyRadius;
        for (int iteration = 0; iteration < 4; iteration++)
        {
            int count = Overlapping(p);
            if (count == 0) break;
            Vector3 probe = p + Vector3.up * 1.2f;
            for (int i = 0; i < count; i++)
            {
                Collider c = overlaps[i];
                Bounds b = c.bounds;
                Vector3 push;
                float clearance;
                if (c is MeshCollider mesh && !mesh.convex)
                {
                    // Rocks and boulders: treat them as round, from their bounds.
                    push = probe - b.center;
                    clearance = Mathf.Max(b.extents.x, b.extents.z) * 0.85f + r;
                }
                else
                {
                    push = probe - c.ClosestPoint(probe);
                    push.y = 0f;
                    clearance = r + 0.02f;
                    if (push.sqrMagnitude < 0.0001f)
                    {
                        // Right inside it: push out from its middle.
                        push = probe - b.center;
                        clearance = r + Mathf.Max(b.extents.x, b.extents.z);
                    }
                }
                push.y = 0f;
                if (push.sqrMagnitude < 0.0001f) push = Vector3.Cross(Vector3.up, transform.forward);
                float d = push.magnitude;
                if (d < clearance) p += push / d * (clearance - d);
            }
            p.y = GroundHeight(p);
        }
        return p;
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
        glowColor = Color.Lerp(eyeColor, dismissColor, blue); // the mouth's light burns the same
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
            go.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off; // just specks of it
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
                f.localScale = Vector3.one * (fragSize[i] * Mathf.Min(1f, p * 8f) * (1f - p)); // a quick swell, then fading away
            }

            if (p >= 1f) f.GetComponent<Renderer>().enabled = false;
            else anyAlive = true;
        }

        fragmentsActive = anyAlive;
    }

    // ---------------------------------------------------------------- Body

    // The legs: long, limp, and dragged. Each foot is a weight on the end of the leg (Verlet): gravity pulls
    // it down, the ground stops it and holds it back, and the hip hauls it along when the leg's at full
    // stretch. The knee is placed between hip and foot (bending forward, the way a knee does).
    const float FootLength = 0.2f; // (for a 2.4m figure)
    float thighLength = 0.68f, shinLength = 0.7f; // (likewise; a model can change them)
    readonly Transform[] legPivots = new Transform[2], kneePivots = new Transform[2], footPivots = new Transform[2];
    readonly Vector3[] footPos = new Vector3[2], footPrev = new Vector3[2];
    bool feetPlaced;

    /// <summary>
    /// The legs ragdolling: they hang from the hips and drag along the ground behind it as it glides, feet
    /// scraping and catching, knees jutting; when it stops they lie where they fell. Off the ground they
    /// dangle and swing.
    /// </summary>
    void LegDrag()
    {
        float dt = Time.deltaTime;
        if (legPivots[0] == null) return;
        if (!IsVisible || dt <= 0f) { feetPlaced = false; return; }

        Vector3 forward = transform.forward;
        for (int i = 0; i < 2; i++)
        {
            Transform hip = legPivots[i];
            Vector3 h = hip.position;
            float size = hip.lossyScale.y;
            float thigh = thighLength * scale * size, shin = shinLength * scale * size, reach = (thigh + shin) * 0.995f;

            // Placed afresh (it's only just appeared, or jumped): the feet on the ground under the hips.
            if (!feetPlaced || (footPos[i] - h).sqrMagnitude > reach * reach * 4f)
            {
                Vector3 under = h + forward * 0.1f * scale + transform.right * (i == 0 ? -0.06f : 0.06f) * scale;
                under.y = Mathf.Max(GroundHeight(under), h.y - reach);
                footPos[i] = footPrev[i] = under;
            }

            // Verlet: carry on as it was going, a little damped, and fall.
            Vector3 p = footPos[i];
            Vector3 v = (p - footPrev[i]) * 0.98f;
            footPrev[i] = p;
            p += v + Physics.gravity * dt * dt;

            // The ground: it can't go through, and it scrapes to a stop along it.
            float ground = GroundHeight(p) + 0.03f * scale;
            if (p.y <= ground)
            {
                p.y = ground;
                Vector3 slide = p - footPrev[i];
                slide.y = 0f;
                p -= slide * 0.35f; // friction
            }

            // The leg: never longer than it is (the hip hauls the foot after it), never folded right up.
            Vector3 d = p - h;
            float len = d.magnitude;
            if (len > reach) p = h + d / len * reach;
            else if (len < reach * 0.35f && len > 0.0001f) p = h + d / len * reach * 0.35f;
            if (p.y < ground) p.y = ground;
            footPos[i] = p;

            // The knee, between them: forward, the way a knee bends.
            d = p - h;
            len = Mathf.Max(d.magnitude, 0.0001f);
            Vector3 dir = d / len;
            float along = (thigh * thigh - shin * shin + len * len) / (2f * len);
            float up = Mathf.Sqrt(Mathf.Max(thigh * thigh - along * along, 0f));
            Vector3 bend = Vector3.ProjectOnPlane(forward + Vector3.up * 0.2f, dir);
            if (bend.sqrMagnitude < 0.0001f) bend = Vector3.ProjectOnPlane(Vector3.up, dir);
            Vector3 knee = h + dir * along + bend.normalized * up;

            hip.rotation = Quaternion.FromToRotation(Vector3.down, knee - h);
            kneePivots[i].rotation = Quaternion.FromToRotation(Vector3.down, p - knee);
            // The foot: dragged toes-down behind it, flat along the ground when at rest.
            Vector3 toes = Vector3.ProjectOnPlane(forward, Vector3.up).normalized;
            Vector3 moving = Vector3.ProjectOnPlane(v / Mathf.Max(dt, 0.0001f), Vector3.up);
            float drag = Mathf.Clamp01(moving.magnitude / (1.5f * scale));
            footPivots[i].rotation = Quaternion.LookRotation(Vector3.Slerp(toes, Vector3.down, 0.55f * drag), Vector3.up);
        }
        feetPlaced = true;
    }

    /// <summary>
    /// A ducker, peeking round its trunk: the instant you look its way it's gone, snapped back behind the
    /// tree; look away for a moment and it leans slowly out again, watching you.
    /// </summary>
    void Duck()
    {
        bool looked = IsPlayerLooking();
        unwatched = looked ? 0f : unwatched + Time.deltaTime;
        float wanted = looked ? 0f : unwatched > 0.8f ? 1f : peekOut;
        float before = peekOut;
        peekOut = Mathf.MoveTowards(peekOut, wanted, Time.deltaTime * (wanted < peekOut ? 7f : 0.7f)); // back fast, out slowly
        if (before > 0.6f && peekOut <= 0.6f) PlaySound(ProceduralAudio.BushRustle, Random.Range(1.4f, 1.8f), 0.25f); // the scuff of it ducking back
        posture.localRotation = Quaternion.Euler(0f, 0f, -peekSide * 28f * peekOut);
    }

    void LateUpdate()
    {
        LegDrag();
    }

    void BuildSilhouette()
    {
        // Posture holds the pose; the silhouette under it vibrates.
        posture = new GameObject("Posture").transform;
        posture.SetParent(transform, false);
        visual = new GameObject("Silhouette").transform;
        visual.SetParent(posture, false);

        Material black = MaterialFactory.Unlit(Color.black);

        // A model saved in the model editor, if there is one to use (not while capturing the game's own look).
        var model = capturing ? null : EnemyModel.ForGame("Scour");
        if (model != null) { BuildFromModel(model, black); return; }

        // Proportions are for a 2.4m figure (+z is forward). Everything is too long and too thin,
        // and the joints bend the wrong way.
        for (int side = -1; side <= 1; side += 2)
        {
            BuildLeg(side, new Vector3(side * 0.11f, 1.12f, -0.02f), black, 0.06f);

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
    /// A leg from the hip (in 2.4m-figure units): long and thin, too long for it (the knees jut up when it
    /// stands), with a long bony foot. It hangs limp and drags along the ground behind it (LegDrag).
    /// </summary>
    void BuildLeg(int side, Vector3 hip, Material mat, float thickness)
    {
        var legPivot = Pivot(visual, "Leg", hip);
        Bone(legPivot, mat, Vector3.zero, Vector3.down * thighLength, thickness);
        var kneePivot = Pivot(legPivot, "Knee", Vector3.down * thighLength);
        Knob(kneePivot, mat, Vector3.zero, thickness * 1.25f);
        Bone(kneePivot, mat, Vector3.zero, Vector3.down * shinLength, thickness * 0.67f);
        var footPivot = Pivot(kneePivot, "Foot", Vector3.down * shinLength);
        Knob(footPivot, mat, Vector3.zero, thickness * 0.83f);
        Bone(footPivot, mat, Vector3.zero, new Vector3(0f, -0.02f, FootLength), thickness * 0.58f);
        Bone(footPivot, mat, new Vector3(0f, -0.02f, FootLength), new Vector3(0f, -0.04f, FootLength + 0.08f), thickness * 0.3f); // the toes
        int i = side < 0 ? 0 : 1;
        legPivots[i] = legPivot;
        kneePivots[i] = kneePivot;
        footPivots[i] = footPivot;
    }

    bool capturing;

    /// <summary>
    /// The scour exactly as the game builds it, as a model (for the model editor): built on an object that's
    /// switched off, so it never wakes, then turned into shapes and thrown away. No extra arms; its dragged
    /// legs and its mouth are left to the game, as they're made either way.
    /// </summary>
    internal static EnemyModel CaptureModel()
    {
        var go = new GameObject("Scour (model capture)");
        go.SetActive(false);
        try
        {
            var s = go.AddComponent<ShadowEntity>();
            Tuning.Apply(s);
            s.capturing = true;
            s.extraArmsChance = 0f;
            s.scale = s.height / 2.4f;
            s.BuildSilhouette();
            var model = EnemyModel.FromHierarchy("Scour", s.height, go.transform, s.headPivot, s.leftArm, s.rightArm,
                s.legPivots[0], s.legPivots[1], s.mouth, new[] { s.leftEye, s.rightEye }, new[] { s.legPivots[0], s.legPivots[1] });
            model.legs = true;
            model.thigh = s.thighLength * s.scale;
            model.shin = s.shinLength * s.scale;
            model.legThickness = 0.06f * s.bulk * s.scale;
            return model;
        }
        finally { Destroy(go); }
    }

    /// <summary>
    /// Built from a model made in the model editor (EnemyModel, ModelEditor) instead: its shapes, on its
    /// joints, with this scour's own dragging legs from its hips, its mouth where the model says, its eyes
    /// (the model's first two eye parts) widening and burning as they do, and its head and arms twitching.
    /// </summary>
    void BuildFromModel(EnemyModel model, Material black)
    {
        float k = height / Mathf.Max(model.height, 0.01f); // model metres to this scour's
        float toFigure = k / Mathf.Max(scale, 0.0001f);     // model metres to 2.4m-figure units
        Color firstEye = Color.clear;
        foreach (var part in model.parts) if (part.eye) { firstEye = part.color; break; }
        if (firstEye.a > 0f) eyeColor = firstEye;
        eyeMat = MaterialFactory.NoFog(eyeColor * eyeIntensity);

        var materials = new Dictionary<string, Material>();
        Material MaterialFor(ModelPart part)
        {
            if (part.eye) return eyeMat;
            string key = ColorUtility.ToHtmlStringRGBA(part.color) + "/" + part.glow;
            if (materials.TryGetValue(key, out var m)) return m;
            m = part.glow > 0f ? MaterialFactory.NoFog(part.color * part.glow)
                : part.color.maxColorComponent < 0.01f ? black : MaterialFactory.Lit(part.color, 0.1f);
            materials[key] = m;
            return m;
        }
        var built = model.Build(visual, k, MaterialFor, false, false);
        foreach (var go in built.parts) go.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.On;

        headPivot = built.head;
        leftArm = built.armLeft;
        rightArm = built.armRight;
        // The eyes SetEyes works on: the first two (stand-ins if the model has fewer).
        leftEye = built.eyes.Count > 0 ? built.eyes[0] : new GameObject("No Eye").transform;
        rightEye = built.eyes.Count > 1 ? built.eyes[1] : new GameObject("No Eye").transform;
        if (leftEye.parent == null) leftEye.SetParent(headPivot, false);
        if (rightEye.parent == null) rightEye.SetParent(headPivot, false);
        eyeBaseScale = leftEye.localScale;

        if (model.legs)
        {
            thighLength = model.thigh * toFigure;
            shinLength = model.shin * toFigure;
            BuildLeg(-1, model.hipLeft * toFigure, black, model.legThickness * toFigure / Mathf.Max(bulk, 0.01f));
            BuildLeg(1, model.hipRight * toFigure, black, model.legThickness * toFigure / Mathf.Max(bulk, 0.01f));
        }

        Vector3 mouthAt = model.mouth * toFigure;
        BuildMouth(mouthAt);

        // The glow round its eyes: a little in front of them.
        Vector3 eyesAt = (leftEye.localPosition + rightEye.localPosition) * 0.5f / Mathf.Max(scale, 0.0001f);
        if (built.eyes.Count == 0) eyesAt = new Vector3(0f, 0.12f, 0.12f);
        eyeLight = new GameObject("Eye Glow").AddComponent<Light>();
        eyeLight.transform.SetParent(headPivot, false);
        eyeLight.transform.localPosition = (eyesAt + Vector3.forward * 0.12f) * scale;
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
    // The mouth: a gaping hole that drops open, lit from inside, glaring light out of it.
    Material mouthGlowMat, mouthBeamMat;
    Light mouthLight;
    Transform mouthBeam;
    Color glowColor = new Color(1f, 0.08f, 0.05f);

    /// <summary>
    /// The mouth, for a 2.4m figure (scaled by its object): a throat sinking back from a ragged oval rim,
    /// folded, dark flesh at the rim glowing brighter the deeper it goes; thick wet lips round it; and two
    /// rows of long, uneven fangs, top and bottom, curving in and back down its throat. Vertex colours are
    /// each part's shade, alphas how much of the glow reaches it (Horror/Throat).
    /// </summary>
    static Mesh BuildMouthMesh()
    {
        const int sides = 32, rings = 8;
        const float rx = 0.05f, ry = 0.078f, rimZ = 0.046f, deepZ = 0.024f;
        var verts = new List<Vector3>();
        var colors = new List<Color>();
        var tris = new List<int>();
        var flesh = new Color(0.07f, 0.006f, 0.005f);
        float seed = Random.Range(0f, 100f);
        float Fold(float a) => 1f + 0.09f * Mathf.Sin(a * 7f + seed) + 0.05f * Mathf.Sin(a * 13f - seed * 0.7f);

        // The throat: rings from the rim down to its depths.
        for (int k = 0; k <= rings; k++)
        {
            float t = k / (float)rings;
            float shrink = 1f - Mathf.Pow(t, 0.8f) * 0.92f;
            float z = Mathf.Lerp(rimZ, deepZ, 1f - (1f - t) * (1f - t));
            for (int i = 0; i < sides; i++)
            {
                float a = i * Mathf.PI * 2f / sides;
                float fold = Mathf.Lerp(Fold(a), 1f, t * 0.5f);
                verts.Add(new Vector3(Mathf.Cos(a) * rx * shrink * fold, Mathf.Sin(a) * ry * shrink * fold, z));
                var c = flesh * (1f - 0.7f * t);
                c.a = Mathf.Pow(t, 1.6f);
                colors.Add(c);
            }
        }
        for (int k = 0; k < rings; k++)
            for (int i = 0; i < sides; i++)
            {
                int a = k * sides + i, b = k * sides + (i + 1) % sides, c = a + sides, d = b + sides;
                tris.Add(a); tris.Add(c); tris.Add(b);
                tris.Add(b); tris.Add(c); tris.Add(d);
            }
        int deep = verts.Count;
        verts.Add(new Vector3(0f, 0f, deepZ - 0.004f));
        colors.Add(new Color(0f, 0f, 0f, 1f));
        for (int i = 0; i < sides; i++)
        {
            tris.Add(deep); tris.Add(rings * sides + (i + 1) % sides); tris.Add(rings * sides + i);
        }

        // The lips: a thick ring round the rim.
        const int tube = 6;
        int lipStart = verts.Count;
        for (int i = 0; i < sides; i++)
        {
            float a = i * Mathf.PI * 2f / sides;
            float fold = Fold(a);
            Vector3 dir = new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f);
            Vector3 centre = new Vector3(dir.x * (rx * fold + 0.004f), dir.y * (ry * fold + 0.004f), rimZ);
            for (int j = 0; j < tube; j++)
            {
                float b = j * Mathf.PI * 2f / tube;
                float thick = 0.006f + 0.002f * Mathf.Sin(a * 5f + seed);
                verts.Add(centre + (dir * Mathf.Cos(b) + Vector3.forward * Mathf.Sin(b)) * thick);
                colors.Add(new Color(0.035f, 0.004f, 0.004f, 0.04f));
            }
        }
        for (int i = 0; i < sides; i++)
            for (int j = 0; j < tube; j++)
            {
                int a = lipStart + i * tube + j, b = lipStart + i * tube + (j + 1) % tube;
                int c = lipStart + ((i + 1) % sides) * tube + j, d = lipStart + ((i + 1) % sides) * tube + (j + 1) % tube;
                tris.Add(a); tris.Add(c); tris.Add(b);
                tris.Add(b); tris.Add(c); tris.Add(d);
            }

        // The fangs: top and bottom rows, longest toward the middle of each, every one a little different.
        var root = new Color(0.1f, 0.085f, 0.07f, 0.05f);
        var tip = new Color(0.5f, 0.46f, 0.38f, 0.12f);
        foreach (var (firstDeg, lastDeg, count) in new[] { (25f, 155f, 9), (205f, 335f, 8) })
            for (int n = 0; n < count; n++)
            {
                float deg = Mathf.Lerp(firstDeg, lastDeg, (n + Random.Range(-0.25f, 0.25f)) / (count - 1f));
                float a = deg * Mathf.Deg2Rad;
                float fold = Fold(a);
                Vector3 baseAt = new Vector3(Mathf.Cos(a) * rx * fold * 0.97f, Mathf.Sin(a) * ry * fold * 0.97f, rimZ - 0.001f);
                float middle = 1f - Mathf.Abs((deg - (firstDeg + lastDeg) * 0.5f) / ((lastDeg - firstDeg) * 0.5f));
                float length = Random.Range(0.016f, 0.024f) + 0.02f * middle;
                Vector3 inward = new Vector3(-baseAt.x, -baseAt.y * 0.8f, 0f).normalized;
                Vector3 dir = (inward * 0.8f + Vector3.back * 0.45f).normalized;
                AddFang(verts, colors, tris, baseAt, dir, length, Random.Range(0.0045f, 0.0065f), root, tip);
            }

        var mesh = new Mesh { name = "Scour Mouth" };
        mesh.SetVertices(verts);
        mesh.SetColors(colors);
        mesh.SetTriangles(tris, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }

    /// <summary>One fang: a four-sided spike from a broad root to a point, curving a little back as it goes.</summary>
    static void AddFang(List<Vector3> verts, List<Color> colors, List<int> tris, Vector3 at, Vector3 dir, float length, float width, Color root, Color tip)
    {
        Vector3 side = Vector3.Cross(dir, Vector3.forward).normalized;
        if (side.sqrMagnitude < 0.001f) side = Vector3.right;
        Vector3 up = Vector3.Cross(side, dir).normalized;
        const int segments = 3;
        int start = verts.Count;
        for (int k = 0; k <= segments; k++)
        {
            float t = k / (float)segments;
            Vector3 centre = at + dir * length * t + Vector3.back * length * 0.25f * t * t; // curving back down the throat
            float w = width * (1f - t) * (1f - 0.3f * t);
            var c = Color.Lerp(root, tip, t);
            verts.Add(centre + side * w); colors.Add(c);
            verts.Add(centre + up * w * 0.6f); colors.Add(c);
            verts.Add(centre - side * w); colors.Add(c);
            verts.Add(centre - up * w * 0.6f); colors.Add(c);
        }
        for (int k = 0; k < segments; k++)
            for (int j = 0; j < 4; j++)
            {
                int a = start + k * 4 + j, b = start + k * 4 + (j + 1) % 4, c = a + 4, d = b + 4;
                tris.Add(a); tris.Add(c); tris.Add(b);
                tris.Add(b); tris.Add(c); tris.Add(d);
            }
    }

    /// <summary>
    /// Not a grin: a mouth that drops open, far too wide and tall, a ring of thin fangs round its rim, and
    /// inside it a light, burning the colour of its eyes, glaring out of it: lighting what it faces, a faint
    /// cone of glare spilling out ahead. Closed it's nothing at all. (Its size follows mouthOpen.)
    /// </summary>
    void BuildMouth() => BuildMouth(new Vector3(0f, -0.005f, 0.11f));

    /// <summary>The mouth, at this point on the head (2.4m-figure units).</summary>
    void BuildMouth(Vector3 at)
    {
        mouth = Pivot(headPivot, "Mouth", at);
        // Throat, lips and fangs in one mesh (Horror/Throat): the light burns deep down its throat, the
        // fangs dark against it.
        Shader throat = Shader.Find("Horror/Throat");
        mouthGlowMat = throat != null ? new Material(throat) : MaterialFactory.NoFog(glowColor * 4f);
        mouthGlowMat.renderQueue = MaterialFactory.FogFreeQueue(2000);
        var mouthMesh = new GameObject("Mouth Mesh");
        mouthMesh.transform.SetParent(mouth, false);
        mouthMesh.transform.localScale = Vector3.one * scale;
        mouthMesh.AddComponent<MeshFilter>().sharedMesh = BuildMouthMesh();
        var mouthRenderer = mouthMesh.AddComponent<MeshRenderer>();
        mouthRenderer.sharedMaterial = mouthGlowMat;
        mouthRenderer.shadowCastingMode = ShadowCastingMode.Off;
        mouthRenderer.receiveShadows = false;
        mouth.localScale = new Vector3(1f, 0.001f, 1f);
        mouth.gameObject.SetActive(false);

        // The glare: a real light thrown out ahead of it, and a faint cone of it in the air.
        mouthLight = new GameObject("Mouth Glare").AddComponent<Light>();
        mouthLight.transform.SetParent(headPivot, false);
        mouthLight.transform.localPosition = (at + new Vector3(0f, 0f, 0.01f)) * scale;
        mouthLight.type = LightType.Spot;
        mouthLight.spotAngle = 55f;
        mouthLight.range = 14f;
        mouthLight.shadows = LightShadows.Soft; // through the gaps in a shack's planks, in slats of light
        mouthLight.shadowNearPlane = 0.3f; // (not blocked by its own head)
        mouthLight.enabled = false;
        Shader beam = Shader.Find("Horror/Beam");
        if (beam != null)
        {
            mouthBeamMat = new Material(beam);
            mouthBeamMat.SetFloat("_FadeOut", 0.65f); // (thinning to nothing, no hard end)
            var go = new GameObject("Mouth Glare Cone");
            go.transform.SetParent(headPivot, false);
            go.transform.localPosition = (at + new Vector3(0f, 0f, 0.02f)) * scale;
            go.AddComponent<MeshFilter>().sharedMesh = WatcherEvent.BuildBeamMesh();
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = mouthBeamMat;
            r.shadowCastingMode = ShadowCastingMode.Off;
            r.receiveShadows = false;
            mouthBeam = go.transform;
            go.SetActive(false);
        }
        // Where the glare lands: a bright, soft pool on the ground.
        Shader glow = Shader.Find("Horror/Glow");
        if (glow != null)
        {
            mouthPoolMat = new Material(glow);
            var pool = GameObject.CreatePrimitive(PrimitiveType.Quad);
            pool.name = "Mouth Glare Pool";
            Destroy(pool.GetComponent<Collider>());
            var pr = pool.GetComponent<Renderer>();
            pr.sharedMaterial = mouthPoolMat;
            pr.shadowCastingMode = ShadowCastingMode.Off;
            pr.receiveShadows = false;
            mouthPool = pool.transform;
            mouthPool.SetParent(transform, false);
            pool.SetActive(false);
        }
    }
    Material mouthPoolMat;
    Transform mouthPool;

    /// <summary>The mouth's light follows how far open it is, in the colour its eyes are burning.</summary>
    void UpdateMouthGlare()
    {
        float open = Mathf.Clamp01(mouthOpen);
        bool shown = open > 0.05f && bodyRenderers != null && bodyRenderers.Length > 0 && bodyRenderers[0].enabled;
        float flicker = 0.85f + 0.15f * Mathf.PerlinNoise(Time.time * 9f, 3.3f);
        if (mouthGlowMat != null) MaterialFactory.SetColor(mouthGlowMat, glowColor * (1f + 6f * open) * flicker);
        if (mouthLight != null)
        {
            mouthLight.enabled = shown;
            mouthLight.color = glowColor;
            mouthLight.intensity = 6f * open * open * flicker;
        }
        if (mouthBeam != null)
        {
            if (mouthBeam.gameObject.activeSelf != shown) mouthBeam.gameObject.SetActive(shown);
            float len = 3.5f * open;
            if (shown) len = WatcherEvent.BeamReach(mouthBeam.position, mouthBeam.forward, len); // stopped by a wall: the light comes on through its gaps
            mouthBeam.localScale = new Vector3(len * 0.45f, len * 0.45f, Mathf.Max(len, 0.01f)) / Mathf.Max(headPivot.lossyScale.x, 0.0001f);
            mouthBeamMat.SetColor("_Color", new Color(glowColor.r, glowColor.g, glowColor.b, 0.12f * open * flicker));
        }
        if (mouthPool != null && mouthLight != null)
        {
            // The pool where the glare meets the ground (or whatever's in the way), sized to the cone there.
            Transform from = mouthLight.transform;
            RaycastHit ground = default;
            bool hit = shown && Physics.Raycast(from.position, from.forward, out ground, mouthLight.range, ~0, QueryTriggerInteraction.Ignore);
            if (mouthPool.gameObject.activeSelf != hit) mouthPool.gameObject.SetActive(hit);
            if (hit)
            {
                mouthPool.SetPositionAndRotation(ground.point + ground.normal * 0.03f, Quaternion.LookRotation(-ground.normal));
                float radius = ground.distance * Mathf.Tan(mouthLight.spotAngle * 0.5f * Mathf.Deg2Rad);
                mouthPool.localScale = Vector3.one * Mathf.Max(0.4f, radius * 1.6f) / Mathf.Max(transform.lossyScale.x, 0.0001f);
                float fade = 1f - Mathf.Clamp01(ground.distance / mouthLight.range);
                mouthPoolMat.SetColor("_Color", glowColor * (1.4f * open * flicker * (0.4f + 0.6f * fade)));
            }
        }
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
        go.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.On; // it throws a shadow in your light

        // It's a ghost: no collisions with the player or trees.
        var col = go.GetComponent<Collider>();
        if (Application.isPlaying) Destroy(col);
        else DestroyImmediate(col);
        return go;
    }
}
