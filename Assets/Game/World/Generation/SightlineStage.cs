using UnityEngine;

namespace Game.World.Generation
{
    /// <summary>
    /// Stage 3 of the plan's list (section 3.5), run once the trees exist: guarantee the views that matter. Any
    /// tree that would hide the hanging giant from where you wake, or reach into the open sky above a lamp, is
    /// cut.
    /// </summary>
    public sealed class SightlineStage : IWorldStage
    {
        public const float EyeHeight = 1.65f;
        const float LampPostTop = 3.4f;

        public string Name => "Sightlines";

        public void Run(WorldPlan plan, WorldGenSettings s, Rng rng)
        {
            for (int i = 0; i < plan.Trees.Count; i++)
            {
                var t = plan.Trees[i];
                if (t.Removed) continue;
                if (BlocksGiant(plan, s, t) || BlocksLampSky(plan, s, t))
                {
                    t.Removed = true;
                    plan.Trees[i] = t;
                }
            }
        }

        /// <summary>The direction (x, z) a bearing in degrees points (0 = +z, clockwise, like a yaw).</summary>
        public static Vector2 Direction(float bearing)
        {
            float a = bearing * Mathf.Deg2Rad;
            return new Vector2(Mathf.Sin(a), Mathf.Cos(a));
        }

        /// <summary>
        /// Would this tree hide any part of the giant (between giantLow and giantHigh degrees up, within
        /// giantHalfWidth of its bearing) from the eye at the start?
        /// </summary>
        public static bool BlocksGiant(WorldPlan plan, WorldGenSettings s, TreeSpot t)
        {
            Vector2 d = Wrap.Delta(plan.Start, t.Position, plan.Size);
            float dist = d.magnitude;
            if (dist < 0.5f) return true;
            float height = TreeShapes.Height(t.Kind, t.Variant, s) * t.Scale;
            float crown = TreeShapes.CrownRadius(t.Kind) * t.Scale;
            float eye = plan.GroundAt(plan.Start) + EyeHeight;
            float top = plan.GroundAt(t.Position) + height;
            float topElevation = Mathf.Atan2(top - eye, Mathf.Max(0.1f, dist - crown)) * Mathf.Rad2Deg;
            if (topElevation < s.giantLow) return false; // too low to reach the giant's feet
            Vector2 dir = Direction(plan.GiantBearing);
            float off = Vector2.Angle(dir, d);
            float halfWidth = s.giantHalfWidth + Mathf.Atan2(crown, dist) * Mathf.Rad2Deg;
            return off < halfWidth;
        }

        /// <summary>Would this tree's crown reach into the cone of open sky above a lamp?</summary>
        public static bool BlocksLampSky(WorldPlan plan, WorldGenSettings s, TreeSpot t)
        {
            float height = TreeShapes.Height(t.Kind, t.Variant, s) * t.Scale;
            float crown = TreeShapes.CrownRadius(t.Kind) * t.Scale;
            float tan = Mathf.Tan(s.lampSkyCone * Mathf.Deg2Rad);
            foreach (var lamp in plan.Lamps)
            {
                float dist = plan.Distance(lamp.Position, t.Position);
                float rise = plan.GroundAt(t.Position) + height - (plan.GroundAt(lamp.Position) + LampPostTop);
                if (rise <= 0f) continue;
                if (dist - crown < tan * rise) return true;
            }
            return false;
        }

        /// <summary>For tests: is the giant visible along its bearing past every (kept) tree and the ground?</summary>
        public static bool GiantVisible(WorldPlan plan, WorldGenSettings s)
        {
            foreach (var t in plan.Trees)
                if (!t.Removed && BlocksGiant(plan, s, t)) return false;
            // The ground (hills) along the bearing must stay under the giant's feet.
            float eye = plan.GroundAt(plan.Start) + EyeHeight;
            Vector2 dir = Direction(plan.GiantBearing);
            for (float r = 5f; r < plan.Size * 0.5f; r += 2f)
            {
                float ground = plan.GroundAt(Wrap.Into(plan.Start + dir * r, plan.Size));
                if (Mathf.Atan2(ground - eye, r) * Mathf.Rad2Deg >= s.giantLow) return false;
            }
            return true;
        }
    }
}
