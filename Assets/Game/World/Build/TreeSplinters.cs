using Game.Audio;
using Game.Core;
using UnityEngine;

namespace Game.World.Build
{
    /// <summary>
    /// A tree burst apart (trampled by the blind one): splinters of bark and pale wood from all up its trunk, and
    /// a spray of leaves from its crown if it has one, flung away and up, tumbling, falling, skittering to a stop
    /// on the ground, and shrinking away to nothing in a couple of seconds. A crack and a crash of wood.
    /// </summary>
    public sealed class TreeSplinters : MonoBehaviour
    {
        const int Shards = 46, LeafBits = 34;
        const float Gravity = -14f;

        Transform[] bits;
        Vector3[] vel, spin, size;
        float[] life, ground;
        bool[] isLeaf;
        float age;

        static Mesh quad, cube;

        /// <summary>Burst a tree standing at <paramref name="at"/> (its parts within <paramref name="bounds"/>, around its foot) away along <paramref name="away"/>.</summary>
        public static void Burst(Vector3 at, Bounds bounds, Vector3 away, Material bark, Material fresh, Material leaf)
            => Burst(at, bounds, away, bark, fresh, leaf, Quaternion.identity, Vector3.zero);

        /// <summary>
        /// Burst a tree that's going over: its parts as they'd be standing, turned by <paramref name="lean"/> about
        /// its foot, and still swinging (<paramref name="spin"/>: radians a second about its foot), which the bits
        /// carry on with.
        /// </summary>
        public static void Burst(Vector3 at, Bounds bounds, Vector3 away, Material bark, Material fresh, Material leaf, Quaternion lean, Vector3 spin)
        {
            if (cube == null)
            {
                var c = GameObject.CreatePrimitive(PrimitiveType.Cube);
                cube = c.GetComponent<MeshFilter>().sharedMesh;
                Destroy(c);
                var q = GameObject.CreatePrimitive(PrimitiveType.Quad);
                quad = q.GetComponent<MeshFilter>().sharedMesh;
                Destroy(q);
            }
            var go = new GameObject("Splinters") { layer = Layers.Effects };
            go.transform.position = at;
            go.AddComponent<TreeSplinters>().Build(at, bounds, away, bark, fresh, leaf, lean, spin);
            if (Services.TryGet<SoundPlayer>(out var sounds))
            {
                sounds.Play(Sound.Crack, at + Vector3.up * 1.5f, MixerNames.World, 1f, Random.Range(0.55f, 0.7f), 60f);
                sounds.Play(Sound.Twig, at + Vector3.up * 1f, MixerNames.World, 1f, 0.6f, 40f);
                sounds.Play(Sound.Rustle, at + Vector3.up * 4f, MixerNames.World, leaf != null ? 0.9f : 0.3f, 0.7f, 40f);
            }
            if (Services.TryGet<EventBus>(out var events)) events.Publish(new NoiseEmitted(at, 25f, "tree falling"));
        }

