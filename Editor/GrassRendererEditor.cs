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
        private SerializedProperty externalDataAsset;
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
            externalDataAsset = serializedObject.FindProperty("externalDataAsset");
            
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
            
            // === EXTERNAL DATA STORAGE ===
            EditorGUILayout.LabelField("Data Storage", EditorStyles.boldLabel);
            
            EditorGUILayout.PropertyField(externalDataAsset, new GUIContent("External Data Asset", 
                "Store grass data in an external asset instead of the scene. Recommended for large grass counts."));
            
            if (renderer.externalDataAsset != null)
            {
                // Show external asset info
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.LabelField($"Asset: {renderer.externalDataAsset.name}", EditorStyles.miniLabel);
                EditorGUILayout.LabelField($"Asset Instances: {renderer.externalDataAsset.InstanceCount:N0}", EditorStyles.miniLabel);
                EditorGUILayout.LabelField($"Rendered Instances: {cachedGrassCount:N0}", EditorStyles.miniLabel);
                if (!string.IsNullOrEmpty(renderer.externalDataAsset.LastSaveTime))
                    EditorGUILayout.LabelField($"Last Saved: {renderer.externalDataAsset.LastSaveTime}", EditorStyles.miniLabel);
                EditorGUILayout.EndVertical();
                // Determine current state
                bool assetHasData = renderer.externalDataAsset.InstanceCount > 0;
                bool rendererHasData = cachedGrassCount > 0;
                
                // === STATE 1: Asset has data, Renderer is empty → Offer to Load ===
                if (assetHasData && !rendererHasData)
                {
                    EditorGUILayout.HelpBox(
                        $"External asset has {renderer.externalDataAsset.InstanceCount:N0} grass instances but renderer is empty.\n" +
                        "Click 'Load from Asset' to display the grass.", 
                        MessageType.Warning);
                    
                    GUI.backgroundColor = Color.green;
                    if (GUILayout.Button("▶ Load from Asset", GUILayout.Height(30)))
                    {
                        renderer.LoadFromExternalAsset();
                        UpdateCachedCount();
                    }
                    GUI.backgroundColor = Color.white;
                }
                // === STATE 2: Asset is empty, Renderer has data → Offer to Migrate ===
                else if (!assetHasData && rendererHasData)
                {
                    EditorGUILayout.HelpBox(
                        $"Renderer has {cachedGrassCount:N0} grass instances but external asset is empty.\n" +
                        "Click 'Migrate to Asset' to save data to the external asset.", 
                        MessageType.Warning);
                    
                    GUI.backgroundColor = new Color(1f, 0.8f, 0.2f); // Yellow
                    if (GUILayout.Button("📤 Migrate to Asset", GUILayout.Height(30)))
                    {
                        renderer.SaveToExternalAsset();
                        AssetDatabase.SaveAssets();
                        Debug.Log($"Migrated {cachedGrassCount:N0} grass instances to external asset.", renderer.externalDataAsset);
                    }
                    GUI.backgroundColor = Color.white;
                }
                // === STATE 3: Both have data or asset has data → Normal operation ===
                else if (assetHasData || rendererHasData)
                {
                    EditorGUILayout.BeginHorizontal();
                    if (GUILayout.Button("💾 Save + Backup"))
                    {
                        // Create backup BEFORE saving (keeps last good state)
                        renderer.externalDataAsset.CreateBackup();
                        renderer.SaveToExternalAsset();
                        AssetDatabase.SaveAssets();
                        // Force refresh material after save to prevent color reset
                        renderer.ForceReinitialize();
                    }
                    if (GUILayout.Button("📂 Load from Asset"))
                    {
                        if (EditorUtility.DisplayDialog("Load Grass Data", 
                            "This will replace current grass data with data from the asset. Continue?", 
                            "Yes, Load", "Cancel"))
                        {
                            renderer.LoadFromExternalAsset();
                            UpdateCachedCount();
                        }
                    }
                    EditorGUILayout.EndHorizontal();
                    
                    // Show backup info and restore option
                    if (renderer.externalDataAsset.HasBackup())
                    {
                        EditorGUILayout.BeginHorizontal();
                        EditorGUILayout.LabelField(renderer.externalDataAsset.GetBackupInfo(), EditorStyles.miniLabel);
                        if (GUILayout.Button("🔄 Restore from Backup", GUILayout.Width(140)))
                        {
                            if (EditorUtility.DisplayDialog("Restore from Backup", 
                                "This will restore grass data from the last backup. Use if data is corrupted.", 
                                "Yes, Restore", "Cancel"))
                            {
                                if (renderer.externalDataAsset.RestoreFromBackup())
                                {
                                    renderer.LoadFromExternalAsset();
                                    UpdateCachedCount();
                                }
                            }
                        }
                        EditorGUILayout.EndHorizontal();
                    }
                }
                // === STATE 4: Both are empty → Show error with recovery options ===
                else
                {
                    EditorGUILayout.HelpBox(
                        "⚠️ Both external asset and renderer are empty!\n\n" +
                        "This can happen if the scene was saved with the new serialization format " +
                        "before the asset was populated.\n\n" +
                        "Recovery options:\n" +
                        "• If a JSON backup exists, restore from it\n" +
                        "• Undo recent changes (Ctrl+Z)\n" +
                        "• Re-paint the grass", 
                        MessageType.Error);
                    
                    // Check for backup and offer restore
                    if (renderer.externalDataAsset.HasBackup())
                    {
                        GUI.backgroundColor = Color.green;
                        if (GUILayout.Button("🔄 Restore from Backup", GUILayout.Height(30)))
                        {
                            if (renderer.externalDataAsset.RestoreFromBackup())
                            {
                                renderer.LoadFromExternalAsset();
                                UpdateCachedCount();
                                Debug.Log("Successfully restored grass data from backup!", renderer);
                            }
                        }
                        GUI.backgroundColor = Color.white;
                        EditorGUILayout.LabelField(renderer.externalDataAsset.GetBackupInfo(), EditorStyles.miniLabel);
                    }
                    else
                    {
                        EditorGUILayout.HelpBox("No backup file found. You may need to re-paint the grass.", MessageType.Warning);
                    }
                }
            }
            else
            {
                if (GUILayout.Button("✨ Create New Data Asset"))
                {
                    CreateNewDataAsset(renderer);
                }
                
                if (cachedGrassCount > 1000)
                {
                    EditorGUILayout.HelpBox(
                        $"You have {cachedGrassCount:N0} grass instances stored in the scene. " +
                        "Consider using an external data asset for better performance.", 
                        MessageType.Info);
                }
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
            // Force Reinitialize now includes recovery from external asset - replaces old Rebuild Buffers
            if (GUILayout.Button("🔧 Force Reinitialize"))
            {
                renderer.ForceReinitialize();
                UpdateCachedCount();
            }
            if (GUILayout.Button("🔍 Diagnose Issues"))
            {
                renderer.DiagnoseRenderingIssues();
            }
            EditorGUILayout.EndHorizontal();
            
            if (GUILayout.Button("🗑 Clear All Grass"))
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
        
        private void CreateNewDataAsset(GrassRenderer renderer)
        {
            // Create GrassData folder if it doesn't exist
            string folderPath = "Assets/GrassData";
            if (!AssetDatabase.IsValidFolder(folderPath))
            {
                AssetDatabase.CreateFolder("Assets", "GrassData");
            }
            
            // Generate unique name based on scene and renderer
            string sceneName = renderer.gameObject.scene.name;
            if (string.IsNullOrEmpty(sceneName)) sceneName = "Untitled";
            string baseName = $"GrassData_{sceneName}_{renderer.gameObject.name}";
            string assetPath = AssetDatabase.GenerateUniqueAssetPath($"{folderPath}/{baseName}.asset");
            
            // Create the asset
            var asset = ScriptableObject.CreateInstance<GrassDataAsset>();
            AssetDatabase.CreateAsset(asset, assetPath);
            
            // Assign to renderer
            Undo.RecordObject(renderer, "Assign External Data Asset");
            renderer.externalDataAsset = asset;
            EditorUtility.SetDirty(renderer);
            
            // If renderer has existing grass data, save it to the new asset
            if (renderer.GrassDataList != null && renderer.GrassDataList.Count > 0)
            {
                renderer.SaveToExternalAsset();
            }
            
            AssetDatabase.SaveAssets();
            
            // Ping the asset in Project window
            EditorGUIUtility.PingObject(asset);
            
            Debug.Log($"Created new grass data asset: {assetPath}", asset);
        }
    }
}

