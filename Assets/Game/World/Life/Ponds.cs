using System.Collections.Generic;
using Game.Audio;
using Game.Core;
using Game.Player;
using Game.Rendering;
using Game.World.Build;
using Game.World.Generation;
using UnityEngine;

namespace Game.World
{
    /// <summary>
    /// The ponds (section 6.13): wading slows you and splashes, glowing mushrooms grow round their edges, and frogs
    /// croak from them, falling silent when something's near (the silence is the warning).
    /// </summary>
    public sealed class Ponds : MonoBehaviour
    {
        WorldPlan plan;
        WorldWrap wrap;
        ForestLifeSettings s;
        Material glow;
        float nextCroak;
        bool hushed;
        System.IDisposable moodSub;
        System.Func<Vector3, bool> waterAt;

        public static Ponds Build(WorldPlan plan, ForestLifeSettings s, WorldWrap wrap, Transform parent)
        {
            var p = new GameObject("Ponds").AddComponent<Ponds>();
            p.transform.SetParent(parent, false);
            p.plan = plan;
            p.wrap = wrap;
            p.s = s;
            p.Make();
            return p;
        }

        void Make()
        {
            // Wading: the player asks whether a point is under water (any copy of the map).
            waterAt = p => plan != null && plan.InWater(Wrap.Into(new Vector2(p.x, p.z), plan.Size)) && p.y < WaterLevel(p) + 0.1f;
            PlayerController.WaterAt = waterAt;
            PlayerController.WadeSpeed = s.wadeSpeed;

            glow = RuntimeMaterials.LitGlow("Mushroom", new Color(0.5f, 0.55f, 0.45f), new Color(0.35f, 0.9f, 0.7f) * 0.6f);
            var rng = new Rng(plan.Seed, 97);
            foreach (var pond in plan.Ponds)
                for (int i = 0; i < s.mushroomsPerPond; i++)
                {
                    float a = rng.Angle();
                    float r = pond.Radius * rng.Range(1.05f, 1.35f);
                    Vector2 at = Wrap.Into(pond.Centre + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r, plan.Size);
                    var cap = Mushroom(rng.Range(0.6f, 1.4f));
                    wrap.Add(cap, new Vector3(at.x, plan.GroundAt(at), at.y));
                }
        }

        float WaterLevel(Vector3 p)
        {
            var m = Wrap.Into(new Vector2(p.x, p.z), plan.Size);
            foreach (var pond in plan.Ponds) if (plan.Distance(m, pond.Centre) < pond.Radius) return pond.Water;
            return float.MinValue;
        }

        Transform Mushroom(float size)
        {
            var root = new GameObject("Glowing mushroom").transform;
            root.SetParent(transform, false);
            var stem = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            Destroy(stem.GetComponent<Collider>());
            stem.transform.SetParent(root, false);
            stem.transform.localPosition = new Vector3(0f, 0.04f * size, 0f);
            stem.transform.localScale = new Vector3(0.015f, 0.04f, 0.015f) * size;
            stem.GetComponent<Renderer>().sharedMaterial = glow;
            var cap = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            Destroy(cap.GetComponent<Collider>());
            cap.transform.SetParent(root, false);
            cap.transform.localPosition = new Vector3(0f, 0.085f * size, 0f);
            cap.transform.localScale = new Vector3(0.07f, 0.03f, 0.07f) * size;
            cap.GetComponent<Renderer>().sharedMaterial = glow;
            return root;
        }

        void Start()
        {
            if (Services.TryGet<EventBus>(out var events))
                moodSub = events.Subscribe<MusicMoodChanged>(e => hushed = e.Mood != MusicMood.Calm);
        }

        void OnDestroy()
        {
            moodSub?.Dispose();
            if (glow != null) Destroy(glow);
            // (Only if a newer world hasn't already put in its own.)
            if (PlayerController.WaterAt == waterAt) PlayerController.WaterAt = null;
        }

        // The frogs: a chorus from the nearest pond, while all's calm.
        void Update()
        {
            if (hushed || PlayerPlace.InDarkRoom || Time.time < nextCroak) return;
            nextCroak = Time.time + Random.Range(0.6f, 2.8f);
            var cam = Camera.main;
            if (cam == null || plan.Ponds.Count == 0) return;
            Vector3 c = cam.transform.position;
            Pond best = null;
            float bestDist = 50f;
            Vector3 bestAt = Vector3.zero;
            foreach (var pond in plan.Ponds)
            {
                Vector3 at = wrap.Nearest(new Vector3(pond.Centre.x, pond.Water, pond.Centre.y), c);
                float d = (at - c).magnitude;
                if (d < bestDist) { bestDist = d; best = pond; bestAt = at; }
            }
            if (best == null || !Services.TryGet<SoundPlayer>(out var sounds)) return;
            float a = Random.Range(0f, Mathf.PI * 2f);
            Vector3 from = bestAt + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * best.Radius;
            sounds.Play(Sound.Croak, from, MixerNames.Outside, Random.Range(0.3f, 0.55f), Random.Range(0.8f, 1.25f), 45f);
        }
    }
}
