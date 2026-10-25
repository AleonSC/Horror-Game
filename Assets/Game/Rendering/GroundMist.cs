using System.Threading.Tasks;
using Game.Core;
using UnityEngine;
using UnityEngine.Rendering.HighDefinition;

namespace Game.Rendering
{
    /// <summary>
    /// The thick low mist (HDRP local volumetric fog, so the flashlight and lamps light it by themselves), lying on
    /// the ground all round the camera and following every rise and dip of it. Its density is one 3D texture made
    /// for where you are, with the mist's height above the ground worked into every column of it (so it hugs the
    /// hills and hummocks exactly, in one even layer), made afresh every couple of seconds on another thread with
    /// its patches drifted on a little, and each new one blended in over the last as it comes (so the mist
    /// drifts smoothly). Its grid is fixed in the world, so the mist doesn't shimmer as you walk.
    /// </summary>
    public sealed class GroundMist : MonoBehaviour
    {
        const int Across = 64, Up = 48; // (about 1.4 m across, and a fraction of a metre up, a cell)
        const float Refresh = 2f; // (seconds between new textures; each blends in over the next this-many)

        sealed class Layer
        {
            public LocalVolumetricFog Volume;
            public Texture3D Texture;
        }

        readonly Layer[] layers = new Layer[2];
        int newest = -1;      // the layer blending in (the other's blending out)
        float blend = 1f;     // how far the newest has blended in
        float nextBake;
        Task<byte[]> baking;
        Vector3 bakeCorner, bakeSize;
        Vector2 drift;        // how far the patches have drifted (metres)
        readonly float[] ground = new float[Across * Across];

        LayerSettings last;
        struct LayerSettings { public Color Albedo; public float MeanFreePath; }

        void Awake()
        {
            for (int i = 0; i < 2; i++)
            {
                var go = new GameObject($"Mist {i}");
                go.transform.SetParent(transform, false);
                var v = go.AddComponent<LocalVolumetricFog>();
                v.parameters.maskMode = LocalVolumetricFogMaskMode.Texture;
                v.parameters.blendingMode = LocalVolumetricFogBlendingMode.Additive;
                v.parameters.falloffMode = LocalVolumetricFogFalloffMode.Linear;
                v.parameters.scaleMode = LocalVolumetricFogScaleMode.ScaleInvariant;
                v.parameters.textureTiling = Vector3.one;
                v.parameters.textureScrollingSpeed = Vector3.zero;
                v.enabled = false;
                layers[i] = new Layer
                {
                    Volume = v,
                    Texture = new Texture3D(Across, Up, Across, TextureFormat.Alpha8, false)
                    {
                        name = $"Ground mist {i}",
                        wrapMode = TextureWrapMode.Clamp,
                        filterMode = FilterMode.Bilinear,
                        hideFlags = HideFlags.HideAndDontSave,
                    },
                };
                v.parameters.volumeMask = layers[i].Texture;
            }
        }

        void OnDestroy()
        {
            foreach (var l in layers) if (l != null && l.Texture != null) Destroy(l.Texture);
        }

