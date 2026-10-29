using Game.Audio;
using Game.Core;
using Game.Player;
using UnityEngine;

namespace Game.World
{
    /// <summary>
    /// The lone door standing in the forest (section 6.5): locked until all ten lamps are lit. Open, it's a window
    /// into a vast black pillared room far below the world (and the room's doorway looks back out); step through
    /// and you're there. The forest's sound fades to an echoing silence, and nothing can follow. Step back through
    /// the room's doorway to return.
    /// </summary>
    [RequireComponent(typeof(Door))]
    public sealed class LoneDoor : MonoBehaviour
    {
        [Tooltip("The door's frame (it doesn't swing). Empty: this object's parent.")]
        public Transform frame;
        [Tooltip("The opening, in the frame's space.")]
        public Vector2 opening = new Vector2(1.16f, 2.2f);
        public Vector3 openingCentre = new Vector3(0f, 1.1f, 0f);
        /// <summary>The twin doorway in the dark room (set when the world is built).</summary>
        public Transform room;

        Door door;
        System.IDisposable sub;
        float lastSideForest, lastSideRoom;

        public bool Unlocked => door != null && !door.locked;

        void Start()
        {
            door = GetComponent<Door>();
            if (frame == null) frame = transform.parent != null ? transform.parent : transform;
            door.locked = !(Services.TryGet<GameRun>(out var run) && run.LampsLit >= GameRun.LampCount);
            if (Services.TryGet<EventBus>(out var events)) sub = events.Subscribe<DoorUnlocked>(_ => door.locked = false);
            if (room != null)
            {
                Portal.Create(frame, room, opening, openingCentre, () => (door.IsOpen && !door.locked) || door.ajar > 0.001f); // (through the crack, ajar: a glimpse of the room)
                Portal.Create(room, frame, opening, openingCentre, null);
            }
        }

        void OnDestroy()
        {
            sub?.Dispose();
            if (PlayerPlace.InDarkRoom) Leave(false);
        }

        void LateUpdate()
        {
            if (room == null || !Services.TryGet<PlayerController>(out var player) || player.Held) return;
            Vector3 p = player.Head.position;
            if (!PlayerPlace.InDarkRoom)
            {
                if (door.IsOpen && !door.locked && Crossed(frame, p, ref lastSideForest)) Go(player, frame, room, true);
                else lastSideForest = Side(frame, p);
            }
            else if (Crossed(room, p, ref lastSideRoom)) Go(player, room, frame, false);
        }

        float Side(Transform t, Vector3 p) => Mathf.Sign(t.InverseTransformPoint(p).z);

        // Through the opening since last frame?
        bool Crossed(Transform t, Vector3 p, ref float lastSide)
        {
            Vector3 local = t.InverseTransformPoint(p);
            float side = Mathf.Sign(local.z);
            bool inside = Mathf.Abs(local.x - openingCentre.x) < opening.x * 0.5f && local.y > 0f && local.y < openingCentre.y + opening.y * 0.5f + 0.3f;
            bool crossed = lastSide != 0f && side != lastSide && inside;
            lastSide = side;
            return crossed;
        }

        // Carry the player from one doorway to the same place relative to the other.
        void Go(PlayerController player, Transform from, Transform to, bool intoRoom)
        {
            Vector3 feet = to.TransformPoint(from.InverseTransformPoint(player.transform.position));
            Quaternion facing = to.rotation * Quaternion.Inverse(from.rotation) * player.transform.rotation;
            player.Teleport(feet, facing, keepPitch: true);
            lastSideForest = lastSideRoom = 0f;
            if (intoRoom) Enter();
            else Leave(true);
        }

        void Enter()
        {
            PlayerPlace.InDarkRoom = true;
            if (Services.TryGet<EventBus>(out var events)) events.Publish(new EnteredDarkRoom());
            if (Services.TryGet<SoundPlayer>(out var sounds)) sounds.Play2D(Sound.DoorShut, MixerNames.Player, 0.6f, 0.6f);
        }

        void Leave(bool publish)
        {
            PlayerPlace.InDarkRoom = false;
            if (publish && Services.TryGet<EventBus>(out var events)) events.Publish(new ExitedDarkRoom());
        }
    }
}
