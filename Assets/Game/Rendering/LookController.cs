using Game.Core;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;
using UnityEngine.SceneManagement;

namespace Game.Rendering
{
    /// <summary>
    /// Applies the <see cref="LookConfig"/> every frame: a global HDRP volume (exposure, fog, sky, colour), the
    /// moon, and the ground mist. It's the only directional light: any other in a loaded scene is switched off,
    /// so every scene looks the same. Values are read live, so edits show at once.
    /// </summary>
    public sealed class LookController : MonoBehaviour
    {
        // Above any volume a scene might have.
        const float VolumePriority = 1000f;

        LookConfig config;
        VolumeProfile profile;
        Exposure exposure;
        Tonemapping tonemapping;
        VisualEnvironment environment;
        GradientSky sky;
        Fog fog;
        ColorAdjustments colorAdjustments;
        Vignette vignette;
        FilmGrain grain;
        MotionBlur motionBlur;
        DepthOfField blur;
        ScreenSpaceAmbientOcclusion rtShade;
        ScreenSpaceReflection rtReflect;
        bool rayTraced;
        Light moon;
        HDAdditionalLightData moonData;
        GroundMist mist;
        NightSky nightSky;
        Color lastAmbient = new Color(-1f, 0f, 0f);

        readonly System.Collections.Generic.List<System.IDisposable> subs = new System.Collections.Generic.List<System.IDisposable>();

        static readonly int FogFreePathId = Shader.PropertyToID("_HorrorFogFreePath"), FogColorId = Shader.PropertyToID("_HorrorFogColor");

        public LookConfig Config => config;

        /// <summary>Something tinting the world (the giant's eye turns the fog red): the colour, and how much (0-1).</summary>
        public Color FogTint = Color.black;
        public float FogTintAmount;
        /// <summary>Blurred vision (waking up groggy), 0-1: everything past arm's length soft, clearing outward as it falls.</summary>
        public float Blur;
        /// <summary>The world going (a scour has you), 0-1: the fog turns black and stops glowing, the moon goes out.</summary>
        public float Void;
        /// <summary>While it goes: how far you can still see (metres).</summary>
        public float VoidSight = 30f;

        /// <summary>What HDRP multiplies light by at this exposure (EV100). Screen colours are divided by it.</summary>
        public static float ExposureMultiplier(float ev100) => 1f / (1.2f * Mathf.Pow(2f, ev100));

        /// <summary>Fog thickness as HDRP's mean free path (metres): 0.85 / density.</summary>
        public static float MeanFreePath(float density) => Mathf.Clamp(0.85f / Mathf.Max(density, 0.0001f), 1f, 5000f);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void AttachToGame()
        {
            if (Services.TryGet<GameRoot>(out var root) && root.GetComponent<LookController>() == null)
                root.gameObject.AddComponent<LookController>();
        }

        void Awake()
        {
            config = Services.Get<GameSetup>().Get<LookConfig>();
            if (config == null)
            {
                Debug.LogWarning("No LookConfig in the GameSetup asset; using the default look.");
                config = ScriptableObject.CreateInstance<LookConfig>();
            }

            BuildVolume();
            BuildMoon();
            mist = new GameObject("Ground Mist").AddComponent<GroundMist>();
            mist.transform.SetParent(transform, false);
            nightSky = new GameObject("Night Sky").AddComponent<NightSky>();
            nightSky.transform.SetParent(transform, false);

            Services.Register(this);
            SceneManager.sceneLoaded += OnSceneLoaded;
            SwitchOffSceneDirectionalLights();
            // (The world comes back when you wake, or a run starts.)
            if (Services.TryGet<EventBus>(out var events))
            {
                subs.Add(events.Subscribe<PlayerRespawned>(_ => Void = 0f));
                subs.Add(events.Subscribe<RunStarted>(_ => Void = 0f));
            }
            Apply();
        }

        void OnDestroy()
        {
            foreach (var sub in subs) sub.Dispose();
            Services.Unregister(this);
            SceneManager.sceneLoaded -= OnSceneLoaded;
            if (profile != null) Destroy(profile);
        }

        void LateUpdate() => Apply();

