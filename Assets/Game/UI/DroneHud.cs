using Game.Core;
using Game.Player;
using UnityEngine;
using UnityEngine.UIElements;

namespace Game.UI
{
    /// <summary>
    /// The drone's screen while you fly it (section 7): a recording frame, battery and signal, height and how far
    /// from you it is, the lamp and thermal, warnings when the battery's low or the signal's going, and static
    /// (a little always, more as the signal weakens). The usual HUD is hidden meanwhile.
    /// </summary>
    public sealed class DroneHud : MonoBehaviour
    {
        VisualElement root, noise, rec;
        Label battery, height, distance, mode, warning;
        VisualElement batteryFill;
        readonly VisualElement[] bars = new VisualElement[4];
        Texture2D noiseTexture;
        float burstUntil;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void AttachToGame()
        {
            if (!Services.TryGet<PlayerController>(out _)) return;
            if (Services.TryGet<GameRoot>(out var root) && root.GetComponent<DroneHud>() == null)
                root.gameObject.AddComponent<DroneHud>();
        }

        void Start()
        {
            var ui = UiRoot.Get();
            if (ui == null) return;
            root = UiStyle.Fill(new VisualElement { name = "Drone screen", pickingMode = PickingMode.Ignore });
            root.style.display = DisplayStyle.None;
            ui.Menus.Add(root);
            root.SendToBack(); // (under the pause menu)

            // Static: a field of noise jumping about each frame, stronger as the signal goes.
            noiseTexture = new Texture2D(256, 256, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Repeat, name = "Static" };
            var px = new Color32[256 * 256];
            for (int i = 0; i < px.Length; i++)
            {
                byte v = (byte)Random.Range(0, 255);
                px[i] = new Color32(v, v, v, (byte)Random.Range(90, 255));
            }
            noiseTexture.SetPixels32(px);
            noiseTexture.Apply(false);
            noise = new VisualElement { pickingMode = PickingMode.Ignore };
            noise.style.position = Position.Absolute;
            noise.style.left = -128;
            noise.style.top = -128;
            noise.style.width = UiRoot.Width + 256;
            noise.style.height = UiRoot.Height + 256;
            noise.style.backgroundImage = new StyleBackground(noiseTexture);
            noise.style.opacity = 0f;
            root.Add(noise);

            // The frame's corners.
            for (int c = 0; c < 4; c++)
            {
                bool right = c % 2 == 1, bottom = c >= 2;
                Corner(right, bottom, true);
                Corner(right, bottom, false);
            }

            rec = UiStyle.Box(root, UiStyle.Red);
            rec.style.position = Position.Absolute;
            rec.style.left = 90;
            rec.style.top = 72;
            rec.style.width = 12;
            rec.style.height = 12;
            UiStyle.Rounded(rec, 6f);
            Place(UiStyle.Text(root, "REC", 16, UiStyle.Ink, TextAnchor.MiddleLeft, 2f), 110, 68);

            battery = Place(UiStyle.Text(root, "", 16, UiStyle.Ink, TextAnchor.MiddleRight, 1f), -1, 68, 170, true);
            var barBack = UiStyle.Box(root, new Color(1f, 1f, 1f, 0.15f));
            barBack.style.position = Position.Absolute;
            barBack.style.right = 90;
            barBack.style.top = 94;
            barBack.style.width = 120;
            barBack.style.height = 5;
            batteryFill = UiStyle.Box(barBack, UiStyle.Ink);
            batteryFill.style.height = Length.Percent(100);
            Place(UiStyle.Text(root, "SIGNAL", 13, UiStyle.Muted, TextAnchor.MiddleRight, 1f), -1, 108, 170, true);
            for (int i = 0; i < 4; i++)
            {
                var b = UiStyle.Box(root, UiStyle.Ink);
                b.style.position = Position.Absolute;
                b.style.right = 90 + (3 - i) * 9;
                b.style.top = 148 - (i + 1) * 5;
                b.style.width = 6;
                b.style.height = (i + 1) * 5;
                bars[i] = b;
            }
            height = Place(UiStyle.Text(root, "", 15, UiStyle.Ink, TextAnchor.MiddleLeft, 1f), 90, UiRoot.Height - 110);
            distance = Place(UiStyle.Text(root, "", 15, UiStyle.Ink, TextAnchor.MiddleLeft, 1f), 90, UiRoot.Height - 88);
            mode = Place(UiStyle.Text(root, "", 15, UiStyle.Warm, TextAnchor.MiddleRight, 2f), -1, UiRoot.Height - 110, 300, true);

            warning = UiStyle.Text(root, "", 20, UiStyle.Red, TextAnchor.MiddleCenter, 4f);
            warning.style.position = Position.Absolute;
            warning.style.left = 0;
            warning.style.right = 0;
            warning.style.top = 120;
            warning.style.unityFontStyleAndWeight = FontStyle.Bold;

            var cross = UiStyle.Text(root, "+", 22, new Color(1f, 1f, 1f, 0.5f), TextAnchor.MiddleCenter);
            cross.style.position = Position.Absolute;
            cross.style.left = 0;
            cross.style.right = 0;
            cross.style.top = UiRoot.Height * 0.5f - 14f;

            var hint = UiStyle.KeyHints(root, 12, ("W A S D", "Fly"), ("Space", "Up"), ("Ctrl", "Down"), ("Shift", "Fast"), ("LMB", "Lamp"), ("T", "Thermal"), ("F", "Back to you"));
            hint.style.position = Position.Absolute;
            hint.style.left = 0;
            hint.style.right = 0;
            hint.style.bottom = 36;
            hint.style.justifyContent = Justify.Center;
        }

