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
        public static bool InBeam(PlayerController player, Vector3 point, float marginDegrees, bool ignoreBlockers = false)
        {
            var light = player != null ? player.Flashlight : null;
            if (light == null || !light.IsOn) return false;
            var beam = light.Beam;
            Vector3 to = point - beam.position;
            float dist = to.magnitude;
            if (dist > light.Reach) return false;
            if (Vector3.Angle(beam.forward, to) > light.Settings.beamAngle * 0.5f + marginDegrees) return false;
            return ignoreBlockers || !Occluded(beam.position, point);
        }

        /// <summary>Is something solid between the two points (stopping a little short of the far one)?</summary>
        public static bool Occluded(Vector3 from, Vector3 to)
        {
            Vector3 d = to - from;
            float dist = d.magnitude;
            if (dist < 0.4f) return false;
            return Physics.Raycast(from, d / dist, dist - 0.35f, Blockers, QueryTriggerInteraction.Ignore);
        }

        /// <summary>Horizontal distance between two points.</summary>
        public static float FlatDistance(Vector3 a, Vector3 b)
        {
            float dx = a.x - b.x, dz = a.z - b.z;
            return Mathf.Sqrt(dx * dx + dz * dz);
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
