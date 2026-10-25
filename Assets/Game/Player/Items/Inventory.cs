using System;

namespace Game.Player
{
    /// <summary>The things you can carry (section 6.3). Snacks aren't here: they're eaten on the spot.</summary>
    public enum ItemKind { None, Flashlight, Batteries, Torch, Lighter, FlareGun, Glowsticks, Camera, KeyFob, Map, Drone, Radio, Spray }

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

        /// <summary>What it is and what F does with it (the inventory's page for it).</summary>
        public static string DescriptionOf(ItemKind kind)
        {
            switch (kind)
            {
                case ItemKind.Flashlight: return "Your light. F turns it on and off. They don't like being seen in it. It eats battery.";
                case ItemKind.Batteries: return "F puts one in the flashlight: a tenth of a full charge each.";
                case ItemKind.Torch: return "F lights it from a lighter (a little of its fuel). Burns a couple of minutes; nothing comes close to its fire.";
                case ItemKind.Lighter: return "F opens it: a small flame, and a small circle nothing will enter. Its fuel runs down while it's open.";
                case ItemKind.FlareGun: return "One flare. Fired high it bursts in the sky, floods the forest red and sends everything hunting you back. Fired low, it's wasted.";
                case ItemKind.Glowsticks: return "F cracks one and tosses it. A light that lasts for minutes, nothing more: they pay it no attention.";
                case ItemKind.Camera: return "F takes a photo. The flash drives off whatever is in the picture, for a while. A polaroid comes out.";
                case ItemKind.KeyFob: return "F presses it: the car chirps and flashes, wherever it is. Other things hear it too.";
                case ItemKind.Drone: return "F sets it down and flies it from the remote: you see through its camera (click: its lamp, T: thermal). You stand there blind and deaf while it's up, and its motors carry. F brings your eyes back. Click it to pick it up again.";
                case ItemKind.Radio: return "F sets it down, playing. Something blind will come for it from anywhere, and smash it. Click it to pick it up again.";
                case ItemKind.Map: return "A ranger's sketch of the trails between the lamps. Lit lamps are inked in as you go. It doesn't say where you are. F holds it up.";
                default: return "";
            }
        }

        public static string NameOf(ItemKind kind)
        {
            switch (kind)
            {
                case ItemKind.FlareGun: return "Flare gun";
                case ItemKind.KeyFob: return "Key fob";
                case ItemKind.Spray: return "Luminescent spray";
                case ItemKind.None: return "";
                default: return kind.ToString();
            }
        }
    }
}
