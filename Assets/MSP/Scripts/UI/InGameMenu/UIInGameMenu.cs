using System;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.UI;

public class UIInGameMenu : MonoBehaviour
{
	[System.Serializable]
	public struct TabTogglePair
	{
		public CustomXRToggle toggle;
		public GameObject tabGameObject;
	}

	[SerializeField]
	private TabTogglePair[] m_tabTogglePairs;

	private int m_currentSelectedTabIndex = -1;

	void Start()
	{
		SessionManager.Instance.SessionUserManager.SessionConnectedPlayerDataListChanged += OnConnectedPlayerDataListChanged;
		RefreshMenu();

		for (int i = 0; i < m_tabTogglePairs.Length; i++)
		{
			int index = i;

			m_tabTogglePairs[i].toggle.OnPressOn.AddListener(() =>
			{
				SelectTab(index);
			});
			m_tabTogglePairs[i].toggle.OnPressOff.AddListener(() =>
			{
				m_tabTogglePairs[index].tabGameObject.SetActive(false);
				m_currentSelectedTabIndex = -1;
			});
		}

		SelectTab(0);
	}

	private void OnConnectedPlayerDataListChanged(SessionConnectedPlayerData[] a_connectedPlayerData)
	{
		RefreshMenu();
	}

	void OnEnable()
	{
		m_tabTogglePairs[0].toggle.IsSelected = true;
		SelectTab(0);
		RefreshMenu();
	}

	private void RefreshMenu()
	{
		if (SessionManager.Instance.SessionUserManager.IsLocalClientGameMaster())
		{
			for (int i = 0; i < m_tabTogglePairs.Length; i++)
			{
				m_tabTogglePairs[i].toggle.gameObject.SetActive(true);
			}
		}
		else
		{
			SelectTab(0);

			for (int i = 0; i < m_tabTogglePairs.Length; i++)
			{
				m_tabTogglePairs[i].toggle.gameObject.SetActive(false);
			}
		}
	}

	private void SelectTab(int a_tabIndex)
	{
		if (a_tabIndex < 0 || a_tabIndex >= m_tabTogglePairs.Length)
		{
			Debug.LogError("Tab Index is out of bounds: " + a_tabIndex, this);
			return;
		}

		if (a_tabIndex == m_currentSelectedTabIndex)
		{
			Debug.LogWarning("Tab Index is already selected: " + a_tabIndex, this);
			return;
		}

		if (m_currentSelectedTabIndex != -1)
		{
			m_tabTogglePairs[m_currentSelectedTabIndex].toggle.IsSelected = false;
			m_tabTogglePairs[m_currentSelectedTabIndex].tabGameObject.SetActive(false);
		}

		m_currentSelectedTabIndex = a_tabIndex;
		m_tabTogglePairs[m_currentSelectedTabIndex].toggle.IsSelected = true;
		m_tabTogglePairs[m_currentSelectedTabIndex].tabGameObject.SetActive(true);
	}

	void OnDestroy()
	{
		if (SessionManager.Instance.SessionUserManager != null)
		{
			SessionManager.Instance.SessionUserManager.SessionConnectedPlayerDataListChanged -= OnConnectedPlayerDataListChanged;
		}
	}
}
