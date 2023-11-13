#ifndef CUSTOM_LIT_INPUT_INCLUDED
#define CUSTOM_LIT_INPUT_INCLUDED

TEXTURE2D(_BaseMap);
TEXTURE2D(_MaskMap);
TEXTURE2D(_MatCap);
TEXTURE2D(_NormalMap);
TEXTURE2D(_EmissionMap);

SAMPLER(sampler_BaseMap);

TEXTURE2D(_DetailMap);
TEXTURE2D(_DetailNormalMap);
SAMPLER(sampler_DetailMap);

UNITY_INSTANCING_BUFFER_START(UnityPerMaterial)
    UNITY_DEFINE_INSTANCED_PROP(half4, _BaseMap_ST)
    UNITY_DEFINE_INSTANCED_PROP(half4, _BaseColor)
    UNITY_DEFINE_INSTANCED_PROP(half4, _EmissionColor)
    UNITY_DEFINE_INSTANCED_PROP(float, _MatCapPower)
    UNITY_DEFINE_INSTANCED_PROP(float, _NormalScale)
    UNITY_DEFINE_INSTANCED_PROP(float, _Cutoff)
    UNITY_DEFINE_INSTANCED_PROP(float, _Metallic)
    UNITY_DEFINE_INSTANCED_PROP(float, _Smoothness)
    UNITY_DEFINE_INSTANCED_PROP(half, _SpecularPower)
UNITY_INSTANCING_BUFFER_END(UnityPerMaterial)
#define INPUT_PROP(name) UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, name)

struct InputConfig {
    half2 baseUV;
    half2 detailUV;
};

InputConfig GetInputConfig (half2 baseUV, half2 detailUV = 0.0) {
    InputConfig c;
    c.baseUV = baseUV;
    c.detailUV = detailUV;
    return c;
}
half2 TransformBaseUV (half2 baseUV) {
    half4 baseST = UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _BaseMap_ST);
    return baseUV * baseST.xy + baseST.zw;
}

half4 GetBase (half2 baseUV) {
    const half4 map = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, baseUV);
    const half4 color = UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _BaseColor);
    return map * color;
}

half3 GetEmission (half2 baseUV) {
    half4 map = SAMPLE_TEXTURE2D(_EmissionMap, sampler_BaseMap, baseUV);
    half4 color = UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _EmissionColor);
    return map.rgb * color.rgb;
}

half3 GetNormalTS (InputConfig c) {
    const half4 map = SAMPLE_TEXTURE2D(_NormalMap, sampler_BaseMap, c.baseUV);
    //const half scale = INPUT_PROP(_NormalScale);
    half3 normal = DecodeNormal(map);
    return normal;
}
float GetCutoff (half2 baseUV) {
    return UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _Cutoff);
}

float GetMetallic (half2 baseUV) {
    return UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _Metallic);
}

float GetSmoothness (half2 baseUV) {
    return UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _Smoothness);
}

half3 GetMatCap (half2 baseUV) {
    return SAMPLE_TEXTURE2D(_MatCap, sampler_BaseMap, baseUV).rgb;
}

#endif