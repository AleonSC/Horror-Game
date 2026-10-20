using UnityEngine;

/// <summary>
/// Generates textures in code (no image files needed), each made once on first use and shared:
/// - the forest floor: bare brown soil scattered with pebbles and clods, plus a matching normal map so
///   the flashlight picks out every bump (tiles seamlessly);
/// - bark (with a normal map) for tree trunks and branches, and pine needles for foliage;
/// - the eyeball of the giant eye in the sky.
/// </summary>
public static class ProceduralTextures
{
    const int Size = 512;

    static Texture2D dirt, dirtNormal, skyEye, bark, barkNormal, needles, wornPath, rock, rockNormal, waterNormal, endGrain;

    /// <summary>Tiling forest-floor dirt.</summary>
    public static Texture2D Dirt { get { if (dirt == null) MakeDirt(); return dirt; } }
    /// <summary>Normal map matching Dirt.</summary>
    public static Texture2D DirtNormal { get { if (dirtNormal == null) MakeDirt(); return dirtNormal; } }
    /// <summary>Tiling bark: furrowed plates running lengthwise, with faint lichen. Tall (v runs up the trunk).</summary>
    public static Texture2D Bark { get { if (bark == null) MakeBark(); return bark; } }
    /// <summary>Normal map matching Bark.</summary>
    public static Texture2D BarkNormal { get { if (barkNormal == null) MakeBark(); return barkNormal; } }
    /// <summary>Tiling pine needles: dark green streaks running down from the tips.</summary>
    public static Texture2D Needles => needles != null ? needles : (needles = MakeNeedles());
    /// <summary>
    /// A faded strip of trodden dirt: u runs across the path (fading out at both edges), v along it (tiles).
    /// Mostly see-through, so the ground shows through it.
    /// </summary>
    public static Texture2D WornPath => wornPath != null ? wornPath : (wornPath = MakeWornPath());
    /// <summary>Tiling weathered granite: grey speckled stone, dark cracks, pale lichen and dark moss.</summary>
    public static Texture2D Rock { get { if (rock == null) MakeRock(); return rock; } }
    /// <summary>Normal map matching Rock.</summary>
    public static Texture2D RockNormal { get { if (rockNormal == null) MakeRock(); return rockNormal; } }
    /// <summary>Gentle overlapping ripples, as a tiling normal map, for still water.</summary>
    public static Texture2D WaterNormal => waterNormal != null ? waterNormal : (waterNormal = MakeWaterNormal());
    /// <summary>The sawn end of a log: growth rings, darker heartwood, a few radial cracks, a rim of bark.</summary>
    public static Texture2D EndGrain => endGrain != null ? endGrain : (endGrain = MakeEndGrain());
    /// <summary>A bloodshot red eyeball with a fibrous, glowing iris (no pupil; the shader draws that).</summary>
    public static Texture2D SkyEye => skyEye != null ? skyEye : (skyEye = MakeSkyEye());

    // ---------------------------------------------------------------- Tiling noise

    // Value noise on a lattice that wraps every `period` cells, so the texture tiles seamlessly.
    static float Hash(int x, int y, int seed)
    {
        unchecked
        {
            int h = x * 374761393 + y * 668265263 + seed * 144665;
            h = (h ^ (h >> 13)) * 1274126177;
            return ((h ^ (h >> 16)) & 0xffff) / 65535f;
        }
    }

    static float TileNoise(float x, float y, int period, int seed)
    {
        int x0 = Mathf.FloorToInt(x), y0 = Mathf.FloorToInt(y);
        float fx = x - x0, fy = y - y0;
        fx = fx * fx * (3f - 2f * fx);
        fy = fy * fy * (3f - 2f * fy);
        int ax = Mod(x0, period), bx = Mod(x0 + 1, period);
        int ay = Mod(y0, period), by = Mod(y0 + 1, period);
        float a = Mathf.Lerp(Hash(ax, ay, seed), Hash(bx, ay, seed), fx);
        float b = Mathf.Lerp(Hash(ax, by, seed), Hash(bx, by, seed), fx);
        return Mathf.Lerp(a, b, fy);
    }

