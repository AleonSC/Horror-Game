using System;
using Game.Core;
using UnityEngine;

namespace Game.World
{
    /// <summary>The forest's own life and events (section 6.13 of the plan), in Assets/Config/World/ForestLifeConfig.asset.</summary>
    [CreateAssetMenu(menuName = "Game/Config/Forest Life", fileName = "ForestLifeConfig")]
    public sealed class ForestLifeConfig : GameConfig
    {
        [Tooltip("Your own out-of-place things for the phantoms (any models, standing at their pivot). Empty: the built-in ones (a chair, a stop sign, a gurney, a TV, a mailbox, a tricycle).")]
        public GameObject[] phantoms = new GameObject[0];

        public ForestLifeSettings settings = new ForestLifeSettings();
    }

    [Serializable]
    public sealed class ForestLifeSettings
    {
        [Header("Falling trees")]
        [Range(0f, 300f), Tooltip("The first one this many seconds into a run...")]
        public float firstTreeFall = 30f;
        [Range(5f, 600f), Tooltip("...then one every this many seconds (shortest)...")]
        public float minTreeFall = 45f;
        [Range(5f, 600f), Tooltip("...and longest.")]
        public float maxTreeFall = 90f;
        [Range(3f, 60f), Tooltip("A tree in plain view between this far...")]
        public float treeFallNear = 12f;
        [Range(5f, 80f), Tooltip("...and this far.")]
        public float treeFallFar = 25f;

        [Header("Phantoms")]
        [Range(0f, 1f), Tooltip("Chance, each time you turn your head quickly, that something's waiting at the edge of your view.")]
        public float phantomChance = 0.25f;
        [Range(0f, 600f), Tooltip("At least this long between them.")]
        public float phantomGap = 40f;
        [Range(30f, 400f), Tooltip("Turning faster than this (degrees a second) counts.")]
        public float phantomTurnSpeed = 120f;

        [Header("Door knocks")]
        [Range(0f, 1f), Tooltip("Chance that shutting a door is answered by three slow knocks from the trees.")]
        public float knockChance = 0.2f;

        [Header("Spider webs")]
        [Range(0, 300), Tooltip("Webs strung between close trees at head height.")]
        public int webs = 70;
        [Range(0f, 1f), Tooltip("How much they show in the light (they're nearly invisible otherwise).")]
        public float webShine = 0.35f;

        [Header("Vines")]
        [Range(0f, 0.3f), Tooltip("Share of trees with a vine hanging from them.")]
        public float vineShare = 0.04f;
        [Range(10f, 150f), Tooltip("Only vines this close move (the rest hang still).")]
        public float vineSimulateWithin = 45f;

        [Header("Owls")]
        [Range(0, 200)] public int owls = 45;
        [Range(5f, 120f), Tooltip("Their heads turn to follow you within this.")]
        public float owlWatch = 45f;
        [Tooltip("How high up the trunk they sit, in metres (lowest, highest). About eye level by default. Takes effect with the next world.")]
        public Vector2 owlHeight = new Vector2(1.5f, 2.3f);
        [Range(5f, 300f), Tooltip("Each calls every this many seconds (shortest)...")]
        public float minOwlCall = 25f;
        [Range(5f, 300f), Tooltip("...and longest.")]
        public float maxOwlCall = 70f;

        [Header("Deer (as the prototype had them)")]
        [Range(0, 20), Tooltip("How many deer (on Restart). They turn up in small herds.")]
        public int deer = 6;
        [Range(10f, 200f), Tooltip("They roam within this distance of you.")]
        public float deerRange = 70f;
        [Range(5f, 60f), Tooltip("They stop and watch you inside this...")]
        public float deerAlert = 24f;
        [Range(3f, 40f), Tooltip("...and bolt at this (one spooked sets off the herd round it).")]
        public float deerFlee = 14f;
        [Range(0.2f, 4f)] public float deerWalkSpeed = 1.1f;
        [Range(2f, 20f)] public float deerGallopSpeed = 10f;
        [Range(5f, 300f), Tooltip("A grazing deer near you grunts, bleats or snorts every this many seconds (shortest)...")]
        public float minDeerCall = 30f;
        [Range(5f, 300f), Tooltip("...and longest.")]
        public float maxDeerCall = 80f;
        [Range(5f, 150f), Tooltip("Only deer this close call.")]
        public float deerCallDistance = 60f;
        [Tooltip("A deer you startle drops a battery as it bolts.")]
        public bool deerDropBattery = true;

        [Header("Small things")]
        [Range(0, 200)] public int beetles = 40;
        [Range(0, 40)] public int rats = 6;
        [Range(1f, 15f), Tooltip("Rats flee when you're this close.")]
        public float ratFlee = 5f;
        [Range(0, 100), Tooltip("Moths round each lit lamp near you.")]
        public int mothsPerLamp = 14;
        [Range(0, 100), Tooltip("Fireflies round each lit lamp near you.")]
        public int firefliesPerLamp = 18;

        [Header("Flocks")]
        [Range(2f, 300f), Tooltip("Bats cross overhead every this many seconds (shortest)...")]
        public float minBats = 15f;
        [Range(2f, 300f)] public float maxBats = 45f;
        [Range(2f, 300f), Tooltip("Crows burst up from the trees every this many seconds (shortest)...")]
        public float minCrows = 20f;
        [Range(2f, 300f)] public float maxCrows = 60f;

        [Header("Ponds")]
        [Range(0f, 1f), Tooltip("How slow wading is (share of walking speed).")]
        public float wadeSpeed = 0.55f;
        [Range(0, 40), Tooltip("Glowing mushrooms round each pond.")]
        public int mushroomsPerPond = 14;

        [Header("Leaves")]
        [Range(0, 2000), Tooltip("Dead leaves lying round you.")]
        public int leaves = 400;
        [Range(0, 200), Tooltip("Leaves drifting down from the canopy.")]
        public int fallingLeaves = 30;

        public ForestLifeSettings Clone() => (ForestLifeSettings)MemberwiseClone();
    }
}
