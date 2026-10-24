using System.Collections.Generic;
using Game.Core;
using Game.Rendering;
using UnityEngine;

namespace Game.World.Build
{
    /// <summary>
    /// The room through the lone door (section 6.5): a vast black floor far below the world, with rows of pillars
    /// rising into the dark, and the twin of the door's frame standing in the middle. Nothing lights it but what you
    /// bring.
    /// </summary>
    public static class DarkRoom
    {
        public const float Depth = 600f;
        const float Size = 220f, PillarStep = 16f, PillarHeight = 45f;

        /// <summary>Build it under the door. Returns the twin frame (the doorway you arrive by).</summary>
        public static Transform Build(Vector3 doorAt, float doorYaw, Transform parent, List<Object> made)
        {
            var root = new GameObject("Dark Room").transform;
            root.SetParent(parent, false);
            root.SetPositionAndRotation(new Vector3(doorAt.x, doorAt.y - Depth, doorAt.z), Quaternion.Euler(0f, doorYaw, 0f));

            var floor = RuntimeMaterials.Lit("Dark room floor", new Color(0.02f, 0.02f, 0.022f), 0.55f);
            var stone = RuntimeMaterials.Lit("Dark room pillars", new Color(0.03f, 0.03f, 0.032f), 0.3f);
            made.Add(floor);
            made.Add(stone);

            var f = Shape(PrimitiveType.Cube, root, new Vector3(0f, -0.5f, 0f), new Vector3(Size, 1f, Size), floor, Layers.Ground);
            f.name = "Floor";
            // Pillars in rows, leaving the middle clear round the doorway.
            for (float x = -Size * 0.5f + PillarStep; x < Size * 0.5f; x += PillarStep)
            for (float z = -Size * 0.5f + PillarStep; z < Size * 0.5f; z += PillarStep)
            {
                if (Mathf.Abs(x) < PillarStep * 0.6f && Mathf.Abs(z) < PillarStep * 0.6f) continue;
                Shape(PrimitiveType.Cylinder, root, new Vector3(x, PillarHeight * 0.5f, z), new Vector3(2.4f, PillarHeight * 0.5f, 2.4f), stone, Layers.Props);
            }
            // Unseen walls at the edge, so you can't walk off into nothing.
            for (int i = 0; i < 4; i++)
            {
                var wall = new GameObject("Edge") { layer = Layers.Props };
                wall.transform.SetParent(root, false);
                wall.transform.localRotation = Quaternion.Euler(0f, i * 90f, 0f);
                wall.transform.localPosition = wall.transform.localRotation * new Vector3(0f, 5f, Size * 0.5f);
                wall.AddComponent<BoxCollider>().size = new Vector3(Size, 10f, 1f);
            }

            // The twin of the door's frame (no door in it: always open).
            var frame = new GameObject("Dark Room Door").transform;
            frame.SetParent(root, false);
            Shape(PrimitiveType.Cube, frame, new Vector3(-0.65f, 1.15f, 0f), new Vector3(0.14f, 2.3f, 0.2f), stone, Layers.Props);
            Shape(PrimitiveType.Cube, frame, new Vector3(0.65f, 1.15f, 0f), new Vector3(0.14f, 2.3f, 0.2f), stone, Layers.Props);
            Shape(PrimitiveType.Cube, frame, new Vector3(0f, 2.36f, 0f), new Vector3(1.44f, 0.14f, 0.2f), stone, Layers.Props);
            return frame;
        }

        static GameObject Shape(PrimitiveType type, Transform parent, Vector3 at, Vector3 scale, Material m, int layer)
        {
            var go = GameObject.CreatePrimitive(type);
            go.layer = layer;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = at;
            go.transform.localScale = scale;
            go.GetComponent<Renderer>().sharedMaterial = m;
            return go;
        }
    }
}
