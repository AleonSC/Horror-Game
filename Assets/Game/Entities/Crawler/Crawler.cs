using Game.Audio;
using Game.Core;
using Game.Player;
using Game.Rendering;
using UnityEngine;

namespace Game.Entities
{
    public enum CrawlerState { Lurking, Crawling, Leaping, Latched, Fleeing }

    /// <summary>
    /// A crawler (section 6.9): a pale, too-human head with a gaping mouth and long black hair, on four long arms
    /// with their elbows jutting high, lying low in the mist. Its eyes shine in your beam; its face follows you;
    /// it twitches; in your light it goes still, out of it it scuttles in bursts.
    /// Come close walking (or closer running) and it leaps onto your face: you can't see or move, it shrieks (the
    /// blind one hears), and you mash Space to throw it off (or it lets go after a while). Being latched onto
    /// brings another scour. Crouch past it and it doesn't notice. It keeps out of safe lights (your beam doesn't bother it).
    /// </summary>
    public sealed class Crawler : MonoBehaviour, IThreat
    {
        CrawlerSettings s;
        PlayerController player;
        Transform model, head;
        readonly Transform[] arms = new Transform[4], fores = new Transform[4];
        Material skin, eyeMat;
        Animator animator;
        Vector3 target, leapFrom;
        float stateTime, nextMove, phase, lastClick, nextShriek;
        int taps;
        bool custom;

        public int Id { get; private set; }
        public CrawlerState State { get; private set; }
        public float DistanceToPlayer => player != null ? PlayerSense.FlatDistance(transform.position, player.transform.position) : float.MaxValue;
        public bool ThreatVisible => true;
        public bool ThreatTaking => State == CrawlerState.Latched;
        public bool ThreatHunting => State == CrawlerState.Leaping || State == CrawlerState.Latched;
        public bool ThreatWatching => false;
        public bool OnFace => State == CrawlerState.Latched;

        public void Init(int id, CrawlerConfig config, System.Random rng)
        {
            Id = id;
            s = config.settings;
            gameObject.layer = Layers.Entities;
            skin = RuntimeMaterials.Lit("Crawler skin", new Color(0.36f, 0.33f, 0.3f), 0.42f); // (pale, damp: it shows in your beam)
            eyeMat = RuntimeMaterials.Glow("Crawler eyes", Color.black);
            if (config.customModel != null) UseYours(config.customModel);
            else Build(rng);
            nextMove = Random.Range(0.3f, 1f) * s.repositionEvery;
        }

        Heat.Source heat;

        void OnEnable()
        {
            Threats.Add(this);
            heat = Heat.Add(transform, 0.4f, 0.35f);
        }

        void OnDisable()
        {
            Threats.Remove(this);
            Heat.Remove(heat);
        }

        void OnDestroy()
        {
            if (OnFace) LetGo(false);
            if (skin != null) Destroy(skin);
            if (eyeMat != null) Destroy(eyeMat);
            foreach (var m in extra) if (m != null) Destroy(m);
        }

        // ------------------------------------------------------------------ the body

        void UseYours(GameObject prefab)
        {
            custom = true;
            var m = Instantiate(prefab, transform, false);
            foreach (var c in m.GetComponentsInChildren<Collider>()) Destroy(c);
            model = m.transform;
            animator = m.GetComponentInChildren<Animator>();
            foreach (var t in m.GetComponentsInChildren<Transform>())
                if (t.name == "Eyes") foreach (var r in t.GetComponentsInChildren<Renderer>()) r.sharedMaterial = eyeMat;
        }

