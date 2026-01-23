
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

public class BootSceneSettingsProvider : SettingsProvider
{
	private SerializedObject m_bootSceneSettings;

	public BootSceneSettingsProvider(string a_path, SettingsScope a_scopes, IEnumerable<string> a_keywords = null) : base(a_path, a_scopes, a_keywords)
	{
		
	}

	public override void OnActivate(string a_searchContext, VisualElement a_rootElement)
	{
		m_bootSceneSettings = new SerializedObject(BootSceneSettings.GetOrCreateSettings());
	}

	public override void OnGUI(string a_searchContext)
	{
		EditorGUILayout.PropertyField(m_bootSceneSettings.FindProperty("m_editorBootScene"));
		m_bootSceneSettings.ApplyModifiedPropertiesWithoutUndo();
	}

	[SettingsProvider]
	public static SettingsProvider CreateMyCustomSettingsProvider()
	{
		return new BootSceneSettingsProvider("Project/Boot Scene", SettingsScope.Project);
	}
}
