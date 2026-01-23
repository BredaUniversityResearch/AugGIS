using System;
using UnityEngine;

public class ToggleGameObjectsBasedOnSessionState : MonoBehaviour
{
	[SessionStateDropDown]
	[SerializeField]
	private SerializableTypeData m_targetSessionState;

	[SerializeField]
	private GameObject[] m_gameObjectsToToggle;

	void Start()
	{
		SessionManager.Instance.SessionFSM.OnStateEnter += OnStateEntered;
		SessionManager.Instance.SessionFSM.OnStateExit += OnStateExit;

		ToggleGameObjects(SessionManager.Instance.SessionFSM.CurrentState.GetType() == m_targetSessionState.GetType());
	}


	void OnDestroy()
	{
		SessionManager.Instance.SessionFSM.OnStateEnter -= OnStateEntered;
		SessionManager.Instance.SessionFSM.OnStateExit -= OnStateExit;
	}

	private void OnStateEntered(Type type)
	{
		if(type == m_targetSessionState.Type)
		{
			ToggleGameObjects(true);
		}
	}

	private void OnStateExit(Type type)
	{
		if(type == m_targetSessionState.Type)
		{
			ToggleGameObjects(false);
		}
	}

	private void ToggleGameObjects(bool activeState)
	{
		foreach(GameObject gameObject in m_gameObjectsToToggle)
		{
			gameObject.SetActive(activeState);
		}
	}
}
