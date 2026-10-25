using System;
using Game.Core;
using UnityEngine;

namespace Game.Entities
{
    /// <summary>The scours (section 6.7 of the plan), in Assets/Config/Entities/ScourConfig.asset.</summary>
    [CreateAssetMenu(menuName = "Game/Config/Scour", fileName = "ScourConfig")]
    public sealed class ScourConfig : GameConfig
    {
        [Header("Look")]
        [Tooltip("Use your own model instead of the built-in scour (affects new scours: press Play again).")]
        public bool useCustomModel;
        [Tooltip("Your scour prefab. Add a Scour Model Rig to it to say where its eyes, head, jaw and mouth are (docs/custom-scour.md).")]
        public GameObject customModel;

        public ScourSettings settings = new ScourSettings();
    }

    [Serializable]
    public sealed class ScourSettings
    {
        [Header("How many")]
        [Range(0f, 300f), Tooltip("Seconds of calm before the first one appears.")]
        public float firstDelay = 60f;
        [Range(10f, 600f), Tooltip("After the calm, one more joins every this many seconds (x difficulty)...")]
        public float extraEvery = 60f;
        [Range(0, 50), Tooltip("...this many times (x difficulty's 'how many').")]
        public int extraCount = 10;
        [Range(0f, 60f), Tooltip("When a lamp is lit one more joins, this many seconds later (at least)...")]
        public float minLampJoinDelay = 4f;
        [Range(0f, 60f), Tooltip("...and at most.")]
        public float maxLampJoinDelay = 10f;

        [Header("Appearing")]
        [Range(0.1f, 5f)] public float materializeTime = 1.2f;
        [Range(5f, 60f)] public float minSpawnDistance = 22f;
        [Range(5f, 80f)] public float maxSpawnDistance = 32f;
        [Range(10f, 180f), Tooltip("Appears within this many degrees of where you're facing.")]
        public float spawnViewAngle = 70f;
        [Range(0f, 60f), Tooltip("After being driven off, back this many seconds later (at least)...")]
        public float minReturnDelay = 7f;
        [Range(0f, 60f), Tooltip("...and at most.")]
        public float maxReturnDelay = 12f;
        [Range(20f, 150f), Tooltip("Get this far away and it vanishes and comes back closer.")]
        public float relocateDistance = 50f;
        [Range(0f, 1f), Tooltip("Share that peek from behind trunks and duck back when looked at.")]
        public float duckerChance = 0.5f;

        [Header("Creeping")]
        [Range(0.5f, 20f), Tooltip("Seconds between creeps (shortest)...")]
        public float minCreepInterval = 3f;
        [Range(0.5f, 30f), Tooltip("...and longest. Quicker with aggression.")]
        public float maxCreepInterval = 6f;
        [Range(0.5f, 8f), Tooltip("Metres per creep.")]
        public float creepStep = 3f;
        [Range(10f, 90f), Tooltip("Within this many degrees of your view counts as looking at it.")]
        public float lookAngle = 45f;
        [Range(1f, 6f), Tooltip("A creep that would bring it this close takes you.")]
        public float killDistance = 3f;
        [Tooltip("Looking at it only holds it still while your flashlight is on. In the dark it creeps even while you watch.")]
        public bool creepsWhenWatchedInDark = true;
        [Tooltip("Each creep, it comes apart where it stands and pulls back together at the new spot (its appearing, very fast). Off: it just jumps.")]
        public bool creepBlink = true;
        [Range(0.02f, 1f), Tooltip("Seconds to come apart...")]
        public float blinkOut = 0.1f;
        [Range(0.02f, 1f), Tooltip("...and to pull back together.")]
        public float blinkIn = 0.15f;
        [Range(0f, 1f), Tooltip("Chance a creep takes it to a tree on its way to you, to hide behind the trunk and peek out.")]
        public float creepToTreeChance = 0.35f;

        [Header("In the dark")]
        [Range(3f, 30f), Tooltip("Within this, with no light on it, it grows and comes for you.")]
        public float reactDistance = 14f;
        [Range(0f, 5f), Tooltip("Metres a second it advances.")]
        public float advanceSpeed = 0.75f;
        [Range(0f, 0.5f), Tooltip("How fast it grows while advancing (scale a second).")]
        public float growRate = 0.03f;
        [Range(1f, 3f)] public float maxGrowth = 1.8f;
        [Range(0.5f, 4f), Tooltip("Advancing, it takes you when this close.")]
        public float touchDistance = 1.5f;

