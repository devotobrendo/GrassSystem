// Copyright (c) 2026 Brendo Otavio Carvalho de Matos. All rights reserved.

using UnityEditor;
using UnityEngine;

namespace GrassSystem
{
    [CustomEditor(typeof(SO_GrassSettings))]
    public class SO_GrassSettingsEditor : Editor
    {
        // Serialized properties
        private SerializedProperty grassMode;
        
        // References
        private SerializedProperty cullingShader;
        private SerializedProperty grassMaterial;
        private SerializedProperty grassMesh;
        
        // Custom Mesh Settings
        private SerializedProperty customMeshes;
        private SerializedProperty minSize;
        private SerializedProperty maxSize;
        private SerializedProperty useOnlyAlbedoColor;
        private SerializedProperty meshRotationOffset;
        
        // Blade Dimensions (Default mode)
        private SerializedProperty minWidth;
        private SerializedProperty maxWidth;
        private SerializedProperty minHeight;
        private SerializedProperty maxHeight;
        
        // Wind
        private SerializedProperty windSpeed;
        private SerializedProperty windStrength;
        private SerializedProperty windFrequency;
        
        // LOD & Culling
        private SerializedProperty minFadeDistance;
        private SerializedProperty maxDrawDistance;
        private SerializedProperty cullingTreeDepth;
        
        // Checkered Pattern
        private SerializedProperty useCheckeredPattern;
        private SerializedProperty patternColorA;
        private SerializedProperty patternColorB;
        private SerializedProperty patternScale;
        
        // Tip Customization
        private SerializedProperty useTipCutout;
        private SerializedProperty tipMaskTexture;
        private SerializedProperty tipCutoffHeight;
        
        // Textures
        private SerializedProperty albedoTexture;
        private SerializedProperty normalMap;
        
        // Lighting
        private SerializedProperty topTint;
        private SerializedProperty bottomTint;
        private SerializedProperty translucency;
        private SerializedProperty useAlignedNormals;
        
        // Terrain Lightmap
        private SerializedProperty useTerrainLightmap;
        private SerializedProperty terrain;
        private SerializedProperty terrainLightmapInfluence;
        
        // Interaction
        private SerializedProperty interactorStrength;
        private SerializedProperty maxInteractors;
        
        // Rendering
        private SerializedProperty castShadows;
        private SerializedProperty receiveShadows;
        
        // Debug
        private SerializedProperty drawCullingBounds;
        
        private void OnEnable()
        {
            grassMode = serializedObject.FindProperty("grassMode");
            
            cullingShader = serializedObject.FindProperty("cullingShader");
            grassMaterial = serializedObject.FindProperty("grassMaterial");
            grassMesh = serializedObject.FindProperty("grassMesh");
            
            customMeshes = serializedObject.FindProperty("customMeshes");
            minSize = serializedObject.FindProperty("minSize");
            maxSize = serializedObject.FindProperty("maxSize");
            useOnlyAlbedoColor = serializedObject.FindProperty("useOnlyAlbedoColor");
            meshRotationOffset = serializedObject.FindProperty("meshRotationOffset");
            
            minWidth = serializedObject.FindProperty("minWidth");
            maxWidth = serializedObject.FindProperty("maxWidth");
            minHeight = serializedObject.FindProperty("minHeight");
            maxHeight = serializedObject.FindProperty("maxHeight");
            
            windSpeed = serializedObject.FindProperty("windSpeed");
            windStrength = serializedObject.FindProperty("windStrength");
            windFrequency = serializedObject.FindProperty("windFrequency");
            
            minFadeDistance = serializedObject.FindProperty("minFadeDistance");
            maxDrawDistance = serializedObject.FindProperty("maxDrawDistance");
            cullingTreeDepth = serializedObject.FindProperty("cullingTreeDepth");
            
            useCheckeredPattern = serializedObject.FindProperty("useCheckeredPattern");
            patternColorA = serializedObject.FindProperty("patternColorA");
            patternColorB = serializedObject.FindProperty("patternColorB");
            patternScale = serializedObject.FindProperty("patternScale");
            
            useTipCutout = serializedObject.FindProperty("useTipCutout");
            tipMaskTexture = serializedObject.FindProperty("tipMaskTexture");
            tipCutoffHeight = serializedObject.FindProperty("tipCutoffHeight");
            
            albedoTexture = serializedObject.FindProperty("albedoTexture");
            normalMap = serializedObject.FindProperty("normalMap");
            
            topTint = serializedObject.FindProperty("topTint");
            bottomTint = serializedObject.FindProperty("bottomTint");
            translucency = serializedObject.FindProperty("translucency");
            useAlignedNormals = serializedObject.FindProperty("useAlignedNormals");
            
            useTerrainLightmap = serializedObject.FindProperty("useTerrainLightmap");
            terrain = serializedObject.FindProperty("terrain");
            terrainLightmapInfluence = serializedObject.FindProperty("terrainLightmapInfluence");
            
            interactorStrength = serializedObject.FindProperty("interactorStrength");
            maxInteractors = serializedObject.FindProperty("maxInteractors");
            
            castShadows = serializedObject.FindProperty("castShadows");
            receiveShadows = serializedObject.FindProperty("receiveShadows");
            
            drawCullingBounds = serializedObject.FindProperty("drawCullingBounds");
        }
        
        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            
            var settings = (SO_GrassSettings)target;
            bool isCustomMeshMode = settings.grassMode == GrassMode.CustomMesh;
            
