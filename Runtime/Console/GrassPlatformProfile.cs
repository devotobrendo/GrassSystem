// Copyright (c) 2026 Brendo Otavio Carvalho de Matos. All rights reserved.

using UnityEngine;
using UnityEngine.Rendering;
using GrassSystem;

namespace GrassSystem.Consoles
{
    public enum GrassPlatformClass { Full, Switch }

    [CreateAssetMenu(fileName = "GrassProfile", menuName = "Grass System/Platform Profile")]
    public class GrassPlatformProfile : ScriptableObject
    {
        public GrassPlatformClass targetClass = GrassPlatformClass.Full;

        [Header("Mesh")]
        public bool overrideMesh;
        public GrassMode meshMode = GrassMode.CustomMesh;
        public GrassProceduralType proceduralType = GrassProceduralType.Blade;
        public Mesh[] meshes;

        [Header("Thinning")]
        public bool overrideThinning;
        [Range(0f, 1f)] public float farKeepFraction = 1f;
        public float thinStartDistance = 10f;
        public float thinRampDistance = 5f;
        [Range(0f, 1f)] public float coverageCompensation = 1f;
        [Min(1f)] public float maxCoverageScale = 4f;
        public Vector2 sizeScale = Vector2.one;

        [Header("Instance Density")]
        public bool overrideInstanceDensity;
        [Range(0.01f, 1f)] public float instanceDensity = 1f;

        [Header("Albedo")]
        public bool overrideAlbedo;
        public bool useFlatAlbedo;

        [Header("Draw Distance")]
        public bool overrideDrawDistance;
        public float minFadeDistance = 30f;
        public float maxDrawDistance = 50f;

        [Header("Shadows")]
        public bool overrideShadows;
        public ShadowCastingMode castShadows = ShadowCastingMode.Off;
        public bool overrideReceiveShadows;
        public bool receiveShadows;
    }
}
