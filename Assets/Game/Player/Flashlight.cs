using Game.Core;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering.HighDefinition;

namespace Game.Player
{
    /// <summary>
    /// The hand-held flashlight (section 4.6): a shadowed spot from the right hand that trails the view a
    /// little, a wide faint spill round it, light bounced back off whatever it lands on close by, and dimming
    /// when pointed at something near. Put it on the camera. F switches it on and off (for now; the hotbar
    /// takes this over later). Battery comes with the player in milestone 2.
    /// </summary>
    public sealed class Flashlight : MonoBehaviour
    {
        [Tooltip("Optional: settings to use instead of the ones in the GameSetup.")]
        [SerializeField] FlashlightConfig configOverride;

        FlashlightConfig config;
        Transform hand;
        Light beam, spill, bounce;
        HDAdditionalLightData beamData, spillData, bounceData;
        Texture2D cookie;
        float cookieFeather = -1f;
        float nearLevel = 1f;
        float bounceLevel;
        Color bounceTint = Color.white;
        Collider tintFrom;

        public bool IsOn { get; private set; } = true;
        public FlashlightSettings Settings => config.settings;

        void Awake()
        {
            config = configOverride;
            if (config == null && Services.TryGet<GameSetup>(out var setup)) config = setup.Get<FlashlightConfig>();
            if (config == null)
            {
                Debug.LogWarning("No FlashlightConfig found; using defaults.");
                config = ScriptableObject.CreateInstance<FlashlightConfig>();
            }

            hand = new GameObject("Flashlight").transform;
            hand.SetParent(transform, false);

            beamData = hand.gameObject.AddHDLight(LightType.Spot);
            beam = hand.GetComponent<Light>();

            var spillGo = new GameObject("Flashlight Spill");
            spillGo.transform.SetParent(hand, false);
            spillData = spillGo.AddHDLight(LightType.Spot);
            spill = spillGo.GetComponent<Light>();
            spill.shadows = LightShadows.None;

            // The bounce isn't on the hand: it sits where the beam lands.
            var bounceGo = new GameObject("Flashlight Bounce");
            bounceGo.transform.SetParent(transform, false);
            bounceData = bounceGo.AddHDLight(LightType.Point);
            bounce = bounceGo.GetComponent<Light>();
            bounce.shadows = LightShadows.None;
            bounceData.volumetricDimmer = 0f; // (no glowing ball of fog where it lands)
        }

        void Start() => hand.rotation = transform.rotation;

        void OnDestroy()
        {
            if (cookie != null) Destroy(cookie);
        }

        public void SetOn(bool on) => IsOn = on;

        void Update()
        {
            var keyboard = Keyboard.current;
            if (keyboard != null && keyboard.fKey.wasPressedThisFrame) SetOn(!IsOn);
        }

        void LateUpdate()
        {
            var s = config.settings;
            FollowView(s);
            UpdateBeam(s);
            UpdateSpill(s);
            UpdateBounce(s);
        }

        // The hand follows the eye, and the beam trails the view a little, up to maxLag degrees.
        void FollowView(FlashlightSettings s)
        {
            hand.position = transform.TransformPoint(s.handOffset);
            Quaternion target = transform.rotation;
            Quaternion r = s.swaySpeed > 0f
                ? Quaternion.Slerp(hand.rotation, target, 1f - Mathf.Exp(-s.swaySpeed * Time.deltaTime))
                : target;
            hand.rotation = Quaternion.RotateTowards(target, r, s.maxLag);
        }

