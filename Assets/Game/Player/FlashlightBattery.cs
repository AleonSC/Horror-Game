using UnityEngine;

namespace Game.Player
{
    /// <summary>
    /// The flashlight's charge, 0-1 (section 4.6). Drains while on; facing a scour down costs a slice; batteries
    /// add 10%. Below the low mark the beam dims toward lowDim. Plain C#, so it can be tested.
    /// </summary>
    public sealed class FlashlightBattery
    {
        public float Charge { get; private set; } = 1f;
        public bool Empty => Charge <= 0f;

        /// <summary>Drain for dt seconds of use, on a battery that lasts lifeSeconds.</summary>
        public void Drain(float dt, float lifeSeconds) => Charge = Mathf.Max(0f, Charge - dt / Mathf.Max(0.01f, lifeSeconds));

        /// <summary>Take a share of the full charge (facing a scour down: 0.1).</summary>
        public void Spend(float share) => Charge = Mathf.Max(0f, Charge - share);

        public void Add(float share) => Charge = Mathf.Min(1f, Charge + share);

        /// <summary>Top up to at least this much (waking up again).</summary>
        public void AtLeast(float share) => Charge = Mathf.Max(Charge, Mathf.Clamp01(share));

        /// <summary>0 while healthy, rising to 1 as it dies below the low mark.</summary>
        public float Lowness(float lowMark) => lowMark > 0f ? Mathf.Clamp01(1f - Charge / lowMark) : 0f;

        /// <summary>How bright the beam is from the charge: 1 above the low mark, down to lowDim when empty.</summary>
        public float Brightness(float lowMark, float lowDim) => Mathf.Lerp(1f, lowDim, Lowness(lowMark));
    }
}
