using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Wind.
///
/// Trees and grass lean downwind and sway, with gusts that roll across the forest in waves.
/// Audio: gusting wind, rustling leaves, and creaking trunks in strong gusts.
///
/// Sways the trees and grass DarkForestGenerator builds (its SwayPivots and GrassPivots). DarkForestGenerator adds this
/// automatically. Leave the clip slots empty to use the built-in synthesized sounds.
/// </summary>
public class WeatherSystem : MonoBehaviour
{
    [Header("Wind")]
    [Range(0f, 1f)] public float windStrength = 0.75f;
    [Tooltip("Direction the wind blows towards, in degrees (0 = +Z).")]
    public float windDirection = 35f;
    [Tooltip("Slowly turn the wind direction over time (degrees per second).")]
    public float windDirectionDrift = 0.5f;
    [Tooltip("How far trees lean at full strength, in degrees.")]
    public float maxSwayAngle = 4f;
    [Tooltip("How fast gust waves travel across the forest.")]
    public float gustSpeed = 0.35f;
    [Tooltip("Only trees within this distance of the camera are animated.")]
    public float swayRadius = 70f;

    [Header("Grass")]
    [Tooltip("How far grass bends at full strength, in degrees.")]
    public float grassSwayAngle = 12f;
    [Tooltip("Grass sways this many times faster than trees.")]
    public float grassSwaySpeed = 2.5f;
    [Tooltip("Only grass within this distance of the camera is animated.")]
    public float grassSwayRadius = 40f;

    [Header("Audio")]
    [Range(0f, 1f)] public float windVolume = 0.35f;
    [Range(0f, 1f)] public float leavesVolume = 0.3f;
    [Range(0f, 1f)] public float creakVolume = 0.6f;
    [Tooltip("Gust strength (0-1) above which trees may creak.")]
    public float creakThreshold = 0.65f;

    [Header("Clip Overrides (optional)")]
    public AudioClip windClip;
    public AudioClip leavesClip;
    public AudioClip[] creakClips;

    /// <summary>Current wind including gusts at the listener, 0 to ~1.2.</summary>
    public float CurrentWind { get; private set; }
    public Vector3 WindVector => Quaternion.Euler(0f, windDirection, 0f) * Vector3.forward;

    /// <summary>A set of things that sway, with their resting rotations.</summary>
    class SwayGroup
    {
        public readonly List<Transform> pivots = new List<Transform>();
        public readonly List<Quaternion> baseRotations = new List<Quaternion>();

        public void Refresh(List<Transform> source)
        {
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
    }

    DarkForestGenerator generator;
    readonly SwayGroup trees = new SwayGroup();
    readonly SwayGroup grass = new SwayGroup();
    List<Transform> pivots => trees.pivots;

    AudioSource windSource, leavesSource;
    float creakCooldown;

    void Start()
    {
        generator = FindAnyObjectByType<DarkForestGenerator>();

        windSource = StartLoop(windClip != null ? windClip : ProceduralAudio.Wind);
        leavesSource = StartLoop(leavesClip != null ? leavesClip : ProceduralAudio.Leaves);
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

        CurrentWind = windStrength * Gust(WorldWrap.Fold(listener), wind);

        if (generator == null) generator = FindAnyObjectByType<DarkForestGenerator>();
        if (generator != null)
        {
            trees.Refresh(generator.SwayPivots);
            grass.Refresh(generator.GrassPivots);
            Sway(trees, listener, wind, maxSwayAngle, 1f, swayRadius, 1f);
            Sway(grass, listener, wind, grassSwayAngle, grassSwaySpeed, grassSwayRadius, 2f);
        }
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

    void Sway(SwayGroup group, Vector3 listener, Vector3 wind, float maxAngle, float speed, float radius, float shockScale)
    {
        Vector3 axisWorld = Vector3.Cross(Vector3.up, wind); // leaning around this tilts things downwind
        float radiusSq = radius * radius;
        float time = Time.time * speed;

        for (int i = 0; i < group.pivots.Count; i++)
        {
            Transform pivot = group.pivots[i];
            if (pivot == null) continue;

            Vector3 p = pivot.position;
            if ((p - listener).sqrMagnitude > radiusSq) continue;

            // Stable per-object randomness so neighbours don't move in lockstep.
            float seed = Mathf.Repeat(Mathf.Sin(p.x * 12.9898f + p.z * 78.233f) * 43758.5453f, 1f);

            // Gusts are worked out from the position folded back into the map, so they don't jump when the map wraps.
            float gust = Gust(WorldWrap.Fold(p), wind);
            float lean = windStrength * maxAngle * gust * 0.7f;
            float bob = Mathf.Sin(time * (1.1f + seed * 0.8f) + seed * 20f) * windStrength * maxAngle * 0.3f * gust;

            Transform parent = pivot.parent;
            Vector3 axis = parent != null ? parent.InverseTransformDirection(axisWorld) : axisWorld;
            Quaternion rotation = Quaternion.AngleAxis(lean + bob, axis) * group.baseRotations[i];

            // A shockwave passing throws it over, away from where it came from.
            float shock = Shockwave.LeanAt(p, out Vector3 away);
            if (Mathf.Abs(shock) > 0.01f)
            {
                Vector3 shockAxis = Vector3.Cross(Vector3.up, away);
                if (parent != null) shockAxis = parent.InverseTransformDirection(shockAxis);
                rotation = Quaternion.AngleAxis(shock * shockScale, shockAxis) * rotation;
            }
            pivot.localRotation = rotation;
        }
    }

    // ---------------------------------------------------------------- Audio

    void UpdateAudio(Vector3 listener)
    {
        float smoothing = Time.deltaTime * 0.8f;
        float quiet = (1f - 0.9f * EntityDread.Silence) * (1f - PortalDoor.RoomQuiet); // hushed near an entity, gone in the room
        if (EntityDread.Silence > 0f) smoothing = Mathf.Max(smoothing, Time.deltaTime * 1.5f);
        windSource.volume = Mathf.MoveTowards(windSource.volume, windVolume * Mathf.Clamp01(0.3f + CurrentWind * 0.8f) * quiet, smoothing);
        windSource.pitch = Mathf.Lerp(0.85f, 1.15f, Mathf.Clamp01(CurrentWind));
        leavesSource.volume = Mathf.MoveTowards(leavesSource.volume, leavesVolume * Mathf.Clamp01(CurrentWind * CurrentWind) * quiet, smoothing);

        // Strong gusts make nearby trunks groan.
        creakCooldown -= Time.deltaTime;
        if (creakCooldown <= 0f && CurrentWind > creakThreshold && pivots.Count > 0 && PortalDoor.RoomQuiet < 0.05f)
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
