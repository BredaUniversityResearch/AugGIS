#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace ColourPalette
{
	[CustomEditor(typeof(CustomTMPText))]
	public class CustomTMPTextEditor : TMPro.EditorUtilities.TMP_EditorPanelUI
	{
		public override void OnInspectorGUI()
		{
			base.OnInspectorGUI();

			serializedObject.Update();
			EditorGUILayout.ObjectField(serializedObject.FindProperty("colourAsset"), typeof(ColourAsset));
			serializedObject.ApplyModifiedProperties();
		}
	}
}
#endif
