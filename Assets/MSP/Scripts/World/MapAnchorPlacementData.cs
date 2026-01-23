using UnityEngine;
using System;

[CreateAssetMenu(fileName = "AnchorData", menuName = "MSP/AnchorData")]
public class MapAnchorPlacementData : ScriptableObject
{
	private Action<Vector3> AnchorPlacementPositionChanged;
	private Vector3 m_anchorPosition;
	public Vector3 AnchorPosition => m_anchorPosition;

	public void SetAnchorPlacementPosition(Vector3 a_newPosition)
	{
		m_anchorPosition = a_newPosition;
		AnchorPlacementPositionChanged?.Invoke(a_newPosition);
	} 
}
