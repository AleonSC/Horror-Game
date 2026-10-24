using System.Text;
using Game.Core;
using Game.Player;
using UnityEngine;
using UnityEngine.UIElements;

namespace Game.UI
{
    /// <summary>
    /// The in-game display (section 7), in UI Toolkit: the crosshair, what you can use (with a meter while cranking),
    /// the row of ten lamps, battery and stamina bottom left, the hotbar for a moment after it changes (with what's
    /// held and what's left in it), hints, and the fade and message when you're taken.
    /// </summary>
    public sealed class Hud : MonoBehaviour
    {
        VisualElement dot, promptBand, meter, meterFill, hotbar, fade;
        Label prompt, held, hint, message, batteryText;
        VisualElement batteryFill, batteryBack, staminaBack, staminaFill;
        readonly VisualElement[] lampFill = new VisualElement[GameRun.LampCount], lampBox = new VisualElement[GameRun.LampCount];
        readonly VisualElement[] slot = new VisualElement[Inventory.SlotCount], slotMeter = new VisualElement[Inventory.SlotCount];
        readonly Label[] slotName = new Label[Inventory.SlotCount], slotCount = new Label[Inventory.SlotCount];
        string lastPrompt;
        bool built;

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

            // The crosshair, and the prompt band under it: ◆ C R A N K ◆ [ LMB ]
            dot = Centred(UiStyle.Box(hud, Color.white), 0f, 0f, 3f, 3f);
            promptBand = Centred(UiStyle.Box(hud, UiStyle.Band), 0f, 49f, 340f, 30f);
            prompt = UiStyle.Text(promptBand, "", 14, UiStyle.Ink);
            prompt.style.flexGrow = 1f;
            meter = Centred(UiStyle.Box(hud, new Color(0f, 0f, 0f, 0.6f)), 0f, 72f, 240f, 5f);
            meterFill = UiStyle.Box(meter, UiStyle.Warm);
            meterFill.style.height = Length.Percent(100);

            // Ten squares along the top, filled as lamps are lit.
            var row = UiStyle.Box(hud, UiStyle.Clear);
            row.style.position = Position.Absolute;
            row.style.top = 24;
            row.style.left = Length.Percent(50);
            row.style.marginLeft = -(GameRun.LampCount * 18f - 7f) / 2f;
            row.style.flexDirection = FlexDirection.Row;
            for (int i = 0; i < GameRun.LampCount; i++)
            {
                var box = UiStyle.Box(row, new Color(0f, 0f, 0f, 0.5f));
                box.style.width = box.style.height = 11;
                box.style.marginRight = i < GameRun.LampCount - 1 ? 7 : 0;
                box.style.paddingLeft = box.style.paddingRight = box.style.paddingTop = box.style.paddingBottom = 2;
                UiStyle.Border(box, new Color(1f, 1f, 1f, 0.25f));
                var fill = UiStyle.Box(box, UiStyle.Warm);
                fill.style.flexGrow = 1f;
                lampBox[i] = box;
                lampFill[i] = fill;
            }

            // Bottom left: the battery with its percentage, and the thin stamina bar above it.
            batteryBack = UiStyle.Box(hud, new Color(0f, 0f, 0f, 0.55f));
            batteryBack.style.position = Position.Absolute;
            batteryBack.style.left = 32;
            batteryBack.style.bottom = 34;
            batteryBack.style.width = 180;
            batteryBack.style.height = 12;
            batteryBack.style.paddingLeft = batteryBack.style.paddingRight = batteryBack.style.paddingTop = batteryBack.style.paddingBottom = 2;
            batteryFill = UiStyle.Box(batteryBack, UiStyle.Ink);
            batteryFill.style.height = Length.Percent(100);
            batteryText = UiStyle.Text(hud, "", 14, UiStyle.Ink, TextAnchor.MiddleLeft);
            batteryText.style.position = Position.Absolute;
            batteryText.style.left = 220;
            batteryText.style.bottom = 30;
            staminaBack = UiStyle.Box(hud, new Color(0f, 0f, 0f, 0.45f));
            staminaBack.style.position = Position.Absolute;
            staminaBack.style.left = 32;
            staminaBack.style.bottom = 54;
            staminaBack.style.height = 4;
            staminaFill = UiStyle.Box(staminaBack, UiStyle.Ink);
            staminaFill.style.height = Length.Percent(100);

