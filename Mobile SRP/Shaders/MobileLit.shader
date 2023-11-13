Shader "Mobile RP/Mobile - Lit" {
	
    Properties {
        _BaseMap("Diffuse", 2D) = "white" {}
        _BaseColor("Color", Color) = (0.5, 0.5, 0.5, 1.0)
        _Cutoff ("Alpha Cutoff", Range(0.0, 1.0)) = 0.5
    	
    	[Toggle(_NORMAL_MAP)] _NormalMapToggle ("Normal Map", Float) = 0
		[NoScaleOffset] _NormalMap("Normals", 2D) = "bump" {}
		//_NormalScale("Normal Scale", Range(0, 1)) = 1
    	
        [Toggle(_CLIPPING)] _Clipping ("Alpha Clipping", Float) = 0
        
        _MaskMap("OSM (Occlusion, Smoothness/Gloss, Metallic)", 2D) = "white" {}
    	
    	_Metallic ("Metallic Add", Range(0, 1)) = 0.5
        _Smoothness ("Smoothness", Range(0, 1)) = 0.5
    	_SpecularPower ("Specular Power", Range(0, 10.0)) = 0.5
    	
    	[NoScaleOffset] _EmissionMap("Emission", 2D) = "white" {}
		[HDR] _EmissionColor("Emission", Color) = (0.0, 0.0, 0.0, 0.0)
    	
    	[Toggle(_MATCAP_ON)] _MatCapOn ("Use Matcap", Float) = 0
        _MatCap("MatCap", 2D) = "black" {}
		_MatCapPower ("MatCap Power", Range(0, 10.0)) = 0.5
    	
		[Toggle(_RECEIVE_SHADOWS)] _ReceiveShadows ("Receive Shadows", Float) = 1
		[KeywordEnum(On, Clip, Dither, Off)] _Shadows ("Shadows", Float) = 0
    	
        [Toggle(_PREMULTIPLY_ALPHA)] _PremulAlpha ("Premultiply Alpha", Float) = 0
        [Toggle(_VERTEX_LIGHTING_ON)] _VertexLighting("Use Vertex Lighting", Float) = 0
        
        [Enum(UnityEngine.Rendering.BlendMode)] _SrcBlend ("Src Blend", Float) = 1
        [Enum(UnityEngine.Rendering.BlendMode)] _DstBlend ("Dst Blend", Float) = 0
        [Enum(Off, 0, On, 1)] _ZWrite ("Z Write", Float) = 1
    }
	
    SubShader {
    	HLSLINCLUDE
		#include "../ShaderLibrary/Common.hlsl"
		#include "LitInput.hlsl"
		ENDHLSL
        Pass {
            Tags {
                "LightMode" = "DefaultLit"
            }
            Blend [_SrcBlend] [_DstBlend]
            ZWrite [_ZWrite]

            HLSLPROGRAM
			#pragma target 3.5
			#pragma shader_feature _CLIPPING
			#pragma shader_feature _RECEIVE_SHADOWS
			#pragma shader_feature _PREMULTIPLY_ALPHA
			#pragma shader_feature _NORMAL_MAP
			#pragma shader_feature _VERTEX_LIGHTING_ON
			#pragma shader_feature _MATCAP_ON
			
			#pragma multi_compile _ SHADOWS_ENABLED
			#pragma multi_compile _ _DIRECTIONAL_PCF3 _DIRECTIONAL_PCF5 _DIRECTIONAL_PCF7
			#pragma multi_compile _ _CASCADE_BLEND_SOFT _CASCADE_BLEND_DITHER
			#pragma multi_compile _ LIGHTMAP_ON
			#pragma multi_compile _ DIRLIGHTMAP_COMBINED
			#pragma multi_compile_instancing
			
            #pragma vertex LitPassVertex
            #pragma fragment LitPassFragment
			#include "MobileLitPass.hlsl"
            ENDHLSL
        }
		Pass {
			Tags {
				"LightMode" = "ShadowCaster"
			}

			ColorMask 0

			HLSLPROGRAM
			#pragma target 3.5
			#pragma shader_feature _ _SHADOWS_CLIP _SHADOWS_DITHER
			#pragma shader_feature _ SHADOWS_ENABLED
			#pragma multi_compile_instancing
			
			#pragma vertex ShadowCasterPassVertex
			#pragma fragment ShadowCasterPassFragment
			#if defined(SHADOWS_ENABLED)
			#include "ShadowCasterPass.hlsl"
			#else
			#include "EmptyShadowCasterPass.hlsl"
			#endif
			ENDHLSL
		}

		Pass {
			Tags {
				"LightMode" = "Meta"
			}

			Cull Off

			HLSLPROGRAM
			
			#pragma target 3.5
			#pragma vertex MetaPassVertex
			#pragma fragment MetaPassFragment
			#include "MetaPass.hlsl"
			ENDHLSL
		}
	}

    CustomEditor "CustomShaderGUI"
}