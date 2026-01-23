using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UIMessagePopup : MonoBehaviour
{
	public struct PopupOptionData
	{
		public readonly string buttonText;
		public readonly Action OnClickedAction;

		public PopupOptionData(string a_buttonText, Action a_OnClickedAction)
		{
			buttonText = a_buttonText;
			OnClickedAction = a_OnClickedAction;
		}
	}

	public struct PopupShowArguments
	{
		public readonly string title;
		public readonly string body;
		public EPopupType popupType;
		public readonly PopupOptionData[] options;

		public PopupShowArguments(string a_title, string a_body, EPopupType a_popupType, PopupOptionData[] a_options)
		{
			title = a_title;
			body = a_body;
			popupType = a_popupType;
			options = a_options;
		}
	}

	public enum EPopupType
	{
		Info,
		Error
	}

	[SerializeField]
	private TextMeshProUGUI m_popupTilteText;

	[SerializeField]
	private TextMeshProUGUI m_popupBodyText;

	[SerializeField]
	private Transform m_buttonRoot;

	[SerializeField]
	private CustomXRButton m_optionButtonPrefab;

	[SerializeField]
	private Image m_iconImage;

	[SerializeField]
	private Sprite m_infoSprite;

	[SerializeField]
	private Sprite m_errorSprite;

	public void Show(PopupShowArguments a_enableArguments)
	{
		//destoy all previous button options
		for (int i = 0; i < m_buttonRoot.childCount; i++)
		{
			Destroy(m_buttonRoot.GetChild(i).gameObject);
		}

		m_popupTilteText.text = a_enableArguments.title;
		m_popupBodyText.text = a_enableArguments.body;

		m_iconImage.sprite = a_enableArguments.popupType == EPopupType.Info ? m_infoSprite : m_errorSprite;
		m_iconImage.color = a_enableArguments.popupType == EPopupType.Info ? Color.white : Color.red;

		for (int i = 0; i < a_enableArguments.options.Length; i++)
		{
			PopupOptionData currentPopupOptionData = a_enableArguments.options[i];
			CustomXRButton button = Instantiate(m_optionButtonPrefab, m_buttonRoot);
			button.GetComponentInChildren<TextMeshProUGUI>().text = currentPopupOptionData.buttonText;
			button.OnPress.AddListener(() => { currentPopupOptionData.OnClickedAction?.Invoke(); });
		}
	}
}
