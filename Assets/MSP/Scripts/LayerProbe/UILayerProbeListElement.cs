using POV_Unity;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UILayerProbeListElement : UIBaseListElement
{
	[SerializeField]
	private TextMeshProUGUI m_typeText;

	[SerializeField]
	private Image m_icon;

	public override void SetData(IUIListElementData data)
	{
		base.SetData(data);

		UILayerProbe.UILayerProbeElementData uILayerProbeElementData = data as UILayerProbe.UILayerProbeElementData;
		Debug.Assert(data != null);

		m_typeText.text = uILayerProbeElementData.InfoText;
		m_icon.sprite = AssetManager.GetSprite(uILayerProbeElementData.LayerQueryData.layer.Category.icon);
	}
}
