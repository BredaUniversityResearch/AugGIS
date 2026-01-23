using System;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

public class SceneLoader : MonoBehaviour
{
	[SceneNameDropdown]
	[SerializeField]
	private string m_sceneToLoad;

	[SerializeField]
	private bool m_additiveLoading = false;

	[SerializeField]
	private bool m_networkedLoading = false;
	
	public void LoadScene()
	{
		if(m_networkedLoading)
		{
			LoadSceneNetworked();
		}
		else
		{
			LoadSceneOffline();
		}
	}

	private void LoadSceneOffline()
	{
		SceneManager.LoadScene(m_sceneToLoad, m_additiveLoading? LoadSceneMode.Additive : LoadSceneMode.Single);
	}

	private void LoadSceneNetworked()
	{
		NetworkManager.Singleton.SceneManager.SetClientSynchronizationMode(m_additiveLoading? LoadSceneMode.Additive : LoadSceneMode.Single);
		NetworkManager.Singleton.SceneManager.VerifySceneBeforeLoading = VerifySceneLoad;
		if (NetworkManager.Singleton.IsServer || NetworkManager.Singleton.IsHost)
		{
			NetworkManager.Singleton.SceneManager.LoadScene(m_sceneToLoad, m_additiveLoading? LoadSceneMode.Additive : LoadSceneMode.Single);
		}
	}

	private bool VerifySceneLoad(int a_sceneIndex, string a_sceneName, LoadSceneMode loadSceneMode)
	{
		if (a_sceneName != m_sceneToLoad)
		{
			Debug.LogError("Scene Not verified: " + a_sceneName);
			return false;
		}
		return true;
	}
}
