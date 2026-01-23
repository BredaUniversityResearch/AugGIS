using Sirenix.OdinInspector;
using TMPro;
using UnityEngine;
using static UIScanLocalServersMenu;

public class UIServerToggleableListElement : UIBaseListElement
{
	[SerializeField]
	[Required]
	private TextMeshProUGUI m_serverInfoText;

	public override void SetData(IUIListElementData data)
	{
		base.SetData(data);

		UILocalServerData uiServerListElementData = data as UILocalServerData;
		m_serverInfoText.text = uiServerListElementData.ServerInfoText;
	}
}
