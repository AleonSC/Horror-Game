using System;
using System.Collections;
using System.Collections.Generic;
using Game.Audio;
using Game.Core;
using Game.Rendering;
using UnityEngine;
using UnityEngine.Rendering.HighDefinition;

namespace Game.World
{
    /// <summary>
    /// The lone door's ring (section 6.5): ten standing stones round it, each with a rune cut into the face it
    /// turns to the door, one for each lamp. As each lamp is lit the next stone wakes, its rune kindling cold and
    /// pale; and the door answers: a blow from the other side that shakes it in its frame, more light seeping
    /// out under it and through the keyhole each time, and it creaks a little further open (still locked: a
    /// glimpse of the room through the crack). At the tenth every stone flares, the light floods out
    /// round the door, and it unlocks. Works round your own door too (the light seeps from its foot).
    /// </summary>
    public sealed class DoorShrine : MonoBehaviour
    {
        public const int Stones = GameRun.LampCount;

        [Tooltip("The colour the runes and the door's light burn.")]
        public Color glow = new Color(0.62f, 0.8f, 1f);
        [Tooltip("How bright a woken rune is.")]
        public float runeGlow = 2.2f;
        [Tooltip("Each woken stone's light on the ground round it (candela).")]
        public float stoneLight = 4f;
        [Tooltip("The light seeping out round the door when all ten are lit (candela); a share of it before.")]
        public float doorLight = 60f;
        [Range(0f, 0.6f), Tooltip("How far the door stands open by the ninth lamp (a share of its full swing); it opens a little more with each lamp up to there, and swings open at the tenth.")]
        public float ajarAtNine = 0.33f;
                [Tooltip("How far off the door's blow and the stones waking are heard (metres).")]
        public float heardFrom = 160f;

        readonly Material[] runes = new Material[Stones];
        readonly Light[] lights = new Light[Stones];
        readonly float[] level = new float[Stones];   // each stone's rune now (0 dark, 1 woken; above 1 flaring)
        readonly float[] target = new float[Stones];
        Material seep;
        readonly List<Light> doorLights = new List<Light>();
        Transform hinge;
        Door door;
        float seepLevel, seepTarget;
        readonly List<IDisposable> subs = new List<IDisposable>();
        readonly List<UnityEngine.Object> made = new List<UnityEngine.Object>();

        /// <summary>The ring round this door (its root: the door faces +Z), the stones set on the ground as given.</summary>
        public static DoorShrine Build(Transform doorRoot, Func<Vector3, float> groundAt)
        {
            var shrine = doorRoot.gameObject.AddComponent<DoorShrine>();
            shrine.Make(doorRoot, groundAt);
            return shrine;
        }

