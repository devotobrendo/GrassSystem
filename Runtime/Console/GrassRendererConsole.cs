// Copyright (c) 2026 Brendo Otavio Carvalho de Matos. All rights reserved.

using UnityEngine;
using UnityEngine.Rendering;
using GrassSystem;

namespace GrassSystem.Consoles
{
    [ExecuteAlways]
    public class GrassRendererConsole : MonoBehaviour
    {
        [Header("Settings")]
        public SO_GrassSettings settings;

        [Header("Platform Variant")]
        public PlatformVariant variantMode = PlatformVariant.Auto;
        public GrassPlatformProfileSet profileSet;

        [Header("Slim Data Source")]
        [Tooltip("Baked slim grass data asset (position + widthHeight only). Generate via Tools/Grass System/Convert to Console (Slim).")]
        public GrassDataConsoleAsset dataAsset;

        [Header("Baked Decals")]
        [Tooltip("Optional baked decal asset applied automatically whenever this renderer rebuilds its material.")]
        [SerializeField] private GrassDecalBakeAsset bakedDecalAsset;

        [Header("Console Optimization")]
        [Range(0f, 1f)]
        [Tooltip("Fraction of far blades kept by GPU thinning. 1 = off (no thinning). Lower thins distant grass, removing whole instances (fewer verts + fill). Switch target ~0.5.")]
        public float farKeepFraction = 1f;
        [Tooltip("Distance (m) beyond which grass thins toward Far Keep Fraction over a ~5m blend. Nearer than this, all blades are kept.")]
        public float thinStartDistance = 10f;
        [Tooltip("Runtime blade size multiplier. Default mode: x = width, y = height (independent). Custom Mesh mode: x = uniform scale, y ignored (mesh keeps its modeled proportions). 1,1 = original.")]
        public Vector2 sizeScale = Vector2.one;
        [Range(0f, 1f)]
        [Tooltip("How much thinned-away blades widen the survivors to keep ground coverage. 1 = full (far grass widens as you thin). 0 = off (grass just gets sparser, no widening).")]
        public float coverageCompensation = 1f;
        [Range(0.01f, 1f)]
        [Tooltip("Fraction of the baked instances actually uploaded to the GPU. Unlike Far Keep Fraction, which drops blades inside the compute shader, this shrinks the source buffer and the dispatch itself — use it to preview what a decimated bake would cost in memory and culling. Rebuilds buffers when changed.")]
        public float instanceDensity = 1f;

        [System.NonSerialized]
        private GrassDataConsole[] grassData = System.Array.Empty<GrassDataConsole>();

        private ComputeBuffer sourceBuffer;
        private ComputeBuffer visibleBuffer;
        private GraphicsBuffer argsBuffer;

        private ComputeShader cullingShaderInstance;
        private int cullingKernel;
        private const int THREAD_GROUP_SIZE = 128;

        private Bounds renderBounds;
        private readonly uint[] argsReset = new uint[5] { 0, 0, 0, 0, 0 };
        private Vector4[] interactorData = new Vector4[16];
        private Material materialInstance;
        private Mesh cachedMesh;
        private RenderTexture runtimeDecalMap;
        private int activeInstanceCount;
        private int readbackCounter;
        private const int READBACK_INTERVAL = 8;
        private float lastAppliedDensity = 1f;
        private float lastSeenDensity = 1f;
        private float densityChangeTime = -1f;
        private const float DENSITY_DEBOUNCE = 0.25f;
        private Texture pendingDecalOverlay;
        private Vector4 pendingDecalOverlayBounds;

