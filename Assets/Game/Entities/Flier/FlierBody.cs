using System.Collections.Generic;
using Game.Rendering;
using UnityEngine;

namespace Game.Entities
{
    /// <summary>
    /// The flier's body: the built-in one (a vast, starved, bat-winged thing: torn membrane wings on long finger
    /// bones, a long neck and toothed beak, trailing talons and a whip of a tail) or your own model. One unit across the wings, facing +Z. Its wings beat or glide.
    /// </summary>
    public sealed class FlierBody : MonoBehaviour
    {
        Transform wingL, wingR, tipL, tipR, head;
        Animator animator;
        Material skin;
        float phase;

        public Transform Eye { get; private set; }

        public static FlierBody Create(Transform parent, FlierConfig config)
        {
            var go = new GameObject("Flier body") { layer = Game.Core.Layers.Effects };
            go.transform.SetParent(parent, false);
            var b = go.AddComponent<FlierBody>();
            b.skin = RuntimeMaterials.Silhouette("Flier");
            if (config.customModel != null) b.UseYours(config.customModel);
            else b.Build();
            return b;
        }

        void OnDestroy()
        {
            if (skin != null) Destroy(skin);
            foreach (var m in meshes) if (m != null) Destroy(m);
        }

        public void SetShade(float shade) => RuntimeMaterials.SetSilhouetteShade(skin, shade);

        void UseYours(GameObject prefab)
        {
            var model = Instantiate(prefab, transform, false);
            foreach (var c in model.GetComponentsInChildren<Collider>()) Destroy(c);
            var renderers = model.GetComponentsInChildren<Renderer>();
            if (renderers.Length > 0)
            {
                var b = renderers[0].bounds;
                foreach (var r in renderers) b.Encapsulate(r.bounds);
                if (b.size.x > 0.001f) model.transform.localScale = model.transform.localScale / b.size.x;
                foreach (var r in renderers) r.sharedMaterial = skin; // (drawn in the haze, so the fog can't hide it)
            }
            animator = model.GetComponentInChildren<Animator>();
            foreach (var t in model.GetComponentsInChildren<Transform>()) if (t.name == "Eye") Eye = t;
            if (Eye == null) Eye = Point(transform, new Vector3(0f, -0.01f, 0.2f));
        }

