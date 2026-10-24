using System.Collections.Generic;
using Game.Core;
using Game.Rendering;
using Game.World.Generation;
using UnityEngine;

namespace Game.World.Build
{
    /// <summary>
    /// Grey-box stand-ins for the structures and the lone door, placed where the plan put them, so the layout can
    /// be walked and judged. The real buildings (prefabs, with doors, rooms and items) come with milestone 4/5.
    /// </summary>
    public sealed class Placeholders
    {
        readonly Material wood, darkWood, canvas, metal, paint, stone, bark, needles;
        readonly List<Object> made;
        readonly WorldArtConfig art;

        /// <param name="art">Your prefabs for the places (WorldArtConfig), used where set; null for all grey boxes.</param>
        public Placeholders(List<Object> made, Material bark, Material needles, WorldArtConfig art = null)
        {
            this.made = made;
            this.art = art;
            this.bark = bark;
            this.needles = needles;
            wood = Keep(RuntimeMaterials.Lit("Wood", new Color(0.15f, 0.11f, 0.08f), 0.2f));
            darkWood = Keep(RuntimeMaterials.Lit("Dark wood", new Color(0.07f, 0.05f, 0.04f), 0.25f));
            canvas = Keep(RuntimeMaterials.Lit("Canvas", new Color(0.24f, 0.21f, 0.15f), 0.1f));
            metal = Keep(RuntimeMaterials.Lit("Rusty metal", new Color(0.12f, 0.09f, 0.07f), 0.35f));
            paint = Keep(RuntimeMaterials.Lit("Car paint", new Color(0.14f, 0.03f, 0.03f), 0.55f));
            stone = Keep(RuntimeMaterials.Lit("Fire stones", new Color(0.16f, 0.16f, 0.15f), 0.2f));
        }

        Material Keep(Material m)
        {
            made.Add(m);
            return m;
        }

        GameObject PrefabFor(StructureKind kind)
        {
            if (art == null) return null;
            switch (kind)
            {
                case StructureKind.Cabin: return art.cabin;
                case StructureKind.Shack: return art.shack;
                case StructureKind.Campsite: return art.campsite;
                case StructureKind.Watchtower: return art.watchtower;
                case StructureKind.Car: return art.car;
                case StructureKind.LandmarkTree: return art.landmarkTree;
                default: return null;
            }
        }

        /// <param name="loot">What's to be found in this place (from the plan).</param>
        public GameObject Structure(StructureSite site, float ground, Transform parent, IList<Loot> loot = null)
        {
            var prefab = PrefabFor(site.Kind);
            if (prefab != null)
            {
                var placed = Object.Instantiate(prefab, new Vector3(site.Position.x, ground, site.Position.y), Quaternion.Euler(0f, site.Yaw, 0f), parent);
                placed.name = site.Kind.ToString();
                PlaceFittings.Fit(placed.transform, site.Kind, loot);
                return placed;
            }
            var root = new GameObject(site.Kind.ToString()) { layer = Layers.Props };
            root.transform.SetParent(parent, false);
            root.transform.SetPositionAndRotation(new Vector3(site.Position.x, ground, site.Position.y), Quaternion.Euler(0f, site.Yaw, 0f));
            var t = root.transform;
            switch (site.Kind)
            {
                case StructureKind.Cabin:
                    Room(t, new Vector3(8f, 3.2f, 7f), 1.1f, 2.1f, wood, darkWood);
                    Table(t, PlaceFittings.CabinTableSpot);
                    break;
                case StructureKind.Shack:
                    Room(t, new Vector3(4f, 2.6f, 5f), 1f, 2f, wood, darkWood);
                    Table(t, PlaceFittings.TableSpot);
                    break;
                case StructureKind.Campsite:
                    Tent(t, PlaceFittings.TentSpot, PlaceFittings.TentYaw);
                    for (int i = 0; i < 8; i++)
                    {
                        float a = i / 8f * Mathf.PI * 2f;
                        Shape(PrimitiveType.Sphere, t, new Vector3(1.5f + Mathf.Cos(a) * 0.5f, 0.08f, Mathf.Sin(a) * 0.5f), Vector3.one * 0.2f, stone, false);
                    }
                    Box(t, PlaceFittings.BackpackSpot + new Vector3(0f, 0.25f, 0f), new Vector3(0.35f, 0.5f, 0.22f), canvas, false);
                    break;
                case StructureKind.Watchtower:
                    for (int i = 0; i < 4; i++)
                    {
                        float x = (i % 2 == 0 ? -1.6f : 1.6f), z = (i < 2 ? -1.6f : 1.6f);
                        Shape(PrimitiveType.Cylinder, t, new Vector3(x, 6f, z), new Vector3(0.2f, 6f, 0.2f), darkWood, true);
                    }
                    Box(t, new Vector3(0f, 12f, 0f), new Vector3(4f, 0.25f, 4f), wood);
                    Box(t, new Vector3(0f, 12.6f, 1.95f), new Vector3(4f, 1f, 0.08f), wood);
                    Box(t, new Vector3(0f, 12.6f, -1.95f), new Vector3(4f, 1f, 0.08f), wood);
                    Box(t, new Vector3(0f, 14f, 0f), new Vector3(4.4f, 0.2f, 4.4f), darkWood, false);
                    // The ladder up the west side (rails and rungs: you climb through them, not on them).
                    for (int side = -1; side <= 1; side += 2)
                        Box(t, new Vector3(-2.15f, 6.4f, side * 0.3f), new Vector3(0.07f, 12.8f, 0.07f), darkWood, false);
                    for (float y = 0.35f; y < 12.6f; y += 0.35f)
                        Box(t, new Vector3(-2.15f, y, 0f), new Vector3(0.05f, 0.05f, 0.6f), darkWood, false);
                    break;
                case StructureKind.Car:
                    Box(t, new Vector3(0f, 0.65f, 0f), new Vector3(1.8f, 0.8f, 4.4f), paint);
                    Box(t, new Vector3(0f, 1.35f, -0.2f), new Vector3(1.6f, 0.65f, 2.2f), paint);
                    for (int i = 0; i < 4; i++)
                    {
                        var wheel = Shape(PrimitiveType.Cylinder, t, new Vector3(i % 2 == 0 ? -0.9f : 0.9f, 0.35f, i < 2 ? -1.4f : 1.4f), new Vector3(0.7f, 0.12f, 0.7f), metal, false);
                        wheel.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
                    }
                    break;
                case StructureKind.LandmarkTree:
                    LandmarkTree(t);
                    break;
            }
            PlaceFittings.Fit(t, site.Kind, loot);
            return root;
        }

