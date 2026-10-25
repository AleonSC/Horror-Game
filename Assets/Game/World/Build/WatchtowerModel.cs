using System.Collections.Generic;
using Game.Core;
using Game.Rendering;
using UnityEngine;

namespace Game.World.Build
{
    /// <summary>
    /// An old fire lookout tower (section 6.5), 12 m to its deck: four timber legs splayed out to concrete footings,
    /// tied with girts and X-braced on every face, bolted at the joints; a planked balcony all round on joists and
    /// knee braces, railed, with a gap on the west where the ladder comes up; in the middle, the lookout room:
    /// boarded to the sill with windows above (old glass, some panes cracked or gone) on three sides, the west side
    /// boarded up with the door in it, a hipped tin roof with a finial, a lightning rod and the radio's aerial; a
    /// floodlight on a bracket at each corner under the eaves, wired to a switch box inside by the door (the
    /// button). Inside (a shelter: sounds from outside muffled), a desk the length of one wall with a CB radio left
    /// on, a logbook and a little TV playing Snake, a stool, a steel locker (the drone, luminescent spray), roller
    /// blinds and a lantern hanging from the roof. The ladder
    /// goes up the west side, its rails running up past the balcony to grab. What you can stand on and bump into
    /// has simple colliders; the details have none.
    /// </summary>
    public sealed class WatchtowerModel
    {
        public const float Deck = 12f;                     // the deck's underside (its top is 12.125)
        const float Leg = 2.45f, LegTop = 1.85f;           // the legs' corners, at the ground and under the deck
        const float B = 3f;                                // the balcony's half-width
        const float C = 1.7f;                              // the lookout room's half-width
        const float Gap = 0.55f;                           // half the gap in the railing where the ladder comes up
        const float DoorHalf = 0.45f, DoorH = 2.05f;       // the room's door (in its west wall, facing the ladder)
        const float WallTop = Deck + 1.0f, Eaves = Deck + 2.5f, Apex = Deck + 3.6f;
        public const float LadderX = -B - 0.55f;           // the ladder's rails (west of the balcony)

        readonly List<Object> made;
        readonly System.Random rng;
        readonly Material timber, darkTimber, plank, concrete, metal, tin, glass, brass, rope, cloth;

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
            cloth = Keep(RuntimeMaterials.Lit("Blind cloth", new Color(0.36f, 0.31f, 0.22f), 0.1f, true));
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
            Balcony(t);
            Room(t);
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

        // ------------------------------------------------------------------ the balcony

        void Balcony(Transform t)
        {
            // Joists out past the legs (on knee braces), a rim round the edge, planks across, one floor to stand on.
            for (int i = 0; i < 7; i++)
                Box(t, new Vector3(Mathf.Lerp(-B + 0.1f, B - 0.1f, i / 6f), Deck - 0.1f, 0f), new Vector3(0.12f, 0.2f, B * 2f), darkTimber, false);
            for (int side = 0; side < 4; side++)
            {
                Vector3 outward = Outward(side), along = side % 2 == 0 ? Vector3.right : Vector3.forward;
                Box(t, outward * (B - 0.05f) + Vector3.up * (Deck - 0.05f), Vector3.Scale(along, Vector3.one * B * 2f) + Vector3.Scale(Abs(outward), Vector3.one * 0.1f) + Vector3.up * 0.26f, timber, false);
            }
            foreach (int sx in new[] { -1, 1 })
            foreach (int sz in new[] { -1, 1 })
            {
                Vector3 under = LegAt(sx, sz, Deck - 1.7f);
                Beam(t, under, new Vector3(sx * (B - 0.15f), Deck - 0.2f, sz * LegTop), 0.1f, 0.14f, timber, false);
                Beam(t, under, new Vector3(sx * LegTop, Deck - 0.2f, sz * (B - 0.15f)), 0.1f, 0.14f, timber, false);
            }
            int planks = 21;
            for (int i = 0; i < planks; i++)
            {
                float z = Mathf.Lerp(-B + 0.14f, B - 0.14f, i / (planks - 1f));
                var p = Box(t, new Vector3(R(-0.02f, 0.02f), Deck + 0.06f + R(-0.008f, 0.008f), z), new Vector3(B * 2f, 0.05f, 0.27f), plank, false);
                p.transform.localRotation = Quaternion.Euler(0f, 0f, R(-0.5f, 0.5f));
            }
            Hidden(t, new Vector3(0f, Deck + 0.06f, 0f), new Vector3(B * 2f, 0.13f, B * 2f));

            // The railing: posts, a top rail and a middle one, the gap on the west where the ladder comes up; and
            // what stops you going over it.
            const float railH = 1.05f;
            for (int side = 0; side < 4; side++)
            {
                Vector3 outward = Outward(side), along = side % 2 == 0 ? Vector3.right : Vector3.forward;
                Vector3 edge = outward * (B - 0.06f);
                bool ladderSide = side == 3;
                for (int k = 0; k <= 6; k++)
                {
                    float u = Mathf.Lerp(-B + 0.06f, B - 0.06f, k / 6f);
                    if (ladderSide && Mathf.Abs(u) < Gap) continue;
                    Box(t, edge + along * u + Vector3.up * (Deck + 0.125f + railH * 0.5f), new Vector3(0.09f, railH, 0.09f), timber, false);
                }
                // (spans: the whole side, or the two either side of the gap)
                var spans = ladderSide ? new[] { (-B, -Gap), (Gap, B) } : new[] { (-B, B) };
                foreach (var (u0, u1) in spans)
                {
                    float uc = (u0 + u1) * 0.5f, len = u1 - u0;
                    Vector3 size(float h, float d) => Vector3.Scale(along, Vector3.one * len) + Vector3.Scale(Abs(outward), Vector3.one * d) + Vector3.up * h;
                    Box(t, edge + along * uc + Vector3.up * (Deck + 0.125f + railH), size(0.06f, 0.12f), darkTimber, false);
                    Box(t, edge + along * uc + Vector3.up * (Deck + 0.125f + railH * 0.5f), size(0.05f, 0.06f), timber, false);
                    Hidden(t, edge + along * uc + Vector3.up * (Deck + 0.125f + 0.6f), size(1.2f, 0.1f));
                }
                if (ladderSide) // (posts either side of the gap, the grab rails tied to them)
                    for (int sgn = -1; sgn <= 1; sgn += 2)
                        Box(t, edge + along * sgn * Gap + Vector3.up * (Deck + 0.125f + railH * 0.5f), new Vector3(0.1f, railH, 0.1f), darkTimber, false);
            }
        }

