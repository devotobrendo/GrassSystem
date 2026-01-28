// Copyright (c) 2026 Brendo Otavio Carvalho de Matos. All rights reserved.

using UnityEngine;

namespace GrassSystem
{
    /// <summary>
    /// Projects a decal texture onto grass rendered by GrassRenderer.
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
        
        [Header("Target")]
        [Tooltip("The GrassRenderer to apply the decal to. If null, will try to find one in the scene.")]
        public GrassRenderer targetRenderer;
        
        // Shader property IDs for performance
        private static readonly int PropDecalEnabled = Shader.PropertyToID("_DecalEnabled");
        private static readonly int PropDecalTex = Shader.PropertyToID("_DecalTex");
        private static readonly int PropDecalBounds = Shader.PropertyToID("_DecalBounds");
        private static readonly int PropDecalRotation = Shader.PropertyToID("_DecalRotation");
        private static readonly int PropDecalBlend = Shader.PropertyToID("_DecalBlend");
        
        private void OnEnable()
        {
            if (targetRenderer == null)
            {
                targetRenderer = FindFirstObjectByType<GrassRenderer>();
            }
            ApplyDecal();
        }
        
        private void OnDisable()
        {
            ClearDecal();
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
            }
        }
        
        private void OnValidate()
        {
            // Clamp size to positive values
            size.x = Mathf.Max(0.1f, size.x);
            size.y = Mathf.Max(0.1f, size.y);
            
            ApplyDecal();
        }
        
        /// <summary>
        /// Applies the decal to the target GrassRenderer's material.
        /// </summary>
        public void ApplyDecal()
        {
            if (targetRenderer == null)
            {
                Debug.LogWarning("[GrassDecal] No targetRenderer assigned or found!", this);
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
        
        private void OnDrawGizmosSelected()
        {
            // Draw decal bounds in scene view
            Gizmos.color = new Color(0f, 1f, 0.5f, 0.5f);
            
            Vector3 center = new Vector3(transform.position.x, transform.position.y, transform.position.z);
            Vector3 halfSize = new Vector3(size.x * 0.5f, 0.1f, size.y * 0.5f);
            
            // Draw rotated wireframe box
            Matrix4x4 oldMatrix = Gizmos.matrix;
            Gizmos.matrix = Matrix4x4.TRS(center, Quaternion.Euler(0, transform.eulerAngles.y, 0), Vector3.one);
            Gizmos.DrawWireCube(Vector3.zero, new Vector3(size.x, 0.2f, size.y));
            Gizmos.color = new Color(0f, 1f, 0.5f, 0.15f);
            Gizmos.DrawCube(Vector3.zero, new Vector3(size.x, 0.1f, size.y));
            Gizmos.matrix = oldMatrix;
            
            // Draw direction indicator
            Gizmos.color = Color.green;
            Vector3 forward = Quaternion.Euler(0, transform.eulerAngles.y, 0) * Vector3.forward * (size.y * 0.5f + 0.5f);
            Gizmos.DrawLine(center, center + forward);
            Gizmos.DrawSphere(center + forward, 0.15f);
        }
    }
}
