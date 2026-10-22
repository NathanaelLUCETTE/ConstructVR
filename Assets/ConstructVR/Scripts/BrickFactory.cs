using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace ConstructVR
{
    /// <summary>Maillages des briques (corps biseauté + tenons) et matériaux de couleur.</summary>
    public static class BrickFactory
    {
        const int StudSegments = 24;

        static LegoLibrary s_Library;
        static readonly Dictionary<int, Mesh> s_Meshes = new Dictionary<int, Mesh>();
        static readonly Dictionary<Color, Material> s_GhostMaterials = new Dictionary<Color, Material>();

        public static void Init(LegoLibrary library)
        {
            s_Library = library;
            s_GhostMaterials.Clear();
        }

        public static Mesh GetMesh(int shapeId)
        {
            if (s_Library != null && s_Library.BrickMeshes != null && shapeId < s_Library.BrickMeshes.Length && s_Library.BrickMeshes[shapeId] != null)
                return s_Library.BrickMeshes[shapeId];
            if (!s_Meshes.TryGetValue(shapeId, out var mesh))
            {
                mesh = BuildBrickMesh(LegoCatalog.Shapes[shapeId]);
                s_Meshes[shapeId] = mesh;
            }
            return mesh;
        }

        public static Material GetMaterial(int colorId) => s_Library.ColorMaterials[colorId];

        public static Material GetGhostMaterial(Color color)
        {
            if (!s_GhostMaterials.TryGetValue(color, out var mat) || mat == null)
            {
                mat = new Material(s_Library.GhostMaterial) { name = "Fantome" };
                mat.SetColor("_BaseColor", color);
                s_GhostMaterials[color] = mat;
            }
            return mat;
        }

        /// <summary>Crée un objet visuel (sans physique) pour une brique : aperçus, modèles, décor.</summary>
        public static GameObject CreateVisual(int shapeId, Material material, Transform parent, string name = null)
        {
            var go = new GameObject(name ?? LegoCatalog.Shapes[shapeId].Name);
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = GetMesh(shapeId);
            go.AddComponent<MeshRenderer>().sharedMaterial = material;
            return go;
        }

        /// <summary>Position locale (par rapport au centre de la plaque de base) d'une brique posée.</summary>
        public static Vector3 LocalPosition(BrickPlacement p)
        {
            var f = LegoCatalog.Shapes[p.Shape].Footprint(p.Rot);
            float half = LegoCatalog.BaseplateSize * LegoDims.Pitch * 0.5f;
            return new Vector3((p.X + f.x * 0.5f) * LegoDims.Pitch - half,
                               p.Level * LegoDims.PlateHeight,
                               (p.Z + f.y * 0.5f) * LegoDims.Pitch - half);
        }

        public static Quaternion LocalRotation(int rot) => Quaternion.Euler(0f, rot * 90f, 0f);

        // ---------------------------------------------------------------- maillages

        public static Mesh BuildBrickMesh(BrickShape s)
        {
            var mesh = BuildBlock(s.SizeX, s.SizeZ, s.Height, true, LegoDims.Bevel);
            mesh.name = s.Name;
            return mesh;
        }

        public static Mesh BuildBaseplateMesh()
        {
            var mesh = BuildBlock(LegoCatalog.BaseplateSize, LegoCatalog.BaseplateSize, LegoDims.PlateHeight, true, LegoDims.Bevel * 2f);
            mesh.name = "Plaque de base";
            return mesh;
        }

        /// <summary>Bloc centré en x/z, base à y = 0, arêtes biseautées, avec ou sans tenons.</summary>
        public static Mesh BuildBlock(int sizeX, int sizeZ, float height, bool studs, float bevel)
        {
            var b = new MeshBuilder();
            float p = LegoDims.Pitch, g = LegoDims.Gap;
            var min = new Vector3(-sizeX * p * 0.5f + g, 0f, -sizeZ * p * 0.5f + g);
            var max = new Vector3(sizeX * p * 0.5f - g, height, sizeZ * p * 0.5f - g);
            b.AddBeveledBox(min, max, bevel);
            if (studs)
            {
                for (int x = 0; x < sizeX; x++)
                for (int z = 0; z < sizeZ; z++)
                {
                    var c = new Vector3((x + 0.5f) * p - sizeX * p * 0.5f, height, (z + 0.5f) * p - sizeZ * p * 0.5f);
                    b.AddStud(c, LegoDims.StudRadius, LegoDims.StudHeight, StudSegments, LegoDims.Bevel * 0.8f);
                }
            }
            return b.ToMesh();
        }

        public class MeshBuilder
        {
            readonly List<Vector3> m_V = new List<Vector3>();
            readonly List<Vector3> m_N = new List<Vector3>();
            readonly List<Vector2> m_UV = new List<Vector2>();
            readonly List<int> m_T = new List<int>();

            int Add(Vector3 v, Vector3 n, Vector2 uv)
            {
                m_V.Add(v); m_N.Add(n); m_UV.Add(uv);
                return m_V.Count - 1;
            }

            /// <summary>Triangle plat, orienté automatiquement vers la normale donnée.</summary>
            public void Tri(Vector3 a, Vector3 b, Vector3 c, Vector3 n)
            {
                if (Vector3.Dot(Vector3.Cross(b - a, c - a), n) < 0f) { var t = b; b = c; c = t; }
                int i = Add(a, n, Vector2.zero);
                Add(b, n, Vector2.right);
                Add(c, n, Vector2.up);
                m_T.Add(i); m_T.Add(i + 1); m_T.Add(i + 2);
            }

            /// <summary>Quadrilatère plat (sommets dans l'ordre du contour).</summary>
            public void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 n)
            {
                if (Vector3.Dot(Vector3.Cross(b - a, c - a), n) < 0f) { var t = b; b = d; d = t; }
                int i = Add(a, n, new Vector2(0, 0));
                Add(b, n, new Vector2(0, 1));
                Add(c, n, new Vector2(1, 1));
                Add(d, n, new Vector2(1, 0));
                m_T.Add(i); m_T.Add(i + 1); m_T.Add(i + 2);
                m_T.Add(i); m_T.Add(i + 2); m_T.Add(i + 3);
            }

            public void AddBeveledBox(Vector3 lo, Vector3 hi, float bevel)
            {
                var size = hi - lo;
                bevel = Mathf.Min(bevel, Mathf.Min(size.x, Mathf.Min(size.y, size.z)) * 0.3f);
                Vector3 ext(int ax, int s, Vector3 v) { v[ax] = s > 0 ? hi[ax] : lo[ax]; return v; }
                Vector3 ins(int ax, int s, Vector3 v) { v[ax] = s > 0 ? hi[ax] - bevel : lo[ax] + bevel; return v; }
                Vector3 axis(int ax, int s) { var v = Vector3.zero; v[ax] = s; return v; }

                // 6 faces
                for (int a = 0; a < 3; a++)
                for (int s = -1; s <= 1; s += 2)
                {
                    int u = (a + 1) % 3, w = (a + 2) % 3;
                    var p00 = ext(a, s, ins(w, -1, ins(u, -1, Vector3.zero)));
                    var p10 = ext(a, s, ins(w, -1, ins(u, 1, Vector3.zero)));
                    var p11 = ext(a, s, ins(w, 1, ins(u, 1, Vector3.zero)));
                    var p01 = ext(a, s, ins(w, 1, ins(u, -1, Vector3.zero)));
                    Quad(p00, p10, p11, p01, axis(a, s));
                }

                // 12 arêtes biseautées
                for (int a1 = 0; a1 < 3; a1++)
                for (int a2 = a1 + 1; a2 < 3; a2++)
                {
                    int a3 = 3 - a1 - a2;
                    for (int s1 = -1; s1 <= 1; s1 += 2)
                    for (int s2 = -1; s2 <= 1; s2 += 2)
                    {
                        var n = (axis(a1, s1) + axis(a2, s2)).normalized;
                        var q0 = ins(a3, -1, ext(a1, s1, ins(a2, s2, Vector3.zero)));
                        var q1 = ins(a3, 1, ext(a1, s1, ins(a2, s2, Vector3.zero)));
                        var q2 = ins(a3, 1, ext(a2, s2, ins(a1, s1, Vector3.zero)));
                        var q3 = ins(a3, -1, ext(a2, s2, ins(a1, s1, Vector3.zero)));
                        Quad(q0, q1, q2, q3, n);
                    }
                }

                // 8 coins
                for (int sx = -1; sx <= 1; sx += 2)
                for (int sy = -1; sy <= 1; sy += 2)
                for (int sz = -1; sz <= 1; sz += 2)
                {
                    var n = new Vector3(sx, sy, sz).normalized;
                    var cx = ext(0, sx, ins(1, sy, ins(2, sz, Vector3.zero)));
                    var cy = ext(1, sy, ins(0, sx, ins(2, sz, Vector3.zero)));
                    var cz = ext(2, sz, ins(0, sx, ins(1, sy, Vector3.zero)));
                    Tri(cx, cy, cz, n);
                }
            }

            public void AddStud(Vector3 baseCenter, float r, float h, int seg, float bevel)
            {
                // côté (normales lisses), puis petit biseau, puis dessus
                int ring0 = m_V.Count;
                for (int i = 0; i <= seg; i++)
                {
                    float a = i * Mathf.PI * 2f / seg;
                    var d = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                    float u = (float)i / seg;
                    Add(baseCenter + d * r, d, new Vector2(u, 0));
                    Add(baseCenter + d * r + Vector3.up * (h - bevel), d, new Vector2(u, 0.8f));
                    var nb = (d + Vector3.up).normalized;
                    Add(baseCenter + d * r + Vector3.up * (h - bevel), nb, new Vector2(u, 0.8f));
                    Add(baseCenter + d * (r - bevel) + Vector3.up * h, nb, new Vector2(u, 1f));
                }
                for (int i = 0; i < seg; i++)
                {
                    int a = ring0 + i * 4, b = a + 4;
                    // côté
                    m_T.Add(a); m_T.Add(a + 1); m_T.Add(b + 1);
                    m_T.Add(a); m_T.Add(b + 1); m_T.Add(b);
                    // biseau
                    m_T.Add(a + 2); m_T.Add(a + 3); m_T.Add(b + 3);
                    m_T.Add(a + 2); m_T.Add(b + 3); m_T.Add(b + 2);
                }
                int c = Add(baseCenter + Vector3.up * h, Vector3.up, new Vector2(0.5f, 0.5f));
                int top0 = m_V.Count;
                for (int i = 0; i <= seg; i++)
                {
                    float a = i * Mathf.PI * 2f / seg;
                    Add(baseCenter + new Vector3(Mathf.Cos(a) * (r - bevel), h, Mathf.Sin(a) * (r - bevel)), Vector3.up,
                        new Vector2(0.5f + Mathf.Cos(a) * 0.5f, 0.5f + Mathf.Sin(a) * 0.5f));
                }
                for (int i = 0; i < seg; i++)
                {
                    m_T.Add(c); m_T.Add(top0 + i + 1); m_T.Add(top0 + i);
                }
            }

            public Mesh ToMesh()
            {
                var mesh = new Mesh();
                if (m_V.Count > 65000) mesh.indexFormat = IndexFormat.UInt32;
                mesh.SetVertices(m_V);
                mesh.SetNormals(m_N);
                mesh.SetUVs(0, m_UV);
                mesh.SetTriangles(m_T, 0);
                mesh.RecalculateBounds();
                mesh.RecalculateTangents();
                return mesh;
            }
        }
    }
}
