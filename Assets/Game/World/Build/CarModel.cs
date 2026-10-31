using System.Collections.Generic;
using Game.Core;
using Game.Rendering;
using UnityEngine;

namespace Game.World.Build
{
    /// <summary>
    /// The abandoned car, built in: an old four-door sedan, about 4.7 m long, its front +Z, standing on its wheels at
    /// the pivot. Paint gone dull, rust round the edges, chrome bumpers and trim, round headlights in chrome rims, a
    /// barred grille, tail lights, the plate. Four doors on their hinges (they open: <see cref="Door"/>), each with
    /// its tinted window, handle and inner trim; tinted windscreen and rear window. Inside: a dashboard with its
    /// dials, the steering wheel, a gear stick, front seats with headrests, the back bench, the mirror, and the
    /// glove box behind its little door. The trunk lid opens. One tyre's flat. The parts <see cref="Car"/> works
    /// with are named: "Door FL", "Door FR", "Door RL", "Door RR", "Trunk lid", "Glove box lid", "Headlight L/R",
    /// "Tail light L/R", "Indicator L/R".
    /// </summary>
    public sealed class CarModel
    {
        readonly List<Object> made;
        readonly Material paint, rust, chrome, rubber, glass, fabric, dash, lens, tail, amber, plate, carpet;
        Transform t;

        public CarModel(List<Object> made)
        {
            this.made = made;
            paint = Keep(RuntimeMaterials.Lit("Car paint", new Color(0.16f, 0.035f, 0.03f), 0.45f));
            rust = Keep(RuntimeMaterials.Lit("Rust", new Color(0.2f, 0.09f, 0.04f), 0.15f));
            chrome = Keep(RuntimeMaterials.Lit("Chrome", new Color(0.55f, 0.55f, 0.57f), 0.85f));
            chrome.SetFloat("_Metallic", 1f);
            rubber = Keep(RuntimeMaterials.Lit("Tyre", new Color(0.025f, 0.025f, 0.025f), 0.25f));
            glass = Keep(RuntimeMaterials.Glass("Tinted glass", new Color(0.015f, 0.018f, 0.022f, 0.86f)));
            fabric = Keep(RuntimeMaterials.Lit("Seat fabric", new Color(0.09f, 0.075f, 0.06f), 0.1f));
            dash = Keep(RuntimeMaterials.Lit("Dashboard", new Color(0.04f, 0.04f, 0.045f), 0.35f));
            lens = Keep(RuntimeMaterials.LitGlow("Headlight lens", new Color(0.75f, 0.75f, 0.7f), Color.black));
            tail = Keep(RuntimeMaterials.LitGlow("Tail light", new Color(0.35f, 0.02f, 0.02f), Color.black));
            amber = Keep(RuntimeMaterials.LitGlow("Indicator", new Color(0.5f, 0.28f, 0.05f), Color.black));
            plate = Keep(RuntimeMaterials.Lit("Number plate", new Color(0.7f, 0.68f, 0.6f), 0.3f));
            carpet = Keep(RuntimeMaterials.Lit("Carpet", new Color(0.035f, 0.03f, 0.028f), 0.05f));
        }

        Material Keep(Material m)
        {
            made.Add(m);
            return m;
        }

