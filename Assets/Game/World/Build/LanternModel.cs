using System.Collections.Generic;
using Game.Core;
using Game.Rendering;
using UnityEngine;

namespace Game.World.Build
{
    /// <summary>
    /// A small hurricane lantern, made of simple shapes: a tin base and cap, a glass globe in a wire guard, a bail
    /// handle over the top. It stands on its base, or hangs by its bail from a cord. With a <see cref="Lantern"/>
    /// on it, so it clicks on and off.
    /// </summary>
    public static class LanternModel
    {
        /// <summary>Standing on a table or shelf: its base at <paramref name="at"/>.</summary>
        public static Lantern Standing(Transform parent, Vector3 at, float yaw, Material tin, Material wire, List<Object> made, float range = 4.5f, float intensity = 2.2f)
        {
            var root = Make(parent, at, yaw, tin, wire, made, false, 0f);
            var lantern = root.GetComponent<Lantern>();
            lantern.range = range;
            lantern.intensity = intensity;
            return lantern;
        }

        /// <summary>Hanging from a cord <paramref name="cord"/> long below <paramref name="hook"/>.</summary>
        public static Lantern Hanging(Transform parent, Vector3 hook, float cord, Material tin, Material wire, List<Object> made, float range = 3.2f, float intensity = 1.6f)
        {
            var root = Make(parent, hook + Vector3.down * (cord + 0.26f), 0f, tin, wire, made, true, cord);
            var lantern = root.GetComponent<Lantern>();
            lantern.range = range;
            lantern.intensity = intensity;
            return lantern;
        }

        // Its parts above its base (local y): base 0-0.03, globe 0.03-0.17, cap 0.17-0.2, bail up to 0.26.
        static GameObject Make(Transform parent, Vector3 at, float yaw, Material tin, Material wire, List<Object> made, bool hanging, float cord)
        {
            var root = new GameObject("Lantern") { layer = Layers.Props };
            root.transform.SetParent(parent, false);
            root.transform.localPosition = at;
            root.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
            var t = root.transform;
            Part(PrimitiveType.Cylinder, t, new Vector3(0f, 0.015f, 0f), new Vector3(0.085f, 0.015f, 0.085f), tin);       // base
            Part(PrimitiveType.Cylinder, t, new Vector3(0f, 0.035f, 0f), new Vector3(0.07f, 0.005f, 0.07f), wire);       // (its rim)
            var glassMat = RuntimeMaterials.LitGlow("Lantern glass", new Color(0.5f, 0.45f, 0.35f), Color.black);
            made.Add(glassMat);
            var globe = Part(PrimitiveType.Sphere, t, new Vector3(0f, 0.1f, 0f), new Vector3(0.07f, 0.13f, 0.07f), glassMat);
            globe.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            for (int i = 0; i < 4; i++) // (the wire guard round the globe)
            {
                float a = i * Mathf.PI * 0.5f + 0.4f;
                var off = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * 0.04f;
                Rod(t, off + Vector3.up * 0.035f, off * 0.8f + Vector3.up * 0.17f, 0.0018f, wire);
            }
            Part(PrimitiveType.Cylinder, t, new Vector3(0f, 0.18f, 0f), new Vector3(0.06f, 0.012f, 0.06f), tin);        // cap
            Part(PrimitiveType.Sphere, t, new Vector3(0f, 0.2f, 0f), new Vector3(0.05f, 0.025f, 0.05f), tin);            // (its dome)
            // The bail: a wire loop over the top.
            Vector3 l = new Vector3(-0.045f, 0.17f, 0f), r = new Vector3(0.045f, 0.17f, 0f), top = new Vector3(0f, 0.26f, 0f);
            Rod(t, l, new Vector3(-0.03f, 0.24f, 0f), 0.0018f, wire);
            Rod(t, new Vector3(-0.03f, 0.24f, 0f), top, 0.0018f, wire);
            Rod(t, top, new Vector3(0.03f, 0.24f, 0f), 0.0018f, wire);
            Rod(t, new Vector3(0.03f, 0.24f, 0f), r, 0.0018f, wire);
            if (hanging && cord > 0f) Rod(t, top, top + Vector3.up * cord, 0.003f, wire);
            var hit = root.AddComponent<SphereCollider>(); // (what you click)
            hit.isTrigger = true;
            hit.center = new Vector3(0f, 0.1f, 0f);
            hit.radius = 0.1f;
            var lantern = root.AddComponent<Lantern>();
            lantern.glass = globe.GetComponent<Renderer>();
            lantern.flame = new Vector3(0f, 0.1f, 0f);
            return root;
        }

        static GameObject Part(PrimitiveType type, Transform parent, Vector3 pos, Vector3 scale, Material mat)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = type.ToString();
            go.layer = Layers.Props;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localScale = scale;
            go.GetComponent<Renderer>().sharedMaterial = mat;
            Object.Destroy(go.GetComponent<Collider>());
            return go;
        }

        static void Rod(Transform parent, Vector3 from, Vector3 to, float radius, Material mat)
        {
            Vector3 d = to - from;
            if (d.sqrMagnitude < 1e-6f) return;
            var rod = Part(PrimitiveType.Cylinder, parent, (from + to) * 0.5f, new Vector3(radius * 2f, d.magnitude * 0.5f, radius * 2f), mat);
            rod.transform.localRotation = Quaternion.FromToRotation(Vector3.up, d);
            rod.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }
    }
}
