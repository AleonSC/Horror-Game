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
    /// Each category uses your own prefabs from WorldArtConfig when it has some, and the built-in shapes if not.
    /// </summary>
    public sealed class ForestPopulator
    {
        const float GrassRange = 45f, TallGrassRange = 60f, BushRange = 65f, RockRange = 75f, TreeRange = 200f;
        static readonly float[] TreeLods = { 40f, 95f };

        readonly WorldPlan plan;
        readonly InstancedRenderer renderer;
        readonly List<Transform> chunks;
        readonly List<Object> made;
        readonly int across;
        readonly WorldArtConfig art;
        readonly MaterialFixer fixer;
        readonly PrefabArt.Result[] livingArt, deadArt;

        public Material Bark, Needles, DeadWood;
        /// <summary>Every standing tree, once populated (owls pick theirs from here).</summary>
        public readonly List<ForestTree> Trees = new List<ForestTree>();
        // How far the wind leans each over (its top moves this much of its height per unit of wind): a tree's top moves
        // tens of centimetres in a gust, grass bows right over.
        const float GrassSway = 0.3f, TallGrassSway = 0.35f, BushSway = 0.15f;
        /// <summary>(Public so what hangs from the trees can lean with them.)</summary>
        public const float TreeSway = 0.06f, DeadTreeSway = 0.035f;
        Material bushA, bushB, grassDry, grassGreen, tallGrass, rock, logWood;

        /// <param name="livingArt">Your living trees, already extracted (their heights went into the plan), or null.</param>
        public ForestPopulator(WorldPlan plan, InstancedRenderer renderer, List<Transform> chunks, List<Object> made,
                               WorldArtConfig art, MaterialFixer fixer, PrefabArt.Result[] livingArt, PrefabArt.Result[] deadArt)
        {
            this.plan = plan;
            this.renderer = renderer;
            this.chunks = chunks;
            this.made = made;
            this.art = art;
            this.fixer = fixer;
            this.livingArt = livingArt;
            this.deadArt = deadArt;
            across = Mathf.CeilToInt(plan.Size / TerrainBuilder.ChunkSize);
            // (Pale enough to catch a little of your light and the lamps': darker and the trees were flat black shapes.)
            // Bark: plates of layered scales and deep fissures, round the built-in trunks (or your own).
            Bark = art != null && art.bark != null ? Instanced(fixer.Fix(art.bark)) : Surface("Bark", ForestTextures.Bark, ForestSurfaces.BarkTiling, Color.white);
            Needles = Mat("Needles", new Color(0.09f, 0.13f, 0.08f), 0.18f, true);
            DeadWood = art != null && art.deadWood != null ? Instanced(fixer.Fix(art.deadWood)) : Surface("Dead wood", ForestTextures.DeadWood, ForestSurfaces.BarkTiling, Color.white);
            bushA = Mat("Bush leaves", new Color(0.07f, 0.11f, 0.045f), 0.3f, true); // (leaves: double-sided, a little waxy)
            bushB = Mat("Bush leaves (dry)", new Color(0.13f, 0.11f, 0.045f), 0.25f, true);
            grassDry = Mat("Dry grass", new Color(0.24f, 0.2f, 0.12f), 0.1f, true);
            grassGreen = Mat("Grass", new Color(0.09f, 0.11f, 0.06f), 0.1f, true);
            tallGrass = Mat("Tall grass", new Color(0.17f, 0.16f, 0.09f), 0.1f, true);
            rock = Mat("Rock", new Color(0.18f, 0.18f, 0.17f), 0.25f, false);
            logWood = art != null && art.deadWood != null ? DeadWood : Surface("Log", ForestTextures.DeadWood, new Vector2(2f, 1f), new Color(0.75f, 0.7f, 0.65f));
        }

        Material Mat(string name, Color c, float smooth, bool doubleSided, Texture2D texture = null, Vector2 tiling = default)
        {
            var m = RuntimeMaterials.LitTextured(name, c, smooth, texture, texture != null ? tiling : Vector2.one, doubleSided);
            made.Add(m);
            return m;
        }

        static Material Instanced(Material m)
        {
            m.enableInstancing = true; // (the forest is drawn instanced)
            return m;
        }

        // A lit material with one of the made-in-code surfaces: colour, bumps and mask (crevices and shine).
        Material Surface(string name, ForestTextures.Set set, Vector2 tiling, Color tint)
        {
            var m = Mat(name, tint, 0.2f, false, set.Colour, tiling);
            RuntimeMaterials.SetNormalMap(m, set.Normals, 1f);
            RuntimeMaterials.SetMaskMap(m, set.Mask);
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
            // Per kind and variant: the renderer kinds that draw it (a built-in pine is two: trunk and crown).
            var pine = livingArt != null ? FromPrefabs("Tree", livingArt, TreeLods, TreeRange, true, Layers.Trees) : BuiltInPines();
            var dead = deadArt != null ? FromPrefabs("Dead tree", deadArt, TreeLods, TreeRange, true, Layers.Trees) : BuiltInDead();
            float custom = art != null ? Mathf.Max(0.01f, art.treeSize) : 1f;
            foreach (var set in pine) foreach (int k in set) renderer.SetSway(k, TreeSway);
            foreach (var set in dead) foreach (int k in set) renderer.SetSway(k, DeadTreeSway);

            foreach (var t in plan.Trees)
            {
                if (t.Removed) continue;
                bool isPine = t.Kind == TreeKind.Pine;
                float size = t.Scale * ((isPine ? livingArt : deadArt) != null ? custom : 1f);
                var m = Matrix4x4.TRS(Ground(t.Position), Quaternion.Euler(0f, t.Yaw, 0f), Vector3.one * size);
                var kinds = isPine ? pine : dead;
                foreach (int k in kinds[t.Variant % kinds.Length]) renderer.Add(k, t.Position, m);

                var treeGo = Collider(t.Position, "Tree", Layers.Trees);
                var marker = treeGo.AddComponent<ForestTree>();
                Trees.Add(marker);
                marker.Kinds = kinds[t.Variant % kinds.Length];
                marker.Home = t.Position;
                marker.Matrix = m;
                var col = treeGo.AddComponent<CapsuleCollider>();
                col.radius = 0.3f * t.Scale;
                col.height = 6f * t.Scale;
                col.center = Vector3.up * 3f * t.Scale;
            }
        }

        int[][] BuiltInPines()
        {
            var kinds = new int[TreeShapes.PineVariants][];
            for (int v = 0; v < kinds.Length; v++)
            {
                var trunks = new Mesh[3];
                var crowns = new Mesh[3];
                for (int lod = 0; lod < 3; lod++)
                {
                    var (t, c) = ForestMeshes.Pine(new System.Random(plan.Seed * 31 + v), TreeShapes.Height(TreeKind.Pine, v), lod);
                    trunks[lod] = Keep(t.ToMesh($"Pine {v} trunk LOD{lod}"));
                    crowns[lod] = Keep(c.ToMesh($"Pine {v} crown LOD{lod}"));
                }
                kinds[v] = new[]
                {
                    renderer.AddKind(InstancedRenderer.Kind.Simple($"Pine trunk {v}", trunks, Bark, TreeLods, TreeRange, true, Layers.Trees)),
                    renderer.AddKind(InstancedRenderer.Kind.Simple($"Pine crown {v}", crowns, Needles, TreeLods, TreeRange, true, Layers.Trees)),
                };
            }
            return kinds;
        }

        int[][] BuiltInDead()
        {
            var kinds = new int[TreeShapes.DeadVariants][];
            for (int v = 0; v < kinds.Length; v++)
            {
                var lods = new Mesh[3];
                for (int lod = 0; lod < 3; lod++)
                    lods[lod] = Keep(ForestMeshes.DeadTree(new System.Random(plan.Seed * 37 + v), TreeShapes.Height(TreeKind.Dead, v), lod).ToMesh($"Dead tree {v} LOD{lod}"));
                kinds[v] = new[] { renderer.AddKind(InstancedRenderer.Kind.Simple($"Dead tree {v}", lods, DeadWood, TreeLods, TreeRange, true, Layers.Trees)) };
            }
            return kinds;
        }

        // One renderer kind per prefab: its meshes and materials, in its own levels of detail.
        int[][] FromPrefabs(string name, PrefabArt.Result[] results, float[] lodDistances, float maxDistance, bool shadows, int layer)
        {
            var kinds = new int[results.Length][];
            for (int v = 0; v < results.Length; v++)
                kinds[v] = new[] { renderer.AddKind(new InstancedRenderer.Kind { Name = $"{name} {v}", Lods = results[v].Lods, LodDistances = lodDistances, MaxDistance = maxDistance, Shadows = shadows, Layer = layer }) };
            return kinds;
        }

        // Your prefabs for a category, extracted, or null if there are none (or none usable).
        PrefabArt.Result[] Extract(GameObject[] prefabs)
        {
            if (prefabs == null || prefabs.Length == 0) return null;
            var list = new List<PrefabArt.Result>();
            foreach (var p in prefabs)
            {
                var r = PrefabArt.Extract(p, fixer);
                if (r != null) list.Add(r);
                else if (p != null) Debug.LogWarning($"WorldArtConfig: '{p.name}' has no meshes to draw; skipped.");
            }
            return list.Count > 0 ? list.ToArray() : null;
        }

        // One renderer kind per variant: your prefabs if there are any, otherwise the built-in meshes.
        int[] Kinds(string name, GameObject[] prefabs, System.Func<int, Mesh> builtIn, int builtInCount, System.Func<int, Material> builtInMaterial, float maxDistance, bool shadows, int layer)
        {
            var custom = Extract(prefabs);
            int count = custom != null ? custom.Length : builtInCount;
            var kinds = new int[count];
            for (int v = 0; v < count; v++)
            {
                kinds[v] = custom != null
                    ? renderer.AddKind(new InstancedRenderer.Kind { Name = $"{name} {v}", Lods = custom[v].Lods, LodDistances = new float[0], MaxDistance = maxDistance, Shadows = shadows, Layer = layer })
                    : renderer.AddKind(InstancedRenderer.Kind.Simple($"{name} {v}", new[] { Keep(builtIn(v)) }, builtInMaterial(v), new float[0], maxDistance, shadows, layer));
            }
            return kinds;
        }

        void AddBushes()
        {
            var kinds = Kinds("Bush", art?.bushes, k => ForestMeshes.Bush(new System.Random(plan.Seed * 41 + k), k / 2).ToMesh($"Bush {k}"), 6,
                              k => k % 2 == 0 ? bushA : bushB, BushRange, true, Layers.Default);
            bool builtIn = kinds.Length == 6 && (art == null || art.bushes == null || art.bushes.Length == 0);
            foreach (int k in kinds) renderer.SetSway(k, BushSway, pushed: true);
            foreach (var b in plan.Bushes)
            {
                int k = builtIn ? b.Kind * 2 + b.Variant % 2 : (b.Kind * 4 + b.Variant) % kinds.Length;
                renderer.Add(kinds[k], b.Position, Matrix4x4.TRS(Ground(b.Position), Quaternion.Euler(0f, b.Yaw, 0f), Vector3.one * b.Size));
            }
        }

        void AddGrass()
        {
            var grass = Kinds("Grass", art?.grass, v => ForestMeshes.GrassClump(new System.Random(plan.Seed * 43 + v), 1f).ToMesh($"Grass {v}"), 4,
                              v => v < 2 ? grassDry : grassGreen, GrassRange, false, Layers.Default);
            var tall = Kinds("Tall grass", art?.tallGrass, v => ForestMeshes.GrassClump(new System.Random(plan.Seed * 47 + v), 1f).ToMesh($"Tall grass {v}"), 4,
                             v => tallGrass, TallGrassRange, false, Layers.Default);
            foreach (int k in grass) renderer.SetSway(k, GrassSway, pushed: true);
            foreach (int k in tall) renderer.SetSway(k, TallGrassSway, pushed: true);
            foreach (var g in plan.Grass)
            {
                float w = 0.8f + 0.4f * g.Size;
                renderer.Add(grass[g.Variant % grass.Length], g.Position, Matrix4x4.TRS(Ground(g.Position), Quaternion.Euler(0f, g.Yaw, 0f), new Vector3(w, g.Size, w)));
            }
            foreach (var g in plan.TallGrass)
                renderer.Add(tall[g.Variant % tall.Length], g.Position, Matrix4x4.TRS(Ground(g.Position), Quaternion.Euler(0f, g.Yaw, 0f), new Vector3(1.3f, g.Size, 1.3f)));
        }

        void AddRocksAndLogs()
        {
            var rocks = Kinds("Rock", art?.rocks, v => ForestMeshes.Rock(new System.Random(plan.Seed * 53 + v)).ToMesh($"Rock {v}"), 6, v => rock, RockRange, true, Layers.Props);
            var boulders = Kinds("Boulder", art?.rocks, v => ForestMeshes.Rock(new System.Random(plan.Seed * 53 + v)).ToMesh($"Boulder {v}"), 6, v => rock, TreeRange, true, Layers.Props);
            var logs = Kinds("Log", art?.logs, v => ForestMeshes.Log(new System.Random(plan.Seed * 59 + v)).ToMesh($"Log {v}"), 3, v => logWood, RockRange, true, Layers.Props);

            foreach (var r in plan.Rocks)
            {
                var pos = Ground(r.Position) + Vector3.down * 0.15f * r.Size; // sunk into the ground
                renderer.Add(rocks[r.Variant % rocks.Length], r.Position, Matrix4x4.TRS(pos, Quaternion.Euler(r.Variant * 7f, r.Yaw, 0f), Vector3.one * r.Size));
                if (r.Size < 0.8f) continue;
                var c = Collider(r.Position, "Rock", Layers.Props).AddComponent<SphereCollider>();
                c.radius = 0.4f * r.Size;
            }
            foreach (var b in plan.Boulders)
            {
                var pos = Ground(b.Position) + Vector3.down * 0.35f * b.Size; // half buried
                renderer.Add(boulders[b.Variant % boulders.Length], b.Position, Matrix4x4.TRS(pos, Quaternion.Euler(0f, b.Yaw, 0f), Vector3.one * b.Size));
                var c = Collider(b.Position, "Boulder", Layers.Props).AddComponent<SphereCollider>();
                c.radius = 0.42f * b.Size;
                c.center = Vector3.down * 0.3f * b.Size;
            }
            foreach (var l in plan.Logs)
            {
                var rot = Quaternion.Euler(0f, l.Yaw, 0f);
                renderer.Add(logs[l.Variant % logs.Length], l.Position, Matrix4x4.TRS(Ground(l.Position), rot, new Vector3(1f, 1f, l.Size)));
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
