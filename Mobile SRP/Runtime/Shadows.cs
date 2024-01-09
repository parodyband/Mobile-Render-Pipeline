using System;
using JetBrains.Annotations;
using UnityEngine;
using UnityEngine.Experimental.Rendering.RenderGraphModule;
using UnityEngine.Rendering;

public class Shadows
{
    private const int MaxShadowedDirLightCount = 4, MaxShadowedOtherLightCount = 16;
    private const int MaxCascades = 4;

    private static readonly GlobalKeyword[] DirectionalFilterKeywords =
    {
        GlobalKeyword.Create("_DIRECTIONAL_PCF3"),
        GlobalKeyword.Create("_DIRECTIONAL_PCF5"),
        GlobalKeyword.Create("_DIRECTIONAL_PCF7"),
    };

    private static readonly GlobalKeyword[] OtherFilterKeywords =
    {
        GlobalKeyword.Create("_OTHER_PCF3"),
        GlobalKeyword.Create("_OTHER_PCF5"),
        GlobalKeyword.Create("_OTHER_PCF7"),
    };

    private static readonly GlobalKeyword[] CascadeBlendKeywords =
    {
        GlobalKeyword.Create("_CASCADE_BLEND_SOFT"),
        GlobalKeyword.Create("_CASCADE_BLEND_DITHER"),
    };

    private static readonly GlobalKeyword[] ShadowMaskKeywords =
    {
        GlobalKeyword.Create("_SHADOW_MASK_ALWAYS"),
        GlobalKeyword.Create("_SHADOW_MASK_DISTANCE"),
    };

    private static readonly int
        DirShadowAtlasId = Shader.PropertyToID("_DirectionalShadowAtlas"),
        DirShadowMatricesId = Shader.PropertyToID("_DirectionalShadowMatrices"),
        OtherShadowAtlasId = Shader.PropertyToID("_OtherShadowAtlas"),
        OtherShadowMatricesId = Shader.PropertyToID("_OtherShadowMatrices"),
        OtherShadowTilesId = Shader.PropertyToID("_OtherShadowTiles"),
        CascadeCountId = Shader.PropertyToID("_CascadeCount"),
        CascadeCullingSpheresId = Shader.PropertyToID("_CascadeCullingSpheres"),
        CascadeDataId = Shader.PropertyToID("_CascadeData"),
        ShadowAtlasSizeId = Shader.PropertyToID("_ShadowAtlasSize"),
        ShadowDistanceFadeId = Shader.PropertyToID("_ShadowDistanceFade"),
        ShadowPancakingId = Shader.PropertyToID("_ShadowPancaking");

    private static readonly Vector4[]
        CascadeCullingSpheres = new Vector4[MaxCascades],
        CascadeData = new Vector4[MaxCascades],
        OtherShadowTiles = new Vector4[MaxShadowedOtherLightCount];

    private static readonly Matrix4x4[]
        DirShadowMatrices =
            new Matrix4x4[MaxShadowedDirLightCount * MaxCascades],
        OtherShadowMatrices = new Matrix4x4[MaxShadowedOtherLightCount];

    private struct ShadowedDirectionalLight
    {
        public int visibleLightIndex;
        public float slopeScaleBias;
        public float nearPlaneOffset;
    }

    private readonly ShadowedDirectionalLight[] m_ShadowedDirectionalLights =
        new ShadowedDirectionalLight[MaxShadowedDirLightCount];

    private struct ShadowedOtherLight
    {
        public int visibleLightIndex;
        public float slopeScaleBias;
        public float normalBias;
        public bool isPoint;
    }

    private readonly ShadowedOtherLight[] m_ShadowedOtherLights =
        new ShadowedOtherLight[MaxShadowedOtherLightCount];

    private int m_ShadowedDirLightCount, m_ShadowedOtherLightCount;

    private CommandBuffer m_Buffer;

    private ScriptableRenderContext m_Context;

    private CullingResults m_CullingResults;

    private ShadowSettings m_Settings;

    private bool m_UseShadowMask;

    private Vector4 m_AtlasSizes;

    private TextureHandle m_DirectionalAtlas, m_OtherAtlas;

