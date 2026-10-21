using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// A small abandoned shack: raised plank floor, gappy weathered walls with a broken window,
/// a sagging roof, and a creaky door that opens when clicked. Inside, a torch and a lighter
/// sit on a table, waiting to be picked up, and a torch in an iron bracket on the wall, which a lighter will
/// light (LightableTorch). Or, built as a cabin, a bigger one with rooms: a front room with the table and the
/// torch, and behind it, through creaking inner doors, a bedroom and a store room, dark.
/// DarkForestGenerator places a few around the map.
/// </summary>
public class Shack : MonoBehaviour
{
    public static readonly List<Shack> All = new List<Shack>();
    /// <summary>Clears this class's static state (StaticReset calls it when Play starts).</summary>
    internal static void ResetStatics() => All.Clear();

    const float DoorWidth = 0.9f, DoorHeight = 2f, RoofPitch = 22f;
    // The cabin's inner wall between the front room and the back rooms (z), and its doorways (x).
    const float CabinSplit = 0.3f, BedroomDoor = -2.6f, StoreDoor = 1.6f;

    /// <summary>True if this is the big cabin with rooms.</summary>
    public bool IsCabin { get; private set; }
    float width = 4f, depth = 3.4f, wallHeight = 2.4f, doorX = 0.8f;

    [SerializeField] ShackDoor door;
    [SerializeField] Transform table;
    [SerializeField] Transform nightstand;
    [SerializeField] Transform droneSpot;
    [Range(0f, 1f), Tooltip("Chance of a battery lying on the table as well.")]
    public float batteryChance = 0.5f;
    [Range(0f, 1f), Tooltip("Chance of the car's key fob lying on the table.")]
    public float keyFobChance = 0.2f;
    [Range(0f, 1f), Tooltip("Chance of a portable radio on the floor (the blind one comes for it when it plays).")]
    public float radioChance = 0.3f;
    CarRadio radio;
    LightableTorch torch;
    readonly List<ShackDoor> doors = new List<ShackDoor>();

    /// <summary>
    /// True if a point is inside the shack (or just in its doorway). Things on its table can only be reached
    /// from in here, not through the walls from outside.
    /// </summary>
    public bool Contains(Vector3 point)
    {
        Vector3 l = transform.InverseTransformPoint(point);
        return Mathf.Abs(l.x) < width * 0.5f - 0.02f
            && l.z > -depth * 0.5f + 0.02f && l.z < depth * 0.5f + 0.7f // a little way out the front: reaching in through the door
            && l.y > floorY - 0.5f && l.y < floorY + wallHeight + 0.5f;
    }
    float floorY;

    void OnEnable() => All.Add(this);
    void OnDisable() => All.Remove(this);

