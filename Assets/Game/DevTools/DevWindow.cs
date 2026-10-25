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
        const float MinWidth = 460f, MinHeight = 260f, Grip = 10f;

        readonly List<IDevTab> tabs = new List<IDevTab>();
        int current;
        bool open;
        Vector2 scroll;
        CursorLockMode savedLock;
        bool savedVisible;
        float width = 680f, height;
        int resizing; // 0 no, 1 the right edge, 2 the bottom edge, 3 the corner

        public bool IsOpen => open;

        /// <summary>Frame time in milliseconds, smoothed (the plan's budget: under 16.6, under 8 in the look test).</summary>
        public float FrameMs { get; private set; }

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
            AddTab(new LightingTab());
            AddTab(new LookTab());
            AddTab(new HuntersTab());
            AddTab(new RevealTab());
            AddTab(new SoundsTab());

            if (Services.TryGet<PlayerPreferences>(out var prefs))
            {
                current = Mathf.Max(0, IndexOf(prefs.devTab));
                width = prefs.devWidth;
                height = prefs.devHeight;
            }
        }

        void OnDestroy() => Services.Unregister(this);

        public void AddTab(IDevTab tab)
        {
            if (tab != null && !tabs.Contains(tab)) tabs.Add(tab);
        }

        void Update()
        {
            float ms = Time.unscaledDeltaTime * 1000f;
            FrameMs = FrameMs <= 0f ? ms : Mathf.Lerp(FrameMs, ms, 0.05f);

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
            if (Services.TryGet<UiState>(out var ui))
            {
                if (open) ui.PanelOpened(takesKeys: false); // (you can still walk with it open)
                else
                {
                    ui.PanelClosed(takesKeys: false);
                    ui.Typing = false;
                }
            }
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
            // While the search box has the keyboard, typing doesn't walk the player.
            if (Event.current.type == EventType.Layout && Services.TryGet<UiState>(out var typingUi))
                typingUi.Typing = GUIUtility.keyboardControl != 0;

            // Lay out on a 1080-high canvas, scaled to the screen.
            float scale = Mathf.Clamp(Screen.height / VirtualHeight, 0.75f, 2f);
            var oldMatrix = GUI.matrix;
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));
            float maxW = Screen.width / scale - 32f, maxH = Screen.height / scale - 32f;
            float w = Mathf.Clamp(width, MinWidth, Mathf.Max(MinWidth, maxW));
            float h = height <= 0f ? maxH : Mathf.Clamp(height, MinHeight, Mathf.Max(MinHeight, maxH));
            var area = new Rect(16f, 16f, w, h);
            Resize(area, maxW, maxH);
            DevStyle.LabelWidth = Mathf.Clamp(w * 0.36f, 150f, 340f);

            GUILayout.BeginArea(area, DevStyle.PanelStyle);
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

            // The corner grip.
            var grip = new Rect(area.xMax - 16f, area.yMax - 16f, 14f, 14f);
            GUI.Label(grip, "◢", DevStyle.MutedLabel);

            GUI.matrix = oldMatrix;
        }

        // Drag the right edge, the bottom edge or the corner to resize; it's remembered.
        void Resize(Rect area, float maxW, float maxH)
        {
            var e = Event.current;
            var right = new Rect(area.xMax - Grip, area.y, Grip * 2f, area.height);
            var bottom = new Rect(area.x, area.yMax - Grip, area.width, Grip * 2f);
            var corner = new Rect(area.xMax - 20f, area.yMax - 20f, 30f, 30f);
            switch (e.type)
            {
                case EventType.MouseDown when e.button == 0:
                    resizing = corner.Contains(e.mousePosition) ? 3 : right.Contains(e.mousePosition) ? 1 : bottom.Contains(e.mousePosition) ? 2 : 0;
                    if (resizing != 0)
                    {
                        if (height <= 0f) height = area.height;
                        e.Use();
                    }
                    break;
                case EventType.MouseDrag when resizing != 0:
                    if (resizing != 2) width = Mathf.Clamp(e.mousePosition.x - area.x, MinWidth, Mathf.Max(MinWidth, maxW));
                    if (resizing != 1) height = Mathf.Clamp(e.mousePosition.y - area.y, MinHeight, Mathf.Max(MinHeight, maxH));
                    e.Use();
                    break;
                case EventType.MouseUp when resizing != 0:
                    resizing = 0;
                    // (Dragged to the bottom: full height again.)
                    if (height >= maxH - 4f) height = 0f;
                    if (Services.TryGet<PlayerPreferences>(out var prefs))
                    {
                        prefs.devWidth = width;
                        prefs.devHeight = height;
                        prefs.Save(PlayerPreferences.DefaultPath);
                    }
                    e.Use();
                    break;
            }
        }
    }
}
