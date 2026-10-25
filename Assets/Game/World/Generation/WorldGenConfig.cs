using System;
using Game.Core;
using UnityEngine;

namespace Game.World.Generation
{
    /// <summary>
    /// The numbers behind world generation (section 3 of the plan), in Assets/Config/World/WorldGenConfig.asset.
    /// The seed comes from the run (GameSetup: Randomize Seed Each Run / Fixed Seed).
    /// </summary>
    [CreateAssetMenu(menuName = "Game/Config/World Generation", fileName = "WorldGenConfig")]
    public sealed class WorldGenConfig : GameConfig
    {
        public WorldGenSettings settings = new WorldGenSettings();
    }

    [Serializable]
    public sealed class WorldGenSettings
    {
        [Header("Map")]
        [Tooltip("Metres across (it wraps round at the edges).")]
        public float size = 320f;

        [Header("Terrain")]
        public int broadWaves = 8;
        public float broadMinWavelength = 40f;
        public float broadAmplitude = 0.35f;
        public int smallWaves = 12;
        public float smallMinWavelength = 10f;
        public float smallAmplitude = 0.06f;
        [Tooltip("Multiplies every wave's height.")]
        public float bumpiness = 1.5f;
        [Range(0f, 1.5f), Tooltip("Hummocks: low mounds and dips all over the forest floor, up to this high (metres), so it's never flat.")]
        public float hummocks = 0.35f;
        [Range(2f, 30f), Tooltip("About how far apart the hummocks are (metres).")]
        public float hummockSize = 7f;
        public int hills = 4;
        public Vector2 hillHeight = new Vector2(7f, 13f);
        public Vector2 hillRadius = new Vector2(30f, 50f);
        [Range(0f, 1f), Tooltip("Share of hills stretched into ridges.")]
        public float ridgeChance = 0.5f;
        public Vector2 ridgeStretch = new Vector2(1.4f, 2.2f);
        public int ponds = 3;
        public Vector2 pondRadius = new Vector2(7f, 13f);
        [Tooltip("The middle of a pond is at least this deep (metres).")]
        public float pondDepth = 0.9f;
        public int puddles = 40;

        [Header("Layout")]
        public float startClearing = 28f;
        [Tooltip("Degrees either side of where you face that the giant can be.")]
        public float giantSpread = 12f;
        public int lamps = 10;
        public float lampMinFromStart = 55f;
        public float lampMinApart = 25f;
        public float lampClearing = 12f;
        [Range(0f, 0.6f)] public float lampClearingRagged = 0.3f;
        [Tooltip("Tall grass stays this far from a lamp post.")]
        public float lampTallGrassGap = 4f;
        public int shacks = 3;
        public float shacksApart = 60f;
        public int campsites = 3;
        [Tooltip("An old concrete bunker dug into an earth mound (0 or 1).")]
        public int bunkers = 1;
        public float campsitesApart = 50f;
        public float doorMinFromStart = 65f;
        public int doorAttempts = 600;
        public float doorFromStructures = 25f;
        public float doorFromEdge = 75f;
        public int glades = 14;
        public Vector2 gladeRadius = new Vector2(7f, 16f);

        [Header("Sightlines")]
        [Tooltip("The giant's feet (degrees above the horizon)...")]
        public float giantLow = 10f;
        [Tooltip("...and its neck.")]
        public float giantHigh = 38f;
        [Tooltip("Degrees either side of the giant's bearing kept clear.")]
        public float giantHalfWidth = 5f;
        [Tooltip("No canopy within this cone (degrees from straight up) above a lamp.")]
        public float lampSkyCone = 55f;

        [Header("Trails")]
        public int extraLoops = 2;
        public float trailWidth = 1.4f;
        [Range(0.5f, 10f), Tooltip("Each lamp stands on a patch of trodden earth this wide (radius, metres); its trails run out of it.")]
        public float lampPatchRadius = 2f;
        [Range(0f, 1f), Tooltip("Share of a trail that's worn (the rest peters out).")]
        public float trailWorn = 0.75f;
        [Range(0f, 10f), Tooltip("Grass clumps lining each side of a worn trail (and round the lamps' patches), per metre.")]
        public float trailVergeGrass = 3f;
        [Range(0f, 2f), Tooltip("Tall grass clumps along each side, per metre.")]
        public float trailVergeTallGrass = 0.3f;

