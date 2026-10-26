using Game.Core;
using UnityEngine;

namespace Game.Entities
{
    // Hiding behind trunks (section 6.7): duckers appear behind one, and a creep can take it to one on the way
    // to you. Tucked behind, it leans out to peek while you look away, and ducks back when you look.
    public sealed partial class Scour
    {
        bool FindTreeNear(Vector3 p, out Collider tree)
        {
            tree = null;
            var hits = Physics.OverlapSphere(p, 6f, Layers.Mask(Layers.Trees), QueryTriggerInteraction.Ignore);
            float best = float.MaxValue;
            foreach (var h in hits)
            {
                if (!IsTrunk(h)) continue;
                float d = (TrunkCentre(h) - p).sqrMagnitude;
                if (d < best) { best = d; tree = h; }
            }
            return tree != null;
        }

        // A trunk near where a normal creep would land: closer to the player than it is now (so it's still
        // closing in), not so close it could take them, and not much further than a creep would go.
        bool FindTreeOnTheWay(Vector3 next, float dist, float stepLength, Vector3 target, out Collider tree)
        {
            tree = null;
            var hits = Physics.OverlapSphere(next, stepLength, Layers.Mask(Layers.Trees), QueryTriggerInteraction.Ignore);
            float best = float.MaxValue;
            foreach (var h in hits)
            {
                if (!IsTrunk(h)) continue;
                Vector3 p = TrunkCentre(h);
                float toPlayer = PlayerSense.FlatDistance(p, target);
                if (toPlayer >= dist - 0.5f || toPlayer < s.killDistance + 1.5f) continue;
                if (PlayerSense.FlatDistance(p, transform.position) > stepLength * 1.6f) continue;
                float d = PlayerSense.FlatDistance(p, next);
                if (d < best) { best = d; tree = h; }
            }
            if (tree != null && !RoomAt(PeekSpotFor(tree, 0f))) tree = null; // (no room behind it: another trunk, a rock)
            return tree != null;
        }

        // The middle of a trunk at about head height (its collider's, not its object's: they needn't match).
        static Vector3 TrunkCentre(Collider c)
        {
            var b = c.bounds;
            return new Vector3(b.center.x, b.min.y, b.center.z);
        }

        // Right behind the trunk from the player, whether tucked in or peeking: it stays put and only cranes its
        // head round (the pose does that; see PoseShape's Peek). On a thicker or thinner trunk than the pose was
        // made for, it stands that much further to the side it peeks round, so its head still clears the edge and
        // the rest of it stays hidden. (Facing the player, its left is the world's Cross(up, away).)
        Vector3 PeekSpot(float outAmount) => PeekSpotFor(peekTree, outAmount);

        Vector3 PeekSpotFor(Collider peekTree, float outAmount)
        {
            Vector3 trunk = TrunkCentre(peekTree);
            Vector3 away = trunk - player.transform.position;
            away.y = 0f;
            away = away.sqrMagnitude > 0.01f ? away.normalized : Vector3.forward;
            Vector3 left = Vector3.Cross(Vector3.up, away);
            float r = peekTree is CapsuleCollider c ? c.radius * Mathf.Max(peekTree.transform.lossyScale.x, peekTree.transform.lossyScale.z) : Mathf.Min(peekTree.bounds.extents.x, peekTree.bounds.extents.z);
            r = Mathf.Max(r, 0.1f);
            float k = Mathf.Max(0.5f, s.height / 2.4f); // (the pose was made for a 2.4 m scour)
            float shift = Mathf.Clamp(r - 0.32f * k, -0.08f, 0.2f);
            Vector3 spot = trunk + away * (r + 0.25f * k) + left * peekSide * shift;
            spot.y = PlayerSense.GroundHeight(spot);
            return spot;
        }

        // A ducker snaps back behind its trunk when looked at, and leans out again when you look away.
        void UpdatePeeking(float dt)
        {
            bool watched = Time.time - appearedAt > DuckGrace &&
                           PlayerSense.IsLookedAt(player, peekTree.transform.position + Vector3.up * 1.6f, s.lookAngle * 0.6f);
            // Ducks back quickly (a sharp creep, not a jump), leans out again slowly.
            peekOut = Mathf.MoveTowards(peekOut, watched ? 0f : 1f, dt * (watched ? 3f : 0.6f));
            // (Craning out slow and smooth, snapping back in fast.)
            float shown = watched ? peekOut : Mathf.SmoothStep(0f, 1f, peekOut);
            body.SetPeek(shown, peekSide, false);
            transform.position = Vector3.MoveTowards(transform.position, PeekSpot(peekOut), 2.5f * dt);
        }
    }
}
