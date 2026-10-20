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
    [Tooltip("How far away things can be clicked.")]
    public float reach = 4f;
    [Tooltip("How far off the centre of the screen (in metres, at the object) something can be and still be picked out.")]
    public float aimRadius = 0.7f;
    [Tooltip("Extra leeway per metre of distance, so far things are as easy to pick out as near ones.")]
    public float aimSpreadPerMetre = 0.15f;
    public bool showHud = true;

    Lamp focused;
    BatteryPickup focusedBattery;
    ShackDoor focusedDoor;
    ItemPickup focusedItem;
    WatchtowerButton focusedButton;
    Flashlight flashlight;
    Hotbar hotbar;
    GUIStyle labelStyle, smallStyle;
    static readonly Collider[] nearby = new Collider[128];

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

        Collider target = FindTarget(cam.transform);
        if (target != null)
        {
            focused = target.GetComponentInParent<Lamp>();
            focusedBattery = target.GetComponentInParent<BatteryPickup>();
            focusedDoor = target.GetComponentInParent<ShackDoor>();
            focusedItem = target.GetComponentInParent<ItemPickup>();
            focusedButton = target.GetComponentInParent<WatchtowerButton>();
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

    /// <summary>
    /// Picks the interactable nearest to the line of sight, within reach and aimRadius of it,
    /// that isn't hidden behind something. Much more forgiving than an exact raycast hit.
    /// </summary>
    Collider FindTarget(Transform cam)
    {
        Vector3 eye = cam.position, forward = cam.forward;
        int count = Physics.OverlapSphereNonAlloc(eye, reach, nearby, ~0, QueryTriggerInteraction.Collide);

        Collider best = null;
        float bestScore = float.MaxValue;
        for (int i = 0; i < count; i++)
        {
            Collider col = nearby[i];
            Component owner = Owner(col);
            if (owner == null) continue;

            // The point on the object closest to the line of sight.
            float along = Mathf.Clamp(Vector3.Dot(col.bounds.center - eye, forward), 0f, reach);
            Vector3 closest = col.ClosestPoint(eye + forward * along);
            float ahead = Vector3.Dot(closest - eye, forward);
            if (ahead < 0f || (closest - eye).magnitude > reach) continue;

            float offAxis = Vector3.Distance(closest, eye + forward * ahead);
            float allowed = aimRadius + ahead * aimSpreadPerMetre;
            if (offAxis > allowed) continue;
            if (!Visible(eye, col, closest, owner)) continue;

            float score = offAxis / allowed + ahead * 0.02f; // closest to the crosshair wins, nearer breaks ties
            if (score < bestScore)
            {
                bestScore = score;
                best = col;
            }
        }
        return best;
    }

    /// <summary>The interactable a collider belongs to, if any.</summary>
    static Component Owner(Collider col)
    {
        Component c = col.GetComponentInParent<WatchtowerButton>();
        if (c == null) c = col.GetComponentInParent<ItemPickup>();
        if (c == null) c = col.GetComponentInParent<ShackDoor>();
        if (c == null) c = col.GetComponentInParent<Lamp>();
        if (c == null) c = col.GetComponentInParent<BatteryPickup>();
        return c;
    }

    /// <summary>
    /// True if any of a few points on the target can be seen. Small things lie on the ground or a table,
    /// so a line to one point often just grazes that surface; only call it hidden if every line is blocked.
    /// </summary>
    static bool Visible(Vector3 eye, Collider col, Vector3 closest, Component owner)
    {
        Bounds b = col.bounds;
        return Clear(eye, closest, owner)
            || Clear(eye, b.center, owner)
            || Clear(eye, b.center + Vector3.up * b.extents.y, owner);
    }

    /// <summary>True if nothing solid (other than the target itself) is clearly in front of the point.</summary>
    static bool Clear(Vector3 from, Vector3 to, Component owner)
    {
        Vector3 d = to - from;
        float dist = d.magnitude;
        if (dist < 0.1f) return true;
        // Ignore hits right at the point: that's the surface the thing is resting on.
        if (!Physics.Raycast(from, d / dist, out var hit, dist - 0.35f, ~0, QueryTriggerInteraction.Ignore)) return true;
        return Owner(hit.collider) == owner;
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