        void Make(Transform root, Func<Vector3, float> groundAt)
        {
            var rng = new System.Random(Mathf.RoundToInt(root.position.x * 7f + root.position.z * 13f));
            var stone = Keep(RuntimeMaterials.Lit("Standing stone", new Color(0.16f, 0.16f, 0.155f), 0.18f));
            var moss = Keep(RuntimeMaterials.Lit("Stone moss", new Color(0.07f, 0.09f, 0.05f), 0.08f));
            for (int i = 0; i < Stones; i++)
            {
                // Round the door, none straight in front or behind it (the way through stays open).
                float a = (i + 0.5f) / Stones * Mathf.PI * 2f;
                float r = 6f + (float)(rng.NextDouble() - 0.5) * 0.6f;
                Vector3 local = new Vector3(Mathf.Sin(a) * r, 0f, Mathf.Cos(a) * r);
                Vector3 world = root.TransformPoint(local);
                world.y = groundAt(world) - 0.25f; // (sunk a little: they've stood here a long time)
                var at = new GameObject($"Standing stone {i + 1}") { layer = Layers.Props }.transform;
                at.SetParent(root, true);
                at.position = world;
                // Facing the door, leaning a little.
                Vector3 toDoor = root.position - world;
                toDoor.y = 0f;
                at.rotation = Quaternion.LookRotation(toDoor.normalized) * Quaternion.Euler((float)(rng.NextDouble() - 0.5) * 8f, 0f, (float)(rng.NextDouble() - 0.5) * 8f);

                float height = 1.7f + (float)rng.NextDouble() * 0.9f, width = 0.45f + (float)rng.NextDouble() * 0.2f, depth = 0.26f + (float)rng.NextDouble() * 0.1f;
                var mesh = Keep(StoneMesh(rng, width, depth, height));
                var go = new GameObject("Stone") { layer = Layers.Props };
                go.transform.SetParent(at, false);
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                go.AddComponent<MeshRenderer>().sharedMaterial = stone;
                var col = at.gameObject.AddComponent<CapsuleCollider>();
                col.radius = Mathf.Max(width, depth) * 0.9f;
                col.height = height;
                col.center = Vector3.up * height * 0.5f;
                // Moss low down on its back.
                var m = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                Destroy(m.GetComponent<Collider>());
                m.transform.SetParent(at, false);
                m.transform.localPosition = new Vector3(0f, 0.35f, -depth * 0.55f);
                m.transform.localScale = new Vector3(width * 1.6f, 0.5f, 0.08f);
                m.GetComponent<Renderer>().sharedMaterial = moss;

                // The rune, cut into the face toward the door, at chest height.
                runes[i] = Keep(RuntimeMaterials.LitGlow($"Rune {i + 1}", new Color(0.05f, 0.05f, 0.05f), Color.black));
                Rune(at, new Vector3(0f, height * 0.58f, depth * 1.06f + 0.01f), rng, runes[i]);

                var lightGo = new GameObject("Stone light");
                lightGo.transform.SetParent(at, false);
                lightGo.transform.localPosition = new Vector3(0f, height * 0.58f, depth + 0.5f);
                lightGo.AddHDLight(LightType.Point);
                var l = lightGo.GetComponent<Light>();
                l.color = glow;
                l.range = 5f;
                l.shadows = LightShadows.None;
                l.enabled = false;
                lights[i] = l;
            }

            // The light seeping out round the door: a pale line along its foot and the keyhole, both sides, and a
            // light just off each face.
            hinge = FindChild(root, "Door");
            door = hinge != null ? hinge.GetComponent<Door>() : root.GetComponentInChildren<Door>();
            seep = Keep(RuntimeMaterials.Glow("Light under the door", Color.black));
            foreach (int side in new[] { 1, -1 })
            {
                var strip = GameObject.CreatePrimitive(PrimitiveType.Cube);
                Destroy(strip.GetComponent<Collider>());
                strip.name = "Light under the door";
                strip.transform.SetParent(root, false);
                strip.transform.localPosition = new Vector3(0f, 0.035f, side * 0.06f);
                strip.transform.localScale = new Vector3(1.05f, 0.012f, 0.02f);
                strip.GetComponent<Renderer>().sharedMaterial = seep;
                strip.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                var lgo = new GameObject("Door light");
                lgo.transform.SetParent(root, false);
                lgo.transform.localPosition = new Vector3(0f, 0.3f, side * 0.45f);
                lgo.AddHDLight(LightType.Point);
                var dl = lgo.GetComponent<Light>();
                dl.color = glow;
                dl.range = 6f;
                dl.shadows = LightShadows.None;
                dl.enabled = false;
                doorLights.Add(dl);
            }
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
                if (t.name == "Keyhole" && t.TryGetComponent<Renderer>(out var key)) key.sharedMaterial = seep;
        }

        Material Keep(Material m) { made.Add(m); return m; }
        Mesh Keep(Mesh m) { made.Add(m); return m; }

        static Transform FindChild(Transform root, string name)
        {
            foreach (var t in root.GetComponentsInChildren<Transform>(true)) if (t != root && t.name == name) return t;
            return null;
        }

