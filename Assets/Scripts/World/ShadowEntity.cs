using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// A tall pitch-black silhouette that materializes in the distance and stands watching the player.
///
/// As the player approaches it starts to vibrate and its eyes widen, while the player slows down
/// until they can't step any closer. Then it builds to a crescendo (violent shaking, stretching,
/// flaring eyes, camera shake), "poofs" into a burst of black fragments, and after a while
/// rematerializes somewhere in the distance with blazing eyes that slowly dim.
///
/// The body is unlit black, so the flashlight doesn't light it: it shows as a solid shadow
/// against lit trees and ground. DarkForestGenerator spawns one automatically.
/// Eyes use HDR colors, so they glow if your camera/volume has Bloom (URP's default volume does).
/// </summary>
public class ShadowEntity : MonoBehaviour
{
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

    [Header("Look")]
    public float height = 2.4f;

    [Header("Audio (optional)")]
    public AudioClip materializeSound;
    public AudioClip crescendoSound;
    public AudioClip poofSound;
    [Range(0f, 1f)] public float volume = 0.8f;

    public enum State { Hidden, Materializing, Watching, Crescendo }
    public State CurrentState { get; private set; }

    FirstPersonController player;
    DarkForestGenerator generator;
    AudioSource audioSource;

    Transform visual;
    Renderer[] bodyRenderers;
    Transform leftEye, rightEye;
    Vector3 eyeBaseScale;
    Material eyeMat;
    Light eyeLight;
    float scale; // height / 2.4, proportions below are for a 2.4m figure

    float timer;
    float eyeFlare; // 1 right after spawning, fades to 0

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

        audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.playOnAwake = false;
        audioSource.spatialBlend = 1f;
        audioSource.maxDistance = 50f;
    }

    void Start()
    {
        generator = FindAnyObjectByType<DarkForestGenerator>();
        Hide(initialHideTime);
    }

    void OnDisable()
    {
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
        }
    }

    // ---------------------------------------------------------------- States

    void Hide(float duration)
    {
        CurrentState = State.Hidden;
        timer = duration;
        SetVisible(false);
        ReleasePlayer();
    }

    void StartMaterialize()
    {
        transform.position = PickSpawnPoint();
        FacePlayer();
        ResetVisual();

        CurrentState = State.Materializing;
        timer = materializeTime;
        StartFragments(converge: true);
        PlaySound(materializeSound);
    }

    void FinishMaterialize()
    {
        SetVisible(true);
        eyeFlare = 1f;
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

        if (player != null) player.SetApproachLimit(transform.position, reactDistance, stopDistance);

        eyeFlare = Mathf.MoveTowards(eyeFlare, 0f, Time.deltaTime / Mathf.Max(eyeDimTime, 0.01f));

        // 0 when the player is far away, 1 when they're at the stop distance.
        float closeness = Mathf.Clamp01(Mathf.InverseLerp(reactDistance, stopDistance, dist));

        Vibrate(maxVibration * closeness);
        float brightness = Mathf.Lerp(eyeIntensity, spawnEyeIntensity, eyeFlare) + closeness * eyeIntensity * 2f;
        SetEyes(Mathf.Lerp(1f, maxEyeWiden, closeness), brightness);

        if (dist <= stopDistance + 0.15f)
        {
            CurrentState = State.Crescendo;
            timer = 0f;
            PlaySound(crescendoSound);
        }
    }

    void UpdateCrescendo()
    {
        FacePlayer();
        if (player != null) player.SetApproachLimit(transform.position, reactDistance, stopDistance);

        timer += Time.deltaTime;
        float c = Mathf.Clamp01(timer / Mathf.Max(crescendoTime, 0.01f));
        float e = c * c; // ease in: slow build, violent finish

        Vibrate(Mathf.Lerp(maxVibration, crescendoVibration, e));
        visual.localScale = new Vector3(1f - 0.15f * e, 1f + 0.2f * e, 1f - 0.15f * e);

        float flicker = 1f + Random.Range(-0.3f, 0.3f) * e;
        SetEyes(Mathf.Lerp(maxEyeWiden, maxEyeWiden * 1.8f, e),
                Mathf.Lerp(eyeIntensity * 3f, spawnEyeIntensity * 1.5f, e) * flicker);

        if (player != null) player.cameraShake = maxCameraShake * e;

        if (c >= 1f) Poof();
    }

    void Poof()
    {
        StartFragments(converge: false);
        PlaySound(poofSound);
        ResetVisual();
        Hide(Random.Range(minRespawnDelay, maxRespawnDelay));
    }

    void ReleasePlayer()
    {
        if (player == null) return;
        player.ClearApproachLimit();
        player.cameraShake = 0f;
    }

    // ---------------------------------------------------------------- Helpers

    Vector3 PickSpawnPoint()
    {
        Vector3 forward = target.forward;
        forward.y = 0f;
        if (forward.sqrMagnitude < 0.001f) forward = Vector3.forward;
        forward = Quaternion.Euler(0f, Random.Range(-spawnViewAngle, spawnViewAngle), 0f) * forward.normalized;

        Vector3 p = target.position + forward * Random.Range(minSpawnDistance, maxSpawnDistance);

        // Keep it inside the generated forest.
        if (generator != null)
        {
            float half = generator.areaSize * 0.5f - 3f;
            Vector3 center = generator.transform.position;
            p.x = Mathf.Clamp(p.x, center.x - half, center.x + half);
            p.z = Mathf.Clamp(p.z, center.z - half, center.z + half);
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

        Color c = eyeColor * brightness;
        if (eyeMat.HasProperty("_BaseColor")) eyeMat.SetColor("_BaseColor", c);
        if (eyeMat.HasProperty("_Color")) eyeMat.SetColor("_Color", c);

        eyeLight.intensity = brightness * eyeLightStrength;
    }

    void SetVisible(bool visible)
    {
        foreach (var r in bodyRenderers) r.enabled = visible;
        eyeLight.enabled = visible;
    }

    void PlaySound(AudioClip clip)
    {
        if (clip != null) audioSource.PlayOneShot(clip, volume);
    }

    // ---------------------------------------------------------------- Fragments

    void BuildFragments()
    {
        fragmentRoot = new GameObject("Shadow Entity Fragments").transform;

        Material black = MakeUnlitMaterial(Color.black);
        Material ember = MakeUnlitMaterial(eyeColor * 4f);

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

        Material black = MakeUnlitMaterial(Color.black);

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

        eyeMat = MakeUnlitMaterial(eyeColor * eyeIntensity);
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

    static Material MakeUnlitMaterial(Color color)
    {
        bool usingSrp = GraphicsSettings.currentRenderPipeline != null;
        Shader shader = Shader.Find(usingSrp ? "Universal Render Pipeline/Unlit" : "Unlit/Color");
        if (shader == null) shader = Shader.Find("Unlit/Color");

        var mat = new Material(shader);
        if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
        if (mat.HasProperty("_Color")) mat.SetColor("_Color", color);
        return mat;
    }
}
