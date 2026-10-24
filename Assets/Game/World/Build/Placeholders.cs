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
        readonly Material tentCanvas, groundSheet, rope, ash, charcoal, ember, pot;
        readonly Material ground;
        Material concrete, concreteDark, rust;
        readonly List<Object> made;
        readonly WorldArtConfig art;

        /// <param name="art">Your prefabs for the places (WorldArtConfig), used where set; null for all grey boxes.</param>
        /// <param name="ground">The forest floor's material (the bunker's earth mound), or null for plain earth.</param>
        public Placeholders(List<Object> made, Material bark, Material needles, WorldArtConfig art = null, Material ground = null)
        {
            this.ground = ground;
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
            tentCanvas = Keep(RuntimeMaterials.Lit("Tent canvas", new Color(0.17f, 0.18f, 0.12f), 0.12f, true)); // (faded olive)
            groundSheet = Keep(RuntimeMaterials.Lit("Ground sheet", new Color(0.06f, 0.07f, 0.06f), 0.3f));
            rope = Keep(RuntimeMaterials.Lit("Rope", new Color(0.33f, 0.3f, 0.23f), 0.1f));
            ash = Keep(RuntimeMaterials.Lit("Ash", new Color(0.09f, 0.09f, 0.085f), 0.05f));
            charcoal = Keep(RuntimeMaterials.Lit("Charcoal", new Color(0.025f, 0.022f, 0.02f), 0.15f));
            ember = Keep(RuntimeMaterials.Glow("Embers", new Color(1f, 0.28f, 0.05f) * 2.5f));
            pot = Keep(RuntimeMaterials.Lit("Pot", new Color(0.1f, 0.1f, 0.1f), 0.5f));
            pot.SetFloat("_Metallic", 0.8f);
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
                case StructureKind.Bunker: return art.bunker;
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
                    Room(t, new Vector3(8f, 3.2f, 7f), 1.6f, 2.3f, wood, darkWood);
                    Table(t, PlaceFittings.CabinTableSpot);
                    break;
                case StructureKind.Shack:
                    Room(t, new Vector3(4f, 2.6f, 5f), 1.5f, 2.2f, wood, darkWood);
                    Table(t, PlaceFittings.TableSpot);
                    break;
                case StructureKind.Campsite:
                    Campsite(t, new System.Random(Mathf.RoundToInt(site.Position.x * 31f + site.Position.y * 17f)));
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
                case StructureKind.Bunker:
                    Bunker(t, new System.Random(Mathf.RoundToInt(site.Position.x * 13f + site.Position.y * 29f)));
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

        // A camp someone left in a hurry: an A-frame tent with its door flaps tied back, facing a dead fire in a ring
        // of stones (ash, charred logs, a few embers still glowing), two logs to sit on and a stump, a pot by the
        // fire, a lantern by the tent, a pile of firewood, and the backpack slumped where it was dropped.
        void Campsite(Transform t, System.Random rng)
        {
            float R(float a, float b) => a + (b - a) * (float)rng.NextDouble();
            Tent(t, PlaceFittings.TentSpot, PlaceFittings.TentYaw);

            var fire = new Vector3(1.5f, 0f, 0f);
            // The ring of stones, each its own size and turned its own way.
            int stones = 11;
            for (int i = 0; i < stones; i++)
            {
                float a = (i + R(-0.2f, 0.2f)) / stones * Mathf.PI * 2f;
                float size = R(0.17f, 0.27f);
                var st = Shape(PrimitiveType.Sphere, t, fire + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * 0.58f + Vector3.up * size * 0.25f,
                    new Vector3(size, size * R(0.5f, 0.75f), size * R(0.8f, 1.1f)), stone, false);
                st.transform.localRotation = Quaternion.Euler(R(-10f, 10f), R(0f, 360f), R(-10f, 10f));
            }
            Shape(PrimitiveType.Cylinder, t, fire + Vector3.up * 0.008f, new Vector3(1f, 0.008f, 1f), ash, false);
            // Charred logs fallen in on each other, and the embers among them.
            for (int i = 0; i < 4; i++)
            {
                float a = i * 95f + R(-15f, 15f);
                var log = Shape(PrimitiveType.Cylinder, t, fire + Quaternion.Euler(0f, a, 0f) * new Vector3(0f, 0.07f, 0.12f), new Vector3(0.1f, 0.28f, 0.1f), charcoal, false);
                log.transform.localRotation = Quaternion.Euler(0f, a, 0f) * Quaternion.Euler(80f, 0f, 0f);
            }
            for (int i = 0; i < 7; i++)
            {
                Vector2 o = new Vector2(R(-0.25f, 0.25f), R(-0.25f, 0.25f));
                Shape(PrimitiveType.Sphere, t, fire + new Vector3(o.x, 0.025f, o.y), Vector3.one * R(0.03f, 0.06f), ember, false);
            }
            Effects.Glow(t, fire + Vector3.up * 0.15f, 0.9f, new Color(1f, 0.3f, 0.08f) * 0.25f, 0.6f, 2.5f, "Embers glow");
            // Smoke still curling up off it.
            var smoke = new GameObject("Smoke") { layer = Layers.Effects };
            smoke.transform.SetParent(t, false);
            smoke.transform.localPosition = fire + Vector3.up * 0.15f;
            smoke.AddComponent<CampSmoke>();

            // Somewhere to sit: two logs round the far side of the fire, and a stump.
            foreach (float deg in new[] { 25f + R(-8f, 8f), -45f + R(-8f, 8f) })
            {
                Vector3 dir = Quaternion.Euler(0f, -deg, 0f) * Vector3.right;
                var seat = Shape(PrimitiveType.Cylinder, t, fire + dir * 1.55f + Vector3.up * 0.15f, new Vector3(0.3f, R(0.6f, 0.75f), 0.3f), bark, true);
                seat.transform.localRotation = Quaternion.LookRotation(dir) * Quaternion.Euler(0f, 0f, 90f); // (lying across the way to the fire)
            }
            Shape(PrimitiveType.Cylinder, t, new Vector3(0.8f, 0.2f, -1.4f), new Vector3(0.42f, 0.2f, 0.42f), bark, true);
            Shape(PrimitiveType.Cylinder, t, new Vector3(0.8f, 0.405f, -1.4f), new Vector3(0.38f, 0.005f, 0.38f), wood, false); // (its cut top)

            // A pot sitting on the stones, a mug knocked over.
            var potAt = fire + new Vector3(-0.15f, 0.2f, 0.55f);
            Shape(PrimitiveType.Cylinder, t, potAt, new Vector3(0.22f, 0.08f, 0.22f), pot, false);
            Rod(t, potAt + new Vector3(-0.1f, 0.08f, 0f), potAt + new Vector3(0f, 0.18f, 0f), 0.006f, pot);
            Rod(t, potAt + new Vector3(0f, 0.18f, 0f), potAt + new Vector3(0.1f, 0.08f, 0f), 0.006f, pot);
            var mug = Shape(PrimitiveType.Cylinder, t, fire + new Vector3(0.9f, 0.04f, 0.9f), new Vector3(0.08f, 0.05f, 0.08f), pot, false);
            mug.transform.localRotation = Quaternion.Euler(0f, R(0f, 360f), 90f);

            // A camping lantern by the tent's door, long out.
            var lamp = new Vector3(-0.55f, 0f, 0.75f);
            Shape(PrimitiveType.Cylinder, t, lamp + Vector3.up * 0.03f, new Vector3(0.12f, 0.03f, 0.12f), pot, false);
            Shape(PrimitiveType.Cylinder, t, lamp + Vector3.up * 0.13f, new Vector3(0.09f, 0.07f, 0.09f), groundSheet, false);
            Shape(PrimitiveType.Cylinder, t, lamp + Vector3.up * 0.22f, new Vector3(0.11f, 0.02f, 0.11f), pot, false);
            Rod(t, lamp + new Vector3(-0.05f, 0.24f, 0f), lamp + new Vector3(0f, 0.3f, 0f), 0.005f, pot);
            Rod(t, lamp + new Vector3(0f, 0.3f, 0f), lamp + new Vector3(0.05f, 0.24f, 0f), 0.005f, pot);

            // Firewood stacked by the tent.
            for (int i = 0; i < 6; i++)
            {
                int row = i < 3 ? 0 : i < 5 ? 1 : 2;
                float x = (i - (row == 0 ? 1 : row == 1 ? 3.5f : 5f)) * 0.13f;
                var wood_ = Shape(PrimitiveType.Cylinder, t, new Vector3(-1.5f + x, 0.06f + row * 0.1f, -1.55f), new Vector3(0.11f, R(0.3f, 0.36f), 0.11f), bark, false);
                wood_.transform.localRotation = Quaternion.Euler(90f, R(-6f, 6f), 0f);
            }

            // The backpack, slumped: a body, a lid, a front pocket, straps and a bedroll on top.
            var pack = new GameObject("Backpack look") { layer = Layers.Props }.transform;
            pack.SetParent(t, false);
            pack.localPosition = PlaceFittings.BackpackSpot;
            pack.localRotation = Quaternion.Euler(-12f, R(-30f, 30f), R(-4f, 4f));
            Box(pack, new Vector3(0f, 0.24f, 0f), new Vector3(0.34f, 0.46f, 0.22f), canvas, false);
            Box(pack, new Vector3(0f, 0.47f, 0.02f), new Vector3(0.36f, 0.06f, 0.25f), canvas, false);
            Box(pack, new Vector3(0f, 0.17f, 0.13f), new Vector3(0.26f, 0.2f, 0.06f), canvas, false);
            for (int side = -1; side <= 1; side += 2)
                Box(pack, new Vector3(side * 0.09f, 0.26f, -0.115f), new Vector3(0.04f, 0.4f, 0.012f), groundSheet, false);
            var roll = Shape(PrimitiveType.Cylinder, pack, new Vector3(0f, 0.57f, 0f), new Vector3(0.14f, 0.2f, 0.14f), tentCanvas, false);
            roll.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
        }

        // An A-frame tent, open at the front (+Z of the tent) with its flaps tied back: crouch to get in. The canvas
        // sags a little between the poles and hangs slack down the sides; poles at each end, guy ropes out to stakes,
        // a ground sheet and a sleeping bag inside.
        void Tent(Transform t, Vector3 at, float yaw)
        {
            const float halfWidth = 0.85f, ridge = 1.25f, length = 2.3f, sag = 0.07f;
            var tent = new GameObject("Tent") { layer = Layers.Props }.transform;
            tent.SetParent(t, false);
            tent.localPosition = at;
            tent.localRotation = Quaternion.Euler(0f, yaw, 0f);
            float RidgeAt(float z) { float u = z / (length * 0.5f); return ridge - sag * (1f - u * u); }

            var cloth = new MeshBuilder();
            const int along = 8, down = 4;
            for (int side = -1; side <= 1; side += 2)
            {
                int start = cloth.VertexCount;
                for (int i = 0; i <= along; i++)
                for (int j = 0; j <= down; j++)
                {
                    float z = -length * 0.5f + length * i / along;
                    float f = j / (float)down; // 0 at the ridge, 1 on the ground
                    float h = RidgeAt(z);
                    // Slack: the canvas bellies in a little between ridge and ground.
                    float slack = Mathf.Sin(f * Mathf.PI) * 0.045f * (1f - Mathf.Abs(z) / length);
                    var p = new Vector3(side * halfWidth * f, h * (1f - f), z) + new Vector3(-side * h, -halfWidth, 0f).normalized * slack;
                    cloth.AddVertex(p, new Vector2(z, f));
                }
                for (int i = 0; i < along; i++)
                for (int j = 0; j < down; j++)
                {
                    int a = start + i * (down + 1) + j, b = a + 1, c = a + down + 1, d = c + 1;
                    if (side > 0) { cloth.AddTriangle(a, b, c); cloth.AddTriangle(b, d, c); }
                    else { cloth.AddTriangle(a, c, b); cloth.AddTriangle(b, c, d); }
                }
            }
            // The back, closed; the front flaps, folded back against the sides and tied.
            float back = -length * 0.5f, front = length * 0.5f;
            Tri(cloth, new Vector3(-halfWidth, 0f, back), new Vector3(0f, RidgeAt(back), back), new Vector3(halfWidth, 0f, back));
            for (int side = -1; side <= 1; side += 2)
            {
                var top = new Vector3(0f, RidgeAt(front), front);
                var foot = new Vector3(side * halfWidth, 0f, front);
                var tied = new Vector3(side * (halfWidth * 0.62f + 0.1f), ridge * 0.42f, front - 0.3f);
                Tri(cloth, top, foot, tied);
            }
            var clothMesh = cloth.ToMesh("Tent canvas");
            made.Add(clothMesh);
            var canvasGo = new GameObject("Canvas") { layer = Layers.Props };
            canvasGo.transform.SetParent(tent, false);
            canvasGo.AddComponent<MeshFilter>().sharedMesh = clothMesh;
            canvasGo.AddComponent<MeshRenderer>().sharedMaterial = tentCanvas;

            // Something solid where the canvas is (unseen slabs along each slope and across the back).
            float slant = Mathf.Sqrt(halfWidth * halfWidth + ridge * ridge);
            float lean = Mathf.Atan2(halfWidth, ridge) * Mathf.Rad2Deg;
            for (int side = -1; side <= 1; side += 2)
            {
                var wall = new GameObject("Tent side") { layer = Layers.Props };
                wall.transform.SetParent(tent, false);
                wall.transform.localPosition = new Vector3(side * halfWidth * 0.5f, ridge * 0.5f, 0f);
                wall.transform.localRotation = Quaternion.Euler(0f, 0f, side * lean); // (leaning in to meet at the ridge)
                wall.AddComponent<BoxCollider>().size = new Vector3(0.04f, slant, length);
            }
            var backWall = new GameObject("Tent back") { layer = Layers.Props };
            backWall.transform.SetParent(tent, false);
            backWall.transform.localPosition = new Vector3(0f, ridge * 0.4f, back);
            backWall.AddComponent<BoxCollider>().size = new Vector3(halfWidth * 1.6f, ridge * 0.8f, 0.04f);

            // Poles, ropes and stakes.
            foreach (float z in new[] { back, front })
            {
                float outward = Mathf.Sign(z);
                Rod(tent, new Vector3(0f, 0f, z), new Vector3(0f, ridge + 0.1f, z), 0.018f, darkWood);
                var stake = new Vector3(0f, 0f, z + outward * 1f);
                Rod(tent, new Vector3(0f, ridge + 0.06f, z), stake + Vector3.up * 0.06f, 0.005f, rope);
                Rod(tent, stake + Vector3.down * 0.05f, stake + Vector3.up * 0.1f, 0.012f, darkWood);
            }
            for (int side = -1; side <= 1; side += 2)
                foreach (float z in new[] { -0.75f, 0.75f })
                {
                    var from = new Vector3(side * halfWidth * 0.45f, ridge * 0.55f, z);
                    var stake = new Vector3(side * (halfWidth + 0.55f), 0f, z);
                    Rod(tent, from, stake + Vector3.up * 0.05f, 0.004f, rope);
                    Rod(tent, stake + Vector3.down * 0.05f, stake + Vector3.up * 0.08f, 0.01f, darkWood);
                }
            // Inside: the ground sheet, and a sleeping bag along one side (the loot lies down the middle).
            Box(tent, new Vector3(0f, 0.01f, 0f), new Vector3(halfWidth * 2f - 0.1f, 0.02f, length - 0.05f), groundSheet, false);
            var bag = Shape(PrimitiveType.Capsule, tent, new Vector3(-halfWidth * 0.5f, 0.07f, -0.1f), new Vector3(0.38f, 0.9f, 0.12f), canvas, false);
            bag.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            PlaceFittings.AddShelter(tent, "Tent", new Bounds(new Vector3(0f, 0.55f, 0f), new Vector3(halfWidth * 1.4f, 1.1f, length)));
        }

        static void Tri(MeshBuilder m, Vector3 a, Vector3 b, Vector3 c)
        {
            int i = m.AddVertex(a, Vector2.zero), j = m.AddVertex(b, Vector2.right), k = m.AddVertex(c, Vector2.up);
            m.AddTriangle(i, j, k);
        }

        // A thin rod between two points (a pole, a rope, a handle).
        void Rod(Transform parent, Vector3 from, Vector3 to, float radius, Material mat)
        {
            Vector3 d = to - from;
            if (d.sqrMagnitude < 1e-6f) return;
            var rod = Shape(PrimitiveType.Cylinder, parent, (from + to) * 0.5f, new Vector3(radius * 2f, d.magnitude * 0.5f, radius * 2f), mat, false);
            rod.transform.localRotation = Quaternion.FromToRotation(Vector3.up, d);
            rod.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        // ------------------------------------------------------------------ the bunker

        // How far forward of the bunker's middle its front is (the doorway faces +Z).
        const float BunkerFront = 3.5f;

        // An old concrete bunker dug into the hillside: a thick, stained front with a dark doorway under a heavy
        // lintel and a capping slab, stepped wing walls angling out either side to hold back the earth, and an
        // earth mound heaped over it all. Through the doorway a short corridor runs back into a small dark room,
        // a crate at its back (its loot on it), an old drum in a corner, rubble on the floor.
        void Bunker(Transform t, System.Random rng)
        {
            float R(float a, float b) => a + (b - a) * (float)rng.NextDouble();
            if (concrete == null)
            {
                var tex = ConcreteTexture(rng.Next());
                made.Add(tex);
                concrete = Keep(RuntimeMaterials.LitTextured("Concrete", Color.white, 0.12f, tex, Vector2.one));
                concreteDark = Keep(RuntimeMaterials.LitTextured("Concrete (inside)", new Color(0.55f, 0.55f, 0.55f), 0.18f, tex, Vector2.one));
                rust = Keep(RuntimeMaterials.Lit("Rusted drum", new Color(0.16f, 0.08f, 0.04f), 0.3f));
            }
            const float F = BunkerFront, wall = 0.3f;
            const float doorW = 1.15f, doorH = 2.05f, faceW = 4.4f, faceH = 3f, faceD = 1f;
            float hw = doorW * 0.5f;
            // The front: two piers, the lintel over the doorway, and a capping slab overhanging it.
            float pier = (faceW - doorW) * 0.5f;
            Box(t, new Vector3(-(hw + pier * 0.5f), faceH * 0.5f, F - faceD * 0.5f), new Vector3(pier, faceH, faceD), concrete);
            Box(t, new Vector3(hw + pier * 0.5f, faceH * 0.5f, F - faceD * 0.5f), new Vector3(pier, faceH, faceD), concrete);
            Box(t, new Vector3(0f, (doorH + faceH) * 0.5f, F - faceD * 0.5f), new Vector3(doorW, faceH - doorH, faceD), concrete);
            Box(t, new Vector3(0f, faceH + 0.17f, F - faceD * 0.5f + 0.05f), new Vector3(faceW + 0.4f, 0.34f, faceD + 0.3f), concrete);
            // (A lip round the doorway, a touch proud of the face.)
            Box(t, new Vector3(-hw - 0.1f, doorH * 0.5f, F + 0.04f), new Vector3(0.2f, doorH + 0.1f, 0.1f), concrete, false);
            Box(t, new Vector3(hw + 0.1f, doorH * 0.5f, F + 0.04f), new Vector3(0.2f, doorH + 0.1f, 0.1f), concrete, false);
            Box(t, new Vector3(0f, doorH + 0.1f, F + 0.05f), new Vector3(doorW + 0.4f, 0.22f, 0.12f), concrete, false);

            // The wing walls, angling out and stepping down.
            for (int side = -1; side <= 1; side += 2)
            {
                Vector3 dir = new Vector3(side * Mathf.Sin(32f * Mathf.Deg2Rad), 0f, Mathf.Cos(32f * Mathf.Deg2Rad));
                Vector3 start = new Vector3(side * (faceW * 0.5f - 0.15f), 0f, F - 0.25f);
                float[] heights = { 2.5f, 1.7f, 0.95f };
                const float step = 0.9f;
                for (int k = 0; k < heights.Length; k++)
                {
                    var seg = Box(t, start + dir * ((k + 0.5f) * step) + Vector3.up * heights[k] * 0.5f, new Vector3(0.36f, heights[k], step + 0.02f), concrete);
                    seg.transform.localRotation = Quaternion.LookRotation(dir);
                }
            }

            // Inside: the corridor, then the room (walls, floor and ceiling of darker, damp concrete).
            float corridorBack = F - faceD - 2.6f, roomFront = corridorBack, roomBack = roomFront - 3f;
            const float roomHalf = 1.6f, roomH = 2.3f;
            float cz = (F - faceD + corridorBack) * 0.5f, cl = F - faceD - corridorBack;
            Box(t, new Vector3(-hw - wall * 0.5f, doorH * 0.5f, cz), new Vector3(wall, doorH, cl), concreteDark);
            Box(t, new Vector3(hw + wall * 0.5f, doorH * 0.5f, cz), new Vector3(wall, doorH, cl), concreteDark);
            Box(t, new Vector3(0f, doorH + 0.15f, cz), new Vector3(doorW + wall * 2f, 0.3f, cl), concreteDark);
            Box(t, new Vector3(0f, 0.02f, (F + roomBack) * 0.5f), new Vector3(roomHalf * 2f + wall * 2f, 0.04f, F - roomBack), concreteDark, false); // floor
            float rz = (roomFront + roomBack) * 0.5f, rl = roomFront - roomBack;
            Box(t, new Vector3(-roomHalf - wall * 0.5f, roomH * 0.5f, rz), new Vector3(wall, roomH, rl), concreteDark);
            Box(t, new Vector3(roomHalf + wall * 0.5f, roomH * 0.5f, rz), new Vector3(wall, roomH, rl), concreteDark);
            Box(t, new Vector3(0f, roomH * 0.5f, roomBack - wall * 0.5f), new Vector3(roomHalf * 2f + wall * 2f, roomH, wall), concreteDark);
            float flank = roomHalf - hw;
            Box(t, new Vector3(-(hw + flank * 0.5f), roomH * 0.5f, roomFront + wall * 0.5f), new Vector3(flank, roomH, wall), concreteDark);
            Box(t, new Vector3(hw + flank * 0.5f, roomH * 0.5f, roomFront + wall * 0.5f), new Vector3(flank, roomH, wall), concreteDark);
            Box(t, new Vector3(0f, (doorH + roomH) * 0.5f, roomFront + wall * 0.5f), new Vector3(doorW, roomH - doorH, wall), concreteDark);
            Box(t, new Vector3(0f, roomH + 0.15f, rz), new Vector3(roomHalf * 2f + wall * 2f, 0.3f, rl + wall * 2f), concreteDark);

            // What's left in it: the crate (its loot goes on top), a drum, some rubble.
            Box(t, PlaceFittings.BunkerCrateSpot + new Vector3(0f, 0.37f, 0f), new Vector3(1f, 0.74f, 0.6f), wood);
            for (int i = 0; i < 3; i++) Box(t, PlaceFittings.BunkerCrateSpot + new Vector3(0f, 0.15f + i * 0.22f, 0.305f), new Vector3(1.02f, 0.04f, 0.02f), darkWood, false);
            Shape(PrimitiveType.Cylinder, t, new Vector3(roomHalf - 0.4f, 0.45f, roomBack + 0.45f), new Vector3(0.58f, 0.45f, 0.58f), rust, true);
            for (int i = 0; i < 9; i++)
            {
                float sz = R(0.08f, 0.25f);
                var lump = Box(t, new Vector3(R(-roomHalf + 0.3f, roomHalf - 0.3f), sz * 0.3f, R(roomBack + 0.3f, F - faceD - 0.3f)), new Vector3(sz, sz * 0.6f, sz * R(0.7f, 1.3f)), concrete, false);
                lump.transform.localRotation = Quaternion.Euler(R(-20f, 20f), R(0f, 360f), R(-20f, 20f));
            }

            // The earth heaped over it: high and flat over the rooms (so nothing pokes out), falling away round
            // the sides and back, banked against the front's sides, open in front of the doorway.
            EarthMound(t, new Vector2(roomHalf + wall + 0.3f, F - 0.05f), roomBack - wall - 0.3f, faceH, rng);

            PlaceFittings.AddShelter(t, "Bunker", new Bounds(new Vector3(0f, roomH * 0.5f, (F - faceD + roomBack) * 0.5f), new Vector3(roomHalf * 2f, roomH, F - faceD - roomBack)));
        }

        // A heightfield of earth over the bunker: full height over the box (|x| < half.x, back < z < half.y),
        // easing down to the ground over 3.2 m round it, a little lumpy; nothing in front of the doorway.
        void EarthMound(Transform t, Vector2 half, float back, float height, System.Random rng)
        {
            const float step = 0.5f, fall = 3.2f;
            float x0 = -half.x - fall - 0.5f, x1 = half.x + fall + 0.5f, z0 = back - fall - 0.5f, z1 = half.y + fall;
            int nx = Mathf.CeilToInt((x1 - x0) / step), nz = Mathf.CeilToInt((z1 - z0) / step);
            float seed = (float)rng.NextDouble() * 100f;
            float H(float x, float z)
            {
                if (z > half.y - 0.1f && Mathf.Abs(x) < 2.3f) return -0.15f; // (the doorway's open ground)
                float dx = Mathf.Max(0f, Mathf.Abs(x) - half.x), dz = Mathf.Max(0f, Mathf.Max(back - z, z - half.y));
                float d = Mathf.Sqrt(dx * dx + dz * dz);
                float u = Mathf.Clamp01(d / fall);
                float lumps = (Mathf.PerlinNoise(x * 0.6f + seed, z * 0.6f) - 0.5f) * 0.35f * (1f - u * 0.5f);
                return height * (1f - u * u * (3f - 2f * u)) + lumps - 0.1f;
            }
            var verts = new List<Vector3>();
            var uvs = new List<Vector2>();
            var tris = new List<int>();
            for (int j = 0; j <= nz; j++)
            for (int i = 0; i <= nx; i++)
            {
                float x = x0 + i * step, z = z0 + j * step;
                verts.Add(new Vector3(x, H(x, z), z));
                uvs.Add(new Vector2(x, z) / 3f);
            }
            for (int j = 0; j < nz; j++)
            for (int i = 0; i < nx; i++)
            {
                int a = j * (nx + 1) + i, b = a + 1, c = a + nx + 1, d = c + 1;
                if (verts[a].y <= 0f && verts[b].y <= 0f && verts[c].y <= 0f && verts[d].y <= 0f) continue;
                tris.Add(a); tris.Add(c); tris.Add(b);
                tris.Add(b); tris.Add(c); tris.Add(d);
            }
            var mesh = new Mesh { name = "Bunker mound" };
            mesh.SetVertices(verts);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateTangents();
            mesh.RecalculateBounds();
            made.Add(mesh);
            var go = new GameObject("Earth") { layer = Layers.Ground };
            go.transform.SetParent(t, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = ground != null ? ground : darkWood;
            go.AddComponent<MeshCollider>().sharedMesh = mesh;
        }

        // Old poured concrete: grey, blotched lighter and darker, streaked down by years of rain, a green tinge of
        // moss creeping up from the bottom, and fine cracks.
        static Texture2D ConcreteTexture(int seed)
        {
            var rng = new System.Random(seed);
            const int size = 256;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, true) { name = "Concrete", wrapMode = TextureWrapMode.Repeat };
            var px = new Color[size * size];
            float o = (float)rng.NextDouble() * 100f;
            var streak = new float[size];
            for (int x = 0; x < size; x++) streak[x] = Mathf.PerlinNoise(x * 0.09f + o, 0.5f) * Mathf.PerlinNoise(x * 0.4f + o, 3.3f);
            Color baseC = new Color(0.36f, 0.35f, 0.32f), dark = new Color(0.16f, 0.15f, 0.13f), moss = new Color(0.17f, 0.2f, 0.11f);
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float u = x / (float)size, v = y / (float)size;
                float blotch = Mathf.PerlinNoise(u * 5f + o, v * 5f) * 0.6f + Mathf.PerlinNoise(u * 19f + o, v * 19f) * 0.4f;
                Color c = Color.Lerp(dark, baseC * 1.15f, Mathf.SmoothStep(0.15f, 0.85f, blotch));
                // Rain streaks: running down from the top, fading as they go.
                float run = streak[x] * Mathf.Lerp(1f, 0.3f, 1f - v) * (0.6f + 0.4f * Mathf.PerlinNoise(u * 40f, v * 3f + o));
                c = Color.Lerp(c, dark * 0.8f, Mathf.Clamp01(run * 1.4f));
                // Moss low down.
                float m = Mathf.Clamp01((0.35f - v) / 0.35f) * Mathf.PerlinNoise(u * 12f + o, v * 12f + 7f);
                c = Color.Lerp(c, moss, Mathf.SmoothStep(0.25f, 0.6f, m));
                c *= 0.9f + 0.2f * (float)rng.NextDouble(); // (grain)
                c.a = 1f;
                px[y * size + x] = c;
            }
            for (int k = 0; k < 14; k++) // cracks: wandering dark lines
            {
                float x = rng.Next(size), y = rng.Next(size), a = (float)rng.NextDouble() * Mathf.PI * 2f;
                int len = rng.Next(20, 90);
                for (int i = 0; i < len; i++)
                {
                    a += ((float)rng.NextDouble() - 0.5f) * 0.6f;
                    x += Mathf.Cos(a); y += Mathf.Sin(a);
                    int xi = ((int)x % size + size) % size, yi = ((int)y % size + size) % size;
                    px[yi * size + xi] *= 0.45f;
                }
            }
            tex.SetPixels(px);
            tex.Apply(true);
            return tex;
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
