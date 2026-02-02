// Copyright (c) 2026 Brendo Otavio Carvalho de Matos. All rights reserved.

using UnityEditor;
using UnityEngine;

namespace GrassSystem
{
    /// <summary>
    /// Custom Editor for GrassRenderer that prevents Inspector lag
    /// by avoiding serialization of the massive grassData list.
    /// </summary>
    [CustomEditor(typeof(GrassRenderer))]
    public class GrassRendererEditor : Editor
    {
        private SerializedProperty settings;
        private bool showDebugInfo = false;
        
        // Cached values to avoid accessing large list every frame
        private int cachedGrassCount = 0;
        private double lastCountUpdateTime = 0;
        private const double COUNT_UPDATE_INTERVAL = 0.5; // Update count every 0.5 seconds
        
        private void OnEnable()
        {
            // Only cache the settings property - we intentionally skip grassData
            // to avoid performance issues with large grass counts
            settings = serializedObject.FindProperty("settings");
            
            // Initial count
            UpdateCachedCount();
        }
        
        // Prevent constant Inspector repainting which causes lag
        public override bool RequiresConstantRepaint() => false;
        
        private void UpdateCachedCount()
        {
            var renderer = (GrassRenderer)target;
            if (renderer != null && renderer.GrassDataList != null)
            {
                cachedGrassCount = renderer.GrassDataList.Count;
            }
            lastCountUpdateTime = EditorApplication.timeSinceStartup;
        }
        
        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            
            var renderer = (GrassRenderer)target;
            
            // Update cached count periodically (throttled to avoid lag)
            if (EditorApplication.timeSinceStartup - lastCountUpdateTime > COUNT_UPDATE_INTERVAL)
            {
                UpdateCachedCount();
            }
            
            // === SETTINGS ===
            EditorGUILayout.PropertyField(settings, new GUIContent("Grass Settings"));
            
            if (renderer.settings == null)
            {
                EditorGUILayout.HelpBox("Please assign a Grass Settings asset.", MessageType.Warning);
            }
            
            EditorGUILayout.Space(10);
            
            // === STATISTICS (cached to avoid lag) ===
            EditorGUILayout.LabelField("Statistics", EditorStyles.boldLabel);
            
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Grass Instances", cachedGrassCount.ToString("N0"));
            if (GUILayout.Button("↻", GUILayout.Width(25)))
            {
                UpdateCachedCount();
            }
            EditorGUILayout.EndHorizontal();
            
            EditorGUILayout.LabelField("Visible Count", renderer.VisibleGrassCount.ToString("N0"));
            
            // Memory estimate
            float memoryKB = (cachedGrassCount * 48) / 1024f; // 48 bytes per GrassData
            EditorGUILayout.LabelField("Estimated Memory", $"{memoryKB:F1} KB");
            
            EditorGUILayout.Space(10);
            
            // === ACTIONS ===
            EditorGUILayout.LabelField("Actions", EditorStyles.boldLabel);
            
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Rebuild Buffers"))
            {
                renderer.RebuildBuffers();
                UpdateCachedCount();
            }
            if (GUILayout.Button("Force Reinitialize"))
            {
                renderer.ForceReinitialize();
                UpdateCachedCount();
            }
            EditorGUILayout.EndHorizontal();
            
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Diagnose Issues"))
            {
                renderer.DiagnoseRenderingIssues();
            }
            if (GUILayout.Button("Clear All Grass"))
            {
                if (EditorUtility.DisplayDialog("Clear Grass", 
                    $"Are you sure you want to delete {cachedGrassCount:N0} grass instances?", 
                    "Yes, Clear All", "Cancel"))
                {
                    renderer.ClearGrass();
                    EditorUtility.SetDirty(renderer);
                    UpdateCachedCount();
                }
            }
            EditorGUILayout.EndHorizontal();
            
            EditorGUILayout.Space(10);
            
            // === DEBUG INFO (collapsible) ===
            showDebugInfo = EditorGUILayout.Foldout(showDebugInfo, "Debug Information", true);
            if (showDebugInfo)
            {
                EditorGUI.indentLevel++;
                
                // Show buffer status using cached count
                EditorGUILayout.LabelField("Has Grass Data", cachedGrassCount > 0 ? "Yes" : "No");
                EditorGUILayout.LabelField("Material Instance", renderer.MaterialInstance != null ? "Valid" : "Null");
                
                if (renderer.settings != null)
                {
                    EditorGUILayout.LabelField("Grass Mode", renderer.settings.grassMode.ToString());
                    EditorGUILayout.LabelField("Color Mode", renderer.settings.colorMode.ToString());
                }
                
                EditorGUI.indentLevel--;
            }
            
            serializedObject.ApplyModifiedProperties();
        }
    }
}

