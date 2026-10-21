using UnityEngine;

/// <summary>
/// Narration on screen: each line slowly fades in, lingers, and fades out before the next.
///
/// The opening narration (added by DarkForestGenerator) starts on a black screen that fades away during the
/// first lines; its lines are read from Assets/Scripts/Resources/IntroText.txt (one line each; blank lines
/// skipped), or the lines below if that file is missing.
///
/// Markup: *WORDS* are shown in red; **WORDS** are red and fade out more slowly than the rest of their
/// line, lingering a moment after it; ***WORDS*** are red and linger much longer (longLingerTime), and
/// the next line waits for them to go. Lines are shown in capitals (allCaps).
///
/// Show() puts up more narration mid-game (LampNarration does, as lamps are lit), without the black screen,
/// optionally "frantic": all red, bold, shaking and flickering, and quicker.
/// </summary>
public class IntroText : MonoBehaviour
{
    [TextArea]
    public string[] lines =
    {
        "*WAKE UP.* THERE IS LITTLE TIME.",
        "YOU *MUST* MOVE.",
        "**HE** IS AWARE OF YOUR INTRUSION.",
        "FIND THE *DOOR.*",
        "KEEP THE FOREST *BRIGHT.*",
        "THEY DON'T LIKE BEING *SEEN.*",
        "BUT BE CAREFUL...",
        "...THEY LIKE **YOU** EVEN LESS.",
    };

    [Header("Timing (seconds)")]
    public float startDelay = 3f;
    public float fadeInTime = 2f;
    public float holdTime = 2.5f;
    public float fadeOutTime = 2f;
    public float gapBetweenLines = 0.4f;
    [Tooltip("How long the opening black screen takes to fade away (starts with the first line).")]
    public float blackScreenFadeTime = 6f;
    public bool showBlackScreen = true;
    [Tooltip("How long ***marked*** words take to fade; the next line waits for them.")]
    public float longLingerTime = 7f;

    [Header("Style")]
    public Font font;
    public Color textColor = new Color(0.85f, 0.82f, 0.76f);
    [Tooltip("Colour of *marked* words.")]
    public Color highlightColor = new Color(0.78f, 0.06f, 0.05f);
    [Tooltip("**Marked** words take this many times longer to fade out than the rest of their line.")]
    public float lingerFadeFactor = 1.5f;
    public bool allCaps = true;
    [Tooltip("Text height as a fraction of screen height.")]
    public float textSize = 0.032f;
    [Tooltip("Vertical position as a fraction of screen height (0 = top, 1 = bottom).")]
    [Range(0f, 1f)] public float verticalPosition = 0.62f;
    [Tooltip("All red, bold, shaking and flickering.")]
    public bool frantic;

    /// <summary>True while the opening narration is still showing its lines (ScreamEvent and the music wait for it).</summary>
    public static bool Playing { get; private set; }

    /// <summary>True while any narration is on screen (so mid-game narration waits its turn).</summary>
    public static bool AnyShowing => showing > 0;

    /// <summary>How opaque the opening black screen is right now, 0 to 1 (the player wakes up once it lifts).</summary>
    public static float BlackScreen { get; private set; }
    static int showing;
    /// <summary>Clears this class's static state (StaticReset calls it when Play starts).</summary>
    internal static void ResetStatics() { showing = 0; Playing = false; BlackScreen = 0f; }

    bool isOpening = true;
    float timer;
    float[] starts;   // when each line begins (after startDelay)
    float endTime;
    GUIStyle style;

    public bool IsFinished => starts != null && timer > startDelay + endTime;

    /// <summary>Puts up narration mid-game: no black screen; frantic is all red, shaking and quick.</summary>
    public static IntroText Show(string[] lines, bool frantic)
    {
        var go = new GameObject(frantic ? "Narration (Frantic)" : "Narration");
        go.SetActive(false); // so Awake doesn't load the opening lines over these
        var n = go.AddComponent<IntroText>();
        n.isOpening = false;
        n.lines = lines;
        n.showBlackScreen = false;
        n.startDelay = 0.6f;
        n.frantic = frantic;
        if (frantic)
        {
            n.fadeInTime = 0.25f;
            n.holdTime = 1.7f;
            n.fadeOutTime = 0.5f;
            n.gapBetweenLines = 0.15f;
            n.lingerFadeFactor = 2f;
            n.textSize = 0.038f;
            n.textColor = n.highlightColor;
        }
        else
        {
            n.fadeInTime = 1.4f;
            n.holdTime = 2.2f;
            n.fadeOutTime = 1.6f;
            n.gapBetweenLines = 0.3f;
        }
        go.SetActive(true);
        return n;
    }

