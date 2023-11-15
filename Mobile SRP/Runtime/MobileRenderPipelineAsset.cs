using System;
using System.Linq;
using UnityEditor;
#if UNITY_EDITOR
using UnityEditorInternal;
#endif
using UnityEngine;
using UnityEngine.Rendering;

namespace Mobile_SRP.Runtime
{

    [CreateAssetMenu(menuName = "Rendering/Mobile Render Pipeline")]
    public class MobileRenderPipelineAsset : RenderPipelineAsset
    {
        [SerializeField] private bool useDynamicBatching = true, useGPUInstancing = true, useSrpBatcher = true;
        [SerializeField] private ShadowSettings shadows;
        //[Header("Shader Resources")]
        [HideInInspector]
        [SerializeField] private Noise noise;
        
        private static readonly int BlueNoiseTextureRGB512 = Shader.PropertyToID("_BlueNoiseTextureRGB512");
        private static readonly int BlueNoiseTextureLll512 = Shader.PropertyToID("_BlueNoiseTextureLLL512");

        protected override RenderPipeline CreatePipeline()
        {
            if (shadows.enableShadows)
            {
                Shader.EnableKeyword("SHADOWS_ENABLED");
            }
            else
            {
                Shader.DisableKeyword("SHADOWS_ENABLED");
            }
            
            noise.blueNoiseRGB512 = Resources.Load<Texture2D>("Noises/Blue Noise 512/LDR_RGB1_0");
            noise.blueNoiseLll512 = Resources.Load<Texture2D>("Noises/Blue Noise 512/LDR_LLL1_0");
            
            Shader.SetGlobalTexture(BlueNoiseTextureRGB512, noise.blueNoiseRGB512);
            Shader.SetGlobalTexture(BlueNoiseTextureLll512, noise.blueNoiseLll512);
            return new MobileRenderPipeline(
                useDynamicBatching, useGPUInstancing, useSrpBatcher, shadows
            );
        }
#if UNITY_EDITOR

        private const string EditorResourcesGuid = "443ba509e96a03d45bdd512b76acea5f";
        [SerializeField] [HideInInspector]
        private MobileRenderPipelineEditorResources editorResourcesAsset;

        private MobileRenderPipelineEditorResources EditorResources
        {
            get
            {
                if (editorResourcesAsset != null && !editorResourcesAsset.Equals(null))
                    return editorResourcesAsset;

                var resourcePath = AssetDatabase.GUIDToAssetPath(EditorResourcesGuid);
                var objs = InternalEditorUtility.LoadSerializedFileAndForget(resourcePath);
                editorResourcesAsset = objs is { Length: > 0 } ? objs.First() as MobileRenderPipelineEditorResources : null;
                
                return editorResourcesAsset;
            }
        }


        public override Material defaultMaterial => GetMaterial(DefaultMaterialType.Standard);
#endif
        public enum DefaultMaterialType
        {
            Standard,
            Particle,
            Terrain,
            Sprite,
            Decal
        }

        private Material GetMaterial(DefaultMaterialType materialType)
        {
#if UNITY_EDITOR

            return materialType switch
            {
                DefaultMaterialType.Standard => EditorResources.materials.Lit,
                DefaultMaterialType.Particle => EditorResources.materials.ParticleLit,
                DefaultMaterialType.Terrain => EditorResources.materials.TerrainLit,
                DefaultMaterialType.Decal => EditorResources.materials.Decal,
                _ => null
            };
#else
            return null;
#endif
        }
    }

    [Serializable]
    public class Noise
    {
        public Texture2D blueNoiseRGB512;
        public Texture2D blueNoiseLll512;
    }
}
