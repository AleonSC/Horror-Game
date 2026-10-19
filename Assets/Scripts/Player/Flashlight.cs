using UnityEngine;

/// <summary>
/// Toggleable flashlight (press F). Put this on the player's camera.
/// If no Light is assigned, a spot light is created as a child automatically.
/// The light lags slightly behind the camera for a hand-held feel.
/// </summary>
public class Flashlight : MonoBehaviour
{
    [Header("Light")]
    [Tooltip("Spot light to toggle. Leave empty to create one automatically.")]
    public Light flashlight;
    public bool startOn = true;
    public float range = 25f;
    public float spotAngle = 50f;
    public float innerSpotAngle = 25f;
    public float intensity = 3f;
    public Color color = new Color(1f, 0.95f, 0.85f);
    public LightShadows shadows = LightShadows.Soft;
    [Tooltip("Position of the light relative to the camera (slightly right/down, like a hand).")]
    public Vector3 localOffset = new Vector3(0.25f, -0.2f, 0.2f);

    [Header("Sway")]
    [Tooltip("How quickly the beam catches up with where the camera is looking. 0 = no lag.")]
    public float swaySpeed = 12f;

    [Header("Audio")]
    public AudioClip toggleSound;
    [Range(0f, 1f)] public float toggleVolume = 0.6f;

    public bool IsOn => flashlight != null && flashlight.enabled;

    AudioSource audioSource;

    void Awake()
    {
        if (flashlight == null)
        {
            var go = new GameObject("Flashlight Light");
            go.transform.SetParent(transform, false);
            go.transform.localPosition = localOffset;
            flashlight = go.AddComponent<Light>();
            flashlight.type = LightType.Spot;
        }

        flashlight.type = LightType.Spot;
        flashlight.range = range;
        flashlight.spotAngle = spotAngle;
        flashlight.innerSpotAngle = innerSpotAngle;
        flashlight.intensity = intensity;
        flashlight.color = color;
        flashlight.shadows = shadows;
        flashlight.enabled = startOn;

        if (toggleSound != null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
            audioSource.spatialBlend = 0f;
        }
    }

    void Update()
    {
        if (HorrorInput.FlashlightPressed)
            Toggle();
    }

    void LateUpdate()
    {
        // Smoothly follow the camera's rotation so the beam drags a little when turning.
        if (flashlight == null || flashlight.transform == transform) return;
        Transform t = flashlight.transform;
        t.rotation = swaySpeed > 0f
            ? Quaternion.Slerp(t.rotation, transform.rotation, 1f - Mathf.Exp(-swaySpeed * Time.deltaTime))
            : transform.rotation;
    }

    public void Toggle() => SetOn(!IsOn);

    public void SetOn(bool on)
    {
        if (flashlight == null) return;
        flashlight.enabled = on;
        if (audioSource != null) audioSource.PlayOneShot(toggleSound, toggleVolume);
    }
}
