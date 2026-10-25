using Game.Audio;
using Game.Core;
using Game.Player;
using Game.Rendering;
using UnityEngine;
using UnityEngine.Rendering.HighDefinition;

namespace Game.Entities
{
    /// <summary>
    /// What a scour sometimes leaves when you drive it off: one of its eyes, a wet, veined ball trailing a strand
    /// of nerve, hanging in the air. It drifts after you at a distance, about head height, bobbing a little, and
    /// never stops looking at you, turning with your every move (with small darting flicks, as eyes do). It
    /// can't hurt you. After a while (ScourConfig > The eye left behind) it stops, its iris kindles red and
    /// brightens, and it shoots up into the sky toward the giant, a streak of light, and is gone.
    /// </summary>
    public sealed class ScourEyeball : MonoBehaviour
    {
        const int MaxAlive = 3;
        const float Size = 0.17f;           // the ball's diameter (metres)
        const float Height = 2.1f;          // how high over the ground it floats
        const float KindleTime = 2.2f;      // glowing up before it flies
        const float FlyTime = 7f;

        static int alive;
        static Material sclera, iris, pupil, nerve;
        static Texture2D veins;

        float watchTime, keep;
        float age;
        float angle, angleDrift;
        Vector3 velocity;
        Vector3 flyVelocity;
        Quaternion dart = Quaternion.identity;
        float nextDart;
        Transform ball;
        Material irisGlow;
        MeshRenderer halo;
        Light glow;
        readonly Transform[] strand = new Transform[5];
        readonly Vector3[] strandAt = new Vector3[6];
        PlayerController player;

        /// <summary>Leave an eye here, to watch for this many seconds, keeping about this far off.</summary>
        public static void Spawn(Vector3 at, float watchSeconds, float distance)
        {
            if (alive >= MaxAlive) return;
            var go = new GameObject("Scour eye") { layer = Layers.Entities };
            if (Services.TryGet<GameRoot>(out var root)) go.transform.SetParent(root.transform, true);
            go.transform.position = at;
            var eye = go.AddComponent<ScourEyeball>();
            eye.watchTime = watchSeconds;
            eye.keep = distance;
        }

