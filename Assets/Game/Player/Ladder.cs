using System.Collections.Generic;
using UnityEngine;

namespace Game.Player
{
    /// <summary>
    /// A ladder (section 6.1): inside its box, W climbs the way you look (up, or down when you look down), S the
    /// other way. Space jumps off it. The box is in this object's space.
    /// </summary>
    public sealed class Ladder : MonoBehaviour
    {
        static readonly List<Ladder> all = new List<Ladder>();

        [Tooltip("Where you can climb, in this object's space.")]
        public Bounds box = new Bounds(new Vector3(0f, 6f, 0f), new Vector3(1f, 12f, 1f));

        void OnEnable() => all.Add(this);
        void OnDisable() => all.Remove(this);

        public bool Contains(Vector3 p) => box.Contains(transform.InverseTransformPoint(p));

        /// <summary>The middle of where you climb, in the world.</summary>
        public Vector3 Centre => transform.TransformPoint(box.center);

        public static Ladder At(Vector3 p)
        {
            foreach (var l in all) if (l != null && l.Contains(p)) return l;
            return null;
        }
    }
}