    public void Setup(CullingResults cullingResults, ShadowSettings settings)
    {
        m_CullingResults = cullingResults;
        m_Settings = settings;
        m_ShadowedDirLightCount = m_ShadowedOtherLightCount = 0;
        m_UseShadowMask = false;
    }

    public Vector4 ReserveDirectionalShadows(Light light, int visibleLightIndex)
    {
        if (m_ShadowedDirLightCount >= MaxShadowedDirLightCount ||
            light.shadows == LightShadows.None || !(light.shadowStrength > 0f)) return new Vector4(0f, 0f, 0f, -1f);
        float maskChannel = -1;
        var lightBaking = light.bakingOutput;
        if (
            lightBaking.lightmapBakeType == LightmapBakeType.Mixed &&
            lightBaking.mixedLightingMode == MixedLightingMode.Shadowmask)
        {
            m_UseShadowMask = true;
            maskChannel = lightBaking.occlusionMaskChannel;
        }

        if (!m_CullingResults.GetShadowCasterBounds(visibleLightIndex, out _))
        {
            return new Vector4(-light.shadowStrength, 0f, 0f, maskChannel);
        }

        m_ShadowedDirectionalLights[m_ShadowedDirLightCount] =
            new ShadowedDirectionalLight
            {
                visibleLightIndex = visibleLightIndex,
                slopeScaleBias = light.shadowBias,
                nearPlaneOffset = light.shadowNearPlane
            };
        return new Vector4(
            light.shadowStrength,
            m_Settings.directional.cascadeCount * m_ShadowedDirLightCount++,
            light.shadowNormalBias, maskChannel);
    }

    public Vector4 ReserveOtherShadows(Light light, int visibleLightIndex)
    {
        if (light.shadows == LightShadows.None || light.shadowStrength <= 0f)
        {
            return new Vector4(0f, 0f, 0f, -1f);
        }

        var maskChannel = -1f;
        var lightBaking = light.bakingOutput;
        if (
            lightBaking.lightmapBakeType == LightmapBakeType.Mixed &&
            lightBaking.mixedLightingMode == MixedLightingMode.Shadowmask)
        {
            m_UseShadowMask = true;
            maskChannel = lightBaking.occlusionMaskChannel;
        }

        var isPoint = light.type == LightType.Point;
        var newLightCount = m_ShadowedOtherLightCount + (isPoint ? 6 : 1);
        if (
            newLightCount > MaxShadowedOtherLightCount ||
            !m_CullingResults.GetShadowCasterBounds(visibleLightIndex, out _))
        {
            return new Vector4(-light.shadowStrength, 0f, 0f, maskChannel);
        }

        m_ShadowedOtherLights[m_ShadowedOtherLightCount] = new ShadowedOtherLight
        {
            visibleLightIndex = visibleLightIndex,
            slopeScaleBias = light.shadowBias,
            normalBias = light.shadowNormalBias,
            isPoint = isPoint
        };

        var data = new Vector4(
            light.shadowStrength, m_ShadowedOtherLightCount,
            isPoint ? 1f : 0f, maskChannel);
        m_ShadowedOtherLightCount = newLightCount;
        return data;
    }

    public ShadowTextures GetRenderTextures(
        RenderGraph renderGraph,
        RenderGraphBuilder builder)
    {
        var atlasSize = (int)m_Settings.directional.atlasSize;
        var desc = new TextureDesc(atlasSize, atlasSize)
        {
            depthBufferBits = DepthBits.Depth32,
            isShadowMap = true,
            name = "Directional Shadow Atlas"
        };
        m_DirectionalAtlas = m_ShadowedDirLightCount > 0
            ? builder.WriteTexture(renderGraph.CreateTexture(desc))
            : renderGraph.defaultResources.defaultShadowTexture;

        atlasSize = (int)m_Settings.other.atlasSize;
        desc.width = desc.height = atlasSize;
        desc.name = "Other Shadow Atlas";
        m_OtherAtlas = m_ShadowedOtherLightCount > 0
            ? builder.WriteTexture(renderGraph.CreateTexture(desc))
            : renderGraph.defaultResources.defaultShadowTexture;
        return new ShadowTextures(m_DirectionalAtlas, m_OtherAtlas);
    }