    /// <summary>Like TileNoise, but wrapping every px cells across and py cells down (for stretched patterns).</summary>
    static float TileNoise2(float x, float y, int px, int py, int seed)
    {
        int x0 = Mathf.FloorToInt(x), y0 = Mathf.FloorToInt(y);
        float fx = x - x0, fy = y - y0;
        fx = fx * fx * (3f - 2f * fx);
        fy = fy * fy * (3f - 2f * fy);
        int ax = Mod(x0, px), bx = Mod(x0 + 1, px);
        int ay = Mod(y0, py), by = Mod(y0 + 1, py);
        float a = Mathf.Lerp(Hash(ax, ay, seed), Hash(bx, ay, seed), fx);
        float b = Mathf.Lerp(Hash(ax, by, seed), Hash(bx, by, seed), fx);
        return Mathf.Lerp(a, b, fy);
    }

    /// <summary>Fractal noise over a unit square (u, v in 0..1) that tiles. 0..1.</summary>
    static float Fbm(float u, float v, int basePeriod, int octaves, int seed)
    {
        float sum = 0f, amp = 0.5f, norm = 0f;
        int period = basePeriod;
        for (int o = 0; o < octaves; o++)
        {
            sum += TileNoise(u * period, v * period, period, seed + o * 31) * amp;
            norm += amp;
            amp *= 0.5f;
            period *= 2;
        }
        return sum / norm;
    }

    static int Mod(int a, int m) => ((a % m) + m) % m;

    // ---------------------------------------------------------------- Bark & needles

