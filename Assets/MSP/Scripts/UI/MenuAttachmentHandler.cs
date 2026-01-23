using System;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.Events;

public class MenuAttachmentHandler : MonoBehaviour
{
	[Serializable]
	public struct AttachementHandleData
	{
		public MenuAttachmentData m_menuAttachementData;
		public GameObject m_menuGameobject;
	}

	[SerializeField]
	private AttachementHandleData[] m_possibleAttachements;

	[Space]
	public UnityEvent OnMenuEnabled;
	public UnityEvent OnMenuDisabled;

	[ReadOnly]
	[SerializeField]
	private GameObject m_currentAttachedMenuGameobject = null;
	private MenuAttachmentData m_currentAttachementData = null;

	private Dictionary<MenuAttachmentData, GameObject> m_attachemedMenuDictionary = new Dictionary<MenuAttachmentData, GameObject>();

	private void Awake()
	{
		foreach (AttachementHandleData data in m_possibleAttachements)
		{
			data.m_menuAttachementData.SetupAttachement(this);
			m_attachemedMenuDictionary[data.m_menuAttachementData] = data.m_menuGameobject;
		}
	}

	public GameObject EnableMenu(MenuAttachmentData a_menuAttachmentData)
	{
		if (m_currentAttachementData != null)
		{
			m_currentAttachementData.DisableMenu();
		}

		m_currentAttachementData = a_menuAttachmentData;
		m_currentAttachedMenuGameobject = m_attachemedMenuDictionary[a_menuAttachmentData];
		m_currentAttachedMenuGameobject.SetActive(true);

		OnMenuEnabled.Invoke();
		return m_currentAttachedMenuGameobject;
	}

	public void DisableCurrentMenu()
	{
		if (m_currentAttachedMenuGameobject != null)
		{
			m_currentAttachedMenuGameobject.SetActive(false);
			OnMenuDisabled.Invoke();
		}

		m_currentAttachedMenuGameobject = null;
		m_currentAttachementData = null;
	}

	private void OnDestroy()
	{
		foreach (AttachementHandleData data in m_possibleAttachements)
		{
			data.m_menuAttachementData.Reset();
		}
	}

	void OnDisable()
	{
		if (m_currentAttachementData != null)
		{
			m_currentAttachementData.DisableMenu();
		}
	}
}
