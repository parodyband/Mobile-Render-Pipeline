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

        private static readonly ShaderTagId TimeShaderTagId = new("Time");

        private CullingResults m_CullingResults;

        private readonly Lighting m_Lighting = new();

        private readonly CommandBuffer m_Buffer = new()
        {
            name = BufferName
        };

        public float FrameTime = 0;
        public void Render(ScriptableRenderContext context, Camera camera, bool useDynamicBatching,
            bool useGPUInstancing, bool useLightsPerObject, ShadowSettings shadowSettings)
        {
            m_Context = context;
            m_Camera = camera;
            
            FrameTime += Time.deltaTime;
            
            if (FrameTime > 100)
            {
                FrameTime = 0;
            }

            m_Buffer.SetGlobalVector("_Time", new Vector2(Time.deltaTime, FrameTime));
            //Debug.Log(FrameTime);
            PrepareBuffer();
            PrepareForSceneWindow();

            if (!Cull(shadowSettings.maxDistance)) return;

            m_Buffer.BeginSample(SampleName);
            
            m_Lighting.Setup(context, m_CullingResults, shadowSettings, useLightsPerObject);

            m_Buffer.EndSample(SampleName);

            Setup();
            DrawVisibleGeometry(useDynamicBatching, useGPUInstancing, useLightsPerObject);
            DrawUnsupportedShaders();
            DrawGizmos();

            m_Lighting.Cleanup();

            Submit();
        }

        private bool Cull(float maxShadowDistance)
        {
            if (!m_Camera.TryGetCullingParameters(out ScriptableCullingParameters p)) return false;
            p.shadowDistance = Mathf.Min(maxShadowDistance, m_Camera.farClipPlane);
            m_CullingResults = m_Context.Cull(ref p);
            return true;
        }

        private void Setup()
        {
            m_Context.SetupCameraProperties(m_Camera);
            var worldToViewMatrix = m_Camera.worldToCameraMatrix;
            m_Buffer.SetGlobalMatrix("_WorldToViewMatrix", worldToViewMatrix);
            //m_Buffer.SetGlobalMatrix("_ViewToWorldMatrix", worldToViewMatrix.inverse);
            //m_Buffer.SetGlobalVector("_WorldSpaceCameraPos", m_Camera.transform.position);
            m_Buffer.SetGlobalVector("_ProjectionParams", GetProjectionParams(m_Camera));
            m_Buffer.SetGlobalVector("_ScreenParams",
                new Vector4(m_Camera.pixelWidth, m_Camera.pixelHeight, 1 / m_Camera.pixelWidth,
                    1 / m_Camera.pixelHeight));
            var flags = m_Camera.clearFlags;
            m_Buffer.ClearRenderTarget(
                flags <= CameraClearFlags.Depth,
                flags <= CameraClearFlags.Color,
                flags == CameraClearFlags.Color ? m_Camera.backgroundColor.linear : Color.clear
            );
            m_Buffer.BeginSample(SampleName);
            ExecuteBuffer();
        }

        private Vector4 GetProjectionParams(Camera camera)
        {
            var p = camera.projectionMatrix;
            var f = camera.nearClipPlane;
            var n = camera.farClipPlane;
            var a = p[2, 2];
            var b = p[3, 2];
            return new Vector4(-1 / (a * f + b), 1 / (a * n + b), -a / (a * n + b), a / (a * f + b));
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

        private void DrawVisibleGeometry(
            bool useDynamicBatching, bool useGPUInstancing, bool useLightsPerObject
        )
        {
            var lightsPerObjectFlags = useLightsPerObject
                ? PerObjectData.LightData | PerObjectData.LightIndices
                : PerObjectData.None;
            var sortingSettings = new SortingSettings(m_Camera)
            {
                criteria = SortingCriteria.CommonOpaque
            };
            var drawingSettings = new DrawingSettings(
                UnlitShaderTagId, sortingSettings
            )
            {
                enableDynamicBatching = useDynamicBatching,
                enableInstancing = useGPUInstancing,
                perObjectData =
                    PerObjectData.ReflectionProbes |
                    PerObjectData.Lightmaps | PerObjectData.ShadowMask |
                    PerObjectData.LightProbe | PerObjectData.OcclusionProbe |
                    PerObjectData.LightProbeProxyVolume |
                    PerObjectData.OcclusionProbeProxyVolume |
                    lightsPerObjectFlags
            };
            drawingSettings.SetShaderPassName(1, LitShaderTagId);

            var filteringSettings = new FilteringSettings(RenderQueueRange.opaque);

            m_Context.DrawRenderers(
                m_CullingResults, ref drawingSettings, ref filteringSettings
            );

            m_Context.DrawSkybox(m_Camera);

            sortingSettings.criteria = SortingCriteria.CommonTransparent;
            drawingSettings.sortingSettings = sortingSettings;
            filteringSettings.renderQueueRange = RenderQueueRange.transparent;

            m_Context.DrawRenderers(
                m_CullingResults, ref drawingSettings, ref filteringSettings
            );
        }
    }
}