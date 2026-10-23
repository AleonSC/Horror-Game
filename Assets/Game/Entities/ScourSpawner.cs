using System.Collections.Generic;
using Game.Core;
using Game.Player;
using UnityEngine;

namespace Game.Entities
{
    /// <summary>
    /// Keeps the scours (section 6.7). For now (milestone 2) there's one, appearing after a calm start; the
    /// count rules (+1 per lamp, +1 a minute) come with the loop in milestone 4. Only runs in scenes with a
    /// player.
    /// </summary>
    public sealed class ScourSpawner : MonoBehaviour
    {
        readonly List<Scour> scours = new List<Scour>();
        ScourConfig config;
        System.IDisposable resetSub;
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
            if (Services.TryGet<EventBus>(out var events)) resetSub = events.Subscribe<RunReset>(_ => ResetAll());
            Spawn(config.settings.firstDelay);
        }

        void OnDestroy()
        {
            resetSub?.Dispose();
            Services.Unregister(this);
        }

        /// <summary>Add a scour that appears after <paramref name="delay"/> seconds.</summary>
        public Scour Spawn(float delay)
        {
            var go = new GameObject($"Scour #{nextId}");
            var scour = go.AddComponent<Scour>();
            scour.Init(nextId++, config.settings, delay, rng);
            scours.Add(scour);
            return scour;
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
            Spawn(config.settings.firstDelay);
        }
    }
}
