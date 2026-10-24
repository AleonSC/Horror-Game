using System.Collections.Generic;
using Game.Core;
using Game.Rendering;
using Game.World.Generation;
using UnityEngine;

namespace Game.World.Build
{
    /// <summary>
    /// Makes the forest when the game starts (put it in the Forest scene): generates the plan from the run's seed,
    /// builds it (ground, trails, ponds, lamps, structures, the door, the forest), places the player at the start
    /// facing the giant's way, and every frame keeps the world wrapped round the camera and draws the forest.
    /// Restarting the run builds a new world.
    /// </summary>
    public sealed class WorldBuilder : MonoBehaviour
    {
        WorldPlan plan;
        WorldWrap wrap;
        InstancedRenderer forest;
        Transform root;
        readonly List<Object> made = new List<Object>();
        System.IDisposable runSub;

        public WorldPlan Plan => plan;
        public InstancedRenderer Forest => forest;
        public WorldWrap Wrapping => wrap;
        public List<string> Timings { get; } = new List<string>();

        void Awake() => Services.Register(this);

        void Start()
        {
            int seed = Services.TryGet<GameRun>(out var run) ? run.Seed : 1;
            Build(seed);
            if (Services.TryGet<EventBus>(out var events)) runSub = events.Subscribe<RunStarted>(e => Build(e.Seed));
        }

        void OnDestroy()
        {
            runSub?.Dispose();
            Clear();
            Services.Unregister(this);
        }

        void LateUpdate()
        {
            var cam = Camera.main;
            if (cam == null || plan == null) return;
            wrap.Update(cam.transform.position);
            forest.Render(cam);
        }

        // In the Scene view (with this selected, in Play): the plan's layout.
        void OnDrawGizmosSelected()
        {
            if (plan == null) return;
            float y = plan.GroundAt(plan.Start) + 2f;
            Vector3 At(Vector2 p) => new Vector3(p.x, plan.GroundAt(p) + 1f, p.y);
            Gizmos.color = Color.white;
            Gizmos.DrawWireCube(new Vector3(plan.Size * 0.5f, y, plan.Size * 0.5f), new Vector3(plan.Size, 0.1f, plan.Size));
            foreach (var c in plan.Clearings)
            {
                Gizmos.color = c.Kind == ClearingKind.Start ? Color.green : c.Kind == ClearingKind.Lamp ? new Color(1f, 0.7f, 0.3f) : c.Kind == ClearingKind.Glade ? new Color(0.5f, 0.8f, 0.4f) : Color.gray;
                Gizmos.DrawWireSphere(At(c.Centre), c.Radius);
            }
            Gizmos.color = Color.red;
            Gizmos.DrawLine(At(plan.Start), At(plan.Start) + new Vector3(SightlineStage.Direction(plan.GiantBearing).x, 0f, SightlineStage.Direction(plan.GiantBearing).y) * 120f);
            Gizmos.color = new Color(0.6f, 0.45f, 0.3f);
            foreach (var t in plan.Trails)
                for (int i = 0; i < t.Points.Count - 1; i++)
                    if (Wrap.Distance(t.Points[i], t.Points[i + 1], plan.Size) < 3f) Gizmos.DrawLine(At(t.Points[i]), At(t.Points[i + 1]));
            Gizmos.color = Color.cyan;
            foreach (var p in plan.Ponds) Gizmos.DrawWireSphere(new Vector3(p.Centre.x, p.Water, p.Centre.y), p.Radius);
            Gizmos.color = Color.magenta;
            foreach (var st in plan.Structures) Gizmos.DrawWireCube(At(st.Position), new Vector3(st.HalfSize.x * 2f, 4f, st.HalfSize.y * 2f));
            Gizmos.DrawWireCube(At(plan.Door), new Vector3(2f, 3f, 2f));
            Gizmos.color = Color.yellow;
            foreach (var b in plan.Batteries) Gizmos.DrawSphere(At(b), 0.4f);
        }

