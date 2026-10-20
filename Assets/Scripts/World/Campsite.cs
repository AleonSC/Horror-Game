using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// An abandoned campsite: a sagging tent, a burnt-out campfire with a few dying embers and a thin trail of
/// smoke (it ignores fog, so you can spot camps far off across the forest), a log to sit on, and a backpack
/// with a flare gun and a bundle of glowsticks beside it. DarkForestGenerator places a few around the map.
/// </summary>
public class Campsite : MonoBehaviour
{
    public static readonly List<Campsite> All = new List<Campsite>();

    [Header("Smoke")]
    public int puffCount = 34;
    public float puffLife = 16f;
    [Tooltip("How fast smoke leaves the embers; it slows as it rises and spreads.")]
    public float riseSpeed = 2.4f;
    [Tooltip("Colour low down, faintly warmed by the embers...")]
    public Color smokeColorLow = new Color(0.33f, 0.28f, 0.25f, 0.5f);
    [Tooltip("...and higher up, cool grey.")]
    public Color smokeColor = new Color(0.27f, 0.28f, 0.3f, 0.32f);

    [SerializeField] Transform fire;
    [SerializeField] Transform backpack;
    readonly List<Transform> puffs = new List<Transform>();
    readonly List<float> puffAge = new List<float>();
    readonly List<Renderer> puffRenderers = new List<Renderer>();
    readonly List<Renderer> embers = new List<Renderer>();
    readonly List<float> puffSeed = new List<float>();
    readonly List<float> puffSpin = new List<float>();
    readonly List<Vector2> puffDrift = new List<Vector2>();
    MaterialPropertyBlock block;
    WeatherSystem weather;

    void OnEnable() => All.Add(this);
    void OnDisable() => All.Remove(this);

