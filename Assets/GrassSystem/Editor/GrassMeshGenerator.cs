// Copyright (c) 2026 Brendo Otavio Carvalho de Matos. All rights reserved.

using UnityEditor;
using UnityEngine;

namespace GrassSystem
{
    public static class GrassMeshGenerator
    {
        [MenuItem("Assets/Create/Grass System/Generate Default Grass Mesh")]
        public static void CreateGrassMesh()
        {
            Mesh mesh = GenerateGrassBladeMesh();
            
            string path = EditorUtility.SaveFilePanelInProject(
                "Save Grass Mesh",
                "GrassBlade",
                "asset",
                "Choose location for grass blade mesh"
            );
            
            if (!string.IsNullOrEmpty(path))
            {
                AssetDatabase.CreateAsset(mesh, path);
                AssetDatabase.SaveAssets();
                Selection.activeObject = mesh;
                Debug.Log($"Created grass mesh at: {path}");
            }
        }
        
        public static Mesh GenerateGrassBladeMesh()
        {
            Mesh mesh = new Mesh();
            mesh.name = "GrassBlade";
            
            Vector3[] vertices = new Vector3[]
            {
                new Vector3(-0.5f, 0f, 0f),
                new Vector3(0.5f, 0f, 0f),
                new Vector3(-0.3f, 1f, 0f),
                new Vector3(0.3f, 1f, 0f)
            };
            
            Vector2[] uvs = new Vector2[]
            {
                new Vector2(0f, 0f),
                new Vector2(1f, 0f),
                new Vector2(0f, 1f),
                new Vector2(1f, 1f)
            };
            
            Vector3[] normals = new Vector3[]
            {
                Vector3.forward,
                Vector3.forward,
                Vector3.forward,
                Vector3.forward
            };
            
            Vector4[] tangents = new Vector4[]
            {
                new Vector4(1f, 0f, 0f, 1f),
                new Vector4(1f, 0f, 0f, 1f),
                new Vector4(1f, 0f, 0f, 1f),
                new Vector4(1f, 0f, 0f, 1f)
            };
            
            int[] triangles = new int[] { 0, 2, 1, 1, 2, 3 };
            
            mesh.vertices = vertices;
            mesh.uv = uvs;
            mesh.normals = normals;
            mesh.tangents = tangents;
            mesh.triangles = triangles;
            mesh.RecalculateBounds();
            
            return mesh;
        }
        
        public static Mesh GenerateDetailedGrassBladeMesh(int segments = 3)
        {
            Mesh mesh = new Mesh();
            mesh.name = "GrassBladeDetailed";
            
            int vertCount = (segments + 1) * 2;
            Vector3[] vertices = new Vector3[vertCount];
            Vector2[] uvs = new Vector2[vertCount];
            Vector3[] normals = new Vector3[vertCount];
            Vector4[] tangents = new Vector4[vertCount];
            
            for (int i = 0; i <= segments; i++)
            {
                float t = (float)i / segments;
                float width = Mathf.Lerp(0.5f, 0.1f, t);
                
                int idx = i * 2;
                vertices[idx] = new Vector3(-width, t, 0f);
                vertices[idx + 1] = new Vector3(width, t, 0f);
                
                uvs[idx] = new Vector2(0f, t);
                uvs[idx + 1] = new Vector2(1f, t);
                
                normals[idx] = Vector3.forward;
                normals[idx + 1] = Vector3.forward;
                
                tangents[idx] = new Vector4(1f, 0f, 0f, 1f);
                tangents[idx + 1] = new Vector4(1f, 0f, 0f, 1f);
            }
            
            int[] triangles = new int[segments * 6];
            for (int i = 0; i < segments; i++)
            {
                int idx = i * 6;
                int vert = i * 2;
                
                triangles[idx] = vert;
                triangles[idx + 1] = vert + 2;
                triangles[idx + 2] = vert + 1;
                
                triangles[idx + 3] = vert + 1;
                triangles[idx + 4] = vert + 2;
                triangles[idx + 5] = vert + 3;
            }
            
            mesh.vertices = vertices;
            mesh.uv = uvs;
            mesh.normals = normals;
            mesh.tangents = tangents;
            mesh.triangles = triangles;
            mesh.RecalculateBounds();
            
            return mesh;
        }
    }
}
