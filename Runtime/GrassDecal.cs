// Copyright (c) 2026 Brendo Otavio Carvalho de Matos. All rights reserved.

using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace GrassSystem
{
    /// <summary>
    /// Projects a decal texture onto grass rendered by GrassRenderer.
    /// Optionally also projects onto the terrain/surface below using URP Decal Projector.
    /// Position and rotation are controlled via the Transform component.
    /// </summary>
    [ExecuteAlways]
    public class GrassDecal : MonoBehaviour
    {
        [Header("Decal Settings")]
        [Tooltip("The texture to project onto the grass. Use textures with alpha for transparency.")]
        public Texture2D decalTexture;
        
        [Tooltip("Size of the decal projection area in world units (X, Z).")]
        public Vector2 size = new Vector2(5f, 5f);
        
        [Range(0f, 1f)]
        [Tooltip("Blend strength of the decal. 0 = invisible, 1 = fully visible.")]
        public float blend = 1f;
        
        [Header("Surface Decal (Terrain)")]
        [Tooltip("Also project the decal onto the terrain/surface below the grass using URP Decal Projector.")]
        public bool paintSurface = false;
        
        [Range(0f, 1f)]
        [Tooltip("Opacity of the surface decal. Lower values make it more subtle under the grass.")]
        public float surfaceBlend = 0.3f;
        
        [Tooltip("Material for the surface decal. If null, a default URP Decal material will be created.")]
        public Material surfaceDecalMaterial;
        
        [Tooltip("Projection depth for surface decal (how far down to project).")]
        public float projectionDepth = 10f;
        
        [Header("Target")]
        [Tooltip("The GrassRenderer to apply the decal to. If null, will try to find one in the scene.")]
        public GrassRenderer targetRenderer;
        
        // Shader property IDs for performance
        private static readonly int PropDecalEnabled = Shader.PropertyToID("_DecalEnabled");
        private static readonly int PropDecalTex = Shader.PropertyToID("_DecalTex");
        private static readonly int PropDecalBounds = Shader.PropertyToID("_DecalBounds");
        private static readonly int PropDecalRotation = Shader.PropertyToID("_DecalRotation");
        private static readonly int PropDecalBlend = Shader.PropertyToID("_DecalBlend");
        
        // Surface decal components
        private DecalProjector _surfaceProjector;
        private Material _generatedMaterial;
        
        private void OnEnable()
        {
            if (targetRenderer == null)
            {
                targetRenderer = FindFirstObjectByType<GrassRenderer>();
            }
            ApplyDecal();
            UpdateSurfaceDecal();
        }
        
        private void OnDisable()
        {
            ClearDecal();
            DisableSurfaceDecal();
        }
        
        private void OnDestroy()
        {
            CleanupGeneratedMaterial();
            if (_surfaceProjector != null)
            {
                if (Application.isPlaying)
                    Destroy(_surfaceProjector.gameObject);
                else
                    DestroyImmediate(_surfaceProjector.gameObject);
            }
        }
        
        private void Update()
        {
            // Try to find renderer if not set
            if (targetRenderer == null)
            {
                targetRenderer = FindFirstObjectByType<GrassRenderer>();
            }
        }
        
        private void LateUpdate()
        {
            // Apply decal in LateUpdate to ensure GrassRenderer has initialized its material
            if (enabled && decalTexture != null)
            {
                ApplyDecal();
                UpdateSurfaceDecal();
            }
        }
        
        private void OnValidate()
        {
            // Clamp size to positive values
            size.x = Mathf.Max(0.1f, size.x);
            size.y = Mathf.Max(0.1f, size.y);
            projectionDepth = Mathf.Max(0.1f, projectionDepth);
            
            ApplyDecal();
            
            // Delay surface decal update in editor to avoid issues
            #if UNITY_EDITOR
            UnityEditor.EditorApplication.delayCall += () =>
            {
                if (this != null)
                    UpdateSurfaceDecal();
            };
            #endif
        }
        
        /// <summary>
        /// Applies the decal to the target GrassRenderer's material.
        /// </summary>
        public void ApplyDecal()
        {
            if (targetRenderer == null)
            {
                return;
            }
            
            if (targetRenderer.MaterialInstance == null)
            {
                // Material not ready yet, will be applied next frame
                return;
            }
            
            Material mat = targetRenderer.MaterialInstance;
            
            if (decalTexture != null && enabled)
            {
                mat.SetFloat(PropDecalEnabled, 1f);
                mat.SetTexture(PropDecalTex, decalTexture);
                mat.SetVector(PropDecalBounds, new Vector4(
                    transform.position.x,
                    transform.position.z,
                    size.x,
                    size.y
                ));
                mat.SetFloat(PropDecalRotation, transform.eulerAngles.y * Mathf.Deg2Rad);
                mat.SetFloat(PropDecalBlend, blend);
            }
            else
            {
                mat.SetFloat(PropDecalEnabled, 0f);
            }
        }
        
        /// <summary>
        /// Clears the decal from the material.
        /// </summary>
        public void ClearDecal()
        {
            if (targetRenderer != null && targetRenderer.MaterialInstance != null)
            {
                targetRenderer.MaterialInstance.SetFloat(PropDecalEnabled, 0f);
            }
        }
        
        /// <summary>
        /// Updates or creates the surface decal projector.
        /// </summary>
        private void UpdateSurfaceDecal()
        {
            if (!paintSurface || decalTexture == null)
            {
                DisableSurfaceDecal();
                return;
            }
            
            // Create or get the projector
            if (_surfaceProjector == null)
            {
                CreateSurfaceProjector();
            }
            
            if (_surfaceProjector == null) return;
            
            _surfaceProjector.enabled = true;
            
            // Update projector properties
            _surfaceProjector.size = new Vector3(size.x, projectionDepth, size.y);
            _surfaceProjector.fadeFactor = surfaceBlend;
            
            // Update material texture
            Material projMat = GetOrCreateProjectorMaterial();
            if (projMat != null)
            {
                projMat.SetTexture("_Base_Map", decalTexture);
                _surfaceProjector.material = projMat;
            }
            
            // Match transform
            Transform projTransform = _surfaceProjector.transform;
            projTransform.position = transform.position;
            projTransform.rotation = Quaternion.Euler(90f, transform.eulerAngles.y, 0f);
        }
        
        private void CreateSurfaceProjector()
        {
            // Check for existing child projector
            Transform existingChild = transform.Find("SurfaceDecalProjector");
            if (existingChild != null)
            {
                _surfaceProjector = existingChild.GetComponent<DecalProjector>();
                if (_surfaceProjector != null) return;
            }
            
            // Create new projector as child (visible and editable)
            GameObject projectorObj = new GameObject("SurfaceDecalProjector");
            projectorObj.transform.SetParent(transform);
            projectorObj.transform.localPosition = Vector3.zero;
            
            _surfaceProjector = projectorObj.AddComponent<DecalProjector>();
            _surfaceProjector.pivot = new Vector3(0f, 1f, 0f);
            _surfaceProjector.scaleMode = DecalScaleMode.ScaleInvariant;
            _surfaceProjector.drawDistance = 1000f;
            _surfaceProjector.startAngleFade = 180f;
            _surfaceProjector.endAngleFade = 180f;
        }
        
        private Material GetOrCreateProjectorMaterial()
        {
            if (surfaceDecalMaterial != null)
            {
                return surfaceDecalMaterial;
            }
            
            // Create a simple decal material if none provided
            if (_generatedMaterial == null)
            {
                Shader decalShader = Shader.Find("Shader Graphs/Decal");
                if (decalShader == null)
                {
                    decalShader = Shader.Find("Universal Render Pipeline/Decal");
                }
                
                if (decalShader != null)
                {
                    _generatedMaterial = new Material(decalShader);
                    _generatedMaterial.name = "GrassDecal_Generated";
                    _generatedMaterial.hideFlags = HideFlags.DontSave;
                }
            }
            
            return _generatedMaterial;
        }
        
        private void DisableSurfaceDecal()
        {
            if (_surfaceProjector != null)
            {
                _surfaceProjector.enabled = false;
            }
        }
        
        private void CleanupGeneratedMaterial()
        {
            if (_generatedMaterial != null)
            {
                if (Application.isPlaying)
                    Destroy(_generatedMaterial);
                else
                    DestroyImmediate(_generatedMaterial);
                _generatedMaterial = null;
            }
        }
        
        private void OnDrawGizmosSelected()
        {
            // Draw decal bounds in scene view
            Gizmos.color = new Color(0f, 1f, 0.5f, 0.5f);
            
            Vector3 center = new Vector3(transform.position.x, transform.position.y, transform.position.z);
            
            // Draw rotated wireframe box
            Matrix4x4 oldMatrix = Gizmos.matrix;
            Gizmos.matrix = Matrix4x4.TRS(center, Quaternion.Euler(0, transform.eulerAngles.y, 0), Vector3.one);
            Gizmos.DrawWireCube(Vector3.zero, new Vector3(size.x, 0.2f, size.y));
            Gizmos.color = new Color(0f, 1f, 0.5f, 0.15f);
            Gizmos.DrawCube(Vector3.zero, new Vector3(size.x, 0.1f, size.y));
            
            // Draw projection depth
            if (paintSurface)
            {
                Gizmos.color = new Color(0f, 0.5f, 1f, 0.3f);
                Gizmos.DrawWireCube(new Vector3(0, -projectionDepth * 0.5f, 0), new Vector3(size.x, projectionDepth, size.y));
            }
            
            Gizmos.matrix = oldMatrix;
            
            // Draw direction indicator
            Gizmos.color = Color.green;
            Vector3 forward = Quaternion.Euler(0, transform.eulerAngles.y, 0) * Vector3.forward * (size.y * 0.5f + 0.5f);
            Gizmos.DrawLine(center, center + forward);
            Gizmos.DrawSphere(center + forward, 0.15f);
        }
    }
}

