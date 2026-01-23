using System;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class UIInGameGeneralSettings : MonoBehaviour
{
	[SerializeField]
	[Required]
	private MenuAttachmentData m_inGameMenuAttachement;

	[SerializeField]
	[Required]
	private CustomXRButton m_resumeButton;

	[SerializeField]
	[Required]
	private CustomXRButton m_mainMenuButton;

	[SerializeField]
	[Required]
	private CustomXRButton m_quitButton;

	void Awake()
	{
		m_resumeButton.OnPress.AddListener(OnResumeButtonClicked);
		m_mainMenuButton.OnPress.AddListener(OnMainMenuButtonClicked);
		m_quitButton.OnPress.AddListener(OnQuitButtonClicked);
	}

	private void OnResumeButtonClicked()
	{
		m_inGameMenuAttachement.DisableMenu();
	}

	private void OnMainMenuButtonClicked()
	{
		SessionManager.Instance.LeaveSession();
	}

	private void OnQuitButtonClicked()
	{
		Application.Quit();
	}
}
