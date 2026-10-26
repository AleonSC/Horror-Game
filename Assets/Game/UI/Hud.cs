using System.Text;
using Game.Core;
using Game.Player;
using UnityEngine;
using UnityEngine.UIElements;

namespace Game.UI
{
    /// <summary>
    /// The in-game display (section 7), in UI Toolkit: the crosshair (with ticks closing in on what you can use) and
    /// its prompt (with a meter while cranking), the row of ten lamp diamonds, the battery and stamina bottom left,
    /// the hotbar for a moment after it changes (drawings of the items, the one in hand raised and bracketed, what
    /// it is and what's left in it above), hints, and the fade and message when you're taken.
    /// </summary>
    public sealed class Hud : MonoBehaviour
    {
        VisualElement dot, reticle, promptBand, meter, meterFill, hotbar, fade;
        Label prompt, promptKey, heldName, heldDetail, hint, message, batteryText, lampCount;
        VisualElement batteryFrame, batteryIcon, staminaBack, staminaFill;
        readonly VisualElement[] batteryCells = new VisualElement[10];
        readonly VisualElement[] lampFill = new VisualElement[GameRun.LampCount], lampBox = new VisualElement[GameRun.LampCount], lampHalo = new VisualElement[GameRun.LampCount];
        readonly VisualElement[] slot = new VisualElement[Inventory.SlotCount], slotMeter = new VisualElement[Inventory.SlotCount], slotIcon = new VisualElement[Inventory.SlotCount];
        readonly VisualElement[] slotBrackets = new VisualElement[Inventory.SlotCount], slotGlow = new VisualElement[Inventory.SlotCount];
        readonly Label[] slotCount = new Label[Inventory.SlotCount];
        readonly float[] slotRaise = new float[Inventory.SlotCount];
        readonly ItemKind[] slotKind = new ItemKind[Inventory.SlotCount];
        readonly bool[] slotLit = new bool[Inventory.SlotCount];
        string lastPrompt;
        int lampsShown = -1;
        float lampPulse = 10f, reticleShown;
        bool built;

        const float SlotSize = 54f, SlotGap = 8f;
        static readonly Color Faint = new Color(1f, 1f, 1f, 0.1f);
        static Color Warmish(float a) => new Color(UiStyle.Warm.r, UiStyle.Warm.g, UiStyle.Warm.b, a);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void AttachToGame()
        {
            if (Services.TryGet<GameRoot>(out var root) && root.GetComponent<Hud>() == null)
                root.gameObject.AddComponent<Hud>();
        }

        void Start() => Build();

