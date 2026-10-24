using Game.Core;
using Game.Rendering;
using UnityEngine;

namespace Game.Entities
{
    /// <summary>
    /// A black hole opening flat on the ground (a scour driven off falls into it): it spreads open, dark motes swirl
    /// round its edge and drift up out of it, and when it's done it shrinks shut and the motes fade.
    /// </summary>
    public sealed class GroundPortal : MonoBehaviour
    {
        const int Motes = 36;
        const float OpenTime = 0.35f, CloseTime = 0.45f;

        Material black, moteMat;
        Transform disc;
        readonly Transform[] motes = new Transform[Motes];
        readonly float[] moteAngle = new float[Motes], moteHeight = new float[Motes], moteSpeed = new float[Motes];
        float radius, age, closeAt = -1f;

        /// <summary>The hole is fully open (something can fall in now).</summary>
        public bool Open => age >= OpenTime && closeAt < 0f;

        public static GroundPortal Create(Vector3 ground, float radius)
        {
            var go = new GameObject("Ground portal") { layer = Layers.Effects };
            go.transform.position = ground + Vector3.up * 0.03f;
            var p = go.AddComponent<GroundPortal>();
            p.radius = radius;
            p.Build();
            p.Apply();
            return p;
        }

        void Build()
        {
            black = RuntimeMaterials.Unlit("Ground portal", Color.black, true);
            moteMat = RuntimeMaterials.Unlit("Portal motes", new Color(0.015f, 0.012f, 0.012f));
            var d = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            Destroy(d.GetComponent<Collider>());
            d.layer = Layers.Effects;
            d.transform.SetParent(transform, false);
            d.GetComponent<Renderer>().sharedMaterial = black;
            d.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            disc = d.transform;
            for (int i = 0; i < Motes; i++)
            {
                var m = GameObject.CreatePrimitive(PrimitiveType.Quad);
                Destroy(m.GetComponent<Collider>());
                m.layer = Layers.Effects;
                m.transform.SetParent(transform, false);
                var r = m.GetComponent<Renderer>();
                r.sharedMaterial = moteMat;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                motes[i] = m.transform;
                moteAngle[i] = Random.Range(0f, Mathf.PI * 2f);
                moteHeight[i] = Random.Range(0f, 2.5f);
                moteSpeed[i] = Random.Range(0.6f, 1.6f);
            }
        }

        /// <summary>Shrink shut, then go.</summary>
        public void Close()
        {
            if (closeAt < 0f) closeAt = age;
        }

        void OnDestroy()
        {
            if (black != null) Destroy(black);
            if (moteMat != null) Destroy(moteMat);
        }

        void Update()
        {
            age += Time.deltaTime;
            Apply();
            if (closeAt >= 0f && age - closeAt >= CloseTime) Destroy(gameObject);
        }

        void Apply()
        {
            float open = Mathf.SmoothStep(0f, 1f, age / OpenTime);
            if (closeAt >= 0f) open *= 1f - Mathf.SmoothStep(0f, 1f, (age - closeAt) / CloseTime);
            // The hole, with a slow ragged wobble to its edge.
            float wobble = 1f + 0.06f * Mathf.Sin(age * 7f);
            disc.localScale = new Vector3(radius * 2f * open * wobble, 0.002f, radius * 2f * open / wobble);
            disc.localRotation = Quaternion.Euler(0f, age * 40f, 0f);

            // Motes swirling in toward the edge and drifting up out of it, thinning out as they rise.
            var cam = Camera.main;
            for (int i = 0; i < Motes; i++)
            {
                moteAngle[i] += Time.deltaTime * moteSpeed[i] * 2.2f;
                moteHeight[i] += Time.deltaTime * moteSpeed[i] * 0.9f;
                if (moteHeight[i] > 2.6f) moteHeight[i] = 0f;
                float h = moteHeight[i];
                float r = radius * open * (1.15f - 0.35f * h / 2.6f);
                motes[i].localPosition = new Vector3(Mathf.Cos(moteAngle[i]) * r, h, Mathf.Sin(moteAngle[i]) * r);
                float size = 0.07f * open * (1f - h / 2.6f);
                motes[i].localScale = Vector3.one * size;
                if (cam != null) motes[i].rotation = Quaternion.LookRotation(motes[i].position - cam.transform.position);
            }
        }
    }
}
