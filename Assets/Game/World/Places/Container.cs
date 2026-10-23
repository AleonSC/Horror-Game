using System.Collections.Generic;
using Game.Audio;
using Game.Core;
using Game.Player;
using UnityEngine;

namespace Game.World
{
    /// <summary>Something to search with a left click (the car's glove box and trunk, a backpack): you take what's inside.</summary>
    public sealed class Container : MonoBehaviour, IInteractable
    {
        public string label = "Search";
        readonly List<(ItemKind kind, int count)> contents = new List<(ItemKind, int)>();

        public string Prompt => contents.Count > 0 ? label : "Empty";
        public bool CanInteract => contents.Count > 0;
        public Vector3 FocusPoint => transform.position;

        public void Put(ItemKind kind, int count) => contents.Add((kind, count));

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
            for (int i = 0; i < contents.Count; i++)
            {
                var (kind, count) = contents[i];
                if (!player.Hotbar.Give(kind, count)) return; // (no room: the rest stays)
                contents.RemoveAt(i--);
            }
            if (Services.TryGet<SoundPlayer>(out var sounds)) sounds.Play(Sound.DoorShut, transform.position, MixerNames.World, 0.3f, 1.6f, 8f);
        }
    }
}
