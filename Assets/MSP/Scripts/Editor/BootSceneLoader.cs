using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

[InitializeOnLoadAttribute]
public static class BootSceneLoader
{
	private static BootSceneSettings ms_bootSceneSettings;

	static BootSceneLoader()
	{
		EditorApplication.playModeStateChanged += LoadDefaultScene;
		ms_bootSceneSettings = BootSceneSettings.GetOrCreateSettings();
	}

	private static void LoadDefaultScene(PlayModeStateChange a_state)
	{
		if (a_state == PlayModeStateChange.ExitingEditMode)
		{
			EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo();
		}

		if (a_state == PlayModeStateChange.EnteredPlayMode)
		{
			if(ms_bootSceneSettings.EditorBootScene == null || ms_bootSceneSettings.EditorBootScene.name == SceneManager.GetActiveScene().name)
			{
				return;
			}

			EditorSceneManager.LoadScene(ms_bootSceneSettings.EditorBootScene.name,LoadSceneMode.Single);
		}
	}
}