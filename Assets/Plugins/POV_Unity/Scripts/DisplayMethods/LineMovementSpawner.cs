using Sirenix.OdinInspector;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace POV_Unity
{
	public class LineMovementSpawner : SerializedMonoBehaviour
	{
		[SerializeField, HideInInspector] VectorLayer m_layer;
		[SerializeField, HideInInspector] VectorObject m_object;
		[SerializeField, HideInInspector] DMLineModelMovement m_displayMethod;

		float m_timePassed;
		float m_nextSpawnTime;

		public void Initialise(VectorLayer a_layer, VectorObject a_object, DMLineModelMovement a_displayMethod)
		{
			m_layer = a_layer;
			m_object = a_object;
			m_displayMethod = a_displayMethod;
		}


		private void Update()
		{
			//Periodically spawn MovingObject+ModelObject objects
			m_timePassed += Time.deltaTime;
			if(m_timePassed > m_nextSpawnTime)
			{
				m_timePassed = 0f;
				m_nextSpawnTime = UnityEngine.Random.Range(m_displayMethod.spawn_interval_min, m_displayMethod.spawn_interval_max);
				SpawnObject();
			}
		}

		void SpawnObject()
		{
			GameObject go = new GameObject("MovingLineModelObject");
			go.transform.SetParent(transform, false);
			go.AddComponent<ModelObject>().Initialise(m_layer, m_object, m_displayMethod);
			go.AddComponent<MovingObject>().Initialise(m_layer, m_object, m_displayMethod);
		}
	}
}
