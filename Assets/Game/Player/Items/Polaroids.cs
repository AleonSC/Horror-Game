using System.Collections;
using System.Collections.Generic;
using Game.Core;
using UnityEngine;
using UnityEngine.Rendering.HighDefinition;

namespace Game.Player
{
    /// <summary>
    /// The camera's photos (section 6.3): each shot is rendered once from your view, lit by the flash, into a
    /// square picture that's kept for the run (the inventory shows them; the HUD shows one developing).
    /// </summary>
    public sealed class Polaroids : MonoBehaviour
    {
        public sealed class Photo
        {
            public RenderTexture Picture;
            public float TakenAt;   // run time, seconds
            public int LampsLit;
        }

        readonly List<Photo> photos = new List<Photo>();
        Camera shooter;
        System.IDisposable resetSub;

        public IReadOnlyList<Photo> Photos => photos;

        void Awake()
        {
            Services.Register(this);
            // (A new run starts with no photos.)
            if (Services.TryGet<EventBus>(out var events)) resetSub = events.Subscribe<RunStarted>(_ => Clear());
        }

        void OnDestroy()
        {
            resetSub?.Dispose();
            Clear();
            if (shooter != null) Destroy(shooter.gameObject);
            Services.Unregister(this);
        }

        void Clear()
        {
            foreach (var p in photos) if (p.Picture != null) p.Picture.Release();
            photos.Clear();
        }

        /// <summary>Take a photo of what this camera sees, now.</summary>
        public void Take(Camera from, ItemSettings s)
        {
            if (from == null) return;
            if (shooter == null) MakeShooter();
            int size = Mathf.Clamp(s.photoSize, 64, 2048);
            var picture = new RenderTexture(size, size, 24, RenderTextureFormat.DefaultHDR) { name = $"Polaroid {photos.Count + 1}" };
            picture.Create();
            shooter.transform.SetPositionAndRotation(from.transform.position, from.transform.rotation);
            shooter.fieldOfView = from.fieldOfView;
            shooter.nearClipPlane = from.nearClipPlane;
            shooter.farClipPlane = from.farClipPlane;
            shooter.cullingMask = from.cullingMask;
            shooter.targetTexture = picture;
            int lamps = Services.TryGet<GameRun>(out var run) ? run.LampsLit : 0;
            photos.Add(new Photo { Picture = picture, TakenAt = run != null ? run.Elapsed : Time.time, LampsLit = lamps });
            StartCoroutine(Shoot(photos.Count - 1));
        }

        // On for one frame (rendered with the flash at its brightest), then off.
        IEnumerator Shoot(int index)
        {
            shooter.enabled = true;
            yield return null;
            shooter.enabled = false;
            if (Services.TryGet<EventBus>(out var events)) events.Publish(new PhotoTaken(index));
        }

        void MakeShooter()
        {
            var go = new GameObject("Polaroid camera");
            go.transform.SetParent(transform, false);
            shooter = go.AddComponent<Camera>();
            shooter.enabled = false;
            shooter.aspect = 1f;
            // Graded like your view (the picture is shown on screen, not in the world), but no motion blur: on a
            // camera's first frame there's nothing to blur from.
            var hd = go.AddComponent<HDAdditionalCameraData>();
            hd.customRenderingSettings = true;
            hd.renderingPathCustomFrameSettingsOverrideMask.mask[(uint)FrameSettingsField.MotionBlur] = true;
            hd.renderingPathCustomFrameSettings.SetEnabled(FrameSettingsField.MotionBlur, false);
        }
    }
}
