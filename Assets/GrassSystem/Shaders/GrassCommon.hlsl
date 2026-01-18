// Copyright (c) 2026 Brendo Otavio Carvalho de Matos. All rights reserved.

#ifndef GRASS_COMMON_INCLUDED
#define GRASS_COMMON_INCLUDED

struct GrassDrawData
{
    float3 position;
    float3 normal;
    float2 widthHeight;
    float3 color;
    float patternMask;
    float distanceScale;
};

float2 CalculateWind(float3 worldPos, float time, float speed, float strength, float frequency)
{
    float2 windUV = worldPos.xz * frequency;
    float2 wind1 = sin(windUV + time * speed) * strength;
    float2 wind2 = sin(windUV * 0.5 + time * speed * 0.7) * strength * 0.5;
    return wind1 + wind2;
}

float CalculateCheckerPattern(float3 worldPos, float scale)
{
    float2 patternUV = worldPos.xz / scale;
    float checker = fmod(floor(patternUV.x) + floor(patternUV.y), 2.0);
    return checker;
}

float3x3 RotationY(float angle)
{
    float s = sin(angle);
    float c = cos(angle);
    return float3x3(
        c, 0, s,
        0, 1, 0,
        -s, 0, c
    );
}

float Hash(float2 p)
{
    return frac(sin(dot(p, float2(12.9898, 78.233))) * 43758.5453);
}

float3 TransformGrassVertex(
    float3 localPos,
    float3 worldPivot,
    float3 surfaceNormal,
    float width,
    float height,
    float distanceScale,
    float2 windOffset,
    float3 interactionOffset,
    float uvY
)
{
    float3 scaledPos = localPos;
    scaledPos.x *= width;
    scaledPos.y *= height * distanceScale;
    
    float rotation = Hash(worldPivot.xz) * 6.28318;
    scaledPos = mul(RotationY(rotation), scaledPos);
    
    float windInfluence = uvY * uvY;
    scaledPos.xz += windOffset * windInfluence;
    
    float bendInfluence = uvY;
    scaledPos.xz += interactionOffset.xz * bendInfluence;
    scaledPos.y -= length(interactionOffset.xz) * bendInfluence * 0.3;
    
    float3 worldPos = worldPivot + scaledPos;
    return worldPos;
}

float CalculateTipCutout(float uvY, float cutoffHeight)
{
    return step(uvY, cutoffHeight);
}

#endif