        // ------------------------------------------------------------------ the lookout room

        // Walls all round: boards to the sill, windows above (old glass, a pane cracked or gone here and there) on
        // three sides; the west side (the ladder's) boarded right up, with the door. Inside, a shelter: sounds from
        // outside come muffled.
        void Room(Transform t)
        {
            foreach (int sx in new[] { -1, 1 })
            foreach (int sz in new[] { -1, 1 })
                Box(t, new Vector3(sx * (C - 0.07f), (Deck + Eaves) * 0.5f, sz * (C - 0.07f)), new Vector3(0.15f, Eaves - Deck, 0.15f), timber, false);

            for (int side = 0; side < 4; side++)
            {
                Vector3 along = side % 2 == 0 ? Vector3.right : Vector3.forward;
                Vector3 outward = Outward(side);
                Vector3 mid = outward * (C - 0.04f);
                bool doorSide = side == 3;
                float boardTop = doorSide ? Eaves : WallTop;
                int boards = 15;
                for (int i = 0; i < boards; i++)
                {
                    float u = Mathf.Lerp(-C + 0.12f, C - 0.12f, i / (boards - 1f));
                    bool overDoor = doorSide && Mathf.Abs(u) < DoorHalf + 0.06f;
                    float y0 = overDoor ? Deck + DoorH + 0.08f : Deck + 0.13f;
                    if (boardTop - y0 < 0.05f) continue;
                    Box(t, mid + along * u + Vector3.up * ((y0 + boardTop) * 0.5f),
                        Vector3.Scale(along, Vector3.one * 0.22f) + Vector3.Scale(Abs(outward), Vector3.one * 0.035f) + Vector3.up * (boardTop - y0 + R(-0.03f, 0.01f)),
                        rng.Next(4) == 0 ? darkTimber : plank, false);
                }
                // Solid walls, to the eaves (on the door side, each side of the doorway and over it).
                float wy = (Deck + Eaves) * 0.5f, wh = Eaves - Deck;
                if (doorSide)
                {
                    for (int sgn = -1; sgn <= 1; sgn += 2)
                    {
                        float u0 = DoorHalf + 0.05f, uc = sgn * (u0 + C) * 0.5f;
                        Hidden(t, mid + along * uc + Vector3.up * wy, Vector3.Scale(along, Vector3.one * (C - u0)) + Vector3.Scale(Abs(outward), Vector3.one * 0.1f) + Vector3.up * wh);
                    }
                    float hy = Deck + DoorH + 0.1f;
                    Hidden(t, mid + Vector3.up * ((hy + Eaves) * 0.5f), Vector3.Scale(along, Vector3.one * (DoorHalf * 2f + 0.1f)) + Vector3.Scale(Abs(outward), Vector3.one * 0.1f) + Vector3.up * (Eaves - hy));
                    // The frame round the doorway.
                    for (int sgn = -1; sgn <= 1; sgn += 2)
                        Box(t, mid + outward * 0.03f + along * sgn * (DoorHalf + 0.05f) + Vector3.up * (Deck + 0.125f + DoorH * 0.5f), Vector3.Scale(along, Vector3.one * 0.1f) + Vector3.Scale(Abs(outward), Vector3.one * 0.12f) + Vector3.up * DoorH, darkTimber, false);
                    Box(t, mid + outward * 0.03f + Vector3.up * (Deck + 0.125f + DoorH + 0.05f), Vector3.Scale(along, Vector3.one * (DoorHalf * 2f + 0.2f)) + Vector3.Scale(Abs(outward), Vector3.one * 0.12f) + Vector3.up * 0.1f, darkTimber, false);
                }
                else Hidden(t, mid + Vector3.up * wy, Vector3.Scale(along, Vector3.one * C * 2f) + Vector3.Scale(Abs(outward), Vector3.one * 0.1f) + Vector3.up * wh);
                Box(t, mid + Vector3.up * (Eaves - 0.06f), Vector3.Scale(along, Vector3.one * C * 2f) + Vector3.Scale(Abs(outward), Vector3.one * 0.12f) + Vector3.up * 0.12f, timber, false);
                if (doorSide) continue;
                // The sill, then two bays of windows, each split by a cross-bar.
                Box(t, mid + outward * 0.04f + Vector3.up * (WallTop + 0.02f), Vector3.Scale(along, Vector3.one * C * 2f) + Vector3.Scale(Abs(outward), Vector3.one * 0.14f) + Vector3.up * 0.05f, darkTimber, false);
                Box(t, mid + Vector3.up * ((WallTop + Eaves) * 0.5f), Vector3.Scale(along, Vector3.one * 0.07f) + Vector3.Scale(Abs(outward), Vector3.one * 0.08f) + Vector3.up * (Eaves - WallTop), timber, false);
                for (int bay = 0; bay < 2; bay++)
                {
                    float u0 = bay == 0 ? -C : 0f, u1 = bay == 0 ? 0f : C, uc = (u0 + u1) * 0.5f;
                    float cy = (WallTop + Eaves) * 0.5f;
                    RollerBlind(t, mid - outward * 0.09f + along * uc + Vector3.up * (Eaves - 0.17f), -outward, u1 - u0 - 0.14f, Eaves - WallTop - 0.22f);
                    if (rng.Next(8) == 0) continue; // (gone)
                    Box(t, mid + along * uc + Vector3.up * (cy + 0.05f), Vector3.Scale(along, Vector3.one * (u1 - u0 - 0.06f)) + Vector3.Scale(Abs(outward), Vector3.one * 0.05f) + Vector3.up * 0.04f, timber, false);
                    var pane = Box(t, mid + along * uc + Vector3.up * cy, Vector3.Scale(along, Vector3.one * (u1 - u0 - 0.08f)) + Vector3.Scale(Abs(outward), Vector3.one * 0.01f) + Vector3.up * (Eaves - WallTop - 0.14f), glass, false);
                    pane.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    if (rng.Next(4) == 0)
                    {
                        var crack = Box(t, mid + outward * 0.008f + along * (uc + R(-0.25f, 0.25f)) + Vector3.up * (cy + R(-0.2f, 0.2f)),
                            Vector3.Scale(along, Vector3.one * R(0.25f, 0.5f)) + Vector3.Scale(Abs(outward), Vector3.one * 0.003f) + Vector3.up * 0.008f, rope, false);
                        crack.transform.localRotation = Quaternion.AngleAxis(R(-40f, 40f), outward);
                    }
                }
            }
            // The ceiling (so you can't jump out through the roof).
            Hidden(t, new Vector3(0f, Eaves + 0.15f, 0f), new Vector3(C * 2f, 0.3f, C * 2f));

            // The door: boards on ledges and a brace, a latch; hinged on its south side, swinging out.
            var hinge = new GameObject("Door") { layer = Layers.Props }.transform;
            hinge.SetParent(t, false);
            hinge.localPosition = new Vector3(-C + 0.02f, Deck + 0.125f, -DoorHalf);
            var leaf = Box(hinge, new Vector3(0f, DoorH * 0.5f, DoorHalf), new Vector3(0.05f, DoorH - 0.02f, DoorHalf * 2f - 0.02f), plank, true);
            leaf.GetComponent<Renderer>().enabled = false;
            for (int i = 0; i < 4; i++)
                Box(hinge, new Vector3(0f, DoorH * 0.5f, Mathf.Lerp(0.12f, DoorHalf * 2f - 0.12f, i / 3f)), new Vector3(0.045f, DoorH - 0.03f, DoorHalf * 0.5f - 0.015f), i == 2 ? darkTimber : plank, false);
            foreach (float y in new[] { 0.3f, DoorH - 0.3f })
                Box(hinge, new Vector3(-0.04f, y, DoorHalf), new Vector3(0.03f, 0.12f, DoorHalf * 2f - 0.1f), darkTimber, false);
            Beam(hinge, new Vector3(-0.04f, 0.36f, 0.12f), new Vector3(-0.04f, DoorH - 0.36f, DoorHalf * 2f - 0.12f), 0.03f, 0.1f, darkTimber, false);
            Box(hinge, new Vector3(-0.07f, DoorH * 0.48f, DoorHalf * 2f - 0.12f), new Vector3(0.04f, 0.05f, 0.14f), metal, false);
            for (int i = 0; i < 2; i++) Box(hinge, new Vector3(-0.04f, i == 0 ? 0.3f : DoorH - 0.3f, 0.12f), new Vector3(0.035f, 0.05f, 0.25f), metal, false);

            // Where the shelter is (only a marker).
            var inside = new GameObject("Inside") { layer = Layers.Props };
            inside.transform.SetParent(t, false);
            inside.transform.localPosition = new Vector3(0f, Deck + 1.3f, 0f);
            var box = inside.AddComponent<BoxCollider>();
            box.isTrigger = true;
            box.size = new Vector3(C * 2f - 0.2f, 2.3f, C * 2f - 0.2f);
        }

