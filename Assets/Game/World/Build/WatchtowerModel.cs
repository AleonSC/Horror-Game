using System.Collections.Generic;
using Game.Core;
using Game.Rendering;
using UnityEngine;

namespace Game.World.Build
{
    /// <summary>
    /// An old fire lookout tower (section 6.5), 12 m to its deck: four timber legs splayed out to concrete footings,
    /// tied with girts and X-braced on every face, bolted at the joints; a planked deck on joists; a lookout cab with
    /// plank walls to waist height and windows all round above (old glass, some panes cracked or gone), corner
    /// posts, a hipped tin roof with a finial, a lightning rod and an aerial; a floodlight on a bracket at each
    /// corner under the eaves, with their conduit down to a switch box by the door (the button); inside, a round
    /// fire-finder table with its sighting ring, a stool, and a lantern hanging from the roof. The ladder goes up
    /// the west side through a hooped safety cage, its rails running up past the deck to grab. What you can stand
    /// on and bump into has simple colliders; the details have none.
    /// </summary>
    public sealed class WatchtowerModel
    {
        public const float Deck = 12f;                     // the deck's underside (its top is 12.125)
        const float Leg = 2.45f, LegTop = 1.85f;           // the legs' corners, at the ground and under the deck
        const float Half = 2.15f;                          // the deck's half-width
        const float WallTop = Deck + 1.15f, Eaves = Deck + 2.45f, Apex = Deck + 3.7f;
        public const float LadderX = -2.75f;               // the ladder's rails (west of the tower)

        readonly List<Object> made;
        readonly System.Random rng;
        readonly Material timber, darkTimber, plank, concrete, metal, tin, glass, brass, rope;

        public WatchtowerModel(List<Object> made, System.Random rng)
        {
            this.made = made;
            this.rng = rng;
            timber = Keep(RuntimeMaterials.Lit("Tower timber", new Color(0.2f, 0.16f, 0.12f), 0.15f));
            darkTimber = Keep(RuntimeMaterials.Lit("Tower dark timber", new Color(0.11f, 0.085f, 0.065f), 0.18f));
            plank = Keep(RuntimeMaterials.Lit("Tower planks", new Color(0.24f, 0.2f, 0.15f), 0.12f));
            concrete = Keep(RuntimeMaterials.Lit("Footing concrete", new Color(0.3f, 0.29f, 0.27f), 0.1f));
            metal = Keep(RuntimeMaterials.Lit("Tower iron", new Color(0.13f, 0.12f, 0.12f), 0.45f));
            metal.SetFloat("_Metallic", 0.6f);
            tin = Keep(RuntimeMaterials.Lit("Tower roof tin", new Color(0.22f, 0.14f, 0.09f), 0.35f, true));
            tin.SetFloat("_Metallic", 0.4f);
            glass = Keep(RuntimeMaterials.Glass("Tower glass", new Color(0.32f, 0.36f, 0.38f, 0.22f)));
            brass = Keep(RuntimeMaterials.Lit("Tower brass", new Color(0.45f, 0.33f, 0.14f), 0.6f));
            brass.SetFloat("_Metallic", 0.8f);
            rope = Keep(RuntimeMaterials.Lit("Tower cable", new Color(0.05f, 0.05f, 0.05f), 0.3f));
        }

        Material Keep(Material m)
        {
            made.Add(m);
            return m;
        }

        float R(float a, float b) => a + (b - a) * (float)rng.NextDouble();

        // A leg's corner at a height (they lean in from the footings to under the deck).
        static Vector3 LegAt(int sx, int sz, float y)
        {
            float k = Mathf.Lerp(Leg, LegTop, Mathf.Clamp01(y / Deck));
            return new Vector3(sx * k, y, sz * k);
        }

        public void Build(Transform t)
        {
            Frame(t);
            DeckAndCab(t);
            Roof(t);
            Floodlights(t);
            Inside(t);
            Ladder(t);
        }

        // ------------------------------------------------------------------ the frame

