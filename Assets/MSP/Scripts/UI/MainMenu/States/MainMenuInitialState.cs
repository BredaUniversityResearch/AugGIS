using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.UI;

public class MainMenuInitialState : AMainMenuState
{
	[SerializeField]
	[Required]
	private NetworkSessionConnectionData m_networkSessionConnectionData;

	[SerializeField]
	[Required]
	private CustomXRButton m_joinButton;

	[SerializeField]
	[Required]
	private CustomXRButton m_hostButton;

	[SerializeField]
	[Required]
	private CustomXRButton m_quitButton;

	public override void OnInitialise()
	{
		base.OnInitialise();
		m_quitButton.OnPress.AddListener(OnQuitButtonPressed);
		m_joinButton.OnPress.AddListener(OnJoinButtonClicked);
		m_hostButton.OnPress.AddListener(OnHostButtonClicked);
	}

	private void OnHostButtonClicked()
	{
		m_networkSessionConnectionData.isServer = true;
		m_networkSessionConnectionData.ip = SessionManager.Instance.GetLocalIPAdress();
		
		MainMenuFSM.ChangeState<MainMenuUserSettingsState>();
	}

	private void OnJoinButtonClicked()
	{
		MainMenuFSM.ChangeState<MainMenuScanServersState>();
	}

	private void OnQuitButtonPressed()
	{
		Application.Quit();
	}
}
