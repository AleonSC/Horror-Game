using System.Collections.Generic;
using Game.Audio;
using Game.Core;
using Game.Player;
using Game.World.Build;
using UnityEngine;

namespace Game.World
{
    /// <summary>
    /// Dark crows (section 6.13). Every so often one crosses high overhead, alone or in a loose flock, flapping and
    /// gliding; and a few flocks are always on the ground somewhere about you, pecking and hopping, until you come
    /// near (or run, or make a noise), when they all burst up cawing and fly off. Each is replaced, out of sight,
    /// once it's gone.
    /// </summary>
    public sealed class Crows : MonoBehaviour
    {
        sealed class Crow
        {
            public Transform Body, Head, WingL, WingR, TipL, TipR;
            public GameObject[] Legs;
            public Vector3 Velocity, HopFrom, HopTo;
            public float Phase, Glide, Wait, Hop = -1f, Peck, Delay, Speed;
            public bool Flying;
        }

        sealed class Flock
        {
            public readonly List<Crow> Birds = new List<Crow>();
            public bool OnGround, Scared;
            public Vector3 Centre;
            public float Life;
        }

        readonly List<Flock> flocks = new List<Flock>();
        ForestLifeConfig config;
        PlayerController player;
        System.IDisposable noiseSub;
        float nextOver, nextGround;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void AttachToGame()
        {
            if (!Services.TryGet<PlayerController>(out _) || !Services.TryGet<WorldBuilder>(out _)) return;
            if (Services.TryGet<GameRoot>(out var root) && root.GetComponentInChildren<Crows>() == null)
                new GameObject("Crows").AddComponent<Crows>().transform.SetParent(root.transform, false);
        }

        void Awake()
        {
            config = Services.Get<GameSetup>().Get<ForestLifeConfig>();
            if (config == null) config = ScriptableObject.CreateInstance<ForestLifeConfig>();
            var s = config.settings;
            nextOver = Time.time + Random.Range(s.minCrowsOver, s.maxCrowsOver) * 0.5f;
        }

        void Start()
        {
            if (Services.TryGet<EventBus>(out var events)) noiseSub = events.Subscribe<NoiseEmitted>(OnNoise);
        }

        void OnDestroy() => noiseSub?.Dispose();

        Vector3 P => player.transform.position;
        ForestLifeSettings S => config.settings;

        void Update()
        {
            if (player == null && !Services.TryGet(out player)) return;
            if (PlayerPlace.InDarkRoom) return;
            float dt = Time.deltaTime;
            var s = S;

            if (Time.time >= nextOver)
            {
                nextOver = Time.time + Random.Range(Mathf.Min(s.minCrowsOver, s.maxCrowsOver), Mathf.Max(s.minCrowsOver, s.maxCrowsOver));
                FlyOver(Random.value < s.crowSoloShare ? 1 : Random.Range(Mathf.Min(s.minCrowFlock, s.maxCrowFlock), Mathf.Max(s.minCrowFlock, s.maxCrowFlock) + 1));
            }
            int grounded = 0;
            foreach (var f in flocks) if (f.OnGround && !f.Scared) grounded++;
            if (grounded < s.crowFlocksOnGround && Time.time >= nextGround)
            {
                nextGround = Time.time + 2f;
                if (TryLand(out var at)) Land(at, Random.Range(Mathf.Min(s.minCrowFlock, s.maxCrowFlock), Mathf.Max(s.minCrowFlock, s.maxCrowFlock) + 1));
            }

            for (int i = flocks.Count - 1; i >= 0; i--)
            {
                var f = flocks[i];
                if (f.OnGround && !f.Scared)
                {
                    float d = Flat(f.Centre - P).magnitude;
                    if (d > 110f || grounded > s.crowFlocksOnGround) { Remove(i); grounded--; continue; }
                    if (d < s.crowScare || (player.Running && d < s.crowScareRunning)) Scare(f);
                }
                if (!f.OnGround || f.Scared)
                {
                    f.Life -= dt;
                    if (f.Life <= 0f) { Remove(i); continue; }
                }
                foreach (var c in f.Birds)
                {
                    if (c.Flying) Fly(c, f, dt);
                    else Ground(c, f, dt);
                }
                if ((f.Scared || !f.OnGround) && f.Birds.Count > 0 && Random.value < dt * (f.Scared ? 2.5f : 0.35f))
                    Caw(f.Birds[Random.Range(0, f.Birds.Count)].Body.position, f.Scared ? 0.7f : 0.45f);
            }
        }

