using System;
using Sirenix.OdinInspector;
using Unity.VectorGraphics;
using UnityEngine;
using UnityEngine.SceneManagement;

public class MainMenuSessionState : ASessionState
{
	[SerializeField]
	[Required]
	private MenuAttachmentData m_mainMenuAttachementData = null;

	public override void OnEnter()
	{
		GameObject menuGameObject = m_mainMenuAttachementData.EnableMenu();
		menuGameObject.GetComponent<UIMainMenu>().MainMenuFSM.ChangeState<MainMenuInitialState>();
	}

	public override void OnUpdate()
	{
	}

	public override void OnExit()
	{
		m_mainMenuAttachementData.DisableMenu();
	}
}
