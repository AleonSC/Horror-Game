using System;
using System.IO;
using Game.Core;
using Game.Entities;
using Game.Player;
using UnityEngine;

namespace Game.DevTools
{
    /// <summary>
    /// Writes down every game event with the run time it happened at, so a play session can be looked at
    /// afterwards (open it with F11); and keeps the run as a timeline (twice a second: what each threat was doing,
    /// the threat level, how you were moving, the flashlight, cover), saved as an HTML page beside the log to open
    /// in a browser and judge the pacing by. Saved to SessionLogs/ in the project (or the player's data folder in a
    /// build): automatically when Play stops, and each run's timeline when a new run starts.
    /// </summary>
    public sealed class SessionRecorder : MonoBehaviour
    {
        const float SampleEvery = 0.5f;

        IDisposable subscription;
        GameRoot root;
        PlayerController player;
        float nextSample;
        int shelters;
        ThreatLevel level;

        public SessionLog Log { get; } = new SessionLog();
        public SessionTimeline Timeline { get; private set; } = NewTimeline();
        public string LastSavedPath { get; private set; }
        public string LastTimelinePath { get; private set; }

        public static string Folder =>
            Application.isEditor
                ? Path.Combine(Path.GetDirectoryName(Application.dataPath) ?? ".", "SessionLogs")
                : Path.Combine(Application.persistentDataPath, "SessionLogs");

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void AttachToGame()
        {
            if (Services.TryGet<GameRoot>(out var r) && r.GetComponent<SessionRecorder>() == null)
                r.gameObject.AddComponent<SessionRecorder>();
        }

        // The lanes in the order they're shown: the threats, then you.
        static SessionTimeline NewTimeline()
        {
            var t = new SessionTimeline();
            foreach (var name in new[] { "Scours", "Blind one", "Crawlers", "Flier" }) t.ThreatLane(name);
            t.ThreatLane("Giant", oneIsAmbient: true);
            t.LaneFor(SessionTimeline.LevelLane, SessionTimeline.Threats, SessionTimeline.LevelValues);
            t.LaneFor(SessionTimeline.MoveLane, SessionTimeline.Player, SessionTimeline.MoveValues);
            t.LaneFor(SessionTimeline.LightLane, SessionTimeline.Player, SessionTimeline.OnOffValues);
            t.LaneFor(SessionTimeline.CoverLane, SessionTimeline.Player, SessionTimeline.CoverValues);
            return t;
        }

        void Awake()
        {
            root = Services.Get<GameRoot>();
            Log.Header = $"Session {DateTime.Now:yyyy-MM-dd HH:mm:ss}, {Application.productName} {Application.version}, Unity {Application.unityVersion}";
            // The first run started before this was attached, so note it here.
            if (root.Run != null)
            {
                Log.Add(0f, new RunStarted(root.Run.RunNumber, root.Run.Seed).ToString());
                Timeline.Header = $"Run {root.Run.RunNumber} (seed {root.Run.Seed}), {DateTime.Now:yyyy-MM-dd HH:mm}";
            }
            subscription = root.Events.SubscribeAll(OnEvent);
            Services.Register(this);
        }

        void OnEvent(IGameEvent e)
        {
            // (Footsteps and panting come every moment: they'd drown everything else.)
            if (e is NoiseEmitted n && n.Everyday) return;
            // A new run's events are timed from its own start.
            float time = e is RunStarted ? 0f : root.Run?.Elapsed ?? 0f;
            Log.Add(time, e.ToString());
            OnTimelineEvent(e, time);
        }

        void OnTimelineEvent(IGameEvent e, float time)
        {
            switch (e)
            {
                case RunStarted r:
                    // A new run: the last one's timeline is saved and a fresh one begun.
                    if (!Timeline.Empty) SaveTimeline();
                    Timeline = NewTimeline();
                    Timeline.Header = $"Run {r.RunNumber} (seed {r.Seed}), {DateTime.Now:yyyy-MM-dd HH:mm}";
                    shelters = 0;
                    level = ThreatLevel.None;
                    nextSample = 0f;
                    break;
                case LampLit l: Timeline.Mark(time, SessionTimeline.MarkKind.Lamp, l.ToString()); break;
                case PlayerCaught c: Timeline.Mark(time, SessionTimeline.MarkKind.Caught, c.ToString()); break;
                case EntitySpawned s: Timeline.Mark(time, SessionTimeline.MarkKind.Appeared, s.ToString()); break;
                case ScareHappened s: Timeline.Mark(time, SessionTimeline.MarkKind.Scare, s.What); break;
                case HuntsReset h: Timeline.Mark(time, SessionTimeline.MarkKind.HuntsReset, h.ToString()); break;
                case FloodlightsLit f: Timeline.Mark(time, SessionTimeline.MarkKind.Floodlights, f.ToString()); break;
                case BossThemeStarted b: Timeline.Mark(time, SessionTimeline.MarkKind.Theme, b.ToString()); break;
                case EnteredShelter _: shelters++; break;
                case ExitedShelter _: shelters = Mathf.Max(0, shelters - 1); break;
                case ThreatLevelChanged t: level = t.Level; Timeline.Set(SessionTimeline.LevelLane, time, (int)t.Level); break;
            }
        }