        void Awake()
        {
            alive++;
            if (sclera == null)
            {
                veins = VeinTexture();
                sclera = RuntimeMaterials.LitTextured("Scour eye white", new Color(0.78f, 0.72f, 0.64f), 0.85f, veins, Vector2.one);
                pupil = RuntimeMaterials.Lit("Scour eye pupil", new Color(0.01f, 0.005f, 0.005f), 0.95f);
                nerve = RuntimeMaterials.Lit("Scour eye nerve", new Color(0.32f, 0.12f, 0.1f), 0.7f);
            }
            irisGlow = RuntimeMaterials.LitGlow("Scour eye iris", new Color(0.35f, 0.05f, 0.03f), Color.black);

            ball = new GameObject("Ball").transform;
            ball.SetParent(transform, false);
            Part(PrimitiveType.Sphere, ball, Vector3.zero, Vector3.one * Size, sclera);
            Part(PrimitiveType.Sphere, ball, new Vector3(0f, 0f, Size * 0.4f), new Vector3(Size * 0.55f, Size * 0.55f, Size * 0.22f), irisGlow);
            Part(PrimitiveType.Sphere, ball, new Vector3(0f, 0f, Size * 0.475f), new Vector3(Size * 0.26f, Size * 0.3f, Size * 0.06f), pupil); // (a little tall: not quite a man's)
            halo = Effects.Glow(ball, new Vector3(0f, 0f, Size * 0.45f), Size * 1.6f, Color.black, 0.4f, 3f, "Eye glow");

            // The strand of nerve trailing from the back of it, swinging behind as it moves.
            for (int i = 0; i < strand.Length; i++)
            {
                strand[i] = Part(PrimitiveType.Cylinder, transform, Vector3.zero, Vector3.one, nerve).transform;
                strand[i].GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
            for (int i = 0; i < strandAt.Length; i++) strandAt[i] = transform.position - Vector3.up * i * 0.05f;

            angle = Random.Range(0f, Mathf.PI * 2f);
            angleDrift = Random.Range(0.08f, 0.16f) * (Random.value < 0.5f ? -1f : 1f);
        }

        void Start() => Play(Sound.ScourClick, 0.4f, 1.6f, 15f); // (a last wet click as it's left)

        void OnDestroy()
        {
            alive--;
            if (irisGlow != null) Destroy(irisGlow);
        }

        static GameObject Part(PrimitiveType type, Transform parent, Vector3 pos, Vector3 scale, Material mat)
        {
            var go = GameObject.CreatePrimitive(type);
            Destroy(go.GetComponent<Collider>());
            go.layer = Layers.Entities;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localScale = scale;
            go.GetComponent<Renderer>().sharedMaterial = mat;
            return go;
        }

        void Update()
        {
            if (player == null && !Services.TryGet(out player)) return;
            if (!GameFlow.IsPlaying) return;
            float dt = Time.deltaTime;
            age += dt;
            Vector3 head = player.Head != null ? player.Head.position : player.transform.position + Vector3.up * 1.6f;

            if (age < watchTime) Follow(head, dt);
            else if (age < watchTime + KindleTime) Kindle(head, (age - watchTime) / KindleTime);
            else if (age < watchTime + KindleTime + FlyTime) Fly(dt, (age - watchTime - KindleTime) / FlyTime);
            else
            {
                Destroy(gameObject);
                return;
            }
            Strand(dt);
        }

        // Drifting after you, about this far off, going slowly round you; never inside a trunk if it can help it.
        void Follow(Vector3 head, float dt)
        {
            angle += angleDrift * dt;
            Vector3 p = player.transform.position;
            Vector3 want = p + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * keep;
            want.y = PlayerSense.GroundHeight(want) + Height + Mathf.Sin(age * 1.3f) * 0.18f;
            // A trunk between it and where it wants to be, or there: it goes round the other way.
            if (Physics.CheckSphere(want, 0.4f, Layers.Mask(Layers.Trees), QueryTriggerInteraction.Ignore)
                || Physics.Linecast(transform.position, want, Layers.Mask(Layers.Trees), QueryTriggerInteraction.Ignore))
                angleDrift = -angleDrift;
            float maxSpeed = Mathf.Max(2f, (want - transform.position).magnitude * 0.8f); // (it keeps up if you run)
            transform.position = Vector3.SmoothDamp(transform.position, want, ref velocity, 1.2f, maxSpeed, dt);
            Watch(head, dt, 1f);
        }

        // Looking at you: turning with you, quickly, with small darting flicks now and then.
        void Watch(Vector3 head, float dt, float darts)
        {
            if (Time.time >= nextDart)
            {
                nextDart = Time.time + Random.Range(0.4f, 1.6f);
                dart = Quaternion.Euler(Random.Range(-4f, 4f) * darts, Random.Range(-5f, 5f) * darts, 0f);
            }
            Vector3 to = head - transform.position;
            if (to.sqrMagnitude < 0.0001f) return;
            Quaternion look = Quaternion.LookRotation(to) * dart;
            ball.rotation = Quaternion.Slerp(ball.rotation, look, 1f - Mathf.Exp(-14f * dt));
        }

        // It stops dead, still staring, and its iris kindles, brighter and brighter, shivering.
        void Kindle(Vector3 head, float t)
        {
            velocity = Vector3.zero;
            Watch(head, Time.deltaTime, 0f);
            if (glow == null)
            {
                var go = new GameObject("Eye light");
                go.transform.SetParent(ball, false);
                go.transform.localPosition = new Vector3(0f, 0f, Size * 0.8f);
                glow = go.AddHDLight(LightType.Point).GetComponent<Light>();
                glow.color = new Color(1f, 0.2f, 0.08f);
                glow.range = 6f;
                glow.shadows = LightShadows.None;
                Play(Sound.Crescendo, 0.35f, 2.2f, 25f);
            }
            float k = t * t;
            SetGlow(Mathf.Lerp(0.5f, 14f, k));
            ball.localPosition = Random.insideUnitSphere * 0.012f * k;
        }

        // Up and away, faster and faster, toward the giant (or just up, if there isn't one), shrinking into the dark.
        void Fly(float dt, float t)
        {
            if (flyVelocity == Vector3.zero)
            {
                Vector3 dir = Vector3.up;
                if (Services.TryGet<Giant>(out var giant))
                {
                    Vector3 to = giant.ShownAt - transform.position;
                    to.y = Mathf.Max(to.y, to.magnitude * 0.5f);
                    if (to.sqrMagnitude > 1f) dir = to.normalized;
                }
                flyVelocity = dir * 3f;
                Play(Sound.Disperse, 0.5f, 1.6f, 30f);
            }
            flyVelocity += flyVelocity.normalized * 30f * dt;
            transform.position += flyVelocity * dt;
            ball.localPosition = Vector3.zero;
            ball.rotation = Quaternion.Slerp(ball.rotation, Quaternion.LookRotation(flyVelocity), 1f - Mathf.Exp(-6f * dt));
            SetGlow(14f * (1f - t * 0.7f));
            if (glow != null) glow.range = Mathf.Lerp(6f, 2f, t);
        }

        void SetGlow(float strength)
        {
            Color c = new Color(1f, 0.16f, 0.06f);
            RuntimeMaterials.SetGlow(irisGlow, c * strength * 0.6f);
            if (halo != null) Effects.SetGlow(halo, c * strength * 0.25f);
            if (glow != null) glow.intensity = strength * 0.4f;
        }

        // The strand: each point trails the one before it, sagging, so it swings and drags behind.
        void Strand(float dt)
        {
            const float link = 0.045f;
            strandAt[0] = ball.position - ball.forward * Size * 0.45f;
            for (int i = 1; i < strandAt.Length; i++)
            {
                Vector3 p = strandAt[i] + Vector3.down * 0.25f * dt;
                Vector3 d = p - strandAt[i - 1];
                strandAt[i] = strandAt[i - 1] + (d.sqrMagnitude > 1e-6f ? d.normalized : Vector3.down) * link;
            }
            for (int i = 0; i < strand.Length; i++)
            {
                Vector3 a = strandAt[i], b = strandAt[i + 1];
                float r = Mathf.Lerp(0.022f, 0.006f, i / (float)(strand.Length - 1));
                strand[i].position = (a + b) * 0.5f;
                strand[i].rotation = Quaternion.FromToRotation(Vector3.up, b - a);
                strand[i].localScale = new Vector3(r, link * 0.5f, r);
            }
        }

        void Play(Sound sound, float volume, float pitch, float range)
        {
            if (Services.TryGet<SoundPlayer>(out var sounds))
                sounds.Play(sound, transform.position, MixerNames.Entities, volume, pitch, range);
        }

        // The white of the eye: off-white, yellowing toward the back, red veins branching in from behind.
        static Texture2D VeinTexture()
        {
            const int w = 128, h = 64;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, true) { name = "Scour eye veins", wrapMode = TextureWrapMode.Repeat };
            var px = new Color[w * h];
            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                float u = x / (float)w, v = y / (float)h;
                // (the sphere's U runs round it, V pole to pole; its front, +Z, is at U 0.75)
                float front = Mathf.Abs(Mathf.Repeat(u - 0.75f + 0.5f, 1f) - 0.5f) * 2f; // 0 at the front, 1 at the back
                float n1 = Mathf.PerlinNoise(x * 0.09f, y * 0.09f), n2 = Mathf.PerlinNoise(x * 0.23f + 9f, y * 0.23f + 3f);
                float vein = Mathf.Clamp01(1f - Mathf.Abs(n1 - 0.5f) * 18f) + 0.6f * Mathf.Clamp01(1f - Mathf.Abs(n2 - 0.5f) * 22f);
                vein *= Mathf.Clamp01(front * 1.4f - 0.15f) * Mathf.Clamp01(Mathf.Sin(v * Mathf.PI) * 1.5f);
                Color white = Color.Lerp(new Color(1f, 0.97f, 0.92f), new Color(0.85f, 0.72f, 0.55f), front * front);
                px[y * w + x] = Color.Lerp(white, new Color(0.55f, 0.04f, 0.03f), Mathf.Clamp01(vein));
            }
            tex.SetPixels(px);
            tex.Apply(true);
            return tex;
        }
    }
}