        // ------------------------------------------------------------------ the roof

        void Roof(Transform t)
        {
            float e = C + 0.6f;
            var m = new MeshBuilder();
            Vector3 apex = new Vector3(0f, Apex, 0f);
            Vector3[] c = { new Vector3(-e, Eaves, -e), new Vector3(e, Eaves, -e), new Vector3(e, Eaves, e), new Vector3(-e, Eaves, e) };
            for (int i = 0; i < 4; i++)
            {
                Vector3 a = c[i], b = c[(i + 1) % 4];
                const int strips = 14;
                for (int k = 0; k < strips; k++)
                {
                    float u0 = k / (float)strips, u1 = (k + 1) / (float)strips;
                    float lift0 = (k % 2 == 0) ? 0.03f : 0f, lift1 = (k % 2 == 0) ? 0f : 0.03f;
                    Vector3 p0 = Vector3.Lerp(a, b, u0) + Vector3.up * lift0, p1 = Vector3.Lerp(a, b, u1) + Vector3.up * lift1;
                    Vector3 q0 = Vector3.Lerp(apex, Vector3.Lerp(a, b, u0), 0.02f), q1 = Vector3.Lerp(apex, Vector3.Lerp(a, b, u1), 0.02f);
                    int v0 = m.AddVertex(p0, new Vector2(u0, 0f)), v1 = m.AddVertex(p1, new Vector2(u1, 0f));
                    int v2 = m.AddVertex(q1, new Vector2(u1, 1f)), v3 = m.AddVertex(q0, new Vector2(u0, 1f));
                    m.AddTriangle(v0, v2, v1);
                    m.AddTriangle(v0, v3, v2);
                }
                Rod(t, apex + Vector3.up * 0.03f, a + Vector3.up * 0.04f, 0.045f, tin);
                Beam(t, a + Vector3.down * 0.08f, b + Vector3.down * 0.08f, 0.04f, 0.16f, darkTimber, false);
                Beam(t, new Vector3(a.x * 0.92f, Eaves - 0.05f, a.z * 0.92f), apex + Vector3.down * 0.2f, 0.08f, 0.12f, timber, false);
            }
            var roof = m.ToMesh("Tower roof");
            made.Add(roof);
            var go = new GameObject("Roof") { layer = Layers.Props };
            go.transform.SetParent(t, false);
            go.AddComponent<MeshFilter>().sharedMesh = roof;
            go.AddComponent<MeshRenderer>().sharedMaterial = tin;
            Cyl(t, apex + Vector3.up * 0.12f, 0.07f, 0.22f, metal);
            Sphere(t, apex + Vector3.up * 0.27f, 0.09f, metal);
            Rod(t, apex + Vector3.up * 0.3f, apex + Vector3.up * 1.7f, 0.012f, brass);
            // The radio's aerial, on the roof over it, its lead running down into the room.
            Vector3 mast = new Vector3(c[2].x * 0.7f, Eaves + 0.3f * (Apex - Eaves) + 0.05f, c[1].z * 0.35f);
            Rod(t, mast + Vector3.down * 0.1f, mast + Vector3.up * 3.2f, 0.018f, metal);
            for (int i = 0; i < 3; i++) Rod(t, mast + Vector3.up * (1.6f + i * 0.45f) + Vector3.forward * 0.4f, mast + Vector3.up * (1.6f + i * 0.45f) + Vector3.back * 0.4f, 0.007f, metal);
            Rod(t, mast + Vector3.up * 3.1f, c[1] + Vector3.up * 0.08f, 0.004f, rope);
            Rod(t, mast + Vector3.up * 3.1f, c[2] + Vector3.up * 0.08f, 0.004f, rope);
        }

