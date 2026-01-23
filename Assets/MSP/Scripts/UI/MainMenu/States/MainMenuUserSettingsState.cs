using Sirenix.OdinInspector;
using UnityEngine;

public class MainMenuUserSettingsState : AMainMenuState
{
	[SerializeField]
	[Required]
	private NetworkSessionConnectionData m_networkSessionConnectionData;
	
	private UIMainMenu m_mainMenu;

	public override void OnInitialise()
	{
		base.OnInitialise();
		m_mainMenu = MainMenuFSM.GetComponent<UIMainMenu>();
	}

	public override void OnEnter()
	{
		base.OnEnter();

		m_mainMenu.EnableTraversalButtons(
			() => { MainMenuFSM.ChangeState<MainMenuConfirmationState>(); },
			() =>
			{
				if (m_networkSessionConnectionData.isServer)
				{
					MainMenuFSM.ChangeState<MainMenuInitialState>();
				}
				else
				{
					MainMenuFSM.ChangeState<MainMenuScanServersState>();
				}
			});
	}

	public override void OnExit()
	{
		base.OnExit();
		m_mainMenu.DisableTraversalButtons();
	}
}
