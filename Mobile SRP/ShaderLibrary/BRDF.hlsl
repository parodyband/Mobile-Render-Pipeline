#ifndef CUSTOM_BRDF_INCLUDED
#define CUSTOM_BRDF_INCLUDED

struct BRDF {
    real3 diffuse;
    real3 specular;
    real roughness;
    real perceptualRoughness;
    real fresnel;
};

#define MIN_REFLECTIVITY 0.04

real OneMinusReflectivity (real metallic) {
    const real range = 1.0 - MIN_REFLECTIVITY;
    return range - metallic * range;
}

BRDF GetBRDF (Surface surface, bool applyAlphaToDiffuse = false) {
    BRDF brdf;
    const real oneMinusReflectivity = OneMinusReflectivity(surface.metallic);

    brdf.diffuse = surface.color * oneMinusReflectivity;
    if (applyAlphaToDiffuse) {
        brdf.diffuse *= surface.alpha;
    }
    brdf.specular = lerp(MIN_REFLECTIVITY, surface.color, surface.metallic);

    brdf.perceptualRoughness = PerceptualSmoothnessToPerceptualRoughness(surface.smoothness);
    brdf.roughness = clamp(PerceptualRoughnessToRoughness(brdf.perceptualRoughness) * brdf.perceptualRoughness, .001, .99);
	
    brdf.fresnel = saturate(surface.smoothness + 1.0 - oneMinusReflectivity);
    return brdf;
}

//Actual Specular Strength

// real SpecularStrength (Surface surface, BRDF brdf, Light light) {
//     const real3 h = SafeNormalize(light.direction + surface.viewDirection);
//     const real nh2 = Square(saturate(dot(surface.normal, h)));
//     const real lh2 = Square(saturate(dot(light.direction, h)));
//     const real r2 = Square(brdf.roughness);
//     const real d2 = Square(nh2 * (r2 - 1.0) + 1.00001);
//     const real normalization = brdf.roughness * 4.0 + 2.0;
//     return r2 / (d2 * max(0.1, lh2) * normalization);
// }

//Fake Mobile Strength
real SpecularStrength(Surface surface, BRDF brdf, Light light) {
    // Calculate the halfway vector
    const real3 h = SafeNormalize(light.direction + surface.viewDirection);
    const real nh = max(dot(surface.normal, h), 0.0);

    // Map roughness to specular power
    const real specularPower = 1.0 / max(Square(brdf.roughness), 0.001);
    // Calculate the final specular strength
    return pow(nh, specularPower) * 20 * saturate(1.0 - brdf.roughness * 1.5);
}


real3 DirectBRDF (Surface surface, BRDF brdf, Light light) {
    return SpecularStrength(surface, brdf, light) * brdf.specular + brdf.diffuse;
}

real3 IndirectBRDF (
    Surface surface, BRDF brdf, real3 diffuse, real3 specular
) {
    real fresnelStrength = surface.fresnelStrength *
        Pow4(1.0 - saturate(dot(surface.normal, surface.viewDirection)));
    real3 reflection =
        specular * lerp(brdf.specular, brdf.fresnel, fresnelStrength);
    reflection /= brdf.roughness * brdf.roughness + 1.0;
	
    return (diffuse * brdf.diffuse + reflection) * surface.occlusion;
}

#endif