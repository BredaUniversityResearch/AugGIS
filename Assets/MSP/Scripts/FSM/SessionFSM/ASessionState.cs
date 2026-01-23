using Sirenix.OdinInspector;
using UnityEngine;

public abstract class ASessionState : MonoBehaviour, IFSMState
{
	public SessionFSM SessionFSM
	{
		get;
		set;
	}

	public void OnInitialise()
	{
		
	}

	public virtual void OnEnter()
	{

	}

	public virtual void OnUpdate()
	{

	}

	public virtual void OnExit()
	{

	}
}
