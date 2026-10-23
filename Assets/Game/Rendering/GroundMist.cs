using UnityEngine;
using UnityEngine.Rendering.HighDefinition;

namespace Game.Rendering
{
    /// <summary>
    /// The thick low mist (HDRP local volumetric fog, so the flashlight and lamps light it by themselves). A box
    /// of mist round the camera, sitting on the ground. It moves in whole steps of the patch size, so the
    /// patches stay put in the world as you walk.
    /// </summary>
    public sealed class GroundMist : MonoBehaviour
    {
        LocalVolumetricFog volume;
        Texture3D density;
        float builtPatchiness = -1f;
        float groundHeight;
        bool hasGround;

        void Awake()
        {
            volume = gameObject.AddComponent<LocalVolumetricFog>();
            volume.parameters.maskMode = LocalVolumetricFogMaskMode.Texture;
            volume.parameters.blendingMode = LocalVolumetricFogBlendingMode.Additive;
            volume.parameters.falloffMode = LocalVolumetricFogFalloffMode.Linear;
            volume.parameters.scaleMode = LocalVolumetricFogScaleMode.ScaleInvariant;
        }

        void OnDestroy()
        {
            if (density != null) Destroy(density);
        }

        /// <summary>Follow the camera and take the look's mist settings (called every frame by LookController).</summary>
        public void Apply(LookSettings s, Camera cam)
        {
            volume.enabled = s.mist && cam != null;
            if (!volume.enabled) return;

            // The patchiness is baked into the texture, so rebuild it when that changes.
            if (density == null || !Mathf.Approximately(builtPatchiness, s.mistPatchiness))
            {
                if (density != null) Destroy(density);
                density = MistNoise.Build(s.mistPatchiness);
                builtPatchiness = s.mistPatchiness;
                volume.parameters.volumeMask = density;
            }

            float reach = s.mistReach;
            float tile = Mathf.Max(s.mistPatchSize, 1f);
            float width = Mathf.Max(tile, Mathf.Round(reach * 2f / tile) * tile); // a whole number of patches
            float top = Mathf.Max(0.1f, s.mistHeight);

            var p = cam.transform.position;
            FindGround(p);
            float x = Mathf.Floor(p.x / tile) * tile;
            float z = Mathf.Floor(p.z / tile) * tile;
            transform.SetPositionAndRotation(new Vector3(x, groundHeight + top * 0.5f - 0.2f, z), Quaternion.identity);

            volume.parameters.size = new Vector3(width, top, width);
            volume.parameters.textureTiling = new Vector3(width / tile, 1f, width / tile);
            volume.parameters.textureScrollingSpeed = new Vector3(s.mistDrift / tile, 0f, s.mistDrift * 0.6f / tile);
            volume.parameters.albedo = s.mistColor;
            volume.parameters.meanFreePath = Mathf.Max(0.05f, s.mistThickness);
            // Soft sides so the box's edge never shows; the top fade is already in the texture.
            volume.parameters.positiveFade = new Vector3(0.3f, 0.01f, 0.3f);
            volume.parameters.negativeFade = new Vector3(0.3f, 0.01f, 0.3f);
        }

        // The mist sits on the ground under the camera (smoothed, so it doesn't jump over rocks).
        void FindGround(Vector3 from)
        {
            if (Physics.Raycast(from + Vector3.up, Vector3.down, out var hit, 60f, ~0, QueryTriggerInteraction.Ignore))
            {
                groundHeight = hasGround ? Mathf.Lerp(groundHeight, hit.point.y, 1f - Mathf.Exp(-3f * Time.deltaTime)) : hit.point.y;
                hasGround = true;
            }
            else if (!hasGround)
            {
                groundHeight = from.y - 1.7f;
            }
        }
    }
}
