#ifndef CUSTOM_LIGHTING_INCLUDED
#define CUSTOM_LIGHTING_INCLUDED

static const half3 grayscaleConversion = half3(0.299, 0.587, 0.114);

half3 IncomingLight (Surface surface, Light light) {
    const half nDotL = max(0, dot(surface.normal, light.direction));
    return saturate(nDotL * light.attenuation) * light.color;
}

//saturate(dot(surface.normal, light.direction) * light.attenuation) * light.color;
//PBR
half3 GetLighting (Surface surface, BRDF brdf, Light light) {
    return IncomingLight(surface, light) * DirectBRDF(surface, brdf, light);
}

half3 GetLighting (Surface surfaceWS, BRDF brdf, GI gi) {
    const ShadowData shadowData = GetShadowData(surfaceWS);
    float3 color = gi.diffuse * brdf.diffuse;
    
    for (int i = 0; i < GetDirectionalLightCount(); i++) {
        const Light light = GetDirectionalLight(i, surfaceWS, shadowData);
        color += GetLighting(surfaceWS, brdf, light);
    }
    
    return color;
}

half SpecularStrengthMobile(Surface surface, half smoothness, half3 lightDirection) {
    // Convert smoothness to roughness
    const half roughness = 1.0 - smoothness;

    const half3 h = SafeNormalize(lightDirection + surface.viewDirection);
    const half nh2 = Square(saturate(dot(surface.normal, h)));
    const half lh2 = Square(saturate(dot(lightDirection, h)));
    const half r2 = Square(roughness); // Use roughness for the calculation
    const half d2 = Square(nh2 * (r2 - 1.0) + 1.00001);
    const half normalization = roughness * 4.0 + 2.0; // Adjust based on roughness
    const half nDotL = saturate(dot(surface.normal, lightDirection));
    const half linearFalloff = 1.0 - nDotL;
    return r2 / (d2 * max(0.1, lh2) * normalization) * linearFalloff;
}
//Mobile
half GetSmoothnessPower(float smoothness) {
    return exp2(10 * smoothness + 1);
}

//
// half GetSpecularDot (Surface surface, float3 lightDirection) {
//     return saturate(dot(surface.normal, normalize(surface.viewDirection + lightDirection)));
// }

half4 MobileLightingHandler (Surface surface, Light light) {
    //const half lightIntensity = dot(light.color, grayscaleConversion);
    half attenuation = light.attenuation;
    half nDotL = max(0,dot(surface.normal, light.direction));

    #if defined(_HALF_LAMBERT)
    nDotL = Square(nDotL * .5 + .5);
    #endif
    
    half3 radiance = light.color * attenuation;

    //radiance *= attenuation;
    
    //Blinn Phong
    //const half specularDot = GetSpecularDot(surface, light.direction);
    //const half specular = pow(specularDot, GetSmoothnessPower(surface.smoothness)) * diffuseGray * surface.specularPower;

    //GGX
    const half specularStrength = SpecularStrengthMobile(surface, surface.smoothness, light.direction);
    
    const half specular = specularStrength * nDotL * surface.specularPower;
    
    half4 color = half4(surface.color * radiance * (nDotL + specular),1);
    
    #ifdef _MATCAP_ON
    // const half matCapInfluence = lerp(0, 1, surface.metallic * nDotL);
    // matCapColor.rgb = lerp(half3(0, 0, 0), surface.matcap * surface.color * lightIntensity, matCapInfluence);
    // color = lerp(color, matCapColor, matCapInfluence);
    #endif
    
    #if defined(SHADOWS_ENABLED)
    color.a = attenuation;
    #endif
    
    return color;
}


half3 GetMobileLighting (Surface surface, GI gi) {
    #if defined(SHADOWS_ENABLED)
    const ShadowData shadowData = GetShadowData(surface);
    #endif
    
    half4 radiance = half4(gi.diffuse * surface.color,1);

    const int directionalLightCount = GetDirectionalLightCount();
    for (int i = 0; i < directionalLightCount; i++) {
        #if defined(SHADOWS_ENABLED)
        Light light = GetDirectionalLight(i, surface, shadowData);
        #else
        Light light = GetDirectionalLight(i);
        #endif
        #if defined(_HALF_LAMBERT)
        light.attenuation += .2;
        #endif
        radiance += MobileLightingHandler(surface, light);
    }
    
    const int otherLightCount = GetOtherLightCount();
    for (int j = 0; j < otherLightCount; j++) {
        Light light = GetOtherLight(j, surface);
        radiance += MobileLightingHandler(surface, light);
    }

    const half diffuseGray = saturate(dot(radiance.rgb, grayscaleConversion) * 5);
    
    #ifdef _MATCAP_ON
    const half3 metal = lerp(0, surface.matcap * surface.color, surface.metallic * (radiance.a + .1));
    radiance.rgb += lerp(0,metal, surface.metallic * .5 * diffuseGray);
    #endif
    
    return radiance.rgb;
}










//Cheaper Vertex Lighting
half3 MobileLightingHandlerVertex (Surface surface, Light light, float specular) {
    
    const half3 diffuse = IncomingLight(surface, light);
    
    half3 color = surface.color * (diffuse + specular);
    
    return color;
}

half3 GetMobileLightingVertex(Surface surface, float specular, GI gi) {
    
    #if defined(SHADOWS_ENABLED)
    const ShadowData shadowData = GetShadowData(surface);
    #endif
    
    half3 radiance = half3(gi.diffuse * surface.color);
    
    for (int i = 0; i < GetDirectionalLightCount(); i++) {
        #if defined(SHADOWS_ENABLED)
        const Light light = GetDirectionalLight(i, surface, shadowData);
        #else
        const Light light = GetDirectionalLight(i);
        #endif
        
        radiance += MobileLightingHandlerVertex(surface, light, specular);
    }

    for (int j = 0; j < GetOtherLightCount(); j++) {
        Light light = GetOtherLight(j, surface);
        radiance += MobileLightingHandlerVertex(surface, light, specular);
    }
    
    return radiance;
}

#endif