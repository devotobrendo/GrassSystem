// Copyright (c) 2026 Brendo Otavio Carvalho de Matos. All rights reserved.

Shader "GrassSystem/GrassUnlitConsole"
{
    Properties
    {
        [Header(Textures)]
        _MainTex ("Albedo (RGB)", 2D) = "white" {}

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

        [Header(Shadow Receiving)]
        _ShadowIntensity ("Shadow Intensity", Range(0, 1)) = 0.5

        [Header(Depth Perception)]
        _InstanceColorVariation ("Instance Color Variation", Range(0, 0.3)) = 0
        _HeightDarkening ("Height Darkening", Range(0, 0.5)) = 0
        _BackfaceDarkening ("Backface Darkening", Range(0, 0.5)) = 0

        [Header(Custom Mesh Mode)]
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

        [Header(Baked Decal Map)]
        _BakedOverrideMap ("Baked Override Map", 2D) = "black" {}
        _BakedMultiplyMap ("Baked Multiply Map", 2D) = "white" {}
        _BakedAdditiveMap ("Baked Additive Map", 2D) = "black" {}
        _BakedDecalBounds ("Baked Decal Bounds (minX, minZ, sizeX, sizeZ)", Vector) = (0, 0, 100, 100)

        [Header(Debug)]
        _DebugBladeScale ("Debug Blade Scale (fill test)", Range(0, 1)) = 1
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

            Stencil
            {
                Ref 64
                Comp Always
                Pass Replace
            }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #pragma multi_compile_instancing

            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _SHADOWS_SOFT

            #pragma multi_compile_local _ _DECALS_ON
            #pragma multi_compile_local _ _BAKED_DECALS
            #pragma multi_compile_local _ _LIGHTPROBES_ON
            #pragma multi_compile_local _ _RECEIVE_SHADOWS_ON
            #pragma multi_compile_local _ _TIPCUTOUT_ON

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "GrassCommonSlim.hlsl"

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
            TEXTURE2D(_BakedOverrideMap);
            TEXTURE2D(_BakedMultiplyMap);
            TEXTURE2D(_BakedAdditiveMap);
            SAMPLER(sampler_MainTex);
            SAMPLER(sampler_TipMask);
            SAMPLER(sampler_DecalTex);
            SAMPLER(sampler_Decal2Tex);
            SAMPLER(sampler_Decal3Tex);
            SAMPLER(sampler_Decal4Tex);
            SAMPLER(sampler_Decal5Tex);
            SAMPLER(sampler_BakedOverrideMap);
            SAMPLER(sampler_BakedMultiplyMap);
            SAMPLER(sampler_BakedAdditiveMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                float _UseTipCutout;
                float _TipCutoff;
                float _AlphaCutoff;
                float _WindSpeed;
                float _WindStrength;
                float _WindFrequency;
                float _AmbientBoost;
                float _LightProbeInfluence;
                float _ShadowIntensity;
                float _InstanceColorVariation;
                float _HeightDarkening;
                float _BackfaceDarkening;
                float _UseUniformScale;
                float4 _MeshRotation;
                float _MaxTiltAngle;
                float _TiltVariation;
                float _MaxBendAngle;
                float _DecalEnabled;
                float4 _DecalBounds;
                float _DecalRotation;
                float _DecalBlend;
                float _DecalBlendMode;
                float _Decal2Enabled;
                float4 _Decal2Bounds;
                float _Decal2Rotation;
                float _Decal2Blend;
                float _Decal2BlendMode;
                float _Decal3Enabled;
                float4 _Decal3Bounds;
                float _Decal3Rotation;
                float _Decal3Blend;
                float _Decal3BlendMode;
                float _Decal4Enabled;
                float4 _Decal4Bounds;
                float _Decal4Rotation;
                float _Decal4Blend;
                float _Decal4BlendMode;
                float _Decal5Enabled;
                float4 _Decal5Bounds;
                float _Decal5Rotation;
                float _Decal5Blend;
                float _Decal5BlendMode;
                float4 _BakedDecalBounds;
                float _DebugBladeScale;
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
                half fogFactor : TEXCOORD2;
                half instanceVariation : TEXCOORD3;
                #if defined(_RECEIVE_SHADOWS_ON)
                float4 shadowCoord : TEXCOORD4;
                #endif
                #if defined(_LIGHTPROBES_ON)
                half3 lightProbeColor : TEXCOORD5;
                #endif
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
                        float distXZ = length(grassData.position.xz - interactorPos.xz);

                        float heightAboveBase = interactorPos.y - grassBase;

                        if (distXZ < radius)
                        {
                            float hInfluence = 1.0 - (distXZ / radius);
                            hInfluence = hInfluence * hInfluence;

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

                                interactionOffset += pushDir * influence * _InteractorStrength * 2.0;
                            }
                        }
                    }
                }

                windOffset *= (1.0 - maxInfluence);

                float3 worldPos = TransformGrassVertex(
                    input.positionOS.xyz,
                    grassData.position,
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

                worldPos = grassData.position + (worldPos - grassData.position) * _DebugBladeScale;

                output.positionWS = worldPos;
                output.positionCS = TransformWorldToHClip(worldPos);
                output.uv = TRANSFORM_TEX(input.uv, _MainTex);
                output.fogFactor = ComputeFogFactor(output.positionCS.z);

                output.instanceVariation = Hash(grassData.position.xz + float2(5.123, 7.456));

                #if defined(_RECEIVE_SHADOWS_ON)
                output.shadowCoord = TransformWorldToShadowCoord(worldPos);
                #endif

                #if defined(_LIGHTPROBES_ON)
                output.lightProbeColor = SampleSH(float3(0, 1, 0));
                #endif

                return output;
            }

            half4 frag(Varyings input, bool isFrontFace : SV_IsFrontFace) : SV_Target
            {
                #if defined(_TIPCUTOUT_ON)
                half tipMask = SAMPLE_TEXTURE2D(_TipMask, sampler_TipMask, input.uv).a;
                if (input.uv.y > _TipCutoff && tipMask < _AlphaCutoff)
                    discard;
                #endif

                half4 albedoTex = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv);
                half3 baseColor = albedoTex.rgb;

                #if defined(_DECALS_ON)
                DecalResult d1, d2, d3, d4, d5;
                d1.color = baseColor; d1.applied = 0;
                d2.color = baseColor; d2.applied = 0;
                d3.color = baseColor; d3.applied = 0;
                d4.color = baseColor; d4.applied = 0;
                d5.color = baseColor; d5.applied = 0;

                if (_DecalEnabled > 0.5)
                {
                    float2 uv = CalculateDecalUV(input.positionWS, _DecalBounds, _DecalRotation);
                    half4 s = SAMPLE_TEXTURE2D(_DecalTex, sampler_DecalTex, uv);
                    d1 = ApplyDecalLayer(baseColor, input.positionWS, _DecalEnabled, _DecalBounds, _DecalRotation, _DecalBlend, _DecalBlendMode, s, uv);
                }

                if (_Decal2Enabled > 0.5)
                {
                    float2 uv = CalculateDecalUV(input.positionWS, _Decal2Bounds, _Decal2Rotation);
                    half4 s = SAMPLE_TEXTURE2D(_Decal2Tex, sampler_Decal2Tex, uv);
                    d2 = ApplyDecalLayer(d1.color, input.positionWS, _Decal2Enabled, _Decal2Bounds, _Decal2Rotation, _Decal2Blend, _Decal2BlendMode, s, uv);
                }
                else { d2.color = d1.color; }

                if (_Decal3Enabled > 0.5)
                {
                    float2 uv = CalculateDecalUV(input.positionWS, _Decal3Bounds, _Decal3Rotation);
                    half4 s = SAMPLE_TEXTURE2D(_Decal3Tex, sampler_Decal3Tex, uv);
                    d3 = ApplyDecalLayer(d2.color, input.positionWS, _Decal3Enabled, _Decal3Bounds, _Decal3Rotation, _Decal3Blend, _Decal3BlendMode, s, uv);
                }
                else { d3.color = d2.color; }

                if (_Decal4Enabled > 0.5)
                {
                    float2 uv = CalculateDecalUV(input.positionWS, _Decal4Bounds, _Decal4Rotation);
                    half4 s = SAMPLE_TEXTURE2D(_Decal4Tex, sampler_Decal4Tex, uv);
                    d4 = ApplyDecalLayer(d3.color, input.positionWS, _Decal4Enabled, _Decal4Bounds, _Decal4Rotation, _Decal4Blend, _Decal4BlendMode, s, uv);
                }
                else { d4.color = d3.color; }

                if (_Decal5Enabled > 0.5)
                {
                    float2 uv = CalculateDecalUV(input.positionWS, _Decal5Bounds, _Decal5Rotation);
                    half4 s = SAMPLE_TEXTURE2D(_Decal5Tex, sampler_Decal5Tex, uv);
                    d5 = ApplyDecalLayer(d4.color, input.positionWS, _Decal5Enabled, _Decal5Bounds, _Decal5Rotation, _Decal5Blend, _Decal5BlendMode, s, uv);
                }
                else { d5.color = d4.color; }

                baseColor = d5.color;

                half anyDecalApplied = saturate(d1.applied + d2.applied + d3.applied + d4.applied + d5.applied);
                half albedoLuma = dot(albedoTex.rgb, half3(0.299, 0.587, 0.114));
                half lumaFactor = lerp(1.0, albedoLuma * 2.0, 0.5);
                baseColor = lerp(baseColor, baseColor * lumaFactor, anyDecalApplied);
                #endif

                #if defined(_BAKED_DECALS)
                {
                    float2 bakedUV = (input.positionWS.xz - _BakedDecalBounds.xy) / _BakedDecalBounds.zw;
                    float2 inBounds = step(float2(0, 0), bakedUV) * step(bakedUV, float2(1, 1));
                    float isInMap = inBounds.x * inBounds.y;

                    half4 bakedOverride = SAMPLE_TEXTURE2D(_BakedOverrideMap, sampler_BakedOverrideMap, bakedUV);
                    half4 bakedMultiply = SAMPLE_TEXTURE2D(_BakedMultiplyMap, sampler_BakedMultiplyMap, bakedUV);
                    half4 bakedAdditive = SAMPLE_TEXTURE2D(_BakedAdditiveMap, sampler_BakedAdditiveMap, bakedUV);

                    float overrideCoverage = bakedOverride.a * isInMap;
                    float multiplyCoverage = bakedMultiply.a * isInMap;
                    float additiveCoverage = bakedAdditive.a * isInMap;
                    float decalCoverage = saturate(overrideCoverage + multiplyCoverage + additiveCoverage);

                    baseColor = lerp(baseColor, baseColor * bakedMultiply.rgb, multiplyCoverage);
                    baseColor += bakedAdditive.rgb * isInMap;
                    baseColor = lerp(baseColor, bakedOverride.rgb, overrideCoverage);

                    half albedoLuma = dot(albedoTex.rgb, half3(0.299, 0.587, 0.114));
                    half lumaFactor = lerp(1.0, albedoLuma * 2.0, 0.5);
                    baseColor = lerp(baseColor, baseColor * lumaFactor, decalCoverage);
                }
                #endif

                baseColor += (input.instanceVariation - 0.5) * _InstanceColorVariation;

                baseColor *= lerp(1.0 - _HeightDarkening, 1.0, input.uv.y);

                baseColor *= isFrontFace ? 1.0 : (1.0 - _BackfaceDarkening);

                #if defined(_RECEIVE_SHADOWS_ON)
                half shadowAtten = MainLightRealtimeShadow(input.shadowCoord);
                baseColor *= lerp(1.0, shadowAtten, _ShadowIntensity);
                #endif

                #if defined(_LIGHTPROBES_ON)
                half3 ambient = input.lightProbeColor * _AmbientBoost;
                baseColor *= lerp(1.0, ambient, _LightProbeInfluence);
                #endif

                half3 finalColor = MixFog(baseColor, input.fogFactor);
                return half4(finalColor, 1.0);
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
            #include "GrassCommonSlim.hlsl"

            StructuredBuffer<GrassDrawData> _GrassBuffer;

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                float _UseTipCutout;
                float _TipCutoff;
                float _AlphaCutoff;
                float _WindSpeed;
                float _WindStrength;
                float _WindFrequency;
                float _AmbientBoost;
                float _LightProbeInfluence;
                float _ShadowIntensity;
                float _InstanceColorVariation;
                float _HeightDarkening;
                float _BackfaceDarkening;
                float _UseUniformScale;
                float4 _MeshRotation;
                float _MaxTiltAngle;
                float _TiltVariation;
                float _MaxBendAngle;
                float _DecalEnabled;
                float4 _DecalBounds;
                float _DecalRotation;
                float _DecalBlend;
                float _DecalBlendMode;
                float _Decal2Enabled;
                float4 _Decal2Bounds;
                float _Decal2Rotation;
                float _Decal2Blend;
                float _Decal2BlendMode;
                float _Decal3Enabled;
                float4 _Decal3Bounds;
                float _Decal3Rotation;
                float _Decal3Blend;
                float _Decal3BlendMode;
                float _Decal4Enabled;
                float4 _Decal4Bounds;
                float _Decal4Rotation;
                float _Decal4Blend;
                float _Decal4BlendMode;
                float _Decal5Enabled;
                float4 _Decal5Bounds;
                float _Decal5Rotation;
                float _Decal5Blend;
                float _Decal5BlendMode;
                float4 _BakedDecalBounds;
                float _DebugBladeScale;
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

                worldPos = grassData.position + (worldPos - grassData.position) * _DebugBladeScale;

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
