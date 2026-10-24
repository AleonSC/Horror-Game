using System.Collections.Generic;
using Game.Audio;
using Game.Core;
using UnityEngine;

namespace Game.Player
{
    /// <summary>
    /// A radio set down, playing (section 6.3): an old song through a small speaker, crackling. It carries
    /// further for something blind than for you: the blind one comes for it from anywhere and smashes it. Click
    /// it to pick it up again (and stop it).
    /// </summary>
    public sealed class PlacedRadio : MonoBehaviour, IInteractable
    {
        static readonly List<PlacedRadio> playing = new List<PlacedRadio>();
        /// <summary>Every radio playing now.</summary>
        public static IReadOnlyList<PlacedRadio> Playing => playing;

        ItemSettings s;
        AudioSource song, hiss;
        float callIn;
        System.IDisposable resetSub;

        public string Prompt => "Pick up radio";
        public bool CanInteract => true;
        public Vector3 FocusPoint => transform.position + Vector3.up * 0.08f;
        /// <summary>Within this, the blind one smashes it.</summary>
        public float SmashDistance => s != null ? s.radioSmashDistance : 3f;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetOnPlay() => playing.Clear();

        public static PlacedRadio SetDown(PlayerController player, ItemSettings s, GameObject art)
        {
            Vector3 at = player.transform.position + player.transform.forward * 0.9f;
            if (Physics.Raycast(at + Vector3.up * 2f, Vector3.down, out var hit, 10f, Layers.Mask(Layers.Ground) | Layers.Mask(Layers.Props), QueryTriggerInteraction.Ignore)) at = hit.point;
            var go = new GameObject("Radio (playing)") { layer = Layers.Props };
            go.transform.SetPositionAndRotation(at, Quaternion.Euler(0f, player.transform.eulerAngles.y + 180f, 0f)); // (facing you)
            ItemModels.Make(ItemKind.Radio, art, go.transform, false);
            var col = go.AddComponent<BoxCollider>();
            col.size = new Vector3(0.3f, 0.25f, 0.2f);
            col.center = Vector3.up * 0.1f;
            col.isTrigger = true;
            var r = go.AddComponent<PlacedRadio>();
            r.s = s;
            r.Play();
            return r;
        }

        void Play()
        {
            playing.Add(this);
            if (Services.TryGet<SoundPlayer>(out var sounds))
            {
                song = Loop(sounds, Sound.RadioSong, s.radioVolume);
                hiss = Loop(sounds, Sound.Static, s.radioVolume * 0.25f);
            }
            if (Services.TryGet<EventBus>(out var events)) resetSub = events.Subscribe<RunStarted>(_ => Destroy(gameObject));
        }

        AudioSource Loop(SoundPlayer sounds, Sound sound, float volume)
        {
            var src = gameObject.AddComponent<AudioSource>();
            src.clip = sounds.Clip(sound);
            src.loop = true;
            src.spatialBlend = 1f;
            src.rolloffMode = AudioRolloffMode.Linear;
            src.maxDistance = s.radioRange;
            src.minDistance = 1.5f;
            src.volume = volume;
            src.outputAudioMixerGroup = sounds.Group(MixerNames.World);
            if (src.clip != null) src.Play();
            return src;
        }

        void OnDestroy()
        {
            playing.Remove(this);
            resetSub?.Dispose();
        }

        void Update()
        {
            if (song != null) song.maxDistance = s.radioRange;
            // It calls, now and then, to whatever can hear that far: anywhere.
            callIn -= Time.deltaTime;
            if (callIn > 0f) return;
            callIn = 2f;
            if (Services.TryGet<EventBus>(out var events)) events.Publish(new NoiseEmitted(transform.position, 100000f, "radio"));
        }

        public void Interact()
        {
            if (!Services.TryGet<PlayerController>(out var player) || player.Hotbar == null) return;
            if (!player.Hotbar.Give(ItemKind.Radio)) return; // (no room)
            Destroy(gameObject);
        }

        /// <summary>Smashed (by the blind one): a crunch, a last squeal of static, and it's in pieces.</summary>
        public void Smash()
        {
            if (Services.TryGet<SoundPlayer>(out var sounds))
            {
                sounds.Play(Sound.Crack, transform.position, MixerNames.World, 1f, 0.6f, 40f);
                sounds.Play(Sound.Clunk, transform.position, MixerNames.World, 0.9f, 0.7f, 40f);
                sounds.Play(Sound.Static, transform.position, MixerNames.World, 0.6f, 1.6f, 30f);
            }
            // The bits stay where it was.
            foreach (var r in GetComponentsInChildren<Renderer>())
            {
                var t = r.transform;
                t.SetParent(transform.parent, true);
                t.position = new Vector3(t.position.x + Random.Range(-0.3f, 0.3f), transform.position.y + 0.01f, t.position.z + Random.Range(-0.3f, 0.3f));
                t.rotation = Quaternion.Euler(Random.Range(-90f, 90f), Random.Range(0f, 360f), Random.Range(-90f, 90f));
                Destroy(t.gameObject, 120f);
            }
            Destroy(gameObject);
        }
    }
}
