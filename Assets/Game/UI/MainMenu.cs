using System.Collections;
using System.Collections.Generic;
using Game.Audio;
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
                filmCam.farClipPlane = Mathf.Max(playerCam.farClipPlane, 750f); // (out over the endless forest)
                filmCam.cullingMask = playerCam.cullingMask;
                playerCam.enabled = false;
                playerEars = playerCam.GetComponent<AudioListener>();
                if (playerEars != null) playerEars.enabled = false;
            }
            go.AddComponent<UnityEngine.Rendering.HighDefinition.HDAdditionalCameraData>();
            go.AddComponent<AudioListener>();
            film = new MenuFilm(filmCam, config.shotTime);
            Game.Rendering.LookController.MenuLift = 1f;
            Game.World.Panorama.On = true;
        }

        void StopFilm()
        {
            Game.Rendering.LookController.MenuLift = 0f;
            Game.World.Panorama.On = false;
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
            if (config.glitchIntro && narration != null)
            {
                yield return GlitchIntro(narration, flow);
                yield break;
            }
            int wake = Mathf.Max(0, config.linesBeforeWaking);
            if (narration != null) yield return narration.Intro(0, wake);

            flow?.Set(GamePhase.Playing);
            if (Services.TryGet<PlayerController>(out var player)) player.StartGroggy(config.wakeGroggyTime);
            if (narration != null) narration.StartCoroutine(narration.IntroThenFinish(wake));
            float fade = Mathf.Max(0.2f, config.wakeFadeTime);
            bool coming = false;
            for (float t = 0f; t < fade && black != null; t += Time.unscaledDeltaTime)
            {
                // Eyes opening: slow to start, a little lift, slow to finish.
                float x = t / fade;
                black.style.opacity = 1f - x * x * x * (x * (x * 6f - 15f) + 10f);
                // The grogginess only starts to wear off once you can see (as the black lifts past 70%).
                if (!coming && black.style.opacity.value < 0.3f && player != null)
                {
                    coming = true;
                    player.StartGroggy(config.wakeGroggyTime);
                }
                yield return null;
            }
            if (black != null) black.RemoveFromHierarchy();
            if (root != null) root.RemoveFromHierarchy();
            Destroy(this);
        }

        // The glitching intro: each line in the middle of the dark; between them the dark tears and for a moment
        // you see through your own eyes, in the forest (groggy, unable to move), then it tears back for the next
        // line. After the last, you wake as usual, and the giant answers.
        IEnumerator GlitchIntro(Narration narration, GameFlow flow)
        {
            Services.TryGet<PlayerController>(out var player);
            int lines = narration.IntroLines;
            float total = lines * (config.lineTime * 1.4f + 1f + config.glitchPeek);
            if (player != null) player.StartGroggy(total + config.wakeGroggyTime);
            var ui = UiRoot.Get();
            var tears = new List<VisualElement>();
            for (int i = 0; i < 16 && ui != null; i++)
            {
                var bar = UiStyle.Box(ui.Top, Color.black);
                bar.style.position = Position.Absolute;
                bar.style.left = 0;
                bar.style.right = 0;
                bar.style.display = DisplayStyle.None;
                tears.Add(bar);
            }
            for (int i = 0; i < lines; i++)
            {
                yield return narration.MiddleLine(i);
                if (i < lines - 1) yield return Glitch(tears, Mathf.Max(0.3f, config.glitchPeek));
            }
            foreach (var bar in tears) bar.RemoveFromHierarchy();

            // Awake: as the ordinary intro does after its first line.
            flow?.Set(GamePhase.Playing);
            if (player != null) player.StartGroggy(config.wakeGroggyTime);
            float fade = Mathf.Max(0.2f, config.wakeFadeTime);
            for (float t = 0f; t < fade && black != null; t += Time.unscaledDeltaTime)
            {
                float x = t / fade;
                black.style.opacity = 1f - x * x * x * (x * (x * 6f - 15f) + 10f);
                yield return null;
            }
            Narration.Finished();
            if (black != null) black.RemoveFromHierarchy();
            if (root != null) root.RemoveFromHierarchy();
            Destroy(this);
        }

        // The dark tearing open (black flickering, bars of noise and colour across the screen, a burst of
        // static), a moment of seeing, and tearing shut again.
        IEnumerator Glitch(List<VisualElement> tears, float peek)
        {
            AudioSource crackle = null;
            if (Services.TryGet<SoundPlayer>(out var sounds)) crackle = sounds.Play2D(Sound.Static, MixerNames.UI, 0.45f, Random.Range(0.8f, 1.2f));
            const float tear = 0.28f;
            for (float t = 0f; t < tear; t += Time.unscaledDeltaTime)
            {
                Tear(tears, 1f - t / tear, 1f);
                yield return null;
            }
            if (crackle != null) crackle.Stop();
            Tear(tears, 0f, 0f);
            for (float t = 0f; t < peek; t += Time.unscaledDeltaTime)
            {
                // (now and then a flicker of it even while you see)
                Tear(tears, 0f, Random.value < 0.08f ? 0.3f : 0f);
                yield return null;
            }
            if (sounds != null) crackle = sounds.Play2D(Sound.Static, MixerNames.UI, 0.45f, Random.Range(0.8f, 1.2f));
            for (float t = 0f; t < tear; t += Time.unscaledDeltaTime)
            {
                Tear(tears, t / tear, 1f);
                yield return null;
            }
            if (crackle != null) crackle.Stop();
            Tear(tears, 1f, 0f);
        }

        // One frame of the tear: the black on (with this chance), and this share of the bars thrown across it.
        void Tear(List<VisualElement> tears, float dark, float bars)
        {
            if (black != null) black.style.opacity = Random.value < dark ? 1f : Random.value < 0.3f ? 0.6f : 0f;
            foreach (var bar in tears)
            {
                bool on = Random.value < bars * 0.7f;
                bar.style.display = on ? DisplayStyle.Flex : DisplayStyle.None;
                if (!on) continue;
                bar.style.top = Random.Range(0f, UiRoot.Height);
                bar.style.height = Random.value < 0.7f ? Random.Range(2f, 14f) : Random.Range(20f, 90f);
                bar.style.translate = new Translate(Random.Range(-120f, 120f), 0);
                float k = Random.value;
                bar.style.backgroundColor = k < 0.45f ? new Color(0f, 0f, 0f, 0.95f)
                    : k < 0.7f ? new Color(0.1f, 0.35f, 0.33f, 0.55f)
                    : k < 0.88f ? new Color(0.45f, 0.04f, 0.04f, 0.5f)
                    : new Color(0.85f, 0.85f, 0.85f, 0.35f);
            }
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
