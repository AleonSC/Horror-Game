using Game.Core;
using Game.Rendering;
using UnityEngine;

namespace Game.World
{
    /// <summary>
    /// Smoke rising off a campsite's dying fire (section 6.6): soft grey puffs drifting up, slowly swelling and
    /// thinning out, leaning and carried off with the wind. They're lit like anything else, so they show in your
    /// flashlight and the embers' glow, and hardly at all in the dark.
    /// </summary>
    public sealed class CampSmoke : MonoBehaviour
    {
        // A thin column off the fire, rising faster as it goes, spreading and carried off by the wind, up past the
        // treetops into the sky (about 80 m up by the time each puff is gone): seen from a long way off.
        const int Puffs = 60;
        const float Life = 45f, Rise = 0.6f, Lift = 0.025f;

        static Mesh quad;
        static Material material;
        static Texture2D puffTexture;
        static readonly int ColorId = Shader.PropertyToID("_BaseColor");

        readonly Transform[] puffs = new Transform[Puffs];
        readonly Renderer[] renderers = new Renderer[Puffs];
        readonly float[] age = new float[Puffs], spin = new float[Puffs], size = new float[Puffs];
        readonly Vector3[] drift = new Vector3[Puffs];
        MaterialPropertyBlock block;

        void Start()
        {
            if (material == null)
            {
                var q = GameObject.CreatePrimitive(PrimitiveType.Quad);
                quad = q.GetComponent<MeshFilter>().sharedMesh;
                Destroy(q);
                puffTexture = PuffTexture();
                material = RuntimeMaterials.LitBlended("Smoke", new Color(0.32f, 0.32f, 0.33f), 0f, puffTexture);
                RuntimeMaterials.SetWisp(material);
            }
            block = new MaterialPropertyBlock();
            for (int i = 0; i < Puffs; i++)
            {
                var go = new GameObject("Puff") { layer = Layers.Effects };
                go.transform.SetParent(transform, false);
                go.AddComponent<MeshFilter>().sharedMesh = quad;
                var r = go.AddComponent<MeshRenderer>();
                r.sharedMaterial = material;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                r.receiveShadows = false;
                r.rayTracingMode = UnityEngine.Experimental.Rendering.RayTracingMode.Off; // (ray traced reflections and light would see the whole quad)
                r.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
                puffs[i] = go.transform;
                renderers[i] = r;
                Respawn(i, Life * i / Puffs); // (already rising when you first see it)
            }
        }

        void Respawn(int i, float startAge)
        {
            age[i] = startAge;
            spin[i] = Random.Range(-6f, 6f);
            size[i] = Random.Range(0.8f, 1.2f);
            drift[i] = new Vector3(Random.Range(-0.08f, 0.08f), 0f, Random.Range(-0.08f, 0.08f));
        }

        void LateUpdate()
        {
            var cam = Camera.main;
            if (cam == null) return;
            bool near = (cam.transform.position - transform.position).sqrMagnitude < 450f * 450f;
            for (int i = 0; i < Puffs; i++) if (renderers[i].enabled != near) renderers[i].enabled = near;
            if (!near) return;
            float dt = Time.deltaTime;
            Vector2 wind = Services.TryGet<Wind>(out var w) ? w.At(transform.position.x, transform.position.z, Time.time) : Vector2.zero;
            for (int i = 0; i < Puffs; i++)
            {
                age[i] += dt;
                if (age[i] >= Life) Respawn(i, 0f);
                float t = age[i] / Life;
                // Up (quicker as it climbs), spreading, and carried off more the higher it gets.
                float a = age[i];
                Vector3 p = Vector3.up * (a * Rise + a * a * Lift) + drift[i] * a + new Vector3(wind.x, 0f, wind.y) * (a * 0.4f + a * a * 0.015f);
                p += new Vector3(Mathf.Sin(a * 0.5f + i), 0f, Mathf.Cos(a * 0.4f + i * 2f)) * (0.1f + 3f * t);
                var puff = puffs[i];
                puff.position = transform.position + p;
                puff.rotation = Quaternion.LookRotation(cam.transform.forward, cam.transform.up) * Quaternion.Euler(0f, 0f, spin[i] * age[i]);
                puff.localScale = Vector3.one * Mathf.Lerp(0.3f, 16f, Mathf.Pow(t, 0.65f)) * size[i];
                // Thickest just after it leaves the fire, thinning as it spreads, gone at the top.
                float alpha = Mathf.Clamp01(t / 0.01f) * Mathf.Pow(1f - t, 1.4f) * 0.45f;
                block.SetColor(ColorId, new Color(0.32f, 0.32f, 0.33f, alpha));
                renderers[i].SetPropertyBlock(block);
            }
        }

        // A soft, uneven puff: thick in the middle, wispy at the edges.
        static Texture2D PuffTexture()
        {
            const int size = 64;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, true) { name = "Smoke puff", wrapMode = TextureWrapMode.Clamp };
            var px = new Color[size * size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float u = (x + 0.5f) / size * 2f - 1f, v = (y + 0.5f) / size * 2f - 1f;
                float d = Mathf.Sqrt(u * u + v * v);
                float wisp = Mathf.PerlinNoise(x * 0.12f, y * 0.12f) * 0.6f + Mathf.PerlinNoise(x * 0.3f + 5f, y * 0.3f) * 0.4f;
                float a = Mathf.Clamp01(1f - d) * Mathf.Clamp01(1f - d);
                a *= Mathf.Lerp(0.5f, 1.2f, wisp);
                px[y * size + x] = new Color(1f, 1f, 1f, Mathf.Clamp01(a));
            }
            tex.SetPixels(px);
            tex.Apply(true);
            return tex;
        }
    }
}
