namespace Game.Core
{
    /// <summary>
    /// Whether a window that needs the mouse is open (dev window, pause menu, inventory). While one is, the
    /// player's view doesn't turn and clicks don't reach the game. Overlays (the dev window) leave the keyboard
    /// to the game, so you can still walk; full windows (pause, inventory) take it too.
    /// </summary>
    public sealed class UiState
    {
        int openPanels;
        int keyboardPanels;

        public bool AnyOpen => openPanels > 0;

        /// <summary>True when the keys shouldn't move the player: a full window is open, or you're typing.</summary>
        public bool KeysTaken => keyboardPanels > 0 || Typing;

        /// <summary>Set by a window while one of its text boxes has the keyboard.</summary>
        public bool Typing { get; set; }

        /// <param name="takesKeys">False for an overlay you can walk around with.</param>
        public void PanelOpened(bool takesKeys = true)
        {
            openPanels++;
            if (takesKeys) keyboardPanels++;
        }

        public void PanelClosed(bool takesKeys = true)
        {
            if (openPanels > 0) openPanels--;
            if (takesKeys && keyboardPanels > 0) keyboardPanels--;
        }
    }
}
