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
        public GameObject map;
        public GameObject drone;
        public GameObject radio;

        [Header("Lying in the world (empty: the held model)")]
        public GameObject batteryPickup;
        public GameObject torchPickup;
        public GameObject lighterPickup;
        public GameObject flareGunPickup;
        public GameObject glowsticksPickup;
        public GameObject cameraPickup;
        public GameObject keyFobPickup;
        public GameObject mapPickup;
        public GameObject dronePickup;
        public GameObject radioPickup;
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
                case ItemKind.Map: return map;
                case ItemKind.Drone: return drone;
                case ItemKind.Radio: return radio;
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
                case ItemKind.Map: own = mapPickup; break;
                case ItemKind.Drone: own = dronePickup; break;
                case ItemKind.Radio: own = radioPickup; break;
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
        public float batteryGlowStrength = 0.04f;
        [Range(0.2f, 5f), Tooltip("How far that light reaches (metres).")]
        public float batteryGlowRange = 1.5f;
        [Range(0f, 10f), Tooltip("The green outline of light round a battery lying in the forest.")]
        public float batteryOutline = 0.8f;
        [Range(0.1f, 2f), Tooltip("How big a battery's grab target is (radius, metres): bigger is easier to take running past.")]
        public float batteryGrabRadius = 0.55f;
        [Tooltip("The outline of light traced round anything else lying about to be picked up.")]
        public Color itemOutlineColor = new Color(0.9f, 0.92f, 1f);
        [Range(0f, 10f), Tooltip("How bright that outline is (0: none).")]
        public float itemOutline = 0.35f;

        [Header("Torch")]
        [Range(10f, 600f), Tooltip("Seconds a torch burns.")]
        public float torchBurnTime = 120f;
        [Range(0f, 1f), Tooltip("Lighting it uses this much of the lighter's fuel.")]
        public float torchLightCost = 0.1f;
        [Range(0f, 30f), Tooltip("Radius of the safe circle round a lit torch (metres).")]
        public float torchSafeRadius = 7f;
        public Color torchColor = new Color(1f, 0.55f, 0.22f);
        [Range(0f, 200f)] public float torchIntensity = 6f;
        [Range(1f, 40f)] public float torchRange = 11f;
        [Range(0f, 1f), Tooltip("How much the torch lights up the fog round you (1: fully, which washes out the screen).")]
        public float torchFogGlow = 0.12f;

        [Header("Lighter")]
        [Range(5f, 600f), Tooltip("Seconds of flame in a full lighter.")]
        public float lighterFuelTime = 60f;
        [Range(0f, 20f), Tooltip("Radius of the safe circle round an open lighter (metres).")]
        public float lighterSafeRadius = 3.5f;
        public Color lighterColor = new Color(1f, 0.65f, 0.3f);
        [Range(0f, 50f)] public float lighterIntensity = 1.2f;
        [Range(1f, 20f)] public float lighterRange = 4.5f;
        [Range(0f, 1f), Tooltip("How much the lighter lights up the fog round you.")]
        public float lighterFogGlow = 0.08f;
        [Range(0f, 20f), Tooltip("How bright the flame itself glows (torch and lighter; the built-in flame only).")]
        public float flameGlow = 2f;

        [Header("Flare gun")]
        [Range(5f, 80f), Tooltip("How fast the flare leaves the gun (metres a second).")]
        public float flareSpeed = 38f;
        [Range(0f, 90f), Tooltip("Aimed at least this far above level, it bursts in the sky. Lower, it's wasted.")]
        public float flareSkyAngle = 30f;
        [Range(1f, 60f), Tooltip("Seconds the burst floods the forest with red light.")]
        public float flareBurnTime = 14f;
        public Color flareColor = new Color(1f, 0.22f, 0.1f);
        [Range(0f, 100000f), Tooltip("Brightness of the burst (candela: it's far up).")]
        public float flareIntensity = 6000f;
        [Range(10f, 500f)] public float flareRange = 160f;
        [Range(0f, 1f), Tooltip("How much the burst lights up the fog (1: fully; lower keeps it from turning everything red).")]
        public float flareFogGlow = 0.15f;
        [Range(0f, 5f), Tooltip("The red haze round the burning flare in the sky.")]
        public float flareHaze = 0.5f;

        [Header("Glowsticks")]
        [Range(1, 30), Tooltip("Glowsticks in a bundle.")]
        public int glowsticksPerBundle = 10;
        [Range(0.5f, 15f), Tooltip("How far you toss one (metres).")]
        public float glowstickToss = 3f;
        [Range(10f, 900f), Tooltip("Seconds one glows.")]
        public float glowstickTime = 240f;
        public Color glowstickColor = new Color(1f, 0.85f, 0.15f);
        [Range(0f, 20f)] public float glowstickIntensity = 0.8f;
        [Range(1f, 15f)] public float glowstickRange = 5f;
        [Range(1, 100), Tooltip("At most this many glowing at once (the oldest goes out).")]
        public int maxGlowsticks = 30;

        [Header("Drone")]
        [Range(10f, 600f), Tooltip("Seconds of flight in a full battery.")]
        public float droneFlightTime = 150f;
        [Range(20f, 600f), Tooltip("How far from you it can fly before the signal's gone (metres).")]
        public float droneRange = 260f;
        [Range(0f, 1f), Tooltip("Battery used while it hovers on its own, as a share of what flying uses.")]
        public float droneHoverDrain = 0.35f;
        [Range(0f, 1f), Tooltip("Static over its picture even at full signal (it thickens as the signal goes).")]
        public float droneStatic = 0.12f;
        [Range(0f, 1f), Tooltip("How thick all its static is (the baseline, the bursts and the signal going), as a share: 1 the full amount.")]
        public float droneStaticAmount = 0.5f;
        [Range(1f, 20f)] public float droneSpeed = 6f;
        [Range(1f, 4f), Tooltip("Shift: this many times faster.")]
        public float droneBoost = 2.2f;
        [Range(0.5f, 10f)] public float droneClimbSpeed = 3f;
        [Range(10f, 200f)] public float droneMaxHeight = 60f;
        [Range(0f, 400f), Tooltip("How far its motors carry (the blind one hears them).")]
        public float droneNoise = 220f;
        public Color droneLampColor = new Color(0.85f, 0.9f, 1f);
        [Range(0f, 200f)] public float droneLampIntensity = 25f;
        [Range(1f, 60f)] public float droneLampRange = 30f;

        [Header("Radio")]
        [Range(5f, 200f), Tooltip("How far the song carries for you (metres).")]
        public float radioRange = 45f;
        [Range(0f, 1f)] public float radioVolume = 0.8f;
        [Range(0.5f, 10f), Tooltip("Within this, the blind one smashes it (metres).")]
        public float radioSmashDistance = 3f;

        [Header("Luminescent spray")]
        [Range(0.5f, 10f), Tooltip("How far it reaches (metres).")]
        public float sprayReach = 3.5f;
        [Range(0.01f, 0.3f), Tooltip("Seconds between blots while you hold F.")]
        public float sprayInterval = 0.035f;
        [Range(0.0005f, 0.05f), Tooltip("Share of the can each blot uses (1/700: about 25 s of spraying).")]
        public float sprayPerMark = 1f / 700f;
        [Range(0.03f, 0.6f), Tooltip("How big each blot is (metres).")]
        public float spraySize = 0.16f;
        public Color sprayColor = new Color(0.55f, 1f, 0.6f);
        [Range(0f, 3f), Tooltip("How much the paint glows in the dark.")]
        public float sprayGlow = 0.35f;
        [Range(100, 10000), Tooltip("Most blots in the world at once (the oldest go).")]
        public int maxSprayMarks = 2500;

        [Header("Camera")]
        [Range(1, 20)] public int cameraShots = 3;
        [Range(5f, 100f), Tooltip("The flash drives off anything in frame this close, with a clear line to it (metres).")]
        public float cameraRange = 40f;
        [Range(5f, 90f), Tooltip("Half the width of the frame (degrees).")]
        public float cameraHalfAngle = 30f;
        [Range(1f, 120f), Tooltip("Seconds what it drives off stays away.")]
        public float cameraRepelTime = 30f;
        [Range(0f, 2000f)] public float cameraFlashIntensity = 600f;
        [Range(128, 1024), Tooltip("Size of a polaroid's picture (pixels, square).")]
        public int photoSize = 512;
        [Range(1f, 30f), Tooltip("Seconds for a polaroid to develop.")]
        public float photoDevelopTime = 8f;

        [Header("Key fob")]
        [Range(5f, 200f), Tooltip("How far off things hear the car's chirp (metres).")]
        public float carChirpHeard = 60f;

        [Header("Snack")]
        [Range(0f, 1f), Tooltip("Each snack adds this much sprint time for the run (0.25 = 25%).")]
        public float snackBonus = 0.25f;

        [Header("Hand")]
        [Tooltip("Where held things sit, from the eye (right, up, forward; metres).")]
        public Vector3 handOffset = new Vector3(0.28f, -0.26f, 0.45f);
        [Tooltip("Where the drone's remote sits, from the eye (right, up, forward; metres). Higher and nearer the middle than other held things, so its screen is in view.")]
        public Vector3 remoteOffset = new Vector3(0.16f, -0.15f, 0.42f);

        public ItemSettings Clone() => (ItemSettings)MemberwiseClone();
    }
}
