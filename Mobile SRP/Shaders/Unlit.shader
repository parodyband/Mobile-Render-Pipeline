Shader "Mobile RP/Unlit" {
	
	Properties {
		_BaseMap("Texture", 2D) = "white" {}
		_BaseColor("Color", Color) = (1.0, 1.0, 1.0, 1.0)
		_Cutoff ("Alpha Cutoff", Range(0.0, 1.0)) = 0.5
		[Toggle(_CLIPPING)] _Clipping ("Alpha Clipping", Float) = 0
		[Enum(UnityEngine.Rendering.BlendMode)] _SrcBlend("SrcBlend", Float) = 1
		[Enum(UnityEngine.Rendering.BlendMode)] _DstBlend("DstBlend", Float) = 0
		[Enu(Off, 0, On, 1)] _ZWrite("ZWrite", Float) = 1
	}
	
	SubShader {
		
		Pass {
			
			Blend [_SrcBlend] [_DstBlend]
			ZWrite [_ZWrite]
			
			HLSLPROGRAM
			#include "UnlitPass.hlsl"
			
			#pragma vertex UnlitPassVertex
			#pragma fragment UnlitPassFragment
			#pragma multi_compile_instancing
			
			ENDHLSL
		}
	}
}