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

// =====================================================
// ORGANIC NOISE FUNCTIONS FOR NATURAL GRASS VARIATION
// =====================================================

// Gradient noise for smooth organic variation (Perlin-like)
float2 GradientNoise2D(float2 p)
{
    float2 i = floor(p);
    float2 f = frac(p);
    
    // Smooth interpolation curve
    float2 u = f * f * (3.0 - 2.0 * f);
    
    return u;
}

// Hash function returning 2D random vector from 2D input
float2 Hash2D(float2 p)
{
    float3 p3 = frac(float3(p.xyx) * float3(0.1031, 0.1030, 0.0973));
    p3 += dot(p3, p3.yzx + 33.33);
    return frac((p3.xx + p3.yz) * p3.zy);
}

// Simplex-like noise for organic shapes
float PerlinNoise(float2 p)
{
    float2 i = floor(p);
    float2 f = frac(p);
    
    // Quintic smooth interpolation
    float2 u = f * f * f * (f * (f * 6.0 - 15.0) + 10.0);
    
    // Four corners
    float a = Hash(i + float2(0.0, 0.0));
    float b = Hash(i + float2(1.0, 0.0));
    float c = Hash(i + float2(0.0, 1.0));
    float d = Hash(i + float2(1.0, 1.0));
    
    // Bilinear interpolation
    return lerp(lerp(a, b, u.x), lerp(c, d, u.x), u.y);
}

// Fractal Brownian Motion (FBM) for organic clump shapes
float FBMNoise(float2 p, int octaves, float persistence, float lacunarity)
{
    float value = 0.0;
    float amplitude = 1.0;
    float frequency = 1.0;
    float maxValue = 0.0;
    
    for (int i = 0; i < octaves; i++)
    {
        value += PerlinNoise(p * frequency) * amplitude;
        maxValue += amplitude;
        amplitude *= persistence;
        frequency *= lacunarity;
    }
    
    return value / maxValue;
}

// Worley/Cellular noise for organic blob/clump shapes
float WorleyNoise(float2 p)
{
    float2 i = floor(p);
    float2 f = frac(p);
    
    float minDist = 1.0;
    
    // Check 3x3 neighborhood
    for (int y = -1; y <= 1; y++)
    {
        for (int x = -1; x <= 1; x++)
        {
            float2 neighbor = float2(x, y);
            float2 offset = float2(neighbor);
            
            // Random point position in neighboring cell
            float2 cellPoint = float2(
                Hash(i + neighbor),
                Hash(i + neighbor + float2(127.1, 311.7))
            );
            
            float2 diff = neighbor + cellPoint - f;
            float dist = length(diff);
            minDist = min(minDist, dist);
        }
    }
    
    return minDist;
}

// Voronoi noise with sharp cell edges (distance to edge instead of cell center)
float VoronoiNoise(float2 p)
{
    float2 i = floor(p);
    float2 f = frac(p);
    
    float minDist1 = 1.0;
    float minDist2 = 1.0;
    
    // Check 3x3 neighborhood
    for (int y = -1; y <= 1; y++)
    {
        for (int x = -1; x <= 1; x++)
        {
            float2 neighbor = float2(x, y);
            float2 cellPoint = float2(
                Hash(i + neighbor),
                Hash(i + neighbor + float2(127.1, 311.7))
            );
            
            float2 diff = neighbor + cellPoint - f;
            float dist = length(diff);
            
            // Track two closest distances for edge detection
            if (dist < minDist1)
            {
                minDist2 = minDist1;
                minDist1 = dist;
            }
            else if (dist < minDist2)
            {
                minDist2 = dist;
            }
        }
    }
    
    // Return edge distance (creates sharper cell boundaries)
    return minDist2 - minDist1;
}

// Blue Noise - uniform scattered distribution without clumping
// Creates natural-looking uniform spread of color variation
float BlueNoise(float2 p)
{
    // Use golden ratio based sampling for blue noise approximation
    float2 r = float2(0.618033988749894848, 0.381966011250105152);
    float n = frac(sin(dot(p, float2(12.9898, 78.233))) * 43758.5453);
    
    // Multi-octave blue noise for smoother distribution
    float value = 0;
    float2 offset = float2(0, 0);
    float totalWeight = 0;
    
    for (int i = 0; i < 3; i++)
    {
        float2 cell = floor(p + offset);
        float2 f = frac(p + offset);
        
        float minDist = 1.0;
        for (int y = -1; y <= 1; y++)
        {
            for (int x = -1; x <= 1; x++)
            {
                float2 neighbor = float2(x, y);
                float2 randPoint = Hash2D(cell + neighbor);
                float dist = length(neighbor + randPoint - f);
                minDist = min(minDist, dist);
            }
        }
        float weight = 1.0 / (i + 1);
        value += minDist * weight;
        totalWeight += weight;
        offset += r * 17.0;
        p *= 1.5;
    }
    
    // Normalize to full 0-1 range (removed * 0.7 which was causing dark output)
    return saturate(value / totalWeight);
}


