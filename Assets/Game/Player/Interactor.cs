using Game.Core;
using UnityEngine;

namespace Game.Player
{
    /// <summary>
    /// What the crosshair is on (section 6.2): the interactable nearest the line of sight within reach, with a
    /// little aim assist, as long as nothing's in the way.
    /// </summary>
    public sealed class Interactor
    {
        readonly Collider[] nearby = new Collider[32];

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
