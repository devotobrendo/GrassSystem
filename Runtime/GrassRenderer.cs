// Copyright (c) 2026 Brendo Otavio Carvalho de Matos. All rights reserved.

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Serialization;

namespace GrassSystem
{
    [ExecuteAlways]
    public class GrassRenderer : MonoBehaviour, ISerializationCallbackReceiver
    {
        [Header("Settings")]
        public SO_GrassSettings settings;
        
        [Header("External Data (Optional)")]
        [Tooltip("Optional: Store grass data in an external asset instead of the scene. Recommended for large grass counts.")]
        public GrassDataAsset externalDataAsset;
        
        // ==== GRASS DATA STORAGE ====
        // PERF FIX: grassData is [NonSerialized] to prevent Unity from serializing
        // 500k+ items every time the Inspector is opened/refreshed.
        // Unity creates a SerializedObject internally when any component is selected,
        // which serializes ALL [SerializeField] fields. With 500k GrassData structs,
        // this caused multi-second freezes on every Inspector interaction.
        //
        // Persistence is handled exclusively through GrassDataAsset (external asset).
        // The legacy field below handles migration from scenes saved before this change.
        [System.NonSerialized]
        private List<GrassData> grassData = new List<GrassData>();
        
        // Legacy: preserves embedded data from scenes saved before the NonSerialized change.
        // Unity deserializes existing scene data into this field via FormerlySerializedAs.
        // OnAfterDeserialize migrates it to the runtime grassData field, then clears it.
        [SerializeField, HideInInspector, FormerlySerializedAs("grassData")]
        private List<GrassData> _embeddedGrassDataLegacy = new List<GrassData>();
        
        private ComputeBuffer sourceBuffer;
        private ComputeBuffer visibleBuffer;
        private GraphicsBuffer argsBuffer;
        private ComputeBuffer interactorBuffer;
        
        // Per-renderer instance of the compute shader.
        // CRITICAL: Multiple renderers sharing the same ComputeShader asset causes
        // buffer binding collisions and D3D11 DEVICE_LOST crashes.
        // Each renderer MUST have its own instance.
        private ComputeShader cullingShaderInstance;
        private int cullingKernel;
        private const int THREAD_GROUP_SIZE = 128;
        
        private Bounds renderBounds;
        private readonly uint[] argsReset = new uint[5] { 0, 0, 0, 0, 0 };
        private Vector4[] interactorData = new Vector4[16];
        private Material materialInstance;
        private Mesh cachedMesh;
        
        private static readonly int PropSourceBuffer = Shader.PropertyToID("_SourceBuffer");
        private static readonly int PropVisibleBuffer = Shader.PropertyToID("_VisibleBuffer");
        private static readonly int PropIndirectArgs = Shader.PropertyToID("_IndirectArgsBuffer");
        private static readonly int PropGrassBuffer = Shader.PropertyToID("_GrassBuffer");
        private static readonly int PropViewProjMatrix = Shader.PropertyToID("_ViewProjectionMatrix");
        private static readonly int PropCameraPos = Shader.PropertyToID("_CameraPosition");
        private static readonly int PropFrustumPlanes = Shader.PropertyToID("_FrustumPlanes");
        private static readonly int PropMinFade = Shader.PropertyToID("_MinFadeDistance");
        private static readonly int PropMaxDraw = Shader.PropertyToID("_MaxDrawDistance");
        private static readonly int PropInstanceCount = Shader.PropertyToID("_InstanceCount");
        private static readonly int PropInteractors = Shader.PropertyToID("_Interactors");
        private static readonly int PropInteractorCount = Shader.PropertyToID("_InteractorCount");
        private static readonly int PropInteractorStrength = Shader.PropertyToID("_InteractorStrength");
        private static readonly int PropTime = Shader.PropertyToID("_Time");
        private static readonly int PropWindSpeed = Shader.PropertyToID("_WindSpeed");
        private static readonly int PropWindStrength = Shader.PropertyToID("_WindStrength");
        private static readonly int PropWindFrequency = Shader.PropertyToID("_WindFrequency");
        
        private Vector4[] frustumPlanes = new Vector4[6];
        private Plane[] cameraPlanes = new Plane[6];
        private bool isInitialized;
        
        #if UNITY_EDITOR
        private int deferredLoadRetries = 0;
        private const int MAX_DEFERRED_RETRIES = 5;
        #endif
        
        private uint[] readbackArgs = new uint[5];
        private int lastVisibleCount;
        
        // Track if material needs reapplication (after scene save, domain reload, etc.)
        private bool materialDirty = false;
        private float lastMaterialCheck = 0f;
        private const float MATERIAL_CHECK_INTERVAL = 0.5f;
        
        // ========================================
        // FAILSAFE: Auto-recovery system
        // Guarantees grass ALWAYS appears, even if
        // initial load fails (additive scenes, async loading, etc.)
        // ========================================
        private float lastRecoveryAttemptTime = -999f;
        private int recoveryAttemptCount = 0;
        private const float RECOVERY_INTERVAL = 0.5f;
        private const float RECOVERY_BACKOFF_INTERVAL = 3.0f;
        private const int RECOVERY_BACKOFF_THRESHOLD = 10;
        
        public List<GrassData> GrassDataList
        {
            get => grassData;
            set
            {
                grassData = value;
                if (isInitialized) RebuildBuffers();
            }
        }
        
        public int VisibleGrassCount => lastVisibleCount;
        
        /// <summary>
        /// Gets the material instance used for rendering. Used by GrassDecal for applying decals.
        /// </summary>
        public Material MaterialInstance => materialInstance;
        
        // ========================================
        // DIAGNOSTIC TOOLS
        // ========================================
        
        /// <summary>
        /// Force cleans up and reinitializes all rendering resources.
        /// If local data is empty, attempts to recover from external asset.
        /// Use this when grass is not rendering correctly.
        /// </summary>
        [ContextMenu("Force Reinitialize")]
        public void ForceReinitialize()
        {
            Cleanup();
            
            // Attempt to recover from external asset if local data is empty
            if (grassData.Count == 0 && externalDataAsset != null && externalDataAsset.InstanceCount > 0)
            {
                Debug.Log($"GrassRenderer: Recovering {externalDataAsset.InstanceCount:N0} instances from external asset.", this);
                grassData = externalDataAsset.LoadData();
            }
            
            if (grassData.Count > 0)
                Initialize();
            
            Debug.Log($"GrassRenderer: Reinitialized. instances={grassData.Count}, isInitialized={isInitialized}", this);
        }
        
