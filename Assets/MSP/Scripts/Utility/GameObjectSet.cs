using System.Collections;
using System.Collections.Generic;
using UnityEngine;


[CreateAssetMenu(fileName = "GameObjectSet", menuName = "MSP/GameObjectSet")]
public class GameObjectSet : ScriptableObject, IEnumerable<GameObject>
{
	private List<GameObject> m_gameobjects = new List<GameObject>();
	public int Count => m_gameobjects.Count;

	public void Add(GameObject a_gameObject)
	{
		if (!m_gameobjects.Contains(a_gameObject))
		{
			m_gameobjects.Add(a_gameObject);
		}
		else
		{
			Debug.LogErrorFormat("Set already contains GameObject with name: {0}.", a_gameObject.name);
		}
	}

	public void Remove(GameObject a_gameObject)
	{
		if (m_gameobjects.Contains(a_gameObject))
		{
			m_gameobjects.Remove(a_gameObject);
		}
		else
		{
			Debug.LogErrorFormat("Set does not contains GameObject with name: {0}.", a_gameObject.name);
		}
	}

	public void Clear()
	{
		m_gameobjects.Clear();
	}

	public GameObject this[int a_index]
	{
		get => m_gameobjects[a_index];
	}

	public IEnumerator<GameObject> GetEnumerator()
	{
		return m_gameobjects.GetEnumerator();
	}

	IEnumerator IEnumerable.GetEnumerator()
	{
		return GetEnumerator();
	}
}