        private static readonly int PropSourceBuffer = Shader.PropertyToID("_SourceBuffer");
        private static readonly int PropVisibleBuffer = Shader.PropertyToID("_VisibleBuffer");
        private static readonly int PropGrassBuffer = Shader.PropertyToID("_GrassBuffer");
        private static readonly int PropViewProjMatrix = Shader.PropertyToID("_ViewProjectionMatrix");
        private static readonly int PropCameraPos = Shader.PropertyToID("_CameraPosition");
        private static readonly int PropFrustumPlanes = Shader.PropertyToID("_FrustumPlanes");
        private static readonly int PropMinFade = Shader.PropertyToID("_MinFadeDistance");
        private static readonly int PropMaxDraw = Shader.PropertyToID("_MaxDrawDistance");
        private static readonly int PropFarKeep = Shader.PropertyToID("_FarKeepFraction");
        private static readonly int PropThinStart = Shader.PropertyToID("_ThinStartDistance");
        private static readonly int PropSizeScale = Shader.PropertyToID("_SizeScale");
        private static readonly int PropCoverageComp = Shader.PropertyToID("_CoverageCompensation");
        private static readonly int PropUseUniformScale = Shader.PropertyToID("_UseUniformScale");
        private static readonly int PropInstanceCount = Shader.PropertyToID("_InstanceCount");
        private static readonly int PropInteractors = Shader.PropertyToID("_Interactors");
        private static readonly int PropInteractorCount = Shader.PropertyToID("_InteractorCount");
        private static readonly int PropInteractorStrength = Shader.PropertyToID("_InteractorStrength");

        private Vector4[] frustumPlanes = new Vector4[6];
        private Plane[] cameraPlanes = new Plane[6];
        private bool isInitialized;
        private GrassMode lastAppliedMode;
        private bool lastAppliedFlatAlbedo;

        private int lastVisibleCount;
        private bool materialDirty;
        private float lastMaterialCheck;
        private const float MATERIAL_CHECK_INTERVAL = 0.5f;

        private float lastRecoveryAttemptTime = -999f;
        private int recoveryAttemptCount;
        private const float RECOVERY_INTERVAL = 0.5f;
        private const float RECOVERY_BACKOFF_INTERVAL = 3.0f;
        private const int RECOVERY_BACKOFF_THRESHOLD = 10;

        public GrassDataConsole[] GrassDataArray
        {
            get => grassData;
            set
            {
                grassData = value ?? System.Array.Empty<GrassDataConsole>();
                if (isInitialized) RebuildBuffers();
            }
        }

        public int TotalGrassCount => activeInstanceCount > 0 ? activeInstanceCount : grassData.Length;
        public int BakedInstanceCount => grassData.Length;
        public int VisibleGrassCount => lastVisibleCount;
        public GrassDecalBakeAsset BakedDecalAsset => bakedDecalAsset;
        public Material MaterialInstance => materialInstance;
        public bool IsInitialized => isInitialized;
        private GrassPlatformProfile ActiveProfile => profileSet != null ? profileSet.Resolve(variantMode) : null;

        private GrassMode EffectiveGrassMode
        {
            get
            {
                if (GrassConsoleDebug.ModeOverrideEnabled) return GrassConsoleDebug.ModeOverride;
                var p = ActiveProfile;
                if (p != null && p.overrideMesh) return p.meshMode;
                return settings != null ? settings.grassMode : GrassMode.Default;
            }
        }

        private float EffectiveInstanceDensity =>
            GrassConsoleDebug.InstanceDensityOverrideEnabled ? GrassConsoleDebug.InstanceDensity : instanceDensity;

        private GrassDataConsole[] BuildUploadData()
        {
            float keep = Mathf.Clamp01(EffectiveInstanceDensity);
            if (keep >= 1f || grassData.Length == 0)
                return grassData;

            var kept = new System.Collections.Generic.List<GrassDataConsole>(Mathf.Max(16, Mathf.CeilToInt(grassData.Length * keep)));
            for (int i = 0; i < grassData.Length; i++)
            {
                uint h = (uint)i;
                h = (h ^ 61u) ^ (h >> 16);
                h *= 9u;
                h ^= h >> 4;
                h *= 0x27d4eb2du;
                h ^= h >> 15;
                if (h * (1f / 4294967295f) <= keep)
                    kept.Add(grassData[i]);
            }

            return kept.Count > 0 ? kept.ToArray() : grassData;
        }