    /// <summary>
    /// Turns narration written with quotes into the markup: "quoted words" are red; a quote starting with a
    /// hyphen, "-like this", lingers; inside a quote, -WORD- lingers much longer than everything else.
    /// </summary>
    public static string FromQuotes(string line)
    {
        var sb = new System.Text.StringBuilder();
        int i = 0;
        while (i < line.Length)
        {
            if (line[i] != '"') { sb.Append(line[i]); i++; continue; }
            int close = line.IndexOf('"', i + 1);
            if (close < 0) { sb.Append(line.Substring(i + 1)); break; }
            string q = line.Substring(i + 1, close - i - 1);
            i = close + 1;

            if (q.StartsWith("-") && q.IndexOf('-', 1) < 0)
            {
                sb.Append("**").Append(q.Substring(1)).Append("**"); // the whole quote lingers
                continue;
            }
            // -WORD- inside the quote: that word lingers long; the rest is red.
            int dash = q.IndexOf('-');
            int dashEnd = dash >= 0 ? q.IndexOf('-', dash + 1) : -1;
            if (dash >= 0 && dashEnd > dash)
            {
                string before = q.Substring(0, dash), word = q.Substring(dash + 1, dashEnd - dash - 1), after = q.Substring(dashEnd + 1);
                if (before.Length > 0) sb.Append('*').Append(before).Append('*');
                sb.Append("***").Append(word).Append("***");
                if (after.Length > 0) sb.Append('*').Append(after).Append('*');
            }
            else sb.Append('*').Append(q).Append('*');
        }
        return sb.ToString();
    }

    void Awake()
    {
        if (!isOpening) return;
        Playing = true;
        var file = Resources.Load<TextAsset>("IntroText");
        if (file == null) return;

        var fromFile = new System.Collections.Generic.List<string>();
        foreach (var raw in file.text.Split('\n'))
        {
            string line = raw.Trim();
            if (line.Length > 0) fromFile.Add(line);
        }
        if (fromFile.Count > 0) lines = fromFile.ToArray();
    }

    void OnEnable() => showing++;

    void OnDisable()
    {
        showing = Mathf.Max(0, showing - 1);
        if (isOpening) Playing = false;
        if (showBlackScreen) BlackScreen = 0f;
    }

    void Update()
    {
        if (starts == null) PlanTiming();
        // Mid-game narration steps aside while a boss holds your view: it fades out, waits, and picks up
        // where it left off once the moment is over.
        bool held = !isOpening && FirstPersonController.AttentionHeld;
        hush = Mathf.MoveTowards(hush, held ? 0f : 1f, Time.deltaTime / 0.4f);
        if (!held) timer += Time.deltaTime;
        if (showBlackScreen) BlackScreen = 1f - Mathf.Clamp01((timer - startDelay) / Mathf.Max(blackScreenFadeTime, 0.01f));
        if (IsFinished && isOpening) Playing = false;
        if (IsFinished && (!showBlackScreen || timer > startDelay + blackScreenFadeTime))
        {
            if (isOpening) Destroy(this);
            else Destroy(gameObject);
        }
    }

    float hush = 1f;

    float LineDuration => fadeInTime + holdTime + fadeOutTime + gapBetweenLines;

    /// <summary>When each line starts: one after another, and a line with long-lingering words waits for them.</summary>
    void PlanTiming()
    {
        starts = new float[lines.Length];
        float t = 0f;
        for (int i = 0; i < lines.Length; i++)
        {
            starts[i] = t;
            float slot = LineDuration;
            if (lines[i].Contains("***")) slot = fadeInTime + holdTime + Mathf.Max(fadeOutTime, longLingerTime) + gapBetweenLines;
            t += slot;
        }
        // The last line's lingering words, still fading after its slot.
        endTime = t + Mathf.Max(0f, fadeOutTime * lingerFadeFactor - fadeOutTime - gapBetweenLines);
    }

