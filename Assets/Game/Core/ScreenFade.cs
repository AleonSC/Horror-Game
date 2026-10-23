using UnityEngine;

namespace Game.Core
{
    /// <summary>
    /// A full-screen fade and a message over it (being taken, waking up). Whoever runs a sequence sets it; the
    /// HUD draws it.
    /// </summary>
    public sealed class ScreenFade
    {
        public float Alpha;
        public Color Color = Color.black;
        public string Message = "";
        public float MessageAlpha;

        public void Clear()
        {
            Alpha = 0f;
            Message = "";
            MessageAlpha = 0f;
        }
    }
}
