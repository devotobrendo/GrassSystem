// Copyright (c) 2026 Brendo Otavio Carvalho de Matos. All rights reserved.

using UnityEngine;

namespace GrassSystem
{
    /// <summary>
    /// Lightweight ground decal projector that creates a Quad mesh at runtime.
    /// Uses the GrassSystem/GroundDecal shader which skips grass via stencil.
    /// Draw distance is handled in-shader using _WorldSpaceCameraPos.
    /// SRP Batcher compatible via instanced material (CBUFFER properties).
    /// </summary>
    [ExecuteAlways]
    [AddComponentMenu("Grass System/Ground Decal Projector")]
    public class GroundDecalProjector : MonoBehaviour
    {
        // ========================================
        // Size
        // ========================================
        [Header("Size")]
        [Min(0.01f)]
        public float width = 1f;

        [Min(0.01f)]
        public float height = 1f;

        [Tooltip("Small offset to prevent Z-fighting.")]
        public float yOffset = 0.01f;

        // ========================================
        // Material
        // ========================================
        [Header("Material")]
        [Tooltip("Material using GrassSystem/GroundDecal shader.")]
        public Material decalMaterial;

        // ========================================
        // Material Overrides (per-instance)
        // ========================================
        [Header("Overrides")]
        public Vector2 tiling = Vector2.one;
        public Vector2 offset = Vector2.zero;

        [Range(0f, 1f)]
        public float opacity = 1f;

        // ========================================
        // Draw Distance
        // ========================================
        [Header("Draw Distance")]
        [Min(0f)]
        [Tooltip("Max distance from camera. 0 = infinite.")]
        public float drawDistance = 1000f;

        [Range(0f, 1f)]
        [Tooltip("When fade starts (0 = immediately, 1 = at max distance).")]
        public float startFade = 0.9f;

        // ========================================
        // Internal State
        // ========================================
        private MeshFilter _meshFilter;
        private MeshRenderer _meshRenderer;
        private Mesh _quadMesh;
        private Material _instanceMaterial;

        // Dirty tracking — avoids redundant GPU/CPU calls
        private float _builtWidth;
        private float _builtHeight;
        private float _builtYOffset;

        private Vector2 _syncedTiling;
        private Vector2 _syncedOffset;
        private float _syncedOpacity;
        private float _syncedDrawDistance;
        private float _syncedStartFade;
        private Material _syncedSourceMaterial;

        // Pre-allocated mesh data (zero GC after first build)
        private readonly Vector3[] _vertices = new Vector3[4];
        private readonly Vector2[] _uvs = { new(0, 0), new(1, 0), new(1, 1), new(0, 1) };
        private readonly int[] _triangles = { 0, 2, 1, 0, 3, 2 };
        private readonly Vector3[] _normals = { Vector3.up, Vector3.up, Vector3.up, Vector3.up };

        // Shader property IDs (cached, no string hashing at runtime)
        private static readonly int MainTexSTId = Shader.PropertyToID("_MainTex_ST");
        private static readonly int BlendId = Shader.PropertyToID("_Blend");
        private static readonly int DrawDistanceId = Shader.PropertyToID("_DrawDistance");
        private static readonly int StartFadeId = Shader.PropertyToID("_StartFade");

        // ========================================
        // Lifecycle
        // ========================================

        private void OnEnable()
        {
            EnsureComponents();
            ForceRebuild();
        }

        private void OnDisable()
        {
            if (_meshRenderer != null)
                _meshRenderer.enabled = false;
        }

        private void OnDestroy()
        {
            CleanupMesh();
            CleanupMaterial();
        }

        private void Update()
        {
            if (QuadSizeChanged())
                RebuildQuad();

            if (MaterialDirty())
                SyncMaterial();
        }

        private void OnValidate()
        {
            // OnValidate fires before OnEnable on recompile — guard against null
            if (_meshFilter == null || _meshRenderer == null) return;
            ForceRebuild();
        }

        /// <summary>
        /// Invalidates all dirty flags and forces a full rebuild + sync.
        /// </summary>
        private void ForceRebuild()
        {
            InvalidateDirtyFlags();
            RebuildQuad();
            SyncMaterial();
        }

        private void InvalidateDirtyFlags()
        {
            _builtWidth = -1f;
            _builtHeight = -1f;
            _builtYOffset = float.NaN;
            _syncedOpacity = -1f;
            _syncedDrawDistance = -1f;
            _syncedStartFade = -1f;
            _syncedTiling = new Vector2(float.NaN, float.NaN);
            _syncedOffset = new Vector2(float.NaN, float.NaN);
            _syncedSourceMaterial = null;
        }

        // ========================================
        // Component Setup
        // ========================================

        private void EnsureComponents()
        {
            if (_meshFilter == null)
            {
                _meshFilter = GetComponent<MeshFilter>();
                if (_meshFilter == null)
                {
                    _meshFilter = gameObject.AddComponent<MeshFilter>();
                    _meshFilter.hideFlags = HideFlags.HideInInspector;
                }
            }

            if (_meshRenderer == null)
            {
                _meshRenderer = GetComponent<MeshRenderer>();
                if (_meshRenderer == null)
                {
                    _meshRenderer = gameObject.AddComponent<MeshRenderer>();
                    _meshRenderer.hideFlags = HideFlags.HideInInspector;
                }

                _meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                _meshRenderer.receiveShadows = false;
                _meshRenderer.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
                _meshRenderer.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
                _meshRenderer.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
                _meshRenderer.allowOcclusionWhenDynamic = false;
            }

            _meshRenderer.enabled = true;
        }