    public void Render(RenderGraphContext context)
    {
        m_Buffer = context.cmd;
        m_Context = context.renderContext;

        if (m_ShadowedDirLightCount > 0)
        {
            RenderDirectionalShadows();
        }

        if (m_ShadowedOtherLightCount > 0)
        {
            RenderOtherShadows();
        }

        m_Buffer.SetGlobalTexture(DirShadowAtlasId, m_DirectionalAtlas);
        m_Buffer.SetGlobalTexture(OtherShadowAtlasId, m_OtherAtlas);

        SetKeywords(ShadowMaskKeywords,
            m_UseShadowMask ? QualitySettings.shadowmaskMode == ShadowmaskMode.Shadowmask ? 0 : 1 : -1);
        m_Buffer.SetGlobalInt(CascadeCountId, m_ShadowedDirLightCount > 0 ? m_Settings.directional.cascadeCount : 0);
        var f = 1f - m_Settings.directional.cascadeFade;
        m_Buffer.SetGlobalVector(ShadowDistanceFadeId, new Vector4(
            1f / m_Settings.maxDistance, 1f / m_Settings.distanceFade,
            1f / (1f - f * f)));
        m_Buffer.SetGlobalVector(ShadowAtlasSizeId, m_AtlasSizes);
        ExecuteBuffer();
    }

    private void RenderDirectionalShadows()
    {
        var atlasSize = (int)m_Settings.directional.atlasSize;
        m_AtlasSizes.x = atlasSize;
        m_AtlasSizes.y = 1f / atlasSize;
        m_Buffer.SetRenderTarget(
            m_DirectionalAtlas,
            RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store);
        m_Buffer.ClearRenderTarget(true, false, Color.clear);
        m_Buffer.SetGlobalFloat(ShadowPancakingId, 1f);
        m_Buffer.BeginSample("Directional Shadows");
        ExecuteBuffer();

        var tiles = m_ShadowedDirLightCount * m_Settings.directional.cascadeCount;
        var split = tiles <= 1 ? 1 : tiles <= 4 ? 2 : 4;
        var tileSize = atlasSize / split;

        for (var i = 0; i < m_ShadowedDirLightCount; i++)
        {
            RenderDirectionalShadows(i, split, tileSize);
        }

        m_Buffer.SetGlobalVectorArray(
            CascadeCullingSpheresId, CascadeCullingSpheres);
        m_Buffer.SetGlobalVectorArray(CascadeDataId, CascadeData);
        m_Buffer.SetGlobalMatrixArray(DirShadowMatricesId, DirShadowMatrices);
        SetKeywords(
            DirectionalFilterKeywords, (int)m_Settings.directional.filter - 1);
        SetKeywords(
            CascadeBlendKeywords, (int)m_Settings.directional.cascadeBlend - 1);
        m_Buffer.EndSample("Directional Shadows");
        ExecuteBuffer();
    }

    private void RenderDirectionalShadows(int index, int split, int tileSize)
    {
        var light = m_ShadowedDirectionalLights[index];
        var shadowSettings = new ShadowDrawingSettings(
            m_CullingResults, light.visibleLightIndex,
            BatchCullingProjectionType.Orthographic)
        {
            useRenderingLayerMaskTest = true
        };
        var cascadeCount = m_Settings.directional.cascadeCount;
        var tileOffset = index * cascadeCount;
        var ratios = m_Settings.directional.CascadeRatios;
        var cullingFactor =
            Mathf.Max(0f, 0.8f - m_Settings.directional.cascadeFade);
        var tileScale = 1f / split;
        for (var i = 0; i < cascadeCount; i++)
        {
            m_CullingResults.ComputeDirectionalShadowMatricesAndCullingPrimitives(
                light.visibleLightIndex, i, cascadeCount, ratios, tileSize,
                light.nearPlaneOffset, out Matrix4x4 viewMatrix,
                out Matrix4x4 projectionMatrix, out ShadowSplitData splitData);
            splitData.shadowCascadeBlendCullingFactor = cullingFactor;
            shadowSettings.splitData = splitData;
            if (index == 0)
            {
                SetCascadeData(i, splitData.cullingSphere, tileSize);
            }

            var tileIndex = tileOffset + i;
            DirShadowMatrices[tileIndex] = ConvertToAtlasMatrix(
                projectionMatrix * viewMatrix,
                SetTileViewport(tileIndex, split, tileSize), tileScale);
            m_Buffer.SetViewProjectionMatrices(viewMatrix, projectionMatrix);
            m_Buffer.SetGlobalDepthBias(0f, light.slopeScaleBias);
            ExecuteBuffer();
            m_Context.DrawShadows(ref shadowSettings);
            m_Buffer.SetGlobalDepthBias(0f, 0f);
        }
    }