        public void Build(Transform root, System.Random rng)
        {
            t = root;
            // The body: the floor and sills, the engine bay and the rear, the hood, the front and back ends.
            Box(t, "Sills", new Vector3(0f, 0.46f, -0.05f), new Vector3(1.78f, 0.28f, 4.3f), paint, true);
            Box(t, "Front", new Vector3(0f, 0.81f, 1.74f), new Vector3(1.78f, 0.42f, 1.15f), paint, true);
            Box(t, "Rear", new Vector3(0f, 0.79f, -1.83f), new Vector3(1.78f, 0.38f, 1.05f), paint, true);
            Box(t, "Hood", new Vector3(0f, 1.045f, 1.74f), new Vector3(1.74f, 0.05f, 1.12f), paint, false).transform.localRotation = Quaternion.Euler(-2f, 0f, 0f);
            Box(t, "Front panel", new Vector3(0f, 0.8f, 2.31f), new Vector3(1.78f, 0.42f, 0.05f), paint, false);
            Box(t, "Grille", new Vector3(0f, 0.8f, 2.335f), new Vector3(0.86f, 0.22f, 0.03f), dash, false);
            for (int i = 0; i < 5; i++) Box(t, "Grille bar", new Vector3(0f, 0.71f + i * 0.045f, 2.355f), new Vector3(0.88f, 0.012f, 0.012f), chrome, false);
            Box(t, "Rear panel", new Vector3(0f, 0.78f, -2.36f), new Vector3(1.78f, 0.36f, 0.05f), paint, false);
            // The trunk lid, hinged at its front edge, opening up.
            var trunk = Pivot(t, "Trunk lid", new Vector3(0f, 0.98f, -1.3f));
            Box(trunk, "Trunk panel", new Vector3(0f, 0.025f, -0.52f), new Vector3(1.74f, 0.05f, 1.06f), paint, true);
            Box(trunk, "Trunk lock", new Vector3(0f, -0.04f, -1.04f), new Vector3(0.08f, 0.05f, 0.02f), chrome, false);
            var lid = trunk.gameObject.AddComponent<Lid>();
            lid.what = "trunk";
            lid.openAngle = -65f;
            // Bumpers, chrome, wrapping round the corners.
            foreach (float z in new[] { 2.4f, -2.42f })
            {
                Box(t, "Bumper", new Vector3(0f, 0.5f, z), new Vector3(1.84f, 0.11f, 0.12f), chrome, true);
                for (int side = -1; side <= 1; side += 2)
                    Box(t, "Bumper end", new Vector3(side * 0.9f, 0.5f, z - Mathf.Sign(z) * 0.14f), new Vector3(0.06f, 0.11f, 0.3f), chrome, false);
            }
            // Lights: round headlights in chrome rims, amber indicators under them, red tail lights, the plate.
            for (int side = -1; side <= 1; side += 2)
            {
                string s = side < 0 ? "L" : "R";
                Cyl(t, "Headlight rim", new Vector3(side * 0.62f, 0.83f, 2.32f), 0.11f, 0.06f, chrome, Vector3.forward);
                Cyl(t, $"Headlight {s}", new Vector3(side * 0.62f, 0.83f, 2.35f), 0.09f, 0.02f, lens, Vector3.forward, true);
                Box(t, $"Indicator {s}", new Vector3(side * 0.62f, 0.66f, 2.335f), new Vector3(0.16f, 0.06f, 0.03f), amber, false);
                Box(t, $"Tail light {s}", new Vector3(side * 0.66f, 0.86f, -2.39f), new Vector3(0.26f, 0.12f, 0.03f), tail, false);
                Box(t, "Tail rim", new Vector3(side * 0.66f, 0.86f, -2.38f), new Vector3(0.29f, 0.15f, 0.02f), chrome, false);
            }
            Box(t, "Plate", new Vector3(0f, 0.63f, -2.405f), new Vector3(0.34f, 0.15f, 0.01f), plate, false);
            Box(t, "Plate (front)", new Vector3(0f, 0.6f, 2.465f), new Vector3(0.34f, 0.12f, 0.01f), plate, false);

            // The greenhouse: pillars, roof with chrome drip rails, windscreen and rear window.
            for (int side = -1; side <= 1; side += 2)
            {
                Rod(t, new Vector3(side * 0.82f, 1.03f, 1.13f), new Vector3(side * 0.67f, 1.43f, 0.56f), 0.035f, paint);
                Rod(t, new Vector3(side * 0.8f, 1.0f, 0.0f), new Vector3(side * 0.69f, 1.43f, -0.02f), 0.04f, paint);
                Rod(t, new Vector3(side * 0.82f, 1.0f, -1.28f), new Vector3(side * 0.67f, 1.43f, -0.86f), 0.06f, paint);
                Box(t, "Drip rail", new Vector3(side * 0.7f, 1.44f, -0.15f), new Vector3(0.02f, 0.02f, 1.42f), chrome, false);
            }
            Box(t, "Roof", new Vector3(0f, 1.455f, -0.15f), new Vector3(1.42f, 0.05f, 1.44f), paint, true);
            Pane(t, "Windscreen", new Vector3(0f, 1.03f, 1.13f), new Vector3(0f, 1.43f, 0.56f), 1.48f);
            Pane(t, "Rear window", new Vector3(0f, 0.99f, -1.28f), new Vector3(0f, 1.43f, -0.86f), 1.44f);
            for (int side = -1; side <= 1; side += 2) // (the little fixed panes behind the rear doors)
                Pane(t, "Quarter window", new Vector3(side * 0.8f, 1.02f, -1.2f), new Vector3(side * 0.7f, 1.4f, -0.9f), 0.25f, true);

            // The doors.
            Door(t, "Door FL", -1, 1.15f, 1.15f, true);
            Door(t, "Door FR", 1, 1.15f, 1.15f, true);
            Door(t, "Door RL", -1, 0f, 1.28f, false);
            Door(t, "Door RR", 1, 0f, 1.28f, false);

            // Inside.
            Box(t, "Carpet", new Vector3(0f, 0.335f, -0.1f), new Vector3(1.62f, 0.02f, 2.5f), carpet, false);
            Box(t, "Dashboard", new Vector3(0f, 0.96f, 0.98f), new Vector3(1.62f, 0.22f, 0.32f), dash, false);
            Box(t, "Dash top", new Vector3(0f, 1.075f, 1.0f), new Vector3(1.62f, 0.03f, 0.38f), dash, false);
            Box(t, "Dials", new Vector3(-0.38f, 1.0f, 0.815f), new Vector3(0.36f, 0.12f, 0.02f), Keep(RuntimeMaterials.Lit("Dials", new Color(0.02f, 0.02f, 0.02f), 0.8f)), false);
            for (int i = 0; i < 3; i++) Cyl(t, "Dial", new Vector3(-0.5f + i * 0.12f, 1.0f, 0.802f), 0.04f, 0.006f, chrome, Vector3.forward);
            // The glove box: a little door in the dash on the passenger's side, hinged at its bottom.
            var glove = Pivot(t, "Glove box lid", new Vector3(0.42f, 0.89f, 0.815f));
            Box(glove, "Glove box door", new Vector3(0f, 0.075f, 0f), new Vector3(0.38f, 0.15f, 0.025f), dash, true);
            Box(glove, "Glove box catch", new Vector3(0f, 0.13f, -0.016f), new Vector3(0.06f, 0.02f, 0.01f), chrome, false);
            var gloveLid = glove.gameObject.AddComponent<Lid>();
            gloveLid.what = "glove box";
            gloveLid.openAngle = -75f;
            // The steering wheel on its column, the gear stick, the mirror.
            var wheel = Pivot(t, "Steering wheel", new Vector3(-0.38f, 1.04f, 0.62f));
            wheel.localRotation = Quaternion.Euler(-60f, 0f, 0f);
            Ring(wheel, Vector3.zero, 0.19f, 0.018f, dash, Vector3.up);
            Rod(wheel, Vector3.zero, new Vector3(0f, 0.25f, 0f), 0.025f, dash);
            Rod(wheel, new Vector3(-0.17f, 0f, 0f), new Vector3(0.17f, 0f, 0f), 0.012f, dash);
            Rod(t, new Vector3(0f, 0.36f, 0.62f), new Vector3(0.02f, 0.62f, 0.55f), 0.012f, chrome);
            Cyl(t, "Gear knob", new Vector3(0.02f, 0.64f, 0.55f), 0.025f, 0.04f, dash, Vector3.up);
            Box(t, "Mirror", new Vector3(0f, 1.36f, 0.58f), new Vector3(0.24f, 0.07f, 0.03f), dash, false);
            // Seats: two in front, with headrests; the bench behind.
            for (int side = -1; side <= 1; side += 2)
            {
                Box(t, "Seat", new Vector3(side * 0.38f, 0.5f, 0.15f), new Vector3(0.52f, 0.14f, 0.52f), fabric, false);
                Box(t, "Seat back", new Vector3(side * 0.38f, 0.86f, -0.14f), new Vector3(0.52f, 0.62f, 0.12f), fabric, false).transform.localRotation = Quaternion.Euler(-10f, 0f, 0f);
                Box(t, "Headrest", new Vector3(side * 0.38f, 1.24f, -0.21f), new Vector3(0.26f, 0.16f, 0.09f), fabric, false);
            }
            Box(t, "Back seat", new Vector3(0f, 0.5f, -0.85f), new Vector3(1.42f, 0.14f, 0.52f), fabric, false);
            Box(t, "Back seat back", new Vector3(0f, 0.83f, -1.13f), new Vector3(1.42f, 0.56f, 0.12f), fabric, false).transform.localRotation = Quaternion.Euler(-12f, 0f, 0f);
            Box(t, "Parcel shelf", new Vector3(0f, 1.0f, -1.3f), new Vector3(1.42f, 0.03f, 0.3f), dash, false);

            // The wheels: tyres, chrome hubcaps; the back left's flat.
            for (int i = 0; i < 4; i++)
            {
                int side = i % 2 == 0 ? -1 : 1;
                float z = i < 2 ? 1.42f : -1.42f;
                bool flat = i == 2;
                float r = flat ? 0.31f : 0.36f;
                var tyre = Cyl(t, "Tyre", new Vector3(side * 0.79f, r, z), 0.36f, 0.22f, rubber, Vector3.right);
                if (flat) tyre.transform.localScale = new Vector3(0.72f, 0.11f, 0.62f);
                Cyl(t, "Hubcap", new Vector3(side * 0.905f, r, z), 0.17f, 0.02f, chrome, Vector3.right);
                Cyl(t, "Hub nut", new Vector3(side * 0.918f, r, z), 0.04f, 0.02f, chrome, Vector3.right);
            }
            // Rust, eating in round the wheel arches, the sills and the edges.
            for (int i = 0; i < 14; i++)
            {
                int side = rng.Next(2) == 0 ? -1 : 1;
                float z = (float)rng.NextDouble() * 4.2f - 2.1f;
                float y = 0.38f + (float)rng.NextDouble() * 0.5f;
                var patch = Box(t, "Rust", new Vector3(side * 0.896f, y, z), new Vector3(0.008f, 0.06f + (float)rng.NextDouble() * 0.16f, 0.1f + (float)rng.NextDouble() * 0.3f), rust, false);
                patch.transform.localRotation = Quaternion.Euler((float)rng.NextDouble() * 40f - 20f, 0f, 0f);
            }
        }

