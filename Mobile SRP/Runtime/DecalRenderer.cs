using System.Collections.Generic;
using System.Linq;
using UnityEngine;	
public static class DecalRenderer
{
	private static readonly Dictionary<MobileProjector,Decal> Decals = new();
	private static readonly int DecalAtlas = Shader.PropertyToID("_DecalAtlas");
	private static readonly int DecalDimensions = Shader.PropertyToID("_DecalDimensions");
	private static readonly int DecalCount = Shader.PropertyToID("_DecalCount");
	private static readonly int MaxDecalsOnScreen = Shader.PropertyToID("_MaxDecalsOnScreen");

	private static bool _isDirty = true;

	public static void InitializeDecalShaderResources(DecalSettings settings)
	{
		Shader.SetGlobalTexture(DecalAtlas, settings.decalAtlas);
		Shader.SetGlobalVector(DecalDimensions, settings.atlasDimensions);
		Shader.SetGlobalInteger(MaxDecalsOnScreen, settings.maxDecalsOnScreen);
	}
	
	private static void AddDecal(MobileProjector projector, Decal decal)
	{
		Decals.TryAdd(projector, decal);
	}
	
	public static void RemoveDecal(MobileProjector projector)
	{
		Decals.Remove(projector);
	}
	
	public static void UpdateDecal(MobileProjector projector, Decal decal)
	{
		AddDecal(projector, decal);
		Decals[projector] = decal;
		_isDirty = true;
	}
	
	public static void FlushDecals()
	{
		Decals.Clear();
		_isDirty = true;
	}

	public static void UpdateDecals()
	{
		if (Decals.Count == 0)
		{
			Shader.SetGlobalInteger(DecalCount, 0);
			return;
		}
		if (!_isDirty) return;
		var array = Decals.Values.ToArray();
		var buffer = new ComputeBuffer(array.Length, 144);
		buffer.SetData(array);
		Shader.SetGlobalBuffer(Shader.PropertyToID("_Decals"), buffer);
		Shader.SetGlobalInteger(DecalCount, array.Length);
		_isDirty = false;
	}
}

public struct Decal
{
	public Matrix4x4 ProjectorMatrix;
	public Matrix4x4 BoxMatrix;
	public Vector4 ProjectorParams;
}