using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Lightning, now and then, somewhere in the sky: a jagged forking bolt drawn down out of the clouds to the
/// treeline, flickering on and off a few times; the clouds light up round it (NightSky), a cold flash
/// washes over the forest, and a moment later (longer the further off it struck)
/// thunder rolls in. Silhouettes, the hanging giant above all, stand out black against the flash.
/// Not heard or seen from the dark room. DarkForestGenerator adds this automatically.
/// </summary>
public class Lightning : MonoBehaviour
{
    [Tooltip("Seconds between strikes (picked at random between the two).")]
    public float minInterval = 15f;
    public float maxInterval = 45f;
    [Tooltip("Brightness of the flash light over the forest.")]
    public float flashIntensity = 1.6f;
    public Color flashColor = new Color(0.72f, 0.78f, 1f);
    [Range(0f, 1f)] public float thunderVolume = 0.9f;

    const float Distance = 110f; // drawn just inside the sky dome, behind the trees
    const int Segments = 16;

    float timer, strikeTime = -1f, thunderTimer = -1f, thunderVolumeNow;
    Vector3 strikeDir;
    readonly List<LineRenderer> lines = new List<LineRenderer>();
    readonly List<Vector3[]> shapes = new List<Vector3[]>(); // bolt points as directions from the camera
    Light flash;
    AudioSource thunder;
    Material boltMaterial;

    // The flicker: (seconds since the strike, brightness).
    static readonly Vector2[] Flicker =
    {
        new Vector2(0f, 1f), new Vector2(0.05f, 0.15f), new Vector2(0.09f, 1f), new Vector2(0.16f, 0.35f),
        new Vector2(0.22f, 0.9f), new Vector2(0.32f, 0.5f), new Vector2(0.6f, 0f),
    };

    public static Lightning Instance { get; private set; }
    /// <summary>Clears this class's static state (StaticReset calls it when Play starts).</summary>
    internal static void ResetStatics() => Instance = null;

    void Awake() => Instance = this;
    void OnDestroy() { if (Instance == this) Instance = null; }

    /// <summary>
    /// Strikes now, in a given compass direction from the camera (degrees). Closeness 1 is right on top of
    /// you: the thunder comes almost at once, loud; 0 is far off, late and soft.
    /// </summary>
    public void StrikeAt(float bearing, float closeness)
    {
        Strike(bearing);
        float far = 1f - Mathf.Clamp01(closeness);
        thunderTimer = Mathf.Lerp(0.15f, 4f, far);
        thunderVolumeNow = thunderVolume * Mathf.Lerp(1.15f, 0.45f, far);
        timer = Mathf.Max(timer, minInterval * 0.5f); // no other strike straight after
    }

    [Tooltip("At the storm's height (7 lamps lit) strikes come every this many seconds (picked between the two).")]
    public float peakMinInterval = 3f, peakMaxInterval = 9f;

    /// <summary>How built-up the storm is: 0 below 5 lamps lit, then a third, two thirds, and 1 (its height) at 7.</summary>
    public static float Storm => Lamp.TimesLit < 5 ? 0f : Mathf.Clamp01((Lamp.TimesLit - 4f) / 3f);

    void Start()
    {
        timer = Random.Range(minInterval * 0.5f, maxInterval);
        flash = new GameObject("Lightning Flash").AddComponent<Light>();
        flash.transform.SetParent(transform, false);
        flash.type = LightType.Directional;
        flash.color = flashColor;
        flash.enabled = false;

        thunder = ProceduralAudio.AddSource(gameObject, null, false, false, thunderVolume);
        boltMaterial = MaterialFactory.NoFog(new Color(0.85f, 0.9f, 1f) * 6f, 2455, false);
    }

    void Update()
    {
        float dt = Time.deltaTime;
        timer -= dt;
        if (timer <= 0f)
        {
            // The storm builds with the lamps: from the 5th it comes more often and closer, peaking by the 7th.
            float storm = Storm;
            timer = Random.Range(Mathf.Lerp(minInterval, peakMinInterval, storm), Mathf.Lerp(maxInterval, peakMaxInterval, storm));
            if (!PortalDoor.PlayerInRoom)
            {
                Strike();
                float far = Random.value * Mathf.Lerp(1f, 0.45f, storm);
                thunderTimer = Mathf.Lerp(0.6f, 4f, far);
                thunderVolumeNow = thunderVolume * Mathf.Lerp(1f, 0.45f, far);
                // At its height, now and then a second strike right after the first.
                if (storm > 0.5f && Random.value < 0.35f * storm) timer = Mathf.Min(timer, Random.Range(0.4f, 1.5f));
            }
        }

        if (thunderTimer > 0f)
        {
            thunderTimer -= dt;
            if (thunderTimer <= 0f)
            {
                thunder.pitch = Random.Range(0.85f, 1.05f);
                thunder.PlayOneShot(ProceduralAudio.Thunder, thunderVolumeNow * (1f - PortalDoor.RoomQuiet));
            }
        }
    }

