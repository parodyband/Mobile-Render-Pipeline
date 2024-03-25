#ifndef MOBILE_DECAL_INCLUDED
#define MOBILE_DECAL_INCLUDED

#define _BoxScale 1.0
#define _BoxSmoothnessRange 0.05
#define MaxDecalsOnScreen 4

struct Decal
{
    float4x4 projectorMatrix;
    float4x4 boxMatrix;
    float4 projectionParams;
};

TEXTURE2D(projectorTexture);
SAMPLER(samplerProjectorTexture);

StructuredBuffer<Decal> _Decals;
float2 _DecalDimensions;
int _DecalCount;

float sdfBox(float3 p, float3 b, float r)
{
    float3 d = abs(p) - b + r;
    return min(max(d.x, max(d.y, d.z)), 0.0) + length(max(d, 0.0)) - r;
}

float3 GetProjectors(float3 worldPos, float3 normalWS)
{
    float3 finalProjection = float3(0, 0, 0);

    for (uint i = 0; i < MaxDecalsOnScreen; i++)
    {
        Decal decal = _Decals[i];

        // Calculate projector UV coordinates
        float4 projectorPos = mul(decal.projectorMatrix, float4(worldPos, 1.0));
        float2 projectorUV = projectorPos.xy / projectorPos.w * 0.5 + 0.5;

        // Calculate projector direction
        float3 projectorDir = -normalize(decal.projectorMatrix[2].xyz);

        // Sample projector texture
        half3 projection = SAMPLE_TEXTURE2D(projectorTexture, samplerProjectorTexture, projectorUV);

        // Apply projection based on normal direction
        float ndotl = dot(normalWS, projectorDir);
        projection *= step(ndotl, 0);

        // Calculate box mask
        float4 localPos = mul(decal.boxMatrix, float4(worldPos, 1.0));
        float3 scaledPos = localPos.xyz / localPos.w;
        float boxMask = sdfBox(scaledPos, 0.45, _BoxSmoothnessRange);
        boxMask = smoothstep(-_BoxSmoothnessRange, _BoxSmoothnessRange, boxMask);

        // Apply box mask to the projection
        projection *= 1.0 - boxMask;

        // Accumulate the projection
        finalProjection += projection;
    }
    return finalProjection;
}

#endif