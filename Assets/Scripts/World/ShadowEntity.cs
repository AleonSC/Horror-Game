using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// A tall pitch-black silhouette that materializes in the distance and watches the player.
///
/// - While the player isn't looking, it creeps a few metres closer every few seconds.
///   If a creep would bring it within kill distance, it takes the player (game over).
/// - If the player approaches, it vibrates and its eyes widen while the player slows down until
///   they can't get any closer. Then it builds to a crescendo, "poofs" and rematerializes far
///   away with blazing eyes. That's the only way to push it back.
/// - It can't enter or creep into a lit lamp's safe zone, and won't creep while the player is in one.
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

    [Header("Vibration")]
    public float maxVibration = 0.04f;
    public float crescendoVibration = 0.25f;

    [Header("Eyes")]
    public Color eyeColor = new Color(1f, 0.08f, 0.05f);
    [Tooltip("Normal eye brightness (HDR multiplier).")]
    public float eyeIntensity = 1f;
    [Tooltip("Eye brightness right after it materializes.")]
    public float spawnEyeIntensity = 12f;
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

    [Header("Audio")]
    [Range(0f, 1f)] public float volume = 0.9f;
    [Tooltip("Leave clips empty to use the built-in synthesized sounds.")]
    public AudioClip whisperLoop;
    public AudioClip materializeSound;
    public AudioClip creepSound;
    public AudioClip crescendoSound;
    public AudioClip poofSound;
    public AudioClip killSound;

    public enum State { Hidden, Materializing, Watching, Crescendo, Killing }
    public State CurrentState { get; private set; }
    public bool IsVisible => CurrentState == State.Watching || CurrentState == State.Crescendo || CurrentState == State.Killing;

    FirstPersonController player;
    DarkForestGenerator generator;
    AudioSource sfx, whisper;

    Transform visual;
    Renderer[] bodyRenderers;
    Transform leftEye, rightEye;
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
    }

    void Start()
    {
        generator = FindAnyObjectByType<DarkForestGenerator>();
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
        CurrentState = State.Hidden;
        timer = duration;
        SetVisible(false);
        whisper.Stop();
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
        if (Lamp.IsInSafeZone(transform.position))
        {
            Poof();
            return;
        }

        if (player != null) player.SetApproachLimit(this, transform.position, reactDistance, stopDistance);

        eyeFlare = Mathf.MoveTowards(eyeFlare, 0f, Time.deltaTime / Mathf.Max(eyeDimTime, 0.01f));

        // 0 when the player is far away, 1 when they're at the stop distance.
        float closeness = Mathf.Clamp01(Mathf.InverseLerp(reactDistance, stopDistance, dist));

        Vibrate(maxVibration * closeness);
        float brightness = Mathf.Lerp(eyeIntensity, spawnEyeIntensity, eyeFlare) + closeness * eyeIntensity * 2f;
        SetEyes(Mathf.Lerp(1f, maxEyeWiden, closeness), brightness);
        whisper.volume = Mathf.Lerp(0.25f, 1f, closeness) * volume;

        if (dist <= stopDistance + triggerMargin)
        {
            CurrentState = State.Crescendo;
            timer = 0f;
            PlaySound(crescendoSound != null ? crescendoSound : ProceduralAudio.Crescendo);
            return;
        }

        creepTimer -= Time.deltaTime;
        if (creepTimer <= 0f) TryCreep();
    }

    void TryCreep()
    {
        if (IsPlayerLooking() || Lamp.IsInSafeZone(target.position) || GameOverScreen.IsActive)
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
        if (Lamp.IsInSafeZone(next)) return;

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
        whisper.volume = volume;
        whisper.pitch = Mathf.Lerp(1f, 1.6f, e);

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
        PlaySound(killSound != null ? killSound : ProceduralAudio.Screech, 1f);
    }

    void UpdateKilling()
    {
        timer += Time.deltaTime;

        Vibrate(crescendoVibration * 0.6f);
        visual.localScale = new Vector3(0.85f, 1.2f, 0.85f);
        SetEyes(maxEyeWiden * 2f, spawnEyeIntensity * 2f * Random.Range(0.7f, 1.3f));
        whisper.volume = volume;

        // Force the camera to stare into its face.
        Transform cam = Camera.main != null ? Camera.main.transform : null;
        if (cam != null)
        {
            Vector3 head = transform.position + Vector3.up * 2.2f * scale;
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

    void ReleasePlayer()
    {
        if (player == null) return;
        player.ClearApproachLimit(this);
    }

    // ---------------------------------------------------------------- Helpers

    bool IsPlayerLooking()
    {
        Transform cam = Camera.main != null ? Camera.main.transform : target;
        Vector3 head = transform.position + Vector3.up * 2f * scale;
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
            p = target.position + dir * Random.Range(minSpawnDistance, maxSpawnDistance);

            // Keep it inside the generated forest.
            if (generator != null)
            {
                float half = generator.areaSize * 0.5f - 3f;
                Vector3 center = generator.transform.position;
                p.x = Mathf.Clamp(p.x, center.x - half, center.x + half);
                p.z = Mathf.Clamp(p.z, center.z - half, center.z + half);
            }

            if (!Lamp.IsInSafeZone(p)) break;
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

    void PlaySound(AudioClip clip, float pitch = 0f)
    {
        if (clip == null) return;
        sfx.pitch = pitch > 0f ? pitch : Random.Range(0.9f, 1.1f);
        sfx.PlayOneShot(clip, volume);
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

        // Long thin legs
        AddPart(PrimitiveType.Cylinder, black, new Vector3(-0.12f, 0.55f, 0f), new Vector3(0.12f, 0.55f, 0.12f), 0f);
        AddPart(PrimitiveType.Cylinder, black, new Vector3(0.12f, 0.55f, 0f), new Vector3(0.12f, 0.55f, 0.12f), 0f);
        // Narrow torso
        AddPart(PrimitiveType.Capsule, black, new Vector3(0f, 1.55f, 0f), new Vector3(0.42f, 0.5f, 0.25f), 0f);
        // Thin neck and small head, tilted slightly
        AddPart(PrimitiveType.Cylinder, black, new Vector3(0f, 2.05f, 0.02f), new Vector3(0.08f, 0.1f, 0.08f), 0f);
        AddPart(PrimitiveType.Sphere, black, new Vector3(0f, 2.22f, 0.04f), new Vector3(0.26f, 0.32f, 0.26f), 12f);
        // Arms that hang down past the knees
        AddPart(PrimitiveType.Cylinder, black, new Vector3(-0.3f, 1.25f, 0f), new Vector3(0.07f, 0.68f, 0.07f), -6f);
        AddPart(PrimitiveType.Cylinder, black, new Vector3(0.3f, 1.25f, 0f), new Vector3(0.07f, 0.68f, 0.07f), 6f);

        eyeMat = MaterialFactory.Unlit(eyeColor * eyeIntensity);
        eyeBaseScale = new Vector3(0.035f, 0.025f, 0.02f) * scale;
        leftEye = AddPart(PrimitiveType.Sphere, eyeMat, new Vector3(-0.055f, 2.25f, 0.18f), Vector3.one, 0f);
        rightEye = AddPart(PrimitiveType.Sphere, eyeMat, new Vector3(0.055f, 2.25f, 0.18f), Vector3.one, 0f);

        eyeLight = new GameObject("Eye Glow").AddComponent<Light>();
        eyeLight.transform.SetParent(visual, false);
        eyeLight.transform.localPosition = new Vector3(0f, 2.25f, 0.3f) * scale;
        eyeLight.type = LightType.Point;
        eyeLight.color = eyeColor;
        eyeLight.range = 6f;
        eyeLight.shadows = LightShadows.None;

        bodyRenderers = visual.GetComponentsInChildren<Renderer>();
        SetEyes(1f, eyeIntensity);
    }

    Transform AddPart(PrimitiveType type, Material mat, Vector3 pos, Vector3 partScale, float roll)
    {
        var go = CreatePart(type, mat, visual);
        go.transform.localPosition = pos * scale;
        go.transform.localScale = partScale * scale;
        go.transform.localRotation = Quaternion.Euler(0f, 0f, roll);
        return go.transform;
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
