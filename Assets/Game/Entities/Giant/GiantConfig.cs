using System;
using Game.Core;
using UnityEngine;

namespace Game.Entities
{
    /// <summary>The giant and the eye (section 6.11 of the plan), in Assets/Config/Entities/GiantConfig.asset.</summary>
    [CreateAssetMenu(menuName = "Game/Config/Giant", fileName = "GiantConfig")]
    public sealed class GiantConfig : GameConfig
    {
        [Header("Look")]
        [Tooltip("Your own giant (feet at the pivot, facing +Z, any size: it's scaled to Height). An Animator gets a 'Walk' float (0-1) and a 'Hanging' bool. Empty: the built-in one.")]
        public GameObject customModel;

        public GiantSettings settings = new GiantSettings();
    }

    [Serializable]
    public sealed class GiantSettings
    {
        [Header("Hanging (from the start)")]
        [Range(100f, 2000f), Tooltip("How tall it is (metres).")]
        public float height = 660f;
        [Range(1f, 60f), Tooltip("Hanging, its feet are this many degrees above the horizon...")]
        public float feetElevation = 10f;
        [Range(5f, 80f), Tooltip("...and its neck this many. (Together with its height, these set how far away it hangs.)")]
        public float neckElevation = 38f;
        [Range(5f, 300f), Tooltip("Seconds to turn once on its chain, slowly back and forth.")]
        public float turnTime = 45f;
        [Range(0f, 90f), Tooltip("How far it turns each way (degrees).")]
        public float turnAngle = 40f;
        [Range(0, 12), Tooltip("Empty chains hanging elsewhere round the horizon.")]
        public int emptyChains = 4;
        [Range(0f, 1f), Tooltip("The chains fade into the sky from this far up their length (0 their bottom, 1 their top)...")]
        public float chainFadeFrom = 0.3f;
        [Range(0f, 1f), Tooltip("...and are gone by this far up.")]
        public float chainFadeTo = 0.9f;
        [Range(0f, 1f), Tooltip("How much of the fog's colour it takes: 0 black, 1 invisible in the fog.")]
        public float haze = 0.55f;
        [Range(0f, 1f), Tooltip("How much the fog in front shows over it: 0 a crisp silhouette that blocks the flashlight's glow, 1 the glow shows through but the lit fog softens it.")]
        public float beamThrough = 0.4f;
        [Tooltip("The moonlight (a faint glow in the sky) comes from right behind it.")]
        public bool moonBehind = true;
        [Range(0f, 1.3f), Tooltip("Where behind it: 0 its feet, 1 its neck.")]
        public float moonAt = 0.72f;
        [Range(0f, 30f), Tooltip("Degrees each lamp lit makes it swing on its chain.")]
        public float joltSwing = 6f;
        [Tooltip("The first clear view of it gently draws your eyes up to it (once).")]
        public bool drawViewOnFirstSight = true;
        [Range(0f, 120f), Tooltip("Not before this many seconds into a run.")]
        public float firstSightAfter = 8f;

        [Tooltip("Solid black (ignores haze and beam-through).")]
        public bool solidBlack = true;

        [Header("Awake: its eyes and its search ray (once it's fallen)")]
        [Range(0.1f, 10f), Tooltip("Seconds for its eyes to open once it's landed.")]
        public float eyesOpenTime = 2.5f;
        public Color ownEyeColor = new Color(1f, 0.12f, 0.05f);
        [Range(0f, 50f)] public float ownEyeGlow = 6f;
        [Tooltip("The search ray from its eyes, sweeping the distance.")]
        public Color scanColor = new Color(1f, 0.35f, 0.25f);
        [Range(1f, 40f), Tooltip("How wide the ray is (degrees).")]
        public float scanAngle = 7f;
        [Range(0f, 60f), Tooltip("How far down it points (degrees below its gaze).")]
        public float scanPitch = 16f;
        [Range(0f, 90f), Tooltip("How far it sweeps each way as it walks (degrees).")]
        public float scanSweep = 35f;
        [Range(0.05f, 2f), Tooltip("How fast it sweeps.")]
        public float scanSweepSpeed = 0.3f;
        [Range(0f, 5000000f)] public float scanIntensity = 400000f;
        [Range(0f, 10f), Tooltip("The shaft of the ray in the air.")]
        public float scanShaft = 0.6f;