        void Remove(int i)
        {
            foreach (var c in flocks[i].Birds) if (c.Body != null) Destroy(c.Body.gameObject);
            flocks.RemoveAt(i);
        }

        // ------------------------------------------------------------------ overhead

        // Across the sky from one side of you to the other, high up, a loose straggle if there's more than one.
        void FlyOver(int count)
        {
            var s = S;
            Vector3 side = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f) * Vector3.forward;
            Vector3 across = Vector3.Cross(Vector3.up, side);
            float height = Random.Range(Mathf.Min(s.crowHeight.x, s.crowHeight.y), Mathf.Max(s.crowHeight.x, s.crowHeight.y));
            Vector3 from = P - side * 90f + across * Random.Range(-35f, 35f) + Vector3.up * height;
            var f = new Flock { Life = 180f / Mathf.Max(1f, s.crowSpeed) + 4f };
            for (int i = 0; i < count; i++)
            {
                var c = Make(true);
                // Strung out behind the leader, wider toward the back.
                float back = i * Random.Range(1.2f, 2.6f);
                c.Body.position = from - side * back + across * Random.Range(-1f, 1f) * (1.5f + back * 0.4f) + Vector3.up * Random.Range(-2f, 2f);
                c.Speed = s.crowSpeed * Random.Range(0.92f, 1.08f);
                c.Velocity = side * c.Speed;
                f.Birds.Add(c);
            }
            flocks.Add(f);
            if (Random.value < 0.6f) Caw(from + side * 30f, 0.5f);
        }

        // ------------------------------------------------------------------ on the ground

        // Somewhere open about you, out of plain sight if it can, to put a flock down.
        bool TryLand(out Vector3 at)
        {
            var cam = Camera.main;
            for (int attempt = 0; attempt < 8; attempt++)
            {
                float a = Random.Range(0f, Mathf.PI * 2f), r = Random.Range(40f, 80f);
                at = OnGround(P + new Vector3(Mathf.Cos(a) * r, 0f, Mathf.Sin(a) * r));
                if (Physics.CheckSphere(at + Vector3.up * 1f, 2.5f, Layers.Mask(Layers.Trees) | Layers.Mask(Layers.Props), QueryTriggerInteraction.Ignore)) continue;
                if (cam != null && attempt < 6 && Vector3.Dot(cam.transform.forward, (at - cam.transform.position).normalized) > 0.5f) continue;
                return true;
            }
            at = default;
            return false;
        }

        void Land(Vector3 at, int count)
        {
            var f = new Flock { OnGround = true, Centre = at };
            for (int i = 0; i < count; i++)
            {
                var c = Make(false);
                Vector2 o = Random.insideUnitCircle * 3.5f;
                c.Body.position = OnGround(at + new Vector3(o.x, 0f, o.y)) + Vector3.up * 0.075f;
                c.Body.rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
                c.Wait = Random.Range(0f, 3f);
                f.Birds.Add(c);
            }
            flocks.Add(f);
        }