// Cluster Noise - creates natural grass clump/patch patterns
// Mimics how grass naturally grows in clusters
float ClusterNoise(float2 p)
{
    // Primary cluster layer - large patches
    float2 cell = floor(p);
    float2 f = frac(p);
    
    float value = 0;
    float weight = 0;
    
    // Check 3x3 neighborhood for cluster centers
    for (int y = -1; y <= 1; y++)
    {
        for (int x = -1; x <= 1; x++)
        {
            float2 neighbor = float2(x, y);
            float2 randPoint = Hash2D(cell + neighbor);
            float2 diff = neighbor + randPoint - f;
            
            // Gaussian-like falloff for soft cluster edges
            float dist = length(diff);
            float influence = exp(-dist * dist * 2.0);
            
            // Each cluster has its own color assignment
            float clusterColor = Hash(cell + neighbor);
            value += clusterColor * influence;
            weight += influence;
        }
    }
    
    return saturate(value / max(weight, 0.001));
}

// Gradient Blend - smooth natural color transitions
// Creates gentle flowing color changes across terrain
float GradientBlend(float2 p)
{
    // Multi-directional smooth gradients
    float angle1 = Hash(floor(p * 0.1)) * 6.28318;
    float angle2 = Hash(floor(p * 0.1) + 100.0) * 6.28318;
    
    float2 dir1 = float2(cos(angle1), sin(angle1));
    float2 dir2 = float2(cos(angle2), sin(angle2));
    
    // Smooth perlin-like gradients in multiple directions
    float grad1 = PerlinNoise(p * 0.5) * 0.5 + 0.5;
    float grad2 = PerlinNoise(p * 0.3 + 50.0) * 0.5 + 0.5;
    float grad3 = PerlinNoise(p * 0.7 + 100.0) * 0.5 + 0.5;
    
    // Blend gradients for natural flowing color
    float result = grad1 * 0.5 + grad2 * 0.3 + grad3 * 0.2;
    
    return saturate(result);
}

// Stochastic Noise - irregular non-repeating pattern
// Breaks up visual repetition for natural look
float StochasticNoise(float2 p)
{
    // Sample from multiple offset positions and blend
    float2 offset1 = float2(Hash(floor(p.x * 0.3)), Hash(floor(p.y * 0.3))) * 100.0;
    float2 offset2 = float2(Hash(floor(p.y * 0.3)), Hash(floor(p.x * 0.3))) * 100.0;
    
    // Three layers with different rotations and scales
    float n1 = WorleyNoise(p + offset1);
    float n2 = WorleyNoise(p * 1.3 + offset2);
    float n3 = PerlinNoise(p * 0.7) * 0.5 + 0.5;
    
    // Stochastic blend based on position
    float blend = Hash(floor(p * 0.5)) * 0.5 + 0.25;
    float result = lerp(lerp(n1, n2, blend), n3, 0.3);
    
    return saturate(result);
}

// Main organic variation function - creates natural irregular color patches
// Like natural grass fields with soft blended areas of different shades
// Returns 0-1 value used to interpolate between min/max color range
float CalculateOrganicVariation(float3 worldPos, float scale, float clumpiness, float contrast, float edgeSoftness)
{
    float2 p = worldPos.xz / scale;
    
    // Layer 1: Large-scale blotchy variation (main patches)
    float large = FBMNoise(p * 0.3, 3, 0.5, 2.0);
    
    // Layer 2: Medium patches overlapping
    float medium = FBMNoise(p * 0.6 + float2(31.7, 47.3), 3, 0.5, 2.0);
    
    // Layer 3: Smaller detail variation
    float small = FBMNoise(p * 1.2 + float2(73.1, 89.7), 2, 0.5, 2.0);
    
    // Layer 4: Subtle Worley for occasional darker spots (like worn areas)
    float spots = 1.0 - WorleyNoise(p * 0.5 + float2(17.3, 29.1)) * 0.4;
    
    // Combine layers with organic blending
    // clumpiness affects the weight of distinct patches vs smooth blend
    float baseBlend = large * 0.45 + medium * 0.35 + small * 0.2;
    
    // Add darker spots based on clumpiness
    baseBlend = baseBlend * lerp(1.0, spots, clumpiness * 0.5);
    
    // Apply soft S-curve for natural transitions
    // Edge softness controls how gradual the color transitions are
    float softness = lerp(0.8, 0.3, edgeSoftness);
    baseBlend = smoothstep(0.5 - softness, 0.5 + softness, baseBlend);
    
    // Apply contrast for more or less distinct patches
    baseBlend = saturate((baseBlend - 0.5) * contrast + 0.5);
    
    // Very subtle per-blade micro variation
    float micro = Hash(worldPos.xz * 20.0) * 0.06 - 0.03;
    baseBlend = saturate(baseBlend + micro);
    
    return baseBlend;
}

