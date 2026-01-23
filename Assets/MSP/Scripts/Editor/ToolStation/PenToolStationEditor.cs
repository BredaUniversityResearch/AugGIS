using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(PenToolStation))]
public class PenToolStationEditor : ToolStationEditor
{
	SerializedProperty m_colorEntries;
	SerializedProperty m_thicknessEntries;

	
	private int m_assignedColorEntryCount = 0;
	private int m_assignedThicknessEntryCount = 0;

	// Called in OnEnable in the base class
	protected override void LoadProperties()
	{
		base.LoadProperties();

		m_colorEntries = serializedObject.FindProperty("m_colorEntries");
		m_thicknessEntries = serializedObject.FindProperty("m_thicknessEntries");
	}

	protected override void OnCategoryEntryCreated(ToolStationCategoryEntry a_entry, CategorySettings a_categorySettings, CategoryEntrySettings a_entrySettings)
	{
		if(a_categorySettings.name.ToLower() == "color")
		{
			PenToolStationColorCategoryEntry entry = a_entry as PenToolStationColorCategoryEntry;
			SerializedProperty colorEntry = m_colorEntries.GetArrayElementAtIndex(m_assignedColorEntryCount);
			Color color = colorEntry.colorValue;
			entry.SetPenColor(color);
			m_assignedColorEntryCount++;
		}
		else if(a_categorySettings.name.ToLower() == "thickness")
		{
			(a_entry as PenToolStationThicknessCategoryEntry).SetThickness(m_thicknessEntries.GetArrayElementAtIndex(m_assignedThicknessEntryCount).floatValue);
			m_assignedThicknessEntryCount++;
		}
	}

	protected override void DestroyMenu()
	{
		base.DestroyMenu();
		m_assignedColorEntryCount = 0;
		m_assignedThicknessEntryCount = 0;
	}
}
