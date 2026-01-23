using Sirenix.OdinInspector;
using UnityEngine;

public class PawnToolStationCategoryEntry : ToolStationCategoryEntry
{
	static readonly Vector3 k_colliderPadding = new Vector3(0.03f, 0.02f, 0.05f);

	[ReadOnly]
	[SerializeField]
	private BoxCollider m_collider;

	[ReadOnly]
	[SerializeField]
	private GameObject m_currentVisuals = null;

	[SerializeField]
	[ReadOnly]
	private GameObject m_visualsPrefab = null;
	public GameObject VisualsPrefab => m_visualsPrefab;

	public void SetVisuals(GameObject a_visualsPrefab)
	{
		if (m_currentVisuals != null)
		{
			Destroy(m_currentVisuals);
		}

		m_currentVisuals = Instantiate(a_visualsPrefab, m_pivotedTransform);
		
		// Create collider for 'entry-select' interactions
		m_collider = m_currentVisuals.AddComponent<BoxCollider>();
		m_collider.size = k_colliderPadding;

		m_xrSimpleInteractable.colliders.Clear();
		m_xrSimpleInteractable.colliders.Add(m_collider);

		m_currentVisuals.transform.localPosition = Vector3.zero;
		m_currentVisuals.gameObject.layer = LayerMask.NameToLayer("PokeInteractable");
		m_visualsPrefab = a_visualsPrefab;
	}
}
