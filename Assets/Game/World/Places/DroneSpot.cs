using Game.Core;
using Game.Player;
using Game.World.Build;
using Game.World.Generation;
using UnityEngine;

namespace Game.World
{
    /// <summary>
    /// Where the drone might be (section 3.7): the deck of each watchtower. There's one drone, and it's on the deck
    /// of whichever tower you come to first (within 18 m of it across the ground); the others stay empty.
    /// </summary>
    public sealed class DroneSpot : MonoBehaviour
    {
        const float Reach = 18f;

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
            Vector3 off = player.transform.position - transform.position;
            off.y = 0f; // (across the ground: the watchtower's deck is high over you)
            if (off.sqrMagnitude > Reach * Reach) return;
            placedIn = plan;
            if (container != null) container.Put(ItemKind.Drone, 1);
            else Pickup.Create(ItemKind.Drone, 1, transform.position, yaw, transform.parent);
            enabled = false;
        }
    }
}
