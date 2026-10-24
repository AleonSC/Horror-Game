using Game.Audio;
using Game.Core;
using UnityEngine;

namespace Game.World
{
    /// <summary>
    /// A hinged lid you click open and shut (the car's trunk and glove box): it swings about this object's pivot,
    /// round its own X axis. What's behind it (a container to search) can only be reached while it's open.
    /// </summary>
    public sealed class Lid : MonoBehaviour, IInteractable
    {
        [Tooltip("What it's called in the prompt (\"trunk\").")]
        public string what = "lid";
        [Tooltip("How far it swings open (degrees about its X axis; negative swings the other way).")]
        public float openAngle = -70f;
        [Tooltip("Seconds to swing.")]
        public float swingTime = 0.6f;
        [Tooltip("Colliders only reachable while it's open (the container inside).")]
        public Collider[] reveals = new Collider[0];

        Quaternion shut;
        float open;
        bool opening;

        public bool IsOpen => opening;
        public string Prompt => opening ? $"Close the {what}" : $"Open the {what}";
        public bool CanInteract => true;
        public Vector3 FocusPoint => GetComponentInChildren<Collider>() is Collider c ? c.bounds.center : transform.position;

        void Awake() => shut = transform.localRotation;

        void Start() => Reveal(false);

        public void Interact()
        {
            opening = !opening;
            if (Services.TryGet<SoundPlayer>(out var sounds))
                sounds.Play(opening ? Sound.DoorOpen : Sound.DoorShut, FocusPoint, MixerNames.World, 0.55f, opening ? 1.25f : 1.35f, 15f);
            if (opening) Reveal(true);
        }

        void Reveal(bool on)
        {
            foreach (var c in reveals) if (c != null) c.enabled = on;
        }

        void Update()
        {
            float target = opening ? 1f : 0f;
            if (Mathf.Approximately(open, target)) return;
            open = Mathf.MoveTowards(open, target, Time.deltaTime / Mathf.Max(0.05f, swingTime));
            transform.localRotation = shut * Quaternion.Euler(openAngle * Mathf.SmoothStep(0f, 1f, open), 0f, 0f);
            if (open <= 0f) Reveal(false);
        }
    }
}
