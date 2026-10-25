using UnityEngine;

namespace Game.World.Build
{
    /// <summary>
    /// A ground chunk's copies one map-width away all round, shown only behind the main menu (see
    /// <see cref="Panorama"/>), so the far trees stand on ground and the forest runs on into the fog. They share
    /// the chunk's mesh and material; no colliders. They're children of the chunk, so they move with it as the
    /// world wraps.
    /// </summary>
    public sealed class PanoramaGround : MonoBehaviour
    {
        public float mapSize = 320f;

        Renderer[] copies;
        bool shown;

        void Update()
        {
            bool want = Panorama.On;
            if (want == shown) return;
            shown = want;
            if (want && copies == null) Make();
            if (copies != null) foreach (var r in copies) if (r != null) r.enabled = want;
        }

        void Make()
        {
            var filter = GetComponent<MeshFilter>();
            var source = GetComponent<MeshRenderer>();
            if (filter == null || source == null) return;
            copies = new Renderer[8];
            int n = 0;
            for (int z = -1; z <= 1; z++)
            for (int x = -1; x <= 1; x++)
            {
                if (x == 0 && z == 0) continue;
                var go = new GameObject("Panorama copy") { layer = gameObject.layer };
                go.transform.SetParent(transform, false);
                go.transform.localPosition = new Vector3(x * mapSize, 0f, z * mapSize);
                go.AddComponent<MeshFilter>().sharedMesh = filter.sharedMesh;
                var r = go.AddComponent<MeshRenderer>();
                r.sharedMaterial = source.sharedMaterial;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                copies[n++] = r;
            }
        }
    }
}
