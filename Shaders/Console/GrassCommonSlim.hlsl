// Copyright (c) 2026 Brendo Otavio Carvalho de Matos. All rights reserved.

#ifndef GRASS_COMMON_SLIM_INCLUDED
#define GRASS_COMMON_SLIM_INCLUDED

struct GrassDrawData
{
    float3 position;
    float2 widthHeight;
    float distanceScale;
};

float Hash(float2 p)
{
    return frac(sin(dot(p, float2(12.9898, 78.233))) * 43758.5453);
}

float2 CalculateWind(float3 worldPos, float time, float speed, float strength, float frequency)
{
    float phaseOffset = Hash(worldPos.xz) * 6.28318;
    float amplitudeVariation = 0.7 + Hash(worldPos.xz + float2(1.618, 2.718)) * 0.6;

    float2 windUV = worldPos.xz * frequency;
    float2 wind1 = sin(windUV + time * speed + phaseOffset) * strength * amplitudeVariation;
    float2 wind2 = sin(windUV * 0.5 + time * speed * 0.7 + phaseOffset * 0.5) * strength * 0.5 * amplitudeVariation;
    return wind1 + wind2;
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

float3x3 RotationFromEuler(float3 eulerRad)
{
    float cx = cos(eulerRad.x); float sx = sin(eulerRad.x);
    float cy = cos(eulerRad.y); float sy = sin(eulerRad.y);
    float cz = cos(eulerRad.z); float sz = sin(eulerRad.z);

    return float3x3(
        cy*cz, sx*sy*cz - cx*sz, cx*sy*cz + sx*sz,
        cy*sz, sx*sy*sz + cx*cz, cx*sy*sz - sx*cz,
        -sy, sx*cy, cx*cy
    );
}

float3 TransformGrassVertex(
    float3 localPos,
    float3 worldPivot,
    float width,
    float height,
    float distanceScale,
    float2 windOffset,
    float3 interactionOffset,
    float uvY,
    float useUniformScale,
    float3 meshRotation,
    float maxTiltAngle,
    float tiltVariation,
    float maxBendAngle
)
{
    float3 scaledPos = localPos;

    if (useUniformScale > 0.5)
    {
        scaledPos = mul(RotationFromEuler(meshRotation), scaledPos);
        scaledPos *= width * distanceScale;
    }
    else
    {
        scaledPos.x *= width;
        scaledPos.y *= height * distanceScale;
        scaledPos.z *= width;
    }

    float tiltHash1 = Hash(worldPivot.xz + float2(3.14159, 2.71828));
    float tiltHash2 = Hash(worldPivot.xz + float2(1.41421, 1.73205));
    float tiltX = (tiltHash1 - 0.5) * 2.0 * maxTiltAngle * tiltVariation;
    float tiltZ = (tiltHash2 - 0.5) * 2.0 * maxTiltAngle * tiltVariation;

    float3x3 tiltMatrix = RotationFromEuler(float3(tiltX, 0, tiltZ));
    scaledPos = mul(tiltMatrix, scaledPos);

    float rotation = Hash(worldPivot.xz) * 6.28318;
    scaledPos = mul(RotationY(rotation), scaledPos);

    float windInfluence = uvY * uvY;
    float windMagnitude = length(windOffset) * windInfluence;
    if (windMagnitude > 0.001)
    {
        float windBendAngle = min(windMagnitude * 0.5, 0.5);

        float2 rotatedWind = float2(
            windOffset.x * cos(-rotation) - windOffset.y * sin(-rotation),
            windOffset.x * sin(-rotation) + windOffset.y * cos(-rotation)
        );
        float2 windDir = normalize(rotatedWind);

        float3x3 windBend = RotationFromEuler(float3(windDir.y * windBendAngle, 0, -windDir.x * windBendAngle));
        scaledPos = mul(windBend, scaledPos);
    }

    float bendInfluence = uvY;
    float interactMagnitude = length(interactionOffset.xz) * bendInfluence;
    if (interactMagnitude > 0.001)
    {
        float interactBendAngle = min(interactMagnitude * maxBendAngle, maxBendAngle);
        float2 interactDir = normalize(interactionOffset.xz);

        float2 localInteract = float2(
            interactDir.x * cos(-rotation) - interactDir.y * sin(-rotation),
            interactDir.x * sin(-rotation) + interactDir.y * cos(-rotation)
        );

        float3x3 interactBend = RotationFromEuler(float3(localInteract.y * interactBendAngle, 0, -localInteract.x * interactBendAngle));
        scaledPos = mul(interactBend, scaledPos);
    }

    float3 worldPos = worldPivot + scaledPos;
    return worldPos;
}

float3 TransformGrassVertex(
    float3 localPos,
    float3 worldPivot,
    float width,
    float height,
    float distanceScale,
    float2 windOffset,
    float3 interactionOffset,
    float uvY,
    float useUniformScale,
    float3 meshRotation,
    float maxTiltAngle,
    float tiltVariation
)
{
    return TransformGrassVertex(
        localPos, worldPivot,
        width, height, distanceScale,
        windOffset, interactionOffset, uvY,
        useUniformScale, meshRotation, maxTiltAngle, tiltVariation,
        1.4
    );
}

struct DecalResult
{
    half3 color;
    float applied;
};

float2 CalculateDecalUV(float3 worldPos, float4 bounds, float rotation)
{
    float2 relPos = worldPos.xz - bounds.xy;
    float cosR = cos(rotation);
    float sinR = sin(rotation);
    float2 rotatedPos = float2(
        relPos.x * cosR - relPos.y * sinR,
        relPos.x * sinR + relPos.y * cosR
    );
    return float2(rotatedPos.x / bounds.z, -rotatedPos.y / bounds.w) + 0.5;
}

float IsInsideDecalBounds(float2 uv)
{
    float2 inside = step(float2(0, 0), uv) * step(uv, float2(1, 1));
    return inside.x * inside.y;
}

half3 ApplyDecalBlendMode(half3 baseColor, half3 decalColor, float blendMode)
{
    half3 overrideResult = decalColor;
    half3 multiplyResult = baseColor * decalColor * 2.0;
    half3 additiveResult = baseColor + decalColor;

    float isMultiply = step(0.5, blendMode) * step(blendMode, 1.5);
    float isAdditive = step(1.5, blendMode);
    float isOverride = 1.0 - isMultiply - isAdditive;

    return overrideResult * isOverride + multiplyResult * isMultiply + additiveResult * isAdditive;
}

DecalResult ApplyDecalLayer(
    half3 baseColor,
    float3 worldPos,
    float enabled,
    float4 bounds,
    float rotation,
    float blend,
    float blendMode,
    half4 decalSample,
    float2 decalUV
)
{
    DecalResult result;
    result.color = baseColor;
    result.applied = 0.0;

    float isEnabled = step(0.5, enabled);
    float isInside = IsInsideDecalBounds(decalUV);
    float hasAlpha = step(0.01, decalSample.a);
    float shouldApply = isEnabled * isInside * hasAlpha;

    half3 blendedColor = ApplyDecalBlendMode(baseColor, decalSample.rgb, blendMode);

    float finalBlend = shouldApply * decalSample.a * blend;
    result.color = lerp(baseColor, blendedColor, finalBlend);
    result.applied = shouldApply;

    return result;
}

#endif
