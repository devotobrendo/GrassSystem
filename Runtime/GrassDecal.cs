// Copyright (c) 2026 Brendo Otavio Carvalho de Matos. All rights reserved.

using System.Collections.Generic;
using UnityEngine;

namespace GrassSystem
{
    /// <summary>
    /// Projects a decal texture onto grass rendered by one or more GrassRenderers.
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
        
        [Header("Targets")]
        [Tooltip("The GrassRenderers to apply the decal to. If empty, will try to find all in the scene.")]
        public List<GrassRenderer> targetRenderers = new List<GrassRenderer>();
        
        [Tooltip("Automatically find and target all GrassRenderers in the scene.")]
        public bool autoFindAll = false;
        
        // Shader property IDs for performance
        private static readonly int PropDecalEnabled = Shader.PropertyToID("_DecalEnabled");
        private static readonly int PropDecalTex = Shader.PropertyToID("_DecalTex");
        private static readonly int PropDecalBounds = Shader.PropertyToID("_DecalBounds");
        private static readonly int PropDecalRotation = Shader.PropertyToID("_DecalRotation");
        private static readonly int PropDecalBlend = Shader.PropertyToID("_DecalBlend");
        
        // Cache for auto-find to avoid FindObjectsOfType every frame
        private GrassRenderer[] cachedRenderers;
        private float lastAutoFindTime;
        private const float AUTO_FIND_INTERVAL = 1f;
        
        // Track previous targets to detect removals and clear decals properly
        private HashSet<GrassRenderer> previousTargets = new HashSet<GrassRenderer>();
        
        private void OnEnable()
        {
            if (autoFindAll || targetRenderers.Count == 0)
            {
                RefreshAutoFind();
            }
            ApplyDecalToAll();
        }
        
        private void OnDisable()
        {
            ClearDecalFromAll();
        }
        
        private void Update()
        {
            // Refresh auto-find periodically (not every frame for performance)
            if (autoFindAll && Time.time - lastAutoFindTime > AUTO_FIND_INTERVAL)
            {
                RefreshAutoFind();
            }
        }
        
        private void LateUpdate()
        {
            // Apply decal in LateUpdate to ensure GrassRenderers have initialized their materials
            if (enabled && decalTexture != null)
            {
                SyncTargetsAndApply();
            }
        }
        
        /// <summary>
        /// Syncs targets with previous frame, clears removed renderers, and applies to current.
        /// </summary>
        private void SyncTargetsAndApply()
        {
            var currentTargets = new HashSet<GrassRenderer>();
            
            foreach (var renderer in GetEffectiveTargets())
            {
                if (renderer != null)
                {
                    currentTargets.Add(renderer);
                    ApplyDecalToRenderer(renderer);
                }
            }
            
            // Clear decal from any renderers that were removed
            foreach (var prevRenderer in previousTargets)
            {
                if (prevRenderer != null && !currentTargets.Contains(prevRenderer))
                {
                    if (prevRenderer.MaterialInstance != null)
                    {
                        prevRenderer.MaterialInstance.SetFloat(PropDecalEnabled, 0f);
                    }
                }
            }
            
            previousTargets = currentTargets;
        }
        
        private void OnValidate()
        {
            // Clamp size to positive values
            size.x = Mathf.Max(0.1f, size.x);
            size.y = Mathf.Max(0.1f, size.y);
            
            // Clean null entries from list
            targetRenderers.RemoveAll(r => r == null);
            
            ApplyDecalToAll();
        }
        
        /// <summary>
        /// Refreshes the cached list of all GrassRenderers in the scene.
        /// </summary>
        public void RefreshAutoFind()
        {
            cachedRenderers = FindObjectsByType<GrassRenderer>(FindObjectsSortMode.None);
            lastAutoFindTime = Time.time;
        }
        
        /// <summary>
        /// Gets the effective list of target renderers (manual list or auto-found).
        /// </summary>
        private IEnumerable<GrassRenderer> GetEffectiveTargets()
        {
            if (autoFindAll)
            {
                if (cachedRenderers == null)
                    RefreshAutoFind();
                return cachedRenderers;
            }
            return targetRenderers;
        }
        
        /// <summary>
        /// Applies the decal to all target GrassRenderers.
        /// </summary>
        public void ApplyDecalToAll()
        {
            foreach (var renderer in GetEffectiveTargets())
            {
                if (renderer != null)
                    ApplyDecalToRenderer(renderer);
            }
        }
        
        /// <summary>
        /// Applies the decal to a specific GrassRenderer's material.
        /// </summary>
        private void ApplyDecalToRenderer(GrassRenderer renderer)
        {
            if (renderer.MaterialInstance == null)
            {
                // Material not ready yet, will be applied next frame
                return;
            }
            
            Material mat = renderer.MaterialInstance;
            
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
        /// Clears the decal from all target renderers.
        /// </summary>
        public void ClearDecalFromAll()
        {
            foreach (var renderer in GetEffectiveTargets())
            {
                if (renderer != null && renderer.MaterialInstance != null)
                {
                    renderer.MaterialInstance.SetFloat(PropDecalEnabled, 0f);
                }
            }
        }
        
        /// <summary>
        /// Adds a renderer to the target list if not already present.
        /// </summary>
        public void AddTarget(GrassRenderer renderer)
        {
            if (renderer != null && !targetRenderers.Contains(renderer))
            {
                targetRenderers.Add(renderer);
                ApplyDecalToRenderer(renderer);
            }
        }
        
        /// <summary>
        /// Removes a renderer from the target list.
        /// </summary>
        public void RemoveTarget(GrassRenderer renderer)
        {
            if (renderer != null && targetRenderers.Remove(renderer))
            {
                if (renderer.MaterialInstance != null)
                    renderer.MaterialInstance.SetFloat(PropDecalEnabled, 0f);
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
            Gizmos.matrix = oldMatrix;
            
            // Draw direction indicator
            Gizmos.color = Color.green;
            Vector3 forward = Quaternion.Euler(0, transform.eulerAngles.y, 0) * Vector3.forward * (size.y * 0.5f + 0.5f);
            Gizmos.DrawLine(center, center + forward);
            Gizmos.DrawSphere(center + forward, 0.15f);
        }
    }
}