    private void SetCascadeData(int index, Vector4 cullingSphere, float tileSize)
    {
        var texelSize = 2f * cullingSphere.w / tileSize;
        var filterSize =
            texelSize * ((float)m_Settings.directional.filter + 1f);
        cullingSphere.w -= filterSize;
        cullingSphere.w *= cullingSphere.w;
        CascadeCullingSpheres[index] = cullingSphere;
        CascadeData[index] = new Vector4(
            1f / cullingSphere.w,
            filterSize * 1.4142136f);
    }

    private void RenderOtherShadows()
    {
        var atlasSize = (int)m_Settings.other.atlasSize;
        m_AtlasSizes.z = atlasSize;
        m_AtlasSizes.w = 1f / atlasSize;
        m_Buffer.SetRenderTarget(
            m_OtherAtlas,
            RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store);
        m_Buffer.ClearRenderTarget(true, false, Color.clear);
        m_Buffer.SetGlobalFloat(ShadowPancakingId, 0f);
        m_Buffer.BeginSample("Other Shadows");
        ExecuteBuffer();

        var tiles = m_ShadowedOtherLightCount;
        var split = tiles <= 1 ? 1 : tiles <= 4 ? 2 : 4;
        var tileSize = atlasSize / split;

        for (var i = 0; i < m_ShadowedOtherLightCount;)
        {
            if (m_ShadowedOtherLights[i].isPoint)
            {
                RenderPointShadows(i, split, tileSize);
                i += 6;
            }
            else
            {
                RenderSpotShadows(i, split, tileSize);
                i += 1;
            }
        }

        m_Buffer.SetGlobalMatrixArray(OtherShadowMatricesId, OtherShadowMatrices);
        m_Buffer.SetGlobalVectorArray(OtherShadowTilesId, OtherShadowTiles);
        SetKeywords(OtherFilterKeywords, (int)m_Settings.other.filter - 1);
        m_Buffer.EndSample("Other Shadows");
        ExecuteBuffer();
    }

    private void RenderSpotShadows(int index, int split, int tileSize)
    {
        var light = m_ShadowedOtherLights[index];
        var shadowSettings = new ShadowDrawingSettings(
            m_CullingResults, light.visibleLightIndex,
            BatchCullingProjectionType.Perspective)
        {
            useRenderingLayerMaskTest = true
        };
        m_CullingResults.ComputeSpotShadowMatricesAndCullingPrimitives(
            light.visibleLightIndex, out var viewMatrix,
            out var projectionMatrix, out var splitData);
        shadowSettings.splitData = splitData;
        var texelSize = 2f / (tileSize * projectionMatrix.m00);
        var filterSize = texelSize * ((float)m_Settings.other.filter + 1f);
        var bias = light.normalBias * filterSize * 1.4142136f;
        var offset = SetTileViewport(index, split, tileSize);
        var tileScale = 1f / split;
        SetOtherTileData(index, offset, tileScale, bias);
        OtherShadowMatrices[index] = ConvertToAtlasMatrix(
            projectionMatrix * viewMatrix, offset, tileScale);

        m_Buffer.SetViewProjectionMatrices(viewMatrix, projectionMatrix);
        m_Buffer.SetGlobalDepthBias(0f, light.slopeScaleBias);
        ExecuteBuffer();
        m_Context.DrawShadows(ref shadowSettings);
        m_Buffer.SetGlobalDepthBias(0f, 0f);
    }

