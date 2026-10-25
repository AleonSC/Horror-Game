using System.Collections.Generic;
using Game.Core;
using Game.Rendering;
using UnityEngine;

namespace Game.World
{
    /// <summary>
    /// A lightning bolt in the sky, with the flash (section 6.13): a jagged, branching streak from the cloud base
    /// down toward the horizon, white-hot and flickering with the flash, a glow in the cloud where it starts. Each
    /// strike is a new bolt. It's drawn at a fixed distance and scaled to look as big as one that far off really
    /// would (like the tornadoes), so it stays inside the camera's far clip, and the fog dims it as it would.
    /// </summary>
    public sealed class LightningBolt : MonoBehaviour
    {
        const float RenderDistance = 340f;
        const float CloudBase = 520f; // (how high the bolts start, at the real distance)

        Mesh mesh;
        Material core;
        MeshRenderer meshRenderer;
        Renderer cloudGlow;
        float brightness;

        public static LightningBolt Create(Transform parent)
        {
            var go = new GameObject("Lightning bolt") { layer = Layers.Effects };
            go.transform.SetParent(parent, false);
            var bolt = go.AddComponent<LightningBolt>();
            bolt.mesh = new Mesh { name = "Lightning bolt" };
            go.AddComponent<MeshFilter>().sharedMesh = bolt.mesh;
            bolt.core = RuntimeMaterials.Glow("Lightning", Color.black, true);
            bolt.meshRenderer = go.AddComponent<MeshRenderer>();
            bolt.meshRenderer.sharedMaterial = bolt.core;
            bolt.meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            bolt.meshRenderer.receiveShadows = false;
            bolt.cloudGlow = Effects.Glow(go.transform, Vector3.zero, 1f, Color.black, 0.5f, 1.6f, "Lightning in the cloud");
            bolt.Hide();
            return bolt;
        }

        void OnDestroy()
        {
            if (mesh != null) Destroy(mesh);
            if (core != null) Destroy(core);
        }

        /// <summary>
        /// A new bolt toward <paramref name="bearing"/> (degrees round from north), striking
        /// <paramref name="distance"/> metres off, seen from <paramref name="eye"/>.
        /// </summary>
        public void Strike(Vector3 eye, float bearing, float distance, float glow)
        {
            brightness = glow;
            Vector3 flat = Quaternion.Euler(0f, bearing, 0f) * Vector3.forward;
            float d = Mathf.Max(100f, distance);
            float k = RenderDistance / d;                      // (everything shrunk by this, so it looks as big)
            float top = Mathf.Clamp(CloudBase * k, 70f, 480f);
            transform.position = eye + flat * RenderDistance + Vector3.down * 20f; // (its foot down behind the trees)
            transform.rotation = Quaternion.LookRotation(-flat); // (its plane faces you)
            Build(top, Mathf.Clamp(2.4f * k, 0.6f, 3f), new System.Random(Random.Range(0, int.MaxValue)));
            cloudGlow.transform.localPosition = new Vector3(0f, top + 20f, 0f);
            cloudGlow.transform.localScale = Vector3.one * top * 0.9f;
            meshRenderer.enabled = true;
            cloudGlow.enabled = true;
            Level(1f);
        }

        /// <summary>How bright it is now, 0-1 (it flickers with the flash).</summary>
        public void Level(float f)
        {
            RuntimeMaterials.SetGlow(core, new Color(0.85f, 0.9f, 1f) * brightness * f);
            Effects.SetGlow(cloudGlow, new Color(0.55f, 0.6f, 0.8f) * 0.25f * f);
        }

        public void Hide()
        {
            meshRenderer.enabled = false;
            cloudGlow.enabled = false;
        }

        // The bolt in its own plane (X across, Y up, facing -Z to the camera): a main channel from the cloud down,
        // jagged by repeated midpoint offsets, with forks that split off it and fork again, thinner each time.
        void Build(float top, float width, System.Random rng)
        {
            var verts = new List<Vector3>();
            var tris = new List<int>();
            float R(float a, float b) => a + (b - a) * (float)rng.NextDouble();

            void Channel(Vector2 from, Vector2 to, float w, int depth)
            {
                var pts = new List<Vector2> { from, to };
                float jag = (to - from).magnitude * 0.18f;
                for (int pass = 0; pass < 6; pass++)
                {
                    var next = new List<Vector2>();
                    for (int i = 0; i < pts.Count - 1; i++)
                    {
                        Vector2 a = pts[i], b = pts[i + 1], dir = (b - a).normalized;
                        Vector2 side = new Vector2(-dir.y, dir.x);
                        next.Add(a);
                        next.Add((a + b) * 0.5f + side * R(-jag, jag));
                    }
                    next.Add(pts[pts.Count - 1]);
                    pts = next;
                    jag *= 0.55f;
                }
                // A ribbon along it, tapering toward its end.
                for (int i = 0; i < pts.Count - 1; i++)
                {
                    Vector2 a = pts[i], b = pts[i + 1];
                    Vector2 dir = (b - a).normalized, side = new Vector2(-dir.y, dir.x);
                    float wa = w * Mathf.Lerp(1f, 0.35f, i / (float)pts.Count), wb = w * Mathf.Lerp(1f, 0.35f, (i + 1) / (float)pts.Count);
                    int v = verts.Count;
                    verts.Add(a - side * wa * 0.5f);
                    verts.Add(a + side * wa * 0.5f);
                    verts.Add(b - side * wb * 0.5f);
                    verts.Add(b + side * wb * 0.5f);
                    tris.Add(v); tris.Add(v + 2); tris.Add(v + 1);
                    tris.Add(v + 1); tris.Add(v + 2); tris.Add(v + 3);
                }
                if (depth >= 2) return;
                // Forks: off at a slant from points along it, heading down and out.
                int forks = depth == 0 ? rng.Next(3, 7) : rng.Next(0, 3);
                for (int f = 0; f < forks; f++)
                {
                    int at = rng.Next(pts.Count / 8, pts.Count * 3 / 4);
                    Vector2 start = pts[at];
                    Vector2 main = (to - from).normalized;
                    float turn = R(20f, 55f) * (rng.NextDouble() < 0.5 ? -1f : 1f);
                    Vector2 dir = (Vector2)(Quaternion.Euler(0f, 0f, turn) * main);
                    float len = (to - from).magnitude * R(0.15f, 0.4f) * (depth == 0 ? 1f : 0.6f);
                    Channel(start, start + dir * len, w * 0.5f, depth + 1);
                }
            }

            Vector2 cloud = new Vector2(R(-0.15f, 0.15f) * top, top);
            Vector2 ground = new Vector2(R(-0.3f, 0.3f) * top, 0f);
            Channel(cloud, ground, width, 0);
            mesh.Clear();
            var v3 = new List<Vector3>(verts.Count);
            foreach (var p in verts) v3.Add(new Vector3(p.x, p.y, 0f));
            mesh.SetVertices(v3);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateBounds();
        }
    }
}
