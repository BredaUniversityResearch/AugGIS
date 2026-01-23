using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace POV_Unity
{
	public class FollowGameObject : MonoBehaviour
	{
		[SerializeField] GameObject m_objectToFollow;
		[SerializeField] float m_maxDriftBeforeSnap = 1f;

		private void Start()
		{
			m_maxDriftBeforeSnap = m_maxDriftBeforeSnap * m_maxDriftBeforeSnap;
		}

		public void SetTarget(GameObject a_object)
		{
			m_objectToFollow = a_object;
			transform.position = m_objectToFollow.transform.position;
			transform.rotation = m_objectToFollow.transform.rotation;
		}

		private void Update()
		{
			if(m_objectToFollow == null)
			{
				Debug.Log("Followed object has been destroyed, following canceled");
				Destroy(this);
				return;
			}
			if(Vector3.SqrMagnitude(transform.position - m_objectToFollow.transform.position) > m_maxDriftBeforeSnap)
			{
				transform.position = m_objectToFollow.transform.position;
			}
		}
	}
}
