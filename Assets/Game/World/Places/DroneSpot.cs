using Game.Core;
using Game.Player;
using Game.World.Build;
using Game.World.Generation;
using UnityEngine;

namespace Game.World
{
    /// <summary>
    /// Where the drone might be (section 3.7): the cabin's table, the watchtower's deck, the car's back seat. There's
    /// one drone, and it's at whichever of these you reach first; the others stay empty.
    /// </summary>
    public sealed class DroneSpot : MonoBehaviour
    {
        const float Reach = 14f;

        static WorldPlan placedIn; // (the world whose drone has been placed: one per world)
        Container container;
        float yaw;

        public static void Add(Transform at, Vector3 localSpot, Container into = null)
        {
            var go = new GameObject("Drone spot");
            go.transform.SetParent(at, false);
            go.transform.localPosition = localSpot;
            var spot = go.AddComponent<DroneSpot>();
            spot.container = into;
            spot.yaw = at.eulerAngles.y + 30f;
        }

        void Update()
        {
            var plan = Services.TryGet<WorldBuilder>(out var w) ? w.Plan : null;
            if (plan != null && placedIn == plan) { enabled = false; return; } // (found somewhere else)
            if (!Services.TryGet<PlayerController>(out var player)) return;
            if ((player.transform.position - transform.position).sqrMagnitude > Reach * Reach) return;
            placedIn = plan;
            if (container != null) container.Put(ItemKind.Drone, 1);
            else Pickup.Create(ItemKind.Drone, 1, transform.position, yaw, transform.parent);
            enabled = false;
        }
    }
}
