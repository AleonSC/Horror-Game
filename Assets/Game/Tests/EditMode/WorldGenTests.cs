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
        public void Wrap_ShortWayRound()
        {
            Assert.AreEqual(-20f, Wrap.Delta(310f, 290f, 320f), 1e-4f);
            Assert.AreEqual(20f, Wrap.Delta(310f, 10f, 320f), 1e-4f);
            Assert.AreEqual(Mathf.Sqrt(200f), Wrap.Distance(new Vector2(315f, 5f), new Vector2(5f, 315f), 320f), 1e-3f);
            Assert.AreEqual(5f, Wrap.Into(325f, 320f), 1e-4f);
            Assert.AreEqual(315f, Wrap.Into(-5f, 320f), 1e-4f);
        }
    }
}
