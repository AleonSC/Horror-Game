using System.Collections.Generic;
using Game.World.Generation;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests
{
    public class WorldGenTests
    {
        static readonly WorldGenSettings S = new WorldGenSettings();
        static readonly Dictionary<int, WorldPlan> cache = new Dictionary<int, WorldPlan>();

        static WorldPlan Plan(int seed)
        {
            if (!cache.TryGetValue(seed, out var plan)) cache[seed] = plan = WorldGenerator.Generate(seed, S);
            return plan;
        }

        [Test]
        public void SameSeed_SameWorld()
        {
            var a = WorldGenerator.Generate(77, S);
            var b = WorldGenerator.Generate(77, S);
            Assert.AreEqual(a.Trees.Count, b.Trees.Count);
            Assert.AreEqual(a.Grass.Count, b.Grass.Count);
            Assert.AreEqual(a.Lamps[3].Position, b.Lamps[3].Position);
            Assert.AreEqual(a.Trees[100].Position, b.Trees[100].Position);
            Assert.AreEqual(a.Height.Sample(12.3f, 45.6f), b.Height.Sample(12.3f, 45.6f));
            Assert.AreEqual(a.Door, b.Door);
        }

        [Test]
        public void DifferentSeeds_DifferentWorlds()
        {
            Assert.AreNotEqual(Plan(1).Lamps[0].Position, Plan(2).Lamps[0].Position);
        }

        [Test]
        public void Ground_TilesAcrossTheEdges()
        {
            var plan = Plan(3);
            for (float z = 0f; z < plan.Size; z += 17.3f)
            {
                Assert.AreEqual(plan.Height.Sample(0f, z), plan.Height.Sample(plan.Size, z), 1e-3f);
                Assert.AreEqual(plan.Height.Sample(z, 0f), plan.Height.Sample(z, plan.Size), 1e-3f);
            }
        }

        [TestCase(1)] [TestCase(5)] [TestCase(9)] [TestCase(123)]
        public void Layout_FollowsTheRules(int seed)
        {
            var plan = Plan(seed);
            Assert.AreEqual(S.lamps, plan.Lamps.Count, "all ten lamps placed");
            foreach (var l in plan.Lamps)
            {
                Assert.GreaterOrEqual(plan.Distance(l.Position, plan.Start), S.lampMinFromStart);
                foreach (var o in plan.Lamps)
                    if (o != l) Assert.GreaterOrEqual(plan.Distance(l.Position, o.Position), S.lampMinApart);
                foreach (var st in plan.Structures)
                    Assert.Greater(plan.Distance(l.Position, st.Position), 7f);
            }

            Assert.GreaterOrEqual(plan.Distance(plan.Door, plan.Start), S.doorMinFromStart);
            Assert.That(plan.Door.x >= S.doorFromEdge && plan.Door.x <= plan.Size - S.doorFromEdge);
            Assert.That(plan.Door.y >= S.doorFromEdge && plan.Door.y <= plan.Size - S.doorFromEdge);

            float startRadius = plan.StartClearing.Radius;
            foreach (var c in plan.Clearings)
                if (c != plan.StartClearing) Assert.Less(c.Radius, startRadius, "the start clearing is the biggest");

            Assert.LessOrEqual(Mathf.Abs(Mathf.DeltaAngle(plan.StartYaw, plan.GiantBearing)), S.giantSpread + 1e-3f);
        }

        [TestCase(1)] [TestCase(7)]
        public void Forest_HasTheRightAmounts(int seed)
        {
            var plan = Plan(seed);
            int kept = 0;
            foreach (var t in plan.Trees) if (!t.Removed) kept++;
            Assert.That(kept, Is.InRange(S.trees * 0.8f, S.trees * 1.05f), "about 2800 trees");
            Assert.That(plan.Grass.Count, Is.GreaterThan(S.grass));
            Assert.That(plan.TallGrass.Count, Is.GreaterThan(500));
            Assert.That(plan.Rocks.Count, Is.GreaterThan(S.rocks * 0.8f));
            Assert.That(plan.Batteries.Count, Is.EqualTo(S.batteries));
            Assert.That(plan.Ponds.Count, Is.EqualTo(S.ponds));
        }

        [Test]
        public void NoTrees_InTheStartClearingOrWater()
        {
            var plan = Plan(11);
            foreach (var t in plan.Trees)
            {
                if (t.Removed) continue;
                Assert.Greater(plan.Distance(t.Position, plan.Start), plan.StartClearing.Radius * 0.85f);
                Assert.IsFalse(plan.InWater(t.Position));
            }
        }

        [Test]
        public void Ponds_AreDeepEnough()
        {
            var plan = Plan(13);
            foreach (var pond in plan.Ponds)
                Assert.LessOrEqual(plan.GroundAt(pond.Centre), pond.Water - S.pondDepth + 1e-3f);
        }

        [Test]
        public void Giant_VisibleFromTheStart_On50Seeds()
        {
            for (int seed = 1000; seed < 1050; seed++)
            {
                var plan = WorldGenerator.Generate(seed, S, "Sightlines");
                Assert.IsTrue(SightlineStage.GiantVisible(plan, S), $"seed {seed}");
            }
        }

        [Test]
        public void LampSky_IsOpen()
        {
            var plan = Plan(21);
            foreach (var t in plan.Trees)
                if (!t.Removed) Assert.IsFalse(SightlineStage.BlocksLampSky(plan, S, t));
        }

        [Test]
        public void YourTrees_HeightsAndVariantsGoIntoThePlan()
        {
            var s = S.Clone();
            s.livingTreeHeights = new[] { 30f, 34f };
            var plan = WorldGenerator.Generate(31, s, "Sightlines");
            foreach (var t in plan.Trees)
                if (t.Kind == TreeKind.Pine) Assert.Less(t.Variant, 2);
            Assert.AreEqual(34f, TreeShapes.Height(TreeKind.Pine, 1, s));
            Assert.IsTrue(SightlineStage.GiantVisible(plan, s), "taller trees: still cut out of the giant's view");
        }

        [Test]
        public void Wrap_ShortWayRound()
        {
            Assert.AreEqual(-20f, Wrap.Delta(310f, 290f, 320f), 1e-4f);
            Assert.AreEqual(20f, Wrap.Delta(310f, 10f, 320f), 1e-4f);
            Assert.AreEqual(Mathf.Sqrt(200f), Wrap.Distance(new Vector2(315f, 5f), new Vector2(5f, 315f), 320f), 1e-3f);
            Assert.AreEqual(5f, Wrap.Into(325f, 320f), 1e-4f);
            Assert.AreEqual(315f, Wrap.Into(-5f, 320f), 1e-4f);
        }
    
        [Test]
        public void EveryPlace_HasItsLoot()
        {
            foreach (int seed in new[] { 4, 5, 6 })
            {
                var plan = Plan(seed);
                for (int i = 0; i < plan.Structures.Count; i++)
                {
                    var kind = plan.Structures[i].Kind;
                    var loot = plan.Loot.FindAll(l => l.Structure == i);
                    bool Has(LootPlace place, LootKind what) => loot.Exists(l => l.Place == place && l.Kind == what);
                    if (kind == StructureKind.Shack)
                    {
                        Assert.IsTrue(Has(LootPlace.Table, LootKind.Torch) && Has(LootPlace.Table, LootKind.Lighter), $"seed {seed}: a shack without its torch and lighter");
                    }
                    else if (kind == StructureKind.Campsite)
                    {
                        Assert.IsTrue(Has(LootPlace.Tent, LootKind.Snack), $"seed {seed}: a tent without a snack");
                        Assert.IsTrue(Has(LootPlace.Backpack, LootKind.FlareGun) && Has(LootPlace.Backpack, LootKind.Glowsticks), $"seed {seed}: a backpack without its flare gun and glowsticks");
                    }
                    else if (kind == StructureKind.Car)
                    {
                        Assert.IsTrue(Has(LootPlace.GloveBox, LootKind.Battery) && Has(LootPlace.Trunk, LootKind.Camera) && Has(LootPlace.BackSeat, LootKind.Radio), $"seed {seed}: the car without its batteries, camera and radio");
                    }
                    else if (kind == StructureKind.Watchtower)
                    {
                        Assert.IsTrue(Has(LootPlace.Deck, LootKind.Map), $"seed {seed}: the watchtower without its map");
                    }
                    else if (kind == StructureKind.Bunker)
                    {
                        Assert.IsTrue(Has(LootPlace.Table, LootKind.Battery) && Has(LootPlace.Table, LootKind.Glowsticks), $"seed {seed}: the bunker without its batteries and glowsticks");
                    }
                    else Assert.AreEqual(0, loot.Count, $"seed {seed}: loot in a {kind}");
                }
            }
        }

        [Test]
        public void ThePlaces_AreAlwaysAllThere()
        {
            for (int seed = 1; seed <= 20; seed++)
            {
                var plan = Plan(seed);
                int Count(StructureKind k) => plan.Structures.FindAll(x => x.Kind == k).Count;
                Assert.AreEqual(S.campsites, Count(StructureKind.Campsite), $"seed {seed}: campsites");
                Assert.AreEqual(1, Count(StructureKind.Cabin), $"seed {seed}: the cabin");
                Assert.AreEqual(S.shacks - 1, Count(StructureKind.Shack), $"seed {seed}: shacks");
                Assert.AreEqual(1, Count(StructureKind.Car), $"seed {seed}: the car");
                Assert.AreEqual(S.bunkers, Count(StructureKind.Bunker), $"seed {seed}: the bunker");
            }
        }

        [Test]
        public void Loot_IsTheSameForTheSameSeed()
        {
            var a = WorldGenerator.Generate(88, S);
            var b = WorldGenerator.Generate(88, S);
            Assert.AreEqual(a.Loot.Count, b.Loot.Count);
            for (int i = 0; i < a.Loot.Count; i++) Assert.AreEqual(a.Loot[i].Kind, b.Loot[i].Kind);
        }
    
        [Test]
        public void Buildings_StandOnLevelGround()
        {
            foreach (int seed in new[] { 4, 5, 6, 7 })
            {
                var plan = Plan(seed);
                foreach (var st in plan.Structures)
                {
                    if (st.Kind == StructureKind.LandmarkTree) continue;
                    float centre = plan.GroundAt(st.Position);
                    float r = Mathf.Max(st.HalfSize.x, st.HalfSize.y);
                    for (int k = 0; k < 8; k++)
                    {
                        float a = k * Mathf.PI / 4f;
                        var p = Wrap.Into(st.Position + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r, plan.Size);
                        Assert.AreEqual(centre, plan.GroundAt(p), 0.05f, $"seed {seed}: the {st.Kind}'s ground isn't level");
                    }
                }
                Assert.AreEqual(plan.GroundAt(plan.Door), plan.GroundAt(Wrap.Into(plan.Door + new Vector2(1.4f, 1.4f), plan.Size)), 0.05f);
            }
        }
    
        [Test]
        public void CabinAndShacks_StandBesideATrail_DoorFacingIt()
        {
            foreach (int seed in new[] { 4, 5, 6, 7, 8 })
            {
                var plan = Plan(seed);
                int found = 0;
                foreach (var st in plan.Structures)
                {
                    if (st.Kind != StructureKind.Shack && st.Kind != StructureKind.Cabin) continue;
                    found++;
                    float a = st.Yaw * Mathf.Deg2Rad;
                    var forward = new Vector2(Mathf.Sin(a), Mathf.Cos(a));
                    var door = st.Position + forward * st.HalfSize.y;
                    // The nearest trail point to its door is just in front of it.
                    float best = float.MaxValue;
                    Vector2 nearest = default;
                    foreach (var t in plan.Trails)
                        foreach (var q in t.Points)
                        {
                            float d = Wrap.Distance(door, q, plan.Size);
                            if (d < best) { best = d; nearest = q; }
                        }
                    Assert.Less(best, 6f, $"seed {seed}: a {st.Kind} {best:0.0} m from any trail");
                    Assert.Greater(Vector2.Dot(Wrap.Delta(st.Position, nearest, plan.Size).normalized, forward), 0.5f, $"seed {seed}: a {st.Kind} facing away from its trail");
                }
                Assert.Greater(found, 0, $"seed {seed}: no cabin or shacks");
            }
        }
    }


}
