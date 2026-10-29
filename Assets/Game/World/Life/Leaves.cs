using Game.Core;
using Game.Player;
using Game.Rendering;
using Game.World.Build;
using UnityEngine;

namespace Game.World
{
    /// <summary>
    /// Dead leaves (section 6.13): lying all round you, kicked up by strong gusts and by your running feet, tumbling
    /// and settling again; and leaves letting go of the trees round you (more in a gust) and fluttering down,
    /// rocking side to side as a real leaf does, to lie a while where they land. Drawn instanced, round the camera.
    /// </summary>
    public sealed class Leaves : MonoBehaviour
    {
        const float Reach = 14f;

        struct Leaf
        {
            public Vector3 Pos, Vel;
            public float Ground, Spin, Yaw, Tilt;
            public bool Flying, Hidden;
        }

        // One coming off a tree: hanging there (Wait), then fluttering down, then lying (Lie) a while.
        struct Falling
        {
            public Vector3 Pos;
            public float Ground, Wait, Lie, Yaw, Spin, Phase, Freq, Swing, SwingYaw, Size, Tilt;
            public bool Down;
        }

        Leaf[] leaves;
        Falling[] fallingLeaves;
        int lying, falling;
        ForestLifeSettings s;
        Mesh mesh;
        Material material, fallingMaterial;
        readonly Collider[] trees = new Collider[8];
        readonly Matrix4x4[] batch = new Matrix4x4[InstancedDraw.MaxPerCall];

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void AttachToGame()
        {
            if (!Services.TryGet<PlayerController>(out _) || !Services.TryGet<WorldBuilder>(out _)) return;
            if (Services.TryGet<GameRoot>(out var root) && root.GetComponentInChildren<Leaves>() == null)
                new GameObject("Leaves").AddComponent<Leaves>().transform.SetParent(root.transform, false);
        }

        void Start()
        {
            var config = Services.Get<GameSetup>().Get<ForestLifeConfig>();
            s = config != null ? config.settings : new ForestLifeSettings();
            lying = s.leaves;
            falling = s.fallingLeaves;
            leaves = new Leaf[lying];
            fallingLeaves = new Falling[falling];
            mesh = new Mesh { name = "Leaf" };
            // (leaf-shaped: a point at the tip, broad across the middle, narrowing to the stalk)
            mesh.vertices = new[] { new Vector3(0f, 0f, 0.07f), new Vector3(-0.035f, 0f, 0.02f), new Vector3(0.035f, 0f, 0.02f), new Vector3(-0.03f, 0f, -0.03f), new Vector3(0.03f, 0f, -0.03f), new Vector3(0f, 0f, -0.06f) };
            mesh.triangles = new[] { 0, 2, 1, 1, 2, 4, 1, 4, 3, 3, 4, 5 };
            mesh.RecalculateNormals();
            mesh.bounds = new Bounds(Vector3.zero, Vector3.one * 0.2f);
            material = RuntimeMaterials.Lit("Dead leaf", new Color(0.22f, 0.13f, 0.06f), 0.15f, true);
            material.enableInstancing = true;
            // (fresher off the tree: a touch lighter and warmer, to catch your light as they come down)
            fallingMaterial = RuntimeMaterials.Lit("Falling leaf", s.fallingLeafColour, 0.2f, true);
            fallingMaterial.enableInstancing = true;
            var cam = Camera.main;
            Vector3 c = cam != null ? cam.transform.position : Vector3.zero;
            for (int i = 0; i < leaves.Length; i++) Respawn(i, c, true);
            for (int i = 0; i < fallingLeaves.Length; i++) NewFalling(i, c, true);
        }

        void OnDestroy()
        {
            if (mesh != null) Destroy(mesh);
            if (material != null) Destroy(material);
            if (fallingMaterial != null) Destroy(fallingMaterial);
        }

