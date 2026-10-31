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
        readonly Material tentCanvas, groundSheet, rope, ash, charcoal, ember, pot, sooty, birch, blanket, pillow;
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
            stone = Keep(RuntimeMaterials.Lit("Fire stones", new Color(0.2f, 0.195f, 0.18f), 0.22f));
            sooty = Keep(RuntimeMaterials.Lit("Sooted stones", new Color(0.06f, 0.055f, 0.05f), 0.15f));
            birch = Keep(RuntimeMaterials.Lit("Birch bark", new Color(0.55f, 0.52f, 0.46f), 0.2f));
            blanket = Keep(RuntimeMaterials.LitTextured("Blanket", Color.white, 0.05f, Keep(PlaidTexture()), new Vector2(3f, 3f), true));
            pillow = Keep(RuntimeMaterials.Lit("Pillow", new Color(0.5f, 0.47f, 0.4f), 0.08f));
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

        Texture2D Keep(Texture2D t)
        {
            made.Add(t);
            return t;
        }

        // A wool blanket's check: deep red and dark green bands crossing on brown, with a thin pale stripe.
        static Texture2D PlaidTexture()
        {
            const int size = 64;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, true) { name = "Blanket check", wrapMode = TextureWrapMode.Repeat };
            Color Band(int i)
            {
                int k = i % 32;
                if (k < 10) return new Color(0.32f, 0.05f, 0.04f);       // red
                if (k < 12) return new Color(0.45f, 0.4f, 0.3f);         // pale stripe
                if (k < 20) return new Color(0.05f, 0.1f, 0.06f);        // green
                return new Color(0.14f, 0.08f, 0.05f);                   // brown
            }
            var px = new Color[size * size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                // Woven: alternate threads show the warp or the weft.
                bool warp = ((x + y) & 1) == 0;
                Color c = Color.Lerp(Band(x), Band(y), warp ? 0.35f : 0.65f);
                px[y * size + x] = c * (0.9f + 0.1f * ((x * 7 + y * 13) % 5) / 4f);
            }
            tex.SetPixels(px);
            tex.Apply(true);
            return tex;
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
                {
                    var door = Room(t, PlaceFittings.CabinSize, 1.6f, 2.3f, wood, darkWood, false);
                    new HutModel(made, stone).Build(t, door, PlaceFittings.CabinSize, 1.6f, 2.3f, true, PlaceFittings.CabinTableSpot, Rng(site));
                    break;
                }
                case StructureKind.Shack:
                {
                    var door = Room(t, new Vector3(4f, 2.6f, 5f), 1.5f, 2.2f, wood, darkWood, false);
                    new HutModel(made, stone).Build(t, door, new Vector3(4f, 2.6f, 5f), 1.5f, 2.2f, false, PlaceFittings.TableSpot, Rng(site));
                    break;
                }
                case StructureKind.Campsite:
                    Campsite(t, new System.Random(Mathf.RoundToInt(site.Position.x * 31f + site.Position.y * 17f)));
                    break;
                case StructureKind.Watchtower:
                    new WatchtowerModel(made, Rng(site)).Build(t);
                    break;
                case StructureKind.Car:
                {
                    var rng = new System.Random(Mathf.RoundToInt(site.Position.x * 17f + site.Position.y * 7f));
                    var model = new CarModel(made);
                    model.Build(t, rng);
                    model.Crash(rng);
                    CrashSite(t, rng);
                    break;
                }
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

        // Into the pine: the car run off the track head-on into a big tree. The tree, the bark torn off its trunk
        // where it hit, the car's front folded round it (see CarModel.Crash), the driver's door hanging open, one
        // headlight still on (Car.oneLightOn), the ruts it tore in the ground coming off the track, glass and
        // snapped branches strewn round its front.
        void CrashSite(Transform t, System.Random rng)
        {
            float R(float a, float b) => a + (b - a) * (float)rng.NextDouble();
            // The tree: a big one, just off the car's middle, right against its folded front.
            var (trunk, crown) = ForestMeshes.Oak(rng, 16f, 0, 0.5f);
            var tree = new GameObject("Crash tree") { layer = Layers.Trees }.transform;
            tree.SetParent(t, false);
            tree.localPosition = new Vector3(0.3f, -0.05f, 2.35f);
            tree.localRotation = Quaternion.Euler(0f, R(0f, 360f), 0f);
            var tm = trunk.ToMesh("Crash tree trunk");
            var cm = crown.ToMesh("Crash tree crown");
            made.Add(tm); made.Add(cm);
            var tgo = new GameObject("Trunk") { layer = Layers.Trees };
            tgo.transform.SetParent(tree, false);
            tgo.AddComponent<MeshFilter>().sharedMesh = tm;
            tgo.AddComponent<MeshRenderer>().sharedMaterial = bark != null ? bark : darkWood;
            var cgo = new GameObject("Crown") { layer = Layers.Trees };
            cgo.transform.SetParent(tree, false);
            cgo.AddComponent<MeshFilter>().sharedMesh = cm;
            cgo.AddComponent<MeshRenderer>().sharedMaterial = needles != null ? needles : darkWood;
            var col = tree.gameObject.AddComponent<CapsuleCollider>();
            col.radius = 0.55f;
            col.height = 8f;
            col.center = new Vector3(0f, 4f, 0f);
            // The bark torn off where the car hit: pale wood showing, splinters standing out.
            var fresh = Keep(RuntimeMaterials.Lit("Torn wood", new Color(0.42f, 0.33f, 0.22f), 0.15f));
            Box(t, new Vector3(0.28f, 0.95f, 1.9f), new Vector3(0.55f, 0.9f, 0.06f), fresh, false).transform.localRotation = Quaternion.Euler(0f, R(-8f, 8f), 0f);
            for (int i = 0; i < 6; i++)
                Box(t, new Vector3(R(0.05f, 0.55f), R(0.6f, 1.35f), 1.86f), new Vector3(0.02f, R(0.15f, 0.35f), 0.02f), fresh, false).transform.localRotation = Quaternion.Euler(R(-40f, 40f), 0f, R(-30f, 30f));
            // The ruts it tore coming off the track: two dark furrows back from its wheels, in short lengths
            // laid on the ground as it rises and falls.
            var rutMat = Keep(RuntimeMaterials.Lit("Torn earth", new Color(0.07f, 0.055f, 0.04f), 0.05f));
            Physics.SyncTransforms(); // (the ground's just been built and placed: let the raycasts see it)
            for (int side = -1; side <= 1; side += 2)
                for (int k = 0; k < 14; k++)
                {
                    float z = -1.6f - k * 0.75f, x = side * 0.82f - k * 0.09f;
                    Vector3 local = new Vector3(x, 0f, z);
                    Vector3 world = t.TransformPoint(local);
                    float y = Physics.Raycast(world + Vector3.up * 5f, Vector3.down, out var hit, 12f, Layers.Mask(Layers.Ground), QueryTriggerInteraction.Ignore) ? t.InverseTransformPoint(hit.point).y : 0f;
                    Box(t, new Vector3(x, y + 0.012f, z), new Vector3(0.3f + 0.05f * Mathf.Sin(k), 0.03f, 0.8f), rutMat, false).transform.localRotation = Quaternion.Euler(0f, -7f, 0f);
                }
            // Glass and bits of grille strewn round the front, snapped branches down from the tree.
            var shardMat = Keep(RuntimeMaterials.Glass("Broken glass", new Color(0.4f, 0.42f, 0.43f, 0.8f)));
            for (int i = 0; i < 26; i++)
                Box(t, new Vector3(R(-1.4f, 1.6f), 0.015f, R(1.2f, 3.4f)), new Vector3(R(0.03f, 0.08f), 0.006f, R(0.03f, 0.07f)), shardMat, false).transform.localRotation = Quaternion.Euler(0f, R(0f, 360f), 0f);
            for (int i = 0; i < 5; i++)
            {
                var b = Shape(PrimitiveType.Cylinder, t, new Vector3(R(-2f, 2.4f), 0.05f, R(1.5f, 4f)), new Vector3(R(0.05f, 0.09f), R(0.5f, 0.9f), R(0.05f, 0.09f)), bark != null ? bark : darkWood, false);
                b.transform.localRotation = Quaternion.Euler(90f, R(0f, 360f), 0f);
            }
            // (Its front's in the tree: the door, the light.)
            var car = t.GetComponent<Car>() ?? t.gameObject.AddComponent<Car>();
            car.oneLightOn = true;
            foreach (var d in t.GetComponentsInChildren<Door>(true))
                if (d.name == "Door FL") d.ajar = 0.85f;
        }

        static System.Random Rng(StructureSite site) => new System.Random(Mathf.RoundToInt(site.Position.x * 23f + site.Position.y * 31f));

        // A hollow hut: walls with a doorway in the front (+Z) wall, a floor, a roof and a door that swings out.
        // Not shown (shown: false), it's just what you bump into, the door's hinge and the shelter, and the
        // detailed model goes over it. Returns the door's hinge.
        Transform Room(Transform t, Vector3 size, float doorWidth, float doorHeight, Material walls, Material roof, bool shown = true)
        {
            int before = t.childCount;
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
            if (!shown)
                for (int i = before; i < t.childCount; i++)
                    foreach (var r in t.GetChild(i).GetComponentsInChildren<Renderer>()) r.enabled = false;
            return hinge;
        }

        void Table(Transform t, Vector3 at)
        {
            Box(t, at + new Vector3(0f, 0.74f, 0f), new Vector3(1.2f, 0.05f, 0.7f), darkWood);
            for (int i = 0; i < 4; i++)
                Box(t, at + new Vector3(i % 2 == 0 ? -0.52f : 0.52f, 0.36f, i < 2 ? -0.28f : 0.28f), new Vector3(0.06f, 0.72f, 0.06f), darkWood, false);
        }

        // A camp someone left in a hurry: an A-frame tent (crawl in with E; its flap opens and shuts, and a little
        // lantern hangs inside), and a fire laid ready but never lit, in a fancy pit: a ring of big rocks with a
        // lower ring of smaller, sooted stones inside it, a bed of old ash and charcoal, a teepee of split logs
        // over kindling and a twist of birch bark, and an iron tripod over it with a kettle hung on a chain, smoke
        // still rising off the ashes of the last fire into the sky (so the camp can be found from afar). Light
        // it with a lighter (the Campfire). Two logs to sit on and a stump, a mug, a pile of firewood, and the
        // backpack slumped where it was dropped.
        void Campsite(Transform t, System.Random rng)
        {
            float R(float a, float b) => a + (b - a) * (float)rng.NextDouble();
            Tent(t, PlaceFittings.TentSpot, PlaceFittings.TentYaw);

            var fire = new Vector3(1.5f, 0f, 0f);
            var pit = new GameObject("Campfire") { layer = Layers.Props }.transform;
            pit.SetParent(t, false);
            pit.localPosition = fire;
            // The ring: big rounded rocks, each its own size and turned its own way, and inside them a lower ring
            // of smaller stones, blackened by old fires.
            int big = 12;
            for (int i = 0; i < big; i++)
            {
                float a = (i + R(-0.15f, 0.15f)) / big * Mathf.PI * 2f;
                float size = R(0.24f, 0.32f);
                Rock(pit, new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * 0.68f + Vector3.up * size * 0.18f,
                    new Vector3(size, size * R(0.55f, 0.75f), size * R(0.8f, 1f)), R(0f, 360f), stone, rng);
            }
            int small = 16;
            for (int i = 0; i < small; i++)
            {
                float a = (i + 0.5f + R(-0.2f, 0.2f)) / small * Mathf.PI * 2f;
                float size = R(0.11f, 0.15f);
                Rock(pit, new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * 0.5f + Vector3.up * size * 0.15f,
                    new Vector3(size, size * 0.6f, size), R(0f, 360f), sooty, rng);
            }
            // The bed of the pit: old ash, sunk a touch, with lumps of charcoal in it.
            Shape(PrimitiveType.Cylinder, pit, Vector3.up * 0.006f, new Vector3(0.95f, 0.006f, 0.95f), ash, false);
            for (int i = 0; i < 14; i++)
            {
                Vector2 o = Random2(rng) * 0.36f;
                var lump = Shape(PrimitiveType.Cube, pit, new Vector3(o.x, 0.015f, o.y), new Vector3(R(0.03f, 0.07f), R(0.02f, 0.04f), R(0.03f, 0.06f)), charcoal, false);
                lump.transform.localRotation = Quaternion.Euler(R(-20f, 20f), R(0f, 360f), R(-20f, 20f));
            }
            // Kindling criss-crossed in the middle, a twist of birch bark on it, and the teepee of split logs over.
            for (int i = 0; i < 8; i++)
            {
                float a = i * 47f + R(-10f, 10f);
                Vector3 d = Quaternion.Euler(0f, a, 0f) * Vector3.forward * R(0.12f, 0.17f);
                Stick(pit, -d + Vector3.up * (0.025f + i * 0.006f), d + Vector3.up * (0.03f + i * 0.006f), 0.012f, wood);
            }
            var tinder = Shape(PrimitiveType.Sphere, pit, Vector3.up * 0.07f, new Vector3(0.09f, 0.05f, 0.07f), birch, false);
            tinder.transform.localRotation = Quaternion.Euler(0f, R(0f, 360f), 0f);
            var logs = new List<Renderer>();
            Vector3 apex = Vector3.up * 0.62f;
            for (int i = 0; i < 7; i++)
            {
                float a = (i + R(-0.15f, 0.15f)) / 7f * Mathf.PI * 2f;
                Vector3 foot = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * R(0.3f, 0.36f) + Vector3.up * 0.03f;
                Vector3 top = apex + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * 0.04f;
                logs.Add(Stick(pit, foot, top, R(0.035f, 0.05f), bark));
            }
            // The tripod over it, and a kettle hanging from its chain.
            Vector3 crown = Vector3.up * 1.35f;
            for (int i = 0; i < 3; i++)
            {
                float a = (i / 3f + 0.08f) * Mathf.PI * 2f;
                Stick(pit, new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * 0.95f, crown + new Vector3(Mathf.Cos(a), 0.06f, Mathf.Sin(a)) * 0.06f, 0.013f, metal);
            }
            Vector3 kettle = Vector3.up * 0.88f;
            for (int i = 0; i < 6; i++) // (the chain: little links, each turned to the last)
            {
                var link = Shape(PrimitiveType.Capsule, pit, Vector3.Lerp(crown, kettle + Vector3.up * 0.2f, (i + 0.5f) / 6f), new Vector3(0.014f, 0.022f, 0.006f), metal, false);
                link.transform.localRotation = Quaternion.Euler(0f, i * 90f, 0f);
            }
            Shape(PrimitiveType.Sphere, pit, kettle, new Vector3(0.24f, 0.2f, 0.24f), pot, false);
            Shape(PrimitiveType.Cylinder, pit, kettle + Vector3.up * 0.095f, new Vector3(0.12f, 0.012f, 0.12f), pot, false);
            Shape(PrimitiveType.Sphere, pit, kettle + Vector3.up * 0.115f, Vector3.one * 0.025f, darkWood, false); // (the knob on its lid)
            Rod(pit, kettle + new Vector3(-0.11f, 0.05f, 0f), kettle + Vector3.up * 0.2f, 0.004f, metal);
            Rod(pit, kettle + Vector3.up * 0.2f, kettle + new Vector3(0.11f, 0.05f, 0f), 0.004f, metal);
            var spout = Shape(PrimitiveType.Cylinder, pit, kettle + new Vector3(0.13f, 0.02f, 0f), new Vector3(0.03f, 0.06f, 0.03f), pot, false);
            spout.transform.localRotation = Quaternion.Euler(0f, 0f, -55f);

            // Smoke still curling up off the ashes of the last fire, rising into the sky: seen over the trees from
            // far off, so you can find the camp.
            var remnant = new GameObject("Smoke") { layer = Layers.Effects };
            remnant.transform.SetParent(pit, false);
            remnant.transform.localPosition = Vector3.up * 0.1f;
            remnant.AddComponent<CampSmoke>();

            var campfire = pit.gameObject.AddComponent<Campfire>();
            campfire.logs = logs.ToArray();
            campfire.charred = charcoal;
            var aim = pit.gameObject.AddComponent<SphereCollider>(); // (what you click to light it)
            aim.isTrigger = true;
            aim.center = Vector3.up * 0.3f;
            aim.radius = 0.5f;

            // Somewhere to sit: two logs round the far side of the fire, and a stump.
            foreach (float deg in new[] { 25f + R(-8f, 8f), -45f + R(-8f, 8f) })
            {
                Vector3 dir = Quaternion.Euler(0f, -deg, 0f) * Vector3.right;
                var seat = Shape(PrimitiveType.Cylinder, t, fire + dir * 1.55f + Vector3.up * 0.15f, new Vector3(0.3f, R(0.6f, 0.75f), 0.3f), bark, true);
                seat.transform.localRotation = Quaternion.LookRotation(dir) * Quaternion.Euler(0f, 0f, 90f); // (lying across the way to the fire)
            }
            Shape(PrimitiveType.Cylinder, t, new Vector3(0.8f, 0.2f, -1.4f), new Vector3(0.42f, 0.2f, 0.42f), bark, true);
            Shape(PrimitiveType.Cylinder, t, new Vector3(0.8f, 0.405f, -1.4f), new Vector3(0.38f, 0.005f, 0.38f), wood, false); // (its cut top)

            // A mug knocked over by the fire.
            var mug = Shape(PrimitiveType.Cylinder, t, fire + new Vector3(0.95f, 0.04f, 0.95f), new Vector3(0.08f, 0.05f, 0.08f), pot, false);
            mug.transform.localRotation = Quaternion.Euler(0f, R(0f, 360f), 90f);

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
            // The door flaps (E opens and shuts them), and the doorway you crawl in by.
            var flapGo = new GameObject("Flaps") { layer = Layers.Props };
            flapGo.transform.SetParent(tent, false);
            flapGo.transform.localPosition = new Vector3(0f, 0f, front);
            var flap = flapGo.AddComponent<TentFlap>();
            flap.Build(halfWidth, RidgeAt(front), tentCanvas);
            var doorwayGo = new GameObject("Doorway") { layer = Layers.Props };
            doorwayGo.transform.SetParent(tent, false);
            var doorway = doorwayGo.AddComponent<TentDoorway>();
            doorway.flap = flap;
            doorway.sitAt = new Vector3(0f, 0.03f, -0.3f);
            doorway.outAt = new Vector3(0f, 0.05f, front + 0.85f);

            // Inside: the ground sheet, a wool blanket spread over it (folded back at the head), two pillows, and a
            // little lantern hanging from the ridge (click it on and off). The loot lies down the middle.
            Box(tent, new Vector3(0f, 0.01f, 0f), new Vector3(halfWidth * 2f - 0.1f, 0.02f, length - 0.05f), groundSheet, false);
            Blanket(tent, new Vector3(0f, 0.022f, 0.05f), 1.25f, 1.7f);
            for (int side = -1; side <= 1; side += 2)
            {
                var p = Shape(PrimitiveType.Sphere, tent, new Vector3(side * 0.3f, 0.07f, -0.88f), new Vector3(0.46f, 0.12f, 0.28f), pillow, false);
                p.transform.localRotation = Quaternion.Euler(0f, side * 6f, side * 3f);
            }
            HangingLantern(tent, new Vector3(0f, RidgeAt(0.3f), 0.3f));
            PlaceFittings.AddShelter(tent, "Tent", new Bounds(new Vector3(0f, 0.55f, 0f), new Vector3(halfWidth * 1.4f, 1.1f, length)));
        }

        // A wool blanket lying a little rumpled on the tent floor, its top edge turned back over itself.
        void Blanket(Transform parent, Vector3 centre, float width, float length)
        {
            var m = new MeshBuilder();
            const int nx = 10, nz = 14;
            float Wrinkle(float x, float z) => 0.012f * Mathf.Sin(x * 9f + z * 3f) * Mathf.Sin(z * 7f - x * 2f) + 0.008f * Mathf.Sin(x * 23f + z * 17f);
            for (int layer = 0; layer < 2; layer++)
            {
                int start = m.VertexCount;
                // The main sheet, and the turned-back strip lying on top of its far end.
                float z0 = layer == 0 ? -length * 0.5f : -length * 0.5f, z1 = layer == 0 ? length * 0.5f : -length * 0.5f + 0.28f;
                float lift = layer == 0 ? 0f : 0.018f;
                for (int j = 0; j <= nz; j++)
                for (int i = 0; i <= nx; i++)
                {
                    float x = (i / (float)nx - 0.5f) * width, z = Mathf.Lerp(z0, z1, j / (float)nz);
                    float edgeDrop = Mathf.Max(0f, Mathf.Abs(x) - width * 0.45f) * 0.3f; // (sagging at its sides)
                    m.AddVertex(centre + new Vector3(x, Wrinkle(x, z) + lift - edgeDrop + 0.012f, z), new Vector2(x, z));
                }
                for (int j = 0; j < nz; j++)
                for (int i = 0; i < nx; i++)
                {
                    int a = start + j * (nx + 1) + i, b = a + 1, c = a + nx + 1, d = c + 1;
                    m.AddTriangle(a, c, b);
                    m.AddTriangle(b, c, d);
                }
            }
            var mesh = m.ToMesh("Blanket");
            made.Add(mesh);
            var go = new GameObject("Blanket") { layer = Layers.Props };
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = blanket;
        }

        // A little camping lantern hung on a hook from the tent's ridge: click it on and off.
        void HangingLantern(Transform parent, Vector3 ridgeAt) => LanternModel.Hanging(parent, ridgeAt, 0.06f, pot, metal, made);

        // A rock (the forest's own shape), placed, sized and turned.
        void Rock(Transform parent, Vector3 at, Vector3 size, float yaw, Material mat, System.Random rng)
        {
            var mesh = ForestMeshes.Rock(rng).ToMesh("Fire stone");
            made.Add(mesh);
            var go = new GameObject("Stone") { layer = Layers.Props };
            go.transform.SetParent(parent, false);
            go.transform.localPosition = at;
            go.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
            go.transform.localScale = size;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = mat;
        }

        // A round stick or log between two points (casting shadows, unlike a rod).
        static Renderer Stick(Transform parent, Vector3 from, Vector3 to, float radius, Material mat)
        {
            Vector3 d = to - from;
            var go = Shape(PrimitiveType.Cylinder, parent, (from + to) * 0.5f, new Vector3(radius * 2f, d.magnitude * 0.5f, radius * 2f), mat, false);
            go.transform.localRotation = Quaternion.FromToRotation(Vector3.up, d);
            return go.GetComponent<Renderer>();
        }

        static Vector2 Random2(System.Random rng)
        {
            float a = (float)rng.NextDouble() * Mathf.PI * 2f, r = Mathf.Sqrt((float)rng.NextDouble());
            return new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r;
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
        // a crate at its back (its loot on it), an old drum in a corner, rubble on the floor. Through a doorway in
        // the room's back wall, a long hallway runs on back into the hill, dead lamps in cages along its ceiling, to
        // a staircase going down into the ground: down into a dark that swallows the steps a few metres below you.
        // Walk on down and you're going down, step after step... but turn round and the top's right behind you,
        // as close as it ever was (see StairLoop).
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
            // (Standing out in front of the face, not sunk into it: their sides were in the same planes as the
            // doorway's, and flickered.)
            Box(t, new Vector3(-hw - 0.1f, doorH * 0.5f, F + 0.05f), new Vector3(0.2f, doorH + 0.1f, 0.1f), concrete, false);
            Box(t, new Vector3(hw + 0.1f, doorH * 0.5f, F + 0.05f), new Vector3(0.2f, doorH + 0.1f, 0.1f), concrete, false);
            Box(t, new Vector3(0f, doorH + 0.11f, F + 0.06f), new Vector3(doorW + 0.4f, 0.22f, 0.12f), concrete, false);
            BunkerLamps(t, new[] { new Vector3(-hw - 0.75f, doorH - 0.05f, F), new Vector3(hw + 0.75f, doorH - 0.05f, F) });

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
            // (The corridor runs back to the room's front wall, not into it: its walls and ceiling used to carry on
            // through the wall's thickness, in the same planes as the doorway's sides and top, and flickered.)
            float cz = (F - faceD + corridorBack + wall) * 0.5f, cl = F - faceD - corridorBack - wall;
            Box(t, new Vector3(-hw - wall * 0.5f, doorH * 0.5f, cz), new Vector3(wall, doorH, cl), concreteDark);
            Box(t, new Vector3(hw + wall * 0.5f, doorH * 0.5f, cz), new Vector3(wall, doorH, cl), concreteDark);
            Box(t, new Vector3(0f, doorH + 0.15f, cz), new Vector3(doorW + wall * 2f, 0.3f, cl), concreteDark);
            Box(t, new Vector3(0f, 0.02f, (F + roomBack) * 0.5f), new Vector3(roomHalf * 2f + wall * 2f, 0.04f, F - roomBack), concreteDark, false); // floor
            float rz = (roomFront + roomBack) * 0.5f, rl = roomFront - roomBack;
            Box(t, new Vector3(-roomHalf - wall * 0.5f, roomH * 0.5f, rz), new Vector3(wall, roomH, rl), concreteDark);
            Box(t, new Vector3(roomHalf + wall * 0.5f, roomH * 0.5f, rz), new Vector3(wall, roomH, rl), concreteDark);
            // The back wall, a doorway through it to the hallway.
            {
                float dw = BunkerPlan.DoorWidth, dh = BunkerPlan.DoorHeight;
                float dx = BunkerPlan.HallX, l = -roomHalf - wall, r = roomHalf + wall;
                float a = dx - dw * 0.5f, b = dx + dw * 0.5f;
                Box(t, new Vector3((l + a) * 0.5f, roomH * 0.5f, roomBack - wall * 0.5f), new Vector3(a - l, roomH, wall), concreteDark);
                Box(t, new Vector3((b + r) * 0.5f, roomH * 0.5f, roomBack - wall * 0.5f), new Vector3(r - b, roomH, wall), concreteDark);
                Box(t, new Vector3(dx, (dh + roomH) * 0.5f, roomBack - wall * 0.5f), new Vector3(dw, roomH - dh, wall), concreteDark);
            }
            BunkerHallway(t, rng);
            float flank = roomHalf - hw;
            Box(t, new Vector3(-(hw + flank * 0.5f), roomH * 0.5f, roomFront + wall * 0.5f), new Vector3(flank, roomH, wall), concreteDark);
            Box(t, new Vector3(hw + flank * 0.5f, roomH * 0.5f, roomFront + wall * 0.5f), new Vector3(flank, roomH, wall), concreteDark);
            Box(t, new Vector3(0f, (doorH + roomH) * 0.5f, roomFront + wall * 0.5f), new Vector3(doorW, roomH - doorH, wall), concreteDark);
            Box(t, new Vector3(0f, roomH + 0.15f, rz), new Vector3(roomHalf * 2f + wall * 2f, 0.3f, rl + wall * 2f), concreteDark);

            // What's left in it: the crate (its loot goes on top), a drum, some rubble.
            Box(t, PlaceFittings.BunkerCrateSpot + new Vector3(0f, 0.37f, 0f), new Vector3(1f, 0.74f, 0.6f), wood);
            for (int i = 0; i < 3; i++) Box(t, PlaceFittings.BunkerCrateSpot + new Vector3(0f, 0.15f + i * 0.22f, 0.305f), new Vector3(1.02f, 0.04f, 0.02f), darkWood, false);
            BurnBarrel(t, new Vector3(-roomHalf + 0.45f, 0f, roomFront - 0.55f), rng);
            for (int i = 0; i < 9; i++)
            {
                float sz = R(0.08f, 0.25f);
                var lump = Box(t, new Vector3(R(-roomHalf + 0.3f, roomHalf - 0.3f), sz * 0.3f, R(roomBack + 0.3f, F - faceD - 0.3f)), new Vector3(sz, sz * 0.6f, sz * R(0.7f, 1.3f)), concrete, false);
                lump.transform.localRotation = Quaternion.Euler(R(-20f, 20f), R(0f, 360f), R(-20f, 20f));
            }

            // The earth heaped over it: high and flat over the rooms (so nothing pokes out), falling away round
            // the sides and back, banked against the front's sides, open in front of the doorway.
            EarthMound(t, new Vector2(roomHalf + wall + 0.3f, F - 0.05f), BunkerPlan.StairsEnd - wall - 0.3f, faceH, F - faceD, faceW * 0.5f + 0.1f, rng);

            PlaceFittings.AddShelter(t, "Bunker", new Bounds(new Vector3(0f, roomH * 0.5f, (F - faceD + roomBack) * 0.5f), new Vector3(roomHalf * 2f, roomH, F - faceD - roomBack)));
        }

        // The hallway back from the room and the stairs at its end (see BunkerPlan for where; the ground's dug out
        // for them). Concrete all round, a dead lamp in a wire cage every few metres along the ceiling, the old
        // steel door off its hinges leant against the wall; the stairs solid blocks of concrete under an invisible
        // ramp (so you walk down them smoothly, not in a jolt at every step); a black fog filling the stairwell from
        // the top step down, the same all through it, so the steps fade to black the same few metres ahead of you
        // wherever you are on them; and a StairLoop to take you back up without your knowing.
        void BunkerHallway(Transform t, System.Random rng)
        {
            float R(float a, float b) => a + (b - a) * (float)rng.NextDouble();
            const float wall = 0.3f;
            float x = BunkerPlan.HallX, half = BunkerPlan.HallHalf, height = BunkerPlan.HallHeight;
            float z0 = BunkerPlan.HallStart, z1 = BunkerPlan.HallEnd, len = z0 - z1;
            // The hallway.
            // (Its floor solid itself: the ground's dug away under its last stretch, for the stairs.)
            Box(t, new Vector3(x, -0.08f, (z0 + z1) * 0.5f), new Vector3(half * 2f, 0.24f, len), concreteDark);
            for (int side = -1; side <= 1; side += 2)
                Box(t, new Vector3(x + side * (half + wall * 0.5f), height * 0.5f, (z0 + z1) * 0.5f), new Vector3(wall, height, len), concreteDark);
            Box(t, new Vector3(x, height + 0.15f, (z0 + z1) * 0.5f), new Vector3(half * 2f + wall * 2f, 0.3f, len), concreteDark);
            for (float z = z0 - 1.5f; z > z1 + 0.8f; z -= 2.6f)
            {
                Shape(PrimitiveType.Sphere, t, new Vector3(x, height - 0.12f, z), new Vector3(0.12f, 0.16f, 0.12f), Keep(RuntimeMaterials.Lit("Dead bulb", new Color(0.12f, 0.11f, 0.09f), 0.8f)), false);
                for (int k = 0; k < 4; k++)
                {
                    float ang = k * Mathf.PI * 0.5f;
                    Rod(t, new Vector3(x + Mathf.Cos(ang) * 0.1f, height, z + Mathf.Sin(ang) * 0.1f), new Vector3(x + Mathf.Cos(ang) * 0.09f, height - 0.24f, z + Mathf.Sin(ang) * 0.09f), 0.005f, rust);
                }
                Rod(t, new Vector3(x - half, height - 0.04f, z), new Vector3(x + half, height - 0.04f, z), 0.012f, rust); // (a conduit across)
            }
            // The steel door, off its hinges, leant against the left wall just in from the room.
            var door = Box(t, new Vector3(x - half + 0.15f, 1.0f, z0 - 1.0f), new Vector3(0.06f, 2.0f, 0.95f), rust, false);
            door.transform.localRotation = Quaternion.Euler(0f, 0f, -8f);
            // Rubble and old papers down its length.
            for (int i = 0; i < 6; i++)
            {
                float sz = R(0.06f, 0.18f);
                var lump = Box(t, new Vector3(x + R(-half + 0.1f, half - 0.1f), sz * 0.3f, R(z1 + 0.5f, z0 - 0.5f)), new Vector3(sz, sz * 0.6f, sz), concrete, false);
                lump.transform.localRotation = Quaternion.Euler(R(-20f, 20f), R(0f, 360f), R(-20f, 20f));
            }

            // The stairs: solid blocks, each step's top a rise lower and a run further.
            float rise = BunkerPlan.Rise, run = BunkerPlan.Run;
            int steps = BunkerPlan.Steps;
            var stepMat = concreteDark;
            for (int k = 1; k <= steps; k++)
            {
                float top = -k * rise, zc = z1 - (k - 0.5f) * run;
                Box(t, new Vector3(x, top - 0.4f, zc), new Vector3(half * 2f, 0.8f, run + 0.005f), stepMat, false);
                Box(t, new Vector3(x, top - 0.012f, zc + run * 0.5f - 0.03f), new Vector3(half * 2f, 0.025f, 0.06f), concrete, false); // (the worn nosing)
            }
            // The stairwell: walls the whole way down, a ceiling sloping down over the steps, black at the bottom.
            float bottom = BunkerPlan.Bottom, zEnd = BunkerPlan.StairsEnd, sl = z1 - zEnd;
            for (int side = -1; side <= 1; side += 2)
                Box(t, new Vector3(x + side * (half + wall * 0.5f), (height + bottom - 1f) * 0.5f, (z1 + zEnd) * 0.5f), new Vector3(wall, height - bottom + 1f, sl), concreteDark);
            float slope = Mathf.Atan2(rise, run) * Mathf.Rad2Deg, along = Mathf.Sqrt(sl * sl + bottom * bottom);
            var ceiling = Box(t, new Vector3(x, height + 0.15f + bottom * 0.5f, (z1 + zEnd) * 0.5f), new Vector3(half * 2f + wall * 2f, 0.3f, along), concreteDark);
            ceiling.transform.localRotation = Quaternion.Euler(-slope, 0f, 0f);
            Box(t, new Vector3(x, bottom * 0.5f, zEnd - 0.2f), new Vector3(half * 2f, height - bottom + 1f, 0.4f), Keep(RuntimeMaterials.Unlit("Nothing", Color.black, false)));
            // The ramp you actually walk on (unseen), along the middles of the steps' tops.
            var ramp = Box(t, new Vector3(x, -0.1f + bottom * 0.5f - rise * 0.5f, (z1 + zEnd) * 0.5f), new Vector3(half * 2f, 0.2f, along), concreteDark);
            ramp.transform.localRotation = Quaternion.Euler(-slope, 0f, 0f);
            ramp.GetComponent<Renderer>().enabled = false;

            // The dark: a black fog all through the stairwell from the top step down.
            var fogGo = new GameObject("Stairwell dark");
            fogGo.transform.SetParent(t, false);
            fogGo.transform.localPosition = new Vector3(x, (height + bottom) * 0.5f, (z1 + zEnd) * 0.5f - 0.1f);
            var fog = fogGo.AddComponent<UnityEngine.Rendering.HighDefinition.LocalVolumetricFog>();
            fog.parameters.albedo = Color.black;
            fog.parameters.meanFreePath = 1.3f;
            fog.parameters.size = new Vector3(half * 2f + 0.2f, height - bottom + 0.4f, sl + 0.2f);
            fog.parameters.blendingMode = UnityEngine.Rendering.HighDefinition.LocalVolumetricFogBlendingMode.Additive;
            fog.parameters.positiveFade = new Vector3(0f, 0f, 0.6f / (sl + 0.2f)); // (thinning out just over the top step)
            fog.parameters.negativeFade = Vector3.zero;

            // Going down... and never getting any further.
            var loop = t.gameObject.AddComponent<StairLoop>();
            loop.Set(new Vector3(x, 0f, z1), half, rise, run);
        }

        // An old oil drum made a burn barrel (light it with a lighter: see World.BurnBarrel): ribbed steel gone to
        // rust, a band of its old olive paint flaking off, rust running down it in streaks, a dent in its side, its
        // lid cut off and the rim left ragged, a ring of holes punched round its foot for air, split wood and
        // rubbish heaped inside, a scorch on the floor round it.
        void BurnBarrel(Transform t, Vector3 at, System.Random rng)
        {
            float R(float a, float b) => a + (b - a) * (float)rng.NextDouble();
            const float r = 0.29f, hgt = 0.88f;
            var root = new GameObject("Burn barrel") { layer = Layers.Props }.transform;
            root.SetParent(t, false);
            root.localPosition = at;
            root.localRotation = Quaternion.Euler(0f, R(0f, 360f), 0f);
            var paint = Keep(RuntimeMaterials.Lit("Drum paint", new Color(0.13f, 0.15f, 0.08f), 0.3f));
            var streak = Keep(RuntimeMaterials.Lit("Rust streak", new Color(0.24f, 0.09f, 0.03f), 0.2f));
            var soot = Keep(RuntimeMaterials.Lit("Soot", new Color(0.015f, 0.013f, 0.012f), 0.1f));
            var dark = Keep(RuntimeMaterials.Lit("Inside the drum", new Color(0.02f, 0.015f, 0.012f), 0.1f));
            var wood = Keep(RuntimeMaterials.Lit("Split wood", new Color(0.22f, 0.16f, 0.1f), 0.1f));
            var charred = Keep(RuntimeMaterials.Lit("Charred wood", new Color(0.03f, 0.025f, 0.02f), 0.15f));
            // The drum: its wall (what you click), the rolling hoops and the chimes top and bottom.
            Shape(PrimitiveType.Cylinder, root, new Vector3(0f, hgt * 0.5f, 0f), new Vector3(r * 2f, hgt * 0.5f, r * 2f), rust, true);
            foreach (float y in new[] { hgt * 0.34f, hgt * 0.66f })
                Shape(PrimitiveType.Cylinder, root, new Vector3(0f, y, 0f), new Vector3(r * 2f + 0.025f, 0.012f, r * 2f + 0.025f), rust, false);
            foreach (float y in new[] { 0.015f, hgt - 0.012f })
                Shape(PrimitiveType.Cylinder, root, new Vector3(0f, y, 0f), new Vector3(r * 2f + 0.03f, 0.014f, r * 2f + 0.03f), rust, false);
            // The paint band, flaking: patches of it left round the upper part.
            for (int i = 0; i < 9; i++)
            {
                float a = R(0f, Mathf.PI * 2f), w = R(0.06f, 0.16f), h = R(0.05f, 0.14f);
                var patch = Shape(PrimitiveType.Cube, root, new Vector3(Mathf.Cos(a) * (r + 0.004f), R(0.5f, 0.78f), Mathf.Sin(a) * (r + 0.004f)), new Vector3(w, h, 0.004f), paint, false);
                patch.transform.localRotation = Quaternion.LookRotation(new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)));
            }
            // Rust running down it, and soot up over the rim.
            for (int i = 0; i < 7; i++)
            {
                float a = R(0f, Mathf.PI * 2f), len = R(0.15f, 0.5f);
                var s = Shape(PrimitiveType.Cube, root, new Vector3(Mathf.Cos(a) * (r + 0.006f), hgt - 0.05f - len * 0.5f, Mathf.Sin(a) * (r + 0.006f)), new Vector3(R(0.015f, 0.04f), len, 0.004f), streak, false);
                s.transform.localRotation = Quaternion.LookRotation(new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)));
            }
            for (int i = 0; i < 10; i++)
            {
                float a = i / 10f * Mathf.PI * 2f + R(-0.1f, 0.1f);
                var s = Shape(PrimitiveType.Cube, root, new Vector3(Mathf.Cos(a) * (r + 0.005f), hgt - 0.07f, Mathf.Sin(a) * (r + 0.005f)), new Vector3(0.2f, R(0.08f, 0.16f), 0.004f), soot, false);
                s.transform.localRotation = Quaternion.LookRotation(new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)));
            }
            // A dent in its side.
            var dent = Shape(PrimitiveType.Sphere, root, new Vector3(r * 0.93f, 0.32f, 0.05f), new Vector3(0.05f, 0.16f, 0.14f), soot, false);
            dent.GetComponent<Renderer>().sharedMaterial = streak;
            // The rim, cut ragged: teeth of the old lid standing up round it.
            for (int i = 0; i < 14; i++)
            {
                float a = i / 14f * Mathf.PI * 2f;
                if (rng.NextDouble() < 0.4) continue;
                var tooth = Shape(PrimitiveType.Cube, root, new Vector3(Mathf.Cos(a) * (r - 0.005f), hgt + R(0.01f, 0.03f), Mathf.Sin(a) * (r - 0.005f)), new Vector3(0.09f, R(0.02f, 0.06f), 0.006f), rust, false);
                tooth.transform.localRotation = Quaternion.LookRotation(new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a))) * Quaternion.Euler(R(-15f, 15f), 0f, 0f);
            }
            // Its dark inside (seen over the rim), and the wood and rubbish heaped in it.
            Shape(PrimitiveType.Cylinder, root, new Vector3(0f, hgt - 0.1f, 0f), new Vector3(r * 2f - 0.02f, 0.09f, r * 2f - 0.02f), dark, false);
            var fuel = new List<Renderer>();
            for (int i = 0; i < 7; i++)
            {
                var stick = Shape(PrimitiveType.Cylinder, root, new Vector3(R(-0.12f, 0.12f), hgt + R(-0.02f, 0.08f), R(-0.12f, 0.12f)), new Vector3(R(0.04f, 0.07f), R(0.14f, 0.24f), R(0.04f, 0.07f)), wood, false);
                stick.transform.localRotation = Quaternion.Euler(R(40f, 80f), R(0f, 360f), 0f);
                fuel.Add(stick.GetComponent<Renderer>());
            }
            for (int i = 0; i < 3; i++)
            {
                var junk = Shape(PrimitiveType.Cube, root, new Vector3(R(-0.1f, 0.1f), hgt - 0.02f, R(-0.1f, 0.1f)), new Vector3(R(0.08f, 0.15f), 0.01f, R(0.08f, 0.13f)), Keep(RuntimeMaterials.Lit("Old paper", new Color(0.4f, 0.37f, 0.3f), 0.1f)), false);
                junk.transform.localRotation = Quaternion.Euler(R(-20f, 20f), R(0f, 360f), R(-20f, 20f));
                fuel.Add(junk.GetComponent<Renderer>());
            }
            // The holes punched round its foot.
            var vents = new List<Renderer>();
            for (int i = 0; i < 8; i++)
            {
                float a = (i + 0.5f) / 8f * Mathf.PI * 2f;
                var hole = Shape(PrimitiveType.Cube, root, new Vector3(Mathf.Cos(a) * (r + 0.003f), 0.12f, Mathf.Sin(a) * (r + 0.003f)), new Vector3(0.05f, 0.035f, 0.004f), dark, false);
                hole.transform.localRotation = Quaternion.LookRotation(new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)));
                vents.Add(hole.GetComponent<Renderer>());
            }
            // A scorch on the floor round it.
            Shape(PrimitiveType.Cylinder, root, new Vector3(0f, 0.045f, 0f), new Vector3(0.95f, 0.002f, 0.95f), soot, false);

            var barrel = root.gameObject.AddComponent<Game.World.BurnBarrel>();
            barrel.top = hgt;
            barrel.vents = vents.ToArray();
            barrel.fuel = fuel.ToArray();
            barrel.charred = charred;
        }

        // The two bulkhead lamps either side of the door (at these points on the front, local): each an oval black
        // housing on the concrete, a frosted ribbed glass dome, and a black cage over it (a rim round the oval, a
        // bar down the middle and two across), screws top and bottom. They swell green and red in turn, slowly,
        // out of step (see World.BunkerLights).
        void BunkerLamps(Transform t, Vector3[] spots)
        {
            var housing = Keep(RuntimeMaterials.Lit("Lamp housing", new Color(0.02f, 0.02f, 0.022f), 0.45f));
            var glassMats = new Material[spots.Length];
            var lights = new Light[spots.Length];
            const float hx = 0.11f, hy = 0.17f; // (half the oval's width and height)
            for (int i = 0; i < spots.Length; i++)
            {
                var lamp = new GameObject("Bulkhead lamp") { layer = Layers.Props }.transform;
                lamp.SetParent(t, false);
                lamp.localPosition = spots[i];
                // The housing: an oval back plate standing off the wall, and its rim.
                Shape(PrimitiveType.Cylinder, lamp, new Vector3(0f, 0f, 0.03f), new Vector3(hx * 2f + 0.05f, 0.03f, hy * 2f + 0.05f), housing, false).transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                // The frosted glass: half an oval dome (bright when it's lit).
                glassMats[i] = Keep(RuntimeMaterials.LitGlow("Bulkhead glass", new Color(0.75f, 0.74f, 0.7f), Color.black));
                Shape(PrimitiveType.Sphere, lamp, new Vector3(0f, 0f, 0.06f), new Vector3(hx * 2f, hy * 2f, 0.12f), glassMats[i], false);
                // The cage: a rim round the oval (short bars), a bar down the middle, two across.
                const int segs = 16;
                for (int k = 0; k < segs; k++)
                {
                    float a0 = k / (float)segs * Mathf.PI * 2f, a1 = (k + 1) / (float)segs * Mathf.PI * 2f;
                    Rod(lamp, new Vector3(Mathf.Cos(a0) * hx, Mathf.Sin(a0) * hy, 0.07f), new Vector3(Mathf.Cos(a1) * hx, Mathf.Sin(a1) * hy, 0.07f), 0.009f, housing);
                }
                // (The bars bow out over the dome.)
                for (int k = 0; k < 6; k++)
                {
                    float y0 = Mathf.Lerp(-hy, hy, k / 6f), y1 = Mathf.Lerp(-hy, hy, (k + 1) / 6f);
                    float z0 = 0.07f + 0.06f * Mathf.Sqrt(Mathf.Max(0f, 1f - (y0 / hy) * (y0 / hy))), z1 = 0.07f + 0.06f * Mathf.Sqrt(Mathf.Max(0f, 1f - (y1 / hy) * (y1 / hy)));
                    Rod(lamp, new Vector3(0f, y0, z0), new Vector3(0f, y1, z1), 0.008f, housing);
                }
                foreach (float y in new[] { -hy * 0.42f, hy * 0.42f })
                {
                    float w = hx * Mathf.Sqrt(1f - (y / hy) * (y / hy));
                    for (int k = 0; k < 4; k++)
                    {
                        float x0 = Mathf.Lerp(-w, w, k / 4f), x1 = Mathf.Lerp(-w, w, (k + 1) / 4f);
                        float z0 = 0.07f + 0.05f * Mathf.Sqrt(Mathf.Max(0f, 1f - (x0 / w) * (x0 / w))), z1 = 0.07f + 0.05f * Mathf.Sqrt(Mathf.Max(0f, 1f - (x1 / w) * (x1 / w)));
                        Rod(lamp, new Vector3(x0, y, z0), new Vector3(x1, y, z1), 0.008f, housing);
                    }
                }
                foreach (float y in new[] { -hy - 0.012f, hy + 0.012f })
                    Shape(PrimitiveType.Cylinder, lamp, new Vector3(0f, y, 0.065f), new Vector3(0.022f, 0.004f, 0.022f), housing, false).transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                // Its light, out from the glass.
                var lightGo = new GameObject("Bulkhead light");
                lightGo.transform.SetParent(lamp, false);
                lightGo.transform.localPosition = new Vector3(0f, 0f, 0.25f);
                UnityEngine.Rendering.HighDefinition.GameObjectExtension.AddHDLight(lightGo, LightType.Point);
                var l = lightGo.GetComponent<Light>();
                l.shadows = LightShadows.None;
                l.enabled = false;
                lights[i] = l;
            }
            t.gameObject.AddComponent<Game.World.BunkerLights>().Set(glassMats, lights);
        }

        // A heightfield of earth over the bunker: full height over the box (|x| < half.x, back < z < half.y),
        // easing down to the ground over 3.2 m round it, a little lumpy; nothing in front of the doorway, and a
        // hole over the front's face where the doorway is (the capping slab covers it). (Before, the earth ran
        // straight from full height down to the ground in front, and with its points half a metre apart that
        // slope stood right across the doorway, inside the frame: a wall of dirt where the way in should be.)
        // (faceBack, faceHalf: the front's back and half its width. No earth at all over the front, or in front of
        // it: before, with the mound's points half a metre apart, its last slope from full height down to the
        // ground straddled the front and stood inside the doorway's frame, still blocking the way in.)
        void EarthMound(Transform t, Vector2 half, float back, float height, float faceBack, float faceHalf, System.Random rng)
        {
            const float step = 0.5f, fall = 3.2f;
            float x0 = -half.x - fall - 0.5f, x1 = half.x + fall + 0.5f, z0 = back - fall - 0.5f, z1 = half.y + fall;
            int nx = Mathf.CeilToInt((x1 - x0) / step), nz = Mathf.CeilToInt((z1 - z0) / step);
            float seed = (float)rng.NextDouble() * 100f;
            // Over the front and in front of it (the front and its capping slab close what's left above).
            bool OverFront(Vector3 v) => Mathf.Abs(v.x) < faceHalf && v.z > faceBack + 0.05f;
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
                // A square reaching onto the front: left out.
                if (OverFront(verts[a]) || OverFront(verts[b]) || OverFront(verts[c]) || OverFront(verts[d])) continue;
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
                Color c = Color.Lerp(dark, baseC * 1.15f, MathUtil.SmoothThreshold(0.15f, 0.85f, blotch));
                // Rain streaks: running down from the top, fading as they go.
                float run = streak[x] * Mathf.Lerp(1f, 0.3f, 1f - v) * (0.6f + 0.4f * Mathf.PerlinNoise(u * 40f, v * 3f + o));
                c = Color.Lerp(c, dark * 0.8f, Mathf.Clamp01(run * 1.4f));
                // Moss low down.
                float m = Mathf.Clamp01((0.35f - v) / 0.35f) * Mathf.PerlinNoise(u * 12f + o, v * 12f + 7f);
                c = Color.Lerp(c, moss, MathUtil.SmoothThreshold(0.25f, 0.6f, m));
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

        // The giant oak (the plan's "7x scale"), a landmark you can find your way by.
        void LandmarkTree(Transform t)
        {
            var (trunk, crown) = ForestMeshes.Oak(new System.Random(7), 14f);
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
            // An old, grand door standing on its own: a worn stone step, carved posts on plinths with capitals, a
            // heavy lintel and cornice, and an arch of stones over it with a keystone; the door itself panelled,
            // studded, hung on long iron straps, with a ring knocker and a keyhole. (The opening's 1.16 x 2.2.)
            var doorStone = Keep(RuntimeMaterials.Lit("Door stone", new Color(0.2f, 0.195f, 0.185f), 0.2f));
            var iron = Keep(RuntimeMaterials.Lit("Black iron", new Color(0.035f, 0.033f, 0.032f), 0.45f));
            var brass = Keep(RuntimeMaterials.Lit("Old brass", new Color(0.32f, 0.23f, 0.1f), 0.6f));
            // (The threshold's flush with the ground: the doorway, its window into the room and the room's own
            // doorway all start at the ground.)
            Box(t, new Vector3(0f, 0.015f, 0f), new Vector3(2.3f, 0.03f, 1.05f), doorStone);              // the threshold slab
            foreach (int side in new[] { -1, 1 })
            {
                float x = side * 0.7f;
                Box(t, new Vector3(x, 0.18f, 0f), new Vector3(0.3f, 0.3f, 0.34f), doorStone);             // plinth
                Box(t, new Vector3(x, 1.255f, 0f), new Vector3(0.2f, 1.86f, 0.26f), darkWood);            // post
                for (int f = -1; f <= 1; f += 2) Box(t, new Vector3(x + f * 0.055f, 1.3f, 0.131f), new Vector3(0.025f, 1.6f, 0.012f), wood, false); // fluting
                for (int f = -1; f <= 1; f += 2) Box(t, new Vector3(x + f * 0.055f, 1.3f, -0.131f), new Vector3(0.025f, 1.6f, 0.012f), wood, false);
                Box(t, new Vector3(x, 2.24f, 0f), new Vector3(0.3f, 0.12f, 0.34f), doorStone);            // capital
            }
            Box(t, new Vector3(0f, 2.39f, 0f), new Vector3(1.76f, 0.18f, 0.32f), darkWood);              // lintel
            Box(t, new Vector3(0f, 2.51f, 0f), new Vector3(1.92f, 0.07f, 0.38f), doorStone);             // cornice
            // The arch over it: wedge stones round a half circle, the keystone at the top standing proud.
            const float archR = 0.72f, archY = 2.55f;
            for (int k = 0; k <= 8; k++)
            {
                float a = k / 8f * Mathf.PI;
                bool key = k == 4;
                var wedge = Box(t, new Vector3(Mathf.Cos(a) * archR, archY + Mathf.Sin(a) * archR, 0f), new Vector3(key ? 0.24f : 0.2f, key ? 0.3f : 0.22f, key ? 0.36f : 0.3f), doorStone, false);
                wedge.transform.localRotation = Quaternion.Euler(0f, 0f, a * Mathf.Rad2Deg - 90f);
            }
            Box(t, new Vector3(0f, archY + 0.22f, 0f), new Vector3(1.2f, 0.42f, 0.16f), darkWood, false); // the tympanum, under the arch
            // The door itself hangs from the left post.
            var hinge = new GameObject("Door") { layer = Layers.Props }.transform;
            hinge.SetParent(t, false);
            hinge.localPosition = new Vector3(-0.58f, 0f, 0f);
            Box(hinge, new Vector3(0.58f, 1.1f, 0f), new Vector3(1.16f, 2.2f, 0.07f), wood);
            foreach (int face in new[] { 1, -1 })
            {
                float z = face * 0.042f;
                // Four raised panels.
                foreach (var (px, py, ph) in new[] { (0.31f, 0.55f, 0.75f), (0.85f, 0.55f, 0.75f), (0.31f, 1.55f, 0.95f), (0.85f, 1.55f, 0.95f) })
                {
                    Box(hinge, new Vector3(px, py, z), new Vector3(0.42f, ph, 0.016f), darkWood, false);
                    Box(hinge, new Vector3(px, py, z + face * 0.01f), new Vector3(0.32f, ph - 0.1f, 0.012f), wood, false);
                }
                // Iron straps from the hinge side, studded.
                foreach (float y in new[] { 0.32f, 1.88f })
                {
                    Box(hinge, new Vector3(0.42f, y, z + face * 0.02f), new Vector3(0.84f, 0.06f, 0.012f), iron, false);
                    for (int st = 0; st < 5; st++)
                        Shape(PrimitiveType.Sphere, hinge, new Vector3(0.08f + st * 0.18f, y, z + face * 0.028f), Vector3.one * 0.028f, iron, false);
                }
                // The ring knocker on its boss, and the keyhole plate (the keyhole lets light through later).
                Shape(PrimitiveType.Sphere, hinge, new Vector3(0.58f, 1.32f, z + face * 0.025f), new Vector3(0.07f, 0.07f, 0.03f), brass, false);
                var ring = Shape(PrimitiveType.Cylinder, hinge, new Vector3(0.58f, 1.24f, z + face * 0.034f), new Vector3(0.16f, 0.004f, 0.16f), brass, false);
                ring.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                Box(hinge, new Vector3(1.0f, 1.02f, z + face * 0.022f), new Vector3(0.06f, 0.15f, 0.008f), brass, false);
                var keyhole = Box(hinge, new Vector3(1.0f, 1.0f, z + face * 0.027f), new Vector3(0.012f, 0.035f, 0.004f), iron, false);
                keyhole.name = "Keyhole";
            }
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
