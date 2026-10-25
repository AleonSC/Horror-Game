using System.Collections.Generic;
using Game.Audio;
using UnityEngine;

namespace Game.DevTools
{
    /// <summary>
    /// The Sounds tab: every sound playing right now (loops and one-shots), loudest first, with how loud it's set
    /// to, how far off it is, its group, and a box to mute it. For hunting down a sound you can't place.
    /// </summary>
    sealed class SoundsTab : IDevTab
    {
        public string Title => "Sounds";
        Vector2 scroll;
        bool showQuiet;

        public void Draw()
        {
            GUILayout.Label("What's playing now, loudest first. Tick Mute to silence one (until you untick it).", DevStyle.MutedLabel);
            showQuiet = GUILayout.Toggle(showQuiet, "Show silent ones too (volume 0)");
            var all = Object.FindObjectsByType<AudioSource>(FindObjectsSortMode.None);
            var playing = new List<AudioSource>();
            foreach (var a in all)
                if (a != null && a.isPlaying && a.clip != null && (showQuiet || a.volume > 0.001f || a.mute)) playing.Add(a);
            playing.Sort((a, b) => b.volume.CompareTo(a.volume));
            var cam = Camera.main;
            scroll = GUILayout.BeginScrollView(scroll);
            foreach (var a in playing)
            {
                GUILayout.BeginHorizontal();
                string where = a.spatialBlend > 0.5f && cam != null ? $"{Vector3.Distance(cam.transform.position, a.transform.position):0} m" : "everywhere";
                string group = a.outputAudioMixerGroup != null ? a.outputAudioMixerGroup.name : "-";
                GUILayout.Label($"{a.clip.name}  ({a.gameObject.name}){(a.loop ? "  loop" : "")}", GUILayout.Width(330f));
                GUILayout.Label($"vol {a.volume:0.00}   {where}   {group}", DevStyle.MutedLabel, GUILayout.Width(230f));
                a.mute = GUILayout.Toggle(a.mute, "Mute");
                GUILayout.EndHorizontal();
            }
            if (playing.Count == 0) GUILayout.Label("Nothing playing.", DevStyle.MutedLabel);
            GUILayout.EndScrollView();
            GUILayout.Label($"Under cover (muffled): {SoundPlayer.Muffle:0.00}", DevStyle.MutedLabel);
        }
    }
}
