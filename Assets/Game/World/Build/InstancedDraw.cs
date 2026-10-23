using UnityEngine;
using UnityEngine.Rendering;

namespace Game.World.Build
{
    /// <summary>One GPU-instanced draw (Graphics.RenderMeshInstanced): up to 1023 copies of a mesh in one call.</summary>
    public static class InstancedDraw
    {
        public const int MaxPerCall = 1023;

        public static void Draw(Mesh mesh, Material material, Matrix4x4[] matrices, int count, Bounds bounds, int layer, bool shadows)
        {
            var rp = new RenderParams(material)
            {
                layer = layer,
                shadowCastingMode = shadows ? ShadowCastingMode.On : ShadowCastingMode.Off,
                receiveShadows = true,
                worldBounds = bounds,
            };
            Graphics.RenderMeshInstanced(rp, mesh, 0, matrices, count);
        }
    }
}
