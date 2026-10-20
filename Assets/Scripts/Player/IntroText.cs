using UnityEngine;

/// <summary>
/// Opening narration: each line slowly fades in, lingers, and fades out before the next.
/// The game starts on a black screen that fades away during the first lines.
/// The lines are read from Assets/Scripts/Resources/IntroText.txt (one line each; blank lines are skipped),
/// so the intro can be rewritten without touching code. If that file is missing, the lines below are used.
/// Markup: *WORDS* are shown in red; **WORDS** are red and fade out more slowly than the rest of their
/// line, lingering a moment after it. Lines are shown in capitals (allCaps).
/// DarkForestGenerator adds this automatically.
/// </summary>
public class IntroText : MonoBehaviour
{
    [TextArea]
    public string[] lines =
    {
        "*WAKE UP.* THERE IS LITTLE TIME.",
        "YOU *MUST* MOVE.",
        "**HE** IS AWARE OF YOUR INTRUSION.",
        "THERE IS A CHANCE...",
        "*...MAYBE.*",
        "KEEP THE FOREST *BRIGHT.*",
        "THEY DON'T LIKE THE *LIGHT.*",
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

    public bool IsFinished => timer > startDelay + lines.Length * LineDuration + LingerOverrun;

    /// <summary>How far the lingering words outlast their line's slot.</summary>
    float LingerOverrun => Mathf.Max(0f, fadeOutTime * lingerFadeFactor - fadeOutTime - gapBetweenLines);

    /// <summary>True while an intro is still showing its lines (ScreamEvent waits for it to end).</summary>
    public static bool Playing { get; private set; }

    float LineDuration => fadeInTime + holdTime + fadeOutTime + gapBetweenLines;

    float timer;
    GUIStyle style;

    void Awake()
    {
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

    void OnDestroy() => Playing = false;

    void Update()
    {
        timer += Time.deltaTime;
        if (IsFinished) Playing = false;
        if (IsFinished && timer > startDelay + blackScreenFadeTime) Destroy(this);
    }

    void OnGUI()
    {
        GUI.depth = -500; // above the HUD, below the game over screen

        // Opening black screen.
        float black = 1f - Mathf.Clamp01((timer - startDelay) / Mathf.Max(blackScreenFadeTime, 0.01f));
        if (black > 0f)
        {
            GUI.color = new Color(0f, 0f, 0f, Smooth(black));
            GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), Texture2D.whiteTexture);
        }

        float t = timer - startDelay;
        if (t >= 0f && lines.Length > 0)
        {
            int index = Mathf.FloorToInt(t / LineDuration);
            float local = t - index * LineDuration;
            // The last line's lingering words, still fading as this one begins.
            if (index > 0 && index - 1 < lines.Length)
            {
                float linger = LingerAlpha(local + LineDuration);
                if (linger > 0f) DrawLine(lines[index - 1], 0f, linger);
            }
            if (index < lines.Length) DrawLine(lines[index], LineAlpha(local), LingerAlpha(local));
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

    static float Smooth(float x) => Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(x));

    /// <summary>Splits a line into (text, kind) runs: 0 plain, 1 *red*, 2 **red and lingering**.</summary>
    static System.Collections.Generic.List<(string text, int kind)> Parse(string line)
    {
        var runs = new System.Collections.Generic.List<(string, int)>();
        int i = 0;
        var plain = new System.Text.StringBuilder();
        while (i < line.Length)
        {
            int stars = line[i] == '*' ? (i + 1 < line.Length && line[i + 1] == '*' ? 2 : 1) : 0;
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

    void DrawLine(string line, float alpha, float lingerAlpha)
    {
        if (alpha <= 0f && lingerAlpha <= 0f) return;

        if (style == null)
        {
            style = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontStyle = FontStyle.Italic,
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

        // Each run gets its own colour and fade (rich text), so red words and lingering words sit in place.
        var text = new System.Text.StringBuilder();
        var shadow = new System.Text.StringBuilder();
        foreach (var (run, kind) in Parse(allCaps ? line.ToUpperInvariant() : line))
        {
            float a = kind == 2 ? lingerAlpha : alpha;
            Color c = kind == 0 ? textColor : highlightColor;
            text.Append("<color=#").Append(Hex(c, a * c.a)).Append('>').Append(run).Append("</color>");
            shadow.Append("<color=#").Append(Hex(Color.black, a * 0.8f)).Append('>').Append(run).Append("</color>");
        }

        // Soft shadow so it reads over bright lamps.
        GUI.color = Color.white;
        GUI.Label(new Rect(rect.x + 2f, rect.y + 2f, rect.width, rect.height), shadow.ToString(), style);
        GUI.Label(rect, text.ToString(), style);
    }
}