            // Bottom centre: the hotbar's eight slots, the held item above them.
            hotbar = UiStyle.Box(hud, UiStyle.Clear);
            hotbar.style.position = Position.Absolute;
            hotbar.style.bottom = 46;
            hotbar.style.left = Length.Percent(50);
            hotbar.style.marginLeft = -(Inventory.SlotCount * 52f - 6f) / 2f;
            hotbar.style.flexDirection = FlexDirection.Row;
            for (int i = 0; i < Inventory.SlotCount; i++)
            {
                var s = UiStyle.Box(hotbar, new Color(0f, 0f, 0f, 0.45f));
                s.style.width = s.style.height = 46;
                s.style.marginRight = i < Inventory.SlotCount - 1 ? 6 : 0;
                UiStyle.Rounded(s, 2f);
                var num = UiStyle.Text(s, (i + 1).ToString(), 10, new Color(1f, 1f, 1f, 0.35f), TextAnchor.UpperLeft);
                num.style.position = Position.Absolute;
                num.style.left = 4;
                num.style.top = 2;
                slotName[i] = UiStyle.Fill(UiStyle.Text(s, "", 11, UiStyle.Ink));
                slotCount[i] = UiStyle.Text(s, "", 12, UiStyle.Ink, TextAnchor.LowerRight);
                slotCount[i].style.position = Position.Absolute;
                slotCount[i].style.right = 4;
                slotCount[i].style.bottom = 6;
                slotMeter[i] = UiStyle.Box(s, UiStyle.Warm);
                slotMeter[i].style.position = Position.Absolute;
                slotMeter[i].style.left = 4;
                slotMeter[i].style.bottom = 3;
                slotMeter[i].style.height = 2;
                slot[i] = s;
            }
            held = UiStyle.Text(hud, "", 14, UiStyle.Ink);
            held.style.position = Position.Absolute;
            held.style.left = 0;
            held.style.right = 0;
            held.style.bottom = 102;
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
            else { Show(promptBand, false); Show(meter, false); }
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
            float size = target != null ? 5f : 3f;
            dot.style.width = dot.style.height = size;
            dot.style.marginLeft = dot.style.marginTop = -size / 2f;
            dot.style.backgroundColor = new Color(1f, 1f, 1f, target != null ? 0.85f : 0.45f);
            Show(promptBand, target != null);
            if (target == null) { Show(meter, false); return; }
            string key = target.Prompt + (door ? "E" : "M");
            if (key != lastPrompt)
            {
                lastPrompt = key;
                prompt.text = $"◆   {Spaced(target.Prompt)}   ◆      {(door ? "[ E ]" : "[ LMB ]")}";
            }
            bool progress = target is IProgressInteractable p && p.Progress > 0f;
            Show(meter, progress);
            if (progress) meterFill.style.width = Length.Percent(((IProgressInteractable)target).Progress * 100f);
        }

        void Lamps()
        {
            int lit = Services.TryGet<GameRun>(out var run) ? run.LampsLit : 0;
            for (int i = 0; i < lampFill.Length; i++) Show(lampFill[i], i < lit);
        }

