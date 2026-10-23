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
    /// (a spot), <c>Inside</c> (a Box Collider: the shelter's inside) and <c>Door</c> (swings about its pivot;
    /// needs a collider). Anything not named uses the built-in layout's spot.
    /// </summary>
    public static class PlaceFittings
    {
        // The built-in layout's spots (local to the place).
        public static readonly Vector3 TableSpot = new Vector3(0.9f, 0f, -1.4f);
        public static readonly Vector3 CabinTableSpot = new Vector3(2.2f, 0f, -1.8f);
        public static readonly Vector3 TentSpot = new Vector3(-2f, 0f, 0f);
        public const float TentYaw = 90f; // (opening toward the fire)
        public static readonly Vector3 BackpackSpot = new Vector3(0.3f, 0f, 1.5f);
        static readonly Vector3 GloveBoxSpot = new Vector3(0.95f, 1.05f, 0.45f);
        static readonly Vector3 TrunkSpot = new Vector3(0f, 0.85f, -2.25f);

        public static void Fit(Transform root, StructureKind kind, IList<Loot> loot)
        {
            if (kind == StructureKind.Car && root.GetComponent<Car>() == null) root.gameObject.AddComponent<Car>();
            FitYourDoorAndInside(root, kind);
            if (loot == null || loot.Count == 0) return;

            var onTable = new List<Loot>();
            var inTent = new List<Loot>();
            var containers = new Dictionary<LootPlace, Container>();
            foreach (var l in loot)
            {
                switch (l.Place)
                {
                    case LootPlace.Table: onTable.Add(l); break;
                    case LootPlace.Tent: inTent.Add(l); break;
                    default:
                        if (!containers.TryGetValue(l.Place, out var c)) containers[l.Place] = c = MakeContainer(root, l.Place);
                        if (TryItem(l.Kind, out var item)) c.Put(item, l.Count);
                        break;
                }
            }

            // Laid out in a row along the table top, or along the tent floor.
            Lay(root, onTable, Spot(root, "Table", kind == StructureKind.Cabin ? CabinTableSpot : TableSpot) + Vector3.up * 0.77f, root.right, 0.22f);
            Vector3 tentFloor = Spot(root, "Tent", TentSpot) + Vector3.up * 0.06f;
            Lay(root, inTent, tentFloor, root.right, 0.25f); // (the tent lies across the campsite)
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
            string label = place == LootPlace.GloveBox ? "Open glove box" : place == LootPlace.Trunk ? "Open trunk" : "Search backpack";
            Vector3 fallback = place == LootPlace.GloveBox ? GloveBoxSpot : place == LootPlace.Trunk ? TrunkSpot : BackpackSpot + Vector3.up * 0.3f;
            Vector3 size = place == LootPlace.GloveBox ? new Vector3(0.12f, 0.4f, 0.6f) : place == LootPlace.Trunk ? new Vector3(1.2f, 0.4f, 0.12f) : new Vector3(0.42f, 0.56f, 0.3f);
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
            if (kind != StructureKind.Shack && kind != StructureKind.Cabin && kind != StructureKind.Campsite) return;
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
            else if (kind != StructureKind.Campsite)
                Debug.LogWarning($"Your {root.name} prefab has no child named 'Inside' with a Box Collider, so it isn't a shelter (docs/custom-assets.md).");
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
                default: item = ItemKind.None; return false;
            }
        }
    }
}
