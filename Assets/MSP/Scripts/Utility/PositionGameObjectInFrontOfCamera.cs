using UnityEngine;

public class PositionGameObjectInFrontOfCamera : MonoBehaviour
{
	[SerializeField]
	private float m_forwardOffsetFromCamera = 0.5f;

	[SerializeField]
	private bool m_rotateTowardsCamera = true;

	public void PositionInFrontOfMainCamera(GameObject a_gameObject)
	{
		PositionInfFrontOfCamera(a_gameObject,Camera.main);
	}

	public void PositionInfFrontOfCamera(GameObject a_gameObject,Camera a_camera)
	{
		a_gameObject.transform.position = a_camera.transform.position + (a_camera.transform.forward * m_forwardOffsetFromCamera);

		if(m_rotateTowardsCamera)
		{
			Vector3 facingDirection = (a_gameObject.transform.position - a_camera.transform.position).normalized;
			a_gameObject.transform.rotation = Quaternion.LookRotation(facingDirection,Vector3.up);
		}
	}
}
