using UnityEngine;

namespace Game.Core
{
    /// <summary>
    /// Pausing (section 7): time stops (time scale 0), the Paused sound state comes in, and the mouse is freed for
    /// the menu. The pause menu calls <see cref="Pause"/> and <see cref="Resume"/>.
    /// </summary>
    public sealed class PauseState
    {
        readonly EventBus events;
        readonly UiState ui;
        float timeScaleBefore = 1f;

        public bool Paused { get; private set; }

        public PauseState(EventBus events, UiState ui)
        {
            this.events = events;
            this.ui = ui;
        }

        public void Pause()
        {
            if (Paused) return;
            Paused = true;
            timeScaleBefore = Time.timeScale > 0f ? Time.timeScale : 1f;
            Time.timeScale = 0f;
            ui?.PanelOpened();
            events?.Publish(new GamePaused());
        }

        public void Resume()
        {
            if (!Paused) return;
            Paused = false;
            Time.timeScale = timeScaleBefore;
            ui?.PanelClosed();
            events?.Publish(new GameResumed());
        }
    }
}
