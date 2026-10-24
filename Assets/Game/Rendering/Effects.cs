using Game.Core;
using UnityEngine;
using UnityEngine.Rendering;

namespace Game.Rendering
{
    /// <summary>
    /// The game's own effects (section 4.4), made with its shaders: soft glows (eyes, bulbs, pickups), shafts of
    /// light (floodlights, searchlights) and flames (torch, lighter, flare). If a shader is missing or broken,
    /// these return null and whatever was there before stays (never a pink square).
    /// </summary>
    public static class Effects
    {
        static Mesh quad, cone;
        static bool warned;

        /// <summary>One of the game's shaders ("Game/Glow"), or null (with one warning) if it can't be used.</summary>
        public static Shader GameShader(string name)
        {
            var shader = Shader.Find(name);
            bool broken = shader == null || !shader.isSupported;
#if UNITY_EDITOR
            if (shader != null && UnityEditor.ShaderUtil.ShaderHasError(shader)) broken = true;
#endif
            if (broken)
            {
                if (!warned) Debug.LogWarning($"The shader {name} is missing or has errors, so its effects are left out (see the Console for the shader's own errors).");
                warned = true;
                return null;
            }
            return shader;
        }

        public static Material Material(string shaderName, string name, Color glow)
        {
            var shader = GameShader(shaderName);
            if (shader == null) return null;
            var m = new Material(shader) { name = name, enableInstancing = true };
            m.SetColor("_GlowColor", glow);
            return m;
        }

        /// <summary>A soft glow: a camera-facing disc this wide (metres). Fog: how much the fog dims it (0 none).</summary>
        public static MeshRenderer Glow(Transform parent, Vector3 local, float size, Color glow, float fog = 1f, float falloff = 2.5f, string name = "Glow")
        {
            var m = Material("Game/Glow", name, glow);
            if (m == null) return null;
            m.SetFloat("_FogAmount", fog);
            m.SetFloat("_Falloff", falloff);
            m.SetFloat("_Soft", Mathf.Clamp(size * 0.3f, 0.05f, 2f));
            var r = Make(name, parent, local, Vector3.one * size, Quad, m);
            r.transform.localRotation = Quaternion.identity;
            return r;
        }

        /// <summary>A shaft of light along the parent's +Z: this long, this wide at its far end (radius).</summary>
        public static MeshRenderer Beam(Transform parent, float length, float radius, Color glow, float fog = 0.6f, float dust = 0.35f, string name = "Beam")
        {
            var m = Material("Game/Beam", name, glow);
            if (m == null) return null;
            m.SetFloat("_FogAmount", fog);
            m.SetFloat("_Dust", dust);
            m.SetFloat("_Soft", Mathf.Clamp(radius * 0.5f, 0.2f, 6f));
            return Make(name, parent, Vector3.zero, new Vector3(radius, radius, length), Cone, m);
        }

        /// <summary>A flame standing up from this point, this wide and tall.</summary>
        public static MeshRenderer Flame(Transform parent, Vector3 local, float width, float height, Color brightness, string name = "Flame")
        {
            var m = Material("Game/Flame", name, brightness);
            if (m == null) return null;
            return Make(name, parent, local, new Vector3(width, height, width), Quad, m);
        }

        static MeshRenderer Make(string name, Transform parent, Vector3 local, Vector3 scale, Mesh mesh, Material m)
        {
            var go = new GameObject(name) { layer = Layers.Effects };
            go.transform.SetParent(parent, false);
            go.transform.localPosition = local;
            go.transform.localScale = scale;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = m;
            r.shadowCastingMode = ShadowCastingMode.Off;
            r.receiveShadows = false;
            r.lightProbeUsage = LightProbeUsage.Off;
            r.reflectionProbeUsage = ReflectionProbeUsage.Off;
            return r;
        }

        /// <summary>
        /// An outline of light round a model: a copy of each of its meshes, pushed out a touch, glowing at its edges
        /// (the Rim shader). Returns the material, to change its brightness, or null.
        /// </summary>
        public static Material Outline(Transform model, Color glow, float thickness = 0.006f, float fog = 0.8f)
        {
            var m = Material("Game/Rim", "Outline", glow);
            if (m == null || model == null) return null;
            m.SetFloat("_Thickness", thickness);
            m.SetFloat("_FogAmount", fog);
            foreach (var filter in model.GetComponentsInChildren<MeshFilter>())
            {
                if (filter.sharedMesh == null || filter.gameObject.layer == Layers.Effects) continue;
                var go = new GameObject("Outline") { layer = Layers.Effects };
                go.transform.SetParent(filter.transform, false);
                go.AddComponent<MeshFilter>().sharedMesh = filter.sharedMesh;
                var r = go.AddComponent<MeshRenderer>();
                r.sharedMaterial = m;
                r.shadowCastingMode = ShadowCastingMode.Off;
                r.receiveShadows = false;
                r.lightProbeUsage = LightProbeUsage.Off;
            }
            return m;
        }

        /// <summary>Set an effect's brightness (or, for HDRP's own glowing materials, their glow).</summary>
        public static void SetGlow(Renderer r, Color glow)
        {
            if (r != null) RuntimeMaterials.SetGlow(r.sharedMaterial, glow);
        }

        // A square, -0.5 to 0.5, uv 0 to 1 (the shaders turn it to face you).
        static Mesh Quad
        {
            get
            {
                if (quad != null) return quad;
                quad = new Mesh { name = "Effect quad" };
                quad.vertices = new[] { new Vector3(-0.5f, -0.5f, 0f), new Vector3(0.5f, -0.5f, 0f), new Vector3(-0.5f, 0.5f, 0f), new Vector3(0.5f, 0.5f, 0f) };
                quad.uv = new[] { new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 1f), new Vector2(1f, 1f) };
                quad.triangles = new[] { 0, 2, 1, 1, 2, 3 };
                // (Wide bounds: the shader moves the corners to face the camera.)
                quad.bounds = new Bounds(Vector3.zero, Vector3.one * 1.5f);
                return quad;
            }
        }

        // An open cone: its point at the origin, widening along +Z to radius 1 at z = 1 (uv.y: how far along).
        static Mesh Cone
        {
            get
            {
                if (cone != null) return cone;
                const int around = 24, along = 6;
                var verts = new Vector3[(around + 1) * (along + 1)];
                var normals = new Vector3[verts.Length];
                var uvs = new Vector2[verts.Length];
                for (int j = 0; j <= along; j++)
                {
                    float z = j / (float)along;
                    for (int i = 0; i <= around; i++)
                    {
                        float a = i / (float)around * Mathf.PI * 2f;
                        var dir = new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f);
                        int k = j * (around + 1) + i;
                        verts[k] = dir * z + Vector3.forward * z;
                        normals[k] = (dir - Vector3.forward).normalized; // (out from the cone's wall)
                        uvs[k] = new Vector2(i / (float)around, z);
                    }
                }
                var tris = new int[around * along * 6];
                int t = 0;
                for (int j = 0; j < along; j++)
                    for (int i = 0; i < around; i++)
                    {
                        int a = j * (around + 1) + i, b = a + 1, c = a + around + 1, d = c + 1;
                        tris[t++] = a; tris[t++] = c; tris[t++] = b;
                        tris[t++] = b; tris[t++] = c; tris[t++] = d;
                    }
                cone = new Mesh { name = "Effect cone", vertices = verts, normals = normals, uv = uvs, triangles = tris };
                cone.RecalculateBounds();
                return cone;
            }
        }
    }
}
