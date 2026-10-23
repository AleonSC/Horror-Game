using System;
using UnityEngine;

namespace Game.Player
{
    /// <summary>
    /// How long you can run (section 6.1). Running drains it; standing refills it, walking more slowly, and
    /// every lamp lit slower still. Run it out and you're winded: no running until it's back to windedUntil.
    /// Plain C#, so it can be tested.
    /// </summary>
    public sealed class Stamina
    {
        public float Value { get; private set; } = 1f;
        public bool Winded { get; private set; }
        /// <summary>Extra sprint time from snacks (0.25 = 25% more).</summary>
        public float Bonus;

        public bool CanSprint => !Winded && Value > 0f;

        /// <summary>Raised with true when you run out, false when you get your breath back.</summary>
        public event Action<bool> WindedChanged;

        /// <summary>Advance by dt. Returns true if the player is actually sprinting this frame.</summary>
        public bool Tick(float dt, bool wantsSprint, bool moving, PlayerSettings s, int lampsLit)
        {
            if (dt <= 0f) return wantsSprint && CanSprint;
            if (wantsSprint && moving && CanSprint)
            {
                Value -= dt / Mathf.Max(0.01f, s.sprintTime * (1f + Bonus));
                if (Value <= 0f)
                {
                    Value = 0f;
                    SetWinded(true);
                }
                return true;
            }

            float recover = s.recoverTime * (1f + s.recoverSlowerPerLamp * lampsLit) * (moving ? s.walkingRecoverSlowdown : 1f);
            Value = Mathf.Min(1f, Value + dt / Mathf.Max(0.01f, recover));
            if (Winded && Value >= s.windedUntil) SetWinded(false);
            return false;
        }

        public void Refill()
        {
            Value = 1f;
            SetWinded(false);
        }

        void SetWinded(bool winded)
        {
            if (Winded == winded) return;
            Winded = winded;
            WindedChanged?.Invoke(winded);
        }
    }
}
