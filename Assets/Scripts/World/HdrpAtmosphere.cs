using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

/// <summary>
/// HDRP ignores Unity's basic fog, flat ambient light and camera background colour, which the whole game
/// sets and changes as it goes (the generator, the lighting panel, the giant's red night, the drone's
/// thermal view). This keeps HDRP's own versions in step with them every frame, on a global volume:
///
///   - Exposure is fixed (no auto exposure brightening the dark), at exposure, which by default makes a
///     light of a given intensity about as bright as it was in URP; and no tonemapping (HDRP's default
///     ACES crushes the dark end, where this whole game lives), as URP had none.
///   - The ambient light is a flat gradient sky in RenderSettings.ambientLight's colour.
///   - RenderSettings' fog becomes HDRP fog of the same colour and about the same thickness, with
///     volumetric fog on, so lights with shadows (moon, flashlight, the scours' glare, the searchlight)
///     throw visible beams through the gaps in things.
///   - Each camera's background is a flat colour, its Camera.backgroundColor, instead of HDRP's sky.
///   - Lights are set to Unity's old units (candela, and lux for the moon), so their intensities mean
///     what they did.
///   - Ray tracing, when the graphics card and the project support it (DirectX 12, and Realtime Ray Tracing
///     ticked on the HDRP asset): ray-traced shadows on every light that casts them, ray-traced ambient
///     occlusion and reflections, and optionally ray-traced global illumination (light bouncing off
///     things: costly). The game's own effects (beams, glows, mist, the sky dome, smoke, the portal views)
///     are kept out of the ray-traced scene, which only holds the solid world.
///
/// Classic look (the pause menu's options, and the dev tools' Lighting tab) turns off what URP didn't
/// have: volumetric fog, ray tracing, ambient occlusion and reflections, so it looks close to how the game
/// looked before HDRP. It and the ray tracing switch are saved (PlayerPrefs).
///
/// DarkForestGenerator adds this when the forest is built.
/// </summary>
public class HdrpAtmosphere : MonoBehaviour
{
    void Awake() => Tuning.Apply(this); // saved tuning (Save Tuning on the component's menu)

    [Tooltip("Fixed exposure (EV100). Lower is brighter. -1.9 makes lights about as bright as they were in URP.")]
    public float exposure = -1.9f;
    [Tooltip("Tonemapping off, as in URP. HDRP's default (ACES) makes the dark much darker.")]
    public bool noTonemapping = true;
    [Tooltip("HDRP fog's mean free path is this divided by the old fog density (bigger = thinner fog).")]
    public float fogDistanceScale = 0.85f;
    [Tooltip("Volumetric fog: how much the lights show in the air (the beams and glows in the mist).")]
    [Range(0f, 1f)] public float volumetricAlbedo = 0.55f;
    [Tooltip("How far out the volumetric fog (the lit beams) reaches.")]
    public float volumetricDistance = 70f;
    [Tooltip("How bright the flashlight's glow in the mist is drawn over the giant and other far-off shapes (to match the beam round them).")]
    public float beamOverShapes = 1f;

    [Header("Ray tracing (DirectX 12 and a ray tracing graphics card)")]
    [Tooltip("Use ray tracing where the graphics card and the project support it.")]
    public bool rayTracing = true;
    public bool rayTracedShadows = true;
    public bool rayTracedAmbientOcclusion = true;
    public bool rayTracedReflections = true;
    [Tooltip("Light bouncing off things (a lamp lighting the room round it, a torch warming the walls). Costly.")]
    public bool rayTracedGlobalIllumination = false;

    /// <summary>True if ray tracing is on and this machine and project can do it.</summary>
    public static bool RayTracingActive { get; private set; }

    const string ClassicKey = "Horror.ClassicLook", RayTracingKey = "Horror.RayTracing";

    /// <summary>The look of the game before HDRP: no volumetric fog, ray tracing, ambient occlusion or reflections. Saved.</summary>
    public static bool ClassicLook
    {
        get => PlayerPrefs.GetInt(ClassicKey, 0) == 1;
        set { PlayerPrefs.SetInt(ClassicKey, value ? 1 : 0); PlayerPrefs.Save(); }
    }

    /// <summary>The player's ray tracing switch (it's only used where the machine can ray trace). Saved.</summary>
    public static bool RayTracingWanted
    {
        get => PlayerPrefs.GetInt(RayTracingKey, 1) == 1;
        set { PlayerPrefs.SetInt(RayTracingKey, value ? 1 : 0); PlayerPrefs.Save(); }
    }

    /// <summary>True if this machine and project can ray trace at all (so the switch is worth showing).</summary>
    public static bool RayTracingAvailable => CanRayTrace();
    /// <summary>Clears this class's static state (StaticReset calls it when Play starts).</summary>
    internal static void ResetStatics() => RayTracingActive = false;