    private void RenderPointShadows(int index, int split, int tileSize)
    {
        var light = m_ShadowedOtherLights[index];
        var shadowSettings = new ShadowDrawingSettings(
            m_CullingResults, light.visibleLightIndex,
            BatchCullingProjectionType.Perspective)
        {
            useRenderingLayerMaskTest = true
        };
        var texelSize = 2f / tileSize;
        var filterSize = texelSize * ((float)m_Settings.other.filter + 1f);
        var bias = light.normalBias * filterSize * 1.4142136f;
        var tileScale = 1f / split;
        var fovBias =
            Mathf.Atan(1f + bias + filterSize) * Mathf.Rad2Deg * 2f - 90f;
        for (var i = 0; i < 6; i++)
        {
            m_CullingResults.ComputePointShadowMatricesAndCullingPrimitives(
                light.visibleLightIndex, (CubemapFace)i, fovBias,
                out var viewMatrix, out var projectionMatrix,
                out var splitData);
            viewMatrix.m11 = -viewMatrix.m11;
            viewMatrix.m12 = -viewMatrix.m12;
            viewMatrix.m13 = -viewMatrix.m13;

            shadowSettings.splitData = splitData;
            var tileIndex = index + i;
            var offset = SetTileViewport(tileIndex, split, tileSize);
            SetOtherTileData(tileIndex, offset, tileScale, bias);
            OtherShadowMatrices[tileIndex] = ConvertToAtlasMatrix(
                projectionMatrix * viewMatrix, offset, tileScale);

            m_Buffer.SetViewProjectionMatrices(viewMatrix, projectionMatrix);
            m_Buffer.SetGlobalDepthBias(0f, light.slopeScaleBias);
            ExecuteBuffer();
            m_Context.DrawShadows(ref shadowSettings);
            m_Buffer.SetGlobalDepthBias(0f, 0f);
        }
    }

    private void SetOtherTileData(int index, Vector2 offset, float scale, float bias)
    {
        var border = m_AtlasSizes.w * 0.5f;
        Vector4 data;
        data.x = offset.x * scale + border;
        data.y = offset.y * scale + border;
        data.z = scale - border - border;
        data.w = bias;
        OtherShadowTiles[index] = data;
    }

    private static Matrix4x4 ConvertToAtlasMatrix(Matrix4x4 m, Vector2 offset, float scale)
    {
        if (SystemInfo.usesReversedZBuffer)
        {
            m.m20 = -m.m20;
            m.m21 = -m.m21;
            m.m22 = -m.m22;
            m.m23 = -m.m23;
        }

        m.m00 = (0.5f * (m.m00 + m.m30) + offset.x * m.m30) * scale;
        m.m01 = (0.5f * (m.m01 + m.m31) + offset.x * m.m31) * scale;
        m.m02 = (0.5f * (m.m02 + m.m32) + offset.x * m.m32) * scale;
        m.m03 = (0.5f * (m.m03 + m.m33) + offset.x * m.m33) * scale;
        m.m10 = (0.5f * (m.m10 + m.m30) + offset.y * m.m30) * scale;
        m.m11 = (0.5f * (m.m11 + m.m31) + offset.y * m.m31) * scale;
        m.m12 = (0.5f * (m.m12 + m.m32) + offset.y * m.m32) * scale;
        m.m13 = (0.5f * (m.m13 + m.m33) + offset.y * m.m33) * scale;
        m.m20 = 0.5f * (m.m20 + m.m30);
        m.m21 = 0.5f * (m.m21 + m.m31);
        m.m22 = 0.5f * (m.m22 + m.m32);
        m.m23 = 0.5f * (m.m23 + m.m33);
        return m;
    }

    private Vector2 SetTileViewport(int index, int split, float tileSize)
    {
        var offset = new Vector2(index % split, index / split);
        m_Buffer.SetViewport(new Rect(
            offset.x * tileSize, offset.y * tileSize, tileSize, tileSize));
        return offset;
    }

    private void SetKeywords([NotNull] GlobalKeyword[] keywords, int enabledIndex)
    {
        if (keywords == null) throw new ArgumentNullException(nameof(keywords));
        for (var i = 0; i < keywords.Length; i++)
        {
            m_Buffer.SetKeyword(keywords[i], i == enabledIndex);
        }
    }

    private void ExecuteBuffer()
    {
        m_Context.ExecuteCommandBuffer(m_Buffer);
        m_Buffer.Clear();
    }
}