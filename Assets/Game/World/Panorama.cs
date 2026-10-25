using UnityEngine;

namespace Game.World
{
    /// <summary>
    /// Behind the main menu, the forest is shown endless: the map wraps every few hundred metres, and normally only
    /// its nearest copy is drawn, so beyond that there'd be nothing. While this is on, the trees and the ground are
    /// also drawn at the copies all round, out to <see cref="Reach"/>, and the fog swallows them into the distance.
    /// </summary>
    public static class Panorama
    {
        public static bool On;

        /// <summary>How far the trees are drawn while it's on (metres).</summary>
        public const float Reach = 480f;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetOnPlay() => On = false;
    }
}
