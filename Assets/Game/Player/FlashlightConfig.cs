using System;
using Game.Core;
using UnityEngine;

namespace Game.Player
{
    /// <summary>The flashlight's settings (section 4.6 of the plan), in Assets/Config/Player/FlashlightConfig.asset.</summary>
    [CreateAssetMenu(menuName = "Game/Config/Flashlight", fileName = "FlashlightConfig")]
    public sealed class FlashlightConfig : GameConfig
    {
        [Tooltip("Your own beam shape (a light cookie: white where it's bright). Empty: the built-in one, shaped by Edge Feather.")]
        public Texture2D cookie;

        public FlashlightSettings settings = new FlashlightSettings();
    }

    [Serializable]
    public sealed class FlashlightSettings
    {
        [Header("Beam")]
        public Color color = new Color(1f, 0.93f, 0.82f);
        [Range(0f, 200f), Tooltip("Brightness (candela).")]
        public float intensity = 20.4f;
        [Range(0f, 5f), Tooltip("Multiplies the brightness.")]
        public float boost = 2f;
        [Range(2f, 80f), Tooltip("How far the beam reaches at full strength (metres).")]
        public float range = 24f;
        [Range(1f, 4f), Tooltip("The light carries on this many times the range, fading, so it dies away gradually.")]
        public float rangeReach = 2.5f;
        [Range(5f, 90f), Tooltip("How wide the beam is (degrees).")]
        public float beamAngle = 31f;
        [Range(0f, 1f), Tooltip("How soft the beam's edge is: 0 a crisp ring, 1 a gentle fade from the centre.")]
        public float edgeFeather = 0f;
        public bool shadows = true;
        [Range(0f, 2f), Tooltip("How much the beam shows in the fog and mist.")]
        public float fogGlow = 0.12f;

        [Header("Spill")]
        [Range(0f, 0.5f), Tooltip("A wide, faint light round the beam, so the edges of the view aren't black. 0: none.")]
        public float spillStrength = 0.08f;
        [Range(30f, 170f), Tooltip("How wide the spill is (degrees).")]
        public float spillAngle = 85f;
        [Range(0f, 1f), Tooltip("How much the spill lights the fog, as a share of the beam's. Low, or the near fog goes milky.")]
        public float spillFogGlow = 0.3f;

        [Header("Bounce")]
        [Range(0f, 1f), Tooltip("Light bouncing back off what the beam hits close by, tinted by it. 0: none.")]
        public float bounceStrength = 0.25f;
        [Range(1f, 30f), Tooltip("Beyond this the beam's too spread out for any bounce (metres).")]
        public float bounceDistance = 12f;
        [Range(1f, 20f), Tooltip("How far the bounced light reaches (metres).")]
        public float bounceRange = 7f;

        [Header("Up close")]
        [Range(0f, 10f), Tooltip("Pointed at something nearer than this, the beam dims so it doesn't blaze white (metres).")]
        public float nearDimDistance = 4f;
        [Range(0f, 1f), Tooltip("How bright the beam is up close.")]
        public float nearDim = 0.35f;

        [Header("Battery")]
        [Range(10f, 600f), Tooltip("Seconds a full battery lasts with the light on.")]
        public float batteryLife = 150f;
        [Range(0f, 1f), Tooltip("Below this charge the beam dims and flickers more and more.")]
        public float lowBattery = 0.2f;
        [Range(0f, 1f), Tooltip("How bright the beam is when the battery's nearly dead.")]
        public float lowDim = 0.45f;

        [Header("Hand")]
        [Tooltip("Where the flashlight is held, from the eye (right, up, forward; metres).")]
        public Vector3 handOffset = new Vector3(0.25f, -0.2f, 0.2f);
        [Range(0f, 30f), Tooltip("How quickly the beam catches up with the view. Lower: more lag. 0: none.")]
        public float swaySpeed = 5.5f;
        [Range(0f, 45f), Tooltip("Most the beam can trail behind the view (degrees).")]
        public float maxLag = 14f;

        public FlashlightSettings Clone() => (FlashlightSettings)MemberwiseClone();
    }
}
