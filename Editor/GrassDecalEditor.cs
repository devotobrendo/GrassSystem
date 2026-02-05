// Copyright (c) 2026 Brendo Otavio Carvalho de Matos. All rights reserved.

using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace GrassSystem
{
    [CustomEditor(typeof(GrassDecal))]
    public class GrassDecalEditor : Editor
    {
        private GrassRenderer[] sceneRenderers;
        private string[] rendererNames;
        private bool showTargetList = true;
        
        private SerializedProperty decalTexture;
        private SerializedProperty size;
        private SerializedProperty rotation;
        private SerializedProperty blend;
        private SerializedProperty layer;
        private SerializedProperty blendMode;
        private SerializedProperty targetRenderers;
        private SerializedProperty autoFindAll;
        
        private void OnEnable()
        {
            decalTexture = serializedObject.FindProperty("decalTexture");
            size = serializedObject.FindProperty("size");
            rotation = serializedObject.FindProperty("rotation");
            blend = serializedObject.FindProperty("blend");
            layer = serializedObject.FindProperty("layer");
            blendMode = serializedObject.FindProperty("blendMode");
            targetRenderers = serializedObject.FindProperty("targetRenderers");
            autoFindAll = serializedObject.FindProperty("autoFindAll");
            
            RefreshSceneRenderers();
        }
        
        private void RefreshSceneRenderers()
        {
            sceneRenderers = FindObjectsByType<GrassRenderer>(FindObjectsSortMode.None);
            rendererNames = new string[sceneRenderers.Length];
            
            for (int i = 0; i < sceneRenderers.Length; i++)
            {
                var renderer = sceneRenderers[i];
                string settingsName = renderer.settings != null ? renderer.settings.name : "No Settings";
                rendererNames[i] = $"{renderer.name} ({settingsName})";
            }
        }
        
        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            
            var decal = (GrassDecal)target;
            
            // === DECAL SETTINGS ===
            EditorGUILayout.LabelField("Decal Settings", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(decalTexture);
            EditorGUILayout.PropertyField(size);
            EditorGUILayout.PropertyField(rotation, new GUIContent("Rotation", "Rotation of the decal texture in degrees (0-360)"));
            EditorGUILayout.PropertyField(blend);
            
            EditorGUILayout.Space(5);
            EditorGUILayout.PropertyField(layer, new GUIContent("Layer", "Higher layers override lower layers in overlapping areas"));
            EditorGUILayout.PropertyField(blendMode, new GUIContent("Blend Mode", "How the decal color is combined with the base color"));
            
            EditorGUILayout.Space(10);
            
            // === TARGET SELECTION ===
            EditorGUILayout.LabelField("Target Renderers", EditorStyles.boldLabel);
            
            EditorGUILayout.PropertyField(autoFindAll, new GUIContent("Auto-Target All", "Automatically apply decal to all GrassRenderers in the scene"));
            
            if (decal.autoFindAll)
            {
                EditorGUILayout.HelpBox($"Decal will be applied to all {sceneRenderers.Length} GrassRenderer(s) in the scene.", MessageType.Info);
                
                if (GUILayout.Button("Refresh Scene Renderers"))
                {
                    RefreshSceneRenderers();
                    decal.RefreshAutoFind();
                }
            }
            else
            {
                EditorGUILayout.Space(5);
                
                // Add from dropdown
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.PrefixLabel("Add Renderer");
                
                if (sceneRenderers.Length > 0)
                {
                    int selectedIndex = EditorGUILayout.Popup(-1, rendererNames);
                    if (selectedIndex >= 0 && selectedIndex < sceneRenderers.Length)
                    {
                        var renderer = sceneRenderers[selectedIndex];
                        if (!decal.targetRenderers.Contains(renderer))
                        {
                            Undo.RecordObject(decal, "Add Target Renderer");
                            decal.targetRenderers.Add(renderer);
                            EditorUtility.SetDirty(decal);
                        }
                    }
                }
                else
                {
                    EditorGUILayout.LabelField("No GrassRenderers in scene", EditorStyles.miniLabel);
                }
                EditorGUILayout.EndHorizontal();
                
                // Quick buttons
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("Add All", GUILayout.Width(70)))
                {
                    Undo.RecordObject(decal, "Add All Renderers");
                    foreach (var renderer in sceneRenderers)
                    {
                        if (!decal.targetRenderers.Contains(renderer))
                            decal.targetRenderers.Add(renderer);
                    }
                    EditorUtility.SetDirty(decal);
                }
                if (GUILayout.Button("Clear All", GUILayout.Width(70)))
                {
                    Undo.RecordObject(decal, "Clear All Renderers");
                    decal.targetRenderers.Clear();
                    EditorUtility.SetDirty(decal);
                }
                if (GUILayout.Button("Refresh List", GUILayout.Width(80)))
                {
                    RefreshSceneRenderers();
                }
                EditorGUILayout.EndHorizontal();
                
                EditorGUILayout.Space(5);
                
                // Show current targets with remove buttons
                showTargetList = EditorGUILayout.Foldout(showTargetList, $"Current Targets ({decal.targetRenderers.Count})", true);
                if (showTargetList)
                {
                    EditorGUI.indentLevel++;
                    
                    for (int i = decal.targetRenderers.Count - 1; i >= 0; i--)
                    {
                        var renderer = decal.targetRenderers[i];
                        EditorGUILayout.BeginHorizontal();
                        
                        if (renderer != null)
                        {
                            string settingsName = renderer.settings != null ? renderer.settings.name : "No Settings";
                            EditorGUILayout.LabelField($"{renderer.name} ({settingsName})");
                            
                            if (GUILayout.Button("Select", GUILayout.Width(50)))
                            {
                                Selection.activeGameObject = renderer.gameObject;
                            }
                            if (GUILayout.Button("X", GUILayout.Width(25)))
                            {
                                Undo.RecordObject(decal, "Remove Target Renderer");
                                decal.targetRenderers.RemoveAt(i);
                                EditorUtility.SetDirty(decal);
                            }
                        }
                        else
                        {
                            EditorGUILayout.LabelField("(null - will be removed)", EditorStyles.miniLabel);
                            decal.targetRenderers.RemoveAt(i);
                        }
                        
                        EditorGUILayout.EndHorizontal();
                    }
                    
                    if (decal.targetRenderers.Count == 0)
                    {
                        EditorGUILayout.HelpBox("No targets selected. Add renderers from the dropdown above.", MessageType.Warning);
                    }
                    
                    EditorGUI.indentLevel--;
                }
            }
            
            EditorGUILayout.Space(10);
            
            // === ACTIONS ===
            EditorGUILayout.LabelField("Actions", EditorStyles.boldLabel);
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Apply Now"))
            {
                decal.ApplyDecalToAll();
            }
            if (GUILayout.Button("Clear Decal"))
            {
                decal.ClearDecalFromAll();
            }
            EditorGUILayout.EndHorizontal();
            
            serializedObject.ApplyModifiedProperties();
        }
    }
}
