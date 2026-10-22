using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace CuandoNadieMira.Menu.EditorTools
{
    /// <summary>Mallas simples generadas por código: arcos y la figura votiva (tunjo) de la vitrina.</summary>
    public static class ProceduralMeshes
    {
        public const string Folder = "Assets/CuandoNadieMira/Menu/Models/Generated";

        /// <summary>Medio anillo extruido (arco de medio punto), con la base en y = 0.</summary>
        public static Mesh Arch(float innerRadius, float outerRadius, float depth, int segments = 24)
        {
            var verts = new List<Vector3>();
            var tris = new List<int>();
            float hd = depth * 0.5f;

            // Doble cara: cada cara lleva sus propios vértices por lado para que las normales no se anulen
            void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d)
            {
                int i = verts.Count;
                verts.Add(a); verts.Add(b); verts.Add(c); verts.Add(d);
                tris.AddRange(new[] { i, i + 1, i + 2, i, i + 2, i + 3 });
                int j = verts.Count;
                verts.Add(a); verts.Add(b); verts.Add(c); verts.Add(d);
                tris.AddRange(new[] { j, j + 2, j + 1, j, j + 3, j + 2 });
            }

            for (int s = 0; s < segments; s++)
            {
                float a0 = Mathf.PI * s / segments, a1 = Mathf.PI * (s + 1) / segments;
                Vector3 i0 = new(Mathf.Cos(a0) * innerRadius, Mathf.Sin(a0) * innerRadius, 0);
                Vector3 i1 = new(Mathf.Cos(a1) * innerRadius, Mathf.Sin(a1) * innerRadius, 0);
                Vector3 o0 = new(Mathf.Cos(a0) * outerRadius, Mathf.Sin(a0) * outerRadius, 0);
                Vector3 o1 = new(Mathf.Cos(a1) * outerRadius, Mathf.Sin(a1) * outerRadius, 0);
                Vector3 f = new(0, 0, -hd), b = new(0, 0, hd);
                Quad(i0 + f, o0 + f, o1 + f, i1 + f);     // cara frontal
                Quad(i1 + b, o1 + b, o0 + b, i0 + b);     // cara trasera
                Quad(i0 + b, i0 + f, i1 + f, i1 + b);     // intradós
                Quad(o1 + b, o1 + f, o0 + f, o0 + b);     // trasdós
            }
            return SaveMesh("Arch_" + innerRadius.ToString("0.0") + "_" + outerRadius.ToString("0.0"), verts, tris);
        }

        /// <summary>Figura votiva estilizada de baja poligonización: cuerpo plano, cabeza y tocado alto.</summary>
        public static Mesh Tunjo()
        {
            var parts = new List<CombineInstance>();
            Mesh cube = Resources.GetBuiltinResource<Mesh>("Cube.fbx");

            void Box(Vector3 pos, Vector3 size, float zRot = 0f)
            {
                parts.Add(new CombineInstance
                {
                    mesh = cube,
                    transform = Matrix4x4.TRS(pos, Quaternion.Euler(0, 0, zRot), size)
                });
            }

            // Cuerpo trapezoidal (dos placas) y faldón
            Box(new Vector3(0, 0.12f, 0), new Vector3(0.11f, 0.17f, 0.018f));
            Box(new Vector3(0, 0.03f, 0), new Vector3(0.14f, 0.05f, 0.018f));
            // Cabeza, ojos y nariz
            Box(new Vector3(0, 0.235f, 0), new Vector3(0.075f, 0.065f, 0.022f));
            Box(new Vector3(-0.017f, 0.243f, -0.012f), new Vector3(0.018f, 0.006f, 0.004f));
            Box(new Vector3(0.017f, 0.243f, -0.012f), new Vector3(0.018f, 0.006f, 0.004f));
            Box(new Vector3(0, 0.228f, -0.013f), new Vector3(0.006f, 0.02f, 0.006f));
            // Tocado alto en abanico
            for (int i = -3; i <= 3; i++)
                Box(new Vector3(i * 0.016f, 0.31f, 0), new Vector3(0.008f, 0.1f + (3 - Mathf.Abs(i)) * 0.012f, 0.008f), -i * 9f);
            Box(new Vector3(0, 0.272f, 0), new Vector3(0.12f, 0.012f, 0.014f));
            // Brazos en relieve y bastón
            Box(new Vector3(-0.045f, 0.13f, -0.012f), new Vector3(0.01f, 0.11f, 0.008f), 12f);
            Box(new Vector3(0.045f, 0.13f, -0.012f), new Vector3(0.01f, 0.11f, 0.008f), -12f);
            Box(new Vector3(0.062f, 0.12f, -0.014f), new Vector3(0.006f, 0.2f, 0.006f));
            // Orejeras
            Box(new Vector3(-0.045f, 0.225f, 0), new Vector3(0.016f, 0.03f, 0.01f));
            Box(new Vector3(0.045f, 0.225f, 0), new Vector3(0.016f, 0.03f, 0.01f));

            var mesh = new Mesh { name = "Tunjo" };
            mesh.CombineMeshes(parts.ToArray(), true, true);
            mesh.RecalculateBounds();
            return SaveMesh(mesh);
        }

        static Mesh SaveMesh(string name, List<Vector3> verts, List<int> tris)
        {
            var m = new Mesh { name = name };
            m.SetVertices(verts);
            m.SetTriangles(tris, 0);
            var uv = new Vector2[verts.Count];
            for (int i = 0; i < uv.Length; i++) uv[i] = new Vector2(verts[i].x, verts[i].y + verts[i].z);
            m.uv = uv;
            m.RecalculateNormals();
            m.RecalculateTangents();
            m.RecalculateBounds();
            return SaveMesh(m);
        }

        static Mesh SaveMesh(Mesh m)
        {
            MenuAssetFactory.EnsureFolder(Folder);
            if (m.tangents == null || m.tangents.Length == 0) m.RecalculateTangents();
            string path = $"{Folder}/{m.name}.asset";
            AssetDatabase.DeleteAsset(path);
            AssetDatabase.CreateAsset(m, path);
            return AssetDatabase.LoadAssetAtPath<Mesh>(path);
        }
    }
}
