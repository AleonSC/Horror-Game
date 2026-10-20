using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Makes the generator's bushes react to the player: they have no colliders, so you can run straight
/// through them, but they lean away as you push in, shake as you pass, and rustle (louder the faster
/// you're going). DarkForestGenerator puts this on its "Bushes" group.
/// </summary>
public class BushRustle : MonoBehaviour
{
    [Tooltip("How far a bush leans away from you while you're inside it, in degrees.")]
    public float pushAngle = 18f;
    [Tooltip("How hard bushes shake as you pass through, in degrees.")]
    public float shakeAngle = 10f;
    public float rustleVolume = 0.5f;
    [Tooltip("Below this speed (m/s) you slip through quietly.")]
    public float quietSpeed = 0.6f;

    class Bush
    {
        public Transform transform;
        public Quaternion rest;
        public float radius, shake, phase, soundCooldown;
        public Vector3 axis;
        public bool inside;
    }

    readonly List<Bush> bushes = new List<Bush>();
    readonly List<Bush> moving = new List<Bush>();
    AudioSource[] voices;
    int nextVoice;
    FirstPersonController player;
    Vector3 lastPlayerPos;

    void Start()
    {
        foreach (Transform child in transform)
        {
            float size = child.localScale.x;
            bushes.Add(new Bush { transform = child, rest = child.localRotation, radius = size * 0.8f, phase = Random.Range(0f, 10f) });
        }

        voices = new AudioSource[3];
        for (int i = 0; i < voices.Length; i++)
        {
            var go = new GameObject("Bush Rustle");
            go.transform.SetParent(transform, false);
            voices[i] = ProceduralAudio.AddSource(go, ProceduralAudio.BushRustle, false, true, rustleVolume, 2f, 25f);
        }
    }

    void Update()
    {
        if (player == null)
        {
            player = FindAnyObjectByType<FirstPersonController>();
            if (player == null) return;
            lastPlayerPos = player.transform.position;
        }

        float dt = Time.deltaTime;
        Vector3 feet = player.transform.position;
        Vector3 step = feet - lastPlayerPos;
        step.y = 0f;
        lastPlayerPos = feet;
        if (step.magnitude > 2f) step = Vector3.zero; // teleported (map wrap, respawn)
        float speed = player.CurrentSpeed;

        foreach (var bush in bushes)
        {
            if (bush.transform == null) continue;
            Vector3 to = bush.transform.position - feet;
            to.y = 0f;
            float dist = to.magnitude;
            bool inside = dist < bush.radius;

            if (inside)
            {
                // Leaning away from you, around an axis across your path.
                Vector3 away = dist > 0.01f ? to / dist : player.transform.forward;
                bush.axis = Vector3.Cross(Vector3.up, away);
                if (step.sqrMagnitude > 0f) bush.shake = Mathf.Min(1f, bush.shake + step.magnitude * 2.5f);

                bush.soundCooldown -= dt;
                if ((!bush.inside || bush.soundCooldown <= 0f) && speed > quietSpeed)
                {
                    Rustle(bush.transform.position, speed);
                    bush.soundCooldown = Random.Range(0.5f, 0.8f);
                }
            }
            else if (bush.inside && speed > quietSpeed) Rustle(bush.transform.position, speed * 0.6f); // springing back

            bush.inside = inside;
            if ((inside || bush.shake > 0f) && !moving.Contains(bush)) moving.Add(bush);
        }

        // Animate only the bushes that are being disturbed.
        for (int i = moving.Count - 1; i >= 0; i--)
        {
            var bush = moving[i];
            if (bush.transform == null) { moving.RemoveAt(i); continue; }

            Vector3 to = bush.transform.position - feet;
            to.y = 0f;
            float push = bush.inside ? 1f - Mathf.Clamp01(to.magnitude / bush.radius) : 0f;
            float wobble = Mathf.Sin(Time.time * 13f + bush.phase) * bush.shake * shakeAngle;
            float angle = push * pushAngle + wobble;

            Transform parent = bush.transform.parent;
            Vector3 axis = parent != null ? parent.InverseTransformDirection(bush.axis) : bush.axis;
            bush.transform.localRotation = axis.sqrMagnitude > 0f ? Quaternion.AngleAxis(angle, axis) * bush.rest : bush.rest;

            bush.shake = Mathf.MoveTowards(bush.shake * Mathf.Exp(-2.5f * Time.deltaTime), 0f, 0.01f * Time.deltaTime);
            if (!bush.inside && bush.shake <= 0.001f)
            {
                bush.shake = 0f;
                bush.transform.localRotation = bush.rest;
                moving.RemoveAt(i);
            }
        }
    }

    void Rustle(Vector3 at, float speed)
    {
        var voice = voices[nextVoice];
        nextVoice = (nextVoice + 1) % voices.Length;
        voice.transform.position = at;
        voice.pitch = Random.Range(0.85f, 1.2f);
        voice.PlayOneShot(ProceduralAudio.BushRustle, rustleVolume * Mathf.Clamp01(speed / 6f + 0.3f));
    }
}
