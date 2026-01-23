using UnityEngine;

public class SetTransformReference : MonoBehaviour
{
	[SerializeField]
	private TransformReference m_transformReference;
	
	[SerializeField]
	private Transform m_targetTransform;
	
	void Awake()
	{
		m_transformReference.SetTransformReference(m_targetTransform);
	}
}
