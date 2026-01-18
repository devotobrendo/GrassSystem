// GrassRenderer.cs - Main rendering controller for GPU-instanced grass
// Manages compute buffers, dispatches culling, and renders via RenderMeshIndirect

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
        
        // Compute buffers
        private ComputeBuffer sourceBuffer;
        private ComputeBuffer visibleBuffer;
        private GraphicsBuffer argsBuffer; // Must be GraphicsBuffer for RenderMeshIndirect in Unity 6
        private ComputeBuffer interactorBuffer;
        
        // Kernel ID
        private int cullingKernel;
        private const int THREAD_GROUP_SIZE = 128;
        
        // Bounds for rendering
        private Bounds renderBounds;
        
        // Indirect args: [vertexCount, instanceCount, startVertex, startInstance, 0]
        private readonly uint[] argsReset = new uint[5] { 0, 0, 0, 0, 0 };
        
        // Interactor data cache
        private Vector4[] interactorData = new Vector4[16];
        
        // Material instance
        private Material materialInstance;
        
        // Shader property IDs (cached for performance)
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
        
        // Camera frustum planes
        private Vector4[] frustumPlanes = new Vector4[6];
        private Plane[] cameraPlanes = new Plane[6];
        
        // Is initialized?
        private bool isInitialized;
        
        #region Public API
        
        /// <summary>
        /// Access to grass data for painting tools
        /// </summary>
        public List<GrassData> GrassDataList
        {
            get => grassData;
            set
            {
                grassData = value;
                if (isInitialized) RebuildBuffers();
            }
        }
        
        /// <summary>
        /// Force rebuild of all GPU buffers
        /// </summary>
        public void RebuildBuffers()
        {
            Cleanup();
            if (grassData.Count > 0)
                Initialize();
        }
        
        /// <summary>
        /// Clear all grass data
        /// </summary>
        public void ClearGrass()
        {
            grassData.Clear();
            Cleanup();
        }
        
        // Visible count tracking
        private uint[] readbackArgs = new uint[5];
        private int lastVisibleCount;
        
        /// <summary>
        /// Number of grass instances currently being rendered (after culling)
        /// </summary>
        public int VisibleGrassCount => lastVisibleCount;
        
        #endregion
        
        #region Lifecycle
        
        private void OnEnable()
        {
            if (grassData.Count > 0)
                Initialize();
        }
        
        private void OnDisable()
        {
            Cleanup();
        }
        
        private void Update()
        {
            if (!isInitialized || grassData.Count == 0)
                return;
            
            Camera cam = GetCurrentCamera();
            if (cam == null)
                return;
            
            // Update and dispatch
            UpdateCulling(cam);
            Render();
        }
        
        #endregion
        
        #region Initialization
        
        private void Initialize()
        {
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
            
            // Create source buffer
            sourceBuffer = new ComputeBuffer(grassData.Count, GrassData.Stride, ComputeBufferType.Structured);
            sourceBuffer.SetData(grassData);
            
            // Create visible buffer (append buffer)
            visibleBuffer = new ComputeBuffer(grassData.Count, GrassDrawData.Stride, ComputeBufferType.Append);
            
            // Create indirect args buffer (GraphicsBuffer required for RenderMeshIndirect)
            argsBuffer = new GraphicsBuffer(GraphicsBuffer.Target.IndirectArguments, 1, sizeof(uint) * 5);
            argsReset[0] = settings.grassMesh.GetIndexCount(0);
            argsReset[1] = 0; // Will be set by compute shader
            argsBuffer.SetData(argsReset);
            
            // Get kernel
            cullingKernel = settings.cullingShader.FindKernel("CSMain");
            
            // Set static compute shader properties
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
            
            // Create material instance
            materialInstance = new Material(settings.grassMaterial);
            materialInstance.SetBuffer(PropGrassBuffer, visibleBuffer);
            
            // Apply settings to material
            ApplySettingsToMaterial();
            
            // Calculate bounds
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
            materialInstance.SetColor("_PatternColorA", settings.patternColorA);
            materialInstance.SetColor("_PatternColorB", settings.patternColorB);
            
            materialInstance.SetFloat("_UsePattern", settings.useCheckeredPattern ? 1 : 0);
            materialInstance.SetFloat("_PatternScale", settings.patternScale);
            materialInstance.SetFloat("_UseTipCutout", settings.useTipCutout ? 1 : 0);
            materialInstance.SetFloat("_TipCutoff", settings.tipCutoffHeight);
            
            materialInstance.SetFloat("_WindSpeed", settings.windSpeed);
            materialInstance.SetFloat("_WindStrength", settings.windStrength);
            materialInstance.SetFloat("_WindFrequency", settings.windFrequency);
            materialInstance.SetFloat("_Translucency", settings.translucency);
            materialInstance.SetFloat("_AlignNormals", settings.useAlignedNormals ? 1 : 0);
            
            materialInstance.SetFloat(PropInteractorStrength, settings.interactorStrength);
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
            {
                renderBounds.Encapsulate(data.position);
            }
            renderBounds.Expand(settings.maxHeight * 2);
        }
        
        private void Cleanup()
        {
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
        
        #endregion
        
        #region Rendering
        
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
            // Reset visible buffer and args
            visibleBuffer.SetCounterValue(0);
            argsReset[1] = 0;
            argsBuffer.SetData(argsReset);
            
            // Update camera data
            Matrix4x4 vp = cam.projectionMatrix * cam.worldToCameraMatrix;
            settings.cullingShader.SetMatrix(PropViewProjMatrix, vp);
            settings.cullingShader.SetVector(PropCameraPos, cam.transform.position);
            
            // Update frustum planes
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
            
            // Update interactors
            UpdateInteractors();
            
            // Update time
            settings.cullingShader.SetFloat(PropTime, Time.time);
            
            // Dispatch compute shader
            int threadGroups = Mathf.CeilToInt((float)grassData.Count / THREAD_GROUP_SIZE);
            settings.cullingShader.Dispatch(cullingKernel, threadGroups, 1, 1);
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
            
            // Also set on material for vertex shader
            materialInstance.SetVectorArray(PropInteractors, interactorData);
            materialInstance.SetInt(PropInteractorCount, count);
        }
        
        private void Render()
        {
            var rp = new RenderParams(materialInstance)
            {
                worldBounds = renderBounds,
                shadowCastingMode = settings.castShadows,
                receiveShadows = settings.receiveShadows,
                layer = gameObject.layer
            };
            
            Graphics.RenderMeshIndirect(rp, settings.grassMesh, argsBuffer);
        }
        
        #endregion
        
        #region Debug
        
        private void OnDrawGizmosSelected()
        {
            if (settings != null && settings.drawCullingBounds)
            {
                Gizmos.color = new Color(0, 1, 0, 0.3f);
                Gizmos.DrawWireCube(renderBounds.center, renderBounds.size);
            }
        }
        
        #endregion
    }
}
