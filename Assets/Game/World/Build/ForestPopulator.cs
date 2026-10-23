using System.Collections.Generic;
using Game.Core;
using Game.Rendering;
using Game.World.Generation;
using UnityEngine;

namespace Game.World.Build
{
    /// <summary>
    /// Fills the world from the plan: every tree, bush, grass clump, rock, boulder and log drawn by the instanced
    /// renderer, and colliders for what you can bump into (trunks, big rocks, boulders, logs) on their chunks.
    /// </summary>
    public sealed class ForestPopulator
    {
        const float GrassRange = 45f, TallGrassRange = 60f, BushRange = 65f, RockRange = 75f, TreeRange = 115f;
        static readonly float[] TreeLods = { 32f, 62f };

        readonly WorldPlan plan;
        readonly InstancedRenderer renderer;
        readonly List<Transform> chunks;
        readonly List<Object> made;
        readonly int across;

        public Material Bark, Needles, DeadWood;
        Material bushA, bushB, grassDry, grassGreen, tallGrass, rock, logWood;

        public ForestPopulator(WorldPlan plan, InstancedRenderer renderer, List<Transform> chunks, List<Object> made)
        {
            this.plan = plan;
            this.renderer = renderer;
            this.chunks = chunks;
            this.made = made;
            across = Mathf.CeilToInt(plan.Size / TerrainBuilder.ChunkSize);
            Bark = Mat("Bark", new Color(0.1f, 0.08f, 0.07f), 0.2f, false);
            Needles = Mat("Needles", new Color(0.05f, 0.07f, 0.05f), 0.1f, true);
            DeadWood = Mat("Dead wood", new Color(0.14f, 0.12f, 0.1f), 0.15f, false);
            bushA = Mat("Bush leaves", new Color(0.06f, 0.08f, 0.05f), 0.15f, false);
            bushB = Mat("Bush leaves (dry)", new Color(0.1f, 0.09f, 0.05f), 0.15f, false);
            grassDry = Mat("Dry grass", new Color(0.24f, 0.2f, 0.12f), 0.1f, true);
            grassGreen = Mat("Grass", new Color(0.09f, 0.11f, 0.06f), 0.1f, true);
            tallGrass = Mat("Tall grass", new Color(0.17f, 0.16f, 0.09f), 0.1f, true);
            rock = Mat("Rock", new Color(0.18f, 0.18f, 0.17f), 0.25f, false);
            logWood = Mat("Log", new Color(0.12f, 0.09f, 0.07f), 0.2f, false);
        }

        Material Mat(string name, Color c, float smooth, bool doubleSided)
        {
            var m = RuntimeMaterials.LitTextured(name, c, smooth, null, Vector2.one, doubleSided);
            made.Add(m);
            return m;
        }

        Mesh Keep(Mesh m)
        {
            made.Add(m);
            return m;
        }

        public void Populate()
        {
            AddTrees();
            AddBushes();
            AddGrass();
            AddRocksAndLogs();
        }

