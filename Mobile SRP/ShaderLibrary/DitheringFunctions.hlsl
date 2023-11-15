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
    int4x4 bayerMatrix = int4x4( 0,  8,  2, 10,
                                12,  4, 14,  6,
                                 3, 11,  1,  9,
                                15,  7, 13,  5);

    int2 pos = int2(fmod(position.x, 4), fmod(position.y, 4));

    const real ditheredBrightness = brightness * 16.0;
    if (ditheredBrightness > bayerMatrix[pos.y][pos.x]) {
        return 1.0;
    } else {
        return 0.0;
    }
}


#endif
