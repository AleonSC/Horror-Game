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
    /// of nerve, hanging in the air. It arrives out of the motes the scour came apart into: they swirl back in to
    /// a point and the eye condenses out of them, lids shut, swells to its size with a shudder, its strand
    /// unfurling, and then the lids snap open with a wet click and a flash of red, the pupil a slit that widens
    /// as it finds you. It drifts after you at a distance, about head height, bobbing a little, and
    /// never stops looking at you, turning with your every move (with small darting flicks, as eyes do). It
    /// can't hurt you. Shine your light on it and it blinks, its lids snapping shut over it, again and again while
    /// the beam stays on it, squinting between (and now and then it blinks by itself). After a while (ScourConfig
    /// > The eye left behind) it stops, its iris kindles red and brightens, and it shoots up into the sky toward
    /// the giant, a streak of light, and is gone.
    /// </summary>
    public sealed class ScourEyeball : MonoBehaviour
    {
        const int MaxAlive = 3;
        const float Size = 0.17f;           // the ball's diameter (metres)
        const float Height = 2.1f;          // how high over the ground it floats
        const float KindleTime = 2.2f;      // glowing up before it flies
        const float FlyTime = 7f;
        const float Arrive = 2.1f;          // its entrance (see Arrival)
        const float OpenAt = 0.72f;         // (share of the entrance when its lids snap open)
        const int MoteCount = 46;

        static int alive;
        static Material sclera, pupil, nerve, lidSkin, moteBlack;
        static Mesh lidMesh;
        const float LidUpOpen = -82f, LidDownOpen = 62f; // (degrees about the eye's side axis: drawn back over the ball)
        static Texture2D veins;

        float watchTime, keep;
        float age;
        float angle, angleDrift;
        Vector3 velocity;
        Vector3 flyVelocity;
        Quaternion dart = Quaternion.identity;
        float nextDart;
        Transform upperLid, lowerLid;
        float shut;          // 0 open, 1 shut
        float blinkAt = -1f; // when the blink under way started (-1: none)
        float nextBlink;
        bool wasLit;
        Transform ball, pupilPart;
        Vector3 pupilSize;
        Transform[] motes;
        Vector3[] moteFrom;
        float[] moteSpin, moteSize;
        Light flash;
        bool snapped;
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
                lidSkin = RuntimeMaterials.Lit("Scour eyelid", new Color(0.2f, 0.11f, 0.1f), 0.55f, true);
                lidMesh = LidMesh();
            }
            irisGlow = RuntimeMaterials.LitGlow("Scour eye iris", new Color(0.35f, 0.05f, 0.03f), Color.black);

            ball = new GameObject("Ball").transform;
            ball.SetParent(transform, false);
            Part(PrimitiveType.Sphere, ball, Vector3.zero, Vector3.one * Size, sclera);
            Part(PrimitiveType.Sphere, ball, new Vector3(0f, 0f, Size * 0.4f), new Vector3(Size * 0.55f, Size * 0.55f, Size * 0.22f), irisGlow);
            pupilSize = new Vector3(Size * 0.26f, Size * 0.3f, Size * 0.06f); // (a little tall: not quite a man's)
            pupilPart = Part(PrimitiveType.Sphere, ball, new Vector3(0f, 0f, Size * 0.475f), pupilSize, pupil).transform;
            // Lids: each a quarter of a shell round the front of the ball, swung back out of the way when open.
            upperLid = Lid(false);
            lowerLid = Lid(true);
            nextBlink = Time.time + Random.Range(3f, 7f);
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

            // The motes it gathers out of.
            if (moteBlack == null) moteBlack = RuntimeMaterials.Unlit("Eye motes", Color.black, true);
            motes = new Transform[MoteCount];
            moteFrom = new Vector3[MoteCount];
            moteSpin = new float[MoteCount];
            moteSize = new float[MoteCount];
            for (int i = 0; i < MoteCount; i++)
            {
                var m = Part(PrimitiveType.Cube, transform, Vector3.zero, Vector3.one, moteBlack);
                m.layer = Layers.Effects;
                m.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                motes[i] = m.transform;
                float a = Random.Range(0f, Mathf.PI * 2f);
                moteFrom[i] = new Vector3(Mathf.Cos(a), Random.Range(-0.8f, 0.8f), Mathf.Sin(a)) * Random.Range(0.5f, 1.3f);
                moteSpin[i] = Random.Range(2.5f, 6f) * (Random.value < 0.5f ? -1f : 1f);
                moteSize[i] = Random.Range(0.02f, 0.05f);
            }
            ball.localScale = Vector3.zero;
            // (Nothing shows until its first Update poses it: made mid-frame, it's drawn once as it is, and every
            // mote was a metre-wide black cube and the strand two-metre cylinders, all at the spot: a flicker of
            // something big and dark as the eye began.)
            foreach (var m in motes) m.localScale = Vector3.zero;
            foreach (var p in strand) p.localScale = Vector3.zero;
        }

        // A wet gathering as it starts to form.
        void Start() => Play(Sound.Creep, 0.6f, 0.55f, 18f);

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

            if (age < Arrive)
            {
                Arrival(head, age / Arrive, dt);
                Strand(dt, Mathf.Clamp01((age / Arrive - 0.35f) / 0.5f));
                return;
            }
            float t = age - Arrive;
            if (motes != null) ClearMotes();
            if (t < watchTime) Follow(head, dt);
            else if (t < watchTime + KindleTime) Kindle(head, (t - watchTime) / KindleTime);
            else if (t < watchTime + KindleTime + FlyTime) Fly(dt, (t - watchTime - KindleTime) / FlyTime);
            else
            {
                Destroy(gameObject);
                return;
            }
            Lids(dt, t < watchTime && PlayerSense.InBeam(player, ball.position, 1f));
            Strand(dt, 1f);
            if (flash != null)
            {
                flash.intensity = Mathf.MoveTowards(flash.intensity, 0f, dt * 6f);
                if (flash.intensity <= 0f) { Destroy(flash.gameObject); flash = null; }
            }
        }

        // Its entrance (u from 0 to 1): the motes swirl in and the eye condenses out of them, lids shut, a dim red
        // pulse in it; it swells to its size, overshooting, shuddering; then the lids snap open (a click, a flash
        // of red), the pupil a slit widening as it finds you.
        void Arrival(Vector3 head, float u, float dt)
        {
            // The motes: spiralling in, faster as they close, shrinking into it.
            float gather = Mathf.Clamp01(u / 0.55f);
            float g = gather * gather * (3f - 2f * gather);
            if (motes != null)
                for (int i = 0; i < motes.Length; i++)
                {
                    float r = 1f - g;
                    float spin = moteSpin[i] * (gather + gather * gather * 2f);
                    Vector3 from = moteFrom[i];
                    Vector3 p = new Vector3(from.x * Mathf.Cos(spin) - from.z * Mathf.Sin(spin), from.y, from.x * Mathf.Sin(spin) + from.z * Mathf.Cos(spin)) * r;
                    motes[i].localPosition = p;
                    motes[i].localRotation = Quaternion.Euler(spin * 90f, spin * 57f, 0f);
                    motes[i].localScale = Vector3.one * moteSize[i] * (r > 0.02f ? Mathf.Lerp(0.3f, 1f, r) : 0f);
                }
            // The eye: a speck as they gather, swelling with an overshoot, shuddering.
            float form = Mathf.Clamp01((u - 0.3f) / 0.4f);
            float swell = form < 1f ? Mathf.Sin(form * Mathf.PI * 0.5f) * (1f + 0.25f * Mathf.Sin(form * Mathf.PI)) : 1f;
            ball.localScale = Vector3.one * Mathf.Max(0.05f * g, swell);
            ball.localPosition = Random.insideUnitSphere * 0.01f * (1f - Mathf.Clamp01((u - OpenAt) / 0.25f));
            Watch(head, dt, 0f);
            // Lids shut until they snap open; the pupil a slit, widening.
            float open = Mathf.Clamp01((u - OpenAt) / 0.06f);
            float e = 1f - open;
            upperLid.localRotation = Quaternion.Euler(Mathf.Lerp(LidUpOpen, 4f, e), 0f, 0f);
            lowerLid.localRotation = Quaternion.Euler(Mathf.Lerp(LidDownOpen, -4f, e), 0f, 0f);
            float widen = Mathf.Clamp01((u - OpenAt) / (1f - OpenAt));
            pupilPart.localScale = new Vector3(pupilSize.x * Mathf.Lerp(0.15f, 1f, widen * widen), pupilSize.y * Mathf.Lerp(1.15f, 1f, widen), pupilSize.z);
            if (!snapped && u >= OpenAt)
            {
                snapped = true;
                Play(Sound.ScourSnap, 0.55f, 1.35f, 30f);
                var go = new GameObject("Eye flash");
                go.transform.SetParent(ball, false);
                go.transform.localPosition = new Vector3(0f, 0f, Size * 0.8f);
                flash = go.AddHDLight(LightType.Point).GetComponent<Light>();
                flash.color = new Color(1f, 0.18f, 0.06f);
                flash.range = 5f;
                flash.shadows = LightShadows.None;
                flash.intensity = 4f;
            }
            // The red in it: a dim pulse while it forms, a flash as it opens, dying to nothing.
            float pulse = u < OpenAt ? 0.4f + 0.3f * Mathf.Sin(u * 40f) : Mathf.Lerp(9f, 0f, Mathf.Clamp01((u - OpenAt) / (1f - OpenAt)));
            Color c = new Color(1f, 0.16f, 0.06f);
            RuntimeMaterials.SetGlow(irisGlow, c * pulse * 0.6f * g);
            if (halo != null) Effects.SetGlow(halo, c * pulse * 0.25f * g);
            if (flash != null) flash.intensity = Mathf.MoveTowards(flash.intensity, 0f, dt * 5f);
        }

        void ClearMotes()
        {
            foreach (var m in motes) if (m != null) Destroy(m.gameObject);
            motes = null;
            ball.localScale = Vector3.one;
            pupilPart.localScale = pupilSize;
            SetGlow(0f);
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

        Transform Lid(bool lower)
        {
            var go = new GameObject(lower ? "Lower lid" : "Upper lid") { layer = Layers.Entities };
            go.transform.SetParent(ball, false);
            go.transform.localScale = new Vector3(1f, lower ? -1f : 1f, 1f) * Size * 0.565f;
            go.AddComponent<MeshFilter>().sharedMesh = lidMesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = lidSkin;
            return go.transform;
        }

        // A blink: snapping shut, a moment shut, opening slower. The beam landing on it starts one at once, and
        // while it stays on it it keeps blinking, quick and nervous; otherwise only now and then.
        void Lids(float dt, bool lit)
        {
            if (lit && !wasLit) Blink();
            wasLit = lit;
            if (blinkAt < 0f && Time.time >= nextBlink) Blink();
            float target = 0f;
            if (blinkAt >= 0f)
            {
                float t = Time.time - blinkAt;
                const float close = 0.07f, hold = 0.12f, open = 0.2f;
                target = t < close ? t / close : t < close + hold ? 1f : 1f - (t - close - hold) / open;
                if (t >= close + hold + open)
                {
                    blinkAt = -1f;
                    target = 0f;
                    nextBlink = Time.time + (lit ? Random.Range(0.35f, 0.9f) : Random.Range(4f, 9f));
                }
            }
            if (lit && blinkAt < 0f) target = 0.25f; // (squinting at the light between blinks)
            shut = Mathf.Clamp01(target);
            float e = shut * shut * (3f - 2f * shut);
            // (the lower lid comes only a little way up to meet the upper)
            upperLid.localRotation = Quaternion.Euler(Mathf.Lerp(LidUpOpen, 4f, e), 0f, 0f);
            lowerLid.localRotation = Quaternion.Euler(Mathf.Lerp(LidDownOpen, -4f, e), 0f, 0f);
        }

        void Blink()
        {
            if (blinkAt >= 0f) return;
            blinkAt = Time.time;
        }

        // A quarter shell of a unit sphere: in front (+Z) and above (+Y), from side to side. (Scaled -1 in Y it's
        // the lower lid.) Drawn both sides.
        static Mesh LidMesh()
        {
            const int lon = 14, lat = 7;
            var verts = new Vector3[(lon + 1) * (lat + 1)];
            var uvs = new Vector2[verts.Length];
            for (int j = 0; j <= lat; j++)
            for (int i = 0; i <= lon; i++)
            {
                float a = Mathf.Lerp(-90f, 90f, i / (float)lon) * Mathf.Deg2Rad, b = Mathf.Lerp(0f, 90f, j / (float)lat) * Mathf.Deg2Rad;
                verts[j * (lon + 1) + i] = new Vector3(Mathf.Cos(b) * Mathf.Sin(a), Mathf.Sin(b), Mathf.Cos(b) * Mathf.Cos(a));
                uvs[j * (lon + 1) + i] = new Vector2(i / (float)lon, j / (float)lat);
            }
            var tris = new int[lon * lat * 6];
            int k = 0;
            for (int j = 0; j < lat; j++)
            for (int i = 0; i < lon; i++)
            {
                int v = j * (lon + 1) + i;
                tris[k++] = v; tris[k++] = v + lon + 1; tris[k++] = v + 1;
                tris[k++] = v + 1; tris[k++] = v + lon + 1; tris[k++] = v + lon + 2;
            }
            var m = new Mesh { name = "Scour eyelid", vertices = verts, uv = uvs, triangles = tris };
            m.RecalculateNormals();
            m.RecalculateBounds();
            return m;
        }

        void SetGlow(float strength)
        {
            Color c = new Color(1f, 0.16f, 0.06f);
            RuntimeMaterials.SetGlow(irisGlow, c * strength * 0.6f);
            if (halo != null) Effects.SetGlow(halo, c * strength * 0.25f);
            if (glow != null) glow.intensity = strength * 0.4f;
        }

        // The strand: each point trails the one before it, sagging, so it swings and drags behind.
        void Strand(float dt, float grown)
        {
            float link = 0.045f * Mathf.Max(0.02f, grown); // (unfurling as it forms)
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
                float r = Mathf.Lerp(0.022f, 0.006f, i / (float)(strand.Length - 1)) * Mathf.Clamp01(grown * 2f);
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
