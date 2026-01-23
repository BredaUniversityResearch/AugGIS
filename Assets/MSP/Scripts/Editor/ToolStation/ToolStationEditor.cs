using ColourPalette;
using UnityEditor;
using UnityEngine;


[CustomEditor(typeof(ToolStation), true)]
public class ToolStationEditor : Editor
{
    ToolStation m_targetToolStation;

	SerializedProperty m_allCategories;
	SerializedProperty m_categoryArchSettings;
	SerializedProperty m_categoryEntryArchSettings;
	SerializedProperty m_menuStationElementConfig;
	SerializedProperty m_categoryColor;
	SerializedProperty m_categoryEntryColor;
	SerializedProperty m_entriesYOffset;

	static readonly string s_generatedToolStationMeshesPath = "Assets/GeneratedMeshes/ToolStations/";

    void OnEnable()
    {
		LoadProperties();
    }

	protected virtual void LoadProperties()
    {
        m_targetToolStation = (ToolStation)target;

		m_menuStationElementConfig = serializedObject.FindProperty("m_menuStationElementConfig");
		m_categoryArchSettings = serializedObject.FindProperty("m_categoryArchSettings");
		m_categoryEntryArchSettings = serializedObject.FindProperty("m_categoryEntryArchSettings");
		m_categoryColor = serializedObject.FindProperty("m_categoryColor");
		m_categoryEntryColor = serializedObject.FindProperty("m_categoryEntryColor");
		m_entriesYOffset = serializedObject.FindProperty("m_entriesYOffset");
		m_allCategories = serializedObject.FindProperty("m_allCategories");
    }

    public override void OnInspectorGUI()
    {
		serializedObject.Update();
		DrawDefaultInspector();

		if (GUILayout.Button("Create Menu"))
        {
            DestroyMenu();
			CreateMenu();
        }

		if (GUILayout.Button("Destroy Menu"))
		{
			DestroyMenu();
		}

        serializedObject.ApplyModifiedProperties();
    }

	private void CreateMenu()
	{
		float loopAngleIncrement;
		Vector3 pivotRelativePosition;
		Quaternion pivotRelativeRotation;
		Mesh categoryMesh;
		float startAngle = 0;
		ToolStation.ArchSettings archSettings = (ToolStation.ArchSettings)m_categoryArchSettings.boxedValue;
		ToolStationElementConfig elementConfig = (ToolStationElementConfig)m_menuStationElementConfig.boxedValue;

		GenerateMenuStationElement(archSettings, elementConfig.categorySettings.Count, startAngle, m_targetToolStation.gameObject.name + "_category", out loopAngleIncrement, out pivotRelativePosition, out pivotRelativeRotation, out categoryMesh);

		float pivotYRotation = startAngle;
		Color categoryColor = (m_categoryColor.boxedValue as ColourAsset).GetColour();


		for(int i = 0; i < elementConfig.categorySettings.Count; i++)
		{
			CategorySettings categorySettings = elementConfig.categorySettings[i];

			ToolStationCategory category = Instantiate(elementConfig.toolStationCategoryPrefab, m_targetToolStation.transform);
			category.RegisterToStation(m_targetToolStation, i);
			m_allCategories.InsertArrayElementAtIndex(i);
			m_allCategories.GetArrayElementAtIndex(i).objectReferenceValue = category;
			category.SetCategoryIcon(categorySettings.categoryIcon,  archSettings.archThickness);
			category.DisableCategoryEntries();
			category.gameObject.name = "Category" + i;

			InitialiseMenuStationElement(category, i, pivotYRotation, pivotRelativePosition, pivotRelativeRotation, categoryColor, categoryMesh);

			CreateMenuCategoryEntries(categorySettings,category);
			OnCategoryCreated(category, categorySettings);
			
			pivotYRotation += loopAngleIncrement;
		}
	}

