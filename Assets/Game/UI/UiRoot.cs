using Game.Core;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UIElements;

namespace Game.UI
{
    /// <summary>
    /// The game's UI Toolkit panel (section 7): one UIDocument on a virtual 1600 x 900 canvas, scaled to the screen,
    /// with layers for the HUD, menus and the full-screen fade. Everything is built in code (styled by
    /// <see cref="UiStyle"/>), on the theme in Resources/UI/GameTheme.tss.
    /// </summary>
    public sealed class UiRoot : MonoBehaviour
    {
        public const float Width = 1600f, Height = 900f;

        UIDocument document;
        PanelSettings panel;

        /// <summary>The HUD (under everything else).</summary>
        public VisualElement Hud { get; private set; }
        /// <summary>Menus and screens (pause, inventory, main menu).</summary>
        public VisualElement Menus { get; private set; }
        /// <summary>The fade to black and its message, over everything.</summary>
        public VisualElement Top { get; private set; }

        public static UiRoot Get()
        {
            if (Services.TryGet<UiRoot>(out var ui)) return ui;
            if (!Services.TryGet<GameRoot>(out var root)) return null;
            var go = new GameObject("UI");
            go.transform.SetParent(root.transform, false);
            go.SetActive(false); // (the document needs its panel before it wakes)
            ui = go.AddComponent<UiRoot>();
            ui.Build();
            go.SetActive(true);
            ui.Layers();
            return ui;
        }

        void Build()
        {
            panel = ScriptableObject.CreateInstance<PanelSettings>();
            panel.name = "Game UI";
            panel.scaleMode = PanelScaleMode.ScaleWithScreenSize;
            panel.referenceResolution = new Vector2Int((int)Width, (int)Height);
            panel.screenMatchMode = PanelScreenMatchMode.MatchWidthOrHeight;
            panel.match = 1f; // (by height: wider screens get more room at the sides)
            panel.sortingOrder = 10;
            var theme = Resources.Load<ThemeStyleSheet>("UI/GameTheme");
            if (theme != null) panel.themeStyleSheet = theme;
            else Debug.LogWarning("UI theme missing (Assets/Resources/UI/GameTheme.tss): menus may not draw properly.");
            document = gameObject.AddComponent<UIDocument>();
            document.panelSettings = panel;
            Services.Register(this);

            // UI Toolkit takes its mouse and keys through an event system (with the Input System's module).
            if (EventSystem.current == null)
            {
                var es = new GameObject("EventSystem");
                es.transform.SetParent(transform, false);
                es.AddComponent<EventSystem>();
                es.AddComponent<InputSystemUIInputModule>();
            }
        }

        void Layers()
        {
            var root = document.rootVisualElement;
            root.style.flexGrow = 1f;
            root.pickingMode = PickingMode.Ignore;
            Hud = Layer("HUD");
            Menus = Layer("Menus");
            Top = Layer("Top");
        }

        VisualElement Layer(string name)
        {
            var layer = new VisualElement { name = name, pickingMode = PickingMode.Ignore };
            layer.style.position = Position.Absolute;
            layer.style.left = 0;
            layer.style.right = 0;
            layer.style.top = 0;
            layer.style.bottom = 0;
            document.rootVisualElement.Add(layer);
            return layer;
        }

        void OnDestroy()
        {
            Services.Unregister(this);
            if (panel != null) Destroy(panel);
        }
    }
}
