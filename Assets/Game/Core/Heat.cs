using System.Collections.Generic;
using UnityEngine;

namespace Game.Core
{
    /// <summary>
    /// Warm things, for the drone's thermal view (section 6.3): each living thing registers where it is and how big,
    /// and the view draws a glowing shape there. Remove it when it's gone.
    /// </summary>
    public static class Heat
    {
        public sealed class Source
        {
            public Transform At;
            public float Height, Radius;
            /// <summary>Is it there to be seen now? (Null: whenever its object is active.)</summary>
            public System.Func<bool> Shown;
            public bool Visible => At != null && At.gameObject.activeInHierarchy && (Shown == null || Shown());
        }

        static readonly List<Source> all = new List<Source>();
        public static IReadOnlyList<Source> All => all;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetOnPlay() => all.Clear();

        public static Source Add(Transform at, float height, float radius, System.Func<bool> shown = null)
        {
            var s = new Source { At = at, Height = height, Radius = radius, Shown = shown };
            all.Add(s);
            return s;
        }

        public static void Remove(Source s) => all.Remove(s);
    }
}
