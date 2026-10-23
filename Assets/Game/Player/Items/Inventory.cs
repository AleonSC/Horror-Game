using System;

namespace Game.Player
{
    /// <summary>The things you can carry (section 6.3). Snacks aren't here: they're eaten on the spot.</summary>
    public enum ItemKind { None, Flashlight, Batteries, Torch, Lighter, FlareGun, Glowsticks, Camera, KeyFob }

    /// <summary>One hotbar slot: what's in it, how many, and its own charge (fuel, burn time left, shots).</summary>
    public struct ItemStack
    {
        public ItemKind Kind;
        public int Count;
        /// <summary>The item's own amount left (lighter fuel 0-1, torch burn 0-1, camera shots), set when picked up.</summary>
        public float Charge;
        /// <summary>A lit torch, an open lighter.</summary>
        public bool Lit;

        public bool Empty => Kind == ItemKind.None || Count <= 0;
    }

    /// <summary>
    /// The hotbar (section 6.3): eight slots, starting with just the flashlight. Batteries and glowsticks stack in
    /// one slot; everything else takes a slot each. Plain C#, so it can be tested.
    /// </summary>
    public sealed class Inventory
    {
        public const int SlotCount = 8;

        readonly ItemStack[] slots = new ItemStack[SlotCount];

        public int Selected { get; private set; }
        public ItemStack Held => slots[Selected];
        public ItemKind HeldKind => slots[Selected].Empty ? ItemKind.None : slots[Selected].Kind;

        /// <summary>Raised when anything changes (the HUD shows the hotbar for a moment).</summary>
        public event Action Changed;

        public Inventory() => Reset();

        public ItemStack this[int slot] => slots[slot];

        /// <summary>Back to the start of a run: just the flashlight, held.</summary>
        public void Reset()
        {
            for (int i = 0; i < SlotCount; i++) slots[i] = default;
            slots[0] = new ItemStack { Kind = ItemKind.Flashlight, Count = 1 };
            Selected = 0;
            Changed?.Invoke();
        }

        public static bool Stacks(ItemKind kind) => kind == ItemKind.Batteries || kind == ItemKind.Glowsticks;

        /// <summary>Add some. Returns false (and adds nothing) if there's no room.</summary>
        public bool Add(ItemKind kind, int count = 1, float charge = 1f)
        {
            if (kind == ItemKind.None || count <= 0) return false;
            if (Stacks(kind))
            {
                int have = Find(kind);
                if (have >= 0)
                {
                    slots[have].Count += count;
                    Changed?.Invoke();
                    return true;
                }
                int free = FirstFree();
                if (free < 0) return false;
                slots[free] = new ItemStack { Kind = kind, Count = count, Charge = charge };
                Changed?.Invoke();
                return true;
            }
            // Single items take a slot each (a second lighter takes a second slot).
            bool any = false;
            for (int i = 0; i < count; i++)
            {
                int free = FirstFree();
                if (free < 0) break;
                slots[free] = new ItemStack { Kind = kind, Count = 1, Charge = charge };
                any = true;
            }
            if (any) Changed?.Invoke();
            return any;
        }

        public bool HasRoomFor(ItemKind kind) => (Stacks(kind) && Find(kind) >= 0) || FirstFree() >= 0;

        /// <summary>The first slot holding this kind, or -1.</summary>
        public int Find(ItemKind kind)
        {
            for (int i = 0; i < SlotCount; i++) if (!slots[i].Empty && slots[i].Kind == kind) return i;
            return -1;
        }

        public int CountOf(ItemKind kind)
        {
            int n = 0;
            for (int i = 0; i < SlotCount; i++) if (!slots[i].Empty && slots[i].Kind == kind) n += slots[i].Count;
            return n;
        }

        /// <summary>Use up some of a slot's stack; an emptied slot is cleared.</summary>
        public void Take(int slot, int count = 1)
        {
            if (slots[slot].Empty) return;
            slots[slot].Count -= count;
            if (slots[slot].Count <= 0) slots[slot] = default;
            Changed?.Invoke();
        }

        /// <summary>Clear a slot (a burnt-out torch, a fired flare gun).</summary>
        public void Clear(int slot)
        {
            slots[slot] = default;
            Changed?.Invoke();
        }

        public void SetCharge(int slot, float charge) => slots[slot].Charge = charge;

        public void SetLit(int slot, bool lit)
        {
            if (slots[slot].Lit == lit) return;
            slots[slot].Lit = lit;
            Changed?.Invoke();
        }

        public void Select(int slot)
        {
            if (slot < 0 || slot >= SlotCount || slot == Selected) return;
            Selected = slot;
            Changed?.Invoke();
        }

        /// <summary>The wheel: the next (or previous) slot that holds something.</summary>
        public void Cycle(int direction)
        {
            if (direction == 0) return;
            int step = direction > 0 ? 1 : -1;
            for (int i = 1; i <= SlotCount; i++)
            {
                int s = ((Selected + step * i) % SlotCount + SlotCount) % SlotCount;
                if (!slots[s].Empty)
                {
                    Select(s);
                    return;
                }
            }
        }

        /// <summary>Swap two slots (the inventory screen's drag, later).</summary>
        public void Swap(int a, int b)
        {
            (slots[a], slots[b]) = (slots[b], slots[a]);
            if (Selected == a) Selected = b;
            else if (Selected == b) Selected = a;
            Changed?.Invoke();
        }

        int FirstFree()
        {
            for (int i = 0; i < SlotCount; i++) if (slots[i].Empty) return i;
            return -1;
        }

        public static string NameOf(ItemKind kind)
        {
            switch (kind)
            {
                case ItemKind.FlareGun: return "Flare gun";
                case ItemKind.KeyFob: return "Key fob";
                case ItemKind.None: return "";
                default: return kind.ToString();
            }
        }
    }
}
