using Game.Audio;
using Game.Core;
using UnityEngine;

namespace Game.World
{
    /// <summary>
    /// A roller blind in a window (the watchtower's lookout room): click it and it unrolls down over the glass, or
    /// rolls back up. This object sits at the roller, its +Z into the room; the blind hangs down from it.
    /// </summary>
    public sealed class Blind : MonoBehaviour, IInteractable
    {
        [Tooltip("The cloth (scaled to the length let down; its pivot at its middle).")]
        public Transform cloth;
        [Tooltip("The bar along its bottom edge, and the pull cord's ring hanging from it.")]
        public Transform bar;
        [Tooltip("How far it comes down when lowered (metres).")]
        public float drop = 1.3f;
        public float width = 1.5f;
        [Tooltip("Seconds to roll up or down.")]
        public float rollTime = 0.9f;

        const float RolledUp = 0.05f;
        bool down;
        float t;

        public string Prompt => down ? "Raise the blind" : "Lower the blind";
        public bool CanInteract => true;
        public Vector3 FocusPoint => bar != null ? bar.position : transform.position;

        void Start() => Shape();

        public void Interact()
        {
            down = !down;
            if (Services.TryGet<SoundPlayer>(out var sounds))
                sounds.Play(Sound.Rustle, transform.position + Vector3.down * 0.4f, MixerNames.World, 0.25f, down ? 1.4f : 1.6f, 6f);
        }

        void Update()
        {
            float target = down ? 1f : 0f;
            if (Mathf.Approximately(t, target)) return;
            t = Mathf.MoveTowards(t, target, Time.deltaTime / Mathf.Max(0.05f, rollTime));
            Shape();
        }

        void Shape()
        {
            float e = t * t * (3f - 2f * t);
            float len = Mathf.Lerp(RolledUp, drop, e);
            if (cloth != null)
            {
                cloth.localPosition = new Vector3(0f, -len * 0.5f, 0f);
                cloth.localScale = new Vector3(width, len, 0.006f);
            }
            if (bar != null) bar.localPosition = new Vector3(0f, -len, 0f);
        }
    }
}