        // A leaf on a tree near you (in its crown), to let go after a while. (No tree there: it tries again soon.)
        void NewFalling(int i, Vector3 c, bool start)
        {
            ref var f = ref fallingLeaves[i];
            float reach = s.fallingLeafReach;
            float a = Random.Range(0f, Mathf.PI * 2f), r = Mathf.Sqrt(Random.value) * reach;
            var p = new Vector3(c.x + Mathf.Cos(a) * r, c.y, c.z + Mathf.Sin(a) * r);
            f.Down = false;
            f.Lie = 0f;
            f.Wait = start ? Random.Range(0f, s.fallingLeafWait * 2f) : Random.Range(0.3f, s.fallingLeafWait * 2f);
            int n = Physics.OverlapSphereNonAlloc(p, 6f, trees, Layers.Mask(Layers.Trees), QueryTriggerInteraction.Ignore);
            if (n == 0) { f.Ground = float.NaN; f.Wait = Random.Range(0.5f, 2f); return; }
            Vector3 trunk = trees[Random.Range(0, n)].bounds.center;
            var off = Random.insideUnitCircle.normalized * Random.Range(0.6f, 3.2f);
            p = new Vector3(trunk.x + off.x, 0f, trunk.z + off.y);
            if (!Physics.Raycast(p + Vector3.up * 200f, Vector3.down, out var hit, 2000f, Layers.Mask(Layers.Ground), QueryTriggerInteraction.Ignore))
            { f.Ground = float.NaN; f.Wait = Random.Range(0.5f, 2f); return; }
            f.Ground = hit.point.y + 0.012f;
            p.y = f.Ground + Random.Range(s.fallingLeafHeight * 0.5f, s.fallingLeafHeight);
            f.Pos = p;
            f.Yaw = Random.Range(0f, 360f);
            f.Spin = Random.Range(-40f, 40f);
            f.Phase = Random.Range(0f, Mathf.PI * 2f);
            f.Freq = Random.Range(0.45f, 0.8f); // (rocks to and fro about once every second or two)
            f.Swing = Random.Range(0.25f, 0.6f);
            f.SwingYaw = Random.Range(0f, Mathf.PI * 2f);
            f.Size = Random.Range(1.2f, 1.8f) * s.fallingLeafSize;
            f.Tilt = 0f;
        }

        void Respawn(int i, Vector3 c, bool anywhere)
        {
            ref var l = ref leaves[i];
            float a = Random.Range(0f, Mathf.PI * 2f), r = (anywhere ? Mathf.Sqrt(Random.value) : Random.Range(0.85f, 1f)) * Reach;
            var p = new Vector3(c.x + Mathf.Cos(a) * r, c.y, c.z + Mathf.Sin(a) * r);
            // (From well above, all the way down: the menu film's camera can be high over the trees. No ground
            // found, no leaf, rather than one lying on nothing.)
            l.Hidden = !Physics.Raycast(p + Vector3.up * 200f, Vector3.down, out var hit, 2000f, Layers.Mask(Layers.Ground), QueryTriggerInteraction.Ignore);
            l.Ground = l.Hidden ? c.y : hit.point.y + 0.01f;
            p.y = l.Ground;
            l.Pos = p;
            l.Vel = Vector3.zero;
            l.Flying = false;
            l.Yaw = Random.Range(0f, 360f);
            l.Tilt = Random.Range(-15f, 15f);
            l.Spin = Random.Range(-200f, 200f);
        }

        void Update()
        {
            var cam = Camera.main;
            if (cam == null || leaves == null || PlayerPlace.InDarkRoom) return;
            Vector3 c = cam.transform.position;
            float dt = Mathf.Min(Time.deltaTime, 0.05f);
            var wind = Services.TryGet<Wind>(out var w) ? w : null;
            wind?.Prepare(Time.time);
            Services.TryGet<PlayerController>(out var player);
            Vector3 feet = player != null ? player.transform.position : c;
            bool running = player != null && player.Running;

            int n = 0;
            var bounds = new Bounds(c, Vector3.one * (Mathf.Max(Reach, s.fallingLeafReach) * 2f + 30f));
            for (int i = 0; i < leaves.Length; i++)
            {
                ref var l = ref leaves[i];
                float dx = l.Pos.x - c.x, dz = l.Pos.z - c.z;
                if (dx * dx + dz * dz > Reach * Reach * 1.2f) { Respawn(i, c, false); continue; }
                if (l.Hidden) continue;
                Vector2 push = wind != null ? wind.At(l.Pos.x, l.Pos.z) : Vector2.zero;
                if (!l.Flying)
                {
                    // A strong gust, or your running feet, lift it.
                    float fx = l.Pos.x - feet.x, fz = l.Pos.z - feet.z;
                    bool kicked = running && fx * fx + fz * fz < 1.4f;
                    // (the storm: lighter gusts lift them, and far more often)
                    float storm = wind != null ? wind.Storm : 0f;
                    float lift = Mathf.Lerp(0.9f, 0.35f, storm);
                    if (kicked || (push.sqrMagnitude > lift && Random.value < dt * Mathf.Lerp(0.8f, 4f, storm)))
                    {
                        l.Flying = true;
                        l.Vel = new Vector3(push.x * 3f + (kicked ? fx * 2f : 0f), Random.Range(1.2f, kicked ? 2.5f : 2f), push.y * 3f + (kicked ? fz * 2f : 0f));
                    }
                }
                else
                {
                    // Drifting and tumbling: slow to fall, carried by the wind.
                    l.Vel += (new Vector3(push.x * 4f, -1.2f, push.y * 4f) - l.Vel) * Mathf.Min(1f, dt * 1.5f);
                    l.Vel.x += Mathf.Sin(Time.time * 3f + i) * dt * 2f;
                    l.Pos += l.Vel * dt;
                    l.Yaw += l.Spin * dt;
                    l.Tilt = Mathf.Sin(Time.time * 4f + i) * 60f;
                    if (l.Pos.y <= l.Ground)
                    {
                        l.Pos.y = l.Ground;
                        l.Flying = false;
                        l.Tilt = Random.Range(-15f, 15f);
                    }
                }
                batch[n++] = Matrix4x4.TRS(l.Pos, Quaternion.Euler(l.Tilt, l.Yaw, 0f), Vector3.one);
                if (n == batch.Length)
                {
                    InstancedDraw.Draw(mesh, 0, material, batch, n, bounds, Layers.Effects, false);
                    n = 0;
                }
            }
            if (n > 0) InstancedDraw.Draw(mesh, 0, material, batch, n, bounds, Layers.Effects, false);
            UpdateFalling(c, dt, wind, bounds);
        }

