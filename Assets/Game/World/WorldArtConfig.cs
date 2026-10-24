using Game.Core;
using UnityEngine;

namespace Game.World
{
    /// <summary>
    /// Your own art for the world (Assets/Config/World/WorldArtConfig.asset). Every slot is optional: leave it
    /// empty and the game uses its built-in version. See docs/custom-assets.md. (The scour's model is in
    /// ScourConfig.)
    /// </summary>
    [CreateAssetMenu(menuName = "Game/Config/World Art", fileName = "WorldArtConfig")]
    public sealed class WorldArtConfig : GameConfig
    {
        [Header("Trees (any number of variants each)")]
        [Tooltip("Living trees. Their real heights are used for the sightlines (the giant, the sky over lamps).")]
        public GameObject[] livingTrees = new GameObject[0];
        [Tooltip("Dead, bare trees.")]
        public GameObject[] deadTrees = new GameObject[0];
        [Tooltip("Multiplies your trees' size (the generator also varies each one a little).")]
        public float treeSize = 1f;

        [Header("Plants")]
        public GameObject[] bushes = new GameObject[0];
        [Tooltip("Short grass clumps. Made about 1 m tall; the generator scales each one.")]
        public GameObject[] grass = new GameObject[0];
        [Tooltip("Tall grass for the lamp clearings and glades. About 1 m tall; scaled up to 1-1.5 m.")]
        public GameObject[] tallGrass = new GameObject[0];

        [Header("Rocks and logs")]
        [Tooltip("Rocks about 1 m across (also used, much bigger, for boulders).")]
        public GameObject[] rocks = new GameObject[0];
        [Tooltip("Fallen logs 1 m long along their Z axis (stretched to length).")]
        public GameObject[] logs = new GameObject[0];

        [Header("Ground")]
        [Tooltip("The forest floor (its texture repeats about every 3 m).")]
        public Material ground;
        public Material trail;
        public Material water;

        [Header("Places (prefabs placed where the plan puts them)")]
        [Tooltip("A lamp post. Give it a Lamp component (or name its parts Light, Marker, Glass and Crank).")]
        public GameObject lamp;
        public GameObject cabin;
        public GameObject shack;
        public GameObject campsite;
        public GameObject watchtower;
        public GameObject car;
        public GameObject landmarkTree;
        [Tooltip("The bunker: its doorway facing +Z, a child named Table where its loot goes, and Inside (a Box Collider) over its rooms. Empty: the built-in one.")]
        public GameObject bunker;
        public GameObject door;
    }
}