        // A pale, hairless head, too much like a person's, lying low: sunken sockets with the eyes deep in them, a
        // mouth hanging open far too wide with teeth in it, long black hair hanging down over its face to the
        // ground; and four long human arms from under it, their elbows jutting up high over the head like a
        // spider's legs, coming down to long-fingered hands splayed on the ground.
        void Build(System.Random rng)
        {
            model = new GameObject("Crawler body").transform;
            model.SetParent(transform, false);
            head = new GameObject("Head").transform;
            head.SetParent(model, false);
            head.localPosition = new Vector3(0f, 0.28f, 0f);
            var dark = RuntimeMaterials.Lit("Crawler mouth", new Color(0.01f, 0.005f, 0.005f), 0.6f);
            var teeth = RuntimeMaterials.Lit("Crawler teeth", new Color(0.42f, 0.38f, 0.28f), 0.5f);
            var hair = RuntimeMaterials.Lit("Crawler hair", new Color(0.012f, 0.011f, 0.01f), 0.35f);
            extra.Add(dark); extra.Add(teeth); extra.Add(hair);
            face = new GameObject("Face").transform;
            face.SetParent(head, false);
            Part(PrimitiveType.Sphere, face, new Vector3(0f, 0.02f, 0f), new Vector3(0.24f, 0.29f, 0.3f), skin);               // skull, long
            Part(PrimitiveType.Sphere, face, new Vector3(0f, -0.06f, 0.07f), new Vector3(0.2f, 0.16f, 0.2f), skin);            // cheeks
            for (int e = -1; e <= 1; e += 2)
            {
                Part(PrimitiveType.Sphere, face, new Vector3(e * 0.055f, 0.05f, 0.125f), new Vector3(0.07f, 0.06f, 0.05f), dark); // socket
                Part(PrimitiveType.Sphere, face, new Vector3(e * 0.055f, 0.05f, 0.135f), Vector3.one * 0.028f, eyeMat);         // eye, deep in it
            }
            Part(PrimitiveType.Sphere, face, new Vector3(0f, -0.075f, 0.14f), new Vector3(0.13f, 0.15f, 0.06f), dark);        // the mouth, gaping
            var jaw = Part(PrimitiveType.Sphere, face, new Vector3(0f, -0.17f, 0.08f), new Vector3(0.15f, 0.07f, 0.17f), skin); // jaw, hanging
            jaw.transform.localRotation = Quaternion.Euler(25f, 0f, 0f);
            for (int k = 0; k < 6; k++)
            {
                float x = (k - 2.5f) * 0.018f;
                Part(PrimitiveType.Cube, face, new Vector3(x, -0.012f, 0.155f), new Vector3(0.011f, 0.026f, 0.008f), teeth);
                Part(PrimitiveType.Cube, face, new Vector3(x * 0.9f, -0.14f, 0.15f), new Vector3(0.01f, 0.022f, 0.008f), teeth);
            }
            Part(PrimitiveType.Cylinder, head, new Vector3(0f, -0.05f, -0.15f), new Vector3(0.11f, 0.06f, 0.11f), skin); // the neck's stump
            // Hair: long black strands from the crown, hanging down over the face and round to the ground.
            for (int k = 0; k < 18; k++)
            {
                float a = (k / 18f - 0.5f) * 2.6f;
                var strand = new GameObject("Hair").transform;
                strand.SetParent(face, false);
                strand.localPosition = new Vector3(Mathf.Sin(a) * 0.1f, 0.15f, Mathf.Cos(a) * 0.04f + 0.02f);
                strand.localRotation = Quaternion.Euler(Mathf.Cos(a) * 25f + (float)rng.NextDouble() * 10f, 0f, -Mathf.Sin(a) * 20f);
                float len = 0.32f + (float)rng.NextDouble() * 0.2f;
                Part(PrimitiveType.Cube, strand, new Vector3(0f, -len * 0.5f, 0f), new Vector3(0.012f, len, 0.004f), hair);
            }

            // Four long arms from under the stump, elbows high, hands splayed on the ground.
            for (int i = 0; i < 4; i++)
            {
                float yaw = 45f + i * 90f;
                var arm = new GameObject("Arm").transform;
                arm.SetParent(head, false);
                arm.localPosition = new Vector3(0f, -0.08f, -0.08f);
                arm.localRotation = Quaternion.Euler(0f, yaw, 0f);
                Limb(arm, Vector3.zero, new Vector3(0f, 0.36f, 0.32f), 0.04f);                   // upper arm, up and out
                Part(PrimitiveType.Sphere, arm, new Vector3(0f, 0.36f, 0.32f), Vector3.one * 0.05f, skin); // the elbow, jutting
                var fore = new GameObject("Forearm").transform;
                fore.SetParent(arm, false);
                fore.localPosition = new Vector3(0f, 0.36f, 0.32f);
                Vector3 wrist = new Vector3(0f, -0.6f, 0.26f);
                Limb(fore, Vector3.zero, wrist, 0.03f);                                         // forearm, down to the ground
                Part(PrimitiveType.Cube, fore, wrist + new Vector3(0f, 0.01f, 0.03f), new Vector3(0.05f, 0.012f, 0.06f), skin); // palm
                for (int f = 0; f < 4; f++)
                {
                    float spread = (f - 1.5f) * 18f;
                    Vector3 dir = Quaternion.Euler(0f, spread, 0f) * new Vector3(0f, -0.004f, 1f);
                    Limb(fore, wrist + new Vector3(0f, 0f, 0.05f), wrist + new Vector3(0f, 0f, 0.05f) + dir * (0.11f + (f == 1 || f == 2 ? 0.03f : 0f)), 0.009f);
                }
                arms[i] = arm;
                fores[i] = fore;
            }
        }

