using Game.Audio;
using Game.Core;
using Game.Rendering;
using UnityEngine;
using UnityEngine.Rendering.HighDefinition;

namespace Game.Player
{
    /// <summary>
    /// The LCD on the drone's remote: a little green-backlit radar. You're the cross in the middle, the way you
    /// face is up; the drone is a blinking dot where it is from you (the ring is its range), and every couple of
    /// seconds it pings, a ring spreading out from it and a soft beep. Along the bottom, its battery and its
    /// signal. "No signal" when it's gone.
    /// </summary>
    public sealed class DroneRemoteScreen : MonoBehaviour
    {
        const int W = 48, H = 32;
        const float PingEvery = 1.8f;

        public Renderer screen;
        public PlayerController player;
        public float range = 140f;

        Texture2D tex;
        Color32[] px;
        Material mat;
        float next, lastPing = -10f;

        static readonly Color32 Back = new Color32(70, 110, 55, 255), Ink = new Color32(12, 28, 10, 255), Mid = new Color32(40, 70, 32, 255);

        void Start()
        {
            tex = new Texture2D(W, H, TextureFormat.RGBA32, false) { name = "Remote LCD", filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
            px = new Color32[W * H];
            if (screen != null)
            {
                mat = RuntimeMaterials.Lit("Remote LCD", Color.black, 0.8f);
                mat.SetTexture("_EmissiveColorMap", tex);
                HDMaterial.SetEmissiveColor(mat, Color.white * 0.9f);
                HDMaterial.ValidateMaterial(mat);
                screen.sharedMaterial = mat;
            }
        }

        void OnDestroy()
        {
            if (tex != null) Destroy(tex);
            if (mat != null) Destroy(mat);
        }

        void Update()
        {
            if (tex == null || Time.time < next) return;
            next = Time.time + 0.08f;
            var drone = Drone.Current;
            for (int i = 0; i < px.Length; i++) px[i] = Back;
            int cx = W / 2, cy = H / 2 + 3;
            const float R = 12f;
            // The range ring, faint, and you in the middle.
            Ring(cx, cy, R, Mid);
            Dot(cx, cy, Ink); Dot(cx - 1, cy, Ink); Dot(cx + 1, cy, Ink); Dot(cx, cy - 1, Ink); Dot(cx, cy + 1, Ink);
            if (drone == null || player == null)
            {
                // No signal: a cross over it.
                for (int i = -6; i <= 6; i++) { Dot(cx + i, cy + i, Ink); Dot(cx + i, cy - i, Ink); }
            }
            else
            {
                // Where it is from you, turned so the way you face is up.
                Vector3 off = drone.transform.position - player.transform.position;
                off.y = 0f;
                Vector3 local = Quaternion.Euler(0f, -player.transform.eulerAngles.y, 0f) * off;
                float d = Mathf.Min(1f, local.magnitude / Mathf.Max(1f, range));
                Vector2 dir = local.sqrMagnitude > 0.01f ? new Vector2(local.x, local.z).normalized : Vector2.zero;
                int dx = cx + Mathf.RoundToInt(dir.x * d * R), dy = cy + Mathf.RoundToInt(dir.y * d * R);
                // The ping: a ring spreading out from it, and a soft beep.
                if (Time.time - lastPing >= PingEvery)
                {
                    lastPing = Time.time;
                    if (Services.TryGet<SoundPlayer>(out var sounds)) sounds.Play2D(Sound.Click, MixerNames.Player, 0.18f, 2.8f);
                }
                float p = (Time.time - lastPing) / 0.7f;
                if (p < 1f) Ring(dx, dy, 1.5f + p * 6f, p < 0.5f ? Ink : Mid);
                if ((int)(Time.time * 3f) % 2 == 0 || p < 0.3f)
                    for (int y = -1; y <= 1; y++) for (int x = -1; x <= 1; x++) Dot(dx + x, dy + y, Ink);
                // Battery and signal, along the bottom.
                Bar(2, 2, 20, drone.Battery);
                for (int b = 0; b < 4; b++)
                    if (drone.Signal > b / 4f)
                        for (int y = 0; y <= b + 1; y++) Dot(W - 12 + b * 3, 2 + y, Ink);
            }
            tex.SetPixels32(px);
            tex.Apply(false);
        }

        void Bar(int x, int y, int w, float fill)
        {
            for (int i = 0; i < w; i++) { Dot(x + i, y, Mid); Dot(x + i, y + 2, Mid); }
            int n = Mathf.RoundToInt(w * Mathf.Clamp01(fill));
            for (int i = 0; i < n; i++) Dot(x + i, y + 1, Ink);
        }

        void Ring(int x, int y, float r, Color32 c)
        {
            int steps = Mathf.Max(8, Mathf.RoundToInt(r * 7f));
            for (int i = 0; i < steps; i++)
            {
                float a = i / (float)steps * Mathf.PI * 2f;
                Dot(x + Mathf.RoundToInt(Mathf.Cos(a) * r), y + Mathf.RoundToInt(Mathf.Sin(a) * r), c);
            }
        }

        void Dot(int x, int y, Color32 c)
        {
            if (x >= 0 && y >= 0 && x < W && y < H) px[y * W + x] = c;
        }
    }
}