        [Header("Facing it down (hold your light on it)")]
        [Range(1f, 6f), Tooltip("You can't walk closer than this.")]
        public float stopDistance = 2.5f;
        [Range(0f, 20f), Tooltip("Degrees past the beam's edge that still count as lighting it (anywhere on its body).")]
        public float beamMargin = 6f;
        [Range(0.3f, 10f), Tooltip("Seconds of light on it to drive it off, at middle distance.")]
        public float crescendoTime = 2.5f;
        [Range(0.2f, 3f), Tooltip("From the edge of its range (14 m), the build is this much as fast...")]
        public float buildFar = 0.6f;
        [Range(0.2f, 3f), Tooltip("...and right up close, this much.")]
        public float buildNear = 1.5f;
        [Range(0f, 5f), Tooltip("Lose the beam off it and the build holds this many seconds...")]
        public float faceDownGrace = 1f;
        [Range(0.05f, 3f), Tooltip("...then drains away at this share a second (all gone: it's back to stalking you).")]
        public float faceDownDrain = 0.5f;
        [Range(0f, 1f), Tooltip("Each lamp lit makes the crescendo this much quicker (0.15 = 15%).")]
        public float crescendoSpeedupPerLamp = 0.15f;
        [Range(0.1f, 5f)] public float minCrescendoTime = 0.8f;
        [Range(0.3f, 8f), Tooltip("Driven off, it comes apart into dark motes that scatter on the wind for this long.")]
        public float scatterTime = 2.2f;
        [Range(0f, 1f), Tooltip("Share of the battery it costs to drive one off.")]
        public float dismissCost = 0.1f;
        [Range(0.1f, 1f), Tooltip("While you hold one in your light you move at this share of your speed, whichever way you go (it never stops you): so with others behind you, choose when to face one down.")]
        public float faceDownSlow = 0.5f;
        [Range(0f, 10f), Tooltip("Degrees of camera shake as the crescendo peaks.")]
        public float faceDownShake = 0.9f;

        [Header("Clicking")]
        [Range(1f, 60f), Tooltip("Seconds between its runs of clicks when it's far off...")]
        public float clickFarInterval = 14f;
        [Range(0.3f, 20f), Tooltip("...and when it's close (they quicken as it closes in).")]
        public float clickNearInterval = 1.4f;
        [Range(0f, 1f)] public float clickVolume = 0.9f;

        [Header("The eye left behind")]
        [Range(0f, 1f), Tooltip("Chance that one you drive off leaves an eye behind, floating after you and watching.")]
        public float leftEyeChance = 0.3f;
        [Range(5f, 120f), Tooltip("Seconds it watches you before it glows and flies off into the sky to the giant.")]
        public float leftEyeTime = 30f;
        [Range(3f, 25f), Tooltip("How far off it keeps (metres).")]
        public float leftEyeDistance = 8f;

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
        [Range(0.3f, 6f), Tooltip("Seconds it takes to lift you up to its eyes.")]
        public float liftTime = 2.2f;
        [Range(0f, 2f), Tooltip("How far above its eyes it holds yours (metres; it lifts you to them).")]
        public float liftAboveEyes = 0.05f;
        [Range(1f, 15f), Tooltip("Seconds for the world round you to come apart and the dark to close in, before the fade.")]
        public float dissolveTime = 5f;
        [Range(5f, 60f), Tooltip("How far round you the world is seen coming apart (metres).")]
        public float dissolveReach = 24f;
        [Range(0, 3000), Tooltip("How many motes the world comes apart into.")]
        public int worldMotes = 1200;
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
        [Range(0f, 2f), Tooltip("The soft glow round each eye in the mist, as a share of the eyes' own glow.")]
        public float eyeHalo = 0.12f;
        [Range(0.5f, 10f)] public float eyeLightRange = 3f;
        [Tooltip("Off: the eyes' red light only glows in the fog round them. On: it also lights up what's near (a trunk it peeks round).")]
        public bool eyeLightOnSurfaces;

        [Header("Mouth")]
        [Range(1f, 15f), Tooltip("Its mouth starts to drop open within this distance...")]
        public float mouthOpenDistance = 6f;
        [Range(0.5f, 10f), Tooltip("...and is fully open at this distance.")]
        public float mouthFullDistance = 2.8f;
        [Range(0f, 30f), Tooltip("How brightly its throat glows.")]
        public float throatGlow = 6f;
        [Range(0f, 30f), Tooltip("The red glare out of its mouth (candela when fully open).")]
        public float mouthGlare = 3f;
        [Range(1f, 15f), Tooltip("How far the glare reaches (metres): a red pool on the ground ahead of it.")]
        public float mouthGlareRange = 7f;

        [Header("Body")]
        [Tooltip("Off: a flat black silhouette. On: a very dark body the light picks the bones out of. New scours only.")]
        public bool litBody;
        [Range(1f, 4f)] public float height = 2.4f;
        [Range(0f, 1f), Tooltip("Chance of an extra pair of arms.")]
        public float extraArmsChance = 0f;
        [Tooltip("Off: it just stands there, upright, facing you. On: it strikes a new pose (reaching, arms up, crouched, turned away, head lolled) each time it creeps.")]
        public bool creepPoses;
        [Range(0f, 0.2f)] public float vibration = 0.04f;
        [Range(0f, 1f)] public float crescendoVibration = 0.25f;
        [Range(0f, 10f), Tooltip("Degrees of camera shake when it charges you.")]
        public float cameraShake = 2.5f;

        public ScourSettings Clone() => (ScourSettings)MemberwiseClone();
    }
}
