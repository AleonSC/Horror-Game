using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Real-looking fire: layered flames (the Horror/Flame shader on camera-facing quads that stay upright
/// whichever way the thing they're on is tilted), a hotter inner core, embers spitting up and fading, and
/// a little dark smoke curling off the top. Strength shrinks it (a torch burning down). Used on the torch
/// and the lighter.
/// </summary>
public class Fire : MonoBehaviour
{
    void Awake() => Tuning.Apply(this); // saved tuning (Inspector changes made in Play are kept)

    public float size = 0.3f;
    [Range(0f, 1f)] public float strength = 1f;
    public Color tint = new Color(1f, 0.55f, 0.15f);

    readonly List<Transform> flames = new List<Transform>();
    readonly List<float> flameScale = new List<float>();
    readonly List<Transform> embers = new List<Transform>();
    readonly List<Vector3> emberVel = new List<Vector3>();
    readonly List<float> emberAge = new List<float>(), emberLife = new List<float>();
    readonly List<Transform> smoke = new List<Transform>();
    readonly List<float> smokeAge = new List<float>();
    float seed;

    static Mesh quad;
    static Material emberMat, smokeMat;
    /// <summary>Clears this class's static state (StaticReset calls it when Play starts).</summary>
    internal static void ResetStatics()
    {
        StaticReset.Drop(ref quad);
        StaticReset.Drop(ref emberMat);
        StaticReset.Drop(ref smokeMat);
    }

    /// <summary>A fire at a point on something (its base there, flames rising from it).</summary>
    public static Fire Create(Transform parent, Vector3 localPos, float size, Color tint, int emberCount = 6, int smokeCount = 3)
    {
        var go = new GameObject("Fire");
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPos;
        var fire = go.AddComponent<Fire>();
        fire.size = size;
        fire.tint = tint;
        fire.Build(emberCount, smokeCount);
        return fire;
    }

    void Build(int emberCount, int smokeCount)
    {
        seed = Random.Range(0f, 100f);
        if (quad == null)
        {
            // A unit quad with its bottom edge on the origin: flames rise from where they're put.
            quad = new Mesh { name = "Flame Quad" };
            quad.vertices = new[] { new Vector3(-0.5f, 0f, 0f), new Vector3(0.5f, 0f, 0f), new Vector3(0.5f, 1f, 0f), new Vector3(-0.5f, 1f, 0f) };
            quad.uv = new[] { new Vector2(0, 0), new Vector2(1, 0), new Vector2(1, 1), new Vector2(0, 1) };
            quad.triangles = new[] { 0, 2, 1, 0, 3, 2 };
            quad.RecalculateBounds();
            emberMat = MaterialFactory.NoFog(new Color(1f, 0.55f, 0.15f) * 4f);
            Shader s = Shader.Find("Horror/Smoke");
            smokeMat = s != null ? new Material(s) { renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent } : null;
        }
        Shader flameShader = Shader.Find("Horror/Flame");

        // Layers of flame: the big outer ones, then a hot, bright core.
        (float w, float h, float bright, float speed)[] layers = { (0.6f, 1f, 1.6f, 1f), (0.5f, 0.85f, 1.4f, 1.3f), (0.3f, 0.55f, 2.6f, 1.6f) };
        foreach (var (w, h, bright, speed) in layers)
        {
            var f = new GameObject("Flame");
            f.transform.SetParent(transform, false);
            f.AddComponent<MeshFilter>().sharedMesh = quad;
            var r = f.AddComponent<MeshRenderer>();
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            if (flameShader != null)
            {
                var m = new Material(flameShader);
                m.SetColor("_Color", tint);
                m.SetFloat("_Intensity", bright);
                m.SetFloat("_Seed", seed + flames.Count * 17.3f);
                m.SetFloat("_Speed", speed);
                r.sharedMaterial = m;
            }
            else r.sharedMaterial = MaterialFactory.NoFog(tint * bright);
            f.transform.localScale = new Vector3(w, h, 1f) * size;
            flames.Add(f.transform);
            flameScale.Add(h);
        }

        for (int i = 0; i < emberCount; i++)
        {
            var e = new GameObject("Ember");
            e.transform.SetParent(transform, false);
            e.AddComponent<MeshFilter>().sharedMesh = quad;
            var r = e.AddComponent<MeshRenderer>();
            r.sharedMaterial = emberMat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            embers.Add(e.transform);
            emberVel.Add(Vector3.zero);
            emberAge.Add(Random.Range(0f, 1f));
            emberLife.Add(0f);
        }

        if (smokeMat != null)
            for (int i = 0; i < smokeCount; i++)
            {
                var p = GameObject.CreatePrimitive(PrimitiveType.Quad);
                p.name = "Smoke";
                p.transform.SetParent(transform, false);
                Destroy(p.GetComponent<Collider>());
                var r = p.GetComponent<Renderer>();
                r.sharedMaterial = smokeMat;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                var block = new MaterialPropertyBlock();
                block.SetFloat("_Seed", Random.Range(0f, 100f));
                block.SetColor("_Color", new Color(0.08f, 0.07f, 0.06f, 0.35f));
                r.SetPropertyBlock(block);
                smoke.Add(p.transform);
                smokeAge.Add(i / (float)Mathf.Max(smokeCount, 1));
            }
    }

    void LateUpdate()
    {
        var cam = Camera.main;
        float t = Time.time + seed;
        float dt = Time.deltaTime;
        float s = size * Mathf.Lerp(0.35f, 1f, strength);

        // Flames burn straight up, whatever angle the torch is held at, and turn to face you.
        Vector3 toCam = cam != null ? cam.transform.position - transform.position : Vector3.back;
        toCam.y = 0f;
        Quaternion face = toCam.sqrMagnitude > 0.0001f ? Quaternion.LookRotation(-toCam) : Quaternion.identity;
        for (int i = 0; i < flames.Count; i++)
        {
            flames[i].rotation = face * Quaternion.Euler(0f, (i - 1) * 25f, 0f);
            float flicker = 0.85f + 0.3f * Mathf.PerlinNoise(t * 6f + i * 3f, 0.5f);
            float sway = Mathf.Sin(t * 3f + i) * 4f;
            flames[i].rotation *= Quaternion.Euler(0f, 0f, sway);
            Vector3 baseScale = new Vector3(i == 2 ? 0.3f : i == 1 ? 0.5f : 0.6f, flameScale[i], 1f) * s;
            flames[i].localScale = new Vector3(baseScale.x / Mathf.Max(transform.lossyScale.x, 0.0001f), baseScale.y * flicker / Mathf.Max(transform.lossyScale.y, 0.0001f), 1f);
            flames[i].position = transform.position;
        }

        // Embers spit up, drift and wink out.
        for (int i = 0; i < embers.Count; i++)
        {
            emberAge[i] += dt;
            if (emberAge[i] >= emberLife[i])
            {
                emberAge[i] = 0f;
                emberLife[i] = Random.Range(0.6f, 1.6f);
                embers[i].position = transform.position + Random.insideUnitSphere * s * 0.15f + Vector3.up * s * 0.3f;
                emberVel[i] = new Vector3(Random.Range(-0.3f, 0.3f), Random.Range(0.6f, 1.4f), Random.Range(-0.3f, 0.3f)) * Mathf.Max(s, 0.1f) * 3f;
            }
            emberVel[i] += new Vector3(Mathf.Sin(t * 7f + i) * 0.4f, 0f, Mathf.Cos(t * 5f + i) * 0.4f) * dt;
            embers[i].position += emberVel[i] * dt;
            float life = 1f - emberAge[i] / Mathf.Max(emberLife[i], 0.01f);
            embers[i].rotation = cam != null ? cam.transform.rotation : Quaternion.identity;
            float e = s * 0.04f * life * strength / Mathf.Max(transform.lossyScale.x, 0.0001f);
            embers[i].localScale = Vector3.one * e;
        }

        // Smoke curling up off the top.
        for (int i = 0; i < smoke.Count; i++)
        {
            smokeAge[i] += dt / 2.2f;
            if (smokeAge[i] >= 1f) smokeAge[i] -= 1f;
            float a = smokeAge[i];
            smoke[i].position = transform.position + Vector3.up * s * (0.8f + a * 2.2f) + new Vector3(Mathf.Sin(t * 1.3f + i) , 0f, Mathf.Cos(t * 1.1f + i)) * s * 0.3f * a;
            smoke[i].rotation = cam != null ? Quaternion.LookRotation(smoke[i].position - cam.transform.position) : Quaternion.identity;
            smoke[i].localScale = Vector3.one * s * (0.4f + a * 1.2f) * Mathf.Sin(Mathf.PI * a) / Mathf.Max(transform.lossyScale.x, 0.0001f);
        }
    }
}