        Transform face;
        readonly System.Collections.Generic.List<Material> extra = new System.Collections.Generic.List<Material>();
        float twitch, twitchTo, nextTwitch, lookYaw, lookPitch;

        // A thin limb from a to b.
        void Limb(Transform parent, Vector3 a, Vector3 b, float thickness)
        {
            Vector3 d = b - a;
            var go = Part(PrimitiveType.Capsule, parent, (a + b) * 0.5f, new Vector3(thickness, d.magnitude * 0.5f + thickness * 0.5f, thickness), skin);
            go.transform.localRotation = Quaternion.FromToRotation(Vector3.up, d);
        }

        GameObject Part(PrimitiveType type, Transform parent, Vector3 at, Vector3 scale, Material m)
        {
            var go = GameObject.CreatePrimitive(type);
            Destroy(go.GetComponent<Collider>());
            go.layer = Layers.Entities;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = at;
            go.transform.localScale = scale;
            go.GetComponent<Renderer>().sharedMaterial = m;
            return go;
        }

        void Pose(float speed, float dt)
        {
            if (custom)
            {
                if (animator != null) animator.SetFloat("Speed", speed);
                return;
            }
            phase += dt * (1f + speed * 6f);
            for (int i = 0; i < 4; i++)
            {
                float p = phase + i * Mathf.PI * 0.5f;
                float lift = Mathf.Max(0f, Mathf.Sin(p)) * (speed > 0.05f ? 18f : 3f);
                arms[i].localRotation = Quaternion.Euler(-lift, 45f + i * 90f + Mathf.Cos(p) * (speed > 0.05f ? 14f : 2f), 0f);
                fores[i].localRotation = Quaternion.Euler(lift * 0.8f, 0f, 0f);
            }
            if (head != null) head.localPosition = new Vector3(0f, 0.28f + Mathf.Sin(phase * 2f) * 0.01f, 0f);
            if (face == null || player == null) return;
            // It watches you: the face turns after you when you're near (as far as its neck lets it), tilted up.
            Vector3 to = transform.InverseTransformDirection(player.Head.position - face.position);
            bool near = DistanceToPlayer < 9f;
            lookYaw = Mathf.LerpAngle(lookYaw, near ? Mathf.Clamp(Mathf.Atan2(to.x, to.z) * Mathf.Rad2Deg, -75f, 75f) : 0f, 1f - Mathf.Exp(-3f * dt));
            lookPitch = Mathf.Lerp(lookPitch, near ? Mathf.Clamp(-Mathf.Atan2(to.y, new Vector2(to.x, to.z).magnitude) * Mathf.Rad2Deg, -40f, 10f) : 0f, 1f - Mathf.Exp(-3f * dt));
            // And twitches: the head snapping over to one side and back, more often in your light.
            nextTwitch -= dt;
            if (nextTwitch <= 0f)
            {
                bool lit = InBeam();
                nextTwitch = lit ? Random.Range(0.4f, 1.4f) : Random.Range(1.5f, 4.5f);
                twitchTo = Random.value < 0.5f ? Random.Range(-35f, 35f) : 0f;
            }
            twitch = Mathf.MoveTowards(twitch, twitchTo, dt * 600f);
            if (Mathf.Abs(twitch - twitchTo) < 0.1f && twitchTo != 0f && Random.value < dt * 3f) twitchTo = 0f;
            face.localRotation = Quaternion.Euler(lookPitch, lookYaw, twitch);
        }

