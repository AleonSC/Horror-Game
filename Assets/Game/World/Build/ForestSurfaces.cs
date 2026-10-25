using UnityEngine;

namespace Game.World.Build
{
    /// <summary>
    /// The forest's surfaces, made in code: the forest floor (dark soil and moss under a litter of pine needles,
    /// fallen leaves, twigs and stones), the trails' packed earth and gravel, and pine bark (plates of layered
    /// flakes split by deep fissures) and weathered dead wood. Each is a colour, bumps and a mask (crevice shading
    /// and shine) that agree, and each tiles.
    /// </summary>
    public static class ForestSurfaces
    {
        /// <summary>How many metres the forest floor's texture covers before it repeats.</summary>
        public const float FloorTile = 4f;

        /// <summary>How bark materials tile on the built-in trunks (twice round, once every 2 m up).</summary>
        public static readonly Vector2 BarkTiling = new Vector2(2f, 0.5f);

        // ------------------------------------------------------------------ forest floor

        /// <summary>The forest floor, <paramref name="size"/> pixels across (one tile is <see cref="FloorTile"/> m).</summary>
        public static SurfacePixels Floor(int seed, int size = 1024)
        {
            var cv = new SurfaceCanvas(size, size);
            var rng = new System.Random(seed);
            float cm = size / (FloorTile * 100f); // (pixels per centimetre)

            // Soil, and moss in patches.
            var moss = new float[size * size];
            System.Threading.Tasks.Parallel.For(0, size, y =>
            {
                for (int x = 0; x < size; x++)
                {
                    float u = x / (float)size, v = y / (float)size;
                    int k = y * size + x;
                    float soil = Noise.Tiling(u, v, 8, 8, 5, seed);
                    float crumbs = Noise.Tiling(u, v, 128, 128, 2, seed + 3);
                    float m = Noise.Step(0.6f, 0.68f, Noise.Tiling(u, v, 3, 3, 4, seed + 5));
                    float clump = Noise.Tiling(u, v, 40, 40, 2, seed + 8);
                    float fuzz = Noise.Tiling(u, v, 160, 160, 2, seed + 9);
                    moss[k] = m;
                    cv.Height[k] = soil * 0.5f + crumbs * 0.25f + m * (0.35f + fuzz * 0.25f + clump * 0.25f);
                    Color dirt = Color.Lerp(new Color(0.03f, 0.022f, 0.016f), new Color(0.085f, 0.06f, 0.042f), Noise.Step(0.3f, 0.7f, soil * 0.6f + crumbs * 0.4f));
                    Color green = Color.Lerp(new Color(0.028f, 0.036f, 0.016f), new Color(0.075f, 0.095f, 0.04f), fuzz * 0.5f + clump * 0.5f);
                    cv.Colour[k] = Color.Lerp(dirt, green, m);
                    cv.Shine[k] = Mathf.Lerp(0.3f, 0.06f, m); // (damp soil has a faint sheen; moss none)
                }
            });

            // Old litter: needles long since flattened and darkened, lying in the soil under everything.
            int old = Mathf.RoundToInt(9000 * (size / 1024f) * (size / 1024f));
            for (int i = 0; i < old; i++)
            {
                var at = new Vector2((float)rng.NextDouble() * size, (float)rng.NextDouble() * size);
                if (moss[cv.Index((int)at.x, (int)at.y)] > 0.5f) continue;
                float a = Range(rng, 0f, 6.283f);
                Color c = new Color(0.06f, 0.045f, 0.032f) * Range(rng, 0.7f, 1.4f);
                cv.Strand(at, at + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * Range(rng, 3f, 7f) * cm, 1.1f, cv.HeightAt(at.x, at.y), 0.08f, c, c, 0.3f);
            }

            // Stones and pebbles, half sunk in.
            for (int i = 0; i < 60; i++)
            {
                var at = new Vector2(rng.Next(size), rng.Next(size));
                float r = Range(rng, 0.4f, i < 8 ? 3.5f : 1.5f) * cm;
                float rest = cv.HeightAt(at.x, at.y) - r * 0.05f;
                cv.Stone(at, r, rest, Mathf.Min(1.2f, r * 0.12f), Color.Lerp(new Color(0.17f, 0.16f, 0.15f), new Color(0.3f, 0.28f, 0.25f), (float)rng.NextDouble()), rng.Next());
            }

            // Fallen leaves (a few broadleaves blown in among the pines), some half rotted.
            for (int i = 0; i < 90; i++)
            {
                var at = new Vector2(rng.Next(size), rng.Next(size));
                float len = Range(rng, 3f, 8f) * cm;
                Color c = Pick(rng, new Color(0.24f, 0.13f, 0.055f), new Color(0.17f, 0.1f, 0.05f), new Color(0.3f, 0.2f, 0.08f), new Color(0.12f, 0.08f, 0.05f));
                cv.Leaf(at, len, Range(rng, 0f, 6.283f), cv.HeightAt(at.x, at.y) + 0.05f, c, rng.NextDouble() < 0.4 ? 5f : 0f, (float)rng.NextDouble() * 0.7f, rng.Next(1000));
            }

            // Twigs, crooked, in two or three bends.
            for (int i = 0; i < 30; i++)
            {
                var p = new Vector2(rng.Next(size), rng.Next(size));
                float heading = Range(rng, 0f, 6.283f), width = Range(rng, 0.25f, 0.7f) * cm;
                Color bark = Color.Lerp(new Color(0.1f, 0.08f, 0.06f), new Color(0.22f, 0.19f, 0.15f), (float)rng.NextDouble());
                int bends = rng.Next(2, 5);
                for (int b = 0; b < bends; b++)
                {
                    heading += Range(rng, -0.5f, 0.5f);
                    var q = p + new Vector2(Mathf.Cos(heading), Mathf.Sin(heading)) * Range(rng, 3f, 9f) * cm;
                    float rest = Mathf.Max(cv.HeightAt(p.x, p.y), cv.HeightAt(q.x, q.y)) + 0.1f;
                    cv.Strand(p, q, width, rest, width * 0.35f + 0.3f, bark, bark * 0.9f, 0.2f, 0.1f);
                    // (a side twig now and then)
                    if (rng.NextDouble() < 0.4)
                    {
                        float off = heading + Range(rng, 0.5f, 1f) * (rng.NextDouble() < 0.5 ? -1f : 1f);
                        var mid = (p + q) * 0.5f;
                        cv.Strand(mid, mid + new Vector2(Mathf.Cos(off), Mathf.Sin(off)) * Range(rng, 1.5f, 4f) * cm, width * 0.6f, rest, width * 0.25f + 0.2f, bark, bark, 0.2f, 0.4f);
                    }
                    p = q;
                }
            }

            // The needles: a deep litter of them, in pairs as they fall, from freshly fallen rust to old grey,
            // thinner on the moss.
            int needles = Mathf.RoundToInt(16000 * (size / 1024f) * (size / 1024f));
            for (int i = 0; i < needles; i++)
            {
                var at = new Vector2((float)rng.NextDouble() * size, (float)rng.NextDouble() * size);
                if (moss[cv.Index((int)at.x, (int)at.y)] > 0.5f && rng.NextDouble() < 0.7) continue;
                float len = Range(rng, 4.5f, 8.5f) * cm, angle = Range(rng, 0f, 6.283f);
                Color c = Needle(rng);
                float rest = cv.HeightAt(at.x, at.y) + 0.02f;
                for (int pair = 0; pair < 2; pair++)
                {
                    float a = angle + (pair == 0 ? 0f : Range(rng, 0.05f, 0.35f));
                    var dir = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                    cv.Strand(at, at + dir * len, Mathf.Max(1.1f, 0.13f * cm * 4f), rest, 0.22f, c, c * 0.8f, 0.35f);
                }
            }
            return cv.Finish(1.6f, 0.6f, 5);
        }

