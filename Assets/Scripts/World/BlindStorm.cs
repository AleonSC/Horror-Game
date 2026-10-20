using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// The storm the blind one comes in under: a big, low, churning black cloud that rolls in over the spot
/// where it's about to break out of the ground, lit from inside by flickering red lightning that forks down
/// to the earth round it, each strike a flash of red light across the trees and a crack of thunder. It hangs
/// there through the roar, then thins out and drifts apart. BlindEntity makes one at the start of its
/// entrance and calls Strike on the big beats.
/// </summary>
public class BlindStorm : MonoBehaviour
{
    public float cloudHeight = 26f;
    public float cloudRadius = 20f;
    public float lifetime = 22f;
    public Color cloudColor = new Color(0.035f, 0.025f, 0.03f, 0.9f);
    public Color boltColor = new Color(1f, 0.08f, 0.04f);

    readonly List<Transform> puffs = new List<Transform>();
    readonly List<Renderer> puffRenderers = new List<Renderer>();
    readonly List<float> puffSpin = new List<float>();
    readonly List<Vector3> puffHome = new List<Vector3>();
    readonly List<float> puffSize = new List<float>();
    MaterialPropertyBlock block;
    Material boltMat;
    Light flash;
    float age, glow, flashPeak, nextStrike = 0.8f;
    Vector3 ground;

    readonly List<LineRenderer> bolts = new List<LineRenderer>();
    float boltAge = 99f;

    /// <summary>Rolls a storm in over a point on the ground.</summary>
    public static BlindStorm Over(Vector3 groundPoint)
    {
        var go = new GameObject("Blind Storm");
        go.transform.position = groundPoint;
        var storm = go.AddComponent<BlindStorm>();
        storm.ground = groundPoint;
        storm.Build();
        return storm;
    }

    void Build()
    {
        block = new MaterialPropertyBlock();
        Shader smokeShader = Shader.Find("Horror/Smoke");
        Material smoke = smokeShader != null
            ? new Material(smokeShader) { renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent }
            : MaterialFactory.Unlit(cloudColor);

        // The cloud: big, dark, overlapping billows in a low, flattened mass, thickest in the middle.
        for (int i = 0; i < 30; i++)
        {
            var q = GameObject.CreatePrimitive(PrimitiveType.Quad);
            q.name = "Billow";
            q.transform.SetParent(transform, false);
            Destroy(q.GetComponent<Collider>());
            var r = q.GetComponent<Renderer>();
            r.sharedMaterial = smoke;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            Vector2 d = Random.insideUnitCircle * cloudRadius;
            float centre = 1f - d.magnitude / cloudRadius;
            puffHome.Add(new Vector3(d.x, cloudHeight + Random.Range(-3f, 4f) + centre * 4f, d.y));
            puffSize.Add(Random.Range(12f, 22f) * (0.7f + 0.5f * centre));
            puffSpin.Add(Random.Range(-6f, 6f));
            puffs.Add(q.transform);
            puffRenderers.Add(r);
            block.SetFloat("_Seed", Random.Range(0f, 100f));
            r.SetPropertyBlock(block);
        }

        boltMat = MaterialFactory.NoFog(boltColor * 6f, 2455, false);
        flash = new GameObject("Red Flash").AddComponent<Light>();
        flash.transform.SetParent(transform, false);
        flash.type = LightType.Point;
        flash.color = boltColor;
        flash.range = 70f;
        flash.shadows = LightShadows.None;
        flash.enabled = false;
    }

    /// <summary>A bolt of red lightning out of the cloud, forking down to the ground near it.</summary>
    public void Strike(bool big)
    {
        Vector2 d = Random.insideUnitCircle * (big ? 4f : 12f);
        var generator = FindAnyObjectByType<DarkForestGenerator>();
        Vector3 end = ground + new Vector3(d.x, 0f, d.y);
        if (generator != null) end.y = generator.GetGroundHeight(end.x, end.z);
        Vector2 s = Random.insideUnitCircle * cloudRadius * 0.4f;
        Vector3 start = ground + new Vector3(s.x, cloudHeight - 2f, s.y);

        // The main channel and a few forks off it.
        var main = Jagged(start, end, 14, big ? 2.2f : 1.5f);
        var shapes = new List<Vector3[]> { main };
        int forks = Random.Range(big ? 2 : 1, big ? 5 : 3);
        for (int f = 0; f < forks; f++)
        {
            int from = Random.Range(2, main.Length - 4);
            Vector3 dir = (end - start).normalized + Random.insideUnitSphere * 0.9f;
            shapes.Add(Jagged(main[from], main[from] + dir.normalized * Random.Range(5f, 12f), 6, 1f));
        }
        while (bolts.Count < shapes.Count)
        {
            var lr = new GameObject("Bolt").AddComponent<LineRenderer>();
            lr.transform.SetParent(transform, false);
            lr.sharedMaterial = boltMat;
            lr.useWorldSpace = true;
            lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            lr.receiveShadows = false;
            bolts.Add(lr);
        }
        for (int i = 0; i < bolts.Count; i++)
        {
            bool used = i < shapes.Count;
            bolts[i].enabled = used;
            if (!used) continue;
            bolts[i].positionCount = shapes[i].Length;
            bolts[i].SetPositions(shapes[i]);
            float w = (i == 0 ? 0.45f : 0.18f) * (big ? 1.4f : 1f);
            bolts[i].startWidth = w;
            bolts[i].endWidth = w * 0.4f;
        }
        boltAge = 0f;
        glow = big ? 1f : 0.7f;
        flash.transform.position = Vector3.Lerp(start, end, 0.6f);
        flashPeak = big ? 60f : 35f;

        ProceduralAudio.PlayAt(ProceduralAudio.Thunder, flash.transform.position, big ? 1f : 0.7f, Random.Range(0.7f, 0.9f), 35f);
    }

