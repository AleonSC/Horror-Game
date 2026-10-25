using Game.Audio;
using Game.Core;
using Game.Rendering;
using UnityEngine;

namespace Game.World
{
    /// <summary>
    /// The CB radio in a watchtower's lookout room, left on: hiss, a carrier drifting, a voice that never quite
    /// makes words, and now and then three flat beeps. Click it to switch it off (or back on). Its dial glows amber
    /// while it's on. Heard from out on the balcony, through the walls, it's muffled, more so with the door shut.
    /// </summary>
    public sealed class CbRadio : MonoBehaviour, IInteractable
    {
        [Range(0f, 1f)] public float volume = 0.55f;
        [Tooltip("How far it carries (metres).")]
        public float range = 14f;
        [Tooltip("The dial's glass: it glows while it's on.")]
        public Renderer dial;
        [Tooltip("The place it's in (its shelter and door are looked for on it).")]
        public Transform place;

        AudioSource source;
        AudioLowPassFilter filter;
        Material dialGlow;
        Shelter room;
        Door door;
        bool on = true;

        public string Prompt => on ? "Switch the radio off" : "Switch the radio on";
        public bool CanInteract => true;
        public Vector3 FocusPoint => transform.position + Vector3.up * 0.08f;

        void Start()
        {
            var at = place != null ? place : transform.parent;
            if (at != null)
            {
                room = at.GetComponentInChildren<Shelter>();
                door = at.GetComponentInChildren<Door>();
            }
            if (dial != null)
            {
                dialGlow = RuntimeMaterials.LitGlow("Radio dial", new Color(0.3f, 0.2f, 0.08f), Color.black);
                dial.sharedMaterial = dialGlow;
            }
            if (!Services.TryGet<SoundPlayer>(out var sounds)) return;
            source = gameObject.AddComponent<AudioSource>();
            source.clip = sounds.Clip(Sound.RadioCreep);
            source.loop = true;
            source.spatialBlend = 1f;
            source.rolloffMode = AudioRolloffMode.Linear;
            source.minDistance = 1f;
            source.maxDistance = range;
            source.dopplerLevel = 0f;
            source.volume = volume;
            source.outputAudioMixerGroup = sounds.Group(MixerNames.World);
            filter = gameObject.AddComponent<AudioLowPassFilter>();
            filter.cutoffFrequency = 22000f;
            if (source.clip != null)
            {
                source.time = Random.Range(0f, source.clip.length);
                source.Play();
            }
            SetDial();
        }

        void OnDestroy()
        {
            if (dialGlow != null) Destroy(dialGlow);
        }

        public void Interact()
        {
            on = !on;
            if (Services.TryGet<SoundPlayer>(out var sounds))
                sounds.Play(Sound.Click, transform.position, MixerNames.World, 0.6f, on ? 0.8f : 0.6f, 6f);
            if (source != null)
            {
                if (on) source.Play();
                else source.Pause();
            }
            SetDial();
        }

        void SetDial()
        {
            if (dialGlow != null) RuntimeMaterials.SetGlow(dialGlow, on ? new Color(1f, 0.55f, 0.15f) * 0.9f : Color.black);
        }

        void Update()
        {
            if (filter == null) return;
            // In the room with it: clear. Outside: through the walls, or (door open) round the doorway.
            float muffle = room != null && !room.PlayerInside ? (door != null && door.IsOpen ? 0.55f : 1f) : 0f;
            filter.cutoffFrequency = Mathf.Lerp(filter.cutoffFrequency, SoundPlayer.CutoffFor(muffle), Time.deltaTime * 6f);
        }
    }
}
