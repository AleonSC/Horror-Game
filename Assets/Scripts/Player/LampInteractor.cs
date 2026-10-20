using UnityEngine;

/// <summary>
/// Lets the player click on lamps to charge them, and draws a simple HUD:
/// a crosshair, the lamp meter, and a tiny counter of lamps lit (top right).
/// LampSpawner adds this to the player automatically.
/// </summary>
public class LampInteractor : MonoBehaviour
{
    [Tooltip("How far away a lamp can be clicked.")]
    public float reach = 3.5f;
    public bool showHud = true;

    Lamp focused;
    GUIStyle labelStyle, smallStyle;

    void Update()
    {
        focused = null;
        var cam = Camera.main;
        if (cam == null) return;

        Transform t = cam.transform;
        if (Physics.Raycast(t.position, t.forward, out var hit, reach, ~0, QueryTriggerInteraction.Collide))
            focused = hit.collider.GetComponentInParent<Lamp>();

        if (focused != null && !focused.IsLit && HorrorInput.ClickPressed && Cursor.lockState == CursorLockMode.Locked)
            focused.AddCharge();
    }

    void OnGUI()
    {
        if (!showHud || GameOverScreen.IsActive) return;

        if (labelStyle == null)
        {
            labelStyle = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontSize = 18 };
            labelStyle.normal.textColor = new Color(0.9f, 0.85f, 0.75f);
            smallStyle = new GUIStyle(labelStyle) { fontSize = 13, alignment = TextAnchor.MiddleRight };
        }

        float cx = Screen.width * 0.5f;
        float cy = Screen.height * 0.5f;

        // Crosshair
        DrawRect(new Rect(cx - 2f, cy - 2f, 4f, 4f), new Color(1f, 1f, 1f, focused != null ? 0.9f : 0.4f));

        if (focused != null)
        {
            if (focused.IsLit)
            {
                GUI.Label(new Rect(cx - 200f, cy + 20f, 400f, 30f), "The lamp is lit", labelStyle);
            }
            else
            {
                GUI.Label(new Rect(cx - 200f, cy + 20f, 400f, 30f), "Click repeatedly to light the lamp", labelStyle);
                var bar = new Rect(cx - 100f, cy + 52f, 200f, 10f);
                DrawRect(bar, new Color(0f, 0f, 0f, 0.6f));
                DrawRect(new Rect(bar.x, bar.y, bar.width * focused.Charge, bar.height), new Color(1f, 0.75f, 0.4f));
            }
        }

        // Tiny lit-lamp counter in the top right: a warm dot and a number.
        var counter = new Rect(Screen.width - 66f, 12f, 50f, 16f);
        DrawRect(new Rect(counter.xMax - 6f, counter.y + 5f, 6f, 6f), new Color(1f, 0.75f, 0.4f, 0.9f));
        GUI.Label(new Rect(counter.x, counter.y, counter.width - 12f, counter.height), Lamp.LitCount.ToString(), smallStyle);

        if (Lamp.IsInSafeZone(transform.position))
            GUI.Label(new Rect(cx - 200f, Screen.height - 60f, 400f, 30f), "The light keeps them away", labelStyle);
    }

    static void DrawRect(Rect r, Color c)
    {
        Color old = GUI.color;
        GUI.color = c;
        GUI.DrawTexture(r, Texture2D.whiteTexture);
        GUI.color = old;
    }
}
