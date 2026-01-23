using UnityEngine;
using UnityEngine.UI;

public class UIPlayerInfoToggleableListElement : UIBaseListElement
{
	[SerializeField]
	private Image m_teamColorIcon;

	[SerializeField]
	private Image m_gameMasterIcon;

	public override void SetData(IUIListElementData data)
	{
		base.SetData(data);

		m_teamColorIcon.color = (data as UIPlayerInfoElementData).PlayerTeamColor;
		m_gameMasterIcon.gameObject.SetActive((data as UIPlayerInfoElementData).IsGameMaster);
	}
}