        static Color Needle(System.Random rng)
        {
            double r = rng.NextDouble();
            Color c = r < 0.15 ? new Color(0.3f, 0.17f, 0.08f)   // fresh, rusty
                    : r < 0.45 ? new Color(0.24f, 0.17f, 0.1f)   // tan
                    : r < 0.8 ? new Color(0.16f, 0.125f, 0.09f)  // grey-brown
                    : new Color(0.085f, 0.065f, 0.045f);         // rotting
            return c * Range(rng, 0.8f, 1.15f);
        }

        /// <summary>
        /// The forest floor's large-scale variation, laid over it much bigger (HDRP's detail map: R lighter and
        /// darker, 0.5 none; G and A the tilt; B shine, 0.5 none), so the floor's repeat doesn't show.
        /// </summary>
        public static SurfacePixels FloorPatches(int seed, int size = 256)
        {
            var px = new SurfacePixels { Width = size, Height = size, Albedo = new Color[size * size] };
            var h = new float[size * size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
                h[y * size + x] = Noise.Tiling(x / (float)size, y / (float)size, 4, 4, 4, seed + 41);
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                int k = y * size + x;
                float u = x / (float)size, v = y / (float)size;
                float light = Noise.Tiling(u, v, 3, 3, 4, seed + 43);
                float wet = Noise.Tiling(u, v, 5, 5, 3, seed + 47);
                float hx = h[y * size + (x + 1) % size] - h[y * size + (x + size - 1) % size];
                float hy = h[((y + 1) % size) * size + x] - h[((y + size - 1) % size) * size + x];
                var n = new Vector3(-hx * 6f, -hy * 6f, 1f).normalized;
                px.Albedo[k] = new Color(Mathf.Clamp01(0.5f + (light - 0.5f) * 0.9f), n.y * 0.5f + 0.5f, Mathf.Clamp01(0.5f + (wet - 0.5f) * 0.6f), n.x * 0.5f + 0.5f);
            }
            return px;
        }

