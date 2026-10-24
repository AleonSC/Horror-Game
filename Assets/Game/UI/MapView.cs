using Game.Core;
using Game.Player;
using Game.World;
using Game.World.Build;
using UnityEngine;
using UnityEngine.UIElements;

namespace Game.UI
{
    /// <summary>
    /// The map, held up (F with it in hand): the sketch fills the middle of the view, a little crooked, and you can
    /// still walk and look round with it up. It goes down when you press F again or hold something else. The
    /// sketch is drawn the first time it's needed and inked in as lamps are lit (the inventory shows it too).
    /// </summary>
    public sealed class MapView : MonoBehaviour
    {
        VisualElement sheet;
        MapSketch sketch;
        Game.World.Generation.WorldPlan sketchedFor;
        bool raised;
        float shown;
        System.IDisposable toggleSub, lampSub;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void AttachToGame()
        {
            if (!Services.TryGet<PlayerController>(out _) || !Services.TryGet<WorldBuilder>(out _)) return;
            if (Services.TryGet<GameRoot>(out var root) && root.GetComponent<MapView>() == null)
                root.gameObject.AddComponent<MapView>();
        }

        void Awake() => Services.Register(this);

        void Start()
        {
            if (Services.TryGet<EventBus>(out var events))
            {
                toggleSub = events.Subscribe<MapToggled>(_ => raised = !raised && Paper != null);
                lampSub = events.Subscribe<LampLit>(e => sketch?.InkLamp(e.LampIndex));
            }
            var ui = UiRoot.Get();
            if (ui == null) return;
            sheet = new VisualElement { name = "Map", pickingMode = PickingMode.Ignore };
            sheet.style.position = Position.Absolute;
            sheet.style.width = 620;
            sheet.style.height = 620;
            sheet.style.left = Length.Percent(50);
            sheet.style.top = Length.Percent(50);
            sheet.style.marginLeft = -310;
            sheet.style.marginTop = -330;
            // (Dimmed: it's paper in the dark, not a lit screen.)
            sheet.style.unityBackgroundImageTintColor = new Color(0.72f, 0.7f, 0.66f, 1f);
            sheet.style.display = DisplayStyle.None;
            ui.Hud.Add(sheet);
        }

        void OnDestroy()
        {
            toggleSub?.Dispose();
            lampSub?.Dispose();
            if (sketch != null) Destroy(sketch.Texture);
            Services.Unregister(this);
        }

        /// <summary>The sketch (drawn now if it hasn't been), or null if there's no world.</summary>
        public Texture2D Paper
        {
            get
            {
                var plan = Services.TryGet<WorldBuilder>(out var w) ? w.Plan : null;
                if (plan == null) return null;
                if (sketch == null || sketchedFor != plan)
                {
                    if (sketch != null) Destroy(sketch.Texture);
                    sketch = new MapSketch(plan);
                    sketchedFor = plan;
                    foreach (var lamp in FindObjectsByType<Lamp>())
                        if (lamp.Lit) sketch.InkLamp(lamp.index);
                }
                return sketch.Texture;
            }
        }

        void LateUpdate()
        {
            if (sheet == null) return;
            // Down as soon as it's not in your hand (or you're taken, or somewhere else).
            bool holding = Services.TryGet<PlayerController>(out var player) && player.Hotbar != null && player.Hotbar.Items.HeldKind == ItemKind.Map;
            if (!holding || player.Held || player.Piloting || PlayerPlace.InDarkRoom) raised = false;

            shown = Mathf.MoveTowards(shown, raised ? 1f : 0f, Time.deltaTime * 3.5f);
            if (shown <= 0f)
            {
                if (sheet.style.display != DisplayStyle.None) sheet.style.display = DisplayStyle.None;
                return;
            }
            if (sheet.style.display != DisplayStyle.Flex)
            {
                sheet.style.backgroundImage = new StyleBackground(Paper);
                sheet.style.display = DisplayStyle.Flex;
            }
            // Brought up from below, a little crooked, swaying with your steps.
            float e = shown * shown * (3f - 2f * shown);
            float sway = player != null && player.Moving ? Mathf.Sin(Time.time * 5f) * 6f : 0f;
            sheet.style.translate = new Translate(sway, (1f - e) * 700f + Mathf.Abs(sway) * 0.5f);
            sheet.style.rotate = new Rotate(new Angle(-2.5f + sway * 0.1f, AngleUnit.Degree));
            sheet.style.opacity = Mathf.Clamp01(e * 1.5f);
        }
    }
}