        void Build()
        {
            var ui = UiRoot.Get();
            if (ui == null) return;
            var hud = ui.Hud;

            // The crosshair (a dot, with four ticks closing round it on something you can use), and the prompt band
            // under it, ruled in warm lines: ◆ C R A N K ◆ and the key to press, as a keycap
            dot = Centred(UiStyle.Box(hud, Color.white), 0f, 0f, 3f, 3f);
            UiStyle.Rounded(dot, 2f);
            reticle = Centred(UiStyle.Box(hud, UiStyle.Clear), 0f, 0f, 30f, 30f);
            for (int i = 0; i < 4; i++)
            {
                bool across = i < 2;
                var tick = UiStyle.Box(reticle, new Color(1f, 1f, 1f, 0.75f));
                tick.style.position = Position.Absolute;
                tick.style.width = across ? 6 : 1.5f;
                tick.style.height = across ? 1.5f : 6;
                if (across) { tick.style.top = 14.25f; tick.style.left = i == 0 ? 0 : 24; }
                else { tick.style.left = 14.25f; tick.style.top = i == 2 ? 0 : 24; }
            }
            promptBand = Centred(UiStyle.Box(hud, UiStyle.Band), 0f, 52f, 360f, 32f);
            promptBand.style.borderTopWidth = promptBand.style.borderBottomWidth = 1f;
            promptBand.style.borderTopColor = promptBand.style.borderBottomColor = Warmish(0.3f);
            promptBand.style.flexDirection = FlexDirection.Row;
            promptBand.style.justifyContent = Justify.Center;
            promptBand.style.alignItems = Align.Center;
            prompt = UiStyle.Text(promptBand, "", 14, UiStyle.Ink);
            promptKey = UiStyle.Keycap(promptBand, "LMB", 11);
            promptKey.style.marginLeft = 16;
            meter = Centred(UiStyle.Box(hud, new Color(0f, 0f, 0f, 0.6f)), 0f, 76f, 240f, 5f);
            UiStyle.Rounded(meter, 2f);
            UiStyle.Border(meter, Faint);
            meterFill = UiStyle.Box(meter, UiStyle.Warm);
            meterFill.style.height = Length.Percent(100);

            // Ten diamonds along the top on a fine rule, lit as lamps are, a soft glow round each lit one.
            const float lampGap = 28f;
            float rowWidth = (GameRun.LampCount - 1) * lampGap + 24f;
            var row = UiStyle.Box(hud, UiStyle.Clear);
            row.style.position = Position.Absolute;
            row.style.top = 20;
            row.style.left = Length.Percent(50);
            row.style.marginLeft = -rowWidth / 2f;
            row.style.width = rowWidth;
            row.style.height = 24;
            var rule = UiStyle.Box(row, new Color(1f, 1f, 1f, 0.14f));
            rule.style.position = Position.Absolute;
            rule.style.left = -30;
            rule.style.right = -30;
            rule.style.top = 11.5f;
            rule.style.height = 1;
            for (int i = 0; i < GameRun.LampCount; i++)
            {
                float x = i * lampGap + 12f;
                var halo = Diamond(row, x, 12f, 24f, Warmish(0.16f));
                var box = Diamond(row, x, 12f, 11f, new Color(0.02f, 0.02f, 0.025f, 0.85f));
                UiStyle.Border(box, new Color(1f, 1f, 1f, 0.35f));
                var fill = Diamond(box, 5.5f - 1f, 5.5f - 1f, 7f, UiStyle.Warm);
                fill.style.rotate = new Rotate(new Angle(0f, AngleUnit.Degree));
                fill.style.left = 1f;
                fill.style.top = 1f;
                lampBox[i] = box;
                lampFill[i] = fill;
                lampHalo[i] = halo;
            }
            lampCount = UiStyle.Text(hud, "", 12, UiStyle.Muted, TextAnchor.MiddleLeft, 2f);
            lampCount.style.position = Position.Absolute;
            lampCount.style.top = 25;
            lampCount.style.left = Length.Percent(50);
            lampCount.style.marginLeft = rowWidth / 2f + 40f;

            // Bottom left: the flashlight's battery as a battery, ten cells in it, its percentage beside it, and the
            // thin stamina bar above.
            batteryIcon = UiStyle.Box(hud, UiStyle.Clear);
            batteryIcon.style.position = Position.Absolute;
            batteryIcon.style.left = 30;
            batteryIcon.style.bottom = 27;
            batteryIcon.style.width = batteryIcon.style.height = 22;
            batteryIcon.style.backgroundImage = new StyleBackground(ItemIcons.For(ItemKind.Flashlight));
            batteryIcon.style.unityBackgroundImageTintColor = UiStyle.Muted;
            batteryFrame = UiStyle.Box(hud, new Color(0f, 0f, 0f, 0.5f));
            batteryFrame.style.position = Position.Absolute;
            batteryFrame.style.left = 60;
            batteryFrame.style.bottom = 29;
            batteryFrame.style.width = 150;
            batteryFrame.style.height = 18;
            batteryFrame.style.flexDirection = FlexDirection.Row;
            batteryFrame.style.paddingLeft = batteryFrame.style.paddingRight = batteryFrame.style.paddingTop = batteryFrame.style.paddingBottom = 2.5f;
            UiStyle.Border(batteryFrame, new Color(1f, 1f, 1f, 0.45f), 1.5f);
            UiStyle.Rounded(batteryFrame, 3f);
            for (int i = 0; i < batteryCells.Length; i++)
            {
                var cell = UiStyle.Box(batteryFrame, UiStyle.Ink);
                cell.style.flexGrow = 1f;
                cell.style.marginRight = i < batteryCells.Length - 1 ? 2 : 0;
                UiStyle.Rounded(cell, 1f);
                batteryCells[i] = cell;
            }
            var nub = UiStyle.Box(batteryFrame, new Color(1f, 1f, 1f, 0.45f));
            nub.style.position = Position.Absolute;
            nub.style.right = -6;
            nub.style.top = 4;
            nub.style.width = 3;
            nub.style.height = 7;
            batteryText = UiStyle.Text(hud, "", 14, UiStyle.Ink, TextAnchor.MiddleLeft, 1f);
            batteryText.style.position = Position.Absolute;
            batteryText.style.left = 224;
            batteryText.style.bottom = 28;
            staminaBack = UiStyle.Box(hud, new Color(0f, 0f, 0f, 0.45f));
            staminaBack.style.position = Position.Absolute;
            staminaBack.style.left = 60;
            staminaBack.style.bottom = 56;
            staminaBack.style.height = 4;
            UiStyle.Rounded(staminaBack, 2f);
            staminaFill = UiStyle.Box(staminaBack, UiStyle.Ink);
            staminaFill.style.height = Length.Percent(100);
            UiStyle.Rounded(staminaFill, 2f);

            // Bottom centre: the hotbar. A dark band ruled above with a diamond in the middle; eight slots on it,
            // each a drawing of what's in it, its key and how many; the one in hand raised and bracketed in warm
            // light. What's held is named above, with a word on it under.
            float barWidth = Inventory.SlotCount * SlotSize + (Inventory.SlotCount - 1) * SlotGap;
            hotbar = UiStyle.Box(hud, UiStyle.Clear);
            hotbar.style.position = Position.Absolute;
            hotbar.style.bottom = 30;
            hotbar.style.left = Length.Percent(50);
            hotbar.style.marginLeft = -(barWidth + 28f) / 2f;
            hotbar.style.width = barWidth + 28f;
            hotbar.style.height = SlotSize + 24f;
            var band = UiStyle.Fill(UiStyle.Box(hotbar, new Color(0.015f, 0.015f, 0.02f, 0.55f)));
            band.style.top = 14;
            UiStyle.Rounded(band, 4f);
            UiStyle.Border(band, new Color(1f, 1f, 1f, 0.06f));
            var ruleTop = UiStyle.Box(hotbar, Warmish(0.35f));
            ruleTop.style.position = Position.Absolute;
            ruleTop.style.left = 30;
            ruleTop.style.right = 30;
            ruleTop.style.top = 8;
            ruleTop.style.height = 1;
            Diamond(hotbar, (barWidth + 28f) / 2f, 8.5f, 7f, UiStyle.Warm);
            for (int i = 0; i < Inventory.SlotCount; i++)
            {
                var s = UiStyle.Box(hotbar, new Color(0f, 0f, 0f, 0.5f));
                s.style.position = Position.Absolute;
                s.style.left = 14 + i * (SlotSize + SlotGap);
                s.style.top = 19;
                s.style.width = s.style.height = SlotSize;
                UiStyle.Rounded(s, 3f);
                UiStyle.Border(s, Faint);
                var gloss = UiStyle.Box(s, new Color(1f, 1f, 1f, 0.035f));
                gloss.style.position = Position.Absolute;
                gloss.style.left = gloss.style.right = gloss.style.top = 0;
                gloss.style.height = Length.Percent(45);
                var glow = UiStyle.Box(s, Warmish(0.14f));
                glow.style.position = Position.Absolute;
                glow.style.left = glow.style.top = 7;
                glow.style.width = glow.style.height = SlotSize - 16f;
                UiStyle.Rounded(glow, 20f);
                slotGlow[i] = glow;
                var icon = UiStyle.Box(s, UiStyle.Clear);
                icon.style.position = Position.Absolute;
                icon.style.left = icon.style.top = (SlotSize - 2f - 32f) / 2f;
                icon.style.width = icon.style.height = 32;
                slotIcon[i] = icon;
                var num = UiStyle.Text(s, (i + 1).ToString(), 10, new Color(1f, 1f, 1f, 0.32f), TextAnchor.UpperLeft);
                num.style.position = Position.Absolute;
                num.style.left = 4;
                num.style.top = 2;
                slotCount[i] = UiStyle.Text(s, "", 11, UiStyle.Ink, TextAnchor.LowerRight);
                slotCount[i].style.position = Position.Absolute;
                slotCount[i].style.right = 4;
                slotCount[i].style.bottom = 5;
                slotMeter[i] = UiStyle.Box(s, UiStyle.Warm);
                slotMeter[i].style.position = Position.Absolute;
                slotMeter[i].style.left = 5;
                slotMeter[i].style.bottom = 3;
                slotMeter[i].style.height = 2;
                UiStyle.Rounded(slotMeter[i], 1f);
                slotBrackets[i] = Brackets(s, SlotSize - 2f, 10f, UiStyle.Warm);
                slot[i] = s;
            }
            heldName = UiStyle.Text(hud, "", 15, UiStyle.Ink, TextAnchor.MiddleCenter, 1f);
            heldName.style.position = Position.Absolute;
            heldName.style.left = 0;
            heldName.style.right = 0;
            heldName.style.bottom = 132;
            heldDetail = UiStyle.Text(hud, "", 12, UiStyle.Muted, TextAnchor.MiddleCenter, 0.5f);
            heldDetail.style.position = Position.Absolute;
            heldDetail.style.left = 0;
            heldDetail.style.right = 0;
            heldDetail.style.bottom = 114;
            hint = UiStyle.Text(hud, "", 14, UiStyle.Ink);
            hint.style.position = Position.Absolute;
            hint.style.left = 0;
            hint.style.right = 0;
            hint.style.top = Length.Percent(50);
            hint.style.marginTop = 110;

            // The fade (being taken, waking), over everything.
            fade = UiStyle.Fill(UiStyle.Box(ui.Top, Color.black));
            message = UiStyle.Fill(UiStyle.Text(fade, "", 34, UiStyle.Red));
            message.style.unityFontStyleAndWeight = FontStyle.Bold;
            built = true;
        }

