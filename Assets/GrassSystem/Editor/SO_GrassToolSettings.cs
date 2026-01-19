// Copyright (c) 2026 Brendo Otavio Carvalho de Matos. All rights reserved.

using UnityEngine;

namespace GrassSystem
{
    [System.Serializable]
    public class SO_GrassToolSettings : ScriptableObject
    {
        [Header("Brush")]
        [Range(0.1f, 50f)]
        public float brushSize = 5f;
        
        [Range(0.1f, 10f)]
        public float density = 1f;
        
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
            density = 2f;  // Higher density for Zelda-style grass fields
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
    }
}
