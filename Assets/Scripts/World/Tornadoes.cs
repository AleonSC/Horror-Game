using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Once eight lamps have been lit, tornadoes touch down far off round the horizon: towering dark funnels,
/// bending and writhing, spinning up out of churning dust and debris into a black wall of cloud, with a low
/// distant roar. They wander slowly along the horizon but never come near: they're kept hundreds of metres
/// from the player wherever they go. Run reset (or the lamp count dropping below 8) takes them away.
/// DarkForestGenerator adds this.
/// </summary>
public class Tornadoes : MonoBehaviour
{
    void Awake() => Tuning.Apply(this); // saved tuning (Save Tuning on the component's menu)

    public int lampsToSpawn = 8;
    public int count = 3;
    [Tooltip("How far off they stay (metres).")]
    public float minDistance = 600f, maxDistance = 900f;
    public float height = 450f;
    [Range(0f, 1f)] public float roarVolume = 0.3f;

    readonly List<Twister> twisters = new List<Twister>();
    float spawnTimer;
    FirstPersonController player;
    DarkForestGenerator generator;

    class Twister
    {
        public Transform root;
        public float bearing, distance, drift, phase, age, spin;
        public readonly List<Transform> puffs = new List<Transform>();
        public readonly List<float> puffHeight = new List<float>(), puffAngle = new List<float>(), puffSize = new List<float>();
        public readonly List<Renderer> puffRenderers = new List<Renderer>();
        public readonly List<Transform> debris = new List<Transform>();
        public AudioSource roar;
    }

    Material smoke, debrisMat;
    MaterialPropertyBlock block;

    void Update()
    {
        if (player == null) player = FindAnyObjectByType<FirstPersonController>();
        if (generator == null) generator = FindAnyObjectByType<DarkForestGenerator>();
        if (player == null) return;

        bool want = Lamp.TimesLit >= lampsToSpawn && !GameOverScreen.IsActive;
        if (!want)
        {
            if (twisters.Count > 0) Clear();
            return;
        }

        // They touch down one after another.
        spawnTimer -= Time.deltaTime;
        if (twisters.Count < count && spawnTimer <= 0f)
        {
            Spawn();
            spawnTimer = Random.Range(6f, 14f);
        }

        var cam = Camera.main;
        if (cam != null && cam.farClipPlane < maxDistance + 400f) cam.farClipPlane = maxDistance + 400f;
        foreach (var t in twisters) Animate(t, cam);
    }

    void Clear()
    {
        foreach (var t in twisters) if (t.root != null) Destroy(t.root.gameObject);
        twisters.Clear();
        spawnTimer = 0f;
    }

    void Spawn()
    {
        if (smoke == null)
        {
            Shader s = Shader.Find("Horror/Smoke");
            smoke = s != null ? new Material(s) { renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent } : MaterialFactory.Unlit(new Color(0.06f, 0.06f, 0.065f));
            debrisMat = MaterialFactory.NoFog(new Color(0.02f, 0.02f, 0.02f));
            block = new MaterialPropertyBlock();
        }
        var t = new Twister
        {
            // Spread round the horizon, away from the others.
            bearing = twisters.Count == 0 ? Random.Range(0f, 360f) : twisters[0].bearing + twisters.Count * (360f / count) + Random.Range(-30f, 30f),
            distance = Random.Range(minDistance, maxDistance),
            drift = Random.Range(-1.2f, 1.2f), // degrees a second along the horizon
            phase = Random.Range(0f, 100f),
            spin = Random.Range(0.8f, 1.3f),
        };
        t.root = new GameObject("Tornado").transform;
        t.root.SetParent(transform, false);

        // The funnel: billows spiralling up a bending column, narrow at the ground, spreading into the cloud.
        for (int i = 0; i < 70; i++)
        {
            float h = Mathf.Pow(i / 69f, 1.15f);
            AddPuff(t, h, Random.Range(0f, Mathf.PI * 2f), Mathf.Lerp(40f, 150f, h * h) * Random.Range(0.8f, 1.2f));
        }
        // The dust and debris churned up round its foot, and the black wall of cloud it hangs from.
        for (int i = 0; i < 14; i++) AddPuff(t, Random.Range(0f, 0.04f), Random.Range(0f, Mathf.PI * 2f), Random.Range(60f, 110f), -1f);
        for (int i = 0; i < 16; i++) AddPuff(t, Random.Range(0.95f, 1.05f), Random.Range(0f, Mathf.PI * 2f), Random.Range(160f, 240f), -2f);
        for (int i = 0; i < 24; i++)
        {
            var d = GameObject.CreatePrimitive(PrimitiveType.Cube);
            d.name = "Debris";
            d.transform.SetParent(t.root, false);
            Destroy(d.GetComponent<Collider>());
            d.GetComponent<Renderer>().sharedMaterial = debrisMat;
            d.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            d.transform.localScale = Vector3.one * Random.Range(2f, 6f);
            t.debris.Add(d.transform);
        }

        t.roar = ProceduralAudio.AddSource(t.root.gameObject, ProceduralAudio.TornadoRoar, true, true, roarVolume, 200f, 1600f);
        t.roar.rolloffMode = AudioRolloffMode.Linear;
        t.roar.time = Random.Range(0f, t.roar.clip.length * 0.9f);
        t.roar.Play();
        twisters.Add(t);
    }