    /// <summary>Builds the shack (or, if cabin, the cabin with rooms) at its current position (its pivot on the ground at the centre).</summary>
    public void Build(DarkForestGenerator generator, System.Random rng, bool cabin = false)
    {
        float R(float a, float b) => a + (float)rng.NextDouble() * (b - a);
        IsCabin = cabin;
        if (cabin)
        {
            width = 9f;
            depth = 7f;
            wallHeight = 2.6f;
            doorX = -1.5f;
        }

        var wood = MaterialFactory.Lit(new Color(0.16f, 0.12f, 0.09f), 0.05f);
        var darkWood = MaterialFactory.Lit(new Color(0.1f, 0.08f, 0.06f), 0.05f);
        var roofMat = MaterialFactory.Lit(new Color(0.08f, 0.075f, 0.07f), 0.1f);

        // Raise the floor above the highest corner so it never sinks into a slope.
        float highest = float.MinValue, lowest = float.MaxValue;
        for (int cx = -1; cx <= 1; cx++)
        for (int cz = -1; cz <= 1; cz++)
        {
            Vector3 corner = transform.TransformPoint(new Vector3(cx * width * 0.5f, 0f, cz * depth * 0.5f));
            float h = generator.GetGroundHeight(corner.x, corner.z) - transform.position.y;
            highest = Mathf.Max(highest, h);
            lowest = Mathf.Min(lowest, h);
        }
        floorY = highest + 0.15f;

        // Stilts down to the ground (a grid of them under the cabin), and a step up to the door.
        int postsX = cabin ? 4 : 2, postsZ = cabin ? 3 : 2;
        for (int ix = 0; ix < postsX; ix++)
        for (int iz = 0; iz < postsZ; iz++)
        {
            float x = Mathf.Lerp(-(width * 0.5f - 0.1f), width * 0.5f - 0.1f, ix / (postsX - 1f));
            float z = Mathf.Lerp(-(depth * 0.5f - 0.1f), depth * 0.5f - 0.1f, iz / (postsZ - 1f));
            Vector3 at = transform.TransformPoint(new Vector3(x, 0f, z));
            float ground = generator.GetGroundHeight(at.x, at.z) - transform.position.y;
            float postHeight = floorY - ground + 0.3f;
            Box(darkWood, new Vector3(x, floorY - postHeight * 0.5f, z), new Vector3(0.15f, postHeight, 0.15f), Vector3.zero, true);
        }
        BuildSteps(generator, wood, darkWood, rng);

        // Floor of planks, one plank missing.
        Box(darkWood, new Vector3(0f, floorY - 0.06f, 0f), new Vector3(width, 0.06f, depth), Vector3.zero, true);
        int floorPlanks = Mathf.RoundToInt(width / 0.3f);
        float plankPitch = width / floorPlanks;
        int skip = rng.Next(2, floorPlanks - 3);
        for (int i = 0; i < floorPlanks; i++)
        {
            if (i == skip) continue;
            float x = -width * 0.5f + plankPitch * (i + 0.5f);
            Box(wood, new Vector3(x, floorY, 0f), new Vector3(plankPitch - 0.02f, 0.04f, depth), new Vector3(0f, 0f, R(-1f, 1f)), false);
        }

        // Walls: visuals are loose planks; collisions are simple boxes. The side walls run up into the gables.
        var none = new float[0];
        if (!cabin)
        {
            PlankWall(wood, rng, new Vector3(0f, 0f, -depth * 0.5f), width, 0f, Vector3.back, none, 0f, 0f, 0f, false);      // back
            PlankWall(wood, rng, new Vector3(-width * 0.5f, 0f, 0f), depth, 90f, Vector3.left, none, 0f, 1.1f, 1.7f, true);  // left, with a broken window
            PlankWall(wood, rng, new Vector3(width * 0.5f, 0f, 0f), depth, 90f, Vector3.right, none, 0f, 0f, 0f, true);      // right
        }
        else
        {
            // (A wall turned 90 degrees runs along -z, so "along" is minus z there.)
            PlankWall(wood, rng, new Vector3(0f, 0f, -depth * 0.5f), width, 0f, Vector3.back, none, -2.6f, 1.1f, 1.7f, false); // back, the bedroom's window
            PlankWall(wood, rng, new Vector3(-width * 0.5f, 0f, 0f), depth, 90f, Vector3.left, none, -1.9f, 1.1f, 1.7f, true); // left, the front room's window
            PlankWall(wood, rng, new Vector3(width * 0.5f, 0f, 0f), depth, 90f, Vector3.right, none, 1.9f, 1.1f, 1.7f, true); // right, the store room's window
        }
        PlankWall(wood, rng, new Vector3(0f, 0f, depth * 0.5f), width, 0f, Vector3.forward, new[] { doorX }, 0f, 0f, 0f, false); // front, with the doorway

        WallColliders(new Vector3(0f, 0f, -depth * 0.5f), width, 0f, none);
        WallColliders(new Vector3(-width * 0.5f, 0f, 0f), depth, 90f, none);
        WallColliders(new Vector3(width * 0.5f, 0f, 0f), depth, 90f, none);
        WallColliders(new Vector3(0f, 0f, depth * 0.5f), width, 0f, new[] { doorX });

        if (cabin)
        {
            // Inside: a wall across behind the front room, with a doorway into each back room, and a wall
            // between those two.
            float backLength = depth * 0.5f + CabinSplit;
            var split = new Vector3(0f, 0f, CabinSplit);
            var divider = new Vector3(0f, 0f, -depth * 0.5f + backLength * 0.5f);
            PlankWall(wood, rng, split, width, 0f, Vector3.forward, new[] { BedroomDoor, StoreDoor }, 0f, 0f, 0f, false);
            PlankWall(wood, rng, divider, backLength, 90f, Vector3.right, none, 0f, 0f, 0f, false);
            WallColliders(split, width, 0f, new[] { BedroomDoor, StoreDoor });
            WallColliders(divider, backLength, 90f, none);
        }

        // Sagging, slightly askew gable roof.
        float slabDepth = depth * 0.5f / Mathf.Cos(RoofPitch * Mathf.Deg2Rad) + 0.35f;
        float ridge = floorY + wallHeight + Mathf.Tan(RoofPitch * Mathf.Deg2Rad) * depth * 0.5f;
        for (int side = -1; side <= 1; side += 2)
        {
            Box(roofMat,
                new Vector3(R(-0.05f, 0.05f), ridge - Mathf.Sin(RoofPitch * Mathf.Deg2Rad) * slabDepth * 0.5f + 0.05f, side * Mathf.Cos(RoofPitch * Mathf.Deg2Rad) * slabDepth * 0.5f),
                new Vector3(width + 0.5f, 0.08f, slabDepth),
                new Vector3(side * RoofPitch + R(-2f, 2f), R(-1.5f, 1.5f), R(-1.5f, 1.5f)), false);
        }

        // The door, hinged on its left edge (and the cabin's inner doors).
        doors.Clear();
        door = MakeDoor(new Vector3(doorX, 0f, depth * 0.5f), wood, darkWood);
        if (cabin)
        {
            var bedroomDoor = MakeDoor(new Vector3(BedroomDoor, 0f, CabinSplit), wood, darkWood);
            bedroomDoor.openAngle = 100f; // (into the bedroom, away from you)
            MakeDoor(new Vector3(StoreDoor, 0f, CabinSplit), wood, darkWood).openAngle = 100f;
        }

        // One torch in a bracket on the wall inside, unlit: a lighter will light it. (In the cabin, on the
        // inner wall of the front room, between the doorways.)
        torch = cabin
            ? LightableTorch.WallMounted(transform, new Vector3(-0.5f, floorY + 1.75f, CabinSplit + 0.03f), Quaternion.identity)
            : LightableTorch.WallMounted(transform, new Vector3(width * 0.5f - 0.05f, floorY + 1.75f, 0.45f), Quaternion.Euler(0f, -90f, 0f));

        // Sometimes a portable radio on the floor in the corner (the cabin's store room): turn it on and the
        // blind one comes for it.
        if (rng.NextDouble() < radioChance)
        {
            var radioGo = new GameObject("Radio");
            radioGo.transform.SetParent(transform, false);
            radioGo.transform.localPosition = new Vector3(width * 0.5f - 0.4f, floorY + 0.02f, -depth * 0.5f + 0.35f);
            radioGo.transform.localRotation = Quaternion.Euler(0f, R(-160f, -110f), 0f);
            radio = radioGo.AddComponent<CarRadio>();
            radio.Build();
        }

        // Rickety table against the back wall (in the cabin, against the inner wall of the front room).
        table = new GameObject("Table").transform;
        table.SetParent(transform, false);
        table.localPosition = cabin ? new Vector3(3.3f, floorY, CabinSplit + 0.4f) : new Vector3(-0.9f, floorY, -depth * 0.5f + 0.5f);
        table.localRotation = Quaternion.Euler(0f, R(-6f, 6f) + (cabin ? 180f : 0f), 0f);
        Box(wood, new Vector3(0f, 0.78f, 0f), new Vector3(1.1f, 0.05f, 0.6f), Vector3.zero, true, table);
        for (int lx = -1; lx <= 1; lx += 2)
        for (int lz = -1; lz <= 1; lz += 2)
            Box(darkWood, new Vector3(lx * 0.48f, 0.38f, lz * 0.24f), new Vector3(0.05f, 0.76f, 0.05f), Vector3.zero, false, table);

        // A broken chair on its side.
        var chair = new GameObject("Chair").transform;
        chair.SetParent(transform, false);
        chair.localPosition = cabin ? new Vector3(2.2f, floorY + 0.2f, 1.8f) : new Vector3(1.1f, floorY + 0.2f, -0.6f);
        chair.localRotation = Quaternion.Euler(0f, R(0f, 360f), 85f);
        Box(darkWood, Vector3.zero, new Vector3(0.45f, 0.04f, 0.45f), Vector3.zero, false, chair);
        Box(darkWood, new Vector3(0f, 0.25f, -0.2f), new Vector3(0.45f, 0.5f, 0.04f), Vector3.zero, false, chair);

        if (cabin) Furnish(wood, darkWood, rng);

        MeshMerge.Merge(transform);
        SpawnItems();

        // Shelter: the entities keep 5m off it (the cabin's bigger, so a little more, round its corners).
        var refuge = gameObject.AddComponent<Refuge>();
        refuge.radius = cabin ? Mathf.Max(5f, new Vector2(width, depth).magnitude * 0.5f + 1.5f) : 5f;
    }

