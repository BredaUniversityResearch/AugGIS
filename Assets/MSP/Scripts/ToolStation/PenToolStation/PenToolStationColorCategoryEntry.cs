using Sirenix.OdinInspector;
using UnityEngine;

public class PenToolStationColorCategoryEntry : ToolStationCategoryEntry
{
	[SerializeField]
	[ReadOnly]
	private Color m_penColor;
	public Color PenColor => m_penColor;

	[SerializeField]
	private float m_penColorPercentageFill = .75f;

    protected override void OnValidate()
    {
		base.OnValidate();
        SetPenColor(m_penColor);
		SetPenColorPercentageFill(m_penColorPercentageFill);
    }

	public void SetPenColor(Color a_penColor)
	{
		m_penColor = a_penColor;

        m_materialPropertyBlock.SetColor("_PenColor", a_penColor);

		m_arcMeshRenderer.SetPropertyBlock(m_materialPropertyBlock);
	}

	private void SetPenColorPercentageFill(float a_percentage)
	{
		m_materialPropertyBlock.SetFloat("_PenColorPercentageFill", a_percentage);

		m_arcMeshRenderer.SetPropertyBlock(m_materialPropertyBlock);
	}
}
