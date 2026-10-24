using Game.Audio;
using Game.Core;
using UnityEngine;
using UnityEngine.Rendering.HighDefinition;

namespace Game.Player
{
    /// <summary>
    /// A fired flare (section 6.3). Fired into the sky, it bursts at the top of its arc and floods the forest with
    /// red light, and every hunt starts over. Fired low, it's wasted: it skids along the ground and fizzles out.
    /// </summary>
    public sealed class Flare : MonoBehaviour
    {
        ItemSettings s;
        Vector3 velocity;
        bool sky, burst, landed;
        float burnLeft;
        Light glow;
        Renderer spark, fire, halo;

        public static void Fire(ItemSettings s, Vector3 from, Vector3 direction)
        {
            var go = new GameObject("Flare") { layer = Layers.Effects };
            go.transform.position = from;
            var flare = go.AddComponent<Flare>();
            flare.s = s;
            flare.velocity = direction.normalized * s.flareSpeed;
            float angle = Mathf.Asin(Mathf.Clamp(direction.normalized.y, -1f, 1f)) * Mathf.Rad2Deg;
            flare.sky = angle >= s.flareSkyAngle;

            var sparkGo = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            Destroy(sparkGo.GetComponent<Collider>());
            sparkGo.transform.SetParent(go.transform, false);
            sparkGo.transform.localScale = Vector3.one * 0.12f;
            var r = sparkGo.GetComponent<Renderer>();
            r.sharedMaterial = Game.Rendering.RuntimeMaterials.Glow("Flare spark", s.flareColor * 8f);
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            flare.spark = r;
            // A burning flare: a flame streaming off it and a red glow round it in the air.
            flare.fire = Game.Rendering.Effects.Flame(go.transform, Vector3.zero, 0.18f, 0.45f, Color.white * 4f, "Flare fire");
            flare.halo = Game.Rendering.Effects.Glow(go.transform, Vector3.zero, 2f, s.flareColor * 1.5f, 0.5f, 2.5f, "Flare glow");
            if (flare.fire != null) Game.Rendering.Effects.SetGlow(flare.fire, s.flareColor * 3f);

            var lightGo = new GameObject("Flare light");
            lightGo.transform.SetParent(go.transform, false);
            var data = lightGo.AddHDLight(LightType.Point);
            if (data != null) data.volumetricDimmer = s.flareFogGlow; // (so the fog doesn't drown everything in red)
            flare.glow = lightGo.GetComponent<Light>();
            flare.glow.shadows = LightShadows.None;
            flare.glow.color = s.flareColor;
            flare.glow.intensity = 15f;
            flare.glow.range = 8f;
            flare.burnLeft = s.flareBurnTime;
        }

        void Update()
        {
            float dt = Time.deltaTime;
            if (!burst && !landed)
            {
                velocity += Physics.gravity * dt;
                Vector3 step = velocity * dt;
                int mask = ~(Layers.Mask(Layers.PlayerBody) | Layers.Mask(Layers.Effects) | Layers.Mask(Layers.Entities));
                if (Physics.Raycast(transform.position, step.normalized, out var hit, step.magnitude, mask, QueryTriggerInteraction.Ignore))
                {
                    transform.position = hit.point + hit.normal * 0.05f;
                    Land();
                    return;
                }
                transform.position += step;
                if (sky && velocity.y <= 0f) Burst();
                return;
            }

            burnLeft -= dt;
            float life = Mathf.Clamp01(burnLeft / Mathf.Max(0.01f, burst ? s.flareBurnTime : s.flareBurnTime * 0.4f));
            float flicker = 0.85f + 0.15f * Mathf.PerlinNoise(Time.time * 9f, 0.3f);
            if (burst)
            {
                // Drifting slowly down, burning out.
                transform.position += Vector3.down * 1.2f * dt;
                glow.intensity = s.flareIntensity * Mathf.SmoothStep(0f, 1f, life) * flicker;
            }
            else glow.intensity = 12f * life * flicker;
            if (halo != null)
            {
                halo.transform.localScale = Vector3.one * (burst ? 7f : 2f);
                Game.Rendering.Effects.SetGlow(halo, s.flareColor * ((burst ? s.flareHaze : 1.5f) * life * flicker));
            }
            if (fire != null) Game.Rendering.Effects.SetGlow(fire, s.flareColor * (3f * life));
            if (burnLeft <= 0f) Destroy(gameObject);
        }

        void Burst()
        {
            burst = true;
            glow.range = s.flareRange;
            glow.intensity = s.flareIntensity;
            spark.transform.localScale = Vector3.one * 0.6f;
            if (Services.TryGet<SoundPlayer>(out var sounds)) sounds.Play2D(Sound.FlareBurst, MixerNames.World, 0.9f);
            if (Services.TryGet<EventBus>(out var events))
            {
                events.Publish(new HuntsReset("a flare in the sky"));
                events.Publish(new NoiseEmitted(transform.position, 200f, "flare burst"));
            }
        }

        void Land()
        {
            landed = true;
            velocity = Vector3.zero;
            burnLeft = s.flareBurnTime * 0.4f;
            glow.range = 10f;
        }
    }
}
