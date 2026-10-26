using System.Collections.Generic;
using Game.Core;
using Game.Rendering;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

namespace Game.Entities
{
    /// <summary>
    /// What a red bolt leaves where it struck: branching burn lines spreading over the ground from the spot (like
    /// lightning's own fern-shaped scars), glowing red-hot and cooling, a dim red light on the ground under them,
    /// all gone in a few seconds.
    /// </summary>
    public sealed class StrikeScorch : MonoBehaviour
    {
        Material mat;
        Light glowLight;
        Color color;
        float life, left;

        /// <summary>Burn the ground here, glowing this colour, fading over this many seconds.</summary>
        public static StrikeScorch Burn(Vector3 at, Color color, float seconds, Transform parent = null)
        {
            var go = new GameObject("Strike scorch") { layer = Layers.Effects };
            go.transform.SetParent(parent, false);
            go.transform.position = at;
            var s = go.AddComponent<StrikeScorch>();
            s.Build(at, color, seconds);
            return s;
        }

        void Build(Vector3 at, Color c, float seconds)
        {
            color = c;
            life = left = Mathf.Max(0.1f, seconds);
            var verts = new List<Vector3>();
            var tris = new List<int>();
            int branches = Random.Range(6, 9);
            float start = Random.value * 360f;
            for (int i = 0; i < branches; i++)
                Branch(at, start + i * 360f / branches + Random.Range(-18f, 18f), Random.Range(2f, 3.6f), 0.09f, 0, verts, tris);

            var mesh = new Mesh { name = "Strike scorch" };
            mesh.SetVertices(verts);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            gameObject.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = gameObject.AddComponent<MeshRenderer>();
            mat = RuntimeMaterials.LitGlow("Strike scorch", new Color(0.02f, 0.015f, 0.012f), Color.black);
            r.sharedMaterial = mat;
            r.shadowCastingMode = ShadowCastingMode.Off;
            gameObject.AddComponent<NoLightBounce>();

            var lightGo = new GameObject("Scorch light");
            lightGo.transform.SetParent(transform, false);
            lightGo.transform.position = at + Vector3.up * 0.35f;
            lightGo.AddHDLight(LightType.Point);
            glowLight = lightGo.GetComponent<Light>();
            glowLight.color = c;
            glowLight.range = 4f;
            glowLight.shadows = LightShadows.None;
            Fade();
        }

        // One burn line wandering out from p, forking now and then, thinning as it goes.
        void Branch(Vector3 p, float heading, float length, float width, int depth, List<Vector3> verts, List<int> tris)
        {
            const float step = 0.22f;
            Vector3 origin = transform.position;
            Vector3 prev = p;
            float w = width;
            for (float d = 0f; d < length && w > 0.006f; d += step)
            {
                heading += Random.Range(-28f, 28f);
                Vector3 dir = Quaternion.Euler(0f, heading, 0f) * Vector3.forward;
                Vector3 next = prev + dir * step;
                next.y = PlayerSense.GroundHeight(next) + 0.025f;
                float nw = w * 0.9f;
                Vector3 side = Vector3.Cross(Vector3.up, dir).normalized;
                int b = verts.Count;
                verts.Add(prev - origin - side * w * 0.5f);
                verts.Add(prev - origin + side * w * 0.5f);
                verts.Add(next - origin - side * nw * 0.5f);
                verts.Add(next - origin + side * nw * 0.5f);
                tris.AddRange(new[] { b, b + 2, b + 1, b + 1, b + 2, b + 3 });
                if (depth < 2 && Random.value < 0.22f)
                    Branch(next, heading + (Random.value < 0.5f ? -1f : 1f) * Random.Range(30f, 60f), (length - d) * Random.Range(0.4f, 0.7f), nw * 0.7f, depth + 1, verts, tris);
                prev = next;
                w = nw;
            }
        }

        void Update()
        {
            left -= Time.deltaTime;
            if (left <= 0f) { Destroy(gameObject); return; }
            Fade();
        }

        void Fade()
        {
            float k = left / life;
            float flicker = 0.85f + 0.15f * Mathf.PerlinNoise(Time.time * 6f, 0.3f);
            RuntimeMaterials.SetGlow(mat, color * 5f * k * k * flicker);
            glowLight.intensity = 25f * k * k * flicker;
        }

        void OnDestroy()
        {
            if (mat != null) Destroy(mat);
            var f = GetComponent<MeshFilter>();
            if (f != null && f.sharedMesh != null) Destroy(f.sharedMesh);
        }
    }
}