        void AddTrees()
        {
            var pineTrunk = new int[TreeShapes.PineVariants];
            var pineCrown = new int[TreeShapes.PineVariants];
            for (int v = 0; v < TreeShapes.PineVariants; v++)
            {
                var trunks = new Mesh[3];
                var crowns = new Mesh[3];
                for (int lod = 0; lod < 3; lod++)
                {
                    var (t, c) = ForestMeshes.Pine(new System.Random(plan.Seed * 31 + v), TreeShapes.Height(TreeKind.Pine, v), lod);
                    trunks[lod] = Keep(t.ToMesh($"Pine {v} trunk LOD{lod}"));
                    crowns[lod] = Keep(c.ToMesh($"Pine {v} crown LOD{lod}"));
                }
                pineTrunk[v] = renderer.AddKind(new InstancedRenderer.Kind { Name = $"Pine trunk {v}", Lods = trunks, LodDistances = TreeLods, MaxDistance = TreeRange, Material = Bark, Shadows = true, Layer = Layers.Trees });
                pineCrown[v] = renderer.AddKind(new InstancedRenderer.Kind { Name = $"Pine crown {v}", Lods = crowns, LodDistances = TreeLods, MaxDistance = TreeRange, Material = Needles, Shadows = true, Layer = Layers.Trees });
            }
            var dead = new int[TreeShapes.DeadVariants];
            for (int v = 0; v < TreeShapes.DeadVariants; v++)
            {
                var lods = new Mesh[3];
                for (int lod = 0; lod < 3; lod++)
                    lods[lod] = Keep(ForestMeshes.DeadTree(new System.Random(plan.Seed * 37 + v), TreeShapes.Height(TreeKind.Dead, v), lod).ToMesh($"Dead tree {v} LOD{lod}"));
                dead[v] = renderer.AddKind(new InstancedRenderer.Kind { Name = $"Dead tree {v}", Lods = lods, LodDistances = TreeLods, MaxDistance = TreeRange, Material = DeadWood, Shadows = true, Layer = Layers.Trees });
            }

            foreach (var t in plan.Trees)
            {
                if (t.Removed) continue;
                var pos = Ground(t.Position);
                var m = Matrix4x4.TRS(pos, Quaternion.Euler(0f, t.Yaw, 0f), Vector3.one * t.Scale);
                if (t.Kind == TreeKind.Pine)
                {
                    renderer.Add(pineTrunk[t.Variant], t.Position, m);
                    renderer.Add(pineCrown[t.Variant], t.Position, m);
                }
                else renderer.Add(dead[t.Variant], t.Position, m);

                var col = Collider(t.Position, "Tree", Layers.Trees).AddComponent<CapsuleCollider>();
                col.radius = 0.3f * t.Scale;
                col.height = 6f * t.Scale;
                col.center = Vector3.up * 3f * t.Scale;
            }
        }

        void AddBushes()
        {
            var kinds = new int[6];
            for (int k = 0; k < 6; k++)
            {
                var mesh = Keep(ForestMeshes.Bush(new System.Random(plan.Seed * 41 + k), k / 2).ToMesh($"Bush {k}"));
                kinds[k] = renderer.AddKind(new InstancedRenderer.Kind { Name = $"Bush {k}", Lods = new[] { mesh }, LodDistances = new float[0], MaxDistance = BushRange, Material = k % 2 == 0 ? bushA : bushB, Shadows = true, Layer = Layers.Default });
            }
            foreach (var b in plan.Bushes)
                renderer.Add(kinds[b.Kind * 2 + b.Variant % 2], b.Position, Matrix4x4.TRS(Ground(b.Position), Quaternion.Euler(0f, b.Yaw, 0f), Vector3.one * b.Size));
        }

        void AddGrass()
        {
            var grass = new int[4];
            var tall = new int[4];
            for (int v = 0; v < 4; v++)
            {
                var g = Keep(ForestMeshes.GrassClump(new System.Random(plan.Seed * 43 + v), 1f).ToMesh($"Grass {v}"));
                grass[v] = renderer.AddKind(new InstancedRenderer.Kind { Name = $"Grass {v}", Lods = new[] { g }, LodDistances = new float[0], MaxDistance = GrassRange, Material = v < 2 ? grassDry : grassGreen, Shadows = false, Layer = Layers.Default });
                var tg = Keep(ForestMeshes.GrassClump(new System.Random(plan.Seed * 47 + v), 1f).ToMesh($"Tall grass {v}"));
                tall[v] = renderer.AddKind(new InstancedRenderer.Kind { Name = $"Tall grass {v}", Lods = new[] { tg }, LodDistances = new float[0], MaxDistance = TallGrassRange, Material = tallGrass, Shadows = false, Layer = Layers.Default });
            }
            foreach (var g in plan.Grass)
            {
                float w = 0.8f + 0.4f * g.Size;
                renderer.Add(grass[g.Variant % 4], g.Position, Matrix4x4.TRS(Ground(g.Position), Quaternion.Euler(0f, g.Yaw, 0f), new Vector3(w, g.Size, w)));
            }
            foreach (var g in plan.TallGrass)
                renderer.Add(tall[g.Variant % 4], g.Position, Matrix4x4.TRS(Ground(g.Position), Quaternion.Euler(0f, g.Yaw, 0f), new Vector3(1.3f, g.Size, 1.3f)));
        }