        [Header("Grass patches")]
        [Range(0, 600), Tooltip("Patches of thick grass about the forest floor.")]
        public int grassPatches = 220;
        [Range(0f, 1f), Tooltip("Share of them on the trails, so the paths run through them.")]
        public float grassPatchesOnTrails = 0.6f;
        [Tooltip("How wide a patch is (radius, metres: smallest, largest).")]
        public Vector2 grassPatchSize = new Vector2(2f, 5.5f);
        [Range(0.5f, 15f), Tooltip("Grass clumps per square metre in a patch's middle (thinning to its edge).")]
        public float grassPatchDensity = 8f;

        [Header("Vegetation")]
        public int trees = 2100;
        [Tooltip("Trees stay at least this far apart (metres).")]
        public float treeSpacing = 3f;
        [Tooltip("The landmark: one giant tree, seven times the size of the rest.")]
        public bool landmarkTree = false;
        [Tooltip("Cells of patchiness across the map (the plan's noise scale 0.05 over 320 m: 16).")]
        public int treePatchCells = 16;
        [Range(0f, 1f)] public float deadShare = 0.3f;
        [Range(0f, 0.2f)] public float oldGiantShare = 0.03f;
        public Vector2 oldGiantScale = new Vector2(1.7f, 2.5f);
        public float treeScale = 0.85f;
        [Range(0f, 1f), Tooltip("Share of the (tall enough) pines that are the breakable tree: the one model that can fall.")]
        public float breakableTrees = 0.12f;
        [Range(0f, 0.5f), Tooltip("Share of planned tree spots left empty (natural gaps).")]
        public float emptyShare = 0.12f;
        public int bushes = 3200;
        public Vector2 bushSize = new Vector2(0.45f, 1.4f);
        public int grass = 16000;
        [Tooltip("Cells of meadow patchiness across the map (noise scale 0.03 over 320 m: about 10).")]
        public int meadowCells = 10;
        public Vector2Int tuftsPerTree = new Vector2Int(8, 16);
        [Tooltip("Tall grass clumps per square metre in a glade's middle.")]
        public float gladeGrassDensity = 0.55f;
        public Vector2 tallGrassHeight = new Vector2(1f, 1.5f);

        [Header("Props")]
        public int rocks = 480;
        public Vector2 rockSize = new Vector2(0.3f, 1.6f);
        public int boulders = 40;
        public Vector2 boulderSize = new Vector2(2.5f, 6f);
        public int logs = 140;

        [Header("Pickups")]
        public int batteries = 30;
        public float batteryFromStart = 8f;
        public float batteriesApart = 12f;
        public float batteryFromTrees = 1.2f;
        [Range(0f, 1f), Tooltip("Chance of one more battery on a shack's table.")]
        public float shackBatteryChance = 0.5f;
        [Tooltip("Batteries always on each shack's table, and on the cabin's.")]
        public int batteriesInShack = 2, batteriesInCabin = 3;
        [Range(0f, 1f), Tooltip("Chance of the car's key fob on a shack's table, and in a campsite's backpack.")]
        public float keyFobChance = 0.2f;
        [Range(0f, 1f), Tooltip("Chance of a radio on a shack's table, and in a campsite's backpack (the car always has one).")]
        public float radioChance = 0.3f;
        public int glowsticksInBackpack = 10;
        public int batteriesInBackpack = 2;
        public int batteriesInGloveBox = 2;

        /// <summary>Your own trees' heights (set by the world builder from WorldArtConfig; empty: the built-in trees).</summary>
        [NonSerialized] public float[] livingTreeHeights, deadTreeHeights;

        public WorldGenSettings Clone() => (WorldGenSettings)MemberwiseClone();
    }
}
