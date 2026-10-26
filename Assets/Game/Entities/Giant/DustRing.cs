using Game.Core;
using Game.Rendering;
using UnityEngine;

namespace Game.Entities
{
    /// <summary>
    /// An open ring of billowing dust (the Game/ShockDust shader): the shockwave rolling out from the giant's
    /// landing, or the cloud thrown up round its feet. One unit across and one tall; placed and scaled each frame.
    /// </summary>
    public sealed class DustRing : MonoBehaviour
    {
        static Mesh ring;
        Material mat;
        MeshRenderer r;

        public static DustRing Create(Transform parent, string name, float billow)
        {
            var shader = Effects.GameShader("Game/ShockDust");
            if (shader == null) return null;
            var go = new GameObject(name) { layer = Layers.Effects };
            go.transform.SetParent(parent, false);
            var d = go.AddComponent<DustRing>();
            go.AddComponent<MeshFilter>().sharedMesh = Ring();
            d.r = go.AddComponent<MeshRenderer>();
            d.mat = new Material(shader) { name = name };
            d.mat.SetFloat("_Billow", billow);
            d.r.sharedMaterial = d.mat;
            d.r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            d.r.receiveShadows = false;
            go.SetActive(false);
            return d;
        }

        /// <summary>Its foot centred here, this wide (radius) and tall, this colour (already set for the exposure), this thick (0-1).</summary>
        public void Place(Vector3 foot, float radius, float height, Color color, float thickness)
        {
            if (thickness <= 0.001f || radius <= 0f)
            {
                if (gameObject.activeSelf) gameObject.SetActive(false);
                return;
            }
            if (!gameObject.activeSelf) gameObject.SetActive(true);
            transform.SetPositionAndRotation(foot, Quaternion.identity);
            transform.localScale = new Vector3(radius * 2f, height, radius * 2f);
            color.a = Mathf.Clamp01(thickness);
            mat.SetColor("_UnlitColor", color);
        }

        public void SetSoftness(float metres) => mat.SetFloat("_Soft", metres);

        void OnDestroy()
        {
            if (mat != null) Destroy(mat);
        }

        // An open cylinder, radius 0.5, 0 to 1 high: uv.y its height, faces both ways (the shader draws both sides).
        static Mesh Ring()
        {
            if (ring != null) return ring;
            const int around = 160, up = 6;
            var verts = new Vector3[(around + 1) * (up + 1)];
            var uvs = new Vector2[verts.Length];
            for (int j = 0; j <= up; j++)
                for (int i = 0; i <= around; i++)
                {
                    float a = i / (float)around * Mathf.PI * 2f;
                    float h = j / (float)up;
                    verts[j * (around + 1) + i] = new Vector3(Mathf.Cos(a) * 0.5f, h, Mathf.Sin(a) * 0.5f);
                    uvs[j * (around + 1) + i] = new Vector2(i / (float)around, h);
                }
            var tris = new int[around * up * 6];
            int k = 0;
            for (int j = 0; j < up; j++)
                for (int i = 0; i < around; i++)
                {
                    int a = j * (around + 1) + i, b = a + 1, c = a + around + 1, d = c + 1;
                    tris[k++] = a; tris[k++] = c; tris[k++] = b;
                    tris[k++] = b; tris[k++] = c; tris[k++] = d;
                }
            ring = new Mesh { name = "Dust ring", vertices = verts, uv = uvs, triangles = tris };
            ring.RecalculateBounds();
            return ring;
        }
    }
}