        // Starved and vast, more bat than bird: a lean ridged body; a long neck curving down to a skull with a
        // long, open, toothed beak and two horns swept back; wings of torn membrane stretched between long finger
        // bones that reach past it into claws, a hooked claw at each wrist; long legs trailing under it with hooked
        // talons; and a long tail ending in rags. Mostly seen against the sky, so it's built for its outline.
        void Build()
        {
            var t = transform;
            Shape(PrimitiveType.Capsule, t, Vector3.zero, new Vector3(0.055f, 0.13f, 0.045f), Quaternion.Euler(90f, 0f, 0f)); // body
            Shape(PrimitiveType.Capsule, t, new Vector3(0f, -0.004f, 0.06f), new Vector3(0.07f, 0.06f, 0.055f), Quaternion.Euler(90f, 0f, 0f)); // chest
            for (int i = 0; i < 7; i++) // (the spine, standing out along its back)
                Shape(PrimitiveType.Cube, t, new Vector3(0f, 0.024f, 0.1f - i * 0.035f), new Vector3(0.008f, 0.014f, 0.012f), Quaternion.Euler(-20f, 0f, 0f));

            // The neck, curving forward and down, and the head.
            Vector3[] neck = { new Vector3(0f, 0.012f, 0.13f), new Vector3(0f, 0.008f, 0.18f), new Vector3(0f, -0.006f, 0.225f), new Vector3(0f, -0.024f, 0.262f) };
            for (int i = 0; i + 1 < neck.Length; i++) Bone(t, neck[i], neck[i + 1], 0.022f - i * 0.003f);
            head = Point(t, new Vector3(0f, -0.03f, 0.285f));
            Shape(PrimitiveType.Sphere, head, Vector3.zero, new Vector3(0.042f, 0.036f, 0.06f), Quaternion.identity);         // skull
            Shape(PrimitiveType.Cube, head, new Vector3(0f, 0.001f, 0.075f), new Vector3(0.015f, 0.012f, 0.11f), Quaternion.Euler(6f, 0f, 0f));  // beak
            Shape(PrimitiveType.Cube, head, new Vector3(0f, -0.018f, 0.066f), new Vector3(0.011f, 0.006f, 0.095f), Quaternion.Euler(24f, 0f, 0f)); // jaw, hanging open
            for (int i = 0; i < 6; i++)
            {
                float z = 0.035f + i * 0.016f;
                Shape(PrimitiveType.Cube, head, new Vector3(0f, -0.008f - z * 0.1f, z), new Vector3(0.003f, 0.013f, 0.003f), Quaternion.Euler(10f, 0f, 0f));      // teeth, down
                Shape(PrimitiveType.Cube, head, new Vector3(0f, -0.022f - z * 0.4f + 0.012f, z - 0.004f), new Vector3(0.003f, 0.011f, 0.003f), Quaternion.Euler(-14f, 0f, 0f)); // and up
            }
            for (int side = -1; side <= 1; side += 2)
                Shape(PrimitiveType.Cube, head, new Vector3(side * 0.013f, 0.016f, -0.05f), new Vector3(0.006f, 0.006f, 0.09f), Quaternion.Euler(-28f, side * 14f, 0f)); // horns
            Eye = Point(head, new Vector3(0f, -0.012f, 0.03f));

            // Legs, trailing, with hooked talons.
            legL = Leg(t, -1);
            legR = Leg(t, 1);

            // The tail: a long whip, ragged at its end.
            tail = Point(t, new Vector3(0f, 0f, -0.13f));
            var at = tail;
            for (int i = 0; i < 4; i++)
            {
                Shape(PrimitiveType.Cube, at, new Vector3(0f, 0f, -0.035f), new Vector3(0.012f - i * 0.002f, 0.008f, 0.075f), Quaternion.identity);
                var next = Point(at, new Vector3(0f, 0f, -0.072f));
                tailBones[i] = next;
                at = next;
            }
            var rags = new System.Random(9);
            for (int i = -2; i <= 2; i++)
                Shape(PrimitiveType.Cube, at, new Vector3(i * 0.008f, 0f, -0.03f - (float)rags.NextDouble() * 0.02f), new Vector3(0.007f, 0.002f, 0.06f + (float)rags.NextDouble() * 0.04f), Quaternion.Euler(0f, i * 12f, 0f));

            wingL = Wing(t, -1, out tipL);
            wingR = Wing(t, 1, out tipR);
        }

        Transform legL, legR, tail;
        readonly Transform[] tailBones = new Transform[4];

        Transform Leg(Transform t, int side)
        {
            var hip = Point(t, new Vector3(side * 0.022f, -0.016f, -0.09f));
            Shape(PrimitiveType.Cube, hip, new Vector3(0f, -0.05f, 0f), new Vector3(0.012f, 0.1f, 0.012f), Quaternion.identity);       // thigh
            var knee = Point(hip, new Vector3(0f, -0.1f, 0f));
            Shape(PrimitiveType.Cube, knee, new Vector3(0f, -0.045f, 0f), new Vector3(0.008f, 0.09f, 0.008f), Quaternion.Euler(-30f, 0f, 0f)); // shin
            var foot = Point(knee, new Vector3(0f, -0.085f, 0.03f));
            for (int k = -1; k <= 1; k++)
            {
                var toe = Point(foot, Vector3.zero);
                toe.localRotation = Quaternion.Euler(0f, k * 22f, 0f);
                Shape(PrimitiveType.Cube, toe, new Vector3(0f, 0f, 0.02f), new Vector3(0.004f, 0.004f, 0.04f), Quaternion.identity);
                Shape(PrimitiveType.Cube, toe, new Vector3(0f, -0.008f, 0.042f), new Vector3(0.004f, 0.018f, 0.004f), Quaternion.Euler(-30f, 0f, 0f)); // the hook
            }
            hip.localRotation = Quaternion.Euler(70f, 0f, side * 6f); // (trailing back under the tail)
            return hip;
        }

