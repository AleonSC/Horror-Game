using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Game.Entities
{
    /// <summary>
    /// Builds one mesh out of simple shapes (tubes, ellipsoids, curves), each shape moving rigidly with one
    /// bone. The result is a skinned mesh: one draw for the whole creature, posed by rotating its bones.
    /// </summary>
    public sealed class SkinnedBuilder
    {
        readonly List<Vector3> vertices = new List<Vector3>();
        readonly List<int> triangles = new List<int>();
        readonly List<BoneWeight> weights = new List<BoneWeight>();

        int Vertex(Vector3 p, int bone)
        {
            vertices.Add(p);
            weights.Add(new BoneWeight { boneIndex0 = bone, weight0 = 1f });
            return vertices.Count - 1;
        }

        void Triangle(int a, int b, int c)
        {
            triangles.Add(a);
            triangles.Add(b);
            triangles.Add(c);
        }

        /// <summary>A tapered tube from a to b. Clockwise seen from outside (Unity's front faces).</summary>
        public void Tube(Vector3 a, Vector3 b, float radiusA, float radiusB, int bone, int sides = 6)
        {
            Vector3 axis = b - a;
            if (axis.sqrMagnitude < 1e-8f) return;
            Vector3 dir = axis.normalized;
            Vector3 side = Vector3.Cross(dir, Mathf.Abs(dir.y) < 0.99f ? Vector3.up : Vector3.right).normalized;
            Vector3 side2 = Vector3.Cross(dir, side);
            int start = vertices.Count;
            for (int i = 0; i <= sides; i++)
            {
                float t = i / (float)sides * Mathf.PI * 2f;
                Vector3 o = side * Mathf.Cos(t) + side2 * Mathf.Sin(t);
                Vertex(a + o * radiusA, bone);
                Vertex(b + o * radiusB, bone);
            }
            for (int i = 0; i < sides; i++)
            {
                int v0 = start + i * 2, v1 = v0 + 1, v2 = v0 + 2, v3 = v0 + 3;
                Triangle(v0, v2, v1);
                Triangle(v2, v3, v1);
            }
        }

        /// <summary>Tubes along a polyline, the radius going from r0 to r1.</summary>
        public void Curve(IList<Vector3> points, float r0, float r1, int bone, int sides = 5)
        {
            for (int i = 0; i < points.Count - 1; i++)
            {
                float ta = i / (float)(points.Count - 1), tb = (i + 1) / (float)(points.Count - 1);
                Tube(points[i], points[i + 1], Mathf.Lerp(r0, r1, ta), Mathf.Lerp(r0, r1, tb), bone, sides);
            }
        }

        /// <summary>An ellipsoid (knobs, joints, the skull).</summary>
        public void Ellipsoid(Vector3 centre, Vector3 radii, int bone, int rings = 6, int segments = 8)
        {
            var index = new int[rings + 1, segments + 1];
            for (int r = 0; r <= rings; r++)
            {
                float phi = r / (float)rings * Mathf.PI;
                for (int s = 0; s <= segments; s++)
                {
                    float theta = s / (float)segments * Mathf.PI * 2f;
                    var n = new Vector3(Mathf.Sin(phi) * Mathf.Cos(theta), Mathf.Cos(phi), Mathf.Sin(phi) * Mathf.Sin(theta));
                    index[r, s] = Vertex(centre + Vector3.Scale(n, radii), bone);
                }
            }
            for (int r = 0; r < rings; r++)
            for (int s = 0; s < segments; s++)
            {
                Triangle(index[r, s], index[r, s + 1], index[r + 1, s]);
                Triangle(index[r + 1, s], index[r, s + 1], index[r + 1, s + 1]);
            }
        }

        /// <summary>A small cone from base to tip (fangs).</summary>
        public void Cone(Vector3 basePoint, Vector3 tip, float radius, int bone) => Tube(basePoint, tip, radius, 0f, bone, 4);

        /// <summary>A plain (unskinned) mesh of the shapes, for parts that ride on one bone as a child object.</summary>
        public Mesh ToStaticMesh(string name)
        {
            var mesh = new Mesh { name = name };
            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        public Mesh ToMesh(string name, Matrix4x4[] bindposes)
        {
            var mesh = new Mesh { name = name };
            if (vertices.Count > 65000) mesh.indexFormat = IndexFormat.UInt32;
            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0);
            mesh.boneWeights = weights.ToArray();
            mesh.bindposes = bindposes;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
