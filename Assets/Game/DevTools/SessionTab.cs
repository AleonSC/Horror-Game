using Game.Core;
using UnityEngine;

namespace Game.DevTools
{
    /// <summary>The dev window's Session tab: the recorder's log with Copy, Save and Clear.</summary>
    sealed class SessionTab : IDevTab
    {
        public const string TabTitle = "Session";
        const int ShownLines = 200;

        public string Title => TabTitle;

        public void Draw()
        {
            if (!Services.TryGet<SessionRecorder>(out var recorder))
            {
                GUILayout.Label("The session recorder isn't running.", DevStyle.MutedLabel);
                return;
            }

            var log = recorder.Log;
            GUILayout.Label($"{log.Count} events recorded. Saved automatically when Play stops.", DevStyle.MutedLabel);

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Copy", DevStyle.Button)) recorder.CopyToClipboard();
            if (GUILayout.Button("Save", DevStyle.Button)) recorder.Save();
            if (GUILayout.Button("Clear", DevStyle.Button)) recorder.Clear();
            GUILayout.EndHorizontal();

            if (!string.IsNullOrEmpty(recorder.LastSavedPath))
                GUILayout.Label($"Saved: {recorder.LastSavedPath}", DevStyle.MutedLabel);

            GUILayout.Label(log.Count > ShownLines ? $"Latest {ShownLines}:" : "Log:", DevStyle.Section);
            var entries = log.Entries;
            for (int i = Mathf.Max(0, entries.Count - ShownLines); i < entries.Count; i++)
                GUILayout.Label($"[{SessionLog.FormatTime(entries[i].Time)}] {entries[i].Text}", DevStyle.Mono);
        }
    }
}
