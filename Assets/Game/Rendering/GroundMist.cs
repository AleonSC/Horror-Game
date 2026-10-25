using Game.Core;
using UnityEngine;
using UnityEngine.Rendering.HighDefinition;

namespace Game.Rendering
{
    /// <summary>
    /// The thick low mist (HDRP local volumetric fog, so the flashlight and lamps light it by themselves), lying on
    /// the ground all round the camera and following its rise and fall: a grid of overlapping boxes, each sitting
    /// on the ground under its own middle, their soft edges fading linearly so where they overlap they add up to
    /// exactly one layer of mist (no seams, and on a slope it climbs with the hill rather than sinking into it).
    /// The grid moves in whole steps, a whole number of patches, so the patches stay put in the world as you walk.
    /// </summary>
    public sealed class GroundMist : MonoBehaviour
    {
        // How far apart the boxes' middles are, in patches (each box is twice this across).
        const int PatchesPerStep = 2;
        const int MaxAcross = 7; // (7 x 7 = 49 boxes, inside HDRP's 64 on screen)

        LocalVolumetricFog[] volumes = new LocalVolumetricFog[0];
        Texture3D density;
        float builtPatchiness = -1f;
        readonly System.Collections.Generic.Dictionary<Vector2Int, float> ground = new System.Collections.Generic.Dictionary<Vector2Int, float>();
        readonly System.Collections.Generic.List<Vector2Int> stale = new System.Collections.Generic.List<Vector2Int>();
        float fallbackGround;
        bool hasFallback;

        void OnDestroy()
        {
            if (density != null) Destroy(density);
        }

        LocalVolumetricFog MakeVolume(int i)
        {
            var go = new GameObject($"Mist {i}");
            go.transform.SetParent(transform, false);
            var v = go.AddComponent<LocalVolumetricFog>();
            v.parameters.maskMode = LocalVolumetricFogMaskMode.Texture;
            v.parameters.blendingMode = LocalVolumetricFogBlendingMode.Additive;
            v.parameters.falloffMode = LocalVolumetricFogFalloffMode.Linear;
            v.parameters.scaleMode = LocalVolumetricFogScaleMode.ScaleInvariant;
            if (density != null) v.parameters.volumeMask = density;
            return v;
        }

        /// <summary>Follow the camera and take the look's mist settings (called every frame by LookController).</summary>
        public void Apply(LookSettings s, Camera cam, float gone = 0f)
        {
            bool on = s.mist && cam != null;
            float tile = Mathf.Max(s.mistPatchSize, 1f);
            float step = tile * PatchesPerStep;
            int across = on ? Mathf.Clamp(Mathf.CeilToInt(s.mistReach / step) * 2 + 1, 3, MaxAcross) : 0;
            if (volumes.Length != across * across)
            {
                foreach (var v in volumes) if (v != null) Destroy(v.gameObject);
                volumes = new LocalVolumetricFog[across * across];
                for (int i = 0; i < volumes.Length; i++) volumes[i] = MakeVolume(i);
            }
            if (!on) return;

            // The patchiness is baked into the texture, so rebuild it when that changes.
            if (density == null || !Mathf.Approximately(builtPatchiness, s.mistPatchiness))
            {
                if (density != null) Destroy(density);
                density = MistNoise.Build(s.mistPatchiness);
                builtPatchiness = s.mistPatchiness;
                foreach (var v in volumes) v.parameters.volumeMask = density;
            }

            float top = Mathf.Max(0.1f, s.mistHeight);
            var p = cam.transform.position;
            int cx = Mathf.RoundToInt(p.x / step), cz = Mathf.RoundToInt(p.z / step), half = across / 2;
            transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            stale.Clear();
            foreach (var key in ground.Keys) if (Mathf.Abs(key.x - cx) > half + 1 || Mathf.Abs(key.y - cz) > half + 1) stale.Add(key);
            foreach (var key in stale) ground.Remove(key);

            int n = 0;
            for (int gz = -half; gz <= half; gz++)
            for (int gx = -half; gx <= half; gx++)
            {
                var v = volumes[n++];
                var key = new Vector2Int(cx + gx, cz + gz);
                Vector3 centre = new Vector3(key.x * step, 0f, key.y * step);
                float floor = GroundAt(key, centre, p);
                v.transform.SetPositionAndRotation(new Vector3(centre.x, floor + top * 0.5f - 0.2f, centre.z), Quaternion.identity);
                v.parameters.size = new Vector3(step * 2f, top, step * 2f);
                v.parameters.textureTiling = new Vector3(PatchesPerStep * 2f, 1f, PatchesPerStep * 2f);
                v.parameters.textureScrollingSpeed = new Vector3(s.mistDrift / tile, 0f, s.mistDrift * 0.6f / tile);
                v.parameters.albedo = s.mistColor * (1f - gone); // (no glow while the world goes)
                v.parameters.meanFreePath = Mathf.Max(0.05f, s.mistThickness);
                // Halfway in from every side: each box a tent, rising from nothing at its edge to full at its
                // middle, so neighbours (one step apart) add up to one even layer. The top fade is in the texture.
                v.parameters.positiveFade = new Vector3(0.5f, 0.01f, 0.5f);
                v.parameters.negativeFade = new Vector3(0.5f, 0.01f, 0.5f);
                v.enabled = true;
            }
        }

        // The ground under a box's middle (eased toward a new reading, so it doesn't jump over a rock or a log).
        float GroundAt(Vector2Int key, Vector3 centre, Vector3 camera)
        {
            float found;
            if (Physics.Raycast(new Vector3(centre.x, camera.y + 200f, centre.z), Vector3.down, out var hit, 600f, Layers.Mask(Layers.Ground), QueryTriggerInteraction.Ignore))
                found = hit.point.y;
            else
            {
                // (no ground there: as under the camera)
                if (Physics.Raycast(camera + Vector3.up, Vector3.down, out var under, 500f, Layers.Mask(Layers.Ground), QueryTriggerInteraction.Ignore))
                {
                    fallbackGround = under.point.y;
                    hasFallback = true;
                }
                found = hasFallback ? fallbackGround : camera.y - 1.7f;
            }
            if (ground.TryGetValue(key, out var was)) found = Mathf.Lerp(was, found, 1f - Mathf.Exp(-3f * Time.deltaTime));
            ground[key] = found;
            return found;
        }
    }
}
