using System.Collections.Generic;
using Game.Core;
using Game.Player;
using UnityEngine;

namespace Game.Entities
{
    /// <summary>
    /// Keeps the scours (section 6.7): the first after a calm start, one more per lamp lit, one more a minute for
    /// ten minutes (see <see cref="ScourCount"/>). A flare in the sky drives them all off; a camera flash drives off
    /// the ones in frame. Only runs in scenes with a player.
    /// </summary>
    public sealed class ScourSpawner : MonoBehaviour
    {
        readonly List<Scour> scours = new List<Scour>();
        ScourConfig config;
        readonly List<System.IDisposable> subs = new List<System.IDisposable>();
        int fromClock;
        System.Random rng;
        int nextId = 1;

        public IReadOnlyList<Scour> Scours => scours;
        public ScourSettings Settings => config.settings;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void AttachToGame()
        {
            if (!Services.TryGet<PlayerController>(out _)) return; // (no player: a look test, nothing hunts)
            if (Services.TryGet<GameRoot>(out var root) && root.GetComponent<ScourSpawner>() == null)
                root.gameObject.AddComponent<ScourSpawner>();
        }

        void Awake()
        {
            config = Services.Get<GameSetup>().Get<ScourConfig>();
            if (config == null)
            {
                Debug.LogWarning("No ScourConfig in the GameSetup asset; using defaults.");
                config = ScriptableObject.CreateInstance<ScourConfig>();
            }
            rng = new System.Random(Services.TryGet<GameRun>(out var run) ? run.Seed : 1);
            Services.Register(this);
            if (gameObject.GetComponent<ThreatMonitor>() == null) gameObject.AddComponent<ThreatMonitor>();
        }

        void Start()
        {
            if (Services.TryGet<EventBus>(out var events))
            {
                subs.Add(events.Subscribe<RunReset>(_ => ResetAll()));
                subs.Add(events.Subscribe<LampLit>(_ => Spawn(Random.Range(config.settings.minLampJoinDelay, config.settings.maxLampJoinDelay))));
                subs.Add(events.Subscribe<HuntsReset>(_ => DriveAllOff()));
                subs.Add(events.Subscribe<FlashBurst>(OnFlash));
            }
            Spawn(config.settings.firstDelay);
        }

        void OnDestroy()
        {
            foreach (var sub in subs) sub.Dispose();
            Services.Unregister(this);
        }

        // The clock: one more a minute after the calm, for ten minutes.
        void Update()
        {
            if (!Services.TryGet<GameRun>(out var run)) return;
            var d = Difficulty.Now;
            int due = ScourCount.FromClock(run.Elapsed, config.settings, d.timeBetweenScours, d.extraScours);
            while (fromClock < due)
            {
                fromClock++;
                Spawn(Random.Range(0f, 5f));
            }
        }

        /// <summary>How many the clock has added so far.</summary>
        public int FromClock => fromClock;

        // A flare in the sky: every hunt starts over.
        void DriveAllOff()
        {
            var s = config.settings;
            foreach (var scour in scours)
                if (scour != null) scour.DriveOff(Random.Range(s.minReturnDelay, s.maxReturnDelay) + 10f);
        }

        // The camera's flash: whatever's in frame, near enough, with nothing in the way.
        void OnFlash(FlashBurst e)
        {
            foreach (var scour in scours)
            {
                if (scour == null || scour.State == ScourState.Hidden) continue;
                Vector3 chest = scour.Position + Vector3.up * config.settings.height * 0.6f;
                Vector3 to = chest - e.Position;
                if (to.magnitude > e.Range || Vector3.Angle(e.Direction, to) > e.HalfAngle) continue;
                if (PlayerSense.Occluded(e.Position, chest)) continue;
                scour.DriveOff(e.Seconds);
            }
        }

        /// <summary>Add a scour that appears after <paramref name="delay"/> seconds.</summary>
        public Scour Spawn(float delay)
        {
            var go = new GameObject($"Scour #{nextId}");
            var scour = go.AddComponent<Scour>();
            scour.Init(nextId++, config, delay, rng);
            scours.Add(scour);
            return scour;
        }

        /// <summary>Every scour comes at once (the flier screeching): all that are away appear now.</summary>
        public void AllComeNow()
        {
            foreach (var s in scours)
                if (s != null && s.State == ScourState.Hidden) s.Hide(Random.Range(0f, 1.5f));
        }

        /// <summary>Dev: make one appear now (an existing hidden one, or a new one).</summary>
        public void AppearNow()
        {
            foreach (var s in scours)
            {
                if (s.State != ScourState.Hidden) continue;
                s.Hide(0f);
                return;
            }
            Spawn(0f);
        }

        // Restart: they're all gone, and the calm start begins again.
        void ResetAll()
        {
            foreach (var s in scours) if (s != null) Destroy(s.gameObject);
            scours.Clear();
            nextId = 1;
            fromClock = 0;
            Spawn(config.settings.firstDelay);
        }
    }
}