        void Frame(Transform t)
        {
            int[] s = { -1, 1 };
            foreach (int sx in s)
            foreach (int sz in s)
            {
                // A concrete footing, half sunk, with an iron shoe and bolts holding the leg.
                Box(t, new Vector3(sx * Leg, -0.3f, sz * Leg), new Vector3(0.75f, 1.4f, 0.75f), concrete, true); // (deep, for slopes)
                Box(t, new Vector3(sx * Leg, 0.45f, sz * Leg), new Vector3(0.36f, 0.3f, 0.36f), metal, false);
                for (int b = 0; b < 4; b++)
                    Cyl(t, new Vector3(sx * Leg + (b % 2 == 0 ? -0.22f : 0.22f), 0.42f, sz * Leg + (b < 2 ? -0.22f : 0.22f)), 0.025f, 0.06f, metal);
                Beam(t, LegAt(sx, sz, 0.3f), LegAt(sx, sz, Deck), 0.24f, 0.24f, timber, true);
            }
            // Girts round each level, and X-braces between them on every face.
            float[] levels = { 0.6f, 3.4f, 6.3f, 9.2f, Deck - 0.15f };
            for (int i = 0; i < levels.Length; i++)
            {
                float y = levels[i];
                for (int face = 0; face < 4; face++)
                {
                    var (a, b) = FaceCorners(face, y);
                    Beam(t, a, b, 0.14f, 0.2f, timber, false);
                    if (i == 0) continue;
                    var (a0, b0) = FaceCorners(face, levels[i - 1] + 0.1f);
                    var (a1, b1) = FaceCorners(face, y - 0.1f);
                    Beam(t, a0, b1, 0.08f, 0.16f, darkTimber, false);
                    Beam(t, b0, a1, 0.08f, 0.16f, darkTimber, false);
                    // (bolt plates where they cross)
                    Box(t, (a0 + b1 + b0 + a1) * 0.25f + Outward(face) * 0.07f, new Vector3(0.2f, 0.2f, 0.2f) * 0.5f + Vector3.Scale(Abs(Outward(face)), new Vector3(-0.08f, 0f, -0.08f)), metal, false);
                }
            }
        }

        static (Vector3, Vector3) FaceCorners(int face, float y) => face switch
        {
            0 => (LegAt(-1, -1, y), LegAt(1, -1, y)),   // south
            1 => (LegAt(1, -1, y), LegAt(1, 1, y)),     // east
            2 => (LegAt(1, 1, y), LegAt(-1, 1, y)),     // north
            _ => (LegAt(-1, 1, y), LegAt(-1, -1, y)),   // west
        };

        static Vector3 Outward(int face) => face switch { 0 => Vector3.back, 1 => Vector3.right, 2 => Vector3.forward, _ => Vector3.left };
        static Vector3 Abs(Vector3 v) => new Vector3(Mathf.Abs(v.x), Mathf.Abs(v.y), Mathf.Abs(v.z));

        // ------------------------------------------------------------------ the deck and the cab

