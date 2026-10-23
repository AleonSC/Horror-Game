using UnityEngine;

namespace Game.World.Generation
{
    /// <summary>
    /// A small seeded random generator (xorshift*). The same seed always gives the same world. Each stage forks
    /// its own stream, so changing one stage doesn't reshuffle the others. Safe on any thread.
    /// </summary>
    public sealed class Rng
    {
        ulong state;

        public Rng(int seed, int stream = 0)
        {
            state = Mix((ulong)(uint)seed * 0x9E3779B97F4A7C15UL ^ (ulong)(uint)stream * 0xBF58476D1CE4E5B9UL);
            if (state == 0) state = 0x2545F4914F6CDD1DUL;
        }

        /// <summary>A separate stream for one stage or one kind of thing.</summary>
        public Rng Fork(int stream) => new Rng((int)(Next() & 0x7FFFFFFF), stream);

        public ulong Next()
        {
            state ^= state >> 12;
            state ^= state << 25;
            state ^= state >> 27;
            return state * 0x2545F4914F6CDD1DUL;
        }

        /// <summary>0 (inclusive) to 1 (exclusive).</summary>
        public float Value() => (Next() >> 40) / (float)(1UL << 24);

        public float Range(float min, float max) => min + (max - min) * Value();

        /// <summary>min (inclusive) to max (exclusive).</summary>
        public int Range(int min, int max) => max <= min ? min : min + (int)(Next() % (ulong)(max - min));

        public bool Chance(float p) => Value() < p;

        public float Angle() => Value() * Mathf.PI * 2f;

        public Vector2 InsideSquare(float size) => new Vector2(Value() * size, Value() * size);

        static ulong Mix(ulong z)
        {
            z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
            z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
            return z ^ (z >> 31);
        }
    }
}
