using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace Mobile_SRP.Runtime
{
    //[CreateAssetMenu(menuName = "Rendering/Mobile Render Editor Assets")]
    public class MobileRenderPipelineEditorResources : ScriptableObject
    {
        public MaterialResources materials;
        
        [Serializable, ReloadGroup]
        public sealed class MaterialResources
        {
            /// <summary>
            /// Lit material.
            /// </summary>
            [Reload("Runtime/Materials/DefaultLit.mat")]
            public Material Lit;

            // particleLit is the URP default material for new particle systems.
            // ParticlesUnlit.mat is closest match to the built-in shader.
            // This is correct (current 22.2) despite the Lit/Unlit naming conflict.
            /// <summary>
            /// Particle Lit material.
            /// </summary>
            [Reload("Runtime/Materials/ParticlesUnlit.mat")]
            public Material ParticleLit;

            /// <summary>
            /// Terrain Lit material.
            /// </summary>
            [Reload("Runtime/Materials/TerrainLit.mat")]
            public Material TerrainLit;

            /// <summary>
            /// Decal material.
            /// </summary>
            [Reload("Runtime/Materials/Decal.mat")]
            public Material Decal;
        }
    }
}