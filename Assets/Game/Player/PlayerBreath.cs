using Game.Core;
using Game.Rendering;
using UnityEngine;

namespace Game.Player
{
    /// <summary>
    /// Your breath steaming in the cold night air: a soft puff from just below the view with each breath out,
    /// drifting forward and up, spreading and fading. Rested, it comes slow and faint; as your stamina runs down
    /// the breaths come quicker and the puffs bigger and thicker, and winded you pant out cloud after cloud. It's
    /// lit like anything else, so it shows in your flashlight's beam and by a lamp, and hardly at all in the dark.
    /// </summary>
    public sealed class PlayerBreath : MonoBehaviour
    {
        const int Puffs = 24;

        static Mesh quad;
        static Material material;
        static Texture2D puffTexture;
        static readonly int ColorId = Shader.PropertyToID("_BaseColor");

        readonly Transform[] puffs = new Transform[Puffs];
        readonly Renderer[] renderers = new Renderer[Puffs];
        readonly float[] age = new float[Puffs], life = new float[Puffs], size = new float[Puffs], thick = new float[Puffs], spin = new float[Puffs];
        readonly Vector3[] velocity = new Vector3[Puffs];
        MaterialPropertyBlock block;
        PlayerController player;
        float nextBreath;
        int next;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void AttachToGame()
        {
            if (Services.TryGet<PlayerController>(out var p) && p.GetComponent<PlayerBreath>() == null) p.gameObject.AddComponent<PlayerBreath>();
        }

        void Start()
        {
            player = GetComponent<PlayerController>();
            if (material == null)
            {
                var q = GameObject.CreatePrimitive(PrimitiveType.Quad);
                quad = q.GetComponent<MeshFilter>().sharedMesh;
                Destroy(q);
                puffTexture = PuffTexture();
                material = RuntimeMaterials.LitBlended("Breath", new Color(0.85f, 0.86f, 0.88f), 0f, puffTexture, 2);
                RuntimeMaterials.SetWisp(material);
            }
            block = new MaterialPropertyBlock();
            var holder = new GameObject("Breath") { layer = Layers.Effects }.transform; // (in the world, not on you: puffs stay where you breathed them)
            holder.SetParent(transform.parent, false); // (kept with the player's own root, whatever loads)
            for (int i = 0; i < Puffs; i++)
            {
                var go = new GameObject("Puff") { layer = Layers.Effects };
                go.transform.SetParent(holder, false);
                go.AddComponent<MeshFilter>().sharedMesh = quad;
                var r = go.AddComponent<MeshRenderer>();
                r.sharedMaterial = material;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                r.receiveShadows = false;
                r.rayTracingMode = UnityEngine.Experimental.Rendering.RayTracingMode.Off; // (ray traced reflections and light would see the whole quad)
                r.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
                r.enabled = false;
                puffs[i] = go.transform;
                renderers[i] = r;
                age[i] = life[i] = 1f;
            }
            nextBreath = Time.time + Random.Range(1f, 3f);
        }

        void OnDestroy()
        {
            if (puffs[0] != null) Destroy(puffs[0].parent.gameObject);
        }

        void Update()
        {
            if (player == null || player.Head == null) return;
            float tired = 1f - Mathf.Clamp01(player.Stamina.Value);
            bool winded = player.Stamina.Winded;
            // Breaths: every 3.5 s rested, every 1.1 s spent, quicker still while winded.
            if (Time.time >= nextBreath && GameFlow.IsPlaying)
            {
                float gap = Mathf.Lerp(3.5f, 1.1f, tired) * (winded ? 0.7f : 1f) * Random.Range(0.85f, 1.15f);
                nextBreath = Time.time + gap;
                int puffsOut = winded ? 3 : tired > 0.5f ? 2 : 1;
                for (int k = 0; k < puffsOut; k++) Breathe(tired, winded, k * 0.12f);
            }

            var cam = player.View != null ? player.View.transform : player.Head;
            float dt = Time.deltaTime;
            for (int i = 0; i < Puffs; i++)
            {
                if (age[i] >= life[i])
                {
                    if (renderers[i].enabled) renderers[i].enabled = false;
                    continue;
                }
                age[i] += dt;
                if (age[i] < 0f) continue; // (waiting its turn in a pant)
                float u = Mathf.Clamp01(age[i] / life[i]);
                velocity[i] *= Mathf.Exp(-2.2f * dt);            // (slowing as it leaves the mouth)
                velocity[i] += Vector3.up * 0.12f * dt;           // (warm: it rises a little)
                puffs[i].position += velocity[i] * dt;
                float s = size[i] * Mathf.Lerp(0.35f, 1.6f, Mathf.Sqrt(u));
                puffs[i].localScale = Vector3.one * s;
                puffs[i].rotation = Quaternion.LookRotation(puffs[i].position - cam.position) * Quaternion.Euler(0f, 0f, spin[i] * age[i]);
                // In fast, then thinning away.
                float a = thick[i] * Mathf.Clamp01(u / 0.12f) * (1f - u) * (1f - u);
                block.SetColor(ColorId, new Color(0.85f, 0.86f, 0.88f, a));
                renderers[i].SetPropertyBlock(block);
                renderers[i].enabled = true;
            }
        }

        // One breath out: a puff from just below and in front of the eye, carried forward with you.
        void Breathe(float tired, bool winded, float delay)
        {
            int i = next;
            next = (next + 1) % Puffs;
            var head = player.Head;
            Vector3 forward = Vector3.ProjectOnPlane(head.forward, Vector3.up).normalized;
            if (forward.sqrMagnitude < 0.01f) forward = transform.forward;
            puffs[i].position = head.position + forward * 0.28f + Vector3.down * 0.12f + Random.insideUnitSphere * 0.02f;
            var body = GetComponent<CharacterController>();
            Vector3 carried = body != null && body.enabled ? body.velocity * 0.8f : Vector3.zero;
            velocity[i] = forward * Random.Range(0.5f, 0.9f) * (winded ? 1.4f : 1f) + Vector3.down * 0.05f + Random.insideUnitSphere * 0.08f + carried;
            age[i] = -delay;
            life[i] = Random.Range(1.4f, 2.2f) * Mathf.Lerp(1f, 1.3f, tired);
            size[i] = Random.Range(0.16f, 0.22f) * Mathf.Lerp(1f, 1.8f, tired);
            thick[i] = Mathf.Lerp(0.12f, 0.32f, tired) * (winded ? 1.25f : 1f);
            spin[i] = Random.Range(-25f, 25f);
        }

        // A soft round puff, uneven at its edges, white with the alpha carrying its shape.
        static Texture2D PuffTexture()
        {
            const int size = 64;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, true) { name = "Breath puff", wrapMode = TextureWrapMode.Clamp };
            var px = new Color[size * size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float u = (x + 0.5f) / size * 2f - 1f, v = (y + 0.5f) / size * 2f - 1f;
                float d = Mathf.Sqrt(u * u + v * v);
                float wobble = 0.85f + 0.15f * Mathf.PerlinNoise(x * 0.12f, y * 0.12f) + 0.1f * Mathf.PerlinNoise(x * 0.35f + 7f, y * 0.35f);
                float a = Mathf.Clamp01(1f - d / wobble);
                px[y * size + x] = new Color(1f, 1f, 1f, a * a * (3f - 2f * a));
            }
            tex.SetPixels(px);
            tex.Apply(true);
            return tex;
        }
    }
}
