#ifndef DITHERINGFUNCTIONS_INCLUDED
#define DITHERINGFUNCTIONS_INCLUDED

TEXTURE2D(_BlueNoiseTextureRGB512);
SAMPLER(sampler_BlueNoiseTextureRGB512);
TEXTURE2D(_BlueNoiseTextureLLL512);
SAMPLER(sampler_BlueNoiseTextureLLL512);

real3 BlueNoiseSamplerRGB(real2 uv)
{
    return SAMPLE_TEXTURE2D(_BlueNoiseTextureRGB512, sampler_BlueNoiseTextureRGB512, uv).r;
}

real BlueNoiseSampler(real2 uv)
{
    return SAMPLE_TEXTURE2D(_BlueNoiseTextureLLL512, sampler_BlueNoiseTextureLLL512, uv).r;
}

real BayerDither4x4(real2 position, real brightness) {
    const int4x4 bayerMatrix = int4x4( 0,  8,  2, 10,
                                       12,  4, 14,  6,
                                       3, 11,  1,  9,
                                       15,  7, 13,  5);

    int2 pos = int2(fmod(position.x, 4), fmod(position.y, 4));

    const real ditheredBrightness = brightness * 16.0;
    
    if (ditheredBrightness > bayerMatrix[pos.y][pos.x]) {
        return 1.0;
    }
    
    return 0.0;
}

real BayerDither8x8(real2 position, real brightness) {
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