        void DeckAndCab(Transform t)
        {
            // Joists under the deck, then planks across them (a few warped or short), and one solid floor to stand on.
            for (int i = 0; i < 5; i++)
                Box(t, new Vector3(Mathf.Lerp(-Half + 0.15f, Half - 0.15f, i / 4f), Deck - 0.1f, 0f), new Vector3(0.12f, 0.2f, Half * 2f), darkTimber, false);
            int planks = 15;
            for (int i = 0; i < planks; i++)
            {
                float z = Mathf.Lerp(-Half + 0.14f, Half - 0.14f, i / (planks - 1f));
                var p = Box(t, new Vector3(R(-0.02f, 0.02f), Deck + 0.06f + R(-0.008f, 0.008f), z), new Vector3(Half * 2f, 0.05f, 0.27f), plank, false);
                p.transform.localRotation = Quaternion.Euler(0f, 0f, R(-0.6f, 0.6f));
            }
            var floor = Box(t, new Vector3(0f, Deck + 0.06f, 0f), new Vector3(Half * 2f, 0.13f, Half * 2f), plank, true);
            floor.GetComponent<Renderer>().enabled = false;

            // Corner posts to the eaves.
            foreach (int sx in new[] { -1, 1 })
            foreach (int sz in new[] { -1, 1 })
                Box(t, new Vector3(sx * (Half - 0.07f), (Deck + Eaves) * 0.5f, sz * (Half - 0.07f)), new Vector3(0.15f, Eaves - Deck, 0.15f), timber, false);

            // The walls: boards to waist height (the west wall with the gap the ladder comes up through), a sill,
            // windows above in three bays a side, the head rail over them.
            for (int side = 0; side < 4; side++)
            {
                Vector3 along = side % 2 == 0 ? Vector3.right : Vector3.forward;
                Vector3 outward = side switch { 0 => Vector3.back, 1 => Vector3.right, 2 => Vector3.forward, _ => Vector3.left };
                Vector3 mid = outward * (Half - 0.04f);
                bool ladderSide = side == 3;
                // Boards, standing, each its own shade, the gap left on the ladder side.
                int boards = 18;
                for (int i = 0; i < boards; i++)
                {
                    float u = Mathf.Lerp(-Half + 0.12f, Half - 0.12f, i / (boards - 1f));
                    if (ladderSide && Mathf.Abs(u) < 0.5f) continue;
                    var b = Box(t, mid + along * u + Vector3.up * (Deck + 0.13f + (WallTop - Deck - 0.13f) * 0.5f),
                        Vector3.Scale(along, new Vector3(0.22f, 0f, 0.22f)) + Vector3.Scale(Abs(outward), new Vector3(0.035f, 0f, 0.035f)) + Vector3.up * (WallTop - Deck - 0.13f + R(-0.04f, 0.02f)),
                        rng.Next(4) == 0 ? darkTimber : plank, false);
                }
                // What stops you falling (or climbing out of a window): a solid wall to the eaves each side of the
                // gap, or all along.
                float wy = (Deck + Eaves) * 0.5f, wh = Eaves - Deck;
                if (ladderSide)
                    for (int s = -1; s <= 1; s += 2)
                    {
                        var w = Box(t, mid + along * s * (Half + 0.5f) * 0.5f + Vector3.up * wy, Vector3.Scale(along, Vector3.one * (Half - 0.5f)) + Vector3.Scale(Abs(outward), Vector3.one * 0.08f) + Vector3.up * wh, plank, true);
                        w.GetComponent<Renderer>().enabled = false;
                    }
                else
                {
                    var w = Box(t, mid + Vector3.up * wy, Vector3.Scale(along, Vector3.one * Half * 2f) + Vector3.Scale(Abs(outward), Vector3.one * 0.08f) + Vector3.up * wh, plank, true);
                    w.GetComponent<Renderer>().enabled = false;
                }
                // The sill and the head rail.
                Box(t, mid + outward * 0.04f + Vector3.up * (WallTop + 0.02f), Vector3.Scale(along, Vector3.one * Half * 2f) + Vector3.Scale(Abs(outward), Vector3.one * 0.14f) + Vector3.up * 0.05f, darkTimber, false);
                Box(t, mid + Vector3.up * (Eaves - 0.06f), Vector3.Scale(along, Vector3.one * Half * 2f) + Vector3.Scale(Abs(outward), Vector3.one * 0.12f) + Vector3.up * 0.12f, timber, false);
                // Three bays of windows, two mullions; old glass, a pane cracked or missing here and there.
                for (int bay = 0; bay < 3; bay++)
                {
                    float u0 = Mathf.Lerp(-Half, Half, bay / 3f), u1 = Mathf.Lerp(-Half, Half, (bay + 1) / 3f);
                    if (bay > 0) Box(t, mid + along * u0 + Vector3.up * ((WallTop + Eaves) * 0.5f), Vector3.Scale(along, Vector3.one * 0.06f) + Vector3.Scale(Abs(outward), Vector3.one * 0.08f) + Vector3.up * (Eaves - WallTop), timber, false);
                    if (ladderSide && bay == 1) continue; // (the doorway: no glass over the gap)
                    if (rng.Next(7) == 0) continue;       // (gone)
                    float uc = (u0 + u1) * 0.5f;
                    // A cross-bar splitting it into an upper and lower pane.
                    Box(t, mid + along * uc + Vector3.up * ((WallTop + Eaves) * 0.5f + 0.05f), Vector3.Scale(along, Vector3.one * (u1 - u0 - 0.06f)) + Vector3.Scale(Abs(outward), Vector3.one * 0.05f) + Vector3.up * 0.04f, timber, false);
                    var pane = Box(t, mid + along * uc + Vector3.up * ((WallTop + Eaves) * 0.5f), Vector3.Scale(along, Vector3.one * (u1 - u0 - 0.08f)) + Vector3.Scale(Abs(outward), Vector3.one * 0.01f) + Vector3.up * (Eaves - WallTop - 0.14f), glass, false);
                    pane.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    if (rng.Next(5) == 0) // (a crack across it)
                    {
                        var crack = Box(t, mid + outward * 0.008f + along * (uc + R(-0.2f, 0.2f)) + Vector3.up * ((WallTop + Eaves) * 0.5f + R(-0.2f, 0.2f)),
                            Vector3.Scale(along, Vector3.one * R(0.25f, 0.5f)) + Vector3.Scale(Abs(outward), Vector3.one * 0.003f) + Vector3.up * 0.008f, rope, false);
                        crack.transform.localRotation = Quaternion.AngleAxis(R(-40f, 40f), outward);
                    }
                }
            }
            // A plank over the gap where the ladder comes up, to step onto.
            Box(t, new Vector3(-Half - 0.25f, Deck + 0.06f, 0f), new Vector3(0.5f, 0.06f, 1.0f), plank, true);
        }

