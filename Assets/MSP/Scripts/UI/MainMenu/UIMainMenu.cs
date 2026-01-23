
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

public class UIMainMenu : MonoBehaviour
{
	[SerializeField]
	private MainMenuFSM m_mainMenuFSM;
	public MainMenuFSM MainMenuFSM => m_mainMenuFSM;

	[Header("Traversal Buttons")]
	[SerializeField]
	private RectTransform m_traversalButtonsRootRectTransform;

	[SerializeField]
	private CustomXRButton m_continueButton;
	[SerializeField]
	private CustomXRButton m_backButton;

	void Start()
	{
		m_mainMenuFSM.ChangeState<MainMenuInitialState>();
	}

	public void EnableTraversalButtons(UnityAction a_continueButtonCallback, UnityAction a_backButtonCallback)
	{
		m_traversalButtonsRootRectTransform.gameObject.SetActive(true);

		m_continueButton.isInteractable = true;
		m_backButton.isInteractable = true;

		m_continueButton.OnPress.AddListener(a_continueButtonCallback);
		m_backButton.OnPress.AddListener(a_backButtonCallback);
	}

	public void DisableTraversalButtons()
	{
		m_continueButton.OnPress.RemoveAllListeners();
		m_backButton.OnPress.RemoveAllListeners();

		m_traversalButtonsRootRectTransform.gameObject.SetActive(false);
	}

	public void SetContinueButtonInteratableState(bool isInteractable)
	{
		if (isInteractable == m_continueButton.isInteractable)
			return; // Skip button update call

		m_continueButton.isInteractable = isInteractable;
	}
}
