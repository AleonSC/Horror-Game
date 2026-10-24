using Game.Audio;
using Game.Core;
using Game.Player;
using Game.Rendering;
using UnityEngine;

namespace Game.Entities
{
    public enum CrawlerState { Lurking, Crawling, Leaping, Latched, Fleeing }

    /// <summary>
    /// A crawler (section 6.9): a head on four long thin arms, lying low in the mist. Its eyes shine in your beam.
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
            skin = RuntimeMaterials.Lit("Crawler skin", new Color(0.05f, 0.045f, 0.04f), 0.45f);
            eyeMat = RuntimeMaterials.Glow("Crawler eyes", Color.black);
            if (config.customModel != null) UseYours(config.customModel);
            else Build(rng);
            nextMove = Random.Range(0.3f, 1f) * s.repositionEvery;
        }

        void OnEnable() => Threats.Add(this);
        void OnDisable() => Threats.Remove(this);

        void OnDestroy()
        {
            if (OnFace) LetGo(false);
            if (skin != null) Destroy(skin);
            if (eyeMat != null) Destroy(eyeMat);
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

        void Build(System.Random rng)
        {
            model = new GameObject("Crawler body").transform;
            model.SetParent(transform, false);
            head = new GameObject("Head").transform;
            head.SetParent(model, false);
            head.localPosition = new Vector3(0f, 0.28f, 0f);
            Part(PrimitiveType.Sphere, head, Vector3.zero, new Vector3(0.3f, 0.26f, 0.34f), skin);
            Part(PrimitiveType.Sphere, head, new Vector3(0f, -0.1f, 0.06f), new Vector3(0.2f, 0.1f, 0.2f), skin); // jaw
            Part(PrimitiveType.Cylinder, head, new Vector3(0f, -0.05f, -0.15f), new Vector3(0.12f, 0.06f, 0.12f), skin); // the neck's stump
            for (int e = -1; e <= 1; e += 2)
                Part(PrimitiveType.Sphere, head, new Vector3(e * 0.065f, 0.04f, 0.155f), Vector3.one * 0.045f, eyeMat);

            // Four long thin arms from under the stump, out and down to the ground, like a spider's legs.
            for (int i = 0; i < 4; i++)
            {
                float yaw = 45f + i * 90f;
                var arm = new GameObject("Arm").transform;
                arm.SetParent(head, false);
                arm.localPosition = new Vector3(0f, -0.08f, -0.08f);
                arm.localRotation = Quaternion.Euler(0f, yaw, 0f);
                var upper = Part(PrimitiveType.Capsule, arm, new Vector3(0f, 0.12f, 0.25f), new Vector3(0.035f, 0.28f, 0.035f), skin);
                upper.transform.localRotation = Quaternion.Euler(60f, 0f, 0f);
                var fore = new GameObject("Forearm").transform;
                fore.SetParent(arm, false);
                fore.localPosition = new Vector3(0f, 0.26f, 0.5f);
                var lower = Part(PrimitiveType.Capsule, fore, new Vector3(0f, -0.3f, 0.12f), new Vector3(0.025f, 0.33f, 0.025f), skin);
                lower.transform.localRotation = Quaternion.Euler(-20f, 0f, 0f);
                arms[i] = arm;
                fores[i] = fore;
            }
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
            if (d < 3f && Time.time - lastClick > 1.2f)
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
            Play(Sound.Click, 0.6f, 2.5f, 8f);
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
            Play(Sound.Shriek, 0.8f, 1.8f, 20f);
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
                Play(Sound.Shriek, 1f, Random.Range(1.4f, 1.7f), s.shriekNoise);
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
            Play(Sound.Shriek, 0.7f, 2f, 15f);
            SetState(CrawlerState.Fleeing);
        }

        // ------------------------------------------------------------------ helpers

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
