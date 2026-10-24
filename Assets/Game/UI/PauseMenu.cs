using Game.Audio;
using Game.Core;
using Game.Player;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace Game.UI
{
    /// <summary>
    /// Esc pauses (section 7), in UI Toolkit: time and sound stop, letterbox bars slide in, and the menu offers Return,
    /// Restart, Options and Quit. Options: mouse sensitivity, volumes, motion blur and the difficulty (with a slider
    /// for every Custom value). Choices are saved to the settings file.
    /// </summary>
    public sealed class PauseMenu : MonoBehaviour
    {
        VisualElement root, dim, barTop, barBottom, main, options, confirm, custom;
        Label confirmTitle, confirmDetail;
        System.Action confirmYes;
        readonly Button[] levelButtons = new Button[Difficulty.Levels.Length];
        float bars;
        bool prefsChanged, built;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void AttachToGame()
        {
            if (!Services.TryGet<PlayerController>(out _)) return; // (a look test: nothing to pause)
            if (Services.TryGet<GameRoot>(out var root) && root.GetComponent<PauseMenu>() == null)
                root.gameObject.AddComponent<PauseMenu>();
        }

        bool Paused => Services.TryGet<PauseState>(out var p) && p.Paused;
        /// <summary>The menu is up (other screens leave Esc alone while it is).</summary>
        public bool Open => Paused && root != null && root.style.display == DisplayStyle.Flex;

        void Start()
        {
            var ui = UiRoot.Get();
            if (ui == null) return;
            root = UiStyle.Fill(new VisualElement { name = "Pause" });
            root.style.display = DisplayStyle.None;
            ui.Menus.Add(root);
            dim = UiStyle.Fill(UiStyle.Box(root, new Color(0f, 0f, 0f, 0.45f)));
            barTop = UiStyle.Box(root, Color.black);
            barTop.style.position = Position.Absolute;
            barTop.style.left = barTop.style.right = 0;
            barTop.style.top = 0;
            barBottom = UiStyle.Box(root, Color.black);
            barBottom.style.position = Position.Absolute;
            barBottom.style.left = barBottom.style.right = 0;
            barBottom.style.bottom = 0;
            BuildMain();
            BuildOptions();
            BuildConfirm();
            built = true;
        }

        VisualElement Page()
        {
            var page = UiStyle.Fill(new VisualElement());
            // (Lined up down the left side.)
            page.style.alignItems = Align.FlexStart;
            page.style.justifyContent = Justify.Center;
            page.style.paddingLeft = UiStyle.MenuLeft;
            page.style.display = DisplayStyle.None;
            root.Add(page);
            return page;
        }

        void BuildMain()
        {
            main = Page();
            var title = UiStyle.Text(main, Hud.Spaced("Paused"), 40, UiStyle.Ink, TextAnchor.MiddleLeft);
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            title.style.marginBottom = 30;
            UiStyle.MenuButton(main, "Return", Close);
            UiStyle.MenuButton(main, "Restart", () => Confirm("Start over?", "Lamps go out, everything goes back.", Restart));
            UiStyle.MenuButton(main, "Options", () => Show(options));
            UiStyle.MenuButton(main, "Quit", () => Confirm("Quit?", "This run will be lost.", Quit));
        }

        void BuildConfirm()
        {
            confirm = Page();
            confirmTitle = UiStyle.Text(confirm, "", 40, UiStyle.Ink, TextAnchor.MiddleLeft);
            confirmTitle.style.unityFontStyleAndWeight = FontStyle.Bold;
            confirmDetail = UiStyle.Text(confirm, "", 15, UiStyle.Muted, TextAnchor.MiddleLeft);
            confirmDetail.style.marginTop = 10;
            confirmDetail.style.marginBottom = 24;
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            confirm.Add(row);
            var yes = UiStyle.MenuButton(row, "Yes", () => confirmYes?.Invoke(), 150f);
            yes.style.marginRight = 20;
            UiStyle.MenuButton(row, "No", () => Show(main), 150f);
        }

        void Confirm(string title, string detail, System.Action yes)
        {
            confirmTitle.text = Hud.Spaced(title);
            confirmDetail.text = detail;
            confirmYes = yes;
            Show(confirm);
        }

        void BuildOptions()
        {
            options = Page();
            var title = UiStyle.Text(options, Hud.Spaced("Options"), 34, UiStyle.Ink, TextAnchor.MiddleLeft);
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            title.style.marginBottom = 16;
            var scroll = new ScrollView(ScrollViewMode.Vertical);
            scroll.style.width = 680;
            scroll.style.height = 540;
            scroll.style.backgroundColor = UiStyle.Panel;
            scroll.style.paddingLeft = scroll.style.paddingRight = 24;
            scroll.style.paddingTop = scroll.style.paddingBottom = 12;
            UiStyle.Rounded(scroll, 4f);
            options.Add(scroll);
            var prefs = Services.TryGet<PlayerPreferences>(out var p) ? p : new PlayerPreferences();

            Header(scroll, "Controls");
            Slider(scroll, "Mouse sensitivity", prefs.mouseSensitivity, 0.1f, 5f, "0.00", v => prefs.mouseSensitivity = v);
            var keys = UiStyle.KeyHints(scroll, 12,
                ("W A S D", "Move"), ("Shift", "Run"), ("Ctrl/C", "Crouch"), ("Space", "Jump"),
                ("LMB", "Use, crank, take"), ("E", "Doors"), ("F", "Use what you hold"), ("1-8", "Pick an item"),
                ("G", "Drop it"), ("Tab", "Inventory"), ("Y", "Sit, move seat"), ("E", "Get out"), ("RMB", "Fob: car alarm"), ("Esc", "Pause"));
            keys.style.marginTop = 8;
            keys.style.marginBottom = 6;

            Header(scroll, "Sound");
            Slider(scroll, "Master volume", prefs.masterVolume, 0f, 1f, "P0", v => { prefs.masterVolume = v; Volumes(prefs); });
            Slider(scroll, "Effects volume", prefs.effectsVolume, 0f, 1f, "P0", v => { prefs.effectsVolume = v; Volumes(prefs); });
            Slider(scroll, "Music volume", prefs.musicVolume, 0f, 1f, "P0", v => { prefs.musicVolume = v; Volumes(prefs); });

            Header(scroll, "Picture");
            Toggle(scroll, "Motion blur", prefs.motionBlur, v => prefs.motionBlur = v);
            Slider(scroll, "Motion blur strength", prefs.motionBlurStrength, 0f, 1f, "P0", v => prefs.motionBlurStrength = v);
            Slider(scroll, "Depth of field", prefs.depthOfField, 0f, 1f, "P0", v => prefs.depthOfField = v);

            Header(scroll, "Difficulty");
            var levels = new VisualElement();
            levels.style.flexDirection = FlexDirection.Row;
            levels.style.justifyContent = Justify.SpaceBetween;
            scroll.Add(levels);
            for (int i = 0; i < Difficulty.Levels.Length; i++)
            {
                string level = Difficulty.Levels[i];
                levelButtons[i] = UiStyle.MenuButton(levels, level, () => PickLevel(level, prefs), 148f, 38f);
            }
            var note = UiStyle.Text(scroll, "Batteries in the forest change with the next world (Restart).", 13, UiStyle.Muted, TextAnchor.MiddleLeft);
            note.style.marginTop = 6;

            custom = new VisualElement();
            scroll.Add(custom);
            var d = prefs.customDifficulty ?? (prefs.customDifficulty = new DifficultySettings());
            Slider(custom, "Flashlight battery life", d.batteryLife, 0.25f, 3f, "0.00x", v => prefs.customDifficulty.batteryLife = v);
            Slider(custom, "Batteries in the forest", d.batteriesInForest, 0f, 3f, "0.00x", v => prefs.customDifficulty.batteriesInForest = v);
            Slider(custom, "Time between extra scours", d.timeBetweenScours, 0.25f, 3f, "0.00x", v => prefs.customDifficulty.timeBetweenScours = v);
            Slider(custom, "How many extra scours", d.extraScours, 0f, 3f, "0.00x", v => prefs.customDifficulty.extraScours = v);
            Slider(custom, "Time to face a scour down", d.faceDownTime, 0.25f, 3f, "0.00x", v => prefs.customDifficulty.faceDownTime = v);
            Slider(custom, "Lamp cranking speed", d.crankSpeed, 0.25f, 3f, "0.00x", v => prefs.customDifficulty.crankSpeed = v);
            Slider(custom, "How far the blind one hears", d.blindHearing, 0.25f, 3f, "0.00x", v => prefs.customDifficulty.blindHearing = v);
            Slider(custom, "Time for it to close in", d.blindCloseIn, 0.25f, 3f, "0.00x", v => prefs.customDifficulty.blindCloseIn = v);
            Slider(custom, "How many crawlers", d.crawlers, 0f, 3f, "0.00x", v => prefs.customDifficulty.crawlers = v);
            Slider(custom, "Crawlers from lamp", d.crawlersFromLamp, 1f, 10f, "0", v => prefs.customDifficulty.crawlersFromLamp = Mathf.RoundToInt(v));
            Slider(custom, "Time for moths to gather", d.mothTime, 0.25f, 3f, "0.00x", v => prefs.customDifficulty.mothTime = v);
            RefreshLevel();

            var back = UiStyle.MenuButton(options, "Back", () => Show(main));
            back.style.marginTop = 16;
        }

        void PickLevel(string level, PlayerPreferences prefs)
        {
            if (!Services.TryGet<Difficulty>(out var difficulty)) return;
            // Custom starts from whatever was chosen before, so it's a tweak, not a blank slate.
            if (level == "Custom" && difficulty.Level != "Custom") prefs.customDifficulty = difficulty.Current.Clone();
            difficulty.Level = level;
            prefsChanged = true;
            // The sliders show the copied values.
            options.RemoveFromHierarchy();
            BuildOptions();
            Show(options);
        }

        void RefreshLevel()
        {
            string level = Services.TryGet<Difficulty>(out var d) ? d.Level : "Normal";
            for (int i = 0; i < levelButtons.Length; i++)
            {
                bool on = Difficulty.Levels[i] == level;
                levelButtons[i].text = on ? $"[ {Hud.Spaced(Difficulty.Levels[i])} ]" : Hud.Spaced(Difficulty.Levels[i]);
                levelButtons[i].style.color = on ? UiStyle.Red : UiStyle.Ink;
            }
            custom.style.display = level == "Custom" ? DisplayStyle.Flex : DisplayStyle.None;
        }

        void Header(VisualElement parent, string text)
        {
            var h = UiStyle.Text(parent, Hud.Spaced(text), 15, UiStyle.Red, TextAnchor.MiddleLeft);
            h.style.unityFontStyleAndWeight = FontStyle.Bold;
            h.style.marginTop = 16;
            h.style.marginBottom = 6;
        }

        void Slider(VisualElement parent, string name, float value, float min, float max, string format, System.Action<float> changed)
        {
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;
            row.style.height = 30;
            parent.Add(row);
            var label = UiStyle.Text(row, name, 15, UiStyle.Ink, TextAnchor.MiddleLeft);
            label.style.width = 260;
            var slider = new Slider(min, max) { value = value };
            slider.style.width = 290;
            row.Add(slider);
            var shown = UiStyle.Text(row, Format(value, format), 14, UiStyle.Muted, TextAnchor.MiddleRight);
            shown.style.width = 70;
            slider.RegisterValueChangedCallback(e =>
            {
                changed(e.newValue);
                shown.text = Format(e.newValue, format);
                prefsChanged = true;
            });
        }

        void Toggle(VisualElement parent, string name, bool value, System.Action<bool> changed)
        {
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;
            row.style.height = 30;
            parent.Add(row);
            var label = UiStyle.Text(row, name, 15, UiStyle.Ink, TextAnchor.MiddleLeft);
            label.style.width = 260;
            var toggle = new Toggle { value = value };
            row.Add(toggle);
            toggle.RegisterValueChangedCallback(e =>
            {
                changed(e.newValue);
                prefsChanged = true;
            });
        }

        static string Format(float v, string format) => format.EndsWith("x") ? v.ToString(format.TrimEnd('x')) + "x" : v.ToString(format);

        static void Volumes(PlayerPreferences prefs)
        {
            if (Services.TryGet<AudioStateController>(out var audio)) audio.ApplyVolumes(prefs);
        }

        // ------------------------------------------------------------------ open and shut

        void Update()
        {
            if (!built) return;
            var keyboard = Keyboard.current;
            if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame && !OtherScreenOpen() && GameFlow.IsPlaying && InventoryScreen.EscUsedFrame != Time.frameCount)
            {
                if (!Paused) OpenMenu();
                else if (main.style.display != DisplayStyle.Flex) Show(main);
                else Close();
            }
            bars = Mathf.MoveTowards(bars, Paused ? 1f : 0f, Time.unscaledDeltaTime * 4f);
            root.style.display = bars > 0f ? DisplayStyle.Flex : DisplayStyle.None;
            float h = 110f * Mathf.SmoothStep(0f, 1f, bars);
            barTop.style.height = h;
            barBottom.style.height = h;
            dim.style.opacity = bars;
            float pages = Paused ? Mathf.Clamp01((bars - 0.5f) * 2f) : 0f;
            main.style.opacity = options.style.opacity = confirm.style.opacity = pages;
            root.pickingMode = Paused ? PickingMode.Position : PickingMode.Ignore;
        }

        // (The inventory screen and others handle Esc themselves while they're up.)
        static bool OtherScreenOpen() => Services.TryGet<UiState>(out var ui) && ui.AnyOpen && !(Services.TryGet<PauseState>(out var p) && p.Paused);

        void Show(VisualElement page)
        {
            var was = Current;
            foreach (var p in new[] { main, options, confirm }) p.style.display = p == page ? DisplayStyle.Flex : DisplayStyle.None;
            if (page == options) RefreshLevel();
            // Each page comes together out of motes, the way a scour does.
            if (page != was && Services.TryGet<UiMotes>(out var motes)) motes.Materialize(page);
        }

        VisualElement Current
        {
            get
            {
                foreach (var p in new[] { main, options, confirm }) if (p.style.display == DisplayStyle.Flex) return p;
                return null;
            }
        }

        void OpenMenu()
        {
            // Not while being taken: the fade is already running.
            if (Services.TryGet<CatchFlow>(out var catchFlow) && catchFlow.Active) return;
            if (!Services.TryGet<PauseState>(out var pause)) return;
            pause.Pause();
            foreach (var p in new[] { main, options, confirm }) p.style.display = DisplayStyle.None; // (so main comes in fresh)
            Show(main);
            UnityEngine.Cursor.lockState = CursorLockMode.None;
            UnityEngine.Cursor.visible = true;
        }

        void Close()
        {
            // ...and comes apart again as the game carries on.
            if (Services.TryGet<UiMotes>(out var motes)) motes.Dissolve(Current);
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
    }
}
