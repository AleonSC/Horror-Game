using System.Collections.Generic;
using Game.Audio;
using Game.Core;
using Game.Rendering;
using Game.World.Build;
using UnityEngine;
using UnityEngine.Rendering.HighDefinition;

namespace Game.World
{
    /// <summary>
    /// The built-in out-of-place things for the phantoms (your own models replace them: ForestLifeConfig >
    /// Phantoms): a kitchen chair, a stop sign, a gurney with something under its sheet, an old TV showing
    /// static, a telephone on a side table, ringing, a CB radio on a crate, left on, and a child's tricycle. The
    /// ones that make a sound fade it in as they appear and out as they go (<see cref="PhantomSound"/>).
    /// </summary>
    public static class Phantoms
    {
        public const int Count = 7;
        static Material dialGlow;
        static readonly Dictionary<string, Material> cached = new Dictionary<string, Material>();

        // A material made once and kept (phantoms come and go: no new material each time).
        static Material Cached(string name, Color color, float smoothness)
        {
            if (!cached.TryGetValue(name, out var m) || m == null) cached[name] = m = RuntimeMaterials.Lit(name, color, smoothness);
            return m;
        }

        static Material wood, paint, metal, chrome, red, white, sheet, rubber, bakelite, glass, brass, crate, black;

        public static GameObject Make(int which, Transform parent)
        {
            wood ??= RuntimeMaterials.Lit("Phantom wood", new Color(0.13f, 0.09f, 0.065f), 0.3f);
            paint ??= RuntimeMaterials.Lit("Phantom worn paint", new Color(0.32f, 0.33f, 0.28f), 0.25f);
            metal ??= RuntimeMaterials.Lit("Phantom metal", new Color(0.38f, 0.38f, 0.4f), 0.55f);
            chrome ??= RuntimeMaterials.Lit("Phantom chrome", new Color(0.55f, 0.55f, 0.56f), 0.85f);
            red ??= RuntimeMaterials.Lit("Phantom red", new Color(0.48f, 0.04f, 0.03f), 0.5f);
            white ??= RuntimeMaterials.Lit("Phantom white", new Color(0.72f, 0.72f, 0.7f), 0.4f);
            sheet ??= RuntimeMaterials.Lit("Phantom sheet", new Color(0.62f, 0.62f, 0.58f), 0.15f);
            rubber ??= RuntimeMaterials.Lit("Phantom rubber", new Color(0.03f, 0.03f, 0.03f), 0.3f);
            bakelite ??= RuntimeMaterials.Lit("Phantom bakelite", new Color(0.02f, 0.02f, 0.02f), 0.85f);
            glass ??= RuntimeMaterials.Lit("Phantom glass", new Color(0.04f, 0.05f, 0.05f), 0.95f);
            brass ??= RuntimeMaterials.Lit("Phantom brass", new Color(0.45f, 0.33f, 0.14f), 0.6f);
            crate ??= RuntimeMaterials.Lit("Phantom crate", new Color(0.24f, 0.19f, 0.13f), 0.15f);
            black ??= RuntimeMaterials.Lit("Phantom black", new Color(0.015f, 0.015f, 0.015f), 0.3f);
            var root = new GameObject("Phantom");
            root.transform.SetParent(parent, false);
            var t = root.transform;
            switch (which % Count)
            {
                case 0: Chair(t); break;
                case 1: StopSign(t); break;
                case 2: Gurney(t); break;
                case 3: Tv(t); break;
                case 4: Telephone(t); break;
                case 5: Radio(t); break;
                default: Tricycle(t); break;
            }
            return root;
        }

        // A plain wooden kitchen chair, its paint worn through to the wood: splayed legs, stretchers, a dished
        // seat, two back posts with a curved top rail and spindles.
        static void Chair(Transform t)
        {
            t.localRotation = Quaternion.Euler(0f, 0f, 2.5f); // (one leg sunk a little in the soft ground)
            for (int i = 0; i < 4; i++)
            {
                float x = i % 2 == 0 ? -1f : 1f, z = i < 2 ? -1f : 1f;
                Rod(t, new Vector3(x * 0.2f, 0f, z * 0.2f), new Vector3(x * 0.18f, 0.44f, z * 0.18f), 0.02f, i == 1 ? wood : paint);
            }
            foreach (float y in new[] { 0.14f, 0.18f })
            {
                Rod(t, new Vector3(-0.195f, y, -0.195f), new Vector3(-0.195f, y, 0.195f), 0.011f, paint);
                Rod(t, new Vector3(0.195f, y + 0.04f, -0.195f), new Vector3(0.195f, y + 0.04f, 0.195f), 0.011f, paint);
            }
            Rod(t, new Vector3(-0.19f, 0.2f, 0f), new Vector3(0.19f, 0.2f, 0f), 0.011f, paint);
            Box(t, new Vector3(0f, 0.455f, 0f), new Vector3(0.44f, 0.035f, 0.42f), paint);
            Box(t, new Vector3(0f, 0.475f, 0.01f), new Vector3(0.36f, 0.008f, 0.32f), wood); // (worn through, where it's sat on)
            foreach (float x in new[] { -0.18f, 0.18f })
                Rod(t, new Vector3(x, 0.44f, -0.19f), new Vector3(x * 1.05f, 0.95f, -0.25f), 0.017f, paint);
            for (int k = 0; k < 4; k++) // (the top rail, curved)
            {
                float a0 = Mathf.Lerp(-0.19f, 0.19f, k / 4f), a1 = Mathf.Lerp(-0.19f, 0.19f, (k + 1) / 4f);
                Rod(t, new Vector3(a0, 0.92f, -0.25f + 0.03f * (1f - a0 * a0 * 25f)), new Vector3(a1, 0.92f, -0.25f + 0.03f * (1f - a1 * a1 * 25f)), 0.022f, paint);
            }
            for (int k = 0; k < 3; k++)
            {
                float x = Mathf.Lerp(-0.09f, 0.09f, k / 2f);
                Rod(t, new Vector3(x, 0.47f, -0.2f), new Vector3(x, 0.9f, -0.245f), 0.008f, paint);
            }
        }

