using System.Net;
using Sirenix.OdinInspector;
using TMPro;
using UnityEngine;

public class MainMenuIpSettingsState : AMainMenuState
{
	[SerializeField]
	[Required]
	private NetworkSessionConnectionData m_networkSessionConnectionData;

	[SerializeField]
	private TMP_InputField m_targetIPInputField;

	private UIMainMenu m_mainMenu;

	public override void OnInitialise()
	{
		base.OnInitialise();
		m_mainMenu = MainMenuFSM.GetComponent<UIMainMenu>();
	}

	public override void OnEnter()
	{
		base.OnEnter();

		m_mainMenu.EnableTraversalButtons(() =>
		{
			bool parsed = Utils.TryParseIPAndPortString(m_targetIPInputField.text, out IPAddress ip, out ushort port);
			if(parsed)
			{
				m_networkSessionConnectionData.ip = ip;
				if(port != 0) m_networkSessionConnectionData.Port = port;

				MainMenuFSM.ChangeState<MainMenuUserSettingsState>();
			}
		},
		() => { MainMenuFSM.ChangeState<MainMenuScanServersState>(); });
	}

	public override void OnExit()
	{
		base.OnExit();
		m_mainMenu.DisableTraversalButtons();
	}
}
