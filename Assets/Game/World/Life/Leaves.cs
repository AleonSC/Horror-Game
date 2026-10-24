using Game.Core;
using Game.Player;
using Game.Rendering;
using Game.World.Build;
using UnityEngine;

namespace Game.World
{
    /// <summary>
    /// Dead leaves (section 6.13): lying all round you, kicked up by strong gusts and by your running feet, tumbling
    /// and settling again; and a few always drifting down from the canopy. Drawn instanced, round the camera.
    /// </summary>
    public sealed class Leaves : MonoBehaviour
    {
        const float Reach = 14f;

        struct Leaf
        {
            public Vector3 Pos, Vel;
            public float Ground, Spin, Yaw, Tilt;
            public bool Flying;
        }

        Leaf[] leaves;
        int lying, falling;
        Mesh mesh;
        Material material;
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
            var s = config != null ? config.settings : new ForestLifeSettings();
            lying = s.leaves;
            falling = s.fallingLeaves;
            leaves = new Leaf[lying + falling];
            mesh = new Mesh { name = "Leaf" };
            mesh.vertices = new[] { new Vector3(-0.04f, 0f, -0.05f), new Vector3(0.04f, 0f, -0.05f), new Vector3(-0.04f, 0f, 0.05f), new Vector3(0.04f, 0f, 0.05f) };
            mesh.triangles = new[] { 0, 2, 1, 1, 2, 3 };
            mesh.RecalculateNormals();
            mesh.bounds = new Bounds(Vector3.zero, Vector3.one * 0.2f);
            material = RuntimeMaterials.Lit("Dead leaf", new Color(0.22f, 0.13f, 0.06f), 0.15f, true);
            material.enableInstancing = true;
            var cam = Camera.main;
            Vector3 c = cam != null ? cam.transform.position : Vector3.zero;
            for (int i = 0; i < leaves.Length; i++) Respawn(i, c, true);
        }

        void OnDestroy()
        {
            if (mesh != null) Destroy(mesh);
            if (material != null) Destroy(material);
        }

        void Respawn(int i, Vector3 c, bool anywhere)
        {
            ref var l = ref leaves[i];
            float a = Random.Range(0f, Mathf.PI * 2f), r = (anywhere ? Mathf.Sqrt(Random.value) : Random.Range(0.85f, 1f)) * Reach;
            var p = new Vector3(c.x + Mathf.Cos(a) * r, c.y, c.z + Mathf.Sin(a) * r);
            l.Ground = Physics.Raycast(p + Vector3.up * 20f, Vector3.down, out var hit, 50f, Layers.Mask(Layers.Ground), QueryTriggerInteraction.Ignore) ? hit.point.y + 0.01f : c.y - 1.7f;
            bool fromCanopy = i >= lying;
            p.y = fromCanopy ? l.Ground + Random.Range(anywhere ? 0.5f : 7f, 11f) : l.Ground;
            l.Pos = p;
            l.Vel = Vector3.zero;
            l.Flying = fromCanopy;
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
            var bounds = new Bounds(c, Vector3.one * (Reach * 2f + 30f));
            for (int i = 0; i < leaves.Length; i++)
            {
                ref var l = ref leaves[i];
                float dx = l.Pos.x - c.x, dz = l.Pos.z - c.z;
                if (dx * dx + dz * dz > Reach * Reach * 1.2f) { Respawn(i, c, false); continue; }
                Vector2 push = wind != null ? wind.At(l.Pos.x, l.Pos.z) : Vector2.zero;
                if (!l.Flying)
                {
                    // A strong gust, or your running feet, lift it.
                    float fx = l.Pos.x - feet.x, fz = l.Pos.z - feet.z;
                    bool kicked = running && fx * fx + fz * fz < 1.4f;
                    if (kicked || (push.sqrMagnitude > 0.9f && Random.value < dt * 0.8f))
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
                        if (i >= lying) { Respawn(i, c, false); continue; }
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
        }
    }
}
