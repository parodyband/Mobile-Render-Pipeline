using System.Collections.Generic;
using UnityEngine;

public class BoundsOctreeNode<T>
{
	// Centre of this node
	public Vector3 Center { get; private set; }

	// Length of this node if it has a looseness of 1.0
	public float BaseLength { get; private set; }

	// Looseness value for this node
	private float m_Looseness;

	// Minimum size for a node in this octree
	private float m_MinSize;

	// Actual length of sides, taking the looseness value into account
	private float m_AdjLength;

	// Bounding box that represents this node
	private Bounds m_Bounds;

	// Objects in this node
	private readonly List<OctreeObject> m_Objects = new();

	// Child nodes, if any
	private BoundsOctreeNode<T>[] m_Children;

	private bool HasChildren => m_Children != null;

	// Bounds of potential children to this node. These are actual size (with looseness taken into account), not base size
	private Bounds[] m_ChildBounds;

	// If there are already NUM_OBJECTS_ALLOWED in a node, we split it into children
	// A generally good number seems to be something around 8-15
	const int NUM_OBJECTS_ALLOWED = 8;

	// An object in the octree
	private struct OctreeObject
	{
		public T Obj;
		public Bounds Bounds;
	}

	/// <summary>
	/// Constructor.
	/// </summary>
	/// <param name="baseLengthVal">Length of this node, not taking looseness into account.</param>
	/// <param name="minSizeVal">Minimum size of nodes in this octree.</param>
	/// <param name="loosenessVal">Multiplier for baseLengthVal to get the actual size.</param>
	/// <param name="centerVal">Centre position of this node.</param>
	public BoundsOctreeNode(float baseLengthVal, float minSizeVal, float loosenessVal, Vector3 centerVal)
	{
		SetValues(baseLengthVal, minSizeVal, loosenessVal, centerVal);
	}

	// #### PUBLIC METHODS ####

	/// <summary>
	/// Add an object.
	/// </summary>
	/// <param name="obj">Object to add.</param>
	/// <param name="objBounds">3D bounding box around the object.</param>
	/// <returns>True if the object fits entirely within this node.</returns>
	public bool Add(T obj, Bounds objBounds)
	{
		if (!Encapsulates(m_Bounds, objBounds))
		{
			return false;
		}

		SubAdd(obj, objBounds);
		return true;
	}

	/// <summary>
	/// Remove an object. Makes the assumption that the object only exists once in the tree.
	/// </summary>
	/// <param name="obj">Object to remove.</param>
	/// <returns>True if the object was removed successfully.</returns>
	public bool Remove(T obj)
	{
		var removed = false;

		for (var i = 0; i < m_Objects.Count; i++)
		{
			if (!m_Objects[i].Obj.Equals(obj)) continue;
			removed = m_Objects.Remove(m_Objects[i]);
			break;
		}

		if (!removed && m_Children != null)
		{
			for (var i = 0; i < 8; i++)
			{
				removed = m_Children[i].Remove(obj);
				if (removed) break;
			}
		}

		if (!removed || m_Children == null) return removed;
		// Check if we should merge nodes now that we've removed an item
		if (ShouldMerge())
		{
			Merge();
		}

		return removed;
	}

	/// <summary>
	/// Removes the specified object at the given position. Makes the assumption that the object only exists once in the tree.
	/// </summary>
	/// <param name="obj">Object to remove.</param>
	/// <param name="objBounds">3D bounding box around the object.</param>
	/// <returns>True if the object was removed successfully.</returns>
	public bool Remove(T obj, Bounds objBounds)
	{
		return Encapsulates(m_Bounds, objBounds) && SubRemove(obj, objBounds);
	}

