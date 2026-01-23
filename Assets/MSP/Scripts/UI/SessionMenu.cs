using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace POV_Unity
{
	public class SessionMenu : MonoBehaviour
	{
		[SerializeField] Button m_startAnchoringButton;
		[SerializeField] Button m_finishAnchoringButton;
		[SerializeField] Button m_returnToLoginButton;

		bool m_leavingScene;

		void Start()
		{
			m_startAnchoringButton.gameObject.SetActive(false);
			m_finishAnchoringButton.gameObject.SetActive(true);
			m_startAnchoringButton.onClick.AddListener(StartAnchoring);
			m_finishAnchoringButton.onClick.AddListener(StopAnchoring);
			m_returnToLoginButton.onClick.AddListener(ReturnToLogin);
			NetworkManager.Singleton.OnClientDisconnectCallback += OnClientDisconnected;
		}

		void StartAnchoring()
		{
			m_startAnchoringButton.gameObject.SetActive(false);
			m_finishAnchoringButton.gameObject.SetActive(true);
		}

		void StopAnchoring()
		{
			m_startAnchoringButton.gameObject.SetActive(true);
			m_finishAnchoringButton.gameObject.SetActive(false);

		}

		void ReturnToLogin()
		{
			NetworkManager.Singleton.OnClientDisconnectCallback -= OnClientDisconnected;
			NetworkManager.Singleton.Shutdown();
			SceneManager.UnloadSceneAsync("SessionScene");
			SceneManager.LoadScene("LoginScene", LoadSceneMode.Additive);
		}

		void OnClientDisconnected(ulong a_id)
		{
			if (NetworkManager.Singleton.LocalClientId == a_id)
			{
				ReturnToLogin();
			}
		}
	}
}
