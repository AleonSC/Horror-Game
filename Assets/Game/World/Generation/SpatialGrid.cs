using System.Collections.Generic;
using UnityEngine;

namespace Game.World.Generation
{
    /// <summary>Points bucketed into cells on the wrapping map, for "is anything within r of here" questions.</summary>
    public sealed class SpatialGrid
    {
        readonly float cell, size;
        readonly int cells;
        readonly List<Vector2>[] buckets;

        public SpatialGrid(float mapSize, float cellSize)
        {
            size = mapSize;
            cells = Mathf.Max(1, Mathf.FloorToInt(mapSize / Mathf.Max(0.5f, cellSize)));
            cell = mapSize / cells;
            buckets = new List<Vector2>[cells * cells];
        }

        public void Add(Vector2 p)
        {
            int i = Index(Cell(p.x), Cell(p.y));
            (buckets[i] ??= new List<Vector2>()).Add(Wrap.Into(p, size));
        }

        /// <summary>Is any point closer than r (the short way round)?</summary>
        public bool AnyWithin(Vector2 p, float r)
        {
            float r2 = r * r;
            int span = Mathf.CeilToInt(r / cell);
            int cx = Cell(p.x), cz = Cell(p.y);
            for (int dz = -span; dz <= span; dz++)
            for (int dx = -span; dx <= span; dx++)
            {
                var b = buckets[Index(cx + dx, cz + dz)];
                if (b == null) continue;
                foreach (var q in b)
                    if (Wrap.SqrDistance(p, q, size) < r2) return true;
            }
            return false;
        }

        /// <summary>How many points are closer than r.</summary>
        public int CountWithin(Vector2 p, float r)
        {
            float r2 = r * r;
            int span = Mathf.CeilToInt(r / cell), n = 0;
            int cx = Cell(p.x), cz = Cell(p.y);
            for (int dz = -span; dz <= span; dz++)
            for (int dx = -span; dx <= span; dx++)
            {
                var b = buckets[Index(cx + dx, cz + dz)];
                if (b == null) continue;
                foreach (var q in b)
                    if (Wrap.SqrDistance(p, q, size) < r2) n++;
            }
            return n;
        }

        int Cell(float x) => Mathf.FloorToInt(Wrap.Into(x, size) / cell) % cells;
        int Index(int x, int z) => (((z % cells) + cells) % cells) * cells + ((x % cells) + cells) % cells;
    }
}
