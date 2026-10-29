using System;
using Game.Core;
using UnityEngine;

namespace Game.World
{
    /// <summary>The forest's own life and events (section 6.13 of the plan), in Assets/Config/World/ForestLifeConfig.asset.</summary>
    [CreateAssetMenu(menuName = "Game/Config/Forest Life", fileName = "ForestLifeConfig")]
    public sealed class ForestLifeConfig : GameConfig
    {
        [Tooltip("Your own out-of-place things for the phantoms (any models, standing at their pivot). Empty: the built-in ones (a chair, a stop sign, a gurney, a TV showing static, a ringing telephone, a CB radio left on, a tricycle).")]
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
        [Tooltip("When a falling tree hits anything (the ground, another tree, a rock) it bursts into splinters, as when the blind one walks through one. Off: it breaks into logs that roll.")]
        public bool treeFallSplinters = true;

        [Header("Phantoms")]
        [Range(0f, 1f), Tooltip("Chance, each time you turn your head quickly, that something's waiting at the edge of your view.")]
        public float phantomChance = 0.25f;
        [Range(0f, 600f), Tooltip("At least this long between them.")]
        public float phantomGap = 40f;
        [Range(30f, 400f), Tooltip("Turning faster than this (degrees a second) counts.")]
        public float phantomTurnSpeed = 120f;
        [Tooltip("As the night goes on (lamps lit, 0 to all 10) they come more often: the chance rises to this...")]
        [Range(0f, 1f)] public float phantomChanceLate = 0.6f;
        [Range(0f, 600f), Tooltip("...the least time between them falls to this...")]
        public float phantomGapLate = 12f;
        [Range(30f, 400f), Tooltip("...and a slower turn does it.")]
        public float phantomTurnSpeedLate = 80f;

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
        public float vineShare = 0.1f;
        [Range(1, 4), Tooltip("Up to this many strands from one tree.")]
        public int vinesPerTree = 3;
        [Range(0.5f, 4f), Tooltip("Lowest they hang down to, above the ground (metres)...")]
        public float vineLowest = 0.4f;
        [Range(0.5f, 4f), Tooltip("...and highest.")]
        public float vineHighest = 1.9f;
        [Range(0f, 3f), Tooltip("How hard the wind swings them.")]
        public float vineWind = 1f;
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

        [Header("Lightning bugs")]
        [Range(0, 200), Tooltip("How many round you (on Restart).")]
        public int lightningBugs = 40;
        [Range(5f, 60f), Tooltip("They're about within this distance of you.")]
        public float lightningBugRange = 24f;
        [Range(0f, 20f), Tooltip("How bright a flash is (the batteries' green).")]
        public float lightningBugGlow = 4f;
        [Range(1f, 40f), Tooltip("A flier flashes every this many seconds, give or take a quarter.")]
        public float lightningBugEvery = 14f;
        [Range(0.02f, 1f), Tooltip("A flash rises to full in this many seconds...")]
        public float lightningBugRise = 0.12f;
        [Range(0.1f, 4f), Tooltip("...then fades to nothing over this many.")]
        public float lightningBugFade = 1.4f;
        [Range(0f, 1f), Tooltip("Share of them sitting in the grass, answering nearby flashes (dimmer, two seconds later).")]
        public float lightningBugsSitting = 0.15f;

        [Header("Moths round your flashlight")]
        [Range(0, 80), Tooltip("Most moths drawn to your beam at once.")]
        public int flashlightMoths = 28;
        [Range(10f, 600f), Tooltip("Seconds of the light being on to gather all of them (the difficulty's moth time scales it).")]
        public float mothGatherTime = 120f;
        [Range(0f, 1f), Tooltip("Turn the light off and on again where you stand: the chance each moth comes back to it...")]
        public float mothComeBack = 0.85f;
        [Range(1f, 40f), Tooltip("...less the further it is from you by then (none past this many metres: run off and few find you again).")]
        public float mothComeBackRange = 12f;
        [Tooltip("Lost moths (the light off, or out of it) give up and are gone after this many seconds (shortest, longest).")]
        public Vector2 mothGiveUp = new Vector2(4f, 10f);
        [Range(0.5f, 8f), Tooltip("How fast they fly after the beam (metres a second).")]
        public float mothSpeed = 3f;

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

        [Header("Crows")]
        [Range(5f, 300f), Tooltip("Dark crows fly over high up every this many seconds (shortest)...")]
        public float minCrowsOver = 20f;
        [Range(5f, 300f), Tooltip("...and longest.")]
        public float maxCrowsOver = 55f;
        [Range(0f, 1f), Tooltip("Share of those that are a single crow (the rest are flocks).")]
        public float crowSoloShare = 0.4f;
        [Range(2, 30), Tooltip("A flock is this many crows (fewest)...")]
        public int minCrowFlock = 4;
        [Range(2, 30), Tooltip("...and most.")]
        public int maxCrowFlock = 12;
        [Tooltip("How high over you they fly (metres: lowest, highest).")]
        public Vector2 crowHeight = new Vector2(16f, 32f);
        [Range(2f, 20f), Tooltip("How fast they fly (metres a second).")]
        public float crowSpeed = 8f;
        [Range(0, 10), Tooltip("Flocks on the ground about you, pecking about; they fly off when you come near.")]
        public int crowFlocksOnGround = 3;
        [Range(3f, 40f), Tooltip("Walking this close puts a flock up...")]
        public float crowScare = 14f;
        [Range(3f, 60f), Tooltip("...and running (or any noise you make) this close.")]
        public float crowScareRunning = 24f;

        [Header("White moth")]
        [Range(10f, 900f), Tooltip("Now and then a white moth turns up in the dark near you, every this many seconds (shortest)...")]
        public float minWhiteMoth = 120f;
        [Range(10f, 900f), Tooltip("...and longest.")]
        public float maxWhiteMoth = 300f;
        [Range(2f, 12f), Tooltip("Caught in your flashlight, it follows the beam, this far out along it.")]
        public float whiteMothFollow = 4f;
        [Range(0.5f, 8f), Tooltip("How fast it flies after the beam (swing the light faster than this and you lose it).")]
        public float whiteMothSpeed = 3f;
        [Range(0.1f, 4f), Tooltip("Out of your light this long and it gives up, wandering off slowly.")]
        public float whiteMothLost = 0.8f;
        [Range(0f, 1f), Tooltip("Lead it to a lit lamp: this much more sprint time for the rest of the run (0.15 = 15%), and your breath back.")]
        public float whiteMothBonus = 0.15f;

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
