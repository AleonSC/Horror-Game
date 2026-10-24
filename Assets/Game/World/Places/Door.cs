using Game.Audio;
using Game.Core;
using UnityEngine;

namespace Game.World
{
    /// <summary>A hinged door (shack, cabin): E opens and shuts it. It swings about this object's pivot (the hinge).</summary>
    public sealed class Door : MonoBehaviour, IDoor
    {
        [Tooltip("How far it swings open (degrees).")]
        public float openAngle = 100f;
        [Tooltip("Seconds to swing.")]
        public float swingTime = 0.7f;
        [Tooltip("Locked: it won't open (the lone door, until all ten lamps are lit).")]
        public bool locked;

        Quaternion shut;
        float open;     // 0 shut, 1 open
        bool opening;
        Collider handle;

        public bool IsOpen => opening;
        public string Prompt => locked ? "Locked" : opening ? "Close" : "Open";
        public bool CanInteract => true;
        public Vector3 FocusPoint => handle != null ? handle.bounds.center : transform.position + Vector3.up;

        void Awake()
        {
            shut = transform.localRotation;
            handle = GetComponentInChildren<Collider>();
        }

        public void Interact()
        {
            if (locked)
            {
                Play(Sound.Click, 0.6f, 0.5f);
                return;
            }
            opening = !opening;
            Play(opening ? Sound.DoorOpen : Sound.DoorShut, 0.7f, Random.Range(0.9f, 1.1f));
            // Sometimes, shutting it is answered: three slow knocks, out in the trees.
            if (!opening && GetComponent<LoneDoor>() == null)
            {
                var life = Services.TryGet<GameSetup>(out var setup) ? setup.Get<ForestLifeConfig>() : null;
                if (life != null && Random.value < life.settings.knockChance) StartCoroutine(Knocks());
            }
        }

        System.Collections.IEnumerator Knocks()
        {
            yield return new WaitForSeconds(Random.Range(2.5f, 5f));
            Vector3 from = transform.position + Quaternion.Euler(0f, Random.Range(0f, 360f), 0f) * Vector3.forward * Random.Range(14f, 24f) + Vector3.up * 1.2f;
            for (int i = 0; i < 3; i++)
            {
                if (Services.TryGet<SoundPlayer>(out var sounds)) sounds.Play(Sound.DoorShut, from, MixerNames.Outside, 0.75f, 0.62f, 45f);
                yield return new WaitForSeconds(1.1f);
            }
            if (Services.TryGet<EventBus>(out var events)) events.Publish(new NoiseEmitted(from, 0f, "three knocks"));
        }

        void Update()
        {
            float target = opening ? 1f : 0f;
            if (Mathf.Approximately(open, target)) return;
            open = Mathf.MoveTowards(open, target, Time.deltaTime / Mathf.Max(0.05f, swingTime));
            transform.localRotation = shut * Quaternion.Euler(0f, -openAngle * Mathf.SmoothStep(0f, 1f, open), 0f);
        }

        void Play(Sound sound, float volume, float pitch)
        {
            if (Services.TryGet<SoundPlayer>(out var sounds)) sounds.Play(sound, FocusPoint, MixerNames.World, volume, pitch, 18f);
        }
    }
}
