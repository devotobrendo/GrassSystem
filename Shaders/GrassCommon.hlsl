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
    // Per-instance phase offset for variation (prevents uniform synchronized movement)
    float phaseOffset = Hash(worldPos.xz) * 6.28318;
    float amplitudeVariation = 0.7 + Hash(worldPos.xz + float2(1.618, 2.718)) * 0.6; // 0.7-1.3 range
    
    float2 windUV = worldPos.xz * frequency;
    float2 wind1 = sin(windUV + time * speed + phaseOffset) * strength * amplitudeVariation;
    float2 wind2 = sin(windUV * 0.5 + time * speed * 0.7 + phaseOffset * 0.5) * strength * 0.5 * amplitudeVariation;
    return wind1 + wind2;
}

float CalculateCheckerPattern(float3 worldPos, float scale)
{
    float2 patternUV = worldPos.xz / scale;
    float checker = fmod(floor(patternUV.x) + floor(patternUV.y), 2.0);
    return checker;
}

// Stripe pattern with direction and soft edges (for baseball/soccer field effect)
float CalculateStripePattern(float3 worldPos, float scale, float direction, float softness)
{
    float dirRad = direction * 0.0174533; // degrees to radians
    float2 rotatedPos = float2(
        worldPos.x * cos(dirRad) - worldPos.z * sin(dirRad),
        worldPos.x * sin(dirRad) + worldPos.z * cos(dirRad)
    );
    float stripe = frac(rotatedPos.x / scale);
    // Soft edges using smoothstep
    float edge = softness * 0.5;
    return smoothstep(0.5 - edge, 0.5 + edge, abs(stripe * 2.0 - 1.0));
}

// Procedural noise pattern for organic zone variation
float CalculateNoisePattern(float3 worldPos, float scale, float contrast)
{
    float2 p = worldPos.xz / scale;
    // Multi-octave noise for more natural look
    float n1 = frac(sin(dot(floor(p), float2(127.1, 311.7))) * 43758.5453);
    float n2 = frac(sin(dot(floor(p * 0.5), float2(269.5, 183.3))) * 43758.5453);
    float noise = lerp(n1, n2, 0.5);
    // Apply contrast to create more distinct zones
    return saturate((noise - 0.5) * contrast + 0.5);
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
    float uvY,
    float useUniformScale,
    float3 meshRotation,
    float maxTiltAngle,
    float tiltVariation,
    float maxBendAngle
)
{
    float3 scaledPos = localPos;
    
    // Apply mesh rotation offset first (for custom meshes)
    if (useUniformScale > 0.5)
    {
        scaledPos = mul(RotationFromEuler(meshRotation), scaledPos);
        // Custom Mesh: uniform scale (width is used as size)
        scaledPos *= width * distanceScale;
    }
    else
    {
        // Default: width/height separately
        scaledPos.x *= width;
        scaledPos.y *= height * distanceScale;
    }
    
    // Generate random tilt angles (X and Z rotation) for natural clump look
    float tiltHash1 = Hash(worldPivot.xz + float2(3.14159, 2.71828));
    float tiltHash2 = Hash(worldPivot.xz + float2(1.41421, 1.73205));
    float tiltX = (tiltHash1 - 0.5) * 2.0 * maxTiltAngle * tiltVariation;
    float tiltZ = (tiltHash2 - 0.5) * 2.0 * maxTiltAngle * tiltVariation;
    
    // Apply tilt rotation (before Y rotation for natural outward lean)
    float3x3 tiltMatrix = RotationFromEuler(float3(tiltX, 0, tiltZ));
    scaledPos = mul(tiltMatrix, scaledPos);
    
    // Random Y rotation based on position
    float rotation = Hash(worldPivot.xz) * 6.28318;
    scaledPos = mul(RotationY(rotation), scaledPos);
    
    // Wind influence - apply as rotation-based bending (more natural than translation)
    float windInfluence = uvY * uvY;
    float windMagnitude = length(windOffset) * windInfluence;
    if (windMagnitude > 0.001)
    {
        // Calculate wind bend angle (clamped are it from going crazy)
        float windBendAngle = min(windMagnitude * 0.5, 0.5); // Max ~30 degrees bend
        
        // Rotate the offset direction by the blade's Y rotation to get local wind direction
        float2 rotatedWind = float2(
            windOffset.x * cos(-rotation) - windOffset.y * sin(-rotation),
            windOffset.x * sin(-rotation) + windOffset.y * cos(-rotation)
        );
        float2 windDir = normalize(rotatedWind);
        
        // Apply bend as tilt rotation (forward/back based on wind direction)
        float3x3 windBend = RotationFromEuler(float3(windDir.y * windBendAngle, 0, -windDir.x * windBendAngle));
        scaledPos = mul(windBend, scaledPos);
    }
    
    // Interaction bending - also use rotation for consistency
    float bendInfluence = uvY;
    float interactMagnitude = length(interactionOffset.xz) * bendInfluence;
    if (interactMagnitude > 0.001)
    {
        float interactBendAngle = min(interactMagnitude * maxBendAngle, maxBendAngle);
        float2 interactDir = normalize(interactionOffset.xz);
        
        // Rotate to local space
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

// Legacy version for backward compatibility (calls new version with defaults)
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
    return TransformGrassVertex(
        localPos, worldPivot, surfaceNormal,
        width, height, distanceScale,
        windOffset, interactionOffset, uvY,
        0.0, float3(0, 0, 0), 0.0, 0.0,
        1.4 // Default ~80 degrees
    );
}

// 13-parameter version for backward compatibility
float3 TransformGrassVertex(
    float3 localPos,
    float3 worldPivot,
    float3 surfaceNormal,
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
        localPos, worldPivot, surfaceNormal,
        width, height, distanceScale,
        windOffset, interactionOffset, uvY,
        useUniformScale, meshRotation, maxTiltAngle, tiltVariation,
        1.4 // Default ~80 degrees
    );
}

float CalculateTipCutout(float uvY, float cutoffHeight)
{
    return step(uvY, cutoffHeight);
}

#endif
