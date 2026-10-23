using UnityEngine;

/// <summary>
/// A snack left lying in a tent (a chocolate bar, a bag of trail mix or a cereal bar): click it to eat it.
/// The wrapper crinkles, a few crunchy bites, and your stamina's bigger for the rest of the run
/// (FirstPersonController.StaminaBonus), and topped right up. Campsite puts one in each tent; the run
/// starting over puts them back. LampInteractor handles the clicking.
/// </summary>
public class Snack : MonoBehaviour
{
    void Awake() => Tuning.Apply(this); // saved tuning (Inspector changes made in Play are kept)

    [Tooltip("How much more stamina it gives you (0.25: running lasts a quarter as long again).")]
    public float staminaBonus = 0.25f;

    public enum Kind { ChocolateBar, TrailMix, CerealBar }
    public Kind kind;

    public string DisplayName => kind == Kind.ChocolateBar ? "chocolate bar" : kind == Kind.TrailMix ? "trail mix" : "cereal bar";

    // (By Start the model's been built, so the glow can fit itself round it.)
    void Start()
    {
        if (GetComponent<PickupGlow>() == null) gameObject.AddComponent<PickupGlow>();
    }

    /// <summary>Eats it: more stamina for the run, a full breath now, and it's gone.</summary>
    public void Eat(FirstPersonController player)
    {
        if (player == null) return;
        player.AddStaminaBonus(staminaBonus);
        player.RestoreStamina();
        ProceduralAudio.PlayAt(ProceduralAudio.SnackEat, transform.position, 0.8f, Random.Range(0.95f, 1.05f), 2f);
        SessionRecorder.Log("player", $"Ate the {DisplayName}: stamina now {1f + player.StaminaBonus:0.00}x.");
        Destroy(gameObject);
    }

    /// <summary>A snack of a random kind, lying flat at a spot.</summary>
    public static Snack Create(Transform parent, Vector3 localPos, float yaw)
    {
        var go = new GameObject("Snack");
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPos;
        go.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
        var snack = go.AddComponent<Snack>();
        snack.kind = (Kind)Random.Range(0, 3);

        Color wrapper, band;
        Vector3 size;
        switch (snack.kind)
        {
            case Kind.ChocolateBar: wrapper = new Color(0.45f, 0.08f, 0.07f); band = new Color(0.85f, 0.75f, 0.45f); size = new Vector3(0.16f, 0.018f, 0.055f); break;
            case Kind.TrailMix: wrapper = new Color(0.2f, 0.35f, 0.18f); band = new Color(0.85f, 0.6f, 0.2f); size = new Vector3(0.14f, 0.035f, 0.1f); break;
            default: wrapper = new Color(0.12f, 0.2f, 0.42f); band = new Color(0.9f, 0.85f, 0.75f); size = new Vector3(0.13f, 0.02f, 0.045f); break;
        }
        var wrap = MaterialFactory.Lit(wrapper, 0.55f);  // (shiny foil-ish plastic)
        var label = MaterialFactory.Lit(band, 0.3f);
        var body = Part(PrimitiveType.Cube, wrap, go.transform, new Vector3(0f, size.y * 0.5f, 0f), size);
        // A band of label round the middle, and crimped ends.
        Part(PrimitiveType.Cube, label, go.transform, new Vector3(0f, size.y * 0.5f, 0f), new Vector3(size.x * 0.35f, size.y * 1.04f, size.z * 1.04f));
        for (int end = -1; end <= 1; end += 2)
            Part(PrimitiveType.Cube, wrap, go.transform, new Vector3(end * (size.x * 0.5f + 0.008f), size.y * 0.5f, 0f), new Vector3(0.016f, size.y * 0.35f, size.z * 0.95f));
        if (snack.kind == Kind.TrailMix) body.localRotation = Quaternion.Euler(0f, 0f, 3f); // (a soft bag, slumped)

        // Something for the aim to find (a little bigger than it, so it's easy to pick out on the blanket).
        var aim = go.AddComponent<BoxCollider>();
        aim.isTrigger = true;
        aim.center = new Vector3(0f, size.y * 0.5f, 0f);
        aim.size = size + new Vector3(0.06f, 0.06f, 0.06f);
        return snack;
    }

    static Transform Part(PrimitiveType type, Material mat, Transform parent, Vector3 pos, Vector3 scale)
    {
        var p = GameObject.CreatePrimitive(type);
        p.transform.SetParent(parent, false);
        p.transform.localPosition = pos;
        p.transform.localScale = scale;
        p.GetComponent<Renderer>().sharedMaterial = mat;
        if (Application.isPlaying) Object.Destroy(p.GetComponent<Collider>());
        else Object.DestroyImmediate(p.GetComponent<Collider>());
        return p.transform;
    }
}
