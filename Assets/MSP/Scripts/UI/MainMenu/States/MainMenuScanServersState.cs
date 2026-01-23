using System;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.UI;

public class MainMenuScanServersState : AMainMenuState
{
	private UIMainMenu m_mainMenu;

	[SerializeField]
	[Required]
	private UIScanLocalServersMenu m_scanLocalServersMenu;

	[SerializeField]
	[Required]
	private CustomXRButton m_ipSettingsButton;

	[SerializeField]
	[Required]
	private CustomXRButton m_qrCodeButton;

	public override void OnInitialise()
	{
		base.OnInitialise();
		m_mainMenu = MainMenuFSM.GetComponent<UIMainMenu>();

		m_ipSettingsButton.OnPress.AddListener(OnIpSettingsButtonClicked);

#if USE_OPEN_CV
		m_qrCodeButton.OnPress.AddListener(OnQrCodeButtonClicked);
#else	
		m_qrCodeButton.gameObject.SetActive(false);
#endif
	}

	public override void OnEnter()
	{
		base.OnEnter();
		m_mainMenu.EnableTraversalButtons(() => { MainMenuFSM.ChangeState<MainMenuUserSettingsState>(); }, () => { MainMenuFSM.ChangeState<MainMenuInitialState>(); });
	}

	public override void OnUpdate()
	{
		base.OnUpdate();
		m_mainMenu.SetContinueButtonInteratableState(m_scanLocalServersMenu.AnyServersSelected);
	}

	public override void OnExit()
	{
		base.OnExit();
		m_mainMenu.DisableTraversalButtons();
	}

	private void OnIpSettingsButtonClicked()
	{
		MainMenuFSM.ChangeState<MainMenuIpSettingsState>();
	}

	private void OnQrCodeButtonClicked()
	{
		MainMenuFSM.ChangeState<MainMenuQRCodeState>();
	}
}
