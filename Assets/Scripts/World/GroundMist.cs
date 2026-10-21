using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Low mist lying on the forest floor: soft, slow-drifting banks of it, thickest in hollows and over the
/// ponds, thin or gone elsewhere, so it pools in some places and not others. It lives in a circle round the
/// player (banks that fall behind are moved out ahead), drifts with the wind, thins as you walk into it so
/// you wade through rather than clip it, and glows where the flashlight's beam falls on it.
/// DarkForestGenerator adds this.
/// </summary>
public class GroundMist : MonoBehaviour
{
    public int bankCount = 70;
    public float radius = 45f;
    [Tooltip("Mist colour in the dark (a little lighter than the fog)...")]
    public Color darkColor = new Color(0.05f, 0.058f, 0.07f, 0.3f);
    [Tooltip("...and where the flashlight lights it.")]
    public Color litColor = new Color(0.32f, 0.33f, 0.34f, 0.42f);
    [Tooltip("Size of the areas the mist gathers in, or avoids (smaller = bigger areas).")]
    public float noiseScale = 0.018f;

    class Bank
    {
        public Transform t;
        public Renderer r;
        public Vector3 pos;
        public float width, height, seed, density, fade;
    }

    readonly List<Bank> banks = new List<Bank>();
    DarkForestGenerator generator;
    WeatherSystem weather;
    Flashlight flashlight;
    MaterialPropertyBlock block;
    Vector2 noiseOffset;

    void Start()
    {
        generator = FindAnyObjectByType<DarkForestGenerator>();
        weather = FindAnyObjectByType<WeatherSystem>();
        block = new MaterialPropertyBlock();
        noiseOffset = new Vector2(Random.Range(0f, 1000f), Random.Range(0f, 1000f));

        Shader shader = Shader.Find("Horror/Smoke");
        if (shader == null || generator == null) { enabled = false; return; }
        var mat = new Material(shader) { renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent - 10 };

        for (int i = 0; i < bankCount; i++)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
            go.name = "Mist Bank";
            Destroy(go.GetComponent<Collider>());
            go.transform.SetParent(transform, false);
            var r = go.GetComponent<MeshRenderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            var bank = new Bank { t = go.transform, r = r };
            banks.Add(bank);
            go.SetActive(false);
        }
    }

    /// <summary>How much mist gathers here, 0-1: in its patches, deeper in hollows, over and round the ponds.</summary>
    float Mistiness(Vector3 p)
    {
        float patches = Mathf.PerlinNoise((p.x + noiseOffset.x) * noiseScale, (p.z + noiseOffset.y) * noiseScale);
        float ground = generator.GetGroundHeight(p.x, p.z);
        float around = 0f;
        for (int k = 0; k < 4; k++)
        {
            float a = k * Mathf.PI * 0.5f;
            around += generator.GetGroundHeight(p.x + Mathf.Cos(a) * 8f, p.z + Mathf.Sin(a) * 8f);
        }
        float hollow = Mathf.Clamp01((around * 0.25f - ground) / 0.8f); // lower than the ground round it
        float m = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.45f, 0.7f, patches)) + hollow * 0.8f + Water.NearPond(p, 10f);
        return Mathf.Clamp01(m);
    }

    /// <summary>Puts a bank somewhere in the ring round the player (ahead of them if `ahead`), where mist gathers.</summary>
    void Place(Bank b, Vector3 center, Vector3 ahead, bool anywhere)
    {
        for (int attempt = 0; attempt < 12; attempt++)
        {
            Vector2 dir = Random.insideUnitCircle.normalized;
            if (!anywhere && ahead.sqrMagnitude > 0.01f && Vector2.Dot(dir, new Vector2(ahead.x, ahead.z)) < 0f) dir = -dir;
            float dist = anywhere ? Mathf.Sqrt(Random.value) * radius : Random.Range(radius * 0.7f, radius);
            var p = center + new Vector3(dir.x, 0f, dir.y) * dist;
            float m = Mistiness(p);
            if (Random.value > m) continue;
            b.width = Random.Range(5f, 10f);
            b.height = Random.Range(1.3f, 2.4f);
            b.pos = new Vector3(p.x, generator.GetGroundHeight(p.x, p.z) + b.height * 0.32f, p.z);
            b.seed = Random.Range(0f, 100f);
            b.density = Mathf.Lerp(0.5f, 1f, m);
            b.fade = 0f; // fades in, so nothing pops into view
            if (!b.t.gameObject.activeSelf) b.t.gameObject.SetActive(true);
            return;
        }
        if (b.t.gameObject.activeSelf) b.t.gameObject.SetActive(false); // nowhere misty nearby: wait
        b.fade = 0f;
    }

    bool placedAll;

    void Update()
    {
        var cam = Camera.main;
        if (cam == null || banks.Count == 0) return;
        if (flashlight == null) flashlight = FindAnyObjectByType<Flashlight>();
        Vector3 center = cam.transform.position;
        Vector3 ahead = cam.transform.forward;
        float dt = Time.deltaTime;

        if (!placedAll)
        {
            placedAll = true;
            foreach (var b in banks) { Place(b, center, ahead, true); b.fade = 1f; }
        }

        Vector3 wind = weather != null ? weather.WindVector * (0.15f + 0.35f * weather.CurrentWind) : Vector3.zero;
        Light beam = flashlight != null && flashlight.IsOn ? flashlight.flashlight : null;

        foreach (var b in banks)
        {
            Vector3 flat = b.pos - center;
            flat.y = 0f;
            if (!b.t.gameObject.activeSelf || flat.magnitude > radius * 1.1f)
            {
                // Fallen behind, or still waiting for a spot: out ahead again (a few a frame at most is plenty).
                if (Random.value < 0.05f) Place(b, center, ahead, false);
                continue;
            }

            b.pos += wind * dt;
            b.fade = Mathf.MoveTowards(b.fade, 1f, dt / 4f);

            // Upright, turned to face you, so it always reads as a low bank rather than a flat card.
            Vector3 face = b.pos - center;
            face.y = 0f;
            if (face.sqrMagnitude < 0.0001f) face = Vector3.forward;
            b.t.SetPositionAndRotation(b.pos, Quaternion.LookRotation(face.normalized, Vector3.up));
            b.t.localScale = new Vector3(b.width, b.height, 1f);

            // Thin round the edge of the ring, and as you walk into it.
            float dist = flat.magnitude;
            float alpha = b.density * b.fade
                          * Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(radius, radius * 0.75f, dist))
                          * Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(1.5f, 6f, dist));

            // Lit where the beam falls on it.
            float lit = 0f;
            if (beam != null)
            {
                Vector3 to = b.pos - beam.transform.position;
                float d = to.magnitude;
                if (d < beam.range)
                {
                    float angle = Vector3.Angle(beam.transform.forward, to);
                    float cone = Mathf.InverseLerp(beam.spotAngle * 0.5f + 8f, beam.spotAngle * 0.25f, angle);
                    lit = Mathf.Clamp01(cone) * (1f - d / beam.range) * Mathf.Clamp01(beam.intensity / 12f);
                }
            }
            Color c = Color.Lerp(darkColor, litColor, lit);
            c.a *= alpha;
            block.SetColor("_Color", c);
            block.SetFloat("_Seed", b.seed);
            b.r.SetPropertyBlock(block);
        }
    }
}