        public void Build(int seed)
        {
            Clear();
            var setup = Services.TryGet<GameSetup>(out var gs) ? gs : null;
            var config = setup != null ? setup.Get<WorldGenConfig>() : null;
            var art = setup != null ? setup.Get<WorldArtConfig>() : null;
            var settings = (config != null ? config.settings : new WorldGenSettings()).Clone();
            var fixer = new MaterialFixer(made);

            // Your own trees: their real heights go into the plan, so the sightlines are right for them.
            var living = ExtractAll(art != null ? art.livingTrees : null, fixer);
            var dead = ExtractAll(art != null ? art.deadTrees : null, fixer);
            float treeSize = art != null ? Mathf.Max(0.01f, art.treeSize) : 1f;
            settings.livingTreeHeights = Heights(living, treeSize);
            settings.deadTreeHeights = Heights(dead, treeSize);
            settings.batteries = Mathf.RoundToInt(settings.batteries * Difficulty.Now.batteriesInForest);

            Timings.Clear();
            var watch = System.Diagnostics.Stopwatch.StartNew();
            plan = WorldGenerator.Generate(seed, settings, null, Timings);
            long planned = watch.ElapsedMilliseconds;

            root = new GameObject("World").transform;
            wrap = new WorldWrap(plan.Size);

            Material ground, trail, water;
            if (art != null && art.ground != null) ground = fixer.Fix(art.ground);
            else
            {
                var groundTex = GroundTexture.Build(seed);
                made.Add(groundTex);
                made.Add(ground = RuntimeMaterials.LitTextured("Ground", Color.white, 0.1f, groundTex, Vector2.one));
            }
            if (art != null && art.trail != null) trail = fixer.Fix(art.trail);
            else made.Add(trail = RuntimeMaterials.Lit("Trail", new Color(0.17f, 0.13f, 0.1f), 0.12f));
            if (art != null && art.water != null) water = fixer.Fix(art.water);
            else made.Add(water = RuntimeMaterials.Lit("Water", new Color(0.015f, 0.02f, 0.025f), 0.95f));

            var chunks = TerrainBuilder.BuildChunks(plan, root, ground, trail, made);
            foreach (var c in chunks) wrap.Add(c, c.position);

            foreach (var pond in plan.Ponds)
            {
                var w = TerrainBuilder.Water(pond, water, made);
                w.transform.SetParent(root, false);
                wrap.Add(w.transform, new Vector3(pond.Centre.x, pond.Water, pond.Centre.y));
            }

            float minH = float.MaxValue, maxH = float.MinValue;
            for (int z = 0; z < plan.Height.Resolution; z++)
            for (int x = 0; x < plan.Height.Resolution; x++)
            {
                minH = Mathf.Min(minH, plan.Height[x, z]);
                maxH = Mathf.Max(maxH, plan.Height[x, z]);
            }
            forest = new InstancedRenderer(plan.Size, TerrainBuilder.ChunkSize, minH, maxH + 25f);
            var populator = new ForestPopulator(plan, forest, chunks, made, art, fixer, living, dead);
            populator.Populate();

            var lamps = new LampFactory(art != null ? art.lamp : null);
            made.AddRange(lamps.Materials);
            foreach (var site in plan.Lamps)
            {
                var pos = new Vector3(site.Position.x, plan.GroundAt(site.Position), site.Position.y);
                var lamp = lamps.Create(site.Index, pos, site.Index * 37f, root);
                wrap.Add(lamp.transform, pos);
            }

            var props = new Placeholders(made, populator.Bark, populator.Needles, art);
            var loot = new List<Loot>();
            for (int i = 0; i < plan.Structures.Count; i++)
            {
                var st = plan.Structures[i];
                loot.Clear();
                foreach (var l in plan.Loot) if (l.Structure == i) loot.Add(l);
                var go = props.Structure(st, plan.GroundAt(st.Position), root, loot);
                wrap.Add(go.transform, go.transform.position);
            }

            // The batteries in the forest, each with its faint green light.
            var pickups = new GameObject("Batteries").transform;
            pickups.SetParent(root, false);
            foreach (var b in plan.Batteries)
            {
                var at = new Vector3(b.x, plan.GroundAt(b) + 0.03f, b.y);
                var pickup = Pickup.Create(Game.Player.ItemKind.Batteries, 1, at, (b.x * 37f + b.y * 11f) % 360f, pickups, glowing: true);
                wrap.Add(pickup.transform, at);
            }
            var door = props.Door(plan.Door, plan.DoorYaw, plan.GroundAt(plan.Door), root);
            wrap.Add(door.transform, door.transform.position);
            // The room through it, far below (not wrapped: you're only ever there by the door).
            var lone = door.GetComponentInChildren<LoneDoor>();
            if (lone != null) lone.room = DarkRoom.Build(door.transform.position, plan.DoorYaw, root, made);

            Timings.Add($"Build: {watch.ElapsedMilliseconds - planned} ms ({forest.InstanceCount} instances)");
            Debug.Log($"World {seed} made in {watch.ElapsedMilliseconds} ms: {string.Join(", ", Timings)}");

            PlacePlayer();
        }

        static PrefabArt.Result[] ExtractAll(GameObject[] prefabs, MaterialFixer fixer)
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

        static float[] Heights(PrefabArt.Result[] art, float size)
        {
            if (art == null) return null;
            var h = new float[art.Length];
            for (int i = 0; i < art.Length; i++) h[i] = art[i].Height * size;
            return h;
        }

        // You wake in the start clearing, facing the way the plan chose (the giant's just off it).
        void PlacePlayer()
        {
            if (!Services.TryGet<Game.Player.PlayerController>(out var player)) return;
            var pos = new Vector3(plan.Start.x, plan.GroundAt(plan.Start) + 0.05f, plan.Start.y);
            var facing = Quaternion.Euler(0f, plan.StartYaw, 0f);
            player.SetSpawn(pos, facing);
            player.Teleport(pos, facing);
        }

        void Clear()
        {
            if (root != null) Destroy(root.gameObject);
            foreach (var o in made) if (o != null) Destroy(o);
            made.Clear();
            wrap?.Clear();
            plan = null;
        }
    }
}