    Volume volume;
    Exposure exposureFx;
    Tonemapping tonemapping;
    VisualEnvironment environment;
    GradientSky sky;
    Fog fog;
    ScreenSpaceAmbientOcclusion ambientOcclusion;
    ScreenSpaceReflection reflections;
    GlobalIllumination globalIllumination;
    readonly HashSet<Renderer> rendererSeen = new HashSet<Renderer>();
    float rendererScan;
    Color lastAmbient = new Color(-1f, 0f, 0f);
    readonly HashSet<Light> unitsSet = new HashSet<Light>();
    float lightScan;
    Flashlight flashlight;

    /// <summary>What HDRP multiplies scene light by at this exposure (so colours can be divided back by it).</summary>
    float ExposureMultiplier => 1f / (1.2f * Mathf.Pow(2f, exposure));

    void Build()
    {
        volume = new GameObject("HDRP Atmosphere Volume").AddComponent<Volume>();
        volume.transform.SetParent(transform, false);
        volume.isGlobal = true;
        volume.priority = 50f;
        var profile = ScriptableObject.CreateInstance<VolumeProfile>();
        volume.profile = profile;

        exposureFx = profile.Add<Exposure>(true);
        exposureFx.mode.Override(ExposureMode.Fixed);

        tonemapping = profile.Add<Tonemapping>(true);

        environment = profile.Add<VisualEnvironment>(true);
        environment.skyType.Override((int)SkyType.Gradient);
        environment.skyAmbientMode.Override(SkyAmbientMode.Dynamic);

        sky = profile.Add<GradientSky>(true);
        sky.gradientDiffusion.Override(1f);

        fog = profile.Add<Fog>(true);
        fog.colorMode.Override(FogColorMode.ConstantColor);
        fog.baseHeight.Override(400f);    // (thinning only far above the forest: it's the same all through it)
        fog.maximumHeight.Override(800f);
        fog.enableVolumetricFog.Override(true);

        ambientOcclusion = profile.Add<ScreenSpaceAmbientOcclusion>(false);
        reflections = profile.Add<ScreenSpaceReflection>(false);
        globalIllumination = profile.Add<GlobalIllumination>(false);
    }

    /// <summary>True if this machine can ray trace and the HDRP asset has ray tracing ticked.</summary>
    static bool CanRayTrace()
    {
        if (!SystemInfo.supportsRayTracing) return false;
        var asset = GraphicsSettings.currentRenderPipeline as HDRenderPipelineAsset;
        return asset != null && asset.currentPlatformRenderPipelineSettings.supportRayTracing;
    }

    /// <summary>
    /// The ray-traced effects on the volume, on or off; in the classic look, ambient occlusion, reflections
    /// and global illumination are off altogether (ray traced or not).
    /// </summary>
    void ApplyRayTracing(bool classic)
    {
        bool rt = rayTracing && RayTracingWanted && !classic && CanRayTrace();
        RayTracingActive = rt;

        ambientOcclusion.active = classic || (rt && rayTracedAmbientOcclusion);
        ambientOcclusion.rayTracing.Override(!classic);
        ambientOcclusion.intensity.Override(classic ? 0f : 1f);

        reflections.active = classic || (rt && rayTracedReflections);
        reflections.enabled.Override(!classic);
        reflections.tracing.Override(RayCastingMode.RayTracing);

        globalIllumination.active = classic || (rt && rayTracedGlobalIllumination);
        globalIllumination.enable.Override(!classic);
        globalIllumination.tracing.Override(RayCastingMode.RayTracing);
    }

    /// <summary>
    /// Keeps the game's own effects out of the ray-traced scene: only HDRP's own materials (the solid world)
    /// belong there. (The sky dome, for one, would otherwise block every ray.)
    /// </summary>
    void ExcludeEffectsFromRayTracing()
    {
        foreach (var r in FindObjectsByType<Renderer>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (!rendererSeen.Add(r)) continue;
            bool solid = r is MeshRenderer || r is SkinnedMeshRenderer;
            if (solid)
                foreach (var m in r.sharedMaterials)
                    if (m == null || m.shader == null || !m.shader.name.StartsWith("HDRP/") || m.renderQueue >= (int)RenderQueue.GeometryLast)
                        solid = false;
            if (!solid) r.rayTracingMode = UnityEngine.Experimental.Rendering.RayTracingMode.Off;
        }
        rendererSeen.RemoveWhere(r => r == null);
    }

