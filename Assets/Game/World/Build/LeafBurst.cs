using System.Collections.Generic;
using Game.Audio;
using Game.Core;
using Game.Rendering;
using UnityEngine;

namespace Game.World.Build
{
    /// <summary>
    /// A felled tree's crown bursting as it hits the ground: hundreds of needle tufts and leaves flung up and out
    /// from where the crown was, fluttering down, settling on the ground, and fading away. Drawn instanced.
    /// </summary>
    public sealed class LeafBurst : MonoBehaviour
    {
        struct Bit
        {
            public Vector3 Pos, Vel, Spin;
            public Quaternion Rot;
            public float Ground, Life, Size, Seed;
            public bool Down, Brown;
        }

        Bit[] bits;
        float age;
        static Mesh mesh;
        static Material green, brown;
        static Color greenColour;
        readonly Matrix4x4[] batchA = new Matrix4x4[InstancedDraw.MaxPerCall], batchB = new Matrix4x4[InstancedDraw.MaxPerCall];

        /// <summary>
        /// Burst the crown: hide its renderers, and fling its bits out from points on their meshes.
        /// <paramref name="momentum"/> is the way the tree was falling (they carry on that way a little).
        /// </summary>
        public static void Burst(List<Renderer> crown, Vector3 momentum, int count = 600)
        {
            var points = new List<Vector3>();
            Color colour = new Color(0.09f, 0.13f, 0.08f);
            Bounds all = default;
            bool any = false;
            foreach (var r in crown)
            {
                if (r == null) continue;
                if (!any) { all = r.bounds; any = true; }
                else all.Encapsulate(r.bounds);
                var filter = r.GetComponent<MeshFilter>();
                var m = filter != null ? filter.sharedMesh : null;
                if (m != null && m.isReadable)
                {
                    var vs = m.vertices;
                    var toWorld = r.localToWorldMatrix;
                    int step = Mathf.Max(1, vs.Length / 400);
                    for (int i = 0; i < vs.Length; i += step) points.Add(toWorld.MultiplyPoint3x4(vs[i]));
                }
                var mat = r.sharedMaterial;
                if (mat != null && mat.HasProperty("_BaseColor")) colour = mat.GetColor("_BaseColor");
                r.enabled = false;
            }
            if (!any) return;
            if (points.Count == 0)
                for (int i = 0; i < 200; i++)
                    points.Add(all.center + Vector3.Scale(all.extents, new Vector3(Random.Range(-1f, 1f), Random.Range(-1f, 1f), Random.Range(-1f, 1f))));

            var go = new GameObject("Leaf burst");
            var burst = go.AddComponent<LeafBurst>();
            burst.Make(points, all.center, momentum, colour, count);
            if (Services.TryGet<SoundPlayer>(out var sounds))
            {
                sounds.Play(Sound.Rustle, all.center, MixerNames.Outside, 1f, 0.6f, 50f);
                sounds.Play(Sound.Twig, all.center, MixerNames.Outside, 0.8f, 0.8f, 40f);
            }
        }

        void Make(List<Vector3> points, Vector3 centre, Vector3 momentum, Color colour, int count)
        {
            Ensure(colour);
            bits = new Bit[count];
            for (int i = 0; i < count; i++)
            {
                Vector3 at = points[Random.Range(0, points.Count)] + Random.insideUnitSphere * 0.4f;
                Vector3 outward = at - centre;
                outward = outward.sqrMagnitude > 0.01f ? outward.normalized : Random.onUnitSphere;
                float ground = at.y - 3f;
                if (Physics.Raycast(at + Vector3.up * 2f, Vector3.down, out var hit, 40f, Layers.Mask(Layers.Ground), QueryTriggerInteraction.Ignore)) ground = hit.point.y + 0.02f;
                bits[i] = new Bit
                {
                    Pos = at,
                    Vel = outward * Random.Range(1.5f, 5f) + Vector3.up * Random.Range(1.5f, 5.5f) + momentum * Random.Range(0f, 3f),
                    Spin = Random.onUnitSphere * Random.Range(200f, 700f),
                    Rot = Random.rotation,
                    Ground = ground,
                    Life = Random.Range(7f, 12f),
                    Size = Random.Range(0.07f, 0.16f),
                    Seed = Random.value * 100f,
                    Brown = Random.value < 0.3f,
                };
            }
        }

        static void Ensure(Color colour)
        {
            if (mesh == null)
            {
                // A tuft: a slim pointed blade, crossed with another so it reads from any side.
                mesh = new Mesh { name = "Leaf bit" };
                mesh.SetVertices(new List<Vector3>
                {
                    new Vector3(0f, 0f, -0.5f), new Vector3(0.18f, 0f, 0f), new Vector3(0f, 0f, 0.5f), new Vector3(-0.18f, 0f, 0f),
                    new Vector3(0f, 0.15f, 0f), new Vector3(0f, -0.15f, 0f),
                });
                mesh.SetTriangles(new[] { 0, 1, 2, 0, 2, 3, 0, 4, 2, 0, 2, 5 }, 0);
                mesh.RecalculateNormals();
                mesh.RecalculateBounds();
            }
            if (green == null || greenColour != colour)
            {
                greenColour = colour;
                green = RuntimeMaterials.Lit("Leaf bits", colour * 1.15f, 0.2f, true);
                green.enableInstancing = true;
            }
            if (brown == null)
            {
                brown = RuntimeMaterials.Lit("Leaf bits (dry)", new Color(0.16f, 0.12f, 0.06f), 0.15f, true);
                brown.enableInstancing = true;
            }
        }

        void Update()
        {
            float dt = Time.deltaTime;
            age += dt;
            int nA = 0, nB = 0;
            bool alive = false, framed = false;
            Bounds bounds = default;
            for (int i = 0; i < bits.Length; i++)
            {
                ref var b = ref bits[i];
                if (age > b.Life + 1.5f) continue;
                alive = true;
                if (!b.Down)
                {
                    // Fluttering: light, dragged, swaying side to side as it falls.
                    b.Vel += Vector3.down * 4.5f * dt;
                    b.Vel *= Mathf.Exp(-2.2f * dt);
                    b.Vel += new Vector3(Mathf.Sin(age * 3.1f + b.Seed), 0f, Mathf.Cos(age * 2.7f + b.Seed * 1.3f)) * (2.5f * dt);
                    b.Pos += b.Vel * dt;
                    b.Rot = Quaternion.Euler(b.Spin * dt) * b.Rot;
                    if (b.Pos.y <= b.Ground)
                    {
                        b.Pos.y = b.Ground;
                        b.Down = true;
                        b.Rot = Quaternion.Euler(0f, b.Seed * 37f, 0f);
                    }
                }
                float fade = 1f - Mathf.Clamp01((age - b.Life) / 1.5f);
                var m = Matrix4x4.TRS(b.Pos, b.Rot, Vector3.one * (b.Size * fade));
                if (!framed) { bounds = new Bounds(b.Pos, Vector3.one); framed = true; }
                else bounds.Encapsulate(b.Pos);
                if (b.Brown) { if (nB < batchB.Length) batchB[nB++] = m; }
                else if (nA < batchA.Length) batchA[nA++] = m;
            }
            if (!alive)
            {
                Destroy(gameObject);
                return;
            }
            bounds.Expand(2f);
            if (nA > 0) InstancedDraw.Draw(mesh, 0, green, batchA, nA, bounds, Layers.Effects, false);
            if (nB > 0) InstancedDraw.Draw(mesh, 0, brown, batchB, nB, bounds, Layers.Effects, false);
        }
    }
}
