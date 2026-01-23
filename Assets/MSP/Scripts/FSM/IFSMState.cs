using UnityEngine;

public interface IFSMState
{
	public void OnInitialise();

	public void OnEnter();
	public void OnUpdate();
	public void OnExit();
}
