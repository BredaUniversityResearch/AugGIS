using POV_Unity;
using Unity.Netcode;
using UnityEngine;

public class WorldViewSessionState : ASessionState
{
	[SerializeField]
	private MenuAttachmentData m_rotatingRingMenuAttachementData = null;

	public override void OnEnter()
	{
		m_rotatingRingMenuAttachementData.EnableMenu();
	}

	public override void OnUpdate()
	{
		
	}

	public override void OnExit()
	{
		m_rotatingRingMenuAttachementData.DisableMenu();
	}
}
