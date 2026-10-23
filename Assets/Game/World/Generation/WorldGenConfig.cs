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
        [Range(0f, 1f), Tooltip("Share of a trail that's worn (the rest peters out).")]
        public float trailWorn = 0.75f;

        [Header("Vegetation")]
        public int trees = 2800;
        public float treeSpacing = 2.2f;
        [Tooltip("Cells of patchiness across the map (the plan's noise scale 0.05 over 320 m: 16).")]
        public int treePatchCells = 16;
        [Range(0f, 1f)] public float deadShare = 0.3f;
        [Range(0f, 0.2f)] public float oldGiantShare = 0.03f;
        public Vector2 oldGiantScale = new Vector2(1.7f, 2.5f);
        public float treeScale = 0.85f;
        [Range(0f, 0.5f), Tooltip("Share of planned tree spots left empty (natural gaps).")]
        public float emptyShare = 0.12f;
        public int bushes = 1400;
        public Vector2 bushSize = new Vector2(0.45f, 1.4f);
        public int grass = 10000;
        [Tooltip("Cells of meadow patchiness across the map (noise scale 0.03 over 320 m: about 10).")]
        public int meadowCells = 10;
        public Vector2Int tuftsPerTree = new Vector2Int(4, 10);
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

        /// <summary>Your own trees' heights (set by the world builder from WorldArtConfig; empty: the built-in trees).</summary>
        [NonSerialized] public float[] livingTreeHeights, deadTreeHeights;

        public WorldGenSettings Clone() => (WorldGenSettings)MemberwiseClone();
    }
}
