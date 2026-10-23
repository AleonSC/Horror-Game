using Game.Core;
using Game.Player;
using Game.Rendering;
using UnityEngine;

namespace Game.DevTools
{
    /// <summary>
    /// What the Lighting and Look tabs share: finding the configs, keeping edits, and saving or copying them.
    /// In the editor, edits go straight into the config assets (kept after Play); Save writes them to disk so
    /// GitHub Desktop sees them.
    /// </summary>
    static class LookTools
    {
        public static LookConfig Look => Services.TryGet<LookController>(out var c) ? c.Config : null;

        public static FlashlightConfig Flashlight =>
            Services.TryGet<GameSetup>(out var setup) ? setup.Get<FlashlightConfig>() : null;

        public static string LastMessage;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetOnPlay() => LastMessage = null;

        public static void MarkChanged(Object config)
        {
#if UNITY_EDITOR
            if (config != null) UnityEditor.EditorUtility.SetDirty(config);
#endif
        }

        public static void DrawSaveAndCopy()
        {
            GUILayout.BeginHorizontal();
#if UNITY_EDITOR
            if (GUILayout.Button(new GUIContent("Save", "Write the settings to the config assets, ready to commit"), DevStyle.Button))
            {
                if (Look != null) UnityEditor.AssetDatabase.SaveAssetIfDirty(Look);
                if (Flashlight != null) UnityEditor.AssetDatabase.SaveAssetIfDirty(Flashlight);
                LastMessage = "Saved. Commit and push the Config changes in GitHub Desktop.";
            }
#endif
            if (GUILayout.Button(new GUIContent("Copy for Claude", "Copy every look and flashlight value as text"), DevStyle.Button))
            {
                var text = "Look settings";
                if (Look != null) text += $" (from theme {Look.theme}):\n{JsonUtility.ToJson(Look.settings, true)}";
                if (Flashlight != null) text += $"\n\nFlashlight settings:\n{JsonUtility.ToJson(Flashlight.settings, true)}";
                GUIUtility.systemCopyBuffer = text;
                LastMessage = "Copied. Paste it into the chat.";
            }
            GUILayout.EndHorizontal();
            if (!string.IsNullOrEmpty(LastMessage)) GUILayout.Label(LastMessage, DevStyle.MutedLabel);
        }
    }

    /// <summary>The Lighting tab: themes and the main few sliders.</summary>
    sealed class LightingTab : IDevTab
    {
        public string Title => "Lighting";

        public void Draw()
        {
            var look = LookTools.Look;
            if (look == null)
            {
                GUILayout.Label("The look controller isn't running.", DevStyle.MutedLabel);
                return;
            }

            GUILayout.Label("Theme", DevStyle.Section);
            GUILayout.Label($"Started from: {look.theme}. Choosing one replaces the look settings (not the flashlight).", DevStyle.MutedLabel);
            var names = LookThemes.Names;
            for (int i = 0; i < names.Length; i += 3)
            {
                GUILayout.BeginHorizontal();
                for (int k = i; k < Mathf.Min(i + 3, names.Length); k++)
                {
                    if (GUILayout.Button(names[k], names[k] == look.theme ? DevStyle.TabActive : DevStyle.Tab))
                    {
                        look.ApplyTheme(names[k]);
                        LookTools.MarkChanged(look);
                    }
                }
                GUILayout.EndHorizontal();
            }

            var s = look.settings;
            GUILayout.Label("Main settings", DevStyle.Section);
            bool changed = false;
            changed |= Slider("Brightness", ref s.postExposure, -3f, 5f);
            changed |= Slider("Fog thickness", ref s.fogDensity, 0.001f, 0.3f);
            changed |= Slider("Mist thickness (lower: thicker)", ref s.mistThickness, 0.5f, 60f);
            changed |= Slider("Mist height", ref s.mistHeight, 0.3f, 6f);
            changed |= Slider("Moonlight", ref s.moonBrightness, 0f, 5f);
            changed |= Slider("Contrast", ref s.contrast, -100f, 100f);
            changed |= Slider("Saturation", ref s.saturation, -100f, 100f);
            if (changed) LookTools.MarkChanged(look);

            var flashlight = LookTools.Flashlight;
            if (flashlight != null && Slider("Flashlight brightness", ref flashlight.settings.intensity, 0f, 200f))
                LookTools.MarkChanged(flashlight);

            GUILayout.Label("Every other value is in the Look tab.", DevStyle.MutedLabel);
            GUILayout.Space(6);
            LookTools.DrawSaveAndCopy();
        }

        static bool Slider(string label, ref float value, float min, float max)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(label, DevStyle.Label, GUILayout.Width(170));
            float v = GUILayout.HorizontalSlider(value, min, max);
            GUILayout.Label(v.ToString("0.###"), DevStyle.MutedLabel, GUILayout.Width(52));
            GUILayout.EndHorizontal();
            if (Mathf.Approximately(v, value)) return false;
            value = v;
            return true;
        }
    }

    /// <summary>The Look tab: every look and flashlight value, in folding sections, with search and reset.</summary>
    sealed class LookTab : IDevTab
    {
        string search = "";

        public string Title => "Look";

        public void Draw()
        {
            var look = LookTools.Look;
            if (look == null)
            {
                GUILayout.Label("The look controller isn't running.", DevStyle.MutedLabel);
                return;
            }

            GUILayout.BeginHorizontal();
            GUILayout.Label("Search", DevStyle.MutedLabel, GUILayout.Width(50));
            search = GUILayout.TextField(search, DevStyle.TextField);
            if (search.Length > 0 && GUILayout.Button("×", DevStyle.SmallButton)) search = "";
            GUILayout.EndHorizontal();
            GUILayout.Label("↺ puts a value back to its default (the theme's, for the look).", DevStyle.MutedLabel);

            var lookDefaults = LookThemes.Get(look.theme) ?? new LookSettings();
            if (SettingsDrawer.Draw("Look", look.settings, lookDefaults, search)) LookTools.MarkChanged(look);

            var flashlight = LookTools.Flashlight;
            if (flashlight != null && SettingsDrawer.Draw("Flashlight", flashlight.settings, new FlashlightSettings(), search))
                LookTools.MarkChanged(flashlight);

            GUILayout.Space(6);
            LookTools.DrawSaveAndCopy();
        }
    }
}
