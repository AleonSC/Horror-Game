using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Game.World.Build
{
    /// <summary>Collects triangles and turns them into a Mesh. Shapes can be added with a transform (for merging).</summary>
    public sealed class MeshBuilder
    {
        readonly List<Vector3> vertices = new List<Vector3>();
        readonly List<Vector2> uvs = new List<Vector2>();
        readonly List<int> triangles = new List<int>();

        public int VertexCount => vertices.Count;

        public int AddVertex(Vector3 position, Vector2 uv)
        {
            vertices.Add(position);
            uvs.Add(uv);
            return vertices.Count - 1;
        }

        public void AddTriangle(int a, int b, int c)
        {
            triangles.Add(a);
            triangles.Add(b);
            triangles.Add(c);
        }

        /// <summary>A tapered tube from <paramref name="from"/> to <paramref name="to"/> (trunks, branches, limbs).</summary>
        public void AddTube(Vector3 from, Vector3 to, float radiusFrom, float radiusTo, int sides, bool capTop = false)
        {
            Vector3 axis = to - from;
            if (axis.sqrMagnitude < 1e-6f) return;
            Vector3 dir = axis.normalized;
            Vector3 side = Vector3.Cross(dir, Mathf.Abs(dir.y) < 0.99f ? Vector3.up : Vector3.right).normalized;
            Vector3 side2 = Vector3.Cross(dir, side);
            int start = vertices.Count;
            for (int i = 0; i <= sides; i++)
            {
                float a = i / (float)sides * Mathf.PI * 2f;
                Vector3 offset = side * Mathf.Cos(a) + side2 * Mathf.Sin(a);
                AddVertex(from + offset * radiusFrom, new Vector2(i / (float)sides, 0f));
                AddVertex(to + offset * radiusTo, new Vector2(i / (float)sides, axis.magnitude));
            }
            for (int i = 0; i < sides; i++)
            {
                int a = start + i * 2, b = a + 1, c = a + 2, d = a + 3;
                AddTriangle(a, c, b); // (clockwise seen from outside: Unity's front face)
                AddTriangle(c, d, b);
            }
            if (capTop && radiusTo > 0f)
            {
                int centre = AddVertex(to, new Vector2(0.5f, 0.5f));
                for (int i = 0; i < sides; i++) AddTriangle(start + i * 2 + 1, start + i * 2 + 3, centre);
            }
        }

        /// <summary>Copy another builder's triangles in, moved by <paramref name="matrix"/>.</summary>
        public void Append(MeshBuilder other, Matrix4x4 matrix)
        {
            int offset = vertices.Count;
            for (int i = 0; i < other.vertices.Count; i++)
            {
                vertices.Add(matrix.MultiplyPoint3x4(other.vertices[i]));
                uvs.Add(other.uvs[i]);
            }
            foreach (int t in other.triangles) triangles.Add(t + offset);
        }

        /// <summary>Copy a mesh's triangles in (positions and first UVs), moved by <paramref name="matrix"/>.</summary>
        public void AppendMesh(Mesh mesh, Matrix4x4 matrix)
        {
            int offset = vertices.Count;
            var v = mesh.vertices;
            var uv = mesh.uv;
            for (int i = 0; i < v.Length; i++)
            {
                vertices.Add(matrix.MultiplyPoint3x4(v[i]));
                uvs.Add(uv.Length == v.Length ? uv[i] : Vector2.zero);
            }
            foreach (int t in mesh.triangles) triangles.Add(t + offset);
        }

        /// <summary>
        /// A turned shape: a profile of (radius, height) points, bottom to top on the outside, spun round the Y axis
        /// at <paramref name="centre"/> with this many sides. Each step of the profile is its own band (a crisp edge
        /// between mouldings); <paramref name="flat"/> makes each side flat too (a six-sided lantern), otherwise it's
        /// smooth all round. A profile starting or ending at radius 0 is closed there.
        /// </summary>
        public void AddLathe(Vector3 centre, Vector2[] profile, int sides, bool flat, float turn = 0f)
        {
            for (int k = 0; k < profile.Length - 1; k++)
            {
                Vector2 p0 = profile[k], p1 = profile[k + 1];
                if (flat)
                {
                    for (int i = 0; i < sides; i++)
                    {
                        Vector3 d0 = Around(i, sides, turn), d1 = Around(i + 1, sides, turn);
                        int a = AddVertex(centre + d0 * p0.x + Vector3.up * p0.y, new Vector2(0f, p0.y));
                        int b = AddVertex(centre + d0 * p1.x + Vector3.up * p1.y, new Vector2(0f, p1.y));
                        int c = AddVertex(centre + d1 * p0.x + Vector3.up * p0.y, new Vector2(1f, p0.y));
                        int d = AddVertex(centre + d1 * p1.x + Vector3.up * p1.y, new Vector2(1f, p1.y));
                        AddTriangle(a, b, c);
                        AddTriangle(c, b, d);
                    }
                    continue;
                }
                int start = vertices.Count;
                for (int i = 0; i <= sides; i++)
                {
                    Vector3 dir = Around(i, sides, turn);
                    AddVertex(centre + dir * p0.x + Vector3.up * p0.y, new Vector2(i / (float)sides, p0.y));
                    AddVertex(centre + dir * p1.x + Vector3.up * p1.y, new Vector2(i / (float)sides, p1.y));
                }
                for (int i = 0; i < sides; i++)
                {
                    int a = start + i * 2, b = a + 1, c = a + 2, d = a + 3;
                    AddTriangle(a, b, c); // (clockwise seen from outside)
                    AddTriangle(c, b, d);
                }
            }
        }

        static Vector3 Around(int i, int sides, float turn)
        {
            float a = i / (float)sides * Mathf.PI * 2f + turn;
            return new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
        }

        public Mesh ToMesh(string name)
        {
            var mesh = new Mesh { name = name };
            if (vertices.Count > 65000) mesh.indexFormat = IndexFormat.UInt32;
            mesh.SetVertices(vertices);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateTangents();
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
