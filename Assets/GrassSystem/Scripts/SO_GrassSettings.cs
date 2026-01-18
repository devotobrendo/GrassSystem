// SO_GrassSettings.cs - ScriptableObject for grass system configuration
// Create via: Assets > Create > Grass System > Grass Settings

using UnityEngine;
using UnityEngine.Rendering;

namespace GrassSystem
{
    [CreateAssetMenu(fileName = "GrassSettings", menuName = "Grass System/Grass Settings")]
    public class SO_GrassSettings : ScriptableObject
    {
        [Header("References")]
        [Tooltip("Compute shader for GPU culling")]
        public ComputeShader cullingShader;
        
        [Tooltip("Material using the GrassLit shader")]
        public Material grassMaterial;
        
        [Tooltip("Grass blade mesh (typically a simple quad or triangle)")]
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
        [Tooltip("Distance where grass starts fading")]
        public float minFadeDistance = 30f;
        [Tooltip("Distance where grass is fully invisible")]
        public float maxDrawDistance = 50f;
        [Tooltip("Depth of spatial culling tree (higher = more precise, slower)")]
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
        [Tooltip("Optional texture for tip shape (alpha cutout)")]
        public Texture2D tipMaskTexture;
        [Range(0f, 1f)]
        public float tipCutoffHeight = 0.8f;
        
        [Header("Textures")]
        [Tooltip("Main grass texture (albedo)")]
        public Texture2D albedoTexture;
        [Tooltip("Normal map for grass blades")]
        public Texture2D normalMap;
        
        [Header("Lighting")]
        public Color topTint = new Color(0.8f, 1f, 0.6f);
        public Color bottomTint = new Color(0.2f, 0.4f, 0.1f);
        [Range(0f, 1f)]
        public float translucency = 0.3f;
        public bool useAlignedNormals = true;
        
        [Header("Interaction")]
        [Range(0f, 2f)]
        public float interactorStrength = 1f;
        [Tooltip("Maximum number of interactors (characters) affecting grass")]
        [Range(1, 16)]
        public int maxInteractors = 8;
        
        [Header("Rendering")]
        public ShadowCastingMode castShadows = ShadowCastingMode.Off;
        public bool receiveShadows = true;
        
        [Header("Debug")]
        public bool drawCullingBounds = false;
        
        /// <summary>
        /// Validates settings and returns error message if invalid
        /// </summary>
        public bool Validate(out string error)
        {
            if (cullingShader == null)
            {
                error = "Culling shader is not assigned";
                return false;
            }
            if (grassMaterial == null)
            {
                error = "Grass material is not assigned";
                return false;
            }
            if (grassMesh == null)
            {
                error = "Grass mesh is not assigned";
                return false;
            }
            if (minWidth > maxWidth)
            {
                error = "Min width cannot be greater than max width";
                return false;
            }
            if (minHeight > maxHeight)
            {
                error = "Min height cannot be greater than max height";
                return false;
            }
            if (minFadeDistance >= maxDrawDistance)
            {
                error = "Min fade distance must be less than max draw distance";
                return false;
            }
            
            error = null;
            return true;
        }
    }
}