    void OnGUI()
    {
        if (starts == null) return;
        GUI.depth = -500; // above the HUD, below the game over screen

        // Opening black screen.
        if (showBlackScreen)
        {
            float black = 1f - Mathf.Clamp01((timer - startDelay) / Mathf.Max(blackScreenFadeTime, 0.01f));
            if (black > 0f)
            {
                GUI.color = new Color(0f, 0f, 0f, Smooth(black));
                GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), Texture2D.whiteTexture);
            }
        }

        float t = timer - startDelay;
        if (t >= 0f && lines.Length > 0)
        {
            int index = 0;
            while (index + 1 < starts.Length && t >= starts[index + 1]) index++;
            float local = t - starts[index];
            // The last line's lingering words, still fading as this one begins.
            if (index > 0 && local < fadeOutTime * lingerFadeFactor)
            {
                float since = t - starts[index - 1];
                float linger = LingerAlpha(since);
                if (linger > 0f) DrawLine(lines[index - 1], 0f, linger, 0f, since);
            }
            bool lastDone = index == lines.Length - 1 && local > LineDuration + longLingerTime + 1f;
            if (!lastDone) DrawLine(lines[index], LineAlpha(local), LingerAlpha(local), LongLingerAlpha(local), local);
        }

        GUI.color = Color.white;
    }

    float LineAlpha(float t)
    {
        if (t < fadeInTime) return Smooth(t / fadeInTime);
        t -= fadeInTime;
        if (t < holdTime) return 1f;
        t -= holdTime;
        if (t < fadeOutTime) return Smooth(1f - t / fadeOutTime);
        return 0f;
    }

    /// <summary>Like LineAlpha, but the fade out takes lingerFadeFactor times as long.</summary>
    float LingerAlpha(float t)
    {
        if (t < fadeInTime + holdTime) return LineAlpha(t);
        float fade = fadeOutTime * Mathf.Max(1f, lingerFadeFactor);
        return Smooth(1f - (t - fadeInTime - holdTime) / fade);
    }

    /// <summary>For ***marked*** words: they stay a while after the line goes, then fade slowly.</summary>
    float LongLingerAlpha(float t)
    {
        if (t < fadeInTime + holdTime) return LineAlpha(t);
        float after = t - fadeInTime - holdTime;
        float stay = longLingerTime * 0.45f;
        if (after < stay) return 1f;
        return Smooth(1f - (after - stay) / Mathf.Max(longLingerTime - stay, 0.01f));
    }

    static float Smooth(float x) => Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(x));

    /// <summary>Splits a line into (text, kind) runs: 0 plain, 1 *red*, 2 **red and lingering**, 3 ***red and lingering long***.</summary>
    static System.Collections.Generic.List<(string text, int kind)> Parse(string line)
    {
        var runs = new System.Collections.Generic.List<(string, int)>();
        int i = 0;
        var plain = new System.Text.StringBuilder();
        while (i < line.Length)
        {
            int stars = 0;
            while (stars < 3 && i + stars < line.Length && line[i + stars] == '*') stars++;
            if (stars > 0)
            {
                string marker = new string('*', stars);
                int close = line.IndexOf(marker, i + stars, System.StringComparison.Ordinal);
                if (close > i)
                {
                    if (plain.Length > 0) { runs.Add((plain.ToString(), 0)); plain.Clear(); }
                    runs.Add((line.Substring(i + stars, close - i - stars), stars));
                    i = close + stars;
                    continue;
                }
            }
            plain.Append(line[i]);
            i++;
        }
        if (plain.Length > 0) runs.Add((plain.ToString(), 0));
        return runs;
    }

    static string Hex(Color c, float alpha)
    {
        c.a = Mathf.Clamp01(alpha);
        return ColorUtility.ToHtmlStringRGBA(c);
    }

    void DrawLine(string line, float alpha, float lingerAlpha, float longAlpha, float age)
    {
        alpha *= hush; lingerAlpha *= hush; longAlpha *= hush;
        if (alpha <= 0f && lingerAlpha <= 0f && longAlpha <= 0f) return;

        if (style == null)
        {
            style = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontStyle = frantic ? FontStyle.BoldAndItalic : FontStyle.Italic,
                wordWrap = true,
                richText = true,
            };
            if (font != null) style.font = font;
        }
        style.fontSize = Mathf.Max(12, Mathf.RoundToInt(Screen.height * textSize));
        style.normal.textColor = Color.white;

        float width = Screen.width * 0.8f;
        float height = style.fontSize * 3f;
        var rect = new Rect((Screen.width - width) * 0.5f, Screen.height * verticalPosition - height * 0.5f, width, height);

        // Frantic: it trembles, letter by letter, like it's terrified.
        if (frantic)
        {
            DrawTrembling(line, alpha, lingerAlpha, longAlpha, age, rect);
            return;
        }
        float flicker = 1f;

        // Each run gets its own colour and fade (rich text), so red words and lingering words sit in place.
        var text = new System.Text.StringBuilder();
        var shadow = new System.Text.StringBuilder();
        foreach (var (run, kind) in Parse(allCaps ? line.ToUpperInvariant() : line))
        {
            float a = (kind == 3 ? longAlpha : kind == 2 ? lingerAlpha : alpha) * flicker;
            Color c = kind == 0 ? textColor : highlightColor;
            text.Append("<color=#").Append(Hex(c, a * c.a)).Append('>').Append(run).Append("</color>");
            shadow.Append("<color=#").Append(Hex(Color.black, a * 0.8f)).Append('>').Append(run).Append("</color>");
        }

        // Soft shadow so it reads over bright lamps.
        GUI.color = Color.white;
        GUI.Label(new Rect(rect.x + 2f, rect.y + 2f, rect.width, rect.height), shadow.ToString(), style);
        GUI.Label(rect, text.ToString(), style);
    }

    GUIStyle letterStyle;

    /// <summary>
    /// Frantic lines, drawn a letter at a time, each one trembling on its own: violently as the line comes
    /// in, settling to a constant, terrified shiver, with sudden flinches now and then. A smeared red double
    /// jitters behind it, and the whole thing flickers.
    /// </summary>
    void DrawTrembling(string line, float alpha, float lingerAlpha, float longAlpha, float age, Rect rect)
    {
        if (letterStyle == null)
        {
            letterStyle = new GUIStyle(style) { alignment = TextAnchor.MiddleLeft, wordWrap = false, richText = false };
            letterStyle.normal.textColor = Color.white;
        }
        letterStyle.fontSize = style.fontSize;
        float unit = Screen.height / 1080f;
        float t = Time.time;

        // Lay the letters out along one centred line.
        var letters = new System.Collections.Generic.List<(string ch, Color c, float a, float w)>();
        float total = 0f;
        foreach (var (run, kind) in Parse(allCaps ? line.ToUpperInvariant() : line))
        {
            float a = kind == 3 ? longAlpha : kind == 2 ? lingerAlpha : alpha;
            Color c = kind == 0 ? textColor : highlightColor;
            foreach (char ch in run)
            {
                string s = ch.ToString();
                float w = ch == ' ' ? letterStyle.fontSize * 0.3f : letterStyle.CalcSize(new GUIContent(s)).x * 0.82f;
                letters.Add((s, c, a, w));
                total += w;
            }
        }

        // Shaking hard as it arrives, then a constant shiver; a flinch now and then.
        float arrive = Mathf.Exp(-age * 2.2f);
        float flinch = Mathf.PerlinNoise(t * 1.7f, 3.1f) > 0.72f ? 2.5f : 1f;
        float shake = (1.6f + 10f * arrive) * flinch * unit;
        float flicker = Random.value < 0.07f ? Random.Range(0.35f, 0.8f) : 1f;
        float height = letterStyle.fontSize * 1.6f;
        float x = rect.center.x - total * 0.5f;
        float y = rect.center.y - height * 0.5f;

        Matrix4x4 saved = GUI.matrix;
        for (int i = 0; i < letters.Count; i++)
        {
            var (ch, c, a, w) = letters[i];
            a *= flicker;
            if (a > 0.003f && ch != " ")
            {
                Vector2 off = new Vector2(
                    Mathf.Sin(t * 47f + i * 1.7f) * shake + Random.Range(-0.5f, 0.5f) * shake,
                    Mathf.Sin(t * 39f + i * 2.3f) * shake * 0.8f + Random.Range(-0.5f, 0.5f) * shake);
                var r = new Rect(x + off.x, y + off.y, w * 2f, height);
                float angle = Mathf.Sin(t * 31f + i * 0.9f) * (2f + 9f * arrive) * flinch;
                GUIUtility.RotateAroundPivot(angle, new Vector2(r.x + w * 0.5f, r.center.y));

                GUI.color = new Color(0f, 0f, 0f, a * 0.8f);
                GUI.Label(new Rect(r.x + 2f, r.y + 2f, r.width, r.height), ch, letterStyle);
                GUI.color = new Color(1f, 0.15f, 0.1f, a * 0.3f);
                GUI.Label(new Rect(r.x + Random.Range(-6f, 6f) * unit, r.y + Random.Range(-4f, 4f) * unit, r.width, r.height), ch, letterStyle);
                GUI.color = new Color(c.r, c.g, c.b, a * c.a);
                GUI.Label(r, ch, letterStyle);
                GUI.matrix = saved;
            }
            x += w;
        }
        GUI.color = Color.white;
    }
}

