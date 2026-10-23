namespace Game.Core
{
    /// <summary>
    /// Whether a window that needs the mouse is open (dev window, pause menu, inventory). While one is, the
    /// player's view doesn't turn and clicks don't reach the game.
    /// </summary>
    public sealed class UiState
    {
        int openPanels;

        public bool AnyOpen => openPanels > 0;

        public void PanelOpened() => openPanels++;

        public void PanelClosed()
        {
            if (openPanels > 0) openPanels--;
        }
    }
}