        /// <summary>
        /// Whether ray tracing can be on: a card and driver that can (DirectX 12) and a quality level whose HDRP
        /// asset has it switched on (only High Fidelity does).
        /// </summary>
        public static bool RayTracingSupported =>
            SystemInfo.supportsRayTracing
            && GraphicsSettings.currentRenderPipeline is HDRenderPipelineAsset hdrp
            && hdrp.currentPlatformRenderPipelineSettings.supportRayTracing;

        void OnSceneLoaded(Scene scene, LoadSceneMode mode) => SwitchOffSceneDirectionalLights();

        void BuildVolume()
        {
            profile = ScriptableObject.CreateInstance<VolumeProfile>();
            profile.name = "Look (runtime)";

            exposure = profile.Add<Exposure>(true);
            exposure.mode.Override(ExposureMode.Fixed);

            tonemapping = profile.Add<Tonemapping>(true);

            environment = profile.Add<VisualEnvironment>(true);
            environment.skyType.Override((int)SkyType.Gradient);
            environment.skyAmbientMode.Override(SkyAmbientMode.Dynamic);

            sky = profile.Add<GradientSky>(true);
            sky.gradientDiffusion.Override(1f);

            fog = profile.Add<Fog>(true);
            fog.colorMode.Override(FogColorMode.ConstantColor);
            fog.baseHeight.Override(400f); // (it only thins far above the forest: the same all through it)
            fog.maximumHeight.Override(800f);
            fog.maxFogDistance.Override(5000f);

            colorAdjustments = profile.Add<ColorAdjustments>(true);
            vignette = profile.Add<Vignette>(true);
            grain = profile.Add<FilmGrain>(true);
            motionBlur = profile.Add<MotionBlur>(true);
            blur = profile.Add<DepthOfField>(true);
            blur.nearFocusStart.Override(0f);
            blur.nearFocusEnd.Override(0f);
            // Ray tracing (the player's choice, where the machine and the quality level can): ray-traced contact
            // shade and reflections, and the moon's shadows. Off, these overrides do nothing.
            rtShade = profile.Add<ScreenSpaceAmbientOcclusion>(false);
            rtShade.rayTracing.Override(true);
            rtShade.intensity.Override(1f);
            rtShade.radius.Override(1.5f);
            rtShade.active = false;
            rtReflect = profile.Add<ScreenSpaceReflection>(false);
            rtReflect.enabled.Override(true);
            rtReflect.tracing.Override(RayCastingMode.RayTracing);
            rtReflect.active = false;

            var go = new GameObject("Look Volume");
            go.transform.SetParent(transform, false);
            var volume = go.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.priority = VolumePriority;
            volume.sharedProfile = profile;
        }

        void BuildMoon()
        {
            var go = new GameObject("Moon");
            go.transform.SetParent(transform, false);
            moonData = go.AddHDLight(LightType.Directional);
            moon = go.GetComponent<Light>();
            moonData.interactsWithSky = false; // (no sun disc drawn in the sky)
        }

        void SwitchOffSceneDirectionalLights()
        {
            foreach (var light in FindObjectsByType<Light>())
            {
                if (light == moon || light.type != LightType.Directional || !light.enabled) continue;
                light.enabled = false;
                Debug.Log($"The look switched off the scene's directional light '{light.name}' (the moon replaces it).");
            }
        }

