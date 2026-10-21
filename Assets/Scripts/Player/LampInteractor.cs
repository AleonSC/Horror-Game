using UnityEngine;

/// <summary>
/// Lets the player click on lamps to charge them, batteries to refill the flashlight, shack doors to
/// open them (and the car's door), torches / lighters to pick them up, the car's headlight switch,
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
    CarHeadlightSwitch focusedSwitch;
    CarRadio focusedRadio;
    LightableTorch focusedTorch;
    Flashlight flashlight;
    Hotbar hotbar;
    GUIStyle labelStyle, smallStyle;
    [Tooltip("Small things you pick up (items, batteries) get this much more aim leeway than lamps and doors.")]
    public float smallItemLeeway = 1.3f;
    // Big enough for a shack's worth of planks plus everything near it; when it was smaller, items inside
    // busy places could be left out of the search entirely.
    static readonly Collider[] nearby = new Collider[1024];

    void Update()
    {
        focused = null;
        focusedBattery = null;
        focusedDoor = null;
        focusedItem = null;
        focusedButton = null;
        focusedSwitch = null;
        focusedRadio = null;
        focusedTorch = null;
        if (hotbar == null) hotbar = GetComponent<Hotbar>();
        var cam = Camera.main;
        if (cam == null) return;
        if (flashlight == null) flashlight = FindAnyObjectByType<Flashlight>();

        Collider target = DirectTarget(cam.transform);
        if (target == null) target = FindTarget(cam.transform);
        if (target != null)
        {
            focused = target.GetComponentInParent<Lamp>();
            focusedBattery = target.GetComponentInParent<BatteryPickup>();
            focusedDoor = target.GetComponentInParent<ShackDoor>();
            focusedItem = target.GetComponentInParent<ItemPickup>();
            focusedButton = target.GetComponentInParent<WatchtowerButton>();
            focusedSwitch = target.GetComponentInParent<CarHeadlightSwitch>();
            focusedRadio = target.GetComponentInParent<CarRadio>();
            focusedTorch = target.GetComponentInParent<LightableTorch>();
        }

        if (!HorrorInput.ClickPressed || Cursor.lockState != CursorLockMode.Locked) return;

        if (focusedTorch != null)
            focusedTorch.TryLight(hotbar);
        else if (focusedButton != null)
            focusedButton.Press();
        else if (focusedRadio != null)
            focusedRadio.Toggle();
        else if (focusedSwitch != null && focusedSwitch.car != null)
            focusedSwitch.car.ToggleLights();
        else if (focusedItem != null)
            focusedItem.PickUp(hotbar);
        else if (focusedDoor != null)
            focusedDoor.Toggle();
        else if (focused != null && !focused.IsLit)
            focused.AddCharge();
        else if (focusedBattery != null)
            focusedBattery.PickUp(hotbar);
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
            // (The radio's a good size: it gets no extra leeway, or it's picked from metres off.)
            bool small = owner is ItemPickup || owner is BatteryPickup;
            float allowed = (aimRadius + ahead * aimSpreadPerMetre) * (small ? smallItemLeeway : 1f);
            if (offAxis > allowed) continue;
            if (!Visible(eye, col, closest, owner)) continue;

            float score = offAxis / allowed + ahead * 0.02f; // closest to the crosshair wins, nearer breaks ties
            if (small) score -= 0.15f; // a small item right by a door or lamp is usually what you're after
            if (score < bestScore)
            {
                bestScore = score;
                best = col;
            }
        }
        return best;
    }

    static readonly RaycastHit[] directHits = new RaycastHit[32];

    /// <summary>
    /// Something small you're looking straight at, close by (an item, a battery, a radio): a fat ray along the
    /// crosshair. Taken over the general search, which can miss things lying tucked against others (a
    /// bundle of glowsticks by a backpack) when its line-of-sight checks graze what's beside them.
    /// </summary>
    Collider DirectTarget(Transform cam)
    {
        int count = Physics.SphereCastNonAlloc(cam.position, 0.18f, cam.forward, directHits, 2.6f, ~0, QueryTriggerInteraction.Collide);
        Collider best = null;
        float bestDist = float.MaxValue;
        for (int i = 0; i < count; i++)
        {
            var col = directHits[i].collider;
            if (col == null) continue;
            Component owner = Owner(col);
            if (!(owner is ItemPickup || owner is BatteryPickup || owner is CarRadio)) continue;
            var shack = owner.GetComponentInParent<Shack>();
            if (shack != null && !shack.Contains(cam.position)) continue; // not through a shack's walls
            float d = directHits[i].distance;
            if (d < bestDist) { bestDist = d; best = col; }
        }
        return best;
    }

    /// <summary>The interactable a collider belongs to, if any.</summary>
    static Component Owner(Collider col)
    {
        Component c = col.GetComponentInParent<LightableTorch>();
        if (c == null) c = col.GetComponentInParent<WatchtowerButton>();
        if (c == null) c = col.GetComponentInParent<CarRadio>();
        if (c == null) c = col.GetComponentInParent<CarHeadlightSwitch>();
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
        // Things inside a shack can only be taken from inside it (or its doorway), not through the walls.
        if (owner is ItemPickup || owner is BatteryPickup || owner is CarRadio)
        {
            var shack = owner.GetComponentInParent<Shack>();
            if (shack != null && !shack.Contains(eye)) return false;
        }
        // Within arm's reach, anything you're aiming at counts: things lie on tables, against backpacks...
        if ((closest - eye).magnitude < 1.6f) return true;
        return Clear(eye, closest, owner)
            || Clear(eye, b.center, owner)
            || Clear(eye, b.center + Vector3.up * b.extents.y, owner)
            || Clear(eye, b.center + Vector3.up * (b.extents.y + 0.15f), owner);
    }

    /// <summary>True if nothing solid (other than the target itself) is clearly in front of the point.</summary>
    static bool Clear(Vector3 from, Vector3 to, Component owner)
    {
        Vector3 d = to - from;
        float dist = d.magnitude;
        if (dist < 0.1f) return true;
        // Ignore hits right at the point: that's the surface the thing is resting on.
        if (!Physics.Raycast(from, d / dist, out var hit, dist - 0.35f, ~0, QueryTriggerInteraction.Ignore)) return true;
        if (Owner(hit.collider) == owner) return true;
        // Something it's lying against (a backpack, the table edge, a log) isn't really in the way.
        return (hit.point - to).magnitude < 0.6f;
    }

    // ---------------------------------------------------------------- HUD

    string promptText;
    float promptAge, lastCharge, clickPulse;
    Lamp lastLamp;
    GUIStyle promptStyle, captionStyle;

    static readonly Color Cream = new Color(0.93f, 0.87f, 0.74f);
    static readonly Color Ember = new Color(1f, 0.72f, 0.36f);

    void OnGUI()
    {
        if (!showHud || GameOverScreen.IsActive || InventoryScreen.IsOpen) return;

        if (labelStyle == null)
        {
            labelStyle = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontSize = 18 };
            labelStyle.normal.textColor = new Color(0.9f, 0.85f, 0.75f);
            smallStyle = new GUIStyle(labelStyle) { alignment = TextAnchor.MiddleRight, fontStyle = FontStyle.Bold };
            promptStyle = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold, richText = true };
            captionStyle = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Italic };
        }

        float unit = Mathf.Max(1f, Screen.height / 1080f);
        float cx = Screen.width * 0.5f;
        float cy = Screen.height * 0.5f;

        // What the crosshair's on, and what clicking will do.
        string prompt = null;
        if (focusedTorch != null) prompt = focusedTorch.Prompt;
        else if (focusedRadio != null) prompt = focusedRadio.Prompt;
        else if (focusedButton != null)
        {
            var tower = focusedButton.tower;
            prompt = tower == null ? null :
                tower.LightsOn ? $"The floodlights are on ({Mathf.CeilToInt(tower.TimeLeft)}s)" :
                tower.IsRecharging ? $"The floodlights are recharging ({Mathf.CeilToInt(tower.RechargeLeft)}s)" :
                "Turn on the floodlights";
        }
        else if (focusedSwitch != null && focusedSwitch.car != null)
        {
            var car = focusedSwitch.car;
            prompt = car.IsDead ? "The battery's dead" :
                car.LightsOn ? $"Turn off the headlights · battery {Mathf.CeilToInt(car.Battery * 100f)}%" :
                $"Turn on the headlights · battery {Mathf.CeilToInt(car.Battery * 100f)}%";
        }
        else if (focusedItem != null) prompt = $"Take the {focusedItem.DisplayName}";
        else if (focusedDoor != null)
            prompt = focusedDoor.locked && !focusedDoor.IsOpen ? focusedDoor.lockedMessage
                : focusedDoor.IsOpen ? $"Close the {focusedDoor.noun}" : $"Open the {focusedDoor.noun}";
        else if (focusedBattery != null) prompt = "Take the battery";
        else if (focused != null)
            prompt = focused.IsLit ? "The lamp is lit" : focused.IsCoolingDown ? "The lamp is cooling down" : "Light the lamp";

        bool hasFocus = prompt != null;
        if (prompt != promptText) { promptText = prompt; promptAge = 0f; }
        promptAge += Time.unscaledDeltaTime;

        // The crosshair: a small dot, ringed when it's on something.
        DrawRect(new Rect(cx - 2f * unit, cy - 2f * unit, 4f * unit, 4f * unit), new Color(1f, 1f, 1f, hasFocus ? 0.95f : 0.4f));
        if (hasFocus && focused == null) DrawRing(cx, cy, 9f * unit, 16, 1f, new Color(Cream.r, Cream.g, Cream.b, 0.5f * Ease(promptAge / 0.2f)), 2f * unit, 4f * unit);

        // The lamp's meter: a ring of segments round the crosshair, filling as you crank it.
        if (focused != null) DrawLampRing(focused, cx, cy, unit);
        else lastLamp = null;

        if (prompt != null)
        {
            bool clickable = focused == null ? !(focusedRadio != null && (focusedRadio.IsBroken || focusedRadio.Charge <= 0f)) && !(focusedTorch != null && !focusedTorch.CanLight)
                : !focused.IsLit && !focused.IsCoolingDown;
            DrawPrompt(prompt, cx, cy + (focused != null ? 62f : 34f) * unit, unit, Ease(promptAge / 0.25f), clickable);
        }

        DrawLampCounter();

        // While standing in a lamp's light, how much it has left: a thin gilt bar with a caption above the hotbar.
        if (Lamp.IsInSafeZone(transform.position, out Lamp safeLamp))
        {
            float y = Screen.height - 178f * unit;
            Caption("the light keeps them away", cx, y, unit, 0.85f);
            DrawGiltBar(new Rect(cx - 90f * unit, y + 20f * unit, 180f * unit, 4f * unit), safeLamp.Energy, EnergyColor(safeLamp), unit);
        }
        // Or in a burning torch's circle (at a camp or by a shack): how long it has left.
        else if (SafeZones.Contains(transform.position, out SafeZones.ISource safeSource) && safeSource is LightableTorch torch)
        {
            float y = Screen.height - 178f * unit;
            Caption("the fire keeps them away", cx, y, unit, 0.85f);
            DrawGiltBar(new Rect(cx - 90f * unit, y + 20f * unit, 180f * unit, 4f * unit), torch.Fuel, new Color(1f, 0.6f, 0.25f), unit);
        }
    }

    static float Ease(float x) => Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(x));

    /// <summary>Spaced-out capitals: "OPEN  THE  DOOR".</summary>
    static string Spaced(string text)
    {
        var sb = new System.Text.StringBuilder();
        foreach (char ch in text.ToUpperInvariant())
        {
            if (sb.Length > 0) sb.Append(ch == ' ' ? "  " : " ");
            if (ch != ' ') sb.Append(ch);
        }
        return sb.ToString();
    }

    /// <summary>
    /// A prompt: spaced small capitals between little diamonds, on a soft dark band that fades out at the
    /// ends, a fine rule underneath drawing out from the middle as it appears, and a mouse-button mark when
    /// clicking will do something.
    /// </summary>
    void DrawPrompt(string text, float cx, float y, float unit, float a, bool clickable)
    {
        promptStyle.fontSize = Mathf.RoundToInt(15f * unit);
        string spaced = Spaced(text);
        float w = promptStyle.CalcSize(new GUIContent(spaced)).x;
        float h = 30f * unit;

        // The band.
        float bandW = w + 160f * unit;
        for (int i = 0; i < 12; i++)
        {
            float f = i / 12f;
            float edge = Mathf.SmoothStep(0f, 1f, Mathf.Min(f, 1f - f) * 4f);
            DrawRect(new Rect(cx - bandW * 0.5f + bandW * f, y - h * 0.5f, bandW / 12f + 1f, h), new Color(0f, 0f, 0f, 0.45f * edge * a));
        }

        // Diamonds either side, the click mark on the left.
        float side = w * 0.5f + 18f * unit;
        Diamond(cx - side, y, 3.5f * unit, new Color(Ember.r, Ember.g, Ember.b, a));
        Diamond(cx + side, y, 3.5f * unit, new Color(Ember.r, Ember.g, Ember.b, a));
        if (clickable) MouseMark(cx - side - 22f * unit, y, unit, a);

        // The text, with a shadow.
        var r = new Rect(cx - w * 0.5f - 10f, y - h * 0.5f, w + 20f, h);
        promptStyle.normal.textColor = new Color(0f, 0f, 0f, 0.8f * a);
        GUI.Label(new Rect(r.x + 1.5f, r.y + 1.5f, r.width, r.height), spaced, promptStyle);
        promptStyle.normal.textColor = new Color(Cream.r, Cream.g, Cream.b, a);
        GUI.Label(r, spaced, promptStyle);

        // The rule underneath, drawing out from the middle, fading at its ends.
        float ruleW = (w + 40f * unit) * Ease(promptAge / 0.4f);
        for (int i = 0; i < 16; i++)
        {
            float f = i / 16f;
            float edge = 1f - Mathf.Abs(f - 0.5f) * 2f;
            DrawRect(new Rect(cx - ruleW * 0.5f + ruleW * f, y + h * 0.42f, ruleW / 16f + 0.5f, Mathf.Max(1f, unit)), new Color(Ember.r, Ember.g, Ember.b, 0.7f * edge * a));
        }
    }

    /// <summary>A little mouse with its left button lit: "click".</summary>
    static void MouseMark(float x, float y, float unit, float a)
    {
        float w = 10f * unit, h = 15f * unit, t = Mathf.Max(1f, unit);
        var body = new Rect(x - w * 0.5f, y - h * 0.5f, w, h);
        Color line = new Color(Cream.r, Cream.g, Cream.b, 0.8f * a);
        DrawRect(new Rect(body.x, body.y, body.width, t), line);
        DrawRect(new Rect(body.x, body.yMax - t, body.width, t), line);
        DrawRect(new Rect(body.x, body.y, t, body.height), line);
        DrawRect(new Rect(body.xMax - t, body.y, t, body.height), line);
        DrawRect(new Rect(body.center.x - t * 0.5f, body.y, t, h * 0.4f), line);
        DrawRect(new Rect(body.x + t, body.y + t, w * 0.5f - t * 1.5f, h * 0.4f - t), new Color(Ember.r, Ember.g, Ember.b, a));
    }

    /// <summary>
    /// The lamp's meter round the crosshair: a ring of segments that fill clockwise with warm light as you
    /// crank it, flaring with each click and dimming as it drains; full and slowly pulsing once it's lit,
    /// with its remaining energy; cold blue-grey, refilling, while it cools down.
    /// </summary>
    void DrawLampRing(Lamp lamp, float cx, float cy, float unit)
    {
        if (lamp != lastLamp) { lastLamp = lamp; lastCharge = lamp.Charge; clickPulse = 0f; }
        if (lamp.Charge > lastCharge + 0.001f) clickPulse = 1f; // a click
        lastCharge = lamp.Charge;
        clickPulse = Mathf.MoveTowards(clickPulse, 0f, Time.unscaledDeltaTime * 4f);

        float fill;
        Color color;
        float pulse = 0f;
        if (lamp.IsLit)
        {
            fill = lamp.Energy;
            color = EnergyColor(lamp);
            pulse = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 2.2f);
        }
        else if (lamp.IsCoolingDown)
        {
            fill = lamp.CooldownProgress;
            color = new Color(0.5f, 0.6f, 0.75f);
        }
        else
        {
            fill = lamp.Charge;
            color = Ember;
        }

        float a = Ease(promptAge / 0.25f);
        float radius = (30f + 3f * clickPulse) * unit;
        // The unlit track, then the filled part, with a glow behind it.
        DrawRing(cx, cy, radius, 36, 1f, new Color(0f, 0f, 0f, 0.55f * a), 3.5f * unit, 9f * unit);
        DrawRing(cx, cy, radius, 36, 1f, new Color(1f, 1f, 1f, 0.08f * a), 2f * unit, 7f * unit);
        Color glow = new Color(color.r, color.g, color.b, (0.18f + 0.25f * clickPulse + 0.12f * pulse) * a);
        DrawRing(cx, cy, radius, 36, fill, glow, 6f * unit, 14f * unit);
        DrawRing(cx, cy, radius, 36, fill, new Color(Mathf.Lerp(color.r, 1f, clickPulse * 0.5f), Mathf.Lerp(color.g, 1f, clickPulse * 0.5f), color.b, a), 2.5f * unit, 8f * unit);

        // Its percentage under the crosshair, small.
        captionStyle.fontSize = Mathf.RoundToInt(12f * unit);
        captionStyle.normal.textColor = new Color(Cream.r, Cream.g, Cream.b, 0.75f * a);
        GUI.Label(new Rect(cx - 60f * unit, cy + radius + 2f * unit, 120f * unit, 18f * unit), $"{Mathf.RoundToInt(fill * 100f)}%", captionStyle);
    }

    /// <summary>A ring of short radial ticks (the first fill fraction of them), clockwise from the top.</summary>
    static void DrawRing(float cx, float cy, float radius, int ticks, float fill, Color color, float thickness, float length)
    {
        if (color.a <= 0.003f) return;
        int count = Mathf.RoundToInt(ticks * Mathf.Clamp01(fill));
        Matrix4x4 saved = GUI.matrix;
        Color old = GUI.color;
        GUI.color = color;
        for (int i = 0; i < count; i++)
        {
            float angle = i * 360f / ticks;
            GUIUtility.RotateAroundPivot(angle, new Vector2(cx, cy));
            GUI.DrawTexture(new Rect(cx - thickness * 0.5f, cy - radius - length * 0.5f, thickness, length), Texture2D.whiteTexture);
            GUI.matrix = saved;
        }
        GUI.color = old;
    }

    static void Diamond(float x, float y, float r, Color c)
    {
        Matrix4x4 saved = GUI.matrix;
        GUIUtility.RotateAroundPivot(45f, new Vector2(x, y));
        DrawRect(new Rect(x - r, y - r, r * 2f, r * 2f), c);
        GUI.matrix = saved;
    }

    void Caption(string text, float cx, float y, float unit, float a)
    {
        captionStyle.fontSize = Mathf.RoundToInt(14f * unit);
        string spaced = Spaced(text);
        captionStyle.normal.textColor = new Color(0f, 0f, 0f, 0.7f * a);
        GUI.Label(new Rect(cx - 300f + 1.5f, y - 10f * unit + 1.5f, 600f, 20f * unit), spaced, captionStyle);
        captionStyle.normal.textColor = new Color(Cream.r, Cream.g, Cream.b, a);
        GUI.Label(new Rect(cx - 300f, y - 10f * unit, 600f, 20f * unit), spaced, captionStyle);
    }

    /// <summary>A thin bar with diamond end caps and a soft glow along the filled part.</summary>
    static void DrawGiltBar(Rect bar, float fill, Color color, float unit)
    {
        DrawRect(new Rect(bar.x, bar.y - unit, bar.width, bar.height + 2f * unit), new Color(0f, 0f, 0f, 0.55f));
        float w = bar.width * Mathf.Clamp01(fill);
        DrawRect(new Rect(bar.x, bar.y - 2f * unit, w, bar.height + 4f * unit), new Color(color.r, color.g, color.b, 0.2f));
        DrawRect(new Rect(bar.x, bar.y, w, bar.height), color);
        Diamond(bar.x - 6f * unit, bar.center.y, 3f * unit, Ember);
        Diamond(bar.xMax + 6f * unit, bar.center.y, 3f * unit, Ember);
    }

    /// <summary>How many of the lamps have been lit, in the top right: a glowing dot and "3/10".</summary>
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
        GUI.Label(new Rect(textRect.x + 2f, textRect.y + 2f, textRect.width, textRect.height), $"{Lamp.LitCount}/{Lamp.All.Count}", smallStyle);
        GUI.color = Color.white;
        GUI.Label(textRect, $"{Lamp.LitCount}/{Lamp.All.Count}", smallStyle);
    }

    static Color EnergyColor(Lamp lamp) =>
        lamp.Energy < lamp.flickerBelow ? new Color(0.9f, 0.25f, 0.15f) : new Color(1f, 0.75f, 0.4f);

    static void DrawRect(Rect r, Color c)
    {
        Color old = GUI.color;
        GUI.color = c;
        GUI.DrawTexture(r, Texture2D.whiteTexture);
        GUI.color = old;
    }
}
