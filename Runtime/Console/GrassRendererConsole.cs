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

        public static float DebugFarKeepOverride = -1f;

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
        private static readonly int PropInstanceCount = Shader.PropertyToID("_InstanceCount");
        private static readonly int PropInteractors = Shader.PropertyToID("_Interactors");
        private static readonly int PropInteractorCount = Shader.PropertyToID("_InteractorCount");
        private static readonly int PropInteractorStrength = Shader.PropertyToID("_InteractorStrength");

        private Vector4[] frustumPlanes = new Vector4[6];
        private Plane[] cameraPlanes = new Plane[6];
        private bool isInitialized;

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

        public int VisibleGrassCount => lastVisibleCount;
        public GrassDecalBakeAsset BakedDecalAsset => bakedDecalAsset;
        public Material MaterialInstance => materialInstance;
        public bool IsInitialized => isInitialized;

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

        private void Update()
        {
            if (!isInitialized)
            {
                TryAutoRecover();
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

            Camera cam = GetCurrentCamera();
            if (cam == null)
                return;

            UpdateCulling(cam);
            Render();
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
                sourceBuffer = new ComputeBuffer(grassData.Length, GrassDataConsole.Stride, ComputeBufferType.Structured);
                if (sourceBuffer == null || !sourceBuffer.IsValid())
                {
                    Debug.LogError("GrassRendererConsole: Failed to create sourceBuffer!", this);
                    return;
                }
                sourceBuffer.SetData(grassData);

                visibleBuffer = new ComputeBuffer(grassData.Length, GrassDrawData.Stride, ComputeBufferType.Append);
                if (visibleBuffer == null || !visibleBuffer.IsValid())
                {
                    Debug.LogError("GrassRendererConsole: Failed to create visibleBuffer!", this);
                    Cleanup();
                    return;
                }

                cachedMesh = settings.GetActiveMesh(GetInstanceID());
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
                cullingShaderInstance.SetInt(PropInstanceCount, grassData.Length);
                cullingShaderInstance.SetFloat(PropMinFade, settings.minFadeDistance);
                cullingShaderInstance.SetFloat(PropMaxDraw, settings.maxDrawDistance);

                materialInstance = new Material(settings.grassMaterial);
                materialInstance.SetBuffer(PropGrassBuffer, visibleBuffer);

                ApplySettingsToMaterial();
                UpdateBounds();

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

            if (settings.albedoTexture != null)
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

            if (settings.useReceiveShadows)
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

            bool isCustomMeshMode = settings.grassMode == GrassMode.CustomMesh;
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
            float effFarKeep = DebugFarKeepOverride >= 0f ? DebugFarKeepOverride : farKeepFraction;
            cullingShaderInstance.SetFloat(PropFarKeep, Mathf.Clamp01(effFarKeep));
            cullingShaderInstance.SetFloat(PropThinStart, thinStartDistance);
            cullingShaderInstance.SetVector(PropSizeScale, sizeScale);
            cullingShaderInstance.SetFloat(PropCoverageComp, Mathf.Clamp01(coverageCompensation));

            int threadGroups = Mathf.CeilToInt((float)grassData.Length / THREAD_GROUP_SIZE);
            cullingShaderInstance.Dispatch(cullingKernel, threadGroups, 1, 1);

            GraphicsBuffer.CopyCount(visibleBuffer, argsBuffer, sizeof(uint));

#if UNITY_EDITOR
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
#endif
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

            var rp = new RenderParams(materialInstance)
            {
                worldBounds = renderBounds,
                shadowCastingMode = settings.castShadows,
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

            Mesh currentActiveMesh = settings.GetActiveMesh(GetInstanceID());
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
