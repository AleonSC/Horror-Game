using Game.Core;
using Game.World.Build;
using UnityEngine;

namespace Game.World
{
    /// <summary>
    /// Rain (section 6.13): streaks falling in a box round the camera, drawn instanced. Each drop knows where it
    /// will land (found when it starts), so rain stops at roofs and tree trunks. The wind blows it slant.
    /// </summary>
    public sealed class Rain
    {
        const float BoxSize = 34f, BoxHeight = 18f;
        readonly Vector3[] drops;
        readonly float[] landAt;
        readonly Matrix4x4[] matrices = new Matrix4x4[InstancedDraw.MaxPerCall];
        readonly Mesh mesh;
        readonly Material material;
        readonly int mask = Layers.Mask(Layers.Ground) | Layers.Mask(Layers.Props) | Layers.Mask(Layers.Trees);
        int live;

        public Rain(int maxDrops)
        {
            drops = new Vector3[maxDrops];
            landAt = new float[maxDrops];
            mesh = new Mesh { name = "Raindrop" };
            mesh.vertices = new[] { new Vector3(-0.006f, 0f, 0f), new Vector3(0.006f, 0f, 0f), new Vector3(-0.006f, 0.45f, 0f), new Vector3(0.006f, 0.45f, 0f) };
            mesh.triangles = new[] { 0, 2, 1, 1, 2, 3 };
            mesh.RecalculateNormals();
            mesh.bounds = new Bounds(Vector3.up * 0.2f, Vector3.one);
            material = Game.Rendering.RuntimeMaterials.Haze("Rain", new Color(0.6f, 0.65f, 0.7f));
            material.SetFloat("_EnableFogOnTransparent", 1f); // (rain should fade in the fog like anything else)
            material.enableInstancing = true;
            UnityEngine.Rendering.HighDefinition.HDMaterial.ValidateMaterial(material);
        }

        public void Destroy()
        {
            Object.Destroy(mesh);
            Object.Destroy(material);
        }

        /// <summary>Fall, and draw. <paramref name="amount"/> 0 dry to 1 a downpour.</summary>
        public void Update(Camera cam, float amount, WeatherSettings s, Wind wind, float exposureMul)
        {
            if (cam == null) return;
            int want = Mathf.RoundToInt(drops.Length * Mathf.Clamp01(amount));
            Vector3 c = cam.transform.position;
            while (live < want) Spawn(live++, c, true);
            live = Mathf.Min(live, want);
            if (live == 0) return;

            float dt = Time.deltaTime;
            Vector2 push = wind != null ? wind.At(c.x, c.z, Time.time) : Vector2.zero;
            Vector3 fall = new Vector3(push.x * 4f, -s.dropSpeed, push.y * 4f);
            // Leaning with the wind, turned to face you.
            Vector3 toCam = new Vector3(0f, 0f, 1f);
            var tilt = Quaternion.FromToRotation(Vector3.up, -fall.normalized);
            Color col = new Color(0.6f, 0.65f, 0.7f) * s.dropBrightness / exposureMul;
            col.a = Mathf.Clamp01(s.dropBrightness * 1.5f);
            material.SetColor("_UnlitColor", col);

            int n = 0;
            for (int i = 0; i < live; i++)
            {
                Vector3 p = drops[i] + fall * dt;
                float dx = p.x - c.x, dz = p.z - c.z;
                if (p.y < landAt[i] || Mathf.Abs(dx) > BoxSize * 0.5f || Mathf.Abs(dz) > BoxSize * 0.5f) { Spawn(i, c, false); p = drops[i]; }
                drops[i] = p;
                toCam.x = c.x - p.x;
                toCam.z = c.z - p.z;
                var face = Quaternion.LookRotation(toCam.sqrMagnitude > 0.01f ? toCam : Vector3.forward);
                matrices[n++] = Matrix4x4.TRS(p, tilt * face, Vector3.one);
                if (n == matrices.Length)
                {
                    Flush(n, c);
                    n = 0;
                }
            }
            if (n > 0) Flush(n, c);
        }

        void Flush(int n, Vector3 c) => InstancedDraw.Draw(mesh, 0, material, matrices, n, new Bounds(c, Vector3.one * (BoxSize + BoxHeight * 2f)), Layers.Effects, false);

        // A new drop somewhere above (or, at first, anywhere in the box), and where it'll land.
        void Spawn(int i, Vector3 c, bool anywhere)
        {
            var p = new Vector3(c.x + Random.Range(-0.5f, 0.5f) * BoxSize, c.y + (anywhere ? Random.Range(-4f, BoxHeight) : BoxHeight * Random.Range(0.7f, 1f)), c.z + Random.Range(-0.5f, 0.5f) * BoxSize);
            drops[i] = p;
            landAt[i] = Physics.Raycast(p, Vector3.down, out var hit, BoxHeight * 2f + 30f, mask, QueryTriggerInteraction.Ignore) ? hit.point.y : c.y - 30f;
        }
    }
}