        // A stop sign on a galvanised square post (holes up it), leaning: a white-edged red octagon with STOP on it,
        // bolted on, its back bare metal, a rust stain and a dent.
        static void StopSign(Transform t)
        {
            t.localRotation = Quaternion.Euler(4f, 0f, -5f);
            Box(t, new Vector3(0f, 1.15f, 0f), new Vector3(0.05f, 2.4f, 0.05f), metal);
            for (float y = 0.15f; y < 2.2f; y += 0.1f) Box(t, new Vector3(0f, y, 0.0255f), new Vector3(0.012f, 0.012f, 0.002f), black);
            var face = new GameObject("Sign") { layer = Layers.Props }.transform;
            face.SetParent(t, false);
            face.localPosition = new Vector3(0f, 2.15f, 0.04f);
            Octagon(face, 0.39f, 0.006f, white, 0f);
            Octagon(face, 0.36f, 0.004f, red, 0.005f);
            Octagon(face, 0.39f, 0.006f, metal, -0.007f); // (the bare back)
            // STOP, in blocks of white.
            const float h = 0.15f, w = 0.022f;
            float x0 = -0.21f;
            void Bar(float x, float y, float sx, float sy) => Box(face, new Vector3(x, y, 0.009f), new Vector3(sx, sy, 0.002f), white);
            // S
            Bar(x0 + 0.04f, h * 0.5f - w * 0.5f, 0.08f, w); Bar(x0 + 0.04f, 0f, 0.08f, w); Bar(x0 + 0.04f, -h * 0.5f + w * 0.5f, 0.08f, w);
            Bar(x0 + w * 0.5f, h * 0.25f, w, h * 0.5f); Bar(x0 + 0.08f - w * 0.5f, -h * 0.25f, w, h * 0.5f);
            // T
            x0 += 0.105f; Bar(x0 + 0.04f, h * 0.5f - w * 0.5f, 0.08f, w); Bar(x0 + 0.04f, 0f, w, h);
            // O
            x0 += 0.105f; Bar(x0 + 0.04f, h * 0.5f - w * 0.5f, 0.08f, w); Bar(x0 + 0.04f, -h * 0.5f + w * 0.5f, 0.08f, w);
            Bar(x0 + w * 0.5f, 0f, w, h); Bar(x0 + 0.08f - w * 0.5f, 0f, w, h);
            // P
            x0 += 0.105f; Bar(x0 + w * 0.5f, 0f, w, h); Bar(x0 + 0.04f, h * 0.5f - w * 0.5f, 0.08f, w); Bar(x0 + 0.04f, 0.005f, 0.08f, w);
            Bar(x0 + 0.08f - w * 0.5f, h * 0.25f, w, h * 0.5f);
            foreach (float y in new[] { 0.25f, -0.25f }) Sphere(face, new Vector3(0f, y, 0.01f), 0.012f, metal);
            Box(face, new Vector3(0.18f, -0.24f, 0.0095f), new Vector3(0.09f, 0.05f, 0.001f), Cached("Rust", new Color(0.2f, 0.08f, 0.03f), 0.1f));
        }

