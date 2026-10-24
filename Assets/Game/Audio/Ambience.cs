using System.Collections.Generic;
using Game.Core;
using UnityEngine;

namespace Game.Audio
{
    /// <summary>
    /// The forest's sound (section 5.4): a low drone bed, wind that rises and falls with the gusts where you are,
    /// the canopy hissing and rustling with it, a tension layer that swells when something's near, random distant
    /// noises (twig snaps, moans, rustles), owls and crows calling far off, and trees creaking in strong gusts. All of it on the Outside group, so shelters and the dark room hush it.
    /// </summary>
    public sealed class Ambience : MonoBehaviour
    {
        AudioSource wind, drone, tension, leaves;
        float tensionTarget;
        float nextDistant, nextCreak, nextCall;
        readonly List<System.IDisposable> subs = new List<System.IDisposable>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void AttachToGame()
        {
            if (Services.TryGet<GameRoot>(out var root) && root.GetComponent<Ambience>() == null)
                root.gameObject.AddComponent<Ambience>();
        }

        void Start()
        {
            if (!Services.TryGet<SoundPlayer>(out var sounds)) return;
            wind = Loop(sounds, Sound.WindLoop, MixerNames.Outside);
            drone = Loop(sounds, Sound.Drone, MixerNames.Outside);
            tension = Loop(sounds, Sound.TensionBed, MixerNames.Entities);
            leaves = Loop(sounds, Sound.LeavesLoop, MixerNames.Outside);
            nextDistant = Time.time + Random.Range(6f, 14f);
            nextCall = Time.time + Random.Range(10f, 25f);
            if (Services.TryGet<EventBus>(out var events))
                subs.Add(events.Subscribe<MusicMoodChanged>(e => tensionTarget = e.Mood == MusicMood.Tension ? 0.35f : e.Mood == MusicMood.Danger ? 0.6f : e.Mood == MusicMood.Watcher ? 0.45f : 0f));
        }

        void OnDestroy()
        {
            foreach (var s in subs) s.Dispose();
        }

        AudioSource Loop(SoundPlayer sounds, Sound sound, string group)
        {
            var go = new GameObject(sound.ToString());
            go.transform.SetParent(transform, false);
            var src = go.AddComponent<AudioSource>();
            src.clip = sounds.Clip(sound);
            src.loop = true;
            src.spatialBlend = 0f;
            src.volume = 0f;
            src.outputAudioMixerGroup = sounds.Group(group);
            if (src.clip != null)
            {
                src.time = Random.Range(0f, src.clip.length * 0.9f);
                src.Play();
            }
            return src;
        }

        void Update()
        {
            if (wind == null) return;
            var cam = Camera.main;
            if (cam == null) return;
            Vector3 at = cam.transform.position;
            float dt = Time.deltaTime;

            // The wind's sound follows the gusts where you stand.
            float gust = Services.TryGet<Wind>(out var w) ? w.Strength(at.x, at.z, Time.time) : 0.3f;
            wind.volume = Mathf.Lerp(wind.volume, Mathf.Clamp01(0.2f + gust * 0.6f), 1f - Mathf.Exp(-2f * dt));
            wind.pitch = Mathf.Lerp(0.85f, 1.15f, Mathf.Clamp01(gust));
            // The canopy hisses and rustles with it, louder in the gusts.
            if (leaves != null) leaves.volume = Mathf.Lerp(leaves.volume, Mathf.Clamp01(0.1f + gust * 0.7f), 1f - Mathf.Exp(-3f * dt));
            drone.volume = Mathf.MoveTowards(drone.volume, 0.22f, dt * 0.1f);
            tension.volume = Mathf.MoveTowards(tension.volume, tensionTarget, dt * (tensionTarget > tension.volume ? 0.4f : 0.12f));

            if (!Services.TryGet<SoundPlayer>(out var sounds)) return;
            // Every so often, something out in the dark.
            if (Time.time >= nextDistant)
            {
                nextDistant = Time.time + Random.Range(7f, 22f);
                Vector3 from = at + Quaternion.Euler(0f, Random.Range(0f, 360f), 0f) * Vector3.forward * Random.Range(18f, 55f);
                float roll = Random.value;
                if (roll < 0.5f) sounds.Play(Sound.Twig, from, MixerNames.Outside, Random.Range(0.4f, 0.8f), Random.Range(0.8f, 1.2f), 70f);
                else if (roll < 0.8f) sounds.Play(Sound.Rustle, from, MixerNames.Outside, 0.6f, Random.Range(0.7f, 0.95f), 50f);
                else sounds.Play(Sound.Moan, from, MixerNames.Outside, 0.35f, Random.Range(0.4f, 0.6f), 90f);
            }
            // Now and then an animal far off in the dark: an owl's call, a barn owl's screech, a crow.
            if (Time.time >= nextCall)
            {
                nextCall = Time.time + Random.Range(14f, 40f);
                Vector3 from = at + Quaternion.Euler(0f, Random.Range(0f, 360f), 0f) * Vector3.forward * Random.Range(45f, 90f) + Vector3.up * 8f;
                float roll = Random.value;
                if (roll < 0.55f) sounds.Play(Sound.Hoot, from, MixerNames.Outside, 0.35f, Random.Range(0.9f, 1.05f), 120f);
                else if (roll < 0.75f) sounds.Play(Sound.Screech, from, MixerNames.Outside, 0.25f, Random.Range(0.9f, 1.05f), 120f);
                else sounds.Play(Sound.Caw, from, MixerNames.Outside, 0.3f, Random.Range(0.85f, 1.05f), 120f);
            }
            // Strong gusts make the trees groan.
            if (gust > 0.75f && Time.time >= nextCreak)
            {
                nextCreak = Time.time + Random.Range(2.5f, 7f);
                Vector3 from = at + Quaternion.Euler(0f, Random.Range(0f, 360f), 0f) * Vector3.forward * Random.Range(6f, 25f) + Vector3.up * 6f;
                sounds.Play(Sound.Creak, from, MixerNames.Outside, Random.Range(0.35f, 0.7f), Random.Range(0.7f, 1.2f), 40f);
            }
        }
    }
}
