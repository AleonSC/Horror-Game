using UnityEngine;

namespace Game.World.Generation
{
    /// <summary>
    /// Distances on the wrapping map: walk off one edge and you come back in on the other, so the gap between
    /// two points is the shortest way round (section 3.2 of the plan).
    /// </summary>
    public static class Wrap
    {
        /// <summary>b - a the short way round, between -size/2 and size/2.</summary>
        public static float Delta(float a, float b, float size)
        {
            float d = (b - a) % size;
            if (d > size * 0.5f) d -= size;
            else if (d < -size * 0.5f) d += size;
            return d;
        }

        public static Vector2 Delta(Vector2 a, Vector2 b, float size) => new Vector2(Delta(a.x, b.x, size), Delta(a.y, b.y, size));

        public static float Distance(Vector2 a, Vector2 b, float size) => Delta(a, b, size).magnitude;

        public static float SqrDistance(Vector2 a, Vector2 b, float size) => Delta(a, b, size).sqrMagnitude;

        /// <summary>A coordinate brought into 0..size.</summary>
        public static float Into(float x, float size)
        {
            float r = x % size;
            return r < 0f ? r + size : r;
        }

        public static Vector2 Into(Vector2 p, float size) => new Vector2(Into(p.x, size), Into(p.y, size));

        /// <summary>The whole-map offset (multiple of size) that puts the point nearest to <paramref name="viewer"/>.</summary>
        public static Vector2 NearestCopyOffset(Vector2 point, Vector2 viewer, float size)
        {
            float ox = Mathf.Round((viewer.x - point.x) / size) * size;
            float oz = Mathf.Round((viewer.y - point.y) / size) * size;
            return new Vector2(ox, oz);
        }
    }
}
