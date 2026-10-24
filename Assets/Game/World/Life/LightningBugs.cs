using System.Collections.Generic;
using Game.Core;
using Game.Player;
using Game.Rendering;
using UnityEngine;

namespace Game.World
{
    /// <summary>
    /// Lightning bugs (section 6.13), the way the common ones do it: small dark beetles drifting low and slow
    /// through the trees, each one now and then flashing for half a second (the green of the batteries) while it
    /// swoops up in a "J", then going dark again, every five or six seconds. A few sit in the grass and answer a
    /// flash nearby a couple of seconds later, dimmer. They keep round you as you go.
    /// </summary>
    public sealed class LightningBugs : MonoBehaviour
    {
        sealed class Bug
        {
            public Transform body;
            public Renderer glow;
            public Vector3 velocity;
            public float nextFlash, flashT = -1f, answerAt = -1f, seed, height, brightness = 1f;
            public bool sitting;
        }

        readonly List<Bug> bugs = new List<Bug>();
        ForestLifeSettings s;
        Color green = new Color(0.25f, 1f, 0.35f);
        PlayerController player;
        Material shell;
        Mesh bodyMesh;
        bool placed;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void AttachToGame()
        {
            if (!Services.TryGet<PlayerController>(out _) || !Services.TryGet<Game.World.Build.WorldBuilder>(out _)) return;
            if (Services.TryGet<GameRoot>(out var root) && root.GetComponentInChildren<LightningBugs>() == null)
                new GameObject("Lightning bugs").AddComponent<LightningBugs>().transform.SetParent(root.transform, false);
        }

        void Start()
        {
            var config = Services.Get<GameSetup>().Get<ForestLifeConfig>();
            s = config != null ? config.settings : new ForestLifeSettings();
            var items = Services.Get<GameSetup>().Get<ItemsConfig>();
            if (items != null) green = items.settings.batteryGlow;
            shell = RuntimeMaterials.Lit("Lightning bug", new Color(0.02f, 0.018f, 0.015f), 0.5f);
            var capsule = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            bodyMesh = capsule.GetComponent<MeshFilter>().sharedMesh;
            Destroy(capsule);
            for (int i = 0; i < s.lightningBugs; i++) bugs.Add(Make(i));
        }

        void OnDestroy()
        {
            if (shell != null) Destroy(shell);
        }

        Bug Make(int i)
        {
            var b = new Bug { seed = Random.value * 100f, sitting = Random.value < s.lightningBugsSitting };
            var go = new GameObject("Lightning bug") { layer = Layers.Effects };
            go.transform.SetParent(transform, false);
            b.body = go.transform;
            // A little dark beetle, about a centimetre and a half long (its length along its flight).
            var shellGo = new GameObject("Shell") { layer = Layers.Effects };
            shellGo.transform.SetParent(go.transform, false);
            shellGo.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            shellGo.transform.localScale = new Vector3(0.008f, 0.009f, 0.008f);
            shellGo.AddComponent<MeshFilter>().sharedMesh = bodyMesh;
            var r = shellGo.AddComponent<MeshRenderer>();
            r.sharedMaterial = shell;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            // The light, at its tail end.
            b.glow = Effects.Glow(go.transform, Vector3.back * 0.008f, 0.28f, Color.black, 0.85f, 3f, "Lightning bug glow");
            b.nextFlash = Random.Range(0f, s.lightningBugEvery * 2f);
            return b;
        }

