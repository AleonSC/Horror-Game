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
        /// <param name="segments">The logs: the moment any of them touches anything as it falls, they snap apart (null: it falls whole).</param>
        /// <param name="crown">The needles and leaves, to burst into a flurry as it breaks (or lands).</param>
        /// <param name="ignore">Its own stump (touching that doesn't count).</param>
        public void Begin(Vector3 away, float shudder = 0f, List<Segment> segments = null, List<Renderer> crown = null, Collider ignore = null)
        {
            this.segments = segments;
            this.crown = crown;
            this.ignore = ignore;
            StartCoroutine(Fall(away, shudder));
        }

        Collider ignore;
        readonly Collider[] touching = new Collider[8];

        IEnumerator Fall(Vector3 away, float shudder)
        {
            Vector3 axis = Vector3.Cross(Vector3.up, away);
            Quaternion start = transform.rotation;
            // It gives way: the crack of the trunk going and its tearing and groaning as it starts to lean...
            Play(Sound.TreeBreak, 1f, Random.Range(0.9f, 1.05f));
            for (float t = 0f; t < shudder; t += Time.deltaTime)
            {
                transform.rotation = Quaternion.AngleAxis(Mathf.Sin(t * 55f) * 0.8f * (1f - t / shudder) + t / shudder * 2f, axis) * start;
                yield return null;
            }
            // ...then the rush of its crown through the air as it goes over.
            Play(Sound.TreeRush, 0.8f, Random.Range(0.92f, 1.05f));
            var bodies = new List<Rigidbody>();
            bool cut = segments != null && segments.Count > 1;
            const float time = 2.4f;
            float angle = 0f;
            for (float t = 0f; t < time; t += Time.deltaTime)
            {
                float u = t / time;
                float was = angle;
                angle = 86f * u * u;
                transform.rotation = Quaternion.AngleAxis(angle, axis) * start;
                // Cut into logs: the moment one touches anything (the ground, another tree, a rock), they snap
                // apart there and then, each carrying on as it was going.
                if (cut && angle > 6f && Touching())
                {
                    float spin = (angle - was) / Mathf.Max(0.0001f, Time.deltaTime) * Mathf.Deg2Rad; // (how fast it's swinging over)
                    Snap(axis, spin, away, bodies);
                    break;
                }
                yield return null;
            }
            if (bodies.Count == 0)
            {
                // (Whole, or it never touched: down it thuds, with a little bounce.)
                Landed(away, away * 2.5f);
                for (float t = 0f; t < 0.5f; t += Time.deltaTime)
                {
                    transform.rotation = Quaternion.AngleAxis(86f + Mathf.Sin(t / 0.5f * Mathf.PI) * 3f, axis) * start;
                    yield return null;
                }
                if (cut) Snap(axis, 0f, away, bodies);
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

        // Is any log touching something (not its own stump, nor a trigger)?
        bool Touching()
        {
            int mask = Layers.Mask(Layers.Ground) | Layers.Mask(Layers.Trees) | Layers.Mask(Layers.Props);
            foreach (var s in segments)
            {
                if (s.Piece == null) continue;
                Vector3 along = s.Piece.up * Mathf.Max(0f, s.Length * 0.5f - s.Radius);
                int n = Physics.OverlapCapsuleNonAlloc(s.Piece.position - along, s.Piece.position + along, s.Radius * 0.9f, touching, mask, QueryTriggerInteraction.Ignore);
                for (int i = 0; i < n; i++)
                    if (touching[i] != ignore && !touching[i].transform.IsChildOf(transform)) return true;
            }
            return false;
        }

        // The thud and the crown's flurry (once).
        bool landed;
        void Landed(Vector3 away, Vector3 momentum)
        {
            if (landed) return;
            landed = true;
            Play(Sound.TreeImpact, 1f, Random.Range(0.9f, 1.05f));
            if (Services.TryGet<EventBus>(out var events)) events.Publish(new NoiseEmitted(transform.position, 25f, "tree falling"));
            if (crown != null && crown.Count > 0) LeafBurst.Burst(crown, momentum);
        }

        // The logs snap apart: each its own body, going on the way it was swinging (spin: radians a second about
        // the axis through the stump), with a jolt along the trunk away from the others.
        void Snap(Vector3 axis, float spin, Vector3 away, List<Rigidbody> bodies)
        {
            Play(Sound.Crack, 0.5f, 0.7f); // (the logs parting, under the crash)
            Vector3 pivot = transform.position, w = axis.normalized * spin;
            Vector3 tip = pivot;
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
                Vector3 swing = Vector3.Cross(w, s.Piece.position - pivot);
                Vector3 jolt = along * (i - middle) * Random.Range(0.3f, 0.7f) + Random.insideUnitSphere * 0.3f;
                body.AddForce(swing + jolt, ForceMode.VelocityChange);
                body.AddTorque(w + along * Random.Range(-2f, 2f) + Random.insideUnitSphere * 0.4f, ForceMode.VelocityChange);
                bodies.Add(body);
                if ((s.Piece.position - pivot).sqrMagnitude > (tip - pivot).sqrMagnitude) tip = s.Piece.position;
            }
            // The crown goes with the top: its flurry carries on the way the top was moving.
            Landed(away, Vector3.Cross(w, tip - pivot) * 0.5f + away);
        }

        void Play(Sound sound, float volume, float pitch)
        {
            if (Services.TryGet<SoundPlayer>(out var sounds)) sounds.Play(sound, transform.position + Vector3.up * 2f, MixerNames.Outside, volume, pitch, 70f);
        }
    }
}