        private Mesh ResolveActiveMesh()
        {
            if (settings == null) return null;
            GrassMode mode = EffectiveGrassMode;
            var p = ActiveProfile;
            if (!GrassConsoleDebug.ModeOverrideEnabled && p != null && p.overrideMesh &&
                mode == GrassMode.CustomMesh && p.meshes != null && p.meshes.Length > 0)
            {
                int idx = Mathf.Abs(GetInstanceID()) % p.meshes.Length;
                if (p.meshes[idx] != null) return p.meshes[idx];
            }
            if (mode == GrassMode.Default)
            {
                GrassProceduralType type;
                if (GrassConsoleDebug.BladeTypeOverrideEnabled) type = GrassConsoleDebug.BladeTypeOverride;
                else if (p != null && p.overrideMesh) type = p.proceduralType;
                else type = settings.proceduralType;
                return GrassMeshUtility.GetProceduralMesh(type);
            }
            return settings.GetActiveMesh(GetInstanceID(), mode);
        }

        [ContextMenu("Force Reinitialize")]
        public void ForceReinitialize()
        {
            Cleanup();

            if (grassData.Length == 0 && dataAsset != null && dataAsset.InstanceCount > 0)
            {
                Debug.Log($"GrassRendererConsole: Recovering {dataAsset.InstanceCount:N0} instances from data asset.", this);
                grassData = dataAsset.LoadData();
            }

            if (grassData.Length > 0)
                Initialize();

            Debug.Log($"GrassRendererConsole: Reinitialized. instances={grassData.Length}, isInitialized={isInitialized}", this);
        }

        public void MarkMaterialDirty()
        {
            materialDirty = true;
        }

        public bool AreBuffersValid()
        {
            if (!isInitialized) return false;

            bool buffersValid = sourceBuffer != null && sourceBuffer.IsValid() &&
                         visibleBuffer != null && visibleBuffer.IsValid() &&
                         argsBuffer != null && argsBuffer.IsValid();

            bool materialValid = materialInstance != null && cachedMesh != null && settings != null;

            if (materialValid && buffersValid && materialInstance.shader == null)
                return false;

            return buffersValid && materialValid;
        }

        public void RebuildBuffers()
        {
            Cleanup();
            if (grassData.Length > 0)
                Initialize();
        }

        public void ClearGrass()
        {
            grassData = System.Array.Empty<GrassDataConsole>();
            Cleanup();
        }

        public bool LoadFromDataAsset()
        {
            if (dataAsset == null)
            {
                Debug.LogWarning("GrassRendererConsole: No data asset assigned.", this);
                return false;
            }

            grassData = dataAsset.LoadData();
            Cleanup();
            if (grassData.Length > 0)
                Initialize();

            Debug.Log($"GrassRendererConsole: Loaded {grassData.Length:N0} slim instances from {dataAsset.name}", this);
            return true;
        }

        private void OnEnable()
        {
            UnityEngine.SceneManagement.SceneManager.sceneUnloaded += OnSceneUnloaded;
#if UNITY_EDITOR
            UnityEditor.AssemblyReloadEvents.beforeAssemblyReload += OnBeforeAssemblyReload;
#endif

            if (!GrassConsoleDebug.ActiveRenderers.Contains(this))
                GrassConsoleDebug.ActiveRenderers.Add(this);

            if (dataAsset != null && dataAsset.InstanceCount > 0)
            {
                grassData = dataAsset.LoadData();
                Initialize();
            }
            else if (grassData.Length > 0)
            {
                Initialize();
            }

            if (isInitialized && !AreBuffersValid())
            {
                Debug.LogWarning("GrassRendererConsole: Detected corrupted buffers after enable. Auto-repairing...", this);
                ForceReinitialize();
            }

            recoveryAttemptCount = 0;
            lastRecoveryAttemptTime = -999f;
        }

        private void OnDisable()
        {
            UnityEngine.SceneManagement.SceneManager.sceneUnloaded -= OnSceneUnloaded;
#if UNITY_EDITOR
            UnityEditor.AssemblyReloadEvents.beforeAssemblyReload -= OnBeforeAssemblyReload;
#endif
            GrassConsoleDebug.ActiveRenderers.Remove(this);
            Cleanup();
        }

        private void OnDestroy() => Cleanup();

        private void OnApplicationQuit() => Cleanup();

#if UNITY_EDITOR
        private void OnBeforeAssemblyReload()
        {
            Cleanup();
        }

