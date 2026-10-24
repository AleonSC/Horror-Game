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

        public ForestLifeSettings Clone() => (ForestLifeSettings)MemberwiseClone();
    }
}
