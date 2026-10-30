using System.Collections.Generic;
using Game.Audio;
using Game.Core;
using Game.Player;
using Game.Rendering;
using UnityEngine;

namespace Game.World
{
    /// <summary>
    /// Flies (section 6.13): a few about in the forest round you, each hanging about its own spot, darting from
    /// point to point (a quick straight dash, a sudden turn, a moment hovering) and buzzing all the while. You only
    /// hear one close: the buzz falls away steeply with distance, as a real fly's does, and its pitch bends as it
    /// passes. Now and then one comes to you and buzzes round your head a while before it goes.
    /// </summary>
    public sealed class Flies : MonoBehaviour
    {
        sealed class Fly
        {
            public Transform body;
            public AudioSource buzz;
            public Vector3 home, target, velocity;
            public float retarget, visitLeft, pitch;
        }

        readonly List<Fly> flies = new List<Fly>();
        ForestLifeSettings s;
        PlayerController player;
        Material shell, wing;
        Mesh bodyMesh;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void AttachToGame()
        {
            if (!Services.TryGet<PlayerController>(out _) || !Services.TryGet<Game.World.Build.WorldBuilder>(out _)) return;
            if (Services.TryGet<GameRoot>(out var root) && root.GetComponentInChildren<Flies>() == null)
                new GameObject("Flies").AddComponent<Flies>().transform.SetParent(root.transform, false);
        }

        void Start()
        {
            var config = Services.Get<GameSetup>().Get<ForestLifeConfig>();
            s = config != null ? config.settings : new ForestLifeSettings();
            shell = RuntimeMaterials.Lit("Fly", new Color(0.02f, 0.02f, 0.018f), 0.7f);
            wing = RuntimeMaterials.Lit("Fly wings", new Color(0.25f, 0.25f, 0.27f), 0.9f, true);
            var capsule = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            bodyMesh = capsule.GetComponent<MeshFilter>().sharedMesh;
            Destroy(capsule);
            for (int i = 0; i < s.flies; i++) flies.Add(Make(i));
        }

        void OnDestroy()
        {
            if (shell != null) Destroy(shell);
            if (wing != null) Destroy(wing);
        }