        // ------------------------------------------------------------------ trails

        /// <summary>
        /// The trails' packed earth (U from the middle line, 0.5, out to the edge of the fade, 0, mirrored beyond;
        /// V along it, one repeat every <paramref name="repeat"/> m): sandy, trodden smooth in the middle with the
        /// odd boot print, gravel worked up out of it, and pine litter creeping in from the edges, where it fades.
        /// Solid out to <paramref name="solidShare"/> of the way from the middle; the colour's alpha is the fade.
        /// </summary>
        public static SurfacePixels Trail(int seed, float solidShare, float halfWidth, float repeat)
        {
            const int across = 192, along = 512; // (the canvas: the middle line at x = 0, the outer edge at x = across)
            var cv = new SurfaceCanvas(256, along);
            var rng = new System.Random(seed * 7 + 3);
            float cm = along / (repeat * 100f);
            float acrossCm = halfWidth * 100f / across;

            // Boot prints: shallow dips, mostly toward the middle, along the path.
            var prints = new System.Collections.Generic.List<(Vector2 at, float angle)>();
            for (int i = 0; i < 26; i++)
                prints.Add((new Vector2(Range(rng, 0f, across * 0.45f), Range(rng, 0f, along)), Range(rng, -0.3f, 0.3f) + Mathf.PI * 0.5f));
            for (int y = 0; y < along; y++)
            for (int x = 0; x < cv.W; x++)
            {
                int k = y * cv.W + x;
                float t = Mathf.Clamp01(x / (float)across);
                float u = x / (float)cv.W, v = y / (float)along;
                float lumps = Noise.Tiling(u, v, 4, 8, 4, seed + 1);
                float grain = Noise.Tiling(u, v, 64, 128, 2, seed + 2);
                float worn = 1f - Noise.Step(0.1f, 0.6f, t); // (trodden flat and pale down the middle)
                float dip = 0f;
                foreach (var p in prints)
                {
                    Vector2 d = new Vector2(x * acrossCm * cm, y) - new Vector2(p.at.x * acrossCm * cm, p.at.y);
                    if (Mathf.Abs(d.y) > 20f * cm) continue;
                    float ca = Mathf.Cos(p.angle), sa = Mathf.Sin(p.angle);
                    float lx = (d.x * ca + d.y * sa) / (14f * cm), ly = (-d.x * sa + d.y * ca) / (5.5f * cm);
                    float e = lx * lx + ly * ly;
                    if (e < 1f) dip = Mathf.Max(dip, (1f - e) * 0.35f);
                }
                cv.Height[k] = lumps * Mathf.Lerp(0.6f, 0.25f, worn) + grain * 0.15f - dip;
                Color earth = Color.Lerp(new Color(0.2f, 0.15f, 0.105f), new Color(0.33f, 0.26f, 0.18f), Noise.Step(0.25f, 0.75f, lumps * 0.7f + grain * 0.3f));
                earth = Color.Lerp(earth, earth * 1.15f, worn * 0.5f);
                earth *= 1f - dip * 0.5f;
                // Toward the edge it darkens into the forest floor's litter as it fades.
                earth = Color.Lerp(earth, new Color(0.08f, 0.06f, 0.042f), Noise.Step(solidShare * 0.6f, 1f, t) * 0.75f);
                cv.Colour[k] = earth;
                cv.Shine[k] = Mathf.Lerp(0.22f, 0.32f, worn);
            }
            // Gravel and grit, and a few bigger stones, worked up out of the earth (more where it's worn).
            for (int i = 0; i < 2600; i++)
            {
                float x = across * Mathf.Pow((float)rng.NextDouble(), 1.3f), y = (float)rng.NextDouble() * along;
                bool big = i < 30;
                float r = (big ? Range(rng, 1.5f, 4f) : Range(rng, 0.25f, 0.9f)) * cm;
                Color c = Pick(rng, new Color(0.22f, 0.2f, 0.18f), new Color(0.27f, 0.23f, 0.18f), new Color(0.14f, 0.13f, 0.12f), new Color(0.32f, 0.28f, 0.23f));
                cv.Stone(new Vector2(x, y), r, cv.HeightAt(x, y) - r * 0.04f, Mathf.Min(1f, r * 0.1f), c, rng.Next());
            }
            // Pine litter, thick at the edges, the odd needle in the middle.
            for (int i = 0; i < 2600; i++)
            {
                float t = Mathf.Pow((float)rng.NextDouble(), 0.45f);
                float x = t * across, y = (float)rng.NextDouble() * along;
                if (rng.NextDouble() > 0.04f + 0.96f * Noise.Step(0.35f, 0.85f, t)) continue;
                float len = Range(rng, 4.5f, 8.5f) * cm, a = Range(rng, 0f, 6.283f);
                Color c = Needle(rng);
                var at = new Vector2(x, y);
                cv.Strand(at, at + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * len, 1.2f, cv.HeightAt(x, y) + 0.02f, 0.22f, c, c * 0.8f, 0.35f);
            }
            var canvas = cv.Finish(1.4f, 0.55f, 4);

            // Into the texture: U from the edge (0) to the middle (0.5) and mirrored beyond, alpha the fade.
            const int w = 256;
            var tex = new SurfacePixels { Width = w, Height = along, Albedo = new Color[w * along], Normals = new Color[w * along], Mask = new Color[w * along] };
            var heights = new float[w * along];
            for (int y = 0; y < along; y++)
            for (int x = 0; x < w; x++)
            {
                float t = Mathf.Abs((x + 0.5f) / w - 0.5f) * 2f;
                int src = y * cv.W + Mathf.Clamp(Mathf.RoundToInt(t * (across - 1)), 0, across - 1);
                float fade = 1f - Noise.Step(solidShare, 1f, t);
                fade *= Mathf.Lerp(1f, 0.55f + 0.45f * Noise.Tiling(t * 0.5f, y / (float)along, 8, 32, 3, seed + 17), Noise.Step(solidShare, 1f, t)); // (patchy as it fades)
                var c = canvas.Albedo[src];
                c.a = Mathf.Clamp01(fade);
                tex.Albedo[y * w + x] = c;
                tex.Mask[y * w + x] = canvas.Mask[src];
                heights[y * w + x] = cv.Height[src];
            }
            // (Bumps worked out again in the texture's own directions, as the mirroring flips one side.)
            for (int y = 0; y < along; y++)
            for (int x = 0; x < w; x++)
            {
                float hx = heights[y * w + Mathf.Min(w - 1, x + 1)] - heights[y * w + Mathf.Max(0, x - 1)];
                float hy = heights[((y + 1) % along) * w + x] - heights[((y + along - 1) % along) * w + x];
                var n = new Vector3(-hx * 1.4f * 0.7f, -hy * 1.4f, 1f).normalized;
                tex.Normals[y * w + x] = new Color(n.x * 0.5f + 0.5f, n.y * 0.5f + 0.5f, 1f, 1f);
            }
            return tex;
        }

