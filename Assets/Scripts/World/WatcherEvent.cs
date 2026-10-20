using UnityEngine;

/// <summary>
/// "The Watcher": once the player has lit enough lamps, a giant red eye slowly opens in the sky,
/// stares down at them, and closes. The world takes on a red haze. About ten seconds later there's
/// rumbling in the distance, and a colossal version of the entity, legs like skyscrapers, starts
/// walking toward the player, very slowly, each step a ground-shaking boom. If it reaches them, it's over.
/// The watchtower floodlights drive it back to where it started.
///
/// Uses the fog-ignoring shader so the eye and the giant read through the fog.
/// DarkForestGenerator adds this automatically; GameOverScreen resets it.
/// </summary>
public class WatcherEvent : MonoBehaviour
{
    public static WatcherEvent Instance { get; private set; }

    [Header("Trigger")]
    [Tooltip("Number of lamps lit (this run) that wakes the Watcher.")]
    public int lampsToTrigger = 5;

    [Header("Eye")]
    public float eyeOpenTime = 4f;
    public float eyeWatchTime = 8f;
    public float eyeCloseTime = 3f;
    [Tooltip("How high in the sky the eye is, in degrees above the horizon.")]
    public float eyeElevation = 50f;
    [Tooltip("Apparent width of the eye.")]
    public float eyeSize = 14f;
    [Tooltip("Seconds between the eye closing and the giant appearing.")]
    public float giantDelay = 10f;

    [Header("Giant")]
    public float giantHeight = 660f;
    [Tooltip("Scaled with its height, so it still fills the same part of the sky when it appears.")]
    public float giantStartDistance = 2100f;
    [Tooltip("Metres per second it closes in. Scaled with its height, so it still takes about 4 minutes to arrive.")]
    public float giantSpeed = 7.5f;
    [Tooltip("Seconds per full stride (two steps). Longer legs, slower steps.")]
    public float strideTime = 12f;
    [Tooltip("If it gets this close, it takes the player.")]
    public float giantCatchDistance = 135f;

    [Header("Atmosphere")]
    public Color redFog = new Color(0.11f, 0.015f, 0.015f);
    public Color redAmbient = new Color(0.12f, 0.03f, 0.03f);

    enum Phase { Waiting, EyeOpening, EyeWatching, EyeClosing, Pause, GiantWalking, Done }

    Phase phase = Phase.Waiting;
    float timer;
    float redness;           // 0 normal sky, 1 full red haze
    bool savedAtmosphere;
    Color baseFog, baseAmbient;
    float baseFarClip = -1f;

    Transform eye;
    Material eyeMaterial;
    Vector2 gaze, gazeTarget;
    float saccadeTimer;
    Vector3 lastPlayerPosition;
    Vector3 eyeDirection;

    Transform giant, giantHead;
    Transform[] giantLegs = new Transform[2], giantKnees = new Transform[2], giantArms = new Transform[2];
    Vector3 giantBearing;    // direction from the player to the giant
    float giantDistance, stridePhase, shake;
    bool leftFootDown;

    AudioSource audioSource;
    FirstPersonController player;

