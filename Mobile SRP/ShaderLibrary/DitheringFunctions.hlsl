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

real4x4 bayerMatrix = real4x4(
    0,  8,  2, 10,
    12,  4, 14,  6,
    3, 11,  1,  9,
    15,  7, 13,  5
);

real BayerDither(real2 uv, real4x4 bayerMatrix, real ditherAmount)
{
    // Scale UV to the size of the Bayer matrix
    int2 bayerPos = int2(frac(uv) * 4);

    // Sample the Bayer matrix value
    real bayerValue = bayerMatrix[bayerPos.x][bayerPos.y] / 15.0; // Normalizing

    // Apply dithering
    return bayerValue * ditherAmount;
}


#endif
