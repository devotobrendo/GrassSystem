// Copyright (c) 2026 Brendo Otavio Carvalho de Matos. All rights reserved.

using UnityEngine;
using UnityEngine.Rendering;

namespace GrassSystem
{
    [CreateAssetMenu(fileName = "GrassSettings", menuName = "Grass System/Grass Settings")]
    public class SO_GrassSettings : ScriptableObject
    {
        [Header("References")]
        public ComputeShader cullingShader;
        public Material grassMaterial;
        public Mesh grassMesh;
        
        [Header("Blade Dimensions")]
        [Range(0.01f, 0.5f)]
        public float minWidth = 0.05f;
        [Range(0.01f, 0.5f)]
        public float maxWidth = 0.15f;
        [Range(0.05f, 2f)]
        public float minHeight = 0.2f;
        [Range(0.05f, 2f)]
        public float maxHeight = 0.6f;
        
        [Header("Wind Settings")]
        [Range(0f, 5f)]
        public float windSpeed = 1f;
        [Range(0f, 1f)]
        public float windStrength = 0.3f;
        [Range(0.01f, 1f)]
        public float windFrequency = 0.1f;
        
        [Header("LOD & Culling")]
        public float minFadeDistance = 30f;
        public float maxDrawDistance = 50f;
        [Range(1, 8)]
        public int cullingTreeDepth = 4;
        
        [Header("Checkered Pattern")]
        public bool useCheckeredPattern = false;
        public Color patternColorA = new Color(0.2f, 0.5f, 0.1f);
        public Color patternColorB = new Color(0.15f, 0.45f, 0.08f);
        [Range(0.5f, 10f)]
        public float patternScale = 2f;
        
        [Header("Tip Customization")]
        public bool useTipCutout = false;
        public Texture2D tipMaskTexture;
        [Range(0f, 1f)]
        public float tipCutoffHeight = 0.8f;
        
        [Header("Textures")]
        public Texture2D albedoTexture;
        public Texture2D normalMap;
        
        [Header("Lighting")]
        public Color topTint = new Color(0.8f, 1f, 0.6f);
        public Color bottomTint = new Color(0.2f, 0.4f, 0.1f);
        [Range(0f, 1f)]
        public float translucency = 0.3f;
        public bool useAlignedNormals = true;
        
        [Header("Terrain Lightmap Blending")]
        public bool useTerrainLightmap = false;
        public Terrain terrain;
        [Range(0f, 1f)]
        public float terrainLightmapInfluence = 0.5f;
        
        [Header("Interaction")]
        [Range(0f, 2f)]
        public float interactorStrength = 1f;
        [Range(1, 16)]
        public int maxInteractors = 8;
        
        [Header("Rendering")]
        public ShadowCastingMode castShadows = ShadowCastingMode.Off;
        public bool receiveShadows = true;
        
        [Header("Debug")]
        public bool drawCullingBounds = false;
        
        public bool Validate(out string error)
        {
            if (cullingShader == null) { error = "Culling shader is not assigned"; return false; }
            if (grassMaterial == null) { error = "Grass material is not assigned"; return false; }
            if (grassMesh == null) { error = "Grass mesh is not assigned"; return false; }
            if (minWidth > maxWidth) { error = "Min width cannot be greater than max width"; return false; }
            if (minHeight > maxHeight) { error = "Min height cannot be greater than max height"; return false; }
            if (minFadeDistance >= maxDrawDistance) { error = "Min fade distance must be less than max draw distance"; return false; }
            error = null;
            return true;
        }
    }
}
