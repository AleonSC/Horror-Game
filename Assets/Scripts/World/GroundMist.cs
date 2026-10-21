using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Low mist lying in the forest: a real volume of it (Horror/MistVolume), not sprites. A box round the player
/// is filled, in the shader, with billowing 3D noise that hugs the ground and thins with height, drifting with
/// the wind; it gathers thickest in its patches, in hollows and round the ponds, and is barely there elsewhere.
/// The flashlight's beam shows in it as a cone of lit mist, and the nearest lamps and torches glow in it.
/// It's solid from every angle, wraps round the trees, and you walk through it.
///
/// The shader reads where the ground is and where mist gathers from a small map round the player (rebuilt a
/// little at a time as you move), and needs the camera's depth texture, which this turns on.
/// DarkForestGenerator adds this.
/// </summary>
public class GroundMist : MonoBehaviour
{
    void Awake() => Tuning.Apply(this); // saved tuning (Save Tuning on the component's menu)

    [Header("Look")]
    [Tooltip("How thick the mist is overall.")]
    public float density = 0.55f;
    [Tooltip("How quickly it thins with height above the ground (metres: about the height it's thick to).")]
    public float mistHeight = 0.9f;
    [Tooltip("Its colour in the dark (a touch lighter than the fog).")]
    public Color mistColor = new Color(0.035f, 0.04f, 0.05f);
    [Tooltip("How much mist there is outside its patches, hollows and ponds (0 = none, 1 = as much as in them).")]
    [Range(0f, 1f)] public float patchiness = 0.12f;
    [Tooltip("Size of the billows (smaller = bigger billows).")]
    public float noiseScale = 0.06f;
    [Tooltip("Size of the areas it gathers in, or avoids (smaller = bigger areas).")]
    public float patchScale = 0.018f;
    [Tooltip("How far you can see mist.")]
    public float maxDistance = 60f;
    public float driftSpeed = 0.35f;
    [Tooltip("Close to you it's thinner, so the ground and things nearby are easy to see: thinnest within the first distance, full by the second.")]
    public float clearNear = 1.5f, clearFar = 8f;
    [Tooltip("How thick it is right round you, compared with further off.")]
    [Range(0f, 1f)] public float clearAmount = 0.15f;

    [Header("Round The Lamps")]
    [Tooltip("The mist is thinner round the lamps: thinnest within the first distance, back to full by the second.")]
    public float lampClearNear = 3f, lampClearFar = 11f;
    [Tooltip("How thick it is right by a lamp, compared with elsewhere.")]
    [Range(0f, 1f)] public float lampClearAmount = 0.45f;
    readonly Vector4[] clearPos = new Vector4[4];

    [Header("Storm")]
    [Tooltip("As the storm builds (the rain getting heavier as lamps are lit), the mist thins out to this much of itself by the heaviest rain.")]
    [Range(0f, 1f)] public float atFullStorm = 0.1f;

    [Header("Light In It")]
    [Tooltip("How brightly the flashlight lights the mist.")]
    public float flashlightStrength = 0.05f;
    [Tooltip("How brightly lamps and torches light it.")]
    public float lampStrength = 0.06f;
    public float lampSearchRange = 45f;

    const int MapRes = 64;
    const float MapSize = 160f;
    const int NoiseRes = 48;

    Material mat;
    Transform box;
    Renderer boxRenderer;
    Texture2D map, nextMap;
    Vector2 mapOrigin, nextOrigin;
    float mapMinY, mapMaxY;
    bool rebuilding;
    Vector2 noiseOffset;
    Vector3 drift;
    DarkForestGenerator generator;
    WeatherSystem weather;
    Flashlight flashlight;
    Camera depthCam;
    readonly List<Light> nearLights = new List<Light>();
    readonly Vector4[] pointPos = new Vector4[4];
    readonly Vector4[] pointColor = new Vector4[4];
    float lightSearchTimer;

    void Start()
    {
        generator = FindAnyObjectByType<DarkForestGenerator>();
        weather = FindAnyObjectByType<WeatherSystem>();
        Shader shader = Shader.Find("Horror/MistVolume");
        if (shader == null || !shader.isSupported || generator == null || !SystemInfo.supports3DTextures)
        {
            Debug.LogWarning("[Horror] Ground mist: no volume shader (or no 3D textures) here, so no mist.");
            enabled = false;
            return;
        }
        noiseOffset = new Vector2(Random.Range(0f, 1000f), Random.Range(0f, 1000f));

        mat = new Material(shader);
        mat.SetTexture("_Noise", MakeNoise());
        map = NewMap();
        nextMap = NewMap();

        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = "Mist Volume";
        Destroy(go.GetComponent<Collider>());
        go.transform.SetParent(transform, false);
        boxRenderer = go.GetComponent<Renderer>();
        boxRenderer.sharedMaterial = mat;
        boxRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        boxRenderer.receiveShadows = false;
        box = go.transform;

        var cam = Camera.main;
        Vector3 at = cam != null ? cam.transform.position : Vector3.zero;
        // The first map all at once, so the mist is there from the start.
        var it = BuildMap(new Vector2(at.x, at.z), int.MaxValue);
        while (it.MoveNext()) { }
        SwapMaps();
    }

