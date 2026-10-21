using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Your breath showing in the cold, but only once you've been running a good while: after a long sprint,
/// each heavy breath out is a little cloud in front of your face, quicker and thicker the longer you push
/// on, carrying on for a while after you stop while you get your breath back. Pale in the flashlight's
/// beam, barely there in the dark. LampSpawner adds this to the player.
/// </summary>
public class BreathFog : MonoBehaviour
{
    void Awake() => Tuning.Apply(this); // saved tuning (Save Tuning on the component's menu)

    [Tooltip("Seconds of running before your breath starts to show...")]
    public float runBeforeBreath = 12f;
    [Tooltip("...and before it's at its heaviest.")]
    public float runForFullBreath = 22f;
    [Tooltip("How long it takes to get your breath back after stopping.")]
    public float recoveryTime = 12f;
    public Color litColor = new Color(0.55f, 0.56f, 0.58f, 0.22f);
    public Color darkColor = new Color(0.08f, 0.085f, 0.095f, 0.12f);

    class Puff { public Transform t; public Renderer r; public Vector3 velocity; public float age, life, size, seed; }

    readonly List<Puff> puffs = new List<Puff>();
    FirstPersonController controller;
    Flashlight flashlight;
    MaterialPropertyBlock block;
    float runTime, winded, breathTimer;

    void Start()
    {
        controller = GetComponent<FirstPersonController>();
        flashlight = GetComponentInChildren<Flashlight>();
        if (flashlight == null) flashlight = FindAnyObjectByType<Flashlight>();
        block = new MaterialPropertyBlock();
        Shader shader = Shader.Find("Horror/Smoke");
        if (shader == null || controller == null) { enabled = false; return; }
        var mat = new Material(shader) { renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent + 5 };
        for (int i = 0; i < 10; i++)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
            go.name = "Breath";
            Destroy(go.GetComponent<Collider>());
            var r = go.GetComponent<Renderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            go.SetActive(false);
            puffs.Add(new Puff { t = go.transform, r = r });
        }
    }

    void Update()
    {
        float dt = Time.deltaTime;
        var cam = Camera.main;
        if (cam == null) return;

        // How winded you are: builds once you've run long enough, eases off once you stop.
        if (controller.IsRunning) runTime += dt;
        else runTime = Mathf.Max(0f, runTime - dt * 1.5f);
        float target = Mathf.Clamp01(Mathf.InverseLerp(runBeforeBreath, runForFullBreath, runTime));
        winded = target > winded ? Mathf.MoveTowards(winded, target, dt / 3f) : Mathf.MoveTowards(winded, target, dt / Mathf.Max(recoveryTime, 0.1f));
        if (controller.IsRunning && runTime > runBeforeBreath) winded = Mathf.Max(winded, 0.15f);

        if (winded > 0.02f)
        {
            breathTimer -= dt;
            if (breathTimer <= 0f)
            {
                breathTimer = Mathf.Lerp(2.6f, 0.9f, winded) * Random.Range(0.85f, 1.15f); // panting, the harder the faster
                Exhale(cam.transform, winded);
            }
        }

        bool lit = flashlight != null && flashlight.IsOn;
        Color baseColor = lit ? litColor : darkColor;
        foreach (var p in puffs)
        {
            if (!p.t.gameObject.activeSelf) continue;
            p.age += dt;
            if (p.age >= p.life) { p.t.gameObject.SetActive(false); continue; }
            float t = p.age / p.life;
            p.velocity *= 1f - 1.6f * dt;               // a puff of breath, slowing as it spreads
            p.velocity += Vector3.up * 0.15f * dt;      // warm: drifts up a little
            p.t.position += p.velocity * dt;
            p.t.rotation = cam.transform.rotation * Quaternion.Euler(0f, 0f, p.seed * 3.6f + p.age * 20f);
            p.t.localScale = Vector3.one * Mathf.Lerp(p.size * 0.35f, p.size, 1f - (1f - t) * (1f - t));
            Color c = baseColor;
            c.a *= Mathf.Clamp01(t * 6f) * (1f - t) * (1f - t);
            block.SetColor("_Color", c);
            block.SetFloat("_Seed", p.seed);
            p.r.SetPropertyBlock(block);
        }
    }

    void Exhale(Transform cam, float strength)
    {
        int count = strength > 0.6f ? 3 : 2;
        foreach (var p in puffs)
        {
            if (count <= 0) break;
            if (p.t.gameObject.activeSelf) continue;
            count--;
            p.t.gameObject.SetActive(true);
            // From just under your nose, out in front, carried along with you a little.
            p.t.position = cam.position + cam.forward * 0.4f - cam.up * 0.12f + Random.insideUnitSphere * 0.04f;
            Vector3 carried = controller.Velocity;
            carried.y = 0f;
            p.velocity = cam.forward * Random.Range(0.6f, 1f) + carried * 0.85f - cam.up * 0.1f;
            p.age = 0f;
            p.life = Random.Range(1.2f, 1.8f);
            p.size = Random.Range(0.35f, 0.55f) * Mathf.Lerp(0.8f, 1.2f, strength);
            p.seed = Random.Range(0f, 100f);
        }
    }
}
