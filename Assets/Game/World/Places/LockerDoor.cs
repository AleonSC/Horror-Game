using Game.Audio;
using Game.Core;
using UnityEngine;

namespace Game.World
{
    /// <summary>
    /// A locker you open and shut with a click: its door swings about its hinge (the door's pivot, its Y). What's
    /// inside is lying in it (pickups, on its shelf and floor), to take one by one once it's open.
    /// </summary>
    public sealed class LockerDoor : MonoBehaviour, IInteractable
    {
        public Transform door;
        public float openAngle = -105f;
        public float swingTime = 0.5f;

        Quaternion shut;
        bool open;
        float t;

        public string Prompt => open ? "Close the locker" : "Open the locker";
        public bool CanInteract => true;
        public Vector3 FocusPoint => door != null ? door.position + door.right * 0.45f + Vector3.up * 0.95f : transform.position + Vector3.up;

        void Start()
        {
            if (door != null) shut = door.localRotation;
        }

        public void Interact()
        {
            open = !open;
            if (Services.TryGet<SoundPlayer>(out var sounds))
                sounds.Play(open ? Sound.DoorOpen : Sound.DoorShut, transform.position + Vector3.up, MixerNames.World, 0.5f, open ? 1.5f : 1.7f, 10f);
        }

        void Update()
        {
            if (door == null) return;
            float target = open ? 1f : 0f;
            if (Mathf.Approximately(t, target)) return;
            t = Mathf.MoveTowards(t, target, Time.deltaTime / Mathf.Max(0.05f, swingTime));
            float e = t * t * (3f - 2f * t);
            door.localRotation = shut * Quaternion.Euler(0f, openAngle * e, 0f);
        }
    }
}
