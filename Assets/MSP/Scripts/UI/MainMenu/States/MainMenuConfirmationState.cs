using UnityEngine;

public class MainMenuConfirmationState : AMainMenuState
{
	[SerializeField]
	private MenuAttachmentData m_mainMenuAttachement;

	[SerializeField]
	private MenuAttachmentData m_messagePopupAttachement;

	public override void OnEnter()
	{
		base.OnEnter();

		UIMessagePopup popup = m_messagePopupAttachement.EnableMenu().GetComponent<UIMessagePopup>();
		Debug.Assert(popup != null);

		UIMessagePopup.PopupOptionData declineOptionData = new UIMessagePopup.PopupOptionData("No", () => { m_mainMenuAttachement.EnableMenu(); MainMenuFSM.ChangeState<MainMenuUserSettingsState>(); });
		UIMessagePopup.PopupOptionData acceptOptionData = new UIMessagePopup.PopupOptionData("Yes", () => { m_messagePopupAttachement.DisableMenu(); SessionManager.Instance.SessionFSM.ChangeState<AnchorSetupSessionState>(); });

		UIMessagePopup.PopupShowArguments popupShowArguments = new UIMessagePopup.PopupShowArguments("Connect", "Accept settings and connect to session?", UIMessagePopup.EPopupType.Info, new UIMessagePopup.PopupOptionData[2] { declineOptionData, acceptOptionData });
		popup.Show(popupShowArguments);
	}
}