        /// <summary>
        /// Checks if all GPU buffers are valid and ready for rendering.
        /// Returns false if any buffer is null or invalid (indicating corruption).
        /// </summary>
        public bool AreBuffersValid()
        {
            if (!isInitialized) return false;
            
            bool buffersValid = sourceBuffer != null && sourceBuffer.IsValid() &&
                         visibleBuffer != null && visibleBuffer.IsValid() &&
                         argsBuffer != null && argsBuffer.IsValid();
            
            bool materialValid = materialInstance != null && cachedMesh != null && settings != null;
            
            // Also check if the grass buffer is still bound to the material
            // After scene changes, the material can lose its buffer binding
            if (materialValid && buffersValid)
            {
                // Check if material has lost its buffer (this happens on scene unload)
                // We verify by checking if the material's shader is still valid
                if (materialInstance.shader == null)
                    return false;
            }
            
            return buffersValid && materialValid;
        }
        
        /// <summary>
        /// Logs a comprehensive diagnostic report of the renderer state.
        /// Use this to identify why grass might not be rendering.
        /// </summary>
        [ContextMenu("Diagnose Rendering Issues")]
        public void DiagnoseRenderingIssues()
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("=== GrassRenderer Diagnostics ===");
            sb.AppendLine($"GameObject: {gameObject.name}");
            sb.AppendLine($"isInitialized: {isInitialized}");
            sb.AppendLine($"grassData.Count: {grassData?.Count ?? 0}");
            sb.AppendLine($"lastVisibleCount: {lastVisibleCount}");
            sb.AppendLine("--- Settings ---");
            sb.AppendLine($"settings: {(settings != null ? settings.name : "NULL")}");
            if (settings != null)
            {
                sb.AppendLine($"  grassMode: {settings.grassMode}");
                sb.AppendLine($"  maxDrawDistance: {settings.maxDrawDistance}");
                sb.AppendLine($"  minFadeDistance: {settings.minFadeDistance}");
                sb.AppendLine($"  cullingShader: {(settings.cullingShader != null ? "OK" : "NULL")}");
                sb.AppendLine($"  grassMaterial: {(settings.grassMaterial != null ? "OK" : "NULL")}");
                sb.AppendLine($"  customMeshes.Count: {settings.customMeshes?.Count ?? 0}");
                if (!settings.Validate(out string error))
                    sb.AppendLine($"  VALIDATION ERROR: {error}");
                else
                    sb.AppendLine($"  Validation: PASSED");
            }
            sb.AppendLine("--- Buffers ---");
            sb.AppendLine($"sourceBuffer: {(sourceBuffer != null && sourceBuffer.IsValid() ? "VALID" : "INVALID/NULL")}");
            sb.AppendLine($"visibleBuffer: {(visibleBuffer != null && visibleBuffer.IsValid() ? "VALID" : "INVALID/NULL")}");
            sb.AppendLine($"argsBuffer: {(argsBuffer != null && argsBuffer.IsValid() ? "VALID" : "INVALID/NULL")}");
            sb.AppendLine("--- Rendering ---");
            sb.AppendLine($"materialInstance: {(materialInstance != null ? "EXISTS" : "NULL")}");
            sb.AppendLine($"cachedMesh: {(cachedMesh != null ? cachedMesh.name : "NULL")}");
            sb.AppendLine($"renderBounds: {renderBounds}");
            sb.AppendLine("--- Camera ---");
            var cam = GetCurrentCamera();
            sb.AppendLine($"currentCamera: {(cam != null ? cam.name : "NULL")}");
            if (cam != null)
                sb.AppendLine($"  cameraPosition: {cam.transform.position}");
            sb.AppendLine("--- Sample Data ---");
            if (grassData != null && grassData.Count > 0)
            {
                var sample = grassData[0];
                sb.AppendLine($"  First grass position: {sample.position}");
                sb.AppendLine($"  First grass normal: {sample.normal}");
                sb.AppendLine($"  First grass widthHeight: {sample.widthHeight}");
                if (cam != null)
                {
                    float dist = Vector3.Distance(cam.transform.position, sample.position);
                    sb.AppendLine($"  Distance to camera: {dist:F2}m (maxDraw={settings?.maxDrawDistance ?? 0})");
                }
            }
            
            Debug.Log(sb.ToString(), this);
        }
        
        /// <summary>
        /// Full rebuild - cleans up and reinitializes all buffers.
        /// Use this when structural changes occur (mode change, etc).
        /// </summary>
        public void RebuildBuffers()
        {
            Cleanup();
            if (grassData.Count > 0)
                Initialize();
        }
        
        /// <summary>
        /// Smart rebuild - only recreates buffers if size changed significantly.
        /// Much faster for incremental paint operations.
        /// </summary>
        public void SmartRebuildBuffers()
        {
            if (!isInitialized || sourceBuffer == null || !sourceBuffer.IsValid())
            {
                // Not initialized yet, do full rebuild
                RebuildBuffers();
                return;
            }
            
            int currentCount = grassData.Count;
            int bufferSize = sourceBuffer.count;
            
            // If count is 0, cleanup
            if (currentCount == 0)
            {
                Cleanup();
                return;
            }
            
            // If buffer size matches or is within acceptable range, just update data
            // Acceptable range: buffer can hold 50% more or exactly what we need
            bool needsResize = currentCount > bufferSize || currentCount < bufferSize / 2;
            
            if (!needsResize)
            {
                // Fast path: just update buffer data without recreation
                sourceBuffer.SetData(grassData);
                if (cullingShaderInstance != null)
                    cullingShaderInstance.SetInt(PropInstanceCount, currentCount);
                UpdateBounds();
                return;
            }
            
            // Buffer size changed significantly, do full rebuild
            RebuildBuffers();
        }
        
        public void ClearGrass()
        {
            grassData.Clear();
            Cleanup();
        }
        
        // ========================================
        // EXTERNAL DATA PERSISTENCE
        // ========================================
        
        /// <summary>
        /// Returns true if this renderer is using external data storage.
        /// </summary>
        public bool HasExternalData => externalDataAsset != null;
        
