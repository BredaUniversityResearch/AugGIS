#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

public class BootSceneSettings : ScriptableObject
{
	public const string SETTINGS_PATH = "Assets/MSP/Resources/BootSceneSettings.asset";

	[SerializeField]
	private SceneAsset m_editorBootScene;

	public SceneAsset EditorBootScene => m_editorBootScene;

	public static BootSceneSettings GetOrCreateSettings()
	{
		var settings = AssetDatabase.LoadAssetAtPath<BootSceneSettings>(SETTINGS_PATH);
		if (settings == null)
		{
			settings = ScriptableObject.CreateInstance<BootSceneSettings>();
			AssetDatabase.CreateAsset(settings, SETTINGS_PATH);
			AssetDatabase.SaveAssets();
		}
		return settings;
	}

}
#endif