using System.Threading.Tasks;
using UnityEngine;

namespace Game.World.Build
{
    /// <summary>
    /// The forest's made-in-code textures (<see cref="ForestSurfaces"/>), built on background threads as early as
    /// possible (<see cref="Warm"/>) and kept for the session, so a restart doesn't make them again. Ask for a
    /// set once the world is being built; it waits for any still being made.
    /// </summary>
    public static class ForestTextures
    {
        /// <summary>A surface's textures: colour, bumps and HDRP's mask map (crevice shading in G, shine in A).</summary>
        public sealed class Set
        {
            public Texture2D Colour, Normals, Mask;
        }

        // (Fixed seeds: one look for every world, so they're made once.)
        const int Seed = 1847;

        static Task<SurfacePixels> floorJob, patchesJob, grainJob, barkJob, deadJob, trailJob;
        static Set floor, bark, dead, trail;
        static Texture2D patches, grain, oakLeaves;
        static float trailShare = -1f, trailHalf = -1f;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetOnPlay()
        {
            floorJob = patchesJob = grainJob = barkJob = deadJob = trailJob = null;
            floor = bark = dead = trail = null;
            patches = grain = oakLeaves = null;
            trailShare = trailHalf = -1f;
        }

        /// <summary>Start making them all (on other threads), if they aren't made or being made.</summary>
        public static void Warm(float trailSolidShare, float trailHalfWidth, float trailRepeat)
        {
            if (floor == null && floorJob == null) floorJob = Task.Run(() => ForestSurfaces.Floor(Seed));
            if (patches == null && patchesJob == null) patchesJob = Task.Run(() => ForestSurfaces.FloorPatches(Seed));
            if (grain == null && grainJob == null) grainJob = Task.Run(() => ForestSurfaces.TrailGrain(Seed));
            if (bark == null && barkJob == null) barkJob = Task.Run(() => ForestSurfaces.Bark(Seed, false));
            if (dead == null && deadJob == null) deadJob = Task.Run(() => ForestSurfaces.Bark(Seed + 1, true));
            bool trailChanged = !Mathf.Approximately(trailShare, trailSolidShare) || !Mathf.Approximately(trailHalf, trailHalfWidth);
            if (trailChanged || (trail == null && trailJob == null))
            {
                Destroy(trail);
                trail = null;
                trailShare = trailSolidShare;
                trailHalf = trailHalfWidth;
                trailJob = Task.Run(() => ForestSurfaces.Trail(Seed, trailSolidShare, trailHalfWidth, trailRepeat));
            }
        }

        public static Set Floor => Ready(ref floor, ref floorJob, "Forest floor", true, () => ForestSurfaces.Floor(Seed));

        /// <summary>The forest floor's large patches (a detail map, laid over it much bigger).</summary>
        public static Texture2D FloorPatches => ReadyDetail(ref patches, ref patchesJob, "Forest floor patches", () => ForestSurfaces.FloorPatches(Seed));

        public static Set Bark => Ready(ref bark, ref barkJob, "Bark", true, () => ForestSurfaces.Bark(Seed, false));

        public static Set DeadWood => Ready(ref dead, ref deadJob, "Dead wood", true, () => ForestSurfaces.Bark(Seed + 1, true));

        /// <summary>The trails' earth (call <see cref="Warm"/> first with the trails' size).</summary>
        public static Set Trail => Ready(ref trail, ref trailJob, "Trail earth", false, () => ForestSurfaces.Trail(Seed, Mathf.Max(0f, trailShare), Mathf.Max(0.3f, trailHalf), 3f));

        /// <summary>A spray of oak leaves for the canopy's cards (cut out by its alpha).</summary>
        public static Texture2D OakLeaves
        {
            get
            {
                if (oakLeaves != null) return oakLeaves;
                var px = ForestSurfaces.OakLeaves(Seed);
                oakLeaves = new Texture2D(px.Width, px.Height, TextureFormat.RGBA32, true) { name = "Oak leaves", wrapMode = TextureWrapMode.Clamp, anisoLevel = 4, hideFlags = HideFlags.DontSave };
                // Each smaller level (seen further off) keeps as much leaf showing past the cut-out as the full one:
                // left to plain averaging, the leaves' edges blur below the cut and far canopies go thin and holey.
                var level = px.Albedo;
                int w = px.Width;
                float coverage = Coverage(level, LeafCut);
                for (int mip = 0; mip < oakLeaves.mipmapCount; mip++)
                {
                    if (mip > 0)
                    {
                        level = Half(level, w);
                        w = Mathf.Max(1, w / 2);
                        KeepCoverage(level, coverage, LeafCut);
                    }
                    oakLeaves.SetPixels(level, mip);
                }
                oakLeaves.Apply(false, true);
                return oakLeaves;
            }
        }

