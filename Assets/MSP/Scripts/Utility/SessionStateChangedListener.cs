using System;
using UnityEngine;
using UnityEngine.Events;

public class SessionStateChangedListener : MonoBehaviour
{
	[SerializeField]
	[SessionStateDropDown]
	private SerializableTypeData m_sessionState;

	public UnityEvent OnEnter;
	public UnityEvent OnExit;

	private void Start()
	{
		SessionManager sessionManager = SessionManager.Instance;

		if(sessionManager == null)
		{
			return;
		}

		sessionManager.SessionFSM.OnStateEnter += OnStateEntered;
		sessionManager.SessionFSM.OnStateExit += OnStateExit;
	}

    void OnDestroy()
    {
		SessionManager sessionManager = SessionManager.Instance;
        sessionManager.SessionFSM.OnStateEnter -= OnStateEntered;
		sessionManager.SessionFSM.OnStateExit -= OnStateExit;
    }

    private void OnStateEntered(Type type)
	{
		if (type == m_sessionState.Type)
		{
			OnEnter.Invoke();
		}
	}

	private void OnStateExit(Type type)
	{
		if(type == m_sessionState.Type)
		{
			OnExit.Invoke();
		}
	}
}