        // A hospital gurney left out in the trees: a tubular frame on scissor legs and castors, side rails, the
        // head raised, a thin mattress, and over it a sheet with the shape of someone under it, a tag on a string
        // hanging from a toe; an IV pole at its head, its bag empty.
        static void Gurney(Transform t)
        {
            const float top = 0.78f;
            foreach (float x in new[] { -0.3f, 0.3f })
            {
                Rod(t, new Vector3(x, top, -0.95f), new Vector3(x, top, 0.95f), 0.018f, chrome);
                Rod(t, new Vector3(x * 1.08f, top + 0.08f, -0.6f), new Vector3(x * 1.08f, top + 0.08f, 0.6f), 0.01f, chrome); // (a side rail)
                foreach (float z in new[] { -0.55f, 0.1f, 0.55f }) Rod(t, new Vector3(x, top, z), new Vector3(x * 1.08f, top + 0.08f, z), 0.008f, chrome);
            }
            foreach (float z in new[] { -0.95f, 0.95f }) Rod(t, new Vector3(-0.3f, top, z), new Vector3(0.3f, top, z), 0.018f, chrome);
            // The scissor legs, a castor at each foot.
            foreach (float x in new[] { -0.26f, 0.26f })
            {
                Rod(t, new Vector3(x, 0.12f, -0.7f), new Vector3(x, top - 0.02f, 0.7f), 0.016f, chrome);
                Rod(t, new Vector3(x, 0.12f, 0.7f), new Vector3(x, top - 0.02f, -0.7f), 0.016f, chrome);
                Sphere(t, new Vector3(x, (0.12f + top) * 0.5f, 0f), 0.025f, metal);
                foreach (float z in new[] { -0.7f, 0.7f })
                {
                    Box(t, new Vector3(x, 0.1f, z), new Vector3(0.03f, 0.05f, 0.05f), metal);
                    var wheel = Shape(PrimitiveType.Cylinder, t, new Vector3(x, 0.05f, z + 0.03f), new Vector3(0.09f, 0.012f, 0.09f), rubber);
                    wheel.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
                }
            }
            Rod(t, new Vector3(-0.26f, 0.12f, -0.7f), new Vector3(0.26f, 0.12f, -0.7f), 0.012f, chrome);
            Rod(t, new Vector3(-0.26f, 0.12f, 0.7f), new Vector3(0.26f, 0.12f, 0.7f), 0.012f, chrome);
            // The mattress: flat, then the head end raised.
            Box(t, new Vector3(0f, top + 0.05f, 0.2f), new Vector3(0.6f, 0.07f, 1.45f), white);
            var head = Box(t, new Vector3(0f, top + 0.2f, -0.72f), new Vector3(0.6f, 0.07f, 0.55f), white);
            head.transform.localRotation = Quaternion.Euler(-28f, 0f, 0f);
            // Someone under the sheet: head, shoulders, chest, hips, legs, feet, and the sheet draped over and down.
            var body = new GameObject("Under the sheet") { layer = Layers.Props }.transform;
            body.SetParent(t, false);
            Squash(body, new Vector3(0f, top + 0.38f, -0.78f), new Vector3(0.2f, 0.22f, 0.24f), sheet);              // head
            Squash(body, new Vector3(0f, top + 0.24f, -0.42f), new Vector3(0.48f, 0.18f, 0.3f), sheet);              // shoulders
            Squash(body, new Vector3(0f, top + 0.19f, -0.1f), new Vector3(0.42f, 0.17f, 0.5f), sheet);               // chest
            Squash(body, new Vector3(0f, top + 0.17f, 0.28f), new Vector3(0.4f, 0.15f, 0.36f), sheet);               // hips
            foreach (float x in new[] { -0.1f, 0.1f })
            {
                Squash(body, new Vector3(x, top + 0.14f, 0.62f), new Vector3(0.16f, 0.12f, 0.5f), sheet);            // legs
                Squash(body, new Vector3(x * 1.2f, top + 0.21f, 0.92f), new Vector3(0.1f, 0.16f, 0.09f), sheet);     // feet
            }
            Box(t, new Vector3(0f, top + 0.12f, 0.15f), new Vector3(0.66f, 0.12f, 1.6f), sheet);
            foreach (float x in new[] { -0.335f, 0.335f })
            {
                var drape = Box(t, new Vector3(x, top - 0.06f, 0.1f), new Vector3(0.012f, 0.3f, 1.55f), sheet);
                drape.transform.localRotation = Quaternion.Euler(0f, 0f, x > 0 ? -6f : 6f);
            }
            // The toe tag.
            Rod(t, new Vector3(0.12f, top + 0.24f, 0.97f), new Vector3(0.13f, top + 0.05f, 1.0f), 0.002f, white);
            var tag = Box(t, new Vector3(0.13f, top + 0.0f, 1.0f), new Vector3(0.05f, 0.09f, 0.003f), Cached("Tag", new Color(0.6f, 0.55f, 0.4f), 0.1f));
            tag.transform.localRotation = Quaternion.Euler(0f, 15f, 6f);
            // The IV pole, its bag hanging empty.
            Vector3 pole = new Vector3(-0.36f, 0f, -1.0f);
            Rod(t, pole + Vector3.up * top, pole + Vector3.up * 1.85f, 0.01f, chrome);
            Rod(t, pole + new Vector3(-0.1f, 1.85f, 0f), pole + new Vector3(0.1f, 1.85f, 0f), 0.006f, chrome);
            var bag = Box(t, pole + new Vector3(0.09f, 1.68f, 0f), new Vector3(0.1f, 0.16f, 0.02f), Cached("IV bag", new Color(0.55f, 0.58f, 0.58f), 0.9f));
            bag.transform.localRotation = Quaternion.Euler(0f, 0f, 4f);
            for (int k = 0; k < 6; k++)
            {
                float u0 = k / 6f, u1 = (k + 1) / 6f;
                Vector3 a = Vector3.Lerp(pole + new Vector3(0.09f, 1.6f, 0f), new Vector3(-0.15f, top + 0.15f, -0.5f), u0) + Vector3.down * Mathf.Sin(u0 * Mathf.PI) * 0.25f;
                Vector3 b = Vector3.Lerp(pole + new Vector3(0.09f, 1.6f, 0f), new Vector3(-0.15f, top + 0.15f, -0.5f), u1) + Vector3.down * Mathf.Sin(u1 * Mathf.PI) * 0.25f;
                Rod(t, a, b, 0.003f, chrome);
            }
        }