        /// <summary>
        /// The fine grain laid over the trails in world space (HDRP's detail map, so it's linear): sand and little
        /// pebbles raised out of the earth, catching the light. R: lighter and darker (0.5 none); A and G: the
        /// surface's tilt; B: shine (0.5 none).
        /// </summary>
        public static SurfacePixels TrailGrain(int seed, int size = 256)
        {
            var cv = new SurfaceCanvas(size, size);
            var rng = new System.Random(seed * 11 + 9);
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                int k = y * size + x;
                float sand = Noise.Tiling(x / (float)size, y / (float)size, 96, 96, 2, seed + 4);
                cv.Height[k] = sand * 0.3f + Noise.Hash(x, y, seed) * 0.08f;
                cv.Colour[k] = new Color(0.5f + (sand - 0.5f) * 0.3f, 0f, 0f);
            }
            for (int i = 0; i < 900; i++)
            {
                var at = new Vector2(rng.Next(size), rng.Next(size));
                float shade = Range(rng, 0.35f, 0.7f);
                cv.Stone(at, Range(rng, 0.8f, 2.6f), cv.HeightAt(at.x, at.y) - 0.1f, Range(rng, 0.3f, 0.7f), new Color(shade, 0f, 0f), rng.Next());
            }
            var f = cv.Finish(2.2f, 0f, 2);
            var px = new SurfacePixels { Width = size, Height = size, Albedo = new Color[size * size] };
            for (int k = 0; k < size * size; k++)
            {
                var n = f.Normals[k];
                float tone = Mathf.Clamp01(cv.Colour[k].r * f.Mask[k].g + (1f - f.Mask[k].g) * 0.3f);
                px.Albedo[k] = new Color(tone, n.g, 0.5f + (cv.Height[k] - 0.3f) * 0.2f, n.r);
            }
            return px;
        }