	/// <summary>
	/// Check if the specified bounds intersect with anything in the tree. See also: GetColliding.
	/// </summary>
	/// <param name="checkBounds">Bounds to check.</param>
	/// <returns>True if there was a collision.</returns>
	public bool IsColliding(ref Bounds checkBounds)
	{
		// Are the input bounds at least partially in this node?
		if (!m_Bounds.Intersects(checkBounds))
		{
			return false;
		}

		// Check against any objects in this node
		for (var i = 0; i < m_Objects.Count; i++)
		{
			if (m_Objects[i].Bounds.Intersects(checkBounds))
			{
				return true;
			}
		}

		// Check children
		if (m_Children == null) return false;
		{
			for (var i = 0; i < 8; i++)
			{
				if (m_Children[i].IsColliding(ref checkBounds))
				{
					return true;
				}
			}
		}

		return false;
	}

	/// <summary>
	/// Check if the specified ray intersects with anything in the tree. See also: GetColliding.
	/// </summary>
	/// <param name="checkRay">Ray to check.</param>
	/// <param name="maxDistance">Distance to check.</param>
	/// <returns>True if there was a collision.</returns>
	public bool IsColliding(ref Ray checkRay, float maxDistance = float.PositiveInfinity)
	{
		// Is the input ray at least partially in this node?
		float distance;
		if (!m_Bounds.IntersectRay(checkRay, out distance) || distance > maxDistance)
		{
			return false;
		}

		// Check against any objects in this node
		for (var i = 0; i < m_Objects.Count; i++)
		{
			if (m_Objects[i].Bounds.IntersectRay(checkRay, out distance) && distance <= maxDistance)
			{
				return true;
			}
		}

		// Check children
		if (m_Children == null) return false;
		{
			for (var i = 0; i < 8; i++)
			{
				if (m_Children[i].IsColliding(ref checkRay, maxDistance))
				{
					return true;
				}
			}
		}

		return false;
	}

	/// <summary>
	/// Returns an array of objects that intersect with the specified bounds, if any. Otherwise returns an empty array. See also: IsColliding.
	/// </summary>
	/// <param name="checkBounds">Bounds to check. Passing by ref as it improves performance with structs.</param>
	/// <param name="result">List result.</param>
	/// <returns>Objects that intersect with the specified bounds.</returns>
	public void GetColliding(ref Bounds checkBounds, List<T> result)
	{
		// Are the input bounds at least partially in this node?
		if (!m_Bounds.Intersects(checkBounds))
		{
			return;
		}

		// Check against any objects in this node
		for (var i = 0; i < m_Objects.Count; i++)
		{
			if (m_Objects[i].Bounds.Intersects(checkBounds))
			{
				result.Add(m_Objects[i].Obj);
			}
		}

		// Check children
		if (m_Children == null) return;
		{
			for (var i = 0; i < 8; i++)
			{
				m_Children[i].GetColliding(ref checkBounds, result);
			}
		}
	}

	/// <summary>
	/// Returns an array of objects that intersect with the specified ray, if any. Otherwise returns an empty array. See also: IsColliding.
	/// </summary>
	/// <param name="checkRay">Ray to check. Passing by ref as it improves performance with structs.</param>
	/// <param name="maxDistance">Distance to check.</param>
	/// <param name="result">List result.</param>
	/// <returns>Objects that intersect with the specified ray.</returns>
	public void GetColliding(ref Ray checkRay, List<T> result, float maxDistance = float.PositiveInfinity)
	{
		// Is the input ray at least partially in this node?
		if (!m_Bounds.IntersectRay(checkRay, out var distance) || distance > maxDistance)
		{
			return;
		}

		// Check against any objects in this node
		for (var i = 0; i < m_Objects.Count; i++)
		{
			if (m_Objects[i].Bounds.IntersectRay(checkRay, out distance) && distance <= maxDistance)
			{
				result.Add(m_Objects[i].Obj);
			}
		}

		// Check children
		if (m_Children == null) return;
		{
			for (var i = 0; i < 8; i++)
			{
				m_Children[i].GetColliding(ref checkRay, result, maxDistance);
			}
		}
	}

