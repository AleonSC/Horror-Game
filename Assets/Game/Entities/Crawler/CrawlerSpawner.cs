using System.Collections.Generic;
using Game.Core;
using Game.Player;
using UnityEngine;

namespace Game.Entities
{
    /// <summary>
    /// Keeps the crawlers (section 6.9): once enough lamps are lit (the difficulty says how many: 2 on Normal),
    /// four of them (x difficulty) lie in wait 14-45 m round you, out of the lights. One left far behind is moved.
    /// </summary>
    public sealed class CrawlerSpawner : MonoBehaviour
    {
        readonly List<Crawler> crawlers = new List<Crawler>();
        readonly List<System.IDisposable> subs = new List<System.IDisposable>();
        CrawlerConfig config;
        PlayerController player;
        System.Random rng = new System.Random(5);
        int nextId = 1;
        bool active;

        public CrawlerSettings Settings => config.settings;
        public IReadOnlyList<Crawler> Crawlers => crawlers;
        public bool Active => active;

        public bool SomeoneOnFace
        {
            get
            {
                foreach (var c in crawlers) if (c != null && c.OnFace) return true;
                return false;
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void AttachToGame()
        {
            if (!Services.TryGet<PlayerController>(out _) || !Services.TryGet<Game.World.Build.WorldBuilder>(out _)) return;
            if (Services.TryGet<GameRoot>(out var root) && root.GetComponentInChildren<CrawlerSpawner>() == null)
                new GameObject("Crawlers").AddComponent<CrawlerSpawner>().transform.SetParent(root.transform, false);
        }

        void Awake()
        {
            config = Services.Get<GameSetup>().Get<CrawlerConfig>();
            if (config == null)
            {
                Debug.LogWarning("No CrawlerConfig in the GameSetup asset; using defaults.");
                config = ScriptableObject.CreateInstance<CrawlerConfig>();
            }
            Services.Register(this);
        }

        void Start()
        {
            if (!Services.TryGet<EventBus>(out var events)) return;
            subs.Add(events.Subscribe<LampLit>(e => { if (e.TimesLit >= Difficulty.Now.crawlersFromLamp) Begin(); }));
            subs.Add(events.Subscribe<RunReset>(_ => Clear()));
        }

        void OnDestroy()
        {
            foreach (var s in subs) s.Dispose();
            Services.Unregister(this);
        }

        /// <summary>They come (also the dev button).</summary>
        public void Begin()
        {
            active = true;
            Fill();
        }

        void Clear()
        {
            active = false;
            foreach (var c in crawlers) if (c != null) Destroy(c.gameObject);
            crawlers.Clear();
        }

        void Update()
        {
            if (active && !PlayerPlace.InDarkRoom) Fill();
        }

        // Up to the count (x difficulty); also catches any that were lost.
        void Fill()
        {
            if (player == null && !Services.TryGet(out player)) return;
            crawlers.RemoveAll(c => c == null);
            int want = Mathf.RoundToInt(config.settings.count * Difficulty.Now.crawlers);
            while (crawlers.Count < want)
            {
                var go = new GameObject($"Crawler #{nextId}");
                go.transform.SetParent(transform, false);
                var c = go.AddComponent<Crawler>();
                c.Init(nextId++, config, rng);
                c.PlaceAt(LurkSpot());
                crawlers.Add(c);
            }
        }

        /// <summary>Move one somewhere new round you, out of sight if it can be.</summary>
        public void Relocate(Crawler c) => c.PlaceAt(LurkSpot(true));

        /// <summary>A spot to lie in wait: 14-45 m from you, on open ground, not in a light.</summary>
        public Vector3 LurkSpot(bool behind = false)
        {
            var s = config.settings;
            Vector3 p = player.transform.position;
            Vector3 back = -Vector3.ProjectOnPlane(player.Head.forward, Vector3.up).normalized;
            var zones = Services.TryGet<SafeZones>(out var z) ? z : null;
            for (int attempt = 0; attempt < 30; attempt++)
            {
                float angle = behind ? Random.Range(-70f, 70f) : Random.Range(0f, 360f);
                Vector3 dir = Quaternion.Euler(0f, angle, 0f) * (behind ? back : Vector3.forward);
                Vector3 at = p + dir * Random.Range(s.minDistance, s.maxDistance);
                at.y = PlayerSense.GroundHeight(at);
                if (zones != null && zones.Contains(at, 2f)) continue;
                if (Physics.CheckSphere(at + Vector3.up * 0.4f, 0.5f, Layers.Mask(Layers.Trees) | Layers.Mask(Layers.Props), QueryTriggerInteraction.Ignore)) continue;
                return at;
            }
            Vector3 fallback = p + back * s.maxDistance;
            fallback.y = PlayerSense.GroundHeight(fallback);
            return fallback;
        }
    }
}
