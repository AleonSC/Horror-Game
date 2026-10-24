using System.Collections;
using Game.Audio;
using Game.Core;
using UnityEngine;

namespace Game.World.Build
{
    /// <summary>The toppling: slow to start, faster and faster, a thud, a bounce, then it lies there and sinks away.</summary>
    public sealed class TreeFall : MonoBehaviour
    {
        /// <param name="shudder">Seconds it cracks and shudders before it goes (0: straight over, as when shouldered).</param>
        public void Begin(Vector3 away, float shudder = 0f) => StartCoroutine(Fall(away, shudder));

        IEnumerator Fall(Vector3 away, float shudder)
        {
            Vector3 axis = Vector3.Cross(Vector3.up, away);
            Quaternion start = transform.rotation;
            Play(Sound.Crack, 1f, 0.5f);
            for (float t = 0f; t < shudder; t += Time.deltaTime)
            {
                transform.rotation = Quaternion.AngleAxis(Mathf.Sin(t * 55f) * 0.8f * (1f - t / shudder) + t / shudder * 2f, axis) * start;
                yield return null;
            }
            if (shudder > 0f) Play(Sound.Creak, 1f, 0.6f);
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
            // It sinks away only when nobody's looking.
            while (Camera.main != null && Vector3.Angle(Camera.main.transform.forward, transform.position - Camera.main.transform.position) < 70f
                   && Vector3.Distance(Camera.main.transform.position, transform.position) < 60f)
                yield return new WaitForSeconds(1f);
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