        // ------------------------------------------------------------------ floodlights

        // A floodlight on a pipe at each corner of the balcony (named "Floodlight": the light shines from there), and
        // inside, conduit along the head rail to the switch box by the door (the "Button").
        void Floodlights(Transform t)
        {
            foreach (int sx in new[] { -1, 1 })
            foreach (int sz in new[] { -1, 1 })
            {
                // (on an iron pipe up from the balcony's corner post, out past everything, so the light falls on the
                // forest and not on the railing and planks a metre off; its wire slung back to the roof's corner)
                Vector3 post = new Vector3(sx * (B - 0.06f), Deck + 0.125f + 1.05f, sz * (B - 0.06f));
                Vector3 outward = new Vector3(sx, 0f, sz).normalized;
                Vector3 at = post + Vector3.up * 1.15f + outward * 0.12f;
                Rod(t, post, post + Vector3.up * 1.2f, 0.025f, metal);
                Beam(t, post + Vector3.up * 1.1f, at, 0.04f, 0.04f, metal, false);
                Rod(t, post + Vector3.up * 1.18f, new Vector3(sx * (C + 0.6f), Eaves - 0.04f, sz * (C + 0.6f)), 0.008f, rope);
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
                for (int f = 0; f < 4; f++)
                    Shape(PrimitiveType.Cube, lamp, new Vector3(0f, 0f, -0.3f - f * 0.03f), new Vector3(0.36f, 0.36f, 0.008f), metal);
            }
            float k = C - 0.1f, y = Eaves - 0.15f;
            Rod(t, new Vector3(-k, y, -k), new Vector3(-k, y, k), 0.015f, metal);
            Rod(t, new Vector3(k, y, -k), new Vector3(-k, y, -k), 0.015f, metal);
            Rod(t, new Vector3(k, y, k), new Vector3(-k, y, k), 0.015f, metal);
            Vector3 box = new Vector3(-C + 0.12f, Deck + 1.3f, DoorHalf + 0.45f);
            Rod(t, new Vector3(-k, y, box.z), box + Vector3.up * 0.2f, 0.015f, metal);
            Box(t, box, new Vector3(0.12f, 0.36f, 0.28f), metal, false);
            Box(t, box + new Vector3(0.065f, 0.12f, 0f), new Vector3(0.01f, 0.06f, 0.2f), brass, false);
            Marker(t, "Button", box + new Vector3(0.1f, -0.05f, 0f));
        }

