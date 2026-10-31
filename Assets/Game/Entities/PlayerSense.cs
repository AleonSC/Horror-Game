using Game.Core;
using Game.Player;
using UnityEngine;

namespace Game.Entities
{
    /// <summary>
    /// What an entity can tell about the player (section 6.6): are they looking at it, and is the flashlight
    /// on it. Trunks, rocks, buildings and the ground block both.
    /// </summary>
    public static class PlayerSense
    {
        static readonly int Blockers = Layers.Mask(Layers.Default) | Layers.Mask(Layers.Ground) | Layers.Mask(Layers.Trees) | Layers.Mask(Layers.Props);

        /// <summary>Is the point within <paramref name="degrees"/> of the centre of the player's view, and not hidden behind something?</summary>
        public static bool IsLookedAt(PlayerController player, Vector3 point, float degrees)
        {
            if (player == null || player.Piloting) return false; // (flying the drone, you're blind where you stand)
            var eye = player.Head;
            Vector3 to = point - eye.position;
            if (Vector3.Angle(eye.forward, to) > degrees) return false;
            return !Occluded(eye.position, point);
        }

        /// <summary>Is the flashlight on and lighting the point (with a margin in degrees round the beam's edge)?</summary>
        /// <param name="clearNear">Anything solid within this of the point doesn't count (a trunk the thing's standing against, or half in).</param>
        public static bool InBeam(PlayerController player, Vector3 point, float marginDegrees, bool ignoreBlockers = false, float clearNear = 0.35f)
        {
            var light = player != null ? player.Flashlight : null;
            if (light == null || !light.IsOn) return false;
            var beam = light.Beam;
            Vector3 to = point - beam.position;
            float dist = to.magnitude;
            if (dist > light.Reach) return false;
            if (Vector3.Angle(beam.forward, to) > light.Settings.beamAngle * 0.5f + marginDegrees) return false;
            return ignoreBlockers || !Occluded(beam.position, point, clearNear);
        }

        /// <summary>Is something solid between the two points (stopping a little short of the far one)?</summary>
        public static bool Occluded(Vector3 from, Vector3 to, float shortBy = 0.35f)
        {
            Vector3 d = to - from;
            float dist = d.magnitude;
            if (dist < shortBy + 0.05f) return false;
            return Physics.Raycast(from, d / dist, dist - shortBy, Blockers, QueryTriggerInteraction.Ignore);
        }

        /// <summary>Horizontal distance between two points.</summary>
        public static float FlatDistance(Vector3 a, Vector3 b)
        {
            float dx = a.x - b.x, dz = a.z - b.z;
            return Mathf.Sqrt(dx * dx + dz * dz);
        }

        /// <summary>
        /// Up where nothing on the ground can get hold of them: their feet this far or more above the ground
        /// under them (up a watchtower, high on its ladder). A jump doesn't count.
        /// </summary>
        public static bool OutOfReach(PlayerController player, float above = 2.5f)
        {
            if (player == null) return false;
            Vector3 feet = player.transform.position;
            return feet.y - GroundHeight(feet) > above;
        }

        /// <summary>The ground under a point (or the point itself if there's none).</summary>
        public static float GroundHeight(Vector3 p)
        {
            if (Physics.Raycast(p + Vector3.up * 30f, Vector3.down, out var hit, 80f, Layers.Mask(Layers.Ground), QueryTriggerInteraction.Ignore))
                return hit.point.y;
            return p.y;
        }
    }
}
