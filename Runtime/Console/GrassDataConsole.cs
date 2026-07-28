// Copyright (c) 2026 Brendo Otavio Carvalho de Matos. All rights reserved.

using UnityEngine;

namespace GrassSystem.Consoles
{
    [System.Serializable]
    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
    public struct GrassDataConsole
    {
        public Vector3 position;
        public Vector2 widthHeight;

        public static int Stride => sizeof(float) * 5;

        public GrassDataConsole(Vector3 pos, float width, float height)
        {
            position = pos;
            widthHeight = new Vector2(width, height);
        }
    }
}
