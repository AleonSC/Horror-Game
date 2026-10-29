using UnityEngine;

namespace Game.Audio
{
    /// <summary>
    /// Put on an outside sound's own looping source (the wind, the rain, an animal): it comes through muffled
    /// whenever you're under cover (<see cref="SoundPlayer.Muffle"/>), by <see cref="amount"/>.
    /// </summary>
    [RequireComponent(typeof(AudioSource))]
    public sealed class Muffled : MonoBehaviour
    {
        [Range(0f, 1f), Tooltip("How much it's muffled under cover (1: fully, as through walls).")]
        public float amount = 1f;

        AudioLowPassFilter filter;

        void Awake()
        {
            filter = GetComponent<AudioLowPassFilter>();
            if (filter == null) filter = gameObject.AddComponent<AudioLowPassFilter>();
            filter.cutoffFrequency = 22000f;
        }

        void Update() => filter.cutoffFrequency = SoundPlayer.CutoffFor(Mathf.Max(SoundPlayer.Muffle * amount, SoundPlayer.Deafened));

        /// <summary>Muffle this source under cover.</summary>
        public static void On(AudioSource source, float amount = 1f)
        {
            if (source == null) return;
            var m = source.GetComponent<Muffled>();
            if (m == null) m = source.gameObject.AddComponent<Muffled>();
            m.amount = amount;
        }
    }
}