        // ------------------------------------------------------------------ every frame

        void Update()
        {
            if (player == null && !Services.TryGet(out player)) return;
            if (PlayerPlace.InDarkRoom) return;
            float dt = Time.deltaTime;
            stateTime += dt;
            Eyes();
            switch (State)
            {
                case CrawlerState.Lurking: Lurk(dt); break;
                case CrawlerState.Crawling: Crawl(dt, s.crawlSpeed); break;
                case CrawlerState.Leaping: Leap(); break;
                case CrawlerState.Latched: Latched(dt); break;
                case CrawlerState.Fleeing:
                    Crawl(dt, s.crawlSpeed * 3.5f);
                    if (stateTime > 4f) Spawner?.Relocate(this);
                    break;
            }
        }

        CrawlerSpawner Spawner => Services.TryGet<CrawlerSpawner>(out var sp) ? sp : null;

        void Lurk(float dt)
        {
            Pose(0f, dt);
            float d = DistanceToPlayer;
            if (d < 6f) FaceToward(player.transform.position, dt * 0.5f);
            if (d < 5f && Time.time >= nextBreath)
            {
                // Close: its wet, rasping breath.
                nextBreath = Time.time + Random.Range(2.6f, 4f);
                Play(Sound.CrawlerBreath, Mathf.Lerp(0.7f, 0.35f, d / 5f), Random.Range(0.9f, 1.1f), 8f);
            }
            else if (d < 3f && Time.time - lastClick > 1.2f)
            {
                lastClick = Time.time;
                Play(Sound.Click, 0.45f, Random.Range(1.5f, 2.2f), 6f);
            }
            if (AvoidLight()) return;
            if (TryLeap(d)) return;
            if (d > s.relocateDistance) { Spawner?.Relocate(this); return; }
            nextMove -= dt;
            if (nextMove <= 0f)
            {
                nextMove = s.repositionEvery * Random.Range(0.7f, 1.3f);
                target = Spawner != null ? Spawner.LurkSpot() : transform.position;
                SetState(CrawlerState.Crawling);
            }
        }

