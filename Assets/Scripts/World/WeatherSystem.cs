using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Rain and wind.
///
/// Wind: trees lean downwind and sway, with gusts that roll across the forest in waves.
/// Rain: streaks that follow the camera, slant with the wind, and splash where they land.
/// Audio: gusting wind, rustling leaves, creaking trunks in strong gusts, and layered rain.
///
/// Sways the trees DarkForestGenerator builds (its SwayPivots). DarkForestGenerator adds this
/// automatically. Leave the clip slots empty to use the built-in synthesized sounds.
/// </summary>
public class WeatherSystem : MonoBehaviour
{
    [Header("Wind")]
    [Range(0f, 1f)] public float windStrength = 0.6f;
    [Tooltip("Direction the wind blows towards, in degrees (0 = +Z).")]
    public float windDirection = 35f;
    [Tooltip("Slowly turn the wind direction over time (degrees per second).")]
    public float windDirectionDrift = 0.5f;
    [Tooltip("How far trees lean at full strength, in degrees.")]
    public float maxSwayAngle = 2.5f;
    [Tooltip("How fast gust waves travel across the forest.")]
    public float gustSpeed = 0.35f;
    [Tooltip("Only trees within this distance of the camera are animated.")]
    public float swayRadius = 70f;

    [Header("Rain")]
    [Range(0f, 1f)] public float rainIntensity = 0.7f;
    [Tooltip("Rain gently gets heavier and lighter over time by up to this much.")]
    [Range(0f, 1f)] public float rainVariation = 0.3f;
    public int maxDropsPerSecond = 3500;
    public float rainAreaSize = 30f;
    public float fallSpeed = 18f;
    [Tooltip("How much the wind pushes the rain sideways, in m/s at full strength.")]
    public float windPush = 7f;
    public Color rainColor = new Color(0.8f, 0.85f, 0.95f, 0.45f);
    [Tooltip("Lit rain only shows where light hits it (flashlight, lamps). Unlit rain is visible everywhere.")]
    public bool litRain = true;
    public bool splashes = true;

    [Header("Audio")]
    [Range(0f, 1f)] public float windVolume = 0.35f;
    [Range(0f, 1f)] public float leavesVolume = 0.3f;
    [Range(0f, 1f)] public float rainVolume = 0.5f;
    [Range(0f, 1f)] public float creakVolume = 0.6f;
    [Tooltip("Gust strength (0-1) above which trees may creak.")]
    public float creakThreshold = 0.65f;

    [Header("Clip Overrides (optional)")]
    public AudioClip windClip;
    public AudioClip leavesClip;
    public AudioClip rainClip;
    public AudioClip heavyRainClip;
    public AudioClip[] creakClips;

    /// <summary>Current wind including gusts at the listener, 0 to ~1.2.</summary>
    public float CurrentWind { get; private set; }
    public Vector3 WindVector => Quaternion.Euler(0f, windDirection, 0f) * Vector3.forward;

    DarkForestGenerator generator;
    readonly List<Transform> pivots = new List<Transform>();
    readonly List<Quaternion> baseRotations = new List<Quaternion>();

    ParticleSystem rain;
    AudioSource windSource, leavesSource, rainSource, heavyRainSource;
    float currentRain;
    float creakCooldown;

    void Start()
    {
        generator = FindAnyObjectByType<DarkForestGenerator>();

        windSource = StartLoop(windClip != null ? windClip : ProceduralAudio.Wind);
        leavesSource = StartLoop(leavesClip != null ? leavesClip : ProceduralAudio.Leaves);
        rainSource = StartLoop(rainClip != null ? rainClip : ProceduralAudio.Rain);
        heavyRainSource = StartLoop(heavyRainClip != null ? heavyRainClip : ProceduralAudio.RainHeavy);

        rain = BuildRain();
    }

    AudioSource StartLoop(AudioClip clip)
    {
        var src = ProceduralAudio.AddSource(gameObject, clip, true, false, 0f);
        src.time = Random.Range(0f, clip.length * 0.95f);
        src.Play();
        return src;
    }

