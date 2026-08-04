// Copyright (c) 2026 Brendo Otavio Carvalho de Matos. All rights reserved.

#ifndef GRASS_GROUND_BLEND_INCLUDED
#define GRASS_GROUND_BLEND_INCLUDED

TEXTURE2D(_GrassOverrideMap);
SAMPLER(sampler_GrassOverrideMap);
TEXTURE2D(_GrassMultiplyMap);

half3 GrassGroundBlend(half3 albedo, float3 positionWS, float4 decalBounds, half blend)
{
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

    half3 grass = albedo;
    grass = lerp(grass, grass * mul.rgb, mul.a * inside);
    grass = lerp(grass, ovr.rgb, ovr.a * inside);

    half coverage = saturate(max(ovr.a, mul.a)) * inside;
    return lerp(albedo, grass, coverage * blend);
}

#endif