        // ------------------------------------------------------------------ inside

        void Inside(Transform t)
        {
            // A desk the length of the east wall: a plank top on legs, a shelf under it. On it the radio, a
            // logbook, and a little TV with a joystick (Snake), a stool in front of that.
            float len = C * 2f - 0.2f;
            Vector3 desk = new Vector3(C - 0.32f, Deck + 0.125f, 0f);
            Box(t, desk + Vector3.up * 0.76f, new Vector3(0.5f, 0.04f, len), plank, true);
            foreach (float dz in new[] { -0.5f, 0f, 0.5f })
                Box(t, desk + new Vector3(-0.2f, 0.37f, dz * (len - 0.1f)), new Vector3(0.05f, 0.74f, 0.05f), darkTimber, false);
            Box(t, desk + new Vector3(0.05f, 0.4f, 0f), new Vector3(0.36f, 0.02f, len - 0.1f), darkTimber, false);
            Box(t, desk + new Vector3(0.22f, 0.6f, 0f), new Vector3(0.02f, 0.3f, len), darkTimber, false); // (the back rail)
            Radio(t, desk + new Vector3(0f, 0.78f, -1.0f));
            var log = Box(t, desk + new Vector3(-0.04f, 0.795f, -0.35f), new Vector3(0.2f, 0.025f, 0.28f), Keep(RuntimeMaterials.Lit("Logbook", new Color(0.18f, 0.08f, 0.06f), 0.2f)), false);
            log.transform.localRotation = Quaternion.Euler(0f, 14f, 0f);
            Vector3 tv = desk + new Vector3(0.02f, 0.78f, 0.75f);
            Vector3 stool = new Vector3(C - 0.95f, Deck + 0.125f, 0.75f);
            SnakeTelly(t, tv, stool);
            Cyl(t, stool + Vector3.up * 0.5f, 0.18f, 0.05f, plank);
            for (int i = 0; i < 3; i++)
            {
                float a = i / 3f * Mathf.PI * 2f;
                Rod(t, stool + Vector3.up * 0.48f + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * 0.1f, stool + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * 0.2f, 0.02f, darkTimber);
            }
            // The map on the floor; the locker (the drone in it, in whichever tower you come to first, and a can
            // of luminescent spray in every one).
            Marker(t, "Deck", new Vector3(0f, Deck + 0.13f, -1.05f));
            Locker(t);
            // A lantern hanging from the ridge (click it on and off).
            LanternModel.Hanging(t, new Vector3(-0.15f, Eaves + 0.25f, 0.15f), 0.55f, metal, metal, made, 5f, 2.4f);
        }

