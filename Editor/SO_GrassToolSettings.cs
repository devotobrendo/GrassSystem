// Copyright (c) 2026 Brendo Otavio Carvalho de Matos. All rights reserved.

using UnityEngine;

namespace GrassSystem
{
    /// <summary>
    /// Determines how brush density is calculated during painting.
    /// </summary>
    public enum DensityMode
    {
        /// <summary>Legacy mode: count = brushSize × density</summary>
        PerUnitRadius,
        /// <summary>Grass instances per square meter of brush area</summary>
        InstancesPerM2,
        /// <summary>Grass clusters per square meter of brush area</summary>
        ClustersPerM2
    }
    
    [System.Serializable]
    public class SO_GrassToolSettings : ScriptableObject
    {
        [Header("Brush")]
        [Range(0.1f, 50f)]
        public float brushSize = 5f;
        
        [Header("Density Settings")]
        [Tooltip("How density is calculated: legacy (per unit radius), instances/m², or clusters/m²")]
        public DensityMode densityMode = DensityMode.InstancesPerM2;
        
        [Range(0.1f, 10f)]
        [Tooltip("Legacy density multiplier (used when mode is PerUnitRadius)")]
        public float density = 1f;
        
        [Range(1f, 200f)]
        [Tooltip("Number of instances or clusters per area unit (used for PerM2 modes)")]
        public float densityPerM2 = 20f;
        
        [Range(0.5f, 10f)]
        [Tooltip("Area unit in square meters (e.g., 1 = per 1m², 2 = per 2m²)")]
        public float areaUnit = 1f;
        
        [Header("Removal Settings")]
        [Range(0.01f, 1f)]
        [Tooltip("Removal strength: 1 = remove all grass in brush, 0.5 = remove 50% randomly, 0.1 = remove 10%")]
        public float removalStrength = 1f;
        
        [Header("Surface Filter")]
        [Range(0f, 1f)]
        public float normalLimit = 0.8f;
        
        [Header("Size Override")]
        [Tooltip("When enabled, use custom min/max values below instead of settings")]
        public bool useCustomSize = false;
        
        [Header("Blade Dimensions (Default Mode - Custom Override)")]
        [Range(0.01f, 0.5f)]
        public float minBladeWidth = 0.03f;
        [Range(0.01f, 0.5f)]
        public float maxBladeWidth = 0.08f;
        
        [Range(0.1f, 2f)]
        public float minBladeHeight = 0.15f;
        [Range(0.1f, 2f)]
        public float maxBladeHeight = 0.35f;
        
        [Header("Blade Size (Custom Mesh Mode - Custom Override)")]
        [Range(0.1f, 3f)]
        [Tooltip("Minimum uniform scale for custom meshes")]
        public float minBladeSize = 0.5f;
        [Range(0.1f, 3f)]
        [Tooltip("Maximum uniform scale for custom meshes")]
        public float maxBladeSize = 1.2f;
        
        [Header("Height Brush")]
        [Range(0.1f, 2f)]
        public float heightBrushValue = 0.5f;
        
        [Header("Color")]
        public Color brushColor = new Color(0.3f, 0.6f, 0.2f);
        
        [Range(0f, 0.3f)]
        public float colorVariationR = 0.1f;
        [Range(0f, 0.3f)]
        public float colorVariationG = 0.15f;
        [Range(0f, 0.3f)]
        public float colorVariationB = 0.1f;
        
        [Header("Pattern")]
        [Range(0f, 1f)]
        public float patternBrushValue = 0f;
        
        [Header("Layer Masks")]
        public LayerMask paintMask = -1;
        public LayerMask blockMask = 0;
        
        [Header("Cluster Spawning")]
        [Tooltip("Spawn multiple grass blades in clusters/tufts")]
        public bool useClusterSpawning = true;
        
        [Range(1, 10)]
        [Tooltip("Minimum grass blades per cluster")]
        public int minBladesPerCluster = 3;
        
        [Range(1, 10)]
        [Tooltip("Maximum grass blades per cluster")]
        public int maxBladesPerCluster = 6;
        
        [Range(0.01f, 0.5f)]
        [Tooltip("Radius of each cluster")]
        public float clusterRadius = 0.1f;
        
        [Header("Generation")]
        public int maxGrassToGenerate = 100000;
        [Range(0.01f, 1f)]
        public float generationDensity = 0.5f;
        
        public void ResetToDefaults()
        {
            brushSize = 5f;
            // Density settings
            densityMode = DensityMode.InstancesPerM2;
            density = 2f;  // Legacy mode multiplier
            densityPerM2 = 20f;  // 20 instances per area unit is a good default
            areaUnit = 1f;  // Per 1m² by default
            removalStrength = 1f;  // Remove all by default
            normalLimit = 0.8f;
            useCustomSize = false;
            // Zelda BOTW-style blade dimensions
            minBladeWidth = 0.03f;
            maxBladeWidth = 0.08f;
            minBladeHeight = 0.15f;
            maxBladeHeight = 0.35f;
            minBladeSize = 0.5f;
            maxBladeSize = 1.2f;
            heightBrushValue = 0.25f;
            brushColor = Color.white;  // Use white color, tints handle the green
            colorVariationR = 0.05f;
            colorVariationG = 0.1f;
            colorVariationB = 0.05f;
            patternBrushValue = 0f;
            // Cluster defaults
            useClusterSpawning = true;
            minBladesPerCluster = 3;
            maxBladesPerCluster = 6;
            clusterRadius = 0.1f;
        }
        
        // ========================================
        // ADVANCED LIMITS
        // ========================================
        
        [Header("Advanced Limits")]
        [Tooltip("Customize slider maximum values for extended ranges")]
        public bool showAdvancedLimits = false;
        
        [Header("Brush Limits")]
        [Min(1f)]
        public float maxBrushSizeLimit = 50f;
        [Min(1f)]
        public float maxDensityLimit = 10f;
        [Min(50f)]
        public float maxDensityPerM2Limit = 200f;
        
        [Header("Cluster Limits")]
        [Min(1)]
        public int maxBladesPerClusterLimit = 10;
        [Min(0.1f)]
        public float maxClusterRadiusLimit = 0.5f;
        
        [Header("Blade Dimension Limits")]
        [Min(0.1f)]
        public float maxBladeWidthLimit = 0.5f;
        [Min(0.5f)]
        public float maxBladeHeightLimit = 2f;
        [Min(1f)]
        public float maxBladeSizeLimit = 3f;
        
        [Header("Height Brush Limit")]
        [Min(0.5f)]
        public float maxHeightBrushLimit = 2f;
    }
}
