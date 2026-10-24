using Game.Core;
using Game.Player;
using UnityEngine;
using UnityEngine.Rendering.HighDefinition;

namespace Game.World
{
    /// <summary>
    /// Something lying in the world to take with a left click (section 6.2): a battery, a torch, a snack (eaten on
    /// the spot). Batteries in the forest pulse a faint green light so they can be found.
    /// </summary>
    public sealed class Pickup : MonoBehaviour, IInteractable
    {
        public ItemKind kind;
        public int count = 1;
        [Tooltip("A snack: eaten on the spot instead of carried.")]
        public bool snack;

        Light glow;
        float glowStrength;
        float phase;

        public string Prompt => snack ? "Eat" : "Take";
        public bool CanInteract => true;
        public Vector3 FocusPoint => transform.position + Vector3.up * 0.05f;

        /// <summary>Make one: the model (yours from ItemsConfig, or the built-in one) and a small trigger to aim at.</summary>
        public static Pickup Create(ItemKind kind, int count, Vector3 position, float yaw, Transform parent, bool snack = false, bool glowing = false)
        {
            var config = Services.TryGet<GameSetup>(out var setup) ? setup.Get<ItemsConfig>() : null;
            var art = config != null ? config.art : null;
            var go = new GameObject(snack ? "Snack" : Inventory.NameOf(kind)) { layer = Layers.Pickups };
            go.transform.SetParent(parent, true);
            go.transform.SetPositionAndRotation(position, Quaternion.Euler(0f, yaw, 0f));
            GameObject model;
            if (snack) model = ItemModels.Snack(art != null ? art.snack : null, go.transform);
            else
            {
                model = ItemModels.Make(kind, art != null ? art.Lying(kind) : null, go.transform, false);
                // Built-in models point along Z from the grip: lying down, they sit on their side.
                if (art == null || art.Lying(kind) == null) model.transform.localPosition = Vector3.up * 0.025f;
            }
            foreach (var t in model.GetComponentsInChildren<Transform>()) t.gameObject.layer = Layers.Pickups;
            var flame = model.transform.Find("Flame");
            if (flame != null) flame.gameObject.SetActive(false);

            var col = go.AddComponent<SphereCollider>();
            col.isTrigger = true;
            col.radius = 0.18f;
            col.center = Vector3.up * 0.05f;

            var pickup = go.AddComponent<Pickup>();
            pickup.kind = kind;
            pickup.count = count;
            pickup.snack = snack;
            if (glowing && config != null) pickup.AddGlow(config.settings);
            return pickup;
        }

        void AddGlow(ItemSettings s)
        {
            var lightGo = new GameObject("Glow");
            lightGo.transform.SetParent(transform, false);
            lightGo.transform.localPosition = Vector3.up * 0.15f;
            var data = lightGo.AddHDLight(LightType.Point);
            data.volumetricDimmer = 0f; // (no glowing ball in the mist: just a faint gleam on the ground)
            glow = lightGo.GetComponent<Light>();
            glow.shadows = LightShadows.None;
            glow.color = s.batteryGlow;
            glow.range = s.batteryGlowRange;
            glowStrength = s.batteryGlowStrength;
            phase = Random.value * 10f;
        }

        void Update()
        {
            if (glow == null) return;
            glow.intensity = glowStrength * (0.35f + 0.65f * Mathf.Pow(0.5f + 0.5f * Mathf.Sin((Time.time + phase) * 2.2f), 2f));
        }

        public void Interact()
        {
            if (!Services.TryGet<PlayerController>(out var player) || player.Hotbar == null) return;
            if (snack)
            {
                player.Hotbar.EatSnack();
                Destroy(gameObject);
                return;
            }
            if (player.Hotbar.Give(kind, count)) Destroy(gameObject);
        }
    }
}
