using System.Collections.Generic;
using Game.Audio;
using Game.Core;
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

        public static Owls Build(WorldPlan plan, List<ForestTree> trees, ForestLifeSettings s, WorldWrap wrap, Transform parent)
        {
            var o = new GameObject("Owls").AddComponent<Owls>();
            o.transform.SetParent(parent, false);
            o.s = s;
            var rng = new Rng(plan.Seed, 95);
            for (int attempt = 0; attempt < s.owls * 20 && o.owls.Count < s.owls && trees.Count > 0; attempt++)
            {
                var tree = trees[rng.Range(0, trees.Count)];
                if (tree == null || tree.HasOwl) continue;
                float scale = tree.Matrix.lossyScale.y;
                float a = rng.Angle();
                Vector3 home = new Vector3(tree.Matrix.m03, tree.Matrix.m13, tree.Matrix.m23)
                               + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * 0.55f * scale + Vector3.up * rng.Range(4.5f, 6.5f) * scale;
                var body = AnimalModels.Owl(o.transform).transform;
                body.rotation = Quaternion.Euler(0f, rng.Range(0f, 360f), 0f);
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
                        sounds.Play(Sound.Hoot, o.Body.position, MixerNames.Outside, 0.5f, Random.Range(0.9f, 1.1f), 80f);
                }
            }
        }
    }
}
