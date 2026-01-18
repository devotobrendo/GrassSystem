// GrassData.cs - GPU-compatible grass instance data structure
// Matches the compute buffer layout for efficient GPU instancing

using UnityEngine;

namespace GrassSystem
{
    /// <summary>
    /// Per-instance grass data. Must match compute shader struct layout exactly.
    /// Total size: 48 bytes (aligned for GPU buffers)
    /// </summary>
    [System.Serializable]
    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
    public struct GrassData
    {
        public Vector3 position;    // 12 bytes - World position
        public Vector3 normal;      // 12 bytes - Surface normal (for lighting)
        public Vector2 widthHeight; // 8 bytes  - x: width scale, y: height scale
        public Vector3 color;       // 12 bytes - RGB color
        public float patternMask;   // 4 bytes  - 0 or 1 for checkered pattern
        
        public static int Stride => sizeof(float) * 12; // 48 bytes
        
        public GrassData(Vector3 pos, Vector3 norm, float width, float height, Color col, float pattern = 0f)
        {
            position = pos;
            normal = norm;
            widthHeight = new Vector2(width, height);
            color = new Vector3(col.r, col.g, col.b);
            patternMask = pattern;
        }
    }
    
    /// <summary>
    /// Output struct for visible instances after culling.
    /// Contains index into source buffer + computed data.
    /// </summary>
    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
    public struct GrassDrawData
    {
        public Vector3 position;
        public Vector3 normal;
        public Vector2 widthHeight;
        public Vector3 color;
        public float patternMask;
        public float distanceScale; // LOD scale based on camera distance
        
        public static int Stride => sizeof(float) * 13; // 52 bytes
    }
}
