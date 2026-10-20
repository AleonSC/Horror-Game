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

    public static ShadowEntity Spawn() => new GameObject("Shadow Entity").AddComponent<ShadowEntity>();

    [Tooltip("Who to watch. Found automatically if left empty.")]
    public Transform target;

    [Header("Spawning")]
    [Tooltip("Seconds before it first appears.")]
    public float initialHideTime = 5f;
    public float minSpawnDistance = 22f;
    public float maxSpawnDistance = 32f;
    [Tooltip("Spawns within this many degrees of where the player is facing, so it's likely to be seen.")]
    public float spawnViewAngle = 70f;
    public float minRespawnDelay = 4f;
    public float maxRespawnDelay = 9f;
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

    [Header("Crescendo & Poof")]
    public float crescendoTime = 2.5f;
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

    public enum State { Hidden, Materializing, Watching, Crescendo, Killing }
    public State CurrentState { get; private set; }
    /// <summary>
    /// How far its hunt has got, 0 to 1: how close it has worked its way to the player, and how much it has grown
    /// stalking them in the dark. Only goes up; starts over when it's driven off. Its eyes brighten with it.
    /// </summary>
    public float HuntProgress { get; private set; }

    public bool IsVisible => CurrentState == State.Watching || CurrentState == State.Crescendo || CurrentState == State.Killing;

    FirstPersonController player;
    DarkForestGenerator generator;
    AudioSource sfx, whisper, advanceSource;
    Flashlight flashlight;
    float growth = 1f;
    float circleDirection = 1f;
    float stepSoundTimer;

    Transform visual;
    Renderer[] bodyRenderers;
    Transform leftEye, rightEye;
    Transform headPivot, leftArm, rightArm;
    readonly List<Transform> fingers = new List<Transform>();
    readonly List<Quaternion> fingerRest = new List<Quaternion>();
    Quaternion headTarget = Quaternion.identity;
    float twitchTimer;
    Vector3 eyeBaseScale;
    Material eyeMat;
    Light eyeLight;
    float scale; // height / 2.4, proportions below are for a 2.4m figure

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
        }
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
    }

    void StartMaterialize()
    {
        if (GameOverScreen.IsActive) { timer = 1f; return; }

        transform.position = PickSpawnPoint();
        FacePlayer();
        ResetVisual();

        CurrentState = State.Materializing;
        timer = materializeTime;
        StartFragments(converge: true);
        PlaySound(materializeSound != null ? materializeSound : ProceduralAudio.Materialize);
    }

    void FinishMaterialize()
    {
        SetVisible(true);
        eyeFlare = 1f;
        creepTimer = Random.Range(minCreepInterval, maxCreepInterval);
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
        bool advancing = false;

        if (playerSafe)
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
                Vector3 next = transform.position + toPlayer.normalized * (advanceSpeed * Time.deltaTime);
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
        SetEyes(Mathf.Lerp(1f, maxEyeWiden, closeness), brightness);
        whisper.volume = Mathf.Lerp(0.25f, 1f, closeness) * volume;

        float advanceTarget = advancing ? Mathf.Lerp(0.5f, 1f, closeness) * volume : 0f;
        advanceSource.volume = Mathf.MoveTowards(advanceSource.volume, advanceTarget, Time.deltaTime * 1.5f);
        advanceSource.pitch = Mathf.Lerp(0.9f, 1.3f, Mathf.InverseLerp(1f, maxGrowth, growth));

        if (!playerSafe)
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

    void StartCrescendo()
    {
        CurrentState = State.Crescendo;
        timer = 0f;
        PlaySound(crescendoSound != null ? crescendoSound : ProceduralAudio.Crescendo, 0f, crescendoVolume);
    }

    bool IsLitByFlashlight()
    {
        if (flashlight == null) flashlight = FindAnyObjectByType<Flashlight>();
        if (flashlight == null || !flashlight.IsOn) return false;

        Light beam = flashlight.flashlight;
        Vector3 chest = transform.position + Vector3.up * (1.4f * scale * growth);
        Vector3 toChest = chest - beam.transform.position;
        float dist = toChest.magnitude;

        if (dist > beam.range) return false;
        if (Vector3.Angle(beam.transform.forward, toChest) > beam.spotAngle * 0.5f + beamMargin) return false;
        // Trees in the way block the beam.
        if (Physics.Raycast(beam.transform.position, toChest / dist, dist - 0.3f, ~0, QueryTriggerInteraction.Ignore))
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
        creepTimer = Random.Range(minCreepInterval, maxCreepInterval);

        Vector3 toPlayer = target.position - transform.position;
        toPlayer.y = 0f;
        float dist = toPlayer.magnitude;
        if (dist < 0.01f) return;
        Vector3 dir = toPlayer / dist;

        if (dist - creepStep <= killDistance)
        {
            StartKill();
            return;
        }

        // Step closer, drifting a little to one side so it isn't a perfectly straight line.
        Vector3 side = Vector3.Cross(Vector3.up, dir) * Random.Range(-1f, 1f);
        Vector3 next = transform.position + dir * creepStep + side;
        next.y = GroundHeight(next);
        if (SafeZones.Contains(next)) return;

        transform.position = next;
        FacePlayer();
        PlaySound(creepSound != null ? creepSound : ProceduralAudio.Creep);
    }

    void UpdateCrescendo()
    {
        FacePlayer();
        if (player != null) player.SetApproachLimit(this, transform.position, reactDistance, stopDistance);

        timer += Time.deltaTime;
        float c = Mathf.Clamp01(timer / Mathf.Max(crescendoTime, 0.01f));
        float e = c * c; // ease in: slow build, violent finish

        Vibrate(Mathf.Lerp(maxVibration, crescendoVibration, e));
        visual.localScale = new Vector3(1f - 0.15f * e, 1f + 0.2f * e, 1f - 0.15f * e);

        float flicker = 1f + Random.Range(-0.3f, 0.3f) * e;
        SetEyes(Mathf.Lerp(maxEyeWiden, maxEyeWiden * 1.8f, e),
                Mathf.Lerp(eyeIntensity * 3f, spawnEyeIntensity * 1.5f, e) * flicker);
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
        creepTimer = Random.Range(minCreepInterval, maxCreepInterval);
    }

    void ReleasePlayer()
    {
        if (player == null) return;
        player.ClearApproachLimit(this);
    }

    // ---------------------------------------------------------------- Helpers

    bool IsPlayerLooking()
    {
        Transform cam = Camera.main != null ? Camera.main.transform : target;
        Vector3 head = transform.position + Vector3.up * (2f * scale * growth);
        Vector3 toHead = head - cam.position;

        if (Vector3.Angle(cam.forward, toHead) > lookAngle) return false;

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
            p = target.position + dir * (Random.Range(minSpawnDistance, maxSpawnDistance) * reach);

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

    void SetEyes(float widen, float brightness)
    {
        Vector3 s = new Vector3(eyeBaseScale.x * Mathf.Lerp(1f, widen, 0.5f), eyeBaseScale.y * widen, eyeBaseScale.z);
        leftEye.localScale = s;
        rightEye.localScale = s;
        MaterialFactory.SetColor(eyeMat, eyeColor * brightness);
        eyeLight.intensity = brightness * eyeLightStrength;
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
        visual = new GameObject("Silhouette").transform;
        visual.SetParent(transform, false);

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

        eyeMat = MaterialFactory.Unlit(eyeColor * eyeIntensity);
        eyeBaseScale = new Vector3(0.035f, 0.025f, 0.02f) * scale;
        leftEye = CreatePart(PrimitiveType.Sphere, eyeMat, headPivot).transform;
        leftEye.localPosition = new Vector3(-0.05f, 0.12f, 0.125f) * scale;
        rightEye = CreatePart(PrimitiveType.Sphere, eyeMat, headPivot).transform;
        rightEye.localPosition = new Vector3(0.05f, 0.12f, 0.125f) * scale;

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

    /// <summary>Unsettling idle motion: the head snaps to new angles, arms drift, fingers curl and uncurl.</summary>
    void UpdateTwitch(float dt)
    {
        twitchTimer -= dt;
        if (twitchTimer <= 0f)
        {
            twitchTimer = Random.Range(0.4f, 2.5f);
            headTarget = Quaternion.Euler(Random.Range(-15f, 20f), Random.Range(-30f, 30f), Random.Range(-40f, 40f));
        }
        headPivot.localRotation = Quaternion.Slerp(headPivot.localRotation, headTarget, 1f - Mathf.Exp(-25f * dt));

        float t = Time.time;
        leftArm.localRotation = Quaternion.Euler(Mathf.Sin(t * 0.7f + 1f) * 6f, 0f, Mathf.Sin(t * 0.5f) * 4f);
        rightArm.localRotation = Quaternion.Euler(Mathf.Sin(t * 0.6f + 2.5f) * 6f, 0f, Mathf.Sin(t * 0.45f + 1f) * 4f);
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
