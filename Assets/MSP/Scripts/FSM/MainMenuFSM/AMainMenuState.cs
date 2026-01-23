using UnityEngine;

public abstract class AMainMenuState : MonoBehaviour, IFSMState
{
	public MainMenuFSM MainMenuFSM
	{
		get;
		set;
	}

	[SerializeField]
	private Transform menuRootTransform;

	public virtual void OnInitialise()
	{

	}

	public virtual void OnEnter()
	{
		menuRootTransform.gameObject.SetActive(true);
	}

	public virtual void OnUpdate()
	{

	}

	public virtual void OnExit()
	{
		menuRootTransform.gameObject.SetActive(false);
	}
}
