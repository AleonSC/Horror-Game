using Game.Rendering;
using UnityEngine;

namespace Game.World
{
    /// <summary>
    /// A tornado on the horizon (from the 8th lamp): a vast twisting funnel from the clouds to the ground, hundreds of
    /// metres away, drifting slowly. Like the giant, it's drawn nearer and scaled down so the camera's range never
    /// clips it, in a haze that keeps it a silhouette through the fog.
    /// </summary>
    public sealed class Tornado : MonoBehaviour
    {
        const float RenderDistance = 360f; // (its top stays inside the camera's 600 m far clip)
        const int Segments = 14;

        readonly Transform[] rings = new Transform[Segments];
        Material haze;
        float bearing, distance, height, spin, drift;

        public static Tornado Create(Transform parent, float bearing, float distance, float height, int seed)
        {
            var go = new GameObject("Tornado") { layer = Game.Core.Layers.Effects };
            go.transform.SetParent(parent, false);
            var t = go.AddComponent<Tornado>();
            t.bearing = bearing;
            t.distance = distance;
            t.height = height;
            t.drift = (seed % 2 == 0 ? 1f : -1f) * (0.4f + (seed % 5) * 0.1f);
            t.Build();
            return t;
        }

        void Build()
        {
            haze = RuntimeMaterials.Silhouette("Tornado");
            for (int i = 0; i < Segments; i++)
            {
                var ring = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                Destroy(ring.GetComponent<Collider>());
                ring.layer = Game.Core.Layers.Effects;
                ring.transform.SetParent(transform, false);
                var r = ring.GetComponent<Renderer>();
                r.sharedMaterial = haze;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                rings[i] = ring.transform;
            }
        }

        void OnDestroy()
        {
            if (haze != null) Destroy(haze);
        }

        /// <summary>Place and animate it round the viewer; <paramref name="shade"/>: how much of the fog behind it shows.</summary>
        public void Tick(Vector3 viewer, float shade, float dt)
        {
            RuntimeMaterials.SetSilhouetteShade(haze, shade);
            spin += dt;
            bearing += drift * dt * 0.15f;
            float a = bearing * Mathf.Deg2Rad;
            Vector3 dir = new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a));
            float r = Mathf.Min(distance, RenderDistance);
            float k = r / distance;
            transform.position = viewer + dir * r - Vector3.up * 20f * k;
            // Narrow at the ground, wide at the cloud base, the column snaking as it turns.
            for (int i = 0; i < Segments; i++)
            {
                float u = i / (Segments - 1f);
                float width = Mathf.Lerp(18f, 170f, u * u) * k;
                float y = u * height * k;
                float sway = Mathf.Sin(spin * 0.7f + u * 3.2f) * 30f * u * k + Mathf.Sin(spin * 1.9f + u * 7f) * 6f * k;
                var ring = rings[i];
                ring.localPosition = new Vector3(sway, y, Mathf.Cos(spin * 0.5f + u * 2.4f) * 15f * u * k);
                ring.localScale = new Vector3(width, height * k / Segments * 0.62f, width);
                ring.localRotation = Quaternion.Euler(0f, spin * 90f * (1.5f - u), Mathf.Sin(spin + u * 4f) * 4f);
            }
        }
    }
}