            // === GRASS MODE ===
            EditorGUILayout.Space(5);
            EditorGUILayout.LabelField("Grass Mode", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(grassMode);
            
            EditorGUILayout.HelpBox(
                isCustomMeshMode 
                    ? "Custom Mesh Mode: Use imported meshes with uniform scale."
                    : "Default Mode: Use procedural mesh with width/height control.",
                MessageType.Info);
            
            EditorGUILayout.Space(10);
            
            // === REFERENCES ===
            EditorGUILayout.LabelField("References", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(cullingShader);
            EditorGUILayout.PropertyField(grassMaterial);
            
            EditorGUILayout.Space(10);
            
            // === MODE-SPECIFIC SETTINGS ===
            if (isCustomMeshMode)
            {
                EditorGUILayout.LabelField("Custom Mesh Settings", EditorStyles.boldLabel);
                EditorGUILayout.PropertyField(customMeshes, true);
                EditorGUILayout.PropertyField(minSize);
                EditorGUILayout.PropertyField(maxSize);
                EditorGUILayout.PropertyField(meshRotationOffset, new GUIContent("Rotation Offset (Degrees)"));
                EditorGUILayout.PropertyField(useOnlyAlbedoColor, new GUIContent("Use Only Albedo Color"));
                
                if (settings.useOnlyAlbedoColor)
                {
                    EditorGUILayout.HelpBox("Tints and grass color will be ignored. Only albedo texture color will be used.", MessageType.Info);
                }
                
                EditorGUILayout.Space(10);
                
                // Textures only for Custom Mesh mode
                EditorGUILayout.LabelField("Textures", EditorStyles.boldLabel);
                EditorGUILayout.PropertyField(albedoTexture);
                EditorGUILayout.PropertyField(normalMap);
            }
            else
            {
                // Default mode: simple procedural blade, no textures needed
                EditorGUILayout.LabelField("Blade Dimensions", EditorStyles.boldLabel);
                EditorGUILayout.HelpBox("Procedural Zelda-style triangular blade (no texture needed)", MessageType.None);
                EditorGUILayout.PropertyField(minWidth);
                EditorGUILayout.PropertyField(maxWidth);
                EditorGUILayout.PropertyField(minHeight);
                EditorGUILayout.PropertyField(maxHeight);
            }
            
            EditorGUILayout.Space(10);
            
            // === LIGHTING ===
            EditorGUILayout.LabelField("Lighting", EditorStyles.boldLabel);
            
            // Only show tints if not using only albedo color
            if (!isCustomMeshMode || !settings.useOnlyAlbedoColor)
            {
                EditorGUILayout.PropertyField(topTint);
                EditorGUILayout.PropertyField(bottomTint);
            }
            
            EditorGUILayout.PropertyField(translucency);
            EditorGUILayout.PropertyField(useAlignedNormals);
            
            EditorGUILayout.Space(10);
            
            // === WIND ===
            EditorGUILayout.LabelField("Wind Settings", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(windSpeed);
            EditorGUILayout.PropertyField(windStrength);
            EditorGUILayout.PropertyField(windFrequency);
            
            EditorGUILayout.Space(10);
            
            // === LOD & CULLING ===
            EditorGUILayout.LabelField("LOD & Culling", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(minFadeDistance);
            EditorGUILayout.PropertyField(maxDrawDistance);
            EditorGUILayout.PropertyField(cullingTreeDepth);
            
            EditorGUILayout.Space(10);
            
            // === CHECKERED PATTERN ===
            EditorGUILayout.LabelField("Checkered Pattern", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(useCheckeredPattern);
            if (settings.useCheckeredPattern)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(patternColorA);
                EditorGUILayout.PropertyField(patternColorB);
                EditorGUILayout.PropertyField(patternScale);
                EditorGUI.indentLevel--;
            }
            
            EditorGUILayout.Space(10);
            
            // === TIP CUSTOMIZATION ===
            EditorGUILayout.LabelField("Tip Customization", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(useTipCutout);
            if (settings.useTipCutout)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(tipMaskTexture);
                EditorGUILayout.PropertyField(tipCutoffHeight);
                EditorGUI.indentLevel--;
            }
            
            EditorGUILayout.Space(10);
            
            // === TERRAIN LIGHTMAP ===
            EditorGUILayout.LabelField("Terrain Lightmap Blending", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(useTerrainLightmap);
            if (settings.useTerrainLightmap)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(terrain);
                EditorGUILayout.PropertyField(terrainLightmapInfluence);
                EditorGUI.indentLevel--;
            }
            
            EditorGUILayout.Space(10);
            
            // === INTERACTION ===
            EditorGUILayout.LabelField("Interaction", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(interactorStrength);
            EditorGUILayout.PropertyField(maxInteractors);
            
            EditorGUILayout.Space(10);
            
            // === RENDERING ===
            EditorGUILayout.LabelField("Rendering", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(castShadows);
            EditorGUILayout.PropertyField(receiveShadows);
            
            EditorGUILayout.Space(10);
            
            // === DEBUG ===
            EditorGUILayout.LabelField("Debug", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(drawCullingBounds);
            
            serializedObject.ApplyModifiedProperties();
        }
    }
}