        // ========================================
        // Quad Mesh (zero-allocation rebuild)
        // ========================================

        private bool QuadSizeChanged()
        {
            return !Mathf.Approximately(_builtWidth, width) ||
                   !Mathf.Approximately(_builtHeight, height) ||
                   !Mathf.Approximately(_builtYOffset, yOffset);
        }

        private void RebuildQuad()
        {
            _builtWidth = width;
            _builtHeight = height;
            _builtYOffset = yOffset;

            float halfW = width * 0.5f;
            float halfH = height * 0.5f;

            // Reuse pre-allocated array — zero GC
            _vertices[0] = new Vector3(-halfW, yOffset, -halfH);
            _vertices[1] = new Vector3( halfW, yOffset, -halfH);
            _vertices[2] = new Vector3( halfW, yOffset,  halfH);
            _vertices[3] = new Vector3(-halfW, yOffset,  halfH);

            if (_quadMesh == null)
            {
                _quadMesh = new Mesh
                {
                    name = "GroundDecal_Quad",
                    hideFlags = HideFlags.DontSave
                };

                // UVs, triangles, normals only need to be set once
                _quadMesh.vertices = _vertices;
                _quadMesh.uv = _uvs;
                _quadMesh.triangles = _triangles;
                _quadMesh.normals = _normals;
            }
            else
            {
                // Only update vertices (the part that actually changes)
                _quadMesh.vertices = _vertices;
            }

            _quadMesh.RecalculateBounds();

            if (_meshFilter != null)
                _meshFilter.sharedMesh = _quadMesh;
        }

        // ========================================
        // Material Sync (dirty-checked)
        // ========================================

        private bool MaterialDirty()
        {
            // Source material changed (or first time)
            if (_syncedSourceMaterial != decalMaterial) return true;
            if (_instanceMaterial == null) return true;

            // Per-instance overrides changed
            if (!Mathf.Approximately(_syncedOpacity, opacity)) return true;
            if (!Mathf.Approximately(_syncedDrawDistance, drawDistance)) return true;
            if (!Mathf.Approximately(_syncedStartFade, startFade)) return true;
            if (_syncedTiling != tiling) return true;
            if (_syncedOffset != offset) return true;

            return false;
        }

        /// <summary>
        /// Syncs per-instance overrides to the instanced material.
        /// Only called when dirty flags indicate a change.
        /// </summary>
        private void SyncMaterial()
        {
            if (_meshRenderer == null || decalMaterial == null) return;

            // Recreate instance when source material changes
            if (_instanceMaterial == null ||
                _syncedSourceMaterial != decalMaterial ||
                _instanceMaterial.shader != decalMaterial.shader)
            {
                CleanupMaterial();
                _instanceMaterial = new Material(decalMaterial)
                {
                    name = decalMaterial.name + " (Instance)",
                    hideFlags = HideFlags.DontSave
                };
                _meshRenderer.sharedMaterial = _instanceMaterial;
            }

            // Apply only changed properties
            if (_syncedTiling != tiling || _syncedOffset != offset)
            {
                _instanceMaterial.SetVector(MainTexSTId, new Vector4(tiling.x, tiling.y, offset.x, offset.y));
                _syncedTiling = tiling;
                _syncedOffset = offset;
            }

            if (!Mathf.Approximately(_syncedOpacity, opacity))
            {
                _instanceMaterial.SetFloat(BlendId, opacity);
                _syncedOpacity = opacity;
            }

            if (!Mathf.Approximately(_syncedDrawDistance, drawDistance))
            {
                _instanceMaterial.SetFloat(DrawDistanceId, drawDistance);
                _syncedDrawDistance = drawDistance;
            }

            if (!Mathf.Approximately(_syncedStartFade, startFade))
            {
                _instanceMaterial.SetFloat(StartFadeId, startFade);
                _syncedStartFade = startFade;
            }

            _syncedSourceMaterial = decalMaterial;
        }

        // ========================================
        // Cleanup
        // ========================================

        private void CleanupMesh()
        {
            if (_quadMesh == null) return;

            if (Application.isPlaying) Destroy(_quadMesh);
            else DestroyImmediate(_quadMesh);
            _quadMesh = null;
        }

        private void CleanupMaterial()
        {
            if (_instanceMaterial == null) return;

            if (Application.isPlaying) Destroy(_instanceMaterial);
            else DestroyImmediate(_instanceMaterial);
            _instanceMaterial = null;
        }

        // ========================================
        // Gizmos
        // ========================================

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            Gizmos.matrix = transform.localToWorldMatrix;

            Gizmos.color = new Color(0.2f, 0.8f, 1f, 0.5f);
            Vector3 center = new(0, yOffset, 0);
            Vector3 size = new(width, 0.01f, height);
            Gizmos.DrawWireCube(center, size);

            Gizmos.color = new Color(0.2f, 0.8f, 1f, 0.1f);
            Gizmos.DrawCube(center, size);

            Gizmos.color = new Color(1f, 1f, 0f, 0.8f);
            Gizmos.DrawLine(center + Vector3.up * 0.3f, center);
            Gizmos.DrawWireSphere(center, 0.02f);
        }
#endif
    }
}
