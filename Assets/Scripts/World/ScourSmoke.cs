using UnityEngine;

/// <summary>
/// Black smoke trailing from a scour's feet as it moves (prowling round the edge of the light, closing in):
/// soft dark puffs left behind it on the ground that swell, drift up a little and thin away. Nothing while
/// it stands still, or while it's not there. ShadowEntity adds this.
/// </summary>
public class ScourSmoke : MonoBehaviour
{
    public Color color = new Color(0.01f, 0.01f, 0.012f, 0.55f);
    [Tooltip("Puffs a second at a walk (more the faster it goes).")]
    public float rate = 9f;
    public float life = 2.6f;
    const int Pool = 36;

    ShadowEntity entity;
    readonly Transform[] puffs = new Transform[Pool];
    readonly Renderer[] renderers = new Renderer[Pool];
    readonly float[] age = new float[Pool];
    readonly float[] seed = new float[Pool];
    readonly Vector3[] drift = new Vector3[Pool];
    Material mat;
    MaterialPropertyBlock block;
    Vector3 last;
    float owed;
    int next;

    void Start()
    {
        entity = GetComponent<ShadowEntity>();
        Shader shader = Shader.Find("Horror/Smoke");
        mat = shader != null ? new Material(shader) { renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent } : MaterialFactory.Unlit(Color.black);
        block = new MaterialPropertyBlock();
        var root = new GameObject("Scour Smoke").transform; // (not under the scour: what it leaves stays where it was left)
        for (int i = 0; i < Pool; i++)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
            go.name = "Smoke";
            Destroy(go.GetComponent<Collider>());
            go.transform.SetParent(root, false);
            var r = go.GetComponent<Renderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            go.SetActive(false);
            puffs[i] = go.transform;
            renderers[i] = r;
            age[i] = life;
        }
        smokeRoot = root;
        last = transform.position;
    }
    Transform smokeRoot;

    void Update()
    {
        float dt = Time.deltaTime;
        if (dt <= 0f) return;
        Vector3 pos = transform.position;
        Vector3 moved = pos - last;
        moved.y = 0f;
        last = pos;
        float speed = moved.magnitude / dt;

        // Walking (not standing, not jumping from one spot to another as it comes and goes): leave smoke.
        bool walking = entity != null && entity.IsVisible && speed > 0.25f && speed < 12f;
        if (walking)
        {
            owed += rate * Mathf.Clamp(speed / 1.5f, 0.6f, 2.5f) * dt;
            while (owed >= 1f)
            {
                owed -= 1f;
                Emit(pos - moved * Random.value, moved.normalized);
            }
        }
        else owed = 0f;

        var cam = Camera.main;
        for (int i = 0; i < Pool; i++)
        {
            if (age[i] >= life) continue;
            age[i] += dt;
            var p = puffs[i];
            if (age[i] >= life) { p.gameObject.SetActive(false); continue; }
            float t = age[i] / life;
            p.position += (Vector3.up * 0.35f + drift[i]) * dt;
            p.localScale = Vector3.one * Mathf.Lerp(0.35f, 1.3f, 1f - (1f - t) * (1f - t));
            if (cam != null) p.rotation = cam.transform.rotation * Quaternion.Euler(0f, 0f, seed[i] * 3.6f + age[i] * 20f);
            Color c = color;
            c.a *= Mathf.Clamp01(t * 6f) * Mathf.Pow(1f - t, 1.4f);
            block.SetColor("_Color", c);
            block.SetFloat("_Seed", seed[i]);
            renderers[i].SetPropertyBlock(block);
        }
    }

    void Emit(Vector3 at, Vector3 heading)
    {
        int i = next;
        next = (next + 1) % Pool;
        age[i] = 0f;
        seed[i] = Random.Range(0f, 100f);
        Vector2 spread = Random.insideUnitCircle * 0.15f;
        puffs[i].position = at + new Vector3(spread.x, Random.Range(0.05f, 0.25f), spread.y);
        drift[i] = -heading * Random.Range(0.05f, 0.25f) + new Vector3(Random.Range(-0.1f, 0.1f), 0f, Random.Range(-0.1f, 0.1f));
        puffs[i].gameObject.SetActive(true);
    }

    void OnDisable()
    {
        if (smokeRoot == null) return;
        for (int i = 0; i < Pool; i++) { age[i] = life; if (puffs[i] != null) puffs[i].gameObject.SetActive(false); }
    }

    void OnDestroy()
    {
        if (smokeRoot != null) Destroy(smokeRoot.gameObject);
        if (mat != null) Destroy(mat);
    }
}