        // ------------------------------------------------------------------ bark

        /// <summary>
        /// Bark, <paramref name="width"/> round the trunk by twice that up it (it tiles both ways). Pine: thick
        /// plates, taller than wide, each a jigsaw of overlapping scales lifted at their lower edges, with here and
        /// there one fallen away to show the reddish layer under the grey; deep fissures between the plates, dark
        /// at the bottom with rust-brown walls; a little lichen. <paramref name="dead"/>: the bark mostly fallen
        /// away, leaving weathered silver-grey wood split along its grain, with patches of bark still clinging on.
        /// </summary>
        public static SurfacePixels Bark(int seed, bool dead, int width = 512)
        {
            int height = width * 2;
            var cv = new SurfaceCanvas(width, height);
            var plates = new Cells(6, 4, width, height, 0.4f, seed);
            var scales = new Cells(24, 30, width, height, 0.8f, seed + 100);
            float cm = width / 100f; // (pixels per centimetre, roughly: a tile is about a metre round)
            System.Threading.Tasks.Parallel.For(0, height, y =>
            {
                for (int x = 0; x < width; x++)
                {
                    int k = y * width + x;
                    float u = x / (float)width, v = y / (float)height;
                    // Warped, so the fissures wander.
                    float wx = x + (Noise.Tiling(u, v, 5, 10, 3, seed + 1) - 0.5f) * 40f;
                    float wy = y + (Noise.Tiling(u, v, 5, 10, 3, seed + 2) - 0.5f) * 70f;
                    plates.Find(wx, wy, out float pf1, out float pf2, out int plate, out _, out _);
                    float tone = Noise.Hash(plate, 1, seed), red = Noise.Hash(plate, 2, seed);
                    float gap = (pf2 - pf1) / cm + (Noise.Tiling(u, v, 24, 48, 2, seed + 3) - 0.5f) * 1.5f; // (cm from the fissure)
                    float wide = 1.6f + tone * 2.2f;
                    float onPlate = Noise.Step(wide * 0.6f, wide, gap);
                    float wall = Noise.Step(0f, wide * 0.7f, gap);                 // the fissure's V, deepest in the middle
                    float fibre = Noise.Periodic(u * 160f, v * 10f, 160, 10, seed + 5);

                    // The scales on the plate.
                    scales.Find(x + (Noise.Tiling(u, v, 32, 32, 2, seed + 4) - 0.5f) * 22f, y + (Noise.Tiling(u, v, 32, 32, 2, seed + 13) - 0.5f) * 30f, out float sf1, out float sf2, out int scale, out _, out float sy);
                    float lift = Mathf.Clamp01(0.5f - sy / scales.CellH);          // lifted at the lower edge
                    float seam = Noise.Step(0.5f, 2.5f, sf2 - sf1);                // the gap between scales
                    float sTone = Noise.Hash(scale, 3, seed);
                    bool fallen = Noise.Hash(scale, 4, seed) < 0.04f + red * 0.08f;
                    float crumb = Noise.Tiling(u, v, 96, 192, 2, seed + 14);
                    float weather = Noise.Tiling(u, v, 3, 6, 3, seed + 15);
                    float barkH = wall * 0.55f + onPlate * (0.45f + tone * 0.15f + (fallen ? -0.08f : lift * 0.22f * seam) + fibre * 0.04f + crumb * 0.08f);

                    Color grey = Color.Lerp(new Color(0.17f, 0.14f, 0.12f), new Color(0.33f, 0.28f, 0.23f), tone * 0.35f + sTone * 0.25f + fibre * 0.15f + crumb * 0.25f)
                                 * Mathf.Lerp(0.7f, 1.15f, weather);
                    Color rust = Color.Lerp(new Color(0.27f, 0.15f, 0.09f), new Color(0.4f, 0.24f, 0.14f), fibre * 0.6f + sTone * 0.4f);
                    // (the lifted lower edge of each scale shows a sliver of rust under it)
                    Color scaleC = fallen ? rust : Color.Lerp(grey, rust, Noise.Step(0.75f, 1f, lift) * 0.5f * (0.4f + red));
                    scaleC *= 1f - (1f - seam) * (0.15f + 0.6f * lift) * 0.5f; // (shadowed only under a lifted edge)
                    Color inFissure = Color.Lerp(new Color(0.025f, 0.018f, 0.015f), new Color(0.16f, 0.085f, 0.05f) * (0.8f + fibre * 0.4f), wall);
                    Color barkC = Color.Lerp(inFissure, scaleC, onPlate);
                    float lichen = Noise.Step(0.62f, 0.7f, Noise.Tiling(u, v, 6, 12, 4, seed + 6)) * Noise.Step(0.45f, 0.7f, Noise.Hash(x / 3, y / 3, seed + 7)) * onPlate;
                    barkC = Color.Lerp(barkC, new Color(0.3f, 0.33f, 0.26f), lichen * 0.75f);

                    if (!dead)
                    {
                        cv.Height[k] = barkH + lichen * 0.04f;
                        cv.Colour[k] = barkC;
                        cv.Shine[k] = 0.1f + lift * 0.05f;
                        continue;
                    }
                    // Dead: silvered wood, split along the grain in long cracks, with bark left in patches.
                    float keep = Noise.Step(0.5f, 0.55f, Noise.Tiling(u, v, 3, 5, 4, seed + 8));
                    float grain = Noise.Periodic(u * 220f, v * 6f, 220, 6, seed + 9);
                    // (long cracks wandering up the grain: where a stretched noise crosses its middle)
                    float line = Mathf.Abs(Noise.Tiling(u + (Noise.Tiling(u, v, 4, 4, 2, seed + 10) - 0.5f) * 0.03f, v, 9, 1, 2, seed + 11) - 0.5f);
                    float crack = (1f - Noise.Step(0.004f, 0.006f + 0.02f * Noise.Tiling(u, v, 4, 8, 2, seed + 16), line)) * Noise.Step(0.4f, 0.55f, Noise.Tiling(u, v, 5, 3, 2, seed + 17));
                    float streak = Noise.Tiling(u, v, 14, 2, 2, seed + 12);
                    float woodH = 0.5f + grain * 0.1f - crack * 0.45f;
                    Color wood = Color.Lerp(new Color(0.2f, 0.185f, 0.165f), new Color(0.36f, 0.34f, 0.31f), grain * 0.4f + streak * 0.6f);
                    wood = Color.Lerp(wood, new Color(0.05f, 0.045f, 0.04f), crack);
                    float g = barkC.grayscale;
                    cv.Height[k] = Mathf.Lerp(woodH, barkH * 0.8f + 0.35f, keep);
                    cv.Colour[k] = Color.Lerp(wood, Color.Lerp(barkC, new Color(g, g, g), 0.55f) * 0.85f, keep);
                    cv.Shine[k] = Mathf.Lerp(0.16f, 0.1f, keep);
                }
            });
            return cv.Finish(dead ? 3f : 4f, 0.65f, 8);
        }