	public void GetWithinFrustum(Plane[] planes, List<T> result)
	{
		// Is the input node inside the frustum?
		if (!GeometryUtility.TestPlanesAABB(planes, m_Bounds))
		{
			return;
		}

		// Check against any objects in this node
		for (var i = 0; i < m_Objects.Count; i++)
		{
			if (GeometryUtility.TestPlanesAABB(planes, m_Objects[i].Bounds))
			{
				result.Add(m_Objects[i].Obj);
			}
		}

		// Check children
		if (m_Children == null) return;
		{
			for (var i = 0; i < 8; i++)
			{
				m_Children[i].GetWithinFrustum(planes, result);
			}
		}
	}

	/// <summary>
	/// Set the 8 children of this octree.
	/// </summary>
	/// <param name="childOctrees">The 8 new child nodes.</param>
	public void SetChildren(BoundsOctreeNode<T>[] childOctrees)
	{
		if (childOctrees.Length != 8)
		{
			Debug.LogError("Child octree array must be length 8. Was length: " + childOctrees.Length);
			return;
		}

		m_Children = childOctrees;
	}

	public Bounds GetBounds()
	{
		return m_Bounds;
	}

	/// <summary>
	/// Draws node boundaries visually for debugging.
	/// Must be called from OnDrawGizmos externally. See also: DrawAllObjects.
	/// </summary>
	/// <param name="depth">Used for recurcive calls to this method.</param>
	public void DrawAllBounds(float depth = 0)
	{
		var tintVal = depth / 7; // Will eventually get values > 1. Color rounds to 1 automatically
		Gizmos.color = new Color(tintVal, 0, 1.0f - tintVal);

		var thisBounds = new Bounds(Center, new Vector3(m_AdjLength, m_AdjLength, m_AdjLength));
		Gizmos.DrawWireCube(thisBounds.center, thisBounds.size);

		if (m_Children != null)
		{
			depth++;
			for (var i = 0; i < 8; i++)
			{
				m_Children[i].DrawAllBounds(depth);
			}
		}

		Gizmos.color = Color.white;
	}

	/// <summary>
	/// Draws the bounds of all objects in the tree visually for debugging.
	/// Must be called from OnDrawGizmos externally. See also: DrawAllBounds.
	/// </summary>
	public void DrawAllObjects()
	{
		var tintVal = BaseLength / 20;
		Gizmos.color = new Color(0, 1.0f - tintVal, tintVal, 0.25f);

		foreach (OctreeObject obj in m_Objects)
		{
			Gizmos.DrawCube(obj.Bounds.center, obj.Bounds.size);
		}

		if (m_Children != null)
		{
			for (var i = 0; i < 8; i++)
			{
				m_Children[i].DrawAllObjects();
			}
		}

		Gizmos.color = Color.white;
	}

