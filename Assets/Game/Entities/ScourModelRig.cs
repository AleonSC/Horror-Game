using UnityEngine;

namespace Game.Entities
{
    /// <summary>
    /// Put this on your own scour model's prefab (the root) to tell the game where its parts are. Everything is
    /// optional: leave a slot empty and the game guesses from the model's size. See docs/custom-scour.md.
    /// </summary>
    public sealed class ScourModelRig : MonoBehaviour
    {
        [Header("Where things are")]
        [Tooltip("The head bone or object (eyes and mouth follow it).")]
        public Transform head;
        [Tooltip("Points where the eyes are. The game puts glowing eyes there, unless Eye Renderers are set.")]
        public Transform[] eyePoints = new Transform[0];
        [Tooltip("Your model's own eye meshes, if it has them: the game makes them glow (and turn blue) instead.")]
        public Renderer[] eyeRenderers = new Renderer[0];
        [Tooltip("The mouth: the red glare shines out of here (its forward direction, tilted down a little).")]
        public Transform mouth;
        [Tooltip("The middle of the body: the flashlight has to be on this to face it down.")]
        public Transform chest;

        [Header("Jaw (optional)")]
        [Tooltip("A jaw bone to rotate open as it nears. Leave empty if your animations open the mouth.")]
        public Transform jaw;
        [Tooltip("The jaw's axis to open round (in its own space).")]
        public Vector3 jawAxis = Vector3.right;
        [Range(0f, 90f), Tooltip("Degrees open at fully open (it gapes a little more past that).")]
        public float jawOpenAngle = 40f;

        [Header("Size")]
        [Tooltip("Scale the model so it's as tall as ScourConfig's Height.")]
        public bool matchHeight = true;
        [Tooltip("The model's forward isn't +Z? Turn it this many degrees to face forward.")]
        public float yawOffset;

        [Header("Animator (optional)")]
        [Tooltip("Found on the model if left empty. The game sets these parameters if they exist: Pose (int), " +
                 "Mouth (float 0-1.6), Speed (float m/s), Shudder (float), Side (int -1/1), and the trigger Appear.")]
        public Animator animator;
    }
}