    void Awake()
    {
        Instance = this;
        audioSource = ProceduralAudio.AddSource(gameObject, null, false, false, 1f);
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    void Update()
    {
        if (player == null)
        {
            player = FindAnyObjectByType<FirstPersonController>();
            if (player == null) return;
        }
        float dt = Time.deltaTime;

        switch (phase)
        {
            case Phase.Waiting:
                if (Lamp.TimesLit >= lampsToTrigger && !GameOverScreen.IsActive) OpenEye();
                break;

            case Phase.EyeOpening:
                timer += dt;
                SetEyeOpen(Mathf.SmoothStep(0f, 1f, timer / eyeOpenTime));
                if (timer >= eyeOpenTime) { phase = Phase.EyeWatching; timer = 0f; }
                break;

            case Phase.EyeWatching:
                timer += dt;
                SetEyeOpen(1f);
                if (timer >= eyeWatchTime) { phase = Phase.EyeClosing; timer = 0f; }
                break;

            case Phase.EyeClosing:
                timer += dt;
                SetEyeOpen(1f - Mathf.SmoothStep(0f, 1f, timer / eyeCloseTime));
                if (timer >= eyeCloseTime)
                {
                    eye.gameObject.SetActive(false);
                    phase = Phase.Pause;
                    timer = 0f;
                }
                break;

            case Phase.Pause:
                timer += dt;
                if (timer >= giantDelay) SummonGiant();
                break;

            case Phase.GiantWalking:
                UpdateGiant(dt);
                break;
        }

        // The sky turns red as the eye opens, and stays that way while the giant walks.
        float targetRed = phase == Phase.Waiting || phase == Phase.Done ? 0f : 1f;
        redness = Mathf.MoveTowards(redness, targetRed, dt / 5f);
        ApplyAtmosphere();

        if (shake > 0f)
        {
            player.AddCameraShake(shake);
            shake = Mathf.MoveTowards(shake, 0f, dt * 1.5f);
        }
    }

    void LateUpdate()
    {
        var cam = Camera.main;
        if (cam == null) return;

        // The eye is drawn a short way from the camera, always in the same direction: infinitely far away.
        if (eye != null && eye.gameObject.activeSelf)
        {
            eye.position = cam.transform.position + eyeDirection * 25f;
            eye.rotation = Quaternion.LookRotation(eyeDirection);

            // It studies the player: quick darting glances, then it follows the way they move.
            float dt = Time.deltaTime;
            Vector3 moved = player.transform.position - lastPlayerPosition;
            lastPlayerPosition = player.transform.position;
            Vector3 right = Vector3.Cross(Vector3.up, eyeDirection).normalized;
            float follow = Mathf.Clamp(Vector3.Dot(moved, right) / Mathf.Max(dt, 0.001f) * 0.02f, -0.06f, 0.06f);

            saccadeTimer -= dt;
            if (saccadeTimer <= 0f)
            {
                saccadeTimer = Random.Range(0.4f, 1.8f);
                gazeTarget = new Vector2(Random.Range(-0.07f, 0.07f), Random.Range(-0.04f, 0.04f));
            }
            gaze = Vector2.Lerp(gaze, gazeTarget + new Vector2(-follow, 0f), 1f - Mathf.Exp(-18f * dt)); // eyes jump, they don't glide
            eyeMaterial.SetVector("_Look", new Vector4(gaze.x, gaze.y, 0f, 0f));

            // The slit pupil narrows and widens.
            float t = Time.time;
            eyeMaterial.SetVector("_Pupil", new Vector4(0.06f + 0.04f * (0.5f + 0.5f * Mathf.Sin(t * 0.8f)), 0.32f, 0f, 0f));
        }
    }

    // ---------------------------------------------------------------- Eye

    void OpenEye()
    {
        Vector3 forward = Camera.main != null ? Camera.main.transform.forward : player.transform.forward;
        forward.y = 0f;
        forward = forward.sqrMagnitude > 0.001f ? forward.normalized : Vector3.forward;
        Vector3 axis = Vector3.Cross(Vector3.up, forward);
        eyeDirection = Quaternion.AngleAxis(-eyeElevation, axis) * forward;

        if (eye == null) BuildEye();
        eye.gameObject.SetActive(true);
        lastPlayerPosition = player.transform.position;
        SetEyeOpen(0f);

        audioSource.PlayOneShot(ProceduralAudio.EyeOpen, 1f);
        phase = Phase.EyeOpening;
        timer = 0f;
    }

    void SetEyeOpen(float open)
    {
        eyeMaterial.SetFloat("_Open", open);
    }

    void BuildEye()
    {
        // One quad, drawn behind the rest of the world, with the eye shader doing the lids, gaze and pupil.
        var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
        go.name = "Sky Eye";
        go.transform.SetParent(transform, false);
        Destroy(go.GetComponent<Collider>());
        eye = go.transform;
        eye.localScale = Vector3.one * eyeSize;

        Shader shader = Shader.Find("Horror/Sky Eye");
        eyeMaterial = shader != null ? new Material(shader) : MaterialFactory.NoFog(new Color(1f, 0.1f, 0.05f), 1000, false);
        if (eyeMaterial.HasProperty("_MainTex")) eyeMaterial.mainTexture = ProceduralTextures.SkyEye;
        if (eyeMaterial.HasProperty("_Color")) eyeMaterial.SetColor("_Color", new Color(2.2f, 1.6f, 1.6f, 1f)); // HDR: glows with Bloom
        eyeMaterial.renderQueue = 1000;

        var renderer = go.GetComponent<Renderer>();
        renderer.sharedMaterial = eyeMaterial;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;
    }

    // ---------------------------------------------------------------- Giant

    void SummonGiant()
    {
        if (giant == null) BuildGiant();
        giant.gameObject.SetActive(true);

        // It comes from where the eye was.
        giantBearing = eyeDirection;
        giantBearing.y = 0f;
        giantBearing.Normalize();
        giantDistance = giantStartDistance;
        stridePhase = 0f;

        foreach (var cam in Camera.allCameras)
        {
            if (baseFarClip < 0f) baseFarClip = cam.farClipPlane;
            cam.farClipPlane = Mathf.Max(cam.farClipPlane, giantStartDistance * 2f);
        }

        audioSource.PlayOneShot(ProceduralAudio.DistantRumble, 1f);
        phase = Phase.GiantWalking;
    }

    /// <summary>Drives the giant back to where it started (the watchtower floodlights do this).</summary>
    public void PushBack()
    {
        if (phase == Phase.GiantWalking) giantDistance = giantStartDistance;
    }

    void UpdateGiant(float dt)
    {
        giantDistance = Mathf.Max(0f, giantDistance - giantSpeed * dt);

        Vector3 p = player.transform.position + giantBearing * giantDistance;
        var generator = FindAnyObjectByType<DarkForestGenerator>();
        p.y = generator != null ? generator.GetGroundHeight(p.x, p.z) : player.transform.position.y;
        giant.SetPositionAndRotation(p, Quaternion.LookRotation(-giantBearing));

        // Slow, heavy stride: legs swing in opposite directions, knees bend on the forward swing.
        stridePhase += dt * Mathf.PI * 2f / strideTime;
        float swing = Mathf.Sin(stridePhase);
        for (int i = 0; i < 2; i++)
        {
            float s = i == 0 ? swing : -swing;
            giantLegs[i].localRotation = Quaternion.Euler(s * 18f, 0f, 0f);
            giantKnees[i].localRotation = Quaternion.Euler(-Mathf.Max(0f, s) * 25f, 0f, 0f);
            giantArms[i].localRotation = Quaternion.Euler(-s * 10f, 0f, 0f);
        }
        giant.GetChild(0).localPosition = Vector3.up * (Mathf.Abs(Mathf.Cos(stridePhase)) * 0.03f * giantHeight / 2.4f);

        // Head turns to look down at the player.
        Vector3 toPlayer = player.transform.position - giantHead.position;
        giantHead.rotation = Quaternion.Slerp(giantHead.rotation, Quaternion.LookRotation(toPlayer), 1f - Mathf.Exp(-0.5f * dt));

        // A footfall every time a foot comes down.
        bool leftDown = swing > 0f;
        if (leftDown != leftFootDown)
        {
            leftFootDown = leftDown;
            float closeness = Mathf.InverseLerp(giantStartDistance, giantCatchDistance, giantDistance);
            audioSource.pitch = Random.Range(0.9f, 1.05f);
            audioSource.PlayOneShot(ProceduralAudio.GiantStep, Mathf.Lerp(0.35f, 1f, closeness));
            shake = Mathf.Lerp(0.3f, 3f, closeness);
        }

        if (giantDistance <= giantCatchDistance)
        {
            phase = Phase.Done;
            shake = 4f;
            GameOverScreen.Show();
        }
    }

    void BuildGiant()
    {
        giant = new GameObject("The Watcher").transform;
        giant.SetParent(transform, false);
        var body = new GameObject("Body").transform;
        body.SetParent(giant, false);

        float s = giantHeight / 2.4f; // proportions are for a 2.4m figure, like the entities
        var black = MaterialFactory.NoFog(new Color(0.01f, 0.005f, 0.005f));
        var eyes = MaterialFactory.NoFog(new Color(4f, 0.2f, 0.05f));

        for (int i = 0; i < 2; i++)
        {
            float side = i == 0 ? -1f : 1f;

            // Legs: a thigh from the hip, a knee joint, a shin down to splayed toes.
            var hip = Pivot(body, new Vector3(side * 0.11f, 1.12f, -0.02f) * s);
            Bone(hip, black, Vector3.zero, new Vector3(side * 0.04f, -0.5f, 0.16f) * s, 0.09f * s);
            var knee = Pivot(hip, new Vector3(side * 0.04f, -0.5f, 0.16f) * s);
            Bone(knee, black, Vector3.zero, new Vector3(-side * 0.02f, -0.48f, -0.24f) * s, 0.06f * s);
            for (int toe = -1; toe <= 1; toe++)
                Bone(knee, black, new Vector3(-side * 0.02f, -0.48f, -0.24f) * s, new Vector3(-side * 0.02f + toe * 0.05f, -0.62f, 0.02f) * s, 0.025f * s);
            giantLegs[i] = hip;
            giantKnees[i] = knee;

            // Arms hanging past the knees, with long fingers.
            var shoulder = Pivot(body, new Vector3(side * 0.26f, 1.9f, 0.02f) * s);
            Bone(shoulder, black, Vector3.zero, new Vector3(side * 0.12f, -0.6f, -0.16f) * s, 0.06f * s);
            Bone(shoulder, black, new Vector3(side * 0.12f, -0.6f, -0.16f) * s, new Vector3(side * 0.06f, -1.22f, 0.1f) * s, 0.045f * s);
            for (int f = 0; f < 3; f++)
                Bone(shoulder, black, new Vector3(side * 0.06f, -1.22f, 0.1f) * s, new Vector3(side * 0.06f + (f - 1) * 0.04f, -1.5f, 0.18f) * s, 0.015f * s);
            giantArms[i] = shoulder;
        }

        // Hunched spine, chest, shoulders, neck.
        Bone(body, black, new Vector3(0f, 1.12f, -0.03f) * s, new Vector3(0f, 1.5f, -0.12f) * s, 0.09f * s);
        Bone(body, black, new Vector3(0f, 1.5f, -0.12f) * s, new Vector3(0f, 1.95f, 0.1f) * s, 0.09f * s);
        Bone(body, black, new Vector3(-0.26f, 1.9f, 0.02f) * s, new Vector3(0.26f, 1.9f, 0.02f) * s, 0.05f * s);
        var chest = Part(body, black, PrimitiveType.Capsule, new Vector3(0f, 1.66f, -0.02f) * s, new Vector3(0.3f, 0.22f, 0.17f) * s);
        chest.localRotation = Quaternion.Euler(18f, 0f, 0f);
        Bone(body, black, new Vector3(0f, 1.95f, 0.1f) * s, new Vector3(0f, 2.12f, 0.24f) * s, 0.06f * s);

        // Head with two huge glowing eyes.
        giantHead = Pivot(body, new Vector3(0f, 2.12f, 0.24f) * s);
        Part(giantHead, black, PrimitiveType.Sphere, new Vector3(0f, 0.1f, 0.02f) * s, new Vector3(0.2f, 0.3f, 0.22f) * s);
        for (int e = -1; e <= 1; e += 2)
            Part(giantHead, eyes, PrimitiveType.Sphere, new Vector3(e * 0.05f, 0.12f, 0.125f) * s, new Vector3(0.04f, 0.03f, 0.02f) * s);

        giant.gameObject.SetActive(false);
    }

    Transform Pivot(Transform parent, Vector3 localPos)
    {
        var t = new GameObject("Joint").transform;
        t.SetParent(parent, false);
        t.localPosition = localPos;
        return t;
    }

    void Bone(Transform parent, Material mat, Vector3 a, Vector3 b, float thickness)
    {
        Vector3 d = b - a;
        var bone = Part(parent, mat, PrimitiveType.Cylinder, (a + b) * 0.5f, new Vector3(thickness, d.magnitude * 0.5f, thickness));
        bone.localRotation = Quaternion.FromToRotation(Vector3.up, d);
    }

    Transform Part(Transform parent, Material mat, PrimitiveType type, Vector3 pos, Vector3 scale)
    {
        var go = GameObject.CreatePrimitive(type);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = pos;
        go.transform.localScale = scale;
        var renderer = go.GetComponent<Renderer>();
        renderer.sharedMaterial = mat;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        Destroy(go.GetComponent<Collider>());
        return go.transform;
    }

    // ---------------------------------------------------------------- Atmosphere & reset

    void ApplyAtmosphere()
    {
        if (!savedAtmosphere)
        {
            if (redness <= 0f) return;
            baseFog = RenderSettings.fogColor;
            baseAmbient = RenderSettings.ambientLight;
            savedAtmosphere = true;
        }

        Color fog = Color.Lerp(baseFog, redFog, redness);
        RenderSettings.fogColor = fog;
        RenderSettings.ambientLight = Color.Lerp(baseAmbient, redAmbient, redness);
        foreach (var cam in Camera.allCameras) cam.backgroundColor = fog;
    }

    /// <summary>Puts everything back for a new run (used by GameOverScreen).</summary>
    public void ResetEvent()
    {
        phase = Phase.Waiting;
        timer = 0f;
        shake = 0f;
        if (eye != null) eye.gameObject.SetActive(false);
        if (giant != null) giant.gameObject.SetActive(false);

        redness = 0f;
        if (savedAtmosphere)
        {
            RenderSettings.fogColor = baseFog;
            RenderSettings.ambientLight = baseAmbient;
            foreach (var cam in Camera.allCameras)
            {
                cam.backgroundColor = baseFog;
                if (baseFarClip > 0f) cam.farClipPlane = baseFarClip;
            }
        }
    }
}
