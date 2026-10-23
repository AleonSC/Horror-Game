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

        public GameObject Structure(StructureSite site, float ground, Transform parent)
        {
            var prefab = PrefabFor(site.Kind);
            if (prefab != null)
            {
                var placed = Object.Instantiate(prefab, new Vector3(site.Position.x, ground, site.Position.y), Quaternion.Euler(0f, site.Yaw, 0f), parent);
                placed.name = site.Kind.ToString();
                return placed;
            }
            var root = new GameObject(site.Kind.ToString()) { layer = Layers.Props };
            root.transform.SetParent(parent, false);
            root.transform.SetPositionAndRotation(new Vector3(site.Position.x, ground, site.Position.y), Quaternion.Euler(0f, site.Yaw, 0f));
            var t = root.transform;
            switch (site.Kind)
            {
                case StructureKind.Cabin:
                    Box(t, new Vector3(0f, 1.6f, 0f), new Vector3(8f, 3.2f, 7f), wood);
                    Box(t, new Vector3(0f, 3.5f, 0f), new Vector3(8.6f, 0.6f, 7.6f), darkWood);
                    Box(t, new Vector3(0f, 1.1f, 3.52f), new Vector3(1.1f, 2.1f, 0.08f), darkWood, false); // door
                    break;
                case StructureKind.Shack:
                    Box(t, new Vector3(0f, 1.4f, 0f), new Vector3(4f, 2.8f, 5f), wood);
                    Box(t, new Vector3(0f, 3.0f, 0f), new Vector3(4.5f, 0.4f, 5.5f), darkWood);
                    Box(t, new Vector3(0f, 1.05f, 2.52f), new Vector3(1f, 2f, 0.08f), darkWood, false);
                    break;
                case StructureKind.Campsite:
                    var tent = Box(t, new Vector3(-1.5f, 0.7f, 0f), new Vector3(1.4f, 1.4f, 2.4f), canvas);
                    tent.transform.localRotation = Quaternion.Euler(0f, 0f, 45f);
                    for (int i = 0; i < 8; i++)
                    {
                        float a = i / 8f * Mathf.PI * 2f;
                        Shape(PrimitiveType.Sphere, t, new Vector3(1.5f + Mathf.Cos(a) * 0.5f, 0.08f, Mathf.Sin(a) * 0.5f), Vector3.one * 0.2f, stone, false);
                    }
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
            return root;
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
                return placed;
            }
            var root = new GameObject("Lone Door") { layer = Layers.Props };
            root.transform.SetParent(parent, false);
            root.transform.SetPositionAndRotation(new Vector3(position.x, ground, position.y), Quaternion.Euler(0f, yaw, 0f));
            var t = root.transform;
            Box(t, new Vector3(-0.65f, 1.15f, 0f), new Vector3(0.14f, 2.3f, 0.2f), darkWood);
            Box(t, new Vector3(0.65f, 1.15f, 0f), new Vector3(0.14f, 2.3f, 0.2f), darkWood);
            Box(t, new Vector3(0f, 2.36f, 0f), new Vector3(1.44f, 0.14f, 0.2f), darkWood);
            Box(t, new Vector3(0f, 1.1f, 0f), new Vector3(1.16f, 2.2f, 0.06f), wood);
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
