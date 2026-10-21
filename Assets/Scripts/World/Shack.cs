using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// A small abandoned shack: raised plank floor, gappy weathered walls with a broken window,
/// a sagging roof, and a creaky door that opens when clicked. Inside, a torch and a lighter
/// sit on a table, waiting to be picked up. Outside, a torch in a bracket either side of the door, which
/// a lighter will light (LightableTorch). DarkForestGenerator places a few around the map.
/// </summary>
public class Shack : MonoBehaviour
{
    public static readonly List<Shack> All = new List<Shack>();
    /// <summary>Clears this class's static state (StaticReset calls it when Play starts).</summary>
    internal static void ResetStatics() => All.Clear();

    const float Width = 4f, Depth = 3.4f, WallHeight = 2.4f, DoorWidth = 0.9f, DoorHeight = 2f, DoorX = 0.8f;

    [SerializeField] ShackDoor door;
    [SerializeField] Transform table;
    [Range(0f, 1f), Tooltip("Chance of a battery lying on the table as well.")]
    public float batteryChance = 0.5f;
    [Range(0f, 1f), Tooltip("Chance of the car's key fob lying on the table.")]
    public float keyFobChance = 0.2f;
    [Range(0f, 1f), Tooltip("Chance of a portable radio on the floor (the blind one comes for it when it plays).")]
    public float radioChance = 0.3f;
    CarRadio radio;
    readonly List<LightableTorch> torches = new List<LightableTorch>();

    /// <summary>
    /// True if a point is inside the shack (or just in its doorway). Things on its table can only be reached
    /// from in here, not through the walls from outside.
    /// </summary>
    public bool Contains(Vector3 point)
    {
        Vector3 l = transform.InverseTransformPoint(point);
        return Mathf.Abs(l.x) < Width * 0.5f - 0.02f
            && l.z > -Depth * 0.5f + 0.02f && l.z < Depth * 0.5f + 0.7f // a little way out the front: reaching in through the door
            && l.y > floorY - 0.5f && l.y < floorY + WallHeight + 0.5f;
    }
    float floorY;

    void OnEnable() => All.Add(this);
    void OnDisable() => All.Remove(this);