        void Build(Vector3 at, Bounds bounds, Vector3 away, Material bark, Material fresh, Material leaf, Quaternion lean, Vector3 swing)
        {
            int leaves = leaf != null ? LeafBits : 0;
            int n = Shards + leaves;
            bits = new Transform[n];
            vel = new Vector3[n];
            spin = new Vector3[n];
            size = new Vector3[n];
            life = new float[n];
            ground = new float[n];
            isLeaf = new bool[n];
            float top = Mathf.Max(2f, bounds.max.y);
            Vector3 side = Vector3.Cross(Vector3.up, away);
            for (int i = 0; i < n; i++)
            {
                bool l = i >= Shards;
                isLeaf[i] = l;
                var b = new GameObject(l ? "Leaf" : "Splinter") { layer = Layers.Effects }.transform;
                b.SetParent(transform, false);
                b.gameObject.AddComponent<MeshFilter>().sharedMesh = l ? quad : cube;
                var r = b.gameObject.AddComponent<MeshRenderer>();
                r.sharedMaterial = l ? leaf : (Random.value < 0.6f ? bark : fresh);
                r.shadowCastingMode = l ? UnityEngine.Rendering.ShadowCastingMode.Off : UnityEngine.Rendering.ShadowCastingMode.On;
                Vector3 p;
                if (l)
                {
                    // (from all through the crown)
                    p = new Vector3(Random.Range(bounds.min.x, bounds.max.x), Random.Range(Mathf.Lerp(bounds.min.y, top, 0.4f), top), Random.Range(bounds.min.z, bounds.max.z));
                    size[i] = Vector3.one * Random.Range(0.25f, 0.5f);
                }
                else
                {
                    // (from up the trunk, more of them low, where it was hit)
                    float h = Mathf.Pow(Random.value, 1.6f) * top * 0.85f;
                    p = new Vector3(Random.Range(-0.25f, 0.25f), h, Random.Range(-0.25f, 0.25f));
                    float len = Random.Range(0.25f, 1.1f) * (h < 3f ? 1.2f : 0.8f);
                    size[i] = new Vector3(Random.Range(0.025f, 0.08f), len, Random.Range(0.025f, 0.07f));
                }
                float up = p.y / top;
                p = lean * p;
                b.localPosition = p;
                b.localRotation = Random.rotation;
                b.localScale = size[i];
                float force = l ? Random.Range(1.5f, 4f) : Random.Range(4f, 11f) * Mathf.Lerp(1.2f, 0.6f, up);
                vel[i] = (away * force + side * Random.Range(-0.6f, 0.6f) * force + Vector3.up * Random.Range(1f, 5f) + Random.insideUnitSphere * 1.5f)
                         + Vector3.Cross(swing, p) * 0.35f; // (what was swinging over carries on, the top the fastest)
                spin[i] = Random.insideUnitSphere * (l ? 300f : 900f);
                life[i] = l ? Random.Range(1.8f, 3f) : Random.Range(1.2f, 2.4f);
                ground[i] = Physics.Raycast(at + new Vector3(p.x, 0f, p.z) + vel[i].normalized * 2f + Vector3.up * 20f, Vector3.down, out var hit, 60f, Layers.Mask(Layers.Ground), QueryTriggerInteraction.Ignore)
                    ? hit.point.y - at.y : 0f;
                bits[i] = b;
            }
        }

        void Update()
        {
            float dt = Time.deltaTime;
            age += dt;
            bool anyLeft = false;
            for (int i = 0; i < bits.Length; i++)
            {
                var b = bits[i];
                if (b == null) continue;
                if (age >= life[i])
                {
                    Destroy(b.gameObject);
                    bits[i] = null;
                    continue;
                }
                anyLeft = true;
                if (isLeaf[i])
                {
                    // (leaves flutter down, slowed by the air)
                    vel[i] += Vector3.up * Gravity * 0.15f * dt;
                    vel[i] *= Mathf.Exp(-2.5f * dt);
                    vel[i] += new Vector3(Mathf.Sin(age * 5f + i), 0f, Mathf.Cos(age * 4f + i * 2f)) * 2f * dt;
                }
                else
                {
                    vel[i] += Vector3.up * Gravity * dt;
                    vel[i] *= Mathf.Exp(-0.4f * dt);
                }
                Vector3 p = b.localPosition + vel[i] * dt;
                if (p.y < ground[i] + 0.03f)
                {
                    // On the ground: a bounce, then skittering to a stop.
                    p.y = ground[i] + 0.03f;
                    vel[i] = new Vector3(vel[i].x * 0.45f, Mathf.Abs(vel[i].y) * (isLeaf[i] ? 0f : 0.25f), vel[i].z * 0.45f);
                    spin[i] *= 0.5f;
                }
                b.localPosition = p;
                b.localRotation = Quaternion.Normalize(Quaternion.Euler(spin[i] * dt) * b.localRotation); // (kept unit: it drifts)
                // Shrinking away over the last part of its life.
                float fade = Mathf.Clamp01((life[i] - age) / 0.5f);
                b.localScale = size[i] * fade;
            }
            if (!anyLeft) Destroy(gameObject);
        }
    }
}