    void Strike() => Strike(Random.Range(0f, 360f));

    void Strike(float bearing)
    {
        strikeTime = Time.time;
        // Somewhere round the horizon, from up in the clouds down to the treeline.
        float top = Random.Range(30f, 55f), bottom = Random.Range(-2f, 6f);
        strikeDir = Direction(bearing, (top + bottom) * 0.5f);

        shapes.Clear();
        var main = Bolt(bearing, top, bottom, 1f);
        shapes.Add(main);
        // A couple of forks off the main channel.
        int forks = Random.Range(1, 4);
        for (int i = 0; i < forks; i++)
        {
            int from = Random.Range(2, Segments - 4);
            Vector3 start = main[from];
            float startBearing = Mathf.Atan2(start.x, start.z) * Mathf.Rad2Deg;
            float startElevation = Mathf.Asin(Mathf.Clamp(start.y, -1f, 1f)) * Mathf.Rad2Deg;
            shapes.Add(Bolt(startBearing, startElevation, startElevation - Random.Range(6f, 16f), 0.45f, Random.Range(-1f, 1f) > 0f ? 1f : -1f));
        }

        while (lines.Count < shapes.Count) lines.Add(MakeLine());
        for (int i = 0; i < lines.Count; i++)
        {
            lines[i].enabled = i < shapes.Count;
            if (i < shapes.Count)
            {
                lines[i].positionCount = shapes[i].Length;
                float width = i == 0 ? 0.55f : 0.25f;
                lines[i].startWidth = width;
                lines[i].endWidth = width * 0.4f;
            }
        }

        // Thunder after the flash: the further off, the longer the wait and the softer the roll.
        float far = Random.value;
        thunderTimer = Mathf.Lerp(0.6f, 4f, far);
        thunderVolumeNow = thunderVolume * Mathf.Lerp(1f, 0.45f, far);
    }

    /// <summary>A jagged line of directions from one elevation down to another, wandering sideways.</summary>
    Vector3[] Bolt(float bearing, float top, float bottom, float jag, float lean = 0f)
    {
        var points = new Vector3[Segments];
        float b = bearing;
        for (int i = 0; i < Segments; i++)
        {
            float t = i / (float)(Segments - 1);
            float elevation = Mathf.Lerp(top, bottom, t);
            b += (Random.Range(-1.6f, 1.6f) + lean * 1.2f) * jag;
            points[i] = Direction(b, elevation + Random.Range(-0.8f, 0.8f) * jag);
        }
        return points;
    }

    static Vector3 Direction(float bearing, float elevation)
    {
        float b = bearing * Mathf.Deg2Rad, e = elevation * Mathf.Deg2Rad;
        return new Vector3(Mathf.Sin(b) * Mathf.Cos(e), Mathf.Sin(e), Mathf.Cos(b) * Mathf.Cos(e));
    }

    LineRenderer MakeLine()
    {
        var line = new GameObject("Bolt").AddComponent<LineRenderer>();
        line.transform.SetParent(transform, false);
        line.sharedMaterial = boltMaterial;
        line.useWorldSpace = true;
        line.alignment = LineAlignment.View;
        line.numCapVertices = 2;
        line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        line.receiveShadows = false;
        line.enabled = false;
        return line;
    }

    void LateUpdate()
    {
        var cam = Camera.main;
        float since = Time.time - strikeTime;
        float level = strikeTime < 0f || PortalDoor.PlayerInRoom ? 0f : Sample(since);

        // The bolts sit in the sky round the camera, like the stars.
        for (int i = 0; i < lines.Count; i++)
        {
            bool show = level > 0.25f && i < shapes.Count && cam != null;
            lines[i].enabled = show;
            if (!show) continue;
            Vector3 eye = cam.transform.position;
            var shape = shapes[i];
            for (int k = 0; k < shape.Length; k++) lines[i].SetPosition(k, eye + shape[k] * Distance);
        }

        // The flash over the forest, from the bolt's direction (shadows where the render pipeline allows).
        flash.enabled = level > 0.01f;
        if (flash.enabled)
        {
            flash.intensity = flashIntensity * level;
            flash.shadows = LightShadows.Hard;
            Vector3 from = new Vector3(strikeDir.x, Mathf.Max(strikeDir.y, 0.35f), strikeDir.z).normalized;
            flash.transform.rotation = Quaternion.LookRotation(-from);
        }

        if (NightSky.Instance != null)
        {
            NightSky.Instance.Flash = level;
            NightSky.Instance.FlashDirection = strikeDir;
        }
    }

    static float Sample(float t)
    {
        if (t < 0f || t > Flicker[Flicker.Length - 1].x) return 0f;
        for (int i = 0; i < Flicker.Length - 1; i++)
        {
            if (t <= Flicker[i + 1].x)
            {
                float u = (t - Flicker[i].x) / (Flicker[i + 1].x - Flicker[i].x);
                return Mathf.Lerp(Flicker[i].y, Flicker[i + 1].y, u);
            }
        }
        return 0f;
    }
}