    /// <summary>Builds the shack at its current position (its pivot on the ground at the centre).</summary>
    public void Build(DarkForestGenerator generator, System.Random rng)
    {
        float R(float a, float b) => a + (float)rng.NextDouble() * (b - a);

        var wood = MaterialFactory.Lit(new Color(0.16f, 0.12f, 0.09f), 0.05f);
        var darkWood = MaterialFactory.Lit(new Color(0.1f, 0.08f, 0.06f), 0.05f);
        var roofMat = MaterialFactory.Lit(new Color(0.08f, 0.075f, 0.07f), 0.1f);

        // Raise the floor above the highest corner so it never sinks into a slope.
        float highest = float.MinValue, lowest = float.MaxValue;
        for (int cx = -1; cx <= 1; cx += 2)
        for (int cz = -1; cz <= 1; cz += 2)
        {
            Vector3 corner = transform.TransformPoint(new Vector3(cx * Width * 0.5f, 0f, cz * Depth * 0.5f));
            float h = generator.GetGroundHeight(corner.x, corner.z) - transform.position.y;
            highest = Mathf.Max(highest, h);
            lowest = Mathf.Min(lowest, h);
        }
        floorY = highest + 0.15f;

        // Stilts down to the ground, and a step up to the door.
        for (int cx = -1; cx <= 1; cx += 2)
        for (int cz = -1; cz <= 1; cz += 2)
        {
            float postHeight = floorY - lowest + 0.3f;
            Box(darkWood, new Vector3(cx * (Width * 0.5f - 0.1f), floorY - postHeight * 0.5f, cz * (Depth * 0.5f - 0.1f)), new Vector3(0.15f, postHeight, 0.15f), Vector3.zero, true);
        }
        BuildSteps(generator, wood, darkWood, rng);

        // Floor of planks, one plank missing.
        Box(darkWood, new Vector3(0f, floorY - 0.06f, 0f), new Vector3(Width, 0.06f, Depth), Vector3.zero, true);
        int skip = rng.Next(2, 10);
        for (int i = 0; i < 13; i++)
        {
            if (i == skip) continue;
            float x = -Width * 0.5f + 0.15f + i * 0.3f;
            Box(wood, new Vector3(x, floorY, 0f), new Vector3(0.28f, 0.04f, Depth), new Vector3(0f, 0f, R(-1f, 1f)), false);
        }

        // Walls: visuals are loose planks; collisions are simple boxes.
        PlankWall(wood, rng, new Vector3(0f, 0f, -Depth * 0.5f), Width, 0f, -1f, 0f, 0f);                // back
        PlankWall(wood, rng, new Vector3(-Width * 0.5f, 0f, 0f), Depth, 90f, -1f, 1.1f, 1.7f);          // left, with a broken window
        PlankWall(wood, rng, new Vector3(Width * 0.5f, 0f, 0f), Depth, 90f, -1f, 0f, 0f);               // right
        PlankWall(wood, rng, new Vector3(0f, 0f, Depth * 0.5f), Width, 0f, DoorX, 0f, 0f);              // front, with the doorway

        WallCollider(new Vector3(0f, 0f, -Depth * 0.5f), new Vector3(Width, WallHeight, 0.12f));
        WallCollider(new Vector3(-Width * 0.5f, 0f, 0f), new Vector3(0.12f, WallHeight, Depth));
        WallCollider(new Vector3(Width * 0.5f, 0f, 0f), new Vector3(0.12f, WallHeight, Depth));
        float leftOfDoor = DoorX - DoorWidth * 0.5f + Width * 0.5f;
        float rightOfDoor = Width * 0.5f - (DoorX + DoorWidth * 0.5f);
        WallCollider(new Vector3(-Width * 0.5f + leftOfDoor * 0.5f, 0f, Depth * 0.5f), new Vector3(leftOfDoor, WallHeight, 0.12f));
        WallCollider(new Vector3(Width * 0.5f - rightOfDoor * 0.5f, 0f, Depth * 0.5f), new Vector3(rightOfDoor, WallHeight, 0.12f));
        var lintel = WallCollider(new Vector3(DoorX, DoorHeight, Depth * 0.5f), new Vector3(DoorWidth, WallHeight - DoorHeight, 0.12f));
        lintel.center = new Vector3(0f, (WallHeight - DoorHeight) * 0.5f, 0f);

        // Sagging, slightly askew gable roof.
        float pitch = 22f;
        float slabDepth = Depth * 0.5f / Mathf.Cos(pitch * Mathf.Deg2Rad) + 0.35f;
        float ridge = floorY + WallHeight + Mathf.Tan(pitch * Mathf.Deg2Rad) * Depth * 0.5f;
        for (int side = -1; side <= 1; side += 2)
        {
            Box(roofMat,
                new Vector3(R(-0.05f, 0.05f), ridge - Mathf.Sin(pitch * Mathf.Deg2Rad) * slabDepth * 0.5f + 0.05f, side * Mathf.Cos(pitch * Mathf.Deg2Rad) * slabDepth * 0.5f),
                new Vector3(Width + 0.5f, 0.08f, slabDepth),
                new Vector3(side * pitch + R(-2f, 2f), R(-1.5f, 1.5f), R(-1.5f, 1.5f)), false);
        }

        // The door, hinged on its left edge.
        var hinge = new GameObject("Door Hinge").transform;
        hinge.SetParent(transform, false);
        hinge.localPosition = new Vector3(DoorX - DoorWidth * 0.5f, floorY, Depth * 0.5f);
        door = hinge.gameObject.AddComponent<ShackDoor>();
        var panel = Box(darkWood, new Vector3(DoorWidth * 0.5f, DoorHeight * 0.5f, 0f), new Vector3(DoorWidth - 0.04f, DoorHeight - 0.02f, 0.05f), Vector3.zero, true, hinge);
        panel.name = "Door";
        for (int i = 0; i < 3; i++)
            Box(wood, new Vector3(DoorWidth * 0.5f, 0.3f + i * 0.7f, 0.035f), new Vector3(DoorWidth - 0.08f, 0.1f, 0.03f), Vector3.zero, false, hinge);
        Box(darkWood, new Vector3(DoorWidth - 0.12f, DoorHeight * 0.5f, 0.06f), new Vector3(0.04f, 0.12f, 0.04f), Vector3.zero, false, hinge); // handle

        // A torch in a bracket on the front wall either side of the door, unlit: a lighter will light them.
        torches.Clear();
        float wallFront = Depth * 0.5f + 0.06f;
        torches.Add(LightableTorch.WallMounted(transform, new Vector3(DoorX - DoorWidth * 0.5f - 0.45f, floorY + 1.75f, wallFront), Quaternion.identity));
        torches.Add(LightableTorch.WallMounted(transform, new Vector3(DoorX + DoorWidth * 0.5f + 0.35f, floorY + 1.75f, wallFront), Quaternion.identity));

        // Sometimes a portable radio on the floor in the corner: turn it on and the blind one comes for it.
        if (rng.NextDouble() < radioChance)
        {
            var radioGo = new GameObject("Radio");
            radioGo.transform.SetParent(transform, false);
            radioGo.transform.localPosition = new Vector3(Width * 0.5f - 0.4f, floorY + 0.02f, -Depth * 0.5f + 0.35f);
            radioGo.transform.localRotation = Quaternion.Euler(0f, R(-160f, -110f), 0f);
            radio = radioGo.AddComponent<CarRadio>();
            radio.Build();
        }

        // Rickety table against the back wall.
        table = new GameObject("Table").transform;
        table.SetParent(transform, false);
        table.localPosition = new Vector3(-0.9f, floorY, -Depth * 0.5f + 0.5f);
        table.localRotation = Quaternion.Euler(0f, R(-6f, 6f), 0f);
        Box(wood, new Vector3(0f, 0.78f, 0f), new Vector3(1.1f, 0.05f, 0.6f), Vector3.zero, true, table);
        for (int lx = -1; lx <= 1; lx += 2)
        for (int lz = -1; lz <= 1; lz += 2)
            Box(darkWood, new Vector3(lx * 0.48f, 0.38f, lz * 0.24f), new Vector3(0.05f, 0.76f, 0.05f), Vector3.zero, false, table);

        // A broken chair on its side.
        var chair = new GameObject("Chair").transform;
        chair.SetParent(transform, false);
        chair.localPosition = new Vector3(1.1f, floorY + 0.2f, -0.6f);
        chair.localRotation = Quaternion.Euler(0f, R(0f, 360f), 85f);
        Box(darkWood, Vector3.zero, new Vector3(0.45f, 0.04f, 0.45f), Vector3.zero, false, chair);
        Box(darkWood, new Vector3(0f, 0.25f, -0.2f), new Vector3(0.45f, 0.5f, 0.04f), Vector3.zero, false, chair);

        SpawnItems();
    }

