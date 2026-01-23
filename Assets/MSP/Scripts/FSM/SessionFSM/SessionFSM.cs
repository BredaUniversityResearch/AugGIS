using UnityEditor;
using UnityEngine;

public class SessionFSM : AFSM<ASessionState>
{
	protected override void Awake()
	{
		foreach (ASessionState sessionState in m_availableStates)
		{
			sessionState.SessionFSM = this;
		}

		base.Awake();
	}
}
