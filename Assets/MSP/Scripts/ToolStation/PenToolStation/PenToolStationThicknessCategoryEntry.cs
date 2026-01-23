using Sirenix.OdinInspector;
using UnityEngine;


public class PenToolStationThicknessCategoryEntry : ToolStationCategoryEntry
{
	[SerializeField]
	private SpriteRenderer m_thicknessIcon;	
	
	[SerializeField]
	[ReadOnly]
	private float m_thicknessValue = 0;
	public float ThicknessValue => m_thicknessValue;

	public void SetThickness(float a_thickness)
	{
		m_thicknessValue = a_thickness;
		m_thicknessIcon.transform.localScale = Vector3.one * m_thicknessValue;
	}
}
