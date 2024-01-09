partial class MobileRenderPipelineAsset
{
#if UNITY_EDITOR

	private static readonly string[] RenderingLayerNames;

	static MobileRenderPipelineAsset()
	{
		RenderingLayerNames = new string[31];
		for (var i = 0; i < RenderingLayerNames.Length; i++)
		{
			RenderingLayerNames[i] = "Layer " + (i + 1);
		}
	}

	public override string[] renderingLayerMaskNames => RenderingLayerNames;

#endif
}
