using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Faded paths worn into the dirt around a lamp post: a trodden ring circling the pole (as if something
/// has paced round and round the light) and a few trails wandering off into the trees, thinning out to
/// nothing. A see-through strip laid over the ground. LampSpawner adds one to each lamp.
/// </summary>
public static class LampPaths
{
    static Material material;
    /// <summary>Clears this class's static state (StaticReset calls it when Play starts).</summary>
    internal static void ResetStatics() => StaticReset.Drop(ref material);

    /// <summary>Lays paths around a lamp that's already in place. They're parented to it.</summary>
    public static void Build(Transform lamp, DarkForestGenerator generator)
    {
        if (material == null)
            material = MaterialFactory.LitTransparent(ProceduralTextures.WornPath, Color.white, 0.02f);

        var vertices = new List<Vector3>();
        var uvs = new List<Vector2>();
        var triangles = new List<int>();
        Vector3 center = lamp.position;

        // The ring round the pole, a bit wobbly.
        float ringRadius = Random.Range(1.8f, 2.6f);
        float ringPhase = Random.Range(0f, 10f);
        const int ringSteps = 28;
        var ring = new List<Vector3>();
        var ringWidths = new List<float>();
        for (int i = 0; i <= ringSteps; i++)
        {
            float a = i / (float)ringSteps * Mathf.PI * 2f;
            float r = ringRadius * (1f + 0.08f * Mathf.Sin(a * 3f + ringPhase));
            ring.Add(center + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * r);
            ringWidths.Add(0.75f);
        }
        AddStrip(ring, ringWidths, generator, lamp, vertices, uvs, triangles);

        // Trails leading away, meandering and fading out.
        int trails = Random.Range(2, 4);
        float start = Random.Range(0f, 360f);
        for (int t = 0; t < trails; t++)
        {
            float heading = start + t * 360f / trails + Random.Range(-35f, 35f);
            Vector3 dir = Quaternion.Euler(0f, heading, 0f) * Vector3.forward;
            float length = Random.Range(9f, 18f);
            float turn = Random.Range(-1f, 1f);

            var points = new List<Vector3>();
            var widths = new List<float>();
            Vector3 p = center + dir * ringRadius;
            const float step = 0.6f;
            int steps = Mathf.CeilToInt(length / step);
            for (int i = 0; i <= steps; i++)
            {
                float f = i / (float)steps;
                points.Add(p);
                widths.Add(Mathf.Lerp(0.65f, 0.05f, f * f)); // thins out toward the end
                turn = Mathf.Clamp(turn + Random.Range(-0.5f, 0.5f), -1.5f, 1.5f);
                dir = Quaternion.Euler(0f, turn * 6f, 0f) * dir;
                p += dir * step;
            }
            AddStrip(points, widths, generator, lamp, vertices, uvs, triangles);
        }

        var mesh = new Mesh { name = "Lamp Paths" };
        mesh.SetVertices(vertices);
        mesh.SetUVs(0, uvs);
        mesh.SetTriangles(triangles, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();

        var go = new GameObject("Worn Paths");
        go.transform.SetParent(lamp, false);
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        var renderer = go.AddComponent<MeshRenderer>();
        renderer.sharedMaterial = material;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
    }

    /// <summary>
    /// A strip along a centreline, three vertices across (so it hugs bumps in the ground), sitting just above
    /// the ground, in the lamp's local space. u runs across, v along (one texture length per 2.5m).
    /// </summary>
    static void AddStrip(List<Vector3> line, List<float> widths, DarkForestGenerator generator, Transform lamp,
        List<Vector3> vertices, List<Vector2> uvs, List<int> triangles)
    {
        int first = vertices.Count;
        float v = 0f;
        for (int i = 0; i < line.Count; i++)
        {
            Vector3 tangent = line[Mathf.Min(i + 1, line.Count - 1)] - line[Mathf.Max(i - 1, 0)];
            tangent.y = 0f;
            Vector3 side = Vector3.Cross(Vector3.up, tangent.normalized);
            if (i > 0) v += Vector3.Distance(line[i], line[i - 1]) / 2.5f;

            for (int k = 0; k < 3; k++)
            {
                float across = k * 0.5f;
                Vector3 p = line[i] + side * (across - 0.5f) * widths[i];
                p.y = (generator != null ? generator.GetGroundHeight(p.x, p.z) : lamp.position.y) + 0.04f;
                vertices.Add(lamp.InverseTransformPoint(p));
                uvs.Add(new Vector2(across, v));
            }
        }
        for (int i = 0; i < line.Count - 1; i++)
        {
            for (int k = 0; k < 2; k++)
            {
                int a = first + i * 3 + k, b = a + 3, c = a + 1, d = b + 1;
                triangles.Add(a); triangles.Add(b); triangles.Add(c);
                triangles.Add(c); triangles.Add(b); triangles.Add(d);
            }
        }
    }
}