        // Centre an element on the screen's middle, offset by (x, y).
        static VisualElement Centred(VisualElement e, float x, float y, float w, float h)
        {
            e.style.position = Position.Absolute;
            e.style.left = Length.Percent(50);
            e.style.top = Length.Percent(50);
            e.style.width = w;
            e.style.height = h;
            e.style.marginLeft = x - w / 2f;
            e.style.marginTop = y - h / 2f;
            return e;
        }

        // A square turned on its corner, centred at (x, y) in its parent.
        static VisualElement Diamond(VisualElement parent, float x, float y, float size, Color colour)
        {
            var d = UiStyle.Box(parent, colour);
            d.style.position = Position.Absolute;
            d.style.left = x - size / 2f;
            d.style.top = y - size / 2f;
            d.style.width = d.style.height = size;
            d.style.rotate = new Rotate(new Angle(45f, AngleUnit.Degree));
            return d;
        }

        // Four L-shaped corner brackets round a square this size, each arm this long.
        static VisualElement Brackets(VisualElement parent, float size, float arm, Color colour)
        {
            var holder = UiStyle.Box(parent, UiStyle.Clear);
            holder.style.position = Position.Absolute;
            holder.style.left = holder.style.top = -3;
            holder.style.width = holder.style.height = size + 6f;
            for (int c = 0; c < 4; c++)
            {
                bool right = c % 2 == 1, bottom = c >= 2;
                for (int k = 0; k < 2; k++)
                {
                    var e = UiStyle.Box(holder, colour);
                    e.style.position = Position.Absolute;
                    e.style.width = k == 0 ? arm : 2f;
                    e.style.height = k == 0 ? 2f : arm;
                    if (right) e.style.right = 0; else e.style.left = 0;
                    if (bottom) e.style.bottom = 0; else e.style.top = 0;
                }
            }
            return holder;
        }

