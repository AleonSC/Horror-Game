using Game.Core;
using UnityEngine;

namespace Game.World
{
    /// <summary>
    /// The inside of a shack, the cabin or a tent (section 6.5): stepping in brings the muffled Sheltered sound,
    /// and round it is a refuge, a small safe circle. The inside is a box in this object's space.
    /// </summary>
    public sealed class Shelter : MonoBehaviour
    {
        static int nextId = 1;

        public string shelterName = "Shelter";
        [Tooltip("The inside, in this object's space.")]
        public Bounds inside = new Bounds(new Vector3(0f, 1f, 0f), new Vector3(2f, 2f, 2f));
        [Tooltip("Radius of the refuge round it (metres).")]
        public float refugeRadius = 5f;

        int id;
        bool playerInside;
        SafeZone refuge;

        public bool PlayerInside => playerInside;

        void Awake() => id = nextId++;

        void Start()
        {
            refuge = new SafeZone($"Refuge: {shelterName}", transform.TransformPoint(inside.center), refugeRadius);
            if (Services.TryGet<SafeZones>(out var zones)) zones.Add(refuge);
        }

        void OnDestroy()
        {
            if (Services.TryGet<SafeZones>(out var zones)) zones.Remove(refuge);
            if (playerInside) Publish(false);
        }

        void Update()
        {
            refuge.Center = transform.TransformPoint(inside.center); // (the world wraps: it moves)
            if (!Services.TryGet<Game.Player.PlayerController>(out var player)) return;
            Vector3 eye = player.Head != null ? player.Head.position : player.transform.position + Vector3.up;
            bool now = inside.Contains(transform.InverseTransformPoint(eye));
            if (now != playerInside) Publish(now);
        }

        void Publish(bool entered)
        {
            playerInside = entered;
            if (!Services.TryGet<EventBus>(out var events)) return;
            if (entered) events.Publish(new EnteredShelter(id, shelterName));
            else events.Publish(new ExitedShelter(id, shelterName));
        }
    }
}
