using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.AffordanceSystem.Receiver.Primitives;

/// <summary>
/// Applies a colour affordance to a per-renderer material instance instead of a MaterialPropertyBlock.
/// MaterialPropertyBlocks make a renderer incompatible with the SRP Batcher (and the GPU Resident Drawer);
/// material instances of the same shader variant are still batched, so many of these renderers stay in one SRP batch.
/// Other scripts can set additional properties (e.g. a base colour) on <see cref="Material"/>.
/// </summary>
public class MaterialInstanceColorAffordanceReceiver : ColorAffordanceReceiver
{
	[SerializeField] private Renderer m_renderer;
	[SerializeField] private int m_materialIndex = 0;
	[SerializeField] private string m_colorPropertyName = "_AffordanceColor";

	private Material m_material;
	private int m_colorPropertyId;

	/// <summary>
	/// The material instance owned by this receiver. Created on first access.
	/// </summary>
	public Material Material
	{
		get
		{
			if (m_material == null)
			{
				m_colorPropertyId = Shader.PropertyToID(m_colorPropertyName);
				Material[] materials = m_renderer.sharedMaterials;
				m_material = new Material(materials[m_materialIndex]);
				m_material.name = materials[m_materialIndex].name + " (Instance)";
				materials[m_materialIndex] = m_material;
				m_renderer.sharedMaterials = materials;
			}
			return m_material;
		}
	}

	protected override void OnAffordanceValueUpdated(Color a_newValue)
	{
		Material.SetColor(m_colorPropertyId, a_newValue);
		base.OnAffordanceValueUpdated(a_newValue);
	}

	protected override Color GetCurrentValueForCapture()
	{
		return Material.GetColor(m_colorPropertyId);
	}

	protected override void OnDestroy()
	{
		base.OnDestroy();
		if (m_material != null)
		{
			Destroy(m_material);
			m_material = null;
		}
	}
}
