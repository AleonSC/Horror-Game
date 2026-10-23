using System;
using Game.Core;
using UnityEngine;

namespace Game.Entities
{
    /// <summary>The scours (section 6.7 of the plan), in Assets/Config/Entities/ScourConfig.asset.</summary>
    [CreateAssetMenu(menuName = "Game/Config/Scour", fileName = "ScourConfig")]
    public sealed class ScourConfig : GameConfig
    {
        public ScourSettings settings = new ScourSettings();
    }

    [Serializable]
    public sealed class ScourSettings
    {
        [Header("Appearing")]
        [Range(0f, 300f), Tooltip("Seconds of calm before the first one appears.")]
        public float firstDelay = 60f;
        [Range(0.1f, 5f)] public float materializeTime = 1.2f;
        [Range(5f, 60f)] public float minSpawnDistance = 22f;
        [Range(5f, 80f)] public float maxSpawnDistance = 32f;
        [Range(10f, 180f), Tooltip("Appears within this many degrees of where you're facing.")]
        public float spawnViewAngle = 70f;
        [Range(0f, 60f), Tooltip("After being driven off, back this many seconds later (at least)...")]
        public float minReturnDelay = 10f;
        [Range(0f, 60f), Tooltip("...and at most.")]
        public float maxReturnDelay = 18f;
        [Range(20f, 150f), Tooltip("Get this far away and it vanishes and comes back closer.")]
        public float relocateDistance = 50f;
        [Range(0f, 1f), Tooltip("Share that peek from behind trunks and duck back when looked at.")]
        public float duckerChance = 0.5f;

        [Header("Creeping")]
        [Range(0.5f, 20f), Tooltip("Seconds between creeps (shortest)...")]
        public float minCreepInterval = 4f;
        [Range(0.5f, 30f), Tooltip("...and longest. Quicker with aggression.")]
        public float maxCreepInterval = 8f;
        [Range(0.5f, 8f), Tooltip("Metres per creep.")]
        public float creepStep = 2.5f;
        [Range(10f, 90f), Tooltip("Within this many degrees of your view counts as looking at it.")]
        public float lookAngle = 55f;
        [Range(1f, 6f), Tooltip("A creep that would bring it this close takes you.")]
        public float killDistance = 3f;

        [Header("In the dark")]
        [Range(3f, 30f), Tooltip("Within this, with no light on it, it grows and comes for you.")]
        public float reactDistance = 14f;
        [Range(0f, 5f), Tooltip("Metres a second it advances.")]
        public float advanceSpeed = 0.5f;
        [Range(0f, 0.5f), Tooltip("How fast it grows while advancing (scale a second).")]
        public float growRate = 0.03f;
        [Range(1f, 3f)] public float maxGrowth = 1.8f;
        [Range(0.5f, 4f), Tooltip("Advancing, it takes you when this close.")]
        public float touchDistance = 1.5f;

        [Header("Facing it down")]
        [Range(1f, 6f), Tooltip("You can't get closer than this; reaching it starts the crescendo.")]
        public float stopDistance = 2.5f;
        [Range(0f, 2f)] public float triggerMargin = 0.4f;
        [Range(0f, 20f), Tooltip("Degrees past the beam's edge that still count as lighting it.")]
        public float beamMargin = 5f;
        [Range(0.3f, 10f)] public float crescendoTime = 2.5f;
        [Range(0f, 1f), Tooltip("Each lamp lit makes the crescendo this much quicker (0.15 = 15%).")]
        public float crescendoSpeedupPerLamp = 0.15f;
        [Range(0.1f, 5f)] public float minCrescendoTime = 0.8f;
        [Range(0f, 1f), Tooltip("Share of the battery it costs to drive one off.")]
        public float dismissCost = 0.1f;

        [Header("Provoked")]
        [Range(1, 10), Tooltip("Flashing the light at it this many times...")]
        public int flashesToProvoke = 4;
        [Range(0.5f, 10f), Tooltip("...within this many seconds makes it charge.")]
        public float flashWindow = 2.2f;
        [Range(1f, 60f)] public float chargeSpeed = 22f;
        [Range(0.3f, 4f)] public float chargeStopDistance = 1.2f;
        [Range(0f, 30f)] public float minChargeReturn = 3f;
        [Range(0f, 30f)] public float maxChargeReturn = 6f;
        [Range(0, 10), Tooltip("Each charge makes it angrier (sooner, further creeps), up to this.")]
        public int maxAggression = 4;

        [Header("Taking you")]
        [Range(0.2f, 5f), Tooltip("Metres a second it walks up to you once it has you.")]
        public float approachSpeed = 1.1f;
        [Range(1f, 8f)] public float approachMinDistance = 3.5f;
        [Range(0.2f, 5f)] public float graspTime = 1.4f;
        [Range(0.2f, 5f)] public float portalOpenTime = 1f;
        [Range(0.5f, 8f)] public float dragTime = 2.8f;
        [Range(0f, 120f), Tooltip("After it takes you, seconds before they come back.")]
        public float returnAfterCaught = 25f;

        [Header("Safe light")]
        [Range(0f, 5f), Tooltip("How far outside a safe light it circles.")]
        public float ringMargin = 1.5f;
        [Range(0f, 5f)] public float circleSpeed = 1.2f;

        [Header("Eyes")]
        [Range(0f, 40f), Tooltip("Eye glow just after it appears.")]
        public float spawnEyeGlow = 12f;
        [Range(0f, 10f), Tooltip("Eye glow at the start of a hunt, far off.")]
        public float startEyeGlow = 0.25f;
        [Range(0f, 20f), Tooltip("Eye glow when it's nearly on you.")]
        public float endEyeGlow = 5f;
        [Range(0.1f, 20f)] public float eyeDimTime = 6f;
        [Range(0f, 30f), Tooltip("How bright the eyes flare when the beam crosses them.")]
        public float eyeshine = 9f;
        [Range(0f, 5f), Tooltip("Strength of the red light the eyes throw (candela per unit of glow).")]
        public float eyeLight = 0.15f;
        [Range(0.5f, 10f)] public float eyeLightRange = 3f;

        [Header("Body")]
        [Range(1f, 4f)] public float height = 2.4f;
        [Range(0f, 1f), Tooltip("Chance of an extra pair of arms.")]
        public float extraArmsChance = 0.25f;
        [Range(0f, 0.2f)] public float vibration = 0.04f;
        [Range(0f, 1f)] public float crescendoVibration = 0.25f;
        [Range(0f, 10f), Tooltip("Degrees of camera shake at the peak.")]
        public float cameraShake = 2.5f;

        public ScourSettings Clone() => (ScourSettings)MemberwiseClone();
    }
}
