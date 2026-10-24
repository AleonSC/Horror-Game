using System.Collections.Generic;
using Game.Audio;
using Game.Core;
using Game.Player;
using Game.Rendering;
using Game.World.Build;
using Game.World.Generation;
using UnityEngine;

namespace Game.World
{
    /// <summary>
    /// Vines (section 3.6) hanging from the trees, one to a few strands from a tree, down to about head height: ropes
    /// of points (Verlet, with a little stiffness so they bend rather than fold) that swing in the wind and are
    /// shoved aside as you walk through them, swinging on after you've passed, and rustling. Each is a tapering
    /// stem with pairs of leaves along it, rebuilt as it moves. Only the ones near the camera move; the rest hang still.
    /// </summary>
    public sealed class Vines : MonoBehaviour
    {
        const int Points = 16, Sides = 5;

        sealed class Vine
        {
            public Vector3 Home;      // where it hangs from, on the map
            public float Length, Thickness, Phase;
            public Vector3[] Pos = new Vector3[Points], Prev = new Vector3[Points];
            public Vector3 Offset;    // which copy of the map it's at
            public Mesh Mesh;
            public MeshRenderer Renderer;
            public bool Built;
            public float RustleCooldown;
        }

        readonly List<Vine> vines = new List<Vine>();
        WorldWrap wrap;
        ForestLifeSettings s;
        Material stem, leaf;
        // (Scratch for rebuilding a mesh.)
        readonly List<Vector3> verts = new List<Vector3>();
        readonly List<Vector3> normals = new List<Vector3>();
        readonly List<int> stemTris = new List<int>(), leafTris = new List<int>();

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
            stem = RuntimeMaterials.Lit("Vine", new Color(0.06f, 0.075f, 0.035f), 0.3f, true);
            leaf = RuntimeMaterials.Lit("Vine leaves", new Color(0.05f, 0.09f, 0.035f), 0.35f, true);
            var rng = new Rng(plan.Seed, 93);
            foreach (var t in plan.Trees)
            {
                if (t.Removed || !rng.Chance(s.vineShare)) continue;
                int strands = rng.Range(1, Mathf.Max(1, s.vinesPerTree) + 1);
                float ground = plan.GroundAt(t.Position);
                for (int k = 0; k < strands; k++)
                {
                    float a = rng.Angle();
                    Vector2 at = t.Position + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * rng.Range(0.6f, t.Kind == TreeKind.Pine ? 1.5f : 1.1f) * t.Scale;
                    float top = rng.Range(4.5f, 8f) * Mathf.Clamp(t.Scale, 0.7f, 1.3f);
                    float bottom = rng.Range(s.vineLowest, Mathf.Max(s.vineLowest, s.vineHighest));
                    var vine = new Vine
                    {
                        Home = new Vector3(at.x, ground + top, at.y),
                        Length = Mathf.Max(1f, top - bottom),
                        Thickness = rng.Range(0.012f, 0.022f),
                        Phase = rng.Range(0f, 100f),
                    };
                    var go = new GameObject("Vine") { layer = Layers.Effects };
                    go.transform.SetParent(transform, false);
                    vine.Mesh = new Mesh { name = "Vine" };
                    vine.Mesh.MarkDynamic();
                    go.AddComponent<MeshFilter>().sharedMesh = vine.Mesh;
                    vine.Renderer = go.AddComponent<MeshRenderer>();
                    vine.Renderer.sharedMaterials = new[] { stem, leaf };
                    vine.Renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    for (int i = 0; i < Points; i++) vine.Pos[i] = vine.Prev[i] = vine.Home + Vector3.down * vine.Length * i / (Points - 1f);
                    vines.Add(vine);
                }
            }
        }

