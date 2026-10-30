using Game.Rendering;
using UnityEngine;

namespace Game.World
{
    /// <summary>
    /// The pair of caged bulkhead lamps either side of the bunker's door: each slowly swells green and fades, then
    /// swells red and fades, over and over, the two of them out of step (one green while the other's red).
    /// </summary>
    public sealed class BunkerLights : MonoBehaviour
    {
        [Tooltip("Seconds for one lamp to go once through green and red.")]
        public float period = 6f;
        public Color green = new Color(0.25f, 1f, 0.35f);
        public Color red = new Color(1f, 0.12f, 0.08f);
        [Tooltip("How bright the frosted glass glows at the top of a swell.")]
        public float glassGlow = 0.3f;
        [Tooltip("Each lamp's light on the front and the ground (candela).")]
        public float lightIntensity = 2.5f;
        [Range(1f, 20f)] public float lightRange = 7f;

        Material[] glass;
        Light[] lights;

        public void Set(Material[] glassMaterials, Light[] lamps)
        {
            glass = glassMaterials;
            lights = lamps;
        }

        void Update()
        {
            if (glass == null) return;
            float t = Time.time / Mathf.Max(0.5f, period);
            for (int i = 0; i < glass.Length; i++)
            {
                float u = Mathf.Repeat(t + i * 0.5f, 1f);           // (the second half a cycle behind the first)
                bool isGreen = u < 0.5f;
                float swell = Mathf.Sin(Mathf.Repeat(u * 2f, 1f) * Mathf.PI); // (up and down within each half)
                swell *= swell;
                Color c = isGreen ? green : red;
                RuntimeMaterials.SetGlow(glass[i], c * glassGlow * swell);
                if (lights != null && i < lights.Length && lights[i] != null)
                {
                    lights[i].color = c;
                    lights[i].intensity = lightIntensity * swell;
                    lights[i].range = lightRange;
                    lights[i].enabled = swell > 0.01f;
                }
            }
        }
    }
}
