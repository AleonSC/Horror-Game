using Game.Core;
using UnityEngine;
using UnityEngine.Rendering.HighDefinition;

namespace Game.World
{
    /// <summary>
    /// A window from one doorway into another (the lone door and its twin in the dark room): a second camera
    /// stands where your eye would be relative to the other doorway, and what it sees is shown in this doorway,
    /// with the view fitted exactly to the opening so it lines up from any angle. It only renders while you're
    /// near enough to see it.
    /// </summary>
    public sealed class Portal : MonoBehaviour
    {
        const float RenderWithin = 40f;

        public Transform here;   // this doorway's frame
        public Transform there;  // the other doorway's frame
        public Vector2 size = new Vector2(1.16f, 2.2f);
        public Vector3 centre = new Vector3(0f, 1.1f, 0f);
        /// <summary>Shown only when this says so (the door is open).</summary>
        public System.Func<bool> isOpen;

        Camera view;
        RenderTexture target;
        MeshRenderer face;
        Material faceMat;

        public static Portal Create(Transform here, Transform there, Vector2 size, Vector3 centre, System.Func<bool> isOpen)
        {
            var go = new GameObject("Portal") { layer = Layers.PortalFaces };
            go.transform.SetParent(here, false);
            var p = go.AddComponent<Portal>();
            p.here = here;
            p.there = there;
            p.size = size;
            p.centre = centre;
            p.isOpen = isOpen;
            return p;
        }

        void Start()
        {
            // The opening, as a flat quad in the doorway: u along the frame's x, v up.
            var mesh = new Mesh { name = "Portal opening" };
            float w = size.x * 0.5f, h = size.y * 0.5f;
            mesh.vertices = new[] { centre + new Vector3(-w, -h, 0f), centre + new Vector3(w, -h, 0f), centre + new Vector3(-w, h, 0f), centre + new Vector3(w, h, 0f) };
            mesh.uv = new[] { new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 1f), new Vector2(1f, 1f) };
            mesh.triangles = new[] { 0, 2, 1, 1, 2, 3 };
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            gameObject.AddComponent<MeshFilter>().sharedMesh = mesh;
            face = gameObject.AddComponent<MeshRenderer>();
            faceMat = Game.Rendering.RuntimeMaterials.Haze("Portal", Color.white);
            face.sharedMaterial = faceMat;
            face.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            var camGo = new GameObject("Portal camera");
            camGo.transform.SetParent(transform, false);
            view = camGo.AddComponent<Camera>();
            view.cullingMask = ~Layers.Mask(Layers.PortalFaces); // (it sees your body: you can watch yourself go through)
            view.enabled = false;
            // No post-processing in the window: the main camera grades the whole picture once.
            var hd = camGo.AddComponent<HDAdditionalCameraData>();
            hd.customRenderingSettings = true;
            hd.renderingPathCustomFrameSettingsOverrideMask.mask[(uint)FrameSettingsField.Postprocess] = true;
            hd.renderingPathCustomFrameSettings.SetEnabled(FrameSettingsField.Postprocess, false);
        }

        void OnDestroy()
        {
            if (target != null) { target.Release(); Destroy(target); }
            if (faceMat != null) Destroy(faceMat);
        }

        void LateUpdate()
        {
            var cam = Camera.main;
            bool show = cam != null && there != null && (isOpen == null || isOpen()) && Vector3.Distance(cam.transform.position, here.TransformPoint(centre)) < RenderWithin;
            face.enabled = show;
            view.enabled = show;
            if (!show) return;

            int w = Mathf.Max(64, Screen.width / 2), h = Mathf.Max(64, Screen.height / 2);
            if (target == null || target.width != w || target.height != h)
            {
                if (target != null) { target.Release(); Destroy(target); }
                target = new RenderTexture(w, h, 24, RenderTextureFormat.DefaultHDR) { name = "Portal view" };
                view.targetTexture = target;
                faceMat.SetTexture("_UnlitColorMap", target);
            }

            // Where your eye would be, relative to the other doorway.
            Vector3 eyeLocal = here.InverseTransformPoint(cam.transform.position);
            Vector3 eye = there.TransformPoint(eyeLocal);
            bool front = eyeLocal.z > 0f;

            // The opening's corners as you see them (lower left, lower right, upper left), in the other doorway.
            float hx = size.x * 0.5f, hy = size.y * 0.5f, s = front ? -1f : 1f;
            Vector3 pa = there.TransformPoint(centre + new Vector3(-hx * s, -hy, 0f));
            Vector3 pb = there.TransformPoint(centre + new Vector3(hx * s, -hy, 0f));
            Vector3 pc = there.TransformPoint(centre + new Vector3(-hx * s, hy, 0f));
            Vector3 right = (pb - pa).normalized, up = (pc - pa).normalized;
            Vector3 forward = Vector3.Cross(right, up).normalized;

            view.transform.SetPositionAndRotation(eye, Quaternion.LookRotation(forward, up));
            float near = Mathf.Max(0.01f, Vector3.Dot(forward, pa - eye));
            float l = Vector3.Dot(right, pa - eye), r = Vector3.Dot(right, pb - eye);
            float b = Vector3.Dot(up, pa - eye), t = Vector3.Dot(up, pc - eye);
            view.projectionMatrix = Matrix4x4.Frustum(l, r, b, t, near, 800f);

            // Seen from the front, the texture is mirrored across the opening.
            faceMat.SetTextureScale("_UnlitColorMap", new Vector2(front ? -1f : 1f, 1f));
            faceMat.SetTextureOffset("_UnlitColorMap", new Vector2(front ? 1f : 0f, 0f));
            // It's shown pre-exposed, so undo the exposure the main camera will put on it.
            float mul = Services.TryGet<Game.Rendering.LookController>(out var look) ? Game.Rendering.LookController.ExposureMultiplier(look.Config.settings.exposure) : 1f;
            Game.Rendering.RuntimeMaterials.SetHazeColor(faceMat, Color.white / mul);
        }
    }
}
