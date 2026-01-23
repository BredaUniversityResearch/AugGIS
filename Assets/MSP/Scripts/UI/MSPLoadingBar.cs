using System;
using Sirenix.OdinInspector;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class MSPLoadingBar : MonoBehaviour
{
	[SerializeField]
	[Required]
	private TextMeshProUGUI m_headerTextComponent;

	[SerializeField]
	[Required]
	private TextMeshProUGUI m_infoTextComponent;

	[SerializeField]
	private bool m_showPercentageText = true;

	[SerializeField]
	[ShowIf("m_showPercentageText")]
	private TextMeshProUGUI m_percentageText;

	[SerializeField]
	[Required]
	private Image m_fillImage;

	[SerializeField]
	[Range(0,1)]
	private float m_valueSlider = 0;

	private float m_value = 0;
	public float Value => m_value;

	private void Awake()
	{
		Initialize();
	}

	private void Initialize()
	{
		m_percentageText.gameObject.SetActive(m_showPercentageText);
		InternalSetValue(m_valueSlider);
	}

	public void SetValue(float a_value)
	{	
		InternalSetValue(a_value);
		m_valueSlider = m_value;
	}

	public void SetHeaderText(string a_header)
	{
		m_headerTextComponent.text = a_header;
	}

	public void SetInfoText(string a_info)
	{
		m_infoTextComponent.text = a_info;
	}

	private void InternalSetValue(float a_value)
	{
		m_value = Math.Clamp(a_value, 0 , 1);
		m_fillImage.rectTransform.anchorMax = new Vector2(m_value, 1);

		if(m_showPercentageText)
		{
			m_percentageText.text = (a_value * 100f).ToString("n0") + "%";
		}
	}

	void OnValidate()
	{
		Initialize();
	}
}
