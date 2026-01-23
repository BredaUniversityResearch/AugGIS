using UnityEngine;

public class MainMenuFSM : AFSM<AMainMenuState>
{
	protected override void Awake()
	{
		foreach(AMainMenuState mainMenuState in m_availableStates)
		{
			mainMenuState.MainMenuFSM = this;
		}

		base.Awake();
	}
}