    /// <summary>Builds the camp at its current position (pivot on the ground at the centre).</summary>
    public void Build(DarkForestGenerator generator, System.Random rng)
    {
        float R(float a, float b) => a + (float)rng.NextDouble() * (b - a);
        float Ground(Vector3 local)
        {
            Vector3 w = transform.TransformPoint(local);
            return generator.GetGroundHeight(w.x, w.z) - transform.position.y;
        }

        var canvas = MaterialFactory.Lit(new Color(0.12f, 0.13f, 0.08f), 0.05f);
        var pole = MaterialFactory.Lit(new Color(0.15f, 0.15f, 0.16f), 0.4f);
        var stone = MaterialFactory.Lit(new Color(0.16f, 0.16f, 0.16f), 0.2f);
        var charred = MaterialFactory.Lit(new Color(0.03f, 0.025f, 0.02f), 0.05f);
        var ash = MaterialFactory.Lit(new Color(0.12f, 0.115f, 0.11f), 0f);
        var wood = MaterialFactory.Lit(new Color(0.14f, 0.1f, 0.07f), 0.05f);
        var pack = MaterialFactory.Lit(new Color(0.18f, 0.06f, 0.04f), 0.15f);

        // A-frame tent, one side sagging.
        var tent = Anchor("Tent", new Vector3(-1.6f, 0f, 0.6f), R(-20f, 20f), Ground);
        for (int side = -1; side <= 1; side += 2)
        {
            var slab = Box(canvas, tent, new Vector3(side * 0.45f, 0.6f, 0f), new Vector3(0.04f, 1.45f, 2.2f), false);
            slab.localRotation = Quaternion.Euler(0f, 0f, side * (40f + (side > 0 ? R(0f, 8f) : 0f)));
        }
        Box(canvas, tent, new Vector3(0.25f, 0.45f, 1.12f), new Vector3(0.55f, 0.9f, 0.03f), false).localRotation = Quaternion.Euler(0f, 35f, -12f); // door flap, half open
        Box(pole, tent, new Vector3(0f, 1.18f, 0f), new Vector3(0.03f, 0.03f, 2.3f), false);
        for (int end = -1; end <= 1; end += 2)
            Box(pole, tent, new Vector3(0f, 0.6f, end * 1.1f), new Vector3(0.03f, 1.2f, 0.03f), false);
        var tentCollider = tent.gameObject.AddComponent<BoxCollider>();
        tentCollider.center = new Vector3(0f, 0.6f, 0f);
        tentCollider.size = new Vector3(1.8f, 1.2f, 2.2f);

        // Burnt-out campfire: a ring of stones, charred logs, ash, a few embers still glowing.
        fire = Anchor("Campfire", new Vector3(1.2f, 0f, -0.4f), 0f, Ground);
        for (int i = 0; i < 9; i++)
        {
            float a = i / 9f * Mathf.PI * 2f;
            var rock = Part(PrimitiveType.Sphere, stone, fire, new Vector3(Mathf.Cos(a) * 0.55f, 0.06f, Mathf.Sin(a) * 0.55f), new Vector3(R(0.18f, 0.26f), R(0.12f, 0.18f), R(0.16f, 0.22f)));
            rock.localRotation = Quaternion.Euler(0f, R(0f, 360f), 0f);
        }
        Part(PrimitiveType.Cylinder, ash, fire, new Vector3(0f, 0.01f, 0f), new Vector3(0.85f, 0.01f, 0.85f));
        for (int i = 0; i < 4; i++)
        {
            var log = Part(PrimitiveType.Cylinder, charred, fire, new Vector3(0f, 0.08f, 0f), new Vector3(0.08f, 0.3f, 0.08f));
            log.localRotation = Quaternion.Euler(0f, i * 45f + R(-10f, 10f), 70f + R(-10f, 10f));
        }
        var emberMat = MaterialFactory.Unlit(new Color(0.9f, 0.2f, 0.03f));
        for (int i = 0; i < 6; i++)
        {
            var ember = Part(PrimitiveType.Sphere, emberMat, fire, new Vector3(R(-0.2f, 0.2f), 0.04f, R(-0.2f, 0.2f)), Vector3.one * R(0.03f, 0.06f));
            embers.Add(ember.GetComponent<Renderer>());
        }

        // A log seat by the fire.
        var seat = Anchor("Log Seat", new Vector3(1.4f, 0f, 1.1f), R(-30f, 30f), Ground);
        var seatLog = Part(PrimitiveType.Cylinder, wood, seat, new Vector3(0f, 0.18f, 0f), new Vector3(0.36f, 0.75f, 0.36f), true);
        seatLog.localRotation = Quaternion.Euler(0f, 0f, 90f);

        // The backpack, slumped over, with the flare gun beside it.
        backpack = Anchor("Backpack", new Vector3(-0.2f, 0f, -1.4f), R(0f, 360f), Ground);
        var body = Box(pack, backpack, new Vector3(0f, 0.25f, 0f), new Vector3(0.36f, 0.5f, 0.24f), true);
        body.localRotation = Quaternion.Euler(-18f, 0f, 6f);
        Part(PrimitiveType.Sphere, pack, body, new Vector3(0f, 0.5f, 0f), new Vector3(1f, 0.35f, 1f));
        Box(pack, body, new Vector3(0f, -0.15f, 0.55f), new Vector3(0.75f, 0.35f, 0.2f), false); // front pocket (in body space)
        for (int s = -1; s <= 1; s += 2)
            Box(MaterialFactory.Lit(new Color(0.06f, 0.06f, 0.06f), 0.2f), body, new Vector3(s * 0.28f, 0f, -0.55f), new Vector3(0.12f, 0.9f, 0.08f), false);

        // Smoke puffs, recycled as they rise.
        Material smoke = MakeSmokeMaterial();
        for (int i = 0; i < puffCount; i++)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
            go.name = "Smoke";
            go.transform.SetParent(fire, false);
            RemoveCollider(go);
            var renderer = go.GetComponent<Renderer>();
            renderer.sharedMaterial = smoke;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            puffs.Add(go.transform);
            puffAge.Add(i / (float)puffCount * puffLife);
            puffRenderers.Add(renderer);
            puffSeed.Add(Random.Range(0f, 100f));
            puffSpin.Add(Random.Range(-12f, 12f));
            puffDrift.Add(Random.insideUnitCircle);
        }

