using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public class UIInGameUserSettings : MonoBehaviour
{
	[SerializeField]
	private UIToggleablePagedList m_toggleablePagedList;

	void Awake()
	{
		m_toggleablePagedList.Initialise();
	}

	void Start()
	{
		SessionManager.Instance.SessionUserManager.SessionConnectedPlayerDataListChanged += OnConnectedPlayerDataChanged;
	}

	void OnDestroy()
	{
		SessionManager.Instance.SessionUserManager.SessionConnectedPlayerDataListChanged -= OnConnectedPlayerDataChanged;
	}

	void OnEnable()
	{
		Debug.Log("On Enable: " + SessionManager.Instance.SessionUserManager.ConnectedPlayerCount);

		m_toggleablePagedList.ClearMenu();

		for (int i = 0; i < SessionManager.Instance.SessionUserManager.ConnectedPlayerCount; i++)
		{
			AddConnectedUserDataToToggleableList(SessionManager.Instance.SessionUserManager.GetConnectedPlayerDataAtIndex(i));
		}
	}

	public void OnConnectedPlayerDataChanged(SessionConnectedPlayerData[] a_sessionConnectedPlayerData)
	{
		m_toggleablePagedList.ClearMenu();

		foreach (SessionConnectedPlayerData data in a_sessionConnectedPlayerData)
		{
			AddConnectedUserDataToToggleableList(data);
		}
	}

	private void AddConnectedUserDataToToggleableList(SessionConnectedPlayerData a_userData)
	{
		UIPlayerInfoElementData uiPlayerInfoElementData = new UIPlayerInfoElementData(a_userData.playerName.ToString(), a_userData.teamColor, a_userData.isGameMaster);
		m_toggleablePagedList.AddElementData(uiPlayerInfoElementData);
	}
}
