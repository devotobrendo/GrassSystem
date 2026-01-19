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
        
        [Header("Blade Dimensions")]
        [Range(0.01f, 0.5f)]
        public float bladeWidth = 0.1f;
        
        [Range(0.1f, 2f)]
        public float bladeHeight = 0.4f;
        
        [Header("Blade Size (Custom Mesh Mode)")]
        [Range(0.1f, 3f)]
        [Tooltip("Uniform scale for custom meshes")]
        public float bladeSize = 1f;
        
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
        
        [Header("Generation")]
        public int maxGrassToGenerate = 100000;
        [Range(0.01f, 1f)]
        public float generationDensity = 0.5f;
        
        public void ResetToDefaults()
        {
            brushSize = 5f;
            density = 2f;  // Higher density for Zelda-style grass fields
            normalLimit = 0.8f;
            // Zelda BOTW-style blade dimensions
            bladeWidth = 0.05f;
            bladeHeight = 0.25f;
            bladeSize = 1f;
            heightBrushValue = 0.25f;
            brushColor = Color.white;  // Use white color, tints handle the green
            colorVariationR = 0.05f;
            colorVariationG = 0.1f;
            colorVariationB = 0.05f;
            patternBrushValue = 0f;
        }
    }
}