        // A rough standing stone: an irregular slab, wider than deep, tapering a little and rounding over at the
        // top, every point pushed in or out a little. Its foot at the origin, its face toward +Z.
        static Mesh StoneMesh(System.Random rng, float width, float depth, float height)
        {
            const int sides = 10, rings = 6;
            var verts = new List<Vector3>();
            var tris = new List<int>();
            float tilt = (float)(rng.NextDouble() - 0.5) * 0.15f;
            for (int j = 0; j <= rings; j++)
            {
                float v = j / (float)rings;
                float shrink = j == rings ? 0.35f : 1f - 0.25f * v * v;
                float y = j == rings ? height : height * v * 0.94f;
                for (int i = 0; i < sides; i++)
                {
                    float a = i / (float)sides * Mathf.PI * 2f;
                    float jit = 1f + (float)(rng.NextDouble() - 0.5) * 0.22f;
                    verts.Add(new Vector3(Mathf.Cos(a) * width * shrink * jit + tilt * y, y + (float)(rng.NextDouble() - 0.5) * 0.06f * (j > 0 ? 1f : 0f), Mathf.Sin(a) * depth * shrink * jit));
                }
            }
            for (int j = 0; j < rings; j++)
                for (int i = 0; i < sides; i++)
                {
                    int a = j * sides + i, b = j * sides + (i + 1) % sides, c = a + sides, d = b + sides;
                    tris.Add(a); tris.Add(c); tris.Add(b);
                    tris.Add(b); tris.Add(c); tris.Add(d);
                }
            int top = verts.Count;
            verts.Add(new Vector3(tilt * height, height + 0.05f, 0f));
            for (int i = 0; i < sides; i++) { tris.Add(rings * sides + i); tris.Add(top); tris.Add(rings * sides + (i + 1) % sides); }
            var mesh = new Mesh { name = "Standing stone" };
            mesh.SetVertices(verts);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        // A rune: a tall stroke and a few branches off it, different on every stone.
        static void Rune(Transform stone, Vector3 at, System.Random rng, Material m)
        {
            var rune = new GameObject("Rune").transform;
            rune.SetParent(stone, false);
            rune.localPosition = at;
            void Stroke(Vector2 a, Vector2 b)
            {
                var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
                Destroy(go.GetComponent<Collider>());
                go.transform.SetParent(rune, false);
                Vector2 d = b - a;
                go.transform.localPosition = (Vector3)((a + b) * 0.5f);
                go.transform.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg);
                go.transform.localScale = new Vector3(d.magnitude + 0.025f, 0.028f, 0.012f);
                var r = go.GetComponent<Renderer>();
                r.sharedMaterial = m;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
            float h = 0.22f;
            Stroke(new Vector2(0f, -h), new Vector2(0f, h));
            int branches = 2 + rng.Next(3);
            for (int k = 0; k < branches; k++)
            {
                float y = (float)(rng.NextDouble() * 2 - 1) * h * 0.8f;
                float side = rng.NextDouble() < 0.5 ? -1f : 1f;
                float rise = (float)(rng.NextDouble() * 2 - 1) * 0.12f;
                Stroke(new Vector2(0f, y), new Vector2(side * (0.08f + (float)rng.NextDouble() * 0.06f), y + rise));
            }
            if (rng.NextDouble() < 0.5) Stroke(new Vector2(-0.07f, h * 0.95f), new Vector2(0.07f, h * 0.95f));
        }

        void Start()
        {
            int lit = Services.TryGet<GameRun>(out var run) ? run.LampsLit : 0;
            SetLit(lit, instant: true);
            if (Services.TryGet<EventBus>(out var events))
            {
                subs.Add(events.Subscribe<LampLit>(e => StartCoroutine(Answer(e.TimesLit))));
                subs.Add(events.Subscribe<RunStarted>(_ => SetLit(0, instant: true)));
            }
        }

        void OnDestroy()
        {
            foreach (var s in subs) s.Dispose();
            foreach (var o in made) if (o != null) Destroy(o);
        }

        void SetLit(int lit, bool instant)
        {
            for (int i = 0; i < Stones; i++)
            {
                target[i] = i < lit ? 1f : 0f;
                if (instant) level[i] = target[i];
            }
            seepTarget = Mathf.Clamp01(lit / (float)Stones);
            if (instant) seepLevel = seepTarget;
            SetAjar(lit, instant);
        }

        // A lamp lit: the next stone wakes, and the door answers.
        IEnumerator Answer(int timesLit)
        {
            int stone = Mathf.Clamp(timesLit - 1, 0, Stones - 1);
            Play(Sound.StoneWake, lights[stone] != null ? lights[stone].transform.position : transform.position, 0.9f, UnityEngine.Random.Range(0.95f, 1.05f));
            target[stone] = 1f;
            level[stone] = Mathf.Max(level[stone], 0.01f);
            StartCoroutine(Flare(stone, 2.6f));
            yield return new WaitForSeconds(1.4f);

            // The door: struck from the other side (twice, then three times as the last ones are lit), shaking in
            // its frame, more light seeping out each time.
            int blows = timesLit >= Stones ? 3 : timesLit >= 7 ? 2 : 1;
            for (int b = 0; b < blows; b++)
            {
                Play(Sound.DoorThud, transform.position + Vector3.up * 1.1f, 1f, UnityEngine.Random.Range(0.85f, 1f));
                if (door != null) door.Rattle(0.45f, 1.6f);
                seepLevel = Mathf.Min(1f, seepLevel + 0.4f); // (a pulse of it with each blow)
                yield return new WaitForSeconds(1.2f);
            }
            seepTarget = Mathf.Clamp01(timesLit / (float)Stones);
            SetAjar(timesLit, false); // (and it creaks a little wider)

            if (timesLit >= Stones)
            {
                // All ten: every stone flares, and the light pours out round the door.
                for (int i = 0; i < Stones; i++) StartCoroutine(Flare(i, 3.5f));
                Play(Sound.StoneWake, transform.position + Vector3.up * 2f, 1f, 0.7f);
                seepLevel = 2f;
                yield return new WaitForSeconds(0.8f);
                if (door != null) door.SwingOpen(); // (by itself, slowly: come in)
            }
        }

        // The door stands a little further open with each lamp lit: a crack after the first, a hand's width
        // after a few, a third of the way by the ninth (still locked, still solid: a glimpse of the room through
        // it, no way in). At the tenth it swings open.
        void SetAjar(int lit, bool instant)
        {
            if (door == null || door.IsOpen) return;
            float share = lit <= 0 ? 0f : lit >= Stones ? ajarAtNine : ajarAtNine * Mathf.Pow(lit / (Stones - 1f), 0.8f);
            if (instant) door.ajar = share;
            else door.SetAjar(share);
        }

        // A stone's rune flares well past its woken glow, then settles to it.
        IEnumerator Flare(int i, float seconds)
        {
            for (float t = 0f; t < seconds; t += Time.deltaTime)
            {
                float k = t / seconds;
                level[i] = Mathf.Max(target[i], Mathf.SmoothStep(0f, 1f, Mathf.Min(1f, k * 4f)) * (1f + 2.5f * (1f - k) * (1f - k)));
                yield return null;
            }
            level[i] = target[i];
        }

        void Update()
        {
            float dt = Time.deltaTime;
            float flicker = 0.9f + 0.1f * Mathf.PerlinNoise(Time.time * 3f, 0.7f);
            for (int i = 0; i < Stones; i++)
            {
                if (level[i] != target[i] && level[i] <= 1f) level[i] = Mathf.MoveTowards(level[i], target[i], dt / 1.5f);
                float l = level[i];
                if (runes[i] != null) RuntimeMaterials.SetGlow(runes[i], glow * runeGlow * l * flicker);
                if (lights[i] != null)
                {
                    lights[i].enabled = l > 0.01f;
                    lights[i].intensity = stoneLight * l * flicker;
                }
            }
            seepLevel = Mathf.MoveTowards(seepLevel, seepTarget, dt / (seepLevel > seepTarget ? 2f : 1f));
            float sl = seepLevel * (0.85f + 0.15f * Mathf.PerlinNoise(Time.time * 5f, 2.1f));
            if (seep != null) RuntimeMaterials.SetGlow(seep, glow * 4f * sl);
            foreach (var dl in doorLights)
            {
                dl.enabled = sl > 0.01f;
                dl.intensity = doorLight * 0.5f * sl;
            }
        }

        void Play(Sound sound, Vector3 at, float volume, float pitch)
        {
            if (Services.TryGet<SoundPlayer>(out var sounds)) sounds.Play(sound, at, MixerNames.World, volume, pitch, heardFrom);
        }
    }
}
