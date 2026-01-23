using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

[CustomPropertyDrawer(typeof(SceneNameDropdownAttribute))]
public class SceneNameDropdownAttributeEditor : PropertyDrawer
{
	public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
	{
		EditorGUI.BeginProperty(position, label, property);
		List<string> options = new List<string>();
		foreach (UnityEditor.EditorBuildSettingsScene scene in UnityEditor.EditorBuildSettings.scenes)
		{
			if (scene.enabled)
			{
				string name = scene.path.Substring(scene.path.LastIndexOf('/') + 1);
				name = name.Substring(0, name.Length - 6);
				options.Add(name);
			}
		}

		if (options.Count <= 0)
		{
			property.intValue = 0;
			EditorGUILayout.HelpBox("No Scene was Found.", MessageType.Warning, true);
			return;
		}

		string selectedScene = property.stringValue;

		int index = selectedScene != "" ? options.IndexOf(selectedScene) : 0;

		index = EditorGUILayout.Popup(property.displayName, index, options.ToArray());

		if (index < 0 || index > options.Count || options[index] == selectedScene)
		{
			return;
		}
		
		property.stringValue = options[index];
		EditorGUI.EndProperty();
		EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
	}

	public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
	{ 
		return 0; 
	}
}