    /// <summary>
    /// The cabin's back rooms: a bedroom (an iron bed, its mattress stained and sagging, and a nightstand) and
    /// a store room (shelves of jars, crates, a barrel).
    /// </summary>
    void Furnish(Material wood, Material darkWood, System.Random rng)
    {
        float R(float a, float b) => a + (float)rng.NextDouble() * (b - a);
        var iron = MaterialFactory.Lit(new Color(0.09f, 0.085f, 0.08f), 0.4f);
        var cloth = MaterialFactory.Lit(new Color(0.24f, 0.21f, 0.17f), 0.02f);
        var stain = MaterialFactory.Lit(new Color(0.12f, 0.07f, 0.05f), 0.05f);
        var glass = MaterialFactory.Lit(new Color(0.16f, 0.18f, 0.14f), 0.7f);

        // The bed, along the bedroom's outside wall.
        var bed = new GameObject("Bed").transform;
        bed.SetParent(transform, false);
        bed.localPosition = new Vector3(-width * 0.5f + 0.6f, floorY, -1.9f);
        bed.localRotation = Quaternion.Euler(0f, R(-3f, 3f), 0f);
        Box(iron, new Vector3(0f, 0.35f, 0f), new Vector3(0.95f, 0.06f, 1.95f), Vector3.zero, true, bed);
        foreach (float z in new[] { -1f, 1f })
        {
            Box(iron, new Vector3(0f, z < 0f ? 0.5f : 0.4f, z * 0.98f), new Vector3(0.98f, z < 0f ? 1f : 0.8f, 0.04f), Vector3.zero, false, bed);
            foreach (float x in new[] { -0.47f, 0.47f })
                Box(iron, new Vector3(x, 0.5f, z * 0.98f), new Vector3(0.05f, 1f, 0.05f), Vector3.zero, false, bed);
        }
        Box(cloth, new Vector3(0f, 0.45f, 0f), new Vector3(0.88f, 0.14f, 1.85f), new Vector3(0f, 0f, R(-2f, 2f)), false, bed);
        Box(stain, new Vector3(R(-0.1f, 0.1f), 0.522f, R(-0.2f, 0.3f)), new Vector3(0.45f, 0.01f, 0.6f), new Vector3(0f, R(0f, 360f), 0f), false, bed);
        Box(cloth, new Vector3(0f, 0.56f, -0.75f), new Vector3(0.55f, 0.1f, 0.3f), new Vector3(R(-5f, 5f), R(-10f, 10f), 0f), false, bed);

        // The nightstand at its head, where things get left.
        nightstand = new GameObject("Nightstand").transform;
        nightstand.SetParent(transform, false);
        nightstand.localPosition = new Vector3(-width * 0.5f + 0.35f, floorY, -depth * 0.5f + 0.3f);
        Box(darkWood, new Vector3(0f, 0.28f, 0f), new Vector3(0.42f, 0.56f, 0.4f), Vector3.zero, true, nightstand);
        Box(wood, new Vector3(0f, 0.57f, 0f), new Vector3(0.46f, 0.03f, 0.44f), Vector3.zero, false, nightstand);

        // The store room: shelves against the back wall, jars on them; crates and a barrel.
        var shelves = new GameObject("Shelves").transform;
        shelves.SetParent(transform, false);
        shelves.localPosition = new Vector3(2.2f, floorY, -depth * 0.5f + 0.25f);
        for (int i = 0; i < 4; i++)
        {
            float y = 0.25f + i * 0.5f;
            Box(wood, new Vector3(0f, y, 0f), new Vector3(1.6f, 0.03f, 0.35f), new Vector3(0f, 0f, R(-1.5f, 1.5f)), true, shelves);
            int jars = rng.Next(1, 5);
            for (int j = 0; j < jars; j++)
            {
                var jar = Box(glass, new Vector3(R(-0.7f, 0.7f), y + 0.09f, R(-0.08f, 0.08f)), new Vector3(0.1f, 0.15f, 0.1f), new Vector3(0f, R(0f, 90f), 0f), false, shelves);
                jar.name = "Jar";
            }
        }
        foreach (float x in new[] { -0.78f, 0.78f })
            Box(darkWood, new Vector3(x, 1f, 0f), new Vector3(0.05f, 2f, 0.35f), Vector3.zero, false, shelves);
        for (int i = 0; i < 3; i++)
        {
            float s = R(0.45f, 0.65f);
            // One on its own, two stacked by the wall (clear of the door's swing).
            Vector3 at = i == 0 ? new Vector3(R(2.6f, 3.2f), floorY + s * 0.5f, R(-2.4f, -2f))
                       : new Vector3(3.8f, floorY + s * 0.5f + (i == 2 ? 0.66f : 0f), -1.2f);
            var crate = Box(wood, at, Vector3.one * s, new Vector3(0f, R(0f, 90f), 0f), true);
            crate.name = "Crate";
        }
        var barrel = GameObject.CreatePrimitive(PrimitiveType.Cylinder).transform;
        barrel.SetParent(transform, false);
        barrel.name = "Barrel";
        barrel.localPosition = new Vector3(0.55f, floorY + 0.45f, -0.4f);
        barrel.localScale = new Vector3(0.6f, 0.45f, 0.6f);
        barrel.GetComponent<Renderer>().sharedMaterial = darkWood;

        // On the store room floor by the shelves, the drone.
        droneSpot = new GameObject("Drone Spot").transform;
        droneSpot.SetParent(transform, false);
        droneSpot.localPosition = new Vector3(1.1f, floorY + 0.02f, -2.4f);
    }

