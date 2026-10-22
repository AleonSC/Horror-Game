using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Vines hanging from a few trees' branches (DarkForestGenerator.VineAnchors), each a rope of points
/// simulated with Verlet physics: gravity, gusts of wind (WeatherSystem), the branch swaying above it,
/// and anything walking through: the player and the entities shove them aside, so they swing and settle
/// after you've brushed past (or after something unseen has). Long ones trail on the ground.
/// Only vines near the camera are simulated; distant ones hold still. DarkForestGenerator adds this.
/// </summary>
public class VineSystem : MonoBehaviour
{
    void Awake() => Tuning.Apply(this); // saved tuning (Inspector changes made in Play are kept)

    [Tooltip("Only vines within this distance of the camera move.")]
    public float simulateRadius = 45f;
    [Tooltip("Metres between rope points.")]
    public float segmentLength = 0.3f;
    public float gravity = 9.81f;
    [Range(0.9f, 1f)] public float damping = 0.985f;
    [Tooltip("How hard the wind pushes them.")]
    public float windForce = 2.5f;
    public int constraintIterations = 6;
    public float thickness = 0.018f;
    [Tooltip("Chance of a little leaf at each point along a vine.")]
    [Range(0f, 1f)] public float leafChance = 0.45f;

    const int Sides = 5;

    class Vine
    {
        public Transform sway;
        public Vector3 local;
        public Vector3[] points, previous;
        public Vector3 lastAnchor;
        public Mesh mesh;
        public Renderer renderer;
        public Vector3[] vertices;
        public List<(int point, float angle, float size)> leaves;
        public float seed;
    }

    readonly List<Vine> vines = new List<Vine>();
    DarkForestGenerator generator;
    WeatherSystem weather;
    FirstPersonController player;
    CharacterController playerBody;
    Material material;

    void Start()
    {
        generator = FindAnyObjectByType<DarkForestGenerator>();
        if (generator == null) { enabled = false; return; }
        material = MaterialFactory.Lit(new Color(0.05f, 0.075f, 0.035f), 0.15f);

        foreach (var anchor in generator.VineAnchors)
        {
            if (anchor.sway == null) continue;
            vines.Add(CreateVine(anchor));
        }
    }

    Vine CreateVine(DarkForestGenerator.VineAnchor anchor)
    {
        int count = Mathf.Max(3, Mathf.CeilToInt(anchor.length / segmentLength) + 1);
        var vine = new Vine
        {
            sway = anchor.sway,
            local = anchor.local,
            points = new Vector3[count],
            previous = new Vector3[count],
            seed = Random.Range(0f, 100f),
            leaves = new List<(int, float, float)>(),
        };

        // Hanging straight down to start with (lying on the ground where it reaches it).
        Vector3 top = anchor.sway.TransformPoint(anchor.local);
        vine.lastAnchor = top;
        for (int i = 0; i < count; i++)
        {
            Vector3 p = top + Vector3.down * segmentLength * i;
            p.y = Mathf.Max(p.y, Ground(p) + 0.03f);
            vine.points[i] = vine.previous[i] = p;
        }
        for (int i = 2; i < count; i++)
            if (Random.value < leafChance) vine.leaves.Add((i, Random.Range(0f, 360f), Random.Range(0.6f, 1.3f)));

        var go = new GameObject("Vine");
        go.transform.SetParent(transform, false);
        vine.mesh = new Mesh { name = "Vine" };
        vine.mesh.MarkDynamic();
        vine.vertices = new Vector3[count * Sides + vine.leaves.Count * 8];
        vine.mesh.vertices = vine.vertices;
        vine.mesh.triangles = BuildTriangles(count, vine.leaves.Count);
        go.AddComponent<MeshFilter>().sharedMesh = vine.mesh;
        var renderer = go.AddComponent<MeshRenderer>();
        renderer.sharedMaterial = material;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        vine.renderer = renderer;
        UpdateMesh(vine);
        return vine;
    }

    void LateUpdate()
    {
        var cam = Camera.main;
        if (cam == null) return;
        if (weather == null) weather = FindAnyObjectByType<WeatherSystem>();
        if (player == null)
        {
            player = FindAnyObjectByType<FirstPersonController>();
            if (player != null) playerBody = player.GetComponent<CharacterController>();
        }

        float dt = Mathf.Min(Time.deltaTime, 1f / 30f);
        if (dt <= 0f) return;
        Vector3 eye = cam.transform.position;
        float radiusSq = simulateRadius * simulateRadius;

        foreach (var vine in vines)
        {
            // Its branch has gone (a fallen tree): hide it.
            bool alive = vine.sway != null && vine.sway.gameObject.activeInHierarchy;
            if (vine.renderer.enabled != alive) vine.renderer.enabled = alive;
            if (!alive) continue;

            Vector3 anchor = vine.sway.TransformPoint(vine.local);
            Vector3 jump = anchor - vine.lastAnchor;
            vine.lastAnchor = anchor;
            if (jump.sqrMagnitude > 25f)
            {
                // The map wrapped and the tree moved with it: carry the whole vine along.
                for (int i = 0; i < vine.points.Length; i++) { vine.points[i] += jump; vine.previous[i] += jump; }
            }

            if ((anchor - eye).sqrMagnitude > radiusSq) continue;
            Simulate(vine, anchor, dt);
            UpdateMesh(vine);
        }
    }

