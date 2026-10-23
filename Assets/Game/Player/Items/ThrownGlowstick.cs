using System.Collections.Generic;
using Game.Core;
using Game.Rendering;
using UnityEngine;
using UnityEngine.Rendering.HighDefinition;

namespace Game.Player
{
    /// <summary>A cracked glowstick, tossed a few metres: it lands and glows for minutes. Only a light (nothing fears it).</summary>
    public sealed class ThrownGlowstick : MonoBehaviour
    {
        static readonly List<ThrownGlowstick> glowing = new List<ThrownGlowstick>();

        Light glow;
        float life, lifeLeft, intensity;

        public static void Throw(ItemSettings s, GameObject yours, Vector3 from, Vector3 direction)
        {
            // Too many: the oldest goes out.
            while (glowing.Count >= Mathf.Max(1, s.maxGlowsticks))
            {
                var oldest = glowing[0];
                glowing.RemoveAt(0);
                if (oldest != null) Destroy(oldest.gameObject);
            }

            var go = new GameObject("Glowstick") { layer = Layers.Pickups };
            go.transform.position = from;
            go.transform.rotation = Quaternion.LookRotation(direction) * Quaternion.Euler(0f, 90f, 0f);
            ItemModels.Make(ItemKind.Glowsticks, yours, go.transform, false);
            var col = go.AddComponent<CapsuleCollider>();
            col.radius = 0.012f;
            col.height = 0.15f;
            col.direction = 2;
            var body = go.AddComponent<Rigidbody>();
            body.mass = 0.05f;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            // A lob that lands about toss metres away.
            Vector3 flat = Vector3.ProjectOnPlane(direction, Vector3.up).normalized;
            float speed = Mathf.Sqrt(Mathf.Max(0.5f, s.glowstickToss) * -Physics.gravity.y); // (45 degrees)
            body.AddForce((flat + Vector3.up).normalized * speed, ForceMode.VelocityChange);
            body.angularVelocity = Random.insideUnitSphere * 8f;

            var stick = go.AddComponent<ThrownGlowstick>();
            var lightGo = new GameObject("Glow");
            lightGo.transform.SetParent(go.transform, false);
            lightGo.AddHDLight(LightType.Point);
            stick.glow = lightGo.GetComponent<Light>();
            stick.glow.shadows = LightShadows.None;
            stick.glow.color = s.glowstickColor;
            stick.glow.range = s.glowstickRange;
            stick.intensity = s.glowstickIntensity;
            stick.life = stick.lifeLeft = s.glowstickTime;
            glowing.Add(stick);
        }

        void Update()
        {
            lifeLeft -= Time.deltaTime;
            // It fades over its last fifth.
            float level = Mathf.Clamp01(lifeLeft / Mathf.Max(0.01f, life * 0.2f));
            glow.intensity = intensity * level;
            if (lifeLeft <= 0f) Destroy(gameObject);
        }

        void OnDestroy() => glowing.Remove(this);

        /// <summary>A restart clears them all.</summary>
        public static void ClearAll()
        {
            foreach (var g in glowing.ToArray()) if (g != null) Destroy(g.gameObject);
            glowing.Clear();
        }
    }
}