        /// <summary>
        /// Clears embedded grass data to reduce scene/prefab file size.
        /// Only call this AFTER ensuring data is saved to external asset.
        /// This is an explicit optimization action for version control.
        /// </summary>
        /// <returns>True if data was cleared, false if no external asset configured.</returns>
        public bool ClearEmbeddedDataForVersionControl()
        {
            if (externalDataAsset == null)
            {
                Debug.LogWarning("GrassRenderer: Cannot clear embedded data without external asset configured.", this);
                return false;
            }
            
            // Ensure data is saved to external asset first
            if (grassData.Count > 0 && externalDataAsset.InstanceCount != grassData.Count)
            {
                SaveToExternalAsset();
            }
            
            int clearedCount = grassData.Count;
            grassData.Clear();
            
            Debug.Log($"GrassRenderer: Cleared {clearedCount:N0} embedded instances. Data is safely stored in {externalDataAsset.name}.", this);
            return true;
        }
        
        /// <summary>
        /// Saves current grass data to the external asset.
        /// Does nothing if no external asset is assigned.
        /// </summary>
        /// <returns>True if save was successful.</returns>
        public bool SaveToExternalAsset()
        {
            if (externalDataAsset == null)
            {
                Debug.LogWarning("GrassRenderer: No external data asset assigned.", this);
                return false;
            }
            
            string sceneName = gameObject.scene.name;
            // Save both data and settings reference for proper restoration
            externalDataAsset.SaveData(grassData, sceneName, settings);
            
            Debug.Log($"GrassRenderer: Saved {grassData.Count:N0} grass instances to {externalDataAsset.name}", this);
            return true;
        }
        
        /// <summary>
        /// Loads grass data from the external asset.
        /// Does nothing if no external asset is assigned.
        /// </summary>
        /// <returns>True if load was successful.</returns>
        public bool LoadFromExternalAsset()
        {
            if (externalDataAsset == null)
            {
                Debug.LogWarning("GrassRenderer: No external data asset assigned.", this);
                return false;
            }
            
            // Prevent OnBeforeSerialize from clearing data during this operation
            isPerformingDataOperation = true;
            try
            {
                // If the asset has associated settings, use them
                if (externalDataAsset.AssociatedSettings != null)
                {
                    settings = externalDataAsset.AssociatedSettings;
                }
                
                // Load data and force full reinitialization
                grassData = externalDataAsset.LoadData();
                
                Cleanup();
                if (grassData.Count > 0)
                    Initialize();
                
                // Force apply settings to ensure colors are correct
                ApplySettingsToMaterial();
                
                Debug.Log($"GrassRenderer: Loaded {grassData.Count:N0} grass instances from {externalDataAsset.name}", this);
                return true;
            }
            finally
            {
                isPerformingDataOperation = false;
            }
        }
        
        /// <summary>
        /// Syncs data between embedded storage and external asset.
        /// If external asset has data and embedded is empty, loads from external.
        /// </summary>
        public void SyncWithExternalAsset()
        {
            if (externalDataAsset == null) return;
            
            // If we have external data but no embedded data, load from external
            if (externalDataAsset.InstanceCount > 0 && grassData.Count == 0)
            {
                LoadFromExternalAsset();
            }
            // If we have embedded data, save to external
            else if (grassData.Count > 0)
            {
                SaveToExternalAsset();
            }
        }
        
        private void OnEnable()
        {
            LogEvent("OnEnable");
            
            // Subscribe to scene events for proper cleanup
            #if UNITY_EDITOR
            UnityEditor.SceneManagement.EditorSceneManager.sceneClosing += OnSceneClosing;
            UnityEditor.SceneManagement.EditorSceneManager.sceneOpened += OnSceneOpened;
            UnityEditor.SceneManagement.EditorSceneManager.sceneSaving += OnSceneSaving;
            UnityEditor.SceneManagement.EditorSceneManager.sceneSaved += OnSceneSaved;
            UnityEditor.AssemblyReloadEvents.beforeAssemblyReload += OnBeforeAssemblyReload;
            #endif
            UnityEngine.SceneManagement.SceneManager.sceneUnloaded += OnSceneUnloaded;
            
            // Initialize based on data source priority:
            // 1. External asset (preferred - keeps scene/prefab light)
            // 2. Embedded data (fallback for prefabs without external asset)
            
            bool hasExternalAsset = externalDataAsset != null;
            int externalCount = hasExternalAsset ? externalDataAsset.InstanceCount : 0;
            int embeddedCount = grassData?.Count ?? 0;
            
            bool hasExternalData = hasExternalAsset && externalCount > 0;
            bool hasEmbeddedData = embeddedCount > 0;
            
            if (hasExternalData)
            {
                // Load from external asset (preferred)
                // This handles both fresh loads AND prefab instantiation where embedded data was cleared
                grassData = externalDataAsset.LoadData();
                LogEvent($"Loaded {grassData.Count:N0} instances from external asset");
                Initialize();
            }
            else if (hasEmbeddedData)
            {
                // Use embedded data
                Initialize();
            }
            else if (needsReinitAfterDeserialize && hasExternalAsset)
            {
                // OnAfterDeserialize marked for reload but asset was empty at that time
                // Try loading again now - asset might be ready
                grassData = externalDataAsset.LoadData();
                if (grassData.Count > 0)
                {
                    LogEvent($"Deferred load: {grassData.Count:N0} instances from external asset");
                    Initialize();
                }
                else
                {
                    // Asset may not be ready yet (e.g., after Git branch switch).
                    // Schedule a delayed retry to allow Unity's asset pipeline to catch up.
                    #if UNITY_EDITOR
                    ScheduleDeferredLoad();
                    #endif
                }
            }
            
            needsReinitAfterDeserialize = false;
            
            // Validate buffer state after initialization
            // If buffers are corrupted (e.g., after crash/domain reload), auto-repair
            if (isInitialized && !AreBuffersValid())
            {
                Debug.LogWarning("GrassRenderer: Detected corrupted buffers after enable. Auto-repairing...", this);
                ForceReinitialize();
            }
            
            // Reset failsafe recovery so it starts immediately on next Update if needed
            recoveryAttemptCount = 0;
            lastRecoveryAttemptTime = -999f;
        }
        
        private void OnDisable()
        {
            // Unsubscribe from events
            #if UNITY_EDITOR
            UnityEditor.SceneManagement.EditorSceneManager.sceneClosing -= OnSceneClosing;
            UnityEditor.SceneManagement.EditorSceneManager.sceneOpened -= OnSceneOpened;
            UnityEditor.SceneManagement.EditorSceneManager.sceneSaving -= OnSceneSaving;
            UnityEditor.SceneManagement.EditorSceneManager.sceneSaved -= OnSceneSaved;
            UnityEditor.AssemblyReloadEvents.beforeAssemblyReload -= OnBeforeAssemblyReload;
            
            // Auto-save: grassData is [NonSerialized] so it would be lost on disable.
            // Persist to external asset to prevent data loss.
            if (grassData != null && grassData.Count > 0 && externalDataAsset != null)
            {
                SaveToExternalAsset();
            }
            #endif
            UnityEngine.SceneManagement.SceneManager.sceneUnloaded -= OnSceneUnloaded;
            
            Cleanup();
        }
        