    void Simulate(Vine vine, Vector3 anchor, float dt)
    {
        Vector3[] p = vine.points, prev = vine.previous;
        int n = p.Length;

        // Wind: the overall gust plus a slow flutter of its own.
        Vector3 wind = Vector3.zero;
        if (weather != null)
        {
            float flutter = 0.5f + Mathf.PerlinNoise(Time.time * 0.7f, vine.seed);
            wind = weather.WindVector * weather.CurrentWind * windForce * flutter;
        }
        Vector3 accel = Vector3.down * gravity + wind;

        // Verlet step.
        for (int i = 1; i < n; i++)
        {
            Vector3 velocity = (p[i] - prev[i]) * damping;
            prev[i] = p[i];
            p[i] += velocity + accel * dt * dt;
        }
        p[0] = prev[0] = anchor;

        for (int iteration = 0; iteration < constraintIterations; iteration++)
        {
            // Keep each link its length, pinned at the top.
            for (int i = 0; i < n - 1; i++)
            {
                Vector3 d = p[i + 1] - p[i];
                float len = d.magnitude;
                if (len < 1e-5f) continue;
                Vector3 fix = d * ((len - segmentLength) / len);
                if (i == 0) p[i + 1] -= fix;
                else { p[i] += fix * 0.5f; p[i + 1] -= fix * 0.5f; }
            }

            // Bodies push it aside.
            if (playerBody != null && playerBody.enabled)
            {
                Vector3 feet = player.transform.position + playerBody.center - Vector3.up * (playerBody.height * 0.5f);
                PushOut(p, feet, playerBody.height, playerBody.radius + 0.05f);
            }
            foreach (var entity in ShadowEntity.All)
                if (entity != null && entity.IsVisible)
                    PushOut(p, entity.transform.position, 2.4f * entity.transform.localScale.y, 0.35f);

            // Lies on the ground rather than through it.
            for (int i = 1; i < n; i++)
            {
                float ground = Ground(p[i]) + 0.03f;
                if (p[i].y < ground)
                {
                    p[i].y = ground;
                    prev[i] = Vector3.Lerp(prev[i], p[i], 0.5f); // friction
                }
            }
        }
    }

    /// <summary>Moves points out of an upright capsule (feet position, height, radius).</summary>
    static void PushOut(Vector3[] p, Vector3 feet, float height, float radius)
    {
        for (int i = 1; i < p.Length; i++)
        {
            if (p[i].y < feet.y || p[i].y > feet.y + height) continue;
            Vector3 d = p[i] - feet;
            d.y = 0f;
            float distSq = d.sqrMagnitude;
            if (distSq >= radius * radius) continue;
            float dist = Mathf.Sqrt(distSq);
            Vector3 dir = dist > 1e-4f ? d / dist : Vector3.right;
            p[i] += dir * (radius - dist);
        }
    }

    float Ground(Vector3 p) => generator != null ? generator.GetGroundHeight(p.x, p.z) : 0f;

    // ---------------------------------------------------------------- Mesh

    static int[] BuildTriangles(int count, int leaves)
    {
        var tris = new List<int>();
        for (int i = 0; i < count - 1; i++)
        {
            for (int k = 0; k < Sides; k++)
            {
                int a = i * Sides + k, b = (i + 1) * Sides + k, c = i * Sides + (k + 1) % Sides, d = (i + 1) * Sides + (k + 1) % Sides;
                tris.Add(a); tris.Add(b); tris.Add(c);
                tris.Add(c); tris.Add(b); tris.Add(d);
            }
        }
        int leafStart = count * Sides;
        for (int l = 0; l < leaves; l++)
        {
            int v = leafStart + l * 8;
            // A diamond, front and back (separate vertices, so each face gets its own normal).
            tris.Add(v); tris.Add(v + 1); tris.Add(v + 2);
            tris.Add(v); tris.Add(v + 2); tris.Add(v + 3);
            tris.Add(v + 4); tris.Add(v + 6); tris.Add(v + 5);
            tris.Add(v + 4); tris.Add(v + 7); tris.Add(v + 6);
        }
        return tris.ToArray();
    }

    void UpdateMesh(Vine vine)
    {
        Vector3[] p = vine.points, verts = vine.vertices;
        int n = p.Length;
        Vector3 side = Vector3.right;
        for (int i = 0; i < n; i++)
        {
            Vector3 along = (p[Mathf.Min(i + 1, n - 1)] - p[Mathf.Max(i - 1, 0)]).normalized;
            if (along.sqrMagnitude < 0.5f) along = Vector3.down;
            side = Vector3.Cross(along, Mathf.Abs(along.y) > 0.9f ? Vector3.forward : Vector3.up).normalized;
            Vector3 up = Vector3.Cross(side, along);
            float r = thickness * Mathf.Lerp(1.2f, 0.5f, i / (float)(n - 1)); // thins toward the end
            for (int k = 0; k < Sides; k++)
            {
                float a = k / (float)Sides * Mathf.PI * 2f;
                verts[i * Sides + k] = p[i] + (side * Mathf.Cos(a) + up * Mathf.Sin(a)) * r;
            }
        }

        // Leaves hang off the stem, turning with it.
        int v = n * Sides;
        foreach (var (point, angle, size) in vine.leaves)
        {
            Vector3 along = (p[Mathf.Min(point + 1, n - 1)] - p[point - 1]).normalized;
            Vector3 outward = Quaternion.AngleAxis(angle, along) * Vector3.Cross(along, Vector3.forward).normalized;
            if (outward.sqrMagnitude < 0.5f) outward = Vector3.right;
            Vector3 width = Vector3.Cross(outward, along).normalized * 0.035f * size;
            Vector3 root = p[point];
            Vector3 tip = root + (outward * 0.09f + along * 0.04f) * size;
            Vector3 mid = (root + tip) * 0.5f;
            for (int face = 0; face < 2; face++)
            {
                verts[v++] = root;
                verts[v++] = mid + width;
                verts[v++] = tip;
                verts[v++] = mid - width;
            }
        }

        vine.mesh.vertices = verts;
        vine.mesh.RecalculateNormals();
        vine.mesh.RecalculateBounds();
    }
}
