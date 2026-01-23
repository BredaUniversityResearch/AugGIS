using Sirenix.OdinInspector;
using UnityEngine;

[CreateAssetMenu(fileName = "MenuAttachmentData", menuName = "MSP/UI/MenuAttachmentData")]
public class MenuAttachmentData : ScriptableObject
{
	[SerializeField]
	[ReadOnly]
	private bool m_isAttached = false;

	public bool IsAttached => m_isAttached;
	
	private MenuAttachmentHandler m_handler;

	public GameObject EnableMenu()
	{
		Debug.Assert(m_handler != null,"Menu Attachement has not been setup by Handler!");
		
		m_isAttached = true;
		return m_handler.EnableMenu(this);
	}

	public void DisableMenu()
	{
		if(m_isAttached)
		{
			m_handler.DisableCurrentMenu();
			m_isAttached = false;
		}
	}

	public void ToggleMenu()
	{
		if (m_isAttached)
		{
			DisableMenu();
		}
		else
		{
			EnableMenu();
		}
	}

	public void SetupAttachement(MenuAttachmentHandler a_menuAttachementHandler)
	{
		m_handler = a_menuAttachementHandler;
	}

	public void Reset()
	{
		m_handler = null;
		m_isAttached = false;
	}
}