/// <summary>
/// More narration as lamps are lit (2nd, 3rd, 5th and 7th times), from Resources/LampText2, 3, 5 and 7
/// (or the lines below). Written with quotes: "quoted" words are red, "-hyphened" quotes linger,
/// and -WORD- inside a quote lingers much longer. The 5th is frantic. Each plays once a run, after any
/// narration already on screen. DarkForestGenerator adds this.
/// </summary>
public class LampNarration : MonoBehaviour
{
    static readonly (int lamps, string resource, bool frantic, string[] fallback)[] Beats =
    {
        (2, "LampText2", false, new[]
        {
            "Have you seen the \"-Scours?\"",
            "They \"Really\" don't like light.",
            "\"Use it.\"",
        }),
        (3, "LampText3", false, new[]
        {
            "This is \"-The Auscult.\"",
            "\"This\" one... is \"-Blind.\"",
            "A rare Scour \"-mutation.\"",
            "Don't let him \"-hear you.\"",
        }),
        (5, "LampText5", true, new[]
        {
            "\"NO IT IS TOO LATE!!!\"",
            "\"YOU MUST HURRY!!!\"",
            "\"FOR -HE- HAS SEEN YOU!!!\"",
            "\"LIGHT THE REMAINING!!!!\"",
            "\"FIND THE DOOR!!!\"",
        }),
        (7, "LampText7", true, new[]
        {
            "\"...\"",
            "\"I do not know of this entity.\"",
            "\"I have never witnessed it.\"",
            "\"I must seek safety, please understand.\"",
            "\"Good luck.\"",
        }),
    };

