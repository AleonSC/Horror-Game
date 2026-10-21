using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Old trails worn through the forest between the lamp posts: wide, faded strips of trodden dirt, wandering
/// rather than straight, stepping round the trunks, wider and narrower as they go, and every so often
/// petering out into the ordinary forest floor for a stretch before picking up again, so they hint at where
/// the lamps are without leading you neatly to each one. Every lamp is joined to the network (the shortest
/// web that links them all, plus a couple of extra loops). LampSpawner builds this once the lamps are placed.
/// </summary>
public static class LampTrails
{
    // The same worn-dirt material at a range of opacities, so a trail can fade smoothly in and out along
    // its length (the lit material can't fade along a mesh by itself): Levels steps, faint to full.
    const int Levels = 10;
    static Material[] materials;

    public static void Build(Transform parent, List<Lamp> lamps, DarkForestGenerator generator)
    {
        var old = parent.Find("Lamp Trails");
        if (old != null) Object.Destroy(old.gameObject);
        if (lamps.Count < 2) return;
        if (materials == null)
        {
            materials = new Material[Levels];
            for (int k = 0; k < Levels; k++)
                materials[k] = MaterialFactory.LitTransparent(ProceduralTextures.TrailDirt, new Color(1f, 1f, 1f, (k + 1) / (float)Levels), 0.05f);
        }

        var root = new GameObject("Lamp Trails").transform;
        root.SetParent(parent, false);

        var links = Network(lamps);
        foreach (var (a, b) in links)
            BuildTrail(root, lamps[a].transform.position, lamps[b].transform.position, generator);
        Debug.Log($"[Horror] Lamp trails: {links.Count} trails between {lamps.Count} lamps ({root.childCount} with worn stretches).");
    }

    /// <summary>Which lamps the trails join: a minimum spanning tree, plus two of the shortest other links as loops.</summary>
    static List<(int, int)> Network(List<Lamp> lamps)
    {
        int n = lamps.Count;
        float Dist(int i, int j)
        {
            Vector3 d = lamps[i].transform.position - lamps[j].transform.position;
            d.y = 0f;
            return d.magnitude;
        }
        var edges = new List<(int, int)>();
        var inTree = new bool[n];
        inTree[0] = true;
        for (int added = 1; added < n; added++)
        {
            int bi = -1, bj = -1;
            float best = float.MaxValue;
            for (int i = 0; i < n; i++)
                if (inTree[i])
                    for (int j = 0; j < n; j++)
                        if (!inTree[j] && Dist(i, j) < best) { best = Dist(i, j); bi = i; bj = j; }
            if (bj < 0) break;
            inTree[bj] = true;
            edges.Add((bi, bj));
        }
        var extras = new List<(float, int, int)>();
        for (int i = 0; i < n; i++)
            for (int j = i + 1; j < n; j++)
                if (!edges.Contains((i, j)) && !edges.Contains((j, i))) extras.Add((Dist(i, j), i, j));
        extras.Sort((x, y) => x.Item1.CompareTo(y.Item1));
        for (int k = 0; k < Mathf.Min(2, extras.Count); k++) edges.Add((extras[k].Item2, extras[k].Item3));
        return edges;
    }