// Original geometric pattern functions

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

// Simple noise pattern for zone variation
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

// NEW: Organic pattern using FBM + Worley noise for natural grass clumps
float CalculateOrganicPattern(float3 worldPos, float scale, float clumpiness, float softness)
{
    return CalculateOrganicVariation(worldPos, scale, clumpiness, 2.0, softness);
}

// NEW: Patches pattern - creates visible circular irregular patches
// Like worn grass areas or natural color variation spots
float CalculatePatchesPattern(float3 worldPos, float scale, float clumpiness, float softness)
{
    float2 p = worldPos.xz / scale;
    
    // Main circular patches using Worley noise (creates cell-like circles)
    float cells1 = WorleyNoise(p * 0.5);
    float cells2 = WorleyNoise(p * 0.3 + float2(13.7, 29.3));
    
    // Combine for irregular circles
    float patches = min(cells1, cells2 * 1.2);
    
    // Invert so patches are darker in the center
    patches = 1.0 - patches;
    
    // Apply threshold to make distinct circular spots
    // Clumpiness controls how many and how defined the patches are
    float threshold = lerp(0.4, 0.6, clumpiness);
    patches = smoothstep(threshold - 0.15, threshold + 0.15, patches);
    
    // Apply edge softness
    float soft = lerp(1.0, 0.6, softness);
    patches = lerp(0.5, patches, soft);
    
    // Add subtle variation within patches
    float detail = Hash(worldPos.xz * 8.0) * 0.1;
    patches = saturate(patches + detail - 0.05);
    
    return patches;
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

// =====================================================
// MULTI-LAYER DECAL SYSTEM
// Supports 3 decal layers with priority overwrite
// BlendMode: 0=Override, 1=Multiply, 2=Additive
// =====================================================

struct DecalResult
{
    half3 color;
    float applied; // 1.0 if decal was applied, 0.0 otherwise
};

// Calculate decal UV from world position and bounds
// Returns UV in 0-1 range if inside bounds, otherwise outside range
// UV mapping: texture U = world X (right), texture V = world Z (forward)
float2 CalculateDecalUV(float3 worldPos, float4 bounds, float rotation)
{
    float2 relPos = worldPos.xz - bounds.xy;
    // Use positive rotation to match Unity's Y-axis rotation direction
    float cosR = cos(rotation);
    float sinR = sin(rotation);
    // Rotate relative position around center
    // After rotation: U maps to rotated X, V maps to rotated Z
    float2 rotatedPos = float2(
        relPos.x * cosR - relPos.y * sinR,
        relPos.x * sinR + relPos.y * cosR
    );
    // Flip V to align texture "up" with world forward (Z+)
    // bounds.zw = (sizeX, sizeZ) so we need to match UV to correct axis
    return float2(rotatedPos.x / bounds.z, -rotatedPos.y / bounds.w) + 0.5;
}

// Check if UV is within valid decal bounds (branchless)
float IsInsideDecalBounds(float2 uv)
{
    // Returns 1.0 if inside [0,1] range, 0.0 otherwise
    float2 inside = step(float2(0, 0), uv) * step(uv, float2(1, 1));
    return inside.x * inside.y;
}

// Apply blend mode to decal color (branchless using lerp chain)
// BlendMode: 0=Override, 1=Multiply, 2=Additive
half3 ApplyDecalBlendMode(half3 baseColor, half3 decalColor, float blendMode)
{
    half3 overrideResult = decalColor;
    half3 multiplyResult = baseColor * decalColor * 2.0;
    half3 additiveResult = baseColor + decalColor;
    
    // Branchless selection: blendMode 0 -> override, 1 -> multiply, 2 -> additive
    float isMultiply = step(0.5, blendMode) * step(blendMode, 1.5);
    float isAdditive = step(1.5, blendMode);
    float isOverride = 1.0 - isMultiply - isAdditive;
    
    return overrideResult * isOverride + multiplyResult * isMultiply + additiveResult * isAdditive;
}

// Apply a single decal layer to base color
// Returns the blended color and whether this decal was applied
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
    
    // Early out mask (branchless)
    float isEnabled = step(0.5, enabled);
    float isInside = IsInsideDecalBounds(decalUV);
    float hasAlpha = step(0.01, decalSample.a);
    float shouldApply = isEnabled * isInside * hasAlpha;
    
    // Apply blend mode
    half3 blendedColor = ApplyDecalBlendMode(baseColor, decalSample.rgb, blendMode);
    
    // Final lerp with alpha and blend strength
    float finalBlend = shouldApply * decalSample.a * blend;
    result.color = lerp(baseColor, blendedColor, finalBlend);
    result.applied = shouldApply;
    
    return result;
}

#endif