    /// <summary>Puts a fresh torch and lighter on the table (removing any still there) and shuts the door.</summary>
    public void ResetShack()
    {
        if (door != null) door.Close();
        SpawnItems();
        if (radio != null) radio.ResetRadio();
        foreach (var torch in torches) if (torch != null) torch.ResetTorch();
    }

    void SpawnItems()
    {
        if (table == null) return;
        foreach (var old in table.GetComponentsInChildren<ItemPickup>())
        {
            if (Application.isPlaying) Destroy(old.gameObject);
            else DestroyImmediate(old.gameObject);
        }
        foreach (var old in table.GetComponentsInChildren<BatteryPickup>())
        {
            if (Application.isPlaying) Destroy(old.gameObject);
            else DestroyImmediate(old.gameObject);
        }
        // Sometimes someone left a battery on the table too.
        if (Random.value < batteryChance)
            BatterySpawner.Create(table, new Vector3(0.05f, 0.78f + 0.025f, 0.18f), Random.Range(-20f, 20f));
        ItemPickup.CreateTorch(table, new Vector3(-0.2f, 0.805f, 0.05f), Random.Range(-15f, 15f));
        ItemPickup.CreateLighter(table, new Vector3(0.3f, 0.805f, -0.08f), Random.Range(0f, 360f));
        // Now and then, the car's key fob.
        if (Random.value < keyFobChance)
            ItemPickup.CreateKeyFob(table, new Vector3(-0.4f, 0.805f, -0.15f), Random.Range(0f, 360f));
    }

    /// <summary>A wall of upright planks with gaps, uneven tops, and an optional doorway or window.</summary>
    /// <summary>
    /// Wooden steps from the doorway down to the ground: plank treads on two sloping stringers, as many as
    /// the drop needs, open underneath. Each tread is solid to walk on.
    /// </summary>
    void BuildSteps(DarkForestGenerator generator, Material wood, Material darkWood, System.Random rng)
    {
        float R(float a, float b) => a + (float)rng.NextDouble() * (b - a);
        const float run = 0.3f, maxRise = 0.2f;
        float front = Depth * 0.5f + 0.06f;

        // How far down the ground is in front of the door (it can fall away on a slope).
        float Ground(float z)
        {
            Vector3 w = transform.TransformPoint(new Vector3(DoorX, 0f, z));
            return generator.GetGroundHeight(w.x, w.z) - transform.position.y;
        }
        // Step down until the stairs meet the ground, wherever that is: on a slope it keeps falling away, so
        // it's checked under each step rather than once by the door.
        float rise = maxRise;
        int steps = 1;
        while (steps < 16 && floorY - steps * rise > Ground(front + (steps - 0.5f) * run) + 0.03f) steps++;
        float drop = steps * rise;
        float width = DoorWidth + 0.2f;

        for (int i = 1; i <= steps; i++)
        {
            float top = floorY - i * rise;
            float z = front + (i - 0.5f) * run;
            var tread = Box(wood, new Vector3(DoorX, top - 0.025f, z), new Vector3(width, 0.05f, run + 0.03f), new Vector3(R(-1.5f, 1.5f), 0f, R(-1.5f, 1.5f)), true);
            tread.name = "Step";
        }

        // Stringers: two boards sloping from the doorway down either side of the treads, carried on a step
        // further so they run into the ground rather than stopping short of it.
        float reach = (steps + 1) * run, fall = drop + rise;
        float length = Mathf.Sqrt(fall * fall + reach * reach);
        float angle = Mathf.Atan2(fall, reach) * Mathf.Rad2Deg;
        for (int side = -1; side <= 1; side += 2)
        {
            Box(darkWood, new Vector3(DoorX + side * (width * 0.5f + 0.02f), floorY - fall * 0.5f - 0.08f, front + reach * 0.5f),
                new Vector3(0.05f, 0.18f, length), new Vector3(angle, 0f, 0f), false);
        }
    }

