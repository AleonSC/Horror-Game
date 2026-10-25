namespace Game.World.Generation
{
    /// <summary>
    /// The last stage: a share of the pines becomes the breakable tree, the one model that can be felled (always
    /// the same size, so it breaks the same way every time). Only pines at least as tall as it are picked, so
    /// putting it in their place never rises into a sightline the stages before kept clear.
    /// </summary>
    public sealed class BreakableStage : IWorldStage
    {
        public string Name => "Breakable trees";

        /// <summary>The breakable tree's height (metres): one model, one size.</summary>
        public const float Height = 12f;

        public void Run(WorldPlan plan, WorldGenSettings s, Rng rng)
        {
            for (int i = 0; i < plan.Trees.Count; i++)
            {
                var t = plan.Trees[i];
                if (t.Removed || t.Kind != TreeKind.Pine || t.OldGiant) continue;
                if (t.Scale * TreeShapes.Height(TreeKind.Pine, t.Variant, s) < Height) continue;
                if (!rng.Chance(s.breakableTrees)) continue;
                t.Breakable = true;
                plan.Trees[i] = t;
            }
        }
    }
}
