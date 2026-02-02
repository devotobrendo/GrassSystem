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
        
        // Color Mode
        private SerializedProperty colorMode;
        
        // Pattern Mode
        private SerializedProperty patternType;
        private SerializedProperty patternATip;
        private SerializedProperty patternARoot;
        private SerializedProperty patternBTip;
        private SerializedProperty patternBRoot;
        
        // Natural Blend Colors (3 colors with tip/root)
        private SerializedProperty naturalColor1Tip;
        private SerializedProperty naturalColor1Root;
        private SerializedProperty naturalColor2Tip;
        private SerializedProperty naturalColor2Root;
        private SerializedProperty naturalColor3Tip;
        private SerializedProperty naturalColor3Root;
        
        // Pattern Dimensions
        private SerializedProperty stripeWidth;
        private SerializedProperty checkerboardSize;
        private SerializedProperty stripeAngle;
        
        // Natural Blend Settings
        private SerializedProperty naturalBlendType;
        private SerializedProperty naturalScale;
        private SerializedProperty naturalSoftness;
        private SerializedProperty naturalContrast;
        
        // Texture Options
        private SerializedProperty useAlbedoBlend;
        private SerializedProperty albedoBlendAmount;
        private SerializedProperty useNormalMap;
        
        // Tip Customization
        private SerializedProperty useTipCutout;
        private SerializedProperty tipMaskTexture;
        private SerializedProperty tipCutoffHeight;
        
        // Textures
        private SerializedProperty albedoTexture;
        private SerializedProperty normalMap;
        
        // Lighting (topTint/bottomTint are used for Tint mode now)
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
        
        // Depth Perception
        private SerializedProperty useDepthPerception;
        private SerializedProperty instanceColorVariation;
        private SerializedProperty heightDarkening;
        private SerializedProperty backfaceDarkening;
        
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
        
        private void OnEnable()
        {
            grassMode = serializedObject.FindProperty("grassMode");
            
            cullingShader = serializedObject.FindProperty("cullingShader");
            grassMaterial = serializedObject.FindProperty("grassMaterial");
            grassMesh = serializedObject.FindProperty("grassMesh");
            
            customMeshes = serializedObject.FindProperty("customMeshes");
            minSize = serializedObject.FindProperty("minSize");
            maxSize = serializedObject.FindProperty("maxSize");
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
            
            colorMode = serializedObject.FindProperty("colorMode");
            
            // Pattern Mode
            patternType = serializedObject.FindProperty("patternType");
            patternATip = serializedObject.FindProperty("patternATip");
            patternARoot = serializedObject.FindProperty("patternARoot");
            patternBTip = serializedObject.FindProperty("patternBTip");
            patternBRoot = serializedObject.FindProperty("patternBRoot");
            
            // Natural Blend Colors
            naturalColor1Tip = serializedObject.FindProperty("naturalColor1Tip");
            naturalColor1Root = serializedObject.FindProperty("naturalColor1Root");
            naturalColor2Tip = serializedObject.FindProperty("naturalColor2Tip");
            naturalColor2Root = serializedObject.FindProperty("naturalColor2Root");
            naturalColor3Tip = serializedObject.FindProperty("naturalColor3Tip");
            naturalColor3Root = serializedObject.FindProperty("naturalColor3Root");
            
            // Pattern Dimensions
            stripeWidth = serializedObject.FindProperty("stripeWidth");
            checkerboardSize = serializedObject.FindProperty("checkerboardSize");
            stripeAngle = serializedObject.FindProperty("stripeAngle");
            
            // Natural Blend Settings
            naturalBlendType = serializedObject.FindProperty("naturalBlendType");
            naturalScale = serializedObject.FindProperty("naturalScale");
            naturalSoftness = serializedObject.FindProperty("naturalSoftness");
            naturalContrast = serializedObject.FindProperty("naturalContrast");
            
            // Texture Options
            useAlbedoBlend = serializedObject.FindProperty("useAlbedoBlend");
            albedoBlendAmount = serializedObject.FindProperty("albedoBlendAmount");
            useNormalMap = serializedObject.FindProperty("useNormalMap");
            
            
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
            
            instanceColorVariation = serializedObject.FindProperty("instanceColorVariation");
            heightDarkening = serializedObject.FindProperty("heightDarkening");
            backfaceDarkening = serializedObject.FindProperty("backfaceDarkening");
            useDepthPerception = serializedObject.FindProperty("useDepthPerception");
            
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
            
            // === COLOR MODE === (moved up, right after Textures/Mode settings)
            EditorGUILayout.LabelField("Color Mode", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(colorMode, new GUIContent("Mode", "How grass color is determined"));
            
            bool isTintMode = settings.colorMode == GrassColorMode.Tint;
            bool isAlbedoMode = settings.colorMode == GrassColorMode.Albedo;
            
            // Mode 0: Albedo - pure texture
            if (isAlbedoMode)
            {
                EditorGUILayout.HelpBox("Uses albedo texture colors directly without any tint.", MessageType.Info);
            }
            
            // Mode 1: Tint - TopTint/BottomTint gradient
            if (isTintMode)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(topTint, new GUIContent("Top Tint", "Color at the tip of grass blades"));
                EditorGUILayout.PropertyField(bottomTint, new GUIContent("Bottom Tint", "Color at the root of grass blades"));
                
                EditorGUILayout.Space(5);
                EditorGUILayout.PropertyField(useAlbedoBlend, new GUIContent("Blend with Albedo", "Mix albedo texture with tint colors"));
                if (settings.useAlbedoBlend)
                {
                    EditorGUI.indentLevel++;
                    EditorGUILayout.PropertyField(albedoBlendAmount, new GUIContent("Blend Amount", "0 = pure tint, 1 = full albedo"));
                    EditorGUI.indentLevel--;
                }
                EditorGUILayout.PropertyField(useNormalMap, new GUIContent("Use Normal Map", "Enable normal map for lighting detail"));
                EditorGUI.indentLevel--;
            }
            
            
            // Mode 2: Patterns - Stripes/Checkerboard/NaturalBlend
            bool isPatternsMode = settings.colorMode == GrassColorMode.Patterns;
            if (isPatternsMode)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(patternType, new GUIContent("Pattern Type", "Type of pattern to apply"));
                
                bool isStripes = settings.patternType == PatternType.Stripes;
                bool isCheckerboard = settings.patternType == PatternType.Checkerboard;
                bool isNaturalBlend = settings.patternType == PatternType.NaturalBlend;
                
                EditorGUILayout.Space(5);
                
                // Stripes/Checkerboard: Show Color A/B
                if (isStripes || isCheckerboard)
                {
                    EditorGUILayout.LabelField("Color A", EditorStyles.boldLabel);
                    EditorGUI.indentLevel++;
                    EditorGUILayout.PropertyField(patternATip, new GUIContent("Tip", "Color A at blade tip"));
                    EditorGUILayout.PropertyField(patternARoot, new GUIContent("Root", "Color A at blade root"));
                    EditorGUI.indentLevel--;
                    
                    EditorGUILayout.LabelField("Color B", EditorStyles.boldLabel);
                    EditorGUI.indentLevel++;
                    EditorGUILayout.PropertyField(patternBTip, new GUIContent("Tip", "Color B at blade tip"));
                    EditorGUILayout.PropertyField(patternBRoot, new GUIContent("Root", "Color B at blade root"));
                    EditorGUI.indentLevel--;
                }
                
                // Natural Blend: Show 3 colors with tip/root each
                if (isNaturalBlend)
                {
                    EditorGUILayout.LabelField("Natural Blend Colors", EditorStyles.boldLabel);
                    EditorGUI.indentLevel++;
                    
                    EditorGUILayout.LabelField("Color 1", EditorStyles.miniLabel);
                    EditorGUI.indentLevel++;
                    EditorGUILayout.PropertyField(naturalColor1Tip, new GUIContent("Tip", "Color 1 at blade tip"));
                    EditorGUILayout.PropertyField(naturalColor1Root, new GUIContent("Root", "Color 1 at blade root"));
                    EditorGUI.indentLevel--;
                    
                    EditorGUILayout.LabelField("Color 2", EditorStyles.miniLabel);
                    EditorGUI.indentLevel++;
                    EditorGUILayout.PropertyField(naturalColor2Tip, new GUIContent("Tip", "Color 2 at blade tip"));
                    EditorGUILayout.PropertyField(naturalColor2Root, new GUIContent("Root", "Color 2 at blade root"));
                    EditorGUI.indentLevel--;
                    
                    EditorGUILayout.LabelField("Color 3", EditorStyles.miniLabel);
                    EditorGUI.indentLevel++;
                    EditorGUILayout.PropertyField(naturalColor3Tip, new GUIContent("Tip", "Color 3 at blade tip"));
                    EditorGUILayout.PropertyField(naturalColor3Root, new GUIContent("Root", "Color 3 at blade root"));
                    EditorGUI.indentLevel--;
                    
                    EditorGUI.indentLevel--;
                }
                
                EditorGUILayout.Space(5);
                EditorGUILayout.LabelField("Dimensions", EditorStyles.boldLabel);
                EditorGUI.indentLevel++;
                
                if (isStripes)
                {
                    EditorGUILayout.PropertyField(stripeWidth, new GUIContent("Stripe Width", "Width of stripes in world units"));
                    EditorGUILayout.PropertyField(stripeAngle, new GUIContent("Stripe Angle", "Rotation angle in degrees"));
                    EditorGUILayout.PropertyField(naturalSoftness, new GUIContent("Edge Softness", "Softness of stripe edges"));
                }
                
                if (isCheckerboard)
                {
                    EditorGUILayout.PropertyField(checkerboardSize, new GUIContent("Square Size", "Size of squares in world units"));
                }
                
                if (isNaturalBlend)
                {
                    EditorGUILayout.PropertyField(naturalBlendType, new GUIContent("Blend Type", "Type of natural distribution pattern"));
                    EditorGUILayout.PropertyField(naturalScale, new GUIContent("Scale", "Size of natural areas in world units"));
                    EditorGUILayout.PropertyField(naturalSoftness, new GUIContent("Softness", "0 = sharp edges, 1 = smooth transitions"));
                    EditorGUILayout.PropertyField(naturalContrast, new GUIContent("Contrast", "Separation between color areas"));
                }
                
                EditorGUI.indentLevel--;
                
                EditorGUILayout.Space(5);
                EditorGUILayout.PropertyField(useAlbedoBlend, new GUIContent("Blend with Albedo", "Mix albedo texture with pattern"));
                if (settings.useAlbedoBlend)
                {
                    EditorGUI.indentLevel++;
                    EditorGUILayout.PropertyField(albedoBlendAmount, new GUIContent("Blend Amount", "0 = pure pattern, 1 = full albedo"));
                    EditorGUI.indentLevel--;
                }
                EditorGUILayout.PropertyField(useNormalMap, new GUIContent("Use Normal Map", "Enable normal map for lighting detail"));
                EditorGUI.indentLevel--;
            }
            
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
            
            // === DEPTH PERCEPTION (Unlit Shader) ===
            EditorGUILayout.LabelField("Depth Perception (Unlit Shader)", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(useDepthPerception, new GUIContent("Enable Depth Perception", "Enable depth perception effects for more visual depth"));
            if (settings.useDepthPerception)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(instanceColorVariation, new GUIContent("Instance Color Variation", "Per-instance color variation to break up uniformity"));
                EditorGUILayout.PropertyField(heightDarkening, new GUIContent("Height Darkening", "Darkens the base of grass blades"));
                EditorGUILayout.PropertyField(backfaceDarkening, new GUIContent("Backface Darkening", "Darkens the backface of grass blades"));
                EditorGUI.indentLevel--;
            }
            
            EditorGUILayout.Space(10);
            
            // === LIGHTING === (moved to bottom as requested)
            EditorGUILayout.LabelField("Lighting", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(translucency);
            EditorGUILayout.PropertyField(useAlignedNormals);
            
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
                
                EditorGUI.indentLevel--;
            }
            
            serializedObject.ApplyModifiedProperties();
        }
    }
}
