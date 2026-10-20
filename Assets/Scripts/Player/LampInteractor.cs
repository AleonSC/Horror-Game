using UnityEngine;

/// <summary>
/// Lets the player click on lamps to charge them, batteries to refill the flashlight, shack doors to
/// open them, and torches / lighters to pick them up,
/// and draws a simple HUD: a crosshair, the lamp's charge / energy / cooldown bar, the energy of the
/// lamp you're standing in, and a counter of how many times lamps have been lit (top right).
/// LampSpawner adds this to the player automatically.
/// </summary>
public class LampInteractor : MonoBehaviour
{
    [Tooltip("How far away a lamp or battery can be clicked.")]
    public float reach = 3.5f;
    public bool showHud = true;

    Lamp focused;
    BatteryPickup focusedBattery;
    ShackDoor focusedDoor;
    ItemPickup focusedItem;
    WatchtowerButton focusedButton;
    Flashlight flashlight;
    Hotbar hotbar;
    GUIStyle labelStyle, smallStyle;

    void Update()
    {
        focused = null;
        focusedBattery = null;
        focusedDoor = null;
        focusedItem = null;
        focusedButton = null;
        if (hotbar == null) hotbar = GetComponent<Hotbar>();
        var cam = Camera.main;
        if (cam == null) return;
        if (flashlight == null) flashlight = FindAnyObjectByType<Flashlight>();

        Transform t = cam.transform;
        if (Physics.Raycast(t.position, t.forward, out var hit, reach, ~0, QueryTriggerInteraction.Collide))
        {
            focused = hit.collider.GetComponentInParent<Lamp>();
            focusedBattery = hit.collider.GetComponentInParent<BatteryPickup>();
            focusedDoor = hit.collider.GetComponentInParent<ShackDoor>();
            focusedItem = hit.collider.GetComponentInParent<ItemPickup>();
            focusedButton = hit.collider.GetComponentInParent<WatchtowerButton>();
        }

        if (!HorrorInput.ClickPressed || Cursor.lockState != CursorLockMode.Locked) return;

        if (focusedButton != null)
            focusedButton.Press();
        else if (focusedItem != null)
            focusedItem.PickUp(hotbar);
        else if (focusedDoor != null)
            focusedDoor.Toggle();
        else if (focused != null && !focused.IsLit)
            focused.AddCharge();
        else if (focusedBattery != null)
            focusedBattery.Use(flashlight);
    }

    void OnGUI()
    {
        if (!showHud || GameOverScreen.IsActive) return;

        if (labelStyle == null)
        {
            labelStyle = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontSize = 18 };
            labelStyle.normal.textColor = new Color(0.9f, 0.85f, 0.75f);
            smallStyle = new GUIStyle(labelStyle) { alignment = TextAnchor.MiddleRight, fontStyle = FontStyle.Bold };
        }

        float cx = Screen.width * 0.5f;
        float cy = Screen.height * 0.5f;

        // Crosshair
        bool hasFocus = focused != null || focusedBattery != null || focusedDoor != null || focusedItem != null || focusedButton != null;

        if (focusedButton != null)
        {
            var tower = focusedButton.tower;
            string text = tower == null ? "" :
                tower.LightsOn ? $"The floodlights are on ({Mathf.CeilToInt(tower.TimeLeft)}s)" :
                tower.IsRecharging ? $"The floodlights are recharging ({Mathf.CeilToInt(tower.RechargeLeft)}s)" :
                "Click to turn on the floodlights";
            GUI.Label(new Rect(cx - 200f, cy + 20f, 400f, 30f), text, labelStyle);
        }
        else if (focusedItem != null)
            GUI.Label(new Rect(cx - 200f, cy + 20f, 400f, 30f), $"Click to pick up the {focusedItem.DisplayName}", labelStyle);
        else if (focusedDoor != null)
            GUI.Label(new Rect(cx - 200f, cy + 20f, 400f, 30f), focusedDoor.IsOpen ? "Click to close the door" : "Click to open the door", labelStyle);
        DrawRect(new Rect(cx - 2f, cy - 2f, 4f, 4f), new Color(1f, 1f, 1f, hasFocus ? 0.9f : 0.4f));

