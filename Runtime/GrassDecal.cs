// Copyright (c) 2026 Brendo Otavio Carvalho de Matos. All rights reserved.

using System.Collections.Generic;
using UnityEngine;

namespace GrassSystem
{
    /// <summary>
    /// Decal layer determines which shader slot this decal uses.
    /// Higher layers override lower layers in the same area.
    /// </summary>
    public enum DecalLayer
    {
        Layer1 = 0,
        Layer2 = 1,
        Layer3 = 2,
        Layer4 = 3,
        Layer5 = 4
    }
    
    /// <summary>
    /// Blend mode for how the decal color is combined with the base color.
    /// </summary>
    public enum DecalBlendMode
    {
        Override = 0,   // Replaces base color entirely
        Multiply = 1,   // Multiplies with base color
        Additive = 2    // Adds to base color
    }
    
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
        
        [Range(0f, 360f)]
        [Tooltip("Rotation of the decal texture in degrees. Use this OR Transform rotation.")]
        public float rotation = 0f;
        
        [Range(0f, 1f)]
        [Tooltip("Blend strength of the decal. 0 = invisible, 1 = fully visible.")]
        public float blend = 1f;
        
        [Tooltip("Which decal layer to use. Higher layers override lower layers.")]
        public DecalLayer layer = DecalLayer.Layer1;
        
        [Tooltip("How the decal color is combined with the base color.")]
        public DecalBlendMode blendMode = DecalBlendMode.Override;
        
        [Header("Targets")]
        [Tooltip("The GrassRenderers to apply the decal to. If empty, will try to find all in the scene.")]
        public List<GrassRenderer> targetRenderers = new List<GrassRenderer>();
        
        [Tooltip("Automatically find and target all GrassRenderers in the scene.")]
        public bool autoFindAll = false;
        
        // Shader property IDs for each layer (5 layers)
        private static readonly int[] PropDecalEnabled = {
            Shader.PropertyToID("_DecalEnabled"),
            Shader.PropertyToID("_Decal2Enabled"),
            Shader.PropertyToID("_Decal3Enabled"),
            Shader.PropertyToID("_Decal4Enabled"),
            Shader.PropertyToID("_Decal5Enabled")
        };
        private static readonly int[] PropDecalTex = {
            Shader.PropertyToID("_DecalTex"),
            Shader.PropertyToID("_Decal2Tex"),
            Shader.PropertyToID("_Decal3Tex"),
            Shader.PropertyToID("_Decal4Tex"),
            Shader.PropertyToID("_Decal5Tex")
        };
        private static readonly int[] PropDecalBounds = {
            Shader.PropertyToID("_DecalBounds"),
            Shader.PropertyToID("_Decal2Bounds"),
            Shader.PropertyToID("_Decal3Bounds"),
            Shader.PropertyToID("_Decal4Bounds"),
            Shader.PropertyToID("_Decal5Bounds")
        };
        private static readonly int[] PropDecalRotation = {
            Shader.PropertyToID("_DecalRotation"),
            Shader.PropertyToID("_Decal2Rotation"),
            Shader.PropertyToID("_Decal3Rotation"),
            Shader.PropertyToID("_Decal4Rotation"),
            Shader.PropertyToID("_Decal5Rotation")
        };
        private static readonly int[] PropDecalBlend = {
            Shader.PropertyToID("_DecalBlend"),
            Shader.PropertyToID("_Decal2Blend"),
            Shader.PropertyToID("_Decal3Blend"),
            Shader.PropertyToID("_Decal4Blend"),
            Shader.PropertyToID("_Decal5Blend")
        };
        private static readonly int[] PropDecalBlendMode = {
            Shader.PropertyToID("_DecalBlendMode"),
            Shader.PropertyToID("_Decal2BlendMode"),
            Shader.PropertyToID("_Decal3BlendMode"),
            Shader.PropertyToID("_Decal4BlendMode"),
            Shader.PropertyToID("_Decal5BlendMode")
        };
        
        // Cache for auto-find to avoid FindObjectsOfType every frame
        private GrassRenderer[] cachedRenderers;
        private float lastAutoFindTime;
        private const float AUTO_FIND_INTERVAL = 1f;
        
