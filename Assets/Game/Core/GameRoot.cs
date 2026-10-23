using System;
using UnityEngine;

namespace Game.Core
{
    /// <summary>
    /// Owns the session: the event bus, the player's preferences, the setup asset and the current run. It's
    /// created automatically before the first scene loads (so any scene can be played directly) and lives
    /// until the game quits. Other modules attach their own components to it when the scene has loaded.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class GameRoot : MonoBehaviour
    {
        int runCount;

        public EventBus Events { get; private set; }
        public GameSetup Setup { get; private set; }
        public PlayerPreferences Preferences { get; private set; }
        public GameRun Run { get; private set; }
        public UiState Ui { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void CreateOnPlay()
        {
            if (Services.TryGet<GameRoot>(out _)) return;
            var go = new GameObject("Game");
            DontDestroyOnLoad(go);
            go.AddComponent<GameRoot>();
        }

        void Awake()
        {
            if (Services.TryGet<GameRoot>(out var existing) && existing != this)
            {
                Debug.LogWarning("A second GameRoot was created; removing it.");
                Destroy(this);
                return;
            }

            Events = new EventBus();
            Setup = Resources.Load<GameSetup>(GameSetup.ResourcePath);
            if (Setup == null)
            {
                Debug.LogWarning($"No GameSetup asset found in a Resources folder (expected '{GameSetup.ResourcePath}'). Using defaults.");
                Setup = ScriptableObject.CreateInstance<GameSetup>();
            }
            Preferences = PlayerPreferences.Load(PlayerPreferences.DefaultPath);
            Ui = new UiState();

            Services.Register(this);
            Services.Register(Events);
            Services.Register(Setup);
            Services.Register(Preferences);
            Services.Register(Ui);

            // A project left with Time Scale 0 (Project Settings > Time) would start frozen: nothing moves.
            if (Time.timeScale <= 0f)
            {
                Debug.LogWarning("Time Scale was 0 at startup (Project Settings > Time). Setting it to 1.");
                Time.timeScale = 1f;
            }

            StartNewRun();
        }

        void Update() => Run?.Tick(Time.deltaTime);

        void OnApplicationQuit() => Preferences?.Save(PlayerPreferences.DefaultPath);

        void OnDestroy()
        {
            Services.Unregister(this);
            if (Events != null) Services.Unregister(Events);
            if (Setup != null) Services.Unregister(Setup);
            if (Preferences != null) Services.Unregister(Preferences);
            if (Ui != null) Services.Unregister(Ui);
            if (Run != null) Services.Unregister(Run);
        }

        /// <summary>Throw the current run away and begin a new one (the pause menu's Restart).</summary>
        public void Restart()
        {
            if (Run != null) Events.Publish(new RunReset(Run.RunNumber));
            StartNewRun();
        }

        void StartNewRun()
        {
            if (Run != null) Services.Unregister(Run);
            runCount++;
            int seed = Setup.RandomizeSeedEachRun ? Environment.TickCount ^ (runCount * 7919) : Setup.FixedSeed;
            Run = new GameRun(runCount, seed);
            Services.Register(Run);
            Events.Publish(new RunStarted(Run.RunNumber, Run.Seed));
        }
    }
}
