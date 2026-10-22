using UnityEngine;

/// <summary>
/// Leaves drifting down from the trees around the player: each lets go from somewhere in a tree's crown,
/// tumbles and flutters as it falls, gets carried along by the wind (WeatherSystem), settles on the ground,
/// and after a little while shrinks away to nothing (strong gusts can pick a fallen one up and skitter it
/// along first). Then it's recycled: off another tree near the player.
/// DarkForestGenerator adds this automatically.
/// </summary>
public class FallingLeaves : MonoBehaviour
{
    void Awake() => Tuning.Apply(this); // saved tuning (Save Tuning on the component's menu)

    public int leafCount = 140;
    [Tooltip("Leaves fall within this distance of the player.")]
    public float radius = 25f;
    public float minFallHeight = 6f;
    public float maxFallHeight = 14f;
    [Tooltip("How fast leaves sink, in metres per second.")]
    public float fallSpeed = 0.9f;
    [Tooltip("How strongly the wind carries them.")]
    public float windCarry = 2.5f;
    [Tooltip("Seconds a leaf lies on the ground before being recycled.")]
    public float minRestTime = 3f;
    public float maxRestTime = 7f;
    [Tooltip("Seconds a leaf takes to shrink away once it's done lying there.")]
    public float fadeTime = 1.2f;
    [Tooltip("How far out from a tree's trunk leaves let go of it.")]
    public float crownRadius = 2.8f;

    enum State { Falling, Resting, Blowing }

    class Leaf
    {
        public Transform transform;
        public State state;
        public float timer, phase, flutter, spin;
        public Vector3 drift;
        public Vector3 scale;
    }

    Leaf[] leaves;
    DarkForestGenerator generator;
    WeatherSystem weather;
    Transform player;

    void Start()
    {
        generator = FindAnyObjectByType<DarkForestGenerator>();
        weather = FindAnyObjectByType<WeatherSystem>();

        // A few autumn shades, dark to suit the night.
        var materials = new[]
        {
            MaterialFactory.Lit(new Color(0.3f, 0.14f, 0.04f), 0.15f),
            MaterialFactory.Lit(new Color(0.38f, 0.22f, 0.05f), 0.15f),
            MaterialFactory.Lit(new Color(0.2f, 0.17f, 0.06f), 0.15f),
            MaterialFactory.Lit(new Color(0.34f, 0.08f, 0.03f), 0.15f),
            MaterialFactory.Lit(new Color(0.16f, 0.1f, 0.05f), 0.15f),
        };
        var shapes = new[] { LeafMesh(0.32f, 0.5f), LeafMesh(0.42f, 0.35f), LeafMesh(0.26f, 0.7f) };

        leaves = new Leaf[leafCount];
        for (int i = 0; i < leafCount; i++)
        {
            var go = new GameObject("Leaf");
            go.transform.SetParent(transform, false);
            float size = Random.Range(0.08f, 0.13f);
            go.transform.localScale = Vector3.one * size;
            go.AddComponent<MeshFilter>().sharedMesh = shapes[Random.Range(0, shapes.Length)];
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = materials[Random.Range(0, materials.Length)];
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            WorldWrap.RegisterDynamic(go.transform);
            leaves[i] = new Leaf { transform = go.transform, state = State.Resting, timer = -1f, scale = go.transform.localScale };
        }
    }

    void Update()
    {
        if (player == null)
        {
            var controller = FindAnyObjectByType<FirstPersonController>();
            if (controller == null) return;
            player = controller.transform;
            // Start them part-way through falling so the air is already full of them.
            foreach (var leaf in leaves) Respawn(leaf, Random.Range(0.5f, maxFallHeight));
        }
        if (weather == null) weather = FindAnyObjectByType<WeatherSystem>();

        float dt = Time.deltaTime;
        Vector3 wind = weather != null ? weather.WindVector * weather.CurrentWind : Vector3.zero;

        foreach (var leaf in leaves)
        {
            Vector3 pos = leaf.transform.position;
            Vector3 fromPlayer = pos - player.position;
            fromPlayer.y = 0f;
            if (fromPlayer.magnitude > radius * 1.4f) { Respawn(leaf, Random.Range(minFallHeight, maxFallHeight)); continue; }

            float ground = GroundHeight(pos);
            switch (leaf.state)
            {
                case State.Falling:
                {
                    // Sink with a side-to-side flutter, carried by the wind, tumbling.
                    leaf.phase += dt * leaf.flutter;
                    Vector3 sway = new Vector3(Mathf.Sin(leaf.phase), 0f, Mathf.Cos(leaf.phase * 0.7f)) * 0.6f;
                    pos += (wind * windCarry + leaf.drift + sway - Vector3.up * fallSpeed * (0.7f + 0.3f * Mathf.Sin(leaf.phase * 2f))) * dt;
                    leaf.transform.rotation *= Quaternion.Euler(leaf.spin * dt, leaf.spin * 0.6f * dt, leaf.spin * 0.3f * dt);
                    if (pos.y <= ground + 0.01f) Land(leaf, ref pos, ground);
                    break;
                }

                case State.Resting:
                    pos.y = ground + 0.005f;
                    leaf.timer -= dt;
                    // Its time's up: it shrinks away into the leaf litter.
                    leaf.transform.localScale = leaf.scale * Mathf.Clamp01(1f + leaf.timer / Mathf.Max(fadeTime, 0.01f));
                    if (leaf.timer < 0f && leaf.timer > -fadeTime) break;
                    // A strong gust can pick it up again.
                    if (weather != null && weather.CurrentWind > 0.85f && Random.value < 0.15f * dt)
                    {
                        leaf.state = State.Blowing;
                        leaf.timer = Random.Range(0.6f, 2f);
                    }
                    else if (leaf.timer <= -fadeTime)
                    {
                        Respawn(leaf, Random.Range(minFallHeight, maxFallHeight));
                        continue;
                    }
                    break;

                case State.Blowing:
                    // Skitter along the ground in little hops.
                    leaf.timer -= dt;
                    leaf.phase += dt * 9f;
                    pos += (wind.normalized * 3f + leaf.drift) * dt;
                    pos.y = ground + 0.01f + Mathf.Abs(Mathf.Sin(leaf.phase)) * 0.25f;
                    leaf.transform.rotation *= Quaternion.Euler(400f * dt, 250f * dt, 0f);
                    if (leaf.timer <= 0f) Land(leaf, ref pos, ground);
                    break;
            }
            if (leaf.state != State.Resting) leaf.transform.localScale = leaf.scale;

            leaf.transform.position = pos;
        }
    }