        // A door hinged on its front edge at z = hingeZ, on side -1 (left) or 1, swinging out.
        void Door(Transform parent, string name, int side, float hingeZ, float length, bool front)
        {
            var hinge = Pivot(parent, name, new Vector3(side * 0.89f, 0f, hingeZ));
            float mid = -length * 0.5f;
            Box(hinge, "Door panel", new Vector3(0f, 0.79f, mid), new Vector3(0.06f, 0.46f, length), paint, true);
            Box(hinge, "Door top", new Vector3(-side * 0.08f, 1.43f, mid), new Vector3(0.04f, 0.03f, length - 0.04f), paint, false);
            Box(hinge, "Door post", new Vector3(-side * 0.04f, 1.22f, -length + 0.03f), new Vector3(0.04f, 0.42f, 0.04f), paint, false);
            var pane = Box(hinge, "Window", new Vector3(-side * 0.05f, 1.22f, mid), new Vector3(0.012f, 0.4f, length - 0.1f), glass, true);
            pane.transform.localRotation = Quaternion.Euler(0f, 0f, side * 9f); // (leaning in with the roof)
            pane.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            Box(hinge, "Handle", new Vector3(side * 0.04f, 0.92f, -length + 0.24f), new Vector3(0.025f, 0.03f, 0.13f), chrome, false);
            Box(hinge, "Trim", new Vector3(side * 0.035f, 0.98f, mid), new Vector3(0.008f, 0.02f, length - 0.02f), chrome, false);
            Box(hinge, "Door card", new Vector3(-side * 0.04f, 0.8f, mid), new Vector3(0.02f, 0.4f, length - 0.1f), fabric, false);
            if (front) Box(hinge, "Side mirror", new Vector3(side * 0.1f, 1.08f, -0.1f), new Vector3(0.06f, 0.09f, 0.14f), chrome, false);
            var door = hinge.gameObject.AddComponent<Door>();
            door.openAngle = side < 0 ? -68f : 68f; // (outward either side)
            door.swingTime = 0.6f;
        }

