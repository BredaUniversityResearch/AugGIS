using UnityEngine;

[CreateAssetMenu(fileName = "TransformReference", menuName = "MSP/TransformReference")]
public class TransformReference : ScriptableObject
{
	private Transform m_transform;
	public Transform TransformRef => m_transform;

	public void SetTransformReference(Transform a_transformRef)
	{
		m_transform = a_transformRef;
	}
}