        // Track previous targets to detect removals and clear decals properly
        private HashSet<GrassRenderer> previousTargets = new HashSet<GrassRenderer>();
        
        // Track previous settings to detect changes and clear old layer data
        private DecalLayer previousLayer;
        private bool settingsInitialized = false;
        
        private void OnEnable()
        {
            if (autoFindAll || targetRenderers.Count == 0)
            {
                RefreshAutoFind();
            }
            
            // Initialize tracking
            previousLayer = layer;
            settingsInitialized = true;
            
            ApplyDecalToAll();
        }
        
        private void OnDisable()
        {
            ClearDecalFromAll();
            settingsInitialized = false;
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
        /// Also detects layer changes and clears old layer data to prevent ghost images.
        /// </summary>
        private void SyncTargetsAndApply()
        {
            var currentTargets = new HashSet<GrassRenderer>();
            
            // Detect if the layer has changed - need to clear old layer data
            bool layerChanged = settingsInitialized && previousLayer != layer;
            int oldLayerIdx = (int)previousLayer;
            
            foreach (var renderer in GetEffectiveTargets())
            {
                if (renderer != null)
                {
                    currentTargets.Add(renderer);
                    
                    // If layer changed, clear the old layer first
                    if (layerChanged && renderer.MaterialInstance != null)
                    {
                        renderer.MaterialInstance.SetFloat(PropDecalEnabled[oldLayerIdx], 0f);
                    }
                    
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
                        // Clear from current layer (and old layer if changed)
                        int idx = (int)layer;
                        prevRenderer.MaterialInstance.SetFloat(PropDecalEnabled[idx], 0f);
                        if (layerChanged)
                        {
                            prevRenderer.MaterialInstance.SetFloat(PropDecalEnabled[oldLayerIdx], 0f);
                        }
                    }
                }
            }
            
            // Update tracking
            previousLayer = layer;
            previousTargets = currentTargets;
        }
        
        private void OnValidate()
        {
            // Clamp size to positive values
            size.x = Mathf.Max(0.1f, size.x);
            size.y = Mathf.Max(0.1f, size.y);
            
            // Clean null entries from list
            targetRenderers.RemoveAll(r => r == null);
            
            // Detect layer change and clear old layer data to prevent ghost images
            if (settingsInitialized && previousLayer != layer)
            {
                int oldLayerIdx = (int)previousLayer;
                foreach (var renderer in GetEffectiveTargets())
                {
                    if (renderer != null && renderer.MaterialInstance != null)
                    {
                        renderer.MaterialInstance.SetFloat(PropDecalEnabled[oldLayerIdx], 0f);
                    }
                }
                previousLayer = layer;
            }
            
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
            int idx = (int)layer;
            
            if (decalTexture != null && enabled)
            {
                mat.SetFloat(PropDecalEnabled[idx], 1f);
                mat.SetTexture(PropDecalTex[idx], decalTexture);
                mat.SetVector(PropDecalBounds[idx], new Vector4(
                    transform.position.x,
                    transform.position.z,
                    size.x,
                    size.y
                ));
                // Combine component rotation field with transform Y rotation
                float totalRotation = (rotation + transform.eulerAngles.y) * Mathf.Deg2Rad;
                mat.SetFloat(PropDecalRotation[idx], totalRotation);
                mat.SetFloat(PropDecalBlend[idx], blend);
                mat.SetFloat(PropDecalBlendMode[idx], (float)blendMode);
            }
            else
            {
                mat.SetFloat(PropDecalEnabled[idx], 0f);
            }
        }
        
        /// <summary>
        /// Clears the decal from all target renderers.
        /// </summary>
        public void ClearDecalFromAll()
        {
            int idx = (int)layer;
            foreach (var renderer in GetEffectiveTargets())
            {
                if (renderer != null && renderer.MaterialInstance != null)
                {
                    renderer.MaterialInstance.SetFloat(PropDecalEnabled[idx], 0f);
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
                {
                    int idx = (int)layer;
                    renderer.MaterialInstance.SetFloat(PropDecalEnabled[idx], 0f);
                }
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