        // Pecking about: a peck, a hop, a turn, a look round, then still a while.
        void Ground(Crow c, Flock f, float dt)
        {
            if (c.Hop >= 0f)
            {
                c.Hop += dt / 0.28f;
                float u = Mathf.Min(c.Hop, 1f);
                c.Body.position = Vector3.Lerp(c.HopFrom, c.HopTo, u) + Vector3.up * Mathf.Sin(u * Mathf.PI) * 0.12f;
                Fold(c, 1f - Mathf.Sin(u * Mathf.PI) * 0.4f);
                if (c.Hop >= 1f) c.Hop = -1f;
                return;
            }
            if (c.Peck > 0f)
            {
                c.Peck -= dt;
                if (c.Head != null) c.Head.localRotation = Quaternion.Euler(Mathf.Sin(Mathf.Clamp01(c.Peck / 0.35f) * Mathf.PI) * 60f, 0f, 0f);
                return;
            }
            c.Wait -= dt;
            if (c.Wait > 0f) return;
            c.Wait = Random.Range(0.6f, 3.5f);
            float roll = Random.value;
            if (roll < 0.45f) c.Peck = 0.35f;
            else if (roll < 0.75f)
            {
                // A hop, mostly forward, kept near the others.
                Vector3 dir = Quaternion.Euler(0f, Random.Range(-50f, 50f), 0f) * c.Body.forward;
                if ((c.Body.position - f.Centre).sqrMagnitude > 16f) dir = Flat(f.Centre - c.Body.position).normalized;
                c.HopFrom = c.Body.position;
                c.HopTo = OnGround(c.Body.position + dir * Random.Range(0.25f, 0.6f)) + Vector3.up * 0.075f;
                c.Body.rotation = Quaternion.LookRotation(Flat(dir));
                c.Hop = 0f;
            }
            else if (c.Head != null) c.Head.localRotation = Quaternion.Euler(0f, Random.Range(-70f, 70f), 0f);
        }

        void OnNoise(NoiseEmitted e)
        {
            if (config == null) return;
            // Anything that carries to them puts them up (a big noise from a fair way off, but not from across the map).
            foreach (var f in flocks)
            {
                float d = (f.Centre - e.Position).magnitude;
                if (f.OnGround && !f.Scared && d < e.Radius && d < S.crowScareRunning * 2f) Scare(f);
            }
        }

        // Up they all go, one after another in a rush, away from you and climbing.
        void Scare(Flock f)
        {
            f.Scared = true;
            f.Life = 16f;
            Vector3 away = player != null ? Flat(f.Centre - P).normalized : Vector3.forward;
            foreach (var c in f.Birds)
            {
                c.Delay = Random.Range(0f, 0.7f);
                c.Speed = S.crowSpeed * Random.Range(0.9f, 1.15f);
                c.Velocity = (Quaternion.Euler(0f, Random.Range(-40f, 40f), 0f) * away) * 2.5f + Vector3.up * Random.Range(3.5f, 5f);
                c.Flying = true;
                if (c.Head != null) c.Head.localRotation = Quaternion.identity;
            }
            if (Services.TryGet<SoundPlayer>(out var sounds))
            {
                sounds.Play(Sound.Rustle, f.Centre, MixerNames.Outside, 0.8f, 1.6f, 40f);
                sounds.Play(Sound.Rustle, f.Centre, MixerNames.Outside, 0.6f, 2.1f, 40f);
            }
            Caw(f.Centre, 0.8f);
        }

        // ------------------------------------------------------------------ flight

