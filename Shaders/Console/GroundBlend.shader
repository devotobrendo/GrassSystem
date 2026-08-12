// Copyright (c) 2026 Brendo Otavio Carvalho de Matos. All rights reserved.

Shader "GrassSystem/GroundBlend"
{
    Properties
    {
        _BaseMap    ("Base Map", 2D)             = "white" {}
        _BaseColor  ("Base Color", Color)        = (1,1,1,1)
        _GrassBlend ("Grass Blend", Range(0, 1)) = 0.6

        [Enum(Off,0,On,1)] _ReceiveShadows ("Receive Shadows", Float) = 1

        [NoScaleOffset] _GrassOverrideMap ("Grass Override Map (from Bind Ground Blend)", 2D) = "black" {}
        [NoScaleOffset] _GrassMultiplyMap ("Grass Multiply Map (from Bind Ground Blend)", 2D) = "black" {}
        _GrassDecalBounds ("Grass Decal Bounds (minX, minZ, sizeX, sizeZ)", Vector) = (0, 0, 0, 0)
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
                float4 _BaseMap_ST;
                float4 _BaseColor;
                float  _GrassBlend;
                float  _ReceiveShadows;
                float4 _GrassDecalBounds;
            CBUFFER_END

            TEXTURE2D(_BaseMap);    SAMPLER(sampler_BaseMap);

            #include "GrassGroundBlend.hlsl"
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
                OUT.uv         = TRANSFORM_TEX(IN.uv, _BaseMap);
                OUT.fogFactor  = half(ComputeFogFactor(positionInputs.positionCS.z));

                OUTPUT_LIGHTMAP_UV(IN.lightmapUV, unity_LightmapST, OUT.lightmapUV);
                OUTPUT_SH(OUT.normalWS, OUT.vertexSH);

                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                half4 baseMap = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, IN.uv);
                half3 albedo  = baseMap.rgb * half3(_BaseColor.rgb);

                albedo = GrassGroundBlend(albedo, IN.positionWS, _GrassDecalBounds, half(_GrassBlend));

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
                OUT.positionCS = UnityMetaVertexPosition(IN.positionOS.xyz, IN.lightmapUV,
                    IN.dynamicLightmapUV, unity_LightmapST, unity_DynamicLightmapST);
                OUT.uv = TRANSFORM_TEX(IN.uv, _BaseMap);
                return OUT;
            }

            half4 MetaFrag(MetaVaryings IN) : SV_Target
            {
                half4 baseMap = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, IN.uv);

                MetaInput metaInput = (MetaInput)0;
                metaInput.Albedo = baseMap.rgb * half3(_BaseColor.rgb);

                return UnityMetaFragment(metaInput);
            }
            ENDHLSL
        }
    }

    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
