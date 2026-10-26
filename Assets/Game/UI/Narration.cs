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
        Label centre, centre2, lower;
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
            // (the second of a pair, under the first, in the glitching intro)
            centre2 = UiStyle.Text(ui.Top, "", 30, UiStyle.Ink, TextAnchor.MiddleCenter, 4f);
            centre2.style.position = Position.Absolute;
            centre2.style.left = 0;
            centre2.style.right = 0;
            centre2.style.top = UiRoot.Height * 0.5f + 16f;
            centre2.style.opacity = 0f;
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

        Coroutine introRoutine;
        static bool told;

        /// <summary>The intro's lines are still being said (along the bottom, after you wake).</summary>
        public bool IntroPlaying { get; private set; }

        /// <summary>Say the intro's lines from this one on (after you wake), then tell everyone it's over.</summary>
        public void PlayIntroFrom(int from)
        {
            IntroPlaying = true;
            introRoutine = StartCoroutine(IntroThenFinish(from));
        }

        /// <summary>The intro's lines from this one on, then tell everyone it's over (the giant answers).</summary>
        public IEnumerator IntroThenFinish(int from)
        {
            yield return Intro(from);
            IntroPlaying = false;
            Finished();
        }

        /// <summary>Skipped: no more intro lines, whatever was showing gone; the giant still answers.</summary>
        public void SkipIntro()
        {
            if (introRoutine != null) StopCoroutine(introRoutine);
            introRoutine = null;
            IntroPlaying = false;
            foreach (var l in new[] { centre, centre2 })
            {
                if (l == null) continue;
                l.style.opacity = 0f;
            }
            if (centre != null)
            {
                centre.style.top = StyleKeyword.Null;
                centre.style.bottom = Bottom;
            }
            Finished();
        }

        /// <summary>Tell everyone the intro's over (once).</summary>
        public static void Finished()
        {
            if (told) return;
            told = true;
            if (Services.TryGet<EventBus>(out var events)) events.Publish(new IntroFinished());
        }

        public int IntroLines => config.intro != null ? config.intro.Length : 0;

        /// <summary>One intro line in the middle of the screen (the glitching intro): fades in, holds, fades out.</summary>
        public IEnumerator MiddleLine(int i)
        {
            if (i < 0 || i >= IntroLines) yield break;
            string line = config.intro[i];
            float linger = line.Contains("***") ? 2.2f : line.Contains("**") ? 1.6f : 1f;
            centre.text = NarrationText.Intro(line);
            centre.style.bottom = StyleKeyword.Null;
            centre.style.top = UiRoot.Height * 0.5f - 24f;
            yield return Fade(centre, 0f, 1f, 0.5f, false, true);
            yield return new WaitForSecondsRealtime(config.lineTime * linger);
            yield return Fade(centre, 1f, 0f, 0.4f, false, true);
            centre.style.top = StyleKeyword.Null;
            centre.style.bottom = Bottom;
        }

        /// <summary>
        /// Two intro lines in the middle of the screen (the glitching intro): the first on the top row, the second
        /// fading in under it shortly after; they hold together, then both fade.
        /// </summary>
        public IEnumerator MiddlePair(int i)
        {
            if (i < 0 || i >= IntroLines) yield break;
            bool two = i + 1 < IntroLines;
            string a = config.intro[i], b = two ? config.intro[i + 1] : "";
            float linger = Mathf.Max(Linger(a), two ? Linger(b) : 0f);
            centre.text = NarrationText.Intro(a);
            centre.style.bottom = StyleKeyword.Null;
            centre.style.top = UiRoot.Height * 0.5f - (two ? 52f : 24f);
            centre2.text = two ? NarrationText.Intro(b) : "";
            centre2.style.opacity = 0f;
            yield return Fade(centre, 0f, 1f, 0.5f, false, true);
            if (two)
            {
                yield return new WaitForSecondsRealtime(0.7f);
                yield return Fade(centre2, 0f, 1f, 0.5f, false, true);
            }
            yield return new WaitForSecondsRealtime(config.lineTime * linger);
            for (float t = 0f; t < 0.4f; t += Time.unscaledDeltaTime)
            {
                centre.style.opacity = 1f - t / 0.4f;
                centre2.style.opacity = two ? 1f - t / 0.4f : 0f;
                yield return null;
            }
            centre.style.opacity = 0f;
            centre2.style.opacity = 0f;
            centre.style.top = StyleKeyword.Null;
            centre.style.bottom = Bottom;
        }

        static float Linger(string line) => line.Contains("***") ? 2.2f : line.Contains("**") ? 1.6f : 1f;

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
