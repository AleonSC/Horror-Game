using System;
using Game.Core;
using UnityEngine;

namespace Game.Entities
{
    /// <summary>The flier (section 6.10 of the plan), in Assets/Config/Entities/FlierConfig.asset.</summary>
    [CreateAssetMenu(menuName = "Game/Config/Flier", fileName = "FlierConfig")]
    public sealed class FlierConfig : GameConfig
    {
        [Header("Look")]
        [Tooltip("Your own flier (facing +Z, wings along X, any size: scaled to Wingspan). A child named 'Eye' is where the searchlight comes from. An Animator gets a 'Flap' float (0 glide, 1 beating). Empty: the built-in one.")]
        public GameObject customModel;

        public FlierSettings settings = new FlierSettings();
    }

    [Serializable]
    public sealed class FlierSettings
    {
        [Header("Arriving")]
        [Range(1, 10), Tooltip("It comes when this many lamps are lit.")]
        public int arriveAtLamp = 7;
        [Range(5f, 100f)] public float wingspan = 40f;
        [Range(5f, 80f), Tooltip("How fast it dives in over you (metres a second).")]
        public float diveSpeed = 30f;
        [Range(3f, 40f), Tooltip("How low it passes over you (metres).")]
        public float diveHeight = 12f;

        [Header("Patrolling")]
        [Range(10f, 150f), Tooltip("Its height.")]
        public float altitude = 48f;
        [Range(5f, 100f), Tooltip("The circles it flies over a stretch of forest (radius).")]
        public float circleRadius = 34f;
        [Range(20f, 400f), Tooltip("How far from you the stretches it watches are.")]
        public float patrolRange = 100f;
        [Range(2f, 60f), Tooltip("Flying speed (metres a second).")]
        public float speed = 11f;
        [Range(5f, 120f), Tooltip("Seconds over one stretch before it moves on.")]
        public float stretchTime = 30f;

        [Header("Its eye")]
        [Range(1f, 30f), Tooltip("Half the width of its beam (degrees).")]
        public float beamHalfAngle = 9f;
        public Color beamColor = new Color(0.85f, 0.92f, 1f);
        public Color angryColor = new Color(1f, 0.08f, 0.04f);
        [Range(0f, 100000f), Tooltip("Brightness (candela).")]
        public float beamIntensity = 40000f;
        [Range(0f, 60f), Tooltip("For this long after its eye lights, its beam keeps off you.")]
        public float graceAtStart = 15f;
        [Range(1f, 40f), Tooltip("The beam passing this close to you snaps onto you.")]
        public float lockDistance = 9f;
        [Range(0.5f, 20f), Tooltip("Out of its sight this long (behind trees, under a roof, in a safe light) and it lets you go.")]
        public float hideToLose = 4f;
        [Range(0.5f, 30f), Tooltip("Moving in its light this long in all makes it screech, and every scour comes at once.")]
        public float moveToScreech = 5f;
        [Range(0f, 120f), Tooltip("After letting you go (or screeching), it won't snap onto you for this long.")]
        public float graceAfter = 12f;
        [Range(0f, 300f), Tooltip("After something takes you, it's gone this long.")]
        public float awayAfterCaught = 40f;

        public FlierSettings Clone() => (FlierSettings)MemberwiseClone();
    }
}
