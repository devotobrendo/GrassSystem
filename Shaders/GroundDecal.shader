// Copyright (c) 2026 Brendo Otavio Carvalho de Matos. All rights reserved.
// Ultra-lightweight ground decal shader for URP — Quad-based, no depth sampling.
// Skips grass pixels via stencil buffer (bit 6 / value 64).
// Draw distance fade is done in-shader using _WorldSpaceCameraPos (always correct camera).

Shader "GrassSystem/GroundDecal"
{
    Properties
    {
        [Header(Decal)]
        _MainTex ("Decal Texture", 2D) = "white" {}
        _Color ("Tint Color", Color) = (1, 1, 1, 1)
        _Blend ("Opacity", Range(0, 1)) = 1
        [Enum(Override,0,Multiply,1,Additive,2)] _BlendMode ("Blend Mode", Float) = 0
        
        [Header(Draw Distance)]
        _DrawDistance ("Draw Distance", Float) = 1000
        _StartFade ("Start Fade", Range(0, 1)) = 0.9
    }
    
    SubShader
    {
        Tags 
        { 
            "RenderType" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Transparent-1"
        }
        
        Pass
        {
            Name "GroundDecal"
            Tags { "LightMode" = "UniversalForward" }
            
            // Skip grass pixels (stencil bit 6 = value 64)
            Stencil
            {
                Ref 64
                ReadMask 64
                Comp NotEqual
            }
            
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            ZTest LEqual
            Cull Back
            Offset -1, -1
            
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            
            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            
            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                half4 _Color;
                half _Blend;
                half _BlendMode;
                float _DrawDistance;
                half _StartFade;
            CBUFFER_END
            
            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };
            
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                half2 fogAndFade : TEXCOORD1; // x = fog, y = distance fade
            };
            
            Varyings vert(Attributes input)
            {
                Varyings output;
                float3 worldPos = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = TransformWorldToHClip(worldPos);
                output.uv = TRANSFORM_TEX(input.uv, _MainTex);
                
                // Fog
                output.fogAndFade.x = ComputeFogFactor(output.positionCS.z);
                
                // Draw distance fade — _WorldSpaceCameraPos is always the
                // camera currently rendering (game, scene view, reflection probes).
                float dist = distance(_WorldSpaceCameraPos.xyz, worldPos);
                float fadeStart = _DrawDistance * _StartFade;
                float fadeRange = max(_DrawDistance - fadeStart, 0.001);
                
                // Branchless: when _DrawDistance <= 0, hasLimit = 0, result = 1 (infinite)
                float hasLimit = step(0.001, _DrawDistance);
                float fade = saturate(1.0 - (dist - fadeStart) / fadeRange);
                output.fogAndFade.y = lerp(1.0, fade, hasLimit);
                
                return output;
            }
            
            half4 frag(Varyings input) : SV_Target
            {
                half4 decal = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv) * _Color;
                half distFade = input.fogAndFade.y;
                half alpha = decal.a * _Blend * distFade;
                
                // Branchless blend mode
                half isMultiply = step(0.5, _BlendMode) * step(_BlendMode, 1.5);
                half isAdditive = step(1.5, _BlendMode);
                
                half3 rgb = decal.rgb;
                half a = alpha;
                
                rgb = lerp(rgb, lerp(half3(1, 1, 1), decal.rgb, decal.a), isMultiply);
                rgb = lerp(rgb, decal.rgb * alpha, isAdditive);
                a = lerp(a, step(0.001, alpha), isAdditive);
                
                rgb = MixFog(rgb, input.fogAndFade.x);
                
                return half4(rgb, a);
            }
            ENDHLSL
        }
    }
    
    Fallback Off
}
