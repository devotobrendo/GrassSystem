// Copyright (c) 2026 Brendo Otavio Carvalho de Matos. All rights reserved.

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace GrassSystem
{
    public enum GrassMode
    {
        Default,         // Procedural mesh with width/height
        CustomMesh       // Imported meshes with uniform scale
    }
    
    [CreateAssetMenu(fileName = "GrassSettings", menuName = "Grass System/Grass Settings")]
    public class SO_GrassSettings : ScriptableObject
    {
        [Header("Grass Mode")]
        public GrassMode grassMode = GrassMode.Default;
        
        [Header("References")]
        public ComputeShader cullingShader;
        public Material grassMaterial;
        public Mesh grassMesh;
        
        [Header("Custom Mesh Settings")]
        [Tooltip("List of meshes to randomly spawn (CustomMesh mode only)")]
        public List<Mesh> customMeshes = new List<Mesh>();
        [Range(0.1f, 3f)]
        [Tooltip("Minimum uniform scale for custom meshes")]
        public float minSize = 0.5f;
        [Range(0.1f, 3f)]
        [Tooltip("Maximum uniform scale for custom meshes")]
        public float maxSize = 1.2f;
        [Tooltip("Use only the albedo texture color, ignoring tints and grass color")]
        public bool useOnlyAlbedoColor = false;
        [Tooltip("Rotation offset for custom meshes (degrees)")]
        public Vector3 meshRotationOffset = Vector3.zero;
        
        [Header("Natural Variation")]
        [Range(0f, 45f)]
        [Tooltip("Maximum random tilt angle in degrees for natural clump look")]
        public float maxTiltAngle = 15f;
        [Range(0f, 1f)]
        [Tooltip("How much the tilt varies between grass instances (0 = no tilt, 1 = full random)")]
        public float tiltVariation = 0.7f;
        
        [Header("Blade Dimensions (Default Mode)")]
        [Range(0.01f, 0.3f)]
        public float minWidth = 0.03f;
        [Range(0.01f, 0.3f)]
        public float maxWidth = 0.08f;
        [Range(0.05f, 1.5f)]
        public float minHeight = 0.15f;
        [Range(0.05f, 1.5f)]
        public float maxHeight = 0.35f;
        
        [Header("Wind Settings")]
        [Range(0f, 5f)]
        public float windSpeed = 1.2f;
        [Range(0f, 1f)]
        public float windStrength = 0.25f;
        [Range(0.01f, 1f)]
        public float windFrequency = 0.15f;
        
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
        
        [Header("Textures (Custom Mesh Mode Only)")]
        public Texture2D albedoTexture;
        public Texture2D normalMap;
        
        [Header("Lighting")]
        // Zelda BOTW-style vibrant green tints
        public Color topTint = new Color(0.45f, 0.85f, 0.25f);
        public Color bottomTint = new Color(0.15f, 0.35f, 0.08f);
        [Range(0f, 1f)]
        public float translucency = 0.4f;
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
            
            // Mode-specific validation
            if (grassMode == GrassMode.Default)
            {
                // Default mode uses procedural mesh, so grassMesh is optional
                if (minWidth > maxWidth) { error = "Min width cannot be greater than max width"; return false; }
                if (minHeight > maxHeight) { error = "Min height cannot be greater than max height"; return false; }
            }
            else // CustomMesh mode
            {
                if (customMeshes == null || customMeshes.Count == 0) { error = "At least one custom mesh is required for CustomMesh mode"; return false; }
                foreach (var mesh in customMeshes)
                {
                    if (mesh == null) { error = "Custom meshes list contains null entries"; return false; }
                }
                if (minSize > maxSize) { error = "Min size cannot be greater than max size"; return false; }
            }
            
            if (minFadeDistance >= maxDrawDistance) { error = "Min fade distance must be less than max draw distance"; return false; }
            error = null;
            return true;
        }
        
        /// <summary>
        /// Gets the active mesh based on the current mode.
        /// Default mode uses a procedural Zelda-style triangular blade.
        /// CustomMesh mode uses meshes from the customMeshes list.
        /// </summary>
        public Mesh GetActiveMesh(int seed = 0)
        {
            if (grassMode == GrassMode.CustomMesh && customMeshes != null && customMeshes.Count > 0)
            {
                int index = Mathf.Abs(seed) % customMeshes.Count;
                return customMeshes[index];
            }
            
            // Default mode: always use procedural Zelda-style blade
            return GrassMeshUtility.GetZeldaStyleBlade();
        }
        
        // ========================================
        // ADVANCED LIMITS
        // ========================================
        
        [Header("Advanced Limits")]
        [Tooltip("Customize slider maximum values for extended ranges")]
        public bool showAdvancedLimits = false;
        
        [Header("Size Limits")]
        [Min(0.5f)]
        public float maxSizeLimit = 3f;
        [Min(0.1f)]
        public float maxBladeWidthLimit = 0.3f;
        [Min(0.5f)]
        public float maxBladeHeightLimit = 1.5f;
        
        [Header("Wind Limits")]
        [Min(1f)]
        public float maxWindSpeedLimit = 5f;
        [Min(0.5f)]
        public float maxWindStrengthLimit = 1f;
        
        [Header("LOD Limits")]
        [Min(50f)]
        public float maxDrawDistanceLimit = 200f;
        [Min(30f)]
        public float maxFadeDistanceLimit = 150f;
        
        [Header("Tilt Limits")]
        [Min(15f)]
        public float maxTiltAngleLimit = 45f;
        
        [Header("Interaction Limits")]
        [Min(1f)]
        public float maxInteractorStrengthLimit = 2f;
        [Min(8)]
        public int maxInteractorsLimit = 16;
        
        [Header("Pattern Limits")]
        [Min(5f)]
        public float maxPatternScaleLimit = 10f;
    }
}
