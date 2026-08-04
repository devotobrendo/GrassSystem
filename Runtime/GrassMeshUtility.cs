// Copyright (c) 2026 Brendo Otavio Carvalho de Matos. All rights reserved.

using System.Collections.Generic;
using UnityEngine;

namespace GrassSystem
{
    public enum GrassProceduralType { Blade, Tapered, Quad, Cross, Tuft }

    public static class GrassMeshUtility
    {
        private static readonly Dictionary<GrassProceduralType, Mesh> cachedMeshes = new Dictionary<GrassProceduralType, Mesh>();

        public static Mesh GetZeldaStyleBlade()
        {
            return GetProceduralMesh(GrassProceduralType.Blade);
        }

        public static Mesh GetProceduralMesh(GrassProceduralType type)
        {
            cachedMeshes.TryGetValue(type, out Mesh cached);
            if (cached == null || !cached)
            {
                cached = GenerateProceduralMesh(type);
                cachedMeshes[type] = cached;
            }
            return cached;
        }

        private static Mesh GenerateProceduralMesh(GrassProceduralType type)
        {
            switch (type)
            {
                case GrassProceduralType.Blade: return GenerateZeldaStyleBlade();
                case GrassProceduralType.Tapered: return GenerateTaperedBlade();
                case GrassProceduralType.Quad: return GenerateQuadBlade();
                case GrassProceduralType.Cross: return GenerateCrossBlade();
                case GrassProceduralType.Tuft: return GenerateTuftBlade();
                default: return GenerateZeldaStyleBlade();
            }
        }

        public static Mesh GenerateZeldaStyleBlade()
        {
            Vector3[] vertices =
            {
                new Vector3(-0.5f, 0f, 0f),
                new Vector3(0.5f, 0f, 0f),
                new Vector3(0f, 1f, 0f)
            };

            Vector2[] uvs =
            {
                new Vector2(0f, 0f),
                new Vector2(1f, 0f),
                new Vector2(0.5f, 1f)
            };

            Vector3[] normals =
            {
                Vector3.back,
                Vector3.back,
                Vector3.back
            };

            int[] triangles = { 0, 2, 1 };

            return BuildMesh("ZeldaStyleGrassBlade", vertices, uvs, normals, triangles);
        }

        private static Mesh GenerateTaperedBlade()
        {
            Vector3[] vertices =
            {
                new Vector3(-0.5f, 0f, 0f),
                new Vector3(0.5f, 0f, 0f),
                new Vector3(-0.28f, 0.55f, 0f),
                new Vector3(0.28f, 0.55f, 0f),
                new Vector3(0f, 1f, 0f)
            };

            Vector2[] uvs =
            {
                new Vector2(0f, 0f),
                new Vector2(1f, 0f),
                new Vector2(0.22f, 0.55f),
                new Vector2(0.78f, 0.55f),
                new Vector2(0.5f, 1f)
            };

            Vector3[] normals =
            {
                Vector3.back,
                Vector3.back,
                Vector3.back,
                Vector3.back,
                Vector3.back
            };

            int[] triangles = { 0, 2, 1, 1, 2, 3, 2, 4, 3 };

            return BuildMesh("TaperedGrassBlade", vertices, uvs, normals, triangles);
        }

        private static Mesh GenerateQuadBlade()
        {
            Vector3[] vertices =
            {
                new Vector3(-0.5f, 0f, 0f),
                new Vector3(0.5f, 0f, 0f),
                new Vector3(-0.5f, 1f, 0f),
                new Vector3(0.5f, 1f, 0f)
            };

            Vector2[] uvs =
            {
                new Vector2(0f, 0f),
                new Vector2(1f, 0f),
                new Vector2(0f, 1f),
                new Vector2(1f, 1f)
            };

            Vector3[] normals =
            {
                Vector3.back,
                Vector3.back,
                Vector3.back,
                Vector3.back
            };

            int[] triangles = { 0, 2, 1, 1, 2, 3 };

            return BuildMesh("QuadGrassBlade", vertices, uvs, normals, triangles);
        }

