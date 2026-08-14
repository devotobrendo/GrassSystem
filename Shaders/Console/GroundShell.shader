// Copyright (c) 2026 Brendo Otavio Carvalho de Matos. All rights reserved.

Shader "GrassSystem/GroundShell"
{
    Properties
    {
        [NoScaleOffset] _BottomColorMap ("Bottom Color Map", 2D) = "white" {}
        _BottomTint ("Bottom Tint", Color) = (1,1,1,1)

        [NoScaleOffset] _TopColorMap ("Top Color Map", 2D) = "white" {}
        _TopTint ("Top Tint", Color) = (1,1,1,1)

        _NoiseMap ("Noise Map (tiling/offset)", 2D) = "gray" {}
        _Coverage ("Coverage", Range(0, 1)) = 0.3

        [Enum(Off,0,On,1)] _ReceiveShadows ("Receive Shadows", Float) = 1

        [Toggle(_LIGHTMAP_FROM_UV0)] _LightmapFromUV0 ("Lightmap UVs from UV0", Float) = 0
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType"     = "Opaque"
            "Queue"          = "Geometry"
        }

        HLSLINCLUDE
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _NoiseMap_ST;
                float4 _BottomTint;
                float4 _TopTint;
                float  _Coverage;
                float  _ReceiveShadows;
            CBUFFER_END

            TEXTURE2D(_BottomColorMap);  SAMPLER(sampler_BottomColorMap);
            TEXTURE2D(_TopColorMap);
            TEXTURE2D(_NoiseMap);        SAMPLER(sampler_NoiseMap);

            half3 GroundShellAlbedo(float2 uv)
            {
                half3 noiseSample = SAMPLE_TEXTURE2D(_NoiseMap, sampler_NoiseMap, TRANSFORM_TEX(uv, _NoiseMap)).rgb;
                half  noiseLuma   = dot(noiseSample, half3(0.299h, 0.587h, 0.114h));
                half  t           = lerp(half(_Coverage), 1.0h, noiseLuma);

                half3 bottom = SAMPLE_TEXTURE2D(_BottomColorMap, sampler_BottomColorMap, uv).rgb * half3(_BottomTint.rgb);
                half3 top    = SAMPLE_TEXTURE2D(_TopColorMap, sampler_BottomColorMap, uv).rgb * half3(_TopTint.rgb);

                return lerp(bottom, top, t);
            }
        ENDHLSL

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }
            Cull Back
            ZWrite On
            ZTest LEqual

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex   vert
            #pragma fragment frag

            #pragma multi_compile _ LIGHTMAP_ON
            #pragma multi_compile _ DIRLIGHTMAP_COMBINED
            #pragma multi_compile_fragment _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile_fog
            #pragma shader_feature_local_vertex _LIGHTMAP_FROM_UV0
            #pragma skip_variants STEREO_CUBEMAP_RENDER_ON STEREO_INSTANCING_ON STEREO_MULTIVIEW_ON UNITY_SINGLE_PASS_STEREO DOTS_INSTANCING_ON

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float2 uv         : TEXCOORD0;
                float2 lightmapUV : TEXCOORD1;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
                DECLARE_LIGHTMAP_OR_SH(lightmapUV, vertexSH, 1);
                half3  normalWS   : TEXCOORD2;
                float3 positionWS : TEXCOORD3;
                half   fogFactor  : TEXCOORD4;
            };

            Varyings vert(Attributes IN)
            {
                Varyings OUT = (Varyings)0;

                VertexPositionInputs positionInputs = GetVertexPositionInputs(IN.positionOS.xyz);
                VertexNormalInputs normalInputs = GetVertexNormalInputs(IN.normalOS);

                OUT.positionCS = positionInputs.positionCS;
                OUT.positionWS = positionInputs.positionWS;
                OUT.normalWS   = half3(normalInputs.normalWS);
                OUT.uv         = IN.uv;
                OUT.fogFactor  = half(ComputeFogFactor(positionInputs.positionCS.z));

                #if defined(_LIGHTMAP_FROM_UV0)
                    float2 lightmapSource = IN.uv;
                #else
                    float2 lightmapSource = IN.lightmapUV;
                #endif

                OUTPUT_LIGHTMAP_UV(lightmapSource, unity_LightmapST, OUT.lightmapUV);
                OUTPUT_SH(OUT.normalWS, OUT.vertexSH);

                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                half3 albedo = GroundShellAlbedo(IN.uv);

                float3 rawNormal = float3(IN.normalWS);
                half3 normalWS = half3(rawNormal * rsqrt(max(dot(rawNormal, rawNormal), 1.175494351e-38)));

                half3 bakedGI = SAMPLE_GI(IN.lightmapUV, IN.vertexSH, normalWS);

                Light mainLight = GetMainLight(TransformWorldToShadowCoord(IN.positionWS));
                half shadowAtten = lerp(1.0h, half(mainLight.shadowAttenuation), half(_ReceiveShadows));

                half3 color = albedo * bakedGI * shadowAtten;
                color = MixFog(color, IN.fogFactor);

                return half4(color, 1.0h);
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
            Cull Back

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex   ShadowVert
            #pragma fragment ShadowFrag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            float3 _LightDirection;

            struct ShadowAttributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
            };

            struct ShadowVaryings
            {
                float4 positionCS : SV_POSITION;
            };

            ShadowVaryings ShadowVert(ShadowAttributes IN)
            {
                ShadowVaryings OUT;

                float3 positionWS = TransformObjectToWorld(IN.positionOS.xyz);
                float3 normalWS   = TransformObjectToWorldNormal(IN.normalOS);
                OUT.positionCS = TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, _LightDirection));

                #if UNITY_REVERSED_Z
                    OUT.positionCS.z = min(OUT.positionCS.z, UNITY_NEAR_CLIP_VALUE);
                #else
                    OUT.positionCS.z = max(OUT.positionCS.z, UNITY_NEAR_CLIP_VALUE);
                #endif

                return OUT;
            }

            half4 ShadowFrag(ShadowVaryings IN) : SV_Target { return 0; }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            ZWrite On
            ColorMask 0
            Cull Back

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex   DepthVert
            #pragma fragment DepthFrag

            struct DepthAttributes { float4 positionOS : POSITION; };
            struct DepthVaryings   { float4 positionCS : SV_POSITION; };

            DepthVaryings DepthVert(DepthAttributes IN)
            {
                DepthVaryings OUT;
                OUT.positionCS = TransformObjectToHClip(IN.positionOS.xyz);
                return OUT;
            }

            half DepthFrag(DepthVaryings IN) : SV_Target { return IN.positionCS.z; }
            ENDHLSL
        }

        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode" = "DepthNormals" }
            ZWrite On
            Cull Back

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex   DepthNormalsVert
            #pragma fragment DepthNormalsFrag
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/RenderingLayers.hlsl"

            struct DepthNormalsAttributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
            };

            struct DepthNormalsVaryings
            {
                float4 positionCS : SV_POSITION;
                half3  normalWS   : TEXCOORD0;
            };

            DepthNormalsVaryings DepthNormalsVert(DepthNormalsAttributes IN)
            {
                DepthNormalsVaryings OUT = (DepthNormalsVaryings)0;
                OUT.positionCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.normalWS   = half3(TransformObjectToWorldNormal(IN.normalOS));
                return OUT;
            }

            void DepthNormalsFrag(
                DepthNormalsVaryings IN
                , out half4 outNormalWS : SV_Target0
#ifdef _WRITE_RENDERING_LAYERS
                , out float4 outRenderingLayers : SV_Target1
#endif
            )
            {
                float3 rawNormal = float3(IN.normalWS);
                outNormalWS = half4(half3(rawNormal * rsqrt(max(dot(rawNormal, rawNormal), 1.175494351e-38))), 0.0h);

                #ifdef _WRITE_RENDERING_LAYERS
                    uint renderingLayers = GetMeshRenderingLayer();
                    outRenderingLayers = float4(EncodeMeshRenderingLayer(renderingLayers), 0.0, 0.0, 0.0);
                #endif
            }
            ENDHLSL
        }

        Pass
        {
            Name "Meta"
            Tags { "LightMode" = "Meta" }
            Cull Off

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex   MetaVert
            #pragma fragment MetaFrag
            #pragma shader_feature_local_vertex _LIGHTMAP_FROM_UV0

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/MetaInput.hlsl"

            struct MetaAttributes
            {
                float4 positionOS : POSITION;
                float2 uv         : TEXCOORD0;
                float2 lightmapUV : TEXCOORD1;
                float2 dynamicLightmapUV : TEXCOORD2;
            };

            struct MetaVaryings
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
            };

            MetaVaryings MetaVert(MetaAttributes IN)
            {
                MetaVaryings OUT;

                #if defined(_LIGHTMAP_FROM_UV0)
                    float2 lightmapSource = IN.uv;
                #else
                    float2 lightmapSource = IN.lightmapUV;
                #endif

                OUT.positionCS = UnityMetaVertexPosition(IN.positionOS.xyz, lightmapSource,
                    IN.dynamicLightmapUV, unity_LightmapST, unity_DynamicLightmapST);
                OUT.uv = IN.uv;
                return OUT;
            }

            half4 MetaFrag(MetaVaryings IN) : SV_Target
            {
                MetaInput metaInput = (MetaInput)0;
                metaInput.Albedo = GroundShellAlbedo(IN.uv);

                return UnityMetaFragment(metaInput);
            }
            ENDHLSL
        }
    }

    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
