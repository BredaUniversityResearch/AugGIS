using System;
using POV_Unity;
using UnityEngine;

public class LoadingMapSessionState : ASessionState
{
	[SerializeField]
	private MenuAttachmentData m_loadingMenuAttachmentData;

	void Start()
	{
		SessionManager.Instance.DisconnectedFromSession += OnDisconnectedFromSession;
	}

	void OnDestroy()
	{
		SessionManager.Instance.DisconnectedFromSession -= OnDisconnectedFromSession;
	}

	public override void OnEnter()
	{
		EnableMenu();
	}

	public override void OnUpdate()
	{
		if (ImportedConfigRoot.Instance != null && ImportedConfigRoot.Instance.ImportComplete)
		{
			SessionFSM.ChangeState<WorldViewSessionState>();
		}
	}

	public override void OnExit()
	{
		DisableMenu();
	}

	private void OnDisconnectedFromSession()
	{
		DisableMenu();
	}

	private void EnableMenu()
	{
#if !UNITY_SERVER
		m_loadingMenuAttachmentData.EnableMenu();
#endif
	}

	private void DisableMenu()
	{
#if !UNITY_SERVER
		m_loadingMenuAttachmentData.DisableMenu();
#endif
	}
}
