// Copyright (c) 2026 Brendo Otavio Carvalho de Matos. All rights reserved.

using UnityEngine;

namespace GrassSystem
{
    /// <summary>
    /// Utility class to generate procedural grass blade meshes at runtime.
    /// </summary>
    public static class GrassMeshUtility
    {
        private static Mesh cachedZeldaBlade;
        
        /// <summary>
        /// Gets or creates a Zelda-style triangular grass blade mesh.
        /// This mesh is cached for performance.
        /// </summary>
        public static Mesh GetZeldaStyleBlade()
        {
            if (cachedZeldaBlade == null)
            {
                cachedZeldaBlade = GenerateZeldaStyleBlade();
            }
            return cachedZeldaBlade;
        }
        
        /// <summary>
        /// Generates a simple triangular grass blade mesh similar to Zelda: Breath of the Wild.
        /// This is a single triangle blade that's very efficient for GPU instancing.
        /// </summary>
        public static Mesh GenerateZeldaStyleBlade()
        {
            Mesh mesh = new Mesh();
            mesh.name = "ZeldaStyleGrassBlade";
            
            // Simple triangular blade - 3 vertices forming a pointed grass blade
            // Base is at bottom, point at top - classic Zelda BOTW style
            Vector3[] vertices = new Vector3[]
            {
                new Vector3(-0.5f, 0f, 0f),   // Bottom left
                new Vector3(0.5f, 0f, 0f),    // Bottom right
                new Vector3(0f, 1f, 0f)       // Top center (pointed tip)
            };
            
            Vector2[] uvs = new Vector2[]
            {
                new Vector2(0f, 0f),
                new Vector2(1f, 0f),
                new Vector2(0.5f, 1f)
            };
            
            Vector3[] normals = new Vector3[]
            {
                Vector3.back,
                Vector3.back,
                Vector3.back
            };
            
            Vector4[] tangents = new Vector4[]
            {
                new Vector4(1f, 0f, 0f, 1f),
                new Vector4(1f, 0f, 0f, 1f),
                new Vector4(1f, 0f, 0f, 1f)
            };
            
            // Single triangle
            int[] triangles = new int[] { 0, 2, 1 };
            
            mesh.vertices = vertices;
            mesh.uv = uvs;
            mesh.normals = normals;
            mesh.tangents = tangents;
            mesh.triangles = triangles;
            mesh.RecalculateBounds();
            
            // Mark as non-readable to save memory after upload to GPU
            mesh.UploadMeshData(true);
            
            return mesh;
        }
        
        /// <summary>
        /// Clears the cached mesh. Call this if you need to regenerate.
        /// </summary>
        public static void ClearCache()
        {
            if (cachedZeldaBlade != null)
            {
                Object.DestroyImmediate(cachedZeldaBlade);
                cachedZeldaBlade = null;
            }
        }
    }
}