	private void CreateMenuCategoryEntries(CategorySettings a_categorySettings, ToolStationCategory a_parentCategory)
	{
		float loopAngleIncrement;
		Vector3 pivotRelativePosition;
		Quaternion pivotRelativeRotation;
		Mesh entryMesh;
		float startAngle = 0;
		ToolStation.ArchSettings archSettings = (ToolStation.ArchSettings)m_categoryEntryArchSettings.boxedValue;
		GenerateMenuStationElement(archSettings, a_categorySettings.categoryEntries.Count, startAngle, m_targetToolStation.gameObject.name + "_" + a_categorySettings.name + "_entry", out loopAngleIncrement, out pivotRelativePosition, out pivotRelativeRotation, out entryMesh);

		float pivotYRotation = startAngle;
		Color categoryEntryColor = (m_categoryEntryColor.boxedValue as ColourAsset).GetColour();

		a_parentCategory.SetEntryParentLocalPosition(new Vector3(0, m_entriesYOffset.floatValue, 0));

		// Compensate for category Y rotation
		a_parentCategory.SetEntryParentLocalRotation(Quaternion.Euler(
			0,
			-a_parentCategory.transform.rotation.eulerAngles.y,
			0
		));
		
		for(int i = 0; i < a_categorySettings.categoryEntries.Count; i++)
		{
			CategoryEntrySettings entrySettings = a_categorySettings.categoryEntries[i];

			ToolStationCategoryEntry entry = Instantiate(a_categorySettings.toolStationCategoryEntryPrefab, a_parentCategory.transform);
			entry.gameObject.name = "Entry" + i;
			a_parentCategory.AddEntry(entry);

			InitialiseMenuStationElement(entry, i, pivotYRotation, pivotRelativePosition, pivotRelativeRotation, categoryEntryColor, entryMesh);
			OnCategoryEntryCreated(entry, a_categorySettings, entrySettings);

			pivotYRotation += loopAngleIncrement;
		}
	}

	protected virtual void DestroyMenu()
	{
		foreach(SerializedProperty category in m_allCategories)
		{
			if(category != null)
			{
				ToolStationCategory categoryValue = category.boxedValue as ToolStationCategory;
				DestroyImmediate(categoryValue.gameObject);
			}
		}

		m_allCategories.ClearArray();
	}

	protected virtual void OnCategoryCreated(ToolStationCategory a_category, CategorySettings a_settings)
	{

	}

	protected virtual void OnCategoryEntryCreated(ToolStationCategoryEntry a_entry, CategorySettings a_categorySettings, CategoryEntrySettings a_entrySettings)
	{

	}

	private void InitialiseMenuStationElement(ToolStationElementBase a_menuStationElementBase, int a_index, float a_yRotation, Vector3 a_pivotRelativePosition, Quaternion a_pivotRelativeRotation, Color a_color, Mesh a_mesh)
	{
		a_menuStationElementBase.RegisterToStation(m_targetToolStation, a_index);
		a_menuStationElementBase.Initialise(a_pivotRelativePosition, a_pivotRelativeRotation, a_yRotation, a_color, a_mesh);
	}

	private void GenerateMenuStationElement(ToolStation.ArchSettings a_archSettings, int a_elementCount, float a_startAngle, string a_elementName,
	                                        out float loopAngleIncrement, out Vector3 pivotRelativePosition, out Quaternion pivotRelativeRotation, out Mesh elementMesh)
	{
		loopAngleIncrement = 360f / a_elementCount;

		float endAngle = loopAngleIncrement - a_archSettings.spacingBetweenEntries;

		int numSegments = (int)(ToolStation.ArchSettings.k_segmentsPer360Degrees * ((endAngle - a_startAngle) / 360f));
		elementMesh = DiscShapeGenerator.GenerateShape(a_archSettings.archRadius, a_archSettings.archThickness, numSegments, a_startAngle, endAngle);
		AssetDatabase.CreateAsset(elementMesh, s_generatedToolStationMeshesPath + a_elementName + ".asset");

		pivotRelativePosition = Quaternion.Euler(0, (endAngle - a_startAngle) / 2f, 0) * m_targetToolStation.transform.right * a_archSettings.archRadius;
		pivotRelativeRotation = Quaternion.Euler(0, (endAngle - a_startAngle) / 2f, 0);
	}
}
