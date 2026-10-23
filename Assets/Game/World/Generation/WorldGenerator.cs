using System.Collections.Generic;
using System.Diagnostics;

namespace Game.World.Generation
{
    /// <summary>One step of making a world. Works only on the plan's data, so it can run on a thread and be tested.</summary>
    public interface IWorldStage
    {
        string Name { get; }
        void Run(WorldPlan plan, WorldGenSettings s, Rng rng);
    }

    /// <summary>
    /// Makes a world plan from a seed by running the stages in order (section 3.1): the important places first,
    /// then the forest grown round them.
    /// </summary>
    public static class WorldGenerator
    {
        public static readonly IWorldStage[] Stages =
        {
            new TerrainStage(), new LayoutStage(), new PathStage(), new VegetationStage(), new SightlineStage(),
            new PropStage(), new PickupStage(),
        };

        /// <summary>Make the plan. <paramref name="stopAfter"/>: a stage name to stop after (tests), or null for all.</summary>
        public static WorldPlan Generate(int seed, WorldGenSettings s, string stopAfter = null, List<string> timings = null)
        {
            var plan = new WorldPlan { Seed = seed, Size = s.size, Height = new Heightfield(s.size) };
            for (int i = 0; i < Stages.Length; i++)
            {
                var watch = Stopwatch.StartNew();
                Stages[i].Run(plan, s, new Rng(seed, i + 1));
                timings?.Add($"{Stages[i].Name}: {watch.ElapsedMilliseconds} ms");
                if (Stages[i].Name == stopAfter) break;
            }
            return plan;
        }
    }
}
