namespace Game.World.Generation
{
    /// <summary>
    /// Stage 7 (section 3.7): batteries scattered through the forest, and what's in each place: a shack's table
    /// (torch and lighter, maybe a battery or the key fob), a campsite's tent (a snack) and backpack (flare gun,
    /// glowsticks, batteries, maybe the key fob), the car's glove box (batteries), back seat (a radio) and trunk (the camera), radios here and there, and the map up on the watchtower's deck.
    /// </summary>
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

            var loot = rng.Fork(7);
            for (int i = 0; i < plan.Structures.Count; i++)
            {
                switch (plan.Structures[i].Kind)
                {
                    case StructureKind.Shack:
                        Add(plan, i, LootPlace.Table, LootKind.Torch, 1);
                        Add(plan, i, LootPlace.Table, LootKind.Lighter, 1);
                        Add(plan, i, LootPlace.Table, LootKind.Battery, s.batteriesInShack + (loot.Chance(s.shackBatteryChance) ? 1 : 0));
                        if (loot.Chance(s.keyFobChance)) Add(plan, i, LootPlace.Table, LootKind.KeyFob, 1);
                        if (loot.Chance(s.radioChance)) Add(plan, i, LootPlace.Table, LootKind.Radio, 1);
                        break;
                    case StructureKind.Cabin:
                        Add(plan, i, LootPlace.Table, LootKind.Battery, s.batteriesInCabin);
                        break;
                    case StructureKind.Campsite:
                        Add(plan, i, LootPlace.Tent, LootKind.Snack, 1);
                        Add(plan, i, LootPlace.Backpack, LootKind.FlareGun, 1);
                        Add(plan, i, LootPlace.Backpack, LootKind.Glowsticks, s.glowsticksInBackpack);
                        Add(plan, i, LootPlace.Backpack, LootKind.Battery, s.batteriesInBackpack);
                        if (loot.Chance(s.keyFobChance)) Add(plan, i, LootPlace.Backpack, LootKind.KeyFob, 1);
                        if (loot.Chance(s.radioChance)) Add(plan, i, LootPlace.Backpack, LootKind.Radio, 1);
                        break;
                    case StructureKind.Car:
                        Add(plan, i, LootPlace.GloveBox, LootKind.Battery, s.batteriesInGloveBox);
                        Add(plan, i, LootPlace.Trunk, LootKind.Camera, 1);
                        Add(plan, i, LootPlace.BackSeat, LootKind.Radio, 1);
                        break;
                    case StructureKind.Bunker:
                        // Old supplies on a crate at the back of its dark room.
                        Add(plan, i, LootPlace.Table, LootKind.Battery, 2);
                        Add(plan, i, LootPlace.Table, LootKind.Glowsticks, 2);
                        if (loot.Chance(0.5f)) Add(plan, i, LootPlace.Table, LootKind.FlareGun, 1);
                        break;
                    case StructureKind.Watchtower:
                        Add(plan, i, LootPlace.Deck, LootKind.Map, 1); // (the ranger's sketch of the trails)
                        break;
                }
            }
        }

        static void Add(WorldPlan plan, int structure, LootPlace place, LootKind kind, int count)
        {
            if (count > 0) plan.Loot.Add(new Loot { Structure = structure, Place = place, Kind = kind, Count = count });
        }
    }
}
