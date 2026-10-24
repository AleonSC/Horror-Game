using Game.Core;
using Game.Entities;
using Game.Player;
using UnityEngine;

namespace Game.DevTools
{
    /// <summary>
    /// The Hunters tab: every setting of the giant, the blind one, the crawlers, the flier and the scours, as live
    /// sliders, with search and reset. In the editor the changes are kept in their config assets after Play.
    /// </summary>
    sealed class HuntersTab : IDevTab
    {
        string search = "";

        public string Title => "Hunters";

        public void Draw()
        {
            if (!Services.TryGet<GameSetup>(out var setup))
            {
                GUILayout.Label("The game isn't running.", DevStyle.MutedLabel);
                return;
            }

            GUILayout.BeginHorizontal();
            GUILayout.Label("Search", DevStyle.MutedLabel, GUILayout.Width(66));
            search = GUILayout.TextField(search, DevStyle.TextField);
            if (search.Length > 0 && GUILayout.Button("×", DevStyle.SmallButton)) search = "";
            GUILayout.EndHorizontal();
            GUILayout.Label("Most values change live; a few (like how many empty chains) on Restart. ↺ puts one back to its default. " +
                            "In the editor, what you set here stays in the config assets after you stop.", DevStyle.MutedLabel);
            CatchFlow.Invincible = DevStyle.Switch("Invincible (nothing can take you)", CatchFlow.Invincible);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Restart run", DevStyle.Button) && Services.TryGet<GameRoot>(out var root)) root.Restart();
            if (Services.TryGet<Giant>(out var giant) && Services.TryGet<PlayerController>(out var player) &&
                GUILayout.Button("Look at the giant", DevStyle.Button))
                player.DrawGazeTo(giant.ShownAt, 1.2f);
            GUILayout.EndHorizontal();
            if (giant != null)
            {
                var g = giant.Settings;
                DevStyle.Row("Giant", $"{giant.State}, {giant.Distance:0} m away (hangs {g.HangDistance:0} m off), {g.height:0} m tall");
            }

            Section(setup.Get<GiantConfig>(), "Giant", c => c.settings, () => new GiantSettings());
            Section(setup.Get<BlindConfig>(), "Blind one", c => c.settings, () => new BlindSettings());
            Section(setup.Get<CrawlerConfig>(), "Crawlers", c => c.settings, () => new CrawlerSettings());
            Section(setup.Get<FlierConfig>(), "Flier", c => c.settings, () => new FlierSettings());
            Section(setup.Get<ScourConfig>(), "Scours", c => c.settings, () => new ScourSettings());
        }

        void Section<TConfig>(TConfig config, string title, System.Func<TConfig, object> settings, System.Func<object> defaults) where TConfig : Object
        {
            if (config == null) return;
            var current = settings(config);
            var fresh = defaults();
            if (SettingsDrawer.Draw(title, current, fresh, search)) LookTools.MarkChanged(config);
            if (string.IsNullOrEmpty(search) && GUILayout.Button($"Reset {title.ToLowerInvariant()} to defaults", DevStyle.Button))
            {
                // Every value back to the built-in one (and so saved in its config asset).
                foreach (var f in current.GetType().GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance))
                    f.SetValue(current, f.GetValue(fresh));
                LookTools.MarkChanged(config);
            }
        }
    }
}