        private void OnValidate()
        {
            farKeepFraction = Mathf.Clamp01(farKeepFraction);
            thinStartDistance = Mathf.Max(0f, thinStartDistance);
            if (!Application.isPlaying && isInitialized)
            {
                UnityEditor.EditorApplication.QueuePlayerLoopUpdate();
                UnityEditor.SceneView.RepaintAll();
            }
        }
#endif

        private void OnSceneUnloaded(UnityEngine.SceneManagement.Scene scene)
        {
            if (gameObject.scene == scene)
            {
                Cleanup();
            }
            else if (isInitialized)
            {
                if (!AreBuffersValid())
                    ForceReinitialize();
                else
                    ApplySettingsToMaterial();
            }
        }

        private bool ConsumeDensityChange()
        {
            float density = EffectiveInstanceDensity;

            if (!Mathf.Approximately(density, lastSeenDensity))
            {
                lastSeenDensity = density;
                densityChangeTime = Time.realtimeSinceStartup;
                return false;
            }

            if (densityChangeTime < 0f || Mathf.Approximately(density, lastAppliedDensity))
                return false;

            if (Time.realtimeSinceStartup - densityChangeTime < DENSITY_DEBOUNCE)
                return false;

            densityChangeTime = -1f;
            return true;
        }

        private void Update()
        {
            if (!isInitialized)
            {
                TryAutoRecover();
                return;
            }

            if (ConsumeDensityChange())
            {
                RebuildBuffers();
                return;
            }

            if (sourceBuffer == null || !sourceBuffer.IsValid())
            {
                Debug.LogWarning("GrassRendererConsole: Buffers became invalid. Auto-recovering...", this);
                isInitialized = false;
                recoveryAttemptCount = 0;
                lastRecoveryAttemptTime = -999f;
                return;
            }

            if (grassData.Length == 0)
                return;

#if UNITY_EDITOR
            if (materialDirty || Time.realtimeSinceStartup - lastMaterialCheck > MATERIAL_CHECK_INTERVAL)
            {
                ValidateAndRepairMaterial();
                lastMaterialCheck = Time.realtimeSinceStartup;
            }
#endif

            SyncEffectiveMode();

            Camera cam = GetCurrentCamera();
            if (cam == null)
                return;

            UpdateCulling(cam);
            Render();
        }

        private void SyncEffectiveMode()
        {
            if (!isInitialized || settings == null) return;
            GrassMode mode = EffectiveGrassMode;
            Mesh newMesh = ResolveActiveMesh();
            bool modeChanged = mode != lastAppliedMode;
            bool meshChanged = newMesh != null && newMesh != cachedMesh;
            bool albedoChanged = GrassConsoleDebug.FlatAlbedoEnabled != lastAppliedFlatAlbedo;
            if (!modeChanged && !meshChanged && !albedoChanged) return;

            if (meshChanged && argsBuffer != null && argsBuffer.IsValid())
            {
                cachedMesh = newMesh;
                argsReset[0] = cachedMesh.GetIndexCount(0);
                argsReset[1] = 0;
                argsBuffer.SetData(argsReset);
            }
            ApplySettingsToMaterial();
            lastAppliedMode = mode;
            lastAppliedFlatAlbedo = GrassConsoleDebug.FlatAlbedoEnabled;
        }

        private void TryAutoRecover()
        {
            if (settings == null) return;

            float interval = recoveryAttemptCount >= RECOVERY_BACKOFF_THRESHOLD
                ? RECOVERY_BACKOFF_INTERVAL
                : RECOVERY_INTERVAL;

            if (Time.realtimeSinceStartup - lastRecoveryAttemptTime < interval)
                return;

            lastRecoveryAttemptTime = Time.realtimeSinceStartup;
            recoveryAttemptCount++;

            if (grassData.Length == 0 && dataAsset != null)
            {
                var loaded = dataAsset.LoadData();
                if (loaded.Length > 0)
                    grassData = loaded;
            }

            if (grassData.Length > 0)
            {
                if (!settings.Validate(out string error))
                {
                    if (recoveryAttemptCount <= 1 || recoveryAttemptCount % 20 == 0)
                        Debug.LogWarning($"GrassRendererConsole: Settings invalid during recovery — {error}", this);
                    return;
                }

                Initialize();

                if (isInitialized)
                {
                    Debug.Log($"GrassRendererConsole: Auto-recovered. {grassData.Length:N0} instances loaded (attempt {recoveryAttemptCount})", this);
                    recoveryAttemptCount = 0;
                }
            }
        }