	/// <summary>
	/// We can shrink the octree if:
	/// - This node is >= double minLength in length
	/// - All objects in the root node are within one octant
	/// - This node doesn't have children, or does but 7/8 children are empty
	/// We can also shrink it if there are no objects left at all!
	/// </summary>
	/// <param name="minLength">Minimum dimensions of a node in this octree.</param>
	/// <returns>The new root, or the existing one if we didn't shrink.</returns>
	public BoundsOctreeNode<T> ShrinkIfPossible(float minLength)
	{
		if (BaseLength < (2 * minLength))
		{
			return this;
		}

		if (m_Objects.Count == 0 && (m_Children == null || m_Children.Length == 0))
		{
			return this;
		}

		// Check objects in root
		var bestFit = -1;
		for (var i = 0; i < m_Objects.Count; i++)
		{
			OctreeObject curObj = m_Objects[i];
			var newBestFit = BestFitChild(curObj.Bounds.center);
			if (i == 0 || newBestFit == bestFit)
			{
				// In same octant as the other(s). Does it fit completely inside that octant?
				if (Encapsulates(m_ChildBounds[newBestFit], curObj.Bounds))
				{
					if (bestFit < 0)
					{
						bestFit = newBestFit;
					}
				}
				else
				{
					// Nope, so we can't reduce. Otherwise we continue
					return this;
				}
			}
			else
			{
				return this; // Can't reduce - objects fit in different octants
			}
		}

		// Check objects in children if there are any
		if (m_Children != null)
		{
			var childHadContent = false;
			for (var i = 0; i < m_Children.Length; i++)
			{
				if (!m_Children[i].HasAnyObjects()) continue;
				if (childHadContent)
				{
					return this; // Can't shrink - another child had content already
				}

				if (bestFit >= 0 && bestFit != i)
				{
					return this; // Can't reduce - objects in root are in a different octant to objects in child
				}

				childHadContent = true;
				bestFit = i;
			}
		}

		// Can reduce
		if (m_Children != null)
			return bestFit == -1
				? this
				:
				// We have children. Use the appropriate child as the new root node
				m_Children[bestFit];
		// We don't have any children, so just shrink this node to the new size
		// We already know that everything will still fit in it
		SetValues(BaseLength / 2, m_MinSize, m_Looseness, m_ChildBounds[bestFit].center);
		return this;

		// No objects in entire octree
	}

	/// <summary>
	/// Find which child node this object would be most likely to fit in.
	/// </summary>
	/// <param name="objBounds">The object's bounds.</param>
	/// <returns>One of the eight child octants.</returns>
	public int BestFitChild(Vector3 objBoundsCenter)
	{
		return (objBoundsCenter.x <= Center.x ? 0 : 1) + (objBoundsCenter.y >= Center.y ? 0 : 4) +
		       (objBoundsCenter.z <= Center.z ? 0 : 2);
	}

	/// <summary>
	/// Checks if this node or anything below it has something in it.
	/// </summary>
	/// <returns>True if this node or any of its children, grandchildren etc have something in them</returns>
	public bool HasAnyObjects()
	{
		if (m_Objects.Count > 0) return true;

		if (m_Children == null) return false;
		for (var i = 0; i < 8; i++)
		{
			if (m_Children[i].HasAnyObjects()) return true;
		}

		return false;
	}

	/*
	/// <summary>
	/// Get the total amount of objects in this node and all its children, grandchildren etc. Useful for debugging.
	/// </summary>
	/// <param name="startingNum">Used by recursive calls to add to the previous total.</param>
	/// <returns>Total objects in this node and its children, grandchildren etc.</returns>
	public int GetTotalObjects(int startingNum = 0) {
		int totalObjects = startingNum + objects.Count;
		if (children != null) {
			for (int i = 0; i < 8; i++) {
				totalObjects += children[i].GetTotalObjects();
			}
		}
		return totalObjects;
	}
	*/

	// #### PRIVATE METHODS ####

	/// <summary>
	/// Set values for this node. 
	/// </summary>
	/// <param name="baseLengthVal">Length of this node, not taking looseness into account.</param>
	/// <param name="minSizeVal">Minimum size of nodes in this octree.</param>
	/// <param name="loosenessVal">Multiplier for baseLengthVal to get the actual size.</param>
	/// <param name="centerVal">Centre position of this node.</param>
	public void SetValues(float baseLengthVal, float minSizeVal, float loosenessVal, Vector3 centerVal)
	{
		BaseLength = baseLengthVal;
		m_MinSize = minSizeVal;
		m_Looseness = loosenessVal;
		Center = centerVal;
		m_AdjLength = m_Looseness * baseLengthVal;

		// Create the bounding box.
		var size = new Vector3(m_AdjLength, m_AdjLength, m_AdjLength);
		m_Bounds = new Bounds(Center, size);

		var quarter = BaseLength / 4f;
		var childActualLength = (BaseLength / 2) * m_Looseness;
		var childActualSize = new Vector3(childActualLength, childActualLength, childActualLength);
		m_ChildBounds = new Bounds[8];
		m_ChildBounds[0] = new Bounds(Center + new Vector3(-quarter, quarter, -quarter), childActualSize);
		m_ChildBounds[1] = new Bounds(Center + new Vector3(quarter, quarter, -quarter), childActualSize);
		m_ChildBounds[2] = new Bounds(Center + new Vector3(-quarter, quarter, quarter), childActualSize);
		m_ChildBounds[3] = new Bounds(Center + new Vector3(quarter, quarter, quarter), childActualSize);
		m_ChildBounds[4] = new Bounds(Center + new Vector3(-quarter, -quarter, -quarter), childActualSize);
		m_ChildBounds[5] = new Bounds(Center + new Vector3(quarter, -quarter, -quarter), childActualSize);
		m_ChildBounds[6] = new Bounds(Center + new Vector3(-quarter, -quarter, quarter), childActualSize);
		m_ChildBounds[7] = new Bounds(Center + new Vector3(quarter, -quarter, quarter), childActualSize);
	}