    /// <summary>A billow: at a height up the funnel (0 to 1), an angle round it, a size. Kind -1 dust, -2 cloud.</summary>
    void AddPuff(Twister t, float h, float angle, float size, float kind = 0f)
    {
        var q = GameObject.CreatePrimitive(PrimitiveType.Quad);
        q.name = "Billow";
        q.transform.SetParent(t.root, false);
        Destroy(q.GetComponent<Collider>());
        var r = q.GetComponent<Renderer>();
        r.sharedMaterial = smoke;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        r.receiveShadows = false;
        block.SetFloat("_Seed", Random.Range(0f, 100f));
        float shade = kind == -1f ? 0.09f : kind == -2f ? 0.035f : Random.Range(0.045f, 0.075f);
        block.SetColor("_Color", new Color(shade, shade, shade * 1.1f, kind == -1f ? 0.55f : 0.85f));
        r.SetPropertyBlock(block);
        t.puffs.Add(q.transform);
        t.puffHeight.Add(kind < 0f ? kind - h : h); // funnel billows: their height; dust -1.xx, cloud -2.xx
        t.puffAngle.Add(angle);
        t.puffSize.Add(size);
        t.puffRenderers.Add(r);
    }

    void Animate(Twister t, Camera cam)
    {
        float dt = Time.deltaTime;
        t.age += dt;
        float time = Time.time + t.phase;
        // Wandering slowly along the horizon, coming and going a little, but always far off.
        t.bearing += t.drift * dt;
        float dist = Mathf.Clamp(t.distance + Mathf.Sin(time * 0.05f) * 80f, minDistance, maxDistance);
        Vector3 you = player.transform.position;
        Vector3 basePos = you + Quaternion.Euler(0f, t.bearing, 0f) * Vector3.forward * dist;
        basePos.y = generator != null ? generator.GetGroundHeight(basePos.x, basePos.z) : you.y;
        t.root.position = basePos;

        float grow = Mathf.SmoothStep(0f, 1f, t.age / 10f); // reaching down to the ground as it forms
        Vector3 toCam = cam != null ? cam.transform.position : you;

        // The column bends and writhes.
        Vector3 Centre(float h)
        {
            float bend = Mathf.Sin(h * 2.6f + time * 0.23f) * 60f * h + Mathf.Sin(h * 5.1f + time * 0.41f) * 18f * h;
            float lean = Mathf.Cos(h * 2.1f + time * 0.17f) * 40f * h;
            return basePos + new Vector3(bend, h * height, lean);
        }

        for (int i = 0; i < t.puffs.Count; i++)
        {
            float code = t.puffHeight[i];
            Vector3 pos;
            float size = t.puffSize[i];
            if (code <= -2f)
            {
                // The wall cloud: slowly turning, spread wide over the top.
                float a = t.puffAngle[i] + time * 0.03f * t.spin;
                pos = Centre(1f) + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * size * 0.9f + Vector3.up * (-code - 2f) * 40f;
            }
            else if (code <= -1f)
            {
                // The dust at its foot, churning round fast and low.
                float a = t.puffAngle[i] + time * 0.6f * t.spin;
                pos = basePos + new Vector3(Mathf.Cos(a), 0.2f, Mathf.Sin(a)) * size * 0.5f + Vector3.up * size * 0.3f;
                size *= grow;
            }
            else
            {
                // The funnel: spiralling up, quicker lower down, drawn down to the ground as it forms.
                float h = Mathf.Lerp(1f, code, grow);
                float a = t.puffAngle[i] + time * Mathf.Lerp(1.4f, 0.35f, h) * t.spin;
                float r = Mathf.Lerp(8f, 70f, h * h);
                pos = Centre(h) + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * r;
                size *= Mathf.Lerp(0.6f, 1f, h);
            }
            t.puffs[i].position = pos;
            t.puffs[i].rotation = Quaternion.LookRotation(pos - toCam) * Quaternion.Euler(0f, 0f, time * 20f * t.spin + i * 37f);
            t.puffs[i].localScale = Vector3.one * size;
        }

        // Debris whirling round the bottom of the funnel.
        for (int i = 0; i < t.debris.Count; i++)
        {
            float h = 0.02f + (i % 6) * 0.03f;
            float a = i * 1.7f + time * (2.2f - h * 4f) * t.spin;
            float r = 20f + (i % 5) * 9f;
            t.debris[i].position = Centre(h) + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * r;
            t.debris[i].rotation = Quaternion.Euler(time * 90f + i * 13f, time * 70f + i, i * 31f);
            t.debris[i].gameObject.SetActive(grow > 0.5f);
        }

        if (t.roar != null) t.roar.volume = roarVolume * grow * (1f - Shelter.OutsideQuiet);
    }
}
