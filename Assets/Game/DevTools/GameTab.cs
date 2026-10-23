using Game.Audio;
using Game.Core;
using UnityEngine;

namespace Game.DevTools
{
    /// <summary>The dev window's Game tab: a live summary of the run, restart, and switches to try the sound states.</summary>
    sealed class GameTab : IDevTab
    {
        const int DevShelterId = -1;

        public string Title => "Game";

        public void Draw()
        {
            if (!Services.TryGet<GameRoot>(out var root) || root.Run == null)
            {
                GUILayout.Label("The game isn't running.", DevStyle.MutedLabel);
                return;
            }

            if (Services.TryGet<DevWindow>(out var window) && window.FrameMs > 0f)
                DevStyle.Row("Frame", $"{window.FrameMs:0.0} ms ({1000f / window.FrameMs:0} fps)");

            var run = root.Run;
            GUILayout.Label("Run", DevStyle.Section);
            DevStyle.Row("Run", run.RunNumber.ToString());
            DevStyle.Row("Seed", run.Seed.ToString());
            DevStyle.Row("Time", SessionLog.FormatTime(run.Elapsed));
            DevStyle.Row("Lamps lit", $"{run.LampsLit} / {GameRun.LampCount}");
            if (GUILayout.Button("Restart run", DevStyle.Button)) root.Restart();

            GUILayout.Label("Sound state", DevStyle.Section);
            if (!Services.TryGet<AudioStateController>(out var audio))
            {
                GUILayout.Label("No sound controller running.", DevStyle.MutedLabel);
                return;
            }

            DevStyle.Row("Now", audio.Current.ToString());
            GUILayout.Label("Try each state (sends the same events the game will):", DevStyle.MutedLabel);

            var events = root.Events;
            var inputs = audio.Inputs;

            bool sheltered = DevStyle.Switch("Sheltered", inputs.Sheltered);
            if (sheltered != inputs.Sheltered)
            {
                if (sheltered) events.Publish(new EnteredShelter(DevShelterId, "Dev switch"));
                else events.Publish(new ExitedShelter(DevShelterId, "Dev switch"));
            }

            bool darkRoom = DevStyle.Switch("Dark room", inputs.InDarkRoom);
            if (darkRoom != inputs.InDarkRoom)
            {
                if (darkRoom) events.Publish(new EnteredDarkRoom());
                else events.Publish(new ExitedDarkRoom());
            }

            bool paused = DevStyle.Switch("Paused (sound only)", inputs.Paused);
            if (paused != inputs.Paused)
            {
                if (paused) events.Publish(new GamePaused());
                else events.Publish(new GameResumed());
            }

            GUILayout.Label("Threat nearby", DevStyle.MutedLabel);
            int threat = DevStyle.Choice((int)inputs.Threat, "None", "Dread", "Hush");
            if (threat != (int)inputs.Threat) events.Publish(new ThreatLevelChanged((ThreatLevel)threat));
        }
    }
}
