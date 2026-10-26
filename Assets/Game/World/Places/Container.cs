using System.Collections.Generic;
using Game.Audio;
using Game.Core;
using Game.Player;
using UnityEngine;

namespace Game.World
{
    /// <summary>
    /// Something to search with a left click (the car's glove box and trunk, a backpack, a locker): you take what's
    /// inside. With a door, it swings open the first time (and stays open).
    /// </summary>
    public sealed class Container : MonoBehaviour, IInteractable
    {
        public string label = "Search";
        [Tooltip("A door that swings open about its pivot (its Y) when it's first searched, by this many degrees.")]
        public Transform door;
        public float doorAngle = -105f;
        bool opened;
        readonly List<(ItemKind kind, int count)> contents = new List<(ItemKind, int)>();

        public string Prompt => contents.Count > 0 ? label : "Empty";
        public bool CanInteract => contents.Count > 0;
        public Vector3 FocusPoint => transform.position;

        // The lockers with a drone in them: take one and the rest are emptied of theirs (there's only the one).
        static readonly List<Container> holdingDrone = new List<Container>();

        public void Put(ItemKind kind, int count)
        {
            contents.Add((kind, count));
            if (kind == ItemKind.Drone && !holdingDrone.Contains(this)) holdingDrone.Add(this);
        }

        void OnDestroy() => holdingDrone.Remove(this);

        // A drone taken from here: it's gone from every other.
        void DroneTaken()
        {
            foreach (var other in holdingDrone)
                if (other != null && other != this) other.contents.RemoveAll(c => c.kind == ItemKind.Drone);
            holdingDrone.Clear();
        }

        /// <summary>Make one: a small box to aim at, at this spot (nothing to see: it's part of what it's on).</summary>
        public static Container Create(string label, Vector3 localPosition, Vector3 size, Transform parent)
        {
            var go = new GameObject(label) { layer = Layers.Props };
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            var col = go.AddComponent<BoxCollider>();
            col.size = size;
            col.isTrigger = true;
            var c = go.AddComponent<Container>();
            c.label = label;
            return c;
        }

        public void Interact()
        {
            if (!Services.TryGet<PlayerController>(out var player) || player.Hotbar == null) return;
            if (door != null && !opened)
            {
                opened = true;
                StartCoroutine(Swing());
                if (Services.TryGet<SoundPlayer>(out var creak)) creak.Play(Sound.DoorOpen, transform.position, MixerNames.World, 0.5f, 1.5f, 10f);
            }
            for (int i = 0; i < contents.Count; i++)
            {
                var (kind, count) = contents[i];
                if (!player.Hotbar.Give(kind, count)) return; // (no room: the rest stays)
                contents.RemoveAt(i--);
                if (kind == ItemKind.Drone) DroneTaken();
            }
            if (Services.TryGet<SoundPlayer>(out var sounds)) sounds.Play(Sound.DoorShut, transform.position, MixerNames.World, 0.3f, 1.6f, 8f);
        }

        System.Collections.IEnumerator Swing()
        {
            Quaternion shut = door.localRotation;
            for (float t = 0f; t < 0.6f; t += Time.deltaTime)
            {
                float e = Mathf.SmoothStep(0f, 1f, t / 0.6f);
                door.localRotation = shut * Quaternion.Euler(0f, doorAngle * e, 0f);
                yield return null;
            }
            door.localRotation = shut * Quaternion.Euler(0f, doorAngle, 0f);
        }
    }
}