    /// <summary>A door in a doorway at wallCenter-ish (the doorway's foot, on the wall line), hinged on its left edge.</summary>
    ShackDoor MakeDoor(Vector3 doorway, Material wood, Material darkWood)
    {
        var hinge = new GameObject("Door Hinge").transform;
        hinge.SetParent(transform, false);
        hinge.localPosition = new Vector3(doorway.x - DoorWidth * 0.5f, floorY, doorway.z);
        var d = hinge.gameObject.AddComponent<ShackDoor>();
        var panel = Box(darkWood, new Vector3(DoorWidth * 0.5f, DoorHeight * 0.5f, 0f), new Vector3(DoorWidth - 0.04f, DoorHeight - 0.02f, 0.05f), Vector3.zero, true, hinge);
        panel.name = "Door";
        for (int i = 0; i < 3; i++)
            Box(wood, new Vector3(DoorWidth * 0.5f, 0.3f + i * 0.7f, 0.035f), new Vector3(DoorWidth - 0.08f, 0.1f, 0.03f), Vector3.zero, false, hinge);
        Box(darkWood, new Vector3(DoorWidth - 0.12f, DoorHeight * 0.5f, 0.06f), new Vector3(0.04f, 0.12f, 0.04f), Vector3.zero, false, hinge); // handle
        doors.Add(d);
        return d;
    }