        /// <summary>
        /// Crashed head-on (into a tree): everything forward of the front wheels' arches pushed back and folded in
        /// (to about half its length), the hood buckled up and twisted, the front bumper knocked askew, the
        /// windscreen crazed white, the right headlight smashed dark. Call after <see cref="Build"/>.
        /// </summary>
        public void Crash(System.Random rng)
        {
            const float from = 1.2f, keep = 0.5f;
            var crazed = Keep(RuntimeMaterials.Glass("Crazed glass", new Color(0.32f, 0.34f, 0.35f, 0.9f)));
            foreach (Transform part in t)
            {
                if (part.name.StartsWith("Door") || part.name == "Trunk lid") continue;
                Vector3 p = part.localPosition;
                if (p.z > from)
                {
                    p.z = from + (p.z - from) * keep;
                    part.localPosition = p;
                    if (part.localScale.z > 0.5f) part.localScale = new Vector3(part.localScale.x, part.localScale.y, part.localScale.z * 0.62f);
                }
                switch (part.name)
                {
                    case "Hood":
                        part.localRotation = Quaternion.Euler(-24f, 4f, 7f);
                        part.localPosition += new Vector3(0.05f, 0.14f, 0f);
                        break;
                    case "Bumper":
                        if (part.localPosition.z > 0f) part.localRotation = Quaternion.Euler(0f, 11f, -6f);
                        break;
                    case "Windscreen":
                        part.GetComponent<Renderer>().sharedMaterial = crazed;
                        break;
                    case "Headlight R":
                        part.GetComponent<Renderer>().sharedMaterial = Keep(RuntimeMaterials.Lit("Smashed headlight", new Color(0.03f, 0.03f, 0.03f), 0.2f));
                        part.name = "Headlight R (smashed)";
                        break;
                }
            }
            // The hood folded up in a crease across its middle.
            Box(t, "Hood crease", new Vector3(0.02f, 1.2f, from + 0.38f), new Vector3(1.7f, 0.05f, 0.42f), paint, false).transform.localRotation = Quaternion.Euler(-58f, 4f, 7f);
        }

