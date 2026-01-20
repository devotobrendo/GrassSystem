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
        
        private RaycastHit[] hitResults = new RaycastHit[10];
        private Vector3 lastPaintPos;
        private float minPaintDistance = 0.1f;
        
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
        }
        
        private void OnDisable()
        {
            SceneView.duringSceneGui -= OnSceneGUI;
        }
        
        private void LoadOrCreateSettings()
        {
            string path = "Assets/GrassSystem/Editor/GrassToolSettings.asset";
            toolSettings = AssetDatabase.LoadAssetAtPath<SO_GrassToolSettings>(path);
            
            if (toolSettings == null)
            {
                toolSettings = CreateInstance<SO_GrassToolSettings>();
                if (!AssetDatabase.IsValidFolder("Assets/GrassSystem/Editor"))
                    AssetDatabase.CreateFolder("Assets/GrassSystem", "Editor");
                AssetDatabase.CreateAsset(toolSettings, path);
                AssetDatabase.SaveAssets();
            }
        }
        
        private void OnGUI()
        {
            scrollPos = EditorGUILayout.BeginScrollView(scrollPos);
            
            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("Grass Painter", EditorStyles.boldLabel);
            EditorGUILayout.Space(5);
            
            EditorGUI.BeginChangeCheck();
            targetRenderer = (GrassRenderer)EditorGUILayout.ObjectField(
                "Target Renderer", targetRenderer, typeof(GrassRenderer), true);
            if (EditorGUI.EndChangeCheck() && targetRenderer == null)
                targetRenderer = FindAnyObjectByType<GrassRenderer>();
            
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
            EditorGUILayout.LabelField($"Total Grass: {grassCount:N0}", EditorStyles.boldLabel);
            
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
            
            if (GUILayout.Button("Rebuild Buffers"))
                targetRenderer.RebuildBuffers();
            
            EditorGUILayout.Space(10);
            EditorGUILayout.HelpBox(
                "Hold Right Mouse Button in Scene View to paint.\n" +
                "Shift + RMB to remove.\n" +
                "Scroll wheel to adjust brush size.",
                MessageType.Info);
            
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
            toolSettings.brushSize = EditorGUILayout.Slider("Brush Size", toolSettings.brushSize, 0.1f, 50f);
            
            if (currentMode == PaintMode.Add)
            {
                toolSettings.density = EditorGUILayout.Slider("Density", toolSettings.density, 0.1f, 10f);
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
                        toolSettings.minBladeSize = EditorGUILayout.Slider("Min Size", toolSettings.minBladeSize, 0.1f, 3f);
                        toolSettings.maxBladeSize = EditorGUILayout.Slider("Max Size", toolSettings.maxBladeSize, 0.1f, 3f);
                    }
                    else
                    {
                        var settings = targetRenderer.settings;
                        EditorGUI.BeginDisabledGroup(true);
                        EditorGUILayout.Slider("Min Size (from Settings)", settings.minSize, 0.1f, 3f);
                        EditorGUILayout.Slider("Max Size (from Settings)", settings.maxSize, 0.1f, 3f);
                        EditorGUI.EndDisabledGroup();
                        EditorGUILayout.HelpBox("Enable 'Use Custom Size' to override.", MessageType.Info);
                    }
                }
                else
                {
                    EditorGUILayout.LabelField("Blade Dimensions", EditorStyles.boldLabel);
                    if (toolSettings.useCustomSize)
                    {
                        toolSettings.minBladeWidth = EditorGUILayout.Slider("Min Width", toolSettings.minBladeWidth, 0.01f, 0.5f);
                        toolSettings.maxBladeWidth = EditorGUILayout.Slider("Max Width", toolSettings.maxBladeWidth, 0.01f, 0.5f);
                        toolSettings.minBladeHeight = EditorGUILayout.Slider("Min Height", toolSettings.minBladeHeight, 0.1f, 2f);
                        toolSettings.maxBladeHeight = EditorGUILayout.Slider("Max Height", toolSettings.maxBladeHeight, 0.1f, 2f);
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
                EditorGUILayout.LabelField("Color", EditorStyles.boldLabel);
                
                // Show color options only if not using only albedo color
                bool useOnlyAlbedo = isCustomMeshMode && targetRenderer.settings.useOnlyAlbedoColor;
                if (useOnlyAlbedo)
                {
                    EditorGUILayout.HelpBox("Color settings disabled: 'Use Only Albedo Color' is enabled in settings.", MessageType.Info);
                }
                else
                {
                    toolSettings.brushColor = EditorGUILayout.ColorField("Base Color", toolSettings.brushColor);
                    toolSettings.colorVariationR = EditorGUILayout.Slider("Red Variation", toolSettings.colorVariationR, 0f, 0.3f);
                    toolSettings.colorVariationG = EditorGUILayout.Slider("Green Variation", toolSettings.colorVariationG, 0f, 0.3f);
                    toolSettings.colorVariationB = EditorGUILayout.Slider("Blue Variation", toolSettings.colorVariationB, 0f, 0.3f);
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
                    toolSettings.minBladesPerCluster = EditorGUILayout.IntSlider("Min Blades", toolSettings.minBladesPerCluster, 1, 10);
                    toolSettings.maxBladesPerCluster = EditorGUILayout.IntSlider("Max Blades", toolSettings.maxBladesPerCluster, 1, 10);
                    toolSettings.clusterRadius = EditorGUILayout.Slider("Cluster Radius", toolSettings.clusterRadius, 0.01f, 0.5f);
                    EditorGUI.indentLevel--;
                }
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
                    toolSettings.heightBrushValue = EditorGUILayout.Slider("Height Value", toolSettings.heightBrushValue, 0.1f, 2f);
                }
            }
            else if (currentMode == PaintMode.Pattern)
            {
                toolSettings.patternBrushValue = EditorGUILayout.Slider("Pattern Value (0=A, 1=B)", toolSettings.patternBrushValue, 0f, 1f);
            }
            else if (currentMode == PaintMode.Color)
            {
                EditorGUILayout.Space(5);
                EditorGUILayout.LabelField("Color Brush", EditorStyles.boldLabel);
                toolSettings.brushColor = EditorGUILayout.ColorField("Target Color", toolSettings.brushColor);
                EditorGUILayout.HelpBox("Paint this color onto existing grass blades.", MessageType.Info);
                
                EditorGUILayout.Space(5);
                if (GUILayout.Button("Reset Color (paint white)"))
                    toolSettings.brushColor = Color.white;
            }
        }
        
        private void OnSceneGUI(SceneView sceneView)
        {
            if (targetRenderer == null || toolSettings == null)
                return;
            
            Event e = Event.current;
            
            if (e.type == EventType.ScrollWheel && e.control)
            {
                toolSettings.brushSize = Mathf.Clamp(toolSettings.brushSize - e.delta.y * 0.5f, 0.1f, 50f);
                e.Use();
                Repaint();
            }
            
            Ray ray = HandleUtility.GUIPointToWorldRay(e.mousePosition);
            int hitCount = Physics.RaycastNonAlloc(ray, hitResults, 500f, toolSettings.paintMask);
            
            if (hitCount == 0) return;
            
            Vector3 hitPoint = hitResults[0].point;
            Vector3 hitNormal = hitResults[0].normal;
            
            Handles.color = GetBrushColor();
            Handles.DrawWireDisc(hitPoint, hitNormal, toolSettings.brushSize);
            Handles.color = new Color(Handles.color.r, Handles.color.g, Handles.color.b, 0.2f);
            Handles.DrawSolidDisc(hitPoint, hitNormal, toolSettings.brushSize);
            
            if (e.type == EventType.MouseDown && e.button == 1)
            {
                isPainting = true;
                lastPaintPos = hitPoint;
                e.Use();
            }
            else if (e.type == EventType.MouseUp && e.button == 1)
            {
                isPainting = false;
                e.Use();
                targetRenderer.RebuildBuffers();
                EditorUtility.SetDirty(targetRenderer);
            }
            
            if (isPainting && e.type == EventType.MouseDrag && e.button == 1)
            {
                if (Vector3.Distance(hitPoint, lastPaintPos) > minPaintDistance)
                {
                    PaintMode mode = e.shift ? PaintMode.Remove : currentMode;
                    Paint(hitPoint, hitNormal, mode);
                    lastPaintPos = hitPoint;
                }
                e.Use();
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
            
            var grassList = targetRenderer.GrassDataList;
            
            switch (mode)
            {
                case PaintMode.Add: AddGrass(position, normal, grassList); break;
                case PaintMode.Remove: RemoveGrass(position, grassList); break;
                case PaintMode.Height: EditHeight(position, grassList); break;
                case PaintMode.Pattern: EditPattern(position, grassList); break;
                case PaintMode.Color: EditColor(position, grassList); break;
            }
            
            Undo.RecordObject(targetRenderer, "Paint Grass");
        }
        
        private void AddGrass(Vector3 center, Vector3 normal, List<GrassData> grassList)
        {
            int count = Mathf.RoundToInt(toolSettings.brushSize * toolSettings.density);
            
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
        
        private void RemoveGrass(Vector3 center, List<GrassData> grassList)
        {
            float sqrRadius = toolSettings.brushSize * toolSettings.brushSize;
            
            for (int i = grassList.Count - 1; i >= 0; i--)
            {
                Vector3 diff = grassList[i].position - center;
                diff.y = 0;
                if (diff.sqrMagnitude < sqrRadius)
                    grassList.RemoveAt(i);
            }
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
            EditorUtility.SetDirty(targetRenderer);
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
            Undo.RecordObject(targetRenderer, "Clear Grass");
            targetRenderer.ClearGrass();
            EditorUtility.SetDirty(targetRenderer);
        }
    }
    
    internal static class InternalEditorUtility
    {
        public static string[] layers => UnityEditorInternal.InternalEditorUtility.layers;
        public static int LayerMaskToConcatenatedLayersMask(LayerMask mask) => UnityEditorInternal.InternalEditorUtility.LayerMaskToConcatenatedLayersMask(mask);
        public static LayerMask ConcatenatedLayersMaskToLayerMask(int mask) => UnityEditorInternal.InternalEditorUtility.ConcatenatedLayersMaskToLayerMask(mask);
    }
}
