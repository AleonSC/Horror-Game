using System.Collections.Generic;
using Game.Core;
using Game.Rendering;
using UnityEngine;
using UnityEngine.Rendering.HighDefinition;

namespace Game.Player
{
    /// <summary>
    /// Luminescent spray paint: hold F with the can out and it sprays where you're looking (within reach), leaving
    /// soft-edged, speckled blots of pale green paint that glow faintly in the dark, a trail you can find your way
    /// back by. Each blot sticks to what it landed on (a trunk, a rock, the ground, a wall) and moves with it. The
    /// can runs out; the oldest marks fade when there are too many.
    /// </summary>
    public static class SprayPaint
    {
        static readonly Queue<GameObject> marks = new Queue<GameObject>();
        static Material paint;
        static Mesh quad;
        static float lastMark;

        /// <summary>Spray from the eye this frame. Returns how much paint was used (0 if it hit nothing).</summary>
        public static float Spray(Transform eye, ItemSettings s, float dt)
        {
            if (Time.time - lastMark < s.sprayInterval) return 0f;
            int mask = ~(Layers.Mask(Layers.PlayerBody) | Layers.Mask(Layers.Effects) | Layers.Mask(Layers.Pickups) | Layers.Mask(Layers.Entities));
            // (a little scatter, as from a nozzle)
            Vector3 dir = Quaternion.Euler(Random.Range(-1.5f, 1.5f), Random.Range(-1.5f, 1.5f), 0f) * eye.forward;
            if (!Physics.Raycast(eye.position, dir, out var hit, s.sprayReach, mask, QueryTriggerInteraction.Ignore)) return 0f;
            lastMark = Time.time;
            Mark(hit, s);
            return s.sprayPerMark;
        }

        static void Mark(RaycastHit hit, ItemSettings s)
        {
            if (paint == null)
            {
                var blot = Blot();
                paint = RuntimeMaterials.LitBlended("Luminescent paint", s.sprayColor, 0.3f, blot, 3);
                paint.SetTexture("_EmissiveColorMap", blot);
                HDMaterial.SetEmissiveColor(paint, s.sprayColor * s.sprayGlow);
                paint.SetFloat("_EnableBlendModePreserveSpecularLighting", 0f);
                HDMaterial.ValidateMaterial(paint);
                var q = GameObject.CreatePrimitive(PrimitiveType.Quad);
                quad = q.GetComponent<MeshFilter>().sharedMesh;
                Object.Destroy(q);
            }
            var go = new GameObject("Paint") { layer = Layers.Effects };
            // (stuck to what it hit, so it moves, wraps and goes with it)
            go.transform.SetParent(hit.collider.transform, true);
            go.transform.position = hit.point + hit.normal * 0.006f;
            go.transform.rotation = Quaternion.LookRotation(-hit.normal) * Quaternion.Euler(0f, 0f, Random.Range(0f, 360f));
            float size = Random.Range(0.7f, 1.2f) * s.spraySize;
            Vector3 parent = go.transform.parent != null ? go.transform.parent.lossyScale : Vector3.one;
            go.transform.localScale = new Vector3(size / Mathf.Max(0.001f, parent.x), size / Mathf.Max(0.001f, parent.y), 1f / Mathf.Max(0.001f, parent.z));
            go.AddComponent<MeshFilter>().sharedMesh = quad;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = paint;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            marks.Enqueue(go);
            while (marks.Count > s.maxSprayMarks)
            {
                var old = marks.Dequeue();
                if (old != null) Object.Destroy(old);
            }
        }

        /// <summary>A new run: the paint's gone.</summary>
        public static void ClearAll()
        {
            while (marks.Count > 0)
            {
                var m = marks.Dequeue();
                if (m != null) Object.Destroy(m);
            }
        }

        // A blot of spray: dense in the middle, speckled and soft at its edges.
        static Texture2D Blot()
        {
            const int n = 64;
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, true) { name = "Spray blot", wrapMode = TextureWrapMode.Clamp };
            var px = new Color[n * n];
            var rng = new System.Random(9);
            for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float u = (x + 0.5f) / n * 2f - 1f, v = (y + 0.5f) / n * 2f - 1f;
                float d = Mathf.Sqrt(u * u + v * v);
                float core = Mathf.Clamp01(1f - d * 1.25f);
                float speck = rng.NextDouble() < Mathf.Clamp01(1.1f - d) * 0.6f ? 1f : 0f;
                float a = Mathf.Clamp01(core * core * 1.3f + speck * (1f - d) * 0.7f) * (d < 1f ? 1f : 0f);
                px[y * n + x] = new Color(1f, 1f, 1f, a);
            }
            tex.SetPixels(px);
            tex.Apply(true);
            return tex;
        }
    }
}
