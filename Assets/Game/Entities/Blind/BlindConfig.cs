using System;
using Game.Core;
using UnityEngine;

namespace Game.Entities
{
    /// <summary>The blind one, "the Auscult" (section 6.8 of the plan), in Assets/Config/Entities/BlindConfig.asset.</summary>
    [CreateAssetMenu(menuName = "Game/Config/Blind One", fileName = "BlindConfig")]
    public sealed class BlindConfig : GameConfig
    {
        [Header("Look")]
        [Tooltip("Your own blind one (feet at the pivot, facing +Z, any size: scaled to Height). An Animator gets 'Speed' (m/s), 'Listen' (bool), 'Roar' and 'Take' (triggers). Empty: the built-in one.")]
        public GameObject customModel;

        public BlindSettings settings = new BlindSettings();

        const int Version = 1;
        [SerializeField, HideInInspector] int savedUnder;

        void OnEnable()
        {
            if (savedUnder >= Version) return;
            var fresh = new BlindSettings();
            var s = settings ?? (settings = fresh);
            s.lampWatch = fresh.lampWatch; // (version 1: a lamp lit is its extreme listening, longer)
            savedUnder = Version;
#if UNITY_EDITOR
            UnityEditor.EditorUtility.SetDirty(this);
#endif
        }
    }

    [Serializable]
    public sealed class BlindSettings
    {
        [Header("Arriving")]
        [Range(1, 10), Tooltip("It comes when this many lamps are lit.")]
        public int arriveAtLamp = 3;
        [Range(10f, 120f), Tooltip("It breaks out of the ground this far from you.")]
        public float arriveDistance = 45f;
        [Range(2f, 20f)] public float height = 7.4f;

        [Header("Roaming")]
        [Range(5f, 100f), Tooltip("It wanders within this of you.")]
        public float roamRadius = 40f;
        [Range(0.2f, 5f)] public float roamSpeed = 1.5f;
        [Range(1f, 60f), Tooltip("Every so often it stops to cock its head and click: between this many seconds...")]
        public float minListenEvery = 6f;
        [Range(1f, 60f)] public float maxListenEvery = 14f;
        [Range(0.5f, 10f), Tooltip("...for this long.")]
        public float listenTime = 3f;

        [Header("Teleporting (try it: untick to have it walk your trail instead)")]
        [Tooltip("It doesn't walk about: it stands, listening, then goes in a strike of its own red lightning and comes in another somewhere round you. Only when it hears you does it run at you.")]
        public bool teleports = true;
        [Range(1f, 60f), Tooltip("It stands listening this long (shortest)...")]
        public float perchMin = 7f;
        [Range(1f, 60f), Tooltip("...to this long, before it goes again.")]
        public float perchMax = 14f;
        [Range(0.2f, 15f), Tooltip("Gone, between the strikes, this long (shortest)...")]
        public float goneMin = 1.5f;
        [Range(0.2f, 15f)] public float goneMax = 4f;
        [Range(5f, 100f), Tooltip("It comes in between this close to you...")]
        public float teleportNear = 16f;
        [Range(5f, 150f), Tooltip("...and this far.")]
        public float teleportFar = 38f;
        [Range(0f, 1f), Tooltip("Chance it comes in where you're looking.")]
        public float teleportInView = 0.4f;

        [Header("Striding (in place of the lightning)")]
        [Tooltip("When it moves on (with Teleporting on), it doesn't vanish in lightning: its legs grow and it walks there in huge, slow steps, crushing trees, each one shaking the ground; then it shrinks back down. Off: the lightning.")]
        public bool strides = true;
        [Range(1f, 6f), Tooltip("Its legs grow to this many times their length.")]
        public float strideGrowth = 3.6f;
        [Range(1f, 20f), Tooltip("How fast it strides (metres a second).")]
        public float strideSpeed = 5f;
        [Range(0.5f, 10f), Tooltip("Seconds for its legs to grow, or shrink back.")]
        public float growTime = 2.5f;

        [Header("Its neck")]
        [Range(0f, 40f), Tooltip("Listening hard (and while it stands waiting), its neck lifts its head this far above its shoulders (metres): up over the trees.")]
        public float neckReach = 16f;

