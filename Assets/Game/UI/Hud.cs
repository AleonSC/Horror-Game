using System.Text;
using Game.Core;
using Game.Player;
using UnityEngine;

namespace Game.UI
{
    /// <summary>
    /// The in-game display (section 7), a plain first version until the UI Toolkit one in milestone 7: the
    /// crosshair, what you can use and a meter while cranking, the lamps lit, battery and stamina, and the
    /// fade and message when you're taken. Laid out on a 1600 x 900 canvas, scaled to the screen.
    /// </summary>
    public sealed class Hud : MonoBehaviour
    {
        const float W = 1600f, H = 900f;
        static readonly Color Ink = new Color(0.86f, 0.84f, 0.8f, 0.9f);
        static readonly Color Band = new Color(0.02f, 0.02f, 0.025f, 0.55f);
        static readonly Color Red = new Color(0.85f, 0.08f, 0.05f, 1f);
        static readonly Color Warm = new Color(1f, 0.78f, 0.45f, 1f);

        GUIStyle prompt, small, message, tiny, tinyRight;
        string lastPrompt;
        string spacedPrompt;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void AttachToGame()
        {
            if (Services.TryGet<GameRoot>(out var root) && root.GetComponent<Hud>() == null)
                root.gameObject.AddComponent<Hud>();
        }

        void OnGUI()
        {
            if (Event.current.type != EventType.Repaint) return;
            EnsureStyles();
            float scale = Mathf.Min(Screen.width / W, Screen.height / H);
            var old = GUI.matrix;
            GUI.matrix = Matrix4x4.TRS(new Vector3((Screen.width - W * scale) * 0.5f, (Screen.height - H * scale) * 0.5f, 0f), Quaternion.identity, new Vector3(scale, scale, 1f));

            if (Services.TryGet<PlayerController>(out var player))
            {
                bool held = player.Held;
                if (!held) DrawCrosshair(player);
                DrawLamps();
                DrawMeters(player);
                if (player.Hotbar != null) DrawHotbar(player.Hotbar);
            }
            DrawFade();
            GUI.matrix = old;
        }

        void DrawCrosshair(PlayerController player)
        {
            IInteractable target = player.Interactor.Target;
            bool door = false;
            if (target == null && player.Interactor.Door != null)
            {
                target = player.Interactor.Door;
                door = true;
            }
            float size = target != null ? 5f : 3f;
            Rect(new Rect(W / 2 - size / 2, H / 2 - size / 2, size, size), new Color(1f, 1f, 1f, target != null ? 0.85f : 0.45f));
            if (target == null) return;

            if (target.Prompt != lastPrompt)
            {
                lastPrompt = target.Prompt;
                spacedPrompt = Spaced(target.Prompt);
            }
            // ◆ C R A N K ◆ on a soft dark band, with the mouse (or E) glyph.
            var band = new Rect(W / 2 - 170, H / 2 + 34, 340, 30);
            Rect(band, Band);
            Label(band, $"◆   {spacedPrompt}   ◆      {(door ? "[ E ]" : "[ LMB ]")}", prompt, Ink);

            if (target is IProgressInteractable p && p.Progress > 0f)
            {
                var bar = new Rect(W / 2 - 120, H / 2 + 70, 240, 5);
                Rect(bar, new Color(0f, 0f, 0f, 0.6f));
                Rect(new Rect(bar.x, bar.y, bar.width * p.Progress, bar.height), Warm);
            }
        }

        // A row of ten squares, filled as lamps are lit.
        void DrawLamps()
        {
            int lit = Services.TryGet<GameRun>(out var run) ? run.LampsLit : 0;
            const float size = 11f, gap = 7f;
            float x = W / 2 - (GameRun.LampCount * (size + gap) - gap) / 2;
            for (int i = 0; i < GameRun.LampCount; i++)
            {
                var r = new Rect(x + i * (size + gap), 24, size, size);
                Rect(r, new Color(0f, 0f, 0f, 0.5f));
                if (i < lit) Rect(new Rect(r.x + 2, r.y + 2, size - 4, size - 4), Warm);
                else Outline(r, new Color(1f, 1f, 1f, 0.25f));
            }
        }

        // Bottom left: the battery with its percentage, and the thin stamina bar above it.
        void DrawMeters(PlayerController player)
        {
            float x = 32, y = H - 46;
            if (player.Flashlight != null)
            {
                float charge = player.Flashlight.Battery.Charge;
                Rect(new Rect(x, y, 180, 12), new Color(0f, 0f, 0f, 0.55f));
                Color c = charge < player.Flashlight.Settings.lowBattery ? Red : Ink;
                Rect(new Rect(x + 2, y + 2, 176 * charge, 8), c);
                Label(new Rect(x + 188, y - 4, 80, 20), $"{Mathf.CeilToInt(charge * 100f)}%", small, Ink);
            }

            var stamina = player.Stamina;
            float length = 180f * (1f + stamina.Bonus);
            Rect(new Rect(x, y - 12, length, 4), new Color(0f, 0f, 0f, 0.45f));
            Color sc = stamina.Winded ? Red : Color.Lerp(Red, new Color(0.75f, 0.78f, 0.8f, 0.8f), Mathf.InverseLerp(0.1f, 0.6f, stamina.Value));
            if (stamina.Value < 0.999f || stamina.Winded) Rect(new Rect(x, y - 12, length * stamina.Value, 4), sc);
        }

