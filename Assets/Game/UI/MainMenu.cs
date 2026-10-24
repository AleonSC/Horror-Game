using System.Collections;
using Game.Core;
using Game.Player;
using Game.World.Build;
using UnityEngine;
using UnityEngine.UIElements;

namespace Game.UI
{
    /// <summary>
    /// The start of a session (section 7). Boot: black, with a faint line filling as the world and the music are
    /// made. Then the main menu: the title and START / DIFFICULTY / QUIT over cinematic letterbox bars, a slow
    /// film of the forest behind them. START fades to black, the intro narration plays, and the forest fades in
    /// around you. The menu shows once per session: Restart goes straight back into the forest.
    /// </summary>
    public sealed class MainMenu : MonoBehaviour
    {
        PresentationConfig config;
        VisualElement root, black, barTop, barBottom, buttons, levels, progressBack, progressFill;
        Label title;
        Camera filmCam, playerCam;
        AudioListener playerEars;
        MenuFilm film;
        bool started;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void AttachToGame()
        {
            if (!Services.TryGet<PlayerController>(out _) || !Services.TryGet<WorldBuilder>(out _)) return; // (the forest only)
            var setup = Services.Get<GameSetup>();
            var config = setup.Get<PresentationConfig>();
            if (config != null && !config.showMainMenu) return;
            if (!Services.TryGet<GameRoot>(out var root) || root.GetComponent<MainMenu>() != null) return;
            // Nothing runs under the menu: no clock, no hunters, no moving.
            if (Services.TryGet<GameFlow>(out var flow)) flow.Set(GamePhase.Menu);
            root.gameObject.AddComponent<MainMenu>();
        }

        void Awake()
        {
            config = Services.Get<GameSetup>().Get<PresentationConfig>();
            if (config == null) config = ScriptableObject.CreateInstance<PresentationConfig>();
        }

        void Start()
        {
            var ui = UiRoot.Get();
            if (ui == null) { Begin(); return; }
            Build(ui);
            StartCoroutine(Run());
        }

        void Build(UiRoot ui)
        {
            root = UiStyle.Fill(new VisualElement { name = "Main menu" });
            ui.Menus.Add(root);
            barTop = UiStyle.Box(root, Color.black);
            barTop.style.position = Position.Absolute;
            barTop.style.left = barTop.style.right = barTop.style.top = 0;
            barTop.style.height = 110;
            barBottom = UiStyle.Box(root, Color.black);
            barBottom.style.position = Position.Absolute;
            barBottom.style.left = barBottom.style.right = barBottom.style.bottom = 0;
            barBottom.style.height = 110;

            var column = UiStyle.Fill(new VisualElement());
            // (Lined up down the left side.)
            column.style.alignItems = Align.FlexStart;
            column.style.justifyContent = Justify.Center;
            column.style.paddingLeft = UiStyle.MenuLeft;
            root.Add(column);
            title = UiStyle.Text(column, Hud.Spaced(config.Title), 52, UiStyle.Ink, TextAnchor.MiddleLeft, 6f);
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            title.style.marginBottom = 50;
            buttons = new VisualElement();
            buttons.style.alignItems = Align.FlexStart;
            column.Add(buttons);
            UiStyle.MenuButton(buttons, "Start", Begin);
            UiStyle.MenuButton(buttons, "Difficulty", () => levels.style.display = levels.style.display == DisplayStyle.Flex ? DisplayStyle.None : DisplayStyle.Flex);
            UiStyle.MenuButton(buttons, "Quit", Quit);
            levels = new VisualElement();
            levels.style.flexDirection = FlexDirection.Row;
            levels.style.marginTop = 10;
            levels.style.display = DisplayStyle.None;
            column.Add(levels);
            RefreshLevels();

            // Black over everything at first; the boot line in the middle.
            black = UiStyle.Fill(UiStyle.Box(ui.Top, Color.black));
            progressBack = UiStyle.Box(black, new Color(1f, 1f, 1f, 0.06f));
            progressBack.style.position = Position.Absolute;
            progressBack.style.left = Length.Percent(50);
            progressBack.style.top = Length.Percent(50);
            progressBack.style.width = 240;
            progressBack.style.height = 1;
            progressBack.style.marginLeft = -120;
            progressFill = UiStyle.Box(progressBack, new Color(1f, 1f, 1f, 0.35f));
            progressFill.style.height = Length.Percent(100);
            progressFill.style.width = 0;
            root.style.display = DisplayStyle.None;
        }