        [Header("Following your trail (when it doesn't teleport)")]
        [Tooltip("Roaming, it follows the trail you've left (where you were this many seconds ago), not just wandering.")]
        [Range(0f, 120f)] public float trailBehind = 18f;
        [Range(0.5f, 6f), Tooltip("How fast it follows your trail (metres a second); a little quicker each lamp after it comes.")]
        public float trailSpeed = 2.4f;
        [Range(0f, 0.5f)] public float trailSpeedPerLamp = 0.12f;
        [Range(5f, 120f), Tooltip("Every so often it stops dead and listens hard: between this many seconds...")]
        public float minHardListen = 20f;
        [Range(5f, 120f)] public float maxHardListen = 40f;
        [Range(0f, 80f), Tooltip("...and then it hears you moving within this, even crouched.")]
        public float hardListenReach = 30f;

        [Header("Lamps")]
        [Range(0f, 30f), Tooltip("Each time you light a lamp, wherever it is and whatever it's doing (unless it's taking you), it stops dead and listens its hardest: its neck runs up and up until its head is high over the treetops, turned to stare down at you. This long (0: never)...")]
        public float lampWatch = 12f;
        [Range(10f, 80f), Tooltip("...its head this far above its shoulders (metres; the trees are 11-22 m)...")]
        public float lampNeckReach = 34f;
        [Range(1f, 4f), Tooltip("...hearing everything this many times further: move at all within its hard-listening reach x this, even crouched, and it comes. Stand still till its head comes down.")]
        public float lampHearing = 2f;
        [Range(0f, 1f), Tooltip("Its head and neck up over the trees show through the fog this dark (a silhouette against it), the further off the more: 0 the fog hides them, 1 black.")]
        public float headThroughFog = 0.8f;

        [Header("Your light clicking (x difficulty)")]
        [Range(0f, 80f), Tooltip("Click your flashlight on or off within this of it and it hears it: it stops, snaps its head round to the sound and raises it high over the trees, listening...")]
        public float lightClickHearing = 25f;
        [Range(0f, 30f), Tooltip("...for this long...")]
        public float lightClickAlert = 8f;
        [Range(1f, 4f), Tooltip("...hearing everything (noises, you moving close, you moving while it listens hard) this many times further.")]
        public float lightClickBoost = 1.8f;

        [Header("Hearing (x difficulty)")]
        [Range(0f, 15f), Tooltip("Within this it hears you whatever you do (except crouch).")]
        public float closeHearing = 4f;
        [Range(1f, 12f), Tooltip("Rushing to a noise (metres a second).")]
        public float rushSpeed = 5.2f;
        [Range(0f, 30f), Tooltip("At the spot, it searches this long...")]
        public float searchSpotTime = 5f;
        [Range(0f, 180f), Tooltip("...then this long round it...")]
        public float searchAreaTime = 45f;
        [Range(1f, 40f), Tooltip("...within this of the spot.")]
        public float searchAreaRadius = 10f;

        [Header("Taking you")]
        [Range(0.5f, 6f)] public float takeDistance = 2.6f;
        [Range(0f, 300f), Tooltip("After it takes you, it's gone this long.")]
        public float awayAfterTaking = 60f;

        [Header("Keeping up")]
        [Range(30f, 300f), Tooltip("Get this far away...")]
        public float lostDistance = 70f;
        [Range(5f, 300f), Tooltip("...for this long...")]
        public float lostTime = 30f;
        [Range(10f, 100f), Tooltip("...and it catches up to this far.")]
        public float catchUpTo = 35f;

        [Header("Trees")]
        [Tooltip("It shoulders through trees and knocks them down.")]
        public bool knocksTrees = true;

        [Header("Storm")]
        [Range(10f, 100f), Tooltip("How high the cloud hangs over it.")]
        public float stormHeight = 34f;
        [Range(10f, 200f)] public float stormSize = 70f;
        [Range(0.5f, 30f), Tooltip("Red lightning, every this many seconds (shortest)...")]
        public float minLightning = 2f;
        [Range(0.5f, 30f), Tooltip("...and longest.")]
        public float maxLightning = 7f;
        public Color lightningColor = new Color(1f, 0.1f, 0.05f);
        [Range(0f, 100000f), Tooltip("How bright a strike lights the forest (candela).")]
        public float lightningIntensity = 1500f;
        [Range(0f, 1f), Tooltip("How much a strike lights the fog: high and the whole view flashes red.")]
        public float lightningFogGlow = 0.2f;
        [Range(0f, 60f), Tooltip("How brightly the bolt itself glows.")]
        public float boltGlow = 2.5f;

        public BlindSettings Clone() => (BlindSettings)MemberwiseClone();
    }
}