        void OnDestroy()
        {
            foreach (var v in vines) if (v.Mesh != null) Destroy(v.Mesh);
            if (stem != null) Destroy(stem);
            if (leaf != null) Destroy(leaf);
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
                    v.Built = false;
                }
                float dist = (at - c).magnitude;
                v.Renderer.enabled = dist < far * 1.6f;
                if (!v.Renderer.enabled) continue;
                v.RustleCooldown -= dt;
                bool moving = dist < far;
                if (moving && dt > 0f) Simulate(v, at, dt, wind, player);
                if (moving || !v.Built) Rebuild(v);
            }
        }

        void Simulate(Vine v, Vector3 anchor, float dt, Wind wind, PlayerController player)
        {
            Vector2 push = wind != null ? wind.At(anchor.x, anchor.z, Time.time) : Vector2.zero;
            float gust = 0.7f + 0.6f * Mathf.PerlinNoise(Time.time * 0.4f, v.Phase);
            Vector3 force = (new Vector3(push.x * 5f, 0f, push.y * 5f) * (gust * s.vineWind) + Vector3.down * 9.8f) * (dt * dt);
            for (int i = 1; i < Points; i++)
            {
                Vector3 p = v.Pos[i];
                // (The wind takes the lower end more: it hangs free there.)
                v.Pos[i] += (p - v.Prev[i]) * 0.988f + force * (0.4f + 0.6f * i / (Points - 1f));
                v.Prev[i] = p;
            }

            // You shove it aside as you walk through (all the way up your body), and it swings on after.
            if (player != null)
            {
                Vector3 feet = player.transform.position;
                Vector3 moved = player.Speed > 0.1f ? new Vector3(player.transform.forward.x, 0f, player.transform.forward.z) * player.Speed : Vector3.zero;
                float shoved = 0f;
                for (int i = 1; i < Points; i++)
                {
                    Vector3 p = v.Pos[i];
                    if (p.y < feet.y - 0.1f || p.y > feet.y + 1.95f) continue;
                    Vector3 d = new Vector3(p.x - feet.x, 0f, p.z - feet.z);
                    float m = d.magnitude;
                    const float body = 0.42f;
                    if (m >= body || m < 0.0001f) continue;
                    Vector3 out_ = d / m * (body - m);
                    v.Pos[i] += out_;
                    v.Prev[i] -= moved * dt * 0.35f; // (dragged along a little: it swings after you)
                    shoved += body - m;
                }
                if (shoved > 0.25f && v.RustleCooldown <= 0f && player.Speed > 0.5f && Services.TryGet<SoundPlayer>(out var sounds))
                {
                    v.RustleCooldown = 0.8f;
                    sounds.Play(Sound.Rustle, v.Pos[Points / 2], MixerNames.World, Mathf.Clamp01(shoved) * 0.35f, Random.Range(1.1f, 1.4f), 10f);
                }
            }

            // Keep each link its length, and (more gently) keep it from folding back on itself.
            v.Pos[0] = anchor;
            float seg = v.Length / (Points - 1);
            for (int iter = 0; iter < 5; iter++)
            {
                for (int i = 1; i < Points; i++) Link(v, i - 1, i, seg, 1f);
                for (int i = 2; i < Points; i++) Link(v, i - 2, i, seg * 1.94f, 0.12f);
                v.Pos[0] = anchor;
            }
        }

        static void Link(Vine v, int a, int b, float rest, float stiffness)
        {
            Vector3 d = v.Pos[b] - v.Pos[a];
            float len = d.magnitude;
            if (len < 0.0001f || (stiffness < 1f && len >= rest)) return; // (stiffness only pushes apart: it can still sag straight)
            Vector3 fix = d * (1f - rest / len) * stiffness;
            if (a == 0) v.Pos[b] -= fix;
            else
            {
                v.Pos[b] -= fix * 0.5f;
                v.Pos[a] += fix * 0.5f;
            }
        }

        // The stem: a tube tapering toward the tip; and pairs of leaves along it, turned this way and that.
        void Rebuild(Vine v)
        {
            verts.Clear();
            normals.Clear();
            stemTris.Clear();
            leafTris.Clear();
            Vector3 origin = v.Pos[0];
            Vector3 side = Vector3.right;
            for (int i = 0; i < Points; i++)
            {
                Vector3 along = (v.Pos[Mathf.Min(Points - 1, i + 1)] - v.Pos[Mathf.Max(0, i - 1)]).normalized;
                if (along.sqrMagnitude < 1e-6f) along = Vector3.down;
                side = Vector3.ProjectOnPlane(side, along).normalized;
                if (side.sqrMagnitude < 1e-6f) side = Vector3.ProjectOnPlane(Vector3.forward, along).normalized;
                Vector3 other = Vector3.Cross(along, side);
                float r = v.Thickness * Mathf.Lerp(1f, 0.35f, i / (Points - 1f));
                for (int j = 0; j < Sides; j++)
                {
                    float a = j / (float)Sides * Mathf.PI * 2f;
                    Vector3 n = side * Mathf.Cos(a) + other * Mathf.Sin(a);
                    verts.Add(v.Pos[i] - origin + n * r);
                    normals.Add(n);
                }
            }
            for (int i = 0; i < Points - 1; i++)
                for (int j = 0; j < Sides; j++)
                {
                    int a = i * Sides + j, b = i * Sides + (j + 1) % Sides, c = a + Sides, d = b + Sides;
                    stemTris.Add(a); stemTris.Add(c); stemTris.Add(b);
                    stemTris.Add(b); stemTris.Add(c); stemTris.Add(d);
                }
            // Leaves: a pair at every other point below the top, each a little pointed oval.
            for (int i = 2; i < Points; i += 2)
            {
                Vector3 along = (v.Pos[i] - v.Pos[i - 1]).normalized;
                float turn = (i * 67f + v.Phase * 10f) % 360f;
                Vector3 outward = Quaternion.AngleAxis(turn, along) * Vector3.ProjectOnPlane(Vector3.forward, along).normalized;
                for (int pair = -1; pair <= 1; pair += 2)
                {
                    Vector3 o = outward * pair;
                    Vector3 basePos = v.Pos[i] - origin;
                    Vector3 tip = basePos + (o * 0.09f + Vector3.down * 0.03f);
                    Vector3 width = Vector3.Cross(o, along).normalized * 0.028f;
                    Vector3 n = Vector3.Cross(width, tip - basePos).normalized;
                    int k = verts.Count;
                    verts.Add(basePos); verts.Add(Vector3.Lerp(basePos, tip, 0.45f) + width); verts.Add(tip); verts.Add(Vector3.Lerp(basePos, tip, 0.45f) - width);
                    for (int q = 0; q < 4; q++) normals.Add(n);
                    leafTris.Add(k); leafTris.Add(k + 1); leafTris.Add(k + 2);
                    leafTris.Add(k); leafTris.Add(k + 2); leafTris.Add(k + 3);
                }
            }
            v.Renderer.transform.position = origin;
            v.Mesh.Clear();
            v.Mesh.subMeshCount = 2;
            v.Mesh.SetVertices(verts);
            v.Mesh.SetNormals(normals);
            v.Mesh.SetTriangles(stemTris, 0);
            v.Mesh.SetTriangles(leafTris, 1);
            v.Mesh.RecalculateBounds();
            v.Built = true;
        }
    }
}
