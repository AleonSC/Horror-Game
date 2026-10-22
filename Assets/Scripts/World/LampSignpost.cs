using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// An old carved wooden signpost beside a lamp, its arrow boards pointing the way to the nearest lamps that
/// are still dark (the nearest highest): a squared post with a pointed cap and a collar under it, set in a
/// little cairn of stones, each board cut to an arrow, framed with a carved groove, nailed on, with a row of
/// notches cut into it, one for every thirty metres or so to that lamp. When the lamp a board points to is
/// lit, that board breaks: it cracks, drops, and hangs from one nail, swinging; and a new board is nailed up
/// pointing to the next dark one. LampSpawner puts one by every lamp.
/// </summary>
public class LampSignpost : MonoBehaviour
{
    static Material post, board, groove, metal, stone;
    /// <summary>Clears this class's static state (StaticReset calls it when Play starts).</summary>
    internal static void ResetStatics() => post = board = groove = metal = stone = null; // (forgotten: the scene's objects own them)

    const int Pointing = 2;                 // boards pointing at dark lamps at once
    static readonly float[] Slots = { 1.62f, 1.35f, 1.08f, 0.82f };

    class Board
    {
        public Transform arm;
        public Lamp target;
        public bool broken;
        public float slot, swing, yaw;
    }

    Lamp own;
    List<Lamp> all;
    readonly List<Board> boards = new List<Board>();
    float check;
    int timesLit = -1;

    public static void BuildAll(List<Lamp> lamps, DarkForestGenerator generator)
    {
        if (lamps.Count < 2) return;
        EnsureMaterials();
        foreach (var lamp in lamps) Build(lamp, lamps, generator);
    }

    static Vector3 Toward(Lamp from, Lamp to)
    {
        Vector3 d = WorldWrap.NearestImage(to.transform.position, from.transform.position) - from.transform.position;
        d.y = 0f;
        return d;
    }

    static void Build(Lamp lamp, List<Lamp> lamps, DarkForestGenerator generator)
    {
        // Beside the lamp, off to the side of the way to its nearest neighbour (so a board reaches past it).
        Lamp nearest = null;
        float best = float.MaxValue;
        foreach (var other in lamps)
            if (other != lamp && Toward(lamp, other).magnitude < best) { best = Toward(lamp, other).magnitude; nearest = other; }
        Vector3 first = nearest != null ? Toward(lamp, nearest).normalized : Vector3.forward;
        Vector3 side = Vector3.Cross(Vector3.up, first).normalized;
        Vector3 at = lamp.transform.position + side * 1.3f - first * 0.3f;
        if (generator != null) at.y = generator.GetGroundHeight(at.x, at.z);

        var root = new GameObject("Signpost").transform;
        root.SetParent(lamp.transform, true);
        root.position = at;
        root.rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
        root.localScale = Vector3.one;
        var sign = root.gameObject.AddComponent<LampSignpost>();
        sign.own = lamp;
        sign.all = lamps;

        // The post: squared, a little out of true, a pointed cap and a collar under it.
        var postT = Part(PrimitiveType.Cube, post, root, new Vector3(0f, 0.95f, 0f), new Vector3(0.12f, 2.1f, 0.12f));
        postT.localRotation = Quaternion.Euler(Random.Range(-2f, 2f), 0f, Random.Range(-2f, 2f));
        var postBox = root.gameObject.AddComponent<BoxCollider>();
        postBox.center = new Vector3(0f, 0.95f, 0f);
        postBox.size = new Vector3(0.14f, 2.1f, 0.14f);
        Part(PrimitiveType.Cube, groove, root, new Vector3(0f, 1.88f, 0f), new Vector3(0.15f, 0.04f, 0.15f)); // the collar
        var cap = Part(PrimitiveType.Cube, post, root, new Vector3(0f, 2.02f, 0f), new Vector3(0.1f, 0.1f, 0.1f));
        cap.localRotation = Quaternion.Euler(45f, 45f, 0f); // turned on its corner: a pointed top
        // A cairn of stones round its foot.
        for (int i = 0; i < 6; i++)
        {
            float a = i / 6f * Mathf.PI * 2f + Random.Range(-0.3f, 0.3f);
            float s = Random.Range(0.12f, 0.2f);
            var rock = Part(PrimitiveType.Sphere, stone, root, new Vector3(Mathf.Cos(a) * 0.15f, s * 0.25f, Mathf.Sin(a) * 0.15f), new Vector3(s, s * 0.7f, s * 0.9f));
            rock.localRotation = Quaternion.Euler(Random.Range(0f, 30f), Random.Range(0f, 360f), 0f);
        }
        sign.Refresh(false);
    }