        void UpdateBeam(FlashlightSettings s)
        {
            beam.enabled = IsOn;
            if (!IsOn) return;

            if (!Mathf.Approximately(cookieFeather, s.edgeFeather))
            {
                if (cookie != null) Destroy(cookie);
                cookie = FlashlightCookie.Build(s.edgeFeather);
                cookieFeather = s.edgeFeather;
                beamData.SetCookie(cookie);
            }

            // Pointed at something close, dim (smoothly) so it doesn't blaze white.
            float near = 1f;
            if (s.nearDimDistance > 0f && Probe(s.nearDimDistance, out var hit))
                near = Mathf.Lerp(s.nearDim, 1f, Mathf.SmoothStep(0f, 1f, hit.distance / s.nearDimDistance));
            nearLevel = Mathf.Lerp(nearLevel, near, 1f - Mathf.Exp(-8f * Time.deltaTime));

            beam.color = s.color;
            beam.intensity = s.intensity * s.boost * nearLevel; // (candela: a spot light's own unit in HDRP)
            beam.range = s.range * Mathf.Max(1f, s.rangeReach);
            beam.spotAngle = s.beamAngle;
            beam.innerSpotAngle = s.beamAngle * 0.95f; // (the cookie makes the falloff)
            beam.shadows = s.shadows ? LightShadows.Soft : LightShadows.None;
            beamData.volumetricDimmer = s.fogGlow;
        }

        // The same light, much wider and fainter, fading smoothly from the middle out.
        void UpdateSpill(FlashlightSettings s)
        {
            spill.enabled = IsOn && s.spillStrength > 0f;
            if (!spill.enabled) return;
            spill.spotAngle = Mathf.Clamp(s.spillAngle, s.beamAngle, 170f);
            spill.innerSpotAngle = Mathf.Min(s.beamAngle * 0.5f, spill.spotAngle * 0.3f);
            spill.color = s.color;
            spill.intensity = beam.intensity * s.spillStrength;
            spill.range = beam.range * 0.6f; // (it spreads out: it doesn't carry as far)
            spillData.volumetricDimmer = s.fogGlow * s.spillFogGlow;
        }

        // Where the beam lands nearby, a soft glow comes back toward you, tinted by what it hit. It sits off
        // the surface and partway back, and fades out toward bounceDistance.
        void UpdateBounce(FlashlightSettings s)
        {
            float target = 0f;
            if (IsOn && s.bounceStrength > 0f && Probe(s.bounceDistance, out var hit))
            {
                float fade = 1f - Mathf.SmoothStep(0f, 1f, hit.distance / Mathf.Max(s.bounceDistance, 0.1f));
                target = beam.intensity * s.bounceStrength * 0.1f * fade;
                if (hit.collider != tintFrom)
                {
                    tintFrom = hit.collider;
                    bounceTint = SurfaceTint(hit.collider);
                }
                Vector3 back = (hand.position - hit.point).normalized * Mathf.Min(0.6f, hit.distance * 0.4f);
                Vector3 at = hit.point + hit.normal * 0.5f + back;
                var t = bounce.transform;
                bool jump = (t.position - at).sqrMagnitude > 9f || bounceLevel <= 0.001f;
                t.position = jump ? at : Vector3.Lerp(t.position, at, 1f - Mathf.Exp(-12f * Time.deltaTime));
            }
            bounceLevel = Mathf.MoveTowards(bounceLevel, target, Time.deltaTime * Mathf.Max(target, bounceLevel, 0.5f) * 6f);
            bounce.enabled = bounceLevel > 0.001f;
            if (!bounce.enabled) return;
            bounce.intensity = bounceLevel;
            bounce.range = s.bounceRange;
            bounce.color = Color.Lerp(s.color, s.color * bounceTint, 0.7f);
        }

        bool Probe(float distance, out RaycastHit hit)
        {
            int mask = ~(Layers.Mask(Layers.Effects) | Layers.Mask(Layers.PlayerBody) | Layers.Mask(Layers.Pickups));
            return Physics.Raycast(hand.position, hand.forward, out hit, distance, mask, QueryTriggerInteraction.Ignore);
        }

        // The colour of what the beam hit (its material's, brightened to full: only the hue matters), or white.
        static Color SurfaceTint(Collider c)
        {
            var r = c.GetComponent<Renderer>();
            if (r == null) r = c.GetComponentInChildren<Renderer>();
            var mat = r != null ? r.sharedMaterial : null;
            if (mat == null) return Color.white;
            Color col = mat.HasProperty("_BaseColor") ? mat.GetColor("_BaseColor") : Color.white;
            col /= Mathf.Max(col.maxColorComponent, 0.0001f);
            col.a = 1f;
            return Color.Lerp(Color.white, col, 0.6f);
        }
    }
}
