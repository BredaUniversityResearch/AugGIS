using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public class LoadSceneOnStart : MonoBehaviour
{
    [SerializeField] string m_sceneName;
    [SerializeField] LoadSceneMode  m_loadType = LoadSceneMode.Additive;

    private void Start()
    {
        SceneManager.LoadScene(m_sceneName, m_loadType);
        Destroy(gameObject);
    }
}