        // A wing: an arm bone out to the wrist (with its hooked claw), then four long finger bones fanning from the
        // wrist, the membrane stretched between them torn into scallops and rips along its trailing edge, and from
        // the last finger back to the body's flank. The arm part (and its membrane) moves with the shoulder, the
        // fingers (and theirs) bend at the wrist.
        Transform Wing(Transform t, int side, out Transform tip)
        {
            var rng = new System.Random(side > 0 ? 3 : 4);
            var root = Point(t, new Vector3(side * 0.03f, 0.012f, 0.04f));
            Vector3 wrist = new Vector3(side * 0.19f, 0f, 0.05f); // (forward of the shoulder: the leading edge arches)
            Bone(root, Vector3.zero, wrist, 0.014f);
            Shape(PrimitiveType.Sphere, root, new Vector3(side * 0.1f, 0f, 0.03f), new Vector3(0.02f, 0.016f, 0.02f), Quaternion.identity); // elbow
            tip = Point(root, wrist);
            Shape(PrimitiveType.Cube, tip, new Vector3(side * 0.004f, 0.004f, 0.026f), new Vector3(0.006f, 0.006f, 0.04f), Quaternion.Euler(18f, side * -32f, 0f)); // thumb claw

            // The fingers (in the wrist's space), out to just past where the membrane ends: claws.
            Vector3[] fingers =
            {
                new Vector3(side * 0.29f, 0f, -0.05f),
                new Vector3(side * 0.25f, 0f, -0.2f),
                new Vector3(side * 0.16f, 0f, -0.31f),
                new Vector3(side * 0.05f, 0f, -0.35f),
            };
            foreach (var f in fingers)
            {
                Bone(tip, Vector3.zero, f, 0.007f);
                Shape(PrimitiveType.Cube, tip, f + f.normalized * 0.012f, new Vector3(0.004f, 0.004f, 0.026f), Quaternion.LookRotation(f.normalized) * Quaternion.Euler(20f, 0f, 0f)); // claw
            }

            // The outer membrane: fanned from the wrist, its edge from the wing's tip round each scallop between
            // the fingers' ends (the claws stand clear of it).
            var edge = new List<Vector3> { fingers[0] * 0.95f };
            for (int i = 0; i + 1 < fingers.Length; i++)
                Scallop(edge, fingers[i] * 0.95f, fingers[i + 1] * 0.95f, Vector3.zero, rng);
            Membrane(tip, Vector3.zero, edge, false);

            // The inner membrane: from the shoulder out along the arm to the wrist, back along the last finger, and
            // scalloped in to the flank.
            Vector3 lastTip = wrist + fingers[fingers.Length - 1] * 0.95f;
            Vector3 flank = new Vector3(-side * 0.008f, -0.004f, -0.22f);
            var inner = new List<Vector3> { new Vector3(side * 0.005f, 0f, 0.01f), wrist, lastTip };
            Scallop(inner, lastTip, flank, new Vector3(side * 0.09f, 0f, -0.06f), rng);
            Membrane(root, new Vector3(side * 0.1f, 0f, -0.08f), inner, true);
            return root;
        }

        // The edge from a to b (not including a, including b), bowed in toward the middle, torn: little ragged
        // nicks all along it and now and then a deep rip.
        static void Scallop(List<Vector3> edge, Vector3 a, Vector3 b, Vector3 toward, System.Random rng)
        {
            const int n = 9;
            for (int k = 1; k <= n; k++)
            {
                float u = k / (float)n;
                Vector3 p = Vector3.Lerp(a, b, u);
                if (k < n)
                {
                    float bow = 0.2f * Mathf.Sin(Mathf.PI * u);
                    float tear = (float)rng.NextDouble() * 0.05f + (rng.NextDouble() < 0.12 ? 0.16f : 0f);
                    p = Vector3.Lerp(p, toward, Mathf.Clamp01(bow + tear * Mathf.Sin(Mathf.PI * u)));
                }
                edge.Add(p);
            }
        }

