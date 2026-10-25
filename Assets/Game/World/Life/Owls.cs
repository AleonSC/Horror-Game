using System.Collections.Generic;
using Game.Audio;
using Game.Core;
using Game.Rendering;
using Game.World.Build;
using Game.World.Generation;
using UnityEngine;

namespace Game.World
{
    /// <summary>
    /// Owls (section 6.13) sitting on branches all through the forest: white eyes, heads that turn to follow you
    /// when you're near, and a call now and then. Trees with owls in them never fall.
    /// </summary>
    public sealed class Owls : MonoBehaviour
    {
        sealed class Owl
        {
            public Transform Body, Head;
            public float NextCall;
        }

        readonly List<Owl> owls = new List<Owl>();
        ForestLifeSettings s;

        /// <summary>The owls in the current world (the menu's film looks for one).</summary>
        public static Owls Current { get; private set; }
        public int Count => owls.Count;
        public Transform OwlAt(int i) => owls[i].Body;

        Material bark, leaf, lichen;

        void OnDestroy()
        {
            if (Current == this) Current = null;
            foreach (var f in GetComponentsInChildren<MeshFilter>(true)) if (f.sharedMesh != null && f.sharedMesh.name == "Loft") Destroy(f.sharedMesh);
            foreach (var m in new[] { bark, leaf, lichen }) if (m != null) Destroy(m);
        }

