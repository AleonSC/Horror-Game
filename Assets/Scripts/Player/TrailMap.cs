using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// The map from the top of the watchtower, held up in front of you: a sheet of old, stained paper with a
/// pencil sketch of the trails worn between the lamp posts (LampTrails records them as it lays them), each
/// lamp marked (inked in once it's lit), and the watchtower. No "you are here": you work that out yourself.
/// Hotbar draws it while the map is in hand.
/// </summary>
public static class TrailMap
{
    /// <summary>The trails, as lines of points (world space, as laid).</summary>
    public static readonly List<Vector3[]> Paths = new List<Vector3[]>();
    static Texture2D paper;
    static GUIStyle titleStyle, noteStyle;
    static DarkForestGenerator generator;

    internal static void ResetStatics()
    {
        Paths.Clear();
        StaticReset.Drop(ref paper);
        titleStyle = noteStyle = null;
        generator = null;
    }

    /// <summary>Records a trail (every few points is plenty for a sketch).</summary>
    public static void AddPath(List<Vector3> line)
    {
        var kept = new List<Vector3>();
        for (int i = 0; i < line.Count; i += 4) kept.Add(line[i]);
        if (line.Count > 0 && (line.Count - 1) % 4 != 0) kept.Add(line[line.Count - 1]);
        Paths.Add(kept.ToArray());
    }

    static readonly Color Pencil = new Color(0.22f, 0.15f, 0.09f, 0.8f);
    static readonly Color Ink = new Color(0.16f, 0.1f, 0.06f, 0.95f);
    static readonly Color LitInk = new Color(0.75f, 0.35f, 0.08f, 0.95f);

    /// <summary>Draws the map up in front of you; appear (0-1) brings it up from below.</summary>
    public static void Draw(float appear)
    {
        if (generator == null) generator = Object.FindAnyObjectByType<DarkForestGenerator>();
        float size = generator != null ? generator.areaSize : 320f, half = size * 0.5f;
        float sw = Screen.width, sh = Screen.height;
        float unit = Mathf.Max(1f, sh / 1080f);
        float side = Mathf.Min(sw, sh) * 0.8f;
        float ease = Mathf.SmoothStep(0f, 1f, appear);
        var sheet = new Rect((sw - side) * 0.5f, (sh - side) * 0.5f + (1f - ease) * sh * 0.5f, side, side);
        EnsureStyles(unit);

        Matrix4x4 old = GUI.matrix;
        GUIUtility.RotateAroundPivot(-1.5f, sheet.center); // held a touch crooked

        // The paper, and a shadow under it.
        Fill(new Rect(sheet.x + 8f * unit, sheet.y + 10f * unit, sheet.width, sheet.height), new Color(0f, 0f, 0f, 0.45f));
        GUI.color = Color.white;
        GUI.DrawTexture(sheet, Paper());
        // The frame ruled round it, and the title.
        var inner = new Rect(sheet.x + side * 0.06f, sheet.y + side * 0.11f, side * 0.88f, side * 0.83f);
        Box(inner, 1.4f * unit, new Color(Ink.r, Ink.g, Ink.b, 0.6f));
        GUI.color = Ink;
        GUI.Label(new Rect(sheet.x, sheet.y + side * 0.025f, side, side * 0.08f), "T H E   T R A I L S", titleStyle);

        Vector2 ToMap(Vector3 world)
        {
            float u = Mathf.Repeat(world.x + half, size) / size, v = Mathf.Repeat(world.z + half, size) / size;
            return new Vector2(inner.x + u * inner.width, inner.yMax - v * inner.height);
        }

        // The trails, in pencil, a little shaky, broken where they cross the edge of the map.
        float jitter = 0.8f * unit;
        foreach (var path in Paths)
        {
            for (int i = 1; i < path.Length; i++)
            {
                Vector2 a = ToMap(path[i - 1]), b = ToMap(path[i]);
                if ((a - b).magnitude > inner.width * 0.5f) continue; // wrapped round the world
                float wobble = Mathf.Sin(i * 1.7f) * jitter;
                Line(a + Vector2.up * wobble, b - Vector2.up * wobble, 2f * unit, Pencil);
            }
        }

        // The lamps: a ringed post, inked in once it's lit.
        foreach (var lamp in Lamp.All)
        {
            if (lamp == null) continue;
            Vector2 p = ToMap(lamp.transform.position);
            float r = 7f * unit;
            Circle(p, r, 1.8f * unit, Ink);
            if (lamp.IsLit) Disc(p, r * 0.7f, LitInk);
            Line(p + new Vector2(0f, r), p + new Vector2(0f, r + 7f * unit), 1.6f * unit, Ink);
        }

        // The watchtower: a little lattice, and its name.
        foreach (var tower in Watchtower.All)
        {
            if (tower == null) continue;
            Vector2 p = ToMap(tower.transform.position);
            float h = 22f * unit, w = 9f * unit;
            Line(p + new Vector2(-w, h * 0.5f), p + new Vector2(-w * 0.5f, -h * 0.5f), 1.8f * unit, Ink);
            Line(p + new Vector2(w, h * 0.5f), p + new Vector2(w * 0.5f, -h * 0.5f), 1.8f * unit, Ink);
            Line(p + new Vector2(-w * 0.75f, 0f), p + new Vector2(w * 0.75f, 0f), 1.4f * unit, Ink);
            Line(p + new Vector2(-w * 0.85f, h * 0.25f), p + new Vector2(w * 0.5f, -h * 0.5f), 1f * unit, Ink);
            Line(p + new Vector2(-w * 0.8f, -h * 0.5f), p + new Vector2(w * 0.8f, -h * 0.5f), 2.2f * unit, Ink);
            GUI.color = Ink;
            GUI.Label(new Rect(p.x + 12f * unit, p.y - 12f * unit, 140f * unit, 24f * unit), "tower", noteStyle);
        }

        // A compass mark, and a note in the corner.
        Vector2 n = new Vector2(inner.xMax - 30f * unit, inner.y + 38f * unit);
        Line(n + new Vector2(0f, 14f * unit), n + new Vector2(0f, -14f * unit), 1.6f * unit, Ink);
        Line(n + new Vector2(0f, -14f * unit), n + new Vector2(-5f * unit, -6f * unit), 1.6f * unit, Ink);
        Line(n + new Vector2(0f, -14f * unit), n + new Vector2(5f * unit, -6f * unit), 1.6f * unit, Ink);
        GUI.color = Ink;
        GUI.Label(new Rect(n.x - 10f * unit, n.y - 40f * unit, 20f * unit, 20f * unit), "N", noteStyle);
        GUI.Label(new Rect(inner.x, inner.yMax + side * 0.005f, inner.width, side * 0.05f), "stay on the trails. keep to the light.", noteStyle);

        GUI.color = Color.white;
        GUI.matrix = old;
    }