        private void OnDestroy()
        {
            LogEvent("OnDestroy");
            Cleanup();
        }
        
        private void OnApplicationQuit()
        {
            LogEvent("OnApplicationQuit");
            Cleanup();
        }
        
        #if UNITY_EDITOR
        private void OnSceneClosing(UnityEngine.SceneManagement.Scene scene, bool removingScene)
        {
            if (gameObject.scene == scene)
            {
                LogEvent($"OnSceneClosing ({scene.name})");
                Cleanup();
            }
        }
        
        private void OnSceneOpened(UnityEngine.SceneManagement.Scene scene, UnityEditor.SceneManagement.OpenSceneMode mode)
        {
            // Reinitialize if needed after scene reload
            if (gameObject.scene == scene && grassData.Count > 0 && !isInitialized)
            {
                LogEvent($"OnSceneOpened ({scene.name})");
                Initialize();
            }
        }
        
        private void OnSceneSaving(UnityEngine.SceneManagement.Scene scene, string path)
        {
            // AUTOMATIC OPTIMIZATION: Before scene is saved, clear embedded data
            // if we have a valid external asset with matching data
            if (gameObject.scene == scene && 
                externalDataAsset != null && 
                grassData != null && 
                grassData.Count > 0 &&
                externalDataAsset.InstanceCount == grassData.Count)
            {
                LogEvent($"OnSceneSaving ({scene.name}) - Clearing embedded data to optimize file size");
                
                // Save to external asset first to ensure data is up to date
                SaveToExternalAsset();
                
                // Release GPU resources BEFORE clearing data to prevent
                // rendering with stale buffers during save interval
                Cleanup();
                
                // Clear embedded data - scene file will be small
                grassData.Clear();
                
                // Note: Data will be reloaded from external asset when scene opens (OnSceneSaved)
            }
        }
        
        private void OnBeforeAssemblyReload()
        {
            LogEvent("OnBeforeAssemblyReload (Domain Reload)");
            
            // CRITICAL: grassData is [NonSerialized] for Inspector performance.
            // Domain reload destroys all managed state, so we MUST persist to
            // external asset before the reload happens.
            if (grassData != null && grassData.Count > 0 && externalDataAsset != null)
            {
                SaveToExternalAsset();
                LogEvent($"Saved {grassData.Count:N0} instances to external asset before domain reload");
            }
            
            // Critical: cleanup before domain reload to prevent buffer leaks
            Cleanup();
        }
        
        private void OnSceneSaved(UnityEngine.SceneManagement.Scene scene)
        {
            // After scene save, reload data from external asset if we cleared it during saving
            if (gameObject.scene == scene)
            {
                LogEvent($"OnSceneSaved ({scene.name})");
                
                // If we have an external asset and no data (was cleared during save), reload
                if (externalDataAsset != null && externalDataAsset.InstanceCount > 0 && 
                    (grassData == null || grassData.Count == 0))
                {
                    LogEvent($"Reloading {externalDataAsset.InstanceCount:N0} instances from external asset after save");
                    LoadFromExternalAsset();
                }
                else
                {
                    // Just mark material dirty in case Unity reset properties
                    materialDirty = true;
                }
            }
        }
        
        /// <summary>
        /// Validates that material properties are correctly set and repairs if needed.
        /// Called periodically and after scene operations to catch Unity resets.
        /// Also checks buffer validity, mesh changes, and rebinds if necessary.
        /// </summary>
        private void ValidateAndRepairMaterial()
        {
            if (materialInstance == null || settings == null)
                return;
            
            // Check if buffers are still valid - if not, reinitialize
            if (visibleBuffer == null || !visibleBuffer.IsValid())
            {
                LogEvent("VisibleBuffer invalid. Reinitializing.");
                Initialize();
                return;
            }
            
            // Detect mesh change (e.g., user swapped mesh in SO_GrassSettings inspector)
            // Without this check, changing the mesh after painting shows dots/artifacts
            // because argsBuffer still has the old mesh's index count
            Mesh currentActiveMesh = settings.GetActiveMesh(GetInstanceID());
            if (currentActiveMesh != null && currentActiveMesh != cachedMesh)
            {
                LogEvent($"Mesh changed ({cachedMesh?.name ?? "null"} → {currentActiveMesh.name}). Rebuilding buffers.");
                RebuildBuffers();
                return;
            }
            
            // If material was marked dirty, always reapply and rebind buffer
            if (materialDirty)
            {
                LogEvent("Material marked dirty. Reapplying settings and rebinding buffer.");
                ApplySettingsToMaterial();
                materialInstance.SetBuffer(PropGrassBuffer, visibleBuffer);
                materialDirty = false;
                return;
            }
            
            // Quick check: verify ColorMode is correctly set (if property exists)
            // If it's been reset to 0 when it shouldn't be, reapply all settings
            if (materialInstance.HasProperty("_ColorMode"))
            {
                float currentColorMode = materialInstance.GetFloat("_ColorMode");
                float expectedColorMode = (float)settings.colorMode;
                
                if (Mathf.Abs(currentColorMode - expectedColorMode) > 0.01f)
                {
                    LogEvent($"Material properties mismatch detected (ColorMode: {currentColorMode} vs {expectedColorMode}). Reapplying.");
                    ApplySettingsToMaterial();
                    materialInstance.SetBuffer(PropGrassBuffer, visibleBuffer);
                }
            }
        }
        