    static Texture2D NewMap() => new Texture2D(MapRes, MapRes, TextureFormat.RGBAHalf, false, true)
    {
        name = "Mist Map", wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear,
    };

    /// <summary>
    /// The map round a point: ground height (R) and how much mist gathers there (G), a few rows a frame
    /// (perRow rows) into the spare texture, so moving on doesn't hitch.
    /// </summary>
    IEnumerator BuildMap(Vector2 center, int rowsPerFrame)
    {
        rebuilding = true;
        Vector2 origin = center - Vector2.one * MapSize * 0.5f;
        var px = new Color[MapRes * MapRes];
        float lo = float.MaxValue, hi = float.MinValue;
        float cell = MapSize / (MapRes - 1);
        int rows = 0;
        for (int y = 0; y < MapRes; y++)
        {
            for (int x = 0; x < MapRes; x++)
            {
                float wx = origin.x + x * cell, wz = origin.y + y * cell;
                float ground = generator.GetGroundHeight(wx, wz);
                lo = Mathf.Min(lo, ground);
                hi = Mathf.Max(hi, ground);
                px[y * MapRes + x] = new Color(ground, Mistiness(wx, wz, ground), 0f, 1f);
            }
            if (++rows >= rowsPerFrame) { rows = 0; yield return null; }
        }
        nextMap.SetPixels(px);
        nextMap.Apply(false);
        // Texels sit on the corners of the grid: uv 0 and 1 at the first and last.
        nextOrigin = origin - Vector2.one * cell * 0.5f;
        mapMinY = lo;
        mapMaxY = hi;
        rebuilding = false;
    }

    void SwapMaps()
    {
        (map, nextMap) = (nextMap, map);
        mapOrigin = nextOrigin;
        mat.SetTexture("_MistMap", map);
        float cell = MapSize / (MapRes - 1);
        mat.SetVector("_MapRect", new Vector4(mapOrigin.x, mapOrigin.y, cell * MapRes, cell * MapRes));
    }

    /// <summary>How much mist gathers here, 0-1: in its patches, deeper in hollows, over and round the ponds.</summary>
    /// <summary>How much mist gathers at a point, 0-1 (before the storm thins it). Crawlers hide where it's thick.</summary>
    public float MistAt(float x, float z) => generator != null ? Mistiness(x, z, generator.GetGroundHeight(x, z)) * Mathf.Lerp(1f, atFullStorm, RainDrops.Storm) : 0f;

