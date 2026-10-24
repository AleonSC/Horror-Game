using Game.Audio;
using Game.Core;
using Game.Rendering;
using UnityEngine;
using UnityEngine.Rendering.HighDefinition;

namespace Game.Entities
{
    /// <summary>
    /// The blind one's storm: a low, churning black cloud that hangs over it, with red lightning forking down to
    /// the ground every few seconds (a flash that lights the forest red, and thunder a moment later).
    /// </summary>
    public sealed class Storm : MonoBehaviour
    {
        BlindSettings s;
        LocalVolumetricFog cloud;
        Texture3D density;
        LineRenderer bolt;
        Material boltMat;
        Light flash;
        HDAdditionalLightData flashData;
        float nextStrike, flashLeft, thunderAt = -1f;
        Vector3 thunderFrom;

        public float Strength { get; set; }

        public static Storm Create(BlindSettings s)
        {
            var go = new GameObject("Storm");
            var storm = go.AddComponent<Storm>();
            storm.s = s;
            return storm;
        }

        void Awake()
        {
            cloud = gameObject.AddComponent<LocalVolumetricFog>();
            cloud.parameters.maskMode = LocalVolumetricFogMaskMode.Texture;
            cloud.parameters.blendingMode = LocalVolumetricFogBlendingMode.Additive;
            cloud.parameters.falloffMode = LocalVolumetricFogFalloffMode.Linear;
            cloud.parameters.scaleMode = LocalVolumetricFogScaleMode.ScaleInvariant;
            density = MistNoise.Build(0.8f, 9);
            cloud.parameters.volumeMask = density;
            cloud.parameters.albedo = new Color(0.02f, 0.015f, 0.015f);
            cloud.parameters.positiveFade = new Vector3(0.35f, 0.4f, 0.35f);
            cloud.parameters.negativeFade = new Vector3(0.35f, 0.2f, 0.35f);

            var boltGo = new GameObject("Lightning");
            boltGo.transform.SetParent(transform, false);
            bolt = boltGo.AddComponent<LineRenderer>();
            boltMat = RuntimeMaterials.Glow("Red lightning", new Color(1f, 0.1f, 0.05f) * 40f);
            bolt.sharedMaterial = boltMat;
            bolt.useWorldSpace = true;
            bolt.widthMultiplier = 0.35f;
            bolt.positionCount = 0;
            bolt.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            var flashGo = new GameObject("Lightning flash");
            flashGo.transform.SetParent(transform, false);
            flashData = flashGo.AddHDLight(LightType.Point);
            flash = flashGo.GetComponent<Light>();
            flash.shadows = LightShadows.None;
            flash.enabled = false;
        }

        void OnDestroy()
        {
            if (density != null) Destroy(density);
            if (boltMat != null) Destroy(boltMat);
        }

        /// <summary>Hang over this point (the blind one).</summary>
        public void Follow(Vector3 over, float dt)
        {
            float k = Mathf.Clamp01(Strength);
            cloud.enabled = k > 0.01f;
            transform.position = Vector3.Lerp(transform.position, over + Vector3.up * s.stormHeight, 1f - Mathf.Exp(-0.8f * dt));
            float size = s.stormSize * Mathf.Lerp(0.4f, 1f, k);
            cloud.parameters.size = new Vector3(size, s.stormHeight * 0.55f, size);
            cloud.parameters.textureTiling = new Vector3(2f, 1f, 2f);
            cloud.parameters.textureScrollingSpeed = new Vector3(0.03f, 0.01f, -0.02f); // churning
            cloud.parameters.meanFreePath = Mathf.Lerp(40f, 2.5f, k);

            if (k > 0.5f)
            {
                nextStrike -= dt;
                if (nextStrike <= 0f) Strike();
            }

            if (flashLeft > 0f)
            {
                flashLeft -= dt;
                float f = Mathf.Clamp01(flashLeft / 0.25f) * (0.6f + 0.4f * Mathf.PerlinNoise(Time.time * 40f, 0.5f));
                flash.intensity = s.lightningIntensity * f;
                bolt.widthMultiplier = 0.35f * f;
                if (flashLeft <= 0f)
                {
                    flash.enabled = false;
                    bolt.positionCount = 0;
                }
            }
            if (thunderAt > 0f && Time.time >= thunderAt)
            {
                thunderAt = -1f;
                if (Services.TryGet<SoundPlayer>(out var sounds)) sounds.Play(Sound.Thunder, thunderFrom, MixerNames.Outside, 0.9f, Random.Range(0.8f, 1.05f), 400f);
            }
        }

        // A jagged bolt from the cloud to the ground somewhere under it.
        void Strike()
        {
            nextStrike = Random.Range(s.minLightning, s.maxLightning);
            Vector3 top = transform.position + new Vector3(Random.Range(-1f, 1f), 0f, Random.Range(-1f, 1f)) * s.stormSize * 0.3f;
            Vector3 ground = top + new Vector3(Random.Range(-1f, 1f), 0f, Random.Range(-1f, 1f)) * s.stormSize * 0.25f;
            ground.y = PlayerSense.GroundHeight(ground);
            const int n = 14;
            bolt.positionCount = n;
            for (int i = 0; i < n; i++)
            {
                float u = i / (n - 1f);
                Vector3 p = Vector3.Lerp(top, ground, u);
                if (i > 0 && i < n - 1) p += new Vector3(Random.Range(-1f, 1f), Random.Range(-0.3f, 0.3f), Random.Range(-1f, 1f)) * 2.5f;
                bolt.SetPosition(i, p);
            }
            flash.transform.position = Vector3.Lerp(top, ground, 0.7f);
            flash.color = s.lightningColor;
            flashData.volumetricDimmer = s.lightningFogGlow;
            RuntimeMaterials.SetGlow(boltMat, s.lightningColor * s.boltGlow);
            flash.range = 120f;
            flash.enabled = true;
            flashLeft = 0.35f;
            // Thunder follows at the speed of sound.
            thunderFrom = ground;
            float d = Camera.main != null ? Vector3.Distance(Camera.main.transform.position, ground) : 50f;
            thunderAt = Time.time + d / 343f + 0.1f;
        }
    }
}
