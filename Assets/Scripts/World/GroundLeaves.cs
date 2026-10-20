using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Dead leaves lying on the forest floor around the player, that move: strong gusts lift some and skitter
/// them along; anything going fast enough through them (the player running, galloping deer, scurrying rats,
/// the blind giant, an entity creeping closer) kicks them up into the air, tumbling, before they flutter
/// back down. Walking just nudges them aside. Drawn with GPU instancing (no GameObjects), and recycled
/// round the player as they move, so there are always leaves underfoot.
/// Things that disturb leaves call Register. DarkForestGenerator adds this automatically.
/// </summary>
public class GroundLeaves : MonoBehaviour
{
    public int leafCount = 1000;
    [Tooltip("Leaves lie within this distance of the player.")]
    public float radius = 22f;
    [Tooltip("Something moving faster than this (m/s) kicks leaves into the air; slower just nudges them.")]
    public float kickSpeed = 3.4f;
    [Tooltip("Gusts stronger than this (WeatherSystem.CurrentWind) start lifting leaves.")]
    public float gustThreshold = 0.75f;

    struct Leaf
    {
        public Vector3 position, velocity, spin;
        public Quaternion rotation;
        public float size;
        public int colour;
        public bool flying;
    }

    class Disturber
    {
        public Transform transform;
        public float radius;
        public Vector3 last;
        public bool seen;
    }

    static readonly List<Disturber> disturbers = new List<Disturber>();

    /// <summary>Something that scatters leaves as it moves through them, with the radius of its body or feet.</summary>
    public static void Register(Transform t, float bodyRadius)
    {
        if (t == null) return;
        disturbers.Add(new Disturber { transform = t, radius = bodyRadius });
    }

    Leaf[] leaves;
    Mesh mesh;
    Material[] materials;
    readonly List<Matrix4x4>[] batches = new List<Matrix4x4>[4];
    Transform player;
    CharacterController playerBody;
    Vector3 playerLast;
    DarkForestGenerator generator;
    WeatherSystem weather;
    bool placed;

    static readonly Color[] Colours =
    {
        new Color(0.16f, 0.08f, 0.03f), new Color(0.22f, 0.12f, 0.04f), new Color(0.12f, 0.1f, 0.05f), new Color(0.2f, 0.05f, 0.02f),
    };

    void Start()
    {
        generator = FindAnyObjectByType<DarkForestGenerator>();
        mesh = BuildLeafMesh();
        materials = new Material[Colours.Length];
        for (int i = 0; i < Colours.Length; i++)
        {
            materials[i] = MaterialFactory.Lit(Colours[i], 0.1f);
            materials[i].enableInstancing = true;
            batches[i] = new List<Matrix4x4>();
        }
        leaves = new Leaf[leafCount];
    }

    void Update()
    {
        if (player == null)
        {
            var controller = FindAnyObjectByType<FirstPersonController>();
            if (controller == null) return;
            player = controller.transform;
            playerBody = controller.GetComponent<CharacterController>();
            playerLast = player.position;
        }
        if (weather == null) weather = FindAnyObjectByType<WeatherSystem>();
        if (!placed)
        {
            for (int i = 0; i < leaves.Length; i++) Respawn(ref leaves[i], Random.insideUnitCircle * radius);
            placed = true;
        }

        float dt = Mathf.Min(Time.deltaTime, 0.05f);
        if (dt <= 0f) return;
        Vector3 you = player.position;

        // Who's moving, and how fast (from how far they went this frame).
        var movers = new List<(Vector3 pos, Vector3 vel, float radius)>();
        Vector3 playerVel = (you - playerLast).sqrMagnitude > 25f ? Vector3.zero : (you - playerLast) / dt; // >5m: a teleport
        playerLast = you;
        movers.Add((you, playerVel, playerBody != null ? playerBody.radius : 0.4f));
        for (int i = disturbers.Count - 1; i >= 0; i--)
        {
            var d = disturbers[i];
            if (d.transform == null) { disturbers.RemoveAt(i); continue; }
            Vector3 p = d.transform.position;
            // An entity's creep is a sudden jump of a couple of metres: that counts (it kicks the leaves where it
            // lands). Anything over 10m is a respawn or the map looping, not movement.
            Vector3 v = d.seen && (p - d.last).sqrMagnitude < 100f ? (p - d.last) / dt : Vector3.zero;
            d.last = p;
            d.seen = true;
            if (!d.transform.gameObject.activeInHierarchy) continue;
            if (v.sqrMagnitude < 0.5f) continue;
            if ((p - you).sqrMagnitude > (radius + 5f) * (radius + 5f)) continue;
            movers.Add((p, v, d.radius));
        }

        float wind = weather != null ? weather.CurrentWind : 0f;
        Vector3 windDir = weather != null ? weather.WindVector : Vector3.forward;

        for (int i = 0; i < leaves.Length; i++)
        {
            ref Leaf leaf = ref leaves[i];

            // Too far behind: lay it down again somewhere around the player (mostly ahead of where they're going).
            Vector3 off = leaf.position - you;
            off.y = 0f;
            if (off.sqrMagnitude > radius * radius * 1.3f)
            {
                Vector2 dir = Random.insideUnitCircle.normalized;
                if (playerVel.sqrMagnitude > 1f && Random.value < 0.7f)
                    dir = (new Vector2(playerVel.x, playerVel.z).normalized + Random.insideUnitCircle * 0.8f).normalized;
                Respawn(ref leaf, dir * radius * Random.Range(0.85f, 1f));
                continue;
            }

            if (!leaf.flying)
            {
                // Gusts pick a few up.
                if (wind > gustThreshold && Random.value < (wind - gustThreshold) * 0.25f * dt)
                    Kick(ref leaf, windDir * Random.Range(1.5f, 3.5f) * wind, Random.Range(0.3f, 1.1f));

                // Anything passing through.
                foreach (var (pos, vel, r) in movers)
                {
                    Vector3 d = leaf.position - pos;
                    d.y = 0f;
                    float speed = Mathf.Min(new Vector3(vel.x, 0f, vel.z).magnitude, 9f); // a sudden jump still only kicks so hard
                    float reach = r + 0.25f + speed * 0.06f;
                    if (d.sqrMagnitude > reach * reach || speed < 0.6f) continue;
                    Vector3 away = d.sqrMagnitude > 0.0001f ? d.normalized : Random.insideUnitSphere;
                    away.y = 0f;
                    if (speed >= kickSpeed)
                        Kick(ref leaf, (away * 0.6f + vel.normalized) * speed * Random.Range(0.3f, 0.6f), Random.Range(0.6f, 1.2f) + speed * 0.12f);
                    else
                        leaf.position += away * (reach - d.magnitude) * 0.5f; // just nudged aside
                    break;
                }
                leaf.position.y = Ground(leaf.position) + 0.012f;
            }
            else
            {
                // Tumbling down: heavy drag, a little lift, carried on the wind.
                leaf.velocity += (Vector3.down * 2.6f + windDir * wind * 1.2f) * dt;
                leaf.velocity *= Mathf.Exp(-1.6f * dt);
                leaf.velocity += new Vector3(Mathf.Sin(Time.time * 7f + i), 0f, Mathf.Cos(Time.time * 5f + i)) * 0.8f * dt; // flutter
                leaf.position += leaf.velocity * dt;
                leaf.rotation = Quaternion.Euler(leaf.spin * dt) * leaf.rotation;
                float ground = Ground(leaf.position) + 0.012f;
                if (leaf.position.y <= ground && leaf.velocity.y < 0f)
                {
                    leaf.position.y = ground;
                    leaf.flying = false;
                    leaf.rotation = Flat();
                }
            }
        }

        Draw();
    }