        void Crawl(float dt, float speed)
        {
            Vector3 to = target - transform.position;
            to.y = 0f;
            if (to.magnitude < 0.3f)
            {
                if (State == CrawlerState.Crawling) SetState(CrawlerState.Lurking);
                Pose(0f, dt);
                return;
            }
            // In your light it goes still (only its head moves); otherwise it scuttles in quick bursts and stops,
            // never a steady crawl.
            if (State == CrawlerState.Crawling)
            {
                if (InBeam()) { Pose(0f, dt); return; }
                float burst = Mathf.PerlinNoise(Time.time * 0.9f, Id * 7.3f);
                speed *= burst > 0.55f ? 3.2f : 0.15f;
            }
            if (speed > 0.6f && Time.time >= nextSkitter)
            {
                nextSkitter = Time.time + Random.Range(0.35f, 0.55f);
                Play(Sound.CrawlerSkitter, Mathf.Clamp01(speed / 4f) * 0.8f + 0.2f, Random.Range(0.9f, 1.15f), 12f);
            }
            Vector3 step = to.normalized * Mathf.Min(to.magnitude, speed * dt);
            Vector3 next = transform.position + step;
            next.y = PlayerSense.GroundHeight(next);
            transform.position = next;
            FaceToward(next + step, dt * 4f);
            Pose(speed, dt);
            if (State == CrawlerState.Crawling && (TryLeap(DistanceToPlayer) || AvoidLight())) return;
        }

        // A safe light where it lies (a lamp, a refuge, a torch): it scuttles away into the dark. (Your beam
        // doesn't bother it: it only makes its eyes shine.)
        bool AvoidLight()
        {
            if (!Services.TryGet<SafeZones>(out var zones) || !zones.Contains(transform.position, 1f)) return false;
            Vector3 away = transform.position - player.transform.position;
            away.y = 0f;
            target = transform.position + away.normalized * 12f;
            SetState(CrawlerState.Fleeing);
            Play(Sound.CrawlerSkitter, 1f, 1.2f, 14f);
            return true;
        }

        bool TryLeap(float d)
        {
            if (player.Crouching || player.Held || !player.Moving) return false;
            float reach = player.Running ? s.leapRunning : s.leapWalking;
            if (d > reach) return false;
            if (Services.TryGet<SafeZones>(out var zones) && zones.Contains(player.transform.position)) return false;
            if (Spawner != null && Spawner.SomeoneOnFace) return false;
            var flow = player.GetComponent<CatchFlow>();
            if (flow != null && !flow.CanTake) return false;
            leapFrom = transform.position;
            if (animator != null) animator.SetTrigger("Leap");
            Play(Sound.CrawlerScreech, 0.9f, Random.Range(1f, 1.1f), 20f);
            SetState(CrawlerState.Leaping);
            return true;
        }

        void Leap()
        {
            float t = Mathf.Clamp01(stateTime / Mathf.Max(0.05f, s.leapTime));
            Vector3 face = player.Head.position + player.Head.forward * 0.25f;
            transform.position = Vector3.Lerp(leapFrom, face, t) + Vector3.up * Mathf.Sin(t * Mathf.PI) * 0.4f;
            transform.rotation = Quaternion.LookRotation(player.Head.position - transform.position);
            if (t >= 1f) Latch();
        }

        // ------------------------------------------------------------------ on your face

        void Latch()
        {
            player.SetHeld(true);
            transform.SetParent(player.Head, false);
            transform.localPosition = new Vector3(0f, -0.1f, 0.2f);
            transform.localRotation = Quaternion.Euler(-80f, 180f, 0f); // its arms round your head
            taps = 0;
            nextShriek = 0f;
            SetState(CrawlerState.Latched);
            if (s.summonsScour && Services.TryGet<ScourSpawner>(out var scours)) scours.Spawn(Random.Range(2f, 5f));
        }

        void Latched(float dt)
        {
            // You thrash: the view jerks about. It shrieks, and anything listening hears.
            player.AddShake(2.5f);
            if (Time.time >= nextShriek)
            {
                nextShriek = Time.time + 1.6f;
                Play(Sound.CrawlerScreech, 1f, Random.Range(0.92f, 1.08f), s.shriekNoise);
                if (Services.TryGet<EventBus>(out var events)) events.Publish(new NoiseEmitted(player.transform.position, s.shriekNoise, "crawler shriek"));
            }
            if (Services.TryGet<ScreenFade>(out var fade))
            {
                fade.Color = Color.black;
                fade.Alpha = s.blindness * (0.92f + 0.08f * Mathf.PerlinNoise(Time.time * 9f, 0.2f));
                fade.Message = "SPACE";
                fade.MessageAlpha = 0.55f + 0.45f * Mathf.Abs(Mathf.Sin(Time.time * 7f));
            }
            Pose(2f, dt);
            if (player.StrugglePressed)
            {
                taps++;
                player.AddShake(5f);
            }
            var flow = player.GetComponent<CatchFlow>();
            if (taps >= s.struggleTaps || stateTime >= s.letGoAfter || (flow != null && flow.Active)) LetGo(true);
        }