        if (focusedBattery != null)
        {
            bool full = flashlight != null && flashlight.IsFull;
            string text = full
                ? "Your flashlight is already full"
                : $"Click to use the battery (+{Mathf.RoundToInt(focusedBattery.charge * 100f)}%)";
            GUI.Label(new Rect(cx - 200f, cy + 20f, 400f, 30f), text, labelStyle);
        }

        if (focused != null)
        {
            string label;
            float fill;
            Color barColor;
            if (focused.IsLit)
            {
                label = "The lamp is lit";
                fill = focused.Energy;
                barColor = EnergyColor(focused);
            }
            else if (focused.IsCoolingDown)
            {
                label = "The lamp is cooling down...";
                fill = focused.CooldownProgress;
                barColor = new Color(0.45f, 0.45f, 0.5f);
            }
            else
            {
                label = "Click repeatedly to light the lamp";
                fill = focused.Charge;
                barColor = new Color(1f, 0.75f, 0.4f);
            }

            GUI.Label(new Rect(cx - 200f, cy + 20f, 400f, 30f), label, labelStyle);
            DrawBar(new Rect(cx - 100f, cy + 52f, 200f, 10f), fill, barColor);
        }

        DrawLampCounter();

        // While standing in a lamp's light, show how much it has left.
        if (Lamp.IsInSafeZone(transform.position, out Lamp safeLamp))
        {
            // Sits above the hotbar.
            float unit = Mathf.Max(1f, Screen.height / 1080f);
            float y = Screen.height - 175f * unit;
            GUI.Label(new Rect(cx - 200f, y, 400f, 30f), "The light keeps them away", labelStyle);
            DrawBar(new Rect(cx - 80f, y + 30f, 160f, 6f), safeLamp.Energy, EnergyColor(safeLamp));
        }
    }

    /// <summary>How many times lamps have been turned on, in the top right: a glowing dot and a big number.</summary>
    void DrawLampCounter()
    {
        float unit = Mathf.Max(1f, Screen.height / 1080f);
        float margin = 24f * unit;
        float dot = 18f * unit;
        float height = 48f * unit;
        var area = new Rect(Screen.width - margin - 200f * unit, margin, 200f * unit, height);

        var dotRect = new Rect(area.xMax - dot, area.y + (height - dot) * 0.5f, dot, dot);
        DrawRect(new Rect(dotRect.x - 3f * unit, dotRect.y - 3f * unit, dot + 6f * unit, dot + 6f * unit), new Color(1f, 0.7f, 0.3f, 0.25f));
        DrawRect(dotRect, new Color(1f, 0.78f, 0.42f, 0.95f));

        smallStyle.fontSize = Mathf.RoundToInt(40f * unit);
        smallStyle.normal.textColor = new Color(0.95f, 0.88f, 0.72f);
        var textRect = new Rect(area.x, area.y, area.width - dot - 12f * unit, height);
        GUI.color = new Color(0f, 0f, 0f, 0.7f);
        GUI.Label(new Rect(textRect.x + 2f, textRect.y + 2f, textRect.width, textRect.height), Lamp.TimesLit.ToString(), smallStyle);
        GUI.color = Color.white;
        GUI.Label(textRect, Lamp.TimesLit.ToString(), smallStyle);
    }

    static Color EnergyColor(Lamp lamp) =>
        lamp.Energy < lamp.flickerBelow ? new Color(0.9f, 0.25f, 0.15f) : new Color(1f, 0.75f, 0.4f);

    static void DrawBar(Rect bar, float fill, Color color)
    {
        DrawRect(bar, new Color(0f, 0f, 0f, 0.6f));
        DrawRect(new Rect(bar.x, bar.y, bar.width * Mathf.Clamp01(fill), bar.height), color);
    }

    static void DrawRect(Rect r, Color c)
    {
        Color old = GUI.color;
        GUI.color = c;
        GUI.DrawTexture(r, Texture2D.whiteTexture);
        GUI.color = old;
    }
}