        void RefreshLevels()
        {
            levels.Clear();
            string current = Services.TryGet<Difficulty>(out var d) ? d.Level : "Normal";
            foreach (var level in Difficulty.Levels)
            {
                string l = level;
                var b = UiStyle.MenuButton(levels, current == l ? $"[{l}]" : l, () =>
                {
                    if (Services.TryGet<Difficulty>(out var diff))
                    {
                        if (l == "Custom" && diff.Level != "Custom" && Services.TryGet<PlayerPreferences>(out var p)) p.customDifficulty = diff.Current.Clone();
                        diff.Level = l;
                        if (Services.TryGet<PlayerPreferences>(out var prefs)) prefs.Save(PlayerPreferences.DefaultPath);
                    }
                    RefreshLevels();
                }, 150f, 38f);
                b.style.marginLeft = 0;
                b.style.marginRight = 10;
                if (current == l) b.style.color = UiStyle.Red;
            }
        }

        IEnumerator Run()
        {
            // Boot: wait (on black) until the world and the music are made.
            float waited = 0f;
            while (waited < 15f)
            {
                bool world = Services.TryGet<WorldBuilder>(out var w) && w.Plan != null;
                bool music = !Services.TryGet<Game.Audio.MusicManager>(out var m) || m.Ready;
                progressFill.style.width = 240f * ((world ? 0.6f : 0.1f) + (music ? 0.4f : 0f));
                if (world && music) break;
                waited += Time.unscaledDeltaTime;
                yield return null;
            }
            progressBack.style.display = DisplayStyle.None;

            StartFilm();
            root.style.display = DisplayStyle.Flex;
            FreeMouse();
            while (!started)
            {
                film.Update(Time.unscaledDeltaTime);
                black.style.opacity = film.Black;
                FreeMouse();
                yield return null;
            }
        }

        void StartFilm()
        {
            if (Services.TryGet<PlayerController>(out var player)) playerCam = player.View;
            var go = new GameObject("Film camera") { tag = "MainCamera" };
            filmCam = go.AddComponent<Camera>();
            if (playerCam != null)
            {
                filmCam.fieldOfView = 55f;
                filmCam.nearClipPlane = 0.1f;
                filmCam.farClipPlane = playerCam.farClipPlane;
                filmCam.cullingMask = playerCam.cullingMask;
                playerCam.enabled = false;
                playerEars = playerCam.GetComponent<AudioListener>();
                if (playerEars != null) playerEars.enabled = false;
            }
            go.AddComponent<UnityEngine.Rendering.HighDefinition.HDAdditionalCameraData>();
            go.AddComponent<AudioListener>();
            film = new MenuFilm(filmCam, config.shotTime);
        }

        void StopFilm()
        {
            if (filmCam != null) Destroy(filmCam.gameObject);
            if (playerCam != null) playerCam.enabled = true;
            if (playerEars != null) playerEars.enabled = true;
        }

        static void FreeMouse()
        {
            UnityEngine.Cursor.lockState = CursorLockMode.None;
            UnityEngine.Cursor.visible = true;
        }

        // START: to black, the intro, then the forest fades in round you.
        void Begin()
        {
            if (started) return;
            started = true;
            StopAllCoroutines();
            StartCoroutine(Intro());
        }

        IEnumerator Intro()
        {
            var flow = Services.TryGet<GameFlow>(out var f) ? f : null;
            float from = black != null ? black.resolvedStyle.opacity : 0f;
            for (float t = 0f; t < 1f && black != null; t += Time.unscaledDeltaTime)
            {
                black.style.opacity = Mathf.Lerp(from, 1f, t);
                yield return null;
            }
            if (black != null) black.style.opacity = 1f;
            if (root != null) root.style.display = DisplayStyle.None;
            StopFilm();
            flow?.Set(GamePhase.Intro);
            PlayerController.SetCursorCaptured(true);

            // The first line(s) over black ("wake up"), then you wake in the forest, groggy, and can move while the
            // rest are said.
            Services.TryGet<Narration>(out var narration);
            int wake = Mathf.Max(0, config.linesBeforeWaking);
            if (narration != null) yield return narration.Intro(0, wake);

            flow?.Set(GamePhase.Playing);
            if (Services.TryGet<PlayerController>(out var player)) player.StartGroggy(config.wakeGroggyTime);
            if (narration != null) narration.StartCoroutine(narration.Intro(wake));
            float fade = Mathf.Max(0.2f, config.wakeFadeTime);
            for (float t = 0f; t < fade && black != null; t += Time.unscaledDeltaTime)
            {
                black.style.opacity = 1f - Mathf.SmoothStep(0f, 1f, t / fade);
                yield return null;
            }
            if (black != null) black.RemoveFromHierarchy();
            if (root != null) root.RemoveFromHierarchy();
            Destroy(this);
        }

        void Quit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}