        /// <summary>
        /// Schedules a delayed retry for loading grass data from external asset.
        /// Used when the asset database hasn't fully imported after a branch switch.
        /// </summary>
        private void ScheduleDeferredLoad()
        {
            if (deferredLoadRetries >= MAX_DEFERRED_RETRIES)
            {
                Debug.LogWarning($"GrassRenderer: Failed to load grass data after {MAX_DEFERRED_RETRIES} retries. " +
                                 "Use 'Force Reinitialize' or 'Load from Asset' in the Inspector.", this);
                deferredLoadRetries = 0;
                return;
            }

            deferredLoadRetries++;
            LogEvent($"Scheduling deferred load attempt {deferredLoadRetries}/{MAX_DEFERRED_RETRIES}");

            UnityEditor.EditorApplication.delayCall += () =>
            {
                if (this == null || !this.isActiveAndEnabled) return;
                if (externalDataAsset == null) return;

                // Force reimport the asset on first attempt to ensure it's loaded from disk
                if (deferredLoadRetries == 1)
                {
                    string assetPath = UnityEditor.AssetDatabase.GetAssetPath(externalDataAsset);
                    if (!string.IsNullOrEmpty(assetPath))
                    {
                        UnityEditor.AssetDatabase.ImportAsset(assetPath, UnityEditor.ImportAssetOptions.ForceUpdate);
                    }
                }

                grassData = externalDataAsset.LoadData();
                if (grassData.Count > 0)
                {
                    LogEvent($"Deferred load SUCCESS: {grassData.Count:N0} instances (attempt {deferredLoadRetries})");
                    deferredLoadRetries = 0;
                    Initialize();
                }
                else
                {
                    ScheduleDeferredLoad();
                }
            };
        }
        #endif

        
        private void OnSceneUnloaded(UnityEngine.SceneManagement.Scene scene)
        {
            if (gameObject.scene == scene)
            {
                // Our scene is being unloaded - cleanup
                LogEvent($"OnSceneUnloaded ({scene.name})");
                Cleanup();
            }
            else if (isInitialized)
            {
                // Another scene was unloaded - this can corrupt material state
                // Always reapply settings to ensure colors remain correct
                LogEvent($"OnSceneUnloaded ({scene.name}) - Reapplying material settings...");
                
                if (!AreBuffersValid())
                {
                    // Complete reinitialization needed
                    ForceReinitialize();
                }
                else
                {
                    // Just refresh material settings (colors, etc.)
                    ApplySettingsToMaterial();
                }
            }
        }
        
        private void LogEvent(string eventName)
        {
            if (GrassPerformanceOverlay.LogLifecycleEventsEnabled)
                Debug.Log($"[GrassRenderer] {eventName} - Grass: {grassData.Count}", this);
        }
        
        private void Update()
        {
            // ========================================
            // FAILSAFE: Auto-recovery when not initialized
            // This catches ALL failure cases:
            // - Additive scene loading where asset wasn't ready
            // - Domain reload that corrupted buffers
            // - OnSceneSaving cleared embedded data
            // - Any other initialization failure
            // ========================================
            if (!isInitialized)
            {
                TryAutoRecover();
                return;
            }
            
            // Validate buffers are still alive — GPU resources can die after scene operations
            if (sourceBuffer == null || !sourceBuffer.IsValid())
            {
                Debug.LogWarning("GrassRenderer: Buffers became invalid. Auto-recovering...", this);
                isInitialized = false;
                recoveryAttemptCount = 0; // Reset so recovery starts immediately
                lastRecoveryAttemptTime = -999f;
                return;
            }
            
            if (grassData.Count == 0)
                return;
            
            // Periodic check to ensure material properties are valid
            // This catches cases where Unity resets materials after various operations
            #if UNITY_EDITOR
            if (materialDirty || Time.realtimeSinceStartup - lastMaterialCheck > MATERIAL_CHECK_INTERVAL)
            {
                ValidateAndRepairMaterial();
                lastMaterialCheck = Time.realtimeSinceStartup;
            }
            #endif
            
            Camera cam = GetCurrentCamera();
            if (cam == null)
                return;
            
            UpdateCulling(cam);
            Render();
        }
        
        /// <summary>
        /// Failsafe auto-recovery system. Attempts to load and initialize grass data
        /// when the renderer is not initialized. Uses throttling and backoff to avoid
        /// performance impact. NEVER stops trying — grass must always appear.
        /// Works in both Editor and runtime builds.
        /// </summary>
        private void TryAutoRecover()
        {
            // No settings = nothing to recover
            if (settings == null) return;
            
            // Throttle attempts to avoid performance impact
            float interval = recoveryAttemptCount >= RECOVERY_BACKOFF_THRESHOLD 
                ? RECOVERY_BACKOFF_INTERVAL 
                : RECOVERY_INTERVAL;
            
            if (Time.realtimeSinceStartup - lastRecoveryAttemptTime < interval)
                return;
            
            lastRecoveryAttemptTime = Time.realtimeSinceStartup;
            recoveryAttemptCount++;
            
            // === CHECK 1: Try loading from external asset ===
            if (grassData.Count == 0 && externalDataAsset != null)
            {
                var loaded = externalDataAsset.LoadData();
                if (loaded.Count > 0)
                {
                    grassData = loaded;
                    LogEvent($"Failsafe: Loaded {grassData.Count:N0} instances from external asset (attempt {recoveryAttemptCount})");
                }
            }
            
            // === CHECK 2: Try initializing with whatever data we have ===
            if (grassData.Count > 0)
            {
                // Validate settings before attempting initialization
                if (!settings.Validate(out string error))
                {
                    if (recoveryAttemptCount <= 1 || recoveryAttemptCount % 20 == 0)
                    {
                        Debug.LogWarning($"GrassRenderer: Settings invalid during recovery — {error}", this);
                    }
                    return;
                }
                
                Initialize();
                
                if (isInitialized)
                {
                    Debug.Log($"GrassRenderer: ✅ Auto-recovered! {grassData.Count:N0} instances loaded (attempt {recoveryAttemptCount})", this);
                    recoveryAttemptCount = 0;
                    return;
                }
            }
            
            // === CHECK 3: Log warnings at key milestones ===
            if (recoveryAttemptCount == RECOVERY_BACKOFF_THRESHOLD)
            {
                string assetInfo = externalDataAsset != null 
                    ? $"ExternalAsset='{externalDataAsset.name}' (Count={externalDataAsset.InstanceCount})" 
                    : "No external asset";
                Debug.LogWarning($"GrassRenderer: Recovery slowing down after {RECOVERY_BACKOFF_THRESHOLD} attempts. " +
                                 $"EmbeddedData={grassData.Count}, {assetInfo}. Will keep trying.", this);
            }
        }
        
