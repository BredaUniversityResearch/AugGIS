using System;
using Sirenix.OdinInspector;
using TMPro;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.UI;

public class UIPlayerUsernameInputField : MonoBehaviour
{
	[SerializeField]
	private LocalPlayerSessionDataSO m_playerSessionData;

	[SerializeField]
	[Required]
	private TMP_InputField m_inputField;

	void Start()
	{
		m_inputField.onValueChanged.AddListener(OnInputFieldValueSubmitted);
		m_inputField.text = m_playerSessionData.playerName;
	}

	private void OnInputFieldValueSubmitted(string a_playerName)
	{
		m_playerSessionData.playerName = a_playerName;
	}
}