	/// <summary>
	/// Private counterpart to the public Add method.
	/// </summary>
	/// <param name="obj">Object to add.</param>
	/// <param name="objBounds">3D bounding box around the object.</param>
	private void SubAdd(T obj, Bounds objBounds)
	{
		// We know it fits at this level if we've got this far

		// We always put things in the deepest possible child
		// So we can skip some checks if there are children aleady
		if (!HasChildren)
		{
			// Just add if few objects are here, or children would be below min size
			if (m_Objects.Count < NUM_OBJECTS_ALLOWED || (BaseLength / 2) < m_MinSize)
			{
				var newObj = new OctreeObject { Obj = obj, Bounds = objBounds };
				m_Objects.Add(newObj);
				return; // We're done. No children yet
			}

			// Fits at this level, but we can go deeper. Would it fit there?
			// Create the 8 children
			if (m_Children == null)
			{
				Split();
				if (m_Children == null)
				{
					Debug.LogError("Child creation failed for an unknown reason. Early exit.");
					return;
				}

				// Now that we have the new children, see if this node's existing objects would fit there
				for (var i = m_Objects.Count - 1; i >= 0; i--)
				{
					OctreeObject existingObj = m_Objects[i];
					// Find which child the object is closest to based on where the
					// object's center is located in relation to the octree's center
					var bestFitChild = BestFitChild(existingObj.Bounds.center);
					// Does it fit?
					if (!Encapsulates(m_Children[bestFitChild].m_Bounds, existingObj.Bounds)) continue;
					m_Children[bestFitChild].SubAdd(existingObj.Obj, existingObj.Bounds); // Go a level deeper					
					m_Objects.Remove(existingObj); // Remove from here
				}
			}
		}

		// Handle the new object we're adding now
		var bestFit = BestFitChild(objBounds.center);
		if (Encapsulates(m_Children[bestFit].m_Bounds, objBounds))
		{
			m_Children[bestFit].SubAdd(obj, objBounds);
		}
		else
		{
			// Didn't fit in a child. We'll have to it to this node instead
			var newObj = new OctreeObject { Obj = obj, Bounds = objBounds };
			m_Objects.Add(newObj);
		}
	}

	/// <summary>
	/// Private counterpart to the public <see cref="Remove(T, Bounds)"/> method.
	/// </summary>
	/// <param name="obj">Object to remove.</param>
	/// <param name="objBounds">3D bounding box around the object.</param>
	/// <returns>True if the object was removed successfully.</returns>
	private bool SubRemove(T obj, Bounds objBounds)
	{
		var removed = false;

		for (var i = 0; i < m_Objects.Count; i++)
		{
			if (!m_Objects[i].Obj.Equals(obj)) continue;
			removed = m_Objects.Remove(m_Objects[i]);
			break;
		}

		if (!removed && m_Children != null)
		{
			var bestFitChild = BestFitChild(objBounds.center);
			removed = m_Children[bestFitChild].SubRemove(obj, objBounds);
		}

		if (!removed || m_Children == null) return removed;
		// Check if we should merge nodes now that we've removed an item
		if (ShouldMerge())
		{
			Merge();
		}

		return removed;
	}