        [Header("The eye (at the 5th lamp)")]
        [Range(1, 10)] public int eyeAtLamp = 5;
        [Range(10f, 89f), Tooltip("Degrees above the horizon.")]
        public float eyeElevation = 68f;
        [Range(1f, 200f), Tooltip("Its size, as an angle across (degrees).")]
        public float eyeSize = 9f;
        [Range(0.1f, 20f)] public float eyeOpenTime = 4f;
        [Range(0.1f, 60f)] public float eyeStareTime = 11f;
        [Range(0.1f, 20f)] public float eyeCloseTime = 3f;
        [Range(0f, 30f), Tooltip("Seconds after it opens until the dream hallway takes hold.")]
        public float dreamAfter = 3f;
        [Range(0.1f, 10f), Tooltip("Seconds for the dream to take hold.")]
        public float dreamRamp = 2.5f;
        public Color eyeColor = new Color(1f, 0.08f, 0.03f);
        [Range(0f, 50f)] public float eyeGlow = 8f;
        [Tooltip("The fog's colour while it stares.")]
        public Color redFog = new Color(0.11f, 0.015f, 0.015f);

        [Header("Falling and walking")]
        [Range(0.5f, 15f)] public float fallTime = 4f;
        [Range(0f, 10f), Tooltip("Seconds after it lands until the shockwave rolls out.")]
        public float shockwaveAfter = 2.5f;
        [Range(10f, 1000f), Tooltip("How fast the shockwave travels (metres a second).")]
        public float shockwaveSpeed = 220f;
        [Range(0.5f, 50f)] public float walkSpeed = 7.5f;
        [Range(1f, 30f), Tooltip("Seconds for one stride (two steps).")]
        public float strideTime = 12f;
        [Range(5f, 200f), Tooltip("Every so often it stops to scan: between this many seconds...")]
        public float minScanEvery = 25f;
        [Range(5f, 200f)] public float maxScanEvery = 50f;
        [Range(0.5f, 20f), Tooltip("...for 3-6 seconds.")]
        public float minScanTime = 3f;
        [Range(0.5f, 20f)] public float maxScanTime = 6f;
        [Range(0f, 20f), Tooltip("Camera shake from a step when it's at its nearest (degrees; a third of that far off).")]
        public float stepShake = 4.5f;
        [Range(20f, 1000f), Tooltip("It never comes nearer than this (metres): it slows as it nears, still striding, and keeps this far off however you run at it.")]
        public float closestDistance = 220f;
        [Range(0f, 300f), Tooltip("After it's driven back (or you're taken), seconds before it walks again.")]
        public float restTime = 30f;

        [Header("Searchlights (at the 10th lamp)")]
        [Range(1, 10)] public int searchlightsAtLamp = 10;
        [Range(1f, 40f), Tooltip("The searchlights' circle on the ground (radius, metres).")]
        public float searchlightRadius = 9f;
        [Range(0.1f, 3f), Tooltip("How quickly they follow you (1: as fast as you run).")]
        public float searchlightTracking = 1.05f;
        public Color searchlightColor = new Color(1f, 0.25f, 0.18f);
        [Range(0f, 10000000f), Tooltip("Brightness (candela: they're far away).")]
        public float searchlightIntensity = 1000000f;
        [Range(0f, 10f), Tooltip("The shafts of light from its eyes down to the circle (0: none).")]
        public float searchlightShaft = 0.6f;
        [Range(0, 64), Tooltip("Scours circling the edge of the light.")]
        public int crowd = 32;
        [Range(2f, 40f), Tooltip("How far from you they circle (metres).")]
        public float crowdRadius = 12f;

        public GiantSettings Clone() => (GiantSettings)MemberwiseClone();

        /// <summary>How far away it hangs: where its height spans the feet and neck angles.</summary>
        public float HangDistance
        {
            get
            {
                float span = Mathf.Tan(neckElevation * Mathf.Deg2Rad) - Mathf.Tan(Mathf.Min(feetElevation, neckElevation - 1f) * Mathf.Deg2Rad);
                return height * 0.86f / Mathf.Max(0.01f, span);
            }
        }
    }
}