        // An old wooden TV set on splayed legs, rabbit ears on top, a bulging screen alive with static (and its
        // hiss), knobs and a speaker grille down one side, its flex trailing off into the leaves to nothing.
        static void Tv(Transform t)
        {
            Vector3 c = new Vector3(0f, 0.62f, 0f);
            Box(t, c, new Vector3(0.72f, 0.52f, 0.5f), wood);
            Box(t, c + new Vector3(0f, 0f, 0.252f), new Vector3(0.7f, 0.5f, 0.006f), black); // (the front panel)
            Box(t, c + new Vector3(-0.08f, 0f, 0.258f), new Vector3(0.5f, 0.4f, 0.01f), wood); // (the bezel)
            var screen = Shape(PrimitiveType.Sphere, t, c + new Vector3(-0.08f, 0f, 0.255f), new Vector3(0.44f, 0.34f, 0.06f), black);
            var mat = RuntimeMaterials.Lit("TV static", Color.black, 0.9f); // (its own: the static moves on it)
            screen.GetComponent<Renderer>().sharedMaterial = mat;
            var noise = screen.AddComponent<TvStatic>();
            noise.screen = mat;
            for (int i = 0; i < 2; i++)
            {
                var knob = Shape(PrimitiveType.Cylinder, t, c + new Vector3(0.27f, 0.12f - i * 0.11f, 0.265f), new Vector3(0.06f, 0.012f, 0.06f), chrome);
                knob.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            }
            for (int i = 0; i < 6; i++) Box(t, c + new Vector3(0.27f, -0.08f - i * 0.022f, 0.258f), new Vector3(0.1f, 0.008f, 0.004f), brass);
            foreach (float x in new[] { -1f, 1f })
            foreach (float z in new[] { -1f, 1f })
                Rod(t, new Vector3(x * 0.3f, 0.36f, z * 0.2f), new Vector3(x * 0.34f, 0f, z * 0.24f), 0.015f, wood);
            // Rabbit ears.
            Squash(t, c + new Vector3(0.05f, 0.29f, -0.05f), new Vector3(0.12f, 0.06f, 0.12f), black);
            Rod(t, c + new Vector3(0.05f, 0.3f, -0.05f), c + new Vector3(-0.2f, 0.82f, -0.12f), 0.004f, chrome);
            Rod(t, c + new Vector3(0.05f, 0.3f, -0.05f), c + new Vector3(0.38f, 0.75f, -0.08f), 0.004f, chrome);
            Sphere(t, c + new Vector3(-0.2f, 0.82f, -0.12f), 0.01f, chrome);
            Sphere(t, c + new Vector3(0.38f, 0.75f, -0.08f), 0.01f, chrome);
            // The flex, off into the leaves.
            Vector3 prev = c + new Vector3(0.2f, -0.2f, -0.25f);
            for (int k = 1; k <= 8; k++)
            {
                float u = k / 8f;
                Vector3 p = new Vector3(0.2f + u * 0.6f + Mathf.Sin(u * 7f) * 0.1f, Mathf.Lerp(0.42f, 0.01f, Mathf.Sqrt(u)), -0.25f - u * 0.9f);
                Rod(t, prev, p, 0.005f, rubber);
                prev = p;
            }
            PhantomSound.Add(t.gameObject, Sound.TvStatic, 0.35f, 18f);
        }