    /// <summary>Puts a fresh torch and lighter on the table (removing any still there) and shuts the doors.</summary>
    public void ResetShack()
    {
        foreach (var d in doors) if (d != null) d.Close();
        SpawnItems();
        if (radio != null) radio.ResetRadio();
        if (torch != null) torch.ResetTorch();
    }

    void SpawnItems()
    {
        if (table == null) return;
        foreach (var spot in new[] { table, nightstand, droneSpot })
        {
            if (spot == null) continue;
            foreach (var old in spot.GetComponentsInChildren<ItemPickup>())
            {
                if (Application.isPlaying) Destroy(old.gameObject);
                else DestroyImmediate(old.gameObject);
            }
            foreach (var old in spot.GetComponentsInChildren<BatteryPickup>())
            {
                if (Application.isPlaying) Destroy(old.gameObject);
                else DestroyImmediate(old.gameObject);
            }
        }
        // Sometimes someone left a battery on the table too.
        if (Random.value < batteryChance)
            BatterySpawner.Create(table, new Vector3(0.05f, 0.78f + 0.025f, 0.18f), Random.Range(-20f, 20f));
        ItemPickup.CreateTorch(table, new Vector3(-0.2f, 0.805f, 0.05f), Random.Range(-15f, 15f));
        ItemPickup.CreateLighter(table, new Vector3(0.3f, 0.805f, -0.08f), Random.Range(0f, 360f));
        // Now and then, the car's key fob.
        if (Random.value < keyFobChance)
            ItemPickup.CreateKeyFob(table, new Vector3(-0.4f, 0.805f, -0.15f), Random.Range(0f, 360f));
        // In the cabin, a battery on the nightstand in the dark bedroom, always.
        if (nightstand != null)
            BatterySpawner.Create(nightstand, new Vector3(0f, 0.585f + 0.025f, 0f), Random.Range(0f, 360f));
        // And in its store room, the drone.
        if (droneSpot != null) ItemPickup.CreateDrone(droneSpot, Vector3.zero, Random.Range(-40f, 40f));
    }

