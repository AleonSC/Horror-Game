using System;
using UnityEngine;

namespace Game.Core
{
    /// <summary>
    /// Base for every tuning asset (ScourConfig, LampConfig...). All the game's numbers live in these assets
    /// under Assets/Config, not in script defaults. Edits made in Play mode are kept, and they're in git.
    /// Systems that cache values can listen to <see cref="Changed"/> to pick up live edits.
    /// </summary>
    public abstract class GameConfig : ScriptableObject
    {
        /// <summary>Raised when a value is edited in the Inspector (editor only).</summary>
        public event Action Changed;

        protected virtual void OnValidate() => Changed?.Invoke();
    }
}
