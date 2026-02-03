// Copyright (c) 2026 Brendo Otavio Carvalho de Matos. All rights reserved.

using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace GrassSystem
{
    public class GrassPainterWindow : EditorWindow
    {
        private enum PaintMode { Add, Remove, Height, Pattern, Color }
        private PaintMode currentMode = PaintMode.Add;
        
        private GrassRenderer targetRenderer;
        private SO_GrassToolSettings toolSettings;
        
        private Vector2 scrollPos;
        private bool isPainting;
        private bool brushEnabled = true; // Toggle with P key to disable/enable brush without closing tool
        
        private RaycastHit[] hitResults = new RaycastHit[10];
        private Vector3 lastPaintPos;
        
        // Deferred paint queue for fluid brush experience
        private List<Vector3> pendingPaintPositions = new List<Vector3>();
        private List<Vector3> pendingPaintNormals = new List<Vector3>();
        private PaintMode pendingPaintMode;
        private bool isProcessingDeferred = false;
        private int processedCount = 0;
        private int totalToProcess = 0;
        private const int BATCH_SIZE = 5; // Process N positions per frame
        
        // Dirty state tracking - avoid marking dirty every stroke
        private int strokesSinceLastSave = 0;
        private const int STROKES_BEFORE_SAVE = 10; // Only save every N strokes
        private bool sceneDirtyPending = false;
        
        // Renderer dropdown cache
        private GrassRenderer[] sceneRenderers;
        private string[] rendererNames;
        private int selectedRendererIndex;
        private GrassRenderer previousRenderer;  // Track previous renderer to save data when switching
        
        [MenuItem("Tools/Grass Painter")]
        public static void ShowWindow()
        {
            var window = GetWindow<GrassPainterWindow>("Grass Painter");
            window.minSize = new Vector2(300, 400);
            window.LoadOrCreateSettings();
        }
        
        private void OnEnable()
        {
            SceneView.duringSceneGui += OnSceneGUI;
            LoadOrCreateSettings();
            if (targetRenderer == null)
                targetRenderer = FindAnyObjectByType<GrassRenderer>();
            previousRenderer = targetRenderer;  // Initialize previous renderer tracking
            RefreshSceneRenderers();
        }
        
        private void OnDisable()
        {
            SceneView.duringSceneGui -= OnSceneGUI;
            
            // Flush any pending dirty state when window closes
            if (sceneDirtyPending && targetRenderer != null)
            {
                EditorUtility.SetDirty(targetRenderer);
                sceneDirtyPending = false;
                strokesSinceLastSave = 0;
            }
        }
        
        /// <summary>
        /// Refreshes the cached list of all GrassRenderers in the scene for dropdown selection.
        /// </summary>
        private void RefreshSceneRenderers()
        {
            sceneRenderers = FindObjectsByType<GrassRenderer>(FindObjectsSortMode.None);
            rendererNames = new string[sceneRenderers.Length];
            
            for (int i = 0; i < sceneRenderers.Length; i++)
            {
                int count = sceneRenderers[i].GrassDataList?.Count ?? 0;
                string settingsName = sceneRenderers[i].settings != null 
                    ? sceneRenderers[i].settings.name 
                    : "No Settings";
                rendererNames[i] = $"{sceneRenderers[i].gameObject.name} ({count:N0}) - {settingsName}";
            }
            
            // Update selected index to match current target
            selectedRendererIndex = System.Array.IndexOf(sceneRenderers, targetRenderer);
            if (selectedRendererIndex < 0) selectedRendererIndex = 0;
        }
        
        /// <summary>
        /// Called when switching between GrassRenderers to ensure proper buffer management.
        /// Saves current renderer's data and prepares the new renderer.
        /// </summary>
        private void OnRendererChanged(GrassRenderer newRenderer)
        {
            // Save data on the previous renderer before switching
            if (previousRenderer != null && previousRenderer != newRenderer)
            {
                // Ensure previous renderer's buffers are up to date
                if (previousRenderer.GrassDataList != null && previousRenderer.GrassDataList.Count > 0)
                {
                    previousRenderer.RebuildBuffers();
                    EditorUtility.SetDirty(previousRenderer);
                }
            }
            
            previousRenderer = newRenderer;
            targetRenderer = newRenderer;
            
            // Validate and prepare the new renderer
            if (targetRenderer != null && targetRenderer.settings != null)
            {
                // Validate settings before allowing painting
                if (!targetRenderer.settings.Validate(out string error))
                {
                    Debug.LogWarning($"GrassRenderer settings issue: {error}. Please fix before painting.");
                }
            }
            
            RefreshSceneRenderers();
        }
        
        private void LoadOrCreateSettings()
        {
            // First try to find existing settings anywhere in project
            string[] guids = AssetDatabase.FindAssets("t:SO_GrassToolSettings");
            if (guids.Length > 0)
            {
                string existingPath = AssetDatabase.GUIDToAssetPath(guids[0]);
                toolSettings = AssetDatabase.LoadAssetAtPath<SO_GrassToolSettings>(existingPath);
                if (toolSettings != null) return;
            }
            
            // Create new settings in project Assets folder (not inside the package)
            string folderPath = "Assets/Editor/GrassSystem";
            string assetPath = folderPath + "/GrassToolSettings.asset";
            
            toolSettings = CreateInstance<SO_GrassToolSettings>();
            
            // Create folder hierarchy if needed
            if (!AssetDatabase.IsValidFolder("Assets/Editor"))
                AssetDatabase.CreateFolder("Assets", "Editor");
            if (!AssetDatabase.IsValidFolder(folderPath))
                AssetDatabase.CreateFolder("Assets/Editor", "GrassSystem");
            
            AssetDatabase.CreateAsset(toolSettings, assetPath);
            AssetDatabase.SaveAssets();
            Debug.Log($"Created GrassToolSettings at: {assetPath}");
        }
        
        private void OnGUI()
        {
            scrollPos = EditorGUILayout.BeginScrollView(scrollPos);
            
            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("Grass Painter", EditorStyles.boldLabel);
            
            // Brush toggle with keyboard hint
            EditorGUILayout.BeginHorizontal();
            brushEnabled = EditorGUILayout.Toggle("Brush Enabled", brushEnabled);
            EditorGUILayout.LabelField("(P to toggle)", EditorStyles.miniLabel, GUILayout.Width(70));
            EditorGUILayout.EndHorizontal();
            
            if (!brushEnabled)
            {
                EditorGUILayout.HelpBox("Brush is paused. Press P or toggle above to resume.", MessageType.Info);
            }
            
            EditorGUILayout.Space(5);
            
            // === RENDERER SELECTION ===
            EditorGUILayout.LabelField("Target Renderer", EditorStyles.boldLabel);
            
            // Dropdown for quick switching between renderers
            if (sceneRenderers != null && sceneRenderers.Length > 0)
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUI.BeginChangeCheck();
                selectedRendererIndex = EditorGUILayout.Popup(selectedRendererIndex, rendererNames);
                if (EditorGUI.EndChangeCheck() && selectedRendererIndex < sceneRenderers.Length)
                {
                    OnRendererChanged(sceneRenderers[selectedRendererIndex]);
                }
                
                if (GUILayout.Button("↻", GUILayout.Width(25)))
                    RefreshSceneRenderers();
                EditorGUILayout.EndHorizontal();
            }
            else
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.HelpBox("No GrassRenderers in scene", MessageType.Info);
                if (GUILayout.Button("↻", GUILayout.Width(25)))
                    RefreshSceneRenderers();
                EditorGUILayout.EndHorizontal();
            }
            
            // Manual object field for drag-drop assignment
            EditorGUI.BeginChangeCheck();
            var newRenderer = (GrassRenderer)EditorGUILayout.ObjectField(
                "Manual Assign", targetRenderer, typeof(GrassRenderer), true);
            if (EditorGUI.EndChangeCheck() && newRenderer != targetRenderer)
            {
                OnRendererChanged(newRenderer);
            }
            
            if (targetRenderer == null)
            {
                EditorGUILayout.HelpBox("No GrassRenderer found. Create one or assign it.", MessageType.Warning);
                if (GUILayout.Button("Create Grass Renderer"))
                    CreateGrassRenderer();
                EditorGUILayout.EndScrollView();
                return;
            }
            
            if (targetRenderer.settings == null)
            {
                EditorGUILayout.HelpBox("GrassRenderer has no settings assigned.", MessageType.Error);
                EditorGUILayout.EndScrollView();
                return;
            }
            
            EditorGUILayout.Space(10);
            
            int grassCount = targetRenderer.GrassDataList?.Count ?? 0;
            int avgBladesPerCluster = toolSettings.useClusterSpawning 
                ? (toolSettings.minBladesPerCluster + toolSettings.maxBladesPerCluster) / 2 
                : 1;
            int estimatedClusters = avgBladesPerCluster > 0 ? grassCount / avgBladesPerCluster : grassCount;
            
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField($"Total Grass: {grassCount:N0}", EditorStyles.boldLabel);
            EditorGUILayout.LabelField($"(~{estimatedClusters:N0} clusters)", EditorStyles.miniLabel);
            EditorGUILayout.EndHorizontal();
            
            EditorGUILayout.Space(10);
            
            if (GUILayout.Button("Reset All Settings"))
            {
                if (EditorUtility.DisplayDialog("Reset Settings", "Reset all brush settings to defaults?", "Yes", "Cancel"))
                {
                    toolSettings.ResetToDefaults();
                    EditorUtility.SetDirty(toolSettings);
                }
            }
            
            EditorGUILayout.Space(10);
            
            EditorGUILayout.LabelField("Paint Mode", EditorStyles.boldLabel);
            currentMode = (PaintMode)GUILayout.Toolbar((int)currentMode, 
                new string[] { "Add", "Remove", "Height", "Pattern", "Color" });
            
            EditorGUILayout.Space(10);
            DrawToolSettings();
            EditorGUILayout.Space(10);
            
            EditorGUILayout.LabelField("Actions", EditorStyles.boldLabel);
            
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Generate on Selection"))
                GenerateOnSelection();
            if (GUILayout.Button("Clear All"))
            {
                if (EditorUtility.DisplayDialog("Clear Grass", "Are you sure you want to clear all grass?", "Yes", "Cancel"))
                    ClearAllGrass();
            }
            EditorGUILayout.EndHorizontal();
            
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Rebuild Buffers"))
                targetRenderer.RebuildBuffers();
            
            // Manual memory optimization button
            if (GUILayout.Button("Optimize Memory"))
            {
                OptimizeEditorMemory();
            }
            EditorGUILayout.EndHorizontal();
            
            EditorGUILayout.Space(10);
            EditorGUILayout.HelpBox(
                "Hold Left Mouse Button in Scene View to paint.\n" +
                "Shift + LMB to remove.\n" +
                "Scroll wheel to adjust brush size.",
                MessageType.Info);
            
            EditorGUILayout.Space(10);
            
            // === ADVANCED LIMITS ===
            toolSettings.showAdvancedLimits = EditorGUILayout.Foldout(toolSettings.showAdvancedLimits, "Advanced Limits", true, EditorStyles.foldoutHeader);
            if (toolSettings.showAdvancedLimits)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.HelpBox("Customize slider maximum values for extended ranges.", MessageType.Info);
                
                EditorGUILayout.Space(3);
                EditorGUILayout.LabelField("Brush Limits", EditorStyles.miniLabel);
                toolSettings.maxBrushSizeLimit = EditorGUILayout.FloatField("Max Brush Size", toolSettings.maxBrushSizeLimit);
                toolSettings.maxDensityLimit = EditorGUILayout.FloatField("Max Density (legacy)", toolSettings.maxDensityLimit);
                toolSettings.maxDensityPerM2Limit = EditorGUILayout.FloatField("Max Density/m²", toolSettings.maxDensityPerM2Limit);
                
                EditorGUILayout.Space(3);
                EditorGUILayout.LabelField("Cluster Limits", EditorStyles.miniLabel);
                toolSettings.maxBladesPerClusterLimit = EditorGUILayout.IntField("Max Blades/Cluster", toolSettings.maxBladesPerClusterLimit);
                toolSettings.maxClusterRadiusLimit = EditorGUILayout.FloatField("Max Cluster Radius", toolSettings.maxClusterRadiusLimit);
                
                EditorGUILayout.Space(3);
                EditorGUILayout.LabelField("Blade Dimension Limits", EditorStyles.miniLabel);
                toolSettings.maxBladeWidthLimit = EditorGUILayout.FloatField("Max Blade Width", toolSettings.maxBladeWidthLimit);
                toolSettings.maxBladeHeightLimit = EditorGUILayout.FloatField("Max Blade Height", toolSettings.maxBladeHeightLimit);
                toolSettings.maxBladeSizeLimit = EditorGUILayout.FloatField("Max Blade Size", toolSettings.maxBladeSizeLimit);
                toolSettings.maxHeightBrushLimit = EditorGUILayout.FloatField("Max Height Brush", toolSettings.maxHeightBrushLimit);
                
                EditorGUI.indentLevel--;
            }
            
            EditorGUILayout.EndScrollView();
            
            if (GUI.changed && toolSettings != null)
                EditorUtility.SetDirty(toolSettings);
        }
        
        private void DrawToolSettings()
        {
            if (toolSettings == null) return;
            
            // Show current mode info
            if (targetRenderer != null && targetRenderer.settings != null)
            {
                var mode = targetRenderer.settings.grassMode;
                EditorGUILayout.HelpBox($"Mode: {mode}", MessageType.None);
            }
            
            EditorGUILayout.LabelField("Brush Settings", EditorStyles.boldLabel);
            toolSettings.brushSize = EditorGUILayout.Slider("Brush Size", toolSettings.brushSize, 0.1f, toolSettings.maxBrushSizeLimit);
            
            if (currentMode == PaintMode.Add)
            {
                // Density mode selector
                toolSettings.densityMode = (DensityMode)EditorGUILayout.EnumPopup("Density Mode", toolSettings.densityMode);
                
                // Show contextual density control based on mode
                switch (toolSettings.densityMode)
                {
                    case DensityMode.PerUnitRadius:
                        toolSettings.density = EditorGUILayout.Slider("Density (legacy)", toolSettings.density, 0.1f, toolSettings.maxDensityLimit);
                        EditorGUILayout.HelpBox($"Count = BrushSize × Density = {toolSettings.brushSize:F1} × {toolSettings.density:F1} = {Mathf.RoundToInt(toolSettings.brushSize * toolSettings.density)} clusters", MessageType.None);
                        break;
                        
                    case DensityMode.InstancesPerM2:
                    case DensityMode.ClustersPerM2:
                        // Shared controls for per-m² modes
                        string label = toolSettings.densityMode == DensityMode.InstancesPerM2 ? "Instances" : "Clusters";
                        toolSettings.densityPerM2 = EditorGUILayout.Slider($"{label} per Area Unit", toolSettings.densityPerM2, 1f, toolSettings.maxDensityPerM2Limit);
                        toolSettings.areaUnit = EditorGUILayout.Slider("Area Unit (m²)", toolSettings.areaUnit, 0.5f, 10f);
                        
                        // Calculate and show debug info
                        float brushArea = Mathf.PI * toolSettings.brushSize * toolSettings.brushSize;
                        float effectiveDensity = toolSettings.densityPerM2 / toolSettings.areaUnit;
                        int avgBlades = toolSettings.useClusterSpawning 
                            ? (toolSettings.minBladesPerCluster + toolSettings.maxBladesPerCluster) / 2 
                            : 1;
                        
                        int clusters, instances;
                        if (toolSettings.densityMode == DensityMode.InstancesPerM2)
                        {
                            instances = Mathf.RoundToInt(effectiveDensity * brushArea);
                            clusters = Mathf.Max(1, instances / avgBlades);
                        }
                        else
                        {
                            clusters = Mathf.Max(1, Mathf.RoundToInt(effectiveDensity * brushArea));
                            instances = clusters * avgBlades;
                        }
                        
                        EditorGUILayout.HelpBox(
                            $"Brush area: {brushArea:F1}m² | {toolSettings.densityPerM2:F0} {label.ToLower()} / {toolSettings.areaUnit:F1}m²\n" +
                            $"Per click: ~{clusters} clusters, ~{instances} instances", 
                            MessageType.None);
                        break;
                }
                
                toolSettings.normalLimit = EditorGUILayout.Slider("Normal Limit", toolSettings.normalLimit, 0f, 1f);
                
                EditorGUILayout.Space(5);
                
                // Show different options based on mode
                bool isCustomMeshMode = targetRenderer != null && 
                                        targetRenderer.settings != null && 
                                        targetRenderer.settings.grassMode == GrassMode.CustomMesh;
                
                // Toggle for custom size override
                toolSettings.useCustomSize = EditorGUILayout.Toggle("Use Custom Size", toolSettings.useCustomSize);
                
                if (isCustomMeshMode)
                {
                    EditorGUILayout.LabelField("Blade Size", EditorStyles.boldLabel);
                    if (toolSettings.useCustomSize)
                    {
                        toolSettings.minBladeSize = EditorGUILayout.Slider("Min Size", toolSettings.minBladeSize, 0.1f, toolSettings.maxBladeSizeLimit);
                        toolSettings.maxBladeSize = EditorGUILayout.Slider("Max Size", toolSettings.maxBladeSize, 0.1f, toolSettings.maxBladeSizeLimit);
                    }
                    else
                    {
                        var settings = targetRenderer.settings;
                        EditorGUI.BeginDisabledGroup(true);
                        EditorGUILayout.Slider("Min Size (from Settings)", settings.minSize, 0.1f, toolSettings.maxBladeSizeLimit);
                        EditorGUILayout.Slider("Max Size (from Settings)", settings.maxSize, 0.1f, toolSettings.maxBladeSizeLimit);
                        EditorGUI.EndDisabledGroup();
                        EditorGUILayout.HelpBox("Enable 'Use Custom Size' to override.", MessageType.Info);
                    }
                }
                else
                {
                    EditorGUILayout.LabelField("Blade Dimensions", EditorStyles.boldLabel);
                    if (toolSettings.useCustomSize)
                    {
                        toolSettings.minBladeWidth = EditorGUILayout.Slider("Min Width", toolSettings.minBladeWidth, 0.01f, toolSettings.maxBladeWidthLimit);
                        toolSettings.maxBladeWidth = EditorGUILayout.Slider("Max Width", toolSettings.maxBladeWidth, 0.01f, toolSettings.maxBladeWidthLimit);
                        toolSettings.minBladeHeight = EditorGUILayout.Slider("Min Height", toolSettings.minBladeHeight, 0.1f, toolSettings.maxBladeHeightLimit);
                        toolSettings.maxBladeHeight = EditorGUILayout.Slider("Max Height", toolSettings.maxBladeHeight, 0.1f, toolSettings.maxBladeHeightLimit);
                    }
                    else
                    {
                        var settings = targetRenderer.settings;
                        EditorGUI.BeginDisabledGroup(true);
                        EditorGUILayout.Slider("Min Width (from Settings)", settings.minWidth, 0.01f, 0.3f);
                        EditorGUILayout.Slider("Max Width (from Settings)", settings.maxWidth, 0.01f, 0.3f);
                        EditorGUILayout.Slider("Min Height (from Settings)", settings.minHeight, 0.05f, 1.5f);
                        EditorGUILayout.Slider("Max Height (from Settings)", settings.maxHeight, 0.05f, 1.5f);
                        EditorGUI.EndDisabledGroup();
                        EditorGUILayout.HelpBox("Enable 'Use Custom Size' to override.", MessageType.Info);
                    }
                }
                
                
                EditorGUILayout.Space(5);
                toolSettings.paintMask = EditorGUILayout.MaskField("Paint Mask", 
                    InternalEditorUtility.LayerMaskToConcatenatedLayersMask(toolSettings.paintMask),
                    InternalEditorUtility.layers);
                
                EditorGUILayout.Space(5);
                EditorGUILayout.LabelField("Cluster Spawning", EditorStyles.boldLabel);
                toolSettings.useClusterSpawning = EditorGUILayout.Toggle("Enable Clusters", toolSettings.useClusterSpawning);
                if (toolSettings.useClusterSpawning)
                {
                    EditorGUI.indentLevel++;
                    toolSettings.minBladesPerCluster = EditorGUILayout.IntSlider("Min Blades", toolSettings.minBladesPerCluster, 1, toolSettings.maxBladesPerClusterLimit);
                    toolSettings.maxBladesPerCluster = EditorGUILayout.IntSlider("Max Blades", toolSettings.maxBladesPerCluster, 1, toolSettings.maxBladesPerClusterLimit);
                    toolSettings.clusterRadius = EditorGUILayout.Slider("Cluster Radius", toolSettings.clusterRadius, 0.01f, toolSettings.maxClusterRadiusLimit);
                    EditorGUI.indentLevel--;
                }
            }
            else if (currentMode == PaintMode.Remove)
            {
                EditorGUILayout.LabelField("Removal Settings", EditorStyles.boldLabel);
                toolSettings.removalStrength = EditorGUILayout.Slider("Removal Strength", toolSettings.removalStrength, 0.01f, 1f);
                int percent = Mathf.RoundToInt(toolSettings.removalStrength * 100f);
                EditorGUILayout.HelpBox($"Removes ~{percent}% of grass in brush area each stroke", MessageType.None);
            }
            else if (currentMode == PaintMode.Height)
            {
                bool isCustomMeshMode = targetRenderer != null && 
                                        targetRenderer.settings != null && 
                                        targetRenderer.settings.grassMode == GrassMode.CustomMesh;
                if (isCustomMeshMode)
                {
                    toolSettings.minBladeSize = EditorGUILayout.Slider("Size Value", toolSettings.minBladeSize, 0.1f, 3f);
                }
                else
                {
                    toolSettings.heightBrushValue = EditorGUILayout.Slider("Height Value", toolSettings.heightBrushValue, 0.1f, toolSettings.maxHeightBrushLimit);
                }
            }
            else if (currentMode == PaintMode.Pattern)
            {
                toolSettings.patternBrushValue = EditorGUILayout.Slider("Pattern Value (0=A, 1=B)", toolSettings.patternBrushValue, 0f, 1f);
            }
            else if (currentMode == PaintMode.Color)
            {
                EditorGUILayout.Space(5);
                EditorGUILayout.LabelField("Paint Color Settings", EditorStyles.boldLabel);
                
                bool isCustomMeshMode = targetRenderer != null && 
                                        targetRenderer.settings != null && 
                                        targetRenderer.settings.grassMode == GrassMode.CustomMesh;
                
                // Show color options only if not using only albedo color
                bool useOnlyAlbedo = isCustomMeshMode && targetRenderer.settings.colorMode == GrassColorMode.Albedo;
                if (useOnlyAlbedo)
                {
                    EditorGUILayout.HelpBox("Color settings disabled: 'Albedo Texture' color mode is enabled in settings.", MessageType.Info);
                }
                else
                {
                    toolSettings.brushColor = EditorGUILayout.ColorField("Base Color", toolSettings.brushColor);
                    toolSettings.colorVariationR = EditorGUILayout.Slider("Red Variation", toolSettings.colorVariationR, 0f, 0.3f);
                    toolSettings.colorVariationG = EditorGUILayout.Slider("Green Variation", toolSettings.colorVariationG, 0f, 0.3f);
                    toolSettings.colorVariationB = EditorGUILayout.Slider("Blue Variation", toolSettings.colorVariationB, 0f, 0.3f);
                    
                    EditorGUILayout.Space(5);
                    EditorGUILayout.HelpBox("Paint this color onto existing grass blades in the scene.", MessageType.Info);
                    
                    EditorGUILayout.Space(5);
                    if (GUILayout.Button("Reset to White"))
                        toolSettings.brushColor = Color.white;
                }
            }
        }
        
        private void OnSceneGUI(SceneView sceneView)
        {
            if (targetRenderer == null || toolSettings == null)
                return;
            
            Event e = Event.current;
            
            // Toggle brush on/off with P key (without closing window)
            if (e.type == EventType.KeyDown && e.keyCode == KeyCode.P)
            {
                brushEnabled = !brushEnabled;
                e.Use();
                Repaint();
                SceneView.RepaintAll();
            }
            
            // Handle scroll wheel for brush size
            if (e.type == EventType.ScrollWheel && e.control)
            {
                toolSettings.brushSize = Mathf.Clamp(toolSettings.brushSize - e.delta.y * 0.5f, 0.1f, toolSettings.maxBrushSizeLimit);
                e.Use();
                Repaint();
            }
            
            Ray ray = HandleUtility.GUIPointToWorldRay(e.mousePosition);
            int hitCount = Physics.RaycastNonAlloc(ray, hitResults, 500f, toolSettings.paintMask);
            
            // Draw processing indicator if deferred processing is active
            if (isProcessingDeferred)
            {
                DrawProcessingIndicator(sceneView);
                return; // Block input during processing
            }
            
            // If brush is disabled, show disabled indicator and skip painting
            if (!brushEnabled)
            {
                if (hitCount > 0)
                {
                    DrawBrushDisabledIndicator(hitResults[0].point, hitResults[0].normal);
                }
                return;
            }
            
            if (hitCount == 0) return;
            
            Vector3 hitPoint = hitResults[0].point;
            Vector3 hitNormal = hitResults[0].normal;
            
            // Draw current brush cursor
            DrawBrushPreview(hitPoint, hitNormal);
            
            // Draw pending positions preview during stroke
            if (isPainting && pendingPaintPositions.Count > 0)
            {
                DrawPendingPreview();
            }
            
            if (e.type == EventType.MouseDown && e.button == 0)
            {
                isPainting = true;
                lastPaintPos = hitPoint;
                pendingPaintMode = e.shift ? PaintMode.Remove : currentMode;
                
                // Queue first position
                QueuePaintPosition(hitPoint, hitNormal);
                e.Use();
            }
            else if (e.type == EventType.MouseUp && e.button == 0)
            {
                isPainting = false;
                e.Use();
                
                // Start deferred processing if there are pending positions
                if (pendingPaintPositions.Count > 0)
                {
                    StartDeferredProcessing();
                }
            }
            
            if (isPainting && e.type == EventType.MouseDrag && e.button == 0)
            {
                // Use brush diameter as minimum distance to prevent overlapping circles
                float dynamicMinDistance = toolSettings.brushSize * 2f;
                if (Vector3.Distance(hitPoint, lastPaintPos) > dynamicMinDistance)
                {
                    QueuePaintPosition(hitPoint, hitNormal);
                    lastPaintPos = hitPoint;
                }
                e.Use();
            }
            
            SceneView.RepaintAll();
        }
        
        private void QueuePaintPosition(Vector3 position, Vector3 normal)
        {
            pendingPaintPositions.Add(position);
            pendingPaintNormals.Add(normal);
        }
        
        private void DrawBrushPreview(Vector3 hitPoint, Vector3 hitNormal)
        {
            Color brushColor = GetBrushColor();
            
            // Pulsing effect during stroke
            if (isPainting)
            {
                float pulse = (Mathf.Sin((float)EditorApplication.timeSinceStartup * 4f) + 1f) * 0.15f + 0.7f;
                brushColor.a = pulse;
            }
            
            Handles.color = brushColor;
            Handles.DrawWireDisc(hitPoint, hitNormal, toolSettings.brushSize);
            Handles.color = new Color(brushColor.r, brushColor.g, brushColor.b, 0.2f);
            Handles.DrawSolidDisc(hitPoint, hitNormal, toolSettings.brushSize);
        }
        
        private void DrawBrushDisabledIndicator(Vector3 hitPoint, Vector3 hitNormal)
        {
            // Draw grayed out brush when disabled
            Color disabledColor = new Color(0.5f, 0.5f, 0.5f, 0.3f);
            Handles.color = disabledColor;
            Handles.DrawWireDisc(hitPoint, hitNormal, toolSettings.brushSize);
            
            // Draw "PAUSED" text
            Handles.BeginGUI();
            Vector2 screenPos = HandleUtility.WorldToGUIPoint(hitPoint);
            GUIStyle style = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontStyle = FontStyle.Bold,
                fontSize = 12
            };
            style.normal.textColor = new Color(1f, 1f, 1f, 0.7f);
            Rect rect = new Rect(screenPos.x - 40, screenPos.y - 10, 80, 20);
            GUI.Label(rect, "PAUSED (P)", style);
            Handles.EndGUI();
            
            SceneView.RepaintAll();
        }
        
        private void DrawPendingPreview()
        {
            // Draw ghost circles for pending positions
            Color previewColor = pendingPaintMode == PaintMode.Remove 
                ? new Color(1f, 0.3f, 0.3f, 0.15f) 
                : new Color(0.3f, 1f, 0.3f, 0.15f);
            
            Handles.color = previewColor;
            for (int i = 0; i < pendingPaintPositions.Count; i++)
            {
                Handles.DrawSolidDisc(pendingPaintPositions[i], pendingPaintNormals[i], toolSettings.brushSize * 0.8f);
            }
        }
        
        private void DrawProcessingIndicator(SceneView sceneView)
        {
            // Draw pulsing indicator at center of pending area
            if (pendingPaintPositions.Count > 0)
            {
                Vector3 center = Vector3.zero;
                foreach (var pos in pendingPaintPositions)
                    center += pos;
                center /= pendingPaintPositions.Count;
                
                float pulse = (Mathf.Sin((float)EditorApplication.timeSinceStartup * 6f) + 1f) * 0.5f;
                float radius = toolSettings.brushSize * (1f + pulse * 0.3f);
                
                Color processingColor = pendingPaintMode == PaintMode.Remove 
                    ? new Color(1f, 0.5f, 0f, 0.4f + pulse * 0.3f)
                    : new Color(0f, 1f, 0.5f, 0.4f + pulse * 0.3f);
                
                Handles.color = processingColor;
                Handles.DrawSolidDisc(center, Vector3.up, radius);
                
                // Draw progress text
                Handles.BeginGUI();
                Vector2 screenPos = HandleUtility.WorldToGUIPoint(center);
                string progressText = $"Processing {processedCount}/{totalToProcess}...";
                GUIStyle style = new GUIStyle(GUI.skin.label)
                {
                    alignment = TextAnchor.MiddleCenter,
                    fontStyle = FontStyle.Bold,
                    fontSize = 14
                };
                style.normal.textColor = Color.white;
                
                Rect rect = new Rect(screenPos.x - 80, screenPos.y - 15, 160, 30);
                GUI.Label(rect, progressText, style);
                Handles.EndGUI();
            }
            
            SceneView.RepaintAll();
        }
        
        private void StartDeferredProcessing()
        {
            isProcessingDeferred = true;
            processedCount = 0;
            totalToProcess = pendingPaintPositions.Count;
            
            // Start batch processing
            EditorApplication.delayCall += ProcessNextBatch;
        }
        
        private void ProcessNextBatch()
        {
            if (targetRenderer == null)
            {
                FinishDeferredProcessing();
                return;
            }
            
            var grassList = targetRenderer.GrassDataList;
            int endIndex = Mathf.Min(processedCount + BATCH_SIZE, totalToProcess);
            
            // Process batch of positions
            for (int i = processedCount; i < endIndex; i++)
            {
                Paint(pendingPaintPositions[i], pendingPaintNormals[i], pendingPaintMode);
            }
            
            processedCount = endIndex;
            SceneView.RepaintAll();
            
            // Check if done
            if (processedCount >= totalToProcess)
            {
                FinishDeferredProcessing();
            }
            else
            {
                // Continue processing next frame
                EditorApplication.delayCall += ProcessNextBatch;
            }
        }
        
        private void FinishDeferredProcessing()
        {
            isProcessingDeferred = false;
            
            // Clear pending lists (don't TrimExcess here - too slow, leave that for OptimizeMemory)
            pendingPaintPositions.Clear();
            pendingPaintNormals.Clear();
            
            processedCount = 0;
            totalToProcess = 0;
            
            // Smart rebuild buffers - only recreates if size changed significantly
            if (targetRenderer != null)
            {
                // Use smart rebuild - much faster for incremental paint operations
                targetRenderer.SmartRebuildBuffers();
                
                // Track strokes and only mark dirty periodically (avoids engasgo)
                strokesSinceLastSave++;
                sceneDirtyPending = true;
                
                if (strokesSinceLastSave >= STROKES_BEFORE_SAVE)
                {
                    strokesSinceLastSave = 0;
                    sceneDirtyPending = false;
                    
                    // Defer SetDirty to prevent UI blocking
                    var rendererToMark = targetRenderer;
                    EditorApplication.delayCall += () =>
                    {
                        if (rendererToMark != null)
                        {
                            EditorUtility.SetDirty(rendererToMark);
                        }
                    };
                }
            }
            
            SceneView.RepaintAll();
        }
        
        private Color GetBrushColor()
        {
            return currentMode switch
            {
                PaintMode.Add => Color.green,
                PaintMode.Remove => Color.red,
                PaintMode.Height => Color.yellow,
                PaintMode.Pattern => Color.cyan,
                PaintMode.Color => Color.magenta,
                _ => Color.white
            };
        }
        
        private void Paint(Vector3 position, Vector3 normal, PaintMode mode)
        {
            if (targetRenderer == null) return;
            
            // Validate settings before painting to prevent GPU crashes
            if (targetRenderer.settings == null)
            {
                Debug.LogError("Cannot paint: GrassRenderer has no settings assigned!");
                return;
            }
            
            if (!targetRenderer.settings.Validate(out string error))
            {
                Debug.LogError($"Cannot paint: {error}");
                return;
            }
            
            var grassList = targetRenderer.GrassDataList;
            
            switch (mode)
            {
                case PaintMode.Add: AddGrass(position, normal, grassList); break;
                case PaintMode.Remove: RemoveGrass(position, grassList); break;
                case PaintMode.Height: EditHeight(position, grassList); break;
                case PaintMode.Pattern: EditPattern(position, grassList); break;
                case PaintMode.Color: EditColor(position, grassList); break;
            }
            // Skipping Undo.RecordObject for performance - it's too slow with large grass counts
        }
        
        private void AddGrass(Vector3 center, Vector3 normal, List<GrassData> grassList)
        {
            // Calculate cluster count based on density mode
            int count = CalculateClusterCount();
            
            // Determine if we're in custom mesh mode
            bool isCustomMeshMode = targetRenderer != null && 
                                    targetRenderer.settings != null && 
                                    targetRenderer.settings.grassMode == GrassMode.CustomMesh;
            
            // Get min/max values based on useCustomSize toggle
            float minWidth, maxWidth, minHeight, maxHeight, minSize, maxSize;
            if (toolSettings.useCustomSize)
            {
                minWidth = toolSettings.minBladeWidth;
                maxWidth = toolSettings.maxBladeWidth;
                minHeight = toolSettings.minBladeHeight;
                maxHeight = toolSettings.maxBladeHeight;
                minSize = toolSettings.minBladeSize;
                maxSize = toolSettings.maxBladeSize;
            }
            else
            {
                var settings = targetRenderer.settings;
                minWidth = settings.minWidth;
                maxWidth = settings.maxWidth;
                minHeight = settings.minHeight;
                maxHeight = settings.maxHeight;
                minSize = settings.minSize;
                maxSize = settings.maxSize;
            }
            
            for (int i = 0; i < count; i++)
            {
                Vector2 offset = Random.insideUnitCircle * toolSettings.brushSize;
                Vector3 clusterCenter = center + new Vector3(offset.x, 0, offset.y);
                
                if (Physics.Raycast(clusterCenter + Vector3.up * 10f, Vector3.down, out RaycastHit hit, 20f, toolSettings.paintMask))
                {
                    if (hit.normal.y < toolSettings.normalLimit)
                        continue;
                    
                    // Determine how many blades in this cluster
                    int bladesInCluster = toolSettings.useClusterSpawning 
                        ? Random.Range(toolSettings.minBladesPerCluster, toolSettings.maxBladesPerCluster + 1)
                        : 1;
                    
                    for (int b = 0; b < bladesInCluster; b++)
                    {
                        // Offset within cluster (first blade at center, rest spread out)
                        Vector3 bladePos = hit.point;
                        if (b > 0 && toolSettings.useClusterSpawning)
                        {
                            Vector2 clusterOffset = Random.insideUnitCircle * toolSettings.clusterRadius;
                            bladePos += new Vector3(clusterOffset.x, 0, clusterOffset.y);
                        }
                        
                        Color col = toolSettings.brushColor;
                        col.r += Random.Range(-toolSettings.colorVariationR, toolSettings.colorVariationR);
                        col.g += Random.Range(-toolSettings.colorVariationG, toolSettings.colorVariationG);
                        col.b += Random.Range(-toolSettings.colorVariationB, toolSettings.colorVariationB);
                        
                        // Use size for custom mesh mode, width/height for default mode
                        float width, height;
                        if (isCustomMeshMode)
                        {
                            // For custom mesh mode, width stores the uniform scale
                            width = Random.Range(minSize, maxSize);
                            height = 1f; // Height is ignored in uniform scale mode
                        }
                        else
                        {
                            width = Random.Range(minWidth, maxWidth);
                            height = Random.Range(minHeight, maxHeight);
                        }
                        
                        GrassData data = new GrassData(bladePos, hit.normal, width, height, col, toolSettings.patternBrushValue);
                        grassList.Add(data);
                    }
                }
            }
        }
        
        /// <summary>
        /// Calculates the number of clusters to spawn based on the current density mode.
        /// </summary>
        private int CalculateClusterCount()
        {
            float brushArea = Mathf.PI * toolSettings.brushSize * toolSettings.brushSize;
            
            switch (toolSettings.densityMode)
            {
                case DensityMode.InstancesPerM2:
                    // Calculate total instances, then divide by average blades per cluster
                    // effectiveDensity = densityPerM2 / areaUnit (e.g., 10 per 2m² = 5 per 1m²)
                    float effectiveDensity = toolSettings.densityPerM2 / toolSettings.areaUnit;
                    int totalInstances = Mathf.RoundToInt(effectiveDensity * brushArea);
                    int avgBladesPerCluster = toolSettings.useClusterSpawning 
                        ? (toolSettings.minBladesPerCluster + toolSettings.maxBladesPerCluster) / 2 
                        : 1;
                    return Mathf.Max(1, totalInstances / avgBladesPerCluster);
                    
                case DensityMode.ClustersPerM2:
                    float effectiveDensityC = toolSettings.densityPerM2 / toolSettings.areaUnit;
                    return Mathf.Max(1, Mathf.RoundToInt(effectiveDensityC * brushArea));
                    
                case DensityMode.PerUnitRadius:
                default:
                    // Legacy behavior: count = brushSize × density
                    return Mathf.Max(1, Mathf.RoundToInt(toolSettings.brushSize * toolSettings.density));
            }
        }
        
        private void RemoveGrass(Vector3 center, List<GrassData> grassList)
        {
            float sqrRadius = toolSettings.brushSize * toolSettings.brushSize;
            float removalChance = toolSettings.removalStrength;
            
            // Use RemoveAll with predicate - O(n) instead of O(n²) from RemoveAt
            // This is MUCH faster for large grass counts
            grassList.RemoveAll(grass =>
            {
                Vector3 diff = grass.position - center;
                diff.y = 0;
                if (diff.sqrMagnitude < sqrRadius)
                {
                    // Partial removal: only remove if random check passes
                    return removalChance >= 1f || Random.value < removalChance;
                }
                return false;
            });
        }
        
        private void EditHeight(Vector3 center, List<GrassData> grassList)
        {
            float sqrRadius = toolSettings.brushSize * toolSettings.brushSize;
            
            for (int i = 0; i < grassList.Count; i++)
            {
                Vector3 diff = grassList[i].position - center;
                diff.y = 0;
                if (diff.sqrMagnitude < sqrRadius)
                {
                    var data = grassList[i];
                    data.widthHeight.y = toolSettings.heightBrushValue;
                    grassList[i] = data;
                }
            }
        }
        
        private void EditPattern(Vector3 center, List<GrassData> grassList)
        {
            float sqrRadius = toolSettings.brushSize * toolSettings.brushSize;
            
            for (int i = 0; i < grassList.Count; i++)
            {
                Vector3 diff = grassList[i].position - center;
                diff.y = 0;
                if (diff.sqrMagnitude < sqrRadius)
                {
                    var data = grassList[i];
                    data.patternMask = toolSettings.patternBrushValue;
                    grassList[i] = data;
                }
            }
        }
        
        private void EditColor(Vector3 center, List<GrassData> grassList)
        {
            float sqrRadius = toolSettings.brushSize * toolSettings.brushSize;
            Color col = toolSettings.brushColor;
            
            for (int i = 0; i < grassList.Count; i++)
            {
                Vector3 diff = grassList[i].position - center;
                diff.y = 0;
                if (diff.sqrMagnitude < sqrRadius)
                {
                    var data = grassList[i];
                    data.color = new Vector3(col.r, col.g, col.b);
                    grassList[i] = data;
                }
            }
        }
        
        /// <summary>
        /// Performs a complete memory cleanup and resets the plugin state as if it was just opened.
        /// Clears all caches, pending operations, Undo stack, and forces garbage collection.
        /// </summary>
        private void OptimizeEditorMemory()
        {
            long memBefore = System.GC.GetTotalMemory(false);
            int totalGrassCount = 0;
            
            // 1. Cancel any pending deferred paint operations
            if (isProcessingDeferred)
            {
                isProcessingDeferred = false;
                processedCount = 0;
                totalToProcess = 0;
            }
            
            // 2. Clear pending paint queues and release their memory
            pendingPaintPositions.Clear();
            pendingPaintPositions.TrimExcess();
            pendingPaintNormals.Clear();
            pendingPaintNormals.TrimExcess();
            
            // 3. Reinitialize internal state
            lastPaintPos = Vector3.zero;
            isPainting = false;
            
            // 4. Process all GrassRenderers - compact and reinitialize
            RefreshSceneRenderers();
            foreach (var renderer in sceneRenderers)
            {
                if (renderer != null)
                {
                    // Get grass count before cleanup
                    var grassList = renderer.GrassDataList;
                    if (grassList != null)
                    {
                        totalGrassCount += grassList.Count;
                        
                        // Compact the list to remove wasted capacity
                        grassList.TrimExcess();
                    }
                    
                    // Force full reinitialization of GPU buffers
                    renderer.ForceReinitialize();
                    
                    // Clear any undo records for this renderer
                    Undo.ClearUndo(renderer);
                }
            }
            
            // 5. Clear cached mesh to force regeneration if needed
            GrassMeshUtility.ClearCache();
            
            // 6. Clear global Undo stack (main source of memory accumulation)
            Undo.ClearAll();
            
            // 7. Force full garbage collection cycle
            System.GC.Collect(System.GC.MaxGeneration, System.GCCollectionMode.Forced, true, true);
            System.GC.WaitForPendingFinalizers();
            System.GC.Collect(System.GC.MaxGeneration, System.GCCollectionMode.Forced, true, true);
            
            // 8. Unload unused Unity assets
            var asyncOp = Resources.UnloadUnusedAssets();
            
            // 9. Clear editor caches
            EditorUtility.UnloadUnusedAssetsImmediate(true);
            
            long memAfter = System.GC.GetTotalMemory(true);
            float freedMB = (memBefore - memAfter) / (1024f * 1024f);
            float grassMemoryMB = (totalGrassCount * 48f) / (1024f * 1024f);
            
            Debug.Log($"[Grass System] Memory Optimization Complete!\n" +
                      $"  → Freed: {freedMB:F2} MB\n" +
                      $"  → Grass instances: {totalGrassCount:N0} (~{grassMemoryMB:F2} MB)\n" +
                      $"  → Renderers reinitialized: {sceneRenderers?.Length ?? 0}\n" +
                      $"  → Undo history cleared");
            
            // 10. Force all Inspectors/Editors to refresh and repaint
            // This fixes the "disappearing Inspector" issue
            UnityEditorInternal.InternalEditorUtility.RepaintAllViews();
            
            // Force reload of all custom editors by reselecting current selection
            var currentSelection = Selection.objects;
            Selection.objects = new Object[0];
            EditorApplication.delayCall += () =>
            {
                Selection.objects = currentSelection;
            };
            
            // Force repaint
            SceneView.RepaintAll();
            Repaint();
        }
        
        private void CreateGrassRenderer()
        {
            var go = new GameObject("Grass Renderer");
            targetRenderer = go.AddComponent<GrassRenderer>();
            Selection.activeGameObject = go;
            Undo.RegisterCreatedObjectUndo(go, "Create Grass Renderer");
        }
        
        private void GenerateOnSelection()
        {
            if (targetRenderer == null || Selection.gameObjects.Length == 0)
            {
                EditorUtility.DisplayDialog("No Selection", "Please select one or more meshes to generate grass on.", "OK");
                return;
            }
            
            Undo.RecordObject(targetRenderer, "Generate Grass");
            
            foreach (var go in Selection.gameObjects)
            {
                var meshFilter = go.GetComponent<MeshFilter>();
                if (meshFilter == null || meshFilter.sharedMesh == null)
                    continue;
                GenerateOnMesh(meshFilter);
            }
            
            targetRenderer.RebuildBuffers();
            
            // Defer SetDirty to next frame to prevent UI blocking
            var rendererToMark = targetRenderer;
            EditorApplication.delayCall += () =>
            {
                if (rendererToMark != null)
                {
                    EditorUtility.SetDirty(rendererToMark);
                }
            };
        }
        
        private void GenerateOnMesh(MeshFilter meshFilter)
        {
            Mesh mesh = meshFilter.sharedMesh;
            Transform transform = meshFilter.transform;
            
            var vertices = mesh.vertices;
            var normals = mesh.normals;
            var triangles = mesh.triangles;
            
            float totalArea = 0f;
            for (int i = 0; i < triangles.Length; i += 3)
            {
                Vector3 v0 = transform.TransformPoint(vertices[triangles[i]]);
                Vector3 v1 = transform.TransformPoint(vertices[triangles[i + 1]]);
                Vector3 v2 = transform.TransformPoint(vertices[triangles[i + 2]]);
                totalArea += Vector3.Cross(v1 - v0, v2 - v0).magnitude * 0.5f;
            }
            
            int targetCount = Mathf.Min(Mathf.RoundToInt(totalArea * toolSettings.generationDensity * 100), toolSettings.maxGrassToGenerate);
            var grassList = targetRenderer.GrassDataList;
            int added = 0;
            
            for (int attempt = 0; attempt < targetCount * 3 && added < targetCount; attempt++)
            {
                int triIndex = Random.Range(0, triangles.Length / 3) * 3;
                
                Vector3 v0 = transform.TransformPoint(vertices[triangles[triIndex]]);
                Vector3 v1 = transform.TransformPoint(vertices[triangles[triIndex + 1]]);
                Vector3 v2 = transform.TransformPoint(vertices[triangles[triIndex + 2]]);
                
                float u = Random.value;
                float v = Random.value;
                if (u + v > 1f) { u = 1f - u; v = 1f - v; }
                Vector3 pos = v0 + u * (v1 - v0) + v * (v2 - v0);
                
                Vector3 n0 = transform.TransformDirection(normals[triangles[triIndex]]);
                Vector3 n1 = transform.TransformDirection(normals[triangles[triIndex + 1]]);
                Vector3 n2 = transform.TransformDirection(normals[triangles[triIndex + 2]]);
                Vector3 normal = (n0 + u * (n1 - n0) + v * (n2 - n0)).normalized;
                
                if (normal.y < toolSettings.normalLimit)
                    continue;
                
                // Determine if we're in custom mesh mode
                bool isCustomMeshMode = targetRenderer.settings != null && 
                                        targetRenderer.settings.grassMode == GrassMode.CustomMesh;
                
                // Get min/max values based on useCustomSize toggle
                float minWidth, maxWidth, minHeight, maxHeight, minSize, maxSize;
                if (toolSettings.useCustomSize)
                {
                    minWidth = toolSettings.minBladeWidth;
                    maxWidth = toolSettings.maxBladeWidth;
                    minHeight = toolSettings.minBladeHeight;
                    maxHeight = toolSettings.maxBladeHeight;
                    minSize = toolSettings.minBladeSize;
                    maxSize = toolSettings.maxBladeSize;
                }
                else
                {
                    var settings = targetRenderer.settings;
                    minWidth = settings.minWidth;
                    maxWidth = settings.maxWidth;
                    minHeight = settings.minHeight;
                    maxHeight = settings.maxHeight;
                    minSize = settings.minSize;
                    maxSize = settings.maxSize;
                }
                
                // Determine how many blades in this cluster
                int bladesInCluster = toolSettings.useClusterSpawning 
                    ? Random.Range(toolSettings.minBladesPerCluster, toolSettings.maxBladesPerCluster + 1)
                    : 1;
                
                for (int b = 0; b < bladesInCluster; b++)
                {
                    // Offset within cluster (first blade at center, rest spread out)
                    Vector3 bladePos = pos;
                    if (b > 0 && toolSettings.useClusterSpawning)
                    {
                        Vector2 clusterOffset = Random.insideUnitCircle * toolSettings.clusterRadius;
                        bladePos += new Vector3(clusterOffset.x, 0, clusterOffset.y);
                    }
                    
                    Color col = toolSettings.brushColor;
                    col.r += Random.Range(-toolSettings.colorVariationR, toolSettings.colorVariationR);
                    col.g += Random.Range(-toolSettings.colorVariationG, toolSettings.colorVariationG);
                    col.b += Random.Range(-toolSettings.colorVariationB, toolSettings.colorVariationB);
                    
                    // Use size for custom mesh mode, width/height for default mode
                    float width, height;
                    if (isCustomMeshMode)
                    {
                        width = Random.Range(minSize, maxSize);
                        height = 1f;
                    }
                    else
                    {
                        width = Random.Range(minWidth, maxWidth);
                        height = Random.Range(minHeight, maxHeight);
                    }
                    
                    GrassData data = new GrassData(bladePos, normal, width, height, col, 0f);
                    grassList.Add(data);
                }
                added++;
            }
            
            Debug.Log($"Generated {added} grass clusters ({grassList.Count} total blades) on {meshFilter.gameObject.name}");
        }
        
        private void ClearAllGrass()
        {
            if (targetRenderer == null) return;
            // Skipping Undo.RecordObject for performance - clearing large grass counts would be too slow
            targetRenderer.ClearGrass();
            
            // Defer SetDirty to next frame to prevent UI blocking
            var rendererToMark = targetRenderer;
            EditorApplication.delayCall += () =>
            {
                if (rendererToMark != null)
                {
                    EditorUtility.SetDirty(rendererToMark);
                    Undo.ClearUndo(rendererToMark);
                }
            };
        }
    }
    
    internal static class InternalEditorUtility
    {
        public static string[] layers => UnityEditorInternal.InternalEditorUtility.layers;
        public static int LayerMaskToConcatenatedLayersMask(LayerMask mask) => UnityEditorInternal.InternalEditorUtility.LayerMaskToConcatenatedLayersMask(mask);
        public static LayerMask ConcatenatedLayersMaskToLayerMask(int mask) => UnityEditorInternal.InternalEditorUtility.ConcatenatedLayersMaskToLayerMask(mask);
    }
}
