using UnityEngine;
using UnityEngine.Rendering;

namespace Mobile_SRP.Runtime
{
    public partial class CameraRenderer
    {
        private ScriptableRenderContext m_Context;
        private Camera m_Camera;

        private const string BufferName = "Render Camera";

        private static readonly ShaderTagId UnlitShaderTagId = new("SRPDefaultUnlit");

        private static readonly ShaderTagId LitShaderTagId = new("DefaultLit");

        private CullingResults m_CullingResults;

        private readonly Lighting m_Lighting = new();

        private readonly CommandBuffer m_Buffer = new()
        {
            name = BufferName
        };

        public void Render(ScriptableRenderContext context, Camera camera, bool useDynamicBatching,
            bool useGPUInstancing, ShadowSettings shadowSettings)
        {
            m_Context = context;
            m_Camera = camera;

            PrepareBuffer();
            PrepareForSceneWindow();

            if (!Cull(shadowSettings.maxDistance)) return;

            m_Buffer.BeginSample(SampleName);

            m_Lighting.Setup(context, m_CullingResults, shadowSettings);

            m_Buffer.EndSample(SampleName);

            Setup();
            DrawVisibleGeometry(useDynamicBatching, useGPUInstancing);
            DrawUnsupportedShaders();
            DrawGizmos();

            if (shadowSettings.enableShadows)
                m_Lighting.Cleanup();

            Submit();
        }

        private bool Cull (float maxShadowDistance) {
            if (m_Camera.TryGetCullingParameters(out ScriptableCullingParameters p)) {
                p.shadowDistance = Mathf.Min(maxShadowDistance, m_Camera.farClipPlane);
                m_CullingResults = m_Context.Cull(ref p);
                return true;
            }
            return false;
        }

        private bool Cull()
        {
            if (!m_Camera.TryGetCullingParameters(out ScriptableCullingParameters p)) return false;
            p.shadowDistance = m_Camera.farClipPlane;
            m_CullingResults = m_Context.Cull(ref p);
            return true;
        }

        private void Setup()
        {
            m_Context.SetupCameraProperties(m_Camera);
            var worldToViewMatrix = m_Camera.worldToCameraMatrix;
            m_Buffer.SetGlobalMatrix("_WorldToViewMatrix", worldToViewMatrix);
            var flags = m_Camera.clearFlags;
            m_Buffer.ClearRenderTarget(
                flags <= CameraClearFlags.Depth,
                flags <= CameraClearFlags.Color,
                flags == CameraClearFlags.Color ? m_Camera.backgroundColor.linear : Color.clear
            );
            m_Buffer.BeginSample(SampleName);
            ExecuteBuffer();
        }

        private void Submit()
        {
            m_Buffer.EndSample(SampleName);
            ExecuteBuffer();
            m_Context.Submit();
        }

        private void ExecuteBuffer()
        {
            m_Context.ExecuteCommandBuffer(m_Buffer);
            m_Buffer.Clear();
        }

        private void DrawVisibleGeometry(bool useDynamicBatching, bool useGPUInstancing)
        {
            var sortingSettings = new SortingSettings(m_Camera)
            {
                criteria = SortingCriteria.CommonOpaque
            };

            var drawingSettings = new DrawingSettings(UnlitShaderTagId, sortingSettings)
            {
                enableDynamicBatching = useDynamicBatching,
                enableInstancing = useGPUInstancing,
                perObjectData = PerObjectData.Lightmaps | PerObjectData.LightProbe | PerObjectData.LightProbeProxyVolume | PerObjectData.ShadowMask |
                                PerObjectData.OcclusionProbe | PerObjectData.OcclusionProbeProxyVolume
            };

            drawingSettings.SetShaderPassName(1, LitShaderTagId);

            var filteringSettings = new FilteringSettings(RenderQueueRange.opaque);

            m_Context.DrawRenderers(m_CullingResults, ref drawingSettings, ref filteringSettings);

            m_Context.DrawSkybox(m_Camera);

            sortingSettings.criteria = SortingCriteria.CommonTransparent;
            drawingSettings.sortingSettings = sortingSettings;
            filteringSettings.renderQueueRange = RenderQueueRange.transparent;

            m_Context.DrawRenderers(m_CullingResults, ref drawingSettings, ref filteringSettings);
        }
    }
}