        void Meters(PlayerController player, bool hasPlayer)
        {
            bool light = hasPlayer && player.Flashlight != null;
            Show(batteryBack, light);
            Show(batteryText, light);
            if (light)
            {
                float charge = player.Flashlight.Battery.Charge;
                batteryFill.style.width = Length.Percent(charge * 100f);
                batteryFill.style.backgroundColor = charge < player.Flashlight.Settings.lowBattery ? UiStyle.Red : UiStyle.Ink;
                batteryText.text = $"{Mathf.CeilToInt(charge * 100f)}%";
            }
            Show(staminaBack, hasPlayer);
            if (!hasPlayer) return;
            var stamina = player.Stamina;
            staminaBack.style.width = 180f * (1f + stamina.Bonus);
            Show(staminaFill, stamina.Value < 0.999f || stamina.Winded);
            staminaFill.style.width = Length.Percent(stamina.Value * 100f);
            staminaFill.style.backgroundColor = stamina.Winded ? UiStyle.Red : Color.Lerp(UiStyle.Red, new Color(0.75f, 0.78f, 0.8f, 0.8f), Mathf.InverseLerp(0.1f, 0.6f, stamina.Value));
        }

        void Hotbar(Hotbar bar)
        {
            Show(hotbar, bar != null);
            Show(held, bar != null);
            Show(hint, bar != null && bar.Hint != null);
            if (bar == null) return;
            var items = bar.Items;
            var heldItem = items.Held;
            float since = Time.unscaledTime - bar.LastChanged;
            float show = Mathf.Clamp01((3f - since) / 0.6f);
            if (!heldItem.Empty && heldItem.Lit) show = Mathf.Max(show, 0.5f);
            hotbar.style.opacity = show;
            for (int i = 0; i < Inventory.SlotCount; i++)
            {
                var s = items[i];
                bool selected = i == items.Selected;
                slot[i].style.backgroundColor = new Color(0f, 0f, 0f, selected ? 0.7f : 0.45f);
                UiStyle.Border(slot[i], selected ? new Color(UiStyle.Warm.r, UiStyle.Warm.g, UiStyle.Warm.b, 0.9f) : UiStyle.Clear);
                slotName[i].text = s.Empty ? "" : Short(s.Kind);
                slotName[i].style.color = s.Lit ? UiStyle.Warm : UiStyle.Ink;
                slotCount[i].text = !s.Empty && Inventory.Stacks(s.Kind) && s.Count > 1 ? $"x{s.Count}" : "";
                bool hasMeter = !s.Empty && (s.Kind == ItemKind.Torch || s.Kind == ItemKind.Lighter);
                Show(slotMeter[i], hasMeter);
                if (hasMeter) slotMeter[i].style.width = 38f * Mathf.Clamp01(s.Charge);
            }
            if (!heldItem.Empty && heldItem.Kind != ItemKind.Flashlight)
            {
                string name = Spaced(Inventory.NameOf(heldItem.Kind));
                string extra = Detail(heldItem, bar.Settings);
                held.text = extra.Length > 0 ? $"{name}    {extra}" : name;
            }
            else held.text = "";
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

        static string Short(ItemKind kind)
        {
            switch (kind)
            {
                case ItemKind.Flashlight: return "LIGHT";
                case ItemKind.Batteries: return "BATT";
                case ItemKind.Torch: return "TORCH";
                case ItemKind.Lighter: return "LIGHTER";
                case ItemKind.FlareGun: return "FLARE";
                case ItemKind.Glowsticks: return "GLOW";
                case ItemKind.Camera: return "CAMERA";
                case ItemKind.KeyFob: return "FOB";
                case ItemKind.Map: return "MAP";
                case ItemKind.Drone: return "DRONE";
                case ItemKind.Radio: return "RADIO";
                default: return kind.ToString().ToUpperInvariant();
            }
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
                case ItemKind.KeyFob: return "F: the car chirps";
                case ItemKind.Map: return "F to hold it up";
                case ItemKind.Drone: return held.Lit ? $"out there   F to fly it ({Mathf.CeilToInt((Drone.Current != null ? Drone.Current.Battery : held.Charge) * 100f)}%)" : $"battery {Mathf.CeilToInt(held.Charge * 100f)}%   F to set it down and fly";
                case ItemKind.Radio: return "F to set it down, playing";
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
