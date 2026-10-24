using System;
using Game.Core;
using UnityEngine;

namespace Game.Player
{
    /// <summary>The hotbar items (section 6.3 of the plan), in Assets/Config/Player/ItemsConfig.asset.</summary>
    [CreateAssetMenu(menuName = "Game/Config/Items", fileName = "ItemsConfig")]
    public sealed class ItemsConfig : GameConfig
    {
        [Tooltip("Your own models, held in the hand and lying in the world. Empty: the built-in ones.")]
        public ItemArt art = new ItemArt();

        public ItemSettings settings = new ItemSettings();
    }

    /// <summary>
    /// Your own item models (docs/custom-assets.md). A held model sits at the hand, pointing forward (+Z); a
    /// world model lies on the ground at its pivot. Your torch and lighter can have a child named "Flame":
    /// it's shown only while lit, and the game's light sits there.
    /// </summary>
    [Serializable]
    public sealed class ItemArt
    {
        [Header("Held")]
        public GameObject flashlight;
        public GameObject battery;
        public GameObject torch;
        public GameObject lighter;
        public GameObject flareGun;
        public GameObject glowstick;
        public GameObject camera;
        public GameObject keyFob;

        [Header("Lying in the world (empty: the held model)")]
        public GameObject batteryPickup;
        public GameObject torchPickup;
        public GameObject lighterPickup;
        public GameObject flareGunPickup;
        public GameObject glowsticksPickup;
        public GameObject cameraPickup;
        public GameObject keyFobPickup;
        public GameObject snack;

        public GameObject Held(ItemKind kind)
        {
            switch (kind)
            {
                case ItemKind.Flashlight: return flashlight;
                case ItemKind.Batteries: return battery;
                case ItemKind.Torch: return torch;
                case ItemKind.Lighter: return lighter;
                case ItemKind.FlareGun: return flareGun;
                case ItemKind.Glowsticks: return glowstick;
                case ItemKind.Camera: return camera;
                case ItemKind.KeyFob: return keyFob;
                default: return null;
            }
        }

        public GameObject Lying(ItemKind kind)
        {
            GameObject own;
            switch (kind)
            {
                case ItemKind.Batteries: own = batteryPickup; break;
                case ItemKind.Torch: own = torchPickup; break;
                case ItemKind.Lighter: own = lighterPickup; break;
                case ItemKind.FlareGun: own = flareGunPickup; break;
                case ItemKind.Glowsticks: own = glowsticksPickup; break;
                case ItemKind.Camera: own = cameraPickup; break;
                case ItemKind.KeyFob: own = keyFobPickup; break;
                default: own = null; break;
            }
            return own != null ? own : Held(kind);
        }
    }

    [Serializable]
    public sealed class ItemSettings
    {
        [Header("Batteries")]
        [Range(0f, 1f), Tooltip("How much one battery recharges the flashlight.")]
        public float batteryCharge = 0.1f;
        public Color batteryGlow = new Color(0.25f, 1f, 0.35f);
        [Range(0f, 2f), Tooltip("How bright the pulsing green light on a battery lying in the forest is.")]
        public float batteryGlowStrength = 0.12f;
        [Range(0.2f, 5f), Tooltip("How far that light reaches (metres).")]
        public float batteryGlowRange = 1.5f;

        [Header("Torch")]
        [Range(10f, 600f), Tooltip("Seconds a torch burns.")]
        public float torchBurnTime = 120f;
        [Range(0f, 1f), Tooltip("Lighting it uses this much of the lighter's fuel.")]
        public float torchLightCost = 0.1f;
        [Range(0f, 30f), Tooltip("Radius of the safe circle round a lit torch (metres).")]
        public float torchSafeRadius = 7f;
        public Color torchColor = new Color(1f, 0.55f, 0.22f);
        [Range(0f, 200f)] public float torchIntensity = 18f;
        [Range(1f, 40f)] public float torchRange = 11f;

        [Header("Lighter")]
        [Range(5f, 600f), Tooltip("Seconds of flame in a full lighter.")]
        public float lighterFuelTime = 60f;
        [Range(0f, 20f), Tooltip("Radius of the safe circle round an open lighter (metres).")]
        public float lighterSafeRadius = 3.5f;
        public Color lighterColor = new Color(1f, 0.65f, 0.3f);
        [Range(0f, 50f)] public float lighterIntensity = 3f;
        [Range(1f, 20f)] public float lighterRange = 4.5f;

        [Header("Flare gun")]
        [Range(5f, 80f), Tooltip("How fast the flare leaves the gun (metres a second).")]
        public float flareSpeed = 38f;
        [Range(0f, 90f), Tooltip("Aimed at least this far above level, it bursts in the sky. Lower, it's wasted.")]
        public float flareSkyAngle = 30f;
        [Range(1f, 60f), Tooltip("Seconds the burst floods the forest with red light.")]
        public float flareBurnTime = 14f;
        public Color flareColor = new Color(1f, 0.12f, 0.06f);
        [Range(0f, 100000f), Tooltip("Brightness of the burst (candela: it's far up).")]
        public float flareIntensity = 60000f;
        [Range(10f, 500f)] public float flareRange = 260f;

        [Header("Glowsticks")]
        [Range(1, 30), Tooltip("Glowsticks in a bundle.")]
        public int glowsticksPerBundle = 10;
        [Range(0.5f, 15f), Tooltip("How far you toss one (metres).")]
        public float glowstickToss = 3f;
        [Range(10f, 900f), Tooltip("Seconds one glows.")]
        public float glowstickTime = 240f;
        public Color glowstickColor = new Color(0.35f, 1f, 0.45f);
        [Range(0f, 20f)] public float glowstickIntensity = 1.5f;
        [Range(1f, 15f)] public float glowstickRange = 5f;
        [Range(1, 100), Tooltip("At most this many glowing at once (the oldest goes out).")]
        public int maxGlowsticks = 30;

        [Header("Camera")]
        [Range(1, 20)] public int cameraShots = 3;
        [Range(5f, 100f), Tooltip("The flash drives off anything in frame this close, with a clear line to it (metres).")]
        public float cameraRange = 40f;
        [Range(5f, 90f), Tooltip("Half the width of the frame (degrees).")]
        public float cameraHalfAngle = 30f;
        [Range(1f, 120f), Tooltip("Seconds what it drives off stays away.")]
        public float cameraRepelTime = 30f;
        [Range(0f, 2000f)] public float cameraFlashIntensity = 600f;

        [Header("Key fob")]
        [Range(5f, 200f), Tooltip("How far off things hear the car's chirp (metres).")]
        public float carChirpHeard = 60f;

        [Header("Snack")]
        [Range(0f, 1f), Tooltip("Each snack adds this much sprint time for the run (0.25 = 25%).")]
        public float snackBonus = 0.25f;

        [Header("Hand")]
        [Tooltip("Where held things sit, from the eye (right, up, forward; metres).")]
        public Vector3 handOffset = new Vector3(0.28f, -0.26f, 0.45f);

        public ItemSettings Clone() => (ItemSettings)MemberwiseClone();
    }
}