        // A hollow hut: walls with a doorway in the front (+Z) wall, a floor, a roof and a door that swings out.
        void Room(Transform t, Vector3 size, float doorWidth, float doorHeight, Material walls, Material roof)
        {
            const float wall = 0.12f;
            float w = size.x, h = size.y, d = size.z;
            Box(t, new Vector3(0f, 0.025f, 0f), new Vector3(w, 0.05f, d), roof);                          // floor
            Box(t, new Vector3(0f, h * 0.5f, -d * 0.5f + wall * 0.5f), new Vector3(w, h, wall), walls);     // back
            Box(t, new Vector3(-w * 0.5f + wall * 0.5f, h * 0.5f, 0f), new Vector3(wall, h, d), walls);     // left
            Box(t, new Vector3(w * 0.5f - wall * 0.5f, h * 0.5f, 0f), new Vector3(wall, h, d), walls);      // right
            float side = (w - doorWidth) * 0.5f;
            float front = d * 0.5f - wall * 0.5f;
            Box(t, new Vector3(-(doorWidth + side) * 0.5f, h * 0.5f, front), new Vector3(side, h, wall), walls);
            Box(t, new Vector3((doorWidth + side) * 0.5f, h * 0.5f, front), new Vector3(side, h, wall), walls);
            Box(t, new Vector3(0f, (h + doorHeight) * 0.5f, front), new Vector3(doorWidth, h - doorHeight, wall), walls);
            Box(t, new Vector3(0f, h + 0.15f, 0f), new Vector3(w + 0.5f, 0.3f, d + 0.5f), roof);            // roof

            // The door hangs from its left edge.
            var hinge = new GameObject("Door") { layer = Layers.Props }.transform;
            hinge.SetParent(t, false);
            hinge.localPosition = new Vector3(-doorWidth * 0.5f, 0f, d * 0.5f);
            Box(hinge, new Vector3(doorWidth * 0.5f, doorHeight * 0.5f, 0f), new Vector3(doorWidth - 0.04f, doorHeight - 0.02f, 0.06f), darkWood);
            hinge.gameObject.AddComponent<Door>();

            PlaceFittings.AddShelter(t, t.name, new Bounds(new Vector3(0f, h * 0.5f, 0f), new Vector3(w - wall * 2f, h, d - wall * 2f)));
        }

        void Table(Transform t, Vector3 at)
        {
            Box(t, at + new Vector3(0f, 0.74f, 0f), new Vector3(1.2f, 0.05f, 0.7f), darkWood);
            for (int i = 0; i < 4; i++)
                Box(t, at + new Vector3(i % 2 == 0 ? -0.52f : 0.52f, 0.36f, i < 2 ? -0.28f : 0.28f), new Vector3(0.06f, 0.72f, 0.06f), darkWood, false);
        }

