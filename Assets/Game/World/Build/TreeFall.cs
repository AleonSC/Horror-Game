using System.Collections;
using System.Collections.Generic;
using Game.Audio;
using Game.Core;
using UnityEngine;

namespace Game.World.Build
{
    /// <summary>
    /// The toppling: it cracks and shudders, goes over slow then faster and faster, thuds down and bounces. The
    /// crown bursts into a flurry of needles; a few seconds on, the logs it was cut into come apart and roll. They
    /// lie a while, then sink away when nobody's looking.
    /// </summary>
    public sealed class TreeFall : MonoBehaviour
    {
        /// <summary>One log of the trunk: its piece (centred), how thick and long.</summary>
        public struct Segment
        {
            public Transform Piece;
            public float Radius, Length;
        }

        List<Segment> segments;
        List<Renderer> crown;

        /// <param name="shudder">Seconds it cracks and shudders before it goes (0: straight over, as when shouldered).</param>
        /// <param name="segments">The logs, to come apart once it's down (null: it lies whole).</param>
        /// <param name="crown">The needles and leaves, to burst as it lands.</param>
        public void Begin(Vector3 away, float shudder = 0f, List<Segment> segments = null, List<Renderer> crown = null)
        {
            this.segments = segments;
            this.crown = crown;
            StartCoroutine(Fall(away, shudder));
        }

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
            // The crown bursts as it hits.
            if (crown != null && crown.Count > 0) LeafBurst.Burst(crown, away * 2.5f);
            // A little bounce as it settles.
            for (float t = 0f; t < 0.5f; t += Time.deltaTime)
            {
                transform.rotation = Quaternion.AngleAxis(86f + Mathf.Sin(t / 0.5f * Mathf.PI) * 3f, axis) * start;
                yield return null;
            }

            var bodies = new List<Rigidbody>();
            if (segments != null && segments.Count > 1)
            {
                yield return new WaitForSeconds(Random.Range(2.5f, 3.5f));
                Play(Sound.Crack, 0.7f, 0.85f);
                BreakApart(away, bodies);
            }

            yield return new WaitForSeconds(25f);
            // It sinks away only when nobody's looking.
            while (Camera.main != null && Vector3.Angle(Camera.main.transform.forward, Middle() - Camera.main.transform.position) < 70f
                   && Vector3.Distance(Camera.main.transform.position, Middle()) < 60f)
                yield return new WaitForSeconds(1f);
            foreach (var b in bodies) if (b != null) b.isKinematic = true;
            Vector3 from = transform.position;
            var pieces = new List<(Transform t, Vector3 at)>();
            foreach (var b in bodies) if (b != null) pieces.Add((b.transform, b.transform.position));
            for (float t = 0f; t < 4f; t += Time.deltaTime)
            {
                Vector3 down = Vector3.down * (t / 4f) * 1.5f;
                if (pieces.Count == 0) transform.position = from + down;
                foreach (var p in pieces) if (p.t != null) p.t.position = p.at + down;
                yield return null;
            }
            Destroy(gameObject);
        }

        // Where the wreck lies (the middle of the logs, wherever they rolled).
        Vector3 Middle()
        {
            if (segments == null || segments.Count == 0) return transform.position;
            Vector3 sum = Vector3.zero;
            int n = 0;
            foreach (var s in segments)
                if (s.Piece != null) { sum += s.Piece.position; n++; }
            return n > 0 ? sum / n : transform.position;
        }

        // The logs come apart: each its own body, nudged off along the trunk from the others and set rolling.
        void BreakApart(Vector3 away, List<Rigidbody> bodies)
        {
            float middle = (segments.Count - 1) * 0.5f;
            for (int i = 0; i < segments.Count; i++)
            {
                var s = segments[i];
                if (s.Piece == null) continue;
                var go = s.Piece.gameObject;
                go.layer = Layers.Props;
                var col = go.AddComponent<CapsuleCollider>();
                col.direction = 1; // (along the log)
                col.radius = s.Radius;
                col.height = Mathf.Max(s.Length * 0.96f, s.Radius * 2f + 0.01f);
                var body = go.AddComponent<Rigidbody>();
                body.mass = Mathf.Max(5f, 500f * s.Radius * s.Radius * s.Length);
                body.maxDepenetrationVelocity = 1.5f; // (one half sunk in the slope eases out, rather than leaping)
                body.interpolation = RigidbodyInterpolation.Interpolate;
                Vector3 along = s.Piece.up;
                Vector3 side = Vector3.Cross(along, Vector3.up).normalized;
                Vector3 push = along * (i - middle) * Random.Range(0.4f, 0.9f) + side * Random.Range(-0.7f, 0.7f) + Vector3.up * Random.Range(0.3f, 1f);
                body.AddForce(push, ForceMode.VelocityChange);
                body.AddTorque(along * Random.Range(-3f, 3f) + Random.insideUnitSphere * 0.4f, ForceMode.VelocityChange);
                bodies.Add(body);
            }
        }

        void Play(Sound sound, float volume, float pitch)
        {
            if (Services.TryGet<SoundPlayer>(out var sounds)) sounds.Play(sound, transform.position + Vector3.up * 2f, MixerNames.Outside, volume, pitch, 70f);
        }
    }
}
