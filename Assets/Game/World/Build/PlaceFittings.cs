using System.Collections.Generic;
using Game.Core;
using Game.Player;
using Game.World.Generation;
using UnityEngine;

namespace Game.World.Build
{
    /// <summary>
    /// Fits out a place, built-in or your own prefab: the loot from the plan (on the table, in the tent, in the
    /// backpack, glove box and trunk), its shelter, its door, and the car's fob response. Your prefab can say where
    /// things go with children named <c>Table</c>, <c>Tent</c>, <c>Backpack</c>, <c>GloveBox</c>, <c>Trunk</c>
    /// (a spot), <c>Deck</c> (the watchtower's, where the map lies), <c>Inside</c> (a Box Collider: the shelter's inside) and <c>Door</c> (swings about its pivot;
    /// needs a collider). Anything not named uses the built-in layout's spot.
    /// </summary>
    public static class PlaceFittings
    {
        // The built-in layout's spots (local to the place).
        public static readonly Vector3 TableSpot = new Vector3(0.9f, 0f, -1.4f);
        public static readonly Vector3 CabinTableSpot = new Vector3(2.2f, 0f, -1.8f);
        public static readonly Vector3 BunkerCrateSpot = new Vector3(0f, 0f, -2.65f); // (the crate at the back of its room)
        public static readonly Vector3 TentSpot = new Vector3(-2f, 0f, 0f);
        public const float TentYaw = 90f; // (opening toward the fire)
        public static readonly Vector3 BackpackSpot = new Vector3(0.3f, 0f, 1.5f);
        public static readonly Vector3 DeckSpot = new Vector3(1f, 12.13f, 1f); // (the watchtower's floor, by the button)
        static readonly Vector3 GloveBoxSpot = new Vector3(0.42f, 0.95f, 0.92f);    // (behind its little door in the dash)
        static readonly Vector3 TrunkSpot = new Vector3(0f, 0.8f, -1.85f);         // (under the trunk lid)
        static readonly Vector3 BackSeatSpot = new Vector3(-0.4f, 0.65f, -0.85f);  // (on the back seat: open the rear left door)

        public static void Fit(Transform root, StructureKind kind, IList<Loot> loot)
        {
            if (kind == StructureKind.Car && root.GetComponent<Car>() == null) root.gameObject.AddComponent<Car>();
            if (kind == StructureKind.Watchtower) FitWatchtower(root);
            FitYourDoorAndInside(root, kind);
            Container backSeat = null;
            if (loot != null)
                foreach (var l in loot)
                    if (l.Place == LootPlace.BackSeat) { backSeat = MakeContainer(root, LootPlace.BackSeat); break; }
            // The drone: at whichever of these you reach first.
            if (kind == StructureKind.Cabin) DroneSpot.Add(root, root.InverseTransformPoint(Spot(root, "Table", CabinTableSpot)) + new Vector3(0.3f, 0.78f, 0f));
            if (kind == StructureKind.Watchtower) DroneSpot.Add(root, root.InverseTransformPoint(Spot(root, "Deck", DeckSpot)) + new Vector3(-1.6f, 0.01f, -0.6f));
            if (kind == StructureKind.Car && backSeat != null) DroneSpot.Add(root, backSeat.transform.localPosition, backSeat);
            if (loot == null || loot.Count == 0) return;

            var onTable = new List<Loot>();
            var inTent = new List<Loot>();
            var onDeck = new List<Loot>();
            var containers = new Dictionary<LootPlace, Container>();
            foreach (var l in loot)
            {
                switch (l.Place)
                {
                    case LootPlace.Table: onTable.Add(l); break;
                    case LootPlace.Tent: inTent.Add(l); break;
                    case LootPlace.Deck: onDeck.Add(l); break;
                    default:
                        if (!containers.TryGetValue(l.Place, out var c)) containers[l.Place] = c = l.Place == LootPlace.BackSeat && backSeat != null ? backSeat : MakeContainer(root, l.Place);
                        if (TryItem(l.Kind, out var item)) c.Put(item, l.Count);
                        break;
                }
            }

            // Laid out in a row along the table top, or along the tent floor.
            Lay(root, onTable, Spot(root, "Table", kind == StructureKind.Cabin ? CabinTableSpot : kind == StructureKind.Bunker ? BunkerCrateSpot : TableSpot) + Vector3.up * 0.77f, root.right, 0.22f);
            Vector3 tentFloor = Spot(root, "Tent", TentSpot) + Vector3.up * 0.06f;
            Lay(root, inTent, tentFloor, root.right, 0.25f); // (the tent lies across the campsite)
            Lay(root, onDeck, Spot(root, "Deck", DeckSpot) + Vector3.up * 0.01f, root.right, 0.3f);
        }

        static void Lay(Transform root, List<Loot> items, Vector3 centre, Vector3 along, float gap)
        {
            for (int i = 0; i < items.Count; i++)
            {
                var l = items[i];
                Vector3 at = centre + along * ((i - (items.Count - 1) * 0.5f) * gap);
                float yaw = root.eulerAngles.y + 20f * i;
                if (l.Kind == LootKind.Snack) Pickup.Create(ItemKind.None, 1, at, yaw, root, snack: true);
                else if (TryItem(l.Kind, out var item)) Pickup.Create(item, l.Count, at, yaw, root);
            }
        }

