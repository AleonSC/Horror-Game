using System;
using System.IO;
using Game.Core;
using UnityEngine;

namespace Game.DevTools
{
    /// <summary>
    /// Writes down every game event with the run time it happened at, so a play session can be looked at
    /// afterwards (open it with F11). Saved to SessionLogs/ in the project (or the player's data folder in a
    /// build), and saved automatically when Play stops.
    /// </summary>
    public sealed class SessionRecorder : MonoBehaviour
    {
        IDisposable subscription;
        GameRoot root;

        public SessionLog Log { get; } = new SessionLog();
        public string LastSavedPath { get; private set; }

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

        void Awake()
        {
            root = Services.Get<GameRoot>();
            Log.Header = $"Session {DateTime.Now:yyyy-MM-dd HH:mm:ss}, {Application.productName} {Application.version}, Unity {Application.unityVersion}";
            // The first run started before this was attached, so note it here.
            if (root.Run != null) Log.Add(0f, new RunStarted(root.Run.RunNumber, root.Run.Seed).ToString());
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

        public string Save()
        {
            try
            {
                Directory.CreateDirectory(Folder);
                var path = Path.Combine(Folder, $"Session_{DateTime.Now:yyyy-MM-dd_HH-mm-ss}.txt");
                File.WriteAllText(path, Log.ToText());
                LastSavedPath = path;
                Debug.Log($"Session log saved to {path}");
                return path;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"Couldn't save the session log. ({e.Message})");
                return null;
            }
        }

        public void Clear() => Log.Clear();
    }
}