        private void Initialize()
        {
            Cleanup();

            if (settings == null)
            {
                Debug.LogWarning("GrassRendererConsole: No settings assigned!", this);
                return;
            }

            if (settings.cullingShader == null || settings.grassMaterial == null)
            {
                Debug.LogError("GrassRendererConsole: settings.cullingShader or settings.grassMaterial is null. GrassRendererConsole requires a console-specific SO_GrassSettings — cullingShader must reference GrassCullingSlim.compute and grassMaterial must use a Material built on the GrassUnlitConsole shader. Do not reuse a settings asset authored for the original GrassRenderer (GrassCulling.compute / GrassUnlit) without repointing these two fields.", this);
                return;
            }

            if (!settings.Validate(out string error))
            {
                Debug.LogError($"GrassRendererConsole: Invalid settings - {error}", this);
                return;
            }

            if (grassData.Length == 0)
                return;

            try
            {
                GrassDataConsole[] uploadData = BuildUploadData();
                activeInstanceCount = uploadData.Length;
                lastAppliedDensity = EffectiveInstanceDensity;
                lastSeenDensity = lastAppliedDensity;
                densityChangeTime = -1f;

                sourceBuffer = new ComputeBuffer(activeInstanceCount, GrassDataConsole.Stride, ComputeBufferType.Structured);
                if (sourceBuffer == null || !sourceBuffer.IsValid())
                {
                    Debug.LogError("GrassRendererConsole: Failed to create sourceBuffer!", this);
                    return;
                }
                sourceBuffer.SetData(uploadData);

                visibleBuffer = new ComputeBuffer(activeInstanceCount, GrassDrawData.Stride, ComputeBufferType.Append);
                if (visibleBuffer == null || !visibleBuffer.IsValid())
                {
                    Debug.LogError("GrassRendererConsole: Failed to create visibleBuffer!", this);
                    Cleanup();
                    return;
                }

                cachedMesh = ResolveActiveMesh();
                if (cachedMesh == null)
                {
                    Debug.LogError("GrassRendererConsole: No valid mesh available!", this);
                    Cleanup();
                    return;
                }

                argsBuffer = new GraphicsBuffer(GraphicsBuffer.Target.IndirectArguments, 1, sizeof(uint) * 5);
                if (argsBuffer == null || !argsBuffer.IsValid())
                {
                    Debug.LogError("GrassRendererConsole: Failed to create argsBuffer!", this);
                    Cleanup();
                    return;
                }
                argsReset[0] = cachedMesh.GetIndexCount(0);
                argsReset[1] = 0;
                argsBuffer.SetData(argsReset);

                cullingShaderInstance = Object.Instantiate(settings.cullingShader);
                cullingKernel = cullingShaderInstance.FindKernel("CSMain");

                cullingShaderInstance.SetBuffer(cullingKernel, PropSourceBuffer, sourceBuffer);
                cullingShaderInstance.SetBuffer(cullingKernel, PropVisibleBuffer, visibleBuffer);
                cullingShaderInstance.SetInt(PropInstanceCount, activeInstanceCount);

                materialInstance = new Material(settings.grassMaterial);
                materialInstance.SetBuffer(PropGrassBuffer, visibleBuffer);

                ApplySettingsToMaterial();
                UpdateBounds();

                lastAppliedMode = EffectiveGrassMode;
                isInitialized = true;
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"GrassRendererConsole: Initialization failed - {ex.Message}", this);
                Cleanup();
            }
        }

