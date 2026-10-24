using System.Collections.Generic;

namespace Game.Entities
{
    /// <summary>What the sound states and music need to know about something that hunts the player.</summary>
    public interface IThreat
    {
        /// <summary>Out and showing (not hidden away).</summary>
        bool ThreatVisible { get; }
        float DistanceToPlayer { get; }
        /// <summary>It has the player, or is about to (the Hush).</summary>
        bool ThreatTaking { get; }
        /// <summary>Coming for the player right now (Danger music).</summary>
        bool ThreatHunting { get; }
        /// <summary>The sky is watching (the eye open, the giant walking): the Watcher music.</summary>
        bool ThreatWatching { get; }
    }

    /// <summary>Every hunting thing registers here while it exists (the threat monitor reads them).</summary>
    public static class Threats
    {
        static readonly List<IThreat> all = new List<IThreat>();

        public static IReadOnlyList<IThreat> All => all;

        public static void Add(IThreat t)
        {
            if (t != null && !all.Contains(t)) all.Add(t);
        }

        public static void Remove(IThreat t) => all.Remove(t);

        [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetOnPlay() => all.Clear();
    }
}