    void LateUpdate()
    {
        if (volume == null) Build();
        bool classic = ClassicLook;
        float mul = ExposureMultiplier;
        exposureFx.fixedExposure.Override(exposure);
        tonemapping.active = noTonemapping;
        tonemapping.mode.Override(TonemappingMode.None);

        // Ambient: a flat sky in the ambient colour (only re-baked when it changes).
        Color ambient = RenderSettings.ambientLight.linear / mul;
        if (!Approximately(ambient, lastAmbient))
        {
            lastAmbient = ambient;
            sky.top.Override(ambient);
            sky.middle.Override(ambient);
            sky.bottom.Override(ambient);
        }

        // Fog.
        bool fogOn = RenderSettings.fog && RenderSettings.fogDensity > 0f;
        fog.enabled.Override(fogOn);
        if (fogOn)
        {
            fog.color.Override(RenderSettings.fogColor.linear / mul);
            fog.meanFreePath.Override(Mathf.Clamp(fogDistanceScale / RenderSettings.fogDensity, 1f, 5000f));
            fog.albedo.Override(Color.white * volumetricAlbedo);
            fog.depthExtent.Override(volumetricDistance);
            fog.enableVolumetricFog.Override(!classic); // (the classic look: no beams in the air, as in URP)
        }

        UpdateBeamOverShapes(mul, classic);

        // Each camera's background: its own colour, not the sky.
        foreach (var cam in Camera.allCameras)
        {
            var data = cam.GetComponent<HDAdditionalCameraData>();
            if (data == null) data = cam.gameObject.AddComponent<HDAdditionalCameraData>();
            data.clearColorMode = HDAdditionalCameraData.ClearColorMode.Color;
            data.backgroundColorHDR = cam.backgroundColor.linear / mul;
        }

        ApplyRayTracing(classic);

        // About once a second: new lights get the old units, and ray-traced shadows if they cast shadows.
        lightScan -= Time.unscaledDeltaTime;
        if (lightScan <= 0f)
        {
            lightScan = 1f;
            foreach (var light in FindObjectsByType<Light>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (unitsSet.Add(light))
                {
                    float intensity = light.intensity;
                    light.lightUnit = light.type == LightType.Directional ? LightUnit.Lux : LightUnit.Candela;
                    light.intensity = intensity;
                }
                var hd = light.GetComponent<HDAdditionalLightData>();
                if (hd == null) hd = light.gameObject.AddComponent<HDAdditionalLightData>();
                hd.useRayTracedShadows = RayTracingActive && rayTracedShadows && light.shadows != LightShadows.None;
            }
            unitsSet.RemoveWhere(l => l == null);
        }

        // Every couple of seconds: new effects are kept out of the ray-traced scene.
        rendererScan -= Time.unscaledDeltaTime;
        if (RayTracingActive && rendererScan <= 0f)
        {
            rendererScan = 2f;
            ExcludeEffectsFromRayTracing();
        }
    }

    /// <summary>
    /// The flashlight, for the far-off shapes (Horror/Haze, fog aware) to draw its glow in the mist in front
    /// of them: as bright as HDRP's volumetric fog makes it (the fog's scattering, the light's volumetric
    /// multiplier, the exposure).
    /// </summary>
    void UpdateBeamOverShapes(float mul, bool classic)
    {
        if (flashlight == null) flashlight = FindAnyObjectByType<Flashlight>();
        Light beam = flashlight != null && flashlight.IsOn ? flashlight.flashlight : null;
        bool fogOn = RenderSettings.fog && RenderSettings.fogDensity > 0f;
        if (beam == null || !beam.isActiveAndEnabled || !fogOn || classic) // (no volumetric fog: no glow to match)
        {
            Shader.SetGlobalVector("_HorrorSpotColor", new Vector4(0f, 0f, 0f, 1f));
            return;
        }
        Transform t = beam.transform;
        var hd = beam.GetComponent<HDAdditionalLightData>();
        float dimmer = hd != null ? hd.volumetricDimmer : 1f;
        float meanFreePath = Mathf.Clamp(fogDistanceScale / RenderSettings.fogDensity, 1f, 5000f);
        float scatter = volumetricAlbedo / meanFreePath / (4f * Mathf.PI); // per metre, scattered toward you
        Color c = beam.color.linear * (beam.intensity * scatter * dimmer * mul * beamOverShapes);
        float outer = Mathf.Cos(beam.spotAngle * 0.5f * Mathf.Deg2Rad);
        float inner = Mathf.Cos(Mathf.Min(beam.innerSpotAngle, beam.spotAngle - 1f) * 0.5f * Mathf.Deg2Rad);
        Shader.SetGlobalVector("_HorrorSpotPos", new Vector4(t.position.x, t.position.y, t.position.z, beam.range));
        Shader.SetGlobalVector("_HorrorSpotDir", new Vector4(t.forward.x, t.forward.y, t.forward.z, outer));
        Shader.SetGlobalVector("_HorrorSpotColor", new Vector4(c.r, c.g, c.b, inner));
    }

    static bool Approximately(Color a, Color b) =>
        Mathf.Abs(a.r - b.r) < 0.0005f && Mathf.Abs(a.g - b.g) < 0.0005f && Mathf.Abs(a.b - b.b) < 0.0005f;
}
