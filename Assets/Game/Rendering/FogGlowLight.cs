using Game.Core;
using UnityEngine;
using UnityEngine.Rendering.HighDefinition;

namespace Game.Rendering
{
    /// <summary>
    /// Put on a lamp, eye or other round light: how much it glows in the fog follows the look's
    /// "Lamp Fog Glow" (times <see cref="strength"/>), so one slider tames them all.
    /// </summary>
    [RequireComponent(typeof(Light))]
    public sealed class FogGlowLight : MonoBehaviour
    {
        [Range(0f, 4f), Tooltip("This light's share of the look's Lamp Fog Glow.")]
        public float strength = 1f;

        HDAdditionalLightData data;

        void Awake() => data = GetComponent<HDAdditionalLightData>();

        void LateUpdate()
        {
            if (data == null || !Services.TryGet<LookController>(out var look)) return;
            var s = look.Config.settings;
            data.volumetricDimmer = s.lampFogGlow * strength;
        }
    }
}
