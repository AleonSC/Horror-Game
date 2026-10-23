using Game.Core;
using Game.Rendering;
using UnityEngine;
using UnityEngine.Rendering.HighDefinition;

namespace Game.Entities
{
    /// <summary>
    /// A hole torn in the air (section 6.7): ragged, black, its edge burning red. It opens, holds, and shuts.
    /// A scour is drawn back through one when it's driven off, and drags you through one when it takes you.
    /// </summary>
    public sealed class ScourPortal : MonoBehaviour
    {
        Material black, rim;
        Light glow;
        Vector2 size;
        float openTime;
        float age;
        float closeAt = -1f;
        const float CloseTime = 0.5f;

        /// <summary>Open a portal at <paramref name="centre"/>, its face looking along <paramref name="facing"/>.</summary>
        public static ScourPortal Open(Vector3 centre, Vector3 facing, Vector2 size, float openTime)
        {
            var go = new GameObject("Portal") { layer = Layers.Effects };
            go.transform.SetPositionAndRotation(centre, Quaternion.LookRotation(facing.sqrMagnitude > 0.001f ? facing : Vector3.forward));
            var portal = go.AddComponent<ScourPortal>();
            portal.size = size;
            portal.openTime = Mathf.Max(0.05f, openTime);
            portal.Build();
            portal.Apply(0f);
            return portal;
        }

        void Build()
        {
            int seed = Random.Range(0, 10000);
            black = RuntimeMaterials.Unlit("Portal", Color.black, true);
            rim = RuntimeMaterials.Glow("Portal rim", new Color(1f, 0.08f, 0.02f) * 6f, true);
            Disc("Rim", rim, 1.12f, 0.02f, seed);
            Disc("Hole", black, 1f, 0f, seed);

            var light = new GameObject("Glow");
            light.transform.SetParent(transform, false);
            light.transform.localPosition = Vector3.forward * 0.3f;
            light.AddHDLight(LightType.Point);
            glow = light.GetComponent<Light>();
            glow.color = new Color(1f, 0.1f, 0.03f);
            glow.range = 4f;
            glow.shadows = LightShadows.None;
            light.AddComponent<FogGlowLight>().strength = 1f;
        }

        // A ragged disc: radius wobbling round the edge.
        void Disc(string name, Material mat, float scale, float back, int seed)
        {
            const int segments = 40;
            var verts = new Vector3[segments + 1];
            var tris = new int[segments * 3];
            verts[0] = Vector3.zero;
            for (int i = 0; i < segments; i++)
            {
                float a = i / (float)segments * Mathf.PI * 2f;
                float r = scale * (0.85f + 0.15f * Mathf.PerlinNoise(seed + Mathf.Cos(a) * 2f, seed + Mathf.Sin(a) * 2f) + 0.06f * Mathf.Sin(a * 7f + seed));
                verts[i + 1] = new Vector3(Mathf.Cos(a) * r * 0.5f, Mathf.Sin(a) * r * 0.5f, 0f);
                tris[i * 3] = 0;
                tris[i * 3 + 1] = i + 1;
                tris[i * 3 + 2] = (i + 1) % segments + 1;
            }
            var mesh = new Mesh { name = name, vertices = verts, triangles = tris };
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            var go = new GameObject(name) { layer = Layers.Effects };
            go.transform.SetParent(transform, false);
            go.transform.localPosition = Vector3.back * back;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r2 = go.AddComponent<MeshRenderer>();
            r2.sharedMaterial = mat;
            r2.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        public void Close()
        {
            if (closeAt < 0f) closeAt = age;
        }

        void Update()
        {
            age += Time.deltaTime;
            float open = Mathf.SmoothStep(0f, 1f, age / openTime);
            if (closeAt >= 0f)
            {
                float c = (age - closeAt) / CloseTime;
                open *= 1f - Mathf.Clamp01(c);
                if (c >= 1f)
                {
                    Destroy(gameObject);
                    return;
                }
            }
            Apply(open);
        }

        void Apply(float open)
        {
            // It tears open from a slit, wider than tall at first.
            float w = size.x * Mathf.Lerp(0.15f, 1f, open) * (1f + 0.04f * Mathf.Sin(age * 23f));
            float h = size.y * open;
            transform.localScale = new Vector3(w, Mathf.Max(0.01f, h), 1f);
            float flicker = 0.85f + 0.15f * Mathf.PerlinNoise(age * 8f, 0.3f);
            RuntimeMaterials.SetGlow(rim, new Color(1f, 0.08f, 0.02f) * 6f * flicker * open);
            if (glow != null) glow.intensity = 3f * open * flicker;
        }

        void OnDestroy()
        {
            if (black != null) Destroy(black);
            if (rim != null) Destroy(rim);
            foreach (var mf in GetComponentsInChildren<MeshFilter>()) Destroy(mf.sharedMesh);
        }
    }
}