        // An owl's branch: a bough growing out of the trunk (from inside it) under the owl's feet, curving gently up
        // to a slender tip, with a couple of twigs ending in leaves, and a patch of pale lichen. It's part of the
        // owl (so it goes wherever the map's wrapping puts the owl).
        void Branch(Transform owl, Vector3 home, Vector3 trunkAt, Vector3 dir, float scale, Rng rng)
        {
            if (bark == null)
            {
                bark = RuntimeMaterials.Lit("Branch bark", new Color(0.14f, 0.1f, 0.07f), 0.2f);
                leaf = RuntimeMaterials.Lit("Branch leaves", new Color(0.07f, 0.1f, 0.05f), 0.25f, true);
                lichen = RuntimeMaterials.Lit("Lichen", new Color(0.32f, 0.36f, 0.27f), 0.1f);
            }
            Quaternion toLocal = Quaternion.Inverse(owl.rotation);
            Vector3 L(Vector3 world) => toLocal * (world - home);
            Vector3 side = Vector3.Cross(Vector3.up, dir);
            float droop = rng.Range(-0.04f, 0.04f);
            Vector3 b0 = trunkAt + dir * 0.08f * scale - Vector3.up * 0.16f;
            Vector3 b1 = Vector3.Lerp(trunkAt, home, 0.5f) - Vector3.up * 0.09f + side * droop;
            Vector3 b2 = home - Vector3.up * 0.03f;
            Vector3 b3 = home + dir * 0.3f + Vector3.up * 0.04f - side * droop;
            Vector3 b4 = home + dir * 0.52f + Vector3.up * 0.11f;
            var main = Lofts.Shape(owl, "Branch", bark, 2.1f, true, true,
                (L(b0), new Vector2(0.075f, 0.07f)), (L(b1), new Vector2(0.05f, 0.046f)), (L(b2), new Vector2(0.034f, 0.032f)),
                (L(b3), new Vector2(0.02f, 0.019f)), (L(b4), new Vector2(0.004f, 0.004f)));
            main.layer = owl.gameObject.layer;
            // Two twigs, up and out, each with a few leaves at its end.
            foreach (var (from, way, len) in new[] { (Vector3.Lerp(b1, b2, 0.4f), (side + Vector3.up * 0.9f + dir * 0.3f).normalized, 0.32f),
                                                     (Vector3.Lerp(b2, b3, 0.6f), (-side + Vector3.up * 0.7f + dir * 0.5f).normalized, 0.26f) })
            {
                Vector3 mid = from + way * len * 0.5f + Vector3.up * 0.02f, end = from + way * len;
                Lofts.Shape(owl, "Twig", bark, 2f, true, true,
                    (L(from), new Vector2(0.014f, 0.013f)), (L(mid), new Vector2(0.009f, 0.009f)), (L(end), new Vector2(0.002f, 0.002f)));
                for (int k = 0; k < 4; k++)
                {
                    var l = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                    Destroy(l.GetComponent<Collider>());
                    l.transform.SetParent(owl, false);
                    l.transform.localPosition = L(end + Quaternion.AngleAxis(k * 90f + rng.Range(-20f, 20f), way) * side * 0.03f);
                    l.transform.localRotation = Quaternion.Euler(rng.Range(-40f, 40f), k * 90f, rng.Range(-30f, 30f));
                    l.transform.localScale = new Vector3(0.055f, 0.008f, 0.03f);
                    var r = l.GetComponent<Renderer>();
                    r.sharedMaterial = leaf;
                    r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                }
            }
            // A crust of pale lichen along the top of the bough.
            for (int k = 0; k < 3; k++)
            {
                var p = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                Destroy(p.GetComponent<Collider>());
                p.transform.SetParent(owl, false);
                p.transform.localPosition = L(Vector3.Lerp(b0, b2, 0.25f + k * 0.22f) + Vector3.up * 0.04f);
                p.transform.localScale = new Vector3(0.05f, 0.012f, 0.06f);
                var r = p.GetComponent<Renderer>();
                r.sharedMaterial = lichen;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
        }

        public static Owls Build(WorldPlan plan, List<ForestTree> trees, ForestLifeSettings s, WorldWrap wrap, Transform parent)
        {
            var o = new GameObject("Owls").AddComponent<Owls>();
            o.transform.SetParent(parent, false);
            o.s = s;
            Current = o;
            var rng = new Rng(plan.Seed, 95);
            for (int attempt = 0; attempt < s.owls * 20 && o.owls.Count < s.owls && trees.Count > 0; attempt++)
            {
                var tree = trees[rng.Range(0, trees.Count)];
                if (tree == null || tree.HasOwl) continue;
                float scale = tree.Matrix.lossyScale.y;
                float a = rng.Angle();
                Vector3 dir = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                Vector3 trunk = new Vector3(tree.Matrix.m03, tree.Matrix.m13, tree.Matrix.m23);
                float height = rng.Range(s.owlHeight.x, Mathf.Max(s.owlHeight.x, s.owlHeight.y));
                // Out on its own branch, a little way from the trunk, facing out along it.
                Vector3 home = trunk + dir * (0.42f * scale + 0.25f) + Vector3.up * height;
                var body = AnimalModels.Owl(o.transform).transform;
                body.rotation = Quaternion.LookRotation(dir) * Quaternion.Euler(0f, rng.Range(-35f, 35f), 0f);
                o.Branch(body, home, trunk + Vector3.up * height, dir, scale, rng);
                wrap.Add(body, home);
                tree.HasOwl = true;
                o.owls.Add(new Owl { Body = body, Head = body.Find("Head"), NextCall = rng.Range(s.minOwlCall, s.maxOwlCall) });
            }
            return o;
        }

        void Update()
        {
            var cam = Camera.main;
            if (cam == null || PlayerPlace.InDarkRoom) return;
            Vector3 c = cam.transform.position;
            float dt = Time.deltaTime;
            float watch2 = s.owlWatch * s.owlWatch;
            foreach (var o in owls)
            {
                Vector3 to = c - o.Body.position;
                float d2 = to.sqrMagnitude;
                if (d2 < watch2 && o.Head != null)
                {
                    // Its head follows you, the way an owl's does: smoothly, then a sudden snap.
                    var want = Quaternion.Inverse(o.Body.rotation) * Quaternion.LookRotation(to);
                    o.Head.localRotation = Quaternion.RotateTowards(o.Head.localRotation, want, dt * (Random.value < 0.02f ? 400f : 60f));
                }
                o.NextCall -= dt;
                if (o.NextCall <= 0f)
                {
                    o.NextCall = Random.Range(s.minOwlCall, s.maxOwlCall);
                    if (d2 < 70f * 70f && Services.TryGet<SoundPlayer>(out var sounds))
                    {
                        // Mostly the long hooting call; now and then a barn owl's screech instead.
                        bool screech = Random.value < 0.18f;
                        sounds.Play(screech ? Sound.Screech : Sound.Hoot, o.Body.position, MixerNames.Outside, screech ? 0.45f : 0.55f, Random.Range(0.74f, 0.86f), screech ? 70f : 90f);
                    }
                }
            }
        }
    }
}
