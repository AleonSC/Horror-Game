using Game.Core;
using Game.Rendering;
using Game.World.Build;
using UnityEngine;

namespace Game.Entities
{
    /// <summary>
    /// The world coming apart round you (a scour has you): motes taken from the surfaces all round (the ground,
    /// the trunks, the branches) peel off as the dark reaches them, from the far edge inward, and lift and scatter
    /// on the wind like a dismissed scour, shrinking away. <see cref="Sight"/> is how far the dark has come.
    /// </summary>
    public sealed class WorldMotes : MonoBehaviour
    {
        Vector3[] pos, vel;
        float[] size, at, life, dist;
        float age, duration, reach;
        Mesh quad;
        Material ash;
        readonly Matrix4x4[] batch = new Matrix4x4[InstancedDraw.MaxPerCall];
        Vector3 centre;

        /// <summary>How far you can still see (metres): shrinking from the reach to nothing over the duration.</summary>
        public float Sight => Mathf.Lerp(reach, 0.8f, Ease(Mathf.Clamp01(age / duration)));

        static float Ease(float x) => x * x * (3f - 2f * x);

        public static WorldMotes Begin(Vector3 centre, float reach, int count, float duration)
        {
            var go = new GameObject("The world coming apart") { layer = Layers.Effects };
            var m = go.AddComponent<WorldMotes>();
            m.Build(centre, Mathf.Max(2f, reach), Mathf.Max(0, count), Mathf.Max(0.5f, duration));
            return m;
        }

        void Build(Vector3 c, float r, int count, float time)
        {
            centre = c;
            reach = r;
            duration = time;
            int mask = ~(Layers.Mask(Layers.Effects) | Layers.Mask(Layers.PlayerBody) | Layers.Mask(Layers.Entities)); // (you're inside your own capsule: rays don't hit it)
            var found = new System.Collections.Generic.List<(Vector3 p, float d)>(count);
            for (int i = 0; i < count * 3 && found.Count < count; i++)
            {
                // Every way round you, more of them low (the ground and the trunks fill most of the view).
                Vector3 dir = Random.onUnitSphere;
                dir.y = dir.y * 0.7f - 0.2f;
                if (Physics.Raycast(c, dir.normalized, out var hit, r, mask, QueryTriggerInteraction.Ignore))
                    found.Add((hit.point + hit.normal * 0.05f, hit.distance));
            }
            int n = found.Count;
            pos = new Vector3[n];
            vel = new Vector3[n];
            size = new float[n];
            at = new float[n];
            life = new float[n];
            dist = new float[n];
            Vector2 wind = Services.TryGet<Wind>(out var w) ? w.At(c.x, c.z, Time.time) : new Vector2(0.4f, 0.2f);
            Vector3 drift = new Vector3(wind.x, 0f, wind.y).normalized * Mathf.Lerp(1f, 3f, Mathf.Clamp01(wind.magnitude));
            for (int i = 0; i < n; i++)
            {
                pos[i] = found[i].p;
                dist[i] = found[i].d;
                Vector3 away = pos[i] - c;
                away.y = 0f;
                vel[i] = away.normalized * Random.Range(0.1f, 0.6f) + Vector3.up * Random.Range(0.4f, 1.4f) + drift * Random.Range(0.4f, 1.1f);
                // Far ones bigger, so they read at a distance.
                size[i] = Random.Range(0.04f, 0.09f) * Mathf.Lerp(1f, 3.5f, dist[i] / r);
                life[i] = Random.Range(1.2f, 2.6f);
                // Each goes when the dark reaches it (with a little scatter).
                float y = Mathf.InverseLerp(r, 0.8f, dist[i]);
                float when = 0.5f - Mathf.Sin(Mathf.Asin(1f - 2f * y) / 3f); // (the time Sight reaches it: smoothstep undone)
                at[i] = Mathf.Clamp01(when + Random.Range(-0.05f, 0.02f));
            }

            quad = new Mesh { name = "World mote" };
            quad.vertices = new[] { new Vector3(-0.5f, -0.5f, 0f), new Vector3(0.5f, -0.5f, 0f), new Vector3(-0.5f, 0.5f, 0f), new Vector3(0.5f, 0.5f, 0f) };
            quad.triangles = new[] { 0, 2, 1, 1, 2, 3 };
            quad.RecalculateBounds();
            // Pale ash, a little lighter than the night (it stays clear through the dark that's coming).
            float mul = Services.TryGet<LookController>(out var look) ? LookController.ExposureMultiplier(look.Config.settings.exposure) : 1f;
            ash = RuntimeMaterials.Haze("World motes", Color.black);
            RuntimeMaterials.SetHazeColor(ash, new Color(0.3f, 0.32f, 0.36f) / mul);
            ash.enableInstancing = true;
        }

        void OnDestroy()
        {
            if (quad != null) Destroy(quad);
            if (ash != null) Destroy(ash);
        }

        void Update()
        {
            float dt = Time.deltaTime;
            age += dt;
            var cam = Camera.main;
            Quaternion face = cam != null ? cam.transform.rotation : Quaternion.identity;
            var bounds = new Bounds(centre, Vector3.one * (reach * 2f + 40f));
            float now = age / duration;
            int n = 0;
            for (int i = 0; i < pos.Length; i++)
            {
                if (now < at[i]) continue;
                float t = (age - at[i] * duration) / life[i];
                if (t >= 1f) continue;
                vel[i] += new Vector3(Mathf.Sin(age * 2.3f + i), 0.4f, Mathf.Cos(age * 2f + i * 1.7f)) * dt * 0.9f;
                vel[i] *= 1f - dt * 0.5f;
                pos[i] += vel[i] * dt;
                // Swelling a moment as it comes loose, then shrinking away.
                float sz = size[i] * Mathf.Sin(Mathf.Clamp01(t) * Mathf.PI) * 1.3f;
                batch[n++] = Matrix4x4.TRS(pos[i], face * Quaternion.Euler(0f, 0f, i * 37f + age * 70f), Vector3.one * sz);
                if (n == batch.Length)
                {
                    InstancedDraw.Draw(quad, 0, ash, batch, n, bounds, Layers.Effects, false);
                    n = 0;
                }
            }
            if (n > 0) InstancedDraw.Draw(quad, 0, ash, batch, n, bounds, Layers.Effects, false);
        }
    }
}
