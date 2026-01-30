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
        
        // Color Zones
        private SerializedProperty useColorZones;
        private SerializedProperty zonePatternType;
        private SerializedProperty zoneColorLight;
        private SerializedProperty zoneColorDark;
        private SerializedProperty zoneScale;
        private SerializedProperty zoneDirection;
        private SerializedProperty zoneSoftness;
        private SerializedProperty zoneContrast;
        private SerializedProperty organicAccentColor;
        private SerializedProperty organicClumpiness;
        
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
        private SerializedProperty maxZoneScaleLimit;
        
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
            
            useColorZones = serializedObject.FindProperty("useColorZones");
            zonePatternType = serializedObject.FindProperty("zonePatternType");
            zoneColorLight = serializedObject.FindProperty("zoneColorLight");
            zoneColorDark = serializedObject.FindProperty("zoneColorDark");
            zoneScale = serializedObject.FindProperty("zoneScale");
            zoneDirection = serializedObject.FindProperty("zoneDirection");
            zoneSoftness = serializedObject.FindProperty("zoneSoftness");
            zoneContrast = serializedObject.FindProperty("zoneContrast");
            organicAccentColor = serializedObject.FindProperty("organicAccentColor");
            organicClumpiness = serializedObject.FindProperty("organicClumpiness");
            
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
            maxZoneScaleLimit = serializedObject.FindProperty("maxZoneScaleLimit");
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
            
            // === COLOR ZONES ===
            EditorGUILayout.LabelField("Color Zones", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(useColorZones, new GUIContent("Enable Color Zones", "Creates alternating color zones like baseball/soccer fields or organic clumps"));
            if (settings.useColorZones)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(zonePatternType, new GUIContent("Pattern Type"));
                EditorGUILayout.PropertyField(zoneColorLight, new GUIContent("Light Zone Color"));
                EditorGUILayout.PropertyField(zoneColorDark, new GUIContent("Dark Zone Color"));
                zoneScale.floatValue = EditorGUILayout.Slider(new GUIContent("Zone Scale (meters)", "Size of each zone/stripe in world units"), zoneScale.floatValue, 1f, settings.maxZoneScaleLimit);
                
                // Only show direction for Stripes pattern
                if (settings.zonePatternType == ZonePatternType.Stripes)
                {
                    EditorGUILayout.PropertyField(zoneDirection, new GUIContent("Stripe Direction (°)", "Direction of stripes in degrees (0 = along X axis)"));
                }
                
                EditorGUILayout.PropertyField(zoneSoftness, new GUIContent("Edge Softness", "How soft/blended the edges between zones are"));
                
                // Only show contrast for Noise pattern
                if (settings.zonePatternType == ZonePatternType.Noise)
                {
                    EditorGUILayout.PropertyField(zoneContrast, new GUIContent("Noise Contrast", "Higher values create more distinct zones"));
                }
                
                // Show organic-specific options
                if (settings.zonePatternType == ZonePatternType.Organic)
                {
                    EditorGUILayout.Space(5);
                    EditorGUILayout.LabelField("Organic Settings", EditorStyles.miniLabel);
                    EditorGUILayout.PropertyField(organicAccentColor, new GUIContent("Accent Color", "Third color for variety (yellows, browns, etc)"));
                    EditorGUILayout.PropertyField(organicClumpiness, new GUIContent("Clumpiness", "0 = smooth noise, 1 = distinct blob-like clumps"));
                }
                
                // Show patches-specific options
                if (settings.zonePatternType == ZonePatternType.Patches)
                {
                    EditorGUILayout.Space(5);
                    EditorGUILayout.LabelField("Patches Settings", EditorStyles.miniLabel);
                    EditorGUILayout.PropertyField(organicAccentColor, new GUIContent("Accent Color", "Color for the darkest patch centers"));
                    EditorGUILayout.PropertyField(organicClumpiness, new GUIContent("Patch Density", "0 = few large patches, 1 = many small patches"));
                }
                
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
                EditorGUILayout.PropertyField(maxZoneScaleLimit, new GUIContent("Max Zone Scale Limit"));
                
                EditorGUI.indentLevel--;
            }
            
            serializedObject.ApplyModifiedProperties();
        }
    }
}
