namespace Game.World.Generation
{
    /// <summary>Stage 7 (section 3.7): batteries scattered through the forest (items in structures come with them).</summary>
    public sealed class PickupStage : IWorldStage
    {
        public string Name => "Pickups";

        public void Run(WorldPlan plan, WorldGenSettings s, Rng rng)
        {
            var mask = new ForestMask(plan, s, 0);
            var trunks = new SpatialGrid(plan.Size, 2f);
            foreach (var t in plan.Trees) if (!t.Removed) trunks.Add(t.Position);
            var placed = new SpatialGrid(plan.Size, s.batteriesApart);

            for (int attempt = 0; attempt < s.batteries * 200 && plan.Batteries.Count < s.batteries; attempt++)
            {
                var p = rng.InsideSquare(plan.Size);
                if (plan.Distance(p, plan.Start) < s.batteryFromStart) continue;
                if (!mask.Free(p) || trunks.AnyWithin(p, s.batteryFromTrees) || placed.AnyWithin(p, s.batteriesApart)) continue;
                placed.Add(p);
                plan.Batteries.Add(p);
            }
        }
    }
}
