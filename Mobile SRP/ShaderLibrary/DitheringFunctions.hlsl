#ifndef DITHERINGFUNCTIONS_INCLUDED
#define DITHERINGFUNCTIONS_INCLUDED

TEXTURE2D(_BlueNoiseTextureRGB512);
SAMPLER(sampler_BlueNoiseTextureRGB512);
TEXTURE2D(_BlueNoiseTextureLLL512);
SAMPLER(sampler_BlueNoiseTextureLLL512);

float3 BlueNoiseSamplerRGB(float2 uv)
{
    return SAMPLE_TEXTURE2D(_BlueNoiseTextureRGB512, sampler_BlueNoiseTextureRGB512, uv).r;
}

float BlueNoiseSampler(float2 uv)
{
    return SAMPLE_TEXTURE2D(_BlueNoiseTextureLLL512, sampler_BlueNoiseTextureLLL512, uv).r;
}

float WhiteNoiseSampler(float2 uv)
{
    return frac(sin(dot(uv, float2(12.9898, 78.233))) * 43758.5453);
}

float BayerDither4x4(float2 position, float brightness) {
    const int4x4 bayerMatrix = int4x4( 0,  8,  2, 10,
                                       12,  4, 14,  6,
                                       3, 11,  1,  9,
                                       15,  7, 13,  5);

    int2 pos = int2(fmod(position.x, 4), fmod(position.y, 4));

    const float ditheredBrightness = brightness * 16.0;
    
    if (ditheredBrightness > bayerMatrix[pos.y][pos.x]) {
        return 1.0;
    }
    
    return 0.0;
}

float BayerDither8x8(float2 position, float brightness) {
    const int bayerMatrix[64] = {  0, 32,  8, 40,  2, 34, 10, 42,
                            48, 16, 56, 24, 50, 18, 58, 26,
                            12, 44,  4, 36, 14, 46,  6, 38,
                            60, 28, 52, 20, 62, 30, 54, 22,
                             3, 35, 11, 43,  1, 33,  9, 41,
                            51, 19, 59, 27, 49, 17, 57, 25,
                            15, 47,  7, 39, 13, 45,  5, 37,
                            63, 31, 55, 23, 61, 29, 53, 21 };

    int2 pos = int2(fmod(position.x, 8), fmod(position.y, 8));
    int index = pos.y * 8 + pos.x;

    const float ditheredBrightness = brightness * 64.0;
    if (ditheredBrightness > bayerMatrix[index]) {
        return 1.0;
    }
    return 0.0;
}

#endif