        static Container MakeContainer(Transform root, LootPlace place)
        {
            string label = place == LootPlace.GloveBox ? "Search the glove box" : place == LootPlace.Trunk ? "Search the trunk" : place == LootPlace.BackSeat ? "Search the back seat" : "Search backpack";
            Vector3 fallback = place == LootPlace.GloveBox ? GloveBoxSpot : place == LootPlace.Trunk ? TrunkSpot : place == LootPlace.BackSeat ? BackSeatSpot : BackpackSpot + Vector3.up * 0.3f;
            Vector3 size = place == LootPlace.GloveBox ? new Vector3(0.34f, 0.14f, 0.2f) : place == LootPlace.Trunk ? new Vector3(1.3f, 0.3f, 0.8f) : place == LootPlace.BackSeat ? new Vector3(0.5f, 0.3f, 0.45f) : new Vector3(0.42f, 0.56f, 0.3f);
            var named = Find(root, place.ToString());
            Vector3 local = named != null ? root.InverseTransformPoint(named.position) : fallback;
            return Container.Create(label, local, size, root);
        }

        // Your prefab's named spot (in world space), or the built-in one.
        static Vector3 Spot(Transform root, string name, Vector3 fallback)
        {
            var named = Find(root, name);
            return named != null ? named.position : root.TransformPoint(fallback);
        }

        static void FitYourDoorAndInside(Transform root, StructureKind kind)
        {
            if (kind != StructureKind.Shack && kind != StructureKind.Cabin && kind != StructureKind.Campsite && kind != StructureKind.Bunker) return;
            var door = Find(root, "Door");
            if (door != null && door.GetComponent<Door>() == null && door.GetComponentInChildren<Collider>() != null)
                door.gameObject.AddComponent<Door>();

            if (root.GetComponentInChildren<Shelter>() != null) return;
            var inside = Find(root, "Inside");
            var box = inside != null ? inside.GetComponent<BoxCollider>() : null;
            if (box != null)
            {
                var bounds = new Bounds(root.InverseTransformPoint(inside.TransformPoint(box.center)), Vector3.Scale(box.size, inside.lossyScale));
                Object.Destroy(box); // (only a marker)
                AddShelter(root, root.name, bounds);
            }
            else if (kind != StructureKind.Campsite && kind != StructureKind.Bunker)
                Debug.LogWarning($"Your {root.name} prefab has no child named 'Inside' with a Box Collider, so it isn't a shelter (docs/custom-assets.md).");
        }

        // The ladder (your prefab: a child named "Ladder" with a Box Collider covering where you climb), the button
        // ("Button") and the floodlights (children named "Floodlight").
        static void FitWatchtower(Transform root)
        {
            var ladder = root.gameObject.AddComponent<Game.Player.Ladder>();
            var marked = Find(root, "Ladder");
            var box = marked != null ? marked.GetComponent<BoxCollider>() : null;
            if (box != null)
            {
                ladder.box = new Bounds(root.InverseTransformPoint(marked.TransformPoint(box.center)), Vector3.Scale(box.size, marked.lossyScale));
                Object.Destroy(box);
            }
            else ladder.box = new Bounds(new Vector3(-2.1f, 6.7f, 0f), new Vector3(1f, 13.4f, 1.2f));

            var tower = root.gameObject.AddComponent<Watchtower>();
            var button = Find(root, "Button");
            if (button != null) tower.buttonSpot = root.InverseTransformPoint(button.position);
            var spots = new List<Vector3>();
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
                if (t != root && t.name == "Floodlight") spots.Add(root.InverseTransformPoint(t.position));
            if (spots.Count > 0) tower.lightSpots = spots.ToArray();
        }

        public static Shelter AddShelter(Transform on, string name, Bounds inside)
        {
            var shelter = on.gameObject.AddComponent<Shelter>();
            shelter.shelterName = name;
            shelter.inside = inside;
            return shelter;
        }

        static Transform Find(Transform root, string name)
        {
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
                if (t != root && t.name == name) return t;
            return null;
        }

        public static bool TryItem(LootKind kind, out ItemKind item)
        {
            switch (kind)
            {
                case LootKind.Battery: item = ItemKind.Batteries; return true;
                case LootKind.Torch: item = ItemKind.Torch; return true;
                case LootKind.Lighter: item = ItemKind.Lighter; return true;
                case LootKind.KeyFob: item = ItemKind.KeyFob; return true;
                case LootKind.FlareGun: item = ItemKind.FlareGun; return true;
                case LootKind.Glowsticks: item = ItemKind.Glowsticks; return true;
                case LootKind.Camera: item = ItemKind.Camera; return true;
                case LootKind.Map: item = ItemKind.Map; return true;
                case LootKind.Radio: item = ItemKind.Radio; return true;
                default: item = ItemKind.None; return false;
            }
        }
    }
}