        /// <summary>
        /// Cells over a tiling texture (a jittered grid, so it repeats exactly), stretched up or down by
        /// <c>stretchY</c> (below 1: taller cells). Find gives the nearest and next-nearest distances (their
        /// difference is the distance to the cell's edge), which cell, and the offset from its middle.
        /// </summary>
        sealed class Cells
        {
            readonly int nx, ny;
            readonly float cw, ch, stretch;
            readonly Vector2[] points;

            public float CellH => ch;

            public Cells(int across, int up, int width, int height, float stretchY, int seed)
            {
                nx = across;
                ny = up;
                cw = width / (float)across;
                ch = height / (float)up;
                stretch = stretchY;
                points = new Vector2[nx * ny];
                for (int i = 0; i < points.Length; i++) points[i] = new Vector2(0.15f + Noise.Hash(i, 0, seed) * 0.7f, 0.15f + Noise.Hash(i, 1, seed) * 0.7f);
            }

            public void Find(float x, float y, out float f1, out float f2, out int id, out float ox, out float oy)
            {
                int gx = Mathf.FloorToInt(x / cw), gy = Mathf.FloorToInt(y / ch);
                f1 = f2 = float.MaxValue;
                id = 0;
                ox = oy = 0f;
                for (int dy = -1; dy <= 1; dy++)
                for (int dx = -1; dx <= 1; dx++)
                {
                    int cx = gx + dx, cy = gy + dy;
                    int c = ((cy % ny + ny) % ny) * nx + (cx % nx + nx) % nx;
                    float ddx = x - (cx + points[c].x) * cw, ddy = y - (cy + points[c].y) * ch;
                    float d = Mathf.Sqrt(ddx * ddx + ddy * stretch * ddy * stretch);
                    if (d < f1) { f2 = f1; f1 = d; id = c; ox = ddx; oy = ddy; }
                    else if (d < f2) f2 = d;
                }
            }
        }

