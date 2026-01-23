using System;
using Sirenix.OdinInspector;
using TMPro;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.UI;


public abstract class UIBaseListElement : MonoBehaviour
{
	[SerializeField]
	private TextMeshProUGUI m_text = null;

	protected IUIListElementData m_data;
	public IUIListElementData Data => m_data;

	public GameObject GameObject => gameObject;

	public virtual void SetData(IUIListElementData data)
	{
		m_data = data;
		m_text.text = data.Text;
	}

	public void RefreshData()
	{
		if (m_data != null)
		{
			SetData(m_data);
		}
	}
}
