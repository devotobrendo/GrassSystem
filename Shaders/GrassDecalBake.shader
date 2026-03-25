Shader "Hidden/GrassSystem/DecalBake"
{
    Properties
    {
        _DecalTex ("Decal Texture", 2D) = "white" {}
        _PreviousMap ("Previous Composite", 2D) = "white" {}
        _DecalBounds ("Decal Bounds (posX, posZ, sizeX, sizeZ)", Vector) = (0, 0, 10, 10)
        _DecalRotation ("Decal Rotation (radians)", Float) = 0
        _DecalBlend ("Decal Blend", Float) = 1
        _DecalBlendMode ("Blend Mode (0=Override,1=Multiply,2=Additive)", Float) = 0
        _BakeTargetMode ("Bake Target Mode (0=Override,1=Multiply,2=Additive)", Float) = 0
        _MapBounds ("Map Bounds (minX, minZ, sizeX, sizeZ)", Vector) = (0, 0, 100, 100)
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" }
        Cull Off
        ZWrite Off
        ZTest Always

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            TEXTURE2D(_DecalTex);
            TEXTURE2D(_PreviousMap);
            SAMPLER(sampler_DecalTex);
            SAMPLER(sampler_PreviousMap);

            float4 _DecalBounds;
            float _DecalRotation;
            float _DecalBlend;
            float _DecalBlendMode;
            float _BakeTargetMode;
            float4 _MapBounds;

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                return output;
            }

            float4 frag(Varyings input) : SV_Target
            {
                // Read the current composited result
                float4 previous = SAMPLE_TEXTURE2D(_PreviousMap, sampler_PreviousMap, input.uv);

                // Convert pixel UV to world-space XZ position
                float2 worldXZ = _MapBounds.xy + input.uv * _MapBounds.zw;

                // Calculate this decal's UV using the same math as GrassCommon.hlsl CalculateDecalUV
                float2 relPos = worldXZ - _DecalBounds.xy;
                float cosR = cos(_DecalRotation);
                float sinR = sin(_DecalRotation);
                float2 rotatedPos = float2(
                    relPos.x * cosR - relPos.y * sinR,
                    relPos.x * sinR + relPos.y * cosR
                );
                float2 decalUV = float2(rotatedPos.x / _DecalBounds.z, -rotatedPos.y / _DecalBounds.w) + 0.5;

                // Bounds check
                float2 inside = step(float2(0, 0), decalUV) * step(decalUV, float2(1, 1));
                float isInside = inside.x * inside.y;

                // Sample decal texture
                float4 decalSample = SAMPLE_TEXTURE2D(_DecalTex, sampler_DecalTex, decalUV);
                float hasAlpha = step(0.01, decalSample.a);
                float shouldApply = isInside * hasAlpha;

                float isTargetMode = 1.0 - step(0.5, abs(_DecalBlendMode - _BakeTargetMode));
                float finalBlend = shouldApply * decalSample.a * _DecalBlend * isTargetMode;

                // Override map stores standard straight-alpha color.
                if (_BakeTargetMode < 0.5)
                {
                    float outAlpha = previous.a + finalBlend * (1.0 - previous.a);
                    float3 prevContribution = previous.rgb * previous.a;
                    float3 outContribution = prevContribution * (1.0 - finalBlend) + decalSample.rgb * finalBlend;
                    float3 outColor = outAlpha > 0.0001 ? (outContribution / outAlpha) : float3(0.0, 0.0, 0.0);
                    return float4(outColor, outAlpha);
                }

                // Multiply map stores the factor used by:
                // base = lerp(base, base * baked.rgb, baked.a)
                if (_BakeTargetMode < 1.5)
                {
                    float outAlpha = previous.a + finalBlend * (1.0 - previous.a);
                    float oneMinusOutAlpha = 1.0 - outAlpha;
                    float3 prevFactor = lerp(float3(1.0, 1.0, 1.0), previous.rgb, previous.a);
                    float3 currentFactor = lerp(float3(1.0, 1.0, 1.0), decalSample.rgb * 2.0, finalBlend);
                    float3 outFactor = prevFactor * currentFactor;
                    float3 outColor = outAlpha > 0.0001
                        ? ((outFactor - oneMinusOutAlpha) / outAlpha)
                        : float3(1.0, 1.0, 1.0);
                    return float4(outColor, outAlpha);
                }

                // Additive map stores direct additive contribution in rgb and coverage in alpha.
                float outAdditiveAlpha = previous.a + finalBlend * (1.0 - previous.a);
                float3 outAdditive = previous.rgb + decalSample.rgb * finalBlend;
                return float4(outAdditive, outAdditiveAlpha);
            }
            ENDHLSL
        }
    }
}
