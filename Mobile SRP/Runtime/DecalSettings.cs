using UnityEngine;

[System.Serializable]
public class DecalSettings
{
	public bool useDecals = true;
	public Texture decalAtlas;
	public Vector2 atlasDimensions = new(1, 1);
}