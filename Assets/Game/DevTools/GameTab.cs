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
            DevStyle.Row("Difficulty", root.Difficulty != null ? root.Difficulty.Level : "Normal");
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Restart run", DevStyle.Button)) root.Restart();
            if (GUILayout.Button("Light the nearest lamp", DevStyle.Button)) LightNearestLamp();
            GUILayout.EndHorizontal();

            if (Services.TryGet<WorldBuilder>(out var world) && world.Plan != null)
            {
                DrawWorld(world, root);
                DrawPlaces(run);
            }
            if (Services.TryGet<PlayerController>(out var player)) DrawPlayer(player);
            if (Services.TryGet<ScourSpawner>(out var spawner)) DrawScours(spawner);
            if (Services.TryGet<Giant>(out var giant)) DrawGiant(giant);
            if (Services.TryGet<BlindOne>(out var blind)) DrawBlind(blind);
            if (Services.TryGet<CrawlerSpawner>(out var crawlers)) DrawCrawlers(crawlers);
            if (Services.TryGet<Flier>(out var flier)) DrawFlier(flier);

            GUILayout.Label("Sound state", DevStyle.Section);
            if (!Services.TryGet<AudioStateController>(out var audio))
            {
                GUILayout.Label("No sound controller running.", DevStyle.MutedLabel);
                return;
            }

            DevStyle.Row("Now", audio.Current.ToString());
            if (Services.TryGet<MusicManager>(out var music)) DevStyle.Row("Music", $"{music.Mood} ({music.Playing})");
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

        // Quick trips for testing the places.
        static void DrawPlaces(GameRun run)
        {
            GUILayout.Label("Places", DevStyle.Section);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Go to the car", DevStyle.Button)) GoTo(First<Game.World.Car>(), new Vector3(-2.5f, 0f, 0f));
            if (GUILayout.Button("Go to the watchtower", DevStyle.Button)) GoTo(First<Game.World.Watchtower>(), new Vector3(-3.5f, 0f, 0f));
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            var door = First<Game.World.LoneDoor>();
            if (GUILayout.Button("Go to the lone door", DevStyle.Button) && door != null) GoTo(door.frame != null ? door.frame : door.transform, new Vector3(0f, 0f, 3f));
            if (door != null && !door.Unlocked && GUILayout.Button("Unlock it", DevStyle.Button) && Services.TryGet<EventBus>(out var events)) events.Publish(new DoorUnlocked());
            GUILayout.EndHorizontal();
            if (PlayerPlace.InDarkRoom) GUILayout.Label("You're in the dark room.", DevStyle.MutedLabel);
        }

        static T First<T>() where T : Object
        {
            var all = Object.FindObjectsByType<T>();
            return all.Length > 0 ? all[0] : null;
        }

        static void GoTo(Component place, Vector3 offset)
        {
            if (place == null || !Services.TryGet<PlayerController>(out var player)) return;
            Vector3 at = place.transform.TransformPoint(offset);
            if (Physics.Raycast(at + Vector3.up * 30f, Vector3.down, out var hit, 60f, Layers.Mask(Layers.Ground), QueryTriggerInteraction.Ignore)) at = hit.point + Vector3.up * 0.05f;
            Vector3 look = place.transform.position - at;
            look.y = 0f;
            player.Teleport(at, Quaternion.LookRotation(look.sqrMagnitude > 0.01f ? look : Vector3.forward));
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

            if (player.Hotbar == null) return;
            var items = player.Hotbar.Items;
            var held = new System.Text.StringBuilder();
            for (int i = 0; i < Inventory.SlotCount; i++)
            {
                var slot = items[i];
                if (slot.Empty) continue;
                if (held.Length > 0) held.Append(", ");
                held.Append(Inventory.NameOf(slot.Kind));
                if (slot.Count > 1) held.Append($" x{slot.Count}");
                if (i == items.Selected) held.Append(" (held)");
            }
            GUILayout.Label(held.Length > 0 ? held.ToString() : "Carrying nothing.", DevStyle.MutedLabel);
            if (GUILayout.Button("Give one of every item", DevStyle.Button)) GiveEverything(player.Hotbar);
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
            DevStyle.Row("From the clock", $"{spawner.FromClock} of {spawner.Settings.extraCount}");
            if (GUILayout.Button("Make one appear now", DevStyle.Button)) spawner.AppearNow();
        }

        static void DrawGiant(Giant giant)
        {
            GUILayout.Label("Giant", DevStyle.Section);
            DevStyle.Row("State", $"{giant.State}, {giant.Distance:0} m");
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Open the eye now", DevStyle.Button)) giant.OpenEyeNow();
            if (GUILayout.Button("Searchlights and crowd now", DevStyle.Button)) giant.SearchlightsNow();
            GUILayout.EndHorizontal();
        }

        static void DrawBlind(BlindOne blind)
        {
            GUILayout.Label("Blind one", DevStyle.Section);
            DevStyle.Row("State", blind.State == BlindState.Absent ? $"not yet (comes at lamp {blind.Settings.arriveAtLamp})" : $"{blind.State}, {blind.DistanceToPlayer:0} m");
            if (blind.State == BlindState.Absent && GUILayout.Button("Bring it now", DevStyle.Button)) blind.Arrive();
        }

        static void DrawCrawlers(CrawlerSpawner crawlers)
        {
            GUILayout.Label("Crawlers", DevStyle.Section);
            if (!crawlers.Active)
            {
                DevStyle.Row("State", $"not yet (they come at lamp {Difficulty.Now.crawlersFromLamp})");
                if (GUILayout.Button("Bring them now", DevStyle.Button)) crawlers.Begin();
                return;
            }
            foreach (var c in crawlers.Crawlers)
                if (c != null) DevStyle.Row($"#{c.Id}", $"{c.State}, {c.DistanceToPlayer:0.0} m");
        }

        static void DrawFlier(Flier flier)
        {
            GUILayout.Label("Flier", DevStyle.Section);
            DevStyle.Row("State", flier.State == FlierState.Absent ? $"not yet (comes at lamp {flier.Settings.arriveAtLamp})" : $"{flier.State}, {flier.DistanceToPlayer:0} m");
            if (flier.State == FlierState.Absent && GUILayout.Button("Bring it now", DevStyle.Button)) flier.Arrive();
        }

        static void GiveEverything(Hotbar hotbar)
        {
            hotbar.Give(ItemKind.Batteries, 3);
            hotbar.Give(ItemKind.Torch);
            hotbar.Give(ItemKind.Lighter);
            hotbar.Give(ItemKind.FlareGun);
            hotbar.Give(ItemKind.Glowsticks, hotbar.Settings.glowsticksPerBundle);
            hotbar.Give(ItemKind.Camera);
            hotbar.Give(ItemKind.KeyFob);
        }

        static void LightNearestLamp()
        {
            if (!Services.TryGet<PlayerController>(out var player)) return;
            Game.World.Lamp best = null;
            float bestDist = float.MaxValue;
            foreach (var lamp in Object.FindObjectsByType<Game.World.Lamp>())
            {
                if (lamp.Lit) continue;
                float d = (lamp.transform.position - player.transform.position).sqrMagnitude;
                if (d < bestDist) { bestDist = d; best = lamp; }
            }
            if (best != null) best.LightNow();
        }
    }
}
