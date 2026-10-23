using System.Collections.Generic;
using Game.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Game.DevTools
{
    /// <summary>A page of the dev window. Systems add their own tabs with <see cref="DevWindow.AddTab"/>.</summary>
    public interface IDevTab
    {
        string Title { get; }
        void Draw();
    }

    /// <summary>
    /// The developer window: F9 opens it, F11 opens it on the session recorder. Only in the editor and
    /// development builds (the DevTools assembly isn't compiled into release builds).
    /// </summary>
    public sealed class DevWindow : MonoBehaviour
    {
        const float VirtualHeight = 1080f;
        const float Width = 460f;

        readonly List<IDevTab> tabs = new List<IDevTab>();
        int current;
        bool open;
        Vector2 scroll;
        CursorLockMode savedLock;
        bool savedVisible;

        public bool IsOpen => open;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void AttachToGame()
        {
            if (Services.TryGet<GameRoot>(out var root) && root.GetComponent<DevWindow>() == null)
                root.gameObject.AddComponent<DevWindow>();
        }

        void Awake()
        {
            Services.Register(this);
            AddTab(new GameTab());
            AddTab(new SessionTab());
            AddTab(new PlaceholderTab("Lighting", "Lighting themes and the main look sliders arrive with the look test scene (milestone 1)."));
            AddTab(new PlaceholderTab("Look", "Every look setting as a slider arrives with the look test scene (milestone 1)."));

            if (Services.TryGet<PlayerPreferences>(out var prefs)) current = Mathf.Max(0, IndexOf(prefs.devTab));
        }

        void OnDestroy() => Services.Unregister(this);

        public void AddTab(IDevTab tab)
        {
            if (tab != null && !tabs.Contains(tab)) tabs.Add(tab);
        }

        void Update()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null) return;
            if (keyboard.f9Key.wasPressedThisFrame) SetOpen(!open);
            if (keyboard.f11Key.wasPressedThisFrame)
            {
                int session = IndexOf(SessionTab.TabTitle);
                if (open && current == session) SetOpen(false);
                else
                {
                    SelectTab(session);
                    SetOpen(true);
                }
            }
        }

        public void SetOpen(bool value)
        {
            if (value == open) return;
            open = value;
            if (open)
            {
                // Free the mouse while the window is open, and put it back as it was afterwards.
                savedLock = Cursor.lockState;
                savedVisible = Cursor.visible;
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
            else
            {
                Cursor.lockState = savedLock;
                Cursor.visible = savedVisible;
            }
        }

        void SelectTab(int index)
        {
            if (index < 0 || index >= tabs.Count || index == current) return;
            current = index;
            scroll = Vector2.zero;
            if (Services.TryGet<PlayerPreferences>(out var prefs)) prefs.devTab = tabs[index].Title;
        }

        int IndexOf(string title)
        {
            for (int i = 0; i < tabs.Count; i++)
                if (tabs[i].Title == title) return i;
            return -1;
        }

        void OnGUI()
        {
            if (!open || tabs.Count == 0) return;
            DevStyle.Ensure();

            // Lay out on a 1080-high canvas, scaled to the screen.
            float scale = Mathf.Clamp(Screen.height / VirtualHeight, 0.75f, 2f);
            var oldMatrix = GUI.matrix;
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));
            float height = Screen.height / scale - 32f;

            GUILayout.BeginArea(new Rect(16f, 16f, Width, height), DevStyle.PanelStyle);
            GUILayout.Label("DEV TOOLS", DevStyle.Title);

            GUILayout.BeginHorizontal();
            for (int i = 0; i < tabs.Count; i++)
                if (GUILayout.Button(tabs[i].Title, i == current ? DevStyle.TabActive : DevStyle.Tab)) SelectTab(i);
            GUILayout.EndHorizontal();
            GUILayout.Space(6f);

            scroll = GUILayout.BeginScrollView(scroll, false, false, GUIStyle.none, GUI.skin.verticalScrollbar, GUIStyle.none);
            tabs[current].Draw();
            GUILayout.EndScrollView();

            GUILayout.Label("F9 close  ·  F11 session recorder", DevStyle.MutedLabel);
            GUILayout.EndArea();

            GUI.matrix = oldMatrix;
        }
    }

    sealed class PlaceholderTab : IDevTab
    {
        readonly string message;
        public PlaceholderTab(string title, string message) { Title = title; this.message = message; }
        public string Title { get; }
        public void Draw() => GUILayout.Label(message, DevStyle.MutedLabel);
    }
}