    void PlankWall(Material mat, System.Random rng, Vector3 center, float length, float yaw, float doorX, float windowBottom, float windowTop)
    {
        float R(float a, float b) => a + (float)rng.NextDouble() * (b - a);
        Quaternion rot = Quaternion.Euler(0f, yaw, 0f);
        int planks = Mathf.RoundToInt(length / 0.26f);
        float pitch = length / planks;

        for (int i = 0; i < planks; i++)
        {
            float along = -length * 0.5f + pitch * (i + 0.5f);
            if (rng.NextDouble() < 0.06) continue; // a missing plank

            float top = WallHeight - R(0f, 0.15f);
            bool inDoor = doorX > -1f && Mathf.Abs(along - doorX) < DoorWidth * 0.5f;
            bool inWindow = windowTop > 0f && Mathf.Abs(along) < 0.6f;

            if (inDoor)
            {
                Plank(mat, rot, center, along, DoorHeight, top, R(-2f, 2f));
            }
            else if (inWindow)
            {
                Plank(mat, rot, center, along, 0f, windowBottom, R(-2f, 2f));
                Plank(mat, rot, center, along, windowTop, top, R(-2f, 2f));
            }
            else
            {
                Plank(mat, rot, center, along, 0f, top, R(-2f, 2f));
            }
        }

        // A couple of cross battens, stopping either side of the doorway rather than boarding it up.
        var spans = new List<Vector2>();
        if (doorX > -1f)
        {
            spans.Add(new Vector2(-length * 0.5f, doorX - DoorWidth * 0.5f - 0.04f));
            spans.Add(new Vector2(doorX + DoorWidth * 0.5f + 0.04f, length * 0.5f));
        }
        else spans.Add(new Vector2(-length * 0.5f, length * 0.5f));

        for (int b = 0; b < 2; b++)
        {
            float y = floorY + 0.5f + b * 1.3f;
            foreach (var span in spans)
            {
                float spanLength = span.y - span.x;
                if (spanLength < 0.1f) continue;
                float mid = (span.x + span.y) * 0.5f;
                var batten = Box(mat, center + rot * new Vector3(mid, 0f, 0f) + Vector3.up * y, Vector3.one, Vector3.zero, false);
                batten.localRotation = rot * Quaternion.Euler(0f, 0f, R(-3f, 3f));
                batten.localScale = new Vector3(spanLength, 0.1f, 0.03f);
                batten.localPosition += center.normalized * 0.04f; // on the outside face of the planks
            }
        }
    }

    void Plank(Material mat, Quaternion rot, Vector3 center, float along, float bottom, float top, float lean)
    {
        if (top - bottom < 0.05f) return;
        var plank = Box(mat, center + rot * new Vector3(along, 0f, 0f) + Vector3.up * (floorY + (bottom + top) * 0.5f), Vector3.one, Vector3.zero, false);
        plank.localRotation = rot * Quaternion.Euler(0f, 0f, lean);
        plank.localScale = new Vector3(0.24f, top - bottom, 0.04f);
    }

    BoxCollider WallCollider(Vector3 center, Vector3 size)
    {
        var go = new GameObject("Wall Collider");
        go.transform.SetParent(transform, false);
        go.transform.localPosition = center + Vector3.up * floorY;
        var box = go.AddComponent<BoxCollider>();
        box.center = new Vector3(0f, size.y * 0.5f, 0f);
        box.size = size;
        return box;
    }

    Transform Box(Material mat, Vector3 pos, Vector3 scale, Vector3 euler, bool keepCollider, Transform parent = null)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.transform.SetParent(parent != null ? parent : transform, false);
        go.transform.localPosition = pos;
        go.transform.localRotation = Quaternion.Euler(euler);
        go.transform.localScale = scale;
        go.GetComponent<Renderer>().sharedMaterial = mat;
        if (!keepCollider)
        {
            var col = go.GetComponent<Collider>();
            if (Application.isPlaying) Destroy(col);
            else DestroyImmediate(col);
        }
        return go.transform;
    }
}
