using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Mobile_SRP.Runtime
{
    public class MobileRenderPipeline : RenderPipeline
    {
        private ScriptableRenderContext m_Context;

        private readonly CameraRenderer m_Renderer = new();

        private readonly bool m_UseDynamicBatching;
        private readonly bool m_UseGPUInstancing;
        private readonly ShadowSettings m_ShadowSettings;

        public MobileRenderPipeline(bool useDynamicBatching, bool useGPUInstancing, bool useSrpBatcher, ShadowSettings shadowSettings)
        {
            m_ShadowSettings = shadowSettings;
            m_UseDynamicBatching = useDynamicBatching;
            m_UseGPUInstancing = useGPUInstancing;
            GraphicsSettings.useScriptableRenderPipelineBatching = useSrpBatcher;
            GraphicsSettings.lightsUseLinearIntensity = true;
        }

        protected override void Render(ScriptableRenderContext context, Camera[] cameras)
        {
        }

        protected override void Render(ScriptableRenderContext context, List<Camera> cameras)
        {
            for (int i = 0; i < cameras.Count; i++)
            {
                m_Renderer.Render(
                    context, cameras[i], m_UseDynamicBatching, m_UseGPUInstancing, m_ShadowSettings
                );
            }
        }
    }
}