        Fly Make(int i)
        {
            var go = new GameObject("Fly") { layer = Layers.Effects };
            go.transform.SetParent(transform, false);
            var b = new GameObject("Body") { layer = Layers.Effects };
            b.transform.SetParent(go.transform, false);
            b.transform.localScale = new Vector3(0.006f, 0.005f, 0.009f);
            b.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            b.AddComponent<MeshFilter>().sharedMesh = bodyMesh;
            var r = b.AddComponent<MeshRenderer>();
            r.sharedMaterial = shell;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            // Its wings, a faint blur either side.
            for (int w = -1; w <= 1; w += 2)
            {
                var wg = GameObject.CreatePrimitive(PrimitiveType.Quad);
                Destroy(wg.GetComponent<Collider>());
                wg.layer = Layers.Effects;
                wg.transform.SetParent(go.transform, false);
                wg.transform.localPosition = new Vector3(w * 0.006f, 0.002f, -0.001f);
                wg.transform.localRotation = Quaternion.Euler(90f, w * 15f, 0f);
                wg.transform.localScale = new Vector3(0.008f, 0.006f, 1f);
                var wr = wg.GetComponent<Renderer>();
                wr.sharedMaterial = wing;
                wr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
            var f = new Fly { body = go.transform, pitch = Random.Range(0.85f, 1.2f) };
            if (Services.TryGet<SoundPlayer>(out var sounds))
            {
                var a = go.AddComponent<AudioSource>();
                a.clip = sounds.Clip(Sound.FlyBuzz);
                a.loop = true;
                a.spatialBlend = 1f;
                // (Steep, like the real thing: loud by your ear, gone a few metres off.)
                a.rolloffMode = AudioRolloffMode.Logarithmic;
                a.minDistance = 0.12f;
                a.maxDistance = s.flyHeard;
                a.dopplerLevel = 1.2f; // (the pitch bending as it passes)
                a.volume = s.flyVolume;
                a.pitch = f.pitch;
                a.outputAudioMixerGroup = sounds.Group(MixerNames.World);
                a.time = Random.Range(0f, 4f);
                a.Play();
                f.buzz = a;
            }
            return f;
        }

        void Update()
        {
            if (player == null && !Services.TryGet(out player)) return;
            bool away = PlayerPlace.InDarkRoom || !GameFlow.IsPlaying;
            float dt = Time.deltaTime;
            Vector3 you = player.transform.position;
            Vector3 head = player.Head != null ? player.Head.position : you + Vector3.up * 1.6f;
            foreach (var f in flies)
            {
                if (f.buzz != null)
                {
                    if (away && f.buzz.isPlaying) f.buzz.Pause();
                    else if (!away && !f.buzz.isPlaying) f.buzz.UnPause();
                    f.buzz.maxDistance = s.flyHeard;
                    f.buzz.volume = s.flyVolume;
                }
                if (away) continue;
                // Too far from you (or not placed yet): somewhere new round you.
                if (f.home == Vector3.zero || Flat(f.home - you).magnitude > s.flyRange * 1.3f) Settle(f, you);
                // Now and then, it comes to you.
                if (f.visitLeft <= 0f && Random.value < s.flyVisit / 60f * dt) f.visitLeft = Random.Range(4f, 10f);
                Vector3 centre = f.home;
                float spread = 1.2f;
                if (f.visitLeft > 0f)
                {
                    f.visitLeft -= dt;
                    centre = head + Vector3.up * 0.1f;
                    spread = 0.55f;
                    if (f.visitLeft <= 0f) Settle(f, you);
                }
                // Darting: a new point every moment, flown at in a quick, jittery dash.
                f.retarget -= dt;
                if (f.retarget <= 0f)
                {
                    f.retarget = Random.Range(0.15f, 0.7f);
                    f.target = centre + Random.insideUnitSphere * spread;
                    if (f.visitLeft > 0f && (f.target - head).sqrMagnitude < 0.04f) f.target = head + (f.target - head).normalized * 0.25f; // (not in your eye)
                }
                Vector3 want = (f.target - f.body.position);
                float dist = want.magnitude;
                Vector3 desired = dist > 0.05f ? want / dist * Mathf.Min(3.2f, 1.2f + dist * 4f) : Vector3.zero;
                f.velocity = Vector3.Lerp(f.velocity, desired, 1f - Mathf.Exp(-9f * dt)) + Random.insideUnitSphere * 2.5f * dt;
                f.body.position += f.velocity * dt;
                if (f.velocity.sqrMagnitude > 0.01f) f.body.rotation = Quaternion.Slerp(f.body.rotation, Quaternion.LookRotation(f.velocity), 1f - Mathf.Exp(-14f * dt));
                // (Its wings' pitch rising a touch as it speeds up.)
                if (f.buzz != null) f.buzz.pitch = f.pitch * (1f + f.velocity.magnitude * 0.03f);
            }
        }

        static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0f, v.z);

        static float Ground(Vector3 p) =>
            Physics.Raycast(p + Vector3.up * 30f, Vector3.down, out var hit, 80f, Layers.Mask(Layers.Ground), QueryTriggerInteraction.Ignore) ? hit.point.y : p.y;

        // A spot of its own to hang about, round you, low over the ground.
        void Settle(Fly f, Vector3 you)
        {
            float a = Random.Range(0f, Mathf.PI * 2f), r = Mathf.Sqrt(Random.Range(0.05f, 1f)) * s.flyRange;
            var p = new Vector3(you.x + Mathf.Cos(a) * r, 0f, you.z + Mathf.Sin(a) * r);
            p.y = Ground(p) + Random.Range(0.4f, 2f);
            f.home = p;
            f.visitLeft = 0f;
            if (f.target == Vector3.zero || (f.body.position - p).sqrMagnitude > 900f) f.body.position = p;
            f.target = p;
        }
    }
}
