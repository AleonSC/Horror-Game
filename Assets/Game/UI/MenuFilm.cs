using Game.Core;
using Game.World;
using Game.World.Build;
using Game.World.Generation;
using UnityEngine;

namespace Game.UI
{
    /// <summary>
    /// The slow film behind the main menu (section 7), shot in the generated forest: high over the treetops,
    /// craning down onto a lamp or a camp, the grazing deer, an owl on its branch, creeping low through the mist.
    /// Each shot cuts through black. No monsters are out, and no shot looks toward the giant.
    /// </summary>
    public sealed class MenuFilm
    {
        enum Shot { Treetops, Crane, Deer, Owl, Mist }

        readonly Camera cam;
        readonly float shotTime;
        Vector3 fromPos, toPos, fromLook, toLook;
        bool followGround;
        float t;
        int last = -1;

        /// <summary>How black the screen is (0 clear, 1 black): the cut between shots.</summary>
        public float Black { get; private set; } = 1f;

        public MenuFilm(Camera cam, float shotTime)
        {
            this.cam = cam;
            this.shotTime = Mathf.Max(3f, shotTime);
            NextShot();
        }

        public void Update(float dt)
        {
            t += dt / shotTime;
            if (t >= 1f) NextShot();
            float u = Mathf.SmoothStep(0f, 1f, t);
            Vector3 p = Vector3.Lerp(fromPos, toPos, u);
            if (followGround) p.y = Ground(p) + 0.7f;
            Vector3 look = Vector3.Lerp(fromLook, toLook, u);
            cam.transform.SetPositionAndRotation(p, Quaternion.LookRotation(look - p));
            // Black at each cut, clearing over the first second and closing over the last.
            float edge = 0.9f / shotTime;
            Black = Mathf.Max(1f - Mathf.Clamp01(t / edge), Mathf.Clamp01((t - (1f - edge)) / edge));
        }

        void NextShot()
        {
            t = 0f;
            if (!Services.TryGet<WorldBuilder>(out var world) || world.Plan == null) return;
            var plan = world.Plan;
            Vector3 home = cam.transform.position;
            if (Services.TryGet<Game.Player.PlayerController>(out var player)) home = player.transform.position;

            // A different kind of shot each time.
            int pick;
            do pick = Random.Range(0, 5); while (pick == last);
            last = pick;
            var shot = (Shot)pick;
            if (shot == Shot.Deer && !(Services.TryGet<Wildlife>(out var w) && w.HerdOut)) shot = Shot.Mist;
            if (shot == Shot.Owl && (Owls.Current == null || Owls.Current.Count == 0)) shot = Shot.Treetops;

            Vector3 f = AwayFromGiant(plan, Quaternion.Euler(0f, Random.Range(0f, 360f), 0f) * Vector3.forward);
            followGround = false;
            switch (shot)
            {
                case Shot.Treetops:
                {
                    Vector3 c = home + Random.insideUnitSphere.Flat() * 40f;
                    float y = Ground(c) + 42f;
                    fromPos = new Vector3(c.x, y, c.z);
                    toPos = fromPos + f * 30f;
                    fromLook = fromPos + f * 40f + Vector3.down * 12f;
                    toLook = toPos + f * 40f + Vector3.down * 14f;
                    break;
                }
                case Shot.Crane:
                {
                    Vector3 target = Target(world, plan, home);
                    f = AwayFromGiant(plan, Flat(target - home).sqrMagnitude > 1f ? Flat(target - home).normalized : f);
                    fromPos = target - f * 22f + Vector3.up * 28f;
                    toPos = target - f * 8f + Vector3.up * 3f;
                    fromLook = toLook = target + Vector3.up * 1.2f;
                    break;
                }
                case Shot.Deer:
                {
                    Vector3 herd = Services.Get<Wildlife>().HerdCentre;
                    Vector3 side = Vector3.Cross(Vector3.up, f);
                    fromPos = herd - f * 14f + Vector3.up * 1.6f - side * 2f;
                    toPos = fromPos + side * 4f;
                    fromLook = toLook = herd + Vector3.up * 1f;
                    break;
                }
                case Shot.Owl:
                {
                    var owls = Owls.Current;
                    Transform nearest = owls.OwlAt(0);
                    for (int i = 1; i < owls.Count; i++)
                        if ((owls.OwlAt(i).position - home).sqrMagnitude < (nearest.position - home).sqrMagnitude) nearest = owls.OwlAt(i);
                    Vector3 o = nearest.position;
                    fromPos = o - f * 3.8f - Vector3.up * 0.4f;
                    toPos = o - f * 3f - Vector3.up * 0.2f + Vector3.Cross(Vector3.up, f) * 0.6f;
                    fromLook = toLook = o + Vector3.up * 0.3f;
                    break;
                }
                default: // creeping low through the mist
                {
                    Vector3 c = home + Random.insideUnitSphere.Flat() * 25f;
                    fromPos = c;
                    toPos = c + f * 11f;
                    fromLook = fromPos + f * 20f + Vector3.up * 0.3f;
                    toLook = toPos + f * 20f + Vector3.up * 0.6f;
                    followGround = true;
                    break;
                }
            }
        }

        // A lamp (unlit: its marker blinking) or a campsite, the nearest copy of it.
        static Vector3 Target(WorldBuilder world, WorldPlan plan, Vector3 near)
        {
            Vector2 at;
            var camps = plan.Structures.FindAll(st => st.Kind == StructureKind.Campsite);
            if (camps.Count > 0 && Random.value < 0.5f) at = camps[Random.Range(0, camps.Count)].Position;
            else at = plan.Lamps.Count > 0 ? plan.Lamps[Random.Range(0, plan.Lamps.Count)].Position : plan.Start;
            Vector3 home = new Vector3(at.x, plan.GroundAt(at), at.y);
            return world.Wrapping != null ? world.Wrapping.Nearest(home, near) : home;
        }

        // Never look toward the giant: turn the shot well away from its bearing.
        static Vector3 AwayFromGiant(WorldPlan plan, Vector3 f)
        {
            var b = SightlineStage.Direction(plan.GiantBearing);
            var giant = new Vector3(b.x, 0f, b.y);
            if (Vector3.Angle(Flat(f), giant) < 60f) f = Quaternion.Euler(0f, 130f, 0f) * f;
            return Flat(f).normalized;
        }

        static float Ground(Vector3 p)
        {
            if (Physics.Raycast(p + Vector3.up * 80f, Vector3.down, out var hit, 200f, Layers.Mask(Layers.Ground), QueryTriggerInteraction.Ignore)) return hit.point.y;
            return p.y;
        }

        static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0f, v.z);
    }

    static class VectorFlat
    {
        public static Vector3 Flat(this Vector3 v) => new Vector3(v.x, 0f, v.z);
    }
}
