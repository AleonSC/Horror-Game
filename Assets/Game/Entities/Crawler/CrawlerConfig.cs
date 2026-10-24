using System;
using Game.Core;
using UnityEngine;

namespace Game.Entities
{
    /// <summary>The crawlers (section 6.9 of the plan), in Assets/Config/Entities/CrawlerConfig.asset.</summary>
    [CreateAssetMenu(menuName = "Game/Config/Crawler", fileName = "CrawlerConfig")]
    public sealed class CrawlerConfig : GameConfig
    {
        [Header("Look")]
        [Tooltip("Your own crawler (resting on the ground at the pivot, facing +Z, about 1 m across). A child named 'Eyes' glows. An Animator gets 'Speed' and a 'Leap' trigger. Empty: the built-in one.")]
        public GameObject customModel;

        public CrawlerSettings settings = new CrawlerSettings();
    }

    [Serializable]
    public sealed class CrawlerSettings
    {
        [Header("How many (x difficulty)")]
        [Range(0, 20), Tooltip("Kept round you once they come (the lamp they come at is in the difficulty).")]
        public int count = 4;
        [Range(3f, 60f), Tooltip("They lie in wait at least this far from you...")]
        public float minDistance = 14f;
        [Range(5f, 100f), Tooltip("...and at most this far.")]
        public float maxDistance = 45f;
        [Range(20f, 200f), Tooltip("One left this far behind is moved somewhere new.")]
        public float relocateDistance = 70f;

        [Header("Moving")]
        [Range(0.1f, 5f), Tooltip("Crawling speed (metres a second).")]
        public float crawlSpeed = 1.2f;
        [Range(1f, 60f), Tooltip("Now and then one creeps to a new spot: every this many seconds.")]
        public float repositionEvery = 20f;

        [Header("Leaping")]
        [Range(0.5f, 5f), Tooltip("Come within this walking and it leaps...")]
        public float leapWalking = 1.8f;
        [Range(0.5f, 6f), Tooltip("...or this, running. Crouching past, it doesn't notice.")]
        public float leapRunning = 2.5f;
        [Range(0.1f, 2f)] public float leapTime = 0.35f;

        [Header("On your face")]
        [Range(1, 30), Tooltip("Presses of Space to throw it off...")]
        public int struggleTaps = 8;
        [Range(1f, 30f), Tooltip("...or it lets go after this long.")]
        public float letGoAfter = 6f;
        [Range(0f, 100f), Tooltip("Its shriek carries this far (the blind one hears it).")]
        public float shriekNoise = 30f;
        [Tooltip("Being latched onto brings another scour.")]
        public bool summonsScour = true;
        [Range(0f, 1f), Tooltip("How dark your view goes with it on your face.")]
        public float blindness = 0.88f;

        [Header("Eyes")]
        [Range(0f, 30f), Tooltip("How bright its eyes shine in your beam.")]
        public float eyeshine = 6f;
        [Range(1f, 30f), Tooltip("They shine from this far.")]
        public float eyeshineDistance = 10f;

        public CrawlerSettings Clone() => (CrawlerSettings)MemberwiseClone();
    }
}