    void Update()
    {
        check -= Time.deltaTime;
        if (check > 0f) return;
        check = 0.5f;
        // A new run (the lamps all dark again): fresh boards, pointing to the nearest.
        if (Lamp.TimesLit < timesLit)
        {
            foreach (var b in boards) if (b.arm != null) Destroy(b.arm.gameObject);
            boards.Clear();
        }
        timesLit = Lamp.TimesLit;
        Refresh(true);
    }

    void LateUpdate()
    {
        foreach (var b in boards)
        {
            if (!b.broken || b.arm == null) continue;
            b.swing += Time.deltaTime;
            float sway = Mathf.Sin(b.swing * 1.7f) * 6f * Mathf.Exp(-b.swing * 0.15f) + Mathf.Sin(Time.time * 0.9f + b.slot * 7f) * 1.5f;
            b.arm.localRotation = Quaternion.Euler(0f, b.yaw, -78f + sway);
        }
    }

    /// <summary>
    /// Breaks the boards whose lamp has been lit, and nails up new ones so there are always boards pointing to
    /// the nearest dark lamps (while there are any).
    /// </summary>
    void Refresh(bool withSound)
    {
        if (all == null || own == null) return;
        foreach (var b in boards)
            if (!b.broken && b.target != null && b.target.HasBeenLit) Break(b, withSound); // (lit at all: even if it's since run down)

        var pointedAt = new HashSet<Lamp>();
        int live = 0;
        foreach (var b in boards) if (!b.broken) { live++; pointedAt.Add(b.target); }
        if (live >= Pointing) return;

        var dark = new List<Lamp>();
        foreach (var l in all) if (l != null && l != own && !l.HasBeenLit && !pointedAt.Contains(l)) dark.Add(l);
        dark.Sort((x, y) => Toward(own, x).magnitude.CompareTo(Toward(own, y).magnitude));
        for (int i = 0; i < dark.Count && live < Pointing; i++)
        {
            float slot = FreeSlot();
            if (slot < 0f) break;
            AddBoard(dark[i], slot);
            live++;
        }
    }

    float FreeSlot()
    {
        foreach (float s in Slots)
        {
            bool taken = false;
            foreach (var b in boards) if (b.arm != null && Mathf.Approximately(b.slot, s)) taken = true;
            if (!taken) return s;
        }
        // All taken: the oldest broken board has fallen off by now.
        foreach (var b in boards)
            if (b.broken && b.arm != null)
            {
                float s = b.slot;
                Destroy(b.arm.gameObject);
                boards.Remove(b);
                return s;
            }
        return -1f;
    }

    /// <summary>It cracks off its top nail and drops, hanging by the other, point down.</summary>
    void Break(Board b, bool withSound)
    {
        b.broken = true;
        b.swing = 0f;
        if (b.arm == null) return;
        b.yaw = b.arm.localEulerAngles.y;
        // Hung from the nail at the post end (where the board's pivot is).
        if (withSound) ProceduralAudio.PlayAt(ProceduralAudio.TwigSnap, b.arm.position, 0.6f, Random.Range(0.7f, 0.9f), 4f);
        foreach (var r in b.arm.GetComponentsInChildren<Renderer>())
            if (r.sharedMaterial == board) r.sharedMaterial = groove; // dark, split wood: it reads as dead at a glance
    }

    void AddBoard(Lamp target, float height)
    {
        Vector3 d = Toward(own, target);
        float dist = d.magnitude;
        Vector3 dir = d.normalized;
        int index = boards.Count;
        var arm = new GameObject("Board").transform;
        arm.SetParent(transform, false);
        arm.localPosition = new Vector3(0f, height, 0f);
        arm.rotation = Quaternion.LookRotation(Vector3.Cross(dir, Vector3.up), Vector3.up) * Quaternion.Euler(0f, 0f, Random.Range(-3f, 3f)); // +x along the way it points
        const float length = 0.7f, width = 0.16f, thick = 0.03f;
        float reachOut = length * 0.5f - 0.04f; // most of it out past the post
        float faceSide = index % 2 == 0 ? 1f : -1f;
        var plank = new GameObject("Arrow");
        plank.transform.SetParent(arm, false);
        plank.transform.localPosition = new Vector3(reachOut, 0f, faceSide * 0.075f); // either face of the post
        plank.AddComponent<MeshFilter>().sharedMesh = ArrowMesh(length, width, thick);
        var r = plank.AddComponent<MeshRenderer>();
        r.sharedMaterial = board;
        // A carved groove framing it, two nails at the post end, and the notches counting the distance.
        Vector3 face = new Vector3(0f, 0f, faceSide * (thick * 0.5f + 0.001f));
        foreach (float y in new[] { -width * 0.33f, width * 0.33f })
            Part(PrimitiveType.Cube, groove, plank.transform, face + new Vector3(-0.05f, y, 0f), new Vector3(length * 0.72f, 0.006f, 0.004f));
        foreach (float y in new[] { -0.035f, 0.035f })
            Part(PrimitiveType.Sphere, metal, plank.transform, face + new Vector3(-length * 0.5f + 0.06f, y, 0f), new Vector3(0.012f, 0.012f, 0.006f));
        int notches = Mathf.Clamp(Mathf.RoundToInt(dist / 30f), 1, 6);
        for (int n = 0; n < notches; n++)
            Part(PrimitiveType.Cube, groove, plank.transform, face + new Vector3(-0.12f + n * 0.035f, 0f, 0f), new Vector3(0.008f, width * 0.36f, 0.004f));
        boards.Add(new Board { arm = arm, target = target, slot = height });
    }