    readonly bool[] played = new bool[Beats.Length];
    readonly bool[] pending = new bool[Beats.Length];
    int lastCount;

    void Update()
    {
        int count = Lamp.TimesLit;
        if (count < lastCount) // the run was reset
            for (int i = 0; i < played.Length; i++) played[i] = pending[i] = false;
        lastCount = count;

        for (int i = 0; i < Beats.Length; i++)
            if (!played[i] && count >= Beats[i].lamps) { played[i] = true; pending[i] = true; }

        // One at a time, after anything already on screen (the opening, or the last lamp's lines).
        // ...and never over a boss's arrival while it holds your view.
        if (IntroText.AnyShowing || GameOverScreen.IsActive || FirstPersonController.AttentionHeld) return;
        for (int i = 0; i < Beats.Length; i++)
        {
            if (!pending[i]) continue;
            pending[i] = false;
            IntroText.Show(Load(Beats[i].resource, Beats[i].fallback), Beats[i].frantic);
            return;
        }
    }

    static string[] Load(string resource, string[] fallback)
    {
        var raw = new System.Collections.Generic.List<string>();
        var file = Resources.Load<TextAsset>(resource);
        if (file != null)
            foreach (var l in file.text.Split('\n'))
                if (l.Trim().Length > 0) raw.Add(l.Trim());
        if (raw.Count == 0) raw.AddRange(fallback);
        var lines = new string[raw.Count];
        for (int i = 0; i < raw.Count; i++) lines[i] = IntroText.FromQuotes(raw[i]);
        return lines;
    }
}
