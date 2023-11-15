#ifndef DITHERINGFUNCTIONS_INCLUDED
#define DITHERINGFUNCTIONS_INCLUDED

TEXTURE2D(_BlueNoiseTextureRGB512);
SAMPLER(sampler_BlueNoiseTextureRGB512);

half BlueNoiseSampler(half2 uv)
{
    return SAMPLE_TEXTURE2D(_BlueNoiseTextureRGB512, sampler_BlueNoiseTextureRGB512, uv).r;
}
#endif