    void Land(Leaf leaf, ref Vector3 pos, float ground)
    {
        pos.y = ground + 0.005f;
        leaf.state = State.Resting;
        leaf.timer = Random.Range(minRestTime, maxRestTime);
        leaf.transform.rotation = Quaternion.Euler(Random.Range(-6f, 6f), Random.Range(0f, 360f), Random.Range(-6f, 6f));
    }

    void Respawn(Leaf leaf, float height)
    {
        // Off a tree near the player: somewhere out in its crown.
        Vector3 p = player.position;
        bool found = false;
        if (generator != null && generator.TreePositions.Count > 0)
            for (int tries = 0; tries < 12 && !found; tries++)
            {
                Vector2 t = generator.TreePositions[Random.Range(0, generator.TreePositions.Count)];
                Vector3 tree = WorldWrap.NearestImage(new Vector3(t.x, 0f, t.y), player.position);
                Vector3 off = tree - player.position;
                off.y = 0f;
                if (off.magnitude > radius) continue;
                Vector2 spread = Random.insideUnitCircle * crownRadius;
                p = tree + new Vector3(spread.x, 0f, spread.y);
                found = true;
            }
        if (!found)
        {
            Vector2 offset = Random.insideUnitCircle * radius;
            p = player.position + new Vector3(offset.x, 0f, offset.y);
        }
        p.y = GroundHeight(p) + height;
        leaf.transform.localScale = leaf.scale;
        leaf.transform.SetPositionAndRotation(p, Random.rotation);
        leaf.state = State.Falling;
        leaf.phase = Random.Range(0f, 10f);
        leaf.flutter = Random.Range(1.5f, 3.5f);
        leaf.spin = Random.Range(60f, 240f) * (Random.value < 0.5f ? -1f : 1f);
        leaf.drift = new Vector3(Random.Range(-0.3f, 0.3f), 0f, Random.Range(-0.3f, 0.3f));
    }

    /// <summary>
    /// A leaf (pivot at its middle, about 1 long on z): a pointed oval, wider toward the stalk end by
    /// "shape", curled along its midrib by "curl", with a little stalk; both faces have their own vertices.
    /// </summary>
    static Mesh LeafMesh(float width, float curl)
    {
        var verts = new System.Collections.Generic.List<Vector3>();
        var tris = new System.Collections.Generic.List<int>();
        const int steps = 7;
        for (int face = 0; face < 2; face++)
        {
            int start = verts.Count;
            for (int k = 0; k <= steps; k++)
            {
                float t = k / (float)steps;               // stalk end (0) to tip (1)
                float z = t - 0.5f;
                float w = width * Mathf.Sin(Mathf.PI * Mathf.Pow(t, 0.8f)); // wide near the base, pointed at the tip
                float lift = curl * w * w;                 // the edges curl up off the midrib
                float bow = -0.06f * Mathf.Sin(Mathf.PI * t); // and the whole leaf bows a little
                verts.Add(new Vector3(-w, lift + bow, z));
                verts.Add(new Vector3(0f, bow, z));
                verts.Add(new Vector3(w, lift + bow, z));
            }
            for (int k = 0; k < steps; k++)
                for (int c = 0; c < 2; c++)
                {
                    int a = start + k * 3 + c, b = a + 1, d = a + 3, e = d + 1;
                    if (face == 0) tris.AddRange(new[] { a, d, b, b, d, e });
                    else tris.AddRange(new[] { a, b, d, b, e, d });
                }
        }
        // The stalk.
        int s0 = verts.Count;
        verts.Add(new Vector3(-0.01f, -0.005f, -0.5f));
        verts.Add(new Vector3(0.01f, -0.005f, -0.5f));
        verts.Add(new Vector3(0f, 0.01f, -0.62f));
        tris.AddRange(new[] { s0, s0 + 2, s0 + 1, s0, s0 + 1, s0 + 2 });
        var mesh = new Mesh { name = "Leaf" };
        mesh.SetVertices(verts);
        mesh.SetTriangles(tris, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }

    float GroundHeight(Vector3 p) => generator != null ? generator.GetGroundHeight(p.x, p.z) : 0f;
}
