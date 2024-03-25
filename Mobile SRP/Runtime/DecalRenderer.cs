using System.Collections.Generic;
using System.Linq;
using UnityEngine;	
public static class DecalRenderer
{
	private static readonly Dictionary<MobileProjector,Decal> Decals = new();
	private static readonly int DecalAtlas = Shader.PropertyToID("_DecalAtlas");
	private static readonly int DecalDimensions = Shader.PropertyToID("_DecalDimensions");
	private static readonly int DecalCount = Shader.PropertyToID("_DecalCount");

	public static void InitializeDecalShaderResources(DecalSettings settings)
	{
		Shader.SetGlobalTexture(DecalAtlas, settings.decalAtlas);
		Shader.SetGlobalVector(DecalDimensions, settings.atlasDimensions);
	}
	
	public static void AddDecal(MobileProjector projector, Decal decal)
	{
		Decals.TryAdd(projector, decal);
	}
	
	public static void RemoveDecal(MobileProjector projector)
	{
		Decals.Remove(projector);
	}
	
	public static void UpdateDecals()
	{
		var array = Decals.Values.ToArray();
		var buffer = new ComputeBuffer(array.Length, 144);
		buffer.SetData(array);
		Shader.SetGlobalBuffer(Shader.PropertyToID("_Decals"), buffer);
		Shader.SetGlobalInteger(DecalCount, array.Length);
	}
}

public struct Decal
{
	public Matrix4x4 ProjectorMatrix;
	public Matrix4x4 BoxMatrix;
	public Vector4 ProjectorParams;
}