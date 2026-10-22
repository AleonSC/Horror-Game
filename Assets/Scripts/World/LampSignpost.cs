using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// An old carved wooden signpost beside a lamp, its arrow boards pointing the way to the nearest other lamps
/// (the nearest highest): a squared post with a pointed cap and a collar under it, set in a little cairn of
/// stones, each board cut to an arrow, framed with a carved groove, nailed on, with a row of notches cut
/// into it, one for every thirty metres or so to that lamp. LampSpawner puts one by every lamp.
/// </summary>
public static class LampSignpost
{
    static Material post, board, groove, metal, stone;
    /// <summary>Clears this class's static state (StaticReset calls it when Play starts).</summary>
    internal static void ResetStatics() => post = board = groove = metal = stone = null; // (forgotten: the scene's objects own them)

    public static void BuildAll(List<Lamp> lamps, DarkForestGenerator generator)
    {
        if (lamps.Count < 2) return;
        foreach (var lamp in lamps)
        {
            // The nearest two others (the map loops: whichever way round is shorter).
            var others = new List<(float dist, Vector3 dir)>();
            foreach (var other in lamps)
            {
                if (other == lamp) continue;
                Vector3 d = WorldWrap.NearestImage(other.transform.position, lamp.transform.position) - lamp.transform.position;
                d.y = 0f;
                others.Add((d.magnitude, d.normalized));
            }
            others.Sort((a, b) => a.dist.CompareTo(b.dist));
            Build(lamp, others.GetRange(0, Mathf.Min(2, others.Count)), generator);
        }
    }

    static void Build(Lamp lamp, List<(float dist, Vector3 dir)> targets, DarkForestGenerator generator)
    {
        EnsureMaterials();
        // Beside the lamp, off to the side of the way the first board points (so the board reaches past it).
        Vector3 first = targets[0].dir;
        Vector3 side = Vector3.Cross(Vector3.up, first).normalized;
        Vector3 at = lamp.transform.position + side * 1.3f - first * 0.3f;
        if (generator != null) at.y = generator.GetGroundHeight(at.x, at.z);

        var root = new GameObject("Signpost").transform;
        root.SetParent(lamp.transform, true);
        root.position = at;
        root.rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
        root.localScale = Vector3.one;

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

        // The boards: one per lamp, the nearest highest, each turned to point at it.
        for (int i = 0; i < targets.Count; i++)
        {
            var (dist, dir) = targets[i];
            float height = 1.62f - i * 0.27f;
            var arm = new GameObject("Board").transform;
            arm.SetParent(root, false);
            arm.localPosition = new Vector3(0f, height, 0f);
            arm.rotation = Quaternion.LookRotation(Vector3.Cross(dir, Vector3.up), Vector3.up) * Quaternion.Euler(0f, 0f, Random.Range(-3f, 3f)); // +x along the way it points
            const float length = 0.7f, width = 0.16f, thick = 0.03f;
            float reachOut = length * 0.5f - 0.04f; // most of it out past the post
            var plank = new GameObject("Arrow");
            plank.transform.SetParent(arm, false);
            plank.transform.localPosition = new Vector3(reachOut, 0f, i % 2 == 0 ? 0.075f : -0.075f); // either face of the post
            plank.AddComponent<MeshFilter>().sharedMesh = ArrowMesh(length, width, thick);
            var r = plank.AddComponent<MeshRenderer>();
            r.sharedMaterial = board;
            // A carved groove framing it, two nails at the post end, and the notches counting the distance.
            Vector3 face = new Vector3(0f, 0f, (i % 2 == 0 ? 1f : -1f) * (thick * 0.5f + 0.001f));
            foreach (float y in new[] { -width * 0.33f, width * 0.33f })
                Part(PrimitiveType.Cube, groove, plank.transform, face + new Vector3(-0.05f, y, 0f), new Vector3(length * 0.72f, 0.006f, 0.004f));
            foreach (float y in new[] { -0.035f, 0.035f })
                Part(PrimitiveType.Sphere, metal, plank.transform, face + new Vector3(-length * 0.5f + 0.06f, y, 0f), new Vector3(0.012f, 0.012f, 0.006f));
            int notches = Mathf.Clamp(Mathf.RoundToInt(dist / 30f), 1, 6);
            for (int n = 0; n < notches; n++)
                Part(PrimitiveType.Cube, groove, plank.transform, face + new Vector3(-0.12f + n * 0.035f, 0f, 0f), new Vector3(0.008f, width * 0.36f, 0.004f));
        }
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
