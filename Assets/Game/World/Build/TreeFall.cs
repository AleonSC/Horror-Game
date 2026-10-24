using System.Collections;
using Game.Audio;
using Game.Core;
using UnityEngine;

namespace Game.World.Build
{
    /// <summary>The toppling: slow to start, faster and faster, a thud, a bounce, then it lies there and sinks away.</summary>
    public sealed class TreeFall : MonoBehaviour
    {
        public void Begin(Vector3 away) => StartCoroutine(Fall(away));

        IEnumerator Fall(Vector3 away)
        {
            Vector3 axis = Vector3.Cross(Vector3.up, away);
            Quaternion start = transform.rotation;
            Play(Sound.Crack, 1f, 0.5f);
            const float time = 2.4f;
            for (float t = 0f; t < time; t += Time.deltaTime)
            {
                float u = t / time;
                transform.rotation = Quaternion.AngleAxis(86f * u * u, axis) * start;
                yield return null;
            }
            Play(Sound.GiantStep, 0.6f, 1.6f);
            if (Services.TryGet<EventBus>(out var events)) events.Publish(new NoiseEmitted(transform.position, 25f, "tree falling"));
            // A little bounce as it settles.
            for (float t = 0f; t < 0.5f; t += Time.deltaTime)
            {
                transform.rotation = Quaternion.AngleAxis(86f + Mathf.Sin(t / 0.5f * Mathf.PI) * 3f, axis) * start;
                yield return null;
            }
            yield return new WaitForSeconds(25f);
            Vector3 from = transform.position;
            for (float t = 0f; t < 4f; t += Time.deltaTime)
            {
                transform.position = from + Vector3.down * (t / 4f) * 1.5f;
                yield return null;
            }
            Destroy(gameObject);
        }

        void Play(Sound sound, float volume, float pitch)
        {
            if (Services.TryGet<SoundPlayer>(out var sounds)) sounds.Play(sound, transform.position + Vector3.up * 2f, MixerNames.Outside, volume, pitch, 70f);
        }
    }
}