        void Fly(Crow c, Flock f, float dt)
        {
            if (c.Delay > 0f) { c.Delay -= dt; return; }
            if (c.Legs != null && c.Legs[0].activeSelf) foreach (var leg in c.Legs) leg.SetActive(false);
            float cruise = c.Speed;
            if (f.Scared)
            {
                // Climbing hard away from the ground, then levelling out and going.
                Vector3 flat = Flat(c.Velocity);
                if (flat.sqrMagnitude < 0.01f) flat = Vector3.forward;
                float height = c.Body.position.y - f.Centre.y;
                Vector3 want = flat.normalized * cruise + Vector3.up * (height < 20f ? 4f : 0.5f);
                c.Velocity = Vector3.MoveTowards(c.Velocity, want, dt * 6f);
                c.Glide = 0f;
            }
            else
            {
                // A slight drift and bob, the odd glide.
                c.Velocity += new Vector3(Random.Range(-1f, 1f), Random.Range(-0.3f, 0.3f), Random.Range(-1f, 1f)) * dt * 1.5f;
                c.Velocity = c.Velocity.normalized * cruise;
                if (c.Glide > 0f) c.Glide -= dt;
                else if (Random.value < dt * 0.25f) c.Glide = Random.Range(0.8f, 2f);
            }
            c.Body.position += c.Velocity * dt;
            c.Body.rotation = Quaternion.Slerp(c.Body.rotation, Quaternion.LookRotation(c.Velocity), dt * 6f);

            if (c.Glide > 0f) { Wings(c, 4f, 6f); return; }
            c.Phase += dt * (f.Scared ? 22f : 15f);
            float beat = Mathf.Sin(c.Phase);
            Wings(c, beat * 45f + 8f, Mathf.Sin(c.Phase - 0.9f) * 30f);
        }

        // Inner wings up by this (degrees), tips by this more.
        static void Wings(Crow c, float inner, float tip)
        {
            if (c.WingL != null) { c.WingL.localScale = Vector3.one; c.WingL.localRotation = Quaternion.Euler(0f, 0f, -inner); }
            if (c.WingR != null) { c.WingR.localScale = Vector3.one; c.WingR.localRotation = Quaternion.Euler(0f, 0f, inner); }
            if (c.TipL != null) c.TipL.localRotation = Quaternion.Euler(0f, 0f, -tip);
            if (c.TipR != null) c.TipR.localRotation = Quaternion.Euler(0f, 0f, tip);
        }

        // Wings folded back along the body (1), or half open for a hop.
        static void Fold(Crow c, float folded)
        {
            float yaw = Mathf.Lerp(20f, 100f, folded);
            float shrink = Mathf.Lerp(1f, 0.55f, folded);
            if (c.WingL != null) { c.WingL.localRotation = Quaternion.Euler(0f, -yaw, 0f); c.WingL.localScale = new Vector3(shrink, 1f, 1f); }
            if (c.WingR != null) { c.WingR.localRotation = Quaternion.Euler(0f, yaw, 0f); c.WingR.localScale = new Vector3(shrink, 1f, 1f); }
            if (c.TipL != null) c.TipL.localRotation = Quaternion.Euler(0f, -60f * folded, 0f);
            if (c.TipR != null) c.TipR.localRotation = Quaternion.Euler(0f, 60f * folded, 0f);
        }

        // ------------------------------------------------------------------ bits

        Crow Make(bool flying)
        {
            var body = AnimalModels.Crow(transform).transform;
            var c = new Crow
            {
                Body = body, Head = body.Find("Head"), WingL = body.Find("WingL"), WingR = body.Find("WingR"),
                Phase = Random.value * 10f, Flying = flying,
            };
            if (c.WingL != null) c.TipL = c.WingL.Find("TipL");
            if (c.WingR != null) c.TipR = c.WingR.Find("TipR");
            var legs = new List<GameObject>();
            foreach (Transform child in body) if (child.name == "Leg") legs.Add(child.gameObject);
            c.Legs = legs.ToArray();
            if (flying) foreach (var leg in c.Legs) leg.SetActive(false);
            else Fold(c, 1f);
            return c;
        }

        static void Caw(Vector3 at, float volume)
        {
            if (Services.TryGet<SoundPlayer>(out var sounds)) sounds.Play(Sound.Caw, at, MixerNames.Outside, volume, Random.Range(0.68f, 0.82f), 80f);
        }

        static Vector3 OnGround(Vector3 p)
        {
            if (Physics.Raycast(p + Vector3.up * 30f, Vector3.down, out var hit, 80f, Layers.Mask(Layers.Ground), QueryTriggerInteraction.Ignore)) p.y = hit.point.y;
            return p;
        }

        static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0f, v.z + (v.x == 0f && v.z == 0f ? 0.001f : 0f));
    }
}