        // A black rotary telephone on a little round side table, ringing: the handset rattling in its cradle with
        // each ring, its coiled cord, a flex trailing off.
        static void Telephone(Transform t)
        {
            // The table.
            Cyl(t, new Vector3(0f, 0.62f, 0f), 0.24f, 0.03f, wood);
            for (int i = 0; i < 3; i++)
            {
                float a = i / 3f * Mathf.PI * 2f;
                Rod(t, new Vector3(Mathf.Cos(a) * 0.14f, 0.61f, Mathf.Sin(a) * 0.14f), new Vector3(Mathf.Cos(a) * 0.2f, 0f, Mathf.Sin(a) * 0.2f), 0.014f, wood);
            }
            Cyl(t, new Vector3(0f, 0.25f, 0f), 0.16f, 0.015f, wood); // (a shelf)
            // The phone: a sloped body, the dial, the cradle and handset.
            Vector3 p = new Vector3(0f, 0.635f, 0f);
            Box(t, p + new Vector3(0f, 0.035f, 0f), new Vector3(0.2f, 0.07f, 0.22f), bakelite);
            var slope = Box(t, p + new Vector3(0f, 0.08f, 0.04f), new Vector3(0.18f, 0.05f, 0.16f), bakelite);
            slope.transform.localRotation = Quaternion.Euler(-22f, 0f, 0f);
            var dial = Shape(PrimitiveType.Cylinder, t, p + new Vector3(0f, 0.105f, 0.075f), new Vector3(0.12f, 0.004f, 0.12f), chrome);
            dial.transform.localRotation = Quaternion.Euler(68f, 0f, 0f);
            for (int i = 0; i < 10; i++)
            {
                float a = (i / 12f + 0.1f) * Mathf.PI * 2f;
                Shape(PrimitiveType.Cylinder, dial.transform, new Vector3(Mathf.Cos(a) * 0.36f, 0.6f, Mathf.Sin(a) * 0.36f), new Vector3(0.16f, 0.5f, 0.16f), black);
            }
            Cyl(dial.transform, new Vector3(0f, 0.7f, 0f), 0.18f, 0.6f, white); // (the number card)
            foreach (float x in new[] { -0.07f, 0.07f }) Box(t, p + new Vector3(x, 0.11f, -0.06f), new Vector3(0.025f, 0.05f, 0.03f), bakelite);
            var handset = new GameObject("Handset").transform;
            handset.SetParent(t, false);
            handset.localPosition = p + new Vector3(0f, 0.145f, -0.06f);
            Capsule(handset, Vector3.zero, new Vector3(0.035f, 0.11f, 0.035f), new Vector3(0f, 0f, 90f), bakelite);
            foreach (float x in new[] { -0.1f, 0.1f }) Squash(handset, new Vector3(x, -0.015f, 0f), new Vector3(0.065f, 0.035f, 0.065f), bakelite);
            // The coiled cord from the handset round to the body, and the flex off the table.
            Vector3 prev = p + new Vector3(-0.1f, 0.12f, -0.07f);
            for (int k = 1; k <= 24; k++)
            {
                float u = k / 24f;
                Vector3 q = Vector3.Lerp(p + new Vector3(-0.1f, 0.12f, -0.07f), p + new Vector3(-0.09f, 0.03f, 0.1f), u)
                          + new Vector3(-0.06f * Mathf.Sin(u * Mathf.PI), 0f, 0f) + new Vector3(Mathf.Cos(u * 40f) * 0.008f, Mathf.Sin(u * 40f) * 0.008f, 0f);
                Rod(t, prev, q, 0.002f, bakelite);
                prev = q;
            }
            prev = p + new Vector3(0.05f, 0.01f, -0.11f);
            for (int k = 1; k <= 7; k++)
            {
                float u = k / 7f;
                Vector3 q = new Vector3(0.05f + u * 0.3f, Mathf.Lerp(0.64f, 0.01f, Mathf.Min(1f, u * 1.6f)), -0.11f - u * 0.6f);
                Rod(t, prev, q, 0.003f, rubber);
                prev = q;
            }
            var sound = PhantomSound.Add(t.gameObject, Sound.PhoneRing, 0.75f, 30f);
            sound.jiggle = handset;
            sound.ringPeriod = 6f;
            sound.ringFor = 2f;
        }