        private void ApplySettingsToMaterial()
        {
            if (materialInstance == null) return;

            if (GrassConsoleDebug.FlatAlbedoEnabled)
                materialInstance.SetTexture("_MainTex", Texture2D.linearGrayTexture);
            else if (EffectiveGrassMode == GrassMode.Default)
                materialInstance.SetTexture("_MainTex", settings.defaultModeAlbedo != null ? settings.defaultModeAlbedo : Texture2D.linearGrayTexture);
            else if (settings.albedoTexture != null)
                materialInstance.SetTexture("_MainTex", settings.albedoTexture);
            if (settings.tipMaskTexture != null)
                materialInstance.SetTexture("_TipMask", settings.tipMaskTexture);

            if (settings.useTipCutout)
                materialInstance.EnableKeyword("_TIPCUTOUT_ON");
            else
                materialInstance.DisableKeyword("_TIPCUTOUT_ON");
            materialInstance.SetFloat("_TipCutoff", settings.tipCutoffHeight);

            materialInstance.SetFloat("_WindSpeed", settings.windSpeed);
            materialInstance.SetFloat("_WindStrength", settings.windStrength);
            materialInstance.SetFloat("_WindFrequency", settings.windFrequency);

            materialInstance.SetFloat(PropInteractorStrength, settings.interactorStrength);
            materialInstance.SetFloat("_MaxBendAngle", settings.maxBendAngle * Mathf.Deg2Rad);

            var p = ActiveProfile;
            bool effReceiveShadows = (p != null && p.overrideReceiveShadows) ? p.receiveShadows : settings.useReceiveShadows;
            if (effReceiveShadows)
            {
                materialInstance.SetFloat("_ShadowIntensity", settings.shadowIntensity);
                materialInstance.EnableKeyword("_RECEIVE_SHADOWS_ON");
            }
            else
            {
                materialInstance.SetFloat("_ShadowIntensity", 0f);
                materialInstance.DisableKeyword("_RECEIVE_SHADOWS_ON");
            }

            if (settings.useDepthPerception)
            {
                materialInstance.SetFloat("_InstanceColorVariation", settings.instanceColorVariation);
                materialInstance.SetFloat("_HeightDarkening", settings.heightDarkening);
                materialInstance.SetFloat("_BackfaceDarkening", settings.backfaceDarkening);
            }
            else
            {
                materialInstance.SetFloat("_InstanceColorVariation", 0f);
                materialInstance.SetFloat("_HeightDarkening", 0f);
                materialInstance.SetFloat("_BackfaceDarkening", 0f);
            }

            bool isCustomMeshMode = EffectiveGrassMode == GrassMode.CustomMesh;
            materialInstance.SetFloat("_UseUniformScale", isCustomMeshMode ? 1 : 0);

            if (isCustomMeshMode)
            {
                Vector3 rotationRad = settings.meshRotationOffset * Mathf.Deg2Rad;
                materialInstance.SetVector("_MeshRotation", new Vector4(rotationRad.x, rotationRad.y, rotationRad.z, 0));
            }
            else
            {
                materialInstance.SetVector("_MeshRotation", Vector4.zero);
            }

            materialInstance.SetFloat("_MaxTiltAngle", settings.maxTiltAngle * Mathf.Deg2Rad);
            materialInstance.SetFloat("_TiltVariation", settings.tiltVariation);

            if (settings.useLightProbes)
            {
                materialInstance.SetFloat("_LightProbeInfluence", settings.lightProbeInfluence);
                materialInstance.SetFloat("_AmbientBoost", settings.ambientBoost);
                materialInstance.EnableKeyword("_LIGHTPROBES_ON");
            }
            else
            {
                materialInstance.SetFloat("_LightProbeInfluence", 0f);
                materialInstance.SetFloat("_AmbientBoost", 1f);
                materialInstance.DisableKeyword("_LIGHTPROBES_ON");
            }

            ApplyBakedDecalToMaterial();
        }

        public void SetBakedDecalAsset(GrassDecalBakeAsset bakeAsset)
        {
            bakedDecalAsset = bakeAsset;
            ApplyBakedDecalToMaterial();
        }

        public void ClearBakedDecalAsset()
        {
            bakedDecalAsset = null;
            ApplyBakedDecalToMaterial();
        }

