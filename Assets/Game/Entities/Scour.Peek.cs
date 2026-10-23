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
                float d = (h.transform.position - p).sqrMagnitude;
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
                Vector3 p = h.transform.position;
                float toPlayer = PlayerSense.FlatDistance(p, target);
                if (toPlayer >= dist - 0.5f || toPlayer < s.killDistance + 1.5f) continue;
                if (PlayerSense.FlatDistance(p, transform.position) > stepLength * 1.6f) continue;
                float d = PlayerSense.FlatDistance(p, next);
                if (d < best) { best = d; tree = h; }
            }
            return tree != null;
        }

        // Behind the trunk from the player; 'out' 1 leans out to one side, 0 tucked fully behind.
        Vector3 PeekSpot(float outAmount)
        {
            Vector3 trunk = peekTree.transform.position;
            Vector3 away = trunk - player.transform.position;
            away.y = 0f;
            away = away.sqrMagnitude > 0.01f ? away.normalized : Vector3.forward;
            Vector3 side = Vector3.Cross(Vector3.up, away);
            float r = peekTree is CapsuleCollider c ? c.radius * peekTree.transform.lossyScale.x : 0.35f;
            Vector3 spot = trunk + away * (r + 0.3f) + side * (r + 0.1f) * outAmount;
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
            transform.position = Vector3.MoveTowards(transform.position, PeekSpot(peekOut), 2.5f * dt);
        }
    }
}