        // A CB radio on an old slatted crate, left on (the hiss, the drifting carrier, the voice that never quite
        // makes words): its dial glowing amber, a whip aerial, the handset lying on its coiled cord.
        static void Radio(Transform t)
        {
            // The crate: slats on corner posts.
            const float w = 0.5f, d = 0.38f, h = 0.4f;
            foreach (float x in new[] { -1f, 1f })
            foreach (float z in new[] { -1f, 1f })
                Box(t, new Vector3(x * (w * 0.5f - 0.02f), h * 0.5f, z * (d * 0.5f - 0.02f)), new Vector3(0.04f, h, 0.04f), crate);
            for (int k = 0; k < 3; k++)
            {
                float y = 0.06f + k * 0.14f;
                foreach (float z in new[] { -1f, 1f }) Box(t, new Vector3(0f, y, z * d * 0.5f), new Vector3(w, 0.09f, 0.015f), crate);
                foreach (float x in new[] { -1f, 1f }) Box(t, new Vector3(x * w * 0.5f, y, 0f), new Vector3(0.015f, 0.09f, d), crate);
            }
            for (int k = 0; k < 4; k++) Box(t, new Vector3(0f, h + 0.008f, Mathf.Lerp(-d * 0.4f, d * 0.4f, k / 3f)), new Vector3(w, 0.016f, 0.085f), crate);
            // The set.
            Vector3 r = new Vector3(0f, h + 0.016f, 0f);
            var steel = Cached("Phantom radio case", new Color(0.12f, 0.13f, 0.12f), 0.4f);
            Box(t, r + new Vector3(0f, 0.08f, 0f), new Vector3(0.36f, 0.15f, 0.26f), steel);
            Box(t, r + new Vector3(0f, 0.08f, 0.132f), new Vector3(0.34f, 0.13f, 0.005f), black);
            var dial = Box(t, r + new Vector3(-0.07f, 0.11f, 0.136f), new Vector3(0.14f, 0.04f, 0.004f), glass);
            dial.GetComponent<Renderer>().sharedMaterial = dialGlow ??= RuntimeMaterials.LitGlow("Phantom radio dial", new Color(0.3f, 0.2f, 0.08f), new Color(1f, 0.55f, 0.15f) * 0.9f);
            Box(t, r + new Vector3(0.09f, 0.11f, 0.136f), new Vector3(0.06f, 0.035f, 0.004f), Cached("Phantom meter", new Color(0.6f, 0.55f, 0.4f), 0.5f));
            for (int i = 0; i < 4; i++)
            {
                var knob = Shape(PrimitiveType.Cylinder, t, r + new Vector3(-0.12f + i * 0.07f, 0.045f, 0.145f), new Vector3(0.028f, 0.01f, 0.028f), metal);
                knob.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            }
            for (int i = 0; i < 5; i++) Box(t, r + new Vector3(0.14f, 0.045f + i * 0.012f, 0.136f), new Vector3(0.05f, 0.004f, 0.004f), metal);
            // The aerial, and the handset on its cord lying by it.
            Rod(t, r + new Vector3(0.15f, 0.15f, -0.1f), r + new Vector3(0.2f, 1.35f, -0.16f), 0.004f, chrome);
            Sphere(t, r + new Vector3(0.2f, 1.35f, -0.16f), 0.008f, chrome);
            var mic = Box(t, r + new Vector3(-0.15f, 0.025f, 0.2f), new Vector3(0.06f, 0.035f, 0.09f), steel);
            mic.transform.localRotation = Quaternion.Euler(0f, 30f, 0f);
            Vector3 prev = r + new Vector3(-0.12f, 0.03f, 0.15f);
            for (int k = 1; k <= 16; k++)
            {
                float u = k / 16f;
                Vector3 q = Vector3.Lerp(r + new Vector3(-0.12f, 0.03f, 0.15f), r + new Vector3(-0.17f, 0.06f, 0.0f), u) + new Vector3(Mathf.Cos(u * 30f) * 0.01f, 0.01f + Mathf.Sin(u * 30f) * 0.01f, 0f);
                Rod(t, prev, q, 0.003f, rubber);
                prev = q;
            }
            PhantomSound.Add(t.gameObject, Sound.RadioCreep, 0.6f, 22f);
        }

        // A child's tricycle, red paint chipped: a big front wheel with spokes and pedals, two small back wheels on
        // an axle, the frame, a saddle, handlebars with white grips and streamers, a bell.
        static void Tricycle(Transform t)
        {
            Vector3 front = new Vector3(0f, 0.22f, 0.3f);
            Wheel(t, front, 0.22f, 12);
            for (int s = -1; s <= 1; s += 2)
            {
                Rod(t, front + new Vector3(s * 0.04f, 0f, 0f), front + new Vector3(s * 0.04f, 0.33f, -0.04f), 0.012f, red); // (the fork)
                Rod(t, front + new Vector3(s * 0.04f, 0f, 0f), front + new Vector3(s * 0.08f, 0.03f, 0f), 0.008f, chrome); // (crank)
                Box(t, front + new Vector3(s * 0.1f, 0.03f, 0f), new Vector3(0.05f, 0.012f, 0.08f), rubber);            // (pedal)
            }
            Vector3 head = front + new Vector3(0f, 0.36f, -0.05f);
            Rod(t, head, head + new Vector3(0f, 0.14f, -0.03f), 0.014f, chrome);
            Vector3 bar = head + new Vector3(0f, 0.14f, -0.03f);
            Rod(t, bar + Vector3.left * 0.2f, bar + Vector3.right * 0.2f, 0.01f, chrome);
            foreach (float x in new[] { -1f, 1f })
            {
                Rod(t, bar + Vector3.right * x * 0.16f, bar + Vector3.right * x * 0.24f, 0.016f, white);
                for (int k = 0; k < 3; k++) // (streamers)
                {
                    var str = Box(t, bar + Vector3.right * x * 0.245f + new Vector3(x * 0.02f, -0.07f, k * 0.008f - 0.008f), new Vector3(0.006f, 0.14f, 0.003f), k == 1 ? white : red);
                    str.transform.localRotation = Quaternion.Euler(10f * k - 10f, 0f, x * (12f + 6f * k));
                }
            }
            Cyl(t, bar + new Vector3(0.1f, 0.03f, 0f), 0.025f, 0.02f, chrome); // (the bell)
            // The frame back to the axle, the saddle on its post.
            Vector3 axle = new Vector3(0f, 0.13f, -0.25f);
            Rod(t, head + Vector3.down * 0.05f, new Vector3(0f, 0.3f, -0.15f), 0.016f, red);
            Rod(t, new Vector3(0f, 0.3f, -0.15f), axle, 0.016f, red);
            Rod(t, axle + Vector3.left * 0.24f, axle + Vector3.right * 0.24f, 0.012f, red);
            Box(t, axle + new Vector3(0f, 0.04f, -0.04f), new Vector3(0.3f, 0.015f, 0.08f), chrome); // (the step)
            foreach (float x in new[] { -0.24f, 0.24f }) Wheel(t, axle + Vector3.right * x, 0.13f, 8);
            Rod(t, new Vector3(0f, 0.3f, -0.15f), new Vector3(0f, 0.43f, -0.17f), 0.012f, chrome);
            Squash(t, new Vector3(0f, 0.45f, -0.16f), new Vector3(0.14f, 0.04f, 0.18f), black);
            Box(t, new Vector3(0.012f, 0.33f, -0.12f), new Vector3(0.02f, 0.03f, 0.02f), metal); // (a chip of paint gone)
        }

