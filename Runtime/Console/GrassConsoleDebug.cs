// Copyright (c) 2026 Brendo Otavio Carvalho de Matos. All rights reserved.

using System.Collections.Generic;
using UnityEngine;
using GrassSystem;

namespace GrassSystem.Consoles
{
    public static class GrassConsoleDebug
    {
        public static bool OverrideEnabled;
        public static bool FlatAlbedoEnabled;
        public static bool InstanceDensityOverrideEnabled;
        public static float InstanceDensity = 1f;
        public static bool ReadoutEnabled;
        public static float FarKeepFraction = 1f;
        public static float ThinStartDistance = 0f;
        public static float CoverageCompensation = 0f;
        public static Vector2 SizeScale = Vector2.one;
        public static bool ModeOverrideEnabled;
        public static GrassMode ModeOverride = GrassMode.CustomMesh;
        public static bool BladeTypeOverrideEnabled;
        public static GrassProceduralType BladeTypeOverride = GrassProceduralType.Blade;
        public static readonly List<GrassRendererConsole> ActiveRenderers = new();

        public static int TotalInstances
        {
            get
            {
                int total = 0;
                for (int i = 0; i < ActiveRenderers.Count; i++)
                {
                    var renderer = ActiveRenderers[i];
                    if (renderer == null) continue;
                    total += renderer.TotalGrassCount;
                }
                return total;
            }
        }

        public static int VisibleInstances
        {
            get
            {
                int visible = 0;
                for (int i = 0; i < ActiveRenderers.Count; i++)
                {
                    var renderer = ActiveRenderers[i];
                    if (renderer == null) continue;
                    visible += renderer.VisibleGrassCount;
                }
                return visible;
            }
        }
    }
}