        /// <summary>The trails' fine grain (a detail map, in world space).</summary>
        public static Texture2D TrailGrain => ReadyDetail(ref grain, ref grainJob, "Trail grain", () => ForestSurfaces.TrailGrain(Seed));

        static Set Ready(ref Set set, ref Task<SurfacePixels> job, string name, bool repeatBoth, System.Func<SurfacePixels> make)
        {
            if (set != null && set.Colour != null) return set;
            var px = job != null ? job.Result : make();
            job = null;
            set = new Set
            {
                Colour = Texture(px.Width, px.Height, px.Albedo, name, false, repeatBoth),
                Normals = Texture(px.Width, px.Height, px.Normals, name + " bumps", true, repeatBoth),
                Mask = Texture(px.Width, px.Height, px.Mask, name + " mask", true, repeatBoth),
            };
            return set;
        }

        static Texture2D ReadyDetail(ref Texture2D tex, ref Task<SurfacePixels> job, string name, System.Func<SurfacePixels> make)
        {
            if (tex != null) return tex;
            var px = job != null ? job.Result : make();
            job = null;
            tex = Texture(px.Width, px.Height, px.Albedo, name, true, true);
            return tex;
        }

        /// <summary>Where the leaves' alpha is cut (their material uses the same).</summary>
        public const float LeafCut = 0.4f;

        static float Coverage(Color[] px, float cut)
        {
            int n = 0;
            foreach (var c in px) if (c.a > cut) n++;
            return n / (float)Mathf.Max(1, px.Length);
        }

        static Color[] Half(Color[] src, int w)
        {
            int h = w, nw = Mathf.Max(1, w / 2), nh = Mathf.Max(1, h / 2);
            var dst = new Color[nw * nh];
            for (int y = 0; y < nh; y++)
            for (int x = 0; x < nw; x++)
            {
                Color sum = Color.clear;
                float colourWeight = 0f;
                for (int k = 0; k < 4; k++)
                {
                    var c = src[Mathf.Min(h - 1, y * 2 + k / 2) * w + Mathf.Min(w - 1, x * 2 + k % 2)];
                    sum.a += c.a * 0.25f;
                    // (the colour from the leaves only, so their edges don't go grey with the clear background)
                    sum.r += c.r * c.a; sum.g += c.g * c.a; sum.b += c.b * c.a;
                    colourWeight += c.a;
                }
                if (colourWeight > 0f) { sum.r /= colourWeight; sum.g /= colourWeight; sum.b /= colourWeight; }
                dst[y * nw + x] = sum;
            }
            return dst;
        }

        // Scale the alpha so the same share of pixels as the full-size level is past the cut.
        static void KeepCoverage(Color[] px, float want, float cut)
        {
            float lo = 0.5f, hi = 8f;
            for (int i = 0; i < 12; i++)
            {
                float mid = (lo + hi) * 0.5f;
                int n = 0;
                foreach (var c in px) if (c.a * mid > cut) n++;
                if (n / (float)px.Length < want) lo = mid; else hi = mid;
            }
            float scale = (lo + hi) * 0.5f;
            for (int i = 0; i < px.Length; i++) px[i].a = Mathf.Clamp01(px[i].a * scale);
        }

        static Texture2D Texture(int w, int h, Color[] pixels, string name, bool linear, bool repeatBoth)
        {
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, true, linear)
            {
                name = name,
                wrapModeU = repeatBoth ? TextureWrapMode.Repeat : TextureWrapMode.Clamp,
                wrapModeV = TextureWrapMode.Repeat,
                anisoLevel = 8,
                hideFlags = HideFlags.DontSave,
            };
            tex.SetPixels(pixels);
            tex.Apply(true, true); // (kept on the graphics card only)
            return tex;
        }

        static void Destroy(Set s)
        {
            if (s == null) return;
            Object.Destroy(s.Colour);
            Object.Destroy(s.Normals);
            Object.Destroy(s.Mask);
        }
    }
}