        static void Wheel(Transform t, Vector3 centre, float radius, int spokes)
        {
            const int seg = 16;
            for (int i = 0; i < seg; i++)
            {
                float a0 = i / (float)seg * Mathf.PI * 2f, a1 = (i + 1) / (float)seg * Mathf.PI * 2f;
                Rod(t, centre + new Vector3(0f, Mathf.Sin(a0), Mathf.Cos(a0)) * radius, centre + new Vector3(0f, Mathf.Sin(a1), Mathf.Cos(a1)) * radius, radius * 0.12f, rubber);
            }
            for (int i = 0; i < spokes; i++)
            {
                float a = i / (float)spokes * Mathf.PI * 2f;
                Rod(t, centre, centre + new Vector3(0f, Mathf.Sin(a), Mathf.Cos(a)) * radius * 0.9f, 0.003f, chrome);
            }
            var hub = Shape(PrimitiveType.Cylinder, t, centre, new Vector3(radius * 0.25f, 0.03f, radius * 0.25f), red);
            hub.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
        }

        static void Octagon(Transform parent, float radius, float depth, Material m, float z)
        {
            var b = new MeshBuilder();
            int c = b.AddVertex(Vector3.zero, new Vector2(0.5f, 0.5f));
            for (int i = 0; i < 8; i++)
            {
                float a = (i + 0.5f) / 8f * Mathf.PI * 2f;
                b.AddVertex(new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f) * radius, Vector2.zero);
            }
            for (int i = 0; i < 8; i++) b.AddTriangle(c, 1 + (i + 1) % 8, 1 + i);
            for (int i = 0; i < 8; i++) b.AddTriangle(c, 1 + i, 1 + (i + 1) % 8); // (both faces)
            var go = new GameObject("Octagon") { layer = Layers.Props };
            go.transform.SetParent(parent, false);
            go.transform.localPosition = new Vector3(0f, 0f, z);
            go.transform.localScale = new Vector3(1f, 1f, Mathf.Max(0.001f, depth));
            var mesh = b.ToMesh("Octagon");
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshCleanup>().Mesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = m;
        }

        static GameObject Box(Transform p, Vector3 at, Vector3 size, Material m) => Shape(PrimitiveType.Cube, p, at, size, m);
        static GameObject Sphere(Transform p, Vector3 at, float radius, Material m) => Shape(PrimitiveType.Sphere, p, at, Vector3.one * radius * 2f, m);
        static GameObject Squash(Transform p, Vector3 at, Vector3 size, Material m) => Shape(PrimitiveType.Sphere, p, at, size, m);
        static GameObject Cyl(Transform p, Vector3 at, float radius, float height, Material m) => Shape(PrimitiveType.Cylinder, p, at, new Vector3(radius * 2f, height * 0.5f, radius * 2f), m);

        static GameObject Capsule(Transform p, Vector3 at, Vector3 size, Vector3 euler, Material m)
        {
            var go = Shape(PrimitiveType.Capsule, p, at, size, m);
            go.transform.localRotation = Quaternion.Euler(euler);
            return go;
        }

        static void Rod(Transform p, Vector3 a, Vector3 b, float radius, Material m)
        {
            Vector3 d = b - a;
            if (d.sqrMagnitude < 1e-8f) return;
            var go = Shape(PrimitiveType.Cylinder, p, (a + b) * 0.5f, new Vector3(radius * 2f, d.magnitude * 0.5f, radius * 2f), m);
            go.transform.localRotation = Quaternion.FromToRotation(Vector3.up, d);
        }

