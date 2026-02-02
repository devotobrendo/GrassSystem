// Copyright (c) 2026 Brendo Otavio Carvalho de Matos. All rights reserved.

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace GrassSystem
{
    [ExecuteAlways]
    public class GrassRenderer : MonoBehaviour
    {
        [Header("Settings")]
        public SO_GrassSettings settings;
        
        [Header("Grass Data")]
        [SerializeField, HideInInspector]
        private List<GrassData> grassData = new List<GrassData>();
        
        private ComputeBuffer sourceBuffer;
        private ComputeBuffer visibleBuffer;
        private GraphicsBuffer argsBuffer;
        private ComputeBuffer interactorBuffer;
        
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
        
        private uint[] readbackArgs = new uint[5];
        private int lastVisibleCount;
        
        // Track if material needs reapplication (after scene save, domain reload, etc.)
        private bool materialDirty = false;
        private float lastMaterialCheck = 0f;
        private const float MATERIAL_CHECK_INTERVAL = 0.5f;
        
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
        
        public void RebuildBuffers()
        {
            Cleanup();
            if (grassData.Count > 0)
                Initialize();
        }
        
        public void ClearGrass()
        {
            grassData.Clear();
            Cleanup();
        }
        
        private void OnEnable()
        {
            LogEvent("OnEnable");
            
            // Subscribe to scene events for proper cleanup
            #if UNITY_EDITOR
            UnityEditor.SceneManagement.EditorSceneManager.sceneClosing += OnSceneClosing;
            UnityEditor.SceneManagement.EditorSceneManager.sceneOpened += OnSceneOpened;
            UnityEditor.SceneManagement.EditorSceneManager.sceneSaved += OnSceneSaved;
            UnityEditor.AssemblyReloadEvents.beforeAssemblyReload += OnBeforeAssemblyReload;
            #endif
            UnityEngine.SceneManagement.SceneManager.sceneUnloaded += OnSceneUnloaded;
            
            if (grassData.Count > 0)
                Initialize();
        }
        
        private void OnDisable()
        {
            // Unsubscribe from events
            #if UNITY_EDITOR
            UnityEditor.SceneManagement.EditorSceneManager.sceneClosing -= OnSceneClosing;
            UnityEditor.SceneManagement.EditorSceneManager.sceneOpened -= OnSceneOpened;
            UnityEditor.SceneManagement.EditorSceneManager.sceneSaved -= OnSceneSaved;
            UnityEditor.AssemblyReloadEvents.beforeAssemblyReload -= OnBeforeAssemblyReload;
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
        
        private void OnBeforeAssemblyReload()
        {
            LogEvent("OnBeforeAssemblyReload (Domain Reload)");
            // Critical: cleanup before domain reload to prevent buffer leaks
            Cleanup();
        }
        
        private void OnSceneSaved(UnityEngine.SceneManagement.Scene scene)
        {
            // After scene save, Unity may reset material properties
            if (gameObject.scene == scene)
            {
                LogEvent($"OnSceneSaved ({scene.name}) - Marking material dirty");
                materialDirty = true;
            }
        }
        
        /// <summary>
        /// Validates that material properties are correctly set and repairs if needed.
        /// Called periodically and after scene operations to catch Unity resets.
        /// </summary>
        private void ValidateAndRepairMaterial()
        {
            if (materialInstance == null || settings == null)
                return;
            
            // If material was marked dirty, always reapply
            if (materialDirty)
            {
                LogEvent("Material marked dirty. Reapplying settings.");
                ApplySettingsToMaterial();
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
                }
            }
        }
        #endif

        
        private void OnSceneUnloaded(UnityEngine.SceneManagement.Scene scene)
        {
            if (gameObject.scene == scene)
            {
                LogEvent($"OnSceneUnloaded ({scene.name})");
                Cleanup();
            }
        }
        
        private void LogEvent(string eventName)
        {
            if (GrassPerformanceOverlay.LogLifecycleEventsEnabled)
                Debug.Log($"[GrassRenderer] {eventName} - Grass: {grassData.Count}", this);
        }
        
        private void Update()
        {
            if (!isInitialized || grassData.Count == 0 || sourceBuffer == null)
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
                return;
            
            sourceBuffer = new ComputeBuffer(grassData.Count, GrassData.Stride, ComputeBufferType.Structured);
            sourceBuffer.SetData(grassData);
            
            visibleBuffer = new ComputeBuffer(grassData.Count, GrassDrawData.Stride, ComputeBufferType.Append);
            
            // Get and cache the active mesh based on mode
            // This ensures consistency between argsBuffer index count and rendered mesh
            cachedMesh = settings.GetActiveMesh(GetInstanceID());
            if (cachedMesh == null)
            {
                Debug.LogError("GrassRenderer: No valid mesh available!", this);
                return;
            }
            
            argsBuffer = new GraphicsBuffer(GraphicsBuffer.Target.IndirectArguments, 1, sizeof(uint) * 5);
            argsReset[0] = cachedMesh.GetIndexCount(0);
            argsReset[1] = 0;
            argsBuffer.SetData(argsReset);
            
            cullingKernel = settings.cullingShader.FindKernel("CSMain");
            
            settings.cullingShader.SetBuffer(cullingKernel, PropSourceBuffer, sourceBuffer);
            settings.cullingShader.SetBuffer(cullingKernel, PropVisibleBuffer, visibleBuffer);
            settings.cullingShader.SetBuffer(cullingKernel, PropIndirectArgs, argsBuffer);
            settings.cullingShader.SetInt(PropInstanceCount, grassData.Count);
            settings.cullingShader.SetFloat(PropMinFade, settings.minFadeDistance);
            settings.cullingShader.SetFloat(PropMaxDraw, settings.maxDrawDistance);
            settings.cullingShader.SetFloat(PropWindSpeed, settings.windSpeed);
            settings.cullingShader.SetFloat(PropWindStrength, settings.windStrength);
            settings.cullingShader.SetFloat(PropWindFrequency, settings.windFrequency);
            settings.cullingShader.SetFloat(PropInteractorStrength, settings.interactorStrength);
            
            materialInstance = new Material(settings.grassMaterial);
            materialInstance.SetBuffer(PropGrassBuffer, visibleBuffer);
            
            ApplySettingsToMaterial();
            UpdateBounds();
            
            isInitialized = true;
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
            
            sourceBuffer?.Release();
            visibleBuffer?.Release();
            argsBuffer?.Release();
            interactorBuffer?.Release();
            
            sourceBuffer = null;
            visibleBuffer = null;
            argsBuffer = null;
            interactorBuffer = null;
            
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
            return Camera.main;
        }
        
        private void UpdateCulling(Camera cam)
        {
            visibleBuffer.SetCounterValue(0);
            argsReset[1] = 0;
            argsBuffer.SetData(argsReset);
            
            Matrix4x4 vp = cam.projectionMatrix * cam.worldToCameraMatrix;
            settings.cullingShader.SetMatrix(PropViewProjMatrix, vp);
            settings.cullingShader.SetVector(PropCameraPos, cam.transform.position);
            
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
            settings.cullingShader.SetVectorArray(PropFrustumPlanes, frustumPlanes);
            
            UpdateInteractors();
            settings.cullingShader.SetFloat(PropTime, Time.time);
            
            // CRITICAL: Rebind buffers every frame to support multiple GrassRenderers
            // When multiple renderers share the same ComputeShader asset, buffer bindings
            // from one renderer would overwrite the other. Each renderer must bind its own buffers.
            settings.cullingShader.SetBuffer(cullingKernel, PropSourceBuffer, sourceBuffer);
            settings.cullingShader.SetBuffer(cullingKernel, PropVisibleBuffer, visibleBuffer);
            settings.cullingShader.SetBuffer(cullingKernel, PropIndirectArgs, argsBuffer);
            settings.cullingShader.SetInt(PropInstanceCount, grassData.Count);
            
            int threadGroups = Mathf.CeilToInt((float)grassData.Count / THREAD_GROUP_SIZE);
            settings.cullingShader.Dispatch(cullingKernel, threadGroups, 1, 1);
            
            // Use async readback to avoid GPU stall (critical for performance)
            // Only do readback in Editor for debugging - skip in builds for max performance
            #if UNITY_EDITOR
            if (argsBuffer != null && argsBuffer.IsValid())
            {
                AsyncGPUReadback.Request(argsBuffer, (request) =>
                {
                    if (!request.hasError && request.done)
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
            
            settings.cullingShader.SetVectorArray(PropInteractors, interactorData);
            settings.cullingShader.SetInt(PropInteractorCount, count);
            
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
    }
}