    /// <summary>
    /// Wooden steps from the doorway down to the ground: plank treads on two sloping stringers, as many as
    /// the drop needs, open underneath. Each tread is solid to walk on.
    /// </summary>
    void BuildSteps(DarkForestGenerator generator, Material wood, Material darkWood, System.Random rng)
    {
        float R(float a, float b) => a + (float)rng.NextDouble() * (b - a);
        const float run = 0.3f, maxRise = 0.2f;
        float front = depth * 0.5f + 0.06f;

        // How far down the ground is in front of the door (it can fall away on a slope).
        float Ground(float z)
        {
            Vector3 w = transform.TransformPoint(new Vector3(doorX, 0f, z));
            return generator.GetGroundHeight(w.x, w.z) - transform.position.y;
        }
        // Step down until the stairs meet the ground, wherever that is: on a slope it keeps falling away, so
        // it's checked under each step rather than once by the door.
        float rise = maxRise;
        int steps = 1;
        while (steps < 16 && floorY - steps * rise > Ground(front + (steps - 0.5f) * run) + 0.03f) steps++;
        float drop = steps * rise;
        float stairWidth = DoorWidth + 0.2f;

        for (int i = 1; i <= steps; i++)
        {
            float top = floorY - i * rise;
            float z = front + (i - 0.5f) * run;
            var tread = Box(wood, new Vector3(doorX, top - 0.025f, z), new Vector3(stairWidth, 0.05f, run + 0.03f), new Vector3(R(-1.5f, 1.5f), 0f, R(-1.5f, 1.5f)), true);
            tread.name = "Step";
        }

        // Stringers: two boards sloping from the doorway down either side of the treads, carried on a step
        // further so they run into the ground rather than stopping short of it.
        float reach = (steps + 1) * run, fall = drop + rise;
        float length = Mathf.Sqrt(fall * fall + reach * reach);
        float angle = Mathf.Atan2(fall, reach) * Mathf.Rad2Deg;
        for (int side = -1; side <= 1; side += 2)
        {
            Box(darkWood, new Vector3(doorX + side * (stairWidth * 0.5f + 0.02f), floorY - fall * 0.5f - 0.08f, front + reach * 0.5f),
                new Vector3(0.05f, 0.18f, length), new Vector3(angle, 0f, 0f), false);
        }
    }

