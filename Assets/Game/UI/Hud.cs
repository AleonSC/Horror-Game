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

        GUIStyle prompt, small, message;
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
            }
            DrawFade();
            GUI.matrix = old;
        }

        void DrawCrosshair(PlayerController player)
        {
            var target = player.Interactor.Target;
            float size = target != null ? 5f : 3f;
            Rect(new Rect(W / 2 - size / 2, H / 2 - size / 2, size, size), new Color(1f, 1f, 1f, target != null ? 0.85f : 0.45f));
            if (target == null) return;

            if (target.Prompt != lastPrompt)
            {
                lastPrompt = target.Prompt;
                spacedPrompt = Spaced(target.Prompt);
            }
            // ◆ C R A N K ◆ on a soft dark band, with the mouse glyph.
            var band = new Rect(W / 2 - 150, H / 2 + 34, 300, 30);
            Rect(band, Band);
            Label(band, $"◆   {spacedPrompt}   ◆      [ LMB ]", prompt, Ink);

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