        public void SetRuntimeDecalOverlay(Texture overlay, Vector4 overlayBoundsXZ)
        {
            pendingDecalOverlay = overlay;
            pendingDecalOverlayBounds = overlayBoundsXZ;

            if (materialInstance == null || bakedDecalAsset == null)
            {
                Debug.LogWarning("GrassRendererConsole: runtime decal overlay stored but not applied yet — the renderer needs an initialized material and a bakedDecalAsset. It will be applied automatically once both exist.", this);
                return;
            }

            ApplyRuntimeDecalOverlay();
        }

        public void ClearRuntimeDecalOverlay()
        {
            pendingDecalOverlay = null;
            GrassRuntimeDecalCompositor.Release(ref runtimeDecalMap);
            ApplyBakedDecalToMaterial();
        }

        private void ApplyRuntimeDecalOverlay()
        {
            if (pendingDecalOverlay == null || materialInstance == null || bakedDecalAsset == null)
                return;

            if (GrassRuntimeDecalCompositor.Composite(bakedDecalAsset.overrideMap, bakedDecalAsset.bounds, pendingDecalOverlay, pendingDecalOverlayBounds, ref runtimeDecalMap))
                materialInstance.SetTexture("_BakedOverrideMap", runtimeDecalMap);
        }

        private void ApplyBakedDecalToMaterial()
        {
            if (materialInstance == null)
                return;

            if (bakedDecalAsset != null)
            {
                materialInstance.SetTexture("_BakedOverrideMap", bakedDecalAsset.overrideMap);
                materialInstance.SetTexture("_BakedMultiplyMap", bakedDecalAsset.multiplyMap);
                materialInstance.SetTexture("_BakedAdditiveMap", bakedDecalAsset.additiveMap);
                materialInstance.SetVector("_BakedDecalBounds", bakedDecalAsset.bounds);
                materialInstance.EnableKeyword("_BAKED_DECALS");
                materialInstance.DisableKeyword("_DECALS_ON");
            }
            else
            {
                materialInstance.SetTexture("_BakedOverrideMap", null);
                materialInstance.SetTexture("_BakedMultiplyMap", null);
                materialInstance.SetTexture("_BakedAdditiveMap", null);
                materialInstance.DisableKeyword("_BAKED_DECALS");
            }

            if (runtimeDecalMap == null && pendingDecalOverlay != null)
                ApplyRuntimeDecalOverlay();
            else if (runtimeDecalMap != null)
                materialInstance.SetTexture("_BakedOverrideMap", runtimeDecalMap);
        }

        private void UpdateBounds()
        {
            if (grassData.Length == 0)
            {
                renderBounds = new Bounds(transform.position, Vector3.one * 100);
                return;
            }

            renderBounds = new Bounds(grassData[0].position, Vector3.one);
            for (int i = 0; i < grassData.Length; i++)
                renderBounds.Encapsulate(grassData[i].position);
            renderBounds.Expand(settings.maxHeight * 2);
        }

        private void Cleanup()
        {
            sourceBuffer?.Release();
            visibleBuffer?.Release();
            argsBuffer?.Release();

            sourceBuffer = null;
            visibleBuffer = null;
            argsBuffer = null;
            cachedMesh = null;

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

            GrassRuntimeDecalCompositor.Release(ref runtimeDecalMap);

            activeInstanceCount = 0;
            isInitialized = false;
        }

        private Camera GetCurrentCamera()
        {
#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                var sceneView = UnityEditor.SceneView.lastActiveSceneView;
                return sceneView != null ? sceneView.camera : null;
            }
#endif

            if (Camera.main != null)
                return Camera.main;