        void LateUpdate()
        {
            if (!built) return;
            // (No HUD under the main menu or the intro.)
            var hudLayer = dot.parent;
            var want = GameFlow.IsPlaying && !Drone.Piloting ? DisplayStyle.Flex : DisplayStyle.None; // (the drone has its own)
            if (hudLayer.style.display != want) hudLayer.style.display = want;
            bool hasPlayer = Services.TryGet<PlayerController>(out var player);
            Show(dot, hasPlayer && !player.Held);
            if (hasPlayer && !player.Held) Crosshair(player);
            else { Show(promptBand, false); Show(meter, false); Show(reticle, false); }
            Lamps();
            Meters(player, hasPlayer);
            Hotbar(hasPlayer ? player.Hotbar : null);
            Fade();
        }

        void Crosshair(PlayerController player)
        {
            IInteractable target = player.Interactor.Target;
            bool door = false;
            if (target == null && player.Interactor.Door != null)
            {
                target = player.Interactor.Door;
                door = true;
            }
            float size = target != null ? 4f : 3f;
            dot.style.width = dot.style.height = size;
            dot.style.marginLeft = dot.style.marginTop = -size / 2f;
            dot.style.backgroundColor = new Color(1f, 1f, 1f, target != null ? 0.9f : 0.45f);
            // The ticks close in round what you're aiming at.
            reticleShown = Mathf.MoveTowards(reticleShown, target != null ? 1f : 0f, Time.unscaledDeltaTime * 6f);
            Show(reticle, reticleShown > 0f);
            if (reticleShown > 0f)
            {
                float e = reticleShown * reticleShown * (3f - 2f * reticleShown);
                reticle.style.opacity = e;
                reticle.style.scale = new Scale(Vector3.one * Mathf.Lerp(1.8f, 1f, e));
            }
            Show(promptBand, target != null);
            if (target == null) { Show(meter, false); return; }
            string key = target.Prompt + (door ? "E" : "M");
            if (key != lastPrompt)
            {
                lastPrompt = key;
                prompt.text = $"◆   {Spaced(target.Prompt)}   ◆";
                promptKey.text = door ? "E" : "LMB";
            }
            bool progress = target is IProgressInteractable p && p.Progress > 0f;
            Show(meter, progress);
            if (progress) meterFill.style.width = Length.Percent(((IProgressInteractable)target).Progress * 100f);
        }