    static void BuildTrail(Transform root, Vector3 from, Vector3 to, DarkForestGenerator generator)
    {
        Vector3 dir = to - from;
        dir.y = 0f;
        float length = dir.magnitude;
        if (length < 6f) return;
        dir /= length;
        Vector3 side = Vector3.Cross(Vector3.up, dir);
        Vector3 start = from + dir * 2.4f, end = to - dir * 2.4f; // from the rings worn round each post
        float span = length - 4.8f;

        // The line: a lazy bend and smaller wanders, then pushed round the trunks it would run into.
        float bend = Random.Range(-0.18f, 0.18f) * span;
        float phase = Random.Range(0f, 100f);
        int steps = Mathf.Max(2, Mathf.CeilToInt(span / 0.5f));
        var line = new List<Vector3>(steps + 1);
        for (int i = 0; i <= steps; i++)
        {
            float t = i / (float)steps;
            float wander = Mathf.Sin(Mathf.PI * t) * bend + (Mathf.PerlinNoise(phase, t * span * 0.06f) - 0.5f) * 6f * Mathf.Sin(Mathf.PI * t);
            line.Add(Vector3.Lerp(start, end, t) + side * wander);
        }
        if (generator != null)
        {
            var trees = generator.TreePositions;
            for (int pass = 0; pass < 2; pass++)
            {
                for (int i = 1; i < line.Count - 1; i++)
                {
                    var p = new Vector2(line[i].x, line[i].z);
                    foreach (var tree in trees)
                    {
                        Vector2 d = p - tree;
                        float dist = d.magnitude;
                        if (dist < 1.6f)
                            p = tree + (dist > 0.01f ? d / dist : new Vector2(side.x, side.z)) * 1.6f;
                    }
                    line[i] = new Vector3(p.x, 0f, p.y);
                }
                // Smoothed, so the steps round trunks are gentle swerves.
                for (int i = 1; i < line.Count - 1; i++) line[i] = (line[i - 1] + line[i] * 2f + line[i + 1]) * 0.25f;
            }
        }

        // Where it's worn and where it fades into the forest floor: stretches of trail, then gaps, each end
        // of a stretch fading away over several metres (the width stays; it's the wear that fades).
        var widths = new float[line.Count];
        var fades = new float[line.Count];
        float along = 0f, total = 0f;
        for (int i = 1; i < line.Count; i++) total += Vector3.Distance(line[i], line[i - 1]);
        bool worn = Random.value < 0.8f;
        float sectionEnd = Random.Range(8f, 25f);
        float sectionStart = 0f;
        for (int i = 0; i < line.Count; i++)
        {
            if (i > 0) along += Vector3.Distance(line[i], line[i - 1]);
            if (along > sectionEnd)
            {
                worn = !worn;
                sectionStart = sectionEnd;
                sectionEnd += worn ? Random.Range(12f, 30f) : Random.Range(5f, 14f);
            }
            widths[i] = Mathf.Lerp(1.5f, 2.4f, Mathf.PerlinNoise(phase + 7f, along * 0.07f));
            float fadeIn = Mathf.Clamp01((along - sectionStart) / 5f), fadeOut = Mathf.Clamp01((sectionEnd - along) / 5f);
            float ends = Mathf.Clamp01(along / 2.5f) * Mathf.Clamp01((total - along) / 2.5f); // melting into the lamp's ring
            float patchy = Mathf.Lerp(0.75f, 1f, Mathf.PerlinNoise(phase + 31f, along * 0.12f)); // worn more in places
            fades[i] = worn ? Mathf.SmoothStep(0f, 1f, Mathf.Min(fadeIn, fadeOut)) * ends * patchy : 0f;
        }

        // One mesh, one submesh per opacity step; each short segment drawn at its own fade.
        var verts = new List<Vector3>();
        var uvs = new List<Vector2>();
        var trisByLevel = new List<int>[Levels];
        for (int k = 0; k < Levels; k++) trisByLevel[k] = new List<int>();
        Vector3 pivot = (start + end) * 0.5f;
        pivot.y = generator != null ? generator.GetGroundHeight(pivot.x, pivot.z) : from.y;
        int runStart = -1;
        for (int i = 0; i <= line.Count; i++)
        {
            bool on = i < line.Count && fades[i] > 0.02f;
            if (on && runStart < 0) runStart = Mathf.Max(0, i - 1);
            if (!on && runStart >= 0)
            {
                AddStrip(line, widths, fades, runStart, Mathf.Min(i, line.Count - 1), pivot, generator, verts, uvs, trisByLevel);
                runStart = -1;
            }
        }
        bool any = false;
        foreach (var list in trisByLevel) if (list.Count > 0) any = true;
        if (!any) return;

        var mesh = new Mesh { name = "Lamp Trail" };
        if (verts.Count > 65000) mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        mesh.SetVertices(verts);
        mesh.SetUVs(0, uvs);
        mesh.subMeshCount = Levels;
        for (int k = 0; k < Levels; k++) mesh.SetTriangles(trisByLevel[k], k);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        var go = new GameObject("Trail");
        go.transform.SetParent(root, false);
        go.transform.position = pivot;
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        var renderer = go.AddComponent<MeshRenderer>();
        renderer.sharedMaterials = materials;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        WorldWrap.RegisterStatic(go.transform);
    }

    /// <summary>
    /// A strip along part of the line, five vertices across (so it hugs the ground's bumps), just above the
    /// ground, relative to the pivot. u runs across, v along (one texture length per 2.5m). Each segment goes
    /// in the submesh for its opacity (the average fade of its two ends).
    /// </summary>
    static void AddStrip(List<Vector3> line, float[] widths, float[] fades, int from, int to, Vector3 pivot, DarkForestGenerator generator,
        List<Vector3> verts, List<Vector2> uvs, List<int>[] trisByLevel)
    {
        if (to - from < 1) return;
        const int across = 5;
        int first = verts.Count;
        float v = 0f;
        for (int i = from; i <= to; i++)
        {
            Vector3 tangent = line[Mathf.Min(i + 1, line.Count - 1)] - line[Mathf.Max(i - 1, 0)];
            tangent.y = 0f;
            Vector3 side = Vector3.Cross(Vector3.up, tangent.normalized);
            if (i > from) v += Vector3.Distance(line[i], line[i - 1]) / 2.5f;
            for (int k = 0; k < across; k++)
            {
                float u = k / (float)(across - 1);
                Vector3 p = line[i] + side * (u - 0.5f) * widths[i];
                p.y = (generator != null ? generator.GetGroundHeight(p.x, p.z) : pivot.y) + 0.035f;
                verts.Add(p - pivot);
                uvs.Add(new Vector2(u, v));
            }
        }
        for (int i = 0; i < to - from; i++)
        {
            float fade = (fades[from + i] + fades[from + i + 1]) * 0.5f;
            int level = Mathf.RoundToInt(fade * Levels) - 1;
            if (level < 0) continue; // too faint to draw
            var tris = trisByLevel[Mathf.Min(level, Levels - 1)];
            for (int k = 0; k < across - 1; k++)
            {
                int a = first + i * across + k, b = a + across, c = a + 1, d = b + 1;
                tris.Add(a); tris.Add(b); tris.Add(c);
                tris.Add(c); tris.Add(b); tris.Add(d);
            }
        }
    }
}
