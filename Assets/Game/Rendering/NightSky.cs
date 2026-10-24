using UnityEngine;
using UnityEngine.Rendering;

namespace Game.Rendering
{
    /// <summary>
    /// The night sky (section 4.4): a dome kept round the camera, drawn with the NightSky shader through the fog:
    /// stars, drifting clouds and a faint glow where the moon is (no moon is drawn). The moon is wherever <see cref="MoonOverride"/> says
    /// (the giant puts it behind itself), else where the look's moon settings put it; the moonlight comes from it.
    /// </summary>
    public sealed class NightSky : MonoBehaviour
    {
        /// <summary>Set by the giant: the moon hangs this way (a direction toward it), behind its body.</summary>
        public static Vector3? MoonOverride;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetOnPlay() => MoonOverride = null;

        Material material;
        MeshRenderer dome;
        static readonly int MoonDirId = Shader.PropertyToID("_MoonDir"), MoonColorId = Shader.PropertyToID("_MoonColor"),
            MoonSizeId = Shader.PropertyToID("_MoonSize"), MoonGlowId = Shader.PropertyToID("_MoonGlow"),
            StarsId = Shader.PropertyToID("_Stars"), StarDensityId = Shader.PropertyToID("_StarDensity"),
            CoverId = Shader.PropertyToID("_CloudCover"), CloudBrightId = Shader.PropertyToID("_CloudBrightness"), DriftId = Shader.PropertyToID("_CloudDrift");

        /// <summary>Which way the moon is (toward it), from these settings or the giant.</summary>
        public static Vector3 MoonDirection(LookSettings s)
        {
            if (MoonOverride.HasValue) return MoonOverride.Value.normalized;
            // (The light's rotation points the way the light goes: the moon is the other way.)
            return -(Quaternion.Euler(s.moonHeight, s.moonDirection, 0f) * Vector3.forward);
        }

        void Awake()
        {
            var shader = Effects.GameShader("Game/NightSky");
            if (shader == null) { enabled = false; return; }
            material = new Material(shader) { name = "Night sky" };
            var sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            var mesh = sphere.GetComponent<MeshFilter>().sharedMesh;
            Destroy(sphere);
            var go = new GameObject("Night sky dome") { layer = Game.Core.Layers.Effects };
            go.transform.SetParent(transform, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            dome = go.AddComponent<MeshRenderer>();
            dome.sharedMaterial = material;
            dome.shadowCastingMode = ShadowCastingMode.Off;
            dome.receiveShadows = false;
            dome.lightProbeUsage = LightProbeUsage.Off;
            dome.reflectionProbeUsage = ReflectionProbeUsage.Off;
        }

        void OnDestroy()
        {
            if (material != null) Destroy(material);
        }

        /// <summary>Follow the camera and take the look's sky settings (LookController calls this every frame).</summary>
        public void Apply(LookSettings s, Camera cam)
        {
            if (dome == null) return;
            dome.enabled = s.nightSky && cam != null;
            if (!dome.enabled) return;
            // Just inside the far clip (the sky is the furthest thing there is), centred on the camera.
            float radius = cam.farClipPlane * 0.92f;
            dome.transform.position = cam.transform.position;
            dome.transform.localScale = Vector3.one * radius * 2f;
            material.SetVector(MoonDirId, MoonDirection(s));
            material.SetColor(MoonColorId, s.moonGlowColor.linear);
            material.SetFloat(MoonSizeId, s.moonGlowSize);
            material.SetFloat(MoonGlowId, s.moonGlow);
            material.SetFloat(StarsId, s.stars);
            material.SetFloat(StarDensityId, s.starDensity);
            material.SetFloat(CoverId, s.cloudCover);
            material.SetFloat(CloudBrightId, s.cloudBrightness);
            material.SetVector(DriftId, new Vector4(s.cloudDrift, s.cloudDrift * 0.4f, 0f, 0f));
        }
    }
}
