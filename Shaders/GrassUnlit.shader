// Copyright (c) 2026 Brendo Otavio Carvalho de Matos. All rights reserved.
// Optimized Unlit shader for Nintendo Switch - uses Light Probes only

Shader "GrassSystem/GrassUnlit"
{
    Properties
    {
        [Header(Textures)]
        _MainTex ("Albedo (RGB)", 2D) = "white" {}
        
        [Header(Colors)]
        _TopTint ("Top Tint", Color) = (0.8, 1.0, 0.6, 1)
        _BottomTint ("Bottom Tint", Color) = (0.2, 0.4, 0.1, 1)
        
        [Header(Pattern Colors)]
        _PatternATip ("Color A Tip", Color) = (0.45, 0.85, 0.25, 1)
        _PatternARoot ("Color A Root", Color) = (0.15, 0.35, 0.08, 1)
        _PatternBTip ("Color B Tip", Color) = (0.35, 0.65, 0.20, 1)
        _PatternBRoot ("Color B Root", Color) = (0.12, 0.28, 0.06, 1)
        
        [Header(Natural Blend Colors)]
        _NaturalColor1Tip ("Color 1 Tip", Color) = (0.45, 0.85, 0.25, 1)
        _NaturalColor1Root ("Color 1 Root", Color) = (0.15, 0.35, 0.08, 1)
        _NaturalColor2Tip ("Color 2 Tip", Color) = (0.35, 0.65, 0.20, 1)
        _NaturalColor2Root ("Color 2 Root", Color) = (0.12, 0.28, 0.06, 1)
        _NaturalColor3Tip ("Color 3 Tip", Color) = (0.50, 0.70, 0.15, 1)
        _NaturalColor3Root ("Color 3 Root", Color) = (0.20, 0.30, 0.05, 1)
        
        [Header(Color Zones)]
        [Toggle] _UseColorZones ("Enable Color Zones", Float) = 0
        [Enum(Stripes,0,Checkerboard,1,Noise,2,Organic,3,Patches,4)] _ZonePatternType ("Pattern Type", Float) = 0
        _ZoneColorLight ("Light Zone Color", Color) = (0.5, 0.8, 0.3, 1)
        _ZoneColorDark ("Dark Zone Color", Color) = (0.3, 0.55, 0.2, 1)
        _ZoneScale ("Zone Scale", Range(1, 50)) = 5
        _ZoneDirection ("Direction (Stripes)", Range(0, 360)) = 0
        _ZoneSoftness ("Edge Softness", Range(0, 1)) = 0.1
        _ZoneContrast ("Contrast (Noise)", Range(0.5, 3)) = 1.5
        _OrganicAccentColor ("Accent Color (Organic)", Color) = (0.55, 0.6, 0.2, 1)
        _OrganicClumpiness ("Clumpiness (Organic)", Range(0, 1)) = 0.5
        
        [Header(Tip Cutout)]
        [Toggle] _UseTipCutout ("Use Tip Cutout", Float) = 0
        _TipCutoff ("Tip Cutoff Height", Range(0, 1)) = 0.8
        _AlphaCutoff ("Alpha Cutoff", Range(0, 1)) = 0.5
        
        [Header(Wind)]
        _WindSpeed ("Wind Speed", Range(0, 5)) = 1
        _WindStrength ("Wind Strength", Range(0, 1)) = 0.3
        _WindFrequency ("Wind Frequency", Range(0.01, 1)) = 0.1
        
        [Header(Lighting)]
        _AmbientBoost ("Ambient Boost", Range(0, 2)) = 1.0
        _LightProbeInfluence ("Light Probe Influence", Range(0, 1)) = 1.0
        
        [Header(Depth Perception)]
        _InstanceColorVariation ("Instance Color Variation", Range(0, 0.3)) = 0
        _HeightDarkening ("Height Darkening", Range(0, 0.5)) = 0
        _BackfaceDarkening ("Backface Darkening", Range(0, 0.5)) = 0
        
        [Header(Custom Mesh Mode)]
        [Toggle] _UseOnlyAlbedoColor ("Use Only Albedo Color", Float) = 0
        [Toggle] _UseUniformScale ("Use Uniform Scale", Float) = 0
        _MeshRotation ("Mesh Rotation (Radians)", Vector) = (0, 0, 0, 0)
        
        [Header(Natural Variation)]
        _MaxTiltAngle ("Max Tilt Angle (Radians)", Float) = 0.26
        _TiltVariation ("Tilt Variation", Range(0, 1)) = 0.7
        
        [Header(Decal Layer 1)]
        [Toggle] _DecalEnabled ("Enable Decal 1", Float) = 0
        _DecalTex ("Decal 1 Texture", 2D) = "white" {}
        _DecalBounds ("Decal 1 Bounds", Vector) = (0, 0, 10, 10)
        _DecalRotation ("Decal 1 Rotation", Float) = 0
        _DecalBlend ("Decal 1 Blend", Range(0, 1)) = 1
        [Enum(Override,0,Multiply,1,Additive,2)] _DecalBlendMode ("Decal 1 Blend Mode", Float) = 0
        
        [Header(Decal Layer 2)]
        [Toggle] _Decal2Enabled ("Enable Decal 2", Float) = 0
        _Decal2Tex ("Decal 2 Texture", 2D) = "white" {}
        _Decal2Bounds ("Decal 2 Bounds", Vector) = (0, 0, 10, 10)
        _Decal2Rotation ("Decal 2 Rotation", Float) = 0
        _Decal2Blend ("Decal 2 Blend", Range(0, 1)) = 1
        [Enum(Override,0,Multiply,1,Additive,2)] _Decal2BlendMode ("Decal 2 Blend Mode", Float) = 0
        
        [Header(Decal Layer 3)]
        [Toggle] _Decal3Enabled ("Enable Decal 3", Float) = 0
        _Decal3Tex ("Decal 3 Texture", 2D) = "white" {}
        _Decal3Bounds ("Decal 3 Bounds", Vector) = (0, 0, 10, 10)
        _Decal3Rotation ("Decal 3 Rotation", Float) = 0
        _Decal3Blend ("Decal 3 Blend", Range(0, 1)) = 1
        [Enum(Override,0,Multiply,1,Additive,2)] _Decal3BlendMode ("Decal 3 Blend Mode", Float) = 0
        
        [Header(Decal Layer 4)]
        [Toggle] _Decal4Enabled ("Enable Decal 4", Float) = 0
        _Decal4Tex ("Decal 4 Texture", 2D) = "white" {}
        _Decal4Bounds ("Decal 4 Bounds", Vector) = (0, 0, 10, 10)
        _Decal4Rotation ("Decal 4 Rotation", Float) = 0
        _Decal4Blend ("Decal 4 Blend", Range(0, 1)) = 1
        [Enum(Override,0,Multiply,1,Additive,2)] _Decal4BlendMode ("Decal 4 Blend Mode", Float) = 0
        
        [Header(Decal Layer 5)]
        [Toggle] _Decal5Enabled ("Enable Decal 5", Float) = 0
        _Decal5Tex ("Decal 5 Texture", 2D) = "white" {}
        _Decal5Bounds ("Decal 5 Bounds", Vector) = (0, 0, 10, 10)
        _Decal5Rotation ("Decal 5 Rotation", Float) = 0
        _Decal5Blend ("Decal 5 Blend", Range(0, 1)) = 1
        [Enum(Override,0,Multiply,1,Additive,2)] _Decal5BlendMode ("Decal 5 Blend Mode", Float) = 0
    }
    
    SubShader
    {
        Tags 
        { 
            "RenderType" = "Opaque"
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Geometry"
        }
        
        Pass
        {
            Name "Unlit"
            Tags { "LightMode" = "UniversalForward" }
            
            Cull Off
            
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #pragma multi_compile_instancing
            
            // Compile-time stripping: only compile the active color mode + decal toggle
            #pragma shader_feature_local _COLORMODE_ALBEDO _COLORMODE_TINT _COLORMODE_PATTERNS
            #pragma multi_compile_local _ _DECALS_ON
            
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "GrassCommon.hlsl"
            
            StructuredBuffer<GrassDrawData> _GrassBuffer;
            
            float4 _Interactors[16];
            int _InteractorCount;
            float _InteractorStrength;
            
            TEXTURE2D(_MainTex);
            TEXTURE2D(_TipMask);
            TEXTURE2D(_DecalTex);
            TEXTURE2D(_Decal2Tex);
            TEXTURE2D(_Decal3Tex);
            TEXTURE2D(_Decal4Tex);
            TEXTURE2D(_Decal5Tex);
            SAMPLER(sampler_MainTex);
            SAMPLER(sampler_TipMask);
            SAMPLER(sampler_DecalTex);
            SAMPLER(sampler_Decal2Tex);
            SAMPLER(sampler_Decal3Tex);
            SAMPLER(sampler_Decal4Tex);
            SAMPLER(sampler_Decal5Tex);
            
            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                float4 _TopTint;
                float4 _BottomTint;
                
                // Color Mode System (0=Albedo, 1=Tint, 2=Patterns)
                float _ColorMode;
                
                // Pattern Mode
                float _PatternType; // 0=Stripes, 1=Checkerboard, 2=NaturalBlend
                float4 _PatternATip;
                float4 _PatternARoot;
                float4 _PatternBTip;
                float4 _PatternBRoot;
                
                // Natural Blend Colors (3 colors with tip/root)
                float4 _NaturalColor1Tip;
                float4 _NaturalColor1Root;
                float4 _NaturalColor2Tip;
                float4 _NaturalColor2Root;
                float4 _NaturalColor3Tip;
                float4 _NaturalColor3Root;
                
                // Pattern Dimensions
                float _StripeWidth;
                float _CheckerboardSize;
                float _StripeAngle;
                
                // Natural Blend Settings
                float _NaturalBlendType; // 0=BlueNoise, 1=Cluster, 2=Gradient, 3=Stochastic
                float _NaturalScale;
                float _NaturalSoftness;
                float _NaturalContrast;
                
                // Albedo Blend
                float _UseAlbedoBlend;
                float _AlbedoBlendAmount;
                float _UseNormalMap;
                
                float _UseTipCutout;
                float _TipCutoff;
                float _AlphaCutoff;
                float _WindSpeed;
                float _WindStrength;
                float _WindFrequency;
                float _AmbientBoost;
                float _LightProbeInfluence;
                float _InstanceColorVariation;
                float _HeightDarkening;
                float _BackfaceDarkening;
                float _UseUniformScale;
                float4 _MeshRotation;
                float _MaxTiltAngle;
                float _TiltVariation;
                float _MaxBendAngle;
                // Decal Layer 1
                float _DecalEnabled;
                float4 _DecalBounds;
                float _DecalRotation;
                float _DecalBlend;
                float _DecalBlendMode;
                // Decal Layer 2
                float _Decal2Enabled;
                float4 _Decal2Bounds;
                float _Decal2Rotation;
                float _Decal2Blend;
                float _Decal2BlendMode;
                // Decal Layer 3
                float _Decal3Enabled;
                float4 _Decal3Bounds;
                float _Decal3Rotation;
                float _Decal3Blend;
                float _Decal3BlendMode;
                // Decal Layer 4
                float _Decal4Enabled;
                float4 _Decal4Bounds;
                float _Decal4Rotation;
                float _Decal4Blend;
                float _Decal4BlendMode;
                // Decal Layer 5
                float _Decal5Enabled;
                float4 _Decal5Bounds;
                float _Decal5Rotation;
                float _Decal5Blend;
                float _Decal5BlendMode;
            CBUFFER_END
            
            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
                uint instanceID : SV_InstanceID;
            };
            
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                float3 normalWS : TEXCOORD2;
                half3 grassColor : TEXCOORD3;
                float patternMask : TEXCOORD4;
                half fogFactor : TEXCOORD5;
                half instanceVariation : TEXCOORD6;
            };
            
            Varyings vert(Attributes input)
            {
                Varyings output;
                
                GrassDrawData grassData = _GrassBuffer[input.instanceID];
                
                float2 windOffset = CalculateWind(
                    grassData.position,
                    _Time.y,
                    _WindSpeed,
                    _WindStrength,
                    _WindFrequency
                );
                
                float3 interactionOffset = float3(0, 0, 0);
                float maxInfluence = 0;
                
                float grassHeight = grassData.widthHeight.y;
                float grassBase = grassData.position.y;
                
                for (int i = 0; i < _InteractorCount; i++)
                {
                    float3 interactorPos = _Interactors[i].xyz;
                    float radius = _Interactors[i].w;
                    
                    if (radius > 0)
                    {
                        // Distance on XZ plane
                        float distXZ = length(grassData.position.xz - interactorPos.xz);
                        
                        // Vertical distance: how far above grass base is the interactor?
                        float heightAboveBase = interactorPos.y - grassBase;
                        
                        // Check horizontal range
                        if (distXZ < radius)
                        {
                            // Horizontal influence
                            float hInfluence = 1.0 - (distXZ / radius);
                            hInfluence = hInfluence * hInfluence;
                            
                            // Vertical influence: full when at/below base, fades as we go up
                            // At base or below: vInfluence = 1, at grassHeight: vInfluence = 0
                            float vInfluence = 1.0 - saturate(heightAboveBase / grassHeight);
                            
                            float influence = hInfluence * vInfluence;
                            
                            if (influence > 0.01)
                            {
                                maxInfluence = max(maxInfluence, influence);
                                
                                float3 pushDir;
                                if (distXZ > 0.001)
                                {
                                    pushDir = normalize(float3(grassData.position.x - interactorPos.x, 0, grassData.position.z - interactorPos.z));
                                }
                                else
                                {
                                    float angle = Hash(grassData.position.xz) * 6.28318;
                                    pushDir = float3(cos(angle), 0, sin(angle));
                                }
                                
                                // Boost the strength for more dramatic flattening
                                interactionOffset += pushDir * influence * _InteractorStrength * 2.0;
                            }
                        }
                    }
                }
                
                // Reduce wind when grass is being pressed
                windOffset *= (1.0 - maxInfluence);
                
                float3 worldPos = TransformGrassVertex(
                    input.positionOS.xyz,
                    grassData.position,
                    grassData.normal,
                    grassData.widthHeight.x,
                    grassData.widthHeight.y,
                    grassData.distanceScale,
                    windOffset,
                    interactionOffset,
                    input.uv.y,
                    _UseUniformScale,
                    _MeshRotation.xyz,
                    _MaxTiltAngle,
                    _TiltVariation,
                    _MaxBendAngle
                );
                
                output.positionWS = worldPos;
                output.positionCS = TransformWorldToHClip(worldPos);
                output.uv = TRANSFORM_TEX(input.uv, _MainTex);
                output.normalWS = float3(0, 1, 0);
                output.grassColor = grassData.color;
                output.patternMask = grassData.patternMask;
                output.fogFactor = ComputeFogFactor(output.positionCS.z);
                
                // Per-instance variation for depth perception (uses existing hash, very cheap)
                output.instanceVariation = Hash(grassData.position.xz + float2(5.123, 7.456));
                
                return output;
            }
            
            half4 frag(Varyings input, bool isFrontFace : SV_IsFrontFace) : SV_Target
            {
                // Tip cutout
                if (_UseTipCutout > 0.5)
                {
                    half tipMask = SAMPLE_TEXTURE2D(_TipMask, sampler_TipMask, input.uv).a;
                    if (input.uv.y > _TipCutoff && tipMask < _AlphaCutoff)
                        discard;
                }
                
                // ========================================
                // COLOR MODE SYSTEM
                // 0 = Albedo: pure texture (NO effects)
                // 1 = Tint: TopTint/BottomTint gradient
                // 2 = Patterns: Stripes/Checkerboard/NaturalBlend
                // ========================================
                half3 baseColor;
                half4 albedoTex = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv);
                bool isAlbedoMode = false;
                
                // Mode 0: Albedo - PURE texture color (no effects applied)
                #if defined(_COLORMODE_ALBEDO)
                {
                    baseColor = albedoTex.rgb;
                    isAlbedoMode = true;
                }
                // Mode 1: Tint - TopTint/BottomTint gradient (PURE colors, no effects)
                #elif defined(_COLORMODE_TINT)
                {
                    baseColor = lerp(_BottomTint.rgb, _TopTint.rgb, input.uv.y);
                    
                    // Optional albedo blend
                    if (_UseAlbedoBlend > 0.5)
                    {
                        baseColor = lerp(baseColor, baseColor * albedoTex.rgb * 2.0, _AlbedoBlendAmount);
                    }
                }
                // Mode 2: Patterns - Stripes/Checkerboard/NaturalBlend
                #else // _COLORMODE_PATTERNS (default fallback)
                {
                    float2 worldPos = input.positionWS.xz;
                    half3 colorATip = _PatternATip.rgb;
                    half3 colorARoot = _PatternARoot.rgb;
                    half3 colorBTip = _PatternBTip.rgb;
                    half3 colorBRoot = _PatternBRoot.rgb;
                    
                    float patternValue = 0;
                    
                    // Pattern Type 0: Stripes
                    if (_PatternType < 0.5)
                    {
                        float cosA = cos(_StripeAngle);
                        float sinA = sin(_StripeAngle);
                        float rotatedX = worldPos.x * cosA - worldPos.y * sinA;
                        patternValue = frac(rotatedX / _StripeWidth);
                        patternValue = patternValue < 0.5 ? 0 : 1;
                        patternValue = lerp(patternValue, smoothstep(0.45, 0.55, frac(rotatedX / _StripeWidth)), _NaturalSoftness);
                    }
                    // Pattern Type 1: Checkerboard
                    else if (_PatternType < 1.5)
                    {
                        float checkX = floor(worldPos.x / _CheckerboardSize);
                        float checkZ = floor(worldPos.y / _CheckerboardSize); // worldPos.y is actually Z!
                        float checker = fmod(abs(checkX + checkZ), 2.0);
                        patternValue = checker < 0.5 ? 0 : 1;
                    }
                    // Pattern Type 2: Natural Blend (3-color with tip/root using natural patterns)
                    else
                    {
                        // Get noise value (0-1) that determines which color zone we're in
                        float zoneNoise = 0;
                        
                        // Select natural blend type (0=BlueNoise, 1=Cluster, 2=Gradient, 3=Stochastic)
                        if (_NaturalBlendType < 0.5)
                        {
                            zoneNoise = BlueNoise(worldPos / _NaturalScale);
                        }
                        else if (_NaturalBlendType < 1.5)
                        {
                            zoneNoise = ClusterNoise(worldPos / _NaturalScale);
                        }
                        else if (_NaturalBlendType < 2.5)
                        {
                            zoneNoise = GradientBlend(worldPos / _NaturalScale);
                        }
                        else
                        {
                            zoneNoise = StochasticNoise(worldPos / _NaturalScale);
                        }
                        
                        // Apply contrast to expand the noise distribution
                        float contrastMultiplier = 1.0 + _NaturalContrast * 2.0;
                        zoneNoise = saturate((zoneNoise - 0.5) * contrastMultiplier + 0.5);
                        
                        // Calculate 3 color zones with tip/root gradients
                        half3 color1 = lerp(_NaturalColor1Root.rgb, _NaturalColor1Tip.rgb, input.uv.y);
                        half3 color2 = lerp(_NaturalColor2Root.rgb, _NaturalColor2Tip.rgb, input.uv.y);
                        half3 color3 = lerp(_NaturalColor3Root.rgb, _NaturalColor3Tip.rgb, input.uv.y);
                        
                        // Zone thresholds with soft edges
                        float softEdge = _NaturalSoftness * 0.15;
                        
                        float w1 = 1.0 - smoothstep(0.333 - softEdge, 0.333 + softEdge, zoneNoise);
                        float w3 = smoothstep(0.666 - softEdge, 0.666 + softEdge, zoneNoise);
                        float w2 = max(1.0 - w1 - w3, 0.0);
                        
                        // Normalize weights
                        float totalWeight = w1 + w2 + w3;
                        w1 /= totalWeight;
                        w2 /= totalWeight;
                        w3 /= totalWeight;
                        
                        baseColor = color1 * w1 + color2 * w2 + color3 * w3;
                        
                        if (_UseAlbedoBlend > 0.5)
                        {
                            baseColor = lerp(baseColor, baseColor * albedoTex.rgb * 2.0, _AlbedoBlendAmount);
                        }
                        // Skip the A/B pattern code below for natural blend
                        patternValue = -1;
                    }
                    
                    // Apply A/B pattern (only for Stripes/Checkerboard)
                    if (patternValue >= 0)
                    {
                        half3 colorA = lerp(colorARoot, colorATip, input.uv.y);
                        half3 colorB = lerp(colorBRoot, colorBTip, input.uv.y);
                        baseColor = lerp(colorA, colorB, patternValue);
                        
                        if (_UseAlbedoBlend > 0.5)
                        {
                            baseColor = lerp(baseColor, baseColor * albedoTex.rgb * 2.0, _AlbedoBlendAmount);
                        }
                    }
                }
                #endif // _COLORMODE_*
                
                // ========================================
                // SHARED: Multi-layer decal projection (Layer 1 -> 5, last wins)
                // Runs once for ALL color modes — stripped entirely when no decals
                // ========================================
                #if defined(_DECALS_ON)
                float2 decal1UV = CalculateDecalUV(input.positionWS, _DecalBounds, _DecalRotation);
                float2 decal2UV = CalculateDecalUV(input.positionWS, _Decal2Bounds, _Decal2Rotation);
                float2 decal3UV = CalculateDecalUV(input.positionWS, _Decal3Bounds, _Decal3Rotation);
                float2 decal4UV = CalculateDecalUV(input.positionWS, _Decal4Bounds, _Decal4Rotation);
                float2 decal5UV = CalculateDecalUV(input.positionWS, _Decal5Bounds, _Decal5Rotation);
                
                half4 decal1Sample = SAMPLE_TEXTURE2D(_DecalTex, sampler_DecalTex, decal1UV);
                half4 decal2Sample = SAMPLE_TEXTURE2D(_Decal2Tex, sampler_Decal2Tex, decal2UV);
                half4 decal3Sample = SAMPLE_TEXTURE2D(_Decal3Tex, sampler_Decal3Tex, decal3UV);
                half4 decal4Sample = SAMPLE_TEXTURE2D(_Decal4Tex, sampler_Decal4Tex, decal4UV);
                half4 decal5Sample = SAMPLE_TEXTURE2D(_Decal5Tex, sampler_Decal5Tex, decal5UV);
                
                DecalResult d1 = ApplyDecalLayer(baseColor, input.positionWS, _DecalEnabled, _DecalBounds, _DecalRotation, _DecalBlend, _DecalBlendMode, decal1Sample, decal1UV);
                DecalResult d2 = ApplyDecalLayer(d1.color, input.positionWS, _Decal2Enabled, _Decal2Bounds, _Decal2Rotation, _Decal2Blend, _Decal2BlendMode, decal2Sample, decal2UV);
                DecalResult d3 = ApplyDecalLayer(d2.color, input.positionWS, _Decal3Enabled, _Decal3Bounds, _Decal3Rotation, _Decal3Blend, _Decal3BlendMode, decal3Sample, decal3UV);
                DecalResult d4 = ApplyDecalLayer(d3.color, input.positionWS, _Decal4Enabled, _Decal4Bounds, _Decal4Rotation, _Decal4Blend, _Decal4BlendMode, decal4Sample, decal4UV);
                DecalResult d5 = ApplyDecalLayer(d4.color, input.positionWS, _Decal5Enabled, _Decal5Bounds, _Decal5Rotation, _Decal5Blend, _Decal5BlendMode, decal5Sample, decal5UV);
                baseColor = d5.color;
                
                // Albedo mode: preserve texture variation via luma multiplication
                if (isAlbedoMode)
                {
                    float anyDecalApplied = saturate(d1.applied + d2.applied + d3.applied + d4.applied + d5.applied);
                    half albedoLuma = dot(albedoTex.rgb, half3(0.299, 0.587, 0.114));
                    half lumaFactor = lerp(1.0, albedoLuma * 2.0, 0.5);
                    baseColor = lerp(baseColor, baseColor * lumaFactor, anyDecalApplied);
                }
                #endif // _DECALS_ON
                
                // ========================================
                // SHARED: Fog + output (runs once for ALL color modes)
                // ========================================
                half3 finalColor = MixFog(baseColor, input.fogFactor);
                return half4(finalColor, 1.0);
            }
            ENDHLSL
        }
        
        // Simplified ShadowCaster - optional, can be removed for more performance
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }
            
            ZWrite On
            ZTest LEqual
            ColorMask 0
            Cull Off
            
            HLSLPROGRAM
            #pragma vertex ShadowVert
            #pragma fragment ShadowFrag
            #pragma multi_compile_instancing
            
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"
            #include "GrassCommon.hlsl"
            
            StructuredBuffer<GrassDrawData> _GrassBuffer;
            
            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                float4 _TopTint;
                float4 _BottomTint;
                float4 _PatternColorA;
                float4 _PatternColorB;
                float _UseColorZones;
                float _ZonePatternType;
                float4 _ZoneColorLight;
                float4 _ZoneColorDark;
                float _ZoneScale;
                float _ZoneDirection;
                float _ZoneSoftness;
                float _ZoneContrast;
                float4 _OrganicAccentColor;
                float _OrganicClumpiness;
                float _UseTipCutout;
                float _TipCutoff;
                float _AlphaCutoff;
                float _WindSpeed;
                float _WindStrength;
                float _WindFrequency;
                float _AmbientBoost;
                float _LightProbeInfluence;
                float _UseOnlyAlbedoColor;
                float _UseUniformScale;
                float4 _MeshRotation;
                float _MaxTiltAngle;
                float _TiltVariation;
            CBUFFER_END
            
            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
                uint instanceID : SV_InstanceID;
            };
            
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
            };
            
            float3 _LightDirection;
            
            Varyings ShadowVert(Attributes input)
            {
                Varyings output;
                
                GrassDrawData grassData = _GrassBuffer[input.instanceID];
                
                float2 windOffset = CalculateWind(
                    grassData.position,
                    _Time.y,
                    _WindSpeed,
                    _WindStrength,
                    _WindFrequency
                );
                
                float3 worldPos = TransformGrassVertex(
                    input.positionOS.xyz,
                    grassData.position,
                    grassData.normal,
                    grassData.widthHeight.x,
                    grassData.widthHeight.y,
                    grassData.distanceScale,
                    windOffset,
                    float3(0, 0, 0),
                    input.uv.y,
                    _UseUniformScale,
                    _MeshRotation.xyz,
                    _MaxTiltAngle,
                    _TiltVariation
                );
                
                float3 normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.positionCS = TransformWorldToHClip(ApplyShadowBias(worldPos, normalWS, _LightDirection));
                
                return output;
            }
            
            half4 ShadowFrag(Varyings input) : SV_Target
            {
                return 0;
            }
            ENDHLSL
        }
    }
    
    Fallback Off
}