        // A small old TV on the desk facing into the room, its screen bulging, rabbit ears, knobs; a joystick in
        // front of it on a cord (its stick leans as you steer). Click it to sit on the stool and play Snake.
        void SnakeTelly(Transform t, Vector3 at, Vector3 stool)
        {
            var root = new GameObject("Snake TV") { layer = Layers.Props }.transform;
            root.SetParent(t, false);
            root.localPosition = at;
            root.localRotation = Quaternion.Euler(0f, -90f, 0f); // (its face west, into the room)
            var r = root;
            var shell = Keep(RuntimeMaterials.Lit("TV plastic", new Color(0.42f, 0.4f, 0.36f), 0.35f));
            Box(r, new Vector3(0f, 0.15f, -0.02f), new Vector3(0.36f, 0.3f, 0.3f), shell, false);
            Box(r, new Vector3(0f, 0.15f, -0.2f), new Vector3(0.28f, 0.22f, 0.1f), shell, false); // (the tube's back)
            Box(r, new Vector3(0f, 0.15f, 0.132f), new Vector3(0.34f, 0.28f, 0.01f), Keep(RuntimeMaterials.Lit("TV bezel", new Color(0.1f, 0.1f, 0.1f), 0.4f)), false);
            var screen = Shape(PrimitiveType.Sphere, r, new Vector3(-0.03f, 0.16f, 0.135f), new Vector3(0.24f, 0.19f, 0.03f), glass);
            for (int i = 0; i < 2; i++)
            {
                var knob = Shape(PrimitiveType.Cylinder, r, new Vector3(0.13f, 0.21f - i * 0.07f, 0.142f), new Vector3(0.03f, 0.008f, 0.03f), metal);
                knob.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            }
            for (int i = 0; i < 4; i++) Box(r, new Vector3(0.13f, 0.06f + i * 0.015f, 0.14f), new Vector3(0.05f, 0.005f, 0.004f), metal, false);
            for (int i = 0; i < 5; i++) Box(r, new Vector3(-0.1f + i * 0.05f, 0.302f, -0.05f), new Vector3(0.03f, 0.004f, 0.12f), rope, false); // (vents)
            Rod(r, new Vector3(0f, 0.3f, -0.05f), new Vector3(-0.15f, 0.55f, -0.1f), 0.003f, metal);
            Rod(r, new Vector3(0f, 0.3f, -0.05f), new Vector3(0.18f, 0.52f, -0.08f), 0.003f, metal);
            // The joystick, in front, on its cord.
            Vector3 pad = new Vector3(0.05f, 0f, 0.3f);
            Box(r, pad + new Vector3(0f, 0.02f, 0f), new Vector3(0.12f, 0.035f, 0.12f), Keep(RuntimeMaterials.Lit("Joystick base", new Color(0.05f, 0.05f, 0.05f), 0.4f)), false);
            var stickPivot = new GameObject("Stick").transform;
            stickPivot.SetParent(r, false);
            stickPivot.localPosition = pad + new Vector3(-0.02f, 0.04f, 0f);
            Rod(stickPivot, Vector3.zero, Vector3.up * 0.07f, 0.005f, metal);
            Sphere(stickPivot, Vector3.up * 0.075f, 0.014f, Keep(RuntimeMaterials.Lit("Stick top", new Color(0.55f, 0.04f, 0.03f), 0.7f)));
            Cyl(r, pad + new Vector3(0.035f, 0.04f, 0.03f), 0.012f, 0.01f, Keep(RuntimeMaterials.Lit("Fire button", new Color(0.6f, 0.05f, 0.03f), 0.6f)));
            Vector3 prev = pad + new Vector3(0f, 0.02f, -0.06f);
            for (int k = 1; k <= 6; k++)
            {
                float u = k / 6f;
                Vector3 q = Vector3.Lerp(pad + new Vector3(0f, 0.02f, -0.06f), new Vector3(0.12f, 0.03f, 0.13f), u) + new Vector3(Mathf.Sin(u * 5f) * 0.03f, 0f, 0f);
                Rod(r, prev, q, 0.003f, rope);
                prev = q;
            }
            var hit = r.gameObject.AddComponent<BoxCollider>();
            hit.center = new Vector3(0f, 0.15f, 0f);
            hit.size = new Vector3(0.38f, 0.32f, 0.36f);
            var middle = new GameObject("Screen middle").transform;
            middle.SetParent(r, false);
            middle.localPosition = new Vector3(-0.03f, 0.16f, 0.14f);
            var seat = new GameObject("Stool").transform;
            seat.SetParent(t, false);
            seat.localPosition = stool;
            seat.localRotation = Quaternion.Euler(0f, 90f, 0f); // (facing the TV, east)
            var game = r.gameObject.AddComponent<SnakeTv>();
            game.screen = screen.GetComponent<Renderer>();
            game.standAt = seat;
            game.screenCentre = middle;
            game.stick = stickPivot;
            game.eyeHeight = 1.15f;
        }