        void Lamps()
        {
            int lit = Services.TryGet<GameRun>(out var run) ? run.LampsLit : 0;
            if (lit != lampsShown)
            {
                if (lampsShown >= 0 && lit > lampsShown) lampPulse = 0f;
                lampsShown = lit;
                lampCount.text = $"{lit}  /  {GameRun.LampCount}";
            }
            lampPulse += Time.unscaledDeltaTime;
            float pulse = Mathf.Clamp01(1f - lampPulse / 1.4f);
            for (int i = 0; i < lampFill.Length; i++)
            {
                bool on = i < lit;
                Show(lampFill[i], on);
                Show(lampHalo[i], on);
                UiStyle.Border(lampBox[i], on ? Warmish(0.8f) : new Color(1f, 1f, 1f, 0.35f));
                // The one just lit swells and settles, its glow flaring.
                bool newest = on && i == lit - 1 && pulse > 0f;
                float grow = newest ? 1f + 0.6f * pulse * pulse : 1f;
                lampBox[i].style.scale = new Scale(Vector3.one * grow);
                lampHalo[i].style.scale = new Scale(Vector3.one * (newest ? 1f + 1.2f * pulse : 1f));
                lampHalo[i].style.opacity = newest ? 0.6f + 0.4f * pulse : 0.6f + 0.15f * Mathf.Sin(Time.unscaledTime * 1.3f + i);
            }
        }