        void AddRocksAndLogs()
        {
            var rocks = new int[6];
            var boulders = new int[6];
            for (int v = 0; v < 6; v++)
            {
                var mesh = Keep(ForestMeshes.Rock(new System.Random(plan.Seed * 53 + v)).ToMesh($"Rock {v}"));
                rocks[v] = renderer.AddKind(new InstancedRenderer.Kind { Name = $"Rock {v}", Lods = new[] { mesh }, LodDistances = new float[0], MaxDistance = RockRange, Material = rock, Shadows = true, Layer = Layers.Props });
                boulders[v] = renderer.AddKind(new InstancedRenderer.Kind { Name = $"Boulder {v}", Lods = new[] { mesh }, LodDistances = new float[0], MaxDistance = TreeRange, Material = rock, Shadows = true, Layer = Layers.Props });
            }
            var logs = new int[3];
            for (int v = 0; v < 3; v++)
            {
                var mesh = Keep(ForestMeshes.Log(new System.Random(plan.Seed * 59 + v)).ToMesh($"Log {v}"));
                logs[v] = renderer.AddKind(new InstancedRenderer.Kind { Name = $"Log {v}", Lods = new[] { mesh }, LodDistances = new float[0], MaxDistance = RockRange, Material = logWood, Shadows = true, Layer = Layers.Props });
            }

            foreach (var r in plan.Rocks)
            {
                var pos = Ground(r.Position) + Vector3.down * 0.15f * r.Size; // sunk into the ground
                renderer.Add(rocks[r.Variant % 6], r.Position, Matrix4x4.TRS(pos, Quaternion.Euler(r.Variant * 7f, r.Yaw, 0f), Vector3.one * r.Size));
                if (r.Size < 0.8f) continue;
                var c = Collider(r.Position, "Rock", Layers.Props).AddComponent<SphereCollider>();
                c.radius = 0.4f * r.Size;
            }
            foreach (var b in plan.Boulders)
            {
                var pos = Ground(b.Position) + Vector3.down * 0.35f * b.Size; // half buried
                renderer.Add(boulders[b.Variant % 6], b.Position, Matrix4x4.TRS(pos, Quaternion.Euler(0f, b.Yaw, 0f), Vector3.one * b.Size));
                var c = Collider(b.Position, "Boulder", Layers.Props).AddComponent<SphereCollider>();
                c.radius = 0.42f * b.Size;
                c.center = Vector3.down * 0.3f * b.Size;
            }
            foreach (var l in plan.Logs)
            {
                var rot = Quaternion.Euler(0f, l.Yaw, 0f);
                renderer.Add(logs[l.Variant % 3], l.Position, Matrix4x4.TRS(Ground(l.Position), rot, new Vector3(1f, 1f, l.Size)));
                var go = Collider(l.Position, "Log", Layers.Props);
                go.transform.localRotation = rot;
                var c = go.AddComponent<CapsuleCollider>();
                c.direction = 2;
                c.radius = 0.25f;
                c.height = l.Size;
                c.center = Vector3.up * 0.2f;
            }
        }

        Vector3 Ground(Vector2 p) => new Vector3(p.x, plan.GroundAt(p), p.y);

        // An empty object on the chunk under this spot, for a collider.
        GameObject Collider(Vector2 home, string name, int layer)
        {
            int cx = Mathf.Clamp(Mathf.FloorToInt(home.x / TerrainBuilder.ChunkSize), 0, across - 1);
            int cz = Mathf.Clamp(Mathf.FloorToInt(home.y / TerrainBuilder.ChunkSize), 0, across - 1);
            var chunk = chunks[cz * across + cx];
            var go = new GameObject(name) { layer = layer };
            go.transform.SetParent(chunk, false);
            go.transform.localPosition = new Vector3(home.x - cx * TerrainBuilder.ChunkSize, plan.GroundAt(home), home.y - cz * TerrainBuilder.ChunkSize);
            return go;
        }
    }
}
