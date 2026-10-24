using System.Collections;
using System.Collections.Generic;
using Game.Core;
using UnityEngine;
using UnityEngine.UIElements;

namespace Game.UI
{
    /// <summary>
    /// The narration (section 7), along the bottom of the screen: the intro, line by line (capitals; *word* red, **word** red and
    /// lingering, ***word*** lingering long), and lines on lighting certain lamps, low on the screen over the game
    /// ("-word" red; the 5th lamp's frantic: red, bold and shaking).
    /// </summary>
    public sealed class Narration : MonoBehaviour
    {
        PresentationConfig config;
        Label centre, lower;
        readonly Queue<(string text, bool frantic)> queue = new Queue<(string, bool)>();
        bool showing;
        System.IDisposable lampSub;

        public bool Speaking => showing || queue.Count > 0;

        const float Bottom = 105f; // (the lines' distance from the bottom of the 900-high screen)

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void AttachToGame()
        {
            if (!Services.TryGet<Game.Player.PlayerController>(out _)) return;
            if (Services.TryGet<GameRoot>(out var root) && root.GetComponent<Narration>() == null)
                root.gameObject.AddComponent<Narration>();
        }

        void Awake()
        {
            config = Services.Get<GameSetup>().Get<PresentationConfig>();
            if (config == null) config = ScriptableObject.CreateInstance<PresentationConfig>();
            Services.Register(this);
        }

        void Start()
        {
            var ui = UiRoot.Get();
            if (ui == null) return;
            // Both low on the screen, like subtitles.
            centre = UiStyle.Text(ui.Top, "", 30, UiStyle.Ink, TextAnchor.MiddleCenter, 4f);
            centre.style.position = Position.Absolute;
            centre.style.left = 0;
            centre.style.right = 0;
            centre.style.bottom = Bottom;
            centre.style.opacity = 0f;
            lower = UiStyle.Text(ui.Hud, "", 22, UiStyle.Ink, TextAnchor.MiddleCenter, 2f);
            lower.style.position = Position.Absolute;
            lower.style.left = 0;
            lower.style.right = 0;
            lower.style.bottom = Bottom;
            lower.style.opacity = 0f;
            if (Services.TryGet<EventBus>(out var events)) lampSub = events.Subscribe<LampLit>(OnLamp);
        }

        void OnDestroy()
        {
            lampSub?.Dispose();
            Services.Unregister(this);
        }

        void OnLamp(LampLit e)
        {
            foreach (var set in config.lampLines)
            {
                if (set.lamp != e.TimesLit || set.lines == null) continue;
                foreach (var line in set.lines) queue.Enqueue((line, set.frantic));
            }
            if (!showing && queue.Count > 0) StartCoroutine(SayLamp());
        }

        IEnumerator SayLamp()
        {
            showing = true;
            while (queue.Count > 0)
            {
                var (text, frantic) = queue.Dequeue();
                lower.text = NarrationText.Lamp(text, frantic);
                lower.style.fontSize = frantic ? 30 : 22;
                lower.style.unityFontStyleAndWeight = frantic ? FontStyle.Bold : FontStyle.Normal;
                float hold = config.lineTime + text.Length * 0.03f;
                yield return Fade(lower, 0f, 1f, 0.6f, frantic);
                for (float t = 0f; t < hold; t += Time.deltaTime)
                {
                    if (frantic) Shake(lower);
                    yield return null;
                }
                yield return Fade(lower, 1f, 0f, 0.6f, frantic);
                yield return new WaitForSeconds(0.25f);
            }
            lower.style.translate = new Translate(0, 0);
            showing = false;
        }

        /// <summary>Intro lines <paramref name="from"/> up to (not including) <paramref name="to"/>: each fades in, holds
        /// (longer for **emphasis**), and fades out.</summary>
        public IEnumerator Intro(int from = 0, int to = int.MaxValue)
        {
            for (int i = Mathf.Max(0, from); i < Mathf.Min(to, config.intro.Length); i++)
            {
                string line = config.intro[i];
                float linger = line.Contains("***") ? 2.2f : line.Contains("**") ? 1.6f : 1f;
                centre.text = NarrationText.Intro(line);
                yield return Fade(centre, 0f, 1f, 0.8f, false, true);
                yield return new WaitForSecondsRealtime(config.lineTime * linger);
                yield return Fade(centre, 1f, 0f, 0.8f, false, true);
                yield return new WaitForSecondsRealtime(0.35f);
            }
        }

        IEnumerator Fade(Label l, float from, float to, float time, bool frantic, bool unscaled = false)
        {
            for (float t = 0f; t < time; t += unscaled ? Time.unscaledDeltaTime : Time.deltaTime)
            {
                l.style.opacity = Mathf.Lerp(from, to, t / time);
                if (frantic) Shake(l);
                yield return null;
            }
            l.style.opacity = to;
        }

        static void Shake(Label l) => l.style.translate = new Translate(Random.Range(-3f, 3f), Random.Range(-2f, 2f));
    }
}
