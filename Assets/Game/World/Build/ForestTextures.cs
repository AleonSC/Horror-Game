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
        static Texture2D patches, grain;
        static float trailShare = -1f, trailHalf = -1f;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetOnPlay()
        {
            floorJob = patchesJob = grainJob = barkJob = deadJob = trailJob = null;
            floor = bark = dead = trail = null;
            patches = grain = null;
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
