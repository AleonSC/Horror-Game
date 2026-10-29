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
        [Range(0f, 1f), Tooltip("Left standing this far open (a share of the open angle) while it isn't opened: the lone door, creaking a little wider with each lamp. It holds there, solid, until it's opened.")]
        public float ajar;
        [Tooltip("Seconds it takes to creak to a new ajar, or to swing open by itself.")]
        public float creakTime = 2.5f;

        Quaternion shut;
        float open;     // 0 shut, 1 open
        bool opening;
        float swingFor;
        float rattleLeft, rattleFor, rattleDegrees, shownAngle = -1f;
        Collider handle;
        Collider[] solid;
        CharacterController player;

        public bool IsOpen => opening;
        public string Prompt => locked ? "Locked" : opening ? "Close" : "Open";
        public bool CanInteract => true;
        public Vector3 FocusPoint => handle != null ? handle.bounds.center : transform.position + Vector3.up;

        void Awake()
        {
            shut = transform.localRotation;
            handle = GetComponentInChildren<Collider>();
            solid = GetComponentsInChildren<Collider>();
        }

        public void Interact()
        {
            if (locked)
            {
                Play(Sound.Click, 0.6f, 0.5f);
                return;
            }
            opening = !opening;
            swingFor = swingTime;
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

        /// <summary>Swing open by itself, slowly (the lone door when the last lamp's lit).</summary>
        public void SwingOpen()
        {
            if (opening) return;
            opening = true;
            swingFor = creakTime * 1.6f;
            Play(Sound.Creak, 0.8f, 0.55f);
        }

        /// <summary>Creak to standing this far open (a share of the open angle), slowly; it holds there, solid.</summary>
        public void SetAjar(float share)
        {
            share = Mathf.Clamp01(share);
            if (Mathf.Approximately(share, ajar)) return;
            if (!opening && share > ajar) Play(Sound.Creak, 0.7f, Random.Range(0.5f, 0.62f));
            ajar = share;
            swingFor = creakTime;
        }

        /// <summary>Shake in its frame (while it's shut or ajar): knocking against the stop, dying away.</summary>
        public void Rattle(float seconds, float degrees)
        {
            rattleFor = rattleLeft = Mathf.Max(0.05f, seconds);
            rattleDegrees = degrees;
        }

        void Update()
        {
            // Open, or standing ajar (creaking there slowly).
            float target = opening ? 1f : ajar;
            float speed = swingFor > 0f ? swingFor : swingTime;
            // Only solid when at rest shut or ajar (and not shut on you): swinging or open, you walk through it, so
            // it never shoves you or pins you in the doorway.
            bool block = !opening && open <= ajar + 0.001f && !Overlaps();
            foreach (var c in solid) if (c != null && c.isTrigger == block) c.isTrigger = !block;
            if (!Mathf.Approximately(open, target)) open = Mathf.MoveTowards(open, target, Time.deltaTime / Mathf.Max(0.05f, speed));
            float angle = -openAngle * Mathf.SmoothStep(0f, 1f, open);
            if (rattleLeft > 0f)
            {
                rattleLeft -= Time.deltaTime;
                float k = Mathf.Clamp01(rattleLeft / rattleFor);
                angle += Mathf.Sin(rattleLeft * 70f) * rattleDegrees * k * (opening ? 0f : 1f);
            }
            if (Mathf.Approximately(angle, shownAngle)) return;
            shownAngle = angle;
            transform.localRotation = shut * Quaternion.Euler(0f, angle, 0f);
        }

        bool Overlaps()
        {
            if (player == null)
            {
                if (!Services.TryGet<Game.Player.PlayerController>(out var p)) return false;
                player = p.GetComponent<CharacterController>();
                if (player == null) return false;
            }
            var near = player.bounds;
            near.Expand(0.1f);
            foreach (var c in solid) if (c != null && c.bounds.Intersects(near)) return true;
            return false;
        }

        void Play(Sound sound, float volume, float pitch)
        {
            if (Services.TryGet<SoundPlayer>(out var sounds)) sounds.Play(sound, FocusPoint, MixerNames.World, volume, pitch, 18f);
        }
    }
}