    static Vector3[] Jagged(Vector3 a, Vector3 b, int segments, float jag)
    {
        var points = new Vector3[segments + 1];
        Vector3 side = Vector3.Cross(b - a, Vector3.up).normalized;
        Vector3 off = Vector3.zero;
        for (int i = 0; i <= segments; i++)
        {
            float t = i / (float)segments;
            off += (side * Random.Range(-1f, 1f) + Random.insideUnitSphere * 0.5f) * jag;
            off *= 0.7f;
            points[i] = Vector3.Lerp(a, b, t) + (i == 0 || i == segments ? Vector3.zero : off);
        }
        return points;
    }

    // The flicker of a strike: (seconds since, brightness).
    static readonly Vector2[] Flicker =
    {
        new Vector2(0f, 1f), new Vector2(0.06f, 0.2f), new Vector2(0.1f, 1f), new Vector2(0.18f, 0.4f),
        new Vector2(0.25f, 0.85f), new Vector2(0.36f, 0.3f), new Vector2(0.55f, 0f),
    };

    static float FlickerAt(float t)
    {
        if (t >= Flicker[Flicker.Length - 1].x) return 0f;
        for (int i = 0; i < Flicker.Length - 1; i++)
            if (t < Flicker[i + 1].x) return Mathf.Lerp(Flicker[i].y, Flicker[i + 1].y, (t - Flicker[i].x) / (Flicker[i + 1].x - Flicker[i].x));
        return 0f;
    }

    void Update()
    {
        float dt = Time.deltaTime;
        age += dt;
        // Rolling in, hanging, thinning out.
        float presence = Mathf.SmoothStep(0f, 1f, age / 3f) * (1f - Mathf.SmoothStep(0f, 1f, (age - (lifetime - 6f)) / 6f));
        if (age >= lifetime) { Destroy(gameObject); return; }

        // Its own small strikes now and then, between the big ones.
        if (age >= nextStrike && age < lifetime - 6f)
        {
            Strike(false);
            nextStrike = age + Random.Range(1.5f, 4f);
        }

        // The bolts flicker and are gone.
        boltAge += dt;
        float b = FlickerAt(boltAge);
        foreach (var lr in bolts) if (lr.enabled) lr.enabled = b > 0.02f;
        flash.enabled = b > 0.02f;
        flash.intensity = flashPeak * b;
        glow = Mathf.MoveTowards(glow, 0f, dt * 2.5f);

        // The billows: churning slowly, facing the camera, lit red from inside with each strike.
        var cam = Camera.main;
        if (cam == null) return;
        float t = Time.time;
        Color c = Color.Lerp(cloudColor, new Color(0.5f, 0.05f, 0.03f, cloudColor.a), Mathf.Max(glow * b, glow * 0.35f));
        c.a = cloudColor.a * presence;
        for (int i = 0; i < puffs.Count; i++)
        {
            Vector3 home = puffHome[i];
            Vector3 churn = new Vector3(Mathf.Sin(t * 0.13f + i), Mathf.Sin(t * 0.21f + i * 1.7f) * 0.5f, Mathf.Cos(t * 0.11f + i * 0.6f)) * 2.5f;
            Vector3 spread = new Vector3(home.x, 0f, home.z) * (1f + (1f - presence) * 0.6f); // gathering in, drifting apart
            puffs[i].position = ground + spread + Vector3.up * home.y + churn;
            puffs[i].rotation = Quaternion.LookRotation(puffs[i].position - cam.transform.position) * Quaternion.Euler(0f, 0f, t * puffSpin[i]);
            puffs[i].localScale = Vector3.one * puffSize[i] * (0.6f + 0.4f * presence);
            puffRenderers[i].GetPropertyBlock(block);
            block.SetColor("_Color", c);
            puffRenderers[i].SetPropertyBlock(block);
        }
    }
}
