using System;
using System.Collections.Generic;
using Sirenix.Serialization;
using UnityEngine;

public abstract class AFSM<TState> : MonoBehaviour where TState : IFSMState
{
	public event Action<Type> OnStateEnter;
	public event Action<Type> OnStateExit;
	
	[SerializeField]
	protected List<TState> m_availableStates;

	protected TState m_currentState = default(TState);
	public TState CurrentState => m_currentState;


	private Dictionary<Type, TState> m_typeToStateDictionary = new Dictionary<Type, TState>();

	protected virtual void Awake()
	{
		foreach(TState state in m_availableStates)
		{
			m_typeToStateDictionary[state.GetType()] = state; 
			state.OnInitialise();
		}
	}

	private void Update()
	{
		if(m_currentState != null)
		{
			m_currentState.OnUpdate();
		}
	}


	public void ChangeState<T>() where T : TState
	{
		ChangeState(typeof(T));
	}

	public void ChangeState(Type type)
	{
		if (m_typeToStateDictionary.TryGetValue(type, out TState state))
		{
			ChangeState(state);
		}
		else
		{
			Debug.LogError("Could not change state! Make sure it is added to the list of available states! SessionState Type: " + typeof(TState));
		}
	}

	public void ChangeState(TState a_fsmState)
	{
		if (m_currentState != null)
		{
			m_currentState.OnExit();
			OnStateExit?.Invoke(m_currentState.GetType());
		}

		m_currentState = a_fsmState;

		m_currentState.OnEnter();
		OnStateEnter?.Invoke(m_currentState.GetType());
	}
}
