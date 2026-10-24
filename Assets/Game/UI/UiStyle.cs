using UnityEngine;
using UnityEngine.UIElements;

namespace Game.UI
{
    /// <summary>The game's UI look in one place (colours, sizes, the spaced capitals), and helpers to build with it.</summary>
    public static class UiStyle
    {
        public static readonly Color Ink = new Color(0.86f, 0.84f, 0.8f, 0.92f);
        public static readonly Color Muted = new Color(0.6f, 0.58f, 0.55f, 0.9f);
        public static readonly Color Band = new Color(0.02f, 0.02f, 0.025f, 0.55f);
        public static readonly Color Panel = new Color(0.03f, 0.03f, 0.035f, 0.82f);
        public static readonly Color Red = new Color(0.85f, 0.08f, 0.05f, 1f);
        public static readonly Color Warm = new Color(1f, 0.78f, 0.45f, 1f);
        public static readonly Color Clear = new Color(0f, 0f, 0f, 0f);

        public static VisualElement Box(VisualElement parent, Color background, string name = null)
        {
            var e = new VisualElement { name = name, pickingMode = PickingMode.Ignore };
            e.style.backgroundColor = background;
            parent?.Add(e);
            return e;
        }

        public static Label Text(VisualElement parent, string text, int size, Color colour, TextAnchor align = TextAnchor.MiddleCenter, float spacing = 0f)
        {
            var l = new Label(text) { pickingMode = PickingMode.Ignore };
            l.style.fontSize = size;
            l.style.color = colour;
            l.style.unityTextAlign = align;
            l.style.letterSpacing = spacing;
            l.style.marginLeft = l.style.marginRight = l.style.marginTop = l.style.marginBottom = 0;
            l.style.paddingLeft = l.style.paddingRight = l.style.paddingTop = l.style.paddingBottom = 0;
            parent?.Add(l);
            return l;
        }

        /// <summary>Pin an element at an absolute spot on the 1600 x 900 canvas.</summary>
        public static T At<T>(T e, float x, float y, float w, float h) where T : VisualElement
        {
            e.style.position = Position.Absolute;
            e.style.left = x;
            e.style.top = y;
            e.style.width = w;
            e.style.height = h;
            return e;
        }

        /// <summary>Stretch over the whole of its parent.</summary>
        public static T Fill<T>(T e) where T : VisualElement
        {
            e.style.position = Position.Absolute;
            e.style.left = 0;
            e.style.right = 0;
            e.style.top = 0;
            e.style.bottom = 0;
            return e;
        }

        public static void Border(VisualElement e, Color c, float width = 1f)
        {
            e.style.borderLeftColor = e.style.borderRightColor = e.style.borderTopColor = e.style.borderBottomColor = c;
            e.style.borderLeftWidth = e.style.borderRightWidth = e.style.borderTopWidth = e.style.borderBottomWidth = width;
        }

        public static void Rounded(VisualElement e, float r)
        {
            e.style.borderTopLeftRadius = e.style.borderTopRightRadius = e.style.borderBottomLeftRadius = e.style.borderBottomRightRadius = r;
        }

        /// <summary>A menu button: spaced capitals on a dark band, red underline and brighter on hover.</summary>
        /// <summary>How far the menus sit in from the left edge (in the 1600x900 layout).</summary>
        public const float MenuLeft = 150f;

        public static Button MenuButton(VisualElement parent, string text, System.Action clicked, float width = 300f, float height = 46f)
        {
            var b = new Button(clicked) { text = Hud.Spaced(text) };
            b.style.width = width;
            b.style.height = height;
            b.style.fontSize = 18;
            b.style.color = Ink;
            b.style.backgroundColor = new Color(0.05f, 0.05f, 0.06f, 0.7f);
            b.style.unityTextAlign = TextAnchor.MiddleLeft;
            b.style.paddingLeft = 20;
            b.style.marginTop = b.style.marginBottom = 7;
            b.style.marginLeft = b.style.marginRight = 0;
            Border(b, Clear, 0f);
            b.style.borderBottomWidth = 2f;
            b.style.borderBottomColor = Clear;
            b.style.borderLeftWidth = 3f;
            b.style.borderLeftColor = new Color(1f, 1f, 1f, 0.08f);
            Rounded(b, 2f);
            // A small diamond at its right end, lit on hover.
            var mark = Box(b, new Color(1f, 1f, 1f, 0.12f));
            mark.style.position = Position.Absolute;
            mark.style.right = 18;
            mark.style.top = height * 0.5f - 3.5f;
            mark.style.width = mark.style.height = 7;
            mark.style.rotate = new Rotate(new Angle(45f, AngleUnit.Degree));
            b.RegisterCallback<MouseEnterEvent>(_ =>
            {
                b.style.backgroundColor = new Color(0.07f, 0.07f, 0.08f, 0.95f);
                b.style.borderBottomColor = Red;
                b.style.borderLeftColor = Red;
                b.style.color = Color.white;
                b.style.paddingLeft = 26;
                mark.style.backgroundColor = Red;
            });
            b.RegisterCallback<MouseLeaveEvent>(_ =>
            {
                b.style.backgroundColor = new Color(0.05f, 0.05f, 0.06f, 0.7f);
                b.style.borderBottomColor = Clear;
                b.style.borderLeftColor = new Color(1f, 1f, 1f, 0.08f);
                b.style.color = Ink;
                b.style.paddingLeft = 20;
                mark.style.backgroundColor = new Color(1f, 1f, 1f, 0.12f);
            });
            parent?.Add(b);
            return b;
        }
    }
}