    /// <summary>A plank cut to an arrow (pointing along +x, centred): a rectangle with a pointed end, flat shaded.</summary>
    static Mesh ArrowMesh(float length, float width, float thick)
    {
        float hl = length * 0.5f, hw = width * 0.5f, ht = thick * 0.5f, neck = hl - width * 0.55f;
        // The outline, round the edge: the square back end, along to the shoulders, out to the point.
        Vector2[] o =
        {
            new Vector2(-hl, -hw), new Vector2(neck, -hw), new Vector2(hl, 0f), new Vector2(neck, hw), new Vector2(-hl, hw),
        };
        var verts = new List<Vector3>();
        var tris = new List<int>();
        void Tri(Vector3 a, Vector3 b, Vector3 c, Vector3 outward)
        {
            if (Vector3.Dot(Vector3.Cross(b - a, c - a), outward) < 0f) (b, c) = (c, b);
            int i = verts.Count;
            verts.Add(a); verts.Add(b); verts.Add(c);
            tris.Add(i); tris.Add(i + 1); tris.Add(i + 2);
        }
        // The two faces (fanned from the back end's middle), and the edges round them.
        for (int f = -1; f <= 1; f += 2)
        {
            Vector3 centre = new Vector3(-hl * 0.2f, 0f, f * ht);
            for (int k = 0; k < o.Length; k++)
            {
                Vector2 p = o[k], q = o[(k + 1) % o.Length];
                Tri(centre, new Vector3(p.x, p.y, f * ht), new Vector3(q.x, q.y, f * ht), new Vector3(0f, 0f, f));
            }
        }
        for (int k = 0; k < o.Length; k++)
        {
            Vector2 p = o[k], q = o[(k + 1) % o.Length];
            Vector3 outward = new Vector3(q.y - p.y, -(q.x - p.x), 0f);
            if (Vector3.Dot(outward, new Vector3((p.x + q.x) * 0.5f, (p.y + q.y) * 0.5f, 0f)) < 0f) outward = -outward;
            Vector3 a = new Vector3(p.x, p.y, -ht), b = new Vector3(q.x, q.y, -ht), c = new Vector3(q.x, q.y, ht), d = new Vector3(p.x, p.y, ht);
            Tri(a, b, c, outward);
            Tri(a, c, d, outward);
        }
        var mesh = new Mesh { name = "Arrow Board" };
        mesh.SetVertices(verts);
        mesh.SetTriangles(tris, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }

    static void EnsureMaterials()
    {
        if (post != null) return;
        post = MaterialFactory.LitTextured(ProceduralTextures.Bark, ProceduralTextures.BarkNormal, new Color(0.42f, 0.33f, 0.24f), 0.05f);
        board = MaterialFactory.Lit(new Color(0.24f, 0.17f, 0.1f), 0.08f);
        groove = MaterialFactory.Lit(new Color(0.08f, 0.055f, 0.035f), 0.05f);
        metal = MaterialFactory.Lit(new Color(0.12f, 0.1f, 0.09f), 0.4f);
        stone = MaterialFactory.Lit(new Color(0.16f, 0.16f, 0.15f), 0.1f);
    }

    static Transform Part(PrimitiveType type, Material mat, Transform parent, Vector3 pos, Vector3 scale)
    {
        var go = GameObject.CreatePrimitive(type);
        Object.Destroy(go.GetComponent<Collider>());
        go.transform.SetParent(parent, false);
        go.transform.localPosition = pos;
        go.transform.localScale = scale;
        go.GetComponent<Renderer>().sharedMaterial = mat;
        return go.transform;
    }
}
