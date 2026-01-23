using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

public class ToolStationCategory : ToolStationElementBase
{
	[SerializeField]
	private SpriteRenderer m_spriteRenderer;

	[SerializeField]
	[Required]
	private Transform m_entryParentTransform;

	[SerializeField]
	[ReadOnly]
	private List<ToolStationCategoryEntry> m_entries = new List<ToolStationCategoryEntry>();

	public int EntryCount => m_entries.Count;

	public void SetCategoryIcon(Sprite a_sprite, float a_scale)
	{
		m_spriteRenderer.sprite = a_sprite;
		m_spriteRenderer.transform.localScale = new Vector3(a_scale, a_scale, a_scale);
	}

	public void AddEntry(ToolStationCategoryEntry a_entry)
	{
		m_entries.Add(a_entry);
		a_entry.transform.SetParent(m_entryParentTransform);
		a_entry.transform.localPosition = Vector3.zero;
	}

	public void SetEntryParentLocalPosition(Vector3 a_localPos)
	{
		m_entryParentTransform.localPosition = a_localPos;
	}

	public void SetEntryParentLocalRotation(Quaternion a_localRotation)
	{
		m_entryParentTransform.localRotation = a_localRotation;
	}

	public ToolStationCategoryEntry GetEntryAtIndex(int a_index)
	{
		if (a_index < 0 || a_index >= m_entries.Count)
		{
			return null;
		}

		return m_entries[a_index];
	}

	public void EnableCategoryEntries()
	{
		m_entryParentTransform.gameObject.SetActive(true);
	}

	public void DisableCategoryEntries()
	{
		m_entryParentTransform.gameObject.SetActive(false);
	}
}
