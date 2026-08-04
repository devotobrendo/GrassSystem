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
        SoftMid = 7,
        SoftRound = 8,
        SoftDome = 9,
        Tuft = 4
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
                case GrassProceduralType.Soft: return GenerateProfileBlade("SoftGrassBlade", SoftProfile);
                case GrassProceduralType.SoftMid: return GenerateProfileBlade("SoftMidGrassBlade", SoftMidProfile);
                case GrassProceduralType.SoftRound: return GenerateProfileBlade("SoftRoundGrassBlade", SoftRoundProfile);
                case GrassProceduralType.SoftDome: return GenerateProfileBlade("SoftDomeGrassBlade", SoftDomeProfile);
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

        private static readonly (float height, float halfWidth)[] SoftProfile =
        {
            (0.00f, 0.500f), (0.45f, 0.330f), (0.78f, 0.190f), (0.94f, 0.105f)
        };

        private static readonly (float height, float halfWidth)[] SoftMidProfile =
        {
            (0.00f, 0.500f), (0.42f, 0.360f), (0.72f, 0.245f), (0.89f, 0.155f), (0.97f, 0.080f)
        };

        private static readonly (float height, float halfWidth)[] SoftRoundProfile =
        {
            (0.00f, 0.500f), (0.38f, 0.430f), (0.70f, 0.330f), (0.88f, 0.225f), (0.965f, 0.120f)
        };

        private static readonly (float height, float halfWidth)[] SoftDomeProfile =
        {
            (0.00f, 0.500f), (0.70f, 0.380f), (0.88f, 0.304f), (0.97f, 0.166f)
        };

        private static Mesh GenerateProfileBlade(string name, (float height, float halfWidth)[] profile)
        {
            int levels = profile.Length;
            int vertexCount = levels * 2 + 1;
            int apex = vertexCount - 1;

            Vector3[] vertices = new Vector3[vertexCount];
            Vector2[] uvs = new Vector2[vertexCount];
            Vector3[] normals = new Vector3[vertexCount];

            for (int i = 0; i < levels; i++)
            {
                float h = profile[i].height;
                float hw = profile[i].halfWidth;

                vertices[i * 2] = new Vector3(-hw, h, 0f);
                vertices[i * 2 + 1] = new Vector3(hw, h, 0f);
                uvs[i * 2] = new Vector2(0.5f - hw, h);
                uvs[i * 2 + 1] = new Vector2(0.5f + hw, h);
            }

            vertices[apex] = new Vector3(0f, 1f, 0f);
            uvs[apex] = new Vector2(0.5f, 1f);

            for (int i = 0; i < vertexCount; i++)
                normals[i] = Vector3.back;

            int[] triangles = new int[((levels - 1) * 2 + 1) * 3];
            int t = 0;
            for (int i = 0; i < levels - 1; i++)
            {
                int l0 = i * 2;
                int r0 = l0 + 1;
                int l1 = l0 + 2;
                int r1 = l0 + 3;

                triangles[t++] = l0; triangles[t++] = l1; triangles[t++] = r0;
                triangles[t++] = r0; triangles[t++] = l1; triangles[t++] = r1;
            }

            int lastL = (levels - 1) * 2;
            triangles[t++] = lastL; triangles[t++] = apex; triangles[t] = lastL + 1;

            return BuildMesh(name, vertices, uvs, normals, triangles);
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