    void Kick(ref Leaf leaf, Vector3 horizontal, float up)
    {
        leaf.flying = true;
        leaf.velocity = new Vector3(horizontal.x, up, horizontal.z);
        leaf.spin = Random.onUnitSphere * Random.Range(200f, 700f);
    }

    void Respawn(ref Leaf leaf, Vector2 offset)
    {
        Vector3 p = player.position + new Vector3(offset.x, 0f, offset.y);
        p.y = Ground(p) + 0.012f;
        leaf.position = p;
        leaf.velocity = Vector3.zero;
        leaf.flying = false;
        leaf.rotation = Flat();
        leaf.size = Random.Range(0.6f, 1.3f);
        leaf.colour = Random.Range(0, Colours.Length);
    }

    static Quaternion Flat() => Quaternion.Euler(Random.Range(-6f, 6f), Random.Range(0f, 360f), Random.Range(-6f, 6f));

    float Ground(Vector3 p) => generator != null ? generator.GetGroundHeight(p.x, p.z) : 0f;

    void Draw()
    {
        if (PortalDoor.PlayerInRoom) return; // not down there
        foreach (var b in batches) b.Clear();
        for (int i = 0; i < leaves.Length; i++)
        {
            var leaf = leaves[i];
            batches[leaf.colour].Add(Matrix4x4.TRS(leaf.position, leaf.rotation, new Vector3(0.07f, 1f, 0.05f) * leaf.size));
        }
        for (int c = 0; c < batches.Length; c++)
        {
            var list = batches[c];
            for (int start = 0; start < list.Count; start += 1023)
            {
                int count = Mathf.Min(1023, list.Count - start);
                Graphics.DrawMeshInstanced(mesh, 0, materials[c], list.GetRange(start, count), null,
                    UnityEngine.Rendering.ShadowCastingMode.Off, true);
            }
        }
    }

    /// <summary>A leaf shape, flat in XZ and two-sided: a pointed oval with a slight fold down the middle.</summary>
    static Mesh BuildLeafMesh()
    {
        var verts = new[]
        {
            new Vector3(-1f, 0f, 0f), new Vector3(0f, 0.05f, 0.5f), new Vector3(0f, 0.08f, 0f), new Vector3(0f, 0.05f, -0.5f), new Vector3(0.6f, 0.02f, 0f),
        };
        // Top faces, then the same faces wound the other way for the underside.
        var tris = new[] { 0, 1, 2, 0, 2, 3, 2, 1, 4, 3, 2, 4, 0, 2, 1, 0, 3, 2, 2, 4, 1, 3, 4, 2 };
        var all = new Vector3[tris.Length];
        var flatTris = new int[tris.Length];
        for (int i = 0; i < tris.Length; i++) { all[i] = verts[tris[i]]; flatTris[i] = i; }
        var mesh = new Mesh { name = "Leaf" };
        mesh.vertices = all;
        mesh.triangles = flatTris;
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }
}