        static GameObject Shape(PrimitiveType type, Transform parent, Vector3 at, Vector3 scale, Material m)
        {
            var go = GameObject.CreatePrimitive(type);
            Object.Destroy(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false);
            go.transform.localPosition = at;
            go.transform.localScale = scale;
            go.GetComponent<Renderer>().sharedMaterial = m;
            return go;
        }
    }

    /// <summary>
    /// A phantom's sound (the TV's static, the phone ringing, the radio): a loop from it that fades in as it
    /// appears and fades out when it goes (the thing gone at once, its sound lingering away). Optionally rattles a
    /// part (the phone's handset) while it rings.
    /// </summary>
    public sealed class PhantomSound : MonoBehaviour
    {
        public Sound sound;
        public float volume = 0.5f, range = 20f;
        public float fadeIn = 1.5f, fadeOut = 1.8f;
        [Tooltip("A part that rattles while it rings (in time with the loop: the first ringFor seconds of each ringPeriod).")]
        public Transform jiggle;
        public float ringPeriod, ringFor;

        AudioSource source;
        float fade, target = 1f;
        Quaternion jiggleRest;
        Vector3 jigglePos;

        public static PhantomSound Add(GameObject on, Sound sound, float volume, float range)
        {
            var p = on.AddComponent<PhantomSound>();
            p.sound = sound;
            p.volume = volume;
            p.range = range;
            return p;
        }

        /// <summary>The phantom goes: hidden at once, its sound fading away after. False if it has no sound.</summary>
        public static bool Vanish(GameObject phantom)
        {
            var p = phantom != null ? phantom.GetComponent<PhantomSound>() : null;
            if (p == null) return false;
            foreach (var r in phantom.GetComponentsInChildren<Renderer>()) r.enabled = false;
            foreach (var l in phantom.GetComponentsInChildren<Light>()) l.enabled = false;
            p.target = 0f;
            return true;
        }

        void Start()
        {
            if (jiggle != null)
            {
                jiggleRest = jiggle.localRotation;
                jigglePos = jiggle.localPosition;
            }
            if (!Services.TryGet<SoundPlayer>(out var sounds)) return;
            source = gameObject.AddComponent<AudioSource>();
            source.clip = sounds.Clip(sound);
            source.loop = true;
            source.spatialBlend = 1f;
            source.rolloffMode = AudioRolloffMode.Logarithmic;
            source.minDistance = 1.5f;
            source.maxDistance = range;
            source.dopplerLevel = 0f;
            source.volume = 0f;
            source.outputAudioMixerGroup = sounds.Group(MixerNames.World);
            Muffled.On(source);
            if (source.clip != null)
            {
                // (the phone starts on a ring; anything else part way through)
                source.time = ringPeriod > 0f ? 0f : Random.Range(0f, source.clip.length * 0.9f);
                source.Play();
            }
        }

        void Update()
        {
            float dt = Time.deltaTime;
            fade = Mathf.MoveTowards(fade, target, dt / Mathf.Max(0.05f, target > fade ? fadeIn : fadeOut));
            if (source != null) source.volume = volume * fade * fade * (3f - 2f * fade);
            if (target <= 0f && fade <= 0f)
            {
                Destroy(gameObject);
                return;
            }
            if (jiggle != null && source != null && ringPeriod > 0f)
            {
                bool ringing = source.time % ringPeriod < ringFor;
                float k = ringing ? 1f : 0f;
                jiggle.localRotation = jiggleRest * Quaternion.Euler(Mathf.Sin(Time.time * 125f) * 2.5f * k, 0f, Mathf.Sin(Time.time * 97f) * 1.5f * k);
                jiggle.localPosition = jigglePos + Vector3.up * Mathf.Abs(Mathf.Sin(Time.time * 125f)) * 0.004f * k;
            }
        }
    }

    /// <summary>
    /// A TV screen showing static: snow that never holds still (a noise texture glowing on the screen, jumped
    /// about every frame, a dark band rolling slowly down it), and a flickering grey-blue light thrown off it.
    /// </summary>
    public sealed class TvStatic : MonoBehaviour
    {
        public Material screen;
        static Texture2D snow;
        Light glow;
        float next;

        void Start()
        {
            if (snow == null) snow = Snow();
            if (screen != null)
            {
                screen.SetTexture("_EmissiveColorMap", snow);
                HDMaterial.SetEmissiveColor(screen, new Color(0.75f, 0.8f, 0.85f) * 1.6f);
                HDMaterial.ValidateMaterial(screen);
            }
            var go = new GameObject("TV glow");
            go.transform.SetParent(transform, false);
            go.transform.localPosition = new Vector3(0f, 0f, 3f); // (out in front of the screen, in its squashed space)
            glow = go.AddHDLight(LightType.Point).GetComponent<Light>();
            glow.color = new Color(0.7f, 0.78f, 0.9f);
            glow.range = 4f;
            glow.shadows = LightShadows.None;
        }

        void OnDestroy()
        {
            if (screen != null) Destroy(screen);
        }

        void Update()
        {
            if (screen == null || Time.time < next) return;
            next = Time.time + 1f / 30f;
            screen.SetTextureOffset("_EmissiveColorMap", new Vector2(Random.value, Random.value));
            screen.SetTextureScale("_EmissiveColorMap", new Vector2(0.5f, 0.5f));
            if (glow != null) glow.intensity = Random.Range(0.25f, 0.45f);
        }

        static Texture2D Snow()
        {
            const int w = 128, h = 128;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { name = "TV snow", wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Point };
            var px = new Color[w * h];
            var rng = new System.Random(5);
            for (int y = 0; y < h; y++)
            {
                float band = 0.75f + 0.25f * Mathf.Sin(y / (float)h * Mathf.PI * 2f);
                for (int x = 0; x < w; x++)
                {
                    float v = (float)rng.NextDouble();
                    v = v * v * band;
                    px[y * w + x] = new Color(v, v, v * 1.05f, 1f);
                }
            }
            tex.SetPixels(px);
            tex.Apply(false);
            return tex;
        }
    }
}
