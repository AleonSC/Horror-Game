using System.Collections;
using Game.Audio;
using Game.Core;
using UnityEngine;
using UnityEngine.Rendering.HighDefinition;

namespace Game.World
{
    /// <summary>
    /// The abandoned car. For now (milestone 4) it answers its key fob: two chirps and the lights flashing,
    /// wherever it is, which things nearby can hear. Sitting in it and its headlights come with milestone 5.
    /// </summary>
    public sealed class Car : MonoBehaviour
    {
        [Tooltip("How far off things hear the chirp (metres).")]
        public float heard = 60f;

        Light[] lights;
        System.IDisposable fobSub;

        void Start()
        {
            lights = new Light[2];
            for (int i = 0; i < 2; i++)
            {
                var go = new GameObject(i == 0 ? "Left light" : "Right light");
                go.transform.SetParent(transform, false);
                go.transform.localPosition = new Vector3(i == 0 ? -0.65f : 0.65f, 0.75f, 2.25f);
                go.AddHDLight(LightType.Point);
                lights[i] = go.GetComponent<Light>();
                lights[i].color = new Color(1f, 0.7f, 0.3f);
                lights[i].range = 8f;
                lights[i].intensity = 12f;
                lights[i].shadows = LightShadows.None;
                lights[i].enabled = false;
            }
            if (Services.TryGet<EventBus>(out var events)) fobSub = events.Subscribe<KeyFobPressed>(_ => StartCoroutine(Chirp()));
        }

        void OnDestroy() => fobSub?.Dispose();

        IEnumerator Chirp()
        {
            if (Services.TryGet<SoundPlayer>(out var sounds)) sounds.Play(Sound.Chirp, transform.position + Vector3.up, MixerNames.World, 1f, 1f, heard * 2f);
            if (Services.TryGet<EventBus>(out var events)) events.Publish(new NoiseEmitted(transform.position, heard, "car chirp"));
            for (int blink = 0; blink < 2; blink++)
            {
                foreach (var l in lights) l.enabled = true;
                yield return new WaitForSeconds(0.14f);
                foreach (var l in lights) l.enabled = false;
                yield return new WaitForSeconds(0.11f);
            }
        }
    }
}