        void Apply()
        {
            var s = config.settings;
            if (s == null) return;
            float mul = ExposureMultiplier(s.exposure);

            exposure.fixedExposure.Override(s.exposure);
            tonemapping.mode.Override(s.neutralTonemapping ? TonemappingMode.Neutral : TonemappingMode.None);

            // The ambient light is a flat sky in the ambient colour (only updated when it changes: it re-bakes).
            // (Darkened in steps while the world goes: each change re-bakes the ambient light.)
            Color ambient = s.ambient.linear * s.ambientStrength / mul * (1f - Mathf.Round(Mathf.Clamp01(Void) * 5f) / 5f);
            if (ambient != lastAmbient)
            {
                lastAmbient = ambient;
                sky.top.Override(ambient);
                sky.middle.Override(ambient);
                sky.bottom.Override(ambient);
            }

            fog.enabled.Override(true);
            Color fogColor = Color.Lerp(s.fogColor, FogTint, Mathf.Clamp01(FogTintAmount));
            float gone = Mathf.Clamp01(Void);
            fogColor = Color.Lerp(fogColor, Color.black, gone);
            fog.color.Override(fogColor.linear / mul);
            float freePath = MeanFreePath(s.fogDensity);
            if (gone > 0f) freePath = Mathf.Min(freePath, Mathf.Max(0.4f, VoidSight / 3f)); // (seeing about 3 free paths)
            fog.meanFreePath.Override(freePath);
            // (The game's own shaders fade glows and shafts into the fog by these.)
            Shader.SetGlobalFloat(FogFreePathId, freePath);
            Shader.SetGlobalColor(FogColorId, fogColor.linear);
            fog.enableVolumetricFog.Override(s.volumetricFog);
            fog.albedo.Override(Color.white * (s.fogGlow * (1f - gone)));
            fog.anisotropy.Override(s.fogAnisotropy);
            // At least as far as the camera sees, or there's a hard line where the lit fog gives way to plain fog.
            var cam = Camera.main;
            fog.depthExtent.Override(Mathf.Max(s.fogReach, cam != null ? cam.farClipPlane : 0f));

            colorAdjustments.postExposure.Override(s.postExposure);
            colorAdjustments.contrast.Override(s.contrast);
            colorAdjustments.saturation.Override(s.saturation);
            colorAdjustments.colorFilter.Override(s.colorFilter);
            float b = Mathf.Clamp01(Blur);
            vignette.intensity.Override(Mathf.Clamp01(s.vignette + 0.3f * b));
            grain.intensity.Override(s.grain);
            grain.type.Override(FilmGrainLookup.Medium3);
            // Motion blur is the player's choice (options), not part of the look.
            var prefs = Services.TryGet<PlayerPreferences>(out var p) ? p : null;
            motionBlur.intensity.Override(prefs != null && prefs.motionBlur ? prefs.motionBlurStrength : 0f);
            // Depth of field (the player's choice): the far distance going soft. At the default the forest round
            // you stays sharp (out past 200 m) and the giant, drawn 360 m off, is fully blurred; 1 softens from 40 m.
            // Blurred vision (waking groggy) pulls the sharp distance right in, and it pushes outward as it clears.
            float dof = prefs != null ? Mathf.Clamp01(prefs.depthOfField) : 0.5f;
            float farStart = dof > 0.001f ? Mathf.Lerp(380f, 40f, dof) : 2000f, farEnd = dof > 0.001f ? farStart * 1.6f : 4000f; // (0.5: soft from 210 m, fully by 336)
            bool blurring = b > 0.001f || dof > 0.001f;
            blur.focusMode.Override(blurring ? DepthOfFieldMode.Manual : DepthOfFieldMode.Off);
            if (blurring)
            {
                blur.farFocusStart.Override(Mathf.Lerp(farStart, 0.3f, b));
                blur.farFocusEnd.Override(Mathf.Lerp(farEnd, 4f, b));
            }

            bool rt = prefs != null && prefs.rayTracing && RayTracingSupported;
            if (rt != rayTraced)
            {
                rayTraced = rt;
                rtShade.active = rt;
                rtReflect.active = rt;
                moonData.useRayTracedShadows = rt;
            }

            moon.color = s.moonColor;
            moon.intensity = s.moonBrightness * (1f - Mathf.Clamp01(Void)); // (lux: a directional light's own unit in HDRP)
            moon.enabled = s.moonBrightness > 0f;
            moon.shadows = s.moonShadows ? LightShadows.Soft : LightShadows.None;
            moonData.angularDiameter = s.moonShadowSoftness;
            // The moonlight comes from the moon in the sky (behind the giant, once it's put it there).
            moon.transform.rotation = Quaternion.LookRotation(-NightSky.MoonDirection(s));

            mist.Apply(s, cam, Mathf.Clamp01(Void));
            if (nightSky != null) nightSky.Apply(s, cam);
        }
    }
}
