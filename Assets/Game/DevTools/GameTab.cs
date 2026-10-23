using Game.Audio;
using Game.Core;
using Game.Entities;
using Game.Player;
using Game.World.Build;
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

            if (Services.TryGet<WorldBuilder>(out var world) && world.Plan != null) DrawWorld(world, root);
            if (Services.TryGet<PlayerController>(out var player)) DrawPlayer(player);
            if (Services.TryGet<ScourSpawner>(out var spawner)) DrawScours(spawner);

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

        static void DrawWorld(WorldBuilder world, GameRoot root)
        {
            GUILayout.Label("World", DevStyle.Section);
            var plan = world.Plan;
            DevStyle.Row("Seed", plan.Seed.ToString());
            DevStyle.Row("Forest", $"{world.Forest.InstanceCount} things, {world.Forest.DrawnInstances} drawn in {world.Forest.DrawCalls} calls");
            foreach (var t in world.Timings) GUILayout.Label(t, DevStyle.MutedLabel);
            GUILayout.Label("Restart run makes a new world (with Randomize Seed Each Run on, in GameSetup).", DevStyle.MutedLabel);
        }

        static void DrawPlayer(PlayerController player)
        {
            GUILayout.Label("Player", DevStyle.Section);
            if (player.Flashlight != null)
                DevStyle.Row("Battery", $"{player.Flashlight.Battery.Charge * 100f:0}%{(player.Flashlight.IsOn ? " (on)" : "")}");
            DevStyle.Row("Stamina", $"{player.Stamina.Value * 100f:0}%{(player.Stamina.Winded ? " (winded)" : "")}");
            bool safe = Services.TryGet<SafeZones>(out var zones) && zones.Contains(player.transform.position);
            DevStyle.Row("Safe", safe ? "yes" : "no");
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Full battery", DevStyle.Button) && player.Flashlight != null) player.Flashlight.Battery.Add(1f);
            if (GUILayout.Button("Full stamina", DevStyle.Button)) player.Stamina.Refill();
            GUILayout.EndHorizontal();
        }

        static void DrawScours(ScourSpawner spawner)
        {
            GUILayout.Label("Scours", DevStyle.Section);
            foreach (var scour in spawner.Scours)
            {
                if (scour == null) continue;
                string where = scour.State == ScourState.Hidden ? "" : $", {scour.DistanceToPlayer:0.0} m";
                DevStyle.Row($"#{scour.Id}", $"{scour.State}{where}");
            }
            if (GUILayout.Button("Make one appear now", DevStyle.Button)) spawner.AppearNow();
        }
    }
}
