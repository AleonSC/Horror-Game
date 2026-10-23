using System.Collections.Generic;
using Game.World.Generation;
using UnityEngine;

namespace Game.World.Build
{
    /// <summary>
    /// Makes the map wrap (section 3.2). Instead of moving the player at the edge, each piece of the world (a
    /// 32 m chunk of ground and its trees' colliders, a lamp, a building) is kept at whichever copy of it is
    /// nearest the camera. Only pieces half a map away ever jump, deep in the fog.
    /// </summary>
    public sealed class WorldWrap
    {
        struct Item
        {
            public Transform Transform;
            public Vector3 Home;
            public Vector2 Offset;
        }

        readonly float size;
        readonly List<Item> items = new List<Item>();

        public WorldWrap(float mapSize) => size = mapSize;

        public float Size => size;

        /// <summary>Keep this object wrapped. <paramref name="home"/>: its place on the map (x, z in 0..size).</summary>
        public void Add(Transform t, Vector3 home)
        {
            t.position = home;
            items.Add(new Item { Transform = t, Home = home, Offset = Vector2.zero });
        }

        public void Update(Vector3 viewer)
        {
            var v = new Vector2(viewer.x, viewer.z);
            for (int i = 0; i < items.Count; i++)
            {
                var item = items[i];
                if (item.Transform == null) continue;
                Vector2 offset = Wrap.NearestCopyOffset(new Vector2(item.Home.x, item.Home.z), v, size);
                if (offset == item.Offset) continue;
                item.Offset = offset;
                item.Transform.position = item.Home + new Vector3(offset.x, 0f, offset.y);
                items[i] = item;
            }
        }

        /// <summary>The copy of a map point nearest the viewer, in the world.</summary>
        public Vector3 Nearest(Vector3 home, Vector3 viewer)
        {
            Vector2 o = Wrap.NearestCopyOffset(new Vector2(home.x, home.z), new Vector2(viewer.x, viewer.z), size);
            return home + new Vector3(o.x, 0f, o.y);
        }

        public void Clear() => items.Clear();
    }
}
