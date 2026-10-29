using Game.World.Build;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests
{
    // A tree far off is the same tree with less in it, not another tree: its outline at every level of detail
    // stays close to the full one's (it used to change, the random draws running differently per level).
    public class ForestLodTests
    {
        static void Close(Bounds full, Bounds far, string what)
        {
            float size = full.size.magnitude;
            Assert.Less((far.center - full.center).magnitude, size * 0.12f, $"{what}: centre moved");
            Assert.Less((far.size - full.size).magnitude, size * 0.2f, $"{what}: size changed");
        }

        [Test]
        public void Oaks_KeepTheirShape_AtEveryLevel()
        {
            for (int seed = 1; seed <= 8; seed++)
            {
                var (t0, c0) = ForestMeshes.Oak(new System.Random(seed), 14f, 0);
                for (int lod = 1; lod < 3; lod++)
                {
                    var (t, c) = ForestMeshes.Oak(new System.Random(seed), 14f, lod);
                    Close(t0.Bounds, t.Bounds, $"oak {seed} trunk LOD{lod}");
                    Close(c0.Bounds, c.Bounds, $"oak {seed} crown LOD{lod}");
                }
            }
        }

        [Test]
        public void DeadTrees_KeepTheirShape_AtEveryLevel()
        {
            for (int seed = 1; seed <= 8; seed++)
            {
                var full = ForestMeshes.DeadTree(new System.Random(seed), 12f, 0).Bounds;
                for (int lod = 1; lod < 3; lod++)
                    Close(full, ForestMeshes.DeadTree(new System.Random(seed), 12f, lod).Bounds, $"dead tree {seed} LOD{lod}");
            }
        }
    }
}
