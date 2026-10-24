using System.Collections.Generic;
using Game.Core;
using Game.Player;
using Game.Rendering;
using Game.World.Build;
using Game.World.Generation;
using UnityEngine;

namespace Game.World
{
    /// <summary>
    /// Vines (section 3.6) hanging from a few trees: ropes of points (Verlet) that swing in the wind and are pushed
    /// aside as you walk through them. Only the ones near the camera move; the rest hang still.
    /// </summary>
    public sealed class Vines : MonoBehaviour
    {
        const int Points = 10;

        sealed class Vine
        {
            public Vector3 Home;      // where it hangs from, on the map
            public float Length;
            public Vector3[] Pos = new Vector3[Points], Prev = new Vector3[Points];
            public Vector3 Offset;    // which copy of the map it's at
            public LineRenderer Line;
            public bool Settled;
        }

        readonly List<Vine> vines = new List<Vine>();
        WorldWrap wrap;
        ForestLifeSettings s;
        Material material;
        readonly Vector3[] linePoints = new Vector3[Points];

        public static Vines Build(WorldPlan plan, ForestLifeSettings s, WorldWrap wrap, Transform parent)
        {
            var v = new GameObject("Vines").AddComponent<Vines>();
            v.transform.SetParent(parent, false);
            v.wrap = wrap;
            v.s = s;
            v.Make(plan);
            return v;
        }

        void Make(WorldPlan plan)
        {
            material = RuntimeMaterials.Lit("Vine", new Color(0.05f, 0.07f, 0.03f), 0.3f);
            var rng = new Rng(plan.Seed, 93);
            foreach (var t in plan.Trees)
            {
                if (t.Removed || t.Kind != TreeKind.Pine || !rng.Chance(s.vineShare)) continue;
                float a = rng.Angle();
                Vector2 at = t.Position + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * rng.Range(0.7f, 1.4f) * t.Scale;
                float top = rng.Range(5.5f, 8f) * t.Scale;
                var vine = new Vine { Home = new Vector3(at.x, plan.GroundAt(t.Position) + top, at.y), Length = top - rng.Range(0.8f, 1.6f) };
                var go = new GameObject("Vine") { layer = Layers.Effects };
                go.transform.SetParent(transform, false);
                var line = go.AddComponent<LineRenderer>();
                line.sharedMaterial = material;
                line.positionCount = Points;
                line.widthMultiplier = 0.035f;
                line.useWorldSpace = true;
                line.numCapVertices = 2;
                vine.Line = line;
                for (int i = 0; i < Points; i++) vine.Pos[i] = vine.Prev[i] = vine.Home + Vector3.down * vine.Length * i / (Points - 1f);
                vines.Add(vine);
            }
        }

        void OnDestroy()
        {
            if (material != null) Destroy(material);
        }

        void LateUpdate()
        {
            var cam = Camera.main;
            if (cam == null || wrap == null) return;
            Vector3 c = cam.transform.position;
            float dt = Mathf.Min(Time.deltaTime, 0.033f);
            var wind = Services.TryGet<Wind>(out var w) ? w : null;
            Services.TryGet<PlayerController>(out var player);
            float far = s.vineSimulateWithin;

            foreach (var v in vines)
            {
                // Follow the map's wrapping: the whole rope moves with its tree's copy.
                Vector3 at = wrap.Nearest(v.Home, c);
                Vector3 offset = at - v.Home;
                if (offset != v.Offset)
                {
                    Vector3 shift = offset - v.Offset;
                    for (int i = 0; i < Points; i++) { v.Pos[i] += shift; v.Prev[i] += shift; }
                    v.Offset = offset;
                }
                float dist = (at - c).magnitude;
                v.Line.enabled = dist < far * 1.6f;
                if (!v.Line.enabled) continue;
                if (dist < far) Simulate(v, at, dt, wind, player);
                for (int i = 0; i < Points; i++) linePoints[i] = v.Pos[i];
                v.Line.SetPositions(linePoints);
            }
        }

        void Simulate(Vine v, Vector3 anchor, float dt, Wind wind, PlayerController player)
        {
            Vector2 push = wind != null ? wind.At(anchor.x, anchor.z, Time.time) : Vector2.zero;
            Vector3 force = new Vector3(push.x * 6f, -9.8f, push.y * 6f) * dt * dt;
            for (int i = 1; i < Points; i++)
            {
                Vector3 p = v.Pos[i];
                v.Pos[i] += (p - v.Prev[i]) * 0.985f + force;
                v.Prev[i] = p;
            }
            v.Pos[0] = anchor;
            float seg = v.Length / (Points - 1);
            for (int iter = 0; iter < 4; iter++)
            {
                for (int i = 1; i < Points; i++)
                {
                    Vector3 d = v.Pos[i] - v.Pos[i - 1];
                    float len = d.magnitude;
                    if (len < 0.0001f) continue;
                    Vector3 fix = d * (1f - seg / len);
                    if (i == 1) v.Pos[i] -= fix;
                    else
                    {
                        v.Pos[i] -= fix * 0.5f;
                        v.Pos[i - 1] += fix * 0.5f;
                    }
                }
                v.Pos[0] = anchor;
            }
            // You push it aside as you walk through.
            if (player == null) return;
            Vector3 feet = player.transform.position;
            for (int i = 1; i < Points; i++)
            {
                Vector3 p = v.Pos[i];
                if (p.y < feet.y || p.y > feet.y + 1.9f) continue;
                Vector3 d = new Vector3(p.x - feet.x, 0f, p.z - feet.z);
                float m = d.magnitude;
                if (m < 0.45f && m > 0.0001f) v.Pos[i] += d / m * (0.45f - m);
            }
        }
    }
}
