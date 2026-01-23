using Sirenix.OdinInspector;
using TMPro;
using UnityEngine;

public class UILocalIPText : MonoBehaviour
{
	[SerializeField]
	[Required]
	private TextMeshProUGUI m_textComponent;

	void OnEnable()
	{
		UpdateIPText();
	}

	public void UpdateIPText()
	{
		m_textComponent.text = SessionManager.Instance.GetLocalIPAdress().ToString();
	}
}
