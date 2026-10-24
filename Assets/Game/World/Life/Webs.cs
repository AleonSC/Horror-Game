using System.Collections;
using System.Collections.Generic;
using Game.Audio;
using Game.Core;
using Game.Player;
using Game.Rendering;
using Game.World.Build;
using Game.World.Generation;
using UnityEngine;
using UnityEngine.Rendering.HighDefinition;

namespace Game.World
{
    /// <summary>
    /// Spider webs (section 3.6), strung between close trees at head height. They're nearly invisible until light
    /// catches them. Walk into one and it tears across your face.
    /// </summary>
    public sealed class Webs : MonoBehaviour
    {
        readonly List<Transform> webs = new List<Transform>();
        Mesh mesh;
        Material material;

        public static Webs Build(WorldPlan plan, ForestLifeSettings s, WorldWrap wrap, Transform parent)
        {
            var w = new GameObject("Spider webs").AddComponent<Webs>();
            w.transform.SetParent(parent, false);
            w.Make(plan, s, wrap);
            return w;
        }

        void Make(WorldPlan plan, ForestLifeSettings s, WorldWrap wrap)
        {
            mesh = WebMesh();
            material = RuntimeMaterials.Lit("Spider web", new Color(0.85f, 0.85f, 0.82f, s.webShine), 0.95f, true);
            HDMaterial.SetSurfaceType(material, true);
            HDMaterial.ValidateMaterial(material);

            // Pairs of standing trees 1.8-3.2 m apart, taken in a seeded order.
            var rng = new Rng(plan.Seed, 91);
            var trees = plan.Trees;
            for (int attempt = 0; attempt < s.webs * 40 && webs.Count < s.webs && trees.Count > 1; attempt++)
            {
                var a = trees[rng.Range(0, trees.Count)];
                if (a.Removed) continue;
                for (int j = 0; j < 12; j++)
                {
                    var b = trees[rng.Range(0, trees.Count)];
                    if (b.Removed) continue;
                    Vector2 d = Wrap.Delta(a.Position, b.Position, plan.Size);
                    float dist = d.magnitude;
                    if (dist < 1.8f || dist > 3.2f) continue;
                    Vector2 mid = Wrap.Into(a.Position + d * 0.5f, plan.Size);
                    float ground = plan.GroundAt(mid);
                    var web = new GameObject("Web") { layer = Layers.Effects };
                    web.transform.SetParent(transform, false);
                    web.transform.rotation = Quaternion.LookRotation(new Vector3(-d.y, 0f, d.x)); // (spanning from tree to tree)
                    web.transform.localScale = new Vector3(dist - 0.5f, rng.Range(1.0f, 1.4f), 1f);
                    web.AddComponent<MeshFilter>().sharedMesh = mesh;
                    var r = web.AddComponent<MeshRenderer>();
                    r.sharedMaterial = material;
                    r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    wrap.Add(web.transform, new Vector3(mid.x, ground + rng.Range(1.45f, 1.75f), mid.y));
                    webs.Add(web.transform);
                    break;
                }
            }
        }

        void OnDestroy()
        {
            if (mesh != null) Destroy(mesh);
            if (material != null) Destroy(material);
        }

        void Update()
        {
            if (!Services.TryGet<PlayerController>(out var player) || player.Seat != null) return;
            Vector3 head = player.Head.position;
            for (int i = webs.Count - 1; i >= 0; i--)
            {
                var w = webs[i];
                if (w == null) { webs.RemoveAt(i); continue; }
                if ((w.position - head).sqrMagnitude > 9f) continue;
                Vector3 local = w.InverseTransformPoint(head);
                if (Mathf.Abs(local.z) < 0.25f && Mathf.Abs(local.x) < 0.5f && Mathf.Abs(local.y) < 0.6f) Tear(i, player);
            }
        }

        // Across your face: a pale smear over your eyes for a moment, a start, and it's gone.
        void Tear(int i, PlayerController player)
        {
            Destroy(webs[i].gameObject);
            webs.RemoveAt(i);
            player.AddShake(1.5f);
            if (Services.TryGet<SoundPlayer>(out var sounds)) sounds.Play2D(Sound.Rustle, MixerNames.Player, 0.6f, 1.9f);
            if (Services.TryGet<EventBus>(out var events)) events.Publish(new NoiseEmitted(player.transform.position, 0f, "walked into a web"));
            StartCoroutine(Smear());
        }

        IEnumerator Smear()
        {
            if (!Services.TryGet<ScreenFade>(out var fade) || fade.Alpha > 0.05f) yield break;
            fade.Color = new Color(0.7f, 0.7f, 0.66f);
            for (float t = 0f; t < 0.8f; t += Time.deltaTime)
            {
                if (fade.Color.r < 0.5f) yield break; // (something else took the screen)
                fade.Alpha = 0.3f * (1f - t / 0.8f);
                yield return null;
            }
            if (fade.Color.r > 0.5f) { fade.Alpha = 0f; fade.Color = Color.black; }
        }

        // A web, one unit across: strands out from the middle and a spiral round them, as thin strips.
        static Mesh WebMesh()
        {
            var verts = new List<Vector3>();
            var tris = new List<int>();
            void Strip(Vector3 a, Vector3 b, float w)
            {
                Vector3 side = Vector3.Cross(b - a, Vector3.forward).normalized * w;
                int n = verts.Count;
                verts.Add(a - side); verts.Add(a + side); verts.Add(b - side); verts.Add(b + side);
                tris.AddRange(new[] { n, n + 2, n + 1, n + 1, n + 2, n + 3 });
            }
            const int spokes = 9;
            var centre = new Vector3(0.03f, 0.02f, 0f);
            for (int k = 0; k < spokes; k++)
            {
                float a = k / (float)spokes * Mathf.PI * 2f;
                Strip(centre, new Vector3(Mathf.Cos(a) * 0.5f, Mathf.Sin(a) * 0.5f, 0f), 0.0025f);
            }
            for (float r = 0.07f; r < 0.47f; r += 0.06f)
                for (int k = 0; k < spokes; k++)
                {
                    float a0 = k / (float)spokes * Mathf.PI * 2f, a1 = (k + 1) / (float)spokes * Mathf.PI * 2f;
                    float r1 = r + 0.06f / spokes;
                    Strip(centre + new Vector3(Mathf.Cos(a0) * r, Mathf.Sin(a0) * r, 0f), centre + new Vector3(Mathf.Cos(a1) * r1, Mathf.Sin(a1) * r1, 0f), 0.0018f);
                }
            var m = new Mesh { name = "Spider web" };
            m.SetVertices(verts);
            m.SetTriangles(tris, 0);
            m.RecalculateNormals();
            m.RecalculateBounds();
            return m;
        }
    }
}
