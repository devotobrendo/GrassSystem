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
        
        // Natural Variation
        private SerializedProperty maxTiltAngle;
        private SerializedProperty tiltVariation;
        
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
        
        // Advanced Limits
        private SerializedProperty showAdvancedLimits;
        private SerializedProperty maxSizeLimit;
        private SerializedProperty maxBladeWidthLimit;
        private SerializedProperty maxBladeHeightLimit;
        private SerializedProperty maxWindSpeedLimit;
        private SerializedProperty maxWindStrengthLimit;
        private SerializedProperty maxDrawDistanceLimit;
        private SerializedProperty maxFadeDistanceLimit;
        private SerializedProperty maxTiltAngleLimit;
        private SerializedProperty maxInteractorStrengthLimit;
        private SerializedProperty maxInteractorsLimit;
        private SerializedProperty maxPatternScaleLimit;
        
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
            
            maxTiltAngle = serializedObject.FindProperty("maxTiltAngle");
            tiltVariation = serializedObject.FindProperty("tiltVariation");
            
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
            
            // Advanced Limits
            showAdvancedLimits = serializedObject.FindProperty("showAdvancedLimits");
            maxSizeLimit = serializedObject.FindProperty("maxSizeLimit");
            maxBladeWidthLimit = serializedObject.FindProperty("maxBladeWidthLimit");
            maxBladeHeightLimit = serializedObject.FindProperty("maxBladeHeightLimit");
            maxWindSpeedLimit = serializedObject.FindProperty("maxWindSpeedLimit");
            maxWindStrengthLimit = serializedObject.FindProperty("maxWindStrengthLimit");
            maxDrawDistanceLimit = serializedObject.FindProperty("maxDrawDistanceLimit");
            maxFadeDistanceLimit = serializedObject.FindProperty("maxFadeDistanceLimit");
            maxTiltAngleLimit = serializedObject.FindProperty("maxTiltAngleLimit");
            maxInteractorStrengthLimit = serializedObject.FindProperty("maxInteractorStrengthLimit");
            maxInteractorsLimit = serializedObject.FindProperty("maxInteractorsLimit");
            maxPatternScaleLimit = serializedObject.FindProperty("maxPatternScaleLimit");
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
                minSize.floatValue = EditorGUILayout.Slider("Min Size", minSize.floatValue, 0.1f, settings.maxSizeLimit);
                maxSize.floatValue = EditorGUILayout.Slider("Max Size", maxSize.floatValue, 0.1f, settings.maxSizeLimit);
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
                minWidth.floatValue = EditorGUILayout.Slider("Min Width", minWidth.floatValue, 0.01f, settings.maxBladeWidthLimit);
                maxWidth.floatValue = EditorGUILayout.Slider("Max Width", maxWidth.floatValue, 0.01f, settings.maxBladeWidthLimit);
                minHeight.floatValue = EditorGUILayout.Slider("Min Height", minHeight.floatValue, 0.05f, settings.maxBladeHeightLimit);
                maxHeight.floatValue = EditorGUILayout.Slider("Max Height", maxHeight.floatValue, 0.05f, settings.maxBladeHeightLimit);
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
            windSpeed.floatValue = EditorGUILayout.Slider("Wind Speed", windSpeed.floatValue, 0f, settings.maxWindSpeedLimit);
            windStrength.floatValue = EditorGUILayout.Slider("Wind Strength", windStrength.floatValue, 0f, settings.maxWindStrengthLimit);
            EditorGUILayout.PropertyField(windFrequency);
            
            EditorGUILayout.Space(10);
            
            // === NATURAL VARIATION ===
            EditorGUILayout.LabelField("Natural Variation", EditorStyles.boldLabel);
            maxTiltAngle.floatValue = EditorGUILayout.Slider(new GUIContent("Max Tilt Angle", "Maximum random tilt angle in degrees for natural clump look"), maxTiltAngle.floatValue, 0f, settings.maxTiltAngleLimit);
            EditorGUILayout.PropertyField(tiltVariation, new GUIContent("Tilt Variation", "How much the tilt varies between grass instances (0 = no tilt, 1 = full random)"));
            
            EditorGUILayout.Space(10);
            
            // === LOD & CULLING ===
            EditorGUILayout.LabelField("LOD & Culling", EditorStyles.boldLabel);
            minFadeDistance.floatValue = EditorGUILayout.Slider("Min Fade Distance", minFadeDistance.floatValue, 0f, settings.maxFadeDistanceLimit);
            maxDrawDistance.floatValue = EditorGUILayout.Slider("Max Draw Distance", maxDrawDistance.floatValue, 10f, settings.maxDrawDistanceLimit);
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
                patternScale.floatValue = EditorGUILayout.Slider("Pattern Scale", patternScale.floatValue, 0.5f, settings.maxPatternScaleLimit);
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
            interactorStrength.floatValue = EditorGUILayout.Slider("Interactor Strength", interactorStrength.floatValue, 0f, settings.maxInteractorStrengthLimit);
            maxInteractors.intValue = EditorGUILayout.IntSlider("Max Interactors", maxInteractors.intValue, 1, settings.maxInteractorsLimit);
            
            EditorGUILayout.Space(10);
            
            // === RENDERING ===
            EditorGUILayout.LabelField("Rendering", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(castShadows);
            EditorGUILayout.PropertyField(receiveShadows);
            
            EditorGUILayout.Space(10);
            
            // === DEBUG ===
            EditorGUILayout.LabelField("Debug", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(drawCullingBounds);
            
            EditorGUILayout.Space(10);
            
            // === ADVANCED LIMITS ===
            showAdvancedLimits.boolValue = EditorGUILayout.Foldout(showAdvancedLimits.boolValue, "Advanced Limits", true, EditorStyles.foldoutHeader);
            if (showAdvancedLimits.boolValue)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.HelpBox("Customize slider maximum values for extended ranges. Changes take effect immediately.", MessageType.Info);
                
                EditorGUILayout.Space(5);
                EditorGUILayout.LabelField("Size Limits", EditorStyles.miniLabel);
                EditorGUILayout.PropertyField(maxSizeLimit, new GUIContent("Max Size Limit"));
                EditorGUILayout.PropertyField(maxBladeWidthLimit, new GUIContent("Max Blade Width Limit"));
                EditorGUILayout.PropertyField(maxBladeHeightLimit, new GUIContent("Max Blade Height Limit"));
                
                EditorGUILayout.Space(5);
                EditorGUILayout.LabelField("Wind Limits", EditorStyles.miniLabel);
                EditorGUILayout.PropertyField(maxWindSpeedLimit, new GUIContent("Max Wind Speed Limit"));
                EditorGUILayout.PropertyField(maxWindStrengthLimit, new GUIContent("Max Wind Strength Limit"));
                
                EditorGUILayout.Space(5);
                EditorGUILayout.LabelField("LOD Limits", EditorStyles.miniLabel);
                EditorGUILayout.PropertyField(maxFadeDistanceLimit, new GUIContent("Max Fade Distance Limit"));
                EditorGUILayout.PropertyField(maxDrawDistanceLimit, new GUIContent("Max Draw Distance Limit"));
                
                EditorGUILayout.Space(5);
                EditorGUILayout.LabelField("Other Limits", EditorStyles.miniLabel);
                EditorGUILayout.PropertyField(maxTiltAngleLimit, new GUIContent("Max Tilt Angle Limit"));
                EditorGUILayout.PropertyField(maxInteractorStrengthLimit, new GUIContent("Max Interactor Strength Limit"));
                EditorGUILayout.PropertyField(maxInteractorsLimit, new GUIContent("Max Interactors Limit"));
                EditorGUILayout.PropertyField(maxPatternScaleLimit, new GUIContent("Max Pattern Scale Limit"));
                
                EditorGUI.indentLevel--;
            }
            
            serializedObject.ApplyModifiedProperties();
        }
    }
}
