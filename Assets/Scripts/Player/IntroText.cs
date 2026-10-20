using UnityEngine;

/// <summary>
/// Opening narration: each line slowly fades in, lingers, and fades out before the next.
/// The game starts on a black screen that fades away during the first lines.
/// The lines are read from Assets/Scripts/Resources/IntroText.txt (one line each; blank lines are skipped),
/// so the intro can be rewritten without touching code. If that file is missing, the lines below are used.
/// DarkForestGenerator adds this automatically.
/// </summary>
public class IntroText : MonoBehaviour
{
    [TextArea]
    public string[] lines =
    {
        "Wake up.",
        "There is no time.",
        "You must move.",
        "Do not stop.",
        "HE has found you.",
        "But there's a chance...",
        "...maybe.",
        "Keep the forest bright.",
        "They don't like the light.",
        "But be careful...",
        "...They like you even less.",
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
    [Tooltip("Text height as a fraction of screen height.")]
    public float textSize = 0.032f;
    [Tooltip("Vertical position as a fraction of screen height (0 = top, 1 = bottom).")]
    [Range(0f, 1f)] public float verticalPosition = 0.62f;

    public bool IsFinished => timer > startDelay + lines.Length * LineDuration;

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
            if (index < lines.Length) DrawLine(lines[index], LineAlpha(t - index * LineDuration));
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

    static float Smooth(float x) => Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(x));

    void DrawLine(string line, float alpha)
    {
        if (alpha <= 0f) return;

        if (style == null)
        {
            style = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontStyle = FontStyle.Italic,
                wordWrap = true,
            };
            if (font != null) style.font = font;
        }
        style.fontSize = Mathf.Max(12, Mathf.RoundToInt(Screen.height * textSize));
        style.normal.textColor = Color.white;

        float width = Screen.width * 0.8f;
        float height = style.fontSize * 3f;
        var rect = new Rect((Screen.width - width) * 0.5f, Screen.height * verticalPosition - height * 0.5f, width, height);

        // Soft shadow so it reads over bright lamps.
        GUI.color = new Color(0f, 0f, 0f, alpha * 0.8f);
        GUI.Label(new Rect(rect.x + 2f, rect.y + 2f, rect.width, rect.height), line, style);

        GUI.color = new Color(textColor.r, textColor.g, textColor.b, alpha * textColor.a);
        GUI.Label(rect, line, style);
    }
}
