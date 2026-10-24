using UnityEngine;

namespace Game.Core
{
    /// <summary>
    /// The wind (section 6.13): a steady breeze, gusts that roll across the forest in bands, and shockwaves (the
    /// giant landing) that whip everything over as they pass. Anything that sways asks it how hard it's pushed at a
    /// point. The weather sets the strengths. Plain C# (time is passed in), so it can be tested.
    /// </summary>
    public sealed class Wind
    {
        public float Breeze = 0.25f;        // the steady sway (0-1)
        public float Gusts = 1f;            // how strong the gusts are (0-1+)
        public float GustSpeed = 9f;        // how fast they roll across (metres a second)
        public float GustSpacing = 70f;     // metres between gust fronts
        public Vector2 Direction = new Vector2(0.8f, 0.6f);

        Vector3 shockFrom;
        float shockStart = -1000f, shockSpeed = 220f, shockStrength;

        // Worked out once per frame by Prepare, so At is cheap enough for thousands of plants.
        float now, dirX = 1f, dirZ, gustK, breezeT, patchT;
        bool shockLive;

        /// <summary>Get ready to sample at this time (call once a frame before many At calls).</summary>
        public void Prepare(float time)
        {
            now = time;
            Vector2 dir = Direction.sqrMagnitude > 0.0001f ? Direction.normalized : new Vector2(1f, 0f);
            dirX = dir.x;
            dirZ = dir.y;
            gustK = Mathf.PI * 2f / Mathf.Max(1f, GustSpacing);
            breezeT = time * 1.3f;
            patchT = time * 0.05f;
            float since = time - shockStart;
            shockLive = since >= 0f && since <= 20f;
        }

        /// <summary>How hard the wind pushes at this point (x, z), and which way: a vector, about 0 to 1 long.</summary>
        public Vector2 At(float x, float z, float time)
        {
            if (time != now) Prepare(time);
            return At(x, z);
        }

        /// <summary>The push at a point, at the time given to <see cref="Prepare"/>.</summary>
        public Vector2 At(float x, float z)
        {
            float along = x * dirX + z * dirZ;
            float across = -x * dirZ + z * dirX;
            // A slow breeze, out of step from place to place so it doesn't look like one sheet moving.
            float breeze = Breeze * (0.6f + 0.4f * Mathf.Sin(breezeT + along * 0.11f + across * 0.07f));
            // Gust fronts rolling downwind, stronger in some stretches than others.
            float front = Mathf.Sin((along - now * GustSpeed) * gustK);
            front = front > 0f ? front * front * front : 0f;
            float patchy = 0.55f + 0.45f * Mathf.Sin(across * 0.031f + patchT);
            float total = breeze + Gusts * front * patchy;
            var push = new Vector2(dirX * total, dirZ * total);
            if (shockLive) push += Shock(x, z, now);
            return push;
        }

        /// <summary>How gusty it is right here, 0 calm to 1+ (for the wind's sound and tree creaks).</summary>
        public float Strength(float x, float z, float time) => At(x, z, time).magnitude;

        /// <summary>A shockwave rolling out from a point (the giant landing).</summary>
        public void AddShock(Vector3 from, float time, float speed, float strength = 1.5f)
        {
            shockFrom = from;
            shockStart = time;
            shockSpeed = Mathf.Max(1f, speed);
            shockStrength = strength;
        }

        Vector2 Shock(float x, float z, float time)
        {
            float since = time - shockStart;
            if (since < 0f || since > 20f) return Vector2.zero;
            float dx = x - shockFrom.x, dz = z - shockFrom.z;
            float d = Mathf.Sqrt(dx * dx + dz * dz);
            float ring = since * shockSpeed;
            float k = (d - ring) / 25f;
            float amount = Mathf.Exp(-k * k) * shockStrength * Mathf.Clamp01(1f - since / 20f);
            return d > 0.01f ? new Vector2(dx / d, dz / d) * amount : Vector2.zero;
        }
    }
}
