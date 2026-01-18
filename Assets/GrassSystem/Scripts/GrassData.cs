// Copyright (c) 2026 Brendo Otavio Carvalho de Matos. All rights reserved.

using UnityEngine;

namespace GrassSystem
{
    [System.Serializable]
    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
    public struct GrassData
    {
        public Vector3 position;
        public Vector3 normal;
        public Vector2 widthHeight;
        public Vector3 color;
        public float patternMask;
        
        public static int Stride => sizeof(float) * 12;
        
        public GrassData(Vector3 pos, Vector3 norm, float width, float height, Color col, float pattern = 0f)
        {
            position = pos;
            normal = norm;
            widthHeight = new Vector2(width, height);
            color = new Vector3(col.r, col.g, col.b);
            patternMask = pattern;
        }
    }
    
    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
    public struct GrassDrawData
    {
        public Vector3 position;
        public Vector3 normal;
        public Vector2 widthHeight;
        public Vector3 color;
        public float patternMask;
        public float distanceScale;
        
        public static int Stride => sizeof(float) * 13;
    }
}
