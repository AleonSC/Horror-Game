using Game.Core;
using Game.Rendering;
using Game.World.Build;
using UnityEngine;

namespace Game.Entities
{
    /// <summary>
    /// A scour coming apart (driven off): its shape breaks into hundreds of dark motes, taken from its body where
    /// it stood, that lift and scatter on the wind, shrinking away. Its two eyes linger a moment longest, then go out.
    /// </summary>
    public sealed class ScourMotes : MonoBehaviour
    {
        const int Count = 420;

        Vector3[] pos, vel;
        float[] size, life;
        float age, duration;
        Mesh quad;
        Material black;
        readonly Matrix4x4[] batch = new Matrix4x4[InstancedDraw.MaxPerCall];
        Transform[] eyes;
        Material eyeMat;
        Color eyeColor;

        /// <summary>Break this body apart into motes (taken from its shape as it stands now).</summary>
        public static void Burst(ScourLook body, float duration, float blue)
        {
            var points = body.SamplePoints(Count);
            if (points.Length == 0) return;
            var go = new GameObject("Scour coming apart") { layer = Layers.Effects };
            var m = go.AddComponent<ScourMotes>();
            m.duration = Mathf.Max(0.3f, duration);
            m.Build(points, body, blue);
        }

        void Build(Vector3[] points, ScourLook body, float blue)
        {
            int n = points.Length;
            pos = points;
            vel = new Vector3[n];
            size = new float[n];
            life = new float[n];
            Vector3 centre = Vector3.zero;
            foreach (var p in points) centre += p;
            centre /= n;
            Vector2 wind = Services.TryGet<Wind>(out var w) ? w.At(centre.x, centre.z, Time.time) : new Vector2(0.4f, 0.2f);
            Vector3 drift = new Vector3(wind.x, 0f, wind.y).normalized * Mathf.Lerp(1.2f, 3.5f, Mathf.Clamp01(wind.magnitude));
            for (int i = 0; i < n; i++)
            {
                Vector3 out_ = (pos[i] - centre);
                out_.y *= 0.3f;
                // Outward a little, up, and away downwind; the top goes first, the feet last.
                float height = Mathf.InverseLerp(centre.y - 1.3f, centre.y + 1.3f, pos[i].y);
                vel[i] = out_.normalized * Random.Range(0.2f, 0.9f) + Vector3.up * Random.Range(0.3f, 1.2f) + drift * Random.Range(0.5f, 1.2f);
                size[i] = Random.Range(0.03f, 0.075f);
                life[i] = Random.Range(0.55f, 1f) * duration * Mathf.Lerp(1.1f, 0.75f, height);
            }

            quad = new Mesh { name = "Mote" };
            quad.vertices = new[] { new Vector3(-0.5f, -0.5f, 0f), new Vector3(0.5f, -0.5f, 0f), new Vector3(-0.5f, 0.5f, 0f), new Vector3(0.5f, 0.5f, 0f) };
            quad.triangles = new[] { 0, 2, 1, 1, 2, 3 };
            quad.RecalculateBounds();
            black = RuntimeMaterials.Unlit("Scour motes", Color.black, true);
            black.enableInstancing = true;

            // The eyes, left hanging in the air a moment.
            eyeColor = Color.Lerp(new Color(1f, 0.02f, 0f), new Color(0.3f, 0.6f, 1f), blue) * 3f;
            eyeMat = RuntimeMaterials.Glow("Fading eyes", eyeColor);
            eyes = new Transform[2];
            Vector3 eyePoint = body.EyePoint;
            Vector3 side = body.transform.right * 0.045f * body.transform.localScale.x;
            for (int e = 0; e < 2; e++)
            {
                var s = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                Destroy(s.GetComponent<Collider>());
                s.transform.SetParent(transform, false);
                s.transform.position = eyePoint + side * (e == 0 ? -1f : 1f);
                s.transform.localScale = Vector3.one * 0.035f;
                s.GetComponent<Renderer>().sharedMaterial = eyeMat;
                eyes[e] = s.transform;
            }
        }

        void OnDestroy()
        {
            if (quad != null) Destroy(quad);
            if (black != null) Destroy(black);
            if (eyeMat != null) Destroy(eyeMat);
        }

        void Update()
        {
            float dt = Time.deltaTime;
            age += dt;
            var cam = Camera.main;
            Quaternion face = cam != null ? cam.transform.rotation : Quaternion.identity;
            int n = 0;
            for (int i = 0; i < pos.Length; i++)
            {
                float t = age / life[i];
                if (t >= 1f) continue;
                // Carried on the air, slowing a little, curling as they go.
                vel[i] += new Vector3(Mathf.Sin(age * 3f + i), 0.3f, Mathf.Cos(age * 2.6f + i * 1.3f)) * dt * 0.8f;
                vel[i] *= 1f - dt * 0.6f;
                pos[i] += vel[i] * dt;
                float sz = size[i] * (1f - t * t);
                batch[n++] = Matrix4x4.TRS(pos[i], face * Quaternion.Euler(0f, 0f, i * 37f + age * 90f), Vector3.one * sz);
                if (n == batch.Length)
                {
                    InstancedDraw.Draw(quad, 0, black, batch, n, new Bounds(pos[0], Vector3.one * 60f), Layers.Effects, false);
                    n = 0;
                }
            }
            if (n > 0) InstancedDraw.Draw(quad, 0, black, batch, n, new Bounds(pos[0], Vector3.one * 60f), Layers.Effects, false);

            // The eyes hold a moment, then fade and drift apart.
            float eyeFade = 1f - Mathf.SmoothStep(0f, 1f, (age - 0.25f) / 0.6f);
            RuntimeMaterials.SetGlow(eyeMat, eyeColor * eyeFade);
            for (int e = 0; e < eyes.Length; e++)
            {
                eyes[e].position += (Vector3.up * 0.15f + (e == 0 ? -1f : 1f) * Vector3.right * 0.05f) * dt;
                eyes[e].localScale = Vector3.one * 0.035f * Mathf.Max(0.05f, eyeFade);
            }

            if (age >= duration * 1.15f) Destroy(gameObject);
        }
    }
}
