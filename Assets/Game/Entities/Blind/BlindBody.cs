using System.Collections.Generic;
using Game.Rendering;
using UnityEngine;

namespace Game.Entities
{
    /// <summary>
    /// The blind one's body: the built-in one (the Tall Listener) or your own model. A starved, hunched giant with
    /// a smooth, eyeless head and two vast membranes of ear, on a long jointed neck folded down over bare shoulders.
    /// Its arms aren't on its body: they hang from a yoke of bone just under its head, reaching to the ground while
    /// the neck's folded, so it goes on four long limbs. The neck stretches, lifting the head (and the arms hanging
    /// under it) up over the trees to listen; the legs grow, three and a half times their length, for long slow
    /// strides; and the neck curls down to bring its face to yours.
    /// <para>Built in the mock-up's metres (6.1 m stood) under a model scaled to one unit tall, feet at the pivot,
    /// facing +Z; the blind one scales it to its height. Its feet are planted in the world where they land, and the
    /// legs bend to reach them, one lifting and reaching far ahead at a time.</para>
    /// </summary>
    public sealed class BlindBody : MonoBehaviour
    {
        const float Standing = 6.1f;          // (its height in the model's own metres)
        const int NeckSegments = 16;
        const float NeckShort = 0.11f;        // each vertebra, folded (model metres)
        const float LegRest = 1.45f;          // thigh and shin, at their own length

        Animator animator;
        Material skin, mouth, bone, membrane, claw;
        bool custom;

        Transform model, torso, head, jaw, armRoot;
        readonly Transform[] neck = new Transform[NeckSegments];
        readonly Transform[] neckBones = new Transform[NeckSegments];
        readonly Transform[] ears = new Transform[2];
        readonly Transform[] arms = new Transform[2];
        readonly Leg[] legs = new Leg[2];
        readonly List<Mesh> meshes = new List<Mesh>();

        float stretchShown, lookYaw, lookTo, nextLook, snapTilt, nextSnap;

        /// <summary>Its head held turned this way (degrees from where it faces), as at a sound; null: it looks round as it likes.</summary>
        public float? HoldLook;
        /// <summary>How far its arms spread when it roars (0: they hang, 1: flung wide).</summary>
        public float ArmRoar = 1f;
        /// <summary>Its head held tipped down this far (degrees), as at you far below; null: level.</summary>
        public float? HoldPitch;
        float pitchShown;

        sealed class Leg
        {
            public Transform hip, thigh, knee, shin, ankle;
            public Vector3 at, from, to;   // the foot (ankle), in the world
            public bool swing, placed;
            public float u;
        }

        int nextLeg;
        float swingFor = 0.6f;

        /// <summary>
        /// The heron's walk: each knee lifted high, the foot hanging limp, held there dead still while it listens,
        /// then reached forward and set down; the body all but stops while the leg's up, and the head pulls back and
        /// thrusts forward like a wading bird's, its legs grown or not (grown, each step slower). (Off: even strides.)
        /// </summary>
        public bool Heron = true;
        /// <summary>The heron's step: seconds lifting, holding still, and reaching and setting down.</summary>
        public float HeronLiftTime = 0.6f, HeronHoldTime = 0.8f, HeronReachTime = 0.75f;
        /// <summary>How high the heron lifts its foot (a share of a leg's length).</summary>
        public float HeronLift = 0.85f;
        /// <summary>
        /// How fast it's to move just now, as a share of its pace (the heron stops dead while its leg's up, and
        /// surges as it reaches): the blind one moves at its speed times this.
        /// </summary>
        public float Pace { get; private set; } = 1f;
        /// <summary>A foot lifting (where): for its creak.</summary>
        public System.Action<Vector3> FootUp;
        /// <summary>Its neck starting up over the trees (where its head is): for the sound of it.</summary>
        public System.Action<Vector3> NeckRising;
        /// <summary>Its head up over the trees (the neck most of the way stretched).</summary>
        public bool Raised => stretchShown > 0.8f;
        float nominalSpeed, thrust, thrustV;
        float stretchAsked;
        Vector3 lastPos;
        bool lastPosSet;

        /// <summary>A foot coming down: where, and how big the legs were (0 their own length, 1 grown).</summary>
        public System.Action<Vector3, float> FootDown;

        /// <summary>Its own planted feet (false for your model: then the blind one times its steps itself).</summary>
        public bool HasFeet => !custom;

        /// <summary>How far its neck can reach above its shoulders when stretched (metres, world).</summary>
        public float NeckReach { get; set; } = 16f;

        /// <summary>How dark its head and neck show through the fog when they're up over the trees (0 not at all, 1 black).</summary>
        public float FogShow { get; set; } = 0.8f;

        Material fogShape;