            return Camera.allCameras.Length > 0 ? Camera.allCameras[0] : null;
        }

        private void UpdateCulling(Camera cam)
        {
            if (cullingShaderInstance == null || visibleBuffer == null || !visibleBuffer.IsValid() ||
                argsBuffer == null || !argsBuffer.IsValid() || sourceBuffer == null || !sourceBuffer.IsValid())
            {
                isInitialized = false;
                return;
            }

            visibleBuffer.SetCounterValue(0);

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

            cullingShaderInstance.SetBuffer(cullingKernel, PropSourceBuffer, sourceBuffer);
            cullingShaderInstance.SetBuffer(cullingKernel, PropVisibleBuffer, visibleBuffer);
            bool ov = GrassConsoleDebug.OverrideEnabled;
            var p = ActiveProfile;
            bool pt = p != null && p.overrideThinning;
            float effFarKeep = ov ? GrassConsoleDebug.FarKeepFraction : (pt ? p.farKeepFraction : farKeepFraction);
            float effThinStart = ov ? GrassConsoleDebug.ThinStartDistance : (pt ? p.thinStartDistance : thinStartDistance);
            float effCoverage = ov ? GrassConsoleDebug.CoverageCompensation : (pt ? p.coverageCompensation : coverageCompensation);
            Vector2 effSize = ov ? GrassConsoleDebug.SizeScale : (pt ? p.sizeScale : sizeScale);
            cullingShaderInstance.SetFloat(PropFarKeep, Mathf.Clamp01(effFarKeep));
            cullingShaderInstance.SetFloat(PropThinStart, effThinStart);
            cullingShaderInstance.SetVector(PropSizeScale, effSize);
            cullingShaderInstance.SetFloat(PropCoverageComp, Mathf.Clamp01(effCoverage));
            cullingShaderInstance.SetFloat(PropUseUniformScale, (EffectiveGrassMode == GrassMode.CustomMesh) ? 1f : 0f);

            bool pd = p != null && p.overrideDrawDistance;
            cullingShaderInstance.SetFloat(PropMinFade, pd ? p.minFadeDistance : settings.minFadeDistance);
            cullingShaderInstance.SetFloat(PropMaxDraw, pd ? p.maxDrawDistance : settings.maxDrawDistance);

            int threadGroups = Mathf.CeilToInt((float)activeInstanceCount / THREAD_GROUP_SIZE);
            cullingShaderInstance.Dispatch(cullingKernel, threadGroups, 1, 1);

            GraphicsBuffer.CopyCount(visibleBuffer, argsBuffer, sizeof(uint));

            readbackCounter++;
            if ((Application.isEditor || GrassConsoleDebug.ReadoutEnabled) && readbackCounter % READBACK_INTERVAL == 0)
            {
                var cachedArgsBuffer = argsBuffer;
                if (cachedArgsBuffer != null && cachedArgsBuffer.IsValid())
                {
                    AsyncGPUReadback.Request(cachedArgsBuffer, (request) =>
                    {
                        if (!request.hasError && request.done && cachedArgsBuffer != null && cachedArgsBuffer.IsValid())
                        {
                            var data = request.GetData<uint>();
                            if (data.Length > 1)
                                lastVisibleCount = (int)data[1];
                        }
                    });
                }
            }
        }

        private void UpdateInteractors()
        {
            var interactors = GrassInteractor.ActiveInteractors;
            int count = Mathf.Min(interactors.Count, settings.maxInteractors);

            for (int i = 0; i < 16; i++)
            {
                interactorData[i] = i < count ? interactors[i].GetInteractionData() : Vector4.zero;
            }

            materialInstance.SetVectorArray(PropInteractors, interactorData);
            materialInstance.SetInt(PropInteractorCount, count);
        }

        private void Render()
        {
            if (cachedMesh == null) return;

            var p = ActiveProfile;
            var rp = new RenderParams(materialInstance)
            {
                worldBounds = renderBounds,
                shadowCastingMode = (p != null && p.overrideShadows) ? p.castShadows : settings.castShadows,
                receiveShadows = true,
                layer = gameObject.layer,
                renderingLayerMask = settings.renderingLayerMask
            };

            Graphics.RenderMeshIndirect(rp, cachedMesh, argsBuffer);
        }

#if UNITY_EDITOR
        private void ValidateAndRepairMaterial()
        {
            if (materialInstance == null || settings == null)
                return;

            if (visibleBuffer == null || !visibleBuffer.IsValid())
            {
                Initialize();
                return;
            }

            Mesh currentActiveMesh = ResolveActiveMesh();
            if (currentActiveMesh != null && currentActiveMesh != cachedMesh)
            {
                RebuildBuffers();
                return;
            }

            if (materialDirty)
            {
                ApplySettingsToMaterial();
                materialInstance.SetBuffer(PropGrassBuffer, visibleBuffer);
                materialDirty = false;
            }
        }
#endif

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