        void Meters(PlayerController player, bool hasPlayer)
        {
            bool light = hasPlayer && player.Flashlight != null;
            Show(batteryFrame, light);
            Show(batteryText, light);
            Show(batteryIcon, light);
            if (light)
            {
                float charge = player.Flashlight.Battery.Charge;
                bool low = charge < player.Flashlight.Settings.lowBattery;
                // Low: the cells go red and blink.
                float blink = low ? (Mathf.Repeat(Time.unscaledTime, 1f) < 0.6f ? 1f : 0.35f) : 1f;
                float cells = charge * batteryCells.Length;
                for (int i = 0; i < batteryCells.Length; i++)
                {
                    float fill = Mathf.Clamp01(cells - i);
                    batteryCells[i].style.backgroundColor = low ? UiStyle.Red : UiStyle.Ink;
                    batteryCells[i].style.opacity = (fill > 0f ? 0.25f + 0.75f * fill : 0.07f) * blink;
                }
                batteryText.text = $"{Mathf.CeilToInt(charge * 100f)}%";
                batteryText.style.color = low ? UiStyle.Red : UiStyle.Ink;
            }
            Show(staminaBack, hasPlayer);
            if (!hasPlayer) return;
            var stamina = player.Stamina;
            staminaBack.style.width = 150f * (1f + stamina.Bonus);
            Show(staminaFill, stamina.Value < 0.999f || stamina.Winded);
            staminaFill.style.width = Length.Percent(stamina.Value * 100f);
            staminaFill.style.backgroundColor = stamina.Winded ? UiStyle.Red : Color.Lerp(UiStyle.Red, new Color(0.75f, 0.78f, 0.8f, 0.8f), Mathf.InverseLerp(0.1f, 0.6f, stamina.Value));
        }

        void Hotbar(Hotbar bar)
        {
            Show(hotbar, bar != null);
            Show(heldName, bar != null);
            Show(heldDetail, bar != null);
            Show(hint, bar != null && bar.Hint != null);
            if (bar == null) return;
            var items = bar.Items;
            var heldItem = items.Held;
            float since = Time.unscaledTime - bar.LastChanged;
            float show = Mathf.Clamp01((3f - since) / 0.6f);
            if (!heldItem.Empty && heldItem.Lit) show = Mathf.Max(show, 0.5f);
            hotbar.style.opacity = show;
            heldName.style.opacity = heldDetail.style.opacity = Mathf.Max(show, 0.75f);
            float dt = Time.unscaledDeltaTime;
            for (int i = 0; i < Inventory.SlotCount; i++)
            {
                var s = items[i];
                bool selected = i == items.Selected;
                // The one in hand rises and is bracketed; the rest sit back.
                slotRaise[i] = Mathf.MoveTowards(slotRaise[i], selected ? 1f : 0f, dt * 7f);
                float e = slotRaise[i] * slotRaise[i] * (3f - 2f * slotRaise[i]);
                slot[i].style.translate = new Translate(0f, -7f * e);
                slot[i].style.backgroundColor = Color.Lerp(new Color(0f, 0f, 0f, 0.5f), new Color(0.1f, 0.075f, 0.045f, 0.82f), e);
                UiStyle.Border(slot[i], Color.Lerp(Faint, Warmish(0.5f), e));
                Show(slotBrackets[i], e > 0.01f);
                slotBrackets[i].style.opacity = e;
                if (slotKind[i] != s.Kind || slotLit[i] != s.Lit)
                {
                    slotKind[i] = s.Kind;
                    slotLit[i] = s.Lit;
                    var tex = s.Empty ? null : ItemIcons.For(s);
                    slotIcon[i].style.backgroundImage = tex != null ? new StyleBackground(tex) : new StyleBackground(StyleKeyword.None);
                }
                Show(slotIcon[i], !s.Empty);
                slotIcon[i].style.unityBackgroundImageTintColor = s.Lit ? UiStyle.Warm : Color.Lerp(new Color(0.8f, 0.78f, 0.74f, 0.75f), Color.white, e);
                Show(slotGlow[i], !s.Empty && s.Lit);
                if (!s.Empty && s.Lit) slotGlow[i].style.opacity = 0.75f + 0.25f * Mathf.Sin(Time.unscaledTime * 6f + i);
                slotCount[i].text = !s.Empty && Inventory.Stacks(s.Kind) && s.Count > 1 ? $"x{s.Count}" : "";
                bool hasMeter = !s.Empty && (s.Kind == ItemKind.Torch || s.Kind == ItemKind.Lighter || s.Kind == ItemKind.Spray);
                Show(slotMeter[i], hasMeter);
                if (hasMeter) slotMeter[i].style.width = (SlotSize - 12f) * Mathf.Clamp01(s.Charge);
            }
            if (!heldItem.Empty && heldItem.Kind != ItemKind.Flashlight)
            {
                heldName.text = $"◆   {Spaced(Inventory.NameOf(heldItem.Kind))}   ◆";
                heldName.style.color = heldItem.Lit ? UiStyle.Warm : UiStyle.Ink;
                heldDetail.text = Detail(heldItem, bar.Settings);
            }
            else heldName.text = heldDetail.text = "";
            if (bar.Hint != null) hint.text = bar.Hint;
        }