        // ------------------------------------------------------------------ leaves

        /// <summary>
        /// A spray of oak leaves for the canopy's cards (alpha: the leaves, cut out): forty-odd lobed leaves on
        /// their stalks, overlapping, in greens from deep to yellowing, each with its midrib and a darker edge.
        /// </summary>
        public static SurfacePixels OakLeaves(int seed, int size = 256)
        {
            var px = new SurfacePixels { Width = size, Height = size, Albedo = new Color[size * size] };
            var depth = new float[size * size];
            for (int i = 0; i < depth.Length; i++) { depth[i] = -1f; px.Albedo[i] = new Color(0.1f, 0.14f, 0.05f, 0f); }
            var rng = new System.Random(seed);
            int leaves = 130;
            for (int n = 0; n < leaves; n++)
            {
                // All over the card (thinning to its edge), each turned its own way, the later ones on top.
                float a = Range(rng, 0f, 6.283f), r = Mathf.Sqrt((float)rng.NextDouble()) * size * 0.38f;
                Vector2 stalk = new Vector2(size * 0.5f + Mathf.Cos(a) * r, size * 0.5f + Mathf.Sin(a) * r);
                float angle = Range(rng, 0f, 6.283f);
                float len = Range(rng, 0.11f, 0.19f) * size, wide = len * Range(rng, 0.3f, 0.38f);
                Vector2 along = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)), side = new Vector2(-along.y, along.x);
                Color c = Color.Lerp(new Color(0.07f, 0.12f, 0.035f), new Color(0.2f, 0.27f, 0.07f), (float)rng.NextDouble());
                if (rng.NextDouble() < 0.12) c = Color.Lerp(c, new Color(0.3f, 0.26f, 0.08f), 0.6f); // (yellowing)
                int lobes = rng.Next(4, 6);
                float phase = Range(rng, 0f, 3f);
                int reach = Mathf.CeilToInt(len + 4);
                for (int y = -reach; y <= reach; y++)
                for (int x = -reach; x <= reach; x++)
                {
                    int px0 = Mathf.RoundToInt(stalk.x) + x, py0 = Mathf.RoundToInt(stalk.y) + y;
                    if (px0 < 1 || py0 < 1 || px0 >= size - 1 || py0 >= size - 1) continue;
                    Vector2 d = new Vector2(x, y);
                    float u = Vector2.Dot(d, along) / len, v = Vector2.Dot(d, side);
                    if (u < 0f || u > 1f) continue;
                    // The stalk, then the blade: an oval widest past its middle, with rounded lobes down each side.
                    float blade = u < 0.12f ? 0f : wide * Mathf.Sqrt(Mathf.Max(0f, 1f - Mathf.Pow((u - 0.6f) / 0.48f, 2f)));
                    blade *= 0.72f + 0.28f * Mathf.Abs(Mathf.Sin(u * lobes * Mathf.PI + phase));
                    bool onStalk = u < 0.16f && Mathf.Abs(v) < 1.2f;
                    if (!onStalk && Mathf.Abs(v) > blade) continue;
                    int k = py0 * size + px0;
                    float z = n + u * 0.5f;
                    if (z < depth[k]) continue;
                    depth[k] = z;
                    float edge = blade > 0f ? Mathf.Abs(v) / blade : 0f;
                    float rib = Mathf.Exp(-v * v / 1.5f);
                    Color col = c * (1.05f - 0.3f * edge * edge) + new Color(0.06f, 0.07f, 0.02f) * rib;
                    if (onStalk) col = new Color(0.12f, 0.1f, 0.05f);
                    col.a = 1f;
                    px.Albedo[k] = col;
                }
            }
            return px;
        }

        static float Range(System.Random rng, float min, float max) => min + (float)rng.NextDouble() * (max - min);

        static Color Pick(System.Random rng, params Color[] colours) => colours[rng.Next(colours.Length)] * Range(rng, 0.85f, 1.15f);
    }
}
