using UnityEngine;

namespace Game.World.Generation
{
    /// <summary>The ground's height every metre across the wrapping map, sampled smoothly in between.</summary>
    public sealed class Heightfield
    {
        public readonly int Resolution;   // samples across (one per metre)
        public readonly float Size;       // metres across
        readonly float[] heights;

        public Heightfield(float size)
        {
            Size = size;
            Resolution = Mathf.Max(2, Mathf.RoundToInt(size));
            heights = new float[Resolution * Resolution];
        }

        float Step => Size / Resolution;

        public float this[int x, int z]
        {
            get => heights[Mod(z) * Resolution + Mod(x)];
            set => heights[Mod(z) * Resolution + Mod(x)] = value;
        }

        /// <summary>The height at a point (wrapping), smoothly between samples.</summary>
        public float Sample(float x, float z)
        {
            float u = x / Step, v = z / Step;
            int x0 = Mathf.FloorToInt(u), z0 = Mathf.FloorToInt(v);
            float fx = u - x0, fz = v - z0;
            return Mathf.Lerp(Mathf.Lerp(this[x0, z0], this[x0 + 1, z0], fx), Mathf.Lerp(this[x0, z0 + 1], this[x0 + 1, z0 + 1], fx), fz);
        }

        public float Sample(Vector2 p) => Sample(p.x, p.y);

        /// <summary>How steep the ground is at a point (rise per metre).</summary>
        public float Slope(float x, float z)
        {
            float dx = Sample(x + 1f, z) - Sample(x - 1f, z);
            float dz = Sample(x, z + 1f) - Sample(x, z - 1f);
            return Mathf.Sqrt(dx * dx + dz * dz) * 0.5f;
        }

        public float WorldX(int x) => x * Step;

        int Mod(int i) => ((i % Resolution) + Resolution) % Resolution;
    }
}