        // Its neck, head and arms again, as a silhouette the fog doesn't touch: up over the trees in the distance
        // they'd be lost in the fog; this darkens the fog where they are, so they show against it (trees in front
        // still hide them).
        void ThroughFog()
        {
            fogShape = RuntimeMaterials.Silhouette("Blind one through the fog");
            RuntimeMaterials.SetSilhouetteShade(fogShape, 1f);
            foreach (var r in neck[0].GetComponentsInChildren<MeshRenderer>())
            {
                var filter = r.GetComponent<MeshFilter>();
                if (filter == null || filter.sharedMesh == null) continue;
                var go = new GameObject("Through the fog") { layer = r.gameObject.layer };
                go.transform.SetParent(r.transform, false);
                go.AddComponent<MeshFilter>().sharedMesh = filter.sharedMesh;
                var shape = go.AddComponent<MeshRenderer>();
                shape.sharedMaterial = fogShape;
                shape.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                shape.receiveShadows = false;
            }
        }

        // How much it shows through the fog now: by how far up its head is, and how far off it is (near, the fog's
        // thin and it shows as itself).
        void ShowThroughFog()
        {
            if (fogShape == null) return;
            var cam = Camera.main;
            float far = cam != null ? Mathf.Clamp01((Vector3.Distance(cam.transform.position, HeadWorld) - 12f) / 30f) : 1f;
            float up = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.5f, 1f, stretchShown));
            RuntimeMaterials.SetSilhouetteShade(fogShape, 1f - Mathf.Clamp01(FogShow) * up * far);
        }

        /// <summary>How many times their length its legs grow to, striding.</summary>
        public float Growth { get; set; } = 3.6f;

        public static BlindBody Create(Transform parent, BlindConfig config)
        {
            var go = new GameObject("Blind one body") { layer = Game.Core.Layers.Entities };
            go.transform.SetParent(parent, false);
            var b = go.AddComponent<BlindBody>();
            b.skin = RuntimeMaterials.Lit("Blind one skin", new Color(0.05f, 0.046f, 0.043f), 0.4f);
            b.mouth = RuntimeMaterials.Glow("Blind one mouth", new Color(0.5f, 0.02f, 0.01f));
            if (config.customModel != null) b.UseYours(config.customModel);
            else
            {
                b.Build();
                b.ThroughFog();
            }
            return b;
        }

        void OnDestroy()
        {
            foreach (var m in new[] { skin, mouth, bone, membrane, claw, fogShape }) if (m != null) Destroy(m);
            foreach (var m in meshes) if (m != null) Destroy(m);
        }

        /// <summary>The top of its head, in its own space (one unit tall): for your model.</summary>
        public Vector3 HeadLocal => new Vector3(0f, 0.9f, 0.18f);

        /// <summary>Where its head is now, in the world.</summary>
        public Vector3 HeadWorld => head != null ? head.position : transform.TransformPoint(HeadLocal);

        void UseYours(GameObject prefab)
        {
            custom = true;
            var yours = Instantiate(prefab, transform, false);
            foreach (var c in yours.GetComponentsInChildren<Collider>()) Destroy(c);
            var renderers = yours.GetComponentsInChildren<Renderer>();
            if (renderers.Length > 0)
            {
                var b = renderers[0].bounds;
                foreach (var r in renderers) b.Encapsulate(r.bounds);
                if (b.size.y > 0.001f) yours.transform.localScale = yours.transform.localScale / b.size.y;
            }
            animator = yours.GetComponentInChildren<Animator>();
        }

        // ------------------------------------------------------------------ building

        void Build()
        {
            bone = RuntimeMaterials.Lit("Blind one bone", new Color(0.3f, 0.27f, 0.23f), 0.45f);
            membrane = RuntimeMaterials.Lit("Blind one membrane", new Color(0.2f, 0.07f, 0.065f), 0.55f, true);
            claw = RuntimeMaterials.Lit("Blind one claw", new Color(0.02f, 0.018f, 0.016f), 0.8f);
            model = Joint(transform, Vector3.zero);
            model.localScale = Vector3.one / Standing;
            var M = skin;

            // The torso: hips, a gaunt belly, a spine curving forward into the hunch, ribs showing.
            torso = Joint(model, Vector3.zero);
            Ball(torso, new Vector3(0f, 3.0f, 0f), new Vector3(0.42f, 0.26f, 0.28f), M);
            Ball(torso, new Vector3(0f, 3.55f, 0.12f), new Vector3(0.26f, 0.38f, 0.22f), M);
            Rod(torso, new Vector3(0f, 3.0f, -0.05f), new Vector3(0f, 3.9f, 0.1f), 0.125f, M);
            Rod(torso, new Vector3(0f, 3.9f, 0.1f), new Vector3(0f, 4.7f, 0.45f), 0.135f, M);
            Rod(torso, new Vector3(0f, 4.7f, 0.45f), new Vector3(0f, 5.15f, 0.85f), 0.13f, M);
            for (int k = 0; k < 10; k++) // (the spine's knobs, down its back)
            {
                float u = k / 9f;
                Vector3 p = Vector3.Lerp(new Vector3(0f, 3.1f, -0.15f), new Vector3(0f, 5.1f, 0.7f), u) + new Vector3(0f, 0f, -0.1f * Mathf.Sin(u * Mathf.PI));
                Ball(torso, p, new Vector3(0.07f, 0.055f, 0.07f), bone);
            }
            for (int k = 0; k < 6; k++) // (ribs, each side, from the spine round to the front)
            {
                float y = Mathf.Lerp(3.95f, 4.75f, k / 5f);
                float zc = Mathf.Lerp(0.2f, 0.6f, k / 5f);
                float w = 0.48f * (1f - Mathf.Pow(k / 5f - 0.35f, 2f) * 0.9f);
                foreach (float s in new[] { -1f, 1f })
                {
                    Vector3 a = new Vector3(s * 0.06f, y + 0.05f, zc - 0.25f), m = new Vector3(s * w, y - 0.03f, zc + 0.02f), b = new Vector3(s * w * 0.55f, y - 0.12f, zc + 0.32f);
                    Rod(torso, a, m, 0.035f, bone);
                    Rod(torso, m, b, 0.03f, bone);
                }
            }

            // The neck: a chain of vertebrae, each one a joint whose bone lengthens as it stretches.
            Transform parent = Joint(torso, new Vector3(0f, 5.15f, 0.85f));
            for (int i = 0; i < NeckSegments; i++)
            {
                var seg = Joint(parent, i == 0 ? Vector3.zero : new Vector3(0f, NeckShort, 0f));
                float r = 0.12f - i * 0.0028f;
                var boneShape = Shape(PrimitiveType.Cylinder, seg, new Vector3(0f, NeckShort * 0.5f, 0f), new Vector3(r * 2f, NeckShort * 0.5f, r * 2f), M);
                Ball(seg, Vector3.zero, new Vector3(r * 1.25f, r * 0.9f, r * 1.25f), M);
                if (i % 2 == 0) Ball(seg, new Vector3(0f, NeckShort * 0.4f, -r), new Vector3(0.04f, 0.04f, 0.05f), bone); // (a ridge down its back)
                neck[i] = seg;
                neckBones[i] = boneShape.transform;
                parent = seg;
            }

            // The head: long, smooth and eyeless (skin over where the sockets would be), rows of pits it hears
            // through glowing faintly, slits for a nose, a lipless mouth with teeth, and the great ears.
            head = Joint(parent, new Vector3(0f, NeckShort + 0.08f, 0.05f));
            Ball(head, new Vector3(0f, 0.05f, 0.05f), new Vector3(0.36f, 0.42f, 0.52f), M);
            Ball(head, new Vector3(0f, 0.18f, 0.28f), new Vector3(0.3f, 0.16f, 0.3f), M);
            foreach (float s in new[] { -1f, 1f })
            {
                Ball(head, new Vector3(s * 0.14f, 0.12f, 0.47f), new Vector3(0.09f, 0.055f, 0.04f), bone);           // skin over the sockets
                Ball(head, new Vector3(s * 0.04f, -0.02f, 0.56f), new Vector3(0.012f, 0.045f, 0.02f), claw);        // nose slits
                for (int k = 0; k < 5; k++)
                    Ball(head, new Vector3(s * (0.2f + k * 0.012f), 0.25f - k * 0.07f, 0.3f - k * 0.02f), Vector3.one * 0.022f, mouth); // the pits
            }
            jaw = Joint(head, new Vector3(0f, -0.2f, 0.1f));
            Ball(jaw, new Vector3(0f, -0.06f, 0.2f), new Vector3(0.26f, 0.12f, 0.36f), M);
            Ball(head, new Vector3(0f, -0.17f, 0.42f), new Vector3(0.17f, 0.05f, 0.08f), mouth);
            for (int k = 0; k < 9; k++)
            {
                float x = (k - 4) * 0.035f;
                Box(head, new Vector3(x, -0.165f, 0.44f - Mathf.Abs(x) * 0.4f), new Vector3(0.018f, 0.07f, 0.014f), bone, Quaternion.Euler(-8f, 0f, 0f));
                Box(jaw, new Vector3(x * 0.95f, 0.03f, 0.32f - Mathf.Abs(x) * 0.4f), new Vector3(0.016f, 0.06f, 0.013f), bone, Quaternion.Euler(8f, 0f, 0f));
            }
            for (int e = 0; e < 2; e++)
            {
                float s = e == 0 ? -1f : 1f;
                var ear = Joint(head, new Vector3(s * 0.3f, 0.12f, -0.05f));
                Ear(ear, s);
                ear.localRotation = Quaternion.Euler(-14f, s * 32f, s * -14f);
                ears[e] = ear;
            }

            // The arms: hanging from a yoke of bone just under the head, so they go wherever it goes.
            armRoot = Joint(parent, new Vector3(0f, NeckShort * 0.4f, -0.05f));
            Rod(armRoot, new Vector3(-0.42f, 0f, 0f), new Vector3(0.42f, 0f, 0f), 0.07f, M);
            for (int a = 0; a < 2; a++)
            {
                float s = a == 0 ? -1f : 1f;
                Ball(armRoot, new Vector3(s * 0.45f, 0f, 0f), Vector3.one * 0.13f, M);
                arms[a] = Arm(armRoot, new Vector3(s * 0.45f, 0f, 0f), s, 1.55f, M);
            }

            // The legs: hip, knee and ankle joints, bones that lengthen.
            for (int l = 0; l < 2; l++)
            {
                float s = l == 0 ? -1f : 1f;
                var leg = new Leg();
                leg.hip = Joint(torso, new Vector3(s * 0.35f, 3.0f, 0f));
                Ball(leg.hip, Vector3.zero, Vector3.one * 0.17f, M);
                leg.thigh = Shape(PrimitiveType.Cylinder, leg.hip, new Vector3(0f, -LegRest * 0.5f, 0f), new Vector3(0.3f, LegRest * 0.5f, 0.3f), M).transform;
                leg.knee = Joint(leg.hip, new Vector3(0f, -LegRest, 0f));
                Ball(leg.knee, Vector3.zero, Vector3.one * 0.12f, M);
                leg.shin = Shape(PrimitiveType.Cylinder, leg.knee, new Vector3(0f, -LegRest * 0.5f, 0f), new Vector3(0.18f, LegRest * 0.5f, 0.18f), M).transform;
                leg.ankle = Joint(leg.knee, new Vector3(0f, -LegRest, 0f));
                Ball(leg.ankle, Vector3.zero, Vector3.one * 0.08f, M);
                Rod(leg.ankle, Vector3.zero, new Vector3(s * 0.04f, -0.19f, 0.45f), 0.07f, M);
                for (int t = -1; t <= 1; t++)
                    Box(leg.ankle, new Vector3(s * 0.04f + t * 0.07f, -0.2f, 0.56f), new Vector3(0.04f, 0.04f, 0.2f), claw, Quaternion.Euler(10f, t * 12f, 0f));
                legs[l] = leg;
            }
        }

        // An arm hanging from `at`: long thin bones down to a hand of four long curled fingers (k: longer).
        Transform Arm(Transform parent, Vector3 at, float s, float k, Material m)
        {
            var arm = Joint(parent, at);
            Vector3 elbow = new Vector3(s * 0.18f, -1.35f * k, 0.2f), wrist = new Vector3(s * 0.1f, -2.85f * k, 0.42f);
            Rod(arm, Vector3.zero, elbow, 0.09f, m);
            Ball(arm, elbow, Vector3.one * 0.1f, bone);
            Rod(arm, elbow, wrist, 0.065f, m);
            Ball(arm, wrist + new Vector3(0f, -0.1f, 0.04f), new Vector3(0.1f, 0.06f, 0.13f), m);
            for (int f = 0; f < 4; f++)
            {
                Vector3 p = wrist + new Vector3(s * (f - 1.5f) * 0.05f, -0.15f, 0.08f);
                Vector3 d = new Vector3(s * (f - 1.5f) * 0.12f, -0.6f, 0.6f).normalized;
                float seg = 0.75f * Mathf.Sqrt(k) / 3f;
                for (int j = 0; j < 3; j++)
                {
                    Vector3 next = p + d * seg;
                    Rod(arm, p, next, 0.025f * (1f - j * 0.2f), m);
                    p = next;
                    d = (d + Vector3.down * 0.35f).normalized;
                }
                Box(arm, p + d * 0.05f, new Vector3(0.025f, 0.025f, 0.12f), claw, Quaternion.LookRotation(d));
            }
            return arm;
        }

        // An ear: a thin membrane fanned out from the side of the head, its edge scalloped, ribbed with bone.
        void Ear(Transform ear, float s)
        {
            Vector2[] edge =
            {
                new Vector2(0.35f, -0.55f), new Vector2(0.75f, -0.35f), new Vector2(1.05f, 0.05f), new Vector2(1.2f, 0.5f),
                new Vector2(1.05f, 0.95f), new Vector2(0.7f, 1.25f), new Vector2(0.3f, 1.2f), new Vector2(0.05f, 0.75f),
            };
            var verts = new List<Vector3> { new Vector3(0f, 0.1f, 0f) };
            Vector2 prev = new Vector2(0f, -0.25f);
            verts.Add(new Vector3(prev.x * s, prev.y, 0f));
            foreach (var p in edge)
            {
                Vector2 mid = (prev + p) * 0.5f * 0.86f; // (each edge bowing in: scalloped)
                verts.Add(new Vector3(mid.x * s, mid.y, 0f));
                verts.Add(new Vector3(p.x * s, p.y, 0f));
                prev = p;
            }
            verts.Add(new Vector3(0f, 0.3f, 0f));
            var tris = new List<int>();
            for (int i = 1; i < verts.Count - 1; i++) { tris.Add(0); tris.Add(i); tris.Add(i + 1); }
            var mesh = new Mesh { name = "Blind one ear" };
            mesh.SetVertices(verts);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            meshes.Add(mesh);
            var go = new GameObject("Ear membrane") { layer = Game.Core.Layers.Entities };
            go.transform.SetParent(ear, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = membrane;
            for (int b = 0; b < 4; b++)
            {
                var tip = edge[Mathf.Min(7, 2 + b * 2)];
                Rod(ear, new Vector3(0f, 0f, 0.01f), new Vector3(s * tip.x * 0.95f, tip.y * 0.95f, 0.01f), 0.025f, bone);
            }
        }

        // ------------------------------------------------------------------ posing

        /// <summary>
        /// The pose for this frame. <paramref name="speed"/> m/s (for your model's animator; the built-in one walks
        /// by where it's going); <paramref name="listen"/> 0-1 cocking its head; <paramref name="roar"/> 0-1 mouth
        /// wide, head up, arms out; <paramref name="lean"/> 0-1 its neck curling down to bring its face to yours;
        /// <paramref name="stretch"/> 0-1 the neck lifting its head up over the trees (it eases there);
        /// <paramref name="grow"/> 0-1 its legs grown for striding.
        /// </summary>
        public void Pose(float speed, float listen, float roar, float lean, float stretch, float grow, float dt)
        {
            if (custom)
            {
                if (animator != null)
                {
                    animator.SetFloat("Speed", speed);
                    animator.SetBool("Listen", listen > 0.5f);
                }
                return;
            }
            if (model == null || dt <= 0f) return;
            float t = Time.time;
            float g = grow * grow * (3f - 2f * grow);
            float legLen = LegRest * (1f + (Growth - 1f) * g);
            nominalSpeed = speed;
            // (The neck starting up: once each time it's asked up from low.)
            if (stretch >= 0.95f && stretchAsked < 0.95f && stretchShown < 0.5f) NeckRising?.Invoke(HeadWorld);
            stretchAsked = stretch;
            Legs(legLen, g, dt);
            Neck(listen, roar, lean, stretch, dt, t);
            ShowThroughFog();
        }

        // The neck and everything hanging from it: head, ears, jaw, arms.
        void Neck(float listen, float roar, float lean, float stretch, float dt, float t)
        {
            // (Past 1, further than NeckReach: the neck runs on up, as far again for each 1 over; it gets there as
            // quickly as to 1.)
            stretch = Mathf.Max(0f, stretch);
            stretchShown = Mathf.MoveTowards(stretchShown, stretch, dt * Mathf.Max(1f, Mathf.Max(stretch, stretchShown)) / (stretch > stretchShown ? 3.2f : 2.6f));
            float upTo = Mathf.Min(1f, stretchShown);
            float e = upTo * upTo * (3f - 2f * upTo);                       // (0-1: how far up; the sway and the fold go by this)
            float reach = e + Mathf.Max(0f, stretchShown - 1f);            // (and past 1, the neck running on up)
            float scale = Mathf.Max(0.1f, transform.lossyScale.y) / Standing; // (world metres per model metre)
            float longest = NeckReach / scale / NeckSegments;
            float len = Mathf.LerpUnclamped(NeckShort, Mathf.Max(NeckShort, longest), reach);
            len = Mathf.Lerp(len, NeckShort * 2.6f, lean);                     // (reaching down to you)
            float total = 0f, sway = 0f;
            for (int i = 0; i < NeckSegments; i++)
            {
                var seg = neck[i];
                if (i > 0) seg.localPosition = new Vector3(0f, len, 0f); // (all the vertebrae lengthen together)
                neckBones[i].localScale = new Vector3(neckBones[i].localScale.x, len * 0.5f, neckBones[i].localScale.z);
                neckBones[i].localPosition = new Vector3(0f, len * 0.5f, 0f);
                // Folded: curling forward and down over the shoulders. Stretched: up, a slow S of a sway. Leaning:
                // curling right over and down to your face. Roaring: thrown up.
                float fold = (i < 5 ? 0.32f : 0.06f) * (1f - e) * (1f - roar * 0.7f);
                float x = fold + lean * 0.17f + (i < 3 ? thrust * 0.05f * (1f - e) : 0f); // (the heron's head bob, folded only)
                float z = e * Mathf.Sin(t * 0.6f - i * 0.35f) * 0.035f / Mathf.Max(1f, reach); // (a longer neck: a gentler sway, or the head would swing a mile)
                seg.localRotation = Quaternion.Euler(x * Mathf.Rad2Deg, 0f, z * Mathf.Rad2Deg);
                total += x;
                sway += z;
            }
            armRoot.localPosition = new Vector3(0f, len * 0.4f, -0.05f);

            // The head kept level whatever the neck's doing; up there it looks round, stopping dead; listening
            // it cocks over, and now and then snaps toward a sound.
            if (HoldLook.HasValue) lookTo = Mathf.Clamp(HoldLook.Value, -80f, 80f);
            else if (e > 0.8f)
            {
                if (t > nextLook) { nextLook = t + Random.Range(1.2f, 2.8f); lookTo = Random.Range(-75f, 75f); }
            }
            else lookTo = 0f;
            lookYaw = Mathf.Lerp(lookYaw, lookTo, 1f - Mathf.Exp(-(HoldLook.HasValue ? 8f : e > 0.8f ? 2.5f : 1.5f) * dt)); // (snapping round to a sound)
            if (t > nextSnap) { nextSnap = t + Random.Range(2.5f, 5.5f); snapTilt = Random.Range(-30f, 30f); }
            float tilt = (snapTilt + Mathf.Sin(t * 0.7f) * 7f) * listen;
            pitchShown = Mathf.Lerp(pitchShown, HoldPitch.HasValue ? Mathf.Clamp(HoldPitch.Value, -20f, 75f) : 0f, 1f - Mathf.Exp(-3f * dt)); // (peering down at you)
            head.localRotation = Quaternion.Euler(-total * Mathf.Rad2Deg + Mathf.Sin(t * 0.4f) * 4f - roar * 25f + lean * 10f + pitchShown, lookYaw, tilt - sway * Mathf.Rad2Deg);
            jaw.localRotation = Quaternion.Euler(6f + Mathf.Max(roar, lean * 0.6f) * 32f + Mathf.Max(0f, Mathf.Sin(t * 0.5f)) * 8f, 0f, 0f);
            for (int i = 0; i < 2; i++)
            {
                float s = i == 0 ? -1f : 1f;
                float twitch = Mathf.Sin(t * (1.3f - i * 0.2f) + i) * 7f * (0.4f + listen);
                ears[i].localRotation = Quaternion.Euler(-14f - roar * 12f, s * (32f + twitch + roar * 20f) + snapTilt * 0.3f * listen, s * -14f);
            }

            // The arms hang straight down, swinging a little; spread wide to roar; reaching for you as it leans.
            armRoot.localRotation = Quaternion.Euler(-total * Mathf.Rad2Deg + Mathf.Sin(t * 0.8f) * 2.5f * (0.3f + e) - lean * 25f, 0f, -sway * Mathf.Rad2Deg);
            for (int i = 0; i < 2; i++)
            {
                float s = i == 0 ? -1f : 1f;
                arms[i].localRotation = Quaternion.Euler(Mathf.Sin(t * 0.6f + i * 1.5f) * 3f - roar * ArmRoar * 35f, 0f, s * roar * ArmRoar * 40f);
            }
        }

        // ------------------------------------------------------------------ the legs

        // Each foot stays planted where it came down while the body moves over it; one at a time lifts, draws up,
        // reaches far ahead and sets down. The legs bend to reach them (two bones, in the leg's own plane).
        void Legs(float len, float g, float dt)
        {
            Vector3 pos = transform.position;
            if (!lastPosSet) { lastPos = pos; lastPosSet = true; }
            Vector3 vel = (pos - lastPos) / Mathf.Max(dt, 1e-4f);
            lastPos = pos;
            vel.y = 0f;
            float scale = Mathf.Max(0.1f, transform.lossyScale.y) / Standing;
            float speed = vel.magnitude;                       // (world metres a second)
            float stride = 1.35f * len * scale;                // (world)
            Vector3 fwd = speed > 0.05f ? vel / speed : transform.forward;
            // (Walking by the pace it's asked for, not how fast it's going: the heron stands still mid-step.)
            bool walking = Mathf.Max(speed, nominalSpeed) > 0.15f;
            bool heron = Heron && walking;
            float slow = 1f + 0.8f * g; // (grown, its huge legs take longer over each step)
            if (Vector3.Dot(transform.forward, fwd) < 0.2f || speed < 0.05f) fwd = transform.forward;

            for (int i = 0; i < 2; i++)
                if (!legs[i].placed) { legs[i].at = Ground(UnderHip(i)); legs[i].placed = true; }

            // Lift a foot when neither's in the air: walking, the one further behind; standing, one that's strayed.
            if (!legs[0].swing && !legs[1].swing)
            {
                int pick = -1;
                if (walking)
                {
                    float behind0 = Vector3.Dot(legs[0].at - pos, fwd), behind1 = Vector3.Dot(legs[1].at - pos, fwd);
                    int back = behind0 < behind1 ? 0 : 1;
                    if (Mathf.Min(behind0, behind1) < stride * 0.15f) pick = back;
                }
                else
                {
                    for (int i = 0; i < 2; i++)
                        if (Flat(legs[i].at - UnderHip(i)).magnitude > 0.35f * scale * 1.5f + 0.1f) { pick = i; break; }
                }
                if (pick >= 0)
                {
                    var leg = legs[pick];
                    leg.swing = true;
                    leg.u = 0f;
                    leg.from = leg.at;
                    swingFor = walking ? Mathf.Clamp(stride / Mathf.Max(speed, 0.5f) * 0.5f, 0.3f, 1.8f) : 0.45f;
                    Vector3 side = transform.right * (pick == 0 ? -0.35f : 0.35f) * scale;
                    Vector3 to = walking ? pos + fwd * (speed * swingFor + stride * 0.5f) + side : UnderHip(pick);
                    if (heron)
                    {
                        // (Where the body will have got to by the time it's down: slow lifting, still holding, surging on.)
                        swingFor = (HeronLiftTime + HeronHoldTime + HeronReachTime) * slow;
                        float travel = nominalSpeed * (0.35f * HeronLiftTime + 0.05f * HeronHoldTime + 1.4f * HeronReachTime) * slow;
                        to = pos + fwd * (travel + stride * 0.45f) + side;
                    }
                    leg.to = Ground(to);
                    if (walking) FootUp?.Invoke(leg.at);
                }
            }
            // The heron's pace: slow while a knee's lifting, still while it holds, surging as the foot reaches out.
            float paceWant = 1f, phaseThrust = 0f;
            if (heron)
            {
                paceWant = 0.6f;
                float a = HeronLiftTime * slow / swingFor, b = (HeronLiftTime + HeronHoldTime) * slow / swingFor;
                foreach (var leg in legs)
                {
                    if (!leg.swing) continue;
                    paceWant = leg.u < a ? 0.35f : leg.u < b ? 0.05f : 1.4f;
                    phaseThrust = leg.u < a ? -0.4f : leg.u < b ? -1f : 1f;
                }
            }
            Pace = Mathf.MoveTowards(Pace, paceWant, dt * 4f);
            // (The head: pulled back as the knee comes up and while it holds, thrust forward as it steps.)
            thrustV += (phaseThrust - thrust) * 60f * dt;
            thrustV *= Mathf.Exp(-10f * dt);
            thrust += thrustV * dt;
            for (int i = 0; i < 2; i++)
            {
                var leg = legs[i];
                if (!leg.swing) continue;
                leg.u = Mathf.Min(1f, leg.u + dt / swingFor);
                if (heron)
                {
                    // Lifted straight up, the knee high, hardly forward; held there; then reached out and set down.
                    float a = HeronLiftTime * slow / swingFor, b = (HeronLiftTime + HeronHoldTime) * slow / swingFor;
                    float along, up;
                    if (leg.u < a) { float k = leg.u / a; along = 0.1f * k * k * (3f - 2f * k); up = 1f - (1f - k) * (1f - k); }
                    else if (leg.u < b) { float k = (leg.u - a) / (b - a); along = 0.1f + 0.04f * k; up = 1f + 0.03f * Mathf.Sin(k * Mathf.PI); }
                    else { float k = (leg.u - b) / (1f - b); float sk = k * k * (3f - 2f * k); along = 0.14f + 0.86f * sk; up = 1f - Mathf.Pow(sk, 1.6f); }
                    leg.at = Vector3.Lerp(leg.from, leg.to, along);
                    leg.at.y = Mathf.Lerp(leg.from.y, leg.to.y, along) + len * scale * HeronLift * up;
                }
                else
                {
                // Up first, the knee drawn high; then reaching far out ahead; then down.
                float along = Mathf.Pow(Mathf.Max(0f, (leg.u - 0.15f) / 0.85f), 1.5f);
                along = along * along * (3f - 2f * along);
                leg.at = Vector3.Lerp(leg.from, leg.to, along);
                float lift = len * scale * (walking ? 0.6f : 0.25f);
                leg.at.y = Mathf.Lerp(leg.from.y, leg.to.y, along) + lift * Mathf.Sin(Mathf.PI * Mathf.Pow(leg.u, 0.7f));
                }
                if (leg.u >= 1f)
                {
                    leg.swing = false;
                    leg.at = leg.to;
                    nextLeg = 1 - i;
                    FootDown?.Invoke(leg.at, g);
                }
            }

            // The body: high on its legs, dipping a little as a leg reaches out.
            float reaching = 0f;
            foreach (var leg in legs) if (leg.swing) reaching = Mathf.Max(reaching, Mathf.Sin(Mathf.PI * leg.u));
            float w = walking ? 1f : 0f;
            float hipH = len * (1.93f - 0.22f * w - 0.1f * reaching * w) + 0.2f;
            torso.localPosition = new Vector3(0f, hipH - 3.0f, 0f);
            for (int i = 0; i < 2; i++)
            {
                var leg = legs[i];
                Vector3 local = model.InverseTransformPoint(leg.at);
                Vector3 hip = new Vector3(i == 0 ? -0.35f : 0.35f, hipH, 0f);
                float dy = local.y - hip.y, dz = local.z - hip.z;
                float d = Mathf.Min(Mathf.Sqrt(dy * dy + dz * dz), len * 1.995f);
                float baseAngle = Mathf.Atan2(dz, -dy);
                float alpha = Mathf.Acos(Mathf.Clamp01(d / (2f * len)));
                // (A heron's foot hangs limp from the ankle, toes down, while it's up.)
                float hang = heron && leg.swing ? 60f * Mathf.Sin(Mathf.PI * Mathf.Min(1f, leg.u * 1.15f)) : 0f;
                SetLeg(leg, len, -(baseAngle + alpha), 2f * alpha, hang);
            }
        }

        static void SetLeg(Leg leg, float len, float a1, float a2, float hang = 0f)
        {
            leg.thigh.localScale = new Vector3(leg.thigh.localScale.x, len * 0.5f, leg.thigh.localScale.z);
            leg.thigh.localPosition = new Vector3(0f, -len * 0.5f, 0f);
            leg.knee.localPosition = new Vector3(0f, -len, 0f);
            leg.shin.localScale = new Vector3(leg.shin.localScale.x, len * 0.5f, leg.shin.localScale.z);
            leg.shin.localPosition = new Vector3(0f, -len * 0.5f, 0f);
            leg.ankle.localPosition = new Vector3(0f, -len, 0f);
            leg.hip.localRotation = Quaternion.Euler(a1 * Mathf.Rad2Deg, 0f, 0f);
            leg.knee.localRotation = Quaternion.Euler(a2 * Mathf.Rad2Deg, 0f, 0f);
            leg.ankle.localRotation = Quaternion.Euler(-(a1 + a2) * Mathf.Rad2Deg + hang, 0f, 0f); // (the foot flat, or hanging)
        }

        // Where a foot stands under a hip (in the world).
        Vector3 UnderHip(int i) => model.TransformPoint(new Vector3(i == 0 ? -0.35f : 0.35f, 0f, 0.08f));

        // On the ground there (the ankle 0.2 model-metres up); or, while it's down in the ground (breaking out of
        // it), level with its own feet, so its legs don't reach up through the surface.
        Vector3 Ground(Vector3 p)
        {
            float scale = Mathf.Max(0.1f, transform.lossyScale.y) / Standing;
            float ground = PlayerSense.GroundHeight(p), own = transform.position.y;
            p.y = (ground > own + 1.5f * scale + 0.5f ? own : ground) + 0.2f * scale;
            return p;
        }

        /// <summary>Put its feet back under it (it's just been moved: risen out of the ground, come back).</summary>
        public void ResetFeet()
        {
            foreach (var leg in legs) if (leg != null) { leg.placed = false; leg.swing = false; }
            lastPosSet = false;
        }

        static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0f, v.z);

        public void Trigger(string name)
        {
            if (animator != null) animator.SetTrigger(name);
        }

        // ------------------------------------------------------------------ parts

        Transform Joint(Transform parent, Vector3 at)
        {
            var j = new GameObject("Joint").transform;
            j.SetParent(parent, false);
            j.localPosition = at;
            return j;
        }

        GameObject Shape(PrimitiveType type, Transform parent, Vector3 at, Vector3 scale, Material m)
        {
            var go = GameObject.CreatePrimitive(type);
            Destroy(go.GetComponent<Collider>());
            go.layer = Game.Core.Layers.Entities;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = at;
            go.transform.localScale = scale;
            go.GetComponent<Renderer>().sharedMaterial = m;
            return go;
        }

        // A ball of these radii.
        void Ball(Transform parent, Vector3 at, Vector3 radii, Material m) => Shape(PrimitiveType.Sphere, parent, at, radii * 2f, m);

        void Box(Transform parent, Vector3 at, Vector3 size, Material m, Quaternion rotation)
            => Shape(PrimitiveType.Cube, parent, at, size, m).transform.localRotation = rotation;

        // A rounded bone from a to b, this thick (radius).
        void Rod(Transform parent, Vector3 a, Vector3 b, float radius, Material m)
        {
            Vector3 d = b - a;
            if (d.sqrMagnitude < 1e-8f) return;
            var go = Shape(PrimitiveType.Capsule, parent, (a + b) * 0.5f, new Vector3(radius * 2f, d.magnitude * 0.5f + radius, radius * 2f), m);
            go.transform.localRotation = Quaternion.FromToRotation(Vector3.up, d);
        }
    }
}
