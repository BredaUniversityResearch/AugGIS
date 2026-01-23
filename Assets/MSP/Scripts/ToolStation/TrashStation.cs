using Sirenix.OdinInspector;
using Unity.Netcode;
using UnityEngine;

public class TrashStation : NetworkBehaviour
{
	[SerializeField]
	[Required]
	private TransformReference m_rootTransformReference;

	[SerializeField]
	private GameObjectSet m_set;

	public override void OnNetworkSpawn()
	{
		base.OnNetworkSpawn();

		m_set.Add(gameObject);

		if (IsSpawned)
		{
			transform.parent = m_rootTransformReference.TransformRef;
		}
	}

	public override void OnNetworkDespawn()
	{
		base.OnNetworkDespawn();

		m_set.Remove(gameObject);

		if (!IsSpawned && !IsServer)
		{
			Destroy(gameObject);
		}
	}
}