	/// <summary>
	/// Splits the octree into eight children.
	/// </summary>
	private void Split()
	{
		var quarter = BaseLength / 4f;
		var newLength = BaseLength / 2;
		m_Children = new BoundsOctreeNode<T>[8];
		m_Children[0] = new BoundsOctreeNode<T>(newLength, m_MinSize, m_Looseness,
			Center + new Vector3(-quarter, quarter, -quarter));
		m_Children[1] = new BoundsOctreeNode<T>(newLength, m_MinSize, m_Looseness,
			Center + new Vector3(quarter, quarter, -quarter));
		m_Children[2] = new BoundsOctreeNode<T>(newLength, m_MinSize, m_Looseness,
			Center + new Vector3(-quarter, quarter, quarter));
		m_Children[3] = new BoundsOctreeNode<T>(newLength, m_MinSize, m_Looseness,
			Center + new Vector3(quarter, quarter, quarter));
		m_Children[4] = new BoundsOctreeNode<T>(newLength, m_MinSize, m_Looseness,
			Center + new Vector3(-quarter, -quarter, -quarter));
		m_Children[5] = new BoundsOctreeNode<T>(newLength, m_MinSize, m_Looseness,
			Center + new Vector3(quarter, -quarter, -quarter));
		m_Children[6] = new BoundsOctreeNode<T>(newLength, m_MinSize, m_Looseness,
			Center + new Vector3(-quarter, -quarter, quarter));
		m_Children[7] = new BoundsOctreeNode<T>(newLength, m_MinSize, m_Looseness,
			Center + new Vector3(quarter, -quarter, quarter));
	}

	/// <summary>
	/// Merge all children into this node - the opposite of Split.
	/// Note: We only have to check one level down since a merge will never happen if the children already have children,
	/// since THAT won't happen unless there are already too many objects to merge.
	/// </summary>
	private void Merge()
	{
		// Note: We know children != null or we wouldn't be merging
		for (var i = 0; i < 8; i++)
		{
			var curChild = m_Children[i];
			var numObjects = curChild.m_Objects.Count;
			for (var j = numObjects - 1; j >= 0; j--)
			{
				OctreeObject curObj = curChild.m_Objects[j];
				m_Objects.Add(curObj);
			}
		}

		// Remove the child nodes (and the objects in them - they've been added elsewhere now)
		m_Children = null;
	}

	/// <summary>
	/// Checks if outerBounds encapsulates innerBounds.
	/// </summary>
	/// <param name="outerBounds">Outer bounds.</param>
	/// <param name="innerBounds">Inner bounds.</param>
	/// <returns>True if innerBounds is fully encapsulated by outerBounds.</returns>
	private static bool Encapsulates(Bounds outerBounds, Bounds innerBounds)
	{
		return outerBounds.Contains(innerBounds.min) && outerBounds.Contains(innerBounds.max);
	}

	/// <summary>
	/// Checks if there are few enough objects in this node and its children that the children should all be merged into this.
	/// </summary>
	/// <returns>True there are less or the same abount of objects in this and its children than numObjectsAllowed.</returns>
	private bool ShouldMerge()
	{
		var totalObjects = m_Objects.Count;
		if (m_Children == null) return totalObjects <= NUM_OBJECTS_ALLOWED;
		foreach (var child in m_Children)
		{
			if (child.m_Children != null)
			{
				// If any of the *children* have children, there are definitely too many to merge,
				// or the child woudl have been merged already
				return false;
			}

			totalObjects += child.m_Objects.Count;
		}

		return totalObjects <= NUM_OBJECTS_ALLOWED;
	}
}