        private void Initialize()
        {
            // ALWAYS cleanup any existing resources before reinitializing to prevent memory leaks
            Cleanup();
            
            if (settings == null)
            {
                Debug.LogWarning("GrassRenderer: No settings assigned!", this);
                return;
            }
            
            if (!settings.Validate(out string error))
            {
                Debug.LogError($"GrassRenderer: Invalid settings - {error}", this);
                return;
            }
            
            if (grassData.Count == 0)
            {
                LogEvent("No grass data to render - skipping initialization.");
                return;
            }
            
            try
            {
                // Create and validate source buffer
                sourceBuffer = new ComputeBuffer(grassData.Count, GrassData.Stride, ComputeBufferType.Structured);
                if (sourceBuffer == null || !sourceBuffer.IsValid())
                {
                    Debug.LogError("GrassRenderer: Failed to create sourceBuffer!", this);
                    return;
                }
                sourceBuffer.SetData(grassData);
                
                // Create and validate visible buffer
                visibleBuffer = new ComputeBuffer(grassData.Count, GrassDrawData.Stride, ComputeBufferType.Append);
                if (visibleBuffer == null || !visibleBuffer.IsValid())
                {
                    Debug.LogError("GrassRenderer: Failed to create visibleBuffer!", this);
                    Cleanup();
                    return;
                }
                
                // Get and cache the active mesh based on mode
                // This ensures consistency between argsBuffer index count and rendered mesh
                cachedMesh = settings.GetActiveMesh(GetInstanceID());
                if (cachedMesh == null)
                {
                    Debug.LogError("GrassRenderer: No valid mesh available!", this);
                    Cleanup();
                    return;
                }
                
                // Create and validate args buffer
                argsBuffer = new GraphicsBuffer(GraphicsBuffer.Target.IndirectArguments, 1, sizeof(uint) * 5);
                if (argsBuffer == null || !argsBuffer.IsValid())
                {
                    Debug.LogError("GrassRenderer: Failed to create argsBuffer!", this);
                    Cleanup();
                    return;
                }
                argsReset[0] = cachedMesh.GetIndexCount(0);
                argsReset[1] = 0;
                argsBuffer.SetData(argsReset);
                
                // Create per-renderer instance of the compute shader to prevent
                // buffer binding collisions between multiple GrassRenderers
                cullingShaderInstance = Object.Instantiate(settings.cullingShader);
                cullingKernel = cullingShaderInstance.FindKernel("CSMain");
                
                cullingShaderInstance.SetBuffer(cullingKernel, PropSourceBuffer, sourceBuffer);
                cullingShaderInstance.SetBuffer(cullingKernel, PropVisibleBuffer, visibleBuffer);
                cullingShaderInstance.SetBuffer(cullingKernel, PropIndirectArgs, argsBuffer);
                cullingShaderInstance.SetInt(PropInstanceCount, grassData.Count);
                cullingShaderInstance.SetFloat(PropMinFade, settings.minFadeDistance);
                cullingShaderInstance.SetFloat(PropMaxDraw, settings.maxDrawDistance);
                cullingShaderInstance.SetFloat(PropWindSpeed, settings.windSpeed);
                cullingShaderInstance.SetFloat(PropWindStrength, settings.windStrength);
                cullingShaderInstance.SetFloat(PropWindFrequency, settings.windFrequency);
                cullingShaderInstance.SetFloat(PropInteractorStrength, settings.interactorStrength);
                
                materialInstance = new Material(settings.grassMaterial);
                materialInstance.SetBuffer(PropGrassBuffer, visibleBuffer);
                
                ApplySettingsToMaterial();
                UpdateBounds();
                
                isInitialized = true;
                LogEvent($"Initialized successfully with {grassData.Count} instances.");
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"GrassRenderer: Initialization failed - {ex.Message}", this);
                Cleanup();
            }
        }
        
        private void ApplySettingsToMaterial()
        {
            if (materialInstance == null) return;
            
            if (settings.albedoTexture != null)
                materialInstance.SetTexture("_MainTex", settings.albedoTexture);
            if (settings.normalMap != null)
                materialInstance.SetTexture("_NormalMap", settings.normalMap);
            if (settings.tipMaskTexture != null)
                materialInstance.SetTexture("_TipMask", settings.tipMaskTexture);
            
            materialInstance.SetColor("_TopTint", settings.topTint);
            materialInstance.SetColor("_BottomTint", settings.bottomTint);
            
            // Color Mode System (Albedo=0, Tint=1, Patterns=2)
            materialInstance.SetFloat("_ColorMode", (float)settings.colorMode);
            
            // Sync shader_feature keywords so the compiler strips unused color mode branches
            materialInstance.DisableKeyword("_COLORMODE_ALBEDO");
            materialInstance.DisableKeyword("_COLORMODE_TINT");
            materialInstance.DisableKeyword("_COLORMODE_PATTERNS");
            switch (settings.colorMode)
            {
                case GrassColorMode.Albedo:
                    materialInstance.EnableKeyword("_COLORMODE_ALBEDO");
                    break;
                case GrassColorMode.Tint:
                    materialInstance.EnableKeyword("_COLORMODE_TINT");
                    break;
                case GrassColorMode.Patterns:
                    materialInstance.EnableKeyword("_COLORMODE_PATTERNS");
                    break;
            }
            
            // Pattern Mode Settings
            materialInstance.SetFloat("_PatternType", (float)settings.patternType);
            materialInstance.SetColor("_PatternATip", settings.patternATip);
            materialInstance.SetColor("_PatternARoot", settings.patternARoot);
            materialInstance.SetColor("_PatternBTip", settings.patternBTip);
            materialInstance.SetColor("_PatternBRoot", settings.patternBRoot);
            
            // Natural Blend Colors (3 colors with tip/root each)
            materialInstance.SetColor("_NaturalColor1Tip", settings.naturalColor1Tip);
            materialInstance.SetColor("_NaturalColor1Root", settings.naturalColor1Root);
            materialInstance.SetColor("_NaturalColor2Tip", settings.naturalColor2Tip);
            materialInstance.SetColor("_NaturalColor2Root", settings.naturalColor2Root);
            materialInstance.SetColor("_NaturalColor3Tip", settings.naturalColor3Tip);
            materialInstance.SetColor("_NaturalColor3Root", settings.naturalColor3Root);
            
            // Pattern Dimensions
            materialInstance.SetFloat("_StripeWidth", settings.stripeWidth);
            materialInstance.SetFloat("_CheckerboardSize", settings.checkerboardSize);
            materialInstance.SetFloat("_StripeAngle", settings.stripeAngle * Mathf.Deg2Rad);
            
            // Natural Blend Settings
            materialInstance.SetFloat("_NaturalBlendType", (float)settings.naturalBlendType);
            materialInstance.SetFloat("_NaturalScale", settings.naturalScale);
            materialInstance.SetFloat("_NaturalSoftness", settings.naturalSoftness);
            materialInstance.SetFloat("_NaturalContrast", settings.naturalContrast);
            
            // Albedo Blend (for Tint and Pattern modes)
            materialInstance.SetFloat("_UseAlbedoBlend", settings.useAlbedoBlend ? 1 : 0);
            materialInstance.SetFloat("_AlbedoBlendAmount", settings.albedoBlendAmount);
            materialInstance.SetFloat("_UseNormalMap", settings.useNormalMap ? 1 : 0);
            
            materialInstance.SetFloat("_UseTipCutout", settings.useTipCutout ? 1 : 0);
            materialInstance.SetFloat("_TipCutoff", settings.tipCutoffHeight);
            
            materialInstance.SetFloat("_WindSpeed", settings.windSpeed);
            materialInstance.SetFloat("_WindStrength", settings.windStrength);
            materialInstance.SetFloat("_WindFrequency", settings.windFrequency);
            materialInstance.SetFloat("_Translucency", settings.translucency);
            materialInstance.SetFloat("_AlignNormals", settings.useAlignedNormals ? 1 : 0);
            
            materialInstance.SetFloat(PropInteractorStrength, settings.interactorStrength);
            materialInstance.SetFloat("_MaxBendAngle", settings.maxBendAngle * Mathf.Deg2Rad);
            
            materialInstance.SetFloat("_UseTerrainLightmap", settings.useTerrainLightmap ? 1 : 0);
            materialInstance.SetFloat("_TerrainLightmapInfluence", settings.terrainLightmapInfluence);
            
            if (settings.useTerrainLightmap && settings.terrain != null)
            {
                TerrainData terrainData = settings.terrain.terrainData;
                Vector3 terrainPos = settings.terrain.transform.position;
                Vector3 terrainSize = terrainData.size;
                
                materialInstance.SetVector("_TerrainPosition", new Vector4(terrainPos.x, terrainPos.y, terrainPos.z, 0));
                materialInstance.SetVector("_TerrainSize", new Vector4(terrainSize.x, terrainSize.y, terrainSize.z, 0));
                
                int lightmapIndex = settings.terrain.lightmapIndex;
                if (lightmapIndex >= 0 && lightmapIndex < LightmapSettings.lightmaps.Length)
                {
                    var lightmapData = LightmapSettings.lightmaps[lightmapIndex];
                    if (lightmapData.lightmapColor != null)
                        materialInstance.SetTexture("_TerrainLightmap", lightmapData.lightmapColor);
                }
            }
            
            // Custom Mesh Mode settings
            bool isCustomMeshMode = settings.grassMode == GrassMode.CustomMesh;
            materialInstance.SetFloat("_UseUniformScale", isCustomMeshMode ? 1 : 0);
            
            if (isCustomMeshMode)
            {
                // Convert degrees to radians for shader
                Vector3 rotationRad = settings.meshRotationOffset * Mathf.Deg2Rad;
                materialInstance.SetVector("_MeshRotation", new Vector4(rotationRad.x, rotationRad.y, rotationRad.z, 0));
            }
            else
            {
                materialInstance.SetVector("_MeshRotation", Vector4.zero);
            }
            
            // Natural variation - convert degrees to radians
            materialInstance.SetFloat("_MaxTiltAngle", settings.maxTiltAngle * Mathf.Deg2Rad);
            materialInstance.SetFloat("_TiltVariation", settings.tiltVariation);
            
            // Light Probes settings (Unlit shader) - only apply if enabled
            if (settings.useLightProbes)
            {
                materialInstance.SetFloat("_LightProbeInfluence", settings.lightProbeInfluence);
                materialInstance.SetFloat("_AmbientBoost", settings.ambientBoost);
                materialInstance.EnableKeyword("_LIGHTPROBES_ON");
            }
            else
            {
                // Disabled: set to zero to prevent any lighting effect
                materialInstance.SetFloat("_LightProbeInfluence", 0f);
                materialInstance.SetFloat("_AmbientBoost", 1f);
                materialInstance.DisableKeyword("_LIGHTPROBES_ON");
            }
            
            // Depth Perception settings (Unlit shader) - only apply if enabled
            if (settings.useDepthPerception)
            {
                materialInstance.SetFloat("_InstanceColorVariation", settings.instanceColorVariation);
                materialInstance.SetFloat("_HeightDarkening", settings.heightDarkening);
                materialInstance.SetFloat("_BackfaceDarkening", settings.backfaceDarkening);
            }
            else
            {
                // Disabled: set all to 0 to prevent any effect
                materialInstance.SetFloat("_InstanceColorVariation", 0f);
                materialInstance.SetFloat("_HeightDarkening", 0f);
                materialInstance.SetFloat("_BackfaceDarkening", 0f);
            }
        }
        
        private void UpdateBounds()
        {
            if (grassData.Count == 0)
            {
                renderBounds = new Bounds(transform.position, Vector3.one * 100);
                return;
            }
            
            renderBounds = new Bounds(grassData[0].position, Vector3.one);
            foreach (var data in grassData)
                renderBounds.Encapsulate(data.position);
            renderBounds.Expand(settings.maxHeight * 2);
        }
        
        private void Cleanup()
        {
            LogEvent("Cleanup - Releasing buffers");
            
            #if UNITY_EDITOR
            deferredLoadRetries = MAX_DEFERRED_RETRIES; // Cancel any pending retries
            #endif
            
            sourceBuffer?.Release();
            visibleBuffer?.Release();
            argsBuffer?.Release();
            interactorBuffer?.Release();
            
            sourceBuffer = null;
            visibleBuffer = null;
            argsBuffer = null;
            interactorBuffer = null;
            cachedMesh = null;
            
            // Destroy per-renderer compute shader instance
            if (cullingShaderInstance != null)
            {
                if (Application.isPlaying)
                    Destroy(cullingShaderInstance);
                else
                    DestroyImmediate(cullingShaderInstance);
                cullingShaderInstance = null;
            }
            
            if (materialInstance != null)
            {
                if (Application.isPlaying)
                    Destroy(materialInstance);
                else
                    DestroyImmediate(materialInstance);
            }
            
            isInitialized = false;
        }
        
        private Camera GetCurrentCamera()
{
    #if UNITY_EDITOR
    if (!Application.isPlaying)
    {
        var sceneView = UnityEditor.SceneView.lastActiveSceneView;
        return sceneView?.camera;
    }
    #endif
    
    // Try Camera.main first (requires "MainCamera" tag)
    if (Camera.main != null)
        return Camera.main;
    
    // Fallback: find any active camera if Main is missing
    return Camera.allCameras.Length > 0 ? Camera.allCameras[0] : null;
}
        
        private void UpdateCulling(Camera cam)
        {
            // Guard: all buffers and shader must be valid
            if (cullingShaderInstance == null || visibleBuffer == null || !visibleBuffer.IsValid() ||
                argsBuffer == null || !argsBuffer.IsValid() || sourceBuffer == null || !sourceBuffer.IsValid())
            {
                isInitialized = false;
                return;
            }
            
            visibleBuffer.SetCounterValue(0);
            argsReset[1] = 0;
            argsBuffer.SetData(argsReset);
            
            Matrix4x4 vp = cam.projectionMatrix * cam.worldToCameraMatrix;
            cullingShaderInstance.SetMatrix(PropViewProjMatrix, vp);
            cullingShaderInstance.SetVector(PropCameraPos, cam.transform.position);
            
            GeometryUtility.CalculateFrustumPlanes(cam, cameraPlanes);
            for (int i = 0; i < 6; i++)
            {
                frustumPlanes[i] = new Vector4(
                    cameraPlanes[i].normal.x,
                    cameraPlanes[i].normal.y,
                    cameraPlanes[i].normal.z,
                    cameraPlanes[i].distance
                );
            }
            cullingShaderInstance.SetVectorArray(PropFrustumPlanes, frustumPlanes);
            
            UpdateInteractors();
            cullingShaderInstance.SetFloat(PropTime, Time.time);
            
            // Each renderer has its own cullingShaderInstance, so buffer bindings
            // are fully isolated — no collisions between multiple GrassRenderers.
            cullingShaderInstance.SetBuffer(cullingKernel, PropSourceBuffer, sourceBuffer);
            cullingShaderInstance.SetBuffer(cullingKernel, PropVisibleBuffer, visibleBuffer);
            cullingShaderInstance.SetBuffer(cullingKernel, PropIndirectArgs, argsBuffer);
            cullingShaderInstance.SetInt(PropInstanceCount, grassData.Count);
            
            int threadGroups = Mathf.CeilToInt((float)grassData.Count / THREAD_GROUP_SIZE);
            cullingShaderInstance.Dispatch(cullingKernel, threadGroups, 1, 1);
            
            // Use async readback to avoid GPU stall (critical for performance)
            // Only do readback in Editor for debugging - skip in builds for max performance
            #if UNITY_EDITOR
            // Cache buffer reference to prevent stale callback access after Cleanup
            var cachedArgsBuffer = argsBuffer;
            if (cachedArgsBuffer != null && cachedArgsBuffer.IsValid())
            {
                AsyncGPUReadback.Request(cachedArgsBuffer, (request) =>
                {
                    // Guard: buffer may have been released between request and callback
                    if (!request.hasError && request.done && cachedArgsBuffer != null && cachedArgsBuffer.IsValid())
                    {
                        var data = request.GetData<uint>();
                        if (data.Length > 1)
                            lastVisibleCount = (int)data[1];
                    }
                });
            }
            #endif
        }
        
        private void UpdateInteractors()
        {
            var interactors = GrassInteractor.ActiveInteractors;
            int count = Mathf.Min(interactors.Count, settings.maxInteractors);
            
            for (int i = 0; i < 16; i++)
            {
                if (i < count)
                    interactorData[i] = interactors[i].GetInteractionData();
                else
                    interactorData[i] = Vector4.zero;
            }
            
            cullingShaderInstance.SetVectorArray(PropInteractors, interactorData);
            cullingShaderInstance.SetInt(PropInteractorCount, count);
            
            materialInstance.SetVectorArray(PropInteractors, interactorData);
            materialInstance.SetInt(PropInteractorCount, count);
        }
        
        private void Render()
        {
            // Use cached mesh to ensure consistency with argsBuffer
            if (cachedMesh == null) return;
            
            var rp = new RenderParams(materialInstance)
            {
                worldBounds = renderBounds,
                shadowCastingMode = settings.castShadows,
                receiveShadows = settings.receiveShadows,
                layer = gameObject.layer
            };
            
            Graphics.RenderMeshIndirect(rp, cachedMesh, argsBuffer);
        }
        
        private void OnDrawGizmosSelected()
        {
            if (settings != null && settings.drawCullingBounds)
            {
                Gizmos.color = new Color(0, 1, 0, 0.3f);
                Gizmos.DrawWireCube(renderBounds.center, renderBounds.size);
            }
        }
        
        // ========================================
        // SERIALIZATION CALLBACKS FOR PREFAB SUPPORT
        // ========================================
        
        // Track if we need to reinitialize after deserialization
        private bool needsReinitAfterDeserialize = false;
        
        // Flag to prevent OnBeforeSerialize from clearing data during active operations
        // (loading, saving, initializing). Without this, Unity calls OnBeforeSerialize
        // frequently and would clear data before Initialize() completes.
        [System.NonSerialized]
        private bool isPerformingDataOperation = false;
        
        /// <summary>
        /// Called before Unity serializes this object.
        /// 
        /// Since grassData is [NonSerialized], Unity never serializes the runtime list.
        /// The legacy field is used ONLY for backward compatibility with scenes saved
        /// before this optimization. For users without external assets, we copy runtime
        /// data to the legacy field so it persists in the scene file.
        /// </summary>
        public void OnBeforeSerialize()
        {
            if (externalDataAsset != null)
            {
                // External asset handles persistence — keep legacy field empty
                // This is the key optimization: Unity serializes an empty list instead of 500k+ items
                _embeddedGrassDataLegacy?.Clear();
            }
            else if (grassData != null && grassData.Count > 0 && !isPerformingDataOperation)
            {
                // No external asset — copy runtime data to legacy field for scene persistence
                // This path is only for small grass counts without external assets
                _embeddedGrassDataLegacy = new List<GrassData>(grassData);
            }
        }
        
        /// <summary>
        /// Called after Unity deserializes this object.
        /// Migrates legacy embedded data to the runtime field and marks for reinitialization.
        /// </summary>
        public void OnAfterDeserialize()
        {
            // Migration: move legacy embedded data to runtime field
            if (_embeddedGrassDataLegacy != null && _embeddedGrassDataLegacy.Count > 0)
            {
                grassData = _embeddedGrassDataLegacy;
                _embeddedGrassDataLegacy = new List<GrassData>(); // Clear legacy to free serialization weight
                needsReinitAfterDeserialize = true;
            }
            // External asset path: grassData is empty (NonSerialized), load from asset in OnEnable
            else if (externalDataAsset != null)
            {
                needsReinitAfterDeserialize = true;
            }
        }
    }
}