    // ---------------------------------------------------------------- Pencil

    static void Fill(Rect r, Color c)
    {
        GUI.color = c;
        GUI.DrawTexture(r, Texture2D.whiteTexture);
    }

    static void Line(Vector2 a, Vector2 b, float width, Color c)
    {
        Vector2 d = b - a;
        float length = d.magnitude;
        if (length < 0.5f) return;
        Matrix4x4 keep = GUI.matrix;
        GUIUtility.RotateAroundPivot(Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg, a);
        Fill(new Rect(a.x, a.y - width * 0.5f, length, width), c);
        GUI.matrix = keep;
    }

    static void Circle(Vector2 c, float r, float width, Color color)
    {
        const int steps = 14;
        for (int i = 0; i < steps; i++)
        {
            float a0 = i / (float)steps * Mathf.PI * 2f, a1 = (i + 1) / (float)steps * Mathf.PI * 2f;
            Line(c + new Vector2(Mathf.Cos(a0), Mathf.Sin(a0)) * r, c + new Vector2(Mathf.Cos(a1), Mathf.Sin(a1)) * r, width, color);
        }
    }

    static void Disc(Vector2 c, float r, Color color)
    {
        for (float y = -r; y <= r; y += 1f)
        {
            float w = Mathf.Sqrt(Mathf.Max(0f, r * r - y * y));
            Fill(new Rect(c.x - w, c.y + y, w * 2f, 1.2f), color);
        }
    }

    static void Box(Rect r, float width, Color c)
    {
        Fill(new Rect(r.x, r.y, r.width, width), c);
        Fill(new Rect(r.x, r.yMax - width, r.width, width), c);
        Fill(new Rect(r.x, r.y, width, r.height), c);
        Fill(new Rect(r.xMax - width, r.y, width, r.height), c);
    }

    static void EnsureStyles(float unit)
    {
        if (titleStyle == null)
        {
            titleStyle = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };
            noteStyle = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleLeft, fontStyle = FontStyle.Italic };
        }
        titleStyle.fontSize = Mathf.RoundToInt(30f * unit);
        noteStyle.fontSize = Mathf.RoundToInt(17f * unit);
        titleStyle.normal.textColor = noteStyle.normal.textColor = Color.white; // (tinted by GUI.color)
    }

    /// <summary>Old paper: yellowed, blotched with damp stains, darker and grubbier toward the edges.</summary>
    static Texture2D Paper()
    {
        if (paper != null) return paper;
        const int n = 256;
        var px = new Color32[n * n];
        float seed = Random.Range(0f, 100f);
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float u = x / (float)n, v = y / (float)n;
                float stain = Mathf.PerlinNoise(u * 4f + seed, v * 4f) * 0.6f + Mathf.PerlinNoise(u * 13f + seed, v * 13f) * 0.4f;
                float grain = Mathf.PerlinNoise(u * 90f, v * 90f + seed);
                float edge = Mathf.Min(Mathf.Min(u, 1f - u), Mathf.Min(v, 1f - v));
                float dark = 1f - 0.35f * Mathf.Clamp01(1f - edge / 0.08f) - 0.18f * Mathf.Clamp01((stain - 0.55f) * 4f);
                Color c = new Color(0.8f, 0.71f, 0.53f) * dark * (0.94f + 0.08f * grain);
                c.a = 1f;
                px[y * n + x] = c;
            }
        paper = new Texture2D(n, n, TextureFormat.RGBA32, false) { name = "Map Paper", wrapMode = TextureWrapMode.Clamp };
        paper.SetPixels32(px);
        paper.Apply();
        return paper;
    }
}
