using System;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;

namespace POV_Unity
{
	public class MovingObject : MonoBehaviour
	{
		List<Vector3> m_path;
		bool m_forward;
		int m_index;
		float m_timeAlive;
		DMLineModelMovement m_displayMethod;

		public void Initialise(VectorLayer a_layer, VectorObject a_object, DMLineModelMovement a_displayMethod)
		{
			//Convert float[] format to Vector3
			m_displayMethod = a_displayMethod;
			m_path = a_object.Shapes[0].m_points;

			if (!a_displayMethod.direction.HasValue)
			{
				m_forward = UnityEngine.Random.value > 0.5f;
			}
			else
				m_forward = a_displayMethod.direction.Value;

			m_index = m_forward ? 0 : m_path.Count - 1;
			transform.localPosition = m_path[m_index];
			float size = a_displayMethod.GetVariable<float>("size", a_layer, a_object) * ImportedConfigRoot.Instance.ConfigToWorldScale;
			transform.localScale = new Vector3(size, size, size);
		}

		private void Update()
		{
			float distToMove = Time.deltaTime * m_displayMethod.speed * ImportedConfigRoot.Instance.ConfigToWorldScale;
			m_timeAlive += Time.deltaTime;
			if(m_displayMethod.lifetime > 0f && m_displayMethod.lifetime < m_timeAlive)
			{
				Destroy(gameObject);
				return;
			}

			while (true)
			{
				int nextIndex = m_forward ? Math.Min(m_index + 1, m_path.Count - 1) : Math.Max(m_index - 1, 0);
				float distToNext = Vector3.Distance(m_path[nextIndex], transform.localPosition);
				if (distToNext <= distToMove)
				{
					if ((m_forward && nextIndex == m_path.Count - 1) || (!m_forward && nextIndex == 0))
					{
						Destroy(gameObject);
						return;
					}

					m_index = nextIndex;
					distToMove -= distToNext;
					transform.localPosition = m_path[m_index];
				}
				else
				{
					Vector3 direction = (m_path[nextIndex] - m_path[m_index]).normalized;
					transform.localPosition += direction * distToMove;
					transform.localRotation = Quaternion.LookRotation(direction, Vector3.up);
					break;
				}
			}
		}
	}
}
