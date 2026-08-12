// Copyright (c) 2026 Brendo Otavio Carvalho de Matos. All rights reserved.

#ifndef GRASS_GROUND_BLEND_INCLUDED
#define GRASS_GROUND_BLEND_INCLUDED

TEXTURE2D(_GrassOverrideMap);
SAMPLER(sampler_GrassOverrideMap);
TEXTURE2D(_GrassMultiplyMap);
TEXTURE2D(_GrassAdditiveMap);

float _GrassBlendDebugEnabled;
float _GrassBlendDebugValue;

half3 GrassGroundBlend(half3 albedo, float3 positionWS, float4 decalBounds, half blend)
{
    blend = _GrassBlendDebugEnabled > 0.5 ? half(_GrassBlendDebugValue) : blend;

    if (blend <= 0.0h)
        return albedo;

    float2 uv = (positionWS.xz - decalBounds.xy) / max(decalBounds.zw, float2(1e-4, 1e-4));
    float2 bounds = step(float2(0.0, 0.0), uv) * step(uv, float2(1.0, 1.0));
    half inside = half(bounds.x * bounds.y);
    if (inside <= 0.0h)
        return albedo;

    float2 clamped = saturate(uv);
    half4 ovr = SAMPLE_TEXTURE2D(_GrassOverrideMap, sampler_GrassOverrideMap, clamped);
    half4 mul = SAMPLE_TEXTURE2D(_GrassMultiplyMap, sampler_GrassOverrideMap, clamped);
    half4 add = SAMPLE_TEXTURE2D(_GrassAdditiveMap, sampler_GrassOverrideMap, clamped);

    half overrideCoverage = ovr.a * inside;
    half multiplyCoverage = mul.a * inside;
    half additiveCoverage = add.a * inside;
    half decalCoverage = saturate(overrideCoverage + multiplyCoverage + additiveCoverage);

    half3 grass = albedo;
    grass = lerp(grass, grass * mul.rgb, multiplyCoverage);
    grass += add.rgb * inside;
    grass = lerp(grass, ovr.rgb, overrideCoverage);

    half albedoLuma = dot(albedo, half3(0.299h, 0.587h, 0.114h));
    half lumaFactor = lerp(1.0h, albedoLuma * 2.0h, 0.5h);
    grass = lerp(grass, grass * lumaFactor, decalCoverage);

    return lerp(albedo, grass, decalCoverage * blend);
}

#endif
