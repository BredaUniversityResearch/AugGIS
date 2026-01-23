using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class CategoryEntrySettings
{
	public GameObject visualPrefab;
}

[Serializable]
public class CategorySettings
{
	public string name;
	public Sprite categoryIcon;

	[SerializeField]
	public ToolStationCategoryEntry toolStationCategoryEntryPrefab;
	
	public List<CategoryEntrySettings> categoryEntries;
}

[CreateAssetMenu(fileName = "MenuStationElementConfig", menuName = "MSP/MenuStationElementConfig")]
public class ToolStationElementConfig : ScriptableObject
{
	[SerializeField]
	public ToolStationCategory toolStationCategoryPrefab;

	public List<CategorySettings> categorySettings = new List<CategorySettings>(); 
}