        void Update()
        {
            var run = root.Run;
            if (run == null || !GameFlow.IsPlaying) return;
            float time = run.Elapsed;
            if (time < nextSample) return;
            nextSample = time + SampleEvery;
            Sample(time);
        }

        // What everything's doing right now.
        void Sample(float time)
        {
            var t = Timeline;
            int scours = 0, blind = 0, crawlers = 0, flier = 0, giant = 0;
            foreach (var threat in Threats.All)
            {
                if (threat == null) continue;
                int v = threat.ThreatTaking ? 3 : threat.ThreatHunting ? 2 : threat.ThreatVisible || threat.ThreatWatching ? 1 : 0;
                switch (threat)
                {
                    case Scour _: scours = Mathf.Max(scours, v); break;
                    case BlindOne _: blind = Mathf.Max(blind, v); break;
                    case Crawler _: crawlers = Mathf.Max(crawlers, v); break;
                    case Flier _: flier = Mathf.Max(flier, v); break;
                    case Giant _: giant = Mathf.Max(giant, v); break;
                }
            }
            t.Set("Scours", time, scours);
            t.Set("Blind one", time, blind);
            t.Set("Crawlers", time, crawlers);
            t.Set("Flier", time, flier);
            t.Set("Giant", time, giant);
            t.Set(SessionTimeline.LevelLane, time, (int)level);

            if (player == null && !Services.TryGet(out player)) return;
            int move = player.Held ? 4 : player.Piloting ? 5 : player.Crouching ? 3 : player.Running ? 2 : player.Moving ? 1 : 0;
            t.Set(SessionTimeline.MoveLane, time, move);
            t.Set(SessionTimeline.LightLane, time, player.Flashlight != null && player.Flashlight.IsOn ? 1 : 0);
            bool lit = Services.TryGet<SafeZones>(out var zones) && zones.Contains(player.transform.position);
            t.Set(SessionTimeline.CoverLane, time, shelters > 0 || PlayerPlace.InDarkRoom ? 2 : lit ? 1 : 0);
        }

        void OnApplicationQuit()
        {
            if (Log.Count > 0) Save();
        }

        void OnDestroy()
        {
            subscription?.Dispose();
            Services.Unregister(this);
        }

        public void CopyToClipboard() => GUIUtility.systemCopyBuffer = Log.ToText();

        /// <summary>The log, and this run's timeline beside it.</summary>
        public string Save()
        {
            try
            {
                Directory.CreateDirectory(Folder);
                var path = Path.Combine(Folder, $"Session_{DateTime.Now:yyyy-MM-dd_HH-mm-ss}.txt");
                File.WriteAllText(path, Log.ToText());
                LastSavedPath = path;
                Debug.Log($"Session log saved to {path}");
            }
            catch (Exception e)
            {
                Debug.LogWarning($"Couldn't save the session log. ({e.Message})");
            }
            if (!Timeline.Empty) SaveTimeline();
            return LastSavedPath;
        }

        /// <summary>This run's timeline as an HTML page; its path, or null.</summary>
        public string SaveTimeline()
        {
            try
            {
                Directory.CreateDirectory(Folder);
                var path = Path.Combine(Folder, $"Timeline_{DateTime.Now:yyyy-MM-dd_HH-mm-ss}.html");
                File.WriteAllText(path, TimelineReport.ToHtml(Timeline, "Session timeline"));
                LastTimelinePath = path;
                Debug.Log($"Session timeline saved to {path}");
                return path;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"Couldn't save the session timeline. ({e.Message})");
                return null;
            }
        }

        /// <summary>Save this run's timeline now and open it in the browser.</summary>
        public void OpenTimeline()
        {
            var path = SaveTimeline();
            if (path != null) Application.OpenURL(new Uri(path).AbsoluteUri);
        }

        public void Clear()
        {
            Log.Clear();
            Timeline = NewTimeline();
            if (root.Run != null) Timeline.Header = $"Run {root.Run.RunNumber} (seed {root.Run.Seed}), from {SessionLog.FormatTime(root.Run.Elapsed)}";
        }
    }
}