        // ------------------------------------------------------------------ the roof

        void Roof(Transform t)
        {
            const float over = 0.55f;
            float e = Half + over;
            var m = new MeshBuilder();
            Vector3 apex = new Vector3(0f, Apex, 0f);
            Vector3[] c = { new Vector3(-e, Eaves, -e), new Vector3(e, Eaves, -e), new Vector3(e, Eaves, e), new Vector3(-e, Eaves, e) };
            for (int i = 0; i < 4; i++)
            {
                // Each slope in strips (corrugations: ridges and troughs running down it).
                Vector3 a = c[i], b = c[(i + 1) % 4];
                const int strips = 16;
                for (int k = 0; k < strips; k++)
                {
                    float u0 = k / (float)strips, u1 = (k + 1) / (float)strips;
                    float lift0 = (k % 2 == 0) ? 0.03f : 0f, lift1 = (k % 2 == 0) ? 0f : 0.03f;
                    Vector3 p0 = Vector3.Lerp(a, b, u0) + Vector3.up * lift0, p1 = Vector3.Lerp(a, b, u1) + Vector3.up * lift1;
                    // (strips narrowing to the apex: a triangle face)
                    Vector3 q0 = Vector3.Lerp(apex, Vector3.Lerp(a, b, u0), 0.02f), q1 = Vector3.Lerp(apex, Vector3.Lerp(a, b, u1), 0.02f);
                    int v0 = m.AddVertex(p0, new Vector2(u0, 0f)), v1 = m.AddVertex(p1, new Vector2(u1, 0f));
                    int v2 = m.AddVertex(q1, new Vector2(u1, 1f)), v3 = m.AddVertex(q0, new Vector2(u0, 1f));
                    m.AddTriangle(v0, v2, v1);
                    m.AddTriangle(v0, v3, v2);
                }
                // The hip's ridge cap down to the corner, and the fascia board along the eave.
                Rod(t, apex + Vector3.up * 0.03f, a + Vector3.up * 0.04f, 0.045f, tin);
                Beam(t, a + Vector3.down * 0.08f, b + Vector3.down * 0.08f, 0.04f, 0.16f, darkTimber, false);
            }
            var roof = m.ToMesh("Tower roof");
            made.Add(roof);
            var go = new GameObject("Roof") { layer = Layers.Props };
            go.transform.SetParent(t, false);
            go.AddComponent<MeshFilter>().sharedMesh = roof;
            go.AddComponent<MeshRenderer>().sharedMaterial = tin;
            var lid = Box(t, new Vector3(0f, Eaves + 0.2f, 0f), new Vector3(Half * 2f, 0.3f, Half * 2f), tin, true); // (so you can't jump out through it)
            lid.GetComponent<Renderer>().enabled = false;
            // Rafters under it, a finial on top, a lightning rod and an aerial with its wire.
            for (int i = 0; i < 4; i++) Beam(t, new Vector3(c[i].x * 0.92f, Eaves - 0.05f, c[i].z * 0.92f), apex + Vector3.down * 0.2f, 0.08f, 0.12f, timber, false);
            Cyl(t, apex + Vector3.up * 0.12f, 0.07f, 0.22f, metal);
            Sphere(t, apex + Vector3.up * 0.27f, 0.09f, metal);
            Rod(t, apex + Vector3.up * 0.3f, apex + Vector3.up * 1.7f, 0.012f, brass);
            Vector3 mast = new Vector3(c[2].x * 0.75f, Eaves + 0.25f * (Apex - Eaves) + 0.3f, c[2].z * 0.75f); // (on the hip, three quarters out)
            Rod(t, mast + Vector3.down * 0.3f, mast + Vector3.up * 2.4f, 0.02f, metal);
            for (int i = 0; i < 3; i++) Rod(t, mast + Vector3.up * (1.2f + i * 0.4f) + Vector3.left * 0.35f, mast + Vector3.up * (1.2f + i * 0.4f) + Vector3.right * 0.35f, 0.008f, metal);
            Rod(t, mast + Vector3.up * 2.3f, c[3] + Vector3.up * 0.1f, 0.004f, rope);
        }

