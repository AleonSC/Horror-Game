using Game.Audio;
using Game.Core;
using Game.Player;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Game.UI
{
    /// <summary>
    /// Esc pauses (section 7): time and sound stop, letterbox bars come in, and the menu offers Return, Restart,
    /// Options and Quit. Options: mouse sensitivity, volumes, motion blur and the difficulty (with a slider for
    /// every Custom value). A plain first version, like the HUD, until the UI Toolkit one in milestone 7. Laid
    /// out on a 1600 x 900 canvas, scaled.
    /// </summary>
    public sealed class PauseMenu : MonoBehaviour
    {
        const float W = 1600f, H = 900f;
        static readonly Color Ink = new Color(0.86f, 0.84f, 0.8f, 1f);
        static readonly Color Muted = new Color(0.6f, 0.58f, 0.55f, 1f);
        static readonly Color Red = new Color(0.85f, 0.08f, 0.05f, 1f);

        enum Page { Main, Options, ConfirmRestart, ConfirmQuit }

        Page page;
        float bars;   // 0-1: the letterbox bars sliding in
        Vector2 scroll;
        GUIStyle title, button, label, small;
        bool prefsChanged;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void AttachToGame()
        {
            if (!Services.TryGet<PlayerController>(out _)) return; // (a look test: nothing to pause)
            if (Services.TryGet<GameRoot>(out var root) && root.GetComponent<PauseMenu>() == null)
                root.gameObject.AddComponent<PauseMenu>();
        }

        bool Paused => Services.TryGet<PauseState>(out var p) && p.Paused;

        void Update()
        {
            var keyboard = Keyboard.current;
            if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame)
            {
                if (!Paused) Open();
                else if (page != Page.Main) page = Page.Main;
                else Close();
            }
            bars = Mathf.MoveTowards(bars, Paused ? 1f : 0f, Time.unscaledDeltaTime * 4f);
        }

        void Open()
        {
            // Not while being taken: the fade is already running.
            if (Services.TryGet<CatchFlow>(out var catchFlow) && catchFlow.Active) return;
            if (!Services.TryGet<PauseState>(out var pause)) return;
            pause.Pause();
            page = Page.Main;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        void Close()
        {
            SavePrefs();
            if (Services.TryGet<PauseState>(out var pause)) pause.Resume();
            PlayerController.SetCursorCaptured(true);
        }

        void SavePrefs()
        {
            if (!prefsChanged || !Services.TryGet<PlayerPreferences>(out var prefs)) return;
            prefs.Save(PlayerPreferences.DefaultPath);
            prefsChanged = false;
        }

        void OnGUI()
        {
            if (bars <= 0f) return;
            GUI.depth = -10; // (over the HUD)
            EnsureStyles();
            float scale = Mathf.Min(Screen.width / W, Screen.height / H);
            var old = GUI.matrix;
            GUI.matrix = Matrix4x4.TRS(new Vector3((Screen.width - W * scale) * 0.5f, (Screen.height - H * scale) * 0.5f, 0f), Quaternion.identity, new Vector3(scale, scale, 1f));

            // Dim everything, and the letterbox bars slide in from top and bottom.
            var origin = GUI.matrix.inverse.MultiplyPoint(Vector3.zero);
            var full = GUI.matrix.inverse.MultiplyPoint(new Vector3(Screen.width, Screen.height, 0f));
            Fill(new Rect(origin.x, origin.y, full.x - origin.x, full.y - origin.y), new Color(0f, 0f, 0f, 0.45f * bars));
            float bar = 110f * Mathf.SmoothStep(0f, 1f, bars);
            Fill(new Rect(origin.x, origin.y, full.x - origin.x, bar - origin.y), Color.black);
            Fill(new Rect(origin.x, H - bar, full.x - origin.x, full.y - (H - bar)), Color.black);

            if (Paused && bars > 0.5f)
            {
                switch (page)
                {
                    case Page.Main: DrawMain(); break;
                    case Page.Options: DrawOptions(); break;
                    case Page.ConfirmRestart: DrawConfirm("START OVER?", "Lamps go out, everything goes back.", Restart); break;
                    case Page.ConfirmQuit: DrawConfirm("QUIT?", "This run will be lost.", Quit); break;
                }
            }
            GUI.matrix = old;
        }

        void DrawMain()
        {
            Text(new Rect(0, 230, W, 60), "P A U S E D", title, Ink);
            float y = 340;
            if (Button(new Rect(W / 2 - 150, y, 300, 46), "RETURN")) Close();
            if (Button(new Rect(W / 2 - 150, y += 60, 300, 46), "RESTART")) page = Page.ConfirmRestart;
            if (Button(new Rect(W / 2 - 150, y += 60, 300, 46), "OPTIONS")) page = Page.Options;
            if (Button(new Rect(W / 2 - 150, y += 60, 300, 46), "QUIT")) page = Page.ConfirmQuit;
        }

        void DrawConfirm(string question, string detail, System.Action yes)
        {
            Text(new Rect(0, 280, W, 60), question, title, Ink);
            Text(new Rect(0, 340, W, 30), detail, small, Muted);
            if (Button(new Rect(W / 2 - 160, 410, 150, 46), "YES")) yes();
            if (Button(new Rect(W / 2 + 10, 410, 150, 46), "NO")) page = Page.Main;
        }

        void Restart()
        {
            SavePrefs();
            PlayerController.SetCursorCaptured(true);
            if (Services.TryGet<GameRoot>(out var root)) root.Restart(); // (it resumes too)
        }

        void Quit()
        {
            SavePrefs();
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        void DrawOptions()
        {
            if (!Services.TryGet<PlayerPreferences>(out var prefs)) return;
            Text(new Rect(0, 130, W, 50), "O P T I O N S", title, Ink);
            var area = new Rect(W / 2 - 330, 195, 660, 560);
            GUILayout.BeginArea(area);
            scroll = GUILayout.BeginScrollView(scroll, GUIStyle.none, GUI.skin.verticalScrollbar);

            Header("CONTROLS");
            prefs.mouseSensitivity = Slider("Mouse sensitivity", prefs.mouseSensitivity, 0.1f, 5f, "0.00");

            Header("SOUND");
            float master = Slider("Master volume", prefs.masterVolume, 0f, 1f, "P0");
            float effects = Slider("Effects volume", prefs.effectsVolume, 0f, 1f, "P0");
            float music = Slider("Music volume", prefs.musicVolume, 0f, 1f, "P0");
            if (master != prefs.masterVolume || effects != prefs.effectsVolume || music != prefs.musicVolume)
            {
                prefs.masterVolume = master;
                prefs.effectsVolume = effects;
                prefs.musicVolume = music;
                if (Services.TryGet<AudioStateController>(out var audio)) audio.ApplyVolumes(prefs);
            }

            Header("PICTURE");
            prefs.motionBlur = Toggle("Motion blur", prefs.motionBlur);
            if (prefs.motionBlur) prefs.motionBlurStrength = Slider("Motion blur strength", prefs.motionBlurStrength, 0f, 1f, "P0");

            Header("DIFFICULTY");
            if (Services.TryGet<Difficulty>(out var difficulty)) DrawDifficulty(difficulty, prefs);

            GUILayout.EndScrollView();
            GUILayout.EndArea();
            if (GUI.changed) prefsChanged = true;
            if (Button(new Rect(W / 2 - 150, 770, 300, 46), "BACK")) page = Page.Main;
        }

        void DrawDifficulty(Difficulty difficulty, PlayerPreferences prefs)
        {
            GUILayout.BeginHorizontal();
            foreach (var level in Difficulty.Levels)
            {
                bool on = difficulty.Level == level;
                var style = new GUIStyle(button) { normal = { textColor = on ? Red : Ink } };
                if (GUILayout.Button(on ? $"[ {level.ToUpperInvariant()} ]" : level.ToUpperInvariant(), style, GUILayout.Height(38)))
                {
                    // Custom starts from whatever was chosen before, so it's a tweak, not a blank slate.
                    if (level == "Custom" && difficulty.Level != "Custom") prefs.customDifficulty = difficulty.Current.Clone();
                    difficulty.Level = level;
                }
            }
            GUILayout.EndHorizontal();
            Small("Batteries in the forest change with the next world (Restart).");

            if (difficulty.Level != "Custom") return;
            var d = prefs.customDifficulty;
            d.batteryLife = Slider("Flashlight battery life", d.batteryLife, 0.25f, 3f, "0.00x");
            d.batteriesInForest = Slider("Batteries in the forest", d.batteriesInForest, 0f, 3f, "0.00x");
            d.timeBetweenScours = Slider("Time between extra scours", d.timeBetweenScours, 0.25f, 3f, "0.00x");
            d.extraScours = Slider("How many extra scours", d.extraScours, 0f, 3f, "0.00x");
            d.faceDownTime = Slider("Time to face a scour down", d.faceDownTime, 0.25f, 3f, "0.00x");
            d.crankSpeed = Slider("Lamp cranking speed", d.crankSpeed, 0.25f, 3f, "0.00x");
            Small("Later: the blind one, crawlers and moths (they arrive in milestones 5 and 6).");
            d.blindHearing = Slider("How far the blind one hears", d.blindHearing, 0.25f, 3f, "0.00x");
            d.blindCloseIn = Slider("Time for it to close in", d.blindCloseIn, 0.25f, 3f, "0.00x");
            d.crawlers = Slider("How many crawlers", d.crawlers, 0f, 3f, "0.00x");
            d.crawlersFromLamp = Mathf.RoundToInt(Slider("Crawlers from lamp", d.crawlersFromLamp, 1f, 10f, "0"));
            d.mothTime = Slider("Time for moths to gather", d.mothTime, 0.25f, 3f, "0.00x");
        }

        // ------------------------------------------------------------------ drawing helpers

        void Header(string text)
        {
            GUILayout.Space(14);
            GUILayout.Label(Hud.Spaced(text), label);
        }

        void Small(string text) => GUILayout.Label(text, small);

        float Slider(string name, float value, float min, float max, string format)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(name, small, GUILayout.Width(250));
            float v = GUILayout.HorizontalSlider(value, min, max, GUILayout.Width(300));
            string shown = format.EndsWith("x") ? v.ToString(format.TrimEnd('x')) + "x" : v.ToString(format);
            GUILayout.Label(shown, small, GUILayout.Width(70));
            GUILayout.EndHorizontal();
            return v;
        }

        bool Toggle(string name, bool value)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(name, small, GUILayout.Width(250));
            bool v = GUILayout.Toggle(value, value ? " ON" : " OFF", small);
            GUILayout.EndHorizontal();
            return v;
        }

        bool Button(Rect r, string text)
        {
            bool hover = r.Contains(Event.current.mousePosition);
            Fill(r, new Color(0.05f, 0.05f, 0.06f, hover ? 0.95f : 0.7f));
            if (hover) Fill(new Rect(r.x, r.yMax - 2, r.width, 2), Red);
            return GUI.Button(r, Hud.Spaced(text), button);
        }

        void EnsureStyles()
        {
            if (title != null) return;
            title = new GUIStyle { fontSize = 40, alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };
            button = new GUIStyle { fontSize = 18, alignment = TextAnchor.MiddleCenter, normal = { textColor = Ink }, hover = { textColor = Color.white } };
            label = new GUIStyle { fontSize = 15, fontStyle = FontStyle.Bold, normal = { textColor = Red }, margin = new RectOffset(0, 0, 6, 4) };
            small = new GUIStyle { fontSize = 15, normal = { textColor = Ink }, margin = new RectOffset(0, 0, 4, 4) };
        }

        static void Text(Rect r, string text, GUIStyle style, Color c)
        {
            style.normal.textColor = c;
            GUI.Label(r, text, style);
        }

        static void Fill(Rect r, Color c)
        {
            var old = GUI.color;
            GUI.color = c;
            GUI.DrawTexture(r, Texture2D.whiteTexture);
            GUI.color = old;
        }
    }
}