    void Update()
    {
        var cam = Camera.main;
        if (cam == null) return;
        Vector3 listener = cam.transform.position;

        windDirection += windDirectionDrift * Time.deltaTime;
        Vector3 wind = WindVector;

        CurrentWind = windStrength * Gust(listener, wind);
        currentRain = rainIntensity * (1f - rainVariation * Mathf.PerlinNoise(Time.time * 0.02f, 7.3f));

        SwayTrees(listener, wind);
        UpdateRain(listener, wind);
        UpdateAudio(listener);
    }

    /// <summary>Gust strength at a point, 0.3 to ~1.2. Gusts travel downwind as waves.</summary>
    float Gust(Vector3 p, Vector3 wind)
    {
        float wave = Time.time * gustSpeed - Vector3.Dot(p, wind) * 0.03f;
        float slow = Mathf.PerlinNoise(wave, 0.37f);
        float fast = Mathf.PerlinNoise(wave * 3.1f, 4.1f);
        return 0.3f + slow * 0.7f + fast * 0.2f;
    }

    // ---------------------------------------------------------------- Trees

    void SwayTrees(Vector3 listener, Vector3 wind)
    {
        RefreshPivots();
        if (pivots.Count == 0) return;

        Vector3 axisWorld = Vector3.Cross(Vector3.up, wind); // leaning around this tilts trees downwind
        float radiusSq = swayRadius * swayRadius;
        float time = Time.time;

        for (int i = 0; i < pivots.Count; i++)
        {
            Transform pivot = pivots[i];
            if (pivot == null) continue;

            Vector3 p = pivot.position;
            if ((p - listener).sqrMagnitude > radiusSq) continue;

            // Stable per-tree randomness so neighbours don't move in lockstep.
            float seed = Mathf.Repeat(Mathf.Sin(p.x * 12.9898f + p.z * 78.233f) * 43758.5453f, 1f);

            float gust = Gust(p, wind);
            float lean = windStrength * maxSwayAngle * gust * 0.7f;
            float bob = Mathf.Sin(time * (1.1f + seed * 0.8f) + seed * 20f) * windStrength * maxSwayAngle * 0.3f * gust;

            Transform parent = pivot.parent;
            Vector3 axis = parent != null ? parent.InverseTransformDirection(axisWorld) : axisWorld;
            pivot.localRotation = Quaternion.AngleAxis(lean + bob, axis) * baseRotations[i];
        }
    }

    void RefreshPivots()
    {
        if (generator == null) generator = FindAnyObjectByType<DarkForestGenerator>();
        if (generator == null) return;

        var source = generator.SwayPivots;
        bool stale = source.Count != pivots.Count || (pivots.Count > 0 && pivots[0] != source[0]);
        if (!stale) return;

        pivots.Clear();
        baseRotations.Clear();
        foreach (var t in source)
        {
            pivots.Add(t);
            baseRotations.Add(t != null ? t.localRotation : Quaternion.identity);
        }
    }

    // ---------------------------------------------------------------- Rain

    ParticleSystem BuildRain()
    {
        var go = new GameObject("Rain");
        go.transform.SetParent(transform, false);
        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var main = ps.main;
        main.loop = true;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.9f, 1.3f);
        main.startSpeed = 0f;
        main.startSize = new ParticleSystem.MinMaxCurve(0.012f, 0.02f);
        main.startColor = rainColor;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = maxDropsPerSecond * 2;
        main.scalingMode = ParticleSystemScalingMode.World;

        var emission = ps.emission;
        emission.rateOverTime = maxDropsPerSecond * rainIntensity;

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = new Vector3(rainAreaSize, 0.1f, rainAreaSize);

        var velocity = ps.velocityOverLifetime;
        velocity.enabled = true;
        velocity.space = ParticleSystemSimulationSpace.World;

        // Drops die when they hit the ground, trees or rocks.
        var collision = ps.collision;
        collision.enabled = true;
        collision.type = ParticleSystemCollisionType.World;
        collision.mode = ParticleSystemCollisionMode.Collision3D;
        collision.quality = ParticleSystemCollisionQuality.Medium;
        collision.lifetimeLoss = 1f;
        collision.bounce = 0f;
        collision.radiusScale = 0.1f;
        collision.enableDynamicColliders = false;

