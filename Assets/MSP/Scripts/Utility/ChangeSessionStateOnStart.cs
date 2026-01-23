using UnityEngine;

public class ChangeSessionStateOnStart : MonoBehaviour
{
	[SerializeField]
	[SessionStateDropDown]
	private SerializableTypeData m_sessionState;
	
	void Start()
	{
		SessionManager.Instance.SessionFSM.ChangeState(m_sessionState.Type);
	}
}