    float Mistiness(float x, float z, float ground)
    {
        float patches = Mathf.PerlinNoise((x + noiseOffset.x) * patchScale, (z + noiseOffset.y) * patchScale);
        float around = 0f;
        for (int k = 0; k < 4; k++)
        {
            float a = k * Mathf.PI * 0.5f;
            around += generator.GetGroundHeight(x + Mathf.Cos(a) * 8f, z + Mathf.Sin(a) * 8f);
        }
        float hollow = Mathf.Clamp01((around * 0.25f - ground) / 0.8f); // lower than the ground round it
        float m = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.42f, 0.68f, patches)) + hollow * 0.8f
                  + Water.NearPond(new Vector3(x, ground, z), 10f);
        return Mathf.Clamp01(m);
    }

    /// <summary>Tiling 3D value noise, a few octaves, for the billows.</summary>
    static Texture3D MakeNoise()
    {
        const int n = NoiseRes;
        float Hash(int x, int y, int z, int seed)
        {
            unchecked
            {
                int h = x * 374761393 + y * 668265263 + z * 1442695041 + seed * 144665;
                h = (h ^ (h >> 13)) * 1274126177;
                return ((h ^ (h >> 16)) & 0xffff) / 65535f;
            }
        }
        float Noise(float x, float y, float z, int period, int seed)
        {
            int x0 = Mathf.FloorToInt(x), y0 = Mathf.FloorToInt(y), z0 = Mathf.FloorToInt(z);
            float fx = x - x0, fy = y - y0, fz = z - z0;
            fx = fx * fx * (3f - 2f * fx);
            fy = fy * fy * (3f - 2f * fy);
            fz = fz * fz * (3f - 2f * fz);
            int M(int v) => ((v % period) + period) % period;
            int ax = M(x0), bx = M(x0 + 1), ay = M(y0), by = M(y0 + 1), az = M(z0), bz = M(z0 + 1);
            float c00 = Mathf.Lerp(Hash(ax, ay, az, seed), Hash(bx, ay, az, seed), fx);
            float c10 = Mathf.Lerp(Hash(ax, by, az, seed), Hash(bx, by, az, seed), fx);
            float c01 = Mathf.Lerp(Hash(ax, ay, bz, seed), Hash(bx, ay, bz, seed), fx);
            float c11 = Mathf.Lerp(Hash(ax, by, bz, seed), Hash(bx, by, bz, seed), fx);
            return Mathf.Lerp(Mathf.Lerp(c00, c10, fy), Mathf.Lerp(c01, c11, fy), fz);
        }

        var px = new Color32[n * n * n];
        System.Threading.Tasks.Parallel.For(0, n, z => // (slices on every core at once)
        {
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float u = x / (float)n, v = y / (float)n, w = z / (float)n;
                    float sum = 0f, amp = 0.5f, norm = 0f;
                    int period = 4;
                    for (int o = 0; o < 3; o++)
                    {
                        sum += Noise(u * period, v * period, w * period, period, 900 + o) * amp;
                        norm += amp;
                        amp *= 0.5f;
                        period *= 2;
                    }
                    byte b = (byte)Mathf.Clamp(Mathf.RoundToInt(sum / norm * 255f), 0, 255);
                    px[(z * n + y) * n + x] = new Color32(b, b, b, 255);
                }
        });
        var tex = new Texture3D(n, n, n, TextureFormat.RGBA32, false)
        {
            name = "Mist Noise", wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Bilinear,
        };
        tex.SetPixels32(px);
        tex.Apply(false);
        return tex;
    }

    void Update()
    {
        var cam = Camera.main;
        if (cam == null || mat == null) return;

        // The ray stops at the ground and the trees: it needs the depth texture.
        if (depthCam != cam)
        {
            depthCam = cam;
            var data = cam.GetUniversalAdditionalCameraData();
            if (data != null) data.requiresDepthTexture = true;
        }

        // None in the dark room.
        bool show = !PortalDoor.PlayerInRoom;
        if (boxRenderer.enabled != show) boxRenderer.enabled = show;
        if (!show) return;

        Vector3 at = cam.transform.position;
        Vector2 here = new Vector2(at.x, at.z);
        Vector2 mapCenter = mapOrigin + Vector2.one * MapSize * 0.5f;
        // Moving on: a new map round where you are, built over a few frames, then swapped in.
        if (!rebuilding && (here - mapCenter).magnitude > MapSize * 0.5f - maxDistance - 2f)
            StartCoroutine(BuildAndSwap(here));

        // The box: round you, from just under the lowest ground to well over the highest.
        float half = maxDistance;
        Vector3 min = new Vector3(at.x - half, mapMinY - 0.5f, at.z - half);
        Vector3 max = new Vector3(at.x + half, mapMaxY + mistHeight * 5f, at.z + half);
        box.position = (min + max) * 0.5f;
        box.rotation = Quaternion.identity;
        box.localScale = max - min;
        mat.SetVector("_BoxMin", min);
        mat.SetVector("_BoxMax", max);

        // Drifting on the wind, and slowly churning even when it's still.
        Vector3 wind = weather != null ? weather.WindVector * (0.2f + weather.CurrentWind) : Vector3.zero;
        drift += (wind * driftSpeed + new Vector3(0.05f, 0.03f, 0.04f)) * Time.deltaTime * noiseScale;
        mat.SetVector("_Wind", -drift);

        mat.SetColor("_MistColor", mistColor);
        // The storm (the rain) slowly breaks it up: thinning as the rain picks up, nearly gone at its worst.
        float storm = Mathf.SmoothStep(0f, 1f, RainDrops.Storm);
        mat.SetFloat("_Density", density * Mathf.Lerp(1f, atFullStorm, storm));
        mat.SetVector("_NearClear", new Vector4(clearNear, Mathf.Max(clearFar, clearNear + 0.1f), clearAmount, 0f));
        mat.SetFloat("_MistHeight", Mathf.Max(mistHeight, 0.05f));
        mat.SetFloat("_NoiseScale", noiseScale);
        mat.SetFloat("_MaxDistance", maxDistance);
        mat.SetFloat("_Patchiness", patchiness);

        UpdateFlashlight();
        UpdateLamps(at);
        UpdateLampClearings(at);
    }

    /// <summary>The four nearest lamps, lit or not: the mist thins out round each.</summary>
    void UpdateLampClearings(Vector3 at)
    {
        for (int k = 0; k < 4; k++) clearPos[k] = Vector4.zero;
        var best = new float[] { float.MaxValue, float.MaxValue, float.MaxValue, float.MaxValue };
        foreach (var lamp in Lamp.All)
        {
            if (lamp == null) continue;
            Vector3 p = WorldWrap.NearestImage(lamp.transform.position, at);
            float d = (p - at).sqrMagnitude;
            for (int k = 0; k < 4; k++)
            {
                if (d >= best[k]) continue;
                for (int m = 3; m > k; m--) { best[m] = best[m - 1]; clearPos[m] = clearPos[m - 1]; }
                best[k] = d;
                clearPos[k] = new Vector4(p.x, p.y, p.z, 1f);
                break;
            }
        }
        mat.SetVectorArray("_ClearPos", clearPos);
        mat.SetVector("_LampClear", new Vector4(lampClearNear, Mathf.Max(lampClearFar, lampClearNear + 0.1f), lampClearAmount, 0f));
    }

    IEnumerator BuildAndSwap(Vector2 center)
    {
        yield return BuildMap(center, 4);
        SwapMaps();
    }

    void UpdateFlashlight()
    {
        if (flashlight == null) flashlight = FindAnyObjectByType<Flashlight>();
        Light beam = flashlight != null && flashlight.IsOn ? flashlight.flashlight : null;
        if (beam == null || !beam.isActiveAndEnabled)
        {
            mat.SetVector("_SpotColor", new Vector4(0f, 0f, 0f, 1f));
            return;
        }
        Transform t = beam.transform;
        mat.SetVector("_SpotPos", new Vector4(t.position.x, t.position.y, t.position.z, beam.range));
        float outer = Mathf.Cos(beam.spotAngle * 0.5f * Mathf.Deg2Rad);
        float inner = Mathf.Cos(Mathf.Min(beam.innerSpotAngle, beam.spotAngle - 1f) * 0.5f * Mathf.Deg2Rad);
        mat.SetVector("_SpotDir", new Vector4(t.forward.x, t.forward.y, t.forward.z, outer));
        // How bright the beam is as a share of a normal flashlight (dimming as the battery runs down), never
        // more: turn the flashlight's own brightness right up and the mist mustn't blaze white with it.
        float level = Mathf.Clamp(beam.intensity / 12f, 0f, 1.2f);
        Color c = beam.color * (12f * level * flashlightStrength);
        mat.SetVector("_SpotColor", new Vector4(c.r, c.g, c.b, inner));
    }

    /// <summary>The few nearest lamps and torches (point lights), looked for now and then, glowing in it.</summary>
    void UpdateLamps(Vector3 at)
    {
        lightSearchTimer -= Time.deltaTime;
        if (lightSearchTimer <= 0f)
        {
            lightSearchTimer = 0.5f;
            nearLights.Clear();
            foreach (var l in FindObjectsByType<Light>(FindObjectsSortMode.None))
            {
                if (l.type != LightType.Point || !l.isActiveAndEnabled || l.intensity < 0.3f || l.range < 3f) continue;
                if ((l.transform.position - at).sqrMagnitude > lampSearchRange * lampSearchRange) continue;
                nearLights.Add(l);
            }
            nearLights.Sort((a, b) => (a.transform.position - at).sqrMagnitude.CompareTo((b.transform.position - at).sqrMagnitude));
            if (nearLights.Count > 4) nearLights.RemoveRange(4, nearLights.Count - 4);
        }

        int count = 0;
        foreach (var l in nearLights)
        {
            if (l == null || !l.isActiveAndEnabled) continue;
            Vector3 p = l.transform.position;
            pointPos[count] = new Vector4(p.x, p.y, p.z, Mathf.Min(l.range, 14f));
            Color c = l.color * Mathf.Min(l.intensity, 8f) * lampStrength;
            pointColor[count] = new Vector4(c.r, c.g, c.b, 0f);
            count++;
        }
        for (int k = count; k < 4; k++) { pointPos[k] = Vector4.zero; pointColor[k] = Vector4.zero; }
        mat.SetVectorArray("_PointPos", pointPos);
        mat.SetVectorArray("_PointColor", pointColor);
        mat.SetFloat("_PointCount", count);
    }
}