        /// <summary>Follow the camera and take the look's mist settings (called every frame by LookController).</summary>
        public void Apply(LookSettings s, Camera cam, float gone = 0f)
        {
            bool on = s.mist && cam != null;
            if (!on)
            {
                foreach (var l in layers) l.Volume.enabled = false;
                return;
            }
            var p = cam.transform.position;

            // A new texture ready: it starts blending in over the last.
            if (baking != null && baking.IsCompleted)
            {
                int next = newest < 0 ? 0 : 1 - newest;
                var l = layers[next];
                l.Texture.SetPixelData(baking.Result, 0);
                l.Texture.Apply(false, false);
                l.Volume.transform.SetPositionAndRotation(bakeCorner + bakeSize * 0.5f, Quaternion.identity);
                l.Volume.parameters.size = bakeSize;
                blend = newest < 0 ? 1f : 0f;
                newest = next;
                baking = null;
            }
            drift += new Vector2(1f, 0.6f) * (s.mistDrift * Time.deltaTime);
            if (baking == null && Time.time >= nextBake)
            {
                nextBake = Time.time + Refresh;
                Bake(s, p);
            }

            blend = Mathf.Min(1f, blend + Time.deltaTime / Refresh);
            // Fading the old out and the new in by their thickness: together, always one layer's worth.
            float mfp = Mathf.Max(0.05f, s.mistThickness);
            Color albedo = s.mistColor * (1f - gone); // (no glow while the world goes)
            for (int i = 0; i < 2; i++)
            {
                var l = layers[i];
                float w = newest < 0 ? 0f : i == newest ? blend : 1f - blend;
                l.Volume.enabled = w > 0.01f;
                if (!l.Volume.enabled) continue;
                l.Volume.parameters.albedo = albedo;
                l.Volume.parameters.meanFreePath = mfp / w;
                // Soft sides so the box's edge never shows (the top is in the texture).
                l.Volume.parameters.positiveFade = new Vector3(0.15f, 0.01f, 0.15f);
                l.Volume.parameters.negativeFade = new Vector3(0.15f, 0.01f, 0.15f);
            }
        }

        // Read the ground under every column (here, on this thread), then work out the density on another.
        void Bake(LookSettings s, Vector3 camera)
        {
            float width = Mathf.Max(20f, s.mistReach * 2f);
            float cell = width / Across;
            // The grid fixed in the world (whole cells), so the mist doesn't crawl as you move.
            float cx = Mathf.Round(camera.x / cell) * cell, cz = Mathf.Round(camera.z / cell) * cell;
            float x0 = cx - width * 0.5f, z0 = cz - width * 0.5f;
            float low = float.MaxValue, high = float.MinValue, fallback = camera.y - 1.7f;
            if (Physics.Raycast(camera + Vector3.up, Vector3.down, out var under, 500f, Layers.Mask(Layers.Ground), QueryTriggerInteraction.Ignore))
                fallback = under.point.y;
            for (int z = 0; z < Across; z++)
            for (int x = 0; x < Across; x++)
            {
                var top = new Vector3(x0 + (x + 0.5f) * cell, camera.y + 200f, z0 + (z + 0.5f) * cell);
                float g = Physics.Raycast(top, Vector3.down, out var hit, 600f, Layers.Mask(Layers.Ground), QueryTriggerInteraction.Ignore) ? hit.point.y : fallback;
                ground[z * Across + x] = g;
                low = Mathf.Min(low, g);
                high = Mathf.Max(high, g);
            }
            // Seen from high above (the menu's views), a thin layer falls between the fog's slices and shows as
            // bands: so the higher you are, the taller (and thinner) the mist's made, the same amount of it.
            float above = Mathf.Clamp01((camera.y - fallback - 8f) / 30f);
            float stretch = 1f + 3f * above;
            float mistTop = Mathf.Max(0.1f, s.mistHeight) * stretch;
            float bottom = low - 0.3f, height = high - low + mistTop + 0.6f;
            bakeCorner = new Vector3(x0, bottom, z0);
            bakeSize = new Vector3(width, height, width);

            var heights = (float[])ground.Clone();
            float patchiness = s.mistPatchiness, patchSize = Mathf.Max(1f, s.mistPatchSize);
            Vector2 offset = drift;
            float thin = 1f / stretch; // (the same amount of mist, spread taller)
            baking = Task.Run(() =>
            {
                var data = new byte[Across * Up * Across];
                for (int z = 0; z < Across; z++)
                for (int x = 0; x < Across; x++)
                {
                    float g = heights[z * Across + x];
                    float wx = x0 + (x + 0.5f) * cell, wz = z0 + (z + 0.5f) * cell;
                    for (int y = 0; y < Up; y++)
                    {
                        float wy = bottom + (y + 0.5f) / Up * height;
                        float d = MistNoise.Ground(wx, wz, (wy - g) / mistTop, patchiness, patchSize, offset, 1) * thin;
                        data[x + y * Across + z * Across * Up] = (byte)(Mathf.Clamp01(d) * 255f + 0.5f);
                    }
                }
                return data;
            });
        }
    }
}
