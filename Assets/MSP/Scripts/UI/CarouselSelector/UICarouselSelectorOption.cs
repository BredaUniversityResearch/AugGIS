using System;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

public class UICarouselSelectorOption : MonoBehaviour
{
	[SerializeField]
	[Required]
	private Image m_iconImage;

	[SerializeField]
	[Required]
	private Image m_selectedBackgroundImage;

	[SerializeField]
	[Required]
	private XRSimpleInteractable m_xrSimpleInteractable;
	public XRSimpleInteractable XRSimpleInteractable => m_xrSimpleInteractable;

	private IUICarouselSelectionData m_data;
	public IUICarouselSelectionData Data => m_data;

	public void SetData(IUICarouselSelectionData a_data)
	{
		m_data = a_data;

		m_iconImage.sprite = m_data.Icon;
		m_iconImage.color = m_data.Color;
	}

	public void Select()
	{
		m_selectedBackgroundImage.gameObject.SetActive(true);
	}

	public void DeSelect()
	{
		m_selectedBackgroundImage.gameObject.SetActive(false);
	}
}
