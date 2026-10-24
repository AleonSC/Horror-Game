using Game.Core;
using UnityEngine;

namespace Game.World
{
    /// <summary>The car's headlight switch: a click turns them on or off.</summary>
    public sealed class HeadlightSwitch : MonoBehaviour, IInteractable
    {
        public Car car;
        public string Prompt => car != null && car.BatteryLeft <= 0f ? "Flat battery" : "Headlights";
        public bool CanInteract => car != null;
        public Vector3 FocusPoint => transform.position;
        public void Interact()
        {
            if (car != null) car.ToggleLights();
        }
    }
}
