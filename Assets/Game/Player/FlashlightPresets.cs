using System;
using UnityEngine;

namespace Game.Player
{
    /// <summary>
    /// Ready-made beams to pick from (the dev window's Lighting tab). Each sets only how the light looks (colour,
    /// brightness, reach, width, edge, fog and spill); the battery and the hand stay as they are. Pick one, then
    /// fine-tune it in the Look tab.
    /// </summary>
    public static class FlashlightPresets
    {
        public sealed class Preset
        {
            public string Name, Note;
            public Action<FlashlightSettings> Set;
        }

        public static readonly Preset[] All =
        {
            new Preset { Name = "Standard", Note = "The game's own beam: warm white, crisp edge.", Set = s => { } },
            new Preset
            {
                Name = "Old torch", Note = "An old bulb torch: yellow, soft and short.",
                Set = s =>
                {
                    s.color = new Color(1f, 0.78f, 0.5f);
                    s.intensity = 13f; s.range = 17f; s.rangeReach = 2f; s.beamAngle = 38f; s.edgeFeather = 0.55f;
                    s.fogGlow = 0.09f; s.spillStrength = 0.12f; s.spillAngle = 95f; s.bounceStrength = 0.3f;
                },
            },
            new Preset
            {
                Name = "Tactical LED", Note = "Cold blue-white, bright and far, a hard edge.",
                Set = s =>
                {
                    s.color = new Color(0.86f, 0.93f, 1f);
                    s.intensity = 34f; s.range = 36f; s.rangeReach = 2.5f; s.beamAngle = 22f; s.edgeFeather = 0f;
                    s.fogGlow = 0.16f; s.spillStrength = 0.05f; s.spillAngle = 70f; s.bounceStrength = 0.2f;
                },
            },
            new Preset
            {
                Name = "Floodlight", Note = "A wide, even wash that doesn't reach far.",
                Set = s =>
                {
                    s.color = new Color(1f, 0.95f, 0.88f);
                    s.intensity = 22f; s.range = 19f; s.rangeReach = 2.2f; s.beamAngle = 62f; s.edgeFeather = 0.7f;
                    s.fogGlow = 0.1f; s.spillStrength = 0.15f; s.spillAngle = 120f; s.bounceStrength = 0.3f;
                },
            },
            new Preset
            {
                Name = "Spotlight", Note = "A narrow pencil of light that carries a long way.",
                Set = s =>
                {
                    s.color = new Color(1f, 0.96f, 0.9f);
                    s.intensity = 48f; s.range = 50f; s.rangeReach = 2f; s.beamAngle = 13f; s.edgeFeather = 0.15f;
                    s.fogGlow = 0.2f; s.spillStrength = 0.04f; s.spillAngle = 60f; s.bounceStrength = 0.15f;
                },
            },
            new Preset
            {
                Name = "Dim and grim", Note = "Barely enough to see by: orange, short, soft.",
                Set = s =>
                {
                    s.color = new Color(1f, 0.7f, 0.42f);
                    s.intensity = 7f; s.range = 13f; s.rangeReach = 1.8f; s.beamAngle = 34f; s.edgeFeather = 0.5f;
                    s.fogGlow = 0.07f; s.spillStrength = 0.06f; s.spillAngle = 80f; s.bounceStrength = 0.2f;
                },
            },
            new Preset
            {
                Name = "Moonlit", Note = "A pale, cool beam with a soft edge.",
                Set = s =>
                {
                    s.color = new Color(0.8f, 0.86f, 1f);
                    s.intensity = 18f; s.range = 26f; s.rangeReach = 2.5f; s.beamAngle = 30f; s.edgeFeather = 0.35f;
                    s.fogGlow = 0.14f; s.spillStrength = 0.1f; s.spillAngle = 90f; s.bounceStrength = 0.25f;
                },
            },
        };

        /// <summary>Make <paramref name="settings"/> look like the preset (in place, so the light changes now).</summary>
        public static void Apply(Preset preset, FlashlightSettings settings)
        {
            var look = new FlashlightSettings();
            preset.Set(look);
            // Only the look: the battery and the hand are left alone.
            look.batteryLife = settings.batteryLife;
            look.lowBattery = settings.lowBattery;
            look.lowDim = settings.lowDim;
            look.handOffset = settings.handOffset;
            look.swaySpeed = settings.swaySpeed;
            look.maxLag = settings.maxLag;
            JsonUtility.FromJsonOverwrite(JsonUtility.ToJson(look), settings);
        }
    }
}