        void OnDestroy()
        {
            if (noiseTexture != null) Destroy(noiseTexture);
        }

        void Corner(bool right, bool bottom, bool across)
        {
            var e = UiStyle.Box(root, new Color(1f, 1f, 1f, 0.55f));
            e.style.position = Position.Absolute;
            e.style.width = across ? 46 : 2;
            e.style.height = across ? 2 : 46;
            if (right) e.style.right = 60; else e.style.left = 60;
            if (bottom) e.style.bottom = 50; else e.style.top = 50;
        }

        // At x (from the left; -1: from the right edge, 90 in), y from the top.
        Label Place(Label l, float x, float y, float width = 300f, bool fromRight = false)
        {
            l.style.position = Position.Absolute;
            if (fromRight) l.style.right = 90; else l.style.left = x;
            l.style.top = y;
            l.style.width = width;
            return l;
        }

        void LateUpdate()
        {
            if (root == null) return;
            var drone = Drone.Current;
            bool show = drone != null && drone.Flying;
            root.style.display = show ? DisplayStyle.Flex : DisplayStyle.None;
            if (!show) return;

            float t = Time.unscaledTime;
            rec.style.opacity = Mathf.Repeat(t, 1.2f) < 0.7f ? 1f : 0.15f;
            battery.text = $"BATTERY  {Mathf.CeilToInt(drone.Battery * 100f)}%";
            batteryFill.style.width = Length.Percent(drone.Battery * 100f);
            batteryFill.style.backgroundColor = drone.Battery < 0.2f ? UiStyle.Red : UiStyle.Ink;
            int strength = Mathf.CeilToInt(drone.Signal * 4f);
            for (int i = 0; i < 4; i++) bars[i].style.opacity = i < strength ? 1f : 0.15f;
            height.text = $"ALT  {drone.Height:0} m";
            distance.text = $"DIST  {drone.FromYou:0} m";
            mode.text = (drone.Thermal ? "THERMAL" : "") + (drone.LampOn ? (drone.Thermal ? "   " : "") + "LAMP" : "");

            string warn = drone.Signal <= 0.02f ? "SIGNAL LOST: TURN BACK" : drone.Signal < 0.4f ? "SIGNAL WEAK" : drone.Battery < 0.15f ? "LOW BATTERY" : "";
            warning.text = warn;
            warning.style.opacity = Mathf.Repeat(t, 0.9f) < 0.55f ? 1f : 0.25f;

            // Always a little static, now and then a burst; thicker as the signal goes.
            float stat = Mathf.Clamp01((0.6f - drone.Signal) / 0.6f);
            float baseline = drone.BaseStatic;
            if (Time.unscaledTime > burstUntil + 0.2f && Random.value < Time.unscaledDeltaTime * 0.35f) burstUntil = Time.unscaledTime + Random.Range(0.06f, 0.25f);
            float burst = Time.unscaledTime < burstUntil ? 0.25f : 0f;
            stat = Mathf.Max(baseline, stat * 0.75f) + burst + (drone.Battery < 0.05f ? 0.3f : 0f);
            noise.style.opacity = stat * drone.StaticAmount * Random.Range(0.75f, 1f);
            noise.style.translate = new Translate(Random.Range(-128f, 0f), Random.Range(-128f, 0f));
        }
    }
}
