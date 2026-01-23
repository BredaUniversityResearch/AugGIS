using System;
using System.Collections.Generic;
using System.Linq;
using Unity.VisualScripting;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[CustomPropertyDrawer(typeof(SessionStateDropDownAttribute))]
public class SessionStateDropdownAttributeDrawer : PropertyDrawer
{
	public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
	{
		EditorGUI.BeginProperty(position, label, property);

		List<string> options = new List<string>();

		Type baseType = typeof(ASessionState);
		Type[] subclassTypes = baseType.Assembly.GetTypes().Where(t => t.BaseType == baseType).ToArray();

		foreach (Type subclassType in subclassTypes)
		{
			options.Add(subclassType.Name);
		}

		if (options.Count <= 0)
		{
			property.intValue = 0;
			EditorGUILayout.HelpBox("No Subclass of Session State was Found.", MessageType.Warning, true);
			return;
		}

		SerializableTypeData selectedType = (SerializableTypeData)property.GetUnderlyingValue();

		int index = selectedType.IsValid? options.IndexOf(selectedType.name) : 0;

		index = EditorGUILayout.Popup("Session State", index, options.ToArray());

		if(options[index] == selectedType.name)
		{
			return;
		}
		
		SerializableTypeData type = new SerializableTypeData();
		type.name = options[index];
		property.SetUnderlyingValue(type);
		EditorGUI.EndProperty();
		EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
	}

	public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
	{ 
		return 0; 
	}
}