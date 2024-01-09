using UnityEngine;
using UnityEngine.Rendering;

public readonly struct CameraRendererCopier
{
	private static readonly int
		SourceTextureID = Shader.PropertyToID("_SourceTexture"),
		SrcBlendID = Shader.PropertyToID("_CameraSrcBlend"),
		DstBlendID = Shader.PropertyToID("_CameraDstBlend");

	private static readonly Rect FullViewRect = new(0f, 0f, 1f, 1f);

	private static readonly bool CopyTextureSupported =
		SystemInfo.copyTextureSupport > CopyTextureSupport.None;

	public static bool RequiresRenderTargetResetAfterCopy =>
		!CopyTextureSupported;

	public Camera Camera { get; }

	private readonly Material m_Material;

	private readonly CameraSettings.FinalBlendMode m_FinalBlendMode;

	public CameraRendererCopier(
		Material material,
		Camera camera,
		CameraSettings.FinalBlendMode finalBlendMode)
	{
		m_Material = material;
		Camera = camera;
		m_FinalBlendMode = finalBlendMode;
	}

	public void Copy(
		CommandBuffer buffer,
		RenderTargetIdentifier from,
		RenderTargetIdentifier to,
		bool isDepth)
	{
		if (CopyTextureSupported)
		{
			buffer.CopyTexture(from, to);
		}
		else
		{
			CopyByDrawing(buffer, from, to, isDepth);
		}
	}

	public void CopyByDrawing(
		CommandBuffer buffer,
		RenderTargetIdentifier from,
		RenderTargetIdentifier to,
		bool isDepth)
	{
		buffer.SetGlobalTexture(SourceTextureID, from);
		buffer.SetRenderTarget(
			to, RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store);
		buffer.SetViewport(Camera.pixelRect);
		buffer.DrawProcedural(
			Matrix4x4.identity, m_Material, isDepth ? 1 : 0,
			MeshTopology.Triangles, 3);
	}

	public void CopyToCameraTarget(
		CommandBuffer buffer,
		RenderTargetIdentifier from)
	{
		buffer.SetGlobalFloat(SrcBlendID, (float)m_FinalBlendMode.source);
		buffer.SetGlobalFloat(DstBlendID, (float)m_FinalBlendMode.destination);
		buffer.SetGlobalTexture(SourceTextureID, from);
		buffer.SetRenderTarget(
			BuiltinRenderTextureType.CameraTarget,
			m_FinalBlendMode.destination == BlendMode.Zero &&
				Camera.rect == FullViewRect ?
				RenderBufferLoadAction.DontCare : RenderBufferLoadAction.Load,
			RenderBufferStoreAction.Store);
		buffer.SetViewport(Camera.pixelRect);
		buffer.DrawProcedural(
			Matrix4x4.identity, m_Material, 0, MeshTopology.Triangles, 3);
		buffer.SetGlobalFloat(SrcBlendID, 1f);
		buffer.SetGlobalFloat(DstBlendID, 0f);
	}
}
