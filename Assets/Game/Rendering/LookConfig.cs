using Game.Core;
using UnityEngine;

namespace Game.Rendering
{
    /// <summary>
    /// The look of the night: fog, mist, sky, moon, exposure and colour (Assets/Config/Look/LookConfig.asset).
    /// Edit it in the Inspector or from the dev window (F9 > Lighting / Look), even during Play.
    /// </summary>
    [CreateAssetMenu(menuName = "Game/Config/Look", fileName = "LookConfig")]
    public sealed class LookConfig : GameConfig
    {
        [Tooltip("The theme these settings started from (changing it here doesn't apply it; use the dev window).")]
        public string theme = LookThemes.Default;

        public LookSettings settings = new LookSettings();

        /// <summary>Replace the settings with a theme's. Returns false if there's no such theme.</summary>
        public bool ApplyTheme(string name)
        {
            var s = LookThemes.Get(name);
            if (s == null) return false;
            settings = s;
            theme = name;
            return true;
        }
    }
}
