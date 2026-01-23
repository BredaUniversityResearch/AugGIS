using Sirenix.OdinInspector;
using UnityEngine;

public class UIRectScaler : MonoBehaviour
{
	public enum EScaleMode
	{
		Constant,
		Meters
	}

	[SerializeField]
	private EScaleMode m_scaleMode = EScaleMode.Constant;

	[EnableIf("OdinEditorEnableIfMetersFunction")]
	[SerializeField]
	private float m_desiredScaleInMeters = 1;

	[EnableIf("OdinEditorEnableIfConstantFunction")]
	[SerializeField]
	private float m_constantScaleFactor = 0.00065f;

	[SerializeField]
	private RectTransform m_rectTransform;

	void Awake()
	{
		m_rectTransform = GetComponent<RectTransform>();
		SetRectScale();
	}

	void OnValidate()
	{
		SetRectScale();
	}

	private void SetRectScale()
	{
		if(m_scaleMode == EScaleMode.Constant)
		{
			m_rectTransform.localScale = Vector3.one * m_constantScaleFactor;
			m_desiredScaleInMeters = m_constantScaleFactor * m_rectTransform.rect.width;
		}
		else
		{
			float scaleFactor = m_desiredScaleInMeters/m_rectTransform.rect.width;
			m_rectTransform.localScale = Vector3.one * scaleFactor;
			m_constantScaleFactor = scaleFactor;
		}
	}

	public bool OdinEditorEnableIfConstantFunction()
	{
		return m_scaleMode == EScaleMode.Constant;
	}

	public bool OdinEditorEnableIfMetersFunction()
	{
		return m_scaleMode == EScaleMode.Meters;
	}
}