        // ------------------------------------------------------------------ floodlights

        // A floodlight on a bracket at each corner under the eaves (named "Floodlight": the light shines from there),
        // their conduit running along the head rail to the switch box by the doorway (the "Button").
        void Floodlights(Transform t)
        {
            foreach (int sx in new[] { -1, 1 })
            foreach (int sz in new[] { -1, 1 })
            {
                Vector3 corner = new Vector3(sx * (Half + 0.05f), Eaves - 0.35f, sz * (Half + 0.05f));
                Vector3 outward = new Vector3(sx, 0f, sz).normalized;
                Vector3 at = corner + outward * 0.35f;
                Beam(t, corner, at + Vector3.up * 0.1f, 0.05f, 0.05f, metal, false);
                var lamp = new GameObject("Floodlight") { layer = Layers.Props }.transform;
                lamp.SetParent(t, false);
                lamp.localPosition = at;
                lamp.localRotation = Quaternion.LookRotation(outward + Vector3.down * 0.55f);
                // (all of it behind the light, which shines from the lens)
                var housing = Shape(PrimitiveType.Cylinder, lamp, new Vector3(0f, 0f, -0.15f), new Vector3(0.34f, 0.12f, 0.34f), metal);
                housing.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                var lens = Shape(PrimitiveType.Cylinder, lamp, new Vector3(0f, 0f, -0.025f), new Vector3(0.3f, 0.005f, 0.3f), glass);
                lens.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                lens.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                for (int f = 0; f < 4; f++) // (cooling fins)
                {
                    Shape(PrimitiveType.Cube, lamp, new Vector3(0f, 0f, -0.3f - f * 0.03f), new Vector3(0.36f, 0.36f, 0.008f), metal);
                }
                Rod(t, at + Vector3.up * 0.15f, new Vector3(sx * (Half - 0.1f), Eaves - 0.15f, sz * (Half - 0.1f)), 0.012f, rope);
            }
            // The conduit along the head rail to the switch box.
            Rod(t, new Vector3(-Half + 0.1f, Eaves - 0.15f, -Half + 0.1f), new Vector3(-Half + 0.1f, Eaves - 0.15f, Half - 0.1f), 0.015f, metal);
            Rod(t, new Vector3(Half - 0.1f, Eaves - 0.15f, -Half + 0.1f), new Vector3(-Half + 0.1f, Eaves - 0.15f, -Half + 0.1f), 0.015f, metal);
            Rod(t, new Vector3(Half - 0.1f, Eaves - 0.15f, Half - 0.1f), new Vector3(-Half + 0.1f, Eaves - 0.15f, Half - 0.1f), 0.015f, metal);
            Vector3 box = new Vector3(-Half + 0.12f, Deck + 1.3f, 0.75f);
            Rod(t, new Vector3(-Half + 0.1f, Eaves - 0.15f, 0.75f), box + Vector3.up * 0.2f, 0.015f, metal);
            Box(t, box, new Vector3(0.12f, 0.36f, 0.28f), metal, false);
            Box(t, box + new Vector3(0.065f, 0.12f, 0f), new Vector3(0.01f, 0.06f, 0.2f), brass, false); // (its name plate)
            var button = new GameObject("Button") { layer = Layers.Props }.transform;
            button.SetParent(t, false);
            button.localPosition = box + new Vector3(0.1f, -0.05f, 0f);
        }

