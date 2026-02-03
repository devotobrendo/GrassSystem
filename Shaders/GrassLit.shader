// Copyright (c) 2026 Brendo Otavio Carvalho de Matos. All rights reserved.

Shader "GrassSystem/GrassLit"
{
    Properties
    {
        [Header(Textures)]
        _MainTex ("Albedo (RGB)", 2D) = "white" {}
        [Normal] _NormalMap ("Normal Map", 2D) = "bump" {}
        _TipMask ("Tip Mask (Alpha Cutout)", 2D) = "white" {}
        
        [Header(Colors)]
        _TopTint ("Top Tint", Color) = (0.8, 1.0, 0.6, 1)
        _BottomTint ("Bottom Tint", Color) = (0.2, 0.4, 0.1, 1)
        _PatternColorA ("Pattern Color A", Color) = (0.2, 0.5, 0.1, 1)
        _PatternColorB ("Pattern Color B", Color) = (0.15, 0.45, 0.08, 1)
        
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
        _Translucency ("Translucency", Range(0, 1)) = 0.3
        [Toggle] _AlignNormals ("Align Normals to Up", Float) = 1
        
        [Header(Terrain Lightmap)]
        [Toggle] _UseTerrainLightmap ("Use Terrain Lightmap", Float) = 0
        _TerrainLightmap ("Terrain Lightmap", 2D) = "white" {}
        _TerrainLightmapInfluence ("Lightmap Influence", Range(0, 1)) = 0.5
        _TerrainPosition ("Terrain Position", Vector) = (0, 0, 0, 0)
        _TerrainSize ("Terrain Size", Vector) = (1, 1, 1, 0)
        
        [Header(Custom Mesh Mode)]
        [Toggle] _UseOnlyAlbedoColor ("Use Only Albedo Color", Float) = 0
        [Toggle] _UseUniformScale ("Use Uniform Scale", Float) = 0
        _MeshRotation ("Mesh Rotation (Radians)", Vector) = (0, 0, 0, 0)
        
        [Header(Natural Variation)]
        _MaxTiltAngle ("Max Tilt Angle (Radians)", Float) = 0.26
        _TiltVariation ("Tilt Variation", Range(0, 1)) = 0.7
        
        [Header(Decal Projection)]
        [Toggle] _DecalEnabled ("Enable Decal", Float) = 0
        _DecalTex ("Decal Texture", 2D) = "white" {}
        _DecalBounds ("Decal Bounds (centerX, centerZ, sizeX, sizeZ)", Vector) = (0, 0, 10, 10)
        _DecalRotation ("Decal Rotation (radians)", Float) = 0
        _DecalBlend ("Decal Blend", Range(0, 1)) = 1
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
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }
            
            Cull Off
            
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile _ _ADDITIONAL_LIGHTS
            #pragma multi_compile_instancing
            
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "GrassCommon.hlsl"
            
            StructuredBuffer<GrassDrawData> _GrassBuffer;
            
            float4 _Interactors[16];
            int _InteractorCount;
            float _InteractorStrength;
            
            TEXTURE2D(_MainTex);
            TEXTURE2D(_NormalMap);
            TEXTURE2D(_TipMask);
            TEXTURE2D(_TerrainLightmap);
            TEXTURE2D(_DecalTex);
            SAMPLER(sampler_MainTex);
            SAMPLER(sampler_NormalMap);
            SAMPLER(sampler_TipMask);
            SAMPLER(sampler_TerrainLightmap);
            SAMPLER(sampler_DecalTex);
            
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
                float _Translucency;
                float _AlignNormals;
                float _UseTerrainLightmap;
                float _TerrainLightmapInfluence;
                float4 _TerrainPosition;
                float4 _TerrainSize;
                float _UseOnlyAlbedoColor;
                float _UseUniformScale;
                float4 _MeshRotation;
                float _MaxTiltAngle;
                float _TiltVariation;
                float _MaxBendAngle;
                float _DecalEnabled;
                float4 _DecalBounds;
                float _DecalRotation;
                float _DecalBlend;
            CBUFFER_END
            
            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float4 tangentOS : TANGENT;
                float2 uv : TEXCOORD0;
                uint instanceID : SV_InstanceID;
            };
            
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                float3 normalWS : TEXCOORD2;
                float3 viewDirWS : TEXCOORD3;
                float3 grassColor : TEXCOORD4;
                float patternMask : TEXCOORD5;
                float fogFactor : TEXCOORD6;
                float3 tangentWS : TEXCOORD7;
                float3 bitangentWS : TEXCOORD8;
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
                
                [loop]
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
                
                // Reduce wind based on interaction (grass under object stops moving)
                float windReduction = 1.0 - maxInfluence;
                windOffset *= windReduction;
                
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
                
                if (_AlignNormals > 0.5)
                    output.normalWS = float3(0, 1, 0);
                else
                    output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                
                float3 tangent = TransformObjectToWorldDir(input.tangentOS.xyz);
                output.tangentWS = tangent;
                output.bitangentWS = cross(output.normalWS, tangent) * input.tangentOS.w;
                
                output.viewDirWS = GetWorldSpaceNormalizeViewDir(worldPos);
                output.grassColor = grassData.color;
                output.patternMask = grassData.patternMask;
                output.fogFactor = ComputeFogFactor(output.positionCS.z);
                
                return output;
            }
            
            half4 frag(Varyings input) : SV_Target
            {
                // Default mode: no textures, just solid color
                // Custom mesh mode: sample textures
                bool isDefaultMode = _UseUniformScale < 0.5;
                
                half4 albedo = half4(1, 1, 1, 1);
                half3 normalTS = half3(0, 0, 1);
                
                // Sample textures when in Custom Mesh mode OR when Use Only Albedo Color is enabled
                bool shouldSampleTextures = !isDefaultMode || _UseOnlyAlbedoColor > 0.5;
                if (shouldSampleTextures)
                {
                    albedo = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv);
                    normalTS = UnpackNormal(SAMPLE_TEXTURE2D(_NormalMap, sampler_NormalMap, input.uv));
                }
                
                if (_UseTipCutout > 0.5 && !isDefaultMode)
                {
                    half tipMask = SAMPLE_TEXTURE2D(_TipMask, sampler_TipMask, input.uv).a;
                    float tipCut = CalculateTipCutout(input.uv.y, _TipCutoff);
                    clip(tipMask * tipCut - _AlphaCutoff);
                }
                
                float3x3 tangentToWorld = float3x3(
                    normalize(input.tangentWS),
                    normalize(input.bitangentWS),
                    normalize(input.normalWS)
                );
                float3 normalWS = mul(normalTS, tangentToWorld);
                
                // ========================================
                // COLOR MODE SYSTEM
                // 0 = Albedo: pure texture (NO depth effects)
                // 1 = Tint: TopTint/BottomTint gradient
                // 2 = Patterns: Stripes/Checkerboard/NaturalBlend
                // ========================================
                half3 baseColor;
                half4 albedoTex = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv);
                
                // Mode 0: Albedo - PURE texture color (no depth effects)
                if (_ColorMode < 0.5)
                {
                    baseColor = albedoTex.rgb;
                    
                    // Decal projection (optional overlay)
                    if (_DecalEnabled > 0.5)
                    {
                        float2 relPos = input.positionWS.xz - _DecalBounds.xy;
                        float cosR = cos(-_DecalRotation);
                        float sinR = sin(-_DecalRotation);
                        float2 rotatedPos = float2(
                            relPos.x * cosR - relPos.y * sinR,
                            relPos.x * sinR + relPos.y * cosR
                        );
                        float2 decalUV = rotatedPos / _DecalBounds.zw + 0.5;
                        if (decalUV.x >= 0 && decalUV.x <= 1 && decalUV.y >= 0 && decalUV.y <= 1)
                        {
                            half4 decalColor = SAMPLE_TEXTURE2D(_DecalTex, sampler_DecalTex, decalUV);
                            baseColor = lerp(baseColor, decalColor.rgb, decalColor.a * _DecalBlend);
                        }
                    }
                    
                    // Pure PBR lighting, no depth effects
                    InputData inputData = (InputData)0;
                    inputData.positionWS = input.positionWS;
                    inputData.normalWS = normalize(normalWS);
                    inputData.viewDirectionWS = normalize(input.viewDirWS);
                    inputData.shadowCoord = TransformWorldToShadowCoord(input.positionWS);
                    inputData.fogCoord = input.fogFactor;
                    inputData.bakedGI = SampleSH(inputData.normalWS);
                    inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(input.positionCS);
                    
                    SurfaceData surfaceData = (SurfaceData)0;
                    surfaceData.albedo = baseColor;
                    surfaceData.alpha = 1.0;
                    surfaceData.metallic = 0;
                    surfaceData.smoothness = 0.1;
                    surfaceData.normalTS = normalTS;
                    surfaceData.occlusion = 1;
                    
                    half4 color = UniversalFragmentPBR(inputData, surfaceData);
                    color.rgb = MixFog(color.rgb, input.fogFactor);
                    return color;
                }
                
                // Mode 1: Tint - TopTint/BottomTint gradient (PURE colors, PBR only)
                else if (_ColorMode < 1.5)
                {
                    baseColor = lerp(_BottomTint.rgb, _TopTint.rgb, input.uv.y);
                    
                    // Terrain lightmap blending (only in tint mode)
                    if (_UseTerrainLightmap > 0.5)
                    {
                        float2 terrainUV = (input.positionWS.xz - _TerrainPosition.xz) / _TerrainSize.xz;
                        terrainUV = saturate(terrainUV);
                        half3 terrainLight = SAMPLE_TEXTURE2D(_TerrainLightmap, sampler_TerrainLightmap, terrainUV).rgb;
                        baseColor = lerp(baseColor, baseColor * terrainLight, _TerrainLightmapInfluence);
                    }
                    
                    // Optional albedo blend
                    if (_UseAlbedoBlend > 0.5)
                    {
                        baseColor = lerp(baseColor, baseColor * albedoTex.rgb * 2.0, _AlbedoBlendAmount);
                    }
                    
                    // Decal projection (optional overlay)
                    if (_DecalEnabled > 0.5)
                    {
                        float2 relPos = input.positionWS.xz - _DecalBounds.xy;
                        float cosR = cos(-_DecalRotation);
                        float sinR = sin(-_DecalRotation);
                        float2 rotatedPos = float2(
                            relPos.x * cosR - relPos.y * sinR,
                            relPos.x * sinR + relPos.y * cosR
                        );
                        float2 decalUV = rotatedPos / _DecalBounds.zw + 0.5;
                        if (decalUV.x >= 0 && decalUV.x <= 1 && decalUV.y >= 0 && decalUV.y <= 1)
                        {
                            half4 decalColor = SAMPLE_TEXTURE2D(_DecalTex, sampler_DecalTex, decalUV);
                            baseColor = lerp(baseColor, decalColor.rgb, decalColor.a * _DecalBlend);
                        }
                    }
                    
                    // Pure PBR lighting, no extra effects
                    InputData inputData = (InputData)0;
                    inputData.positionWS = input.positionWS;
                    inputData.normalWS = normalize(normalWS);
                    inputData.viewDirectionWS = normalize(input.viewDirWS);
                    inputData.shadowCoord = TransformWorldToShadowCoord(input.positionWS);
                    inputData.fogCoord = input.fogFactor;
                    inputData.bakedGI = SampleSH(inputData.normalWS);
                    inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(input.positionCS);
                    
                    SurfaceData surfaceData = (SurfaceData)0;
                    surfaceData.albedo = baseColor;
                    surfaceData.alpha = 1.0;
                    surfaceData.metallic = 0;
                    surfaceData.smoothness = 0.1;
                    surfaceData.normalTS = normalTS;
                    surfaceData.occlusion = 1;
                    
                    half4 color = UniversalFragmentPBR(inputData, surfaceData);
                    color.rgb = MixFog(color.rgb, input.fogFactor);
                    return color;
                }
                // Mode 2: Patterns - Stripes/Checkerboard/NaturalBlend
                else
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
                        float noise1 = 0, noise2 = 0;
                        
                        // Select natural blend type (0=BlueNoise, 1=Cluster, 2=Gradient, 3=Stochastic)
                        if (_NaturalBlendType < 0.5)
                        {
                            noise1 = BlueNoise(worldPos / _NaturalScale);
                            noise2 = BlueNoise(worldPos / _NaturalScale * 1.7 + 50);
                        }
                        else if (_NaturalBlendType < 1.5)
                        {
                            noise1 = ClusterNoise(worldPos / _NaturalScale);
                            noise2 = ClusterNoise(worldPos / _NaturalScale * 1.3 + 25);
                        }
                        else if (_NaturalBlendType < 2.5)
                        {
                            noise1 = GradientBlend(worldPos / _NaturalScale);
                            noise2 = GradientBlend(worldPos / _NaturalScale * 1.5 + 40);
                        }
                        else
                        {
                            noise1 = StochasticNoise(worldPos / _NaturalScale);
                            noise2 = StochasticNoise(worldPos / _NaturalScale * 1.2 + 30);
                        }
                        
                        // Apply softness and contrast
                        float sharpness = 1.0 - _NaturalSoftness;
                        noise1 = saturate((noise1 - 0.5) * (1.0 + _NaturalContrast * 3.0) + 0.5);
                        noise2 = saturate((noise2 - 0.5) * (1.0 + _NaturalContrast * 3.0) + 0.5);
                        
                        // Blend 3 colors with proper tip/root gradients
                        half3 color1 = lerp(_NaturalColor1Root.rgb, _NaturalColor1Tip.rgb, input.uv.y);
                        half3 color2 = lerp(_NaturalColor2Root.rgb, _NaturalColor2Tip.rgb, input.uv.y);
                        half3 color3 = lerp(_NaturalColor3Root.rgb, _NaturalColor3Tip.rgb, input.uv.y);
                        
                        // Blend colors based on noise values
                        half3 blendedColor = lerp(color1, color2, smoothstep(0.3 - sharpness * 0.2, 0.7 + sharpness * 0.2, noise1));
                        blendedColor = lerp(blendedColor, color3, smoothstep(0.4 - sharpness * 0.2, 0.6 + sharpness * 0.2, noise2) * 0.5);
                        
                        baseColor = blendedColor;
                        
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
                    
                    // Terrain lightmap for patterns (optional)
                    if (_UseTerrainLightmap > 0.5)
                    {
                        float2 terrainUV = (input.positionWS.xz - _TerrainPosition.xz) / _TerrainSize.xz;
                        terrainUV = saturate(terrainUV);
                        half3 terrainLight = SAMPLE_TEXTURE2D(_TerrainLightmap, sampler_TerrainLightmap, terrainUV).rgb;
                        baseColor = lerp(baseColor, baseColor * terrainLight, _TerrainLightmapInfluence);
                    }
                    
                    // Decal projection (optional overlay)
                    if (_DecalEnabled > 0.5)
                    {
                        float2 relPos = input.positionWS.xz - _DecalBounds.xy;
                        float cosR = cos(-_DecalRotation);
                        float sinR = sin(-_DecalRotation);
                        float2 rotatedPos = float2(
                            relPos.x * cosR - relPos.y * sinR,
                            relPos.x * sinR + relPos.y * cosR
                        );
                        float2 decalUV = rotatedPos / _DecalBounds.zw + 0.5;
                        if (decalUV.x >= 0 && decalUV.x <= 1 && decalUV.y >= 0 && decalUV.y <= 1)
                        {
                            half4 decalColor = SAMPLE_TEXTURE2D(_DecalTex, sampler_DecalTex, decalUV);
                            baseColor = lerp(baseColor, decalColor.rgb, decalColor.a * _DecalBlend);
                        }
                    }
                    
                    // Pure PBR lighting, no extra effects
                    InputData inputData = (InputData)0;
                    inputData.positionWS = input.positionWS;
                    inputData.normalWS = normalize(normalWS);
                    inputData.viewDirectionWS = normalize(input.viewDirWS);
                    inputData.shadowCoord = TransformWorldToShadowCoord(input.positionWS);
                    inputData.fogCoord = input.fogFactor;
                    inputData.bakedGI = SampleSH(inputData.normalWS);
                    inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(input.positionCS);
                    
                    SurfaceData surfaceData = (SurfaceData)0;
                    surfaceData.albedo = baseColor;
                    surfaceData.alpha = 1.0;
                    surfaceData.metallic = 0;
                    surfaceData.smoothness = 0.1;
                    surfaceData.normalTS = normalTS;
                    surfaceData.occlusion = 1;
                    
                    half4 color = UniversalFragmentPBR(inputData, surfaceData);
                    color.rgb = MixFog(color.rgb, input.fogFactor);
                    return color;
                }
            }
            ENDHLSL
        }
        
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
            
            float4 _Interactors[16];
            int _InteractorCount;
            float _InteractorStrength;
            
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
                float _Translucency;
                float _AlignNormals;
                float _UseTerrainLightmap;
                float _TerrainLightmapInfluence;
                float4 _TerrainPosition;
                float4 _TerrainSize;
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
    
    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
