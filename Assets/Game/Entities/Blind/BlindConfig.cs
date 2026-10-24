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
        public float lostDistance = 90f;
        [Range(5f, 300f), Tooltip("...for this long...")]
        public float lostTime = 60f;
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
        public float boltGlow = 8f;

        public BlindSettings Clone() => (BlindSettings)MemberwiseClone();
    }
}
