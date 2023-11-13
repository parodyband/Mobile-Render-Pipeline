using Unity.Collections;
using UnityEngine;
using UnityEngine.Rendering;

namespace Mobile_SRP.Runtime
{
    public class Lighting
    {
        private const string BufferName = "Lighting";
        private const int MaxDirLightCount = 4, MaxOtherLightCount = 8;

        private static readonly int DirLightCountId = Shader.PropertyToID("_DirectionalLightCount");
        private static readonly int DirLightColorsId = Shader.PropertyToID("_DirectionalLightColors");
        private static readonly int DirLightDirectionsId = Shader.PropertyToID("_DirectionalLightDirections");
        private static readonly int DirLightShadowDataId = Shader.PropertyToID("_DirectionalLightShadowData");
        
        private static readonly int OtherLightCountID = Shader.PropertyToID("_OtherLightCount");
        private static readonly int OtherLightColorsId = Shader.PropertyToID("_OtherLightColors");
        private static readonly int OtherLightPositionsId = Shader.PropertyToID("_OtherLightPositions");


        private static readonly Vector4[] DirLightColors = new Vector4[MaxDirLightCount];
        private static readonly Vector4[] DirLightDirections = new Vector4[MaxDirLightCount];
        private static readonly Vector4[] DirLightShadowData = new Vector4[MaxDirLightCount];
        
        private static readonly Vector4[] OtherLightColors = new Vector4[MaxOtherLightCount];
        private static readonly Vector4[] OtherLightPositions = new Vector4[MaxOtherLightCount];

        private readonly CommandBuffer m_Buffer = new()
        {
            name = BufferName
        };

        private CullingResults m_CullingResults;
        private readonly Shadows m_Shadows = new();

        public void Setup(ScriptableRenderContext context, CullingResults cullingResults, ShadowSettings shadowSettings)
        {
            m_CullingResults = cullingResults;
            m_Buffer.BeginSample(BufferName);
            if (shadowSettings.enableShadows)
                m_Shadows.Setup(context, cullingResults, shadowSettings);
            SetupLights(shadowSettings.enableShadows);
            if (shadowSettings.enableShadows)
                m_Shadows.Render();
            m_Buffer.EndSample(BufferName);
            context.ExecuteCommandBuffer(m_Buffer);
            m_Buffer.Clear();
        }
        
        public void Cleanup()
        {
            m_Shadows.Cleanup();
        }

        private void SetupLights(bool shadowsEnabled = true)
        {
            NativeArray<VisibleLight> visibleLights = m_CullingResults.visibleLights;
            int dirLightCount = 0, otherLightCount = 0;
            for (var i = 0; i < visibleLights.Length; i++)
            {
                var visibleLight = visibleLights[i];
                switch (visibleLight.lightType)
                {
                    case LightType.Directional:
                        if (dirLightCount < MaxDirLightCount)
                            SetupDirectionalLight(dirLightCount++, ref visibleLight, shadowsEnabled);
                        break;
                    case LightType.Point:
                        if (otherLightCount < MaxOtherLightCount)
                            SetupPointLight(otherLightCount++, ref visibleLight);
                        break;
                    case LightType.Spot:
                        Debug.LogError("Unsupported LightType");
                        break;
                    case LightType.Area:
                        Debug.LogError("Unsupported LightType");
                        break;
                    case LightType.Disc:
                    default:
                        Debug.LogError("Unsupported LightType");
                        break;
                }
            }
            m_Buffer.SetGlobalInt(DirLightCountId, dirLightCount);
            if (dirLightCount > 0)
            {
                m_Buffer.SetGlobalVectorArray(DirLightColorsId, DirLightColors);
                m_Buffer.SetGlobalVectorArray(DirLightDirectionsId, DirLightDirections);
                if (!shadowsEnabled) return;
                m_Buffer.SetGlobalVectorArray(DirLightShadowDataId, DirLightShadowData);
            }
            
            m_Buffer.SetGlobalInt(OtherLightCountID, otherLightCount);
            if (otherLightCount > 0)
            {
                m_Buffer.SetGlobalVectorArray(OtherLightColorsId, OtherLightColors);
                m_Buffer.SetGlobalVectorArray(
                    OtherLightPositionsId, OtherLightPositions
                );
            }
        }
        
        private void SetupPointLight (int index, ref VisibleLight visibleLight) {
            OtherLightColors[index] = visibleLight.finalColor;
            var position = visibleLight.localToWorldMatrix.GetColumn(3);
            position.w = 1f / Mathf.Max(visibleLight.range * visibleLight.range, 0.11f);
            OtherLightPositions[index] = position;
        }

        private void SetupDirectionalLight(int index, ref VisibleLight visibleLight, bool shadowsEnabled = true)
        {
            DirLightColors[index] = visibleLight.finalColor;
            DirLightDirections[index] = -visibleLight.localToWorldMatrix.GetColumn(2);
            if (!shadowsEnabled) return;
            DirLightShadowData[index] = m_Shadows.ReserveDirectionalShadows(visibleLight.light, index);
        }
    }
}