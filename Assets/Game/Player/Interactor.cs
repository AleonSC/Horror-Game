using Game.Core;
using UnityEngine;

namespace Game.Player
{
    /// <summary>
    /// What the crosshair is on (section 6.2): first, whatever the crosshair is right on (any part of it: the edge
    /// of a door counts, not just its middle); otherwise the interactable nearest the line of sight within reach,
    /// with a little aim assist, as long as nothing's in the way.
    /// </summary>
    public sealed class Interactor
    {
        readonly Collider[] nearby = new Collider[32];
        readonly RaycastHit[] along = new RaycastHit[16];

        /// <summary>What a left click would use (never a door).</summary>
        public IInteractable Target { get; private set; }
        /// <summary>The door E would open or shut.</summary>
        public IDoor Door { get; private set; }

        public void Update(Transform eye, float reach, float assistDegrees)
        {
            Target = null;
            Door = null;
            float best = float.MaxValue, bestDoor = float.MaxValue;
            int mask = ~(Layers.Mask(Layers.PlayerBody) | Layers.Mask(Layers.Effects));
            // Right under the crosshair: the nearest door, and the nearest other thing, the line of sight touches
            // before anything solid. (Open doors are triggers, so they count too.)
            IInteractable onTarget = null;
            IDoor onDoor = null;
            int hits = Physics.RaycastNonAlloc(eye.position, eye.forward, along, reach, mask, QueryTriggerInteraction.Collide);
            System.Array.Sort(along, 0, hits, HitOrder);
            for (int i = 0; i < hits; i++)
            {
                var c = along[i].collider;
                var it = c.GetComponentInParent<IInteractable>();
                if (it != null && it.CanInteract)
                {
                    if (it is IDoor d) { if (onDoor == null) onDoor = d; }
                    else if (onTarget == null) onTarget = it;
                }
                else if (!c.isTrigger) break; // (a wall, a trunk: nothing behind it)
                if (onDoor != null && onTarget != null) break;
            }
            Door = onDoor;
            Target = onTarget;
            if (onDoor != null) bestDoor = -1f;
            if (onTarget != null) best = -1f;

            int count = Physics.OverlapSphereNonAlloc(eye.position, reach + 0.5f, nearby, mask, QueryTriggerInteraction.Collide);
            for (int i = 0; i < count; i++)
            {
                var candidate = nearby[i].GetComponentInParent<IInteractable>();
                if (candidate == null || !candidate.CanInteract) continue;
                Vector3 to = candidate.FocusPoint - eye.position;
                float dist = to.magnitude;
                if (dist > reach || dist < 0.001f) continue;
                float angle = Vector3.Angle(eye.forward, to);
                // Small things up close get extra leeway.
                float leeway = assistDegrees + Mathf.Lerp(6f, 0f, dist / reach);
                // A round target (a pickup's) counts all of it, not just its middle.
                if (nearby[i] is SphereCollider round && round.isTrigger)
                    leeway += Mathf.Atan2(round.radius * round.transform.lossyScale.x, dist) * Mathf.Rad2Deg;
                bool door = candidate is IDoor;
                if (angle > leeway || angle >= (door ? bestDoor : best)) continue;
                if (Blocked(eye.position, candidate, to, dist, mask)) continue;
                if (door)
                {
                    bestDoor = angle;
                    Door = (IDoor)candidate;
                }
                else
                {
                    best = angle;
                    Target = candidate;
                }
            }
        }

        static readonly System.Collections.Generic.IComparer<RaycastHit> HitOrder = System.Collections.Generic.Comparer<RaycastHit>.Create((a, b) => a.distance.CompareTo(b.distance));

        public bool TryInteract()
        {
            if (Target == null || !Target.CanInteract) return false;
            Target.Interact();
            return true;
        }

        public bool TryUseDoor()
        {
            if (Door == null || !Door.CanInteract) return false;
            Door.Interact();
            return true;
        }

        // Something else between the eye and it (hitting part of itself is fine).
        static bool Blocked(Vector3 from, IInteractable target, Vector3 to, float dist, int mask)
        {
            if (!Physics.Raycast(from, to / dist, out var hit, dist - 0.05f, mask, QueryTriggerInteraction.Ignore)) return false;
            return hit.collider.GetComponentInParent<IInteractable>() != target;
        }
    }
}
