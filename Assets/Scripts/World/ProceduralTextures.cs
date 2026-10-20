using UnityEngine;

/// <summary>
/// Generates the game's textures in code (no image files needed): tiling dirt for the ground,
/// the moon, a soft glow, and light shafts. Each is made once on first use and shared.
/// </summary>
public static class ProceduralTextures
{
    static Texture2D dirt, dirtNormal, moon, glow, shaft;

    /// <summary>Tiling forest-floor dirt: damp patches, grit and small pebbles.</summary>
    public static Texture2D Dirt { get { if (dirt == null) MakeDirt(); return dirt; } }
    /// <summary>Normal map matching Dirt, so the flashlight picks out bumps and pebbles.</summary>
    public static Texture2D DirtNormal { get { if (dirtNormal == null) MakeDirt(); return dirtNormal; } }
    /// <summary>A pale, cratered moon disc (transparent around it).</summary>
    public static Texture2D Moon => moon != null ? moon : (moon = MakeMoon());
    /// <summary>A soft round glow, bright in the middle and fading to nothing.</summary>
    public static Texture2D Glow => glow != null ? glow : (glow = MakeGlow());
    /// <summary>A soft beam: fades out at the sides and ends, with faint streaks.</summary>
    public static Texture2D Shaft => shaft != null ? shaft : (shaft = MakeShaft());

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

    // ---------------------------------------------------------------- Dirt

    static void MakeDirt()
    {
        const int size = 512;
        var height = new float[size * size];
        var albedo = new Color32[size * size];

        // Pebbles: a few per grid cell, at random spots.
        const int pebbleGrid = 12;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float u = x / (float)size, v = y / (float)size;

                float patches = Fbm(u, v, 3, 3, 11);          // damp, darker areas
                float grain = Fbm(u, v, 24, 3, 47);            // gritty soil
                float speck = TileNoise(u * 128f, v * 128f, 128, 91);

                float pebble = 0f;
                int cx = (int)(u * pebbleGrid), cy = (int)(v * pebbleGrid);
                for (int oy = -1; oy <= 1; oy++)
                for (int ox = -1; ox <= 1; ox++)
                {
                    int gx = cx + ox, gy = cy + oy;
                    for (int k = 0; k < 2; k++)
                    {
                        int hx = Mod(gx, pebbleGrid), hy = Mod(gy, pebbleGrid);
                        float px = (gx + Hash(hx, hy, 200 + k)) / pebbleGrid;
                        float py = (gy + Hash(hx, hy, 300 + k)) / pebbleGrid;
                        float r = 0.006f + 0.01f * Hash(hx, hy, 400 + k);
                        float d = Mathf.Sqrt((u - px) * (u - px) + (v - py) * (v - py));
                        pebble = Mathf.Max(pebble, Mathf.Clamp01(1f - d / r));
                    }
                }
                pebble = Mathf.Sqrt(pebble);

                float h = patches * 0.4f + grain * 0.4f + speck * 0.1f + pebble * 0.6f;
                height[y * size + x] = h;

                var soil = Color.Lerp(new Color(0.09f, 0.065f, 0.045f), new Color(0.16f, 0.12f, 0.085f), patches);
                soil *= 0.8f + 0.4f * grain;
                if (speck > 0.8f) soil *= 0.7f;                                  // dark grit
                soil = Color.Lerp(soil, new Color(0.22f, 0.21f, 0.2f), pebble * 0.8f); // grey stones
                albedo[y * size + x] = soil;
            }
        }

        dirt = new Texture2D(size, size, TextureFormat.RGBA32, true, false) { name = "Dirt", wrapMode = TextureWrapMode.Repeat, anisoLevel = 8, filterMode = FilterMode.Trilinear };
        dirt.SetPixels32(albedo);
        dirt.Apply(true);

        // Normal map from the height (wrapping at the edges). Stored as (x, y, z, 1), which Unity's
        // normal unpacking reads correctly on desktop for both pipelines.
        var normals = new Color32[size * size];
        const float strength = 4f;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = height[y * size + Mod(x + 1, size)] - height[y * size + Mod(x - 1, size)];
                float dy = height[Mod(y + 1, size) * size + x] - height[Mod(y - 1, size) * size + x];
                var n = new Vector3(-dx * strength, -dy * strength, 1f).normalized;
                normals[y * size + x] = new Color(n.x * 0.5f + 0.5f, n.y * 0.5f + 0.5f, n.z * 0.5f + 0.5f, 1f);
            }
        }
        dirtNormal = new Texture2D(size, size, TextureFormat.RGBA32, true, true) { name = "Dirt Normal", wrapMode = TextureWrapMode.Repeat, anisoLevel = 8, filterMode = FilterMode.Trilinear };
        dirtNormal.SetPixels32(normals);
        dirtNormal.Apply(true);
    }

    // ---------------------------------------------------------------- Sky

    static Texture2D MakeMoon()
    {
        const int size = 256;
        var px = new Color32[size * size];
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float u = (x + 0.5f) / size - 0.5f, v = (y + 0.5f) / size - 0.5f;
                float d = Mathf.Sqrt(u * u + v * v) / 0.46f;
                float alpha = Mathf.Clamp01((1f - d) * 40f); // crisp but anti-aliased edge
                float maria = Fbm(u + 0.5f, v + 0.5f, 3, 4, 7);
                float shade = Mathf.Lerp(0.75f, 1f, Mathf.SmoothStep(0.35f, 0.65f, maria));
                shade *= Mathf.Lerp(1f, 0.85f, d * d); // a little darker toward the rim
                px[y * size + x] = new Color(shade, shade, shade * 1.03f, alpha);
            }
        }
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, true, false) { name = "Moon", wrapMode = TextureWrapMode.Clamp };
        tex.SetPixels32(px);
        tex.Apply(true);
        return tex;
    }

    static Texture2D MakeGlow()
    {
        const int size = 128;
        var px = new Color32[size * size];
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float u = (x + 0.5f) / size - 0.5f, v = (y + 0.5f) / size - 0.5f;
                float d = Mathf.Clamp01(Mathf.Sqrt(u * u + v * v) * 2f);
                float g = Mathf.Pow(1f - d, 3f);
                px[y * size + x] = new Color(g, g, g, g);
            }
        }
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, true, false) { name = "Glow", wrapMode = TextureWrapMode.Clamp };
        tex.SetPixels32(px);
        tex.Apply(true);
        return tex;
    }

    static Texture2D MakeShaft()
    {
        const int w = 64, h = 256;
        var px = new Color32[w * h];
        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                float u = (x + 0.5f) / w, v = (y + 0.5f) / h;
                float across = Mathf.Pow(Mathf.Sin(Mathf.PI * u), 2f);               // soft sides
                float along = Mathf.SmoothStep(0f, 0.25f, v) * Mathf.SmoothStep(1f, 0.6f, v); // fade at both ends, longer at the top
                float streaks = 0.75f + 0.25f * TileNoise(u * 16f, v * 2f, 16, 5);   // dust in the light
                float g = across * along * streaks;
                px[y * w + x] = new Color(g, g, g, g);
            }
        }
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, true, false) { name = "Light Shaft", wrapMode = TextureWrapMode.Clamp };
        tex.SetPixels32(px);
        tex.Apply(true);
        return tex;
    }
}