        // A low two-sided tent, open at the front (+Z of the tent): crouch to get in.
        void Tent(Transform t, Vector3 at, float yaw)
        {
            const float halfWidth = 0.8f, ridge = 1.35f, length = 2.4f;
            var tent = new GameObject("Tent") { layer = Layers.Props }.transform;
            tent.SetParent(t, false);
            tent.localPosition = at;
            tent.localRotation = Quaternion.Euler(0f, yaw, 0f);
            float slant = Mathf.Sqrt(halfWidth * halfWidth + ridge * ridge);
            float lean = Mathf.Atan2(halfWidth, ridge) * Mathf.Rad2Deg;
            for (int side = -1; side <= 1; side += 2)
            {
                var panel = Box(tent, new Vector3(side * halfWidth * 0.5f, ridge * 0.5f, 0f), new Vector3(0.04f, slant, length), canvas);
                panel.transform.localRotation = Quaternion.Euler(0f, 0f, -side * lean);
            }
            // The back closed with a stepped flap (three strips narrowing toward the ridge).
            for (int i = 0; i < 3; i++)
            {
                float width = 2f * halfWidth * (1f - (i + 0.5f) / 3f);
                Box(tent, new Vector3(0f, ridge * (i + 0.5f) / 3f, -length * 0.5f + 0.02f), new Vector3(width, ridge / 3f, 0.04f), canvas);
            }
            PlaceFittings.AddShelter(tent, "Tent", new Bounds(new Vector3(0f, 0.55f, 0f), new Vector3(halfWidth * 1.4f, 1.1f, length)));
        }

        // The giant pine (the plan's "7x scale"), a landmark you can find your way by.
        void LandmarkTree(Transform t)
        {
            var (trunk, crown) = ForestMeshes.Pine(new System.Random(7), 14f);
            var trunkMesh = trunk.ToMesh("Landmark trunk");
            var crownMesh = crown.ToMesh("Landmark crown");
            made.Add(trunkMesh);
            made.Add(crownMesh);
            foreach (var (mesh, mat) in new[] { (trunkMesh, bark), (crownMesh, needles) })
            {
                var go = new GameObject(mesh.name) { layer = Layers.Trees };
                go.transform.SetParent(t, false);
                go.transform.localScale = Vector3.one * 7f;
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                go.AddComponent<MeshRenderer>().sharedMaterial = mat;
            }
            var col = t.gameObject.AddComponent<CapsuleCollider>();
            col.radius = 2f;
            col.height = 40f;
            col.center = Vector3.up * 20f;
        }

        public GameObject Door(Vector2 position, float yaw, float ground, Transform parent)
        {
            if (art != null && art.door != null)
            {
                var placed = Object.Instantiate(art.door, new Vector3(position.x, ground, position.y), Quaternion.Euler(0f, yaw, 0f), parent);
                placed.name = "Lone Door";
                // The part that swings: a child named "Door" (swinging about its pivot), or the whole thing.
                Transform swing = placed.transform;
                foreach (var c in placed.GetComponentsInChildren<Transform>(true))
                    if (c != placed.transform && c.name == "Door") { swing = c; break; }
                if (swing.GetComponent<Door>() == null) swing.gameObject.AddComponent<Door>();
                if (swing.GetComponent<LoneDoor>() == null) swing.gameObject.AddComponent<LoneDoor>();
                return placed;
            }
            var root = new GameObject("Lone Door") { layer = Layers.Props };
            root.transform.SetParent(parent, false);
            root.transform.SetPositionAndRotation(new Vector3(position.x, ground, position.y), Quaternion.Euler(0f, yaw, 0f));
            var t = root.transform;
            Box(t, new Vector3(-0.65f, 1.15f, 0f), new Vector3(0.14f, 2.3f, 0.2f), darkWood);
            Box(t, new Vector3(0.65f, 1.15f, 0f), new Vector3(0.14f, 2.3f, 0.2f), darkWood);
            Box(t, new Vector3(0f, 2.36f, 0f), new Vector3(1.44f, 0.14f, 0.2f), darkWood);
            // The door itself hangs from the left post.
            var hinge = new GameObject("Door") { layer = Layers.Props }.transform;
            hinge.SetParent(t, false);
            hinge.localPosition = new Vector3(-0.58f, 0f, 0f);
            Box(hinge, new Vector3(0.58f, 1.1f, 0f), new Vector3(1.16f, 2.2f, 0.06f), wood);
            hinge.gameObject.AddComponent<Door>();
            hinge.gameObject.AddComponent<LoneDoor>();
            return root;
        }

        GameObject Box(Transform parent, Vector3 pos, Vector3 scale, Material mat, bool collider = true) =>
            Shape(PrimitiveType.Cube, parent, pos, scale, mat, collider);

        static GameObject Shape(PrimitiveType type, Transform parent, Vector3 pos, Vector3 scale, Material mat, bool collider)
        {
            var go = GameObject.CreatePrimitive(type);
            go.layer = Layers.Props;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localScale = scale;
            go.GetComponent<Renderer>().sharedMaterial = mat;
            if (!collider) Object.Destroy(go.GetComponent<Collider>());
            return go;
        }
    }
}
