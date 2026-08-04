// Copyright (c) 2026 Brendo Otavio Carvalho de Matos. All rights reserved.

using System.Collections.Generic;
using UnityEngine;

namespace GrassSystem
{
    public enum GrassProceduralType
    {
        Blade = 0,
        Tapered = 1,
        Soft = 5,
        Tuft = 4,
        Clump = 6
    }

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
                case GrassProceduralType.Soft: return GenerateSoftBlade();
                case GrassProceduralType.Tuft: return GenerateTuftBlade();
                case GrassProceduralType.Clump: return GenerateClumpBlade();
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

        private static Mesh GenerateSoftBlade()
        {
            Vector3[] vertices =
            {
                new Vector3(-0.5f, 0f, 0f),
                new Vector3(0.5f, 0f, 0f),
                new Vector3(-0.34f, 0.5f, 0f),
                new Vector3(0.34f, 0.5f, 0f),
                new Vector3(-0.15f, 0.85f, 0f),
                new Vector3(0.15f, 0.85f, 0f),
                new Vector3(0f, 1f, 0f)
            };

            Vector2[] uvs =
            {
                new Vector2(0f, 0f),
                new Vector2(1f, 0f),
                new Vector2(0.16f, 0.5f),
                new Vector2(0.84f, 0.5f),
                new Vector2(0.35f, 0.85f),
                new Vector2(0.65f, 0.85f),
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
                Vector3.back
            };

            int[] triangles = { 0, 2, 1, 1, 2, 3, 2, 4, 3, 3, 4, 5, 4, 6, 5 };

            return BuildMesh("SoftGrassBlade", vertices, uvs, normals, triangles);
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

        private static readonly (float cx, float cz, float halfWidth, float height, float yawDeg)[] ClumpBladeLayout =
        {
            ( 0.00f,  0.00f, 0.20f, 1.00f,   0f),
            (-0.38f,  0.10f, 0.17f, 0.82f,  35f),
            ( 0.36f, -0.08f, 0.18f, 0.88f, -30f),
            (-0.12f, -0.34f, 0.16f, 0.74f,  70f),
            ( 0.16f,  0.34f, 0.15f, 0.70f, -65f)
        };

        private static Mesh GenerateClumpBlade()
        {
            int bladeCount = ClumpBladeLayout.Length;
            Vector3[] vertices = new Vector3[bladeCount * 3];
            Vector2[] uvs = new Vector2[bladeCount * 3];
            Vector3[] normals = new Vector3[bladeCount * 3];
            int[] triangles = new int[bladeCount * 3];

            for (int i = 0; i < bladeCount; i++)
            {
                var blade = ClumpBladeLayout[i];
                float yaw = blade.yawDeg * Mathf.Deg2Rad;
                float dx = Mathf.Cos(yaw) * blade.halfWidth;
                float dz = -Mathf.Sin(yaw) * blade.halfWidth;

                int baseIndex = i * 3;

                vertices[baseIndex] = new Vector3(blade.cx - dx, 0f, blade.cz - dz);
                vertices[baseIndex + 1] = new Vector3(blade.cx + dx, 0f, blade.cz + dz);
                vertices[baseIndex + 2] = new Vector3(blade.cx, blade.height, blade.cz);

                uvs[baseIndex] = new Vector2(0f, 0f);
                uvs[baseIndex + 1] = new Vector2(1f, 0f);
                uvs[baseIndex + 2] = new Vector2(0.5f, 1f);

                normals[baseIndex] = Vector3.back;
                normals[baseIndex + 1] = Vector3.back;
                normals[baseIndex + 2] = Vector3.back;

                triangles[baseIndex] = baseIndex;
                triangles[baseIndex + 1] = baseIndex + 2;
                triangles[baseIndex + 2] = baseIndex + 1;
            }

            return BuildMesh("ClumpGrassBlade", vertices, uvs, normals, triangles);
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
