#ifndef CUSTOM_EMPTY_PASS_INCLUDED
#define CUSTOM_EMPTY_PASS_INCLUDED

struct Attributes {
};

struct Varyings {
};

Varyings ShadowCasterPassVertex (Attributes input) {
    return (Varyings)0; 
}

void ShadowCasterPassFragment (Varyings input) {
}

#endif