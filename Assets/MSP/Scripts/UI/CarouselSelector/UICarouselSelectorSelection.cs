using Sirenix.OdinInspector;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UICarouselSelectorSelection : MonoBehaviour
{
	[SerializeField]
	[Required]
	private Image m_imageComponent;

	[SerializeField]
	[Required]
	private TextMeshProUGUI m_textComponent;

	private IUICarouselSelectionData m_currentData = null;
	public IUICarouselSelectionData CurrentData => m_currentData;

	public void SetData(IUICarouselSelectionData a_newSelectionData)
	{
		m_currentData = a_newSelectionData;

		m_imageComponent.sprite = m_currentData.Icon;
		m_imageComponent.color = m_currentData.Color;
		m_textComponent.text = m_currentData.Label;
	}
}