        // A pane of tinted glass from one edge (bottom) to the other (top), this wide.
        void Pane(Transform parent, string name, Vector3 bottom, Vector3 top, float width, bool side = false)
        {
            Vector3 d = top - bottom;
            var g = Box(parent, name, (bottom + top) * 0.5f, side ? new Vector3(0.012f, d.magnitude, width) : new Vector3(width, d.magnitude, 0.012f), glass, true);
            g.transform.localRotation = Quaternion.FromToRotation(Vector3.up, d.normalized);
            g.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        static Transform Pivot(Transform parent, string name, Vector3 at)
        {
            var p = new GameObject(name) { layer = Layers.Props }.transform;
            p.SetParent(parent, false);
            p.localPosition = at;
            return p;
        }

        static GameObject Box(Transform parent, string name, Vector3 at, Vector3 size, Material m, bool collider)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.layer = Layers.Props;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = at;
            go.transform.localScale = size;
            go.GetComponent<Renderer>().sharedMaterial = m;
            if (!collider) Object.Destroy(go.GetComponent<Collider>());
            return go;
        }

        // A cylinder along an axis (its length that way).
        static GameObject Cyl(Transform parent, string name, Vector3 at, float radius, float length, Material m, Vector3 axis, bool collider = false)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            go.name = name;
            go.layer = Layers.Props;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = at;
            go.transform.localRotation = Quaternion.FromToRotation(Vector3.up, axis);
            go.transform.localScale = new Vector3(radius * 2f, length * 0.5f, radius * 2f);
            go.GetComponent<Renderer>().sharedMaterial = m;
            if (!collider) Object.Destroy(go.GetComponent<Collider>());
            return go;
        }

        static void Rod(Transform parent, Vector3 from, Vector3 to, float radius, Material m)
        {
            Vector3 d = to - from;
            if (d.sqrMagnitude < 1e-6f) return;
            var go = Cyl(parent, "Rod", (from + to) * 0.5f, radius, d.magnitude, m, d.normalized);
            go.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
        }

        static void Ring(Transform parent, Vector3 centre, float radius, float thickness, Material m, Vector3 axis)
        {
            Vector3 u = Vector3.Cross(axis, Mathf.Abs(axis.y) < 0.9f ? Vector3.up : Vector3.right).normalized, v = Vector3.Cross(axis, u);
            const int segments = 14;
            for (int i = 0; i < segments; i++)
            {
                float a0 = i / (float)segments * Mathf.PI * 2f, a1 = (i + 1) / (float)segments * Mathf.PI * 2f;
                Rod(parent, centre + (u * Mathf.Cos(a0) + v * Mathf.Sin(a0)) * radius, centre + (u * Mathf.Cos(a1) + v * Mathf.Sin(a1)) * radius, thickness, m);
            }
        }
    }
}
