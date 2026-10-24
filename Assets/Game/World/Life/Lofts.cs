using System.Collections.Generic;
using UnityEngine;

namespace Game.World
{
    /// <summary>Smooth lofted shapes for the built-in animals and their perches (the prototype's way of making them).</summary>
    public static class Lofts
    {
        /// <summary>
        /// A smooth tube along a path of points, each with a cross-section (half-width sideways, half-height the
        /// other way; squareness 2 is an ellipse, more is boxier), the path and sizes eased between the points
        /// (Catmull-Rom), optionally closed at either end.
        /// </summary>
        public static Mesh Tube((Vector3 at, Vector2 radius)[] path, float squareness, bool capStart, bool capEnd)
        {
            const int sides = 14, steps = 4;
            var centers = new List<Vector3>();
            var radii = new List<Vector2>();
            int n = path.Length;
            for (int i = 0; i < n - 1; i++)
                for (int st = 0; st < steps; st++)
                {
                    float t = st / (float)steps;
                    Vector3 p0 = path[Mathf.Max(0, i - 1)].at, p1 = path[i].at, p2 = path[i + 1].at, p3 = path[Mathf.Min(n - 1, i + 2)].at;
                    centers.Add(0.5f * (2f * p1 + (-p0 + p2) * t + (2f * p0 - 5f * p1 + 4f * p2 - p3) * t * t + (-p0 + 3f * p1 - 3f * p2 + p3) * t * t * t));
                    radii.Add(Vector2.Lerp(path[i].radius, path[i + 1].radius, Mathf.SmoothStep(0f, 1f, t)));
                }
            centers.Add(path[n - 1].at);
            radii.Add(path[n - 1].radius);

            var verts = new List<Vector3>();
            var tris = new List<int>();
            float e = 2f / squareness;
            int rings = centers.Count;
            for (int k = 0; k < rings; k++)
            {
                Vector3 tangent = (centers[Mathf.Min(rings - 1, k + 1)] - centers[Mathf.Max(0, k - 1)]).normalized;
                Vector3 side = Vector3.ProjectOnPlane(Vector3.right, tangent).normalized;
                Vector3 other = Vector3.Cross(tangent, side);
                for (int j = 0; j < sides; j++)
                {
                    float a = j / (float)sides * Mathf.PI * 2f;
                    float c = Mathf.Cos(a), sn = Mathf.Sin(a);
                    float x = Mathf.Sign(c) * Mathf.Pow(Mathf.Abs(c), e), y = Mathf.Sign(sn) * Mathf.Pow(Mathf.Abs(sn), e);
                    verts.Add(centers[k] + side * (x * radii[k].x) + other * (y * radii[k].y));
                }
            }
            for (int k = 0; k < rings - 1; k++)
                for (int j = 0; j < sides; j++)
                {
                    int a = k * sides + j, b = k * sides + (j + 1) % sides, c = a + sides, d = b + sides;
                    tris.AddRange(new[] { a, b, c, b, d, c });
                }
            // Caps: their own vertices, so the rims stay smooth on the tube and flat on the cap.
            void Cap(int k, bool start)
            {
                int center = verts.Count;
                verts.Add(centers[k]);
                for (int j = 0; j < sides; j++) verts.Add(verts[k * sides + j]);
                for (int j = 0; j < sides; j++)
                {
                    int v = center + 1 + j, w = center + 1 + (j + 1) % sides;
                    if (start) tris.AddRange(new[] { center, w, v });
                    else tris.AddRange(new[] { center, v, w });
                }
            }
            if (capStart) Cap(0, true);
            if (capEnd) Cap(rings - 1, false);

            var mesh = new Mesh { name = "Loft" };
            mesh.SetVertices(verts);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>A smooth shape under the parent, drawn with this material (see Tube).</summary>
        public static GameObject Shape(Transform parent, string name, Material mat, float squareness, bool capStart, bool capEnd, params (Vector3 at, Vector2 radius)[] path)
        {
            var go = new GameObject(name) { layer = parent.gameObject.layer };
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = Tube(path, squareness, capStart, capEnd);
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return go;
        }
    }
}
