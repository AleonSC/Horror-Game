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
///     light of a given intensity about as bright as it was in URP.
///   - The ambient light is a flat gradient sky in RenderSettings.ambientLight's colour.
///   - RenderSettings' fog becomes HDRP fog of the same colour and about the same thickness, with
///     volumetric fog on, so lights with shadows (moon, flashlight, the scours' glare, the searchlight)
///     throw visible beams through the gaps in things.
///   - Each camera's background is a flat colour, its Camera.backgroundColor, instead of HDRP's sky.
///   - Lights are set to Unity's old units (candela, and lux for the moon), so their intensities mean
///     what they did.
///
/// DarkForestGenerator adds this when the forest is built.
/// </summary>
public class HdrpAtmosphere : MonoBehaviour
{
    void Awake() => Tuning.Apply(this); // saved tuning (Save Tuning on the component's menu)

    [Tooltip("Fixed exposure (EV100). Lower is brighter. -1.9 makes lights about as bright as they were in URP.")]
    public float exposure = -1.9f;
    [Tooltip("HDRP fog's mean free path is this divided by the old fog density (bigger = thinner fog).")]
    public float fogDistanceScale = 0.85f;
    [Tooltip("Volumetric fog: how much the lights show in the air (the beams and glows in the mist).")]
    [Range(0f, 1f)] public float volumetricAlbedo = 0.55f;
    [Tooltip("How far out the volumetric fog (the lit beams) reaches.")]
    public float volumetricDistance = 70f;

    Volume volume;
    Exposure exposureFx;
    VisualEnvironment environment;
    GradientSky sky;
    Fog fog;
    Color lastAmbient = new Color(-1f, 0f, 0f);
    readonly HashSet<Light> unitsSet = new HashSet<Light>();
    float lightScan;

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
    }

    void LateUpdate()
    {
        if (volume == null) Build();
        float mul = ExposureMultiplier;
        exposureFx.fixedExposure.Override(exposure);

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
        }

        // Each camera's background: its own colour, not the sky.
        foreach (var cam in Camera.allCameras)
        {
            var data = cam.GetComponent<HDAdditionalCameraData>();
            if (data == null) data = cam.gameObject.AddComponent<HDAdditionalCameraData>();
            data.clearColorMode = HDAdditionalCameraData.ClearColorMode.Color;
            data.backgroundColorHDR = cam.backgroundColor.linear / mul;
        }

        // New lights, about once a second: the old units.
        lightScan -= Time.unscaledDeltaTime;
        if (lightScan <= 0f)
        {
            lightScan = 1f;
            foreach (var light in FindObjectsByType<Light>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (!unitsSet.Add(light)) continue;
                float intensity = light.intensity;
                light.lightUnit = light.type == LightType.Directional ? LightUnit.Lux : LightUnit.Candela;
                light.intensity = intensity;
            }
            unitsSet.RemoveWhere(l => l == null);
        }
    }

    static bool Approximately(Color a, Color b) =>
        Mathf.Abs(a.r - b.r) < 0.0005f && Mathf.Abs(a.g - b.g) < 0.0005f && Mathf.Abs(a.b - b.b) < 0.0005f;
}