    static void MakeBark()
    {
        const int w = 256, h = 512;
        var height = new float[w * h];
        var albedo = new Color32[w * h];

        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                float u = x / (float)w, v = y / (float)h;

                // Long plates of bark, split by deep vertical cracks, with fine grain.
                float plates = 0.6f * TileNoise2(u * 9f, v * 3f, 9, 3, 21) + 0.4f * TileNoise2(u * 18f, v * 6f, 18, 6, 22);
                float crackNoise = TileNoise2(u * 14f, v * 2f, 14, 2, 23) * 0.7f + TileNoise2(u * 28f, v * 5f, 28, 5, 24) * 0.3f;
                float crack = Mathf.Pow(1f - Mathf.Abs(crackNoise * 2f - 1f), 7f);          // 1 along thin crack lines
                float grain = TileNoise2(u * 64f, v * 32f, 64, 32, 25);
                float hgt = plates * (1f - 0.85f * crack) + grain * 0.1f;
                height[y * w + x] = hgt;

                Color c = Color.Lerp(new Color(0.05f, 0.04f, 0.032f), new Color(0.16f, 0.13f, 0.1f), Mathf.Clamp01(hgt * 1.2f));
                // Patches of pale lichen on the raised plates.
                float lichen = Ramp(0.68f, 0.78f, TileNoise2(u * 6f, v * 10f, 6, 10, 26)) * Mathf.Clamp01(hgt * 1.5f - 0.3f);
                c = Color.Lerp(c, new Color(0.17f, 0.19f, 0.15f), lichen * 0.55f);
                c.a = 1f;
                albedo[y * w + x] = c;
            }
        }

        bark = new Texture2D(w, h, TextureFormat.RGBA32, true, false)
        {
            name = "Bark", wrapMode = TextureWrapMode.Repeat, anisoLevel = 8, filterMode = FilterMode.Trilinear,
        };
        bark.SetPixels32(albedo);
        bark.Apply(true);

        var normals = new Color32[w * h];
        const float strength = 5f;
        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                float dx = height[y * w + Mod(x + 1, w)] - height[y * w + Mod(x - 1, w)];
                float dy = height[Mod(y + 1, h) * w + x] - height[Mod(y - 1, h) * w + x];
                var n = new Vector3(-dx * strength, -dy * strength, 1f).normalized;
                normals[y * w + x] = new Color(n.x * 0.5f + 0.5f, n.y * 0.5f + 0.5f, n.z * 0.5f + 0.5f, 1f);
            }
        }
        barkNormal = new Texture2D(w, h, TextureFormat.RGBA32, true, true)
        {
            name = "Bark Normal", wrapMode = TextureWrapMode.Repeat, anisoLevel = 8, filterMode = FilterMode.Trilinear,
        };
        barkNormal.SetPixels32(normals);
        barkNormal.Apply(true);
    }

    static Texture2D MakeNeedles()
    {
        const int n = 256;
        var px = new Color32[n * n];
        for (int y = 0; y < n; y++)
        {
            for (int x = 0; x < n; x++)
            {
                float u = x / (float)n, v = y / (float)n;
                // Fine streaks running down the branches, in clumps of lighter and darker green.
                float streaks = TileNoise2(u * 64f, v * 6f, 64, 6, 41) * 0.6f + TileNoise2(u * 128f, v * 12f, 128, 12, 42) * 0.4f;
                float clumps = TileNoise2(u * 8f, v * 4f, 8, 4, 43);
                Color c = Color.Lerp(new Color(0.02f, 0.05f, 0.03f), new Color(0.07f, 0.13f, 0.07f), streaks * (0.5f + 0.7f * clumps));
                c = Color.Lerp(c, new Color(0.06f, 0.08f, 0.04f), Ramp(0.7f, 0.9f, clumps) * 0.4f); // older, browner patches
                c.a = 1f;
                px[y * n + x] = c;
            }
        }
        var tex = new Texture2D(n, n, TextureFormat.RGBA32, true, false)
        {
            name = "Needles", wrapMode = TextureWrapMode.Repeat, anisoLevel = 4, filterMode = FilterMode.Trilinear,
        };
        tex.SetPixels32(px);
        tex.Apply(true);
        return tex;
    }

    // ---------------------------------------------------------------- End grain

    static Texture2D MakeEndGrain()
    {
        const int n = 256;
        var px = new Color32[n * n];
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float dx = (x + 0.5f) / n * 2f - 1f, dy = (y + 0.5f) / n * 2f - 1f;
                float r = Mathf.Sqrt(dx * dx + dy * dy);
                float a = Mathf.Atan2(dy, dx);
                // Rings, a little wobbly.
                float wobble = 0.02f * Mathf.Sin(a * 5f) + 0.015f * Mathf.Sin(a * 11f + 2f);
                float rings = 0.5f + 0.5f * Mathf.Sin((r + wobble) * 70f);
                Color c = Color.Lerp(new Color(0.32f, 0.24f, 0.16f), new Color(0.42f, 0.33f, 0.23f), rings * 0.6f + Hash(x, y, 91) * 0.15f);
                c = Color.Lerp(new Color(0.22f, 0.15f, 0.1f), c, Mathf.Clamp01(r * 3f));          // darker heartwood
                float crack = Mathf.Pow(Mathf.Abs(Mathf.Sin(a * 3.5f + 0.7f)), 60f) * Mathf.Clamp01(r * 1.5f - 0.1f);
                c = Color.Lerp(c, new Color(0.08f, 0.06f, 0.04f), crack);                            // radial checks
                if (r > 0.88f) c = new Color(0.1f, 0.08f, 0.06f);                                   // the bark rim
                c.a = 1f;
                px[y * n + x] = c;
            }
        var tex = new Texture2D(n, n, TextureFormat.RGBA32, true, false)
        {
            name = "End Grain", wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Trilinear,
        };
        tex.SetPixels32(px);
        tex.Apply(true);
        return tex;
    }

    // ---------------------------------------------------------------- Water

    static Texture2D MakeWaterNormal()
    {
        const int n = 256;
        var height = new float[n * n];
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float u = x / (float)n, v = y / (float)n;
                height[y * n + x] = TileNoise(u * 6f, v * 6f, 6, 81) * 0.5f + TileNoise(u * 14f, v * 14f, 14, 82) * 0.3f + TileNoise(u * 32f, v * 32f, 32, 83) * 0.2f;
            }
        var px = new Color32[n * n];
        const float strength = 2.5f;
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float dx = height[y * n + Mod(x + 1, n)] - height[y * n + Mod(x - 1, n)];
                float dy = height[Mod(y + 1, n) * n + x] - height[Mod(y - 1, n) * n + x];
                var nv = new Vector3(-dx * strength, -dy * strength, 1f).normalized;
                px[y * n + x] = new Color(nv.x * 0.5f + 0.5f, nv.y * 0.5f + 0.5f, nv.z * 0.5f + 0.5f, 1f);
            }
        var tex = new Texture2D(n, n, TextureFormat.RGBA32, true, true)
        {
            name = "Water Normal", wrapMode = TextureWrapMode.Repeat, anisoLevel = 4, filterMode = FilterMode.Trilinear,
        };
        tex.SetPixels32(px);
        tex.Apply(true);
        return tex;
    }

    // ---------------------------------------------------------------- Rock

    static void MakeRock()
    {
        const int n = 256;
        var height = new float[n * n];
        var albedo = new Color32[n * n];
        for (int y = 0; y < n; y++)
        {
            for (int x = 0; x < n; x++)
            {
                float u = x / (float)n, v = y / (float)n;
                float body = TileNoise2(u * 4f, v * 4f, 4, 4, 61) * 0.5f + TileNoise2(u * 9f, v * 9f, 9, 9, 62) * 0.3f + TileNoise2(u * 24f, v * 24f, 24, 24, 63) * 0.2f;
                // Cracks: thin ridges where the noise crosses its middle.
                float crackNoise = TileNoise2(u * 6f, v * 6f, 6, 6, 64) * 0.7f + TileNoise2(u * 15f, v * 15f, 15, 15, 65) * 0.3f;
                float crack = Mathf.Pow(1f - Mathf.Abs(crackNoise * 2f - 1f), 14f);
                float grain = TileNoise2(u * 96f, v * 96f, 96, 96, 66);
                float hgt = body * (1f - 0.7f * crack) + grain * 0.08f;
                height[y * n + x] = hgt;

                Color c = Color.Lerp(new Color(0.16f, 0.16f, 0.16f), new Color(0.36f, 0.35f, 0.34f), Mathf.Clamp01(body * 1.3f - 0.1f));
                // Feldspar and mica speckles.
                if (grain > 0.8f) c = Color.Lerp(c, new Color(0.48f, 0.46f, 0.44f), 0.5f);
                else if (grain < 0.18f) c *= 0.7f;
                c = Color.Lerp(c, new Color(0.06f, 0.06f, 0.06f), crack * 0.8f);
                // Crusty pale lichen and dark moss.
                float lichen = Ramp(0.66f, 0.74f, TileNoise2(u * 7f, v * 7f, 7, 7, 67));
                c = Color.Lerp(c, new Color(0.38f, 0.4f, 0.32f), lichen * 0.6f);
                float moss = Ramp(0.6f, 0.75f, TileNoise2(u * 3f, v * 3f, 3, 3, 68)) * Mathf.Clamp01(1.2f - body);
                c = Color.Lerp(c, new Color(0.06f, 0.1f, 0.05f), moss * 0.7f);
                c.a = 1f;
                albedo[y * n + x] = c;
            }
        }

        rock = new Texture2D(n, n, TextureFormat.RGBA32, true, false)
        {
            name = "Rock", wrapMode = TextureWrapMode.Repeat, anisoLevel = 8, filterMode = FilterMode.Trilinear,
        };
        rock.SetPixels32(albedo);
        rock.Apply(true);

        var normals = new Color32[n * n];
        const float strength = 4f;
        for (int y = 0; y < n; y++)
        {
            for (int x = 0; x < n; x++)
            {
                float dx = height[y * n + Mod(x + 1, n)] - height[y * n + Mod(x - 1, n)];
                float dy = height[Mod(y + 1, n) * n + x] - height[Mod(y - 1, n) * n + x];
                var nv = new Vector3(-dx * strength, -dy * strength, 1f).normalized;
                normals[y * n + x] = new Color(nv.x * 0.5f + 0.5f, nv.y * 0.5f + 0.5f, nv.z * 0.5f + 0.5f, 1f);
            }
        }
        rockNormal = new Texture2D(n, n, TextureFormat.RGBA32, true, true)
        {
            name = "Rock Normal", wrapMode = TextureWrapMode.Repeat, anisoLevel = 8, filterMode = FilterMode.Trilinear,
        };
        rockNormal.SetPixels32(normals);
        rockNormal.Apply(true);
    }

    // ---------------------------------------------------------------- Worn path

    static Texture2D MakeWornPath()
    {
        const int w = 128, h = 256;
        var px = new Color32[w * h];
        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                float u = x / (float)(w - 1), v = y / (float)h;
                // Ragged edges: the width wanders along the path.
                float wobble = (TileNoise2(0.5f, v * 8f, 4, 8, 51) - 0.5f) * 0.25f;
                float across = Mathf.Abs(u - 0.5f + wobble) * 2f;
                float edge = 1f - Ramp(0.35f, 1f, across);
                float patches = TileNoise2(u * 4f, v * 10f, 4, 10, 52) * 0.6f + TileNoise2(u * 12f, v * 30f, 12, 30, 53) * 0.4f;
                float alpha = edge * Mathf.Lerp(0.15f, 0.75f, patches);

                float grit = TileNoise2(u * 40f, v * 80f, 40, 80, 54);
                Color c = Color.Lerp(new Color(0.1f, 0.085f, 0.07f), new Color(0.17f, 0.15f, 0.12f), patches * 0.6f + grit * 0.4f);
                // The odd pebble pressed into the trodden earth.
                if (grit > 0.82f) c = Color.Lerp(c, new Color(0.22f, 0.21f, 0.2f), 0.6f);
                c.a = alpha;
                px[y * w + x] = c;
            }
        }
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, true, false)
        {
            name = "Worn Path", wrapMode = TextureWrapMode.Repeat, anisoLevel = 8, filterMode = FilterMode.Trilinear,
        };
        tex.SetPixels32(px);
        tex.Apply(true);
        return tex;
    }

    // ---------------------------------------------------------------- Sky eye

    static Texture2D MakeSkyEye()
    {
        var px = new Color32[Size * Size];
        const float irisRadius = 0.4f; // in -1..1 space, matching the shader

        for (int y = 0; y < Size; y++)
        {
            for (int x = 0; x < Size; x++)
            {
                float u = (x + 0.5f) / Size, v = (y + 0.5f) / Size;
                float px2 = u * 2f - 1f, py2 = v * 2f - 1f;
                float r = Mathf.Sqrt(px2 * px2 + py2 * py2);

                // Sclera: deep crimson, darker toward the corners (it's a ball), threaded with veins.
                float shade = Mathf.Lerp(1f, 0.3f, Ramp(0.45f, 1.1f, r));
                Color color = new Color(0.32f, 0.05f, 0.04f) * shade;
                float ridged = 1f - Mathf.Abs(Fbm(u, v, 5, 4, 13) * 2f - 1f);
                float veins = Mathf.Pow(ridged, 14f) * Ramp(0.42f, 0.8f, r);
                color = Color.Lerp(color, new Color(0.7f, 0.04f, 0.03f), veins);

                if (r < irisRadius + 0.02f)
                {
                    // Iris: fibres radiating from the centre, hot near the pupil, dark at the rim.
                    float angle = Mathf.Atan2(py2, px2) / (Mathf.PI * 2f) + 0.5f;
                    float fibres = TileNoise(angle * 64f, r * 10f, 64, 77) * 0.6f + TileNoise(angle * 160f, r * 3f, 160, 31) * 0.4f;
                    float t = r / irisRadius;
                    Color iris = Color.Lerp(new Color(1f, 0.6f, 0.18f), new Color(0.55f, 0.04f, 0.02f), Mathf.Pow(t, 0.8f));
                    iris *= 0.7f + 0.6f * fibres;
                    iris = Color.Lerp(iris, new Color(1f, 0.85f, 0.45f), Ramp(0.3f, 0f, t) * 0.6f); // glow round the pupil
                    iris *= Mathf.Lerp(1f, 0.12f, Ramp(0.8f, 1f, t));                             // dark limbal ring
                    color = Color.Lerp(iris, color, Ramp(irisRadius - 0.01f, irisRadius + 0.02f, r));
                }

                color.a = 1f;
                px[y * Size + x] = color;
            }
        }

        var tex = new Texture2D(Size, Size, TextureFormat.RGBA32, true, false)
        {
            name = "Sky Eye", wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Trilinear,
        };
        tex.SetPixels32(px);
        tex.Apply(true);
        return tex;
    }

    // Smooth 0..1 ramp as x goes from edge0 to edge1 (works with edge0 > edge1 too).
    static float Ramp(float edge0, float edge1, float x) => Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(edge0, edge1, x));

    // ---------------------------------------------------------------- Dirt

    const int DirtSize = 1024;

    /// <summary>
    /// Realistic bare soil, like a photo of a forest floor: fine, even brown earth with soft darker damp
    /// patches, sandy grains, and hundreds of little pebbles and clods of mixed sizes and colours pressed
    /// into it, each rounded, lit from above and sitting in its own contact shadow. Pebbles are stamped
    /// in (wrapping at the edges) rather than searched for per pixel, so a large texture builds quickly.
    /// </summary>
    static void MakeDirt()
    {
        const int n = DirtSize;
        var height = new float[n * n];
        var col = new Color[n * n];
        var shadow = new float[n * n]; // contact shadow round the pebbles, applied to the soil only

        // Soil: an even mid brown, a little warmer where it's dry, darker where it's damp, finely grained.
        var soil = new Color(0.23f, 0.18f, 0.145f);
        var damp = new Color(0.15f, 0.115f, 0.095f);
        var dry = new Color(0.27f, 0.215f, 0.175f);
        for (int y = 0; y < n; y++)
        {
            for (int x = 0; x < n; x++)
            {
                float u = x / (float)n, v = y / (float)n;
                float patches = Fbm(u, v, 3, 3, 211);
                float dryness = TileNoise(u * 7f, v * 7f, 7, 212);
                float fine = TileNoise(u * 180f, v * 180f, 180, 213) * 0.5f + TileNoise(u * 360f, v * 360f, 360, 214) * 0.5f;
                float speck = Hash(x, y, 215);

                Color c = Color.Lerp(damp, soil, Ramp(0.25f, 0.65f, patches));
                c = Color.Lerp(c, dry, Ramp(0.6f, 0.9f, dryness) * 0.5f);
                c *= 0.88f + 0.24f * fine;
                if (speck > 0.985f) c = Color.Lerp(c, new Color(0.42f, 0.38f, 0.33f), 0.6f);      // pale sand grains
                else if (speck < 0.02f) c *= 0.6f;                                                // dark grit

                col[y * n + x] = c;
                height[y * n + x] = patches * 0.15f + fine * 0.12f;
            }
        }

        var rng = new System.Random(4242);
        float R(float a, float b) => a + (float)rng.NextDouble() * (b - a);

        // Pebbles and clods: a few larger, more medium, and a great many tiny ones (drawn last, on top).
        void Pebbles(int count, float minR, float maxR)
        {
            for (int i = 0; i < count; i++)
            {
                float cx = R(0f, n), cy = R(0f, n);
                float rx = R(minR, maxR);
                float ry = rx * R(0.65f, 1f);
                float angle = R(0f, Mathf.PI);
                float ca = Mathf.Cos(angle), sa = Mathf.Sin(angle);
                float lumpSeed = R(0f, 100f);

                // What it is: mostly clods of earth, some grey and brown stones, the odd pale or rusty one.
                float kind = R(0f, 1f);
                Color c;
                if (kind < 0.45f) c = new Color(0.26f, 0.2f, 0.16f) * R(0.75f, 1.25f);               // clod
                else if (kind < 0.75f) c = new Color(0.33f, 0.3f, 0.27f) * R(0.7f, 1.2f);             // grey stone
                else if (kind < 0.9f) c = new Color(0.35f, 0.25f, 0.18f) * R(0.75f, 1.15f);           // brown stone
                else if (kind < 0.96f) c = new Color(0.5f, 0.47f, 0.43f) * R(0.85f, 1.1f);            // pale quartz
                else c = new Color(0.38f, 0.2f, 0.12f) * R(0.8f, 1.1f);                              // rusty
                float raised = rx * R(0.6f, 1f);

                int reach = Mathf.CeilToInt(rx * 1.6f) + 2;
                for (int oy = -reach; oy <= reach; oy++)
                {
                    for (int ox = -reach; ox <= reach; ox++)
                    {
                        float px = Mathf.Floor(cx) + ox + 0.5f - cx, py = Mathf.Floor(cy) + oy + 0.5f - cy;
                        float lx = (px * ca + py * sa) / rx, ly = (-px * sa + py * ca) / ry;
                        float angleAround = Mathf.Atan2(ly, lx);
                        float lumpy = 1f + 0.18f * Mathf.Sin(angleAround * 3f + lumpSeed) + 0.08f * Mathf.Sin(angleAround * 7f + lumpSeed * 2f);
                        float d = Mathf.Sqrt(lx * lx + ly * ly) / lumpy;

                        int ix = Mod((int)Mathf.Floor(cx) + ox, n), iy = Mod((int)Mathf.Floor(cy) + oy, n);
                        int k = iy * n + ix;
                        if (d < 1f)
                        {
                            // Rounded top, lit from above, darker round its foot; a little texture of its own.
                            float dome = Mathf.Sqrt(1f - d * d);
                            float light = 0.75f + 0.35f * dome - 0.15f * Mathf.Clamp01(ly) + 0.1f * Mathf.Clamp01(-ly);
                            float grain = 0.9f + 0.2f * Hash(ix, iy, 216);
                            float edge = Mathf.Clamp01((1f - d) * rx * 0.8f); // antialiased rim
                            col[k] = Color.Lerp(col[k], c * light * grain, edge);
                            height[k] = Mathf.Max(height[k], 0.25f + raised * 0.06f * dome);
                        }
                        else if (d < 1.6f)
                        {
                            // Sitting in the soil: a soft contact shadow, heavier below it.
                            float s = (1f - (d - 1f) / 0.6f) * (0.35f + 0.15f * Mathf.Clamp01(ly));
                            shadow[k] = Mathf.Max(shadow[k], s);
                        }
                    }
                }
            }
        }
        Pebbles(70, 12f, 22f);
        Pebbles(1300, 5f, 11f);
        Pebbles(11000, 1.6f, 4.5f);

        var albedo = new Color32[n * n];
        for (int i = 0; i < n * n; i++)
        {
            Color c = col[i] * (1f - shadow[i]);
            c.a = 1f;
            albedo[i] = c;
        }

        dirt = new Texture2D(n, n, TextureFormat.RGBA32, true, false)
        {
            name = "Dirt", wrapMode = TextureWrapMode.Repeat, anisoLevel = 8, filterMode = FilterMode.Trilinear,
        };
        dirt.SetPixels32(albedo);
        dirt.Apply(true);

        var normals = new Color32[n * n];
        const float strength = 9f;
        for (int y = 0; y < n; y++)
        {
            for (int x = 0; x < n; x++)
            {
                float dx = height[y * n + Mod(x + 1, n)] - height[y * n + Mod(x - 1, n)];
                float dy = height[Mod(y + 1, n) * n + x] - height[Mod(y - 1, n) * n + x];
                var nv = new Vector3(-dx * strength, -dy * strength, 1f).normalized;
                normals[y * n + x] = new Color(nv.x * 0.5f + 0.5f, nv.y * 0.5f + 0.5f, nv.z * 0.5f + 0.5f, 1f);
            }
        }
        dirtNormal = new Texture2D(n, n, TextureFormat.RGBA32, true, true)
        {
            name = "Dirt Normal", wrapMode = TextureWrapMode.Repeat, anisoLevel = 8, filterMode = FilterMode.Trilinear,
        };
        dirtNormal.SetPixels32(normals);
        dirtNormal.Apply(true);
    }
}