        var renderer = go.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.Stretch;
        renderer.velocityScale = 0.04f;
        renderer.lengthScale = 1f;
        renderer.sharedMaterial = MaterialFactory.Particle(Color.white, litRain);
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;

        if (splashes)
        {
            var splash = BuildSplash(go.transform);
            var subEmitters = ps.subEmitters;
            subEmitters.enabled = true;
            subEmitters.AddSubEmitter(splash, ParticleSystemSubEmitterType.Collision, ParticleSystemSubEmitterProperties.InheritNothing);
        }

        ps.Play();
        return ps;
    }

    ParticleSystem BuildSplash(Transform parent)
    {
        var go = new GameObject("Rain Splash");
        go.transform.SetParent(parent, false);
        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var main = ps.main;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.12f, 0.25f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.6f, 1.5f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.02f, 0.05f);
        main.startColor = rainColor;
        main.gravityModifier = 1f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = maxDropsPerSecond * 2;

        var emission = ps.emission;
        emission.rateOverTime = 0f;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 2, 3) });

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Hemisphere;
        shape.radius = 0.03f;
        shape.rotation = new Vector3(-90f, 0f, 0f); // spray upward

        var renderer = go.GetComponent<ParticleSystemRenderer>();
        renderer.sharedMaterial = MaterialFactory.Particle(Color.white, litRain);
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        return ps;
    }

    void UpdateRain(Vector3 listener, Vector3 wind)
    {
        if (rain == null) return;

        Vector3 push = wind * (windPush * CurrentWind);

        // Spawn upwind so the slanted rain still lands around the player.
        float fallTime = 12f / fallSpeed;
        rain.transform.position = listener + Vector3.up * 12f - push * fallTime;

        var velocity = rain.velocityOverLifetime;
        velocity.x = new ParticleSystem.MinMaxCurve(push.x);
        velocity.y = new ParticleSystem.MinMaxCurve(-fallSpeed);
        velocity.z = new ParticleSystem.MinMaxCurve(push.z);

        var emission = rain.emission;
        emission.rateOverTime = maxDropsPerSecond * currentRain;
    }

    // ---------------------------------------------------------------- Audio

    void UpdateAudio(Vector3 listener)
    {
        float smoothing = Time.deltaTime * 0.8f;
        windSource.volume = Mathf.MoveTowards(windSource.volume, windVolume * Mathf.Clamp01(0.3f + CurrentWind * 0.8f), smoothing);
        windSource.pitch = Mathf.Lerp(0.85f, 1.15f, Mathf.Clamp01(CurrentWind));
        leavesSource.volume = Mathf.MoveTowards(leavesSource.volume, leavesVolume * Mathf.Clamp01(CurrentWind * CurrentWind), smoothing);
        rainSource.volume = Mathf.MoveTowards(rainSource.volume, rainVolume * Mathf.Clamp01(currentRain * 1.3f), smoothing);
        heavyRainSource.volume = Mathf.MoveTowards(heavyRainSource.volume, rainVolume * Mathf.Clamp01((currentRain - 0.3f) * 1.4f), smoothing);

        // Strong gusts make nearby trunks groan.
        creakCooldown -= Time.deltaTime;
        if (creakCooldown <= 0f && CurrentWind > creakThreshold && pivots.Count > 0)
        {
            creakCooldown = Random.Range(2f, 7f);
            Transform tree = NearbyTree(listener, 25f);
            if (tree != null)
            {
                AudioClip clip = creakClips != null && creakClips.Length > 0
                    ? creakClips[Random.Range(0, creakClips.Length)]
                    : ProceduralAudio.TreeCreak;
                ProceduralAudio.PlayAt(clip, tree.position + Vector3.up * 4f, creakVolume, Random.Range(0.7f, 1.2f), 5f);
            }
        }
    }

    Transform NearbyTree(Vector3 listener, float maxDistance)
    {
        // A few random picks is plenty; no need to search every tree.
        for (int i = 0; i < 20; i++)
        {
            Transform t = pivots[Random.Range(0, pivots.Count)];
            if (t != null && (t.position - listener).sqrMagnitude < maxDistance * maxDistance) return t;
        }
        return null;
    }
}