        // Bottom centre: the eight slots, shown for a few seconds after a change (and while something's lit), the
        // held item's name and what's left in it, and any hint ("You need a lighter.").
        void DrawHotbar(Hotbar hotbar)
        {
            var items = hotbar.Items;
            var held = items.Held;
            float since = Time.unscaledTime - hotbar.LastChanged;
            float show = Mathf.Clamp01((3f - since) / 0.6f);
            if (!held.Empty && held.Lit) show = Mathf.Max(show, 0.5f);

            const float size = 46f, gap = 6f;
            float x0 = W / 2 - (Inventory.SlotCount * (size + gap) - gap) / 2, y = H - 92;
            if (show > 0f)
            {
                for (int i = 0; i < Inventory.SlotCount; i++)
                {
                    var r = new Rect(x0 + i * (size + gap), y, size, size);
                    var slot = items[i];
                    bool selected = i == items.Selected;
                    Rect(r, new Color(0f, 0f, 0f, (selected ? 0.7f : 0.45f) * show));
                    if (selected) Outline(r, new Color(Warm.r, Warm.g, Warm.b, 0.9f * show));
                    var ink = new Color(Ink.r, Ink.g, Ink.b, Ink.a * show);
                    Label(new Rect(r.x + 4, r.y + 2, 20, 14), (i + 1).ToString(), tiny, new Color(1f, 1f, 1f, 0.35f * show));
                    if (slot.Empty) continue;
                    Label(new Rect(r.x, r.y + 12, size, 22), Short(slot.Kind), tiny, slot.Lit ? new Color(Warm.r, Warm.g, Warm.b, show) : ink);
                    if (Inventory.Stacks(slot.Kind) && slot.Count > 1) Label(new Rect(r.x, r.y + 28, size - 4, 16), $"x{slot.Count}", tinyRight, ink);
                    if (slot.Kind == ItemKind.Torch || slot.Kind == ItemKind.Lighter) // (burn or fuel left)
                        Rect(new Rect(r.x + 4, r.yMax - 5, (size - 8) * Mathf.Clamp01(slot.Charge), 2), new Color(Warm.r, Warm.g, Warm.b, 0.8f * show));
                }
            }

            if (!held.Empty && held.Kind != ItemKind.Flashlight)
            {
                string name = Spaced(Inventory.NameOf(held.Kind));
                string extra = Detail(held, hotbar.Settings);
                Label(new Rect(0, y - 26, W, 20), extra.Length > 0 ? $"{name}    {extra}" : name, prompt, Ink);
            }

            string hint = hotbar.Hint;
            if (hint != null) Label(new Rect(0, H / 2 + 110, W, 24), hint, prompt, Ink);
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
                default: return "";
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
                default: return "";
            }
        }

        void DrawFade()
        {
            if (!Services.TryGet<ScreenFade>(out var fade) || fade.Alpha <= 0f) return;
            var c = fade.Color;
            c.a = fade.Alpha;
            var full = GUI.matrix.inverse.MultiplyPoint(new Vector3(Screen.width, Screen.height, 0f));
            var origin = GUI.matrix.inverse.MultiplyPoint(Vector3.zero);
            Rect(new Rect(origin.x, origin.y, full.x - origin.x, full.y - origin.y), c);
            if (fade.MessageAlpha > 0f && !string.IsNullOrEmpty(fade.Message))
            {
                var m = Red;
                m.a = fade.MessageAlpha;
                Label(new Rect(0, H / 2 - 30, W, 60), Spaced(fade.Message), message, m);
            }
        }

        // ------------------------------------------------------------------ drawing helpers

        void EnsureStyles()
        {
            if (prompt != null) return;
            prompt = new GUIStyle { fontSize = 14, alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Normal };
            small = new GUIStyle { fontSize = 14, alignment = TextAnchor.MiddleLeft };
            message = new GUIStyle { fontSize = 34, alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };
            tiny = new GUIStyle { fontSize = 11, alignment = TextAnchor.MiddleCenter };
            tinyRight = new GUIStyle { fontSize = 12, alignment = TextAnchor.MiddleRight };
        }

        static void Rect(Rect r, Color c)
        {
            var old = GUI.color;
            GUI.color = c;
            GUI.DrawTexture(r, Texture2D.whiteTexture);
            GUI.color = old;
        }

        static void Outline(Rect r, Color c)
        {
            Rect(new Rect(r.x, r.y, r.width, 1), c);
            Rect(new Rect(r.x, r.yMax - 1, r.width, 1), c);
            Rect(new Rect(r.x, r.y, 1, r.height), c);
            Rect(new Rect(r.xMax - 1, r.y, 1, r.height), c);
        }

        static void Label(Rect r, string text, GUIStyle style, Color c)
        {
            style.normal.textColor = c;
            GUI.Label(r, text, style);
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