        void Fade()
        {
            var f = Services.TryGet<ScreenFade>(out var sf) ? sf : null;
            bool on = f != null && f.Alpha > 0f;
            Show(fade, on);
            if (!on) return;
            var c = f.Color;
            c.a = f.Alpha;
            fade.style.backgroundColor = c;
            bool msg = f.MessageAlpha > 0f && !string.IsNullOrEmpty(f.Message);
            Show(message, msg);
            if (!msg) return;
            message.text = Spaced(f.Message);
            message.style.color = new Color(UiStyle.Red.r, UiStyle.Red.g, UiStyle.Red.b, f.MessageAlpha);
        }

        static void Show(VisualElement e, bool on)
        {
            var d = on ? DisplayStyle.Flex : DisplayStyle.None;
            if (e.style.display != d) e.style.display = d;
        }

        static string Detail(ItemStack held, ItemSettings s)
        {
            switch (held.Kind)
            {
                case ItemKind.Torch: return held.Lit ? $"burning  {Mathf.CeilToInt(held.Charge * s.torchBurnTime)} s" : "F to light (uses lighter fuel)";
                case ItemKind.Lighter: return $"fuel {Mathf.CeilToInt(held.Charge * 100f)}%{(held.Lit ? "" : "   F to open")}";
                case ItemKind.Batteries: return $"x{held.Count}   F to put one in";
                case ItemKind.Glowsticks: return $"x{held.Count}   F to crack and toss";
                case ItemKind.Camera: return $"{Mathf.FloorToInt(held.Charge)} shots";
                case ItemKind.FlareGun: return "one flare: F to fire it into the sky";
                case ItemKind.KeyFob: return "LMB: the car chirps   RMB: its alarm";
                case ItemKind.Map: return "F to hold it up";
                case ItemKind.Drone: return held.Lit ? $"out there   F to fly it ({Mathf.CeilToInt((Drone.Current != null ? Drone.Current.Battery : held.Charge) * 100f)}%)" : $"battery {Mathf.CeilToInt(held.Charge * 100f)}%   F to set it down and fly";
                case ItemKind.Radio: return "F to set it down, playing";
                case ItemKind.Spray: return $"paint {Mathf.CeilToInt(held.Charge * 100f)}%   hold F to spray (it glows)";
                default: return "";
            }
        }

        /// <summary>"Crank" to "C R A N K".</summary>
        public static string Spaced(string text)
        {
            var sb = new StringBuilder(text.Length * 2);
            foreach (char ch in text.ToUpperInvariant())
            {
                if (sb.Length > 0) sb.Append(' ');
                sb.Append(ch);
            }
            return sb.ToString();
        }
    }
}
