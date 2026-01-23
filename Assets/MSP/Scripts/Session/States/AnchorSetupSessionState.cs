using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.Events;

public class AnchorSetupSessionState : ASessionState
{
	[SerializeField]
	[Required]
	private MenuAttachmentData m_setupAnchorsMenuAttachementData = null;

	private AnchorSetupMenu m_anchorSetupMenu = null;

 	public override void OnEnter()
	{
		GameObject setupAnchorsMenuGameobject = m_setupAnchorsMenuAttachementData.EnableMenu();
		Debug.Assert(setupAnchorsMenuGameobject != null);

		m_anchorSetupMenu = setupAnchorsMenuGameobject.GetComponent<AnchorSetupMenu>();
		m_anchorSetupMenu.AnchorPlacementConfirmed += OnAnchorPlacementConfirmed;
	}

	private void OnAnchorPlacementConfirmed()
	{
		SessionManager.Instance.StartNetworkedSession();
	}

	public override void OnUpdate()
	{
		
	}

	public override void OnExit()
	{
		m_setupAnchorsMenuAttachementData.DisableMenu();
		m_anchorSetupMenu.AnchorPlacementConfirmed -= OnAnchorPlacementConfirmed;
		m_anchorSetupMenu = null;
	}
}