        // Thrown off (or it lets go): it lands a little way off and scuttles away.
        void LetGo(bool thrown)
        {
            if (player != null)
            {
                if (Services.TryGet<ScreenFade>(out var fade) && (player.GetComponent<CatchFlow>() == null || !player.GetComponent<CatchFlow>().Active)) fade.Clear();
                player.SetHeld(false);
            }
            transform.SetParent(Spawner != null ? Spawner.transform : null, true);
            if (!thrown || player == null) return;
            Vector3 ahead = Vector3.ProjectOnPlane(player.Head.forward, Vector3.up).normalized;
            Vector3 at = player.transform.position + ahead * 2f;
            at.y = PlayerSense.GroundHeight(at);
            transform.position = at;
            transform.rotation = Quaternion.LookRotation(ahead);
            target = at + ahead * 14f;
            Play(Sound.CrawlerScreech, 0.7f, 1.2f, 15f);
            SetState(CrawlerState.Fleeing);
        }

        // ------------------------------------------------------------------ helpers

        // In your flashlight's beam (lit up, close enough to see).
        bool InBeam() => player != null && player.Flashlight != null && player.Flashlight.IsOn && DistanceToPlayer < s.eyeshineDistance * 1.5f
                         && PlayerSense.InBeam(player, transform.position + Vector3.up * 0.3f, 4f);

        float nextSkitter, nextBreath;

        // Its eyes catch the beam from several metres: a pair of points shining back.
        void Eyes()
        {
            float shine = 0.05f;
            if (State != CrawlerState.Latched && player.Flashlight != null && player.Flashlight.IsOn && DistanceToPlayer < s.eyeshineDistance
                && PlayerSense.InBeam(player, transform.position + Vector3.up * 0.3f, 4f))
                shine = s.eyeshine * (1f - DistanceToPlayer / s.eyeshineDistance * 0.5f);
            RuntimeMaterials.SetGlow(eyeMat, new Color(1f, 0.85f, 0.55f) * shine);
        }

        public void PlaceAt(Vector3 p)
        {
            if (OnFace) LetGo(false);
            transform.position = p;
            if (player != null) transform.rotation = Quaternion.LookRotation(Vector3.ProjectOnPlane(player.transform.position - p, Vector3.up).normalized + Vector3.forward * 0.001f);
            nextMove = s.repositionEvery * Random.Range(0.5f, 1.2f);
            SetState(CrawlerState.Lurking);
        }

        void FaceToward(Vector3 p, float rate)
        {
            Vector3 d = p - transform.position;
            d.y = 0f;
            if (d.sqrMagnitude > 0.001f) transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(d), 1f - Mathf.Exp(-rate * 6f));
        }

        void SetState(CrawlerState next)
        {
            if (State == next) return;
            State = next;
            stateTime = 0f;
            if (next != CrawlerState.Crawling && next != CrawlerState.Lurking && Services.TryGet<EventBus>(out var events))
                events.Publish(new EntityStateChanged(EntityKind.Crawler, Id, next.ToString()));
        }

        void Play(Sound sound, float volume, float pitch, float range)
        {
            if (Services.TryGet<SoundPlayer>(out var sounds)) sounds.Play(sound, transform.position + Vector3.up * 0.3f, MixerNames.Entities, volume, pitch, range);
        }
    }
}
