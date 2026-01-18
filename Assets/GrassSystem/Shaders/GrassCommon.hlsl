// GrassCommon.hlsl - Shared functions for grass rendering
// Include in both compute and render shaders

#ifndef GRASS_COMMON_INCLUDED
#define GRASS_COMMON_INCLUDED

// Grass instance data from compute shader
struct GrassDrawData
{
    float3 position;
    float3 normal;
    float2 widthHeight;
    float3 color;
    float patternMask;
    float distanceScale;
};

// Wind calculation
float2 CalculateWind(float3 worldPos, float time, float speed, float strength, float frequency)
{
    float2 windUV = worldPos.xz * frequency;
    
    // Two layers of wind for natural look
    float2 wind1 = sin(windUV + time * speed) * strength;
    float2 wind2 = sin(windUV * 0.5 + time * speed * 0.7) * strength * 0.5;
    
    return wind1 + wind2;
}

// Calculate checkered pattern based on world position
float CalculateCheckerPattern(float3 worldPos, float scale)
{
    float2 patternUV = worldPos.xz / scale;
    float checker = fmod(floor(patternUV.x) + floor(patternUV.y), 2.0);
    return checker;
}

// Random rotation around Y axis based on position
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

// Simple hash for procedural variation
float Hash(float2 p)
{
    return frac(sin(dot(p, float2(12.9898, 78.233))) * 43758.5453);
}

// Vertex transformation for grass blade
float3 TransformGrassVertex(
    float3 localPos,
    float3 worldPivot,
    float3 surfaceNormal,
    float width,
    float height,
    float distanceScale,
    float2 windOffset,
    float3 interactionOffset,
    float uvY // 0 at base, 1 at tip
)
{
    // Scale by width/height
    float3 scaledPos = localPos;
    scaledPos.x *= width;
    scaledPos.y *= height * distanceScale; // LOD scale affects height
    
    // Random rotation based on position
    float rotation = Hash(worldPivot.xz) * 6.28318; // 0 to 2PI
    scaledPos = mul(RotationY(rotation), scaledPos);
    
    // Apply wind (more at tip)
    float windInfluence = uvY * uvY; // Quadratic - more at top
    scaledPos.xz += windOffset * windInfluence;
    
    // Apply interaction bending (more at tip)
    float bendInfluence = uvY;
    scaledPos.xz += interactionOffset.xz * bendInfluence;
    scaledPos.y -= length(interactionOffset.xz) * bendInfluence * 0.3; // Slight droop when bent
    
    // Align to surface normal (optional - for non-flat terrain)
    // For now, just offset to world pivot
    float3 worldPos = worldPivot + scaledPos;
    
    return worldPos;
}

// Tip cutout calculation
float CalculateTipCutout(float uvY, float cutoffHeight)
{
    // Simple gradient cutoff at tip
    return step(uvY, cutoffHeight);
}

#endif // GRASS_COMMON_INCLUDED
