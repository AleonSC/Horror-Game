using System.Collections.Generic;
using UnityEngine;

namespace Game.Core
{
    /// <summary>A circle of safety (a lit lamp, a held torch, a shack...): nothing can take the player inside it.</summary>
    public sealed class SafeZone
    {
        public string Name;
        public Vector3 Center;
        public float Radius;
        public bool Active = true;

        public SafeZone(string name, Vector3 center, float radius)
        {
            Name = name;
            Center = center;
            Radius = radius;
        }

        /// <summary>Is the point inside (ignoring height), with an optional margin added to the radius?</summary>
        public bool Contains(Vector3 point, float margin = 0f)
        {
            if (!Active) return false;
            float dx = point.x - Center.x, dz = point.z - Center.z;
            float r = Radius + margin;
            return dx * dx + dz * dz <= r * r;
        }
    }

    /// <summary>Every safe zone in the world (section 6.5 of the plan). Lamps, torches and shelters add theirs.</summary>
    public sealed class SafeZones
    {
        readonly List<SafeZone> zones = new List<SafeZone>();

        public IReadOnlyList<SafeZone> All => zones;

        public void Add(SafeZone zone)
        {
            if (zone != null && !zones.Contains(zone)) zones.Add(zone);
        }

        public void Remove(SafeZone zone) => zones.Remove(zone);

        public bool Contains(Vector3 point, float margin = 0f) => Contains(point, out _, margin);

        /// <summary>Is the point inside any active zone? Gives the one it's deepest in.</summary>
        public bool Contains(Vector3 point, out SafeZone zone, float margin = 0f)
        {
            zone = null;
            float best = float.MaxValue;
            foreach (var z in zones)
            {
                if (!z.Contains(point, margin)) continue;
                float dx = point.x - z.Center.x, dz = point.z - z.Center.z;
                float depth = Mathf.Sqrt(dx * dx + dz * dz) - z.Radius;
                if (depth < best)
                {
                    best = depth;
                    zone = z;
                }
            }
            return zone != null;
        }
    }
}