        private static Mesh GenerateCrossBlade()
        {
            Vector3[] vertices =
            {
                new Vector3(-0.5f, 0f, 0f),
                new Vector3(0.5f, 0f, 0f),
                new Vector3(-0.5f, 1f, 0f),
                new Vector3(0.5f, 1f, 0f),
                new Vector3(0f, 0f, -0.5f),
                new Vector3(0f, 0f, 0.5f),
                new Vector3(0f, 1f, -0.5f),
                new Vector3(0f, 1f, 0.5f)
            };

            Vector2[] uvs =
            {
                new Vector2(0f, 0f),
                new Vector2(1f, 0f),
                new Vector2(0f, 1f),
                new Vector2(1f, 1f),
                new Vector2(0f, 0f),
                new Vector2(1f, 0f),
                new Vector2(0f, 1f),
                new Vector2(1f, 1f)
            };

            Vector3[] normals =
            {
                Vector3.back,
                Vector3.back,
                Vector3.back,
                Vector3.back,
                Vector3.left,
                Vector3.left,
                Vector3.left,
                Vector3.left
            };

            int[] triangles = { 0, 2, 1, 1, 2, 3, 4, 6, 5, 5, 6, 7 };

            return BuildMesh("CrossGrassBlade", vertices, uvs, normals, triangles);
        }

        private static Mesh GenerateTuftBlade()
        {
            Vector3[] vertices =
            {
                new Vector3(-0.22f, 0f, 0f),
                new Vector3(0.22f, 0f, 0f),
                new Vector3(0f, 1f, 0f),
                new Vector3(-0.50f, 0f, 0.06f),
                new Vector3(-0.12f, 0f, 0.06f),
                new Vector3(-0.30f, 0.80f, 0.06f),
                new Vector3(0.14f, 0f, -0.05f),
                new Vector3(0.50f, 0f, -0.05f),
                new Vector3(0.32f, 0.72f, -0.05f)
            };

            Vector2[] uvs =
            {
                new Vector2(0f, 0f),
                new Vector2(1f, 0f),
                new Vector2(0.5f, 1f),
                new Vector2(0f, 0f),
                new Vector2(1f, 0f),
                new Vector2(0.5f, 1f),
                new Vector2(0f, 0f),
                new Vector2(1f, 0f),
                new Vector2(0.5f, 1f)
            };

            Vector3[] normals =
            {
                Vector3.back,
                Vector3.back,
                Vector3.back,
                Vector3.back,
                Vector3.back,
                Vector3.back,
                Vector3.back,
                Vector3.back,
                Vector3.back
            };

            int[] triangles = { 0, 2, 1, 3, 5, 4, 6, 8, 7 };

            return BuildMesh("TuftGrassBlade", vertices, uvs, normals, triangles);
        }

        private static Mesh BuildMesh(string name, Vector3[] vertices, Vector2[] uvs, Vector3[] normals, int[] triangles)
        {
            Mesh mesh = new Mesh();
            mesh.name = name;

            Vector4[] tangents = new Vector4[vertices.Length];
            for (int i = 0; i < tangents.Length; i++)
                tangents[i] = new Vector4(1f, 0f, 0f, 1f);

            mesh.vertices = vertices;
            mesh.uv = uvs;
            mesh.normals = normals;
            mesh.tangents = tangents;
            mesh.triangles = triangles;
            mesh.RecalculateBounds();
            mesh.hideFlags = HideFlags.HideAndDontSave;

            return mesh;
        }

        public static void ClearCache()
        {
            foreach (KeyValuePair<GrassProceduralType, Mesh> kvp in cachedMeshes)
            {
                if (kvp.Value != null)
                    Object.DestroyImmediate(kvp.Value);
            }
            cachedMeshes.Clear();
        }
    }
}
