using System.Collections.Generic;
using Game.Audio;
using UnityEngine;

namespace Game.DevTools
{
    /// <summary>
    /// The Sounds tab: every sound heard this session, one row each by name, in a fixed order (loops, then
    /// one-shots, each alphabetical) so the rows hold still. Each row: how many are playing now, the loudest
    /// one's volume and how far off it is, and a Mute box that silences that sound (every copy of it, new ones
    /// too) until it's unticked, with this window shut or not. For hunting down a sound you can't place.
    /// </summary>
    sealed class SoundsTab : IDevTab
    {
        public string Title => "Sounds";

        sealed class Row
        {
            public string Name;
            public bool Loop;
            public int Playing;
            public float Volume, Distance = -1f;
            public string Group = "";
            public float LastHeard = -100f;
        }

        readonly Dictionary<string, Row> rows = new Dictionary<string, Row>();
        readonly List<Row> order = new List<Row>();
        Vector2 scroll;
        float nextRefresh;

        public void Draw()
        {
            SoundMutes.Ensure();
            if (Time.unscaledTime >= nextRefresh)
            {
                nextRefresh = Time.unscaledTime + 0.3f;
                Refresh();
            }
            GUILayout.Label("Every sound heard this session. Tick Mute to silence one, all its copies, until you untick it (it stays muted with this window shut).", DevStyle.MutedLabel);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Unmute all", DevStyle.Button)) SoundMutes.Muted.Clear();
            GUILayout.Label($"   Under cover (muffled): {SoundPlayer.Muffle:0.00}", DevStyle.MutedLabel);
            GUILayout.EndHorizontal();
            scroll = GUILayout.BeginScrollView(scroll);
            Section("Loops", true);
            Section("One-shots", false);
            GUILayout.EndScrollView();
        }

        void Section(string title, bool loops)
        {
            GUILayout.Label(title, DevStyle.Section);
            foreach (var r in order)
            {
                if (r.Loop != loops) continue;
                GUILayout.BeginHorizontal();
                bool now = r.Playing > 0;
                GUILayout.Label(r.Name, now ? GUI.skin.label : DevStyle.MutedLabel, GUILayout.Width(170f));
                string where = !now ? "" : r.Distance < 0f ? "everywhere" : $"{r.Distance:0} m";
                string state = now ? $"{r.Playing} playing   vol {r.Volume:0.00}   {where}   {r.Group}" : $"quiet ({Time.unscaledTime - r.LastHeard:0} s ago)";
                GUILayout.Label(state, DevStyle.MutedLabel, GUILayout.Width(330f));
                bool muted = SoundMutes.Muted.Contains(r.Name);
                bool want = GUILayout.Toggle(muted, "Mute", GUILayout.Width(70f));
                if (want != muted)
                {
                    if (want) SoundMutes.Muted.Add(r.Name);
                    else SoundMutes.Muted.Remove(r.Name);
                    SoundMutes.Apply();
                }
                GUILayout.EndHorizontal();
            }
        }

        void Refresh()
        {
            foreach (var r in order) { r.Playing = 0; r.Volume = 0f; r.Distance = -1f; }
            var cam = Camera.main;
            bool added = false;
            foreach (var a in Object.FindObjectsByType<AudioSource>(FindObjectsSortMode.None))
            {
                if (a == null || a.clip == null || !a.isPlaying) continue;
                string name = a.clip.name;
                if (!rows.TryGetValue(name, out var r))
                {
                    rows[name] = r = new Row { Name = name };
                    order.Add(r);
                    added = true;
                }
                r.Loop |= a.loop;
                // (a muted one still counts as playing: you can see it's still there)
                if (a.volume < 0.001f && !a.mute) continue;
                r.Playing++;
                r.LastHeard = Time.unscaledTime;
                if (a.volume >= r.Volume)
                {
                    r.Volume = a.volume;
                    r.Distance = a.spatialBlend > 0.5f && cam != null ? Vector3.Distance(cam.transform.position, a.transform.position) : -1f;
                    r.Group = a.outputAudioMixerGroup != null ? a.outputAudioMixerGroup.name : "";
                }
            }
            if (added) order.Sort((x, y) => string.CompareOrdinal(x.Name, y.Name));
        }
    }

    /// <summary>Keeps the Sounds tab's mutes on every copy of a muted sound, new ones too, window open or not.</summary>
    sealed class SoundMutes : MonoBehaviour
    {
        public static readonly HashSet<string> Muted = new HashSet<string>();
        static SoundMutes instance;
        static readonly HashSet<AudioSource> ours = new HashSet<AudioSource>();
        float next;

        public static void Ensure()
        {
            if (instance != null) return;
            var go = new GameObject("Dev sound mutes") { hideFlags = HideFlags.HideInHierarchy };
            DontDestroyOnLoad(go);
            instance = go.AddComponent<SoundMutes>();
        }

        void Update()
        {
            if (Time.unscaledTime < next) return;
            next = Time.unscaledTime + 0.1f;
            if (Muted.Count > 0 || ours.Count > 0) Apply();
        }

        public static void Apply()
        {
            foreach (var a in Object.FindObjectsByType<AudioSource>(FindObjectsSortMode.None))
            {
                if (a == null) continue;
                bool want = a.clip != null && Muted.Contains(a.clip.name);
                if (want && !a.mute) { a.mute = true; ours.Add(a); }
                else if (!want && ours.Contains(a)) { a.mute = false; ours.Remove(a); }
            }
            ours.RemoveWhere(a => a == null);
        }
    }
}