    /// <summary>
    /// A wall of upright planks with gaps and uneven tops, with doorways (centred at each of doorways, along
    /// the wall) and optionally a window (centred at windowAt, between windowBottom and windowTop). A gable
    /// wall's planks run on up to the roof. outward is the side the cross battens go on.
    /// </summary>
    void PlankWall(Material mat, System.Random rng, Vector3 center, float length, float yaw, Vector3 outward, float[] doorways,
        float windowAt, float windowBottom, float windowTop, bool gable)
    {
        float R(float a, float b) => a + (float)rng.NextDouble() * (b - a);
        Quaternion rot = Quaternion.Euler(0f, yaw, 0f);
        int planks = Mathf.RoundToInt(length / 0.26f);
        float pitch = length / planks;
        float slope = Mathf.Tan(RoofPitch * Mathf.Deg2Rad);

        for (int i = 0; i < planks; i++)
        {
            float along = -length * 0.5f + pitch * (i + 0.5f);
            if (rng.NextDouble() < 0.06) continue; // a missing plank

            float top = wallHeight - R(0f, 0.15f);
            if (gable) top = wallHeight + slope * (length * 0.5f - Mathf.Abs(along)) - R(0f, 0.12f);
            bool inDoor = false;
            foreach (float d in doorways) if (Mathf.Abs(along - d) < DoorWidth * 0.5f) inDoor = true;
            bool inWindow = windowTop > 0f && Mathf.Abs(along - windowAt) < 0.6f;

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

        // A couple of cross battens, stopping either side of each doorway rather than boarding it up.
        var spans = new List<Vector2>();
        float from = -length * 0.5f;
        var sorted = new List<float>(doorways);
        sorted.Sort();
        foreach (float d in sorted)
        {
            spans.Add(new Vector2(from, d - DoorWidth * 0.5f - 0.04f));
            from = d + DoorWidth * 0.5f + 0.04f;
        }
        spans.Add(new Vector2(from, length * 0.5f));

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
                batten.localPosition += outward * 0.04f; // on the outside face of the planks
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

    /// <summary>The solid of a wall (a box either side of each doorway, and a lintel over it).</summary>
    void WallColliders(Vector3 center, float length, float yaw, float[] doorways)
    {
        var go = new GameObject("Wall Collider");
        go.transform.SetParent(transform, false);
        go.transform.localPosition = center + Vector3.up * floorY;
        go.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
        void Solid(float a, float b, float bottom, float top)
        {
            if (b - a < 0.01f || top - bottom < 0.01f) return;
            var box = go.AddComponent<BoxCollider>();
            box.center = new Vector3((a + b) * 0.5f, (bottom + top) * 0.5f, 0f);
            box.size = new Vector3(b - a, top - bottom, 0.12f);
        }
        var sorted = new List<float>(doorways);
        sorted.Sort();
        float from = -length * 0.5f;
        foreach (float d in sorted)
        {
            Solid(from, d - DoorWidth * 0.5f, 0f, wallHeight);
            Solid(d - DoorWidth * 0.5f, d + DoorWidth * 0.5f, DoorHeight, wallHeight); // the lintel
            from = d + DoorWidth * 0.5f;
        }
        Solid(from, length * 0.5f, 0f, wallHeight);
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