        void UpdateFalling(Vector3 c, float dt, Wind wind, Bounds bounds)
        {
            int n = 0;
            float reach = s.fallingLeafReach;
            for (int i = 0; i < fallingLeaves.Length; i++)
            {
                ref var f = ref fallingLeaves[i];
                Vector2 push = wind != null && !float.IsNaN(f.Ground) ? wind.At(f.Pos.x, f.Pos.z) : Vector2.zero;
                if (f.Wait > 0f)
                {
                    // Still on the tree: a gust shakes it loose sooner (a storm, all the sooner).
                    float storm = wind != null ? wind.Storm : 0f;
                    f.Wait -= dt * (1f + push.sqrMagnitude * s.fallingLeafGusts * (1f + storm * 2f));
                    if (f.Wait <= 0f && float.IsNaN(f.Ground)) NewFalling(i, c, false);
                    continue;
                }
                float dx = f.Pos.x - c.x, dz = f.Pos.z - c.z;
                if (dx * dx + dz * dz > reach * reach * 1.3f) { NewFalling(i, c, false); continue; }
                float size = f.Size;
                if (!f.Down)
                {
                    // Fluttering down: it rocks to and fro, swooping fastest (and dropping) through the middle of
                    // each swing and all but hanging at its ends, turning slowly, carried along by the wind.
                    float w = f.Freq * Mathf.PI * 2f;
                    float ph = f.Phase + Time.time * w;
                    float sin = Mathf.Sin(ph), cos = Mathf.Cos(ph);
                    var dir = new Vector3(Mathf.Cos(f.SwingYaw), 0f, Mathf.Sin(f.SwingYaw));
                    Vector3 v = dir * (f.Swing * w * cos) + new Vector3(push.x, 0f, push.y) * 1.6f;
                    v.y = -s.fallingLeafSpeed * (0.35f + 1.3f * cos * cos);
                    f.Pos += v * dt;
                    f.Yaw += f.Spin * dt;
                    f.Tilt = sin * 50f;
                    f.SwingYaw += Mathf.Sin(Time.time * 0.3f + i) * 0.15f * dt; // (its path wandering)
                    // (the ground under it, now and then, as it drifts)
                    if ((Time.frameCount + i) % 8 == 0 && Physics.Raycast(f.Pos + Vector3.up * 50f, Vector3.down, out var hit, 200f, Layers.Mask(Layers.Ground), QueryTriggerInteraction.Ignore))
                        f.Ground = hit.point.y + 0.012f;
                    if (f.Pos.y <= f.Ground)
                    {
                        f.Pos.y = f.Ground;
                        f.Down = true;
                        f.Lie = s.fallingLeafLie * Random.Range(0.7f, 1.3f);
                        f.Tilt = Random.Range(-12f, 12f);
                    }
                }
                else
                {
                    // Lying where it came down, then gone (shrinking away over its last second).
                    f.Lie -= dt;
                    if (f.Lie <= 0f) { NewFalling(i, c, false); continue; }
                    size *= Mathf.Clamp01(f.Lie);
                }
                Quaternion rot = f.Down ? Quaternion.Euler(f.Tilt, f.Yaw, 0f)
                    : Quaternion.AngleAxis(f.Tilt, new Vector3(-Mathf.Sin(f.SwingYaw), 0f, Mathf.Cos(f.SwingYaw))) * Quaternion.Euler(0f, f.Yaw, 0f);
                batch[n++] = Matrix4x4.TRS(f.Pos, rot, Vector3.one * size);
                if (n == batch.Length)
                {
                    InstancedDraw.Draw(mesh, 0, fallingMaterial, batch, n, bounds, Layers.Effects, false);
                    n = 0;
                }
            }
            if (n > 0) InstancedDraw.Draw(mesh, 0, fallingMaterial, batch, n, bounds, Layers.Effects, false);
        }
    }
}