        SpawnItems();
    }

    /// <summary>Puts a fresh flare gun and glowsticks by the backpack (used when the run resets).</summary>
    public void ResetCampsite()
    {
        SpawnItems();
    }

    void SpawnItems()
    {
        if (backpack == null) return;
        foreach (var old in backpack.GetComponentsInChildren<ItemPickup>())
        {
            if (Application.isPlaying) Destroy(old.gameObject);
            else DestroyImmediate(old.gameObject);
        }
        ItemPickup.CreateFlareGun(backpack, new Vector3(0.35f, 0.02f, 0.15f), Random.Range(0f, 360f));
        ItemPickup.CreateGlowsticks(backpack, new Vector3(-0.35f, 0.02f, -0.05f), Random.Range(0f, 360f));
    }

    void Update()
    {
        if (puffs.Count == 0) return;
        var cam = Camera.main;
        if (cam == null) return;
        if (block == null) block = new MaterialPropertyBlock();
        if (weather == null) weather = FindAnyObjectByType<WeatherSystem>();

        // The wind bends the column over as it rises.
        Vector3 wind = weather != null ? weather.WindVector * (0.4f + weather.CurrentWind) : Vector3.zero;
        Vector3 localWind = fire.InverseTransformDirection(wind);
        float dt = Time.deltaTime;

        for (int i = 0; i < puffs.Count; i++)
        {
            float age = puffAge[i] + dt;
            if (age >= puffLife)
            {
                // Recycled at the embers as a fresh, different wisp.
                age -= puffLife;
                puffSeed[i] = Random.Range(0f, 100f);
                puffSpin[i] = Random.Range(-12f, 12f);
                puffDrift[i] = Random.insideUnitCircle;
            }
            puffAge[i] = age;

            float t = age / puffLife;
            // Rises quickly off the embers, slowing as it cools and spreads.
            float height = riseSpeed * puffLife * (1f - Mathf.Exp(-age / (puffLife * 0.45f))) * 0.45f;
            // Curls and wanders more the higher it gets; the wind bends the column over.
            float curl = t * t * 1.4f;
            Vector3 wander = new Vector3(
                Mathf.Sin(age * 0.7f + puffSeed[i]) * curl + puffDrift[i].x * t * 0.8f,
                0f,
                Mathf.Cos(age * 0.55f + puffSeed[i] * 1.3f) * curl + puffDrift[i].y * t * 0.8f);
            Transform puff = puffs[i];
            puff.localPosition = new Vector3(0f, 0.25f + height, 0f) + wander + localWind * (height * height * 0.04f + height * 0.2f);
            puff.localScale = Vector3.one * Mathf.Lerp(0.35f, 5f, 1f - (1f - t) * (1f - t)); // spreads fast, then slowly
            // Face the camera, turning slowly as it billows.
            puff.rotation = cam.transform.rotation * Quaternion.Euler(0f, 0f, puffSeed[i] * 3.6f + age * puffSpin[i]);

            // Thick and faintly warm over the fire, thinning to cool grey wisps.
            Color c = Color.Lerp(smokeColorLow, smokeColor, Mathf.Clamp01(t * 2.5f));
            c.a *= Mathf.Clamp01(t * 8f) * Mathf.Pow(1f - t, 1.6f);
            block.SetColor("_Color", c);
            block.SetFloat("_Seed", puffSeed[i]);
            puffRenderers[i].SetPropertyBlock(block);
        }

        // Embers pulse faintly.
        float glow = 0.6f + 0.4f * Mathf.PerlinNoise(Time.time * 1.5f, transform.position.x);
        foreach (var e in embers) e.transform.localScale = Vector3.one * 0.045f * glow;
    }

    static Material MakeSmokeMaterial()
    {
        Shader shader = Shader.Find("Horror/Smoke");
        if (shader == null) return MaterialFactory.Unlit(new Color(0.3f, 0.3f, 0.3f));
        return new Material(shader) { renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent };
    }

    // ---------------------------------------------------------------- Building helpers

    Transform Anchor(string name, Vector3 local, float yaw, System.Func<Vector3, float> ground)
    {
        var t = new GameObject(name).transform;
        t.SetParent(transform, false);
        t.localPosition = new Vector3(local.x, ground(local), local.z);
        t.localRotation = Quaternion.Euler(0f, yaw, 0f);
        return t;
    }

    Transform Box(Material mat, Transform parent, Vector3 pos, Vector3 scale, bool keepCollider)
        => Part(PrimitiveType.Cube, mat, parent, pos, scale, keepCollider);

    Transform Part(PrimitiveType type, Material mat, Transform parent, Vector3 pos, Vector3 scale, bool keepCollider = false)
    {
        var go = GameObject.CreatePrimitive(type);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = pos;
        go.transform.localScale = scale;
        go.GetComponent<Renderer>().sharedMaterial = mat;
        if (!keepCollider) RemoveCollider(go);
        return go.transform;
    }

    static void RemoveCollider(GameObject go)
    {
        var col = go.GetComponent<Collider>();
        if (Application.isPlaying) Destroy(col);
        else DestroyImmediate(col);
    }
}