        // ------------------------------------------------------------------ inside

        void Inside(Transform t)
        {
            // The fire finder: a round table on a post, the map disc on it under a brass sighting ring.
            Vector3 table = new Vector3(0.1f, Deck + 0.125f, -0.1f);
            Cyl(t, table + Vector3.up * 0.48f, 0.08f, 0.96f, darkTimber);
            Cyl(t, table + Vector3.up * 0.03f, 0.3f, 0.06f, darkTimber);
            Cyl(t, table + Vector3.up * 0.98f, 0.5f, 0.05f, plank);
            Cyl(t, table + Vector3.up * 1.01f, 0.42f, 0.01f, Keep(RuntimeMaterials.Lit("Map paper", new Color(0.5f, 0.47f, 0.38f), 0.1f)));
            for (int i = 0; i < 16; i++)
            {
                float a0 = i / 16f * Mathf.PI * 2f, a1 = (i + 1) / 16f * Mathf.PI * 2f;
                Rod(t, table + new Vector3(Mathf.Cos(a0) * 0.45f, 1.05f, Mathf.Sin(a0) * 0.45f), table + new Vector3(Mathf.Cos(a1) * 0.45f, 1.05f, Mathf.Sin(a1) * 0.45f), 0.012f, brass);
            }
            Rod(t, table + new Vector3(-0.45f, 1.06f, 0f), table + new Vector3(-0.45f, 1.2f, 0f), 0.006f, brass); // (the sights)
            Rod(t, table + new Vector3(0.45f, 1.06f, 0f), table + new Vector3(0.45f, 1.16f, 0f), 0.006f, brass);
            Box(t, table + Vector3.up * 0.5f, new Vector3(0.8f, 1f, 0.8f), plank, true).GetComponent<Renderer>().enabled = false;
            // A stool by it.
            Vector3 stool = new Vector3(0.95f, Deck + 0.125f, -0.75f);
            Cyl(t, stool + Vector3.up * 0.62f, 0.2f, 0.05f, plank);
            for (int i = 0; i < 3; i++)
            {
                float a = i / 3f * Mathf.PI * 2f;
                Rod(t, stool + Vector3.up * 0.6f + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * 0.12f, stool + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * 0.22f, 0.02f, darkTimber);
            }
            // A lantern hanging from the ridge (click it on and off).
            LanternModel.Hanging(t, new Vector3(0.1f, Eaves + 0.3f, -0.1f), 0.55f, metal, metal, made, 5f, 2.4f);
        }

        // ------------------------------------------------------------------ the ladder

