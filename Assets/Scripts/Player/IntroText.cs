using UnityEngine;

/// <summary>
/// Opening narration: each line slowly fades in, lingers, and fades out before the next.
/// The game starts on a black screen that fades away during the first lines.
/// DarkForestGenerator adds this automatically; edit the lines in the inspector.
/// </summary>
public class IntroText : MonoBehaviour
{
    [TextArea]
    public string[] lines =
    {
        "You wake up with only a flashlight.",
        "Your head is pounding, mouth parched. But you must move on.",
        "Do not stop.",
        "They will find you.",
        "But there's still a chance.",
        "To survive...maybe.",
        "They don't like the light.",
        "It keeps you safe.",
        "But remember...",
        "Moths flock to the light.",
    };

    [Header("Timing (seconds)")]
    public float startDelay = 1.5f;
    public float fadeInTime = 2.5f;
    public float holdTime = 2.5f;
    public float fadeOutTime = 2.5f;
    public float gapBetweenLines = 1f;
    [Tooltip("How long the opening black screen takes to fade away (starts with the first line).")]
    public float blackScreenFadeTime = 8f;

    [Header("Style")]
    public Font font;
    public Color textColor = new Color(0.85f, 0.82f, 0.76f);
    [Tooltip("Text height as a fraction of screen height.")]
    public float textSize = 0.032f;
    [Tooltip("Vertical position as a fraction of screen height (0 = top, 1 = bottom).")]
    [Range(0f, 1f)] public float verticalPosition = 0.62f;

    public bool IsFinished => timer > startDelay + lines.Length * LineDuration;

    float LineDuration => fadeInTime + holdTime + fadeOutTime + gapBetweenLines;

    float timer;
    GUIStyle style;

    void Update()
    {
        timer += Time.deltaTime;
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

        // Soft shadow so it reads over bright lamps and rain.
        GUI.color = new Color(0f, 0f, 0f, alpha * 0.8f);
        GUI.Label(new Rect(rect.x + 2f, rect.y + 2f, rect.width, rect.height), line, style);

        GUI.color = new Color(textColor.r, textColor.g, textColor.b, alpha * textColor.a);
        GUI.Label(rect, line, style);
    }
}