        // A steel locker in the north-west corner: louvred door with a handle, a name card slot, on short feet.
        // Search it and its door swings open. The drone waits in it (in whichever tower you come to first); a can
        // of luminescent spray in every one.
        void Locker(Transform t)
        {
            const float w = 0.55f, d = 0.5f, h = 1.85f;
            var root = new GameObject("Locker") { layer = Layers.Props }.transform;
            root.SetParent(t, false);
            root.localPosition = new Vector3(-C + 0.6f, Deck + 0.125f, C - 0.33f);
            root.localRotation = Quaternion.Euler(0f, 180f, 0f); // (its door south, into the room)
            var steel = Keep(RuntimeMaterials.Lit("Locker steel", new Color(0.2f, 0.26f, 0.24f), 0.45f));
            steel.SetFloat("_Metallic", 0.5f);
            Box(root, new Vector3(-w * 0.5f + 0.01f, h * 0.5f + 0.05f, 0f), new Vector3(0.02f, h, d), steel, false);
            Box(root, new Vector3(w * 0.5f - 0.01f, h * 0.5f + 0.05f, 0f), new Vector3(0.02f, h, d), steel, false);
            Box(root, new Vector3(0f, h * 0.5f + 0.05f, -d * 0.5f + 0.01f), new Vector3(w, h, 0.02f), steel, false);
            Box(root, new Vector3(0f, h + 0.04f, 0f), new Vector3(w, 0.02f, d), steel, false);
            Box(root, new Vector3(0f, 0.06f, 0f), new Vector3(w, 0.02f, d), steel, false);
            Box(root, new Vector3(0f, 1.45f, 0f), new Vector3(w - 0.04f, 0.015f, d - 0.04f), steel, false); // (the shelf)
            foreach (float x in new[] { -1f, 1f })
            foreach (float z in new[] { -1f, 1f })
                Box(root, new Vector3(x * (w * 0.5f - 0.03f), 0.025f, z * (d * 0.5f - 0.03f)), new Vector3(0.03f, 0.05f, 0.03f), rope, false);
            // The door, hinged on its left: louvres, a handle, the card slot.
            var door = new GameObject("Locker door").transform;
            door.SetParent(root, false);
            door.localPosition = new Vector3(-w * 0.5f, 0.05f, d * 0.5f);
            Box(door, new Vector3(w * 0.5f, h * 0.5f, 0.005f), new Vector3(w, h, 0.012f), steel, false);
            for (int i = 0; i < 6; i++)
            {
                var vent = Box(door, new Vector3(w * 0.5f, h - 0.15f - i * 0.03f, 0.013f), new Vector3(w * 0.6f, 0.008f, 0.01f), rope, false);
                vent.transform.localRotation = Quaternion.Euler(30f, 0f, 0f);
                Box(door, new Vector3(w * 0.5f, 0.2f + i * 0.03f, 0.013f), new Vector3(w * 0.6f, 0.008f, 0.01f), rope, false).transform.localRotation = Quaternion.Euler(30f, 0f, 0f);
            }
            Box(door, new Vector3(w - 0.06f, h * 0.52f, 0.02f), new Vector3(0.02f, 0.12f, 0.02f), metal, false);
            Box(door, new Vector3(w * 0.5f, h - 0.4f, 0.013f), new Vector3(0.1f, 0.05f, 0.004f), brass, false);
            // What you bump into, and the search (it is the locker).
            var solid = root.gameObject.AddComponent<BoxCollider>();
            solid.center = new Vector3(0f, h * 0.5f + 0.05f, 0f);
            solid.size = new Vector3(w, h, d);
            var box = root.gameObject.AddComponent<Container>();
            box.label = "Search the locker";
            box.door = door;
            box.Put(Game.Player.ItemKind.Spray, 1);
        }

        // A CB radio set: a steel case, a glowing dial, knobs, a meter, a speaker grille; its handset on a coiled
        // cord, its aerial lead up the wall. It's left on (CbRadio).
        void Radio(Transform t, Vector3 top)
        {
            var root = new GameObject("CB radio") { layer = Layers.Props };
            root.transform.SetParent(t, false);
            root.transform.localPosition = top;
            root.transform.localRotation = Quaternion.Euler(0f, -90f, 0f); // (its face toward the room, west)
            var r = root.transform;
            var steel = Keep(RuntimeMaterials.Lit("Radio case", new Color(0.12f, 0.13f, 0.12f), 0.4f));
            Box(r, new Vector3(0f, 0.08f, 0f), new Vector3(0.36f, 0.15f, 0.26f), steel, false);
            Box(r, new Vector3(0f, 0.08f, 0.132f), new Vector3(0.34f, 0.13f, 0.005f), darkTimber, false); // (the front panel)
            var dial = Box(r, new Vector3(-0.07f, 0.11f, 0.136f), new Vector3(0.14f, 0.04f, 0.004f), glass, false);
            Box(r, new Vector3(0.09f, 0.11f, 0.136f), new Vector3(0.06f, 0.035f, 0.004f), Keep(RuntimeMaterials.Lit("Radio meter", new Color(0.6f, 0.55f, 0.4f), 0.5f)), false);
            for (int i = 0; i < 4; i++)
            {
                var knob = Shape(PrimitiveType.Cylinder, r, new Vector3(-0.12f + i * 0.07f, 0.045f, 0.145f), new Vector3(0.028f, 0.01f, 0.028f), metal);
                knob.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            }
            for (int i = 0; i < 5; i++) Box(r, new Vector3(0.14f, 0.045f + i * 0.012f, 0.136f), new Vector3(0.05f, 0.004f, 0.004f), metal, false); // (the grille)
            // The handset, on a hook at the side, its coiled cord.
            Vector3 hook = new Vector3(0.21f, 0.1f, 0.05f);
            Box(r, hook, new Vector3(0.01f, 0.01f, 0.03f), metal, false);
            Box(r, hook + new Vector3(0.025f, -0.04f, 0.0f), new Vector3(0.035f, 0.09f, 0.055f), steel, false);
            Vector3 prev = hook + new Vector3(0.025f, -0.09f, 0f);
            for (int i = 1; i <= 14; i++)
            {
                float u = i / 14f;
                Vector3 p = Vector3.Lerp(hook + new Vector3(0.025f, -0.09f, 0f), new Vector3(0.12f, 0.02f, -0.1f), u) + new Vector3(Mathf.Sin(u * 30f) * 0.012f, -Mathf.Sin(u * Mathf.PI) * 0.07f, Mathf.Cos(u * 30f) * 0.012f);
                Rod(r, prev, p, 0.004f, rope);
                prev = p;
            }
            // The aerial lead up the wall behind it.
            Rod(r, new Vector3(0f, 0.15f, -0.13f), new Vector3(0f, Eaves - Deck - 0.9f, -0.2f), 0.006f, rope);
            var hit = root.AddComponent<BoxCollider>();
            hit.isTrigger = true;
            hit.center = new Vector3(0f, 0.08f, 0f);
            hit.size = new Vector3(0.42f, 0.2f, 0.32f);
            var radio = root.AddComponent<CbRadio>();
            radio.dial = dial.GetComponent<Renderer>();
            radio.place = t;
        }

