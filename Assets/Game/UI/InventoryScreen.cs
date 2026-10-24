using System.Collections.Generic;
using Game.Core;
using Game.Player;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace Game.UI
{
    /// <summary>
    /// The inventory (section 7): Tab. The world dims but keeps running (you can still walk). The eight slots as a
    /// grid down the left, to drag about (the number keys follow the new order); click one for its page on the
    /// right: what it is, what it's got left, and for the map, the map. Your polaroids underneath, to look at.
    /// Tab or Esc closes it.
    /// </summary>
    public sealed class InventoryScreen : MonoBehaviour
    {
        const float SlotSize = 104f, Gap = 10f;

        VisualElement root, grid, photos, page, ghost;
        Label pageTitle, pageText, pageDetail, ghostLabel;
        VisualElement pageImage;
        readonly List<VisualElement> slots = new List<VisualElement>();
        bool open;
        int pageSlot = -1, pagePhoto = -1;
        int dragFrom = -1;
        Vector2 dragStart;
        bool dragging;
        Hotbar hotbar;

        public bool IsOpen => open;
        /// <summary>The frame Esc closed it on.</summary>
        public static int EscUsedFrame = -1;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void AttachToGame()
        {
            if (!Services.TryGet<PlayerController>(out _)) return;
            if (Services.TryGet<GameRoot>(out var root) && root.GetComponent<InventoryScreen>() == null)
                root.gameObject.AddComponent<InventoryScreen>();
        }

        void Start()
        {
            var ui = UiRoot.Get();
            if (ui == null) return;
            Build(ui);
        }

        void OnDestroy()
        {
            if (open) Close();
        }

        void Build(UiRoot ui)
        {
            root = UiStyle.Fill(new VisualElement { name = "Inventory" });
            root.style.backgroundColor = new Color(0f, 0f, 0f, 0.6f); // (the world dims, and runs on behind)
            root.style.display = DisplayStyle.None;
            ui.Menus.Add(root);

            var left = new VisualElement();
            left.style.position = Position.Absolute;
            left.style.left = UiStyle.MenuLeft;
            left.style.top = 110;
            root.Add(left);
            var title = UiStyle.Text(left, Hud.Spaced("Inventory"), 30, UiStyle.Ink, TextAnchor.MiddleLeft, 4f);
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            title.style.marginBottom = 18;

            grid = new VisualElement();
            grid.style.flexDirection = FlexDirection.Row;
            grid.style.flexWrap = Wrap.Wrap;
            grid.style.width = (SlotSize + Gap) * 4f;
            left.Add(grid);
            for (int i = 0; i < Inventory.SlotCount; i++) grid.Add(MakeSlot(i));

            var photosTitle = UiStyle.Text(left, Hud.Spaced("Photos"), 15, UiStyle.Red, TextAnchor.MiddleLeft, 2f);
            photosTitle.style.marginTop = 26;
            photosTitle.style.marginBottom = 10;
            photos = new VisualElement();
            photos.style.flexDirection = FlexDirection.Row;
            photos.style.flexWrap = Wrap.Wrap;
            photos.style.width = (SlotSize + Gap) * 4f;
            left.Add(photos);

            // The page: what the chosen thing is.
            page = new VisualElement();
            page.style.position = Position.Absolute;
            page.style.left = 760;
            page.style.top = 110;
            page.style.width = 700;
            root.Add(page);
            pageTitle = UiStyle.Text(page, "", 30, UiStyle.Ink, TextAnchor.MiddleLeft, 3f);
            pageTitle.style.unityFontStyleAndWeight = FontStyle.Bold;
            pageText = UiStyle.Text(page, "", 17, UiStyle.Ink, TextAnchor.UpperLeft);
            pageText.style.whiteSpace = WhiteSpace.Normal;
            pageText.style.marginTop = 12;
            pageDetail = UiStyle.Text(page, "", 15, UiStyle.Warm, TextAnchor.UpperLeft);
            pageDetail.style.marginTop = 10;
            pageImage = new VisualElement();
            pageImage.style.marginTop = 22;
            page.Add(pageImage);

            var hint = UiStyle.Text(root, "Drag to rearrange   ·   click for its page   ·   Tab to close", 14, UiStyle.Muted, TextAnchor.MiddleLeft);
            hint.style.position = Position.Absolute;
            hint.style.left = UiStyle.MenuLeft;
            hint.style.bottom = 60;

            ghost = UiStyle.Box(root, new Color(0.1f, 0.1f, 0.11f, 0.9f));
            ghost.style.position = Position.Absolute;
            ghost.style.width = SlotSize;
            ghost.style.height = SlotSize * 0.45f;
            ghost.pickingMode = PickingMode.Ignore;
            ghost.style.display = DisplayStyle.None;
            UiStyle.Border(ghost, UiStyle.Red);
            ghostLabel = UiStyle.Fill(UiStyle.Text(ghost, "", 14, UiStyle.Ink, TextAnchor.MiddleCenter));

            root.RegisterCallback<PointerMoveEvent>(OnPointerMove);
            root.RegisterCallback<PointerUpEvent>(OnPointerUp);
        }

        VisualElement MakeSlot(int index)
        {
            var slot = new VisualElement();
            slot.style.width = SlotSize;
            slot.style.height = SlotSize;
            slot.style.marginRight = Gap;
            slot.style.marginBottom = Gap;
            slot.style.backgroundColor = UiStyle.Panel;
            UiStyle.Rounded(slot, 3f);
            var number = UiStyle.Text(slot, (index + 1).ToString(), 12, UiStyle.Muted, TextAnchor.UpperLeft);
            number.style.position = Position.Absolute;
            number.style.left = 7;
            number.style.top = 5;
            var name = UiStyle.Text(slot, "", 15, UiStyle.Ink, TextAnchor.MiddleCenter);
            name.name = "name";
            name.style.whiteSpace = WhiteSpace.Normal;
            name.style.position = Position.Absolute;
            name.style.left = name.style.right = 6;
            name.style.top = 26;
            name.style.bottom = 30;
            var detail = UiStyle.Text(slot, "", 12, UiStyle.Warm, TextAnchor.MiddleCenter);
            detail.name = "detail";
            detail.style.position = Position.Absolute;
            detail.style.left = detail.style.right = 4;
            detail.style.bottom = 8;
            slot.RegisterCallback<PointerDownEvent>(e =>
            {
                if (e.button != 0 || hotbar == null) return;
                dragFrom = index;
                dragStart = e.position;
                dragging = false;
            });
            slots.Add(slot);
            return slot;
        }

        void OnPointerMove(PointerMoveEvent e)
        {
            if (dragFrom < 0) return;
            if (!dragging && !hotbar.Items[dragFrom].Empty && ((Vector2)e.position - dragStart).sqrMagnitude > 36f)
            {
                dragging = true;
                ghostLabel.text = Inventory.NameOf(hotbar.Items[dragFrom].Kind);
                ghost.style.display = DisplayStyle.Flex;
            }
            if (dragging)
            {
                ghost.style.left = e.position.x - SlotSize * 0.5f;
                ghost.style.top = e.position.y - SlotSize * 0.22f;
            }
        }

        void OnPointerUp(PointerUpEvent e)
        {
            if (dragFrom < 0) return;
            int from = dragFrom;
            dragFrom = -1;
            ghost.style.display = DisplayStyle.None;
            if (!dragging)
            {
                ShowSlot(from); // (a click: its page)
                return;
            }
            dragging = false;
            for (int i = 0; i < slots.Count; i++)
            {
                if (i == from || !slots[i].worldBound.Contains(e.position)) continue;
                hotbar.Items.Swap(from, i);
                if (pageSlot == from) pageSlot = i;
                else if (pageSlot == i) pageSlot = from;
                break;
            }
            Refresh();
        }

        void Update()
        {
            if (root == null) return;
            var keyboard = Keyboard.current;
            bool canOpen = GameFlow.IsPlaying && !(Services.TryGet<PauseState>(out var pause) && pause.Paused) && !PlayerPlace.InDarkRoom
                           && Services.TryGet<PlayerController>(out var player) && !player.Held && player.Hotbar != null;
            if (open && !canOpen) Close();
            if (keyboard == null) return;
            if (keyboard.tabKey.wasPressedThisFrame)
            {
                if (open) Close();
                else if (canOpen) Open();
            }
            else if (open && keyboard.escapeKey.wasPressedThisFrame)
            {
                Close();
                EscUsedFrame = Time.frameCount; // (so the same press doesn't open the pause menu)
            }
            if (open) Refresh();
        }

        void Open()
        {
            hotbar = Services.Get<PlayerController>().Hotbar;
            open = true;
            root.style.display = DisplayStyle.Flex;
            if (Services.TryGet<UiState>(out var ui)) ui.PanelOpened(takesKeys: false); // (you can still walk)
            UnityEngine.Cursor.lockState = CursorLockMode.None;
            UnityEngine.Cursor.visible = true;
            if (pageSlot < 0 && pagePhoto < 0) ShowSlot(hotbar.Items.Selected);
            RebuildPhotos();
            Refresh();
        }

        void Close()
        {
            open = false;
            dragFrom = -1;
            dragging = false;
            if (root != null)
            {
                root.style.display = DisplayStyle.None;
                ghost.style.display = DisplayStyle.None;
            }
            if (Services.TryGet<UiState>(out var ui)) ui.PanelClosed(takesKeys: false);
            if (!(Services.TryGet<PauseState>(out var pause) && pause.Paused)) PlayerController.SetCursorCaptured(true);
        }

        // The slots as they are now (charges run down while it's open).
        void Refresh()
        {
            var items = hotbar.Items;
            var s = hotbar.Settings;
            for (int i = 0; i < slots.Count; i++)
            {
                var stack = items[i];
                var slot = slots[i];
                slot.Q<Label>("name").text = stack.Empty ? "" : Inventory.NameOf(stack.Kind);
                slot.Q<Label>("detail").text = stack.Empty ? "" : Detail(stack, s);
                Color edge = i == pageSlot && pagePhoto < 0 ? UiStyle.Warm : i == items.Selected ? UiStyle.Red : new Color(1f, 1f, 1f, 0.08f);
                UiStyle.Border(slot, edge, i == pageSlot || i == items.Selected ? 2f : 1f);
            }
            if (pageSlot >= 0 && pagePhoto < 0)
            {
                var stack = items[pageSlot];
                pageDetail.text = stack.Empty ? "" : Detail(stack, s);
            }
        }

        static string Detail(ItemStack stack, ItemSettings s)
        {
            switch (stack.Kind)
            {
                case ItemKind.Batteries:
                case ItemKind.Glowsticks: return $"x{stack.Count}";
                case ItemKind.Torch: return stack.Lit ? $"burning, {Mathf.CeilToInt(stack.Charge * s.torchBurnTime)} s" : $"{Mathf.CeilToInt(stack.Charge * 100f)}% left";
                case ItemKind.Lighter: return $"fuel {Mathf.CeilToInt(stack.Charge * 100f)}%";
                case ItemKind.Camera:
                    int shots = Mathf.FloorToInt(stack.Charge);
                    return shots == 1 ? "1 shot" : $"{shots} shots";
                case ItemKind.Flashlight:
                    return Services.TryGet<PlayerController>(out var p) && p.Flashlight != null ? $"battery {p.Flashlight.Battery.Charge * 100f:0}%" : "";
                default: return "";
            }
        }

        void ShowSlot(int index)
        {
            pageSlot = index;
            pagePhoto = -1;
            pageImage.Clear();
            var stack = hotbar != null ? hotbar.Items[index] : default;
            if (stack.Empty)
            {
                pageTitle.text = Hud.Spaced("Empty");
                pageText.text = "Nothing in this slot.";
                pageDetail.text = "";
                return;
            }
            pageTitle.text = Hud.Spaced(Inventory.NameOf(stack.Kind));
            pageText.text = Inventory.DescriptionOf(stack.Kind);
            if (stack.Kind == ItemKind.Map && Services.TryGet<MapView>(out var map) && map.Paper != null)
            {
                var paper = new VisualElement();
                paper.style.width = 480;
                paper.style.height = 480;
                paper.style.backgroundImage = new StyleBackground(map.Paper);
                paper.style.unityBackgroundImageTintColor = new Color(0.85f, 0.83f, 0.78f, 1f);
                paper.style.rotate = new Rotate(new Angle(-1.5f, AngleUnit.Degree));
                pageImage.Add(paper);
            }
            Refresh();
        }

        void ShowPhoto(int index)
        {
            if (!Services.TryGet<Polaroids>(out var polaroids) || index >= polaroids.Photos.Count) return;
            var photo = polaroids.Photos[index];
            pagePhoto = index;
            pageSlot = -1;
            pageImage.Clear();
            pageTitle.text = Hud.Spaced($"Photo {index + 1}");
            pageText.text = photo.LampsLit == 0 ? "Taken before any lamp was lit." : photo.LampsLit == 1 ? "Taken with one lamp lit." : $"Taken with {photo.LampsLit} lamps lit.";
            int minutes = Mathf.FloorToInt(photo.TakenAt / 60f);
            pageDetail.text = $"{minutes}:{Mathf.FloorToInt(photo.TakenAt % 60f):00} into the night";
            var card = PolaroidPopup.MakeCard(400f, out var picture, out var milk);
            picture.style.backgroundImage = new StyleBackground(Background.FromRenderTexture(photo.Picture));
            milk.style.opacity = 0f;
            card.style.alignSelf = Align.FlexStart;
            card.style.rotate = new Rotate(new Angle(2f, AngleUnit.Degree));
            pageImage.Add(card);
            Refresh();
        }

        void RebuildPhotos()
        {
            photos.Clear();
            if (!Services.TryGet<Polaroids>(out var polaroids) || polaroids.Photos.Count == 0)
            {
                UiStyle.Text(photos, "None yet. The camera makes them.", 14, UiStyle.Muted, TextAnchor.MiddleLeft);
                return;
            }
            for (int i = 0; i < polaroids.Photos.Count; i++)
            {
                int index = i;
                var card = PolaroidPopup.MakeCard(78f, out var picture, out var milk);
                picture.style.backgroundImage = new StyleBackground(Background.FromRenderTexture(polaroids.Photos[i].Picture));
                milk.style.opacity = 0f;
                card.pickingMode = PickingMode.Position;
                card.style.marginRight = 14;
                card.style.marginBottom = 10;
                card.style.rotate = new Rotate(new Angle((i % 2 == 0 ? -1f : 1f) * (2f + i), AngleUnit.Degree));
                card.RegisterCallback<PointerUpEvent>(_ => ShowPhoto(index));
                photos.Add(card);
            }
        }
    }
}