        void Update()
        {
            if (player == null && !Services.TryGet(out player)) return;
            if (PlayerPlace.InDarkRoom)
            {
                foreach (var b in bugs) if (b.body.gameObject.activeSelf) b.body.gameObject.SetActive(false);
                return;
            }
            float dt = Time.deltaTime;
            if (dt <= 0f) return;
            Vector3 p = player.transform.position;
            if (!placed)
            {
                foreach (var b in bugs) Place(b, p, true);
                placed = true;
            }

            float t = Time.time;
            for (int i = 0; i < bugs.Count; i++)
            {
                var b = bugs[i];
                if (!b.body.gameObject.activeSelf) b.body.gameObject.SetActive(true);
                Vector3 pos = b.body.position;
                Vector3 off = pos - p;
                off.y = 0f;
                if (off.sqrMagnitude > s.lightningBugRange * s.lightningBugRange * 1.3f) { Place(b, p, false); continue; }

                // Flashing: half a second, quick on, a hold, a softer fade.
                float light = 0f;
                if (b.flashT >= 0f)
                {
                    b.flashT += dt;
                    float f = b.flashT / 0.55f;
                    light = f < 0.25f ? f / 0.25f : f < 0.5f ? 1f : Mathf.Max(0f, 1f - (f - 0.5f) / 0.5f);
                    if (f >= 1f) b.flashT = -1f;
                }
                else if (!b.sitting && t >= b.nextFlash)
                {
                    Flash(b, 1f);
                    b.nextFlash = t + s.lightningBugEvery * Random.Range(0.75f, 1.3f);
                    // Those sitting near answer, a couple of seconds on.
                    foreach (var other in bugs)
                        if (other.sitting && other.answerAt < 0f && (other.body.position - pos).sqrMagnitude < 36f)
                            other.answerAt = t + Random.Range(1.8f, 2.4f);
                }
                if (b.sitting && b.answerAt >= 0f && t >= b.answerAt && b.flashT < 0f)
                {
                    b.answerAt = -1f;
                    Flash(b, 0.55f);
                }

                if (!b.sitting)
                {
                    // Drifting low and slow, wandering; while it flashes it swoops up in a J.
                    Vector3 wander = new Vector3(Mathf.PerlinNoise(b.seed, t * 0.25f) - 0.5f, 0f, Mathf.PerlinNoise(t * 0.25f, b.seed) - 0.5f) * 1.4f;
                    float lift = b.flashT >= 0f ? 0.9f : 0f;
                    float ground = Ground(pos);
                    float wantY = ground + b.height;
                    Vector3 want = wander + Vector3.up * ((wantY - pos.y) * 0.6f + lift);
                    b.velocity = Vector3.Lerp(b.velocity, want, 1f - Mathf.Exp(-1.5f * dt));
                    pos += b.velocity * dt;
                    // After the J, it settles back down.
                    if (pos.y < ground + 0.25f) pos.y = ground + 0.25f;
                    b.body.position = pos;
                    if (b.velocity.sqrMagnitude > 1e-4f) b.body.rotation = Quaternion.LookRotation(b.velocity);
                }
                if (b.glow != null) Effects.SetGlow(b.glow, green * (s.lightningBugGlow * b.brightness * light));
            }
        }

        void Flash(Bug b, float brightness)
        {
            b.flashT = 0f;
            b.brightness = brightness;
        }

        // Somewhere round you: anywhere at the start, at the edge of the range after (so they don't pop in close).
        void Place(Bug b, Vector3 p, bool anywhere)
        {
            float a = Random.Range(0f, Mathf.PI * 2f);
            float r = s.lightningBugRange * (anywhere ? Mathf.Sqrt(Random.Range(0.02f, 1f)) : Random.Range(0.8f, 1f));
            Vector3 at = p + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * r;
            float ground = Ground(at);
            b.height = b.sitting ? 0.12f : Random.Range(0.5f, 2.6f);
            at.y = ground + b.height;
            b.body.position = at;
            b.velocity = Vector3.zero;
            b.flashT = -1f;
        }

        static float Ground(Vector3 p)
        {
            if (Physics.Raycast(p + Vector3.up * 10f, Vector3.down, out var hit, 40f, Layers.Mask(Layers.Ground), QueryTriggerInteraction.Ignore)) return hit.point.y;
            return p.y - 1f;
        }
    }
}