        // A thin sheet: fanned from <paramref name="centre"/> round the edge (closed back to its start if asked).
        void Membrane(Transform parent, Vector3 centre, List<Vector3> edge, bool closed)
        {
            var verts = new List<Vector3> { centre };
            verts.AddRange(edge);
            var tris = new List<int>();
            int count = edge.Count;
            for (int i = 0; i < count - 1; i++) { tris.Add(0); tris.Add(i + 1); tris.Add(i + 2); }
            if (closed) { tris.Add(0); tris.Add(count); tris.Add(1); }
            var mesh = new Mesh { name = "Flier wing membrane" };
            mesh.SetVertices(verts);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            meshes.Add(mesh);
            var go = new GameObject("Membrane") { layer = Game.Core.Layers.Effects };
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = skin;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        readonly List<Mesh> meshes = new List<Mesh>();

        // A thin bone from a to b (a box along it).
        void Bone(Transform parent, Vector3 a, Vector3 b, float thickness)
        {
            Vector3 d = b - a;
            if (d.sqrMagnitude < 1e-8f) return;
            Shape(PrimitiveType.Cube, parent, (a + b) * 0.5f, new Vector3(thickness, thickness, d.magnitude), Quaternion.LookRotation(d.normalized));
        }

        /// <summary><paramref name="flap"/> 0 gliding, 1 beating hard; <paramref name="bank"/> -1..1 (it banks into turns).</summary>
        public void Pose(float flap, float bank, float dt)
        {
            if (animator != null) animator.SetFloat("Flap", flap);
            if (wingL == null) return;
            phase += dt * Mathf.Lerp(0.5f, 1.8f, flap) * Mathf.PI * 2f;
            float beat = Mathf.Sin(phase) * Mathf.Lerp(5f, 42f, flap);
            wingL.localRotation = Quaternion.Euler(0f, 0f, beat + 4f);
            wingR.localRotation = Quaternion.Euler(0f, 0f, -beat - 4f);
            // The fingers lag the arm, and fold a little on the upstroke.
            float bend = Mathf.Sin(phase - 0.9f) * Mathf.Lerp(4f, 30f, flap);
            float fold = Mathf.Max(0f, Mathf.Cos(phase)) * 14f * flap;
            tipL.localRotation = Quaternion.Euler(0f, -fold, bend);
            tipR.localRotation = Quaternion.Euler(0f, fold, -bend);
            transform.localRotation = Quaternion.Euler(0f, 0f, -bank * 25f);

            // The body heaves with each beat, the tail whips, the legs swing, the head hunts about.
            float t = Time.time;
            if (tail != null)
            {
                tail.localRotation = Quaternion.Euler(Mathf.Sin(phase + 1f) * 6f * flap, Mathf.Sin(t * 0.9f) * 10f - bank * 12f, 0f);
                for (int i = 0; i < tailBones.Length; i++)
                    if (tailBones[i] != null) tailBones[i].localRotation = Quaternion.Euler(Mathf.Sin(phase + 1.6f + i * 0.7f) * 5f * flap, Mathf.Sin(t * 0.9f - i * 0.6f) * 9f, 0f);
            }
            if (legL != null)
            {
                legL.localRotation = Quaternion.Euler(70f + Mathf.Sin(t * 1.3f) * 8f + Mathf.Sin(phase) * 5f * flap, 0f, -6f);
                legR.localRotation = Quaternion.Euler(70f + Mathf.Sin(t * 1.3f + 1.4f) * 8f + Mathf.Sin(phase) * 5f * flap, 0f, 6f);
            }
            if (head != null) head.localRotation = Quaternion.Euler(Mathf.Sin(t * 0.7f) * 8f + 10f, Mathf.Sin(t * 0.45f) * 22f, 0f);
        }

        Transform Point(Transform parent, Vector3 at)
        {
            var p = new GameObject("Point").transform;
            p.SetParent(parent, false);
            p.localPosition = at;
            return p;
        }

        void Shape(PrimitiveType type, Transform parent, Vector3 at, Vector3 scale, Quaternion rot)
        {
            var go = GameObject.CreatePrimitive(type);
            Destroy(go.GetComponent<Collider>());
            go.layer = Game.Core.Layers.Effects;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = at;
            go.transform.localRotation = rot;
            go.transform.localScale = scale;
            var r = go.GetComponent<Renderer>();
            r.sharedMaterial = skin;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }
    }
}