        // Up the west side: two rails, rungs, the rails running on past the deck to grab, and hoops of a safety cage
        // round where you climb from 2.5 m up, joined by bars. (You climb through it all: no colliders. The
        // "Ladder" box marks where you can climb.)
        void Ladder(Transform t)
        {
            float top = Deck + 1.2f;
            for (int s = -1; s <= 1; s += 2)
            {
                Box(t, new Vector3(LadderX, top * 0.5f, s * 0.24f), new Vector3(0.06f, top, 0.08f), darkTimber, false);
                // (and bent over onto the deck, to hold as you step off)
                Beam(t, new Vector3(LadderX, top, s * 0.24f), new Vector3(-Half - 0.05f, Deck + 1.0f, s * 0.42f), 0.05f, 0.05f, metal, false);
            }
            for (float y = 0.3f; y < Deck + 0.1f; y += 0.3f)
                Rod(t, new Vector3(LadderX, y, -0.24f), new Vector3(LadderX, y, 0.24f), 0.018f, metal);
            // The cage: hoops (half rings round the climber) every metre and a bit, from 2.5 m up, and five bars.
            Vector3 centre = new Vector3(LadderX - 0.42f, 0f, 0f);
            const float r = 0.42f;
            var bars = new List<Vector3>();
            for (int b = 0; b < 5; b++)
            {
                float a = Mathf.Lerp(-80f, 80f, b / 4f) * Mathf.Deg2Rad;
                bars.Add(new Vector3(-Mathf.Cos(a) * r, 0f, Mathf.Sin(a) * r));
            }
            for (float y = 2.5f; y < top; y += 1.15f)
            {
                const int seg = 10;
                for (int i = 0; i < seg; i++)
                {
                    float a0 = Mathf.Lerp(-90f, 90f, i / (float)seg) * Mathf.Deg2Rad, a1 = Mathf.Lerp(-90f, 90f, (i + 1) / (float)seg) * Mathf.Deg2Rad;
                    Rod(t, centre + new Vector3(-Mathf.Cos(a0) * r, y, Mathf.Sin(a0) * r), centre + new Vector3(-Mathf.Cos(a1) * r, y, Mathf.Sin(a1) * r), 0.015f, metal);
                }
            }
            foreach (var bar in bars) Rod(t, centre + bar + Vector3.up * 2.5f, centre + bar + Vector3.up * (top - 0.05f), 0.012f, metal);
            // Where you can climb.
            var zone = new GameObject("Ladder") { layer = Layers.Props }.transform;
            zone.SetParent(t, false);
            zone.localPosition = new Vector3(LadderX - 0.25f, (top + 0.6f) * 0.5f, 0f); // (reaching from just off the deck's edge)
            var box = zone.gameObject.AddComponent<BoxCollider>();
            box.isTrigger = true;
            box.size = new Vector3(1.1f, top + 0.6f, 1.1f);
        }

        // ------------------------------------------------------------------ shapes

        GameObject Box(Transform parent, Vector3 pos, Vector3 size, Material mat, bool collider)
        {
            var go = Shape(PrimitiveType.Cube, parent, pos, size, mat);
            if (collider) go.AddComponent<BoxCollider>();
            return go;
        }

        // A squared timber (or bar) from a to b, w across and d deep.
        void Beam(Transform parent, Vector3 a, Vector3 b, float w, float d, Material mat, bool collider)
        {
            Vector3 dir = b - a;
            if (dir.sqrMagnitude < 1e-6f) return;
            var go = Box(parent, (a + b) * 0.5f, new Vector3(w, dir.magnitude, d), mat, collider);
            go.transform.localRotation = Quaternion.FromToRotation(Vector3.up, dir);
        }

        void Rod(Transform parent, Vector3 a, Vector3 b, float radius, Material mat)
        {
            Vector3 dir = b - a;
            if (dir.sqrMagnitude < 1e-6f) return;
            var go = Shape(PrimitiveType.Cylinder, parent, (a + b) * 0.5f, new Vector3(radius * 2f, dir.magnitude * 0.5f, radius * 2f), mat);
            go.transform.localRotation = Quaternion.FromToRotation(Vector3.up, dir);
            go.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        void Cyl(Transform parent, Vector3 centre, float radius, float height, Material mat) =>
            Shape(PrimitiveType.Cylinder, parent, centre, new Vector3(radius * 2f, height * 0.5f, radius * 2f), mat);

        void Sphere(Transform parent, Vector3 centre, float radius, Material mat) =>
            Shape(PrimitiveType.Sphere, parent, centre, Vector3.one * radius * 2f, mat);

        static GameObject Shape(PrimitiveType type, Transform parent, Vector3 pos, Vector3 scale, Material mat)
        {
            var go = GameObject.CreatePrimitive(type);
            go.layer = Layers.Props;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localScale = scale;
            go.GetComponent<Renderer>().sharedMaterial = mat;
            Object.Destroy(go.GetComponent<Collider>());
            return go;
        }
    }
}