        // ------------------------------------------------------------------ the ladder

        // Up the west side to the gap in the balcony railing: two rails, rungs, the rails running on past the
        // balcony to grab, stood off from the frame on brackets. (You climb through it: no colliders. The "Ladder"
        // box marks where you can climb, "LadderTop" where climbing off the top puts you.)
        void Ladder(Transform t)
        {
            float top = Deck + 1.2f;
            for (int s = -1; s <= 1; s += 2)
            {
                Box(t, new Vector3(LadderX, top * 0.5f, s * 0.24f), new Vector3(0.06f, top, 0.08f), darkTimber, false);
                Beam(t, new Vector3(LadderX, top, s * 0.24f), new Vector3(-B + 0.06f, Deck + 1.0f, s * Gap), 0.05f, 0.05f, metal, false);
                foreach (float y in new[] { 3.4f, 6.3f, 9.2f })
                    Beam(t, new Vector3(LadderX, y, s * 0.24f), new Vector3(LegAt(-1, 1, y).x, y, s * 0.24f), 0.06f, 0.08f, timber, false);
            }
            Box(t, new Vector3(LadderX, -0.1f, 0f), new Vector3(0.4f, 0.3f, 0.7f), concrete, false);
            for (float y = 0.3f; y < Deck + 0.1f; y += 0.3f)
                Rod(t, new Vector3(LadderX, y, -0.24f), new Vector3(LadderX, y, 0.24f), 0.018f, metal);
            var zone = new GameObject("Ladder") { layer = Layers.Props }.transform;
            zone.SetParent(t, false);
            zone.localPosition = new Vector3(LadderX - 0.05f, (top + 0.6f) * 0.5f, 0f); // (stopping short of the balcony's edge)
            var box = zone.gameObject.AddComponent<BoxCollider>();
            box.isTrigger = true;
            box.size = new Vector3(0.8f, top + 0.6f, 1.0f);
            Marker(t, "LadderTop", new Vector3(-B + 0.55f, Deck + 0.15f, 0f));
        }

        // A roller blind at the top of a window, inside: the roller in its brackets, the cloth (rolled up till you
        // click it), a bar along its bottom and a pull cord with a ring. Clicking anywhere on the window works it.
        void RollerBlind(Transform t, Vector3 at, Vector3 inward, float width, float drop)
        {
            var root = new GameObject("Blind") { layer = Layers.Props }.transform;
            root.SetParent(t, false);
            root.localPosition = at;
            root.localRotation = Quaternion.LookRotation(inward, Vector3.up);
            var rollerTube = Shape(PrimitiveType.Cylinder, root, Vector3.zero, new Vector3(0.05f, width * 0.5f + 0.02f, 0.05f), cloth);
            rollerTube.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
            foreach (float x in new[] { -1f, 1f }) Box(root, new Vector3(x * (width * 0.5f + 0.03f), 0.01f, -0.02f), new Vector3(0.02f, 0.07f, 0.06f), metal, false);
            var clothPart = Box(root, Vector3.zero, Vector3.one, cloth, false).transform;
            var bar = new GameObject("Bar").transform;
            bar.SetParent(root, false);
            Box(bar, Vector3.zero, new Vector3(width, 0.025f, 0.015f), darkTimber, false);
            Rod(bar, new Vector3(0f, -0.01f, 0.01f), new Vector3(0f, -0.2f, 0.01f), 0.002f, rope);
            var ring = Shape(PrimitiveType.Cylinder, bar, new Vector3(0f, -0.22f, 0.01f), new Vector3(0.035f, 0.003f, 0.035f), brass);
            ring.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            var hit = root.gameObject.AddComponent<BoxCollider>();
            hit.isTrigger = true;
            hit.center = new Vector3(0f, -drop * 0.5f, -0.02f);
            hit.size = new Vector3(width, drop, 0.05f);
            var blind = root.gameObject.AddComponent<Blind>();
            blind.cloth = clothPart;
            blind.bar = bar;
            blind.width = width;
            blind.drop = drop;
        }

        void Hidden(Transform parent, Vector3 pos, Vector3 size)
        {
            var go = Box(parent, pos, size, plank, true);
            go.GetComponent<Renderer>().enabled = false;
        }

        static void Marker(Transform parent, string name, Vector3 local)
        {
            var go = new GameObject(name) { layer = Layers.Props };
            go.transform.SetParent(parent, false);
            go.transform.localPosition = local;
